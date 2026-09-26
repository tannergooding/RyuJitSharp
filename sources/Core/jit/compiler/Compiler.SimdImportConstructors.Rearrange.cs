// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    private static var_types UnsignedSimdIndexType(var_types baseType)
    {
        return baseType switch
        {
            TYP_BYTE or TYP_UBYTE => TYP_UBYTE,
            TYP_SHORT or TYP_USHORT => TYP_USHORT,
            TYP_INT or TYP_UINT or TYP_FLOAT => TYP_UINT,
            TYP_LONG or TYP_ULONG or TYP_DOUBLE => TYP_ULONG,
            _ => throw new FatalJitException("Unsupported SIMD index type."),
        };
    }

    public GenTree gtNewSimdConcatNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize, bool leftUpper, bool rightUpper)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));
#if TARGET_XARCH
        if (simdSize == 16)
        {
            if (!leftUpper && !rightUpper)
            {
                return gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveLowToHigh, TYP_FLOAT, simdSize,
                    op1, op2);
            }
            if (leftUpper && rightUpper)
            {
                var result = gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveHighToLow, TYP_FLOAT,
                    simdSize, op2, op1);
                result.Flags |= GTF_REVERSE_OPS;
                return result;
            }

            var leftStart = leftUpper ? 2 : 0;
            var rightStart = rightUpper ? 2 : 0;
            var immediate = leftStart | ((leftStart + 1) << 2) |
                (rightStart << 4) | ((rightStart + 1) << 6);
            return gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, TYP_FLOAT, simdSize,
                op1, op2, gtNewIconNode(TYP_INT, immediate));
        }

        var halfSize = (byte)(simdSize / 2);
        var halfType = GetSimdTypeForSize(halfSize);
        if (!leftUpper)
        {
            var upper = rightUpper
                ? gtNewSimdGetUpperNode(halfType, op2, simdBaseType, simdSize)
                : gtNewSimdGetLowerNode(halfType, op2, simdBaseType, simdSize);
            return gtNewSimdWithUpperNode(type, op1, upper, simdBaseType, simdSize);
        }

        var lower = gtNewSimdGetUpperNode(halfType, op1, simdBaseType, simdSize);
        if (rightUpper)
        {
            var result = gtNewSimdWithLowerNode(type, op2, lower, simdBaseType, simdSize);
            result.Flags |= GTF_REVERSE_OPS;
            return result;
        }

        var rightLower = gtNewSimdGetLowerNode(halfType, op2, simdBaseType, simdSize);
        var convert = simdSize == 32 ? NI_Vector_ToVector256Unsafe : NI_Vector_ToVector512Unsafe;
        var widened = gtNewSimdHWIntrinsicNode(type, convert, simdBaseType, halfSize, lower);
        return gtNewSimdWithUpperNode(type, widened, rightLower, simdBaseType, simdSize);
#else
        throw new FatalJitException("gtNewSimdConcatNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdZipNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize, bool upper)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));
        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        if (count == 1)
        {
            var result = op1;
            if (!gtTreeHasSideEffects(op2, GTF_ALL_EFFECT))
            {
                return result;
            }
            var resultLcl = fgInsertCommaFormTemp(ref result);
            return gtNewBinaryNode(GT_COMMA, type, result,
                gtWrapWithSideEffects(resultLcl, op2, GTF_ALL_EFFECT));
        }

#if TARGET_XARCH
        var elementSize = simdBaseType.Size;
        if (simdSize >= 32 &&
            compOpportunisticallyDependsOn(elementSize == 1 ? InstructionSet_AVX512v2 :
                InstructionSet_AVX512))
        {
            var intrinsic = elementSize switch
            {
                1 => simdSize == 32 ? NI_AVX512v2_PermuteVar32x8x2 : NI_AVX512v2_PermuteVar64x8x2,
                2 => simdSize == 32 ? NI_AVX512_PermuteVar16x16x2 : NI_AVX512_PermuteVar32x16x2,
                4 => simdSize == 32 ? NI_AVX512_PermuteVar8x32x2 : NI_AVX512_PermuteVar16x32x2,
                8 => simdSize == 32 ? NI_AVX512_PermuteVar4x64x2 : NI_AVX512_PermuteVar8x64x2,
                _ => throw new FatalJitException("Unsupported SIMD zip element width."),
            };
            var shuffle = gtNewVconNode(type);
            var indexType = UnsignedSimdIndexType(simdBaseType);
            var start = upper ? count / 2 : 0;
            for (var index = 0; index < count; index++)
            {
                var shuffleIndex = start + (index / 2);
                if ((index & 1) != 0)
                {
                    shuffleIndex += count;
                }
                shuffle.SetElementIntegral(indexType, index, shuffleIndex);
            }
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize,
                op1, shuffle, op2);
        }

        if (simdSize == 16)
        {
            var intrinsic = upper ? NI_X86Base_UnpackHigh : NI_X86Base_UnpackLow;
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);
        }

        var halfSize = (byte)(simdSize / 2);
        var halfType = GetSimdTypeForSize(halfSize);
        var left = upper
            ? gtNewSimdGetUpperNode(halfType, op1, simdBaseType, simdSize)
            : gtNewSimdGetLowerNode(halfType, op1, simdBaseType, simdSize);
        var right = upper
            ? gtNewSimdGetUpperNode(halfType, op2, simdBaseType, simdSize)
            : gtNewSimdGetLowerNode(halfType, op2, simdBaseType, simdSize);
        var leftDup = fgMakeMultiUse(ref left);
        var rightDup = fgMakeMultiUse(ref right);
        var lower = gtNewSimdZipNode(halfType, left, right, simdBaseType, halfSize, upper: false);
        var higher = gtNewSimdZipNode(halfType, leftDup, rightDup, simdBaseType, halfSize, upper: true);
        var convert = simdSize == 32 ? NI_Vector_ToVector256Unsafe : NI_Vector_ToVector512Unsafe;
        var widened = gtNewSimdHWIntrinsicNode(type, convert, simdBaseType, halfSize, lower);
        return gtNewSimdWithUpperNode(type, widened, higher, simdBaseType, simdSize);
#else
        throw new FatalJitException("gtNewSimdZipNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdUnzipNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize, bool odd)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));
        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        if (count == 1)
        {
            if (odd)
            {
                var oddResult = gtWrapWithSideEffects(gtNewZeroConNode(type), op2, GTF_ALL_EFFECT);
                return gtWrapWithSideEffects(oddResult, op1, GTF_ALL_EFFECT);
            }
            var result = op1;
            if (!gtTreeHasSideEffects(op2, GTF_ALL_EFFECT))
            {
                return result;
            }
            var resultLcl = fgInsertCommaFormTemp(ref result);
            return gtNewBinaryNode(GT_COMMA, type, result,
                gtWrapWithSideEffects(resultLcl, op2, GTF_ALL_EFFECT));
        }

#if TARGET_XARCH
        var elementSize = simdBaseType.Size;
        var indexType = UnsignedSimdIndexType(simdBaseType);
        if (simdSize == 16)
        {
            if (elementSize == 4)
            {
                return gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, TYP_FLOAT, simdSize,
                    op1, op2, gtNewIconNode(TYP_INT, odd ? 0xDD : 0x88));
            }

            var wideSize = (byte)(simdSize * 2);
            var wideType = GetSimdTypeForSize(wideSize);
            var shuffle = gtNewVconNode(wideType);
            var wideCount = GenTreeVecCon.ElementCount(wideSize, simdBaseType);
            var start = odd ? 1 : 0;
            var lowerCount = (count - start + 1) / 2;
            for (var index = 0; index < wideCount; index++)
            {
                var shuffleIndex = 0;
                if (index < count)
                {
                    shuffleIndex = index < lowerCount
                        ? start + (index * 2)
                        : count + start + ((index - lowerCount) * 2);
                }
                shuffle.SetElementIntegral(indexType, index, shuffleIndex);
            }
            assert(IsValidForShuffle(shuffle, wideSize, simdBaseType, out _, false));
            GenTree wideResult = gtNewSimdHWIntrinsicNode(wideType, NI_Vector_ToVector256Unsafe,
                simdBaseType, simdSize, op1);
            wideResult = gtNewSimdWithUpperNode(wideType, wideResult, op2, simdBaseType, wideSize);
            var shuffled = gtNewSimdShuffleNode(wideType, wideResult, shuffle, simdBaseType, wideSize, false);
            return gtNewSimdGetLowerNode(type, shuffled, simdBaseType, wideSize);
        }

        if (simdSize >= 32 &&
            compOpportunisticallyDependsOn(elementSize == 1 ? InstructionSet_AVX512v2 :
                InstructionSet_AVX512))
        {
            var intrinsic = elementSize switch
            {
                1 => simdSize == 32 ? NI_AVX512v2_PermuteVar32x8x2 : NI_AVX512v2_PermuteVar64x8x2,
                2 => simdSize == 32 ? NI_AVX512_PermuteVar16x16x2 : NI_AVX512_PermuteVar32x16x2,
                4 => simdSize == 32 ? NI_AVX512_PermuteVar8x32x2 : NI_AVX512_PermuteVar16x32x2,
                8 => simdSize == 32 ? NI_AVX512_PermuteVar4x64x2 : NI_AVX512_PermuteVar8x64x2,
                _ => throw new FatalJitException("Unsupported SIMD unzip element width."),
            };
            var shuffle = gtNewVconNode(type);
            var start = odd ? 1 : 0;
            for (var index = 0; index < count; index++)
            {
                var shuffleIndex = index < count / 2
                    ? start + (2 * index)
                    : count + start + (2 * (index - count / 2));
                shuffle.SetElementIntegral(indexType, index, shuffleIndex);
            }
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize,
                op1, shuffle, op2);
        }

        var op1Dup = fgMakeMultiUse(ref op1);
        var op2Dup = fgMakeMultiUse(ref op2);
        var halfSize = (byte)(simdSize / 2);
        var halfType = GetSimdTypeForSize(halfSize);
        var op1Lower = gtNewSimdGetLowerNode(halfType, op1, simdBaseType, simdSize);
        var op1Upper = gtNewSimdGetUpperNode(halfType, op1Dup, simdBaseType, simdSize);
        var op2Lower = gtNewSimdGetLowerNode(halfType, op2, simdBaseType, simdSize);
        var op2Upper = gtNewSimdGetUpperNode(halfType, op2Dup, simdBaseType, simdSize);
        var lower = gtNewSimdUnzipNode(halfType, op1Lower, op1Upper, simdBaseType, halfSize, odd);
        var higher = gtNewSimdUnzipNode(halfType, op2Lower, op2Upper, simdBaseType, halfSize, odd);
        var convert = simdSize == 32 ? NI_Vector_ToVector256Unsafe : NI_Vector_ToVector512Unsafe;
        var widened = gtNewSimdHWIntrinsicNode(type, convert, simdBaseType, halfSize, lower);
        return gtNewSimdWithUpperNode(type, widened, higher, simdBaseType, simdSize);
#else
        throw new FatalJitException("gtNewSimdUnzipNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdReverseNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(varTypeIsArithmetic(simdBaseType));
        var count = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        if (count == 1)
        {
            return op1;
        }
#if TARGET_XARCH
        var shuffle = gtNewVconNode(type);
        var indexType = UnsignedSimdIndexType(simdBaseType);
        for (var index = 0; index < count; index++)
        {
            shuffle.SetElementIntegral(indexType, index, count - 1 - index);
        }
        assert(IsValidForShuffle(shuffle, simdSize, simdBaseType, out _, false));
        return gtNewSimdShuffleNode(type, op1, shuffle, simdBaseType, simdSize, false);
#else
        throw new FatalJitException("gtNewSimdReverseNode requires its target-specific implementation.");
#endif
    }
#endif
}
