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

    private regNumber allocateReg(Interval currentInterval, RefPosition reference, out RegisterScore score)
    {
        var bit = _regSelector.select(currentInterval, reference, out score);
        if (bit == SRBM_NONE)
        {
            return REG_NA;
        }

        var register = genRegNumFromMask(bit, currentInterval.registerType);
        var record = getRegisterRecord(register);
        var assigned = record.assignedInterval;
        if (assigned != currentInterval && isAssigned(record, getRegisterType(currentInterval, reference)))
        {
            assert(assigned is not null);
            if (_regSelector.isSpilling())
            {
                unassignPhysReg(record, assigned.recentRefPosition);
            }
            else
            {
                // Unassignment clears physReg; remember the historical association first.
                var wasAssigned = _regSelector.foundUnassignedReg() && assigned.physReg == register;
                unassignPhysReg(record, currentInterval.registerType);
                if (_regSelector.isMatchingConstant() && _compiler.opts.OptimizationEnabled)
                {
                    assert(assigned.isConstant);
                    assert(reference.treeNode is not null);
                    reference.treeNode.IsReuseRegVal = true;
                }
                else if (wasAssigned)
                {
                    updatePreviousInterval(record, assigned);
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

    private regNumber assignCopyReg(RefPosition reference)
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
            var register = allocateReg(interval, reference, out var score);
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
