// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public GenTree gtNewSimdSumNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsSimd(simdType) && op1 is not null && op1.Type == simdType);
        assert(varTypeIsArithmetic(simdBaseType));
#if TARGET_XARCH
        if (varTypeIsFloating(simdBaseType))
        {
            // Reduce within each 128-bit lane first, then combine lanes in the same grouping
            // as the recursive lower/upper reduction; floating-point addition is not associative.
            if (simdBaseType == TYP_FLOAT)
            {
                var op1Shuffled = fgMakeMultiUse(ref op1);
                var permute = simdSize == 64 ? NI_AVX512_Permute4x32 : NI_AVX_Permute;
                if (simdSize > 16 || compOpportunisticallyDependsOn(InstructionSet_AVX))
                {
                    op1 = gtNewSimdHWIntrinsicNode(simdType, permute, simdBaseType, simdSize,
                        op1, gtNewIconNode(TYP_INT, 0b10110001));
                    op1 = gtNewSimdBinOpNode(GT_ADD, simdType, op1, op1Shuffled,
                        simdBaseType, simdSize);
                    op1Shuffled = fgMakeMultiUse(ref op1);
                    op1 = gtNewSimdHWIntrinsicNode(simdType, permute, simdBaseType, simdSize,
                        op1, gtNewIconNode(TYP_INT, 0b01001110));
                }
                else
                {
                    assert(simdSize == 16);
                    op1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Shuffle,
                        simdBaseType, simdSize, op1, op1Shuffled,
                        gtNewIconNode(TYP_INT, 0b10110001));
                    op1Shuffled = fgMakeMultiUse(ref op1Shuffled);
                    op1 = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, op1, op1Shuffled,
                        simdBaseType, simdSize);
                    op1Shuffled = fgMakeMultiUse(ref op1);
                    op1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Shuffle,
                        simdBaseType, simdSize, op1, op1Shuffled,
                        gtNewIconNode(TYP_INT, 0b01001110));
                    op1Shuffled = fgMakeMultiUse(ref op1Shuffled);
                }
                op1 = gtNewSimdBinOpNode(GT_ADD, simdType, op1, op1Shuffled,
                    simdBaseType, simdSize);
            }
            else
            {
                var op1Shuffled = fgMakeMultiUse(ref op1);
                var permute = simdSize == 64 ? NI_AVX512_Permute2x64 : NI_AVX_Permute;
                if (simdSize > 16 || compOpportunisticallyDependsOn(InstructionSet_AVX))
                {
                    op1 = gtNewSimdHWIntrinsicNode(simdType, permute, simdBaseType, simdSize,
                        op1, gtNewIconNode(TYP_INT, 0b01010101));
                }
                else
                {
                    assert(simdSize == 16);
                    op1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Shuffle,
                        simdBaseType, simdSize, op1, op1Shuffled,
                        gtNewIconNode(TYP_INT, 0b0001));
                    op1Shuffled = fgMakeMultiUse(ref op1Shuffled);
                }
                op1 = gtNewSimdBinOpNode(GT_ADD, simdType, op1, op1Shuffled,
                    simdBaseType, simdSize);
            }

            if (simdSize == 64)
            {
                var lane1 = fgMakeMultiUse(ref op1);
                var lane2 = fgMakeMultiUse(ref op1);
                var lane3 = fgMakeMultiUse(ref op1);
                var lane0 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_GetLower128,
                    simdBaseType, 64, op1);
                lane1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX512_ExtractVector128,
                    simdBaseType, 64, lane1, gtNewIconNode(TYP_INT, 1));
                lane2 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX512_ExtractVector128,
                    simdBaseType, 64, lane2, gtNewIconNode(TYP_INT, 2));
                lane3 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX512_ExtractVector128,
                    simdBaseType, 64, lane3, gtNewIconNode(TYP_INT, 3));
                var lowerSum = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, lane0, lane1,
                    simdBaseType, 16);
                var upperSum = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, lane2, lane3,
                    simdBaseType, 16);
                simdSize = 16;
                op1 = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, lowerSum, upperSum,
                    simdBaseType, 16);
            }
            else if (simdSize == 32)
            {
                var upper = fgMakeMultiUse(ref op1);
                op1 = gtNewSimdGetLowerNode(TYP_SIMD16, op1, simdBaseType, 32);
                upper = gtNewSimdGetUpperNode(TYP_SIMD16, upper, simdBaseType, 32);
                simdSize = 16;
                op1 = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, op1, upper,
                    simdBaseType, 16);
            }
            assert(simdSize == 16);
            return gtNewSimdToScalarNode(type, op1, simdBaseType, 16);
        }

        if (simdSize == 64)
        {
            var upper = fgMakeMultiUse(ref op1);
            op1 = gtNewSimdGetLowerNode(TYP_SIMD32, op1, simdBaseType, simdSize);
            upper = gtNewSimdGetUpperNode(TYP_SIMD32, upper, simdBaseType, simdSize);
            simdSize = 32;
            op1 = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD32, op1, upper, simdBaseType, 32);
        }
        if (simdSize == 32)
        {
            var upper = fgMakeMultiUse(ref op1);
            op1 = gtNewSimdGetLowerNode(TYP_SIMD16, op1, simdBaseType, simdSize);
            upper = gtNewSimdGetUpperNode(TYP_SIMD16, upper, simdBaseType, simdSize);
            simdSize = 16;
            op1 = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, op1, upper, simdBaseType, 16);
        }
        assert(simdSize == 16);
        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        var elementSize = simdBaseType.Size;
        var shiftVal = elementSize * count / 2;
        while (shiftVal >= elementSize)
        {
            var tmp = fgMakeMultiUse(ref op1);
            var shifted = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_ShiftRightLogical128BitLane,
                simdBaseType, simdSize, op1, gtNewIconNode(TYP_INT, shiftVal));
            op1 = gtNewSimdBinOpNode(GT_ADD, TYP_SIMD16, shifted, tmp, simdBaseType, simdSize);
            shiftVal /= 2;
        }
        return gtNewSimdToScalarNode(type, op1, simdBaseType, simdSize);
#else
        throw new FatalJitException("gtNewSimdSumNode requires its target-specific implementation.");
#endif
    }
#endif
}
