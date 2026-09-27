// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerJTrue(GenTreeUnOp branch)
    {
#if TARGET_XARCH || TARGET_ARM64
        var condition = branch.Op1;
        JITDUMP("Lowering JTRUE:\n");
        DISPTREERANGE(BlockRange(), branch);
        JITDUMP("\n");

#if TARGET_ARM64
        if (condition.Oper.IsCompare && condition.AsOp().Op2.Oper.IsCnsIntOrI)
        {
            var relop = condition.AsOp();
            var op1 = relop.Op1;
            var op2 = relop.Op2;
            var newOper = GT_COUNT;
            GenCondition conditionCode = default;
            if ((relop.Oper is GT_EQ or GT_NE) && op2.IsIntegralConst(0))
            {
                newOper = GT_JCMP;
                conditionCode = GenCondition.FromRelop(relop);
            }
            else if ((relop.Oper is GT_LT or GT_GE) && !relop.IsUnsigned && op2.IsIntegralConst(0))
            {
                var op1Type = op1.Type.ActualType;
                if ((op1 is GenTreeCast cast) && (cast.CastType is TYP_BYTE or TYP_SHORT) &&
                    !cast.HasOverflowCheck)
                {
                    op1Type = cast.CastType;
                    op1 = cast.CastOp;
                    relop.Op1 = op1;
                    op1.IsContained = false;
                    BlockRange().Remove(cast);
                }

                newOper = GT_JTEST;
                conditionCode = new GenCondition(relop.Oper is GT_LT ? GenCondition.NE : GenCondition.EQ);
                op2.AsIntConCommon().IntegralValue = unchecked(1L << ((8 * op1Type.Size) - 1));
            }
            else if ((relop.Oper is GT_TEST_EQ or GT_TEST_NE) && nint.IsPow2(op2.AsIntCon().IconValue))
            {
                newOper = GT_JTEST;
                conditionCode = GenCondition.FromRelop(relop);
            }

            if (newOper is not GT_COUNT)
            {
                var directBranch = new GenTreeOpCC(newOper, branch.Type, conditionCode, op1, op2,
                    branch, NodeThreading.LIR) {
                    Flags = branch.Flags & GTF_COMMON_MASK,
                };
                directBranch._vnPair.SetBoth(ValueNumStore.NoVN);
                BlockRange().ReplaceNode(branch, directBranch);
                op2.IsContained = true;
                BlockRange().Remove(condition);
                JITDUMP($"Lowered to {newOper.Name}\n");
                return null;
            }
        }
#endif

        GenTree result = branch;
        if (TryLowerConditionToFlagsNode(branch, condition, out var code))
        {
            result = new GenTreeCC(GT_JCC, branch.Type, code, branch, NodeThreading.LIR) {
                Flags = branch.Flags,
            };
            result._vnPair.SetBoth(ValueNumStore.NoVN);
            BlockRange().ReplaceNode(branch, result);
        }

        JITDUMP("Lowering JTRUE Result:\n");
        DISPTREERANGE(BlockRange(), result);
        JITDUMP("\n");
        return null;
#else
        throw new System.NotImplementedException("Non-xarch conditional branch lowering is not ported.");
#endif
    }
}
