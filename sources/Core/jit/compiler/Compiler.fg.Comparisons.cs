// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    public static void fgPushConstantsRight(GenTreeOp tree)
    {
        assert(tree.Oper.IsCommutative || tree.Oper.IsCompare);
        var left = tree.Op1;
        var right = tree.Op2;
        if (left.Oper.IsConst)
        {
            // Keep equal kinds of constants stable; prefer handles on the left.
            if (!right.Oper.IsConst || (!left.IsIconHandle() && right.IsIconHandle()))
            {
                tree.Op1 = right;
                tree.Op2 = left;
                if (tree.Oper.IsCompare)
                {
                    tree.SetOper(tree.Oper.SwapRelop, GenTree.PRESERVE_VN);
                }
            }
        }
    }

    public GenTree fgOptimizeRelationalComparison(GenTreeOp comparison)
    {
        assert(comparison.Oper.IsCmpCompare);
        fgPushConstantsRight(comparison);

        // (x & pow2) ==/!= pow2 becomes (x & pow2) !=/== 0.
        if ((comparison.Oper is GT_EQ or GT_NE) && (comparison.Op1.Oper is GT_AND) &&
            comparison.Op2.IsIntegralConstUnsignedPow2 &&
            GenTree.Compare(comparison.Op1.AsOp().Op2, comparison.Op2))
        {
            comparison.SetOper(comparison.Oper.ReverseRelop, GenTree.PRESERVE_VN);
            comparison.Op2 = gtNewZeroConNode(comparison.Op2.Type);
            fgUpdateConstTreeValueNumber(comparison.Op2);
            if (fgGlobalMorph)
            {
                fgMorphTreeDone(comparison.Op2);
            }
        }

        GenTree tree = comparison;
        if (tree.Oper.IsCmpCompare &&
            ((tree.AsOp().Op1.Oper is GT_CAST) || (tree.AsOp().Op2.Oper is GT_CAST)))
        {
            tree = fgOptimizeRelationalComparisonWithCasts(tree.AsOp());
        }

        if (tree.Oper is GT_LT or GT_LE or GT_GE or GT_GT)
        {
            if (tree.AsOp().Op2.Oper.IsIntegralConst)
            {
                tree = fgOptimizeRelationalComparisonWithConst(tree.AsOp());
            }
        }
        else if (tree.Oper is GT_EQ or GT_NE)
        {
            if (tree.AsOp().Op2.Oper.IsIntegralConst)
            {
                tree = fgOptimizeEqualityComparisonWithConst(tree.AsOp());
            }
        }

        if (opts.OptimizationEnabled && fgGlobalMorph && (tree.Oper is GT_LT or GT_LE or GT_GE or GT_GT))
        {
            var relational = tree.AsOp();
            if (relational.IsUnsigned && varTypeIsIntegral(relational.Op1.Type) &&
                relational.Op1.IsNeverNegative(this) && relational.Op2.IsNeverNegative(this))
            {
                relational.Flags &= ~GTF_UNSIGNED;
            }

            if (relational.Op1.Oper.IsIntegralConst || relational.Op2.Oper.IsIntegralConst)
            {
                tree = fgOptimizeRelationalComparisonWithFullRangeConst(relational);
            }
        }

        return tree;
    }

    public GenTree fgOptimizeRelationalComparisonWithFullRangeConst(GenTreeOp comparison)
    {
        assert(comparison.Oper.IsCmpCompare);
        if (gtTreeHasSideEffects(comparison, GTF_SIDE_EFFECT))
        {
            return comparison;
        }

        var left = comparison.Op1;
        var right = comparison.Op2;
        if (!varTypeIsIntegral(left.Type) || !varTypeIsIntegral(right.Type))
        {
            return comparison;
        }

        long leftMinimum;
        long leftMaximum;
        if (left.Oper.IsIntegralConst)
        {
            leftMinimum = left.AsIntConCommon().IntegralValue;
            leftMaximum = leftMinimum;
        }
        else
        {
            var range = IntegralRange.ForNode(left, this);
            leftMinimum = IntegralRange.SymbolicToRealValue(range.LowerBound);
            leftMaximum = IntegralRange.SymbolicToRealValue(range.UpperBound);
        }

        long rightMinimum;
        long rightMaximum;
        if (right.Oper.IsIntegralConst)
        {
            rightMinimum = right.AsIntConCommon().IntegralValue;
            rightMaximum = rightMinimum;
        }
        else
        {
            var range = IntegralRange.ForNode(right, this);
            rightMinimum = IntegralRange.SymbolicToRealValue(range.LowerBound);
            rightMaximum = IntegralRange.SymbolicToRealValue(range.UpperBound);
        }

        var operation = comparison.Oper;
        if (operation is not GT_LT and not GT_LE)
        {
            operation = operation.SwapRelop;
            (leftMinimum, rightMinimum) = (rightMinimum, leftMinimum);
            (leftMaximum, rightMaximum) = (rightMaximum, leftMaximum);
        }

        GenTree? result = null;
        if (comparison.IsUnsigned)
        {
            // A signed range crossing zero spans both ends of the unsigned domain.
            if ((leftMinimum < 0) && (leftMaximum >= 0))
            {
                leftMinimum = 0;
                leftMaximum = -1;
            }

            if ((rightMinimum < 0) && (rightMaximum >= 0))
            {
                rightMinimum = 0;
                rightMaximum = -1;
            }

            if (((operation is GT_LT) && (unchecked((ulong)leftMaximum) < unchecked((ulong)rightMinimum))) ||
                ((operation is GT_LE) && (unchecked((ulong)leftMaximum) <= unchecked((ulong)rightMinimum))))
            {
                result = gtNewOneConNode(TYP_INT);
            }
            else if (((operation is GT_LT) && (unchecked((ulong)leftMinimum) >= unchecked((ulong)rightMaximum))) ||
                ((operation is GT_LE) && (unchecked((ulong)leftMinimum) > unchecked((ulong)rightMaximum))))
            {
                result = gtNewZeroConNode(TYP_INT);
            }
        }
        else if (((operation is GT_LT) && (leftMinimum >= rightMaximum)) ||
            ((operation is GT_LE) && (leftMinimum > rightMaximum)))
        {
            result = gtNewZeroConNode(TYP_INT);
        }
        else if (((operation is GT_LT) && (leftMaximum < rightMinimum)) ||
            ((operation is GT_LE) && (leftMaximum <= rightMinimum)))
        {
            result = gtNewOneConNode(TYP_INT);
        }

        if (result is not null)
        {
            fgUpdateConstTreeValueNumber(result);
            result.SetMorphed(this);
            return result;
        }

        return comparison;
    }

    public GenTree fgOptimizeRelationalComparisonWithConst(GenTreeOp comparison)
    {
        assert(comparison.Oper is GT_LT or GT_LE or GT_GE or GT_GT);
        assert(comparison.Op2.Oper.IsIntegralConst);
        var left = comparison.Op1;
        var right = comparison.Op2.AsIntConCommon();
        assert(left.Type.ActualType == right.Type.ActualType);
        var operation = comparison.Oper;
        var value = right.IntegralValue;

        if (value == 1)
        {
            if (operation is GT_GE)
            {
                operation = comparison.IsUnsigned ? GT_NE : GT_GT;
            }
            else if (operation is GT_LT)
            {
                operation = comparison.IsUnsigned ? GT_EQ : GT_LE;
            }
        }
        else if (!comparison.IsUnsigned && (value == -1))
        {
            if (operation is GT_LE)
            {
                operation = GT_LT;
            }
            else if (operation is GT_GT)
            {
                operation = GT_GE;
            }
        }
        else if (comparison.IsUnsigned || left.IsNeverNegative(this))
        {
            if (operation is GT_LE or GT_GT)
            {
                if (value == 0)
                {
                    operation = operation is GT_LE ? GT_EQ : GT_NE;
                    comparison.Flags &= ~GTF_UNSIGNED;
                }
                else if (((left.Type is TYP_LONG) && (value == long.MaxValue)) ||
                    ((left.Type.ActualType is TYP_INT) && (value == int.MaxValue)))
                {
                    operation = operation is GT_LE ? GT_GE : GT_LT;
                    comparison.Flags &= ~GTF_UNSIGNED;
                }
                else if (opts.OptimizationEnabled && (left.Type is TYP_LONG) && (value == uint.MaxValue))
                {
                    operation = operation is GT_GT ? GT_NE : GT_EQ;
                    var count = gtNewIconNode(TYP_INT, 32);
                    count.SetMorphed(this);
                    var shift = gtNewBinaryNode(GT_RSZ, TYP_LONG, left, count);
                    shift.SetMorphed(this);
                    comparison.Op1 = shift;
                }
            }
        }

        if (comparison.Oper != operation)
        {
            comparison.SetOper(operation, GenTree.PRESERVE_VN);
            right.IntegralValue = 0;
            fgUpdateConstTreeValueNumber(right);
        }

        return comparison;
    }

    public GenTree fgOptimizeRelationalComparisonWithCasts(GenTreeOp comparison)
    {
        var left = comparison.Op1;
        var right = comparison.Op2;
        assert(comparison.Oper.IsCmpCompare);
        assert((left.Oper is GT_CAST) || (right.Oper is GT_CAST));
        assert((left.Type.ActualType == right.Type.ActualType) ||
            (varTypeIsI(left.Type) && varTypeIsI(right.Type)) ||
            (varTypeIsFloating(left.Type) && varTypeIsFloating(right.Type)));
        if (left.Type is not TYP_LONG)
        {
            return comparison;
        }

        static bool SupportedOperand(GenTree operand)
        {
            if (operand.Oper.IsIntegralConst)
            {
                return true;
            }

            if (operand.Oper is GT_CAST)
            {
                if (operand.HasOverflowCheck || (operand.AsCast().CastOp.Type.ActualType is not TYP_INT))
                {
                    return false;
                }

                assert(varTypeIsLong(operand.AsCast().CastType));
                return true;
            }

            return false;
        }

        if (!SupportedOperand(left) || !SupportedOperand(right))
        {
            return comparison;
        }

        bool IsUpperZero(GenTree operand)
        {
            if (operand.Oper.IsIntegralConst)
            {
                var value = operand.AsIntConCommon().IntegralValue;
                return (value >= 0) && (value <= uint.MaxValue);
            }

            assert(operand.Oper is GT_CAST);
            return operand.AsCast().IsUnsigned || IntegralRange.ForNode(operand.AsCast().CastOp, this).IsNonNegative;
        }

        // With zero upper halves, signed and unsigned long comparisons both
        // have the same ordering as unsigned int comparisons.
        if (IsUpperZero(left) && IsUpperZero(right))
        {
            JITDUMP("Removing redundant cast(s) for:\n");
            DISPTREE(comparison);
            JITDUMP("\n\nto:\n\n");
            comparison.Flags |= GTF_UNSIGNED;

            void Transform(ref GenTree use)
            {
                if (use.Oper.IsIntegralConst)
                {
                    var value = unchecked((int)use.AsIntConCommon().IntegralValue);
                    use = use.BashToZeroConst(TYP_INT);
                    use.AsIntCon().IconValue = value;
                    fgUpdateConstTreeValueNumber(use);
                }
                else
                {
                    assert(use.Oper is GT_CAST);
                    use = use.AsCast().CastOp;
                }
            }

            Transform(ref comparison.Op1Ref);
            Transform(ref comparison.Op2Ref);
            assert((comparison.Op1.Type.ActualType is TYP_INT) && (comparison.Op2.Type.ActualType is TYP_INT));
            DISPTREE(comparison);
            JITDUMP("\n");
        }

        return comparison;
    }

    public GenTree fgOptimizeEqualityComparisonWithConst(GenTreeOp comparison)
    {
        assert(comparison.Oper is GT_EQ or GT_NE);
        assert(comparison.Op2.Oper.IsIntegralConst);
        var left = comparison.Op1;
        var right = comparison.Op2.AsIntConCommon();
        if ((left.Oper is GT_NEG) && !left.HasOverflowCheckEx && right.IsIntegralConst(0))
        {
            var operand = left.AsUnOp().Op1;
            var shouldFold = true;
#if TARGET_ARM64
            if (operand.Oper.IsShift && operand.AsOp().Op2.Oper.IsCnsIntOrI)
            {
                shouldFold = false;
            }
#endif
            if (shouldFold)
            {
                comparison.Op1 = operand;
                left = operand;
            }
        }

        // Move repeated unchecked int offsets into the comparand, retaining
        // 32-bit wrapping even when native-sized constant storage is wider.
        if (right.Oper.IsCnsIntOrI && (right.IconValue != 0))
        {
            while ((left.Oper is GT_ADD or GT_SUB) && left.AsOp().Op2.Oper.IsCnsIntOrI &&
                (left.Type is TYP_INT) && !left.HasOverflowCheck)
            {
                var offset = left.AsOp().Op2.AsIntCon().IconValue;
                var value = left.Oper is GT_ADD
                    ? unchecked(right.IconValue - offset)
                    : unchecked(right.IconValue + offset);
                left = left.AsOp().Op1;
                right.IconValue = unchecked((int)value);
            }

            comparison.Op1 = left;
            fgUpdateConstTreeValueNumber(right);
        }

        if (right.IsIntegralConst(0) || right.IsIntegralConst(1))
        {
            var value = right.IntegralValue;
            if (left.Oper.IsCompare)
            {
                if ((value == 0) == (comparison.Oper is GT_EQ))
                {
                    left = gtReverseCond(left);
                }

                noway_assert((left.Flags & GTF_RELOP_JMP_USED) == 0);
                left.Flags |= comparison.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE);
                left._vnPair = comparison._vnPair;
                return left;
            }

            if (fgGlobalMorph && (left.Oper is GT_AND) && (left.AsOp().Op1.Oper is GT_RSZ or GT_RSH))
            {
                var and = left.AsOp();
                var shift = and.Op1.AsOp();
                if (!and.Op2.IsIntegralConst(1))
                {
                    goto Skip;
                }

                if (shift.Op2.Oper.IsCnsIntOrI)
                {
                    var count = shift.Op2.AsIntCon().IconValue;
                    if (count < 0)
                    {
                        goto Skip;
                    }

                    var mask = and.Op2.AsIntConCommon();
                    if ((and.Type is TYP_INT) && (count < 32))
                    {
                        mask.IconValue = unchecked(1 << (int)count);
                    }
                    else if ((and.Type is TYP_LONG) && (count < 64))
                    {
                        mask.IntegralValue = 1L << (int)count;
                    }
                    else
                    {
                        goto Skip;
                    }

                    fgUpdateConstTreeValueNumber(mask);
                    and.Op1 = shift.Op1;
                }
                else
                {
                    // Keep the smaller ARM/BMI2 nonzero test when it is not a jump.
                    if (((comparison.Flags & GTF_RELOP_JMP_USED) == 0) &&
                        (((value == 0) && (comparison.Oper is GT_NE)) ||
                         ((value == 1) && (comparison.Oper is GT_EQ))))
                    {
                        goto Skip;
                    }

                    and.Op1 = shift.Op1;
                    shift.Op1 = and.Op2;
                    and.Op2 = shift;
                    shift.SetOper(GT_LSH);
                    gtUpdateNodeSideEffects(shift);
                }

                if (value == 1)
                {
                    comparison = gtReverseCond(comparison).AsOp();
                    right.IntegralValue = 0;
                    fgUpdateConstTreeValueNumber(right);
                }
            }
        }

    Skip:
        // Negative long constants must not become equal to zero-extended ints.
        if ((right.Type is not TYP_LONG) || ((right.IntegralValue >> 31) != 0))
        {
            return comparison;
        }

        if (left.Oper is not GT_AND)
        {
            if ((left.Oper is GT_CAST) && (left.AsCast().CastOp.Type is TYP_INT) && !left.HasOverflowCheck)
            {
                comparison.Op1 = left.AsCast().CastOp;
                var value = unchecked((int)right.IntegralValue);
                comparison.Op2 = right.BashToZeroConst(TYP_INT);
                comparison.Op2.AsIntCon().IconValue = value;
                fgUpdateConstTreeValueNumber(comparison.Op2);
            }

            return comparison;
        }

        // Narrowing the masked expression cannot preserve its value numbers.
        if (fgGlobalMorph)
        {
            assert(left.Type is TYP_LONG);
            var and = left.AsOp();
            if (and.Op2.Oper is not GT_CNS_NATIVELONG)
            {
                return comparison;
            }

            var mask = and.Op2.AsIntConCommon();
            if ((mask.IntegralValue >> 32) != 0)
            {
                return comparison;
            }

            // Morph can retype an implicit-byref operand; only long operands
            // can narrow directly, while other types need an explicit cast.
            var operand = and.Op1;
            var noValueNumbers = new ValueNumPair();
            if ((operand.Type is TYP_LONG) &&
                optNarrowTree(ref operand, TYP_LONG, TYP_INT, noValueNumbers, false))
            {
                _ = optNarrowTree(ref operand, TYP_LONG, TYP_INT, noValueNumbers, true);
                if ((operand.Oper is GT_CAST) && (operand.AsCast().CastType == operand.AsCast().CastOp.Type.ActualType))
                {
                    operand = operand.AsCast().CastOp;
                }

                and.Op1 = operand;
            }
            else
            {
                var cast = gtNewCastNode(TYP_INT, and.Op1, false, TYP_INT);
                cast.SetMorphed(this);
                and.Op1 = cast;
            }

            var maskValue = unchecked((int)mask.IntegralValue);
            and.Op2 = mask.BashToZeroConst(TYP_INT);
            and.Op2.AsIntCon().IconValue = maskValue;
            and.ChangeType(TYP_INT);
            var compareValue = unchecked((int)right.IntegralValue);
            comparison.Op2 = right.BashToZeroConst(TYP_INT);
            comparison.Op2.AsIntCon().IconValue = compareValue;
        }

        return comparison;
    }
}
