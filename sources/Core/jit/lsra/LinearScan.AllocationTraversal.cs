// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void allocateRegisters()
    {
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        JITDUMP("*************** In LinearScan::allocateRegisters()\n");
#if DEBUG
        if (VERBOSE)
        {
            dumpLsraIntervals("before allocateRegisters");
        }
#endif

        foreach (var interval in intervals)
        {
            interval.recentRefPosition = null;
            interval.isActive = false;
            if (interval.isLocalVar && !stressInitialParamReg())
            {
                var local = interval.getLocalVar(_compiler);
                if (local.lvIsRegArg && (interval.firstRefPosition is not null) && !_compiler.opts.IsOSR)
                {
                    interval.isActive = true;
                }
            }
        }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        if (_enregisterLocalVars)
        {
            _ = VarSetOps.VisitBits(_compiler, _largeVectorVars, variableIndex =>
            {
                getIntervalForLocalVar(checked((uint)variableIndex)).isPartiallySpilled = false;
                return true;
            });
        }
#endif

        initializeAvailableRegs();
        _regsBusyUntilKill = RBM_NONE;
        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            var register = getRegisterRecord(_regIndices[index]);
            register.recentRefPosition = null;
            updateNextFixedRef(register, register.firstRefPosition, _killHead);
            if (register.assignedInterval is Interval incoming)
            {
                updateNextIntervalRef(register.regNum, incoming);
                updateSpillCost(register.regNum, incoming);
                setRegInUse(register.regNum, incoming.registerType);
            }
            else
            {
                clearNextIntervalRef(register.regNum, register.registerType);
                clearSpillCost(register.regNum, register.registerType);
            }
        }

#if DEBUG
        if (VERBOSE)
        {
            dumpRefPositions("BEFORE ALLOCATION");
            dumpVarRefPositions("BEFORE ALLOCATION");
            initializeAllocationDumpFormat();
        }
#endif

        BasicBlock? currentBlock = null;
        var nextKill = _killHead;
        var previousLocation = MinLocation;
        var registersToFree = RBM_NONE;
        var delayedRegistersToFree = RBM_NONE;
        var registersToMakeInactive = RBM_NONE;
        var delayedRegistersToMakeInactive = RBM_NONE;
        var copyRegistersToFree = RBM_NONE;
        _regsInUseThisLocation = RBM_NONE;
        _regsInUseNextLocation = RBM_NONE;
        RefPosition? lastAllocatedRefPosition = null;
        var handledBlockEnd = false;

        foreach (var reference in refPositions)
        {
            var nextReference = reference.nextRefPosition;
            var registersToInactivate = registersToMakeInactive | delayedRegistersToMakeInactive;
            while (tryPopRegister(ref registersToInactivate, out var registerNumber))
            {
                var register = getRegisterRecord(registerNumber);
                clearSpillCost(register.regNum, register.registerType);
                makeRegisterInactive(register);
            }

            if (reference.nodeLocation > previousLocation)
            {
                makeRegsAvailable(registersToMakeInactive);
                registersToMakeInactive = delayedRegistersToMakeInactive;
                delayedRegistersToMakeInactive = RBM_NONE;
            }

#if DEBUG
            _activeRefPosition = null;
            if (VERBOSE)
            {
                dumpAllocationRegisterRecords();
            }
            if (spillAlways() && (lastAllocatedRefPosition is not null) &&
                !lastAllocatedRefPosition.IsPhysRegRef() &&
                !lastAllocatedRefPosition.getInterval().isInternal &&
                (!lastAllocatedRefPosition.RegOptional() ||
                    (lastAllocatedRefPosition.registerAssignment != SRBM_NONE)) &&
                (RefTypeIsDef(lastAllocatedRefPosition.refType) ||
                    lastAllocatedRefPosition.getInterval().isLocalVar))
            {
                assert(lastAllocatedRefPosition.registerAssignment != SRBM_NONE);
                var assigned = lastAllocatedRefPosition.getInterval().assignedReg
                    ?? throw new FatalJitException("Stress spilling requires an assigned register.");
                _activeRefPosition = lastAllocatedRefPosition;
                unassignPhysReg(assigned, lastAllocatedRefPosition);
                _activeRefPosition = null;
                lastAllocatedRefPosition = null;
            }
#endif

            var location = reference.nodeLocation;
            _currentAllocationLocation = location;
            if (location > previousLocation)
            {
                makeRegsAvailable(copyRegistersToFree);
                copyRegistersToFree = RBM_NONE;
                _regsInUseThisLocation = _regsInUseNextLocation;
                _regsInUseNextLocation = RBM_NONE;

                if ((registersToFree | delayedRegistersToFree).IsNonEmpty)
                {
                    freeRegisters(registersToFree);
                    if ((location > previousLocation + 1) && delayedRegistersToFree.IsNonEmpty)
                    {
                        assert(false, "A delayed register free has no reference at its target location.");
                        freeRegisters(delayedRegistersToFree);
                        delayedRegistersToFree = RBM_NONE;
                        _regsInUseThisLocation = RBM_NONE;
                    }
                    registersToFree = delayedRegistersToFree;
                    delayedRegistersToFree = RBM_NONE;
#if DEBUG
                    verifyFreeRegisters(registersToFree);
#endif
                }
            }
            previousLocation = location;

            var referent = reference.referent;
            var refType = reference.refType;
            RefPosition? previousReference = null;
            if (referent is not null)
            {
                previousReference = referent.recentRefPosition;
                referent.recentRefPosition = reference;
            }
            else
            {
                assert(refType is RefType.RefTypeBB or RefType.RefTypeKill or RefType.RefTypeKillGCRefs);
            }

#if DEBUG
            _activeRefPosition = reference;
            if ((refType is RefType.RefTypeBB) && handledBlockEnd && VERBOSE)
            {
                dumpMinimalNewBlock(currentBlock, location, reference);
            }
#endif
            // Dummy definitions precede the boundary in resolution order, but allocation
            // first releases dead locals from the previous block.
            if (!handledBlockEnd && (refType is RefType.RefTypeBB or RefType.RefTypeDummyDef))
            {
                freeRegisters(registersToFree);
                registersToFree = RBM_NONE;
                _regsInUseThisLocation = RBM_NONE;
                _regsInUseNextLocation = RBM_NONE;
                handledBlockEnd = true;
                setCurrentBlockStartLocation(location);
                if (currentBlock is null)
                {
                    currentBlock = startBlockSequence();
                }
                else
                {
                    if (_enregisterLocalVars)
                    {
                        processBlockEndAllocationWithLocals(currentBlock);
                    }
                    else
                    {
                        processBlockEndAllocation(currentBlock);
                    }
                    currentBlock = moveToNextBlock();
                }
#if DEBUG
                dumpMinimalAllocationEvent(
                    MinimalAllocationEvent.START_BB, reference, block: currentBlock);
                if ((refType is RefType.RefTypeDummyDef) && VERBOSE)
                {
                    dumpAllocationRegisterRecords();
                }
#endif
            }

            if (refType is RefType.RefTypeBB)
            {
                handledBlockEnd = false;
                continue;
            }
            if (refType is RefType.RefTypeKill)
            {
                assert(ReferenceEquals(nextKill, reference));
                processKills(reference);
                nextKill = reference.nextRefPosition;
                continue;
            }
            if (refType is RefType.RefTypeKillGCRefs)
            {
                spillGCRefs(reference);
                continue;
            }
            if (refType is RefType.RefTypeFixedReg)
            {
                var fixedRegister = reference.getReg();
                var assignedInterval = fixedRegister.assignedInterval;
                updateNextFixedRef(fixedRegister, nextReference, nextKill);
                if ((assignedInterval is not null) && !assignedInterval.isActive &&
                    assignedInterval.isConstant)
                {
                    clearConstantReg(fixedRegister.regNum);
                    fixedRegister.assignedInterval = null;
                    clearSpillCost(fixedRegister.regNum, assignedInterval.registerType);
                }
                _regsInUseThisLocation |= createRegisterMask(reference.registerAssignment,
                    fixedRegister.registerType);
#if DEBUG
                dumpMinimalAllocationEvent(MinimalAllocationEvent.FIXED_REG, reference,
                    register: reference.assignedReg());
#endif
#if SWIFT_SUPPORT
                if (reference.delayRegFree)
                {
                    _regsInUseNextLocation |= createRegisterMask(reference.registerAssignment,
                        fixedRegister.registerType);
                }
#endif
                continue;
            }

            assert(reference.isIntervalRef());
            var interval = reference.getInterval();
            if (refType is RefType.RefTypeExpUse)
            {
#if DEBUG
                dumpFullAllocationEvent(FullAllocationEvent.EXP_USE, reference);
#endif
                if (interval.physReg != REG_NA)
                {
                    updateNextIntervalRef(interval.physReg, interval);
                }
                continue;
            }

            var assignedRegister = interval.physReg;
            var allocate = true;
#if DEBUG
            var didDump = false;
#endif
            if (refType is RefType.RefTypeParamDef or RefType.RefTypeZeroInit)
            {
                if (nextReference is null)
                {
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.ZERO_REF, reference, interval);
#endif
                    reference.lastUse = true;
                }
                var blockInfo = _blockInfo
                    ?? throw new FatalJitException("Entry allocation requires block information.");
                var firstBlock = _compiler.fgFirstBB
                    ?? throw new FatalJitException("Entry allocation requires a first block.");
                var local = interval.getLocalVar(_compiler);
                assert(!blockInfo[firstBlock.bbNum].hasEHBoundaryIn || interval.isWriteThru);
                if (blockInfo[firstBlock.bbNum].hasEHBoundaryIn ||
                    blockInfo[firstBlock.bbNum].hasEHPred)
                {
                    allocate = false;
                }
                else if ((refType is RefType.RefTypeParamDef) &&
                    (local.lvRefCntWtd() <= BB_UNITY_WEIGHT) &&
                    (!reference.lastUse || (interval.physReg == REG_STK)))
                {
                    allocate = false;
                }
                else if ((interval.physReg == REG_STK) &&
                    ((nextReference ?? throw new FatalJitException(
                        "A stack entry definition requires a next reference.")).treeNode
                        ?? throw new FatalJitException(
                            "The next stack entry reference requires a tree.")).Oper is GT_BITCAST)
                {
                    allocate = false;
                }
                else if (interval.isWriteThru && (refType is RefType.RefTypeZeroInit))
                {
                    allocate = false;
                }
                if (!allocate)
                {
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.NO_ENTRY_REG_ALLOCATED,
                        reference, interval);
                    didDump = true;
#endif
                    setIntervalAsSpilled(interval);
                    if (assignedRegister != REG_NA)
                    {
                        clearNextIntervalRef(assignedRegister, interval.registerType);
                        clearSpillCost(assignedRegister, interval.registerType);
                        makeRegAvailable(assignedRegister, interval.registerType);
                    }
                }
            }
#if FEATURE_SIMD && FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            else if (interval.isUpperVector)
            {
                var localInterval = interval.relatedInterval
                    ?? throw new FatalJitException("Upper-vector references require a related local.");
                assert(localInterval.isLocalVar);
                if (refType is RefType.RefTypeUpperVectorSave)
                {
                    var extraSave = reference.IsExtraUpperVectorSave();
                    var skipSave = canSkipUpperVectorSave(reference, localInterval);
                    if ((localInterval.physReg == REG_NA) || extraSave || skipSave ||
                        (localInterval.isPartiallySpilled && interval.physReg == REG_STK))
                    {
                        if (!reference.liveVarUpperSave || skipSave)
                        {
                            if (extraSave)
                            {
                                reference.skipSaveRestore = true;
                            }
                            else if (skipSave)
                            {
                                reference.skipSaveRestore = true;
                                assert(nextReference?.refType is RefType.RefTypeUpperVectorRestore);
                                (nextReference ?? throw new FatalJitException(
                                    "An upper-vector save requires a matching restore.")).skipSaveRestore = true;
                            }
                            if (assignedRegister != REG_NA)
                            {
                                if (interval.isActive)
                                {
                                    unassignPhysRegNoSpill(getRegisterRecord(assignedRegister));
                                }
                                else
                                {
                                    updateNextIntervalRef(assignedRegister, interval);
                                    updateSpillCost(assignedRegister, interval);
                                }
                                registersToFree |= createRegisterMask(
                                    genSingleTypeRegMask(assignedRegister), interval.registerType);
                            }
                            reference.registerAssignment = SRBM_NONE;
                            lastAllocatedRefPosition = reference;
#if DEBUG
                            dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED,
                                reference, register: assignedRegister);
#endif
                            continue;
                        }
                        assert(!extraSave);
                        allocate = false;
                    }
                    else if (localInterval.registerType is TYP_SIMD64)
                    {
                        allocate = false;
                        localInterval.isPartiallySpilled = true;
                    }
                    else
                    {
                        localInterval.isPartiallySpilled = true;
                    }
                }
                else if (refType is RefType.RefTypeUpperVectorRestore)
                {
                    if (localInterval.isPartiallySpilled)
                    {
                        localInterval.isPartiallySpilled = false;
                    }
                    else
                    {
                        allocate = false;
                    }
                }
            }
            else if (refType is RefType.RefTypeUpperVectorSave)
            {
                if (assignedRegister != REG_NA)
                {
                    var register = getRegisterRecord(assignedRegister);
                    var assigned = register.assignedInterval
                        ?? throw new FatalJitException("Upper-vector save requires an assigned interval.");
                    unassignPhysReg(register, interval.firstRefPosition);
                    if (assigned.isConstant)
                    {
                        clearConstantReg(assignedRegister);
                    }
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED, reference, interval);
#endif
                }
                reference.registerAssignment = SRBM_NONE;
                continue;
            }
#endif
            if (!allocate)
            {
                if (assignedRegister != REG_NA)
                {
                    unassignPhysReg(getRegisterRecord(assignedRegister), reference);
                }
#if DEBUG
                else if (!didDump)
                {
                    dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED, reference, interval);
                }
#endif
                reference.registerAssignment = SRBM_NONE;
                continue;
            }

            if (interval.isSpecialPutArg)
            {
                var source = interval.relatedInterval
                    ?? throw new FatalJitException("Special putarg requires a source local.");
                assert(source.isLocalVar);
                if (refType is RefType.RefTypeDef)
                {
                    assert(source.recentRefPosition?.nodeLocation == location - 1);
                    if (source.isActive &&
                        (genSingleTypeRegMask(source.physReg) == reference.registerAssignment) &&
                        (interval.getNextRefLocation() == _nextFixedRef[(int)source.physReg]))
                    {
                        // The source local must survive unchanged until the putarg's fixed-register kill.
                        _regsBusyUntilKill |= createRegisterMask(
                            genSingleTypeRegMask(source.physReg), source.registerType);
                    }
                    else
                    {
                        interval.isSpecialPutArg = false;
                    }
                }
                if (interval.isSpecialPutArg)
                {
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.SPECIAL_PUTARG,
                        reference, interval, reference.assignedReg());
#endif
                    continue;
                }
            }

            if ((assignedRegister == REG_NA) && RefTypeIsUse(refType))
            {
                reference.reload = true;
#if DEBUG
                dumpFullAllocationEvent(FullAllocationEvent.RELOAD, reference, interval);
#endif
            }
            var assignedBit = SRBM_NONE;
            var isInRegister = false;
            if (assignedRegister != REG_NA)
            {
                isInRegister = true;
                assignedBit = genSingleTypeRegMask(assignedRegister);
                if (!interval.isActive)
                {
                    if (RefTypeIsUse(refType))
                    {
                        var inMap = getInVarToRegMap(_currentBlockNumber)
                            ?? throw new FatalJitException("Exposed use requires an incoming local map.");
                        assert(_enregisterLocalVars &&
                            (inMap[interval.getVarIndex(_compiler)] == REG_STK) &&
                            (previousReference is not null) &&
                            (_currentBlockStartLocation is not null) &&
                            (previousReference.nodeLocation <= _currentBlockStartLocation.Value));
                        isInRegister = false;
                    }
                    else
                    {
                        interval.isActive = true;
                        setRegInUse(assignedRegister, interval.registerType);
                        updateSpillCost(assignedRegister, interval);
                    }
                    updateNextIntervalRef(assignedRegister, interval);
                }
                assert((interval.assignedReg is not null) &&
                    (interval.assignedReg.regNum == assignedRegister) &&
                    ReferenceEquals(interval.assignedReg.assignedInterval, interval));
            }

            if (previousReference is not null)
            {
                assert(ReferenceEquals(previousReference.nextRefPosition, reference));
                assert((assignedRegister == REG_NA) ||
                    (assignedBit == previousReference.registerAssignment) ||
                    reference.outOfOrder || previousReference.copyReg ||
                    (previousReference.refType is RefType.RefTypeExpUse) ||
                    (refType is RefType.RefTypeDummyDef));
            }
            else if ((assignedRegister != REG_NA) && interval.isLocalVar)
            {
                var preferences = interval.registerPreferences;
                var matchesPreferences = (preferences & assignedBit) != SRBM_NONE;
                var keepAssignment = true;
                var nextFixedLocation = _nextFixedRef[(int)assignedRegister];
                var lastReference = interval.lastRefPosition
                    ?? throw new FatalJitException("A preassigned local requires a last reference.");
                if (nextFixedLocation <= lastReference.nodeLocation)
                {
                    var followingReference = nextReference
                        ?? throw new FatalJitException("A conflicting preassigned local requires a next reference.");
                    if (!matchesPreferences || (nextFixedLocation < followingReference.nodeLocation) ||
                        ((followingReference.registerAssignment != assignedBit) &&
                            (nextFixedLocation <= followingReference.getRefEndLocation())))
                    {
                        keepAssignment = false;
                    }
                }
                else if ((refType is RefType.RefTypeParamDef) && !matchesPreferences)
                {
                    keepAssignment = false;
                }
                if (!keepAssignment)
                {
                    var register = getRegisterRecord(interval.physReg);
                    reference.registerAssignment = allRegs(interval.registerType);
                    reference.isFixedRegRef = false;
                    unassignPhysRegNoSpill(register);
                    interval.registerPreferences = interval.registerPreferences == assignedBit
                        ? reference.registerAssignment
                        : interval.registerPreferences & ~assignedBit;
                    assignedRegister = REG_NA;
                    assignedBit = SRBM_NONE;
                }
            }

            if (assignedRegister != REG_NA)
            {
                var register = getRegisterRecord(assignedRegister);
                assert((assignedBit == reference.registerAssignment) ||
                    ReferenceEquals(register.assignedInterval, interval) ||
                    !isRegInUse(assignedRegister, interval.registerType));
                if (conflictingFixedRegReference(assignedRegister, reference))
                {
                    if (ReferenceEquals(register.assignedInterval, interval))
                    {
                        unassignPhysRegNoSpill(register);
                        clearConstantReg(assignedRegister);
                    }
                    reference.moveReg = true;
                    assignedRegister = REG_NA;
                    reference.registerAssignment &= ~assignedBit;
                    setIntervalAsSplitMinimal(interval);
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.MOVE_REG, reference, interval);
#endif
                }
                else if ((assignedBit & reference.registerAssignment) != SRBM_NONE)
                {
                    reference.registerAssignment = assignedBit;
                    if (!interval.isActive)
                    {
                        if (refType is RefType.RefTypeDummyDef)
                        {
                            interval.isActive = true;
                            assert(ReferenceEquals(register.assignedInterval, interval));
                        }
                        else
                        {
                            reference.reload = true;
                        }
                    }
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.KEPT_ALLOCATION,
                        reference, interval, assignedRegister);
#endif
                }
                else if (!RefTypeIsDef(refType))
                {
                    var copyRegister = assignCopyReg(reference);
                    lastAllocatedRefPosition = reference;
                    var copyMask = genSingleTypeRegMask(copyRegister);
                    var originalMask = genSingleTypeRegMask(assignedRegister);
                    updateRegsFreeBusyState(reference, interval.registerType, originalMask | copyMask,
                        ref registersToFree, ref delayedRegistersToFree, interval, assignedRegister);
                    if (!reference.lastUse)
                    {
                        copyRegistersToFree |= createRegisterMask(copyMask, interval.registerType);
                    }
                    if (!interval.isLocalVar)
                    {
                        reference.moveReg = true;
                        reference.copyReg = false;
                    }
                    clearNextIntervalRef(copyRegister, interval.registerType);
                    clearSpillCost(copyRegister, interval.registerType);
                    updateNextIntervalRef(assignedRegister, interval);
                    updateSpillCost(assignedRegister, interval);
                    continue;
                }
                else
                {
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.NEEDS_NEW_REG,
                        reference, register: assignedRegister);
#endif
                    registersToFree |= createRegisterMask(assignedBit, interval.registerType);
                    assignedRegister = REG_NA;
                    if (ReferenceEquals(register.assignedInterval, interval))
                    {
                        unassignPhysRegNoSpill(register);
                    }
                }
            }

            if (assignedRegister == REG_NA)
            {
                if (reference.RegOptional())
                {
                    if (reference.lastUse && reference.reload)
                    {
                        allocate = false;
                    }
                    else if (interval.isWriteThru &&
                        ((nextReference is null) || nextReference.nodeLocation >= _firstColdLocation))
                    {
                        allocate = false;
                    }
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                    if ((refType is RefType.RefTypeUpperVectorRestore) && (interval.physReg == REG_NA))
                    {
                        allocate = false;
                    }
#endif
#if DEBUG
                    if (allocate && regOptionalNoAlloc())
                    {
                        allocate = false;
                    }
#endif
                }
                if (allocate)
                {
                    if (reference.isFixedRegRef && !interval.isActive &&
                        (interval.assignedReg is RegRecord assigned) &&
                        ReferenceEquals(assigned.assignedInterval, interval) &&
                        (genSingleTypeRegMask(assigned.regNum) != reference.registerAssignment))
                    {
                        unassignPhysReg(assigned, (RefPosition?)null);
                    }
                    assignedRegister = allocateReg(interval, reference, out var selectionScore);
#if DEBUG
                    if (assignedRegister != REG_NA)
                    {
                        var allocationEvent = interval.isConstant &&
                            (reference.treeNode?.IsReuseRegVal == true)
                            ? FullAllocationEvent.REUSE_REG
                            : FullAllocationEvent.ALLOC_REG;
                        dumpFullAllocationEvent(allocationEvent, reference, interval,
                            assignedRegister, selectionScore);
                    }
#endif
                }
                if (assignedRegister == REG_NA)
                {
                    assert(reference.RegOptional());
#if DEBUG
                    dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED, reference, interval);
#endif
                    reference.registerAssignment = SRBM_NONE;
                    reference.reload = false;
                    interval.isActive = false;
                    setIntervalAsSpilled(interval);
                }
                if ((refType is RefType.RefTypeDummyDef) && (assignedRegister != REG_NA))
                {
                    setInVarRegForBB(_currentBlockNumber, interval.varNum, assignedRegister);
                }
                if ((assignedRegister != REG_NA) && RefTypeIsUse(refType) && !isInRegister)
                {
                    assert(reference.reload);
                }
            }

            if (assignedRegister != REG_NA)
            {
                assignedBit = genSingleTypeRegMask(assignedRegister);
                var registerMask = createRegisterMask(assignedBit, interval.registerType);
                _regsInUseThisLocation |= registerMask;
                if (reference.delayRegFree)
                {
                    _regsInUseNextLocation |= registerMask;
                }
                reference.registerAssignment = assignedBit;
                interval.physReg = assignedRegister;
                removeRegisterSetForType(ref registersToFree, assignedBit, interval.registerType);
                var unassign = false;
                if (!interval.IsUpperVector())
                {
                    if (interval.isWriteThru)
                    {
                        if (refType is RefType.RefTypeDef)
                        {
                            reference.writeThru = true;
                        }
                        if (!reference.lastUse && reference.spillAfter)
                        {
                            unassign = true;
                        }
                    }
                    if (reference.lastUse || nextReference is null)
                    {
                        if (nextReference is null)
                        {
                            unassign = true;
                        }
                        else
                        {
                            if (reference.delayRegFree)
                            {
                                delayedRegistersToMakeInactive |= registerMask;
                            }
                            else
                            {
                                registersToMakeInactive |= registerMask;
                            }
                            interval.isActive = false;
                        }
                        interval.relatedInterval?.updateRegisterPreferences(assignedBit);
                    }
                    if (unassign)
                    {
                        if (reference.delayRegFree)
                        {
                            delayedRegistersToFree |= registerMask;
                        }
                        else
                        {
                            registersToFree |= registerMask;
                        }
                    }
                }
                if (!unassign)
                {
                    updateNextIntervalRef(assignedRegister, interval);
                    updateSpillCost(assignedRegister, interval);
                }
            }
            lastAllocatedRefPosition = reference;
        }

#if DEBUG
        if (extendLifetimes())
        {
            for (var index = 0; index <= (int)REG_FP_LAST; index++)
            {
                var register = physRegs[index];
                if (register.assignedInterval is Interval interval)
                {
                    interval.isActive = false;
                    unassignPhysReg(register, (RefPosition?)null);
                }
            }
        }
        else
#endif
        {
            freeRegisters(registersToFree | delayedRegistersToFree);
        }
#if DEBUG
        if (VERBOSE)
        {
            dumpAllocationRegisterRecords();
            jitprintf("\n");
            dumpRefPositions("AFTER ALLOCATION");
            dumpVarRefPositions("AFTER ALLOCATION");
            dumpActiveIntervalsAtEnd();
        }
#endif
#else
        NYI("Full LSRA allocation traversal outside Windows AMD64");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("Full LSRA allocation traversal outside Windows AMD64.");
#endif
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE && TARGET_AMD64
    private static bool canSkipUpperVectorSave(RefPosition reference, Interval localInterval)
    {
        assert(reference.refType is RefType.RefTypeUpperVectorSave);
        // The AMD64 tail-call profiler stub does not preserve the upper YMM halves.
        return false;
    }
#endif
}
