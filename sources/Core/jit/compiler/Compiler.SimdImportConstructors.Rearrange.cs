// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_WASM
using System;
#endif

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

#if TARGET_ARM64
        if (simdSize == 8)
        {
            var result = op1;
            if (leftUpper)
            {
                var resultDup = fgMakeMultiUse(ref result);
                result = gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_Arm64_InsertSelectedScalar, TYP_UINT, simdSize,
                    result, gtNewIconNode(TYP_INT, 0), resultDup, gtNewIconNode(TYP_INT, 1));
            }

            return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_Arm64_InsertSelectedScalar, TYP_UINT, simdSize,
                result, gtNewIconNode(TYP_INT, 1), op2, gtNewIconNode(TYP_INT, rightUpper ? 1 : 0));
        }
#endif

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
                gtPrepareOperandsForReordering(ref op1, ref op2);
                return gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveHighToLow, TYP_FLOAT,
                    simdSize, op2, op1);
            }

            var leftStart = leftUpper ? 2 : 0;
            var rightStart = rightUpper ? 2 : 0;
            var immediate = leftStart | ((leftStart + 1) << 2) |
                (rightStart << 4) | ((rightStart + 1) << 6);
            return gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, TYP_FLOAT, simdSize,
                op1, op2, gtNewIconNode(TYP_INT, immediate));
        }
#elif TARGET_WASM
        var elementCount = GenTreeVecCon.ElementCount(simdSize, simdBaseType);
        var half = elementCount / 2;
        var leftStart = leftUpper ? half : 0;
        var rightStart = rightUpper ? half : 0;
        Span<uint> selectors = stackalloc uint[16];

        for (var index = 0; index < elementCount; index++)
        {
            selectors[index] = unchecked((uint)(index < half
                ? leftStart + index
                : elementCount + rightStart + index - half));
        }

        return gtNewSimdWasmTwoSourceShuffleNode(type, op1, op2, selectors, simdBaseType, simdSize);
#elif !TARGET_ARM64
        throw new FatalJitException("gtNewSimdConcatNode requires its target-specific implementation.");
#endif

#if TARGET_XARCH || TARGET_ARM64
        var halfSize = (byte)(simdSize / 2);
        var halfType = GetSimdTypeForSize(halfSize);
        if (!leftUpper)
        {
            var upper = rightUpper
                ? gtNewSimdGetUpperNode(halfType, op2, simdBaseType, simdSize)
                : gtNewSimdGetLowerNode(halfType, op2, simdBaseType, simdSize);
            return gtNewSimdWithUpperNode(type, op1, upper, simdBaseType, simdSize);
        }

        if (rightUpper)
        {
            gtPrepareOperandsForReordering(ref op1, ref op2);
            var leftLower = gtNewSimdGetUpperNode(halfType, op1, simdBaseType, simdSize);
            return gtNewSimdWithLowerNode(type, op2, leftLower, simdBaseType, simdSize);
        }

        var lower = gtNewSimdGetUpperNode(halfType, op1, simdBaseType, simdSize);
        var rightLower = gtNewSimdGetLowerNode(halfType, op2, simdBaseType, simdSize);
#if TARGET_XARCH
        var convert = simdSize == 32 ? NI_Vector_ToVector256Unsafe : NI_Vector_ToVector512Unsafe;
#else
        var convert = NI_Vector_ToVector128Unsafe;
#endif
        var widened = gtNewSimdHWIntrinsicNode(type, convert, simdBaseType, halfSize, lower);
        return gtNewSimdWithUpperNode(type, widened, rightLower, simdBaseType, simdSize);
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
            if (!gtTreeHasSideEffects(op2, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF))
            {
                return result;
            }
            if (result.Oper.IsInvariant)
            {
                return gtWrapWithSideEffects(result, op2, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF);
            }
            var resultLcl = fgInsertCommaFormTemp(ref result);
            return gtNewBinaryNode(GT_COMMA, type, result,
                gtWrapWithSideEffects(resultLcl, op2, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF));
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
#elif TARGET_ARM64
        var intrinsic = upper ? NI_AdvSimd_Arm64_ZipHigh : NI_AdvSimd_Arm64_ZipLow;
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);
#elif TARGET_WASM
        var start = upper ? count / 2 : 0;
        Span<uint> selectors = stackalloc uint[16];

        for (var index = 0; index < count; index++)
        {
            var element = start + index / 2;
            selectors[index] = unchecked((uint)((index & 1) == 0 ? element : count + element));
        }

        return gtNewSimdWasmTwoSourceShuffleNode(type, op1, op2, selectors, simdBaseType, simdSize);
#else
        throw new FatalJitException("gtNewSimdZipNode requires its target-specific implementation.");
#endif

#if TARGET_XARCH
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
                var oddResult = gtWrapWithSideEffects(gtNewZeroConNode(type), op2,
                    GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF);
                return gtWrapWithSideEffects(oddResult, op1, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF);
            }
            var result = op1;
            if (!gtTreeHasSideEffects(op2, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF))
            {
                return result;
            }
            if (result.Oper.IsInvariant)
            {
                return gtWrapWithSideEffects(result, op2, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF);
            }
            var resultLcl = fgInsertCommaFormTemp(ref result);
            return gtNewBinaryNode(GT_COMMA, type, result,
                gtWrapWithSideEffects(resultLcl, op2, GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF));
        }

#if TARGET_ARM64
        // return odd ? AdvSimd.Arm64.UnzipOdd(op1, op2) : AdvSimd.Arm64.UnzipEven(op1, op2);

        var intrinsic = odd ? NI_AdvSimd_Arm64_UnzipOdd : NI_AdvSimd_Arm64_UnzipEven;
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);
#elif TARGET_XARCH
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
#elif TARGET_WASM
        // WASM lacks a native deinterleave. The lower result half gathers op1's even/odd elements and
        // the upper half gathers op2's, so build the selectors and let the shared two-source shuffle
        // scatter them.
        var half = count / 2;
        var start = odd ? 1 : 0;
        Span<uint> selectors = stackalloc uint[16];

        for (var index = 0; index < count; index++)
        {
            selectors[index] = unchecked((uint)(index < half
                ? start + (2 * index)
                : count + start + (2 * (index - half))));
        }

        return gtNewSimdWasmTwoSourceShuffleNode(type, op1, op2, selectors, simdBaseType, simdSize);
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
#if TARGET_ARM64
        if ((simdSize == 8) && (simdBaseType is TYP_INT or TYP_UINT or TYP_FLOAT))
        {
            // return AdvSimd.ReverseElement32(op1.AsInt64()).As<T>();

            return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_ReverseElement32, TYP_LONG, simdSize, op1);
        }
#endif

        // return Shuffle(op1, indices);

        var shuffle = gtNewVconNode(type);
        var indexType = UnsignedSimdIndexType(simdBaseType);
        for (var index = 0; index < count; index++)
        {
            shuffle.SetElementIntegral(indexType, index, count - 1 - index);
        }

        assert(IsValidForShuffle(shuffle, simdSize, simdBaseType, out _, false));
        return gtNewSimdShuffleNode(type, op1, shuffle, simdBaseType, simdSize, false);
    }
#endif
}
