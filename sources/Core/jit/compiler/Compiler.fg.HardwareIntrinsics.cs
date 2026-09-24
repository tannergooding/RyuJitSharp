// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree? fgOptimizeHWIntrinsicAssociative(GenTreeHWIntrinsic tree)
    {
        assert(opts.OptimizationEnabled);
        var intrinsic = tree.HWIntrinsicId;
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var size = tree.SimdSize;
        if (!varTypeIsSimd(type) && !varTypeIsMask(type))
        {
            return null;
        }

        var operation = tree.GetOperForHWIntrinsicId(out var isScalar);
        var needsMatchingBaseType = false;
        switch (operation)
        {
            case GT_ADD:
            case GT_MUL:
            {
                if (!varTypeIsIntegral(baseType))
                {
                    return null;
                }
                needsMatchingBaseType = true;
                break;
            }

            case GT_AND:
            case GT_OR:
            case GT_XOR:
            {
                break;
            }

            default:
            {
                return null;
            }
        }

        var first = tree.GetOp(1);
        var effectiveFirst = first.EffectiveVal;
        if (!effectiveFirst.Oper.IsHWIntrinsic)
        {
            return null;
        }

        var inner = effectiveFirst.AsHWIntrinsic();
        var innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar);
        if ((innerOperation != operation) || (innerIsScalar != isScalar))
        {
            return null;
        }

        if (needsMatchingBaseType && (inner.SimdBaseType != baseType))
        {
            return null;
        }

        if (!inner.GetOp(2).Oper.IsConst || !tree.GetOp(2).Oper.IsConst)
        {
            return null;
        }

        // Intermediate comma VNs cannot be left stale outside global morph.
        if (!fgGlobalMorph && (effectiveFirst != first))
        {
            return null;
        }

        var firstConstant = inner.GetOp(2);
        var secondConstant = tree.GetOp(2);
        assert(firstConstant.Type == type);
        assert(secondConstant.Type == type);

#if TARGET_XARCH && FEATURE_MASKED_HW_INTRINSICS
        var fold = gtNewSimdHWIntrinsicNode(type, intrinsic, baseType, size, firstConstant, secondConstant);
        var folded = gtFoldExprHWIntrinsic(fold);
        assert(folded == firstConstant);
        assert(folded.Oper.IsConst && (folded.Type == type));

        if (effectiveFirst != first)
        {
            first._vnPair = tree._vnPair;
            DEBUG_DESTROY_NODE(secondConstant);
            DEBUG_DESTROY_NODE(tree);

            return first;
        }

        tree.SetOp(1, inner.GetOp(1));
        tree.SetOp(2, inner.GetOp(2));
        DEBUG_DESTROY_NODE(secondConstant);
        DEBUG_DESTROY_NODE(inner);
        assert(tree.GetOp(2) == firstConstant);

        return tree;
#else
        NYI("Hardware-intrinsic constant reassociation requires the target's folding dispatcher");
        fatal(CORJIT_IMPLLIMITATION);
        return null;
#endif
    }
}
#endif
