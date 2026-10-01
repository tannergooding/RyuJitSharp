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
        registers.GetRegSetForType(registerType);

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

    private SingleTypeRegSet stressLimitRegs(RefPosition? refPosition, RegisterType registerType, SingleTypeRegSet mask)
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
#if UNIX_AMD64_ABI
                SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_R12 | SRBM_R13,
#else
                SRBM_RAX | SRBM_RCX | SRBM_RBX | SRBM_ETW_FRAMED_EBP | SRBM_RSI | SRBM_RDI,
#endif
                SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7,
                SRBM_NONE,
                registerType),
            LimitUpperSimdSet => varTypeUsesFloatReg(registerType) ? SRBM_HIGHFLOAT : mask,
            LimitExtGprSet => varTypeUsesIntReg(registerType) ? SRBM_HIGHINT | SRBM_ETW_FRAMED_EBP : mask,
            _ => throw new FatalJitException($"Unsupported LSRA register stress limit: 0x{limit:X}."),
        };

        if (constrainedMask != mask)
        {
            mask = getConstrainedRegMask(
                refPosition, registerType, mask, constrainedMask, refPosition?.minRegCandidateCount ?? 1);
        }

        if (refPosition is not null && refPosition.isFixedRegRef)
        {
            mask |= refPosition.registerAssignment;
        }

        return mask;
#elif TARGET_ARM64
        if (refPosition is not null &&
            refPosition.isLiveAtConsecutiveRegistersLoc(_consecutiveRegistersLocation))
        {
            return mask;
        }

        var limit = _lsraStressMask & 0x3;
        if (limit == 0)
        {
            return mask;
        }

        var count = refPosition?.minRegCandidateCount ?? 1;
        switch (limit)
        {
            case 1:
            {
                if (!_compiler.opts.compDbgEnC)
                {
                    mask = getConstrainedRegMask(refPosition, registerType, mask,
                        calleeSaveRegs(registerType), count);
                }
                break;
            }
            case 2:
            {
                var trash = regType(registerType) switch
                {
                    TYP_INT => SRBM_INT_CALLEE_TRASH,
                    TYP_FLOAT or TYP_DOUBLE => SRBM_FLT_CALLEE_TRASH,
                    TYP_MASK => SRBM_MSK_CALLEE_TRASH,
                    _ => throw new FatalJitException("Unsupported ARM64 LSRA stress register type."),
                };
                mask = getConstrainedRegMask(refPosition, registerType, mask, trash, count);
                break;
            }
            case 3:
            {
                var smallInt = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R19 | SRBM_R20;
                var smallFloat = SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V8 | SRBM_V9;
                var small = regType(registerType) switch
                {
                    TYP_INT => smallInt,
                    TYP_FLOAT or TYP_DOUBLE => smallFloat,
                    TYP_MASK => SRBM_NONE,
                    _ => throw new FatalJitException("Unsupported ARM64 LSRA stress register type."),
                };
                if ((mask & small) != SRBM_NONE)
                {
                    mask = getConstrainedRegMask(refPosition, registerType, mask, small, count);
                }
                break;
            }
        }

        if (refPosition is not null && refPosition.isFixedRegRef)
        {
            mask |= refPosition.registerAssignment;
        }

        return mask;
#elif TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
        var limit = _lsraStressMask & 0x3;
        if (limit == 0)
        {
            return mask;
        }

        var minimumCount = refPosition?.minRegCandidateCount ?? 1u;
        switch (limit)
        {
            case 1:
            {
                if (!_compiler.opts.compDbgEnC)
                {
                    mask = getConstrainedRegMask(refPosition, registerType, mask,
                        getOtherTargetStressCalleeMask(registerType, saved: true), minimumCount);
                }
                break;
            }
            case 2:
            {
                mask = getConstrainedRegMask(refPosition, registerType, mask,
                    getOtherTargetStressCalleeMask(registerType, saved: false), minimumCount);
                break;
            }
            case 3:
            {
                // The floating subset keeps five registers for Vector4 InitN; ARM32
                // also needs six integer registers for virtual-call target setup.
#if TARGET_X86
                const SingleTypeRegSet smallInt = SRBM_EAX | SRBM_ECX | SRBM_EDI;
                const SingleTypeRegSet smallFloat = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM6 | SRBM_XMM7;
#elif TARGET_ARM
                const SingleTypeRegSet smallInt = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 | SRBM_R4 | SRBM_R5;
                const SingleTypeRegSet smallFloat = SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F16 | SRBM_F17;
#elif TARGET_LOONGARCH64
                const SingleTypeRegSet smallInt = SRBM_T1 | SRBM_T3 | SRBM_A0 | SRBM_A1 | SRBM_T0;
                const SingleTypeRegSet smallFloat = SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F8 | SRBM_F9;
#elif TARGET_RISCV64
                const SingleTypeRegSet smallInt = SRBM_T1 | SRBM_T3 | SRBM_A0 | SRBM_A1 | SRBM_T0;
                const SingleTypeRegSet smallFloat = SRBM_FT0 | SRBM_FT1 | SRBM_FT2 | SRBM_FS0 | SRBM_FS1;
#endif
                if ((mask & smallInt) != SRBM_NONE)
                {
                    mask = getConstrainedRegMask(refPosition, registerType, mask, smallInt, minimumCount);
                }
                else if ((mask & smallFloat) != SRBM_NONE)
                {
                    mask = getConstrainedRegMask(refPosition, registerType, mask, smallFloat, minimumCount);
                }
                break;
            }
        }

        if (refPosition is not null && refPosition.isFixedRegRef)
        {
            mask |= refPosition.registerAssignment;
        }

        return mask;
#else
        NYI("LSRA stress register limiting on this target");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LSRA stress register limiting on this target is not ported.");
#endif
    }

#if TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
    private SingleTypeRegSet getOtherTargetStressCalleeMask(RegisterType registerType, bool saved)
    {
#if TARGET_X86
        const SingleTypeRegSet intSaved = SRBM_EBX | SRBM_ESI | SRBM_EDI;
        const SingleTypeRegSet intTrash = SRBM_EAX | SRBM_ECX | SRBM_EDX;
        const SingleTypeRegSet floatSaved = SRBM_NONE;
        const SingleTypeRegSet floatTrash =
            SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3 |
            SRBM_XMM4 | SRBM_XMM5 | SRBM_XMM6 | SRBM_XMM7;
#elif TARGET_ARM
        const SingleTypeRegSet intSaved =
            SRBM_R4 | SRBM_R5 | SRBM_R6 | SRBM_R7 | SRBM_R8 | SRBM_R9 | SRBM_R10;
        const SingleTypeRegSet intTrash =
            SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 | SRBM_R12 | SRBM_LR;
        const SingleTypeRegSet floatSaved =
            SRBM_F16 | SRBM_F17 | SRBM_F18 | SRBM_F19 |
            SRBM_F20 | SRBM_F21 | SRBM_F22 | SRBM_F23 |
            SRBM_F24 | SRBM_F25 | SRBM_F26 | SRBM_F27 |
            SRBM_F28 | SRBM_F29 | SRBM_F30 | SRBM_F31;
        const SingleTypeRegSet floatTrash =
            SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 |
            SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7 |
            SRBM_F8 | SRBM_F9 | SRBM_F10 | SRBM_F11 |
            SRBM_F12 | SRBM_F13 | SRBM_F14 | SRBM_F15;
#elif TARGET_LOONGARCH64
        const SingleTypeRegSet intSaved =
            SRBM_S0 | SRBM_S1 | SRBM_S2 | SRBM_S3 | SRBM_S4 |
            SRBM_S5 | SRBM_S6 | SRBM_S7 | SRBM_S8;
        const SingleTypeRegSet intTrash =
            SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 |
            SRBM_A5 | SRBM_A6 | SRBM_A7 | SRBM_T0 | SRBM_T1 |
            SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6 |
            SRBM_T7 | SRBM_T8;
        const SingleTypeRegSet floatSaved =
            SRBM_F24 | SRBM_F25 | SRBM_F26 | SRBM_F27 |
            SRBM_F28 | SRBM_F29 | SRBM_F30 | SRBM_F31;
        const SingleTypeRegSet floatTrash =
            SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 |
            SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7;
#else
        const SingleTypeRegSet intSaved =
            SRBM_S1 | SRBM_S2 | SRBM_S3 | SRBM_S4 | SRBM_S5 |
            SRBM_S6 | SRBM_S7 | SRBM_S8 | SRBM_S9 | SRBM_S10 | SRBM_S11;
        const SingleTypeRegSet intTrash =
            SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 |
            SRBM_A5 | SRBM_A6 | SRBM_A7 | SRBM_T0 | SRBM_T1 |
            SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6;
        const SingleTypeRegSet floatSaved =
            SRBM_FS0 | SRBM_FS1 | SRBM_FS2 | SRBM_FS3 |
            SRBM_FS4 | SRBM_FS5 | SRBM_FS6 | SRBM_FS7 |
            SRBM_FS8 | SRBM_FS9 | SRBM_FS10 | SRBM_FS11;
        const SingleTypeRegSet floatTrash =
            SRBM_FA0 | SRBM_FA1 | SRBM_FA2 | SRBM_FA3 |
            SRBM_FA4 | SRBM_FA5 | SRBM_FA6 | SRBM_FA7 |
            SRBM_FT0 | SRBM_FT1 | SRBM_FT2 | SRBM_FT3 |
            SRBM_FT4 | SRBM_FT5 | SRBM_FT6 | SRBM_FT7 |
            SRBM_FT8 | SRBM_FT9 | SRBM_FT10 | SRBM_FT11;
#endif
        if (varTypeUsesFloatReg(registerType))
        {
            return saved ? floatSaved : floatTrash;
        }

#if TARGET_X86 && FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(registerType))
        {
            return saved ? SRBM_NONE : _compiler.SRBM_MSK_CALLEE_TRASH;
        }
#endif
        return saved ? intSaved : intTrash;
    }
#endif

#if TARGET_AMD64
    private static SingleTypeRegSet getRegSetForType(
        regMask intRegisters, regMask floatRegisters, regMask maskRegisters, RegisterType registerType) =>
        regType(registerType) switch
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
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            assert(genIsValidDoubleReg(regNum));
            _nextIntervalRef[(int)(regNum + 1)] = MaxLocation;
        }
#endif
    }

    private void updateNextIntervalRef(regNumber regNum, Interval interval)
    {
        var location = interval.getNextRefLocation();
        _nextIntervalRef[(int)regNum] = location;
#if TARGET_ARM
        if (interval.registerType is TYP_DOUBLE)
        {
            _nextIntervalRef[(int)(regNum + 1)] = location;
        }
#endif
    }

    private void clearAllSpillCost() => Array.Fill(_spillCost, 0.0);

    private void clearSpillCost(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        _spillCost[(int)regNum] = 0;
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            assert(genIsValidDoubleReg(regNum));
            _spillCost[(int)(regNum + 1)] = 0;
        }
#endif
    }

    private void updateSpillCost(regNumber regNum, Interval interval)
    {
        var cost = interval.recentRefPosition is not null
            ? getWeight(interval.recentRefPosition)
            : 0;
        _spillCost[(int)regNum] = cost;
#if TARGET_ARM
        if (interval.registerType is TYP_DOUBLE)
        {
            _spillCost[(int)(regNum + 1)] = cost;
        }
#endif
    }

    private SingleTypeRegSet getFreeCandidates(SingleTypeRegSet candidates, RegisterType registerType)
    {
        var available = _availableRegs[(int)regType(registerType)];
        var free = candidates & available;
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            free &= (SingleTypeRegSet)(unchecked((ulong)(long)available) >> 1);
        }
#endif
        return free;
    }

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
        assert(ReferenceEquals(refPosition.getInterval(), interval));
        var registerType = interval.registerType;
        var candidates = refPosition.registerAssignment;
#if TARGET_LOONGARCH64 || TARGET_RISCV64
        // These ABIs can pass floating arguments in integer registers after exhausting the floating bank.
        if ((candidates & allRegs(registerType)) != SRBM_NONE)
        {
            return registerType;
        }

        assert(registerType is TYP_DOUBLE or TYP_FLOAT);
        assert((candidates & allRegs(TYP_I_IMPL)) != SRBM_NONE);
        return TYP_I_IMPL;
#else
        assert((candidates & allRegs(registerType)) != SRBM_NONE);
        return registerType;
#endif
    }

    private LsraLocation getNextIntervalRef(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        var location = _nextIntervalRef[(int)regNum];
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            location = Math.Min(location, _nextIntervalRef[(int)(regNum + 1)]);
        }
#endif
        return location;
    }

    private LsraLocation getNextFixedRef(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        var location = _nextFixedRef[(int)regNum];
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            location = Math.Min(location, _nextFixedRef[(int)(regNum + 1)]);
        }
#endif
        return location;
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
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            var first = genIsValidDoubleReg(regNum) ? regNum : regNum - 1;
            return _regsBusyUntilKill.IsSet(first) || _regsBusyUntilKill.IsSet(first + 1);
        }
#endif
        return _regsBusyUntilKill.IsSet(regNum);
    }

    private bool isRegInUse(regNumber regNum, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
#if TARGET_ARM
        if (registerType is TYP_DOUBLE)
        {
            var first = genIsValidDoubleReg(regNum) ? regNum : regNum - 1;
            return _regsInUseThisLocation.IsSet(first) || _regsInUseThisLocation.IsSet(first + 1);
        }
#endif
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

#if TARGET_ARM
    private bool canSpillDoubleReg(RegRecord record, LsraLocation location)
    {
        assert(genIsValidDoubleReg(record.regNum));
        var second = getSecondHalfRegRec(record);
        if ((record.assignedInterval is not null) && !canSpillReg(record, location))
        {
            return false;
        }
        if ((second.assignedInterval is not null) && !canSpillReg(second, location))
        {
            return false;
        }
        return true;
    }
#endif

    private bool isSpillCandidate(Interval current, RefPosition refPosition, RegRecord regRecord)
    {
        var candidateBit = genSingleTypeRegMask(regRecord.regNum);
        var refLocation = refPosition.nodeLocation;

        assert(!isRegBusy(regRecord.regNum, current.registerType));
        assert(!isRegInUse(regRecord.regNum, current.registerType));
        assert(!refPosition.isFixedRefOfRegMask(candidateBit));
        assert(!conflictingFixedRegReference(regRecord.regNum, refPosition));

#if TARGET_ARM
        if (current.registerType is TYP_DOUBLE)
        {
            return canSpillDoubleReg(regRecord, refLocation);
        }
#endif
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
