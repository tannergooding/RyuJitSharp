// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if DEBUG
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    public static bool isSingleRegister(SingleTypeRegSet registers) => genExactlyOneBit(registers);

    private static SingleTypeRegSet getRegSetForType(regMaskTP registers, RegisterType registerType) =>
        registerType switch
        {
            TYP_INT => registers.IntRegSet,
            TYP_FLOAT or TYP_DOUBLE => registers.FltRegSet,
#if FEATURE_MASKED_HW_INTRINSICS
            TYP_MASK => registers.MskRegSet,
#endif
            _ => throw new FatalJitException($"Unsupported LSRA register type: {registerType}."),
        };

#if DEBUG
    private SingleTypeRegSet getConstrainedRegMask(
        RefPosition? refPosition, RegisterType registerType, SingleTypeRegSet actualMask,
        SingleTypeRegSet constraintMask, uint minimumCount)
    {
        var newMask = actualMask & constraintMask;
        if ((uint)BitOperations.PopCount(unchecked((ulong)newMask)) < minimumCount)
        {
            return actualMask;
        }

        if ((refPosition is not null) && !refPosition.RegOptional())
        {
            var busyRegs = getRegSetForType(_regsBusyUntilKill | _regsInUseThisLocation, registerType);
            if ((newMask & ~busyRegs) == SRBM_NONE)
            {
                return actualMask;
            }
        }

        return newMask;
    }

    private SingleTypeRegSet stressLimitRegs(RefPosition refPosition, RegisterType registerType, SingleTypeRegSet mask)
    {
#if TARGET_AMD64
        const int LimitCallee = 0x1;
        const int LimitCaller = 0x2;
        const int LimitSmallSet = 0x3;
        const int LimitUpperSimdSet = 0x2000;
        const int LimitExtGprSet = 0x4000;
        const int LimitMask = LimitSmallSet | LimitUpperSimdSet | LimitExtGprSet;

        var limit = _lsraStressMask & LimitMask;
        if (limit == 0)
        {
            return mask;
        }

        var constrainedMask = limit switch
        {
            LimitCallee when !_compiler.opts.compDbgEnC => getRegSetForType(
                SRBM_INT_CALLEE_SAVED, SRBM_FLT_CALLEE_SAVED, SRBM_MSK_CALLEE_SAVED, registerType),
            LimitCallee => mask,
            LimitCaller => getRegSetForType(
                _compiler.SRBM_INT_CALLEE_TRASH, _compiler.SRBM_FLT_CALLEE_TRASH, _compiler.SRBM_MSK_CALLEE_TRASH,
                registerType),
            LimitSmallSet => getRegSetForType(
                SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_RSI | SRBM_RDI,
                SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7,
                SRBM_NONE,
                registerType),
            LimitUpperSimdSet => registerType is TYP_FLOAT or TYP_DOUBLE ? SRBM_HIGHFLOAT : mask,
            LimitExtGprSet => registerType is TYP_INT ? SRBM_HIGHINT | SRBM_ETW_FRAMED_EBP : mask,
            _ => throw new FatalJitException($"Unsupported LSRA register stress limit: 0x{limit:X}."),
        };

        if (constrainedMask != mask)
        {
            mask = getConstrainedRegMask(
                refPosition, registerType, mask, constrainedMask, refPosition.minRegCandidateCount);
        }

        if (refPosition.isFixedRegRef)
        {
            mask |= refPosition.registerAssignment;
        }

        return mask;
#else
        NYI("LSRA stress register limiting outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LSRA stress register limiting outside AMD64.");
#endif
    }

#if TARGET_AMD64
    private static SingleTypeRegSet getRegSetForType(
        regMask intRegisters, regMask floatRegisters, regMask maskRegisters, RegisterType registerType) =>
        registerType switch
        {
            TYP_INT => intRegisters,
            TYP_FLOAT or TYP_DOUBLE => floatRegisters,
#if FEATURE_MASKED_HW_INTRINSICS
            TYP_MASK => maskRegisters,
#endif
            _ => throw new FatalJitException($"Unsupported LSRA register type: {registerType}."),
        };
#endif
#endif

    private void resolveConflictingDefAndUse(Interval interval, RefPosition defRefPosition)
    {
        assert(!interval.isLocalVar);
        if (interval.isLocalVar)
        {
            throw new FatalJitException("Def-use conflict resolution is only valid for temporary intervals.");
        }

        var useRefPosition = defRefPosition.nextRefPosition
            ?? throw new FatalJitException("A conflicting definition must be followed by its use.");
        assert(ReferenceEquals(defRefPosition.referent, interval));
        assert(ReferenceEquals(useRefPosition.referent, interval));
        assert(RefTypeIsUse(useRefPosition.refType));
        if (!ReferenceEquals(defRefPosition.referent, interval) ||
            !ReferenceEquals(useRefPosition.referent, interval) ||
            !RefTypeIsUse(useRefPosition.refType))
        {
            throw new FatalJitException("Def-use conflict references must be ordered references of one interval.");
        }

        var useRegAssignment = useRefPosition.registerAssignment;
        var inUse = _regsBusyUntilKill | _regsInUseThisLocation;
        var busyRegs = getRegSetForType(inUse, interval.registerType);
        var useRegConflict = (useRegAssignment & ~busyRegs) == SRBM_NONE;
#if DEBUG
        dumpLsraAllocationEvent(LsraDumpEvent.DEFUSE_CONFLICT, null, defRefPosition);
#endif

        // Changing multi-register assignments can introduce cyclic parallel copies in codegen.
        var defTree = defRefPosition.treeNode
            ?? throw new FatalJitException("A temporary definition must retain its source tree node.");
        var canChangeDef = !defTree.IsMultiRegNode;

        if (canChangeDef && defRefPosition.isFixedRegRef)
        {
            var defRegRecord = getRegisterRecord(defRefPosition.assignedReg());
            canChangeDef = (defRegRecord.assignedInterval is null) || !defRegRecord.assignedInterval.isActive;
        }

        if (!canChangeDef)
        {
#if DEBUG
            dumpLsraAllocationEvent(LsraDumpEvent.DEFUSE_COPY, interval, defRefPosition);
#endif
            return;
        }

        if (useRefPosition.isFixedRegRef)
        {
            var useReg = useRefPosition.assignedReg();
            var nextRegLocation = getNextFixedRef(useReg, useRefPosition.getRegisterType());
            assert(nextRegLocation <= useRefPosition.nodeLocation);
            if (nextRegLocation > useRefPosition.nodeLocation)
            {
                throw new FatalJitException("A fixed use must be present in the next-fixed-reference map.");
            }

            if (nextRegLocation == useRefPosition.nodeLocation)
            {
                var useRegRecord = getRegisterRecord(useReg);
                if (!useRegConflict && (useRegRecord.assignedInterval is not null))
                {
                    var possiblyConflictingRef = useRegRecord.assignedInterval.recentRefPosition
                        ?? throw new FatalJitException("An assigned interval must retain its recent reference.");
                    if (possiblyConflictingRef.getRefEndLocation() >= defRefPosition.nodeLocation)
                    {
                        useRegConflict = true;
                    }
                }

                if (!useRegConflict)
                {
#if DEBUG
                    dumpLsraAllocationEvent(LsraDumpEvent.DEFUSE_DEF_IN_FIXED_USE, interval, defRefPosition);
#endif
                    defRefPosition.registerAssignment = useRegAssignment;
                    return;
                }
            }
            else
            {
                useRegConflict = true;
            }
        }

        if (defRefPosition.isFixedRegRef)
        {
            if (!useRegConflict)
            {
#if DEBUG
                dumpLsraAllocationEvent(LsraDumpEvent.DEFUSE_DEF_IN_USE, interval, defRefPosition);
#endif
                defRefPosition.registerAssignment = useRegAssignment;
                defRefPosition.isFixedRegRef = false;
                return;
            }

            if (useRefPosition.isFixedRegRef)
            {
                var registerType = interval.registerType;
                assert(getRegisterType(interval, defRefPosition) == registerType);
                assert(getRegisterType(interval, useRefPosition) == registerType);
#if DEBUG
                dumpLsraAllocationEvent(LsraDumpEvent.DEFUSE_ANY_DEF, interval, defRefPosition);
#endif
                defRefPosition.registerAssignment = allRegs(registerType);
                defRefPosition.isFixedRegRef = false;
                return;
            }
        }

#if DEBUG
        dumpLsraAllocationEvent(LsraDumpEvent.DEFUSE_COPY, interval, defRefPosition);
#endif
    }

    private void clearAllNextIntervalRef() => Array.Fill(_nextIntervalRef, MaxLocation);

    private void clearNextIntervalRef(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        _nextIntervalRef[(int)regNum] = MaxLocation;
    }

    private void updateNextIntervalRef(regNumber regNum, Interval interval)
    {
        _nextIntervalRef[(int)regNum] = interval.getNextRefLocation();
    }

    private void clearAllSpillCost() => Array.Fill(_spillCost, 0.0);

    private void clearSpillCost(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        _spillCost[(int)regNum] = 0;
    }

    private void updateSpillCost(regNumber regNum, Interval interval)
    {
        _spillCost[(int)regNum] = interval.recentRefPosition is not null
            ? getWeight(interval.recentRefPosition)
            : 0;
    }

    private SingleTypeRegSet getFreeCandidates(SingleTypeRegSet candidates, RegisterType registerType) =>
        candidates & _availableRegs[(int)registerType];

    private SingleTypeRegSet getAvailableGPRsForType(SingleTypeRegSet candidates, var_types registerType)
    {
#if TARGET_AMD64
        if (varTypeIsGC(registerType) || varTypeIsLong(registerType))
        {
            candidates &= SRBM_LOWINT;
        }
#endif
        return candidates;
    }

    private RegRecord getRegisterRecord(regNumber regNum)
    {
        assert((uint)regNum < (uint)physRegs.Length);
        return physRegs[(int)regNum];
    }

    private RegisterType getRegisterType(Interval interval, RefPosition refPosition)
    {
        assert(ReferenceEquals(refPosition.referent, interval));
        assert((refPosition.registerAssignment & allRegs(interval.registerType)) != SRBM_NONE);
        return interval.registerType;
    }

    private LsraLocation getNextIntervalRef(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        return _nextIntervalRef[(int)regNum];
    }

    private LsraLocation getNextFixedRef(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        return _nextFixedRef[(int)regNum];
    }

    private void updateNextFixedRef(RegRecord regRecord, RefPosition? nextRefPosition, RefPosition? nextKill)
    {
        var regNum = regRecord.regNum;
        var isLow = (int)regNum < 64;
        var regMask = genSingleTypeRegMask(isLow ? regNum : (regNumber)((int)regNum - REG_HIGH_BASE));
        var nextLocation = nextRefPosition?.nodeLocation ?? MaxLocation;
        for (var kill = nextKill; (kill is not null) && (kill.nodeLocation < nextLocation); kill = kill.nextRefPosition)
        {
#if HAS_MORE_THAN_64_REGISTERS
            var killedMask = isLow ? kill.killedRegisters.Lower : kill.killedRegisters.Upper;
#else
            var killedMask = kill.killedRegisters.IntRegSet;
#endif
            if ((killedMask & regMask) != SRBM_NONE)
            {
                nextLocation = kill.nodeLocation;
                break;
            }
        }

        if (isLow)
        {
            _fixedRegsLow = nextLocation == MaxLocation
                ? _fixedRegsLow & ~regMask
                : _fixedRegsLow | regMask;
        }
        else
        {
            _fixedRegsHigh = nextLocation == MaxLocation
                ? _fixedRegsHigh & ~regMask
                : _fixedRegsHigh | regMask;
        }

        _nextFixedRef[(int)regNum] = nextLocation;
    }

    private bool isRegBusy(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        return _regsBusyUntilKill.IsSet(regNum);
    }

    private bool isRegInUse(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        return _regsInUseThisLocation.IsSet(regNum);
    }

    private bool isRefPositionActive(RefPosition refPosition, LsraLocation location) =>
        (refPosition.nodeLocation == location) ||
        (unchecked(refPosition.nodeLocation + 1) == location && refPosition.delayRegFree);

    private bool conflictingFixedRegReference(regNumber regNum, RefPosition refPosition)
    {
        if (refPosition.isFixedRefOfReg(regNum))
        {
            return false;
        }

        var refLocation = refPosition.nodeLocation;
        var regRecord = getRegisterRecord(regNum);
        if (isRegInUse(regNum, refPosition.getInterval().registerType) &&
            !ReferenceEquals(regRecord.assignedInterval, refPosition.getInterval()))
        {
            return true;
        }

        var nextPhysRefLocation = _nextFixedRef[(int)regNum];
        return nextPhysRefLocation == refLocation ||
               (refPosition.delayRegFree && nextPhysRefLocation == unchecked(refLocation + 1));
    }

    private bool canSpillReg(RegRecord regRecord, LsraLocation refLocation)
    {
        var assignedInterval = regRecord.assignedInterval;
        assert(assignedInterval is not null);
        if (assignedInterval is null)
        {
            throw new FatalJitException("Cannot spill an unassigned physical register.");
        }

        var recentRefPosition = assignedInterval.recentRefPosition;
        if (recentRefPosition is not null)
        {
            assert(!isRefPositionActive(recentRefPosition, refLocation));
            return true;
        }

        assert(assignedInterval.isLocalVar);
        if (!assignedInterval.isLocalVar)
        {
            throw new FatalJitException("A physical register without a recent reference must belong to a local.");
        }

        var isParameter = assignedInterval.getLocalVar(_compiler).lvIsParam;
        assert(isParameter);
        if (!isParameter)
        {
            throw new FatalJitException("A local without a recent reference must be a parameter.");
        }

        return false;
    }

    private bool isSpillCandidate(Interval current, RefPosition refPosition, RegRecord regRecord)
    {
        var candidateBit = genSingleTypeRegMask(regRecord.regNum);
        var refLocation = refPosition.nodeLocation;

        assert(!isRegBusy(regRecord.regNum, current.registerType));
        assert(!isRegInUse(regRecord.regNum, current.registerType));
        assert(!refPosition.isFixedRefOfRegMask(candidateBit));
        assert(!conflictingFixedRegReference(regRecord.regNum, refPosition));

        return canSpillReg(regRecord, refLocation);
    }

    private weight_t getWeight(RefPosition refPosition)
    {
        assert(refPosition.refType is not RefType.RefTypeKill);
        if (refPosition.refType is RefType.RefTypeKill)
        {
            throw new FatalJitException("Kill references do not have a spill weight.");
        }

        var treeNode = refPosition.treeNode;
        if (treeNode is null)
        {
            return getBlockWeight(refPosition.bbNum);
        }

        if (treeNode.Oper.IsLocal)
        {
            ref var local = ref _compiler.lvaGetDesc(treeNode.AsLclVarCommon().LclNum);
            if (local.lvLRACandidate)
            {
                var weight = local.lvRefCntWtd();
                var interval = refPosition.getInterval();
                if (interval.isSpilled)
                {
                    var firstRefPosition = interval.firstRefPosition
                        ?? throw new FatalJitException("A spilled interval must have a first reference.");

                    if (local.IsLiveInOutOfHandler || firstRefPosition.singleDefSpill)
                    {
                        weight /= 2;
                    }
                    else
                    {
                        weight -= BB_UNITY_WEIGHT;
                    }
                }

                return weight;
            }
        }

        return 4 * getBlockWeight(refPosition.bbNum);
    }

    private weight_t getBlockWeight(uint blockNumber)
    {
        var blockInfo = _blockInfo
            ?? throw new FatalJitException("LSRA block weights are unavailable before interval construction.");

        return blockInfo[checked((int)blockNumber)].weight;
    }
}
