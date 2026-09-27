// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool IsContainableUnaryOrBinaryOp(GenTree parentNode, GenTree childNode)
    {
#if DEBUG
        if (parentNode.Oper.IsBinary)
        {
            assert((parentNode.AsOp().Op1 == childNode) || (parentNode.AsOp().Op2 == childNode));
        }
        else
        {
            assert(parentNode.Oper.IsUnary);
            assert(parentNode.AsUnOp().Op1 == childNode);
        }
#endif
        if (parentNode.IsContained)
        {
            return false;
        }

        if (!varTypeIsIntegral(parentNode.Type) && (parentNode.Type is not TYP_BYREF))
        {
            return false;
        }

        if (parentNode.AsUnOp().Op1.IsContained ||
            (parentNode.Oper.IsBinary && parentNode.AsOp().Op2.IsContained))
        {
            return false;
        }

        if (parentNode.HasOverflowCheckEx)
        {
            return false;
        }

        if (!varTypeIsIntegral(childNode.Type) || ((childNode.Flags & GTF_SET_FLAGS) != 0) ||
            childNode.HasOverflowCheckEx)
        {
            return false;
        }

        if (childNode.Oper is GT_MUL)
        {
            if (childNode.AsOp().Op1.IsContained || childNode.AsOp().Op2.IsContained)
            {
                return false;
            }

            if ((parentNode.Flags & GTF_SET_FLAGS) != 0)
            {
                return false;
            }

            if (parentNode.Oper is GT_ADD)
            {
                return IsInvariantInRange(childNode, parentNode);
            }

            if (parentNode.Oper is GT_SUB)
            {
                assert(childNode == parentNode.AsOp().Op2);
                return IsInvariantInRange(childNode, parentNode);
            }

            return false;
        }

        if (childNode.Oper is GT_LSH or GT_RSH or GT_RSZ)
        {
            if (childNode.AsOp().Op1.IsContained)
            {
                return false;
            }

            var shiftAmountNode = childNode.AsOp().Op2;
            if (!shiftAmountNode.Oper.IsCnsIntOrI)
            {
                return false;
            }

            var shiftAmount = shiftAmountNode.AsIntCon().IconValue;
            // Compare results are INT even when their operands (and this shift) are LONG.
            var maxShift = (childNode.Type.Size * BITS_PER_BYTE) - 1;
            if ((shiftAmount < 1) || (shiftAmount > maxShift))
            {
                return false;
            }

            if (parentNode.Oper is GT_ADD or GT_SUB or GT_AND or GT_NEG)
            {
                if (IsInvariantInRange(childNode, parentNode))
                {
                    assert(shiftAmountNode.IsContained);
                    return true;
                }
            }

            if ((parentNode.Flags & GTF_SET_FLAGS) != 0)
            {
                return false;
            }

            if ((parentNode.Oper is GT_CMP or GT_OR or GT_XOR) || parentNode.Oper.IsCompare)
            {
                if (IsInvariantInRange(childNode, parentNode))
                {
                    assert(shiftAmountNode.IsContained);
                    return true;
                }
            }

            if (parentNode.Oper is GT_NOT or GT_AND_NOT or GT_OR_NOT or GT_XOR_NOT)
            {
                if (IsInvariantInRange(childNode, parentNode))
                {
                    assert(shiftAmountNode.IsContained);
                    return true;
                }
            }

            return false;
        }

        if (childNode.Oper is GT_ROL or GT_ROR)
        {
            if (childNode.AsOp().Op1.IsContained)
            {
                return false;
            }

            var rotateAmountNode = childNode.AsOp().Op2;
            if (!rotateAmountNode.Oper.IsCnsIntOrI)
            {
                return false;
            }

            var wrapAmount = (nint)childNode.Type.Size * BITS_PER_BYTE;
            assert((wrapAmount == 32) || (wrapAmount == 64));
            var rotateAmount = rotateAmountNode.AsIntCon().IconValue % wrapAmount;
            assert((rotateAmount >= 0) && (rotateAmount < wrapAmount));
            if (childNode.Oper is GT_ROL)
            {
                childNode.SetOper(GT_ROR);
                rotateAmount = wrapAmount - rotateAmount;
            }

            // Native normalization is observable even when the parent rejects containment below.
            rotateAmountNode.AsIntCon().IconValue = rotateAmount;
            assert(childNode.Oper is GT_ROR);
            if (parentNode.Oper is GT_AND)
            {
                if (IsInvariantInRange(childNode, parentNode))
                {
                    assert(rotateAmountNode.IsContained);
                    return true;
                }
            }

            if ((parentNode.Flags & GTF_SET_FLAGS) != 0)
            {
                return false;
            }

            if (parentNode.Oper is GT_OR or GT_XOR)
            {
                if (IsInvariantInRange(childNode, parentNode))
                {
                    assert(rotateAmountNode.IsContained);
                    return true;
                }
            }

            return false;
        }

        if (childNode.Oper is GT_NEG)
        {
            var operand = childNode.AsUnOp().Op1;
            if (operand.IsContained && (operand.Oper is not (GT_LSH or GT_RSH or GT_RSZ)))
            {
                return false;
            }

            if ((parentNode.Flags & GTF_SET_FLAGS) != 0)
            {
                return false;
            }

            if (parentNode.Oper is GT_EQ or GT_NE)
            {
                if (!IsInvariantInRange(childNode, parentNode))
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        if (childNode.Oper is GT_CAST)
        {
            var cast = childNode.AsCast();
            var castOp = cast.CastOp;
            var isSupportedCast = false;
            if (varTypeIsSmall(cast.CastType))
            {
                assert(!varTypeIsFloating(castOp.Type));
                isSupportedCast = true;
            }
            else if ((childNode.Type is TYP_LONG) && (castOp.Type.ActualType is TYP_INT))
            {
                isSupportedCast = true;
            }

            if (!isSupportedCast || !IsInvariantInRange(childNode, parentNode))
            {
                return false;
            }

            if (parentNode.Oper is GT_ADD or GT_SUB)
            {
                return true;
            }

            if ((parentNode.Flags & GTF_SET_FLAGS) != 0)
            {
                return false;
            }

            if (parentNode.Oper is GT_CMP)
            {
                return true;
            }

            if (parentNode.Oper.IsCmpCompare)
            {
                if (castOp.IsContained)
                {
                    return false;
                }

                // Comparisons prefer the load's extension; explicit CMP prefers the combined op.
                if (IsContainableMemoryOp(castOp))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        return false;
    }

    private void ContainCheckNeg(GenTreeUnOp neg)
    {
        if (neg.IsContained || !varTypeIsIntegral(neg.Type) || ((neg.Flags & GTF_SET_FLAGS) != 0))
        {
            return;
        }

        var childNode = neg.Op1;
        if (childNode.Oper is GT_MUL)
        {
            if (childNode.AsOp().Op1.IsContained || childNode.AsOp().Op2.IsContained)
            {
                return;
            }

            if (childNode.HasOverflowCheck || !varTypeIsIntegral(childNode.Type) ||
                ((childNode.Flags & GTF_SET_FLAGS) != 0))
            {
                return;
            }

            if (IsInvariantInRange(childNode, neg))
            {
                MakeSrcContained(neg, childNode);
            }
        }
        else if (CompilerInstance.opts.OptimizationEnabled && (childNode.Oper is GT_LSH or GT_RSH or GT_RSZ) &&
            IsContainableUnaryOrBinaryOp(neg, childNode))
        {
            MakeSrcContained(neg, childNode);
        }
    }

    private void ContainCheckNot(GenTreeUnOp notOp)
    {
        if (notOp.IsContained || !varTypeIsIntegral(notOp.Type) || ((notOp.Flags & GTF_SET_FLAGS) != 0))
        {
            return;
        }

        var childNode = notOp.Op1;
        if (CompilerInstance.opts.OptimizationEnabled && (childNode.Oper is GT_LSH or GT_RSH or GT_RSZ) &&
            IsContainableUnaryOrBinaryOp(notOp, childNode))
        {
            MakeSrcContained(notOp, childNode);
        }
    }
}
#endif
