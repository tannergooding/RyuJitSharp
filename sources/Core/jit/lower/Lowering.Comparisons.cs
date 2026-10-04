// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if TARGET_RISCV64
    private GenTree? LowerSavedIntegerCompare(GenTree comparison)
    {
        if (!BlockRange().TryGetUse(comparison, out var compareUse) ||
            (compareUse.User().Oper is GT_JTRUE))
        {
            return comparison;
        }

        var left = comparison.AsOp().Op1;
        var right = comparison.AsOp().Op2;
        assert(comparison.Oper.IsCmpCompare && varTypeUsesIntReg(left.Type));

        if ((comparison.Oper is GT_EQ or GT_NE) && !right.IsIntegralConst(0))
        {
            var type = genActualTypeIsInt(left.Type) ? TYP_INT : TYP_I_IMPL;
            var oper = GT_SUB;
            if (right.Oper.IsIntegralConst && !right.AsIntCon().ImmedValNeedsReloc(CompilerInstance))
            {
                var value = right.AsIntConCommon().IntegralValue;
                var minValue = type is TYP_INT ? int.MinValue : long.MinValue;
                if (value == -2048)
                {
                    oper = GT_XOR;
                }
                else if ((right.Type is not TYP_BYREF) && (value != minValue))
                {
                    oper = GT_ADD;
                    right.AsIntConCommon().IntegralValue = unchecked(-value);
                }
            }

            var arithmetic = CompilerInstance.gtNewBinaryNode(oper, type, left, right);
            var zero = CompilerInstance.gtNewZeroConNode(type);
            comparison.AsOp().Op1 = arithmetic;
            comparison.AsOp().Op2 = zero;
            BlockRange().InsertBefore(comparison, arithmetic, zero);
            ContainCheckBinary(arithmetic.AsOp());
            left = arithmetic;
            right = zero;
        }

        if ((right.Type is not TYP_BYREF) && right.Oper.IsIntegralConst &&
            !right.AsIntConCommon().ImmedValNeedsReloc(CompilerInstance))
        {
            if (comparison.Oper is GT_LE or GT_GE)
            {
                var value = right.AsIntConCommon().IntegralValue;
                bool isOverflow;
                if (genActualTypeIsInt(left.Type))
                {
                    isOverflow = comparison.Oper is GT_LE
                        ? comparison.AsOp().IsUnsigned
                            ? !CheckedOps.TryAddUns((int)value, 1, out _)
                            : !CheckedOps.TryAdd((int)value, 1, out _)
                        : comparison.AsOp().IsUnsigned
                            ? !CheckedOps.TrySubUns((int)value, 1, out _)
                            : !CheckedOps.TrySub((int)value, 1, out _);
                }
                else
                {
                    isOverflow = comparison.Oper is GT_LE
                        ? comparison.AsOp().IsUnsigned
                            ? !CheckedOps.TryAddUns(value, 1L, out _)
                            : !CheckedOps.TryAdd(value, 1L, out _)
                        : comparison.AsOp().IsUnsigned
                            ? !CheckedOps.TrySubUns(value, 1L, out _)
                            : !CheckedOps.TrySub(value, 1L, out _);
                }
                if (!isOverflow)
                {
                    right.AsIntConCommon().IntegralValue = comparison.Oper is GT_LE
                        ? unchecked(value + 1)
                        : unchecked(value - 1);
                    comparison.SetOper(comparison.Oper is GT_LE ? GT_LT : GT_GT, GenTree.PRESERVE_VN);
                }
            }

            if ((comparison.Oper is GT_LT) && right.IsIntegralConst(0) && !comparison.AsOp().IsUnsigned)
            {
                comparison.SetOper(GT_RSZ, GenTree.PRESERVE_VN);
                comparison.ChangeType(genActualType(left.Type));
                right.AsIntConCommon().IntegralValue = (long)((comparison.Type.Size * BITS_PER_BYTE) - 1);
                right.IsContained = true;
                return comparison.Next;
            }
        }

        return comparison;
    }

    private void SignExtendIfNecessary(ref GenTree operand)
    {
        assert(varTypeUsesIntReg(operand.Type));
        if (!genActualTypeIsInt(operand.Type) ||
            operand.Oper is GT_ADD or GT_SUB or GT_MUL or GT_MOD or GT_UMOD or GT_DIV or GT_UDIV or GT_CNS_INT ||
            operand.Oper.IsShiftOrRotate || operand.Oper.IsCmpCompare || operand.Oper.IsAtomic)
        {
            return;
        }

        var cast = CompilerInstance.gtNewCastNode(TYP_I_IMPL, operand, false, TYP_I_IMPL);
        BlockRange().InsertAfter(operand, cast);
        operand = cast;
    }
#endif

    private bool IsProfitableToSetZeroFlag(GenTree op)
    {
#if TARGET_XARCH
        if (op.Oper is GT_LSH or GT_RSH or GT_RSZ or GT_ROR or GT_ROL)
        {
            // BMI2's SHLX, SARX, SHRX and RORX do not set the zero flag.
            if (!op.AsOp().Op2.Oper.IsConst)
            {
                return false;
            }
        }
#endif

        return true;
    }

    // The single-bit reduction lambda in OptimizeConstCompare. Its caller changes
    // TEST_EQ/NE to BITTEST_EQ/NE and clears containment on the resulting bit index.
    private bool TryReduceSingleBitTestOps(GenTreeOp test)
    {
#if TARGET_XARCH || TARGET_RISCV64
        assert(test.Oper is GT_AND or GT_TEST_EQ or GT_TEST_NE);
        var testedOp = test.Op1;
        var bitOp = test.Op2;
#if TARGET_RISCV64
        if (bitOp.IsIntegralConstUnsignedPow2)
        {
            var constant = bitOp.AsIntConCommon();
            constant.IntegralValue = System.Numerics.BitOperations.Log2(constant.UnsignedIntegralValue);
            return true;
        }
#endif
        if (bitOp.Oper is not GT_LSH)
        {
            (bitOp, testedOp) = (testedOp, bitOp);
        }

        if ((bitOp.Oper is GT_LSH) && varTypeIsIntOrI(bitOp.Type) && bitOp.AsOp().Op1.IsIntegralConst(1))
        {
            BlockRange().Remove(bitOp.AsOp().Op1);
            BlockRange().Remove(bitOp);
            test.Op1 = testedOp;
            test.Op2 = bitOp.AsOp().Op2;

            return true;
        }

#if TARGET_XARCH
        // (x >> y) & 1 tests the same bit for either signed or unsigned shifts.
        // BT masks the index modulo the operand width, just like the shift. Keep
        // constant-index shifts: the existing constant-mask TEST is already optimal.
        var shiftOp = test.Op1;
        var oneOp = test.Op2;
        if (!oneOp.IsIntegralConst(1))
        {
            (shiftOp, oneOp) = (oneOp, shiftOp);
        }

        if (oneOp.IsIntegralConst(1) && (shiftOp.Oper is GT_RSH or GT_RSZ) &&
            varTypeIsIntOrI(shiftOp.Type) && !shiftOp.AsOp().Op2.Oper.IsIntegralConst)
        {
            BlockRange().Remove(oneOp);
            BlockRange().Remove(shiftOp);
            test.Op1 = shiftOp.AsOp().Op1;
            test.Op2 = shiftOp.AsOp().Op2;

            // Compare containment is skipped after this reduction. A memory value
            // contained by the removed shift must become a register for reg,reg BT.
            test.Op1.IsContained = false;

            return true;
        }
#endif

        return false;
#else
        throw new System.NotImplementedException("Non-xarch single-bit comparison reduction is not ported.");
#endif
    }
}
