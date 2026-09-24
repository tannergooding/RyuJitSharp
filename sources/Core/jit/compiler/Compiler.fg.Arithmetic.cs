// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTreeOp? fgMorphCommutative(GenTreeOp tree)
    {
        assert(varTypeIsIntegralOrI(tree.Type));
        assert(tree.Oper is GT_ADD or GT_MUL or GT_OR or GT_AND or GT_XOR);
        if (opts.OptimizationDisabled)
        {
            return null;
        }

        var left = tree.Op1.EffectiveVal;
        var operation = tree.Oper;
        if ((left.Oper != operation) || !tree.Op2.Oper.IsCnsIntOrI || !left.AsOp().Op2.Oper.IsCnsIntOrI ||
            left.AsOp().Op1.Oper.IsCnsIntOrI)
        {
            return null;
        }

        // Folding through a comma spine would invalidate its intermediate VNs
        // outside global morph.
        if (!fgGlobalMorph && (left != tree.Op1))
        {
            return null;
        }

        if (operation.MayOverflow && (tree.HasOverflowCheck || left.HasOverflowCheck))
        {
            return null;
        }

        var first = left.AsOp().Op2.AsIntCon();
        var second = tree.Op2.AsIntCon();
        if (!varTypeIsIntegralOrI(tree.Type) || (first.Type is TYP_REF) || (first.Type != second.Type))
        {
            return null;
        }

        var folded = gtFoldExprConst(gtNewBinaryNode(operation, first.Type, first, second));
        if (!folded.Oper.IsCnsIntOrI)
        {
            return null;
        }

        var constant = folded.AsIntCon();
        first.IconValue = constant.IconValue;
        first._vnPair = constant._vnPair;
        first.FieldSeq = constant.FieldSeq;
        left = tree.Op1;
        left._vnPair = tree._vnPair;
        first.SetMorphed(this);
        return left.AsOp();
    }

    public static bool fgOperIsBitwiseRotationRoot(genTreeOps operation) => operation is GT_OR or GT_XOR;

    public GenTree? fgRecognizeAndMorphBitwiseRotation(GenTree tree)
    {
        // Stores, calls and volatile reads cannot be merged. Exceptions may be
        // retained because the same value is still evaluated by the rotation.
        if ((tree.Flags & (GTF_PERSISTENT_SIDE_EFFECTS | GTF_ORDER_SIDEEFF)) != 0)
        {
            return null;
        }

        assert(fgOperIsBitwiseRotationRoot(tree.Oper));
        var left = tree.AsOp().Op1;
        var right = tree.AsOp().Op2;
        GenTreeOp leftShift;
        GenTreeOp rightShift;
        if ((left.Oper is GT_LSH) && (right.Oper is GT_RSZ))
        {
            leftShift = left.AsOp();
            rightShift = right.AsOp();
        }
        else if ((left.Oper is GT_RSZ) && (right.Oper is GT_LSH))
        {
            leftShift = right.AsOp();
            rightShift = left.AsOp();
        }
        else
        {
            return null;
        }

        if (!GenTree.Compare(leftShift.Op1, rightShift.Op1))
        {
            return null;
        }

        var value = leftShift.Op1;
        var type = value.Type.ActualType;
        var bitSize = type.Size * 8;
        noway_assert(bitSize is 32 or 64);
        var leftIndex = leftShift.Op2;
        var rightIndex = rightShift.Op2;
        nint minimumMask = bitSize - 1;
        nint leftMask = -1;
        nint rightMask = -1;
        if (leftIndex.Oper is GT_AND)
        {
            if (!leftIndex.AsOp().Op2.Oper.IsCnsIntOrI)
            {
                return null;
            }

            leftMask = leftIndex.AsOp().Op2.AsIntCon().IconValue;
            leftIndex = leftIndex.AsOp().Op1;
        }

        if (rightIndex.Oper is GT_AND)
        {
            if (!rightIndex.AsOp().Op2.Oper.IsCnsIntOrI)
            {
                return null;
            }

            rightMask = rightIndex.AsOp().Op2.AsIntCon().IconValue;
            rightIndex = rightIndex.AsOp().Op1;
        }

        // Every low bit used by the target's shift count must survive the masks.
        if (((minimumMask & leftMask) != minimumMask) || ((minimumMask & rightMask) != minimumMask))
        {
            return null;
        }

        GenTreeOp? indexWithAdd = null;
        var indexWithoutAdd = leftIndex;
        var rotateOperation = GT_NONE;
        GenTree? rotateIndex = null;
        if (leftIndex.Oper is GT_ADD)
        {
            indexWithAdd = leftIndex.AsOp();
            indexWithoutAdd = rightIndex;
            rotateOperation = GT_ROR;
        }
        else if (rightIndex.Oper is GT_ADD)
        {
            indexWithAdd = rightIndex.AsOp();
            rotateOperation = GT_ROL;
        }

        // Recognize complementary counts y and -y + N, optionally masked,
        // or constants whose sum is N, where N is the value's bit width.
        if ((indexWithAdd is not null) && !indexWithAdd.HasOverflowCheck)
        {
            if (indexWithAdd.Op2.Oper.IsCnsIntOrI && (indexWithAdd.Op2.AsIntCon().IconValue == bitSize) &&
                (indexWithAdd.Op1.Oper is GT_NEG) &&
                GenTree.Compare(indexWithAdd.Op1.AsUnOp().Op1, indexWithoutAdd))
            {
#if !TARGET_64BIT
                // Variable long shifts have helpers on x86; rotations do not.
                if (!indexWithoutAdd.Oper.IsCnsIntOrI && (bitSize == 64))
                {
                    return null;
                }
#endif
                rotateIndex = indexWithoutAdd;
            }
        }
        else if (leftIndex.Oper.IsCnsIntOrI && rightIndex.Oper.IsCnsIntOrI &&
            (unchecked(leftIndex.AsIntCon().IconValue + rightIndex.AsIntCon().IconValue) == bitSize))
        {
            rotateOperation = GT_ROL;
            rotateIndex = leftIndex;
        }

        if (rotateIndex is null)
        {
            return null;
        }

        noway_assert(rotateOperation.IsRotate);
        // Keep the count in range for subsequent transforms and lowering.
        // Targets with implicit masking can remove this explicit mask later.
        if (rotateIndex.Oper.IsCnsIntOrI)
        {
            var amount = rotateIndex.AsIntCon().IconValue;
            if ((amount < 0) || (amount > minimumMask))
            {
                rotateIndex.AsIntCon().IconValue = amount & minimumMask;
            }
        }
        else
        {
            rotateIndex = gtNewBinaryNode(GT_AND, rotateIndex.Type.ActualType, rotateIndex, gtNewIconNode(TYP_INT, minimumMask));
            rotateIndex.SetMorphed(this, doChilren: true);
        }

        var effects = tree.Flags & GTF_ALL_EFFECT;
        if (fgGlobalMorph)
        {
            tree.AsOp().Op1 = value;
            tree.AsOp().Op2 = rotateIndex;
            tree.SetOper(rotateOperation, GenTree.PRESERVE_VN);
            tree.Flags &= GTF_COMMON_MASK;
            var childEffects = (value.Flags | rotateIndex.Flags) & GTF_ALL_EFFECT;
            noway_assert((effects & childEffects) == childEffects);
        }
        else
        {
            tree = gtNewBinaryNode(rotateOperation, type, value, rotateIndex);
            noway_assert(effects == (tree.Flags & GTF_ALL_EFFECT));
        }

        return tree;
    }

    public GenTree? fgOptimizeMultiply(GenTreeOp multiply)
    {
        assert(multiply.Oper is GT_MUL);
#if TARGET_WASM
        // The native transform does not support 64-bit integer operations on wasm32.
        if (multiply.Type is TYP_LONG)
        {
            return null;
        }
#endif
        assert(varTypeIsIntOrI(multiply.Type) || varTypeIsFloating(multiply.Type));
        assert(!multiply.HasOverflowCheck);
        var left = multiply.Op1;
        var right = multiply.Op2;
        assert(multiply.Type == left.Type.ActualType);
        assert(multiply.Type == right.Type.ActualType);

        if (opts.OptimizationEnabled && right.Oper.IsCnsFltOrDbl)
        {
            var multiplier = right.AsDblCon().DconVal;
            if (multiplier == 1.0)
            {
                return left;
            }

            if (multiplier == -1.0)
            {
                if (left.Oper is GT_NEG)
                {
                    return left.AsUnOp().Op1;
                }

                return new GenTreeUnOp(GT_NEG, multiply.Type, left, multiply, NodeThreading.None) {
                    _vnPair = multiply._vnPair,
                };
            }

            // Introducing a comma temp before hoisting can inhibit it; only
            // duplicate locals until rationalization remorphs the linear IR.
            if ((multiplier == 2.0) && (left.Oper.IsLocal || (fgOrder is FGOrderLinear)))
            {
                var clone = fgMakeMultiUse(ref left);
                var add = gtNewBinaryNode(GT_ADD, multiply.Type, left, clone);
                add.SetMorphed(this, doChilren: true);
                return add;
            }
        }

        if ((left.Oper is GT_NEG) && opts.OptimizationEnabled)
        {
            if ((right.Oper.IsCnsIntOrI && !right.IsIconHandle()) || right.Oper.IsCnsFltOrDbl)
            {
                multiply.Op1 = left.AsUnOp().Op1;
                if (right.Oper.IsCnsIntOrI)
                {
                    var constant = right.AsIntCon();
                    constant.SetValueTruncating(unchecked(-constant.IconValue));
                    constant.FieldSeq = null;
                }
                else
                {
                    assert(right.Oper.IsCnsFltOrDbl);
                    right.AsDblCon().DconVal = -right.AsDblCon().DconVal;
                }

                fgUpdateConstTreeValueNumber(right);
                left = multiply.Op1;
            }
        }

        if (right.Oper.IsIntegralConst)
        {
            // Long multiplication on a 32-bit target has already been lowered.
            assert(right.Oper.IsCnsIntOrI);
            var multiplier = right.AsIntConCommon().IconValue;
            if (multiplier == 0)
            {
                if ((left.Flags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) == 0)
                {
                    return right;
                }

                multiply.SetOper(GT_COMMA, GenTree.PRESERVE_VN);
                multiply.Flags &= GTF_COMMON_MASK;
                return multiply;
            }

#if TARGET_XARCH
            var optimizeScale = compCodeOpt is not SMALL_CODE;
#else
            var optimizeScale = false;
#endif
            var magnitude = unchecked((nuint)(multiplier >= 0 ? multiplier : -multiplier));
            var lowestBit = magnitude & unchecked(0 - magnitude);
            var changeToShift = false;
            if (magnitude == lowestBit)
            {
                // The native-width minimum already shifts out the sign bit,
                // so it does not need a separate negation.
                if ((multiplier < 0) && (multiplier != nint.MinValue))
                {
                    left = gtNewUnaryNode(GT_NEG, left.Type.ActualType, left);
                    multiply.Op1 = left;
                    fgMorphTreeDone(left);
                }

                if (magnitude == 1)
                {
                    return left;
                }

                right.AsIntConCommon().IconValue = BitOperations.Log2(magnitude);
                changeToShift = true;
            }
            else if (optimizeScale && (lowestBit > 1) && jitIsScaleIndexMul(unchecked((nint)lowestBit)))
            {
                var shift = BitOperations.Log2(lowestBit);
                var factor = magnitude >> shift;
                if (factor is 3 or 5 or 9)
                {
                    if ((multiplier < 0) && (multiplier != nint.MinValue))
                    {
                        left = gtNewUnaryNode(GT_NEG, left.Type.ActualType, left);
                        multiply.Op1 = left;
                        fgMorphTreeDone(left);
                    }

                    var factorNode = gtNewIconNodeWithVN(this, multiply.Type, (nint)factor);
                    factorNode.SetMorphed(this);
                    left = gtNewBinaryNode(GT_MUL, multiply.Type, left, factorNode);
                    multiply.Op1 = left;
                    fgMorphTreeDone(left);
                    right.AsIntConCommon().IconValue = shift;
                    changeToShift = true;
                }
            }

            if (changeToShift)
            {
                fgUpdateConstTreeValueNumber(right);
                multiply.SetOper(GT_LSH, GenTree.PRESERVE_VN);
                multiply.Flags &= GTF_COMMON_MASK;
                return multiply;
            }
        }

        return null;
    }

    public static GenTree? fgOptimizeBitwiseAnd(GenTreeOp and)
    {
        assert(and.Oper is GT_AND);
        if ((and.Type is TYP_INT) && and.Op1.Oper.IsCompare && and.Op2.IsIntegralConst(1))
        {
            return and.Op1;
        }

        return null;
    }

    public GenTree? fgOptimizeBitwiseXor(GenTreeOp xor)
    {
        assert(xor.Oper is GT_XOR);
        var left = xor.Op1;
        var right = xor.Op2;
        if (right.IsIntegralConst(0))
        {
            return left;
        }
        else if (right.IsIntegralConst(-1))
        {
            return new GenTreeUnOp(GT_NOT, xor.Type, left, xor, NodeThreading.None) {
                _vnPair = xor._vnPair,
            };
        }
        else if (right.IsIntegralConst(1) && left.Oper.IsCompare)
        {
            return gtReverseCond(left);
        }
        else if (varTypeIsFloating(xor.Type) && right.IsFloatNegativeZero)
        {
            return new GenTreeUnOp(GT_NEG, xor.Type, left, xor, NodeThreading.None) {
                _vnPair = xor._vnPair,
            };
        }

        return null;
    }
}
