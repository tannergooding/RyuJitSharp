// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private bool isFree(RegRecord register)
    {
        return (register.assignedInterval is null || !register.assignedInterval.isActive) &&
            !isRegBusy(register.regNum, register.registerType);
    }

    private SingleTypeRegSet callerSaveRegs(RegisterType type)
    {
        assert((uint)type < (uint)TYP_COUNT);
        return _varTypeCalleeTrashRegs[(int)type];
    }

    private SingleTypeRegSet getMatchingConstants(
        SingleTypeRegSet mask, Interval currentInterval, RefPosition reference)
    {
        assert(currentInterval.isConstant && RefTypeIsDef(reference.refType));
        var candidates = mask & _registersWithConstants.GetRegSetForType(currentInterval.registerType);
        var result = SRBM_NONE;
        while (candidates != SRBM_NONE)
        {
            var bit = candidates & unchecked((SingleTypeRegSet)(0UL - (ulong)candidates));
            var register = genRegNumFromMask(bit, currentInterval.registerType);
            candidates ^= bit;
            if (isMatchingConstant(getRegisterRecord(register), reference))
            {
                result |= bit;
            }
        }

        return result;
    }

    private bool isMatchingConstant(RegRecord register, RefPosition reference)
    {
        if (register.assignedInterval is not Interval assigned || !assigned.isConstant ||
            reference.refType is not RefType.RefTypeDef)
        {
            return false;
        }
        var interval = reference.getInterval();
        if (!interval.isConstant || !isRegConstant(register.regNum, interval.registerType))
        {
            return false;
        }
        var tree = reference.treeNode;
        assert(tree is not null);
        assert(assigned.firstRefPosition is not null);
        var other = assigned.firstRefPosition.treeNode;
        assert(other is not null);
        if (tree.Oper != other.Oper)
        {
            return false;
        }

        switch (other.Oper)
        {
            case GT_CNS_INT:
            {
                var value = tree.AsIntCon().IconValue;
                var otherValue = other.AsIntCon().IconValue;
                // Negative int immediates need not have been sign-extended to 64 bits.
                return value == otherValue && (varTypeIsGC(tree.Type) == varTypeIsGC(other.Type) || value == 0) &&
                    (tree.Type == other.Type || value >= 0);
            }

            case GT_CNS_DBL:
            {
                // Bit identity preserves signed zero and NaN payloads.
                return tree.AsDblCon().IsBitwiseEqual(other.AsDblCon()) && tree.Type == other.Type;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                return
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                    !Compiler.varTypeNeedsPartialCalleeSave(assigned.registerType) &&
#endif
                    GenTreeVecCon.Equals(tree.AsVecCon(), other.AsVecCon());
            }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
            {
                return GenTreeMskCon.Equals(tree.AsMskCon(), other.AsMskCon());
            }
#endif

            default:
            {
                return false;
            }
        }
    }

    private regNumber allocateReg(Interval currentInterval, RefPosition reference) =>
        allocateReg(currentInterval, reference, out _);

    private regNumber allocateReg(Interval currentInterval, RefPosition reference, out RegisterScore score) =>
        allocateReg(currentInterval, reference, out score, false);

#if TARGET_ARM64
    private regNumber allocateReg(Interval currentInterval, RefPosition reference, bool needsConsecutiveRegisters) =>
        allocateReg(currentInterval, reference, out _, needsConsecutiveRegisters);
#endif

    private regNumber allocateReg(
        Interval currentInterval, RefPosition reference, out RegisterScore score,
        bool needsConsecutiveRegisters)
    {
        var bit = _regSelector.select(currentInterval, reference, out score, needsConsecutiveRegisters);
        if (bit == SRBM_NONE)
        {
            return REG_NA;
        }

        var register = genRegNumFromMask(bit, currentInterval.registerType);
        var record = getRegisterRecord(register);
        var assigned = record.assignedInterval;
        if (assigned != currentInterval && isAssigned(record, getRegisterType(currentInterval, reference)))
        {
            if (_regSelector.isSpilling())
            {
#if TARGET_ARM
                if (currentInterval.registerType is TYP_DOUBLE)
                {
                    assert(genIsValidDoubleReg(record.regNum));
                    unassignDoublePhysReg(record);
                }
                else if (assigned is Interval assignedDouble && assignedDouble.registerType is TYP_DOUBLE)
                {
                    var firstHalf = assignedDouble.assignedReg
                        ?? throw new FatalJitException("An assigned ARM32 double must retain its first register.");
                    assert(genIsValidDoubleReg(firstHalf.regNum));
                    unassignPhysReg(firstHalf, assignedDouble.recentRefPosition);
                }
                else
#endif
                {
                    assert(assigned is not null);
                    unassignPhysReg(record, assigned.recentRefPosition);
                }
            }
            else
            {
                // Unassignment clears physReg; remember the historical association first.
                var wasAssigned = _regSelector.foundUnassignedReg() &&
                    (assigned is not null) && (assigned.physReg == register);
                unassignPhysReg(record, currentInterval.registerType);
                if (_regSelector.isMatchingConstant() && _compiler.opts.OptimizationEnabled)
                {
                    assert(assigned is not null && assigned.isConstant);
                    assert(reference.treeNode is not null);
                    reference.treeNode.IsReuseRegVal = true;
                }
                else if (wasAssigned)
                {
                    assert(assigned is not null);
#if TARGET_ARM
                    updatePreviousInterval(record, assigned, assigned.registerType);
#else
                    updatePreviousInterval(record, assigned);
#endif
                }
                else
                {
                    assert(!_regSelector.isConstAvailable());
                }
            }
        }

        assignPhysReg(record, currentInterval);
        reference.registerAssignment = bit;
        return register;
    }

#if TARGET_ARM
    private void unassignDoublePhysReg(RegRecord record)
    {
        assert(genIsValidDoubleReg(record.regNum));
        var second = getSecondHalfRegRec(record);
        if (record.assignedInterval is Interval firstInterval)
        {
            if (firstInterval.registerType is TYP_DOUBLE)
            {
                unassignPhysReg(record, firstInterval.recentRefPosition);
            }
            else
            {
                assert(firstInterval.registerType is TYP_FLOAT);
                unassignPhysReg(record, firstInterval.recentRefPosition);
                if (second.assignedInterval is Interval secondInterval)
                {
                    assert(secondInterval.registerType is TYP_FLOAT);
                    unassignPhysReg(second, secondInterval.recentRefPosition);
                }
            }
        }
        else
        {
            var secondInterval = second.assignedInterval
                ?? throw new FatalJitException("A double-register spill requires an occupied half.");
            assert(secondInterval.registerType is TYP_FLOAT);
            unassignPhysReg(second, secondInterval.recentRefPosition);
        }
    }

    private void updatePreviousInterval(RegRecord record, Interval? interval, RegisterType registerType)
    {
        updatePreviousInterval(record, interval);
        if (registerType is TYP_DOUBLE)
        {
            findAnotherHalfRegRec(record).previousInterval = interval;
        }
    }
#endif

    private regNumber assignCopyReg(RefPosition reference) => assignCopyReg(reference, false);

    private regNumber assignCopyReg(RefPosition reference, bool needsConsecutiveRegisters)
    {
        var interval = reference.getInterval();
        assert(interval.isActive);
        var related = interval.relatedInterval;
        var oldRegister = interval.physReg;
        var oldRecord = interval.assignedReg;
        assert(oldRecord is not null && oldRecord.regNum == oldRegister);
        interval.relatedInterval = null;
        interval.isActive = false;
        reference.copyReg = true;

        try
        {
            var register = allocateReg(interval, reference, out var score, needsConsecutiveRegisters);
            assert(register != REG_NA);
            interval.relatedInterval = related;
            dumpCopyRegisterEvent(reference, register, score);
            return register;
        }
        finally
        {
            interval.relatedInterval = related;
            interval.physReg = oldRegister;
            interval.assignedReg = oldRecord;
            interval.isActive = true;
        }
    }
}
