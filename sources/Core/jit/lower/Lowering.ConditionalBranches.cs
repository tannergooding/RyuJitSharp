// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerJTrue(GenTreeUnOp branch)
    {
#if TARGET_LOONGARCH64
        var next = branch.Next;
        var op = branch.Op1;
        GenCondition condition;
        GenTree cmpOp1;
        GenTree cmpOp2;
        if (op.Oper.IsCompare)
        {
            assert(op.Oper is GT_EQ or GT_NE or GT_LT or GT_LE or GT_GE or GT_GT);
            var comparison = op.AsOp();
            condition = GenCondition.FromRelop(comparison);
            cmpOp1 = comparison.Op1;
            cmpOp2 = comparison.Op2;

            if (varTypeIsFloating(cmpOp1.Type))
            {
                op.Type = TYP_VOID;
                op.Flags |= GTF_SET_FLAGS;
                var floatBranch = new GenTreeCC(GT_JCC, branch.Type, new GenCondition(GenCondition.NE),
                    branch, NodeThreading.LIR) {
                    Flags = branch.Flags & GTF_COMMON_MASK,
                };
                floatBranch._vnPair.SetBoth(ValueNumStore.NoVN);
                BlockRange().ReplaceNode(branch, floatBranch);
                return null;
            }

            BlockRange().Remove(op);
        }
        else
        {
            condition = new GenCondition(GenCondition.NE);
            cmpOp1 = op;
            cmpOp2 = CompilerInstance.gtNewZeroConNode(cmpOp1.Type);
            BlockRange().InsertBefore(branch, cmpOp2);
        }

        var directBranch = new GenTreeOpCC(GT_JCMP, branch.Type, condition, cmpOp1, cmpOp2,
            branch, NodeThreading.LIR) {
            Flags = branch.Flags & GTF_COMMON_MASK,
        };
        directBranch._vnPair.SetBoth(ValueNumStore.NoVN);
        BlockRange().ReplaceNode(branch, directBranch);
        if (cmpOp2.Oper.IsCnsIntOrI)
        {
            cmpOp2.IsContained = true;
        }

        return next;
#elif !TARGET_RISCV64 && !TARGET_WASM
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
#elif TARGET_RISCV64
        var condition = branch.Op1;
        assert(!condition.Oper.IsCompare || condition.Oper.IsCmpCompare);

        var conditionCode = new GenCondition(GenCondition.NE);
        GenTree op1;
        GenTree op2;
        if (condition.Oper.IsCompare && !varTypeIsFloating(condition.AsOp().Op1.Type))
        {
            var comparison = condition.AsOp();
            conditionCode = GenCondition.FromIntegralRelop(comparison);
            op1 = comparison.Op1;
            op2 = comparison.Op2;
        }
        else
        {
            if (condition.Oper.IsCompare && varTypeIsFloating(condition.AsOp().Op1.Type) &&
                ((condition.Flags & GTF_RELOP_NAN_UN) != 0))
            {
                condition.SetOper(condition.Oper.ReverseRelop);
                condition.Flags &= ~GTF_RELOP_NAN_UN;
                conditionCode = new GenCondition(GenCondition.EQ);
            }

            op1 = condition;
            op2 = CompilerInstance.gtNewZeroConNode(condition.Type);
        }

        if (op1 == condition)
        {
            BlockRange().InsertBefore(branch, op2);
        }

        var directBranch = new GenTreeOpCC(GT_JCMP, branch.Type, conditionCode, op1, op2,
            branch, NodeThreading.LIR) {
            Flags = branch.Flags & GTF_COMMON_MASK,
        };
        directBranch._vnPair.SetBoth(ValueNumStore.NoVN);
        BlockRange().ReplaceNode(branch, directBranch);
        if (op1 != condition)
        {
            BlockRange().Remove(condition);
        }
        if (!CheckImmedAndMakeContained(directBranch, op2))
        {
            op2.IsContained = false;
        }

        return directBranch.Next;
#elif TARGET_WASM
        // Wasm handles branch conditions directly in code generation.
        return null;
#else
        throw new System.NotImplementedException("The target-specific LowerJTrue override is not ported.");
#endif
    }
}
