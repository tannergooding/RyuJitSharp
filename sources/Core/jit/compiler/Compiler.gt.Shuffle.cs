// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public bool IsValidForShuffle(GenTree indices, byte simdSize, var_types baseType, out bool canBecomeValid,
        bool isShuffleNative)
    {
        canBecomeValid = false;
#if TARGET_XARCH
        if (simdSize == 32)
        {
            if (!compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                return false;
            }
        }
        else if (simdSize == 64)
        {
            if (varTypeIsByte(baseType) && !compOpportunisticallyDependsOn(InstructionSet_AVX512v2))
            {
                return false;
            }
        }
        else
        {
            assert(simdSize == 16);
        }
#endif
        canBecomeValid = true;
        return true;
    }

    public GenTree gtNewSimdShuffleNode(var_types type, GenTree op1, GenTree op2, var_types baseType, byte simdSize,
        bool isShuffleNative)
    {
        assert(varTypeIsSimd(type) && (GetSimdTypeForSize(simdSize) == type));
        assert((op1.Type == type) && (op2.Type == type));
        if (!op2.Oper.IsCnsVec)
        {
            return gtNewSimdShuffleVariableNode(type, op1, op2, baseType, simdSize, isShuffleNative);
        }

        assert(varTypeIsArithmetic(baseType));
        var elementSize = baseType.Size;
        var elementCount = simdSize / elementSize;
        var invalidIndex = false;
        var identity = true;
        var allOutOfRange = true;
        for (var index = 0; index < elementCount; index++)
        {
            var value = op2.GetIntegralVectorConstElement(index, baseType);
            if (value >= (ulong)elementCount)
            {
                invalidIndex = true;
            }
            else
            {
                allOutOfRange = false;
            }
            identity &= value == (ulong)index;
        }

        if (isShuffleNative && invalidIndex)
        {
            return gtNewSimdShuffleVariableNode(type, op1, op2, baseType, simdSize, true);
        }
        if (identity)
        {
            return op1;
        }
        if (allOutOfRange)
        {
            return gtWrapWithSideEffects(gtNewZeroConNode(type), op1, GTF_ALL_EFFECT);
        }

#if TARGET_XARCH
        byte control = 0;
        var crossLane = false;
        var needsZero = varTypeIsSmall(baseType) && (simdSize <= 16);
        var differsByLane = false;
        var indices = default(simd_t);
        var mask = default(simd_t);
        for (var index = 0; index < elementCount; index++)
        {
            var value = op2.GetIntegralVectorConstElement(index, baseType);
            if (value < (ulong)elementCount)
            {
                crossLane |= ((((ulong)index ^ value) * (ulong)elementSize) & ~15UL) != 0;
                control |= unchecked((byte)(value << (index * (elementCount / 2))));
                for (var offset = 0; offset < elementSize; offset++)
                {
                    indices.u8[(index * elementSize) + offset] = (byte)((value * (ulong)elementSize) + (ulong)offset);
                    mask.u8[(index * elementSize) + offset] = 0xFF;
                }
            }
            else
            {
                needsZero = true;
                for (var offset = 0; offset < elementSize; offset++)
                {
                    indices.u8[(index * elementSize) + offset] = 0xFF;
                }
            }
            if ((index * elementSize) >= 16)
            {
                differsByLane |= ((indices.u8[index * elementSize] ^ indices.u8[(index * elementSize) & 15]) & 15) != 0;
            }
        }

        GenTree result;
        if (simdSize == 32)
        {
            assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
            if ((varTypeIsByte(baseType) && !compOpportunisticallyDependsOn(InstructionSet_AVX512v2)) ||
                (varTypeIsShort(baseType) && !compOpportunisticallyDependsOn(InstructionSet_AVX512)) ||
                (!crossLane && (needsZero || (elementSize < 4) || ((elementSize == 4) && differsByLane))))
            {
                baseType = varTypeIsUnsigned(baseType) ? TYP_UBYTE : TYP_BYTE;
                var leftWants = 0;
                var rightWants = 0;
                var nonDefaultMask = false;
                var selection = default(simd_t);
                for (var index = 0; index < simdSize; index++)
                {
                    var value = indices.u8[index];
                    var wants = value < 16 ? 1 : value < 32 ? 2 : 0;
                    if (index < 16)
                    {
                        leftWants |= wants;
                    }
                    else
                    {
                        rightWants |= wants;
                    }
                    value ^= (byte)(index & 0x10);
                    selection.u8[index] = (value is >= 16 and < 32) ? (byte)0xFF : (byte)0;
                    if (indices.u8[index] < 32)
                    {
                        indices.u8[index] &= 15;
                    }
                    nonDefaultMask |= indices.u8[index] != (index & 15);
                }

                // Each lane can use one source lane, or combine a normal and swapped shuffle.
                if ((leftWants != 3) && (rightWants != 3))
                {
                    result = op1;
                    var laneControl = (leftWants == 2 ? 1 : 0) | (rightWants != 1 ? 16 : 0);
                    if (laneControl != 16)
                    {
                        var duplicate = fgMakeMultiUse(ref result);
                        result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute2x128, baseType, simdSize,
                            result, duplicate, gtNewIconNode(TYP_INT, laneControl));
                    }
                    if (nonDefaultMask)
                    {
                        var indexNode = gtNewVconNode(type);
                        indexNode.SimdVal = indices;
                        result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, result, indexNode);
                    }
                }
                else
                {
                    var duplicate = fgMakeMultiUse(ref op1);
                    var duplicate2 = gtCloneExpr(duplicate);
                    GenTree swapped = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute2x128, baseType, simdSize,
                        duplicate, duplicate2, gtNewIconNode(TYP_INT, 1));
                    if (nonDefaultMask)
                    {
                        var indexNode = gtNewVconNode(type);
                        indexNode.SimdVal = indices;
                        op2 = indexNode;
                        var duplicateIndices = fgMakeMultiUse(ref op2);
                        op1 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, op1, op2);
                        swapped = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, swapped, duplicateIndices);
                    }
                    var selectionNode = gtNewVconNode(type);
                    selectionNode.SimdVal = selection;
                    result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_BlendVariable, baseType, simdSize, op1, swapped, selectionNode);
                }
                return result;
            }

            if (elementSize == 4)
            {
                if (!crossLane && !differsByLane)
                {
                    assert(!needsZero);
                    var immediate = 0;
                    for (var index = 0; index < 4; index++)
                    {
                        immediate |= (int)(op2.GetIntegralVectorConstElement(index, baseType) & 3) << (index * 2);
                    }
                    op2 = gtNewIconNode(TYP_INT, immediate);
                    if (varTypeIsFloating(baseType))
                    {
                        var duplicate = fgMakeMultiUse(ref op1);
                        return gtNewSimdHWIntrinsicNode(type, NI_AVX_Shuffle, baseType, simdSize, op1, duplicate, op2);
                    }
                    return gtNewSimdHWIntrinsicNode(type, NI_AVX2_Shuffle, baseType, simdSize, op1, op2);
                }
                for (var index = 0; index < elementCount; index++)
                {
                    indices.u32[index] = (uint)(indices.u8[index * elementSize] / elementSize);
                }
                var indexNode = gtNewVconNode(type);
                indexNode.SimdVal = indices;
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_PermuteVar8x32, baseType, simdSize, indexNode, op1);
            }
            else if (elementSize == 2)
            {
#if DEBUG
                assert(crossLane && canUseEvexEncodingDebugOnly());
#endif
                for (var index = 0; index < elementCount; index++)
                {
                    indices.u16[index] = (ushort)(indices.u8[index * elementSize] / elementSize);
                }
                var indexNode = gtNewVconNode(type);
                indexNode.SimdVal = indices;
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX512_PermuteVar16x16, baseType, simdSize, indexNode, op1);
            }
            else if (elementSize == 1)
            {
                assert(crossLane && compIsaSupportedDebugOnly(InstructionSet_AVX512v2));
                var indexNode = gtNewVconNode(type);
                indexNode.SimdVal = indices;
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX512v2_PermuteVar32x8, baseType, simdSize, indexNode, op1);
            }
            else
            {
                assert(elementSize == 8);
                if (!crossLane)
                {
                    assert(!needsZero);
                    var immediate = 0;
                    for (var index = 0; index < elementCount; index++)
                    {
                        immediate |= (int)(op2.GetIntegralVectorConstElement(index, baseType) & 1) << index;
                    }
                    op2 = gtNewIconNode(TYP_INT, immediate);
                    var duplicate = fgMakeMultiUse(ref op1);
                    return gtNewSimdHWIntrinsicNode(type, NI_AVX_Shuffle, TYP_DOUBLE, simdSize, op1, duplicate, op2);
                }
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute4x64, baseType, simdSize,
                    op1, gtNewIconNode(TYP_INT, control));
            }
        }
        else if (simdSize == 64)
        {
            if (!crossLane)
            {
                if ((elementSize == 8) && !needsZero)
                {
                    var immediate = 0;
                    for (var index = 0; index < elementCount; index++)
                    {
                        immediate |= (int)(op2.GetIntegralVectorConstElement(index, baseType) & 1) << index;
                    }
                    op2 = gtNewIconNode(TYP_INT, immediate);
                    var duplicate = fgMakeMultiUse(ref op1);
                    return gtNewSimdHWIntrinsicNode(type, NI_AVX512_Shuffle, TYP_DOUBLE, simdSize, op1, duplicate, op2);
                }
                if ((elementSize == 4) && !needsZero && !differsByLane)
                {
                    var immediate = 0;
                    for (var index = 0; index < 4; index++)
                    {
                        immediate |= (int)(op2.GetIntegralVectorConstElement(index, baseType) & 3) << (index * 2);
                    }
                    op2 = gtNewIconNode(TYP_INT, immediate);
                    if (varTypeIsFloating(baseType))
                    {
                        var duplicate = fgMakeMultiUse(ref op1);
                        return gtNewSimdHWIntrinsicNode(type, NI_AVX512_Shuffle, baseType, simdSize, op1, duplicate, op2);
                    }
                    return gtNewSimdHWIntrinsicNode(type, NI_AVX512_Shuffle, baseType, simdSize, op1, op2);
                }

                var indexNode = gtNewVconNode(type);
                indexNode.SimdVal = indices;
                baseType = varTypeIsUnsigned(baseType) ? TYP_UBYTE : TYP_BYTE;
                return gtNewSimdHWIntrinsicNode(type, NI_AVX512_Shuffle, baseType, simdSize, op1, indexNode);
            }

            NamedIntrinsic intrinsic;
            switch (elementSize)
            {
                case 1:
                {
                    assert(compIsaSupportedDebugOnly(InstructionSet_AVX512v2));
                    intrinsic = NI_AVX512v2_PermuteVar64x8;
                    break;
                }

                case 2:
                {
                    for (var index = 0; index < elementCount; index++)
                    {
                        indices.u16[index] = (ushort)(indices.u8[index * elementSize] / elementSize);
                    }
                    intrinsic = NI_AVX512_PermuteVar32x16;
                    break;
                }

                case 4:
                {
                    for (var index = 0; index < elementCount; index++)
                    {
                        indices.u32[index] = (uint)(indices.u8[index * elementSize] / elementSize);
                    }
                    intrinsic = NI_AVX512_PermuteVar16x32;
                    break;
                }

                default:
                {
                    assert(elementSize == 8);
                    for (var index = 0; index < elementCount; index++)
                    {
                        indices.u64[index] = (ulong)(indices.u8[index * elementSize] / elementSize);
                    }
                    intrinsic = NI_AVX512_PermuteVar8x64;
                    break;
                }
            }
            var permutationIndices = gtNewVconNode(type);
            permutationIndices.SimdVal = indices;
            result = gtNewSimdHWIntrinsicNode(type, intrinsic, baseType, simdSize, permutationIndices, op1);
            if (needsZero)
            {
                var maskNode = gtNewVconNode(type);
                maskNode.SimdVal = mask;
                result = gtNewSimdBinOpNode(GT_AND, type, maskNode, result, baseType, simdSize);
            }
            return result;
        }
        else if (needsZero)
        {
            baseType = varTypeIsUnsigned(baseType) ? TYP_UBYTE : TYP_BYTE;
            var indexNode = gtNewVconNode(type);
            indexNode.SimdVal.v128[0] = indices.v128[0];
            return gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, baseType, simdSize, op1, indexNode);
        }
        else
        {
            if (varTypeIsLong(baseType))
            {
                baseType = TYP_DOUBLE;
            }
            var immediate = gtNewIconNode(TYP_INT, control);
            if (varTypeIsIntegral(baseType))
            {
                result = gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, baseType, simdSize, op1, immediate);
            }
            else if (compOpportunisticallyDependsOn(InstructionSet_AVX))
            {
                result = gtNewSimdHWIntrinsicNode(type, NI_AVX_Permute, baseType, simdSize, op1, immediate);
            }
            else
            {
                var duplicate = fgMakeMultiUse(ref op1);
                result = gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, baseType, simdSize, op1, duplicate, immediate);
            }
        }

        if (needsZero)
        {
            assert(simdSize == 32);
            var maskNode = gtNewVconNode(type);
            maskNode.SimdVal = mask;
            result = gtNewSimdBinOpNode(GT_AND, type, maskNode, result, baseType, simdSize);
        }
        return result;
#else
        throw new NotImplementedException("Target-specific SIMD constant shuffle construction is not ported.");
#endif
    }
#endif
}
