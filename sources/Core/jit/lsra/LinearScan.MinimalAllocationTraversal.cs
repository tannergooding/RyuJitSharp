// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private enum MinimalAllocationEvent
    {
        FIXED_REG,
        RELOAD,
        NO_REG_ALLOCATED,
        MOVE_REG,
        KEPT_ALLOCATION,
        NEEDS_NEW_REG,
        ALLOC_REG,
        REUSE_REG,
        LAST_USE,
        LAST_USE_DELAYED,
        START_BB,
    }

    private void allocateRegistersMinimal()
    {
        assert(!_enregisterLocalVars);
        if (_enregisterLocalVars)
        {
            throw new FatalJitException("Minimal register allocation cannot run with enregistered locals.");
        }

        JITDUMP("*************** In LinearScan::allocateRegistersMinimal()\n");
        if (VERBOSE)
        {
            dumpLsraIntervals("before allocateRegistersMinimal");
        }

        foreach (var interval in intervals)
        {
            interval.recentRefPosition = null;
            assert(!interval.isActive);
        }

        resetRegStateMinimal();
        clearAllNextIntervalRef();
        clearAllSpillCost();

        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            var register = getRegisterRecord(_regIndices[index]);
            register.recentRefPosition = null;
            updateNextFixedRef(register, register.firstRefPosition, _killHead);
            assert(register.assignedInterval is null);
        }

        if (VERBOSE)
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
        RefPosition? lastAllocatedRefPosition = null;
        var handledBlockEnd = false;

        foreach (var currentRefPosition in refPositions)
        {
            var currentLocation = currentRefPosition.nodeLocation;
            _currentAllocationLocation = currentLocation;

            var registersToInactivate = registersToMakeInactive | delayedRegistersToMakeInactive;
            while (tryPopRegister(ref registersToInactivate, out var registerNumber))
            {
                var register = getRegisterRecord(registerNumber);
                clearSpillCost(register.regNum, register.registerType);
                makeRegisterInactive(register);
            }

            if (currentLocation > previousLocation)
            {
                makeRegsAvailable(registersToMakeInactive);
                registersToMakeInactive = delayedRegistersToMakeInactive;
                delayedRegistersToMakeInactive = RBM_NONE;
            }

            if (Configuration.DEBUG)
            {
                _activeRefPosition = null;
            }

            if (VERBOSE)
            {
                dumpAllocationRegisterRecords();
            }

            var currentReferent = currentRefPosition.referent;
            var refType = currentRefPosition.refType;
            assert(refType is not RefType.RefTypeDummyDef and not RefType.RefTypeParamDef and
                not RefType.RefTypeZeroInit and not RefType.RefTypeExpUse);

            if (Configuration.DEBUG && spillAlways() && (lastAllocatedRefPosition is not null) &&
                !lastAllocatedRefPosition.IsPhysRegRef() &&
                !lastAllocatedRefPosition.getInterval().isInternal &&
                (!lastAllocatedRefPosition.RegOptional() ||
                    (lastAllocatedRefPosition.registerAssignment != SRBM_NONE)) &&
                RefTypeIsDef(lastAllocatedRefPosition.refType))
            {
                assert(lastAllocatedRefPosition.registerAssignment != SRBM_NONE);
                var lastAllocatedInterval = lastAllocatedRefPosition.getInterval();
                var register = lastAllocatedInterval.assignedReg
                    ?? throw new FatalJitException("Stress spilling requires an assigned register.");
                _activeRefPosition = lastAllocatedRefPosition;
                unassignPhysReg(register, lastAllocatedRefPosition);
                _activeRefPosition = null;
                lastAllocatedRefPosition = null;
            }

            if (currentLocation > previousLocation)
            {
                // Release temporary copies without inactivating their interval's primary register.
                makeRegsAvailable(copyRegistersToFree);
                copyRegistersToFree = RBM_NONE;
                _regsInUseThisLocation = _regsInUseNextLocation;
                _regsInUseNextLocation = RBM_NONE;

                if ((registersToFree | delayedRegistersToFree) != RBM_NONE)
                {
                    freeRegisters(registersToFree);
                    if ((currentLocation > (previousLocation + 1)) &&
                        (delayedRegistersToFree != RBM_NONE))
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

            previousLocation = currentLocation;

            RefPosition? previousRefPosition = null;
            if (currentReferent is not null)
            {
                previousRefPosition = currentReferent.recentRefPosition;
                currentReferent.recentRefPosition = currentRefPosition;
            }
            else
            {
                assert(refType is RefType.RefTypeBB or RefType.RefTypeKill or RefType.RefTypeKillGCRefs);
            }

            if (Configuration.DEBUG)
            {
                _activeRefPosition = currentRefPosition;
            }

            if ((refType is RefType.RefTypeBB) && handledBlockEnd && VERBOSE)
            {
                dumpMinimalNewBlock(currentBlock, currentLocation, currentRefPosition);
            }

            if (!handledBlockEnd && (refType is RefType.RefTypeBB))
            {
                freeRegisters(registersToFree);
                registersToFree = RBM_NONE;
                _regsInUseThisLocation = RBM_NONE;
                _regsInUseNextLocation = RBM_NONE;
                handledBlockEnd = true;
                setCurrentBlockStartLocation(currentLocation);

                if (currentBlock is null)
                {
                    currentBlock = startBlockSequence();
                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.START_BB, currentRefPosition, block: currentBlock);
                }
                else
                {
                    processBlockEndAllocation(currentBlock);
                    currentBlock = moveToNextBlock();
                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.START_BB, currentRefPosition, block: currentBlock);
                }
            }

            if (refType is RefType.RefTypeBB)
            {
                handledBlockEnd = false;
                continue;
            }

            if (refType is RefType.RefTypeKill)
            {
                assert(ReferenceEquals(nextKill, currentRefPosition));
                processKills(currentRefPosition);
                nextKill = nextKill?.nextRefPosition;
                continue;
            }

            if (refType is RefType.RefTypeKillGCRefs)
            {
                spillGCRefs(currentRefPosition);
                continue;
            }

            if (refType is RefType.RefTypeFixedReg)
            {
                var register = currentRefPosition.getReg();
                var assignedInterval = register.assignedInterval;
                updateNextFixedRef(register, currentRefPosition.nextRefPosition, nextKill);

                if ((assignedInterval is not null) && !assignedInterval.isActive && assignedInterval.isConstant)
                {
                    clearConstantReg(register.regNum, assignedInterval.registerType);
                    register.assignedInterval = null;
                    clearSpillCost(register.regNum, assignedInterval.registerType);
                    if (Configuration.TARGET_ARM && assignedInterval.registerType is TYP_DOUBLE)
                    {
                        var otherHalf = findAnotherHalfRegRec(register);
                        assert(ReferenceEquals(otherHalf.assignedInterval, assignedInterval));
                        otherHalf.assignedInterval = null;
                        clearSpillCost(otherHalf.regNum, assignedInterval.registerType);
                    }
                }

                _regsInUseThisLocation |=
                    createRegisterMask(currentRefPosition.registerAssignment, register.registerType);
                dumpMinimalAllocationEvent(
                    MinimalAllocationEvent.FIXED_REG, currentRefPosition, register: currentRefPosition.assignedReg());

                if (Configuration.SWIFT_SUPPORT && currentRefPosition.delayRegFree)
                {
                    _regsInUseNextLocation |=
                        createRegisterMask(currentRefPosition.registerAssignment, register.registerType);
                }
                continue;
            }

            assert(!currentRefPosition.isPhysRegRef);
            assert(currentRefPosition.isIntervalRef());
            var currentInterval = currentRefPosition.getInterval();
            assert(!currentInterval.isLocalVar);
            if (currentInterval.isLocalVar)
            {
                throw new FatalJitException("Minimal LSRA references must not be local-variable intervals.");
            }

            var assignedRegister = currentInterval.physReg;

            if (Configuration.FEATURE_SIMD &&
                Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                refType is RefType.RefTypeUpperVectorSave)
            {
                if (assignedRegister != REG_NA)
                {
                    var register = getRegisterRecord(assignedRegister);
                    var assignedInterval = register.assignedInterval
                        ?? throw new FatalJitException("An upper-vector save requires an assigned register.");
                    var firstReference = currentInterval.firstRefPosition
                        ?? throw new FatalJitException("An upper-vector interval must have a first reference.");
                    unassignPhysReg(register, firstReference);
                    if (assignedInterval.isConstant)
                    {
                        clearConstantReg(assignedRegister, assignedInterval.registerType);
                    }

                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.NO_REG_ALLOCATED, currentRefPosition, currentInterval);
                }

                currentRefPosition.registerAssignment = SRBM_NONE;
                continue;
            }

            if ((assignedRegister == REG_NA) && RefTypeIsUse(refType))
            {
                currentRefPosition.reload = true;
                dumpMinimalAllocationEvent(
                    MinimalAllocationEvent.RELOAD, currentRefPosition, currentInterval, assignedRegister);
            }

            var assignedRegisterMask = SRBM_NONE;
            var isInRegister = false;
            if (assignedRegister != REG_NA)
            {
                isInRegister = true;
                assignedRegisterMask = genSingleTypeRegMask(assignedRegister);
                if (!currentInterval.isActive)
                {
                    assert(!RefTypeIsUse(refType));
                    currentInterval.isActive = true;
                    setRegInUse(assignedRegister, currentInterval.registerType);
                    updateSpillCost(assignedRegister, currentInterval);
                    updateNextIntervalRef(assignedRegister, currentInterval);
                }

                assert((currentInterval.assignedReg is not null) &&
                    (currentInterval.assignedReg.regNum == assignedRegister) &&
                    ReferenceEquals(currentInterval.assignedReg.assignedInterval, currentInterval));
            }

            if (previousRefPosition is not null)
            {
                assert(ReferenceEquals(previousRefPosition.nextRefPosition, currentRefPosition));
                assert((assignedRegister == REG_NA) ||
                    (assignedRegisterMask == previousRefPosition.registerAssignment) ||
                    currentRefPosition.outOfOrder || previousRefPosition.copyReg);
            }

            if (assignedRegister != REG_NA)
            {
                var register = getRegisterRecord(assignedRegister);
                assert((assignedRegisterMask == currentRefPosition.registerAssignment) ||
                    ReferenceEquals(register.assignedInterval, currentInterval) ||
                    ((getRegSetForType(_regsInUseThisLocation, currentInterval.registerType) &
                        genSingleTypeRegMask(assignedRegister)) == SRBM_NONE));

                if (conflictingFixedRegReference(assignedRegister, currentRefPosition))
                {
                    if (ReferenceEquals(register.assignedInterval, currentInterval))
                    {
                        unassignPhysRegNoSpill(register);
                        clearConstantReg(assignedRegister, currentInterval.registerType);
                    }

                    currentRefPosition.moveReg = true;
                    assignedRegister = REG_NA;
                    currentRefPosition.registerAssignment &= ~assignedRegisterMask;
                    setIntervalAsSplitMinimal(currentInterval);
                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.MOVE_REG, currentRefPosition, currentInterval, assignedRegister);
                }
                else if ((assignedRegisterMask & currentRefPosition.registerAssignment) != SRBM_NONE)
                {
                    currentRefPosition.registerAssignment = assignedRegisterMask;
                    if (!currentInterval.isActive)
                    {
                        currentRefPosition.reload = true;
                    }
                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.KEPT_ALLOCATION, currentRefPosition, currentInterval, assignedRegister);
                }
                else if (!RefTypeIsDef(refType))
                {
                    var copyRegister = assignCopyRegMinimal(currentRefPosition);
                    if (copyRegister == REG_NA)
                    {
                        throw new FatalJitException("A required copy-register allocation found no register.");
                    }

                    lastAllocatedRefPosition = currentRefPosition;
                    var copyRegisterMask = getSingleTypeRegMask(copyRegister, currentInterval.registerType);
                    var oldRegisterMask = getSingleTypeRegMask(assignedRegister, currentInterval.registerType);
                    updateRegsFreeBusyState(
                        currentRefPosition, currentInterval.registerType, oldRegisterMask | copyRegisterMask,
                        ref registersToFree, ref delayedRegistersToFree, currentInterval, assignedRegister);
                    if (!currentRefPosition.lastUse)
                    {
                        copyRegistersToFree |= createRegisterMask(copyRegisterMask, currentInterval.registerType);
                    }

                    currentRefPosition.moveReg = true;
                    currentRefPosition.copyReg = false;
                    clearNextIntervalRef(copyRegister, currentInterval.registerType);
                    clearSpillCost(copyRegister, currentInterval.registerType);
                    updateNextIntervalRef(assignedRegister, currentInterval);
                    updateSpillCost(assignedRegister, currentInterval);
                    continue;
                }
                else
                {
                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.NEEDS_NEW_REG, currentRefPosition, register: assignedRegister);
                    registersToFree |=
                        createRegisterMask(getSingleTypeRegMask(assignedRegister, currentInterval.registerType),
                            currentInterval.registerType);
                    assignedRegister = REG_NA;
                    if (ReferenceEquals(register.assignedInterval, currentInterval))
                    {
                        unassignPhysRegNoSpill(register);
                    }
                }
            }

            var selectionScore = RegisterScore.NONE;
            if (assignedRegister == REG_NA)
            {
                var allocate = true;
                if (currentRefPosition.RegOptional())
                {
                    if (currentRefPosition.lastUse && currentRefPosition.reload)
                    {
                        allocate = false;
                    }

                    if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                        Configuration.TARGET_XARCH &&
                        (refType is RefType.RefTypeUpperVectorRestore) &&
                        (currentInterval.physReg == REG_NA))
                    {
                        assert(currentRefPosition.regOptional);
                        allocate = false;
                    }

                    if (allocate && regOptionalNoAlloc())
                    {
                        allocate = false;
                    }
                }

                if (allocate)
                {
                    if (currentRefPosition.isFixedRegRef && !currentInterval.isActive &&
                        (currentInterval.assignedReg is not null) &&
                        ReferenceEquals(currentInterval.assignedReg.assignedInterval, currentInterval) &&
                        (genSingleTypeRegMask(currentInterval.assignedReg.regNum) !=
                            currentRefPosition.registerAssignment))
                    {
                        unassignPhysReg(currentInterval.assignedReg, (RefPosition?)null);
                    }

                    assignedRegister = allocateRegMinimal(
                        currentInterval, currentRefPosition, out selectionScore);
                }

                if (assignedRegister == REG_NA)
                {
                    assert(currentRefPosition.RegOptional());
                    dumpMinimalAllocationEvent(
                        MinimalAllocationEvent.NO_REG_ALLOCATED, currentRefPosition, currentInterval);
                    currentRefPosition.registerAssignment = SRBM_NONE;
                    currentRefPosition.reload = false;
                    currentInterval.isActive = false;
                    setIntervalAsSpilled(currentInterval);
                }
                else if (Configuration.DEBUG && VERBOSE)
                {
                    var allocationEvent = (currentInterval.isConstant &&
                        (currentRefPosition.treeNode is not null) &&
                        currentRefPosition.treeNode.IsReuseRegVal)
                        ? MinimalAllocationEvent.REUSE_REG
                        : MinimalAllocationEvent.ALLOC_REG;
                    dumpMinimalAllocationEvent(
                        allocationEvent, currentRefPosition, currentInterval, assignedRegister,
                        selectionScore: selectionScore);
                }

                if ((assignedRegister != REG_NA) && RefTypeIsUse(refType) && !isInRegister)
                {
                    assert(currentRefPosition.reload);
                }
            }

            if (assignedRegister != REG_NA)
            {
                assignedRegisterMask = genSingleTypeRegMask(assignedRegister);
                var assignedRegisterTypeMask = getSingleTypeRegMask(assignedRegister, currentInterval.registerType);
                var assignedRegisterMaskTP = createRegisterMask(
                    assignedRegisterTypeMask, currentInterval.registerType);
                _regsInUseThisLocation |= assignedRegisterMaskTP;
                if (currentRefPosition.delayRegFree)
                {
                    _regsInUseNextLocation |= assignedRegisterMaskTP;
                }

                currentRefPosition.registerAssignment = assignedRegisterMask;
                currentInterval.physReg = assignedRegister;
                removeRegisterSetForType(ref registersToFree, assignedRegisterTypeMask, currentInterval.registerType);

                var unassign = false;
                if (currentRefPosition.lastUse || (currentRefPosition.nextRefPosition is null))
                {
                    assert(currentRefPosition.isIntervalRef());
                    if ((refType is not RefType.RefTypeExpUse) &&
                        (currentRefPosition.nextRefPosition is null))
                    {
                        unassign = true;
                    }
                    else
                    {
                        if (currentRefPosition.delayRegFree)
                        {
                            delayedRegistersToMakeInactive |= assignedRegisterMaskTP;
                        }
                        else
                        {
                            registersToMakeInactive |= assignedRegisterMaskTP;
                        }

                        currentInterval.isActive = false;
                    }

                    currentInterval.relatedInterval?.updateRegisterPreferences(assignedRegisterMask);
                }

                if (unassign)
                {
                    if (currentRefPosition.delayRegFree)
                    {
                        delayedRegistersToFree |= assignedRegisterMaskTP;
                        dumpMinimalAllocationEvent(
                            MinimalAllocationEvent.LAST_USE_DELAYED, currentRefPosition);
                    }
                    else
                    {
                        registersToFree |= assignedRegisterMaskTP;
                        dumpMinimalAllocationEvent(MinimalAllocationEvent.LAST_USE, currentRefPosition);
                    }
                }
                else
                {
                    updateNextIntervalRef(assignedRegister, currentInterval);
                    updateSpillCost(assignedRegister, currentInterval);
                }
            }

            lastAllocatedRefPosition = currentRefPosition;
        }

        if (Configuration.DEBUG && extendLifetimes())
        {
            for (var index = 0; index <= (int)TargetRuntimeMetadata.FloatingRegisterLast; index++)
            {
                var register = physRegs[index];
                var interval = register.assignedInterval;
                if (interval is not null)
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

    private void resetRegStateMinimal()
    {
        initializeAvailableRegs();
        _registersWithConstants = RBM_NONE;
        _regsBusyUntilKill = RBM_NONE;
    }

    private static SingleTypeRegSet getSingleTypeRegMask(regNumber register, RegisterType registerType)
    {
        if (Configuration.TARGET_ARM && registerType is TYP_DOUBLE)
        {
            // A double occupies both halves, even when its second half is queried.
            var firstHalf = genIsValidDoubleReg(register) ? register : register - 1;
            return firstHalf.GetSingleTypeFloatMask(TYP_DOUBLE);
        }
        return genSingleTypeRegMask(register);
    }

    private void clearConstantReg(regNumber register, RegisterType registerType)
    {
        _registersWithConstants &=
            ~createRegisterMask(getSingleTypeRegMask(register, registerType), registerType);
    }

    private void processBlockEndAllocation(BasicBlock currentBlock)
    {
        markBlockVisited(currentBlock);
        resetAllRegistersState();
    }

    private void setIntervalAsSplitMinimal(Interval interval)
    {
        if (interval.isLocalVar)
        {
            var variableIndex = interval.getVarIndex(_compiler);
            var splitOrSpilledVariables = _splitOrSpilledVars ??= VarSetOps.MakeEmpty(_compiler);
            if (!interval.isSplit)
            {
                VarSetOps.AddElemD(_compiler, splitOrSpilledVariables, checked((int)variableIndex));
            }
            else
            {
                assert(VarSetOps.IsMember(_compiler, splitOrSpilledVariables, checked((int)variableIndex)));
            }
        }

        interval.isSplit = true;
    }

    private static void removeRegisterSetForType(
        ref regMaskTP destination, SingleTypeRegSet registers, RegisterType registerType)
    {
        var mask = createRegisterMask(registers, registerType);
        destination = Configuration.HAS_MORE_THAN_64_REGISTERS
            ? new regMaskTP(destination.Lower & ~mask.Lower, destination.Upper & ~mask.Upper)
            : new regMaskTP(destination.Lower & ~mask.Lower);
    }

    private static bool tryPopRegister(ref regMaskTP registers, out regNumber register)
    {
        var lower = unchecked((ulong)(long)registers.Lower);
        if (lower != 0)
        {
            var offset = BitOperations.TrailingZeroCount(lower);
            registers &= ~new regMaskTP((SingleTypeRegSet)(1UL << offset));
            register = (regNumber)(offset + REG_LOW_BASE);
            return true;
        }

        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            var upper = unchecked((ulong)(long)registers.Upper);
            if (upper != 0)
            {
                var offset = BitOperations.TrailingZeroCount(upper);
                registers = new regMaskTP(registers.Lower, registers.Upper & ~(SingleTypeRegSet)(1UL << offset));
                register = (regNumber)(offset + REG_HIGH_BASE);
                return true;
            }
        }

        register = REG_NA;
        return false;
    }

    private bool regOptionalNoAlloc() => Configuration.DEBUG && ((_lsraStressMask & 0x1000) != 0);

    [Conditional("DEBUG")]
    private void dumpMinimalNewBlock(
        BasicBlock? block, LsraLocation location, RefPosition refPosition)
    {
        if (!VERBOSE)
        {
            return;
        }

        if (refPosition.refType is RefType.RefTypeDummyDef)
        {
            dumpAllocationNewBlock(block, location, refPosition);
        }
        else
        {
            dumpRefPositionShort(refPosition, block);
        }
    }

    [Conditional("DEBUG")]
    private void dumpMinimalAllocationEvent(
        MinimalAllocationEvent allocationEvent,
        RefPosition refPosition,
        Interval? interval = null,
        BasicBlock? block = null,
        RegisterScore selectionScore = RegisterScore.NONE)
    {
        dumpMinimalAllocationEvent(allocationEvent, refPosition, interval, REG_NA, block, selectionScore);
    }

    [Conditional("DEBUG")]
    private void dumpMinimalAllocationEvent(
        MinimalAllocationEvent allocationEvent,
        RefPosition refPosition,
        regNumber register,
        Interval? interval = null,
        BasicBlock? block = null,
        RegisterScore selectionScore = RegisterScore.NONE)
    {
        dumpMinimalAllocationEvent(allocationEvent, refPosition, interval, register, block, selectionScore);
    }

    [Conditional("DEBUG")]
    private void dumpMinimalAllocationEvent(
        MinimalAllocationEvent allocationEvent,
        RefPosition refPosition,
        Interval? interval,
        regNumber register,
        BasicBlock? block = null,
        RegisterScore selectionScore = RegisterScore.NONE)
    {
        if (!VERBOSE)
        {
            return;
        }

        initializeAllocationDumpFormat();
        if ((interval is not null) && (register != REG_NA) && (register != REG_STK))
        {
            _allocationDumpRegisters |=
                regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
            dumpAllocationRegisterTitleIfNeeded();
        }

        switch (allocationEvent)
        {
            case MinimalAllocationEvent.FIXED_REG:
            case MinimalAllocationEvent.KEPT_ALLOCATION:
            {
                dumpRefPositionShort(refPosition);
                jitprintf($"Keep     {register.Name,-4} ");
                break;
            }
            case MinimalAllocationEvent.RELOAD:
            {
                dumpRefPositionShort(refPosition);
                jitprintf($"ReLod    {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }
            case MinimalAllocationEvent.NO_REG_ALLOCATED:
            {
                dumpRefPositionShort(refPosition);
                jitprintf("NoReg         ");
                break;
            }
            case MinimalAllocationEvent.MOVE_REG:
            {
                dumpRefPositionShort(refPosition);
                jitprintf($"Move     {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }
            case MinimalAllocationEvent.NEEDS_NEW_REG:
            {
                dumpRefPositionShort(refPosition);
                jitprintf($"Free  {register.Name,-4} ");
                dumpAllocationRegisterRecords();
                break;
            }
            case MinimalAllocationEvent.ALLOC_REG:
            case MinimalAllocationEvent.REUSE_REG:
            {
                dumpRefPositionShort(refPosition);
                if (_allocationPassComplete || (selectionScore is RegisterScore.NONE))
                {
                    var action = allocationEvent is MinimalAllocationEvent.ALLOC_REG ? "Alloc" : "Reuse";
                    jitprintf($"{action,-8} {register.Name,-4} ");
                }
                else
                {
                    var action = allocationEvent is MinimalAllocationEvent.ALLOC_REG ? "A" : "R";
                    jitprintf($"{getScoreName(selectionScore),-5}({action}) {register.Name,-4} ");
                }

                break;
            }
            case MinimalAllocationEvent.LAST_USE:
            case MinimalAllocationEvent.LAST_USE_DELAYED:
            {
                break;
            }
            case MinimalAllocationEvent.START_BB:
            {
                dumpAllocationNewBlock(block, refPosition.nodeLocation, refPosition);
                break;
            }
            default:
            {
                throw new FatalJitException($"Unsupported minimal LSRA dump event: {allocationEvent}.");
            }
        }
    }
}
