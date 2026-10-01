// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if TARGET_ARM
    private RegRecord getSecondHalfRegRec(RegRecord record)
    {
        assert(genIsValidDoubleReg(record.regNum));
        return getRegisterRecord(record.regNum + 1);
    }

    private RegRecord findAnotherHalfRegRec(RegRecord record)
    {
        return getRegisterRecord(findAnotherHalfRegNum(record.regNum));
    }

    private regNumber findAnotherHalfRegNum(regNumber register)
    {
        assert(genIsValidFloatReg(register));
        if (genIsValidDoubleReg(register))
        {
            var other = register + 1;
            assert(!genIsValidDoubleReg(other));
            return other;
        }

        var first = register - 1;
        assert(genIsValidDoubleReg(first));
        return first;
    }

    private bool isSecondHalfReg(RegRecord record, Interval interval)
    {
        var assigned = interval.assignedReg;
        if (assigned is null || interval.registerType is not TYP_DOUBLE)
        {
            return false;
        }

        assert(genIsValidDoubleReg(assigned.regNum));
        var second = assigned.regNum + 1;
        assert(genIsValidFloatReg(second) && !genIsValidDoubleReg(second));
        return ReferenceEquals(getRegisterRecord(second), record);
    }

#endif

#if DEBUG
    private regNumber rotateBlockStartLocation(Interval interval, regNumber targetReg, regMaskTP availableRegs)
    {
        if ((targetReg != REG_STK) && ((_lsraStressMask & 0x300) == 0x200))
        {
            var candidates = allRegs(interval.registerType) & availableRegs.GetRegSetForType(interval.registerType);
            var firstReg = REG_NA;
            var newReg = REG_NA;
            while (candidates != SRBM_NONE)
            {
                var bits = unchecked((ulong)(long)candidates);
                var offset = BitOperations.TrailingZeroCount(bits);
                candidates &= ~unchecked((SingleTypeRegSet)(1UL << offset));
                var nextReg = (regNumber)(offset +
                    (interval.registerType is TYP_MASK ? REG_HIGH_BASE : REG_LOW_BASE));
                if (nextReg > targetReg)
                {
                    newReg = nextReg;
                    break;
                }
                if (firstReg == REG_NA)
                {
                    firstReg = nextReg;
                }
            }
            if (newReg == REG_NA)
            {
                assert(firstReg != REG_NA);
                newReg = firstReg;
            }

            targetReg = newReg;
        }
        return targetReg;
    }
#endif

    private void unassignIntervalBlockStart(RegRecord record, regNumber[]? inMap)
    {
        var assigned = record.assignedInterval;
        if (assigned is null)
        {
            return;
        }
        if (isAssignedToInterval(assigned, record))
        {
            if (!assigned.isLocalVar)
            {
                assert(assigned.isConstant || assigned.IsUpperVector());
                inMap = null;
            }
            var assignedRecord = assigned.assignedReg
                ?? throw new FatalJitException("An assigned interval requires its register record.");
            var assignedReg = assignedRecord.regNum;
            assigned.isActive = false;
            unassignPhysReg(assignedRecord, (RefPosition?)null);
            if ((inMap is not null) &&
                (getVarReg(inMap, assigned.getVarIndex(_compiler)) == assignedReg))
            {
                setVarReg(inMap, assigned.getVarIndex(_compiler), REG_STK);
            }
        }
        else
        {
            clearAssignedInterval(record, assigned.registerType);
        }
    }

    private void setRegsInUseAtBlockStart(regMaskTP registers)
    {
        _availableRegs[(int)TYP_INT] &= ~registers.IntRegSet;
        _availableRegs[(int)TYP_FLOAT] &= ~registers.FltRegSet;
        _availableRegs[(int)TYP_DOUBLE] &= ~registers.FltRegSet;
#if FEATURE_MASKED_HW_INTRINSICS
        _availableRegs[(int)TYP_MASK] &= ~registers.MskRegSet;
#endif
    }

    private void handleDeadBlockCandidates(SingleTypeRegSet candidates, int registerBase, regNumber[] inMap)
    {
        var remaining = unchecked((ulong)(long)candidates);
        while (remaining != 0)
        {
            var offset = BitOperations.TrailingZeroCount(remaining);
            remaining &= remaining - 1;
            var register = (regNumber)(offset + registerBase);
            var record = getRegisterRecord(register);
            makeRegAvailable(register, record.registerType);
            var assigned = record.assignedInterval;
            if (assigned is null)
            {
                continue;
            }
            assert(assigned.isLocalVar || assigned.isConstant || assigned.IsUpperVector());
            if (!assigned.isConstant && ReferenceEquals(assigned.assignedReg, record))
            {
                assigned.isActive = false;
                if (assigned.getNextRefPosition() is null)
                {
                    unassignPhysReg(record, (RefPosition?)null);
                }
                if (!assigned.IsUpperVector())
                {
                    setVarReg(inMap, assigned.getVarIndex(_compiler), REG_STK);
                }
            }
            else
            {
                clearAssignedInterval(record);
            }
        }
    }
}
