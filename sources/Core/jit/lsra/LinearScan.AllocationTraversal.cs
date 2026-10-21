// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void allocateRegisters()
    {
        JITDUMP("*************** In LinearScan::allocateRegisters()\n");
        if (Configuration.DEBUG && VERBOSE)
        {
            dumpLsraIntervals("before allocateRegisters");
        }

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

        if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE && _enregisterLocalVars)
        {
            _ = VarSetOps.VisitBits(_compiler, _largeVectorVars, variableIndex => {
                getIntervalForLocalVar(checked((uint)variableIndex)).isPartiallySpilled = false;
                return true;
            });
        }

        initializeAvailableRegs();
        _regsBusyUntilKill = RBM_NONE;
        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            var register = getRegisterRecord(_regIndices[index]);
            register.recentRefPosition = null;
            updateNextFixedRef(register, register.firstRefPosition, _killHead);
            if (register.assignedInterval is Interval incoming)
            {
                if (Configuration.TARGET_ARM && incoming.registerType is TYP_DOUBLE &&
                    !genIsValidDoubleReg(register.regNum))
                {
                    continue;
                }
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

        if (Configuration.DEBUG && VERBOSE)
        {
            dumpRefPositions("BEFORE ALLOCATION");
            dumpVarRefPositions("BEFORE ALLOCATION");
            initializeAllocationDumpFormat();
        }

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
        _consecutiveRegsInUseThisLocation = SRBM_NONE;
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

            if (Configuration.DEBUG)
            {
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
                    var assigned = lastAllocatedRefPosition.getInterval().assignedReg;
                    if (assigned is null)
                    {
                        FatalJitException.ThrowFatalJitException("Stress spilling requires an assigned register.");
                        return;
                    }
                    _activeRefPosition = lastAllocatedRefPosition;
                    unassignPhysReg(assigned, lastAllocatedRefPosition);
                    _activeRefPosition = null;
                    lastAllocatedRefPosition = null;
                }
            }

            var location = reference.nodeLocation;
            _currentAllocationLocation = location;
            if (location > previousLocation)
            {
                _consecutiveRegsInUseThisLocation = SRBM_NONE;
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
                    if (Configuration.DEBUG)
                    {
                        verifyFreeRegisters(registersToFree);
                    }
                }
            }
            previousLocation = location;

            if (Configuration.DEBUG && Configuration.TARGET_ARM64 &&
                _compiler.info.compNeedsConsecutiveRegisters)
            {
                if (reference.needsConsecutive)
                {
                    _consecutiveRegistersLocation = location;
                }
                else if (_consecutiveRegistersLocation < location)
                {
                    _consecutiveRegistersLocation = MinLocation;
                }
            }
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

            if (Configuration.DEBUG)
            {
                _activeRefPosition = reference;
                if ((refType is RefType.RefTypeBB) && handledBlockEnd && VERBOSE)
                {
                    dumpMinimalNewBlock(currentBlock, location, reference);
                }
            }
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
                dumpMinimalAllocationEvent(
                    MinimalAllocationEvent.START_BB, reference, block: currentBlock);
                if ((refType is RefType.RefTypeDummyDef) && VERBOSE)
                {
                    dumpAllocationRegisterRecords();
                }
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
                    clearConstantReg(fixedRegister.regNum, assignedInterval.registerType);
                    fixedRegister.assignedInterval = null;
                    clearSpillCost(fixedRegister.regNum, assignedInterval.registerType);
                    if (Configuration.TARGET_ARM && assignedInterval.registerType is TYP_DOUBLE)
                    {
                        var otherHalf = findAnotherHalfRegRec(fixedRegister);
                        assert(ReferenceEquals(otherHalf.assignedInterval, assignedInterval));
                        otherHalf.assignedInterval = null;
                        clearSpillCost(otherHalf.regNum, assignedInterval.registerType);
                    }
                }
                _regsInUseThisLocation |= createRegisterMask(reference.registerAssignment,
                    fixedRegister.registerType);
                dumpMinimalAllocationEvent(MinimalAllocationEvent.FIXED_REG, reference,
                    register: reference.assignedReg());
                if (Configuration.SWIFT_SUPPORT && reference.delayRegFree)
                {
                    _regsInUseNextLocation |= createRegisterMask(reference.registerAssignment,
                        fixedRegister.registerType);
                }
                continue;
            }

            assert(reference.isIntervalRef());
            var interval = reference.getInterval();
            if (refType is RefType.RefTypeExpUse)
            {
                dumpFullAllocationEvent(FullAllocationEvent.EXP_USE, reference);
                if (interval.physReg != REG_NA)
                {
                    updateNextIntervalRef(interval.physReg, interval);
                }
                continue;
            }

            var assignedRegister = interval.physReg;
            var allocate = true;
            var needsConsecutive = Configuration.TARGET_ARM64 &&
                _compiler.info.compNeedsConsecutiveRegisters && reference.needsConsecutive;
            var didDump = false;
            if (refType is RefType.RefTypeParamDef or RefType.RefTypeZeroInit)
            {
                if (nextReference is null)
                {
                    dumpFullAllocationEvent(FullAllocationEvent.ZERO_REF, reference, interval);
                    reference.lastUse = true;
                }
                var blockInfo = _blockInfo;
                if (blockInfo is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage261
                    );
                    return;
                }
                var firstBlock = _compiler.fgFirstBB;
                if (firstBlock is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage262
                    );
                    return;
                }
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
                else if (interval.physReg == REG_STK)
                {
                    var nextRef = nextReference;
                    if (nextRef is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage263
                        );
                        return;
                    }

                    var nextTree = nextRef.treeNode;
                    if (nextTree is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage264
                        );
                        return;
                    }

                    if (nextTree.Oper is GT_BITCAST)
                    {
                        allocate = false;
                    }
                    else if (interval.isWriteThru && (refType is RefType.RefTypeZeroInit))
                    {
                        allocate = false;
                    }
                }
                else if (interval.isWriteThru && (refType is RefType.RefTypeZeroInit))
                {
                    allocate = false;
                }
                if (!allocate)
                {
                    if (Configuration.DEBUG)
                    {
                        dumpFullAllocationEvent(FullAllocationEvent.NO_ENTRY_REG_ALLOCATED,
                            reference, interval);
                        didDump = true;
                    }
                    setIntervalAsSpilled(interval);
                    if (assignedRegister != REG_NA)
                    {
                        clearNextIntervalRef(assignedRegister, interval.registerType);
                        clearSpillCost(assignedRegister, interval.registerType);
                        makeRegAvailable(assignedRegister, interval.registerType);
                    }
                }
            }
            else if (Configuration.FEATURE_SIMD &&
                Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE && interval.isUpperVector)
            {
                var localInterval = interval.relatedInterval;
                if (localInterval is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage265
                    );
                    return;
                }
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
                                var matchingRestore = nextReference;
                                if (matchingRestore is null)
                                {
                                    FatalJitException.ThrowFatalJitException(
                                        "An upper-vector save requires a matching restore.");
                                    return;
                                }

                                matchingRestore.skipSaveRestore = true;
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
                                    getSingleTypeRegMask(assignedRegister, interval.registerType),
                                    interval.registerType);
                            }
                            reference.registerAssignment = SRBM_NONE;
                            lastAllocatedRefPosition = reference;
                            dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED,
                                reference, register: assignedRegister);
                            continue;
                        }
                        assert(!extraSave);
                        allocate = false;
                    }
                    else if (Configuration.TARGET_XARCH && localInterval.registerType is TYP_SIMD64)
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
                    var assigned = register.assignedInterval;
                    if (assigned is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage266
                        );
                        return;
                    }
                    unassignPhysReg(register, interval.firstRefPosition);
                    if (assigned.isConstant)
                    {
                        clearConstantReg(assignedRegister, assigned.registerType);
                    }
                    dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED, reference, interval);
                }
                reference.registerAssignment = SRBM_NONE;
                continue;
            }
            if (!allocate)
            {
                if (assignedRegister != REG_NA)
                {
                    unassignPhysReg(getRegisterRecord(assignedRegister), reference);
                }
                else if (Configuration.DEBUG && !didDump)
                {
                    dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED, reference, interval);
                }
                reference.registerAssignment = SRBM_NONE;
                continue;
            }

            if (interval.isSpecialPutArg)
            {
                var source = interval.relatedInterval;
                if (source is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage267
                    );
                    return;
                }
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
                            getSingleTypeRegMask(source.physReg, source.registerType),
                            source.registerType);
                    }
                    else
                    {
                        interval.isSpecialPutArg = false;
                    }
                }
                if (interval.isSpecialPutArg)
                {
                    dumpFullAllocationEvent(FullAllocationEvent.SPECIAL_PUTARG,
                        reference, interval, reference.assignedReg());
                    continue;
                }
            }

            if ((assignedRegister == REG_NA) && RefTypeIsUse(refType))
            {
                reference.reload = true;
                dumpFullAllocationEvent(FullAllocationEvent.RELOAD, reference, interval);
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
                        var inMap = getInVarToRegMap(_currentBlockNumber);
                        if (inMap is null)
                        {
                            FatalJitException.ThrowFatalJitException(
                                CORJIT_INTERNALERROR,
                                FatalJitErrorCode.DirectFailureMessage268
                            );
                            return;
                        }
                        assert(_enregisterLocalVars &&
                            ((regNumber)inMap[interval.getVarIndex(_compiler)] == REG_STK) &&
                            (previousReference is not null) &&
                            (previousReference.nodeLocation <= _currentBlockStartLocation));
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
                var lastReference = interval.lastRefPosition;
                if (lastReference is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage269
                    );
                    return;
                }
                if (nextFixedLocation <= lastReference.nodeLocation)
                {
                    var followingReference = nextReference;
                    if (followingReference is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage270
                        );
                        return;
                    }
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
                        clearConstantReg(assignedRegister, interval.registerType);
                    }
                    reference.moveReg = true;
                    assignedRegister = REG_NA;
                    reference.registerAssignment &= ~assignedBit;
                    setIntervalAsSplitMinimal(interval);
                    dumpFullAllocationEvent(FullAllocationEvent.MOVE_REG, reference, interval);
                }
                else if ((assignedBit & reference.registerAssignment) != SRBM_NONE)
                {
                    if (needsConsecutive && reference.isFirstRefPositionOfConsecutiveRegisters())
                    {
                        if (canAssignNextConsecutiveRegisters(reference, assignedRegister))
                        {
                            reference.registerAssignment = assignedBit;
                            dumpFullAllocationEvent(FullAllocationEvent.KEPT_ALLOCATION,
                                reference, interval, assignedRegister);
                            assignConsecutiveRegisters(reference, assignedRegister);
                        }
                        else
                        {
                            var copy = assignCopyReg(reference, true);
                            assignConsecutiveRegisters(reference, copy);
                            if (copy != assignedRegister)
                            {
                                lastAllocatedRefPosition = reference;
                                var copyBit = getSingleTypeRegMask(copy, interval.registerType);
                                var oldBit = getSingleTypeRegMask(assignedRegister, interval.registerType);
                                if ((oldBit & _consecutiveRegsInUseThisLocation) != SRBM_NONE)
                                {
                                    oldBit = SRBM_NONE;
                                }
                                updateRegsFreeBusyState(reference, interval.registerType, oldBit | copyBit,
                                    ref registersToFree, ref delayedRegistersToFree, interval, assignedRegister);
                                if (!reference.lastUse)
                                {
                                    copyRegistersToFree |= createRegisterMask(copyBit, interval.registerType);
                                }
                                if (!interval.isLocalVar)
                                {
                                    reference.moveReg = true;
                                    reference.copyReg = false;
                                }
                                clearNextIntervalRef(copy, interval.registerType);
                                clearSpillCost(copy, interval.registerType);
                                updateNextIntervalRef(assignedRegister, interval);
                                updateSpillCost(assignedRegister, interval);
                            }
                            else
                            {
                                reference.copyReg = false;
                                reference.registerAssignment = assignedBit;
                            }
                            continue;
                        }
                    }
                    else
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
                        dumpFullAllocationEvent(FullAllocationEvent.KEPT_ALLOCATION,
                            reference, interval, assignedRegister);
                    }
                }
                else if (!RefTypeIsDef(refType))
                {
                    var copyRegister = assignCopyReg(
                        reference, needsConsecutive && refType is RefType.RefTypeUse);
                    lastAllocatedRefPosition = reference;
                    var copyMask = getSingleTypeRegMask(copyRegister, interval.registerType);
                    if (needsConsecutive && reference.isFirstRefPositionOfConsecutiveRegisters())
                    {
                        assignConsecutiveRegisters(reference, copyRegister);
                    }
                    var originalMask = getSingleTypeRegMask(assignedRegister, interval.registerType);
                    if (needsConsecutive && (_consecutiveRegsInUseThisLocation & originalMask) != SRBM_NONE)
                    {
                        originalMask = SRBM_NONE;
                    }
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
                    dumpFullAllocationEvent(FullAllocationEvent.NEEDS_NEW_REG,
                        reference, register: assignedRegister);
                    registersToFree |= createRegisterMask(
                        getSingleTypeRegMask(register.regNum, interval.registerType), interval.registerType);
                    assignedRegister = REG_NA;
                    if (ReferenceEquals(register.assignedInterval, interval))
                    {
                        unassignPhysRegNoSpill(register);
                    }
                }
            }

            if (needsConsecutive)
            {
                if (reference.isFirstRefPositionOfConsecutiveRegisters())
                {
                    if (assignedRegister != REG_NA &&
                        !canAssignNextConsecutiveRegisters(reference, assignedRegister))
                    {
                        assignedRegister = REG_NA;
                        reference.registerAssignment = allRegs(interval.registerType);
                    }
                }
                else if (refType is RefType.RefTypeUse)
                {
                    if (assignedBit == reference.registerAssignment)
                    {
                        allocate = false;
                    }
                    else
                    {
                        assignedRegister = REG_NA;
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
                    if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                        Configuration.TARGET_XARCH &&
                        (refType is RefType.RefTypeUpperVectorRestore) && (interval.physReg == REG_NA))
                    {
                        assert(reference.regOptional);
                        allocate = false;
                    }
                    if (Configuration.DEBUG && allocate && regOptionalNoAlloc())
                    {
                        allocate = false;
                    }
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
                    assignedRegister = allocateReg(
                        interval, reference, out var selectionScore, needsConsecutive);
                    if (needsConsecutive && reference.isFirstRefPositionOfConsecutiveRegisters())
                    {
                        assignConsecutiveRegisters(reference, assignedRegister);
                    }
                    if (assignedRegister != REG_NA)
                    {
                        var allocationEvent = interval.isConstant &&
                            (reference.treeNode?.IsReuseRegVal == true)
                            ? FullAllocationEvent.REUSE_REG
                            : FullAllocationEvent.ALLOC_REG;
                        dumpFullAllocationEvent(allocationEvent, reference, interval,
                            assignedRegister, selectionScore);
                    }
                }
                if (assignedRegister == REG_NA)
                {
                    assert(reference.RegOptional());
                    dumpFullAllocationEvent(FullAllocationEvent.NO_REG_ALLOCATED, reference, interval);
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
                var registerTypeMask = getSingleTypeRegMask(assignedRegister, interval.registerType);
                var registerMask = createRegisterMask(registerTypeMask, interval.registerType);
                _regsInUseThisLocation |= registerMask;
                if (reference.delayRegFree)
                {
                    _regsInUseNextLocation |= registerMask;
                }
                reference.registerAssignment = assignedBit;
                interval.physReg = assignedRegister;
                removeRegisterSetForType(ref registersToFree, registerTypeMask, interval.registerType);
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

        if (Configuration.JIT32_GCENCODER && _compiler.lvaKeepAliveAndReportThis())
        {
            ref var thisLocal = ref _compiler.lvaGetDesc(_compiler.info.compThisArg);
            if (thisLocal.lvLRACandidate)
            {
                var interval = getIntervalForLocalVar(thisLocal._varIndex);
                if (interval.isSplit)
                {
                    setIntervalAsSpilled(interval);
                }
                if (interval.isSpilled)
                {
                    var previousBlockNumber = 0u;
                    for (var reference = interval.firstRefPosition; reference is not null;
                        reference = reference.nextRefPosition)
                    {
                        if (RefTypeIsUse(reference.refType) && reference.bbNum != previousBlockNumber)
                        {
                            var inMap = getInVarToRegMap(reference.bbNum);
                            if (inMap is null)
                            {
                                FatalJitException.ThrowFatalJitException("A live this interval requires an incoming map.");
                                return;
                            }
                            setVarReg(inMap, thisLocal._varIndex, REG_STK);
                        }

                        if (reference.RegOptional())
                        {
                            reference.registerAssignment = SRBM_NONE;
                            reference.reload = false;
                            reference.spillAfter = false;
                        }

                        switch (reference.refType)
                        {
                            case RefType.RefTypeDef:
                            {
                                if (reference.registerAssignment != SRBM_NONE)
                                {
                                    reference.spillAfter = true;
                                }
                                break;
                            }

                            case RefType.RefTypeUse:
                            {
                                if (reference.registerAssignment != SRBM_NONE)
                                {
                                    reference.reload = true;
                                    reference.spillAfter = true;
                                    reference.copyReg = false;
                                    reference.moveReg = false;
                                }
                                break;
                            }
                        }

                        previousBlockNumber = reference.bbNum;
                    }
                }
            }
        }

        if (Configuration.DEBUG && extendLifetimes())
        {
            for (var index = 0; index <= (int)TargetRuntimeMetadata.FloatingRegisterLast; index++)
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
        {
            freeRegisters(registersToFree | delayedRegistersToFree);
        }
        if (Configuration.DEBUG && VERBOSE)
        {
            dumpAllocationRegisterRecords();
            jitprintf("\n");
            dumpRefPositions("AFTER ALLOCATION");
            dumpVarRefPositions("AFTER ALLOCATION");
            dumpActiveIntervalsAtEnd();
        }
    }

    private bool canSkipUpperVectorSave(RefPosition reference, Interval localInterval)
    {
        if (!Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED);
            return default!;
        }

        assert(reference.refType is RefType.RefTypeUpperVectorSave);
        if (!Configuration.TARGET_ARM64)
        {
            // The AMD64 tail-call profiler stub does not preserve the upper YMM halves.
            return false;
        }

        return localInterval.physReg != REG_NA &&
            reference.treeNode?.Oper is GT_PROF_HOOK &&
            !getKillSetForProfilerHook().IsSet(localInterval.physReg) &&
            !new regMaskTP(TargetRuntimeMetadata.FloatingCalleeSavedMask).IsSet(localInterval.physReg);
    }
}
