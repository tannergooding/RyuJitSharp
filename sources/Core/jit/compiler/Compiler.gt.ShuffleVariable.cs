// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public GenTree gtNewSimdShuffleVariableNode(var_types type, GenTree op1, GenTree op2, var_types baseType,
        byte simdSize, bool isShuffleNative)
    {
        assert(varTypeIsSimd(type) && (GetSimdTypeForSize(simdSize) == type));
        assert((op1.Type == type) && (op2.Type == type));
        assert(!op2.Oper.IsCnsVec || isShuffleNative);
#if TARGET_XARCH
        var elementSize = baseType.Size;
        var elementCount = simdSize / elementSize;
        var safeIndices = isShuffleNative ? null : fgMakeMultiUse(ref op2);
        var signedComparisonHint = false;
        GenTree result;
        if (simdSize == 64)
        {
            if (elementSize == 1)
            {
                assert(compIsaSupportedDebugOnly(InstructionSet_AVX512v2));
            }
            var intrinsic = elementSize switch {
                1 => NI_AVX512v2_PermuteVar64x8,
                2 => NI_AVX512_PermuteVar32x16,
                4 => NI_AVX512_PermuteVar16x32,
                _ => NI_AVX512_PermuteVar8x64,
            };
            result = gtNewSimdHWIntrinsicNode(type, intrinsic, baseType, simdSize, op2, op1);
            result.Flags |= GTF_REVERSE_OPS;
        }
        else if ((elementSize == 1) && (simdSize == 16))
        {
            result = gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, baseType, simdSize, op1, op2);
            signedComparisonHint = true;
        }
        else if ((elementSize == 1) && (simdSize == 32) && compOpportunisticallyDependsOn(InstructionSet_AVX512v2))
        {
            result = gtNewSimdHWIntrinsicNode(type, NI_AVX512v2_PermuteVar32x8, baseType, simdSize, op2, op1);
            result.Flags |= GTF_REVERSE_OPS;
        }
        else if ((elementSize == 2) && compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            var intrinsic = simdSize == 16 ? NI_AVX512_PermuteVar8x16 : NI_AVX512_PermuteVar16x16;
            result = gtNewSimdHWIntrinsicNode(type, intrinsic, baseType, simdSize, op2, op1);
            result.Flags |= GTF_REVERSE_OPS;
        }
        else if ((elementSize == 4) && ((simdSize == 32) || compOpportunisticallyDependsOn(InstructionSet_AVX)))
        {
            if (simdSize == 32)
            {
                assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_PermuteVar8x32, baseType, simdSize, op2, op1);
                result.Flags |= GTF_REVERSE_OPS;
            }
            else
            {
                assert((simdSize == 16) && compIsaSupportedDebugOnly(InstructionSet_AVX));
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX_PermuteVar, TYP_FLOAT, simdSize, op1, op2);
            }
        }
        else if ((elementSize == 8) && (simdSize == 32) && compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            result = gtNewSimdHWIntrinsicNode(type, NI_AVX512_PermuteVar4x64, baseType, simdSize, op2, op1);
            result.Flags |= GTF_REVERSE_OPS;
        }
        else if ((elementSize == 8) && (simdSize == 16) && compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            var duplicate = fgMakeMultiUse(ref op1);
            result = gtNewSimdHWIntrinsicNode(type, NI_AVX512_PermuteVar2x64x2, baseType, simdSize, op1, op2, duplicate);
        }
        else if ((elementSize == 8) && ((simdSize == 32) || compOpportunisticallyDependsOn(InstructionSet_AVX)))
        {
            assert((simdSize == 32) ? compIsaSupportedDebugOnly(InstructionSet_AVX2) :
                ((simdSize == 16) && compIsaSupportedDebugOnly(InstructionSet_AVX)));
            var originalBaseType = baseType;
            if (varTypeIsFloating(baseType))
            {
                baseType = TYP_LONG;
            }
            var shiftIntrinsic = simdSize == 32 ? NI_AVX2_ShiftLeftLogical : NI_X86Base_ShiftLeftLogical;
            op2 = gtNewSimdHWIntrinsicNode(type, shiftIntrinsic, baseType, simdSize, op2, gtNewIconNode(TYP_INT, 1));
            baseType = varTypeIsFloating(originalBaseType) ? TYP_FLOAT :
                varTypeIsUnsigned(originalBaseType) ? TYP_UINT : TYP_INT;

            // Duplicate each low 32-bit index, then set the low bit of every second
            // index: a 64-bit element becomes a pair of consecutive 32-bit elements.
            var immediate = gtNewIconNode(TYP_INT, 0b10100000);
            if (varTypeIsFloating(baseType))
            {
                var duplicate = fgMakeMultiUse(ref op2);
                var shuffle = simdSize == 32 ? NI_AVX_Shuffle : NI_X86Base_Shuffle;
                op2 = gtNewSimdHWIntrinsicNode(type, shuffle, baseType, simdSize, op2, duplicate, immediate);
            }
            else
            {
                var shuffle = simdSize == 32 ? NI_AVX2_Shuffle : NI_X86Base_Shuffle;
                op2 = gtNewSimdHWIntrinsicNode(type, shuffle, baseType, simdSize, op2, immediate);
            }
            var offsets = gtNewVconNode(type);
            for (var index = 0; index < simdSize / 4; index++)
            {
                offsets.SimdVal.u32[index] = (uint)(index & 1);
            }
            op2 = gtNewSimdBinOpNode(GT_OR, type, op2, offsets, baseType, simdSize);
            if (simdSize == 32)
            {
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_PermuteVar8x32, baseType, simdSize, op2, op1);
                result.Flags |= GTF_REVERSE_OPS;
            }
            else
            {
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX_PermuteVar, TYP_FLOAT, simdSize, op1, op2);
            }
        }
        else if (simdSize == 32)
        {
            assert(compIsaSupportedDebugOnly(InstructionSet_AVX2) && (elementSize <= 2));
            signedComparisonHint = elementSize == 1;
            GenTree duplicateIndices;
            GenTree duplicateIndices2;
            if (elementSize > 1)
            {
                op2 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_ShiftLeftLogical, baseType, simdSize,
                    op2, gtNewIconNode(TYP_INT, 1));
                baseType = varTypeIsUnsigned(baseType) ? TYP_UBYTE : TYP_BYTE;
                var selectors = gtNewVconNode(type);
                selectors.SimdVal.u64[0] = 0x0606040402020000;
                selectors.SimdVal.u64[1] = 0x0E0E0C0C0A0A0808;
                selectors.SimdVal.u64[2] = 0x0606040402020000;
                selectors.SimdVal.u64[3] = 0x0E0E0C0C0A0A0808;
                op2 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, op2, selectors);

                var offsets = gtNewVconNode(type);
                for (var index = 0; index < simdSize; index++)
                {
                    offsets.SimdVal.u8[index] = (byte)(index & (elementSize - 1));
                }
                op2 = gtNewSimdBinOpNode(GT_OR, type, op2, offsets, baseType, simdSize);
                duplicateIndices = fgMakeMultiUse(ref op2);
                duplicateIndices2 = gtCloneExpr(duplicateIndices);
            }
            else
            {
                duplicateIndices = safeIndices is not null ? gtCloneExpr(safeIndices) : fgMakeMultiUse(ref op2);
                duplicateIndices2 = gtCloneExpr(duplicateIndices);
            }

            GenTree swapped;
            if (!op1.Oper.IsCnsVec)
            {
                var duplicate = fgMakeMultiUse(ref op1);
                var duplicate2 = gtCloneExpr(duplicate);
                swapped = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute2x128, baseType, simdSize,
                    duplicate, duplicate2, gtNewIconNode(TYP_INT, 1));
            }
            else
            {
                swapped = fgMakeMultiUse(ref op1);
                ref var value = ref swapped.AsVecCon().SimdVal;
                (value.u64[0], value.u64[2]) = (value.u64[2], value.u64[0]);
                (value.u64[1], value.u64[3]) = (value.u64[3], value.u64[1]);
            }

            // PSHUFB only selects within each 128-bit lane. Blend a normal and
            // swapped shuffle; XOR bit 4 in the high lane to identify cross-lane indices.
            var shuffle1 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, op1, op2);
            var shuffle2 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, swapped, duplicateIndices);
            var laneBits = gtNewVconNode(type);
            laneBits.SimdVal.u64[2] = 0x1010101010101010;
            laneBits.SimdVal.u64[3] = 0x1010101010101010;
            var normalizedIndices = gtNewSimdBinOpNode(GT_XOR, type, duplicateIndices2, laneBits, baseType, simdSize);
            var lastLaneIndex = gtNewVconNode(type);
            lastLaneIndex.SimdVal.u64[0] = 0x0F0F0F0F0F0F0F0F;
            lastLaneIndex.SimdVal.u64[1] = 0x0F0F0F0F0F0F0F0F;
            lastLaneIndex.SimdVal.u64[2] = 0x0F0F0F0F0F0F0F0F;
            lastLaneIndex.SimdVal.u64[3] = 0x0F0F0F0F0F0F0F0F;
            var selection = gtNewSimdCmpOpNode(GT_GT, type, normalizedIndices, lastLaneIndex, TYP_BYTE, simdSize);
            result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_BlendVariable, baseType, simdSize, shuffle1, shuffle2, selection);
        }
        else
        {
            assert((simdSize == 16) && (elementSize > 1));
            if (varTypeIsFloating(baseType))
            {
                baseType = elementSize == 4 ? TYP_UINT : TYP_ULONG;
            }

            // Expand each element index into consecutive byte indices for PSHUFB.
            op2 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_ShiftLeftLogical, baseType, simdSize,
                op2, gtNewIconNode(TYP_INT, int.TrailingZeroCount(elementSize)));
            baseType = varTypeIsUnsigned(baseType) ? TYP_UBYTE : TYP_BYTE;
            var selectors = gtNewVconNode(type);
            for (var index = 0; index < elementCount; index++)
            {
                for (var offset = 0; offset < elementSize; offset++)
                {
                    selectors.SimdVal.u8[(index * elementSize) + offset] = (byte)(index * elementSize);
                }
            }
            op2 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, baseType, simdSize, op2, selectors);
            var offsets = gtNewVconNode(type);
            for (var index = 0; index < simdSize; index++)
            {
                offsets.SimdVal.u8[index] = (byte)(index & (elementSize - 1));
            }
            op2 = gtNewSimdBinOpNode(GT_OR, type, op2, offsets, baseType, simdSize);
            result = gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, baseType, simdSize, op1, op2);
        }

        if (!isShuffleNative)
        {
            assert(safeIndices is not null);
            var comparisonType = elementSize switch {
                1 => TYP_UBYTE,
                2 => TYP_USHORT,
                4 => TYP_UINT,
                _ => TYP_ULONG,
            };
            var subtractComparand = false;
            if (!compOpportunisticallyDependsOn(InstructionSet_AVX512))
            {
                comparisonType = elementSize switch {
                    1 => TYP_BYTE,
                    2 => TYP_SHORT,
                    4 => TYP_INT,
                    _ => TYP_LONG,
                };
                if (!signedComparisonHint)
                {
                    // Bias unsigned indices into signed order. If PSHUFB already
                    // zeroes high-bit indices, its result needs no such bias.
                    subtractComparand = true;
                    var bias = 1UL << ((elementSize * 8) - 1);
                    var subtraction = gtNewSimdCreateBroadcastNode(type, gtNewLconNode(unchecked((long)bias)),
                        comparisonType, simdSize);
                    safeIndices = gtNewSimdBinOpNode(GT_SUB, type, safeIndices, subtraction, comparisonType, simdSize);
                }
            }

            var comparandValue = (ulong)elementCount;
            if (subtractComparand)
            {
                comparandValue = unchecked(comparandValue - (1UL << ((elementSize * 8) - 1)));
            }
            var comparand = gtNewSimdCreateBroadcastNode(type, gtNewLconNode(unchecked((long)comparandValue)),
                comparisonType, simdSize);
            assert(comparisonType.Size == elementSize);
            var mask = gtNewSimdCmpOpNode(GT_LT, type, safeIndices, comparand, comparisonType, simdSize);
            result = gtNewSimdBinOpNode(GT_AND, type, result, mask, baseType, simdSize);
        }
        else
        {
            assert(safeIndices is null);
        }
        return result;
#else
        throw new NotImplementedException("Target-specific SIMD variable shuffle construction is not ported.");
#endif
    }
#endif
}
