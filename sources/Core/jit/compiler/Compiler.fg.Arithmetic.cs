// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public partial class Compiler
{
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
