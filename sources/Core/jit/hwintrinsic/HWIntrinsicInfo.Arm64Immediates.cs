// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using System;

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static void GetImmOpsPositions(NamedIntrinsic id, int argumentCount, out int first, out int second)
    {
        first = -1;
        second = -1;
        switch (id)
        {
            case NI_AdvSimd_Insert:
            case NI_AdvSimd_InsertScalar:
            case NI_AdvSimd_LoadAndInsertScalar:
            case NI_AdvSimd_LoadAndInsertScalarVector64x2:
            case NI_AdvSimd_LoadAndInsertScalarVector64x3:
            case NI_AdvSimd_LoadAndInsertScalarVector64x4:
            case NI_AdvSimd_Arm64_LoadAndInsertScalar:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
            {
                assert(argumentCount == 3);
                first = 1;
                break;
            }
            case NI_AdvSimd_Arm64_InsertSelectedScalar:
            {
                assert(argumentCount == 4);
                first = 2;
                second = 0;
                break;
            }
            case NI_Sve_SaturatingDecrementBy16BitElementCount:
            case NI_Sve_SaturatingDecrementBy32BitElementCount:
            case NI_Sve_SaturatingDecrementBy64BitElementCount:
            case NI_Sve_SaturatingDecrementBy8BitElementCount:
            case NI_Sve_SaturatingIncrementBy16BitElementCount:
            case NI_Sve_SaturatingIncrementBy32BitElementCount:
            case NI_Sve_SaturatingIncrementBy64BitElementCount:
            case NI_Sve_SaturatingIncrementBy8BitElementCount:
            case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
            case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
            case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
            {
                assert(argumentCount == 3);
                first = 1;
                second = 0;
                break;
            }
            case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
            case NI_Sve2_MultiplyAddRotateComplexBySelectedScalar:
            case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar:
            case NI_Sve2_DotProductRotateComplexBySelectedIndex:
            {
                assert(argumentCount == 5);
                first = 0;
                second = 1;
                break;
            }
            default:
            {
                assert(argumentCount > 0);
                first = 0;
                break;
            }
        }
    }

    public static void lookupImmBounds(NamedIntrinsic id, int simdSize, var_types baseType, int number,
        out int lower, out int upper)
    {
        assert(HasImmediateOperand(id));
        lower = 0;
        upper = 0;
        var category = lookupCategory(id);
        if (category is HW_Category_ShiftLeftByImmediate)
        {
            var size = baseType.Size;
            if (id is NI_Sve2_ShiftLeftLogicalWideningEven or NI_Sve2_ShiftLeftLogicalWideningOdd)
            {
                size /= 2;
            }
            upper = BITS_PER_BYTE * size - 1;
        }
        else if (category is HW_Category_ShiftRightByImmediate)
        {
            lower = 1;
            upper = BITS_PER_BYTE * baseType.Size;
        }
        else if (category is HW_Category_SIMDByIndexedElement)
        {
            upper = id switch {
                NI_Sve_DuplicateSelectedScalarToVector => (512 / (BITS_PER_BYTE * baseType.Size)) - 1,
                NI_Sve2_MultiplyBySelectedScalarWideningEven or
                NI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd or
                NI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract or
                NI_Sve2_MultiplyBySelectedScalarWideningOdd or
                NI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd or
                NI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd or
                NI_Sve2_MultiplyDoublingWideningSaturateEvenBySelectedScalar or
                NI_Sve2_MultiplyDoublingWideningSaturateOddBySelectedScalar =>
                    (simdSize / baseType.Size) * 2 - 1,
                _ => simdSize / baseType.Size - 1,
            };
        }
        else
        {
            switch (id)
            {
                case NI_AdvSimd_DuplicateSelectedScalarToVector64:
                case NI_AdvSimd_DuplicateSelectedScalarToVector128:
                case NI_AdvSimd_Extract:
                case NI_AdvSimd_ExtractVector128:
                case NI_AdvSimd_ExtractVector64:
                case NI_AdvSimd_Insert:
                case NI_AdvSimd_InsertScalar:
                case NI_AdvSimd_LoadAndInsertScalar:
                case NI_AdvSimd_LoadAndInsertScalarVector64x2:
                case NI_AdvSimd_LoadAndInsertScalarVector64x3:
                case NI_AdvSimd_LoadAndInsertScalarVector64x4:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
                case NI_AdvSimd_StoreSelectedScalar:
                case NI_AdvSimd_Arm64_StoreSelectedScalar:
                case NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128:
                case NI_AdvSimd_Arm64_InsertSelectedScalar:
                case NI_Sve_FusedMultiplyAddBySelectedScalar:
                case NI_Sve_FusedMultiplySubtractBySelectedScalar:
                case NI_Sve_ExtractVector:
                {
                    upper = simdSize / baseType.Size - 1;
                    break;
                }
                case NI_Sve_CreateTrueMaskByte:
                case NI_Sve_CreateTrueMaskDouble:
                case NI_Sve_CreateTrueMaskInt16:
                case NI_Sve_CreateTrueMaskInt32:
                case NI_Sve_CreateTrueMaskInt64:
                case NI_Sve_CreateTrueMaskSByte:
                case NI_Sve_CreateTrueMaskSingle:
                case NI_Sve_CreateTrueMaskUInt16:
                case NI_Sve_CreateTrueMaskUInt32:
                case NI_Sve_CreateTrueMaskUInt64:
                case NI_Sve_Count16BitElements:
                case NI_Sve_Count32BitElements:
                case NI_Sve_Count64BitElements:
                case NI_Sve_Count8BitElements:
                {
                    lower = (int)SveMaskPattern.SveMaskPatternLargestPowerOf2;
                    upper = (int)SveMaskPattern.SveMaskPatternAll;
                    break;
                }
                case NI_Sve_SaturatingDecrementBy16BitElementCount:
                case NI_Sve_SaturatingDecrementBy32BitElementCount:
                case NI_Sve_SaturatingDecrementBy64BitElementCount:
                case NI_Sve_SaturatingDecrementBy8BitElementCount:
                case NI_Sve_SaturatingIncrementBy16BitElementCount:
                case NI_Sve_SaturatingIncrementBy32BitElementCount:
                case NI_Sve_SaturatingIncrementBy64BitElementCount:
                case NI_Sve_SaturatingIncrementBy8BitElementCount:
                case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
                case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
                case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
                {
                    assert(number is 1 or 2);
                    lower = number == 1 ? 1 : (int)SveMaskPattern.SveMaskPatternLargestPowerOf2;
                    upper = number == 1 ? 16 : (int)SveMaskPattern.SveMaskPatternAll;
                    break;
                }
                case NI_Sve_GatherPrefetch8Bit:
                case NI_Sve_GatherPrefetch16Bit:
                case NI_Sve_GatherPrefetch32Bit:
                case NI_Sve_GatherPrefetch64Bit:
                case NI_Sve_Prefetch16Bit:
                case NI_Sve_Prefetch32Bit:
                case NI_Sve_Prefetch64Bit:
                case NI_Sve_Prefetch8Bit:
                {
                    lower = 0;
                    upper = 15;
                    break;
                }
                case NI_Sve_AddRotateComplex:
                case NI_Sve2_AddRotateComplex:
                case NI_Sve2_AddSaturateRotateComplex:
                {
                    upper = 1;
                    break;
                }
                case NI_Sve_MultiplyAddRotateComplex:
                case NI_Sve2_MultiplyAddRotateComplex:
                case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex:
                case NI_Sve2_DotProductRotateComplex:
                {
                    upper = 3;
                    break;
                }
                case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
                {
                    upper = number == 1 ? 3 : 1;
                    break;
                }
                case NI_Sve2_DotProductRotateComplexBySelectedIndex:
                {
                    assert(baseType is TYP_BYTE or TYP_SHORT);
                    upper = number == 1 ? 3 : (baseType is TYP_BYTE ? 3 : 1);
                    break;
                }
                case NI_Sve2_MultiplyAddRotateComplexBySelectedScalar:
                {
                    assert(baseType is TYP_USHORT or TYP_SHORT or TYP_INT or TYP_UINT);
                    upper = number == 1 ? 3 : (baseType is TYP_USHORT or TYP_SHORT ? 3 : 1);
                    break;
                }
                case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar:
                {
                    assert(baseType is TYP_INT or TYP_SHORT);
                    upper = number == 1 ? 3 : (baseType is TYP_SHORT ? 3 : 1);
                    break;
                }
                case NI_Sve_TrigonometricMultiplyAddCoefficient:
                {
                    upper = 7;
                    break;
                }
                case NI_Sha3_XorRotateRight:
                {
                    upper = 63;
                    break;
                }
                default:
                {
                    throw new InvalidOperationException($"Unexpected ARM64 immediate intrinsic {id}.");
                }
            }
        }
        assert(lower <= upper);
    }
}
#endif
