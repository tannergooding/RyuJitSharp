// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_WASM
using System;

namespace RyuJitSharp;

public partial class Compiler
{
    // General shuffle IR is single-source. Scatter each source into its result
    // lanes, zero-fill the others, and OR the two vectors to retain foldable IR.
    private GenTree gtNewSimdWasmTwoSourceShuffleNode(var_types type, GenTree op1, GenTree op2,
        ReadOnlySpan<uint> selectors, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));

        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        var indexType = UnsignedSimdIndexType(simdBaseType);
        var indices1 = gtNewVconNode(type);
        var indices2 = gtNewVconNode(type);

        for (var index = 0; index < count; index++)
        {
            var selector = selectors[index];
            assert(selector < unchecked((uint)(2 * count)));

            // The source that does not supply this lane zero-fills it through
            // an out-of-range index in the shared shuffle representation.
            if (selector < unchecked((uint)count))
            {
                indices1.SetElementIntegral(indexType, index, selector);
                indices2.SetElementIntegral(indexType, index, count);
            }
            else
            {
                indices1.SetElementIntegral(indexType, index, count);
                indices2.SetElementIntegral(indexType, index, unchecked(selector - (uint)count));
            }
        }

        assert(IsValidForShuffle(indices1, simdSize, simdBaseType, out _, false));
        assert(IsValidForShuffle(indices2, simdSize, simdBaseType, out _, false));

        var scatter1 = gtNewSimdShuffleNode(type, op1, indices1, simdBaseType, simdSize, false);
        var scatter2 = gtNewSimdShuffleNode(type, op2, indices2, simdBaseType, simdSize, false);

        return gtNewSimdBinOpNode(GT_OR, type, scatter1, scatter2, simdBaseType, simdSize);
    }
}
#endif
