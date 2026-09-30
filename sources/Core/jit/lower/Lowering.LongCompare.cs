// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if LOWER_DECOMPOSE_LONGS
    private GenTree? DecomposeLongCompare(GenTreeOp comparison)
    {
        assert(comparison.Op1.Type is TYP_LONG);
        var source1 = comparison.Op1.AsOp();
        var source2 = comparison.Op2.AsOp();
        assert(source1.Oper is GT_LONG);
        assert(source2.Oper is GT_LONG);

        var low1 = source1.Op1;
        var high1 = source1.Op2;
        var low2 = source2.Op1;
        var high2 = source2.Op2;
        BlockRange().Remove(source1);
        BlockRange().Remove(source2);

        var oper = comparison.Oper;
        GenTree highComparison;
        if (oper is GT_EQ or GT_NE)
        {
            // XOR is commutative, allowing constants on either side. Both halves
            // feed an OR whose flags can be consumed without a redundant compare.
            if (low1.Oper is GT_CNS_INT)
            {
                (low1, low2) = (low2, low1);
            }

            GenTree lowComparison;
            if (low2.IsIntegralConst(0))
            {
                BlockRange().Remove(low2);
                lowComparison = low1;
            }
            else
            {
                lowComparison = new GenTreeOp(GT_XOR, TYP_INT, low1, low2);
                BlockRange().InsertBefore(comparison, lowComparison);
                ContainCheckBinary(lowComparison.AsOp());
            }

            if (high1.Oper is GT_CNS_INT)
            {
                (high1, high2) = (high2, high1);
            }

            GenTree highResult;
            if (high2.IsIntegralConst(0))
            {
                BlockRange().Remove(high2);
                highResult = high1;
            }
            else
            {
                highResult = new GenTreeOp(GT_XOR, TYP_INT, high1, high2);
                BlockRange().InsertBefore(comparison, highResult);
                ContainCheckBinary(highResult.AsOp());
            }

            highComparison = new GenTreeOp(GT_OR, TYP_INT, lowComparison, highResult);
            BlockRange().InsertBefore(comparison, highComparison);
            ContainCheckBinary(highComparison.AsOp());
        }
        else
        {
            assert(oper is GT_LT or GT_LE or GT_GE or GT_GT);
            if (oper is GT_LE or GT_GT)
            {
                var swap = true;
                if ((low2.Oper is GT_CNS_INT) && (high2.Oper is GT_CNS_INT))
                {
                    var lowValue = unchecked((uint)low2.AsIntCon().IconValue);
                    var highValue = unchecked((uint)high2.AsIntCon().IconValue);
                    var value = (ulong)lowValue | ((ulong)highValue << 32);
                    var maximum = comparison.IsUnsigned ? ulong.MaxValue : (ulong)long.MaxValue;
                    if (value != maximum)
                    {
                        value = unchecked(value + 1);
                        low2.AsIntCon().SetValueTruncating(unchecked((int)value));
                        high2.AsIntCon().SetValueTruncating(unchecked((int)(value >> 32)));
                        oper = oper is GT_LE ? GT_LT : GT_GE;
                        swap = false;
                    }
                }

                if (swap)
                {
                    (low1, low2) = (low2, low1);
                    (high1, high2) = (high2, high1);
                    oper = oper.SwapRelop;
                }
            }

            assert(oper is GT_LT or GT_GE);
            if (low2.IsIntegralConst(0))
            {
                BlockRange().Remove(low2);
                if (low1.Oper is GT_CNS_INT or GT_LCL_VAR or GT_LCL_FLD)
                {
                    BlockRange().Remove(low1);
                }
                else
                {
                    low1.IsUnusedValue = true;
                }

                highComparison = new GenTreeOp(GT_CMP, TYP_VOID, high1, high2);
                BlockRange().InsertBefore(comparison, highComparison);
                ContainCheckCompare(highComparison.AsOp());
            }
            else
            {
                var lowComparison = new GenTreeOp(GT_CMP, TYP_VOID, low1, low2);
                lowComparison.Flags |= GTF_SET_FLAGS;
                highComparison = new GenTreeOp(GT_SUB_HI, TYP_INT, high1, high2);
                BlockRange().InsertBefore(comparison, lowComparison, highComparison);
                ContainCheckCompare(lowComparison.AsOp());
                ContainCheckBinary(highComparison.AsOp());

                // A local can move next to SUB_HI without clobbering the carry from CMP.
                if ((high1.Oper is GT_LCL_VAR or GT_LCL_FLD) && IsInvariantInRange(high1, highComparison))
                {
                    BlockRange().Remove(high1);
                    BlockRange().InsertBefore(highComparison, high1);
                }
            }
        }

        highComparison.Flags |= GTF_SET_FLAGS;
        if (highComparison.IsValue)
        {
            highComparison.IsUnusedValue = true;
        }

        var condition = GenCondition.FromIntegralRelop(oper, comparison.IsUnsigned);
        var next = comparison.Next;
        if (BlockRange().TryGetUse(comparison, out var use) && (use.User().Oper is GT_JTRUE))
        {
            var branch = use.User();
            BlockRange().Remove(comparison);
            var cc = new GenTreeCC(GT_JCC, branch.Type, condition, branch, NodeThreading.LIR) {
                Flags = branch.Flags,
            };
            cc._vnPair.SetBoth(ValueNumStore.NoVN);
            BlockRange().ReplaceNode(branch, cc);
            if (next == branch)
            {
                next = cc;
            }
        }
        else
        {
            var cc = new GenTreeCC(GT_SETCC, comparison.Type, condition, comparison, NodeThreading.LIR) {
                Flags = comparison.Flags,
            };
            cc._vnPair.SetBoth(ValueNumStore.NoVN);
            BlockRange().ReplaceNode(comparison, cc);
        }

        return next;
    }
#endif
}
