// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using System.Runtime.Intrinsics.X86;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree fgOptimizeHWIntrinsic(GenTreeHWIntrinsic node)
    {
        assert(opts.OptimizationEnabled);
        var optimized = fgOptimizeHWIntrinsicAssociative(node);
        if (optimized is not null)
        {
            if (optimized != node)
            {
                assert(!fgIsCommaThrow(optimized));
                optimized.SetMorphed(this);
                return optimized;
            }
            else if (!optimized.Oper.IsHWIntrinsic)
            {
                optimized.SetMorphed(this);
                return optimized;
            }
        }

        var intrinsic = node.HWIntrinsicId;
        var type = node.Type;
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        if ((intrinsic is NI_Vector_Create) && (size is 8 or 16) && (node.Operands.Length == 1))
        {
            // Dot already produces a vector. Avoid scalar extraction followed by
            // broadcasting, including the common Sqrt(Dot(x, x)) normalization.
            var operand = node.GetOp(1);
            GenTree? sqrt = null;
            GenTree? toScalar = null;
            var matches = true;
            if (operand.Oper is GT_INTRINSIC)
            {
                matches = varTypeIsFloating(baseType) && (operand.AsIntrinsic().IntrinsicName is NI_System_Math_Sqrt);
                if (matches)
                {
                    sqrt = operand;
                    operand = operand.AsIntrinsic().Op1;
                }
            }

            if (matches && operand.Oper.IsHWIntrinsic)
            {
                var inner = operand.AsHWIntrinsic();
                if (inner.HWIntrinsicId is NI_Vector_ToScalar)
                {
                    operand = inner.GetOp(1);
                    matches = operand.Oper.IsHWIntrinsic;
                    if (matches)
                    {
                        toScalar = inner;
                        inner = operand.AsHWIntrinsic();
                    }
                }

                if (matches && (inner.HWIntrinsicId is NI_Vector_Dot) && (inner.Type == type))
                {
                    if (toScalar is not null)
                    {
                        DEBUG_DESTROY_NODE(toScalar);
                    }
                    if (sqrt is not null)
                    {
                        node = gtNewSimdSqrtNode(GetSimdTypeForSize(size), inner, baseType, size).AsHWIntrinsic();
                        DEBUG_DESTROY_NODE(sqrt);
                    }
                    else
                    {
                        node = inner;
                    }
                    node.SetMorphed(this);
                    return node;
                }
            }
        }

        var operation = node.GetOperForHWIntrinsicId(out var isScalar, getEffectiveOp: true);

        static GenTree ExtractEffectiveOp(genTreeOps effectiveOperation, GenTreeHWIntrinsic inner, bool destroy)
        {
            GenTree? result = null;
            switch (effectiveOperation)
            {
                case GT_NEG:
                {
                    if (inner.Operands.Length == 2)
                    {
                        // Xarch uses value ^ -0.0 or 0 - value for negation.
                        var floating = varTypeIsFloating(inner.SimdBaseType);
                        result = inner.GetOp(floating ? 1 : 2);
                        if (destroy)
                        {
                            DEBUG_DESTROY_NODE(inner.GetOp(floating ? 2 : 1));
                        }
                    }
                    else
                    {
                        result = inner.GetOp(1);
                    }
                    break;
                }

                case GT_NOT:
                {
                    result = inner.GetOp(1);
                    if ((inner.Operands.Length == 2) && destroy)
                    {
                        DEBUG_DESTROY_NODE(inner.GetOp(2));
                    }
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            if (destroy)
            {
                DEBUG_DESTROY_NODE(inner);
            }
            assert(result is not null);
            return result;
        }

        if (isScalar)
        {
            // Scalar operations zero or copy the upper bits.
            return node;
        }

        switch (operation)
        {
            case GT_ADD:
            {
                var first = node.GetOp(1);
                var second = node.GetOp(2);
                if (first.Oper.IsHWIntrinsic)
                {
                    var inner = first.AsHWIntrinsic();
                    var innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                    var innerType = inner.SimdBaseType;
                    if (innerIsScalar)
                    {
                        break;
                    }

                    if (innerOperation is GT_NEG)
                    {
                        // For integrals, (-value) + constant is canonical.
                        if (varTypeIsIntegral(baseType) && second.Oper.IsCnsVec)
                        {
                            break;
                        }
                        if ((varTypeToSigned(baseType) != varTypeToSigned(innerType)) || !gtCanSwapOrder(first, second))
                        {
                            break;
                        }

                        first = ExtractEffectiveOp(GT_NEG, inner, destroy: true);
                        var id = GetHWIntrinsicIdForBinOp(GT_SUB, second, first, baseType, size, isScalar);
                        node.ChangeHWIntrinsicId(id, second, first);
                        return fgMorphHWIntrinsicRequired(node);
                    }
                    else if (innerOperation is GT_NOT)
                    {
                        if (!varTypeIsIntegral(baseType) || !second.Oper.IsCnsVec ||
                            !second.AsVecCon().IsBroadcast(baseType) || !second.AsVecCon().IsScalarOne(baseType))
                        {
                            break;
                        }

                        first = ExtractEffectiveOp(GT_NOT, inner, destroy: true);
                        DEBUG_DESTROY_NODE(second);
                        DEBUG_DESTROY_NODE(node);
                        node = gtNewSimdUnOpNode(GT_NEG, type, first, baseType, size).AsHWIntrinsic();
#if TARGET_XARCH
                        node.GetOp(1).SetMorphed(this);
#endif
                        return fgMorphHWIntrinsicRequired(node);
                    }
                }
                else if (second.Oper.IsHWIntrinsic)
                {
                    var inner = second.AsHWIntrinsic();
                    var innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                    if ((innerOperation is not GT_NEG) || innerIsScalar ||
                        (varTypeToSigned(baseType) != varTypeToSigned(inner.SimdBaseType)))
                    {
                        break;
                    }

                    second = ExtractEffectiveOp(GT_NEG, inner, destroy: true);
                    var id = GetHWIntrinsicIdForBinOp(GT_SUB, first, second, baseType, size, isScalar);
                    node.ChangeHWIntrinsicId(id, first, second);
                    return fgMorphHWIntrinsicRequired(node);
                }
                break;
            }

            case GT_DIV:
            {
                if (varTypeIsIntegral(baseType))
                {
                    break;
                }

                var first = node.GetOp(1);
                var second = node.GetOp(2);
                if (!second.Oper.IsCnsVec)
                {
                    break;
                }

                GenTreeHWIntrinsic? inner = null;
                var innerOperation = GT_NONE;
                var innerType = TYP_UNDEF;
                if (first.Oper.IsHWIntrinsic)
                {
                    inner = first.AsHWIntrinsic();
                    innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                    innerType = inner.SimdBaseType;
                    if (innerIsScalar)
                    {
                        break;
                    }
                }

                var constant = second.AsVecCon();
                if (innerOperation is GT_NEG)
                {
                    if ((varTypeToSigned(baseType) != varTypeToSigned(innerType)) ||
                        !constant.TryEvaluateUnaryInPlace(GT_NEG, isScalar, baseType))
                    {
                        break;
                    }
                    fgUpdateConstTreeValueNumber(second);
                    assert(inner is not null);
                    first = ExtractEffectiveOp(GT_NEG, inner, destroy: true);
                    node.SetOp(1, first);
                }

                if (!fgGlobalMorph)
                {
                    break;
                }

                var allPrecise = true;
                var count = GenTreeVecCon.ElementCount(size, baseType);
                if (baseType is TYP_DOUBLE)
                {
                    for (var index = 0; index < count; index++)
                    {
                        if (!HasPreciseReciprocal(constant.GetElementFloating(TYP_DOUBLE, index)))
                        {
                            allPrecise = false;
                            break;
                        }
                    }
                }
                else
                {
                    assert(baseType is TYP_FLOAT);
                    for (var index = 0; index < count; index++)
                    {
                        if (!HasPreciseReciprocal((float)constant.GetElementFloating(TYP_FLOAT, index)))
                        {
                            allPrecise = false;
                            break;
                        }
                    }
                }

                if (allPrecise)
                {
                    var reciprocal = gtNewOneConNode(GetSimdTypeForSize(size), baseType).AsVecCon();
                    reciprocal.EvaluateBinaryInPlace(GT_DIV, isScalar, baseType, constant);
                    reciprocal.SetMorphed(this);
                    fgUpdateConstTreeValueNumber(reciprocal);
                    var id = GetHWIntrinsicIdForBinOp(GT_MUL, first, reciprocal, baseType, size, isScalar);
                    node.ChangeHWIntrinsicId(id, first, reciprocal);
                    DEBUG_DESTROY_NODE(second);
                }
                break;
            }

            case GT_MUL:
            {
                var first = node.GetOp(1);
                var second = node.GetOp(2);
                if (!second.Oper.IsCnsVec)
                {
                    break;
                }

                GenTreeHWIntrinsic? inner = null;
                var innerOperation = GT_NONE;
                var innerType = TYP_UNDEF;
                if (first.Oper.IsHWIntrinsic)
                {
                    inner = first.AsHWIntrinsic();
                    innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                    innerType = inner.SimdBaseType;
                    if (innerIsScalar)
                    {
                        break;
                    }
                }

                var constant = second.AsVecCon();
                if (innerOperation is GT_NEG)
                {
                    if ((varTypeToSigned(baseType) != varTypeToSigned(innerType)) ||
                        !constant.TryEvaluateUnaryInPlace(GT_NEG, isScalar, baseType))
                    {
                        break;
                    }
                    fgUpdateConstTreeValueNumber(second);
                    assert(inner is not null);
                    first = ExtractEffectiveOp(GT_NEG, inner, destroy: true);
                    node.SetOp(1, first);
                }

                if (!fgGlobalMorph || !varTypeIsFloating(baseType) || !constant.IsBroadcast(baseType) ||
                    (node.Operands.Length != 2))
                {
                    break;
                }

                var multiplier = constant.GetElementFloating(baseType, 0);
                if (multiplier == -1.0)
                {
                    first = gtNewSimdUnOpNode(GT_NEG, type, first, baseType, size);
#if TARGET_XARCH
                    first.AsHWIntrinsic().GetOp(2).SetMorphed(this);
#endif
                    DEBUG_DESTROY_NODE(second);
                    DEBUG_DESTROY_NODE(node);
                    return fgMorphHWIntrinsicRequired(first.AsHWIntrinsic());
                }

                if ((multiplier == 2.0) && (first.Oper.IsLocal || (fgOrder is FGOrderLinear)))
                {
                    // Before hoisting, only locals avoid introducing a comma temp.
                    var clone = fgMakeMultiUse(ref first);
                    _ = GetHWIntrinsicIdForBinOp(GT_ADD, first, clone, baseType, size, isScalar);
                    var add = gtNewSimdBinOpNode(GT_ADD, GetSimdTypeForSize(size), first, clone, baseType, size);
                    add.SetMorphed(this, doChilren: true);
                    DEBUG_DESTROY_NODE(second);
                    DEBUG_DESTROY_NODE(node);
                    return add;
                }
                break;
            }

            case GT_NEG:
            {
                var first = ExtractEffectiveOp(GT_NEG, node, destroy: false);
                if (!first.Oper.IsHWIntrinsic)
                {
                    break;
                }

                var inner = first.AsHWIntrinsic();
                var innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                if (innerIsScalar || (varTypeToSigned(baseType) != varTypeToSigned(inner.SimdBaseType)))
                {
                    break;
                }

                if (innerOperation is GT_NEG)
                {
                    var result = ExtractEffectiveOp(GT_NEG, inner, destroy: true);
                    _ = ExtractEffectiveOp(GT_NEG, node, destroy: true);
                    return result;
                }
                else if ((innerOperation is GT_MUL or GT_DIV) && (inner.Operands.Length == 2))
                {
                    var second = inner.GetOp(2);
                    if (!second.Oper.IsCnsVec)
                    {
                        break;
                    }

                    if ((innerOperation is GT_DIV) && varTypeIsIntegral(baseType))
                    {
                        var canTransform = true;
                        var count = GenTreeVecCon.ElementCount(size, baseType);
                        for (var index = 0; index < count; index++)
                        {
                            var element = second.AsVecCon().GetElementIntegral(baseType, index);
                            if (element is 1 or -1)
                            {
                                canTransform = false;
                                break;
                            }
                        }
                        if (!canTransform)
                        {
                            break;
                        }
                    }

                    if (!second.AsVecCon().TryEvaluateUnaryInPlace(GT_NEG, isScalar, baseType))
                    {
                        break;
                    }
                    fgUpdateConstTreeValueNumber(second);
                    _ = ExtractEffectiveOp(GT_NEG, node, destroy: true);
                    return inner;
                }
                break;
            }

            case GT_NOT:
            {
                var first = ExtractEffectiveOp(GT_NOT, node, destroy: false);
                if (!first.Oper.IsHWIntrinsic)
                {
                    break;
                }

                var inner = first.AsHWIntrinsic();
                GenTreeHWIntrinsic? conversion = null;
                if (inner.IsConvertMaskToVector)
                {
                    conversion = inner;
                    first = inner.GetOp(1);
                    if (!first.Oper.IsHWIntrinsic)
                    {
                        break;
                    }
                    inner = first.AsHWIntrinsic();
                }

                var innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                var innerType = inner.Type;
                var innerId = inner.HWIntrinsicId;
                var innerBaseType = inner.SimdBaseType;
                var innerSize = inner.SimdSize;
                if (innerOperation is GT_NOT)
                {
                    var result = ExtractEffectiveOp(GT_NOT, inner, destroy: true);
                    _ = ExtractEffectiveOp(GT_NOT, node, destroy: true);
                    return result;
                }

                if (innerOperation.IsCompare)
                {
                    if (innerIsScalar)
                    {
                        break;
                    }
                    assert(inner.Operands.Length == 2);
                    var left = inner.GetOp(1);
                    var right = inner.GetOp(2);
                    var lookupType = GetLookupTypeForCmpOp(innerOperation, innerType, innerBaseType, innerSize, reverseCond: true);
                    var id = GetHWIntrinsicIdForCmpOp(innerOperation, lookupType, left, right,
                        innerBaseType, innerSize, innerIsScalar, reverseCond: true);
                    if (id != NI_Illegal)
                    {
                        inner.ResetHWIntrinsicId(id, left, right);
                        _ = ExtractEffectiveOp(GT_NOT, node, destroy: true);
#if FEATURE_MASKED_HW_INTRINSICS
                        if (lookupType != innerType)
                        {
                            assert(conversion is null);
                            assert(varTypeIsSimd(innerType) && varTypeIsMask(lookupType));
                            inner.Type = lookupType;
                            inner = gtNewSimdCvtMaskToVectorNode(type, inner, innerBaseType, innerSize).AsHWIntrinsic();
                        }
                        else if (conversion is not null)
                        {
                            conversion.SetOp(1, inner);
                            inner = conversion;
                        }
#else
                        assert((lookupType == innerType) && (conversion is null));
#endif
                        return fgMorphHWIntrinsicRequired(inner);
                    }
                }
#if TARGET_XARCH
                else if (innerId is NI_AVX_Compare or NI_AVX512_CompareMask)
                {
                    assert(inner.Operands.Length == 3);
                    var modeNode = inner.GetOp(3);
                    if (!modeNode.Oper.IsCnsIntOrI)
                    {
                        break;
                    }

                    var mode = unchecked((FloatComparisonMode)modeNode.AsIntConCommon().IntegralValue);
                    var reversed = mode switch {
                        FloatComparisonMode.UnorderedEqualNonSignaling or FloatComparisonMode.UnorderedEqualSignaling
                            => FloatComparisonMode.OrderedNotEqualNonSignaling,
                        FloatComparisonMode.OrderedFalseNonSignaling or FloatComparisonMode.OrderedFalseSignaling
                            => FloatComparisonMode.UnorderedTrueNonSignaling,
                        FloatComparisonMode.OrderedNotEqualNonSignaling or FloatComparisonMode.OrderedNotEqualSignaling
                            => FloatComparisonMode.UnorderedEqualNonSignaling,
                        FloatComparisonMode.UnorderedTrueNonSignaling or FloatComparisonMode.UnorderedTrueSignaling
                            => FloatComparisonMode.OrderedFalseNonSignaling,
                        _ => mode,
                    };
                    if (reversed != mode)
                    {
                        _ = ExtractEffectiveOp(GT_NOT, node, destroy: true);
                        modeNode.AsIntConCommon().IntegralValue = (byte)reversed;
                        fgUpdateConstTreeValueNumber(modeNode);
                        return fgMorphHWIntrinsicRequired(inner);
                    }
                }
#endif
                break;
            }

            case GT_SUB:
            {
                if (!fgGlobalMorph)
                {
                    break;
                }

                var first = node.GetOp(1);
                var second = node.GetOp(2);
                if (!second.Oper.IsHWIntrinsic)
                {
                    break;
                }
                var inner = second.AsHWIntrinsic();
                var innerOperation = inner.GetOperForHWIntrinsicId(out var innerIsScalar, getEffectiveOp: true);
                if ((innerOperation is not GT_NEG) || innerIsScalar ||
                    (varTypeToSigned(baseType) != varTypeToSigned(inner.SimdBaseType)))
                {
                    break;
                }

                second = ExtractEffectiveOp(GT_NEG, inner, destroy: true);
                if (first.Oper.IsHWIntrinsic)
                {
                    var left = first.AsHWIntrinsic();
                    var leftOperation = left.GetOperForHWIntrinsicId(out var leftIsScalar, getEffectiveOp: true);
                    if ((leftOperation is GT_NEG) && !leftIsScalar &&
                        (varTypeToSigned(baseType) == varTypeToSigned(left.SimdBaseType)))
                    {
                        first = ExtractEffectiveOp(GT_NEG, left, destroy: true);
                        node.SetOp(1, second);
                        node.SetOp(2, first);
                        break;
                    }
                }

                var id = GetHWIntrinsicIdForBinOp(GT_ADD, first, second, baseType, size, isScalar);
                node.ChangeHWIntrinsicId(id, first, second);
                return fgMorphHWIntrinsicRequired(node);
            }
        }

        return node;
    }

    public GenTree fgMorphHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        var allArgsAreConst = true;
        var intrinsic = tree.HWIntrinsicId;
        var canBenefitFromConstantProp = HWIntrinsicInfo.CanBenefitFromConstantProp(intrinsic);
        var hasImmediateOperand = HWIntrinsicInfo.HasImmediateOperand(intrinsic);

        foreach (ref var use in tree.UseEdges)
        {
            assert(use is not null);
            use = fgMorphTree(use);
            var operand = use;
            if (operand.Oper.IsConst)
            {
                if (hasImmediateOperand && operand.Oper.IsCnsIntOrI)
                {
                    operand.Flags |= GTF_DONT_CSE;
                }
                else if (canBenefitFromConstantProp && operand.Oper.IsCnsVec &&
                         tree.ShouldConstantProp(operand, operand.AsVecCon()))
                {
                    operand.Flags |= GTF_DONT_CSE;
                }
            }
            else
            {
                allArgsAreConst = false;
            }

            // A promoted whole struct that survives as an intrinsic operand
            // must use dependent promotion.
            if (operand.Oper is GT_LCL_VAR)
            {
                var local = operand.AsLclVar();
                if (lvaGetDesc(local.LclNum).lvPromoted)
                {
                    lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.simdUserForcesDep);
                }
            }
        }

        gtUpdateNodeOperSideEffects(tree);
        foreach (var operand in tree.Operands)
        {
            tree.Flags |= operand.Flags & GTF_ALL_EFFECT;
        }

        var type = tree.Type;
        var result = gtFoldExpr(tree);
        if (result.Oper.IsHWIntrinsic)
        {
            tree = result.AsHWIntrinsic();
            if (allArgsAreConst && tree.IsVectorCreate)
            {
                foreach (var operand in tree.Operands)
                {
                    operand.Flags |= GTF_DONT_CSE;
                }
            }

            // Folding can insert an inner mask operation whose comparison
            // can now be inverted; morph it before the outer conversion.
            var operandIndex = tree.IsConvertVectorToMask || tree.IsConvertMaskToVector ? 1 : 0;
#if TARGET_ARM64
            if (tree.IsConvertVectorToMask)
            {
                operandIndex = 2;
            }
#endif
            if (operandIndex != 0)
            {
                var inner = tree.GetOp(operandIndex);
                if (inner.Oper.IsHWIntrinsic)
                {
                    inner = fgMorphHWIntrinsicRequired(inner.AsHWIntrinsic());
                    if (inner.Oper.IsHWIntrinsic)
                    {
                        inner = fgMorphHWIntrinsicOptional(inner.AsHWIntrinsic());
                    }
                    inner.SetMorphed(this);
                    tree.SetOp(operandIndex, inner);
                }
            }

            result = fgMorphHWIntrinsicRequired(tree);
            if (result.Oper.IsHWIntrinsic)
            {
                result = fgMorphHWIntrinsicOptional(result.AsHWIntrinsic());
            }
        }

        assert(type == result.Type);
        result.SetMorphed(this);

        return result;
    }

    public GenTree fgMorphHWIntrinsicRequired(GenTreeHWIntrinsic tree)
    {
        var intrinsic = tree.HWIntrinsicId;
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var size = tree.SimdSize;
        var operation = tree.GetOperForHWIntrinsicId(out var isScalar);

        if (tree.IsCommutativeHWIntrinsic)
        {
            assert(tree.Operands.Length == 2);
            var first = tree.GetOp(1);
            var second = tree.GetOp(2);
            if (first.Oper.IsConst)
            {
                (first, second) = (second, first);
                tree.SetOp(1, first);
                tree.SetOp(2, second);
            }

            if ((operation is GT_EQ or GT_NE) && second.Oper.IsCnsVec &&
                first.IsVectorPerElementMask(this, baseType, size))
            {
                // Comparing a per-element mask to zero for equality, or to
                // all bits set for inequality, complements that mask.
                var reverse = operation is GT_EQ ? second.IsVectorZero : second.IsVectorAllBitsSet;
                if (reverse)
                {
                    GenTree? replacement = null;
                    var firstType = first.Type;
#if FEATURE_MASKED_HW_INTRINSICS
                    if (first.IsConvertVectorToMask)
                    {
                        var conversion = first.AsHWIntrinsic();
#if TARGET_XARCH
                        first = conversion.GetOp(1);
#elif TARGET_ARM64
                        first = conversion.GetOp(2);
                        DEBUG_DESTROY_NODE(conversion.GetOp(1));
#else
#error Unsupported platform
#endif
                        tree.SetOp(1, first);
                        firstType = first.Type;
                        DEBUG_DESTROY_NODE(conversion);
                    }

                    if (firstType is TYP_MASK)
                    {
#if TARGET_XARCH
                        replacement = gtNewSimdHWIntrinsicNode(firstType, NI_AVX512_NotMask, baseType, size, first);
#endif
                    }
                    else
#endif
                    {
                        replacement = gtNewSimdUnOpNode(GT_NOT, firstType, first, baseType, size);
#if TARGET_XARCH
                        replacement.AsHWIntrinsic().GetOp(2).SetMorphed(this);
#endif
                    }

                    if (replacement is not null)
                    {
                        DEBUG_DESTROY_NODE(second);
                        DEBUG_DESTROY_NODE(tree);
#if FEATURE_MASKED_HW_INTRINSICS
                        if (firstType != type)
                        {
                            replacement = fgMorphHWIntrinsicRequired(replacement.AsHWIntrinsic());
                            if (replacement.Oper.IsHWIntrinsic)
                            {
                                replacement = fgMorphHWIntrinsicOptional(replacement.AsHWIntrinsic());
                            }
                            replacement.SetMorphed(this);
                            replacement = type is TYP_MASK
                                ? gtNewSimdCvtVectorToMaskNode(type, replacement, baseType, size)
                                : gtNewSimdCvtMaskToVectorNode(type, replacement, baseType, size);
                        }
#endif
                        return fgMorphHWIntrinsicRequired(replacement.AsHWIntrinsic());
                    }
                }
            }
        }
        else if (operation.IsCompare)
        {
            assert(tree.Operands.Length == 2);
            var first = tree.GetOp(1);
            var second = tree.GetOp(2);
            if (!isScalar && first.Oper.IsCnsVec)
            {
                // Scalar comparisons may copy upper bits from the first operand.
                var swapped = operation.SwapRelop;
                var lookupType = GetLookupTypeForCmpOp(swapped, type, baseType, size);
                var id = GetHWIntrinsicIdForCmpOp(swapped, lookupType, second, first,
                    baseType, size, isScalar);
                if (id != NI_Illegal)
                {
                    tree.ResetHWIntrinsicId(id, second, first);
#if FEATURE_MASKED_HW_INTRINSICS
                    if (lookupType != type)
                    {
                        assert(varTypeIsSimd(type) && varTypeIsMask(lookupType));
                        tree.Type = lookupType;
                        tree = gtNewSimdCvtMaskToVectorNode(type, tree, baseType, size).AsHWIntrinsic();
                        return fgMorphHWIntrinsicRequired(tree);
                    }
#endif
                }
            }
        }
#if TARGET_XARCH
        else if (intrinsic is NI_AVX_Compare or NI_AVX512_CompareMask)
        {
            assert(tree.Operands.Length == 3);
            var first = tree.GetOp(1);
            var second = tree.GetOp(2);
            var modeNode = tree.GetOp(3);
            if (first.Oper.IsCnsVec && modeNode.Oper.IsCnsIntOrI)
            {
                var mode = unchecked((FloatComparisonMode)modeNode.AsIntConCommon().IntegralValue);
                if (mode is FloatComparisonMode.UnorderedEqualNonSignaling or FloatComparisonMode.OrderedNotEqualNonSignaling or
                    FloatComparisonMode.UnorderedEqualSignaling or FloatComparisonMode.OrderedNotEqualSignaling)
                {
                    tree.SetOp(1, second);
                    tree.SetOp(2, first);
                }
            }
        }
#endif

#if !TARGET_WASM
        if (intrinsic is NI_Vector_CreateGeometricSequence)
        {
            assert(tree.Operands.Length == 2);
            var first = tree.GetOp(1);
            var second = tree.GetOp(2);
            if (second.Oper.IsConst)
            {
#if TARGET_ARM64
                var canGenerate = !varTypeIsLong(baseType) || first.Oper.IsConst || (size == 8);
#elif TARGET_XARCH
                var canGenerate = first.Oper.IsConst || (size != 32) || !varTypeIsIntegral(baseType) ||
                    compOpportunisticallyDependsOn(InstructionSet_AVX2);
#else
#error Unsupported platform
#endif
                if (canGenerate)
                {
                    return fgMorphTree(gtNewSimdCreateGeometricSequenceNode(type, first, second, baseType, size));
                }
            }
        }
#endif

        switch (operation)
        {
            case GT_SUB:
            {
                if (!fgGlobalMorph || !varTypeIsIntegral(baseType))
                {
                    break;
                }

                var first = tree.GetOp(1);
                var second = tree.GetOp(2);
                if (second.Oper.IsCnsVec)
                {
                    if (!second.AsVecCon().TryEvaluateUnaryInPlace(GT_NEG, isScalar, baseType))
                    {
                        break;
                    }
                    fgUpdateConstTreeValueNumber(second);
                    var id = GetHWIntrinsicIdForBinOp(GT_ADD, first, second, baseType, size, isScalar);
                    tree.ChangeHWIntrinsicId(id, first, second);
                    return fgMorphHWIntrinsicRequired(tree);
                }
                else if (first.Oper.IsCnsVec)
                {
                    if (first.IsVectorZero)
                    {
#if TARGET_ARM64
                        // Xarch represents integer negation as zero minus the operand.
                        second = gtNewSimdUnOpNode(GT_NEG, type, second, baseType, size);
                        DEBUG_DESTROY_NODE(first);
                        DEBUG_DESTROY_NODE(tree);
                        return fgMorphHWIntrinsicRequired(second.AsHWIntrinsic());
#endif
                    }
                    else
                    {
                        second = gtNewSimdUnOpNode(GT_NEG, type, second, baseType, size);
#if TARGET_XARCH
                        second.AsHWIntrinsic().GetOp(varTypeIsFloating(baseType) ? 2 : 1).SetMorphed(this);
#endif
                        var id = GetHWIntrinsicIdForBinOp(GT_ADD, second, first, baseType, size, isScalar);
                        tree.ChangeHWIntrinsicId(id, second, first);
                        second = fgMorphHWIntrinsicRequired(second.AsHWIntrinsic());
                        if (second.Oper.IsHWIntrinsic)
                        {
                            second = fgMorphHWIntrinsicOptional(second.AsHWIntrinsic());
                        }
                        second.SetMorphed(this);
                        tree.SetOp(1, second);
                        return fgMorphHWIntrinsicRequired(tree);
                    }
                }
                break;
            }

#if TARGET_ARM64
            case GT_XOR:
            {
                var first = tree.GetOp(1);
                var second = tree.GetOp(2);
                if (second.IsVectorAllBitsSet)
                {
                    first = gtNewSimdUnOpNode(GT_NOT, type, first, baseType, size);
                    DEBUG_DESTROY_NODE(second);
                    DEBUG_DESTROY_NODE(tree);
                    return fgMorphHWIntrinsicRequired(first.AsHWIntrinsic());
                }

                if (varTypeIsFloating(baseType) && second.IsVectorNegativeZero(baseType))
                {
                    first = gtNewSimdUnOpNode(GT_NEG, type, first, baseType, size);
                    DEBUG_DESTROY_NODE(second);
                    DEBUG_DESTROY_NODE(tree);
                    return fgMorphHWIntrinsicRequired(first.AsHWIntrinsic());
                }
                break;
            }
#endif
        }

        return opts.OptimizationEnabled ? fgOptimizeHWIntrinsic(tree) : tree;
    }

    public static GenTree fgMorphHWIntrinsicOptional(GenTreeHWIntrinsic tree) => tree;
}
#endif
