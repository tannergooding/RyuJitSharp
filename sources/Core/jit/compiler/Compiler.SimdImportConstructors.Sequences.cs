// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public GenTree gtNewSimdGetIndicesNode(var_types type, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));

#if TARGET_XARCH
        var indices = gtNewVconNode(type);
        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        for (var index = 0; index < count; index++)
        {
            if (varTypeIsFloating(simdBaseType))
            {
                indices.SetElementFloating(simdBaseType, index, index);
            }
            else
            {
                indices.SetElementIntegral(simdBaseType, index, index);
            }
        }
        return indices;
#else
        throw new FatalJitException("gtNewSimdGetIndicesNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdCreateSequenceNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));
#if TARGET_XARCH
        GenTree result;
        var isPartial = true;
        if (op2.Oper.IsConst)
        {
            var sequence = gtNewVconNode(type);
            var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
            if (varTypeIsFloating(simdBaseType))
            {
                assert(op2.Oper.IsCnsFltOrDbl);
                if (simdBaseType == TYP_FLOAT)
                {
                    var start = 0.0f;
                    if (op1.Oper.IsConst)
                    {
                        assert(op1.Oper.IsCnsFltOrDbl);
                        start = (float)op1.AsDblCon().DconVal;
                        isPartial = false;
                    }
                    var step = (float)op2.AsDblCon().DconVal;
                    for (var index = 0; index < count; index++)
                    {
                        sequence.SetElementFloating(simdBaseType, index, (float)((index * step) + start));
                    }
                }
                else
                {
                    var start = 0.0;
                    if (op1.Oper.IsConst)
                    {
                        assert(op1.Oper.IsCnsFltOrDbl);
                        start = op1.AsDblCon().DconVal;
                        isPartial = false;
                    }
                    var step = op2.AsDblCon().DconVal;
                    for (var index = 0; index < count; index++)
                    {
                        sequence.SetElementFloating(simdBaseType, index, (index * step) + start);
                    }
                }
            }
            else
            {
                assert(op2.Oper.IsIntegralConst);
                var start = 0UL;
                if (op1.Oper.IsConst)
                {
                    assert(op1.Oper.IsIntegralConst);
                    start = unchecked((ulong)op1.AsIntConCommon().IntegralValue);
                    isPartial = false;
                }
                var step = unchecked((ulong)op2.AsIntConCommon().IntegralValue);
                for (var index = 0; index < count; index++)
                {
                    var value = unchecked(start + ((ulong)index * step));
                    sequence.SetElementIntegral(simdBaseType, index, unchecked((long)value));
                }
            }
            result = sequence;
        }
        else
        {
            var indices = gtNewSimdGetIndicesNode(type, simdBaseType, simdSize);
            result = gtNewSimdBinOpNode(GT_MUL, type, indices, op2, simdBaseType, simdSize);
        }

        if (isPartial)
        {
            var start = gtNewSimdCreateBroadcastNode(type, op1, simdBaseType, simdSize);
            result = gtNewSimdBinOpNode(GT_ADD, type, result, start, simdBaseType, simdSize);
        }
        return result;
#else
        throw new FatalJitException("gtNewSimdCreateSequenceNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdCreateAlternatingSequenceNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));

        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        if (count == 1)
        {
            var result = gtNewSimdCreateBroadcastNode(type, op1, simdBaseType, simdSize);
            if (!gtTreeHasSideEffects(op2, GTF_ALL_EFFECT))
            {
                return result;
            }
            var resultLcl = fgInsertCommaFormTemp(ref result);
            return gtNewBinaryNode(GT_COMMA, type, result,
                gtWrapWithSideEffects(resultLcl, op2, GTF_ALL_EFFECT));
        }

        if (op1.Oper.IsConst && op2.Oper.IsConst)
        {
            var constant = gtNewVconNode(type);
            for (var index = 1; index < count; index += 2)
            {
                if (varTypeIsIntegral(simdBaseType))
                {
                    constant.SetElementIntegral(simdBaseType, index - 1, op1.AsIntConCommon().IntegralValue);
                    constant.SetElementIntegral(simdBaseType, index, op2.AsIntConCommon().IntegralValue);
                }
                else
                {
                    constant.SetElementFloating(simdBaseType, index - 1, op1.AsDblCon().DconVal);
                    constant.SetElementFloating(simdBaseType, index, op2.AsDblCon().DconVal);
                }
            }
            return constant;
        }

        var even = gtNewSimdCreateBroadcastNode(type, op1, simdBaseType, simdSize);
        var odd = gtNewSimdCreateBroadcastNode(type, op2, simdBaseType, simdSize);
        return gtNewSimdZipNode(type, even, odd, simdBaseType, simdSize, upper: false);
    }
#endif
}
