// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;

namespace RyuJitSharp;

public partial struct HWIntrinsicInfo
{
    private static ReadOnlySpan<HWIntrinsicCategory> s_categories => [
        HW_Category_Helper,                     // NI_Vector64_Abs
        HW_Category_Helper,                     // NI_Vector64_AddSaturate
        HW_Category_Helper,                     // NI_Vector64_AndNot
        HW_Category_Helper,                     // NI_Vector64_As
        HW_Category_Helper,                     // NI_Vector64_AsByte
        HW_Category_Helper,                     // NI_Vector64_AsDouble
        HW_Category_Helper,                     // NI_Vector64_AsInt16
        HW_Category_Helper,                     // NI_Vector64_AsInt32
        HW_Category_Helper,                     // NI_Vector64_AsInt64
        HW_Category_Helper,                     // NI_Vector64_AsNInt
        HW_Category_Helper,                     // NI_Vector64_AsNUInt
        HW_Category_Helper,                     // NI_Vector64_AsSByte
        HW_Category_Helper,                     // NI_Vector64_AsSingle
        HW_Category_Helper,                     // NI_Vector64_AsUInt16
        HW_Category_Helper,                     // NI_Vector64_AsUInt32
        HW_Category_Helper,                     // NI_Vector64_AsUInt64
        HW_Category_Helper,                     // NI_Vector64_Ceiling
        HW_Category_Helper,                     // NI_Vector64_ConditionalSelect
        HW_Category_Helper,                     // NI_Vector64_ConvertToDouble
        HW_Category_Helper,                     // NI_Vector64_ConvertToInt32
        HW_Category_Helper,                     // NI_Vector64_ConvertToInt32Native
        HW_Category_Helper,                     // NI_Vector64_ConvertToInt64
        HW_Category_Helper,                     // NI_Vector64_ConvertToInt64Native
        HW_Category_Helper,                     // NI_Vector64_ConvertToSingle
        HW_Category_Helper,                     // NI_Vector64_ConvertToUInt32
        HW_Category_Helper,                     // NI_Vector64_ConvertToUInt32Native
        HW_Category_Helper,                     // NI_Vector64_ConvertToUInt64
        HW_Category_Helper,                     // NI_Vector64_ConvertToUInt64Native
        HW_Category_Helper,                     // NI_Vector64_Create
        HW_Category_Helper,                     // NI_Vector64_CreateScalar
        HW_Category_SIMD,                       // NI_Vector64_CreateScalarUnsafe
        HW_Category_Helper,                     // NI_Vector64_CreateSequence
        HW_Category_Helper,                     // NI_Vector64_Dot
        HW_Category_Helper,                     // NI_Vector64_Equals
        HW_Category_Helper,                     // NI_Vector64_EqualsAny
        HW_Category_Helper,                     // NI_Vector64_ExtractMostSignificantBits
        HW_Category_Helper,                     // NI_Vector64_Floor
        HW_Category_Helper,                     // NI_Vector64_FusedMultiplyAdd
        HW_Category_Helper,                     // NI_Vector64_GetElement
        HW_Category_Helper,                     // NI_Vector64_GreaterThan
        HW_Category_Helper,                     // NI_Vector64_GreaterThanAll
        HW_Category_Helper,                     // NI_Vector64_GreaterThanAny
        HW_Category_Helper,                     // NI_Vector64_GreaterThanOrEqual
        HW_Category_Helper,                     // NI_Vector64_GreaterThanOrEqualAll
        HW_Category_Helper,                     // NI_Vector64_GreaterThanOrEqualAny
        HW_Category_Helper,                     // NI_Vector64_IsEvenInteger
        HW_Category_Helper,                     // NI_Vector64_IsFinite
        HW_Category_Helper,                     // NI_Vector64_IsInfinity
        HW_Category_Helper,                     // NI_Vector64_IsInteger
        HW_Category_Helper,                     // NI_Vector64_IsNaN
        HW_Category_Helper,                     // NI_Vector64_IsNegative
        HW_Category_Helper,                     // NI_Vector64_IsNegativeInfinity
        HW_Category_Helper,                     // NI_Vector64_IsNormal
        HW_Category_Helper,                     // NI_Vector64_IsOddInteger
        HW_Category_Helper,                     // NI_Vector64_IsPositive
        HW_Category_Helper,                     // NI_Vector64_IsPositiveInfinity
        HW_Category_Helper,                     // NI_Vector64_IsSubnormal
        HW_Category_Helper,                     // NI_Vector64_IsZero
        HW_Category_Helper,                     // NI_Vector64_LessThan
        HW_Category_Helper,                     // NI_Vector64_LessThanAll
        HW_Category_Helper,                     // NI_Vector64_LessThanAny
        HW_Category_Helper,                     // NI_Vector64_LessThanOrEqual
        HW_Category_Helper,                     // NI_Vector64_LessThanOrEqualAll
        HW_Category_Helper,                     // NI_Vector64_LessThanOrEqualAny
        HW_Category_Helper,                     // NI_Vector64_LoadAligned
        HW_Category_Helper,                     // NI_Vector64_LoadAlignedNonTemporal
        HW_Category_Helper,                     // NI_Vector64_LoadUnsafe
        HW_Category_Helper,                     // NI_Vector64_Max
        HW_Category_Helper,                     // NI_Vector64_MaxMagnitude
        HW_Category_Helper,                     // NI_Vector64_MaxMagnitudeNumber
        HW_Category_Helper,                     // NI_Vector64_MaxNative
        HW_Category_Helper,                     // NI_Vector64_MaxNumber
        HW_Category_Helper,                     // NI_Vector64_Min
        HW_Category_Helper,                     // NI_Vector64_MinMagnitude
        HW_Category_Helper,                     // NI_Vector64_MinMagnitudeNumber
        HW_Category_Helper,                     // NI_Vector64_MinNative
        HW_Category_Helper,                     // NI_Vector64_MinNumber
        HW_Category_Helper,                     // NI_Vector64_MultiplyAddEstimate
        HW_Category_Helper,                     // NI_Vector64_Narrow
        HW_Category_Helper,                     // NI_Vector64_NarrowWithSaturation
        HW_Category_Helper,                     // NI_Vector64_Round
        HW_Category_Helper,                     // NI_Vector64_ShiftLeft
        HW_Category_Helper,                     // NI_Vector64_Shuffle
        HW_Category_Helper,                     // NI_Vector64_ShuffleNative
        HW_Category_Helper,                     // NI_Vector64_ShuffleNativeFallback
        HW_Category_Helper,                     // NI_Vector64_Sqrt
        HW_Category_Helper,                     // NI_Vector64_StoreAligned
        HW_Category_Helper,                     // NI_Vector64_StoreAlignedNonTemporal
        HW_Category_Helper,                     // NI_Vector64_StoreUnsafe
        HW_Category_Helper,                     // NI_Vector64_SubtractSaturate
        HW_Category_Helper,                     // NI_Vector64_Sum
        HW_Category_SIMD,                       // NI_Vector64_ToScalar
        HW_Category_SIMD,                       // NI_Vector64_ToVector128
        HW_Category_SIMD,                       // NI_Vector64_ToVector128Unsafe
        HW_Category_Helper,                     // NI_Vector64_Truncate
        HW_Category_Helper,                     // NI_Vector64_WidenLower
        HW_Category_Helper,                     // NI_Vector64_WidenUpper
        HW_Category_Helper,                     // NI_Vector64_WithElement
        HW_Category_Helper,                     // NI_Vector64_get_AllBitsSet
        HW_Category_Helper,                     // NI_Vector64_get_E
        HW_Category_Helper,                     // NI_Vector64_get_Epsilon
        HW_Category_Helper,                     // NI_Vector64_get_Indices
        HW_Category_Helper,                     // NI_Vector64_get_NaN
        HW_Category_Helper,                     // NI_Vector64_get_NegativeInfinity
        HW_Category_Helper,                     // NI_Vector64_get_NegativeOne
        HW_Category_Helper,                     // NI_Vector64_get_NegativeZero
        HW_Category_Helper,                     // NI_Vector64_get_One
        HW_Category_Helper,                     // NI_Vector64_get_Pi
        HW_Category_Helper,                     // NI_Vector64_get_PositiveInfinity
        HW_Category_Helper,                     // NI_Vector64_get_Tau
        HW_Category_Helper,                     // NI_Vector64_get_Zero
        HW_Category_Helper,                     // NI_Vector64_op_Addition
        HW_Category_Helper,                     // NI_Vector64_op_BitwiseAnd
        HW_Category_Helper,                     // NI_Vector64_op_BitwiseOr
        HW_Category_Helper,                     // NI_Vector64_op_Division
        HW_Category_Helper,                     // NI_Vector64_op_Equality
        HW_Category_Helper,                     // NI_Vector64_op_ExclusiveOr
        HW_Category_Helper,                     // NI_Vector64_op_Inequality
        HW_Category_Helper,                     // NI_Vector64_op_LeftShift
        HW_Category_Helper,                     // NI_Vector64_op_Multiply
        HW_Category_Helper,                     // NI_Vector64_op_OnesComplement
        HW_Category_Helper,                     // NI_Vector64_op_RightShift
        HW_Category_Helper,                     // NI_Vector64_op_Subtraction
        HW_Category_Helper,                     // NI_Vector64_op_UnaryNegation
        HW_Category_Helper,                     // NI_Vector64_op_UnaryPlus
        HW_Category_Helper,                     // NI_Vector64_op_UnsignedRightShift
        HW_Category_Helper,                     // NI_Vector128_Abs
        HW_Category_Helper,                     // NI_Vector128_AddSaturate
        HW_Category_Helper,                     // NI_Vector128_AndNot
        HW_Category_Helper,                     // NI_Vector128_As
        HW_Category_Helper,                     // NI_Vector128_AsByte
        HW_Category_Helper,                     // NI_Vector128_AsDouble
        HW_Category_Helper,                     // NI_Vector128_AsInt16
        HW_Category_Helper,                     // NI_Vector128_AsInt32
        HW_Category_Helper,                     // NI_Vector128_AsInt64
        HW_Category_Helper,                     // NI_Vector128_AsNInt
        HW_Category_Helper,                     // NI_Vector128_AsNUInt
        HW_Category_Helper,                     // NI_Vector128_AsSByte
        HW_Category_Helper,                     // NI_Vector128_AsSingle
        HW_Category_Helper,                     // NI_Vector128_AsUInt16
        HW_Category_Helper,                     // NI_Vector128_AsUInt32
        HW_Category_Helper,                     // NI_Vector128_AsUInt64
        HW_Category_Helper,                     // NI_Vector128_AsVector
        HW_Category_Helper,                     // NI_Vector128_AsVector128
        HW_Category_SIMD,                       // NI_Vector128_AsVector128Unsafe
        HW_Category_Helper,                     // NI_Vector128_AsVector2
        HW_Category_SIMD,                       // NI_Vector128_AsVector3
        HW_Category_Helper,                     // NI_Vector128_AsVector4
        HW_Category_Helper,                     // NI_Vector128_Ceiling
        HW_Category_Helper,                     // NI_Vector128_ConditionalSelect
        HW_Category_Helper,                     // NI_Vector128_ConvertToDouble
        HW_Category_Helper,                     // NI_Vector128_ConvertToInt32
        HW_Category_Helper,                     // NI_Vector128_ConvertToInt32Native
        HW_Category_Helper,                     // NI_Vector128_ConvertToInt64
        HW_Category_Helper,                     // NI_Vector128_ConvertToInt64Native
        HW_Category_Helper,                     // NI_Vector128_ConvertToSingle
        HW_Category_Helper,                     // NI_Vector128_ConvertToUInt32
        HW_Category_Helper,                     // NI_Vector128_ConvertToUInt32Native
        HW_Category_Helper,                     // NI_Vector128_ConvertToUInt64
        HW_Category_Helper,                     // NI_Vector128_ConvertToUInt64Native
        HW_Category_Helper,                     // NI_Vector128_Create
        HW_Category_Helper,                     // NI_Vector128_CreateScalar
        HW_Category_SIMD,                       // NI_Vector128_CreateScalarUnsafe
        HW_Category_Helper,                     // NI_Vector128_CreateSequence
        HW_Category_Helper,                     // NI_Vector128_Dot
        HW_Category_Helper,                     // NI_Vector128_Equals
        HW_Category_Helper,                     // NI_Vector128_EqualsAny
        HW_Category_Helper,                     // NI_Vector128_ExtractMostSignificantBits
        HW_Category_Helper,                     // NI_Vector128_Floor
        HW_Category_Helper,                     // NI_Vector128_FusedMultiplyAdd
        HW_Category_Helper,                     // NI_Vector128_GetElement
        HW_Category_SIMD,                       // NI_Vector128_GetLower
        HW_Category_SIMD,                       // NI_Vector128_GetUpper
        HW_Category_Helper,                     // NI_Vector128_GreaterThan
        HW_Category_Helper,                     // NI_Vector128_GreaterThanAll
        HW_Category_Helper,                     // NI_Vector128_GreaterThanAny
        HW_Category_Helper,                     // NI_Vector128_GreaterThanOrEqual
        HW_Category_Helper,                     // NI_Vector128_GreaterThanOrEqualAll
        HW_Category_Helper,                     // NI_Vector128_GreaterThanOrEqualAny
        HW_Category_Helper,                     // NI_Vector128_IsEvenInteger
        HW_Category_Helper,                     // NI_Vector128_IsFinite
        HW_Category_Helper,                     // NI_Vector128_IsInfinity
        HW_Category_Helper,                     // NI_Vector128_IsInteger
        HW_Category_Helper,                     // NI_Vector128_IsNaN
        HW_Category_Helper,                     // NI_Vector128_IsNegative
        HW_Category_Helper,                     // NI_Vector128_IsNegativeInfinity
        HW_Category_Helper,                     // NI_Vector128_IsNormal
        HW_Category_Helper,                     // NI_Vector128_IsOddInteger
        HW_Category_Helper,                     // NI_Vector128_IsPositive
        HW_Category_Helper,                     // NI_Vector128_IsPositiveInfinity
        HW_Category_Helper,                     // NI_Vector128_IsSubnormal
        HW_Category_Helper,                     // NI_Vector128_IsZero
        HW_Category_Helper,                     // NI_Vector128_LessThan
        HW_Category_Helper,                     // NI_Vector128_LessThanAll
        HW_Category_Helper,                     // NI_Vector128_LessThanAny
        HW_Category_Helper,                     // NI_Vector128_LessThanOrEqual
        HW_Category_Helper,                     // NI_Vector128_LessThanOrEqualAll
        HW_Category_Helper,                     // NI_Vector128_LessThanOrEqualAny
        HW_Category_Helper,                     // NI_Vector128_LoadAligned
        HW_Category_Helper,                     // NI_Vector128_LoadAlignedNonTemporal
        HW_Category_Helper,                     // NI_Vector128_LoadUnsafe
        HW_Category_Helper,                     // NI_Vector128_Max
        HW_Category_Helper,                     // NI_Vector128_MaxMagnitude
        HW_Category_Helper,                     // NI_Vector128_MaxMagnitudeNumber
        HW_Category_Helper,                     // NI_Vector128_MaxNative
        HW_Category_Helper,                     // NI_Vector128_MaxNumber
        HW_Category_Helper,                     // NI_Vector128_Min
        HW_Category_Helper,                     // NI_Vector128_MinMagnitude
        HW_Category_Helper,                     // NI_Vector128_MinMagnitudeNumber
        HW_Category_Helper,                     // NI_Vector128_MinNative
        HW_Category_Helper,                     // NI_Vector128_MinNumber
        HW_Category_Helper,                     // NI_Vector128_MultiplyAddEstimate
        HW_Category_Helper,                     // NI_Vector128_Narrow
        HW_Category_Helper,                     // NI_Vector128_NarrowWithSaturation
        HW_Category_Helper,                     // NI_Vector128_Round
        HW_Category_Helper,                     // NI_Vector128_ShiftLeft
        HW_Category_Helper,                     // NI_Vector128_Shuffle
        HW_Category_Helper,                     // NI_Vector128_ShuffleNative
        HW_Category_Helper,                     // NI_Vector128_ShuffleNativeFallback
        HW_Category_Helper,                     // NI_Vector128_Sqrt
        HW_Category_Helper,                     // NI_Vector128_StoreAligned
        HW_Category_Helper,                     // NI_Vector128_StoreAlignedNonTemporal
        HW_Category_Helper,                     // NI_Vector128_StoreUnsafe
        HW_Category_Helper,                     // NI_Vector128_SubtractSaturate
        HW_Category_Helper,                     // NI_Vector128_Sum
        HW_Category_SIMD,                       // NI_Vector128_ToScalar
        HW_Category_Helper,                     // NI_Vector128_Truncate
        HW_Category_Helper,                     // NI_Vector128_WidenLower
        HW_Category_Helper,                     // NI_Vector128_WidenUpper
        HW_Category_Helper,                     // NI_Vector128_WithElement
        HW_Category_Helper,                     // NI_Vector128_WithLower
        HW_Category_Helper,                     // NI_Vector128_WithUpper
        HW_Category_Helper,                     // NI_Vector128_get_AllBitsSet
        HW_Category_Helper,                     // NI_Vector128_get_E
        HW_Category_Helper,                     // NI_Vector128_get_Epsilon
        HW_Category_Helper,                     // NI_Vector128_get_Indices
        HW_Category_Helper,                     // NI_Vector128_get_NaN
        HW_Category_Helper,                     // NI_Vector128_get_NegativeInfinity
        HW_Category_Helper,                     // NI_Vector128_get_NegativeOne
        HW_Category_Helper,                     // NI_Vector128_get_NegativeZero
        HW_Category_Helper,                     // NI_Vector128_get_One
        HW_Category_Helper,                     // NI_Vector128_get_Pi
        HW_Category_Helper,                     // NI_Vector128_get_PositiveInfinity
        HW_Category_Helper,                     // NI_Vector128_get_Tau
        HW_Category_Helper,                     // NI_Vector128_get_Zero
        HW_Category_Helper,                     // NI_Vector128_op_Addition
        HW_Category_Helper,                     // NI_Vector128_op_BitwiseAnd
        HW_Category_Helper,                     // NI_Vector128_op_BitwiseOr
        HW_Category_Helper,                     // NI_Vector128_op_Division
        HW_Category_Helper,                     // NI_Vector128_op_Equality
        HW_Category_Helper,                     // NI_Vector128_op_ExclusiveOr
        HW_Category_Helper,                     // NI_Vector128_op_Inequality
        HW_Category_Helper,                     // NI_Vector128_op_LeftShift
        HW_Category_Helper,                     // NI_Vector128_op_Multiply
        HW_Category_Helper,                     // NI_Vector128_op_OnesComplement
        HW_Category_Helper,                     // NI_Vector128_op_RightShift
        HW_Category_Helper,                     // NI_Vector128_op_Subtraction
        HW_Category_Helper,                     // NI_Vector128_op_UnaryNegation
        HW_Category_Helper,                     // NI_Vector128_op_UnaryPlus
        HW_Category_Helper,                     // NI_Vector128_op_UnsignedRightShift
        HW_Category_SIMD,                       // NI_AdvSimd_Abs
        HW_Category_SIMD,                       // NI_AdvSimd_AbsSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_AbsScalar
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteCompareGreaterThan
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteCompareGreaterThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteCompareLessThan
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteCompareLessThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteDifference
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteDifferenceAdd
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteDifferenceWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteDifferenceWideningLowerAndAdd
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteDifferenceWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_AbsoluteDifferenceWideningUpperAndAdd
        HW_Category_SIMD,                       // NI_AdvSimd_Add
        HW_Category_SIMD,                       // NI_AdvSimd_AddHighNarrowingLower
        HW_Category_SIMD,                       // NI_AdvSimd_AddHighNarrowingUpper
        HW_Category_SIMD,                       // NI_AdvSimd_AddPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_AddPairwiseWidening
        HW_Category_SIMD,                       // NI_AdvSimd_AddPairwiseWideningAndAdd
        HW_Category_SIMD,                       // NI_AdvSimd_AddPairwiseWideningAndAddScalar
        HW_Category_SIMD,                       // NI_AdvSimd_AddPairwiseWideningScalar
        HW_Category_SIMD,                       // NI_AdvSimd_AddRoundedHighNarrowingLower
        HW_Category_SIMD,                       // NI_AdvSimd_AddRoundedHighNarrowingUpper
        HW_Category_SIMD,                       // NI_AdvSimd_AddSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_AddSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_AddScalar
        HW_Category_SIMD,                       // NI_AdvSimd_AddWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_AddWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_And
        HW_Category_SIMD,                       // NI_AdvSimd_BitwiseClear
        HW_Category_SIMD,                       // NI_AdvSimd_BitwiseSelect
        HW_Category_SIMD,                       // NI_AdvSimd_Ceiling
        HW_Category_SIMD,                       // NI_AdvSimd_CeilingScalar
        HW_Category_SIMD,                       // NI_AdvSimd_CompareEqual
        HW_Category_SIMD,                       // NI_AdvSimd_CompareGreaterThan
        HW_Category_SIMD,                       // NI_AdvSimd_CompareGreaterThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_CompareLessThan
        HW_Category_SIMD,                       // NI_AdvSimd_CompareLessThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_CompareTest
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundAwayFromZero
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundAwayFromZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToEven
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToEvenScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToNegativeInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToPositiveInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToZero
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToInt32RoundToZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToSingle
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToSingleScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundAwayFromZero
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundAwayFromZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToEven
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToEvenScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToNegativeInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToPositiveInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToZero
        HW_Category_SIMD,                       // NI_AdvSimd_ConvertToUInt32RoundToZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_DivideScalar
        HW_Category_SIMD,                       // NI_AdvSimd_DuplicateSelectedScalarToVector128
        HW_Category_SIMD,                       // NI_AdvSimd_DuplicateSelectedScalarToVector64
        HW_Category_SIMD,                       // NI_AdvSimd_DuplicateToVector128
        HW_Category_SIMD,                       // NI_AdvSimd_DuplicateToVector64
        HW_Category_SIMD,                       // NI_AdvSimd_Extract
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractNarrowingLower
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractNarrowingSaturateLower
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractNarrowingSaturateUnsignedLower
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractNarrowingSaturateUnsignedUpper
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractNarrowingSaturateUpper
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractNarrowingUpper
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractVector128
        HW_Category_SIMD,                       // NI_AdvSimd_ExtractVector64
        HW_Category_SIMD,                       // NI_AdvSimd_Floor
        HW_Category_SIMD,                       // NI_AdvSimd_FloorScalar
        HW_Category_SIMD,                       // NI_AdvSimd_FusedAddHalving
        HW_Category_SIMD,                       // NI_AdvSimd_FusedAddRoundedHalving
        HW_Category_SIMD,                       // NI_AdvSimd_FusedMultiplyAdd
        HW_Category_SIMD,                       // NI_AdvSimd_FusedMultiplyAddNegatedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_FusedMultiplyAddScalar
        HW_Category_SIMD,                       // NI_AdvSimd_FusedMultiplySubtract
        HW_Category_SIMD,                       // NI_AdvSimd_FusedMultiplySubtractNegatedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_FusedMultiplySubtractScalar
        HW_Category_SIMD,                       // NI_AdvSimd_FusedSubtractHalving
        HW_Category_SIMD,                       // NI_AdvSimd_Insert
        HW_Category_SIMD,                       // NI_AdvSimd_InsertScalar
        HW_Category_SIMD,                       // NI_AdvSimd_LeadingSignCount
        HW_Category_SIMD,                       // NI_AdvSimd_LeadingZeroCount
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Load2xVector64
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Load2xVector64AndUnzip
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Load3xVector64
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Load3xVector64AndUnzip
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Load4xVector64
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Load4xVector64AndUnzip
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndInsertScalar
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndInsertScalarVector64x2
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndInsertScalarVector64x3
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndInsertScalarVector64x4
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndReplicateToVector128
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndReplicateToVector64
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndReplicateToVector64x2
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndReplicateToVector64x3
        HW_Category_MemoryLoad,                 // NI_AdvSimd_LoadAndReplicateToVector64x4
        HW_Category_Helper,                     // NI_AdvSimd_LoadVector128
        HW_Category_Helper,                     // NI_AdvSimd_LoadVector64
        HW_Category_SIMD,                       // NI_AdvSimd_Max
        HW_Category_SIMD,                       // NI_AdvSimd_MaxNumber
        HW_Category_SIMD,                       // NI_AdvSimd_MaxNumberScalar
        HW_Category_SIMD,                       // NI_AdvSimd_MaxPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Min
        HW_Category_SIMD,                       // NI_AdvSimd_MinNumber
        HW_Category_SIMD,                       // NI_AdvSimd_MinNumberScalar
        HW_Category_SIMD,                       // NI_AdvSimd_MinPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Multiply
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyAdd
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyAddByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyAddBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalarWideningLower
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndAdd
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndSubtract
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalarWideningUpper
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndAdd
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndSubtract
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingByScalarSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingBySelectedScalarSaturateHigh
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingSaturateHigh
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingWideningLowerAndAddSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingWideningLowerAndSubtractSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndAddSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndSubtractSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndAddSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndSubtractSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingWideningSaturateLower
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningSaturateLowerByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningSaturateLowerBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingWideningSaturateUpper
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningSaturateUpperByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningSaturateUpperBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingWideningUpperAndAddSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyDoublingWideningUpperAndSubtractSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndAddSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndSubtractSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndAddSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndSubtractSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyRoundedDoublingByScalarSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyRoundedDoublingBySelectedScalarSaturateHigh
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyRoundedDoublingSaturateHigh
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplyScalarBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplySubtract
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplySubtractByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_MultiplySubtractBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyWideningLowerAndAdd
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyWideningLowerAndSubtract
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyWideningUpperAndAdd
        HW_Category_SIMD,                       // NI_AdvSimd_MultiplyWideningUpperAndSubtract
        HW_Category_SIMD,                       // NI_AdvSimd_Negate
        HW_Category_SIMD,                       // NI_AdvSimd_NegateSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_NegateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Not
        HW_Category_SIMD,                       // NI_AdvSimd_Or
        HW_Category_SIMD,                       // NI_AdvSimd_OrNot
        HW_Category_SIMD,                       // NI_AdvSimd_PolynomialMultiply
        HW_Category_SIMD,                       // NI_AdvSimd_PolynomialMultiplyWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_PolynomialMultiplyWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_PopCount
        HW_Category_SIMD,                       // NI_AdvSimd_ReciprocalEstimate
        HW_Category_SIMD,                       // NI_AdvSimd_ReciprocalSquareRootEstimate
        HW_Category_SIMD,                       // NI_AdvSimd_ReciprocalSquareRootStep
        HW_Category_SIMD,                       // NI_AdvSimd_ReciprocalStep
        HW_Category_SIMD,                       // NI_AdvSimd_ReverseElement16
        HW_Category_SIMD,                       // NI_AdvSimd_ReverseElement32
        HW_Category_SIMD,                       // NI_AdvSimd_ReverseElement8
        HW_Category_SIMD,                       // NI_AdvSimd_RoundAwayFromZero
        HW_Category_SIMD,                       // NI_AdvSimd_RoundAwayFromZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToNearest
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToNearestScalar
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToNegativeInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToPositiveInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToZero
        HW_Category_SIMD,                       // NI_AdvSimd_RoundToZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmetic
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticRounded
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticRoundedSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticRoundedSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticRoundedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftArithmeticScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftAndInsert
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftAndInsertScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogical
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalSaturate
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalSaturateScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalSaturateUnsigned
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalSaturateUnsignedScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalWideningLower
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_ShiftLeftLogicalWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogical
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalRounded
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalRoundedSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalRoundedSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalRoundedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_ShiftLogicalScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightAndInsert
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightAndInsertScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmetic
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticAdd
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticAddScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRounded
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedAdd
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedAddScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticRoundedScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightArithmeticScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogical
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalAdd
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalAddScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalNarrowingLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalNarrowingSaturateLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalNarrowingSaturateUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalNarrowingUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRounded
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedAdd
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedAddScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedNarrowingLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateLower
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedNarrowingUpper
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalRoundedScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_ShiftRightLogicalScalar
        HW_Category_SIMD,                       // NI_AdvSimd_SignExtendWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_SignExtendWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_SqrtScalar
        HW_Category_MemoryStore,                // NI_AdvSimd_Store
        HW_Category_MemoryStore,                // NI_AdvSimd_StoreSelectedScalar
        HW_Category_MemoryStore,                // NI_AdvSimd_StoreVectorAndZip
        HW_Category_SIMD,                       // NI_AdvSimd_Subtract
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractHighNarrowingLower
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractHighNarrowingUpper
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractRoundedHighNarrowingLower
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractRoundedHighNarrowingUpper
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractScalar
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_SubtractWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_VectorTableLookup
        HW_Category_SIMD,                       // NI_AdvSimd_VectorTableLookupExtension
        HW_Category_SIMD,                       // NI_AdvSimd_Xor
        HW_Category_SIMD,                       // NI_AdvSimd_ZeroExtendWideningLower
        HW_Category_SIMD,                       // NI_AdvSimd_ZeroExtendWideningUpper
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Abs
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareGreaterThan
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqualScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareLessThan
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqualScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteCompareLessThanScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteDifference
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AbsoluteDifferenceScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Add
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AddAcross
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AddAcrossWidening
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AddPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AddPairwiseScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AddSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_AddSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Ceiling
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareEqual
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareEqualScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareGreaterThan
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareGreaterThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareGreaterThanScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareLessThan
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareLessThanOrEqual
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareLessThanScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareTest
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_CompareTestScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToDouble
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToDoubleScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToDoubleUpper
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZero
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToEven
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToEvenScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToZero
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToSingleLower
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToSingleRoundToOddLower
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToSingleRoundToOddUpper
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToSingleUpper
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZero
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToEven
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToEvenScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinityScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToZero
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Divide
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_DuplicateToVector128
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_DuplicateToVector64
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ExtractNarrowingSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ExtractNarrowingSaturateUnsignedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Floor
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_FusedMultiplyAdd
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_FusedMultiplyAddByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_FusedMultiplyAddBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_FusedMultiplyAddScalarBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_FusedMultiplySubtract
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_FusedMultiplySubtractByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_FusedMultiplySubtractBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_FusedMultiplySubtractScalarBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_InsertSelectedScalar
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_Load2xVector128
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_Load2xVector128AndUnzip
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_Load3xVector128
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_Load3xVector128AndUnzip
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_Load4xVector128
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_Load4xVector128AndUnzip
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndInsertScalar
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndReplicateToVector128
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadPairScalarVector64
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadPairVector128
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadPairVector128NonTemporal
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadPairVector64
        HW_Category_MemoryLoad,                 // NI_AdvSimd_Arm64_LoadPairVector64NonTemporal
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Max
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxAcross
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxNumber
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxNumberAcross
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxNumberPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxNumberPairwiseScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxPairwiseScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MaxScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Min
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinAcross
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinNumber
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinNumberAcross
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinNumberPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinNumberPairwiseScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinPairwise
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinPairwiseScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MinScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Multiply
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyDoublingSaturateHighScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyDoublingScalarBySelectedScalarSaturateHigh
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyDoublingWideningAndAddSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyDoublingWideningAndSubtractSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalarBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndAddSaturate
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndSubtractSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyExtended
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyExtendedByScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyExtendedBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyExtendedScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyExtendedScalarBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_MultiplyRoundedDoublingSaturateHighScalar
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_AdvSimd_Arm64_MultiplyScalarBySelectedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Negate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_NegateSaturate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_NegateSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_NegateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalEstimate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalEstimateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalExponentScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalSquareRootEstimate
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalSquareRootEstimateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalSquareRootStep
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalSquareRootStepScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalStep
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReciprocalStepScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ReverseElementBits
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_RoundAwayFromZero
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_RoundToNearest
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_RoundToZero
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ShiftArithmeticRoundedSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ShiftArithmeticSaturateScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_Arm64_ShiftLeftLogicalSaturateScalar
        HW_Category_ShiftLeftByImmediate,       // NI_AdvSimd_Arm64_ShiftLeftLogicalSaturateUnsignedScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ShiftLogicalRoundedSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ShiftLogicalSaturateScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateUnsignedScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_Arm64_ShiftRightLogicalNarrowingSaturateScalar
        HW_Category_ShiftRightByImmediate,      // NI_AdvSimd_Arm64_ShiftRightLogicalRoundedNarrowingSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Sqrt
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_Store
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_StorePair
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_StorePairNonTemporal
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_StorePairScalar
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_StorePairScalarNonTemporal
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_StoreSelectedScalar
        HW_Category_MemoryStore,                // NI_AdvSimd_Arm64_StoreVectorAndZip
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_Subtract
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_SubtractSaturateScalar
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_TransposeEven
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_TransposeOdd
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_UnzipEven
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_UnzipOdd
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_VectorTableLookup
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_VectorTableLookupExtension
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ZipHigh
        HW_Category_SIMD,                       // NI_AdvSimd_Arm64_ZipLow
        HW_Category_SIMD,                       // NI_Aes_Decrypt
        HW_Category_SIMD,                       // NI_Aes_Encrypt
        HW_Category_SIMD,                       // NI_Aes_InverseMixColumns
        HW_Category_SIMD,                       // NI_Aes_MixColumns
        HW_Category_SIMD,                       // NI_Aes_PolynomialMultiplyWideningLower
        HW_Category_SIMD,                       // NI_Aes_PolynomialMultiplyWideningUpper
        HW_Category_Scalar,                     // NI_ArmBase_LeadingZeroCount
        HW_Category_Scalar,                     // NI_ArmBase_ReverseElementBits
        HW_Category_Special,                    // NI_ArmBase_Yield
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_LeadingSignCount
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_LeadingZeroCount
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_MultiplyHigh
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_MultiplyLongAdd
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_MultiplyLongNeg
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_MultiplyLongSub
        HW_Category_Scalar,                     // NI_ArmBase_Arm64_ReverseElementBits
        HW_Category_Scalar,                     // NI_Crc32_ComputeCrc32
        HW_Category_Scalar,                     // NI_Crc32_ComputeCrc32C
        HW_Category_Scalar,                     // NI_Crc32_Arm64_ComputeCrc32
        HW_Category_Scalar,                     // NI_Crc32_Arm64_ComputeCrc32C
        HW_Category_SIMD,                       // NI_Dp_DotProduct
        HW_Category_SIMDByIndexedElement,       // NI_Dp_DotProductBySelectedQuadruplet
        HW_Category_SIMD,                       // NI_Rdm_MultiplyRoundedDoublingAndAddSaturateHigh
        HW_Category_SIMD,                       // NI_Rdm_MultiplyRoundedDoublingAndSubtractSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndAddSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndSubtractSaturateHigh
        HW_Category_SIMD,                       // NI_Rdm_Arm64_MultiplyRoundedDoublingAndAddSaturateHighScalar
        HW_Category_SIMD,                       // NI_Rdm_Arm64_MultiplyRoundedDoublingAndSubtractSaturateHighScalar
        HW_Category_SIMDByIndexedElement,       // NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndAddSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndSubtractSaturateHigh
        HW_Category_SIMD,                       // NI_Sha1_FixedRotate
        HW_Category_SIMD,                       // NI_Sha1_HashUpdateChoose
        HW_Category_SIMD,                       // NI_Sha1_HashUpdateMajority
        HW_Category_SIMD,                       // NI_Sha1_HashUpdateParity
        HW_Category_SIMD,                       // NI_Sha1_ScheduleUpdate0
        HW_Category_SIMD,                       // NI_Sha1_ScheduleUpdate1
        HW_Category_SIMD,                       // NI_Sha256_HashUpdate1
        HW_Category_SIMD,                       // NI_Sha256_HashUpdate2
        HW_Category_SIMD,                       // NI_Sha256_ScheduleUpdate0
        HW_Category_SIMD,                       // NI_Sha256_ScheduleUpdate1
        HW_Category_SIMD,                       // NI_Sve_Abs
        HW_Category_SIMD,                       // NI_Sve_AbsoluteCompareGreaterThan
        HW_Category_SIMD,                       // NI_Sve_AbsoluteCompareGreaterThanOrEqual
        HW_Category_SIMD,                       // NI_Sve_AbsoluteCompareLessThan
        HW_Category_SIMD,                       // NI_Sve_AbsoluteCompareLessThanOrEqual
        HW_Category_SIMD,                       // NI_Sve_AbsoluteDifference
        HW_Category_SIMD,                       // NI_Sve_Add
        HW_Category_SIMD,                       // NI_Sve_AddAcross
        HW_Category_SIMD,                       // NI_Sve_AddRotateComplex
        HW_Category_SIMD,                       // NI_Sve_AddSaturate
        HW_Category_SIMD,                       // NI_Sve_AddSequentialAcross
        HW_Category_SIMD,                       // NI_Sve_And
        HW_Category_SIMD,                       // NI_Sve_AndAcross
        HW_Category_SIMD,                       // NI_Sve_BitwiseClear
        HW_Category_SIMD,                       // NI_Sve_BooleanNot
        HW_Category_SIMD,                       // NI_Sve_Compact
        HW_Category_SIMD,                       // NI_Sve_CompareEqual
        HW_Category_SIMD,                       // NI_Sve_CompareGreaterThan
        HW_Category_SIMD,                       // NI_Sve_CompareGreaterThanOrEqual
        HW_Category_SIMD,                       // NI_Sve_CompareLessThan
        HW_Category_SIMD,                       // NI_Sve_CompareLessThanOrEqual
        HW_Category_SIMD,                       // NI_Sve_CompareNotEqualTo
        HW_Category_SIMD,                       // NI_Sve_CompareUnordered
        HW_Category_SIMD,                       // NI_Sve_Compute16BitAddresses
        HW_Category_SIMD,                       // NI_Sve_Compute32BitAddresses
        HW_Category_SIMD,                       // NI_Sve_Compute64BitAddresses
        HW_Category_SIMD,                       // NI_Sve_Compute8BitAddresses
        HW_Category_SIMD,                       // NI_Sve_ConditionalExtractAfterLastActiveElement
        HW_Category_SIMD,                       // NI_Sve_ConditionalExtractAfterLastActiveElementAndReplicate
        HW_Category_SIMD,                       // NI_Sve_ConditionalExtractLastActiveElement
        HW_Category_SIMD,                       // NI_Sve_ConditionalExtractLastActiveElementAndReplicate
        HW_Category_SIMD,                       // NI_Sve_ConditionalSelect
        HW_Category_SIMD,                       // NI_Sve_ConvertToDouble
        HW_Category_SIMD,                       // NI_Sve_ConvertToInt32
        HW_Category_SIMD,                       // NI_Sve_ConvertToInt64
        HW_Category_SIMD,                       // NI_Sve_ConvertToSingle
        HW_Category_SIMD,                       // NI_Sve_ConvertToUInt32
        HW_Category_SIMD,                       // NI_Sve_ConvertToUInt64
        HW_Category_Scalar,                     // NI_Sve_Count16BitElements
        HW_Category_Scalar,                     // NI_Sve_Count32BitElements
        HW_Category_Scalar,                     // NI_Sve_Count64BitElements
        HW_Category_Scalar,                     // NI_Sve_Count8BitElements
        HW_Category_SIMD,                       // NI_Sve_CreateBreakAfterMask
        HW_Category_SIMD,                       // NI_Sve_CreateBreakAfterPropagateMask
        HW_Category_SIMD,                       // NI_Sve_CreateBreakBeforeMask
        HW_Category_SIMD,                       // NI_Sve_CreateBreakBeforePropagateMask
        HW_Category_SIMD,                       // NI_Sve_CreateBreakPropagateMask
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskByte
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskDouble
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskInt16
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskInt32
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskInt64
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskSByte
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskSingle
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskUInt16
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskUInt32
        HW_Category_SIMD,                       // NI_Sve_CreateFalseMaskUInt64
        HW_Category_SIMD,                       // NI_Sve_CreateMaskForFirstActiveElement
        HW_Category_SIMD,                       // NI_Sve_CreateMaskForNextActiveElement
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskByte
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskDouble
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskInt16
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskInt32
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskInt64
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskSByte
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskSingle
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskUInt16
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskUInt32
        HW_Category_SIMD,                       // NI_Sve_CreateTrueMaskUInt64
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskByte
         HW_Category_SIMD,                      // NI_Sve_CreateWhileLessThanMaskDouble
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskInt16
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskInt32
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskInt64
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskSByte
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskSingle
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskUInt16
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskUInt32
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanMaskUInt64
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskByte
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskDouble
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskInt16
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskInt32
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskInt64
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskSByte
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskSingle
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskUInt16
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskUInt32
        HW_Category_SIMD,                       // NI_Sve_CreateWhileLessThanOrEqualMaskUInt64
        HW_Category_SIMD,                       // NI_Sve_Divide
        HW_Category_SIMD,                       // NI_Sve_DotProduct
        HW_Category_SIMDByIndexedElement,       // NI_Sve_DotProductBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_Sve_DuplicateSelectedScalarToVector
        HW_Category_SIMD,                       // NI_Sve_ExtractAfterLastActiveElement
        HW_Category_Scalar,                     // NI_Sve_ExtractAfterLastActiveElementScalar
        HW_Category_SIMD,                       // NI_Sve_ExtractLastActiveElement
        HW_Category_Scalar,                     // NI_Sve_ExtractLastActiveElementScalar
        HW_Category_SIMD,                       // NI_Sve_ExtractVector
        HW_Category_SIMD,                       // NI_Sve_FloatingPointExponentialAccelerator
        HW_Category_SIMD,                       // NI_Sve_FusedMultiplyAdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve_FusedMultiplyAddBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve_FusedMultiplyAddNegated
        HW_Category_SIMD,                       // NI_Sve_FusedMultiplySubtract
        HW_Category_SIMDByIndexedElement,       // NI_Sve_FusedMultiplySubtractBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve_FusedMultiplySubtractNegated
        HW_Category_Special,                    // NI_Sve_GatherPrefetch16Bit
        HW_Category_Special,                    // NI_Sve_GatherPrefetch32Bit
        HW_Category_Special,                    // NI_Sve_GatherPrefetch64Bit
        HW_Category_Special,                    // NI_Sve_GatherPrefetch8Bit
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVector
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorByteZeroExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorByteZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt16SignExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt16SignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt32SignExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt32SignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorSByteSignExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorSByteSignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt16ZeroExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt32ZeroExtend
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorWithByteOffsetFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_GatherVectorWithByteOffsets
        HW_Category_SIMD,                       // NI_Sve_GetActiveElementCount
        HW_Category_SIMD,                       // NI_Sve_GetFfrByte
        HW_Category_SIMD,                       // NI_Sve_GetFfrDouble
        HW_Category_SIMD,                       // NI_Sve_GetFfrInt16
        HW_Category_SIMD,                       // NI_Sve_GetFfrInt32
        HW_Category_SIMD,                       // NI_Sve_GetFfrInt64
        HW_Category_SIMD,                       // NI_Sve_GetFfrSByte
        HW_Category_SIMD,                       // NI_Sve_GetFfrSingle
        HW_Category_SIMD,                       // NI_Sve_GetFfrUInt16
        HW_Category_SIMD,                       // NI_Sve_GetFfrUInt32
        HW_Category_SIMD,                       // NI_Sve_GetFfrUInt64
        HW_Category_SIMD,                       // NI_Sve_InsertIntoShiftedVector
        HW_Category_SIMD,                       // NI_Sve_LeadingSignCount
        HW_Category_SIMD,                       // NI_Sve_LeadingZeroCount
        HW_Category_MemoryLoad,                 // NI_Sve_Load2xVectorAndUnzip
        HW_Category_MemoryLoad,                 // NI_Sve_Load3xVectorAndUnzip
        HW_Category_MemoryLoad,                 // NI_Sve_Load4xVectorAndUnzip
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVector
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVector128AndReplicateToVector
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendToInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendToUInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorByteZeroExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16SignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16SignExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16SignExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16SignExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt16SignExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt32SignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt32SignExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorInt32SignExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorNonFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendToInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendToUInt16
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorSByteSignExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16ZeroExtendToInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16ZeroExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16ZeroExtendToUInt32
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt16ZeroExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt32ZeroExtendToInt64
        HW_Category_MemoryLoad,                 // NI_Sve_LoadVectorUInt32ZeroExtendToUInt64
        HW_Category_SIMD,                       // NI_Sve_Max
        HW_Category_SIMD,                       // NI_Sve_MaxAcross
        HW_Category_SIMD,                       // NI_Sve_MaxNumber
        HW_Category_SIMD,                       // NI_Sve_MaxNumberAcross
        HW_Category_SIMD,                       // NI_Sve_Min
        HW_Category_SIMD,                       // NI_Sve_MinAcross
        HW_Category_SIMD,                       // NI_Sve_MinNumber
        HW_Category_SIMD,                       // NI_Sve_MinNumberAcross
        HW_Category_SIMD,                       // NI_Sve_Multiply
        HW_Category_SIMD,                       // NI_Sve_MultiplyAdd
        HW_Category_SIMD,                       // NI_Sve_MultiplyAddRotateComplex
        HW_Category_SIMD,                       // NI_Sve_MultiplyAddRotateComplexBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_Sve_MultiplyBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve_MultiplyExtended
        HW_Category_SIMD,                       // NI_Sve_MultiplySubtract
        HW_Category_SIMD,                       // NI_Sve_Negate
        HW_Category_SIMD,                       // NI_Sve_Not
        HW_Category_SIMD,                       // NI_Sve_Or
        HW_Category_SIMD,                       // NI_Sve_OrAcross
        HW_Category_SIMD,                       // NI_Sve_PopCount
        HW_Category_Special,                    // NI_Sve_Prefetch16Bit
        HW_Category_Special,                    // NI_Sve_Prefetch32Bit
        HW_Category_Special,                    // NI_Sve_Prefetch64Bit
        HW_Category_Special,                    // NI_Sve_Prefetch8Bit
        HW_Category_SIMD,                       // NI_Sve_ReciprocalEstimate
        HW_Category_SIMD,                       // NI_Sve_ReciprocalExponent
        HW_Category_SIMD,                       // NI_Sve_ReciprocalSqrtEstimate
        HW_Category_SIMD,                       // NI_Sve_ReciprocalSqrtStep
        HW_Category_SIMD,                       // NI_Sve_ReciprocalStep
        HW_Category_SIMD,                       // NI_Sve_ReverseBits
        HW_Category_SIMD,                       // NI_Sve_ReverseElement
        HW_Category_SIMD,                       // NI_Sve_ReverseElement16
        HW_Category_SIMD,                       // NI_Sve_ReverseElement32
        HW_Category_SIMD,                       // NI_Sve_ReverseElement8
        HW_Category_SIMD,                       // NI_Sve_RoundAwayFromZero
        HW_Category_SIMD,                       // NI_Sve_RoundToNearest
        HW_Category_SIMD,                       // NI_Sve_RoundToNegativeInfinity
        HW_Category_SIMD,                       // NI_Sve_RoundToPositiveInfinity
        HW_Category_SIMD,                       // NI_Sve_RoundToZero
        HW_Category_SIMD,                       // NI_Sve_SaturatingDecrementBy16BitElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingDecrementBy32BitElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingDecrementBy64BitElementCount
        HW_Category_Scalar,                     // NI_Sve_SaturatingDecrementBy8BitElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingDecrementByActiveElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingIncrementBy16BitElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingIncrementBy32BitElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingIncrementBy64BitElementCount
        HW_Category_Scalar,                     // NI_Sve_SaturatingIncrementBy8BitElementCount
        HW_Category_SIMD,                       // NI_Sve_SaturatingIncrementByActiveElementCount
        HW_Category_SIMD,                       // NI_Sve_Scale
        HW_Category_MemoryStore,                // NI_Sve_Scatter
        HW_Category_MemoryStore,                // NI_Sve_Scatter16BitNarrowing
        HW_Category_MemoryStore,                // NI_Sve_Scatter16BitWithByteOffsetsNarrowing
        HW_Category_MemoryStore,                // NI_Sve_Scatter32BitNarrowing
        HW_Category_MemoryStore,                // NI_Sve_Scatter32BitWithByteOffsetsNarrowing
        HW_Category_MemoryStore,                // NI_Sve_Scatter8BitNarrowing
        HW_Category_MemoryStore,                // NI_Sve_Scatter8BitWithByteOffsetsNarrowing
        HW_Category_MemoryStore,                // NI_Sve_ScatterWithByteOffsets
        HW_Category_SIMD,                       // NI_Sve_SetFfr
        HW_Category_SIMD,                       // NI_Sve_ShiftLeftLogical
        HW_Category_SIMD,                       // NI_Sve_ShiftRightArithmetic
        HW_Category_ShiftRightByImmediate,      // NI_Sve_ShiftRightArithmeticForDivide
        HW_Category_SIMD,                       // NI_Sve_ShiftRightLogical
        HW_Category_SIMD,                       // NI_Sve_SignExtend16
        HW_Category_SIMD,                       // NI_Sve_SignExtend32
        HW_Category_SIMD,                       // NI_Sve_SignExtend8
        HW_Category_SIMD,                       // NI_Sve_SignExtendWideningLower
        HW_Category_SIMD,                       // NI_Sve_SignExtendWideningUpper
        HW_Category_SIMD,                       // NI_Sve_Splice
        HW_Category_SIMD,                       // NI_Sve_Sqrt
        HW_Category_MemoryStore,                // NI_Sve_StoreAndZip
        HW_Category_MemoryStore,                // NI_Sve_StoreNarrowing
        HW_Category_MemoryStore,                // NI_Sve_StoreNonTemporal
        HW_Category_SIMD,                       // NI_Sve_Subtract
        HW_Category_SIMD,                       // NI_Sve_SubtractSaturate
        HW_Category_SIMD,                       // NI_Sve_TestAnyTrue
        HW_Category_SIMD,                       // NI_Sve_TestFirstTrue
        HW_Category_SIMD,                       // NI_Sve_TestLastTrue
        HW_Category_SIMD,                       // NI_Sve_TransposeEven
        HW_Category_SIMD,                       // NI_Sve_TransposeOdd
        HW_Category_SIMD,                       // NI_Sve_TrigonometricMultiplyAddCoefficient
        HW_Category_SIMD,                       // NI_Sve_TrigonometricSelectCoefficient
        HW_Category_SIMD,                       // NI_Sve_TrigonometricStartingValue
        HW_Category_SIMD,                       // NI_Sve_UnzipEven
        HW_Category_SIMD,                       // NI_Sve_UnzipOdd
        HW_Category_SIMD,                       // NI_Sve_VectorTableLookup
        HW_Category_SIMD,                       // NI_Sve_Xor
        HW_Category_SIMD,                       // NI_Sve_XorAcross
        HW_Category_SIMD,                       // NI_Sve_ZeroExtend16
        HW_Category_SIMD,                       // NI_Sve_ZeroExtend32
        HW_Category_SIMD,                       // NI_Sve_ZeroExtend8
        HW_Category_SIMD,                       // NI_Sve_ZeroExtendWideningLower
        HW_Category_SIMD,                       // NI_Sve_ZeroExtendWideningUpper
        HW_Category_SIMD,                       // NI_Sve_ZipHigh
        HW_Category_SIMD,                       // NI_Sve_ZipLow
        HW_Category_SIMD,                       // NI_Sve2_AbsSaturate
        HW_Category_SIMD,                       // NI_Sve2_AbsoluteDifferenceAdd
        HW_Category_SIMD,                       // NI_Sve2_AbsoluteDifferenceWideningEven
        HW_Category_SIMD,                       // NI_Sve2_AbsoluteDifferenceWideningLowerAndAddEven
        HW_Category_SIMD,                       // NI_Sve2_AbsoluteDifferenceWideningLowerAndAddOdd
        HW_Category_SIMD,                       // NI_Sve2_AbsoluteDifferenceWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_AddCarryWideningEven
        HW_Category_SIMD,                       // NI_Sve2_AddCarryWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_AddHighNarrowingEven
        HW_Category_SIMD,                       // NI_Sve2_AddHighNarrowingOdd
        HW_Category_SIMD,                       // NI_Sve2_AddPairwise
        HW_Category_SIMD,                       // NI_Sve2_AddPairwiseWideningAndAdd
        HW_Category_SIMD,                       // NI_Sve2_AddRotateComplex
        HW_Category_SIMD,                       // NI_Sve2_AddRoundedHighNarrowingEven
        HW_Category_SIMD,                       // NI_Sve2_AddRoundedHighNarrowingOdd
        HW_Category_SIMD,                       // NI_Sve2_AddSaturate
        HW_Category_SIMD,                       // NI_Sve2_AddSaturateRotateComplex
        HW_Category_SIMD,                       // NI_Sve2_AddWideningEven
        HW_Category_SIMD,                       // NI_Sve2_AddWideningEvenOdd
        HW_Category_SIMD,                       // NI_Sve2_AddWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_BitwiseClearXor
        HW_Category_SIMD,                       // NI_Sve2_BitwiseSelect
        HW_Category_SIMD,                       // NI_Sve2_BitwiseSelectLeftInverted
        HW_Category_SIMD,                       // NI_Sve2_BitwiseSelectRightInverted
        HW_Category_SIMD,                       // NI_Sve2_ConvertToDoubleOdd
        HW_Category_SIMD,                       // NI_Sve2_ConvertToSingleEvenRoundToOdd
        HW_Category_SIMD,                       // NI_Sve2_ConvertToSingleOdd
        HW_Category_SIMD,                       // NI_Sve2_ConvertToSingleOddRoundToOdd
        HW_Category_SIMD,                       // NI_Sve2_CountMatchingElements
        HW_Category_SIMD,                       // NI_Sve2_CountMatchingElementsIn128BitSegments
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskByte
         HW_Category_SIMD,                      // NI_Sve2_CreateWhileGreaterThanMaskDouble
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskInt16
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskInt32
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskInt64
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskSByte
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskSingle
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskUInt16
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskUInt32
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanMaskUInt64
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskByte
         HW_Category_SIMD,                      // NI_Sve2_CreateWhileGreaterThanOrEqualMaskDouble
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt16
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt32
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt64
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskSByte
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskSingle
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt16
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt32
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt64
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskByte
         HW_Category_SIMD,                      // NI_Sve2_CreateWhileReadAfterWriteMaskDouble
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskInt16
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskInt32
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskInt64
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskSByte
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskSingle
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskUInt16
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskUInt32
        HW_Category_SIMD,                       // NI_Sve2_CreateWhileReadAfterWriteMaskUInt64
        HW_Category_SIMD,                       // NI_Sve2_DotProductRotateComplex
        HW_Category_SIMD,                       // NI_Sve2_DotProductRotateComplexBySelectedIndex
        HW_Category_SIMD,                       // NI_Sve2_FusedAddHalving
        HW_Category_SIMD,                       // NI_Sve2_FusedAddRoundedHalving
        HW_Category_SIMD,                       // NI_Sve2_FusedSubtractHalving
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorByteZeroExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorInt16SignExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorInt32SignExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorSByteSignExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal
        HW_Category_MemoryLoad,                 // NI_Sve2_GatherVectorWithByteOffsetsNonTemporal
        HW_Category_SIMD,                       // NI_Sve2_InterleavingXorEvenOdd
        HW_Category_SIMD,                       // NI_Sve2_InterleavingXorOddEven
        HW_Category_SIMD,                       // NI_Sve2_Log2
        HW_Category_SIMD,                       // NI_Sve2_Match
        HW_Category_SIMD,                       // NI_Sve2_MaxNumberPairwise
        HW_Category_SIMD,                       // NI_Sve2_MaxPairwise
        HW_Category_SIMD,                       // NI_Sve2_MinNumberPairwise
        HW_Category_SIMD,                       // NI_Sve2_MinPairwise
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyAddBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve2_MultiplyAddRotateComplex
        HW_Category_SIMD,                       // NI_Sve2_MultiplyAddRotateComplexBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex
        HW_Category_SIMD,                       // NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalarWideningEven
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalarWideningOdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingBySelectedScalarSaturateHigh
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingSaturateHigh
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningAndAddSaturateEven
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningAndAddSaturateEvenOdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningAndAddSaturateOdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEven
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEvenOdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateOdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningSaturateEven
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingWideningSaturateEvenBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve2_MultiplyDoublingWideningSaturateOdd
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyDoublingWideningSaturateOddBySelectedScalar
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyRoundedDoublingBySelectedScalarSaturateHigh
        HW_Category_SIMD,                       // NI_Sve2_MultiplyRoundedDoublingSaturateAndAddHigh
        HW_Category_SIMD,                       // NI_Sve2_MultiplyRoundedDoublingSaturateAndSubtractHigh
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndAddHigh
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndSubtractHigh
        HW_Category_SIMD,                       // NI_Sve2_MultiplyRoundedDoublingSaturateHigh
        HW_Category_SIMDByIndexedElement,       // NI_Sve2_MultiplySubtractBySelectedScalar
        HW_Category_SIMD,                       // NI_Sve2_MultiplyWideningEven
        HW_Category_SIMD,                       // NI_Sve2_MultiplyWideningEvenAndAdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyWideningEvenAndSubtract
        HW_Category_SIMD,                       // NI_Sve2_MultiplyWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyWideningOddAndAdd
        HW_Category_SIMD,                       // NI_Sve2_MultiplyWideningOddAndSubtract
        HW_Category_SIMD,                       // NI_Sve2_NegateSaturate
        HW_Category_SIMD,                       // NI_Sve2_NoMatch
        HW_Category_SIMD,                       // NI_Sve2_PolynomialMultiply
        HW_Category_SIMD,                       // NI_Sve2_PolynomialMultiplyWideningEven
        HW_Category_SIMD,                       // NI_Sve2_PolynomialMultiplyWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_ReciprocalEstimate
        HW_Category_SIMD,                       // NI_Sve2_ReciprocalSqrtEstimate
        HW_Category_MemoryStore,                // NI_Sve2_Scatter16BitNarrowingNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_Scatter16BitWithByteOffsetsNarrowingNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_Scatter32BitNarrowingNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_Scatter32BitWithByteOffsetsNarrowingNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_Scatter8BitNarrowingNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_Scatter8BitWithByteOffsetsNarrowingNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_ScatterNonTemporal
        HW_Category_MemoryStore,                // NI_Sve2_ScatterWithByteOffsetsNonTemporal
        HW_Category_SIMD,                       // NI_Sve2_ShiftArithmeticRounded
        HW_Category_SIMD,                       // NI_Sve2_ShiftArithmeticRoundedSaturate
        HW_Category_SIMD,                       // NI_Sve2_ShiftArithmeticSaturate
        HW_Category_ShiftLeftByImmediate,       // NI_Sve2_ShiftLeftAndInsert
        HW_Category_SIMD,                       // NI_Sve2_ShiftLeftLogicalSaturate
        HW_Category_ShiftLeftByImmediate,       // NI_Sve2_ShiftLeftLogicalSaturateUnsigned
        HW_Category_ShiftLeftByImmediate,       // NI_Sve2_ShiftLeftLogicalWideningEven
        HW_Category_ShiftLeftByImmediate,       // NI_Sve2_ShiftLeftLogicalWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_ShiftLogicalRounded
        HW_Category_SIMD,                       // NI_Sve2_ShiftLogicalRoundedSaturate
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightAndInsert
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticAdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticNarrowingSaturateEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticNarrowingSaturateOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticRounded
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticRoundedAdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalAdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalNarrowingEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalNarrowingOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalNarrowingSaturateEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalNarrowingSaturateOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalRounded
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalRoundedAdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalRoundedNarrowingEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalRoundedNarrowingOdd
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateEven
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateOdd
        HW_Category_SIMD,                       // NI_Sve2_SubtractBorrowWideningEven
        HW_Category_SIMD,                       // NI_Sve2_SubtractBorrowWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_SubtractHighNarrowingEven
        HW_Category_SIMD,                       // NI_Sve2_SubtractHighNarrowingOdd
        HW_Category_SIMD,                       // NI_Sve2_SubtractRoundedHighNarrowingEven
        HW_Category_SIMD,                       // NI_Sve2_SubtractRoundedHighNarrowingOdd
        HW_Category_SIMD,                       // NI_Sve2_SubtractSaturate
        HW_Category_SIMD,                       // NI_Sve2_SubtractWideningEven
        HW_Category_SIMD,                       // NI_Sve2_SubtractWideningEvenOdd
        HW_Category_SIMD,                       // NI_Sve2_SubtractWideningOdd
        HW_Category_SIMD,                       // NI_Sve2_SubtractWideningOddEven
        HW_Category_SIMD,                       // NI_Sve2_VectorTableLookup
        HW_Category_SIMD,                       // NI_Sve2_VectorTableLookupExtension
        HW_Category_SIMD,                       // NI_Sve2_Xor
        HW_Category_ShiftRightByImmediate,      // NI_Sve2_XorRotateRight
        HW_Category_Scalar,                     // NI_Sve_ConditionalExtractAfterLastActiveElementScalar
        HW_Category_Scalar,                     // NI_Sve_ConditionalExtractLastActiveElementScalar
        HW_Category_Helper,                     // NI_Sve_ConvertMaskToVector
        HW_Category_Helper,                     // NI_Sve_ConvertVectorToMask
        HW_Category_Helper,                     // NI_Sve_ConversionTrueMask
        HW_Category_Scalar,                     // NI_Sve_SaturatingDecrementBy16BitElementCountScalar
        HW_Category_Scalar,                     // NI_Sve_SaturatingDecrementBy32BitElementCountScalar
        HW_Category_Scalar,                     // NI_Sve_SaturatingDecrementBy64BitElementCountScalar
        HW_Category_Scalar,                     // NI_Sve_SaturatingIncrementBy16BitElementCountScalar
        HW_Category_Scalar,                     // NI_Sve_SaturatingIncrementBy32BitElementCountScalar
        HW_Category_Scalar,                     // NI_Sve_SaturatingIncrementBy64BitElementCountScalar
        HW_Category_MemoryStore,                // NI_Sve_StoreAndZipx2
        HW_Category_MemoryStore,                // NI_Sve_StoreAndZipx3
        HW_Category_MemoryStore,                // NI_Sve_StoreAndZipx4
        HW_Category_SIMD,                       // NI_Sve_And_Predicates
        HW_Category_SIMD,                       // NI_Sve_BitwiseClear_Predicates
        HW_Category_SIMD,                       // NI_Sve_Or_Predicates
        HW_Category_SIMD,                       // NI_Sve_Xor_Predicates
        HW_Category_SIMD,                       // NI_Sve_ConditionalSelect_Predicates
        HW_Category_SIMD,                       // NI_Sve_ZipHigh_Predicates
        HW_Category_SIMD,                       // NI_Sve_ZipLow_Predicates
        HW_Category_SIMD,                       // NI_Sve_UnzipEven_Predicates
        HW_Category_SIMD,                       // NI_Sve_UnzipOdd_Predicates
        HW_Category_SIMD,                       // NI_Sve_TransposeEven_Predicates
        HW_Category_SIMD,                       // NI_Sve_TransposeOdd_Predicates
        HW_Category_SIMD,                       // NI_Sve_ReverseElement_Predicates
    ];

    private static ReadOnlySpan<HWIntrinsicFlag> s_flags => [
        /* NI_Vector64_Abs                                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_AddSaturate                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_AndNot                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_As                                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsByte                                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsDouble                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsInt16                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsInt32                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsInt64                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsNInt                                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsNUInt                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsSByte                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsSingle                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsUInt16                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsUInt32                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_AsUInt64                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_Ceiling                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_ConditionalSelect                                                    */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_ConvertToDouble                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToInt32                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToInt32Native                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToInt64                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToInt64Native                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToSingle                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToUInt32                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToUInt32Native                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToUInt64                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ConvertToUInt64Native                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_Create                                                               */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector64_CreateScalar                                                         */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector64_CreateScalarUnsafe                                                   */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_Vector64_CreateSequence                                                       */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Dot                                                                  */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_Equals                                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_EqualsAny                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ExtractMostSignificantBits                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector64_Floor                                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_FusedMultiplyAdd                                                     */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_GetElement                                                           */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SupportsContainment | HW_Flag_ReturnsScalarT,
        /* NI_Vector64_GreaterThan                                                          */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_GreaterThanAll                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_GreaterThanAny                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_GreaterThanOrEqual                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_GreaterThanOrEqualAll                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_GreaterThanOrEqualAny                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_IsEvenInteger                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsFinite                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsInfinity                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsInteger                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsNaN                                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsNegative                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsNegativeInfinity                                                   */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsNormal                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsOddInteger                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsPositive                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsPositiveInfinity                                                   */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsSubnormal                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_IsZero                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_LessThan                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_LessThanAll                                                          */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_LessThanAny                                                          */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_LessThanOrEqual                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_LessThanOrEqualAll                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_LessThanOrEqualAny                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_LoadAligned                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_LoadAlignedNonTemporal                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_LoadUnsafe                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Max                                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MaxMagnitude                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MaxMagnitudeNumber                                                   */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MaxNative                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MaxNumber                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Min                                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MinMagnitude                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MinMagnitudeNumber                                                   */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MinNative                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MinNumber                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_MultiplyAddEstimate                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Narrow                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_NarrowWithSaturation                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Round                                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_ShiftLeft                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Shuffle                                                              */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
        /* NI_Vector64_ShuffleNative                                                        */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
        /* NI_Vector64_ShuffleNativeFallback                                                */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
        /* NI_Vector64_Sqrt                                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_StoreAligned                                                         */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_StoreAlignedNonTemporal                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_StoreUnsafe                                                          */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_SubtractSaturate                                                     */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_Sum                                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_ToScalar                                                             */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsScalarT,
        /* NI_Vector64_ToVector128                                                          */      HW_Flag_SpecialCodeGen,
        /* NI_Vector64_ToVector128Unsafe                                                    */      HW_Flag_SpecialCodeGen,
        /* NI_Vector64_Truncate                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_WidenLower                                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_WidenUpper                                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector64_WithElement                                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport,
        /* NI_Vector64_get_AllBitsSet                                                       */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_E                                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_Epsilon                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_Indices                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_NaN                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_NegativeInfinity                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_NegativeOne                                                      */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_NegativeZero                                                     */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_One                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_Pi                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_PositiveInfinity                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_Tau                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_get_Zero                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_Addition                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_BitwiseAnd                                                        */      HW_Flag_InvalidNodeId | HW_Flag_Commutative,
        /* NI_Vector64_op_BitwiseOr                                                         */      HW_Flag_InvalidNodeId | HW_Flag_Commutative,
        /* NI_Vector64_op_Division                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_Equality                                                          */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_ReturnsBoolean,
        /* NI_Vector64_op_ExclusiveOr                                                       */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_Inequality                                                        */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_ReturnsBoolean,
        /* NI_Vector64_op_LeftShift                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_Multiply                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_OnesComplement                                                    */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_RightShift                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_Subtraction                                                       */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_UnaryNegation                                                     */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_UnaryPlus                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector64_op_UnsignedRightShift                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Abs                                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_AddSaturate                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_AndNot                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_As                                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsByte                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsDouble                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsInt16                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsInt32                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsInt64                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsNInt                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsNUInt                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsSByte                                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsSingle                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsUInt16                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsUInt32                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsUInt64                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsVector                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsVector128                                                         */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsVector128Unsafe                                                   */      HW_Flag_SpecialCodeGen,
        /* NI_Vector128_AsVector2                                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_AsVector3                                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_Vector128_AsVector4                                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_Ceiling                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_ConditionalSelect                                                   */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_ConvertToDouble                                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToInt32                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToInt32Native                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToInt64                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToInt64Native                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToSingle                                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToUInt32                                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToUInt32Native                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToUInt64                                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ConvertToUInt64Native                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_Create                                                              */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector128_CreateScalar                                                        */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector128_CreateScalarUnsafe                                                  */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_Vector128_CreateSequence                                                      */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Dot                                                                 */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_Equals                                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_EqualsAny                                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ExtractMostSignificantBits                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector128_Floor                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_FusedMultiplyAdd                                                    */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_GetElement                                                          */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SupportsContainment | HW_Flag_ReturnsScalarT,
        /* NI_Vector128_GetLower                                                            */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Vector128_GetUpper                                                            */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Vector128_GreaterThan                                                         */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_GreaterThanAll                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_GreaterThanAny                                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_GreaterThanOrEqual                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_GreaterThanOrEqualAll                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_GreaterThanOrEqualAny                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_IsEvenInteger                                                       */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsFinite                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsInfinity                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsInteger                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsNaN                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsNegative                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsNegativeInfinity                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsNormal                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsOddInteger                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsPositive                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsPositiveInfinity                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsSubnormal                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_IsZero                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_LessThan                                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_LessThanAll                                                         */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_LessThanAny                                                         */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_LessThanOrEqual                                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_LessThanOrEqualAll                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_LessThanOrEqualAny                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_LoadAligned                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_LoadAlignedNonTemporal                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_LoadUnsafe                                                          */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Max                                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MaxMagnitude                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MaxMagnitudeNumber                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MaxNative                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MaxNumber                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Min                                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MinMagnitude                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MinMagnitudeNumber                                                  */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MinNative                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MinNumber                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_MultiplyAddEstimate                                                 */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Narrow                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_NarrowWithSaturation                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Round                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_ShiftLeft                                                           */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Shuffle                                                             */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
        /* NI_Vector128_ShuffleNative                                                       */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
        /* NI_Vector128_ShuffleNativeFallback                                               */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
        /* NI_Vector128_Sqrt                                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_StoreAligned                                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_StoreAlignedNonTemporal                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_StoreUnsafe                                                         */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_SubtractSaturate                                                    */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_Sum                                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_ToScalar                                                            */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsScalarT,
        /* NI_Vector128_Truncate                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_WidenLower                                                          */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_WidenUpper                                                          */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Vector128_WithElement                                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport,
        /* NI_Vector128_WithLower                                                           */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector128_WithUpper                                                           */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
        /* NI_Vector128_get_AllBitsSet                                                      */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_E                                                               */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_Epsilon                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_Indices                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_NaN                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_NegativeInfinity                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_NegativeOne                                                     */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_NegativeZero                                                    */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_One                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_Pi                                                              */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_PositiveInfinity                                                */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_Tau                                                             */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_get_Zero                                                            */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_Addition                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_BitwiseAnd                                                       */      HW_Flag_InvalidNodeId | HW_Flag_Commutative,
        /* NI_Vector128_op_BitwiseOr                                                        */      HW_Flag_InvalidNodeId | HW_Flag_Commutative,
        /* NI_Vector128_op_Division                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_Equality                                                         */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_ReturnsBoolean,
        /* NI_Vector128_op_ExclusiveOr                                                      */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_Inequality                                                       */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_ReturnsBoolean,
        /* NI_Vector128_op_LeftShift                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_Multiply                                                         */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_OnesComplement                                                   */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_RightShift                                                       */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_Subtraction                                                      */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_UnaryNegation                                                    */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_UnaryPlus                                                        */      HW_Flag_InvalidNodeId,
        /* NI_Vector128_op_UnsignedRightShift                                               */      HW_Flag_InvalidNodeId,
        /* NI_AdvSimd_Abs                                                                   */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_AbsSaturate                                                           */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_AbsScalar                                                             */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_AbsoluteCompareGreaterThan                                            */      HW_Flag_NoFlag,
        /* NI_AdvSimd_AbsoluteCompareGreaterThanOrEqual                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_AbsoluteCompareLessThan                                               */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_AbsoluteCompareLessThanOrEqual                                        */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_AbsoluteDifference                                                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_AbsoluteDifferenceAdd                                                 */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_AbsoluteDifferenceWideningLower                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_AbsoluteDifferenceWideningLowerAndAdd                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_AbsoluteDifferenceWideningUpper                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_AbsoluteDifferenceWideningUpperAndAdd                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Add                                                                   */      HW_Flag_Commutative,
        /* NI_AdvSimd_AddHighNarrowingLower                                                 */      HW_Flag_Commutative,
        /* NI_AdvSimd_AddHighNarrowingUpper                                                 */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_AddPairwise                                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_AddPairwiseWidening                                                   */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_AddPairwiseWideningAndAdd                                             */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_AddPairwiseWideningAndAddScalar                                       */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_AddPairwiseWideningScalar                                             */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_AddRoundedHighNarrowingLower                                          */      HW_Flag_Commutative,
        /* NI_AdvSimd_AddRoundedHighNarrowingUpper                                          */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_AddSaturate                                                           */      HW_Flag_Commutative,
        /* NI_AdvSimd_AddSaturateScalar                                                     */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_AddScalar                                                             */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_AddWideningLower                                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_AddWideningUpper                                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_And                                                                   */      HW_Flag_Commutative,
        /* NI_AdvSimd_BitwiseClear                                                          */      HW_Flag_SpecialImport,
        /* NI_AdvSimd_BitwiseSelect                                                         */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Ceiling                                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_CeilingScalar                                                         */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_CompareEqual                                                          */      HW_Flag_Commutative | HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_CompareGreaterThan                                                    */      HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_CompareGreaterThanOrEqual                                             */      HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_CompareLessThan                                                       */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_CompareLessThanOrEqual                                                */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_CompareTest                                                           */      HW_Flag_Commutative,
        /* NI_AdvSimd_ConvertToInt32RoundAwayFromZero                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToInt32RoundAwayFromZeroScalar                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToInt32RoundToEven                                             */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToInt32RoundToEvenScalar                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToInt32RoundToNegativeInfinity                                 */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToInt32RoundToNegativeInfinityScalar                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToInt32RoundToPositiveInfinity                                 */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToInt32RoundToPositiveInfinityScalar                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToInt32RoundToZero                                             */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToInt32RoundToZeroScalar                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToSingle                                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToSingleScalar                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToUInt32RoundAwayFromZero                                      */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToUInt32RoundAwayFromZeroScalar                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToUInt32RoundToEven                                            */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToUInt32RoundToEvenScalar                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToUInt32RoundToNegativeInfinity                                */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToUInt32RoundToNegativeInfinityScalar                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToUInt32RoundToPositiveInfinity                                */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToUInt32RoundToPositiveInfinityScalar                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ConvertToUInt32RoundToZero                                            */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ConvertToUInt32RoundToZeroScalar                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_DivideScalar                                                          */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_DuplicateSelectedScalarToVector128                                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_DuplicateSelectedScalarToVector64                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_DuplicateToVector128                                                  */      HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_AdvSimd_DuplicateToVector64                                                   */      HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_AdvSimd_Extract                                                               */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsScalarT,
        /* NI_AdvSimd_ExtractNarrowingLower                                                 */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ExtractNarrowingSaturateLower                                         */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ExtractNarrowingSaturateUnsignedLower                                 */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ExtractNarrowingSaturateUnsignedUpper                                 */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ExtractNarrowingSaturateUpper                                         */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ExtractNarrowingUpper                                                 */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ExtractVector128                                                      */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_ExtractVector64                                                       */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Floor                                                                 */      HW_Flag_NoFlag,
        /* NI_AdvSimd_FloorScalar                                                           */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_FusedAddHalving                                                       */      HW_Flag_Commutative,
        /* NI_AdvSimd_FusedAddRoundedHalving                                                */      HW_Flag_Commutative,
        /* NI_AdvSimd_FusedMultiplyAdd                                                      */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_FusedMultiplyAddNegatedScalar                                         */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_FusedMultiplyAddScalar                                                */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_FusedMultiplySubtract                                                 */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_FusedMultiplySubtractNegatedScalar                                    */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_FusedMultiplySubtractScalar                                           */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_FusedSubtractHalving                                                  */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Insert                                                                */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_AdvSimd_InsertScalar                                                          */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_LeadingSignCount                                                      */      HW_Flag_NoFlag,
        /* NI_AdvSimd_LeadingZeroCount                                                      */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Load2xVector64                                                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Load2xVector64AndUnzip                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Load3xVector64                                                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Load3xVector64AndUnzip                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Load4xVector64                                                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Load4xVector64AndUnzip                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadAndInsertScalar                                                   */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_LoadAndInsertScalarVector64x2                                         */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadAndInsertScalarVector64x3                                         */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadAndInsertScalarVector64x4                                         */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadAndReplicateToVector128                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_LoadAndReplicateToVector64                                            */      HW_Flag_NoFlag,
        /* NI_AdvSimd_LoadAndReplicateToVector64x2                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadAndReplicateToVector64x3                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadAndReplicateToVector64x4                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_LoadVector128                                                         */      HW_Flag_InvalidNodeId,
        /* NI_AdvSimd_LoadVector64                                                          */      HW_Flag_InvalidNodeId,
        /* NI_AdvSimd_Max                                                                   */      HW_Flag_Commutative,
        /* NI_AdvSimd_MaxNumber                                                             */      HW_Flag_Commutative,
        /* NI_AdvSimd_MaxNumberScalar                                                       */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_MaxPairwise                                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Min                                                                   */      HW_Flag_Commutative,
        /* NI_AdvSimd_MinNumber                                                             */      HW_Flag_Commutative,
        /* NI_AdvSimd_MinNumberScalar                                                       */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_MinPairwise                                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Multiply                                                              */      HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyAdd                                                           */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyAddByScalar                                                   */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyAddBySelectedScalar                                           */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyByScalar                                                      */      HW_Flag_NoFlag,
        /* NI_AdvSimd_MultiplyBySelectedScalar                                              */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyBySelectedScalarWideningLower                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndAdd                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndSubtract                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyBySelectedScalarWideningUpper                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndAdd                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndSubtract                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingByScalarSaturateHigh                                  */      HW_Flag_NoFlag,
        /* NI_AdvSimd_MultiplyDoublingBySelectedScalarSaturateHigh                          */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyDoublingSaturateHigh                                          */      HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyDoublingWideningLowerAndAddSaturate                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningLowerAndSubtractSaturate                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndAddSaturate                   */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndSubtractSaturate              */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndAddSaturate           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndSubtractSaturate      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningSaturateLower                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyDoublingWideningSaturateLowerByScalar                         */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_MultiplyDoublingWideningSaturateLowerBySelectedScalar                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyDoublingWideningSaturateUpper                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyDoublingWideningSaturateUpperByScalar                         */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_MultiplyDoublingWideningSaturateUpperBySelectedScalar                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyDoublingWideningUpperAndAddSaturate                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningUpperAndSubtractSaturate                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndAddSaturate                   */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndSubtractSaturate              */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndAddSaturate           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndSubtractSaturate      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyRoundedDoublingByScalarSaturateHigh                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_MultiplyRoundedDoublingBySelectedScalarSaturateHigh                   */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_MultiplyRoundedDoublingSaturateHigh                                   */      HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyScalar                                                        */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_MultiplyScalarBySelectedScalar                                        */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_MultiplySubtract                                                      */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplySubtractByScalar                                              */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplySubtractBySelectedScalar                                      */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyWideningLower                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyWideningLowerAndAdd                                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyWideningLowerAndSubtract                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyWideningUpper                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_MultiplyWideningUpperAndAdd                                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_MultiplyWideningUpperAndSubtract                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Negate                                                                */      HW_Flag_NoFlag,
        /* NI_AdvSimd_NegateSaturate                                                        */      HW_Flag_NoFlag,
        /* NI_AdvSimd_NegateScalar                                                          */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Not                                                                   */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Or                                                                    */      HW_Flag_Commutative,
        /* NI_AdvSimd_OrNot                                                                 */      HW_Flag_SpecialImport,
        /* NI_AdvSimd_PolynomialMultiply                                                    */      HW_Flag_Commutative,
        /* NI_AdvSimd_PolynomialMultiplyWideningLower                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_PolynomialMultiplyWideningUpper                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_AdvSimd_PopCount                                                              */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ReciprocalEstimate                                                    */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ReciprocalSquareRootEstimate                                          */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ReciprocalSquareRootStep                                              */      HW_Flag_Commutative,
        /* NI_AdvSimd_ReciprocalStep                                                        */      HW_Flag_Commutative,
        /* NI_AdvSimd_ReverseElement16                                                      */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_ReverseElement32                                                      */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_ReverseElement8                                                       */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_RoundAwayFromZero                                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_RoundAwayFromZeroScalar                                               */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_RoundToNearest                                                        */      HW_Flag_NoFlag,
        /* NI_AdvSimd_RoundToNearestScalar                                                  */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_RoundToNegativeInfinity                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_RoundToNegativeInfinityScalar                                         */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_RoundToPositiveInfinity                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_RoundToPositiveInfinityScalar                                         */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_RoundToZero                                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_RoundToZeroScalar                                                     */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftArithmetic                                                       */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftArithmeticRounded                                                */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftArithmeticRoundedSaturate                                        */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftArithmeticRoundedSaturateScalar                                  */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftArithmeticRoundedScalar                                          */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftArithmeticSaturate                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftArithmeticSaturateScalar                                         */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftArithmeticScalar                                                 */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLeftAndInsert                                                    */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftLeftAndInsertScalar                                              */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLeftLogical                                                      */      HW_Flag_HasImmediateOperand | HW_Flag_NoJmpTableIMM,
        /* NI_AdvSimd_ShiftLeftLogicalSaturate                                              */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftLeftLogicalSaturateScalar                                        */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLeftLogicalSaturateUnsigned                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftLeftLogicalSaturateUnsignedScalar                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLeftLogicalScalar                                                */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar | HW_Flag_NoJmpTableIMM,
        /* NI_AdvSimd_ShiftLeftLogicalWideningLower                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftLeftLogicalWideningUpper                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftLogical                                                          */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftLogicalRounded                                                   */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftLogicalRoundedSaturate                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftLogicalRoundedSaturateScalar                                     */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLogicalRoundedScalar                                             */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLogicalSaturate                                                  */      HW_Flag_NoFlag,
        /* NI_AdvSimd_ShiftLogicalSaturateScalar                                            */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftLogicalScalar                                                    */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightAndInsert                                                   */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightAndInsertScalar                                             */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightArithmetic                                                  */      HW_Flag_HasImmediateOperand | HW_Flag_NoJmpTableIMM,
        /* NI_AdvSimd_ShiftRightArithmeticAdd                                               */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightArithmeticAddScalar                                         */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateLower                            */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedLower                    */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedUpper                    */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUpper                            */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightArithmeticRounded                                           */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedAdd                                        */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedAddScalar                                  */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateLower                     */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedLower             */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedUpper             */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUpper                     */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightArithmeticRoundedScalar                                     */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightArithmeticScalar                                            */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar | HW_Flag_NoJmpTableIMM,
        /* NI_AdvSimd_ShiftRightLogical                                                     */      HW_Flag_HasImmediateOperand | HW_Flag_NoJmpTableIMM,
        /* NI_AdvSimd_ShiftRightLogicalAdd                                                  */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightLogicalAddScalar                                            */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightLogicalNarrowingLower                                       */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightLogicalNarrowingSaturateLower                               */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightLogicalNarrowingSaturateUpper                               */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightLogicalNarrowingUpper                                       */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightLogicalRounded                                              */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightLogicalRoundedAdd                                           */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightLogicalRoundedAddScalar                                     */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightLogicalRoundedNarrowingLower                                */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateLower                        */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateUpper                        */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightLogicalRoundedNarrowingUpper                                */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_ShiftRightLogicalRoundedScalar                                        */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_ShiftRightLogicalScalar                                               */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar | HW_Flag_NoJmpTableIMM,
        /* NI_AdvSimd_SignExtendWideningLower                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_SignExtendWideningUpper                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_SqrtScalar                                                            */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Store                                                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_BaseTypeFromValueTupleArg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_StoreSelectedScalar                                                   */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_BaseTypeFromValueTupleArg | HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_StoreVectorAndZip                                                     */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_BaseTypeFromValueTupleArg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Subtract                                                              */      HW_Flag_NoFlag,
        /* NI_AdvSimd_SubtractHighNarrowingLower                                            */      HW_Flag_NoFlag,
        /* NI_AdvSimd_SubtractHighNarrowingUpper                                            */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_SubtractRoundedHighNarrowingLower                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_SubtractRoundedHighNarrowingUpper                                     */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_SubtractSaturate                                                      */      HW_Flag_NoFlag,
        /* NI_AdvSimd_SubtractSaturateScalar                                                */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_SubtractScalar                                                        */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_SubtractWideningLower                                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_SubtractWideningUpper                                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_VectorTableLookup                                                     */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_VectorTableLookupExtension                                            */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Xor                                                                   */      HW_Flag_Commutative,
        /* NI_AdvSimd_ZeroExtendWideningLower                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_ZeroExtendWideningUpper                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_Abs                                                             */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_AbsSaturate                                                     */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_AbsSaturateScalar                                               */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_AbsScalar                                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_AbsoluteCompareGreaterThan                                      */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqual                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqualScalar                         */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanScalar                                */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_AbsoluteCompareLessThan                                         */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqual                                  */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqualScalar                            */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_AbsoluteCompareLessThanScalar                                   */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_AbsoluteDifference                                              */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_AbsoluteDifferenceScalar                                        */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Add                                                             */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_AddAcross                                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_AddAcrossWidening                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_AddPairwise                                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_AddPairwiseScalar                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_AddSaturate                                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_AddSaturateScalar                                               */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_Ceiling                                                         */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_CompareEqual                                                    */      HW_Flag_Commutative | HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_Arm64_CompareEqualScalar                                              */      HW_Flag_Commutative | HW_Flag_SIMDScalar | HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_Arm64_CompareGreaterThan                                              */      HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_Arm64_CompareGreaterThanOrEqual                                       */      HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar                                 */      HW_Flag_SIMDScalar | HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_Arm64_CompareGreaterThanScalar                                        */      HW_Flag_SIMDScalar | HW_Flag_SupportsContainment | HW_Flag_CanBenefitFromConstantProp,
        /* NI_AdvSimd_Arm64_CompareLessThan                                                 */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_CompareLessThanOrEqual                                          */      HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar                                    */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_CompareLessThanScalar                                           */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_CompareTest                                                     */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_CompareTestScalar                                               */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToDouble                                                 */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToDoubleScalar                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToDoubleUpper                                            */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZero                                 */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZeroScalar                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToEven                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToEvenScalar                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinity                           */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinityScalar                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinity                           */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinityScalar                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToZero                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToSingleLower                                            */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_ConvertToSingleRoundToOddLower                                  */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_ConvertToSingleRoundToOddUpper                                  */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_ConvertToSingleUpper                                            */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZero                                */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZeroScalar                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToEven                                      */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToEvenScalar                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinity                          */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinityScalar                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinity                          */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinityScalar                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToZero                                      */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Divide                                                          */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128                              */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_DuplicateToVector128                                            */      HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_AdvSimd_Arm64_DuplicateToVector64                                             */      HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_SupportsContainment,
        /* NI_AdvSimd_Arm64_ExtractNarrowingSaturateScalar                                  */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ExtractNarrowingSaturateUnsignedScalar                          */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Floor                                                           */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_FusedMultiplyAdd                                                */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_FusedMultiplyAddByScalar                                        */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_FusedMultiplyAddBySelectedScalar                                */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_FusedMultiplyAddScalarBySelectedScalar                          */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_FusedMultiplySubtract                                           */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_FusedMultiplySubtractByScalar                                   */      HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_FusedMultiplySubtractBySelectedScalar                           */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_AdvSimd_Arm64_FusedMultiplySubtractScalarBySelectedScalar                     */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_InsertSelectedScalar                                            */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_NoJmpTableIMM | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_Load2xVector128                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_Load2xVector128AndUnzip                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_Load3xVector128                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_Load3xVector128AndUnzip                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_Load4xVector128                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_Load4xVector128AndUnzip                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadAndInsertScalar                                             */      HW_Flag_NoCodeGen,
        /* NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2                                  */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3                                  */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4                                  */      HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadAndReplicateToVector128                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2                                   */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3                                   */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4                                   */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_MultiReg | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_LoadPairScalarVector64                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_MultiReg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal                               */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_MultiReg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_AdvSimd_Arm64_LoadPairVector128                                               */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_MultiReg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_AdvSimd_Arm64_LoadPairVector128NonTemporal                                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_MultiReg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_AdvSimd_Arm64_LoadPairVector64                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_MultiReg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_AdvSimd_Arm64_LoadPairVector64NonTemporal                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_MultiReg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_AdvSimd_Arm64_Max                                                             */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_MaxAcross                                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MaxNumber                                                       */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_MaxNumberAcross                                                 */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MaxNumberPairwise                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_MaxNumberPairwiseScalar                                         */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MaxPairwise                                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_MaxPairwiseScalar                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MaxScalar                                                       */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Min                                                             */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_MinAcross                                                       */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MinNumber                                                       */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_MinNumberAcross                                                 */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MinNumberPairwise                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_MinNumberPairwiseScalar                                         */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MinPairwise                                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_MinPairwiseScalar                                               */      HW_Flag_BaseTypeFromFirstArg,
        /* NI_AdvSimd_Arm64_MinScalar                                                       */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Multiply                                                        */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_MultiplyByScalar                                                */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_MultiplyBySelectedScalar                                        */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_Arm64_MultiplyDoublingSaturateHighScalar                              */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingScalarBySelectedScalarSaturateHigh              */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingWideningAndAddSaturateScalar                    */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingWideningAndSubtractSaturateScalar               */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalar                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalarBySelectedScalar          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndAddSaturate    */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndSubtractSaturat*/e     HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyExtended                                                */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_MultiplyExtendedByScalar                                        */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_MultiplyExtendedBySelectedScalar                                */      HW_Flag_HasImmediateOperand,
        /* NI_AdvSimd_Arm64_MultiplyExtendedScalar                                          */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyExtendedScalarBySelectedScalar                          */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyRoundedDoublingSaturateHighScalar                       */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarSaturateHigh       */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_MultiplyScalarBySelectedScalar                                  */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Negate                                                          */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_NegateSaturate                                                  */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_NegateSaturateScalar                                            */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_NegateScalar                                                    */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ReciprocalEstimate                                              */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_ReciprocalEstimateScalar                                        */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ReciprocalExponentScalar                                        */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ReciprocalSquareRootEstimate                                    */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_ReciprocalSquareRootEstimateScalar                              */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ReciprocalSquareRootStep                                        */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_ReciprocalSquareRootStepScalar                                  */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ReciprocalStep                                                  */      HW_Flag_Commutative,
        /* NI_AdvSimd_Arm64_ReciprocalStepScalar                                            */      HW_Flag_Commutative | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ReverseElementBits                                              */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_RoundAwayFromZero                                               */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_RoundToNearest                                                  */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_RoundToNegativeInfinity                                         */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_RoundToPositiveInfinity                                         */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_RoundToZero                                                     */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_ShiftArithmeticRoundedSaturateScalar                            */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftArithmeticSaturateScalar                                   */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftLeftLogicalSaturateScalar                                  */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftLeftLogicalSaturateUnsignedScalar                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftLogicalRoundedSaturateScalar                               */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftLogicalSaturateScalar                                      */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateScalar                     */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateUnsignedScalar             */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateScalar              */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedScalar      */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftRightLogicalNarrowingSaturateScalar                        */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_ShiftRightLogicalRoundedNarrowingSaturateScalar                 */      HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_Sqrt                                                            */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_Store                                                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_BaseTypeFromValueTupleArg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_StorePair                                                       */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_StorePairNonTemporal                                            */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_StorePairScalar                                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_StorePairScalarNonTemporal                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen,
        /* NI_AdvSimd_Arm64_StoreSelectedScalar                                             */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_BaseTypeFromValueTupleArg | HW_Flag_HasImmediateOperand | HW_Flag_SIMDScalar | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_StoreVectorAndZip                                               */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_BaseTypeFromValueTupleArg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_Subtract                                                        */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_SubtractSaturateScalar                                          */      HW_Flag_SIMDScalar,
        /* NI_AdvSimd_Arm64_TransposeEven                                                   */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_TransposeOdd                                                    */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_UnzipEven                                                       */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_UnzipOdd                                                        */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_VectorTableLookup                                               */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_VectorTableLookupExtension                                      */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_AdvSimd_Arm64_ZipHigh                                                         */      HW_Flag_NoFlag,
        /* NI_AdvSimd_Arm64_ZipLow                                                          */      HW_Flag_NoFlag,
        /* NI_Aes_Decrypt                                                                   */      HW_Flag_HasRMWSemantics,
        /* NI_Aes_Encrypt                                                                   */      HW_Flag_HasRMWSemantics,
        /* NI_Aes_InverseMixColumns                                                         */      HW_Flag_NoFlag,
        /* NI_Aes_MixColumns                                                                */      HW_Flag_NoFlag,
        /* NI_Aes_PolynomialMultiplyWideningLower                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_Aes_PolynomialMultiplyWideningUpper                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_Commutative,
        /* NI_ArmBase_LeadingZeroCount                                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_ReverseElementBits                                                    */      HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Yield                                                                 */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_SpecialSideEffect_Other,
        /* NI_ArmBase_Arm64_LeadingSignCount                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Arm64_LeadingZeroCount                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Arm64_MultiplyHigh                                                    */      HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Arm64_MultiplyLongAdd                                                 */      HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Arm64_MultiplyLongNeg                                                 */      HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Arm64_MultiplyLongSub                                                 */      HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed,
        /* NI_ArmBase_Arm64_ReverseElementBits                                              */      HW_Flag_NoFloatingPointUsed,
        /* NI_Crc32_ComputeCrc32                                                            */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen,
        /* NI_Crc32_ComputeCrc32C                                                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen,
        /* NI_Crc32_Arm64_ComputeCrc32                                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen,
        /* NI_Crc32_Arm64_ComputeCrc32C                                                     */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen,
        /* NI_Dp_DotProduct                                                                 */      HW_Flag_HasRMWSemantics,
        /* NI_Dp_DotProductBySelectedQuadruplet                                             */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Rdm_MultiplyRoundedDoublingAndAddSaturateHigh                                 */      HW_Flag_HasRMWSemantics,
        /* NI_Rdm_MultiplyRoundedDoublingAndSubtractSaturateHigh                            */      HW_Flag_HasRMWSemantics,
        /* NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndAddSaturateHigh                 */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndSubtractSaturateHigh            */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Rdm_Arm64_MultiplyRoundedDoublingAndAddSaturateHighScalar                     */      HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_Rdm_Arm64_MultiplyRoundedDoublingAndSubtractSaturateHighScalar                */      HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndAddSaturateHigh     */      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndSubtractSaturateHigh*/      HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SIMDScalar,
        /* NI_Sha1_FixedRotate                                                              */      HW_Flag_SIMDScalar,
        /* NI_Sha1_HashUpdateChoose                                                         */      HW_Flag_HasRMWSemantics,
        /* NI_Sha1_HashUpdateMajority                                                       */      HW_Flag_HasRMWSemantics,
        /* NI_Sha1_HashUpdateParity                                                         */      HW_Flag_HasRMWSemantics,
        /* NI_Sha1_ScheduleUpdate0                                                          */      HW_Flag_HasRMWSemantics,
        /* NI_Sha1_ScheduleUpdate1                                                          */      HW_Flag_HasRMWSemantics,
        /* NI_Sha256_HashUpdate1                                                            */      HW_Flag_HasRMWSemantics,
        /* NI_Sha256_HashUpdate2                                                            */      HW_Flag_HasRMWSemantics,
        /* NI_Sha256_ScheduleUpdate0                                                        */      HW_Flag_HasRMWSemantics,
        /* NI_Sha256_ScheduleUpdate1                                                        */      HW_Flag_HasRMWSemantics,
        /* NI_Sve_Abs                                                                       */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_AbsoluteCompareGreaterThan                                                */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_AbsoluteCompareGreaterThanOrEqual                                         */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_AbsoluteCompareLessThan                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_AbsoluteCompareLessThanOrEqual                                            */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_AbsoluteDifference                                                        */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Add                                                                       */      HW_Flag_Scalable | HW_Flag_OptionalEmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_AddAcross                                                                 */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_AddRotateComplex                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand,
        /* NI_Sve_AddSaturate                                                               */      HW_Flag_Scalable,
        /* NI_Sve_AddSequentialAcross                                                       */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_ReduceOperation,
        /* NI_Sve_And                                                                       */      HW_Flag_Scalable | HW_Flag_OptionalEmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_AndAcross                                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_BitwiseClear                                                              */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_OptionalEmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_BooleanNot                                                                */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Compact                                                                   */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_CompareEqual                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CompareGreaterThan                                                        */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CompareGreaterThanOrEqual                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CompareLessThan                                                           */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CompareLessThanOrEqual                                                    */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CompareNotEqualTo                                                         */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CompareUnordered                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_Compute16BitAddresses                                                     */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen,
        /* NI_Sve_Compute32BitAddresses                                                     */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen,
        /* NI_Sve_Compute64BitAddresses                                                     */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen,
        /* NI_Sve_Compute8BitAddresses                                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ConditionalExtractAfterLastActiveElement                                  */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ConditionalExtractAfterLastActiveElementAndReplicate                      */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve_ConditionalExtractLastActiveElement                                       */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ConditionalExtractLastActiveElementAndReplicate                           */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve_ConditionalSelect                                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_SupportsContainment | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_ConvertToDouble                                                           */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConvertToInt32                                                            */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConvertToInt64                                                            */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConvertToSingle                                                           */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConvertToUInt32                                                           */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConvertToUInt64                                                           */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Count16BitElements                                                        */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed,
        /* NI_Sve_Count32BitElements                                                        */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed,
        /* NI_Sve_Count64BitElements                                                        */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed,
        /* NI_Sve_Count8BitElements                                                         */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed,
        /* NI_Sve_CreateBreakAfterMask                                                      */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
        /* NI_Sve_CreateBreakAfterPropagateMask                                             */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CreateBreakBeforeMask                                                     */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
        /* NI_Sve_CreateBreakBeforePropagateMask                                            */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CreateBreakPropagateMask                                                  */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_HasRMWSemantics | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_CreateFalseMaskByte                                                       */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskDouble                                                     */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskInt16                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskInt32                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskInt64                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskSByte                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskSingle                                                     */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskUInt16                                                     */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskUInt32                                                     */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateFalseMaskUInt64                                                     */      HW_Flag_Scalable | HW_Flag_SpecialImport,
        /* NI_Sve_CreateMaskForFirstActiveElement                                           */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve_CreateMaskForNextActiveElement                                            */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_HasRMWSemantics,
        /* NI_Sve_CreateTrueMaskByte                                                        */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskDouble                                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskInt16                                                       */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskInt32                                                       */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskInt64                                                       */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskSByte                                                       */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskSingle                                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskUInt16                                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskUInt32                                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateTrueMaskUInt64                                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialImport,
        /* NI_Sve_CreateWhileLessThanMaskByte                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskDouble                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskInt16                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskInt32                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskInt64                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskSByte                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskSingle                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskUInt16                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskUInt32                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanMaskUInt64                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskByte                                        */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskDouble                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskInt16                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskInt32                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskInt64                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskSByte                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskSingle                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskUInt16                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskUInt32                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_CreateWhileLessThanOrEqualMaskUInt64                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_Divide                                                                    */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_DotProduct                                                                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve_DotProductBySelectedScalar                                                */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_LowVectorOperation,
        /* NI_Sve_DuplicateSelectedScalarToVector                                           */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve_ExtractAfterLastActiveElement                                             */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ExtractAfterLastActiveElementScalar                                       */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_Sve_ExtractLastActiveElement                                                  */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ExtractLastActiveElementScalar                                            */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_Sve_ExtractVector                                                             */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen,
        /* NI_Sve_FloatingPointExponentialAccelerator                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_FusedMultiplyAdd                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_FmaIntrinsic | HW_Flag_SpecialCodeGen,
        /* NI_Sve_FusedMultiplyAddBySelectedScalar                                          */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_FmaIntrinsic | HW_Flag_LowVectorOperation,
        /* NI_Sve_FusedMultiplyAddNegated                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_FmaIntrinsic | HW_Flag_SpecialCodeGen,
        /* NI_Sve_FusedMultiplySubtract                                                     */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_FmaIntrinsic | HW_Flag_SpecialCodeGen,
        /* NI_Sve_FusedMultiplySubtractBySelectedScalar                                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_FmaIntrinsic | HW_Flag_LowVectorOperation,
        /* NI_Sve_FusedMultiplySubtractNegated                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_FmaIntrinsic | HW_Flag_SpecialCodeGen,
        /* NI_Sve_GatherPrefetch16Bit                                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherPrefetch32Bit                                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherPrefetch64Bit                                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherPrefetch8Bit                                                        */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVector                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorByteZeroExtend                                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorByteZeroExtendFirstFaulting                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorFirstFaulting                                                 */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorInt16SignExtend                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorInt16SignExtendFirstFaulting                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorInt32SignExtend                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorInt32SignExtendFirstFaulting                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorSByteSignExtend                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorSByteSignExtendFirstFaulting                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorUInt16ZeroExtend                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting                                 */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorUInt32ZeroExtend                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting                                 */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorWithByteOffsetFirstFaulting                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GatherVectorWithByteOffsets                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_GetActiveElementCount                                                     */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_ExplicitMaskedOperation,
        /* NI_Sve_GetFfrByte                                                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrDouble                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrInt16                                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrInt32                                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrInt64                                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrSByte                                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrSingle                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrUInt16                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrUInt32                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_GetFfrUInt64                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_InsertIntoShiftedVector                                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve_LeadingSignCount                                                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_LeadingZeroCount                                                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Load2xVectorAndUnzip                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_NeedsConsecutiveRegisters | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_Load3xVectorAndUnzip                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_NeedsConsecutiveRegisters | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_Load4xVectorAndUnzip                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_MultiReg | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_NeedsConsecutiveRegisters | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_LoadVector                                                                */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVector128AndReplicateToVector                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteZeroExtendFirstFaulting                                     */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorByteZeroExtendToInt16                                           */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorByteZeroExtendToInt32                                           */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorByteZeroExtendToInt64                                           */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorByteZeroExtendToUInt16                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorByteZeroExtendToUInt32                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorByteZeroExtendToUInt64                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorFirstFaulting                                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt16SignExtendFirstFaulting                                    */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt16SignExtendToInt32                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorInt16SignExtendToInt64                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorInt16SignExtendToUInt32                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorInt16SignExtendToUInt64                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt32SignExtendFirstFaulting                                    */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorInt32SignExtendToInt64                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorInt32SignExtendToUInt64                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorNonFaulting                                                     */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorNonTemporal                                                     */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteSignExtendFirstFaulting                                    */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorSByteSignExtendToInt16                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorSByteSignExtendToInt32                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorSByteSignExtendToInt64                                          */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorSByteSignExtendToUInt16                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorSByteSignExtendToUInt32                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorSByteSignExtendToUInt64                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting                                   */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt16ZeroExtendToInt32                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorUInt16ZeroExtendToInt64                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorUInt16ZeroExtendToUInt32                                        */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorUInt16ZeroExtendToUInt64                                        */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting                                   */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_SpecialSideEffectMask,
        /* NI_Sve_LoadVectorUInt32ZeroExtendToInt64                                         */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_LoadVectorUInt32ZeroExtendToUInt64                                        */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve_Max                                                                       */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_MaxAcross                                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_MaxNumber                                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_MaxNumberAcross                                                           */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_Min                                                                       */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_MinAcross                                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_MinNumber                                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_MinNumberAcross                                                           */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_Multiply                                                                  */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_MultiplyAdd                                                               */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_FmaIntrinsic | HW_Flag_SpecialCodeGen,
        /* NI_Sve_MultiplyAddRotateComplex                                                  */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand,
        /* NI_Sve_MultiplyAddRotateComplexBySelectedScalar                                  */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_Sve_MultiplyBySelectedScalar                                                  */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve_MultiplyExtended                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_MultiplySubtract                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_FmaIntrinsic | HW_Flag_SpecialCodeGen,
        /* NI_Sve_Negate                                                                    */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Not                                                                       */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation,
        /* NI_Sve_Or                                                                        */      HW_Flag_Scalable | HW_Flag_OptionalEmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_OrAcross                                                                  */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_PopCount                                                                  */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Prefetch16Bit                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_Prefetch32Bit                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_Prefetch64Bit                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_Prefetch8Bit                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasImmediateOperand | HW_Flag_SpecialSideEffect_Other,
        /* NI_Sve_ReciprocalEstimate                                                        */      HW_Flag_Scalable,
        /* NI_Sve_ReciprocalExponent                                                        */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ReciprocalSqrtEstimate                                                    */      HW_Flag_Scalable,
        /* NI_Sve_ReciprocalSqrtStep                                                        */      HW_Flag_Scalable,
        /* NI_Sve_ReciprocalStep                                                            */      HW_Flag_Scalable,
        /* NI_Sve_ReverseBits                                                               */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ReverseElement                                                            */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_ReverseElement16                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ReverseElement32                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ReverseElement8                                                           */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_RoundAwayFromZero                                                         */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_RoundToNearest                                                            */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_RoundToNegativeInfinity                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_RoundToPositiveInfinity                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_RoundToZero                                                               */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_SaturatingDecrementBy16BitElementCount                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingDecrementBy32BitElementCount                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingDecrementBy64BitElementCount                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingDecrementBy8BitElementCount                                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingDecrementByActiveElementCount                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy16BitElementCount                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy32BitElementCount                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy64BitElementCount                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_HasScalarInputVariant | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy8BitElementCount                                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementByActiveElementCount                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_BaseTypeFromSecondArg | HW_Flag_HasRMWSemantics,
        /* NI_Sve_Scale                                                                     */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve_Scatter                                                                   */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Scatter16BitNarrowing                                                     */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Scatter16BitWithByteOffsetsNarrowing                                      */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Scatter32BitNarrowing                                                     */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Scatter32BitWithByteOffsetsNarrowing                                      */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Scatter8BitNarrowing                                                      */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Scatter8BitWithByteOffsetsNarrowing                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ScatterWithByteOffsets                                                    */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_SetFfr                                                                    */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialSideEffect_Other | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ShiftLeftLogical                                                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve_ShiftRightArithmetic                                                      */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve_ShiftRightArithmeticForDivide                                             */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand,
        /* NI_Sve_ShiftRightLogical                                                         */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SignExtend16                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_SignExtend32                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_SignExtend8                                                               */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_SignExtendWideningLower                                                   */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_SignExtendWideningUpper                                                   */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_Splice                                                                    */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Sqrt                                                                      */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_StoreAndZip                                                               */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_ExplicitMaskedOperation | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_LowMaskedOperation,
        /* NI_Sve_StoreNarrowing                                                            */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_ExplicitMaskedOperation | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_LowMaskedOperation,
        /* NI_Sve_StoreNonTemporal                                                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_ExplicitMaskedOperation | HW_Flag_SpecialCodeGen | HW_Flag_LowMaskedOperation,
        /* NI_Sve_Subtract                                                                  */      HW_Flag_Scalable | HW_Flag_OptionalEmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve_SubtractSaturate                                                          */      HW_Flag_Scalable,
        /* NI_Sve_TestAnyTrue                                                               */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
        /* NI_Sve_TestFirstTrue                                                             */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
        /* NI_Sve_TestLastTrue                                                              */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
        /* NI_Sve_TransposeEven                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_TransposeOdd                                                              */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_TrigonometricMultiplyAddCoefficient                                       */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen,
        /* NI_Sve_TrigonometricSelectCoefficient                                            */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_TrigonometricStartingValue                                                */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_UnzipEven                                                                 */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_UnzipOdd                                                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_VectorTableLookup                                                         */      HW_Flag_Scalable,
        /* NI_Sve_Xor                                                                       */      HW_Flag_Scalable | HW_Flag_OptionalEmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_XorAcross                                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve_ZeroExtend16                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ZeroExtend32                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ZeroExtend8                                                               */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ZeroExtendWideningLower                                                   */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_ZeroExtendWideningUpper                                                   */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve_ZipHigh                                                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve_ZipLow                                                                    */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasAllMaskVariant,
        /* NI_Sve2_AbsSaturate                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_AbsoluteDifferenceAdd                                                    */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AbsoluteDifferenceWideningEven                                           */      HW_Flag_Scalable,
        /* NI_Sve2_AbsoluteDifferenceWideningLowerAndAddEven                                */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AbsoluteDifferenceWideningLowerAndAddOdd                                 */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AbsoluteDifferenceWideningOdd                                            */      HW_Flag_Scalable,
        /* NI_Sve2_AddCarryWideningEven                                                     */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AddCarryWideningOdd                                                      */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AddHighNarrowingEven                                                     */      HW_Flag_Scalable,
        /* NI_Sve2_AddHighNarrowingOdd                                                      */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AddPairwise                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_ReduceOperation,
        /* NI_Sve2_AddPairwiseWideningAndAdd                                                */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_AddRotateComplex                                                         */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_AddRoundedHighNarrowingEven                                              */      HW_Flag_Scalable,
        /* NI_Sve2_AddRoundedHighNarrowingOdd                                               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_AddSaturate                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_AddSaturateRotateComplex                                                 */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_AddWideningEven                                                          */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_AddWideningEvenOdd                                                       */      HW_Flag_Scalable,
        /* NI_Sve2_AddWideningOdd                                                           */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_BitwiseClearXor                                                          */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_BitwiseSelect                                                            */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_BitwiseSelectLeftInverted                                                */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_BitwiseSelectRightInverted                                               */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ConvertToDoubleOdd                                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ConvertToSingleEvenRoundToOdd                                            */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ConvertToSingleOdd                                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ConvertToSingleOddRoundToOdd                                             */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_CountMatchingElements                                                    */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_CountMatchingElementsIn128BitSegments                                    */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg,
        /* NI_Sve2_CreateWhileGreaterThanMaskByte                                           */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskDouble                                         */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskInt16                                          */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskInt32                                          */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskInt64                                          */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskSByte                                          */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskSingle                                         */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskUInt16                                         */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskUInt32                                         */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanMaskUInt64                                         */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskByte                                    */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskDouble                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt16                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt32                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt64                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskSByte                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskSingle                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt16                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt32                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt64                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskByte                                        */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskDouble                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskInt16                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskInt32                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskInt64                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskSByte                                       */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskSingle                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskUInt16                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskUInt32                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_CreateWhileReadAfterWriteMaskUInt64                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve2_DotProductRotateComplex                                                  */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_DotProductRotateComplexBySelectedIndex                                   */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation | HW_Flag_SpecialImport | HW_Flag_BaseTypeFromSecondArg,
        /* NI_Sve2_FusedAddHalving                                                          */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_FusedAddRoundedHalving                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_FusedSubtractHalving                                                     */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_GatherVectorByteZeroExtendNonTemporal                                    */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorInt16SignExtendNonTemporal                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal                    */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorInt32SignExtendNonTemporal                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal                    */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorNonTemporal                                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorSByteSignExtendNonTemporal                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal                                  */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_GatherVectorWithByteOffsetsNonTemporal                                   */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_InterleavingXorEvenOdd                                                   */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_InterleavingXorOddEven                                                   */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_Log2                                                                     */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Match                                                                    */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_MaxNumberPairwise                                                        */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MaxPairwise                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_ReduceOperation,
        /* NI_Sve2_MinNumberPairwise                                                        */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MinPairwise                                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_ReduceOperation,
        /* NI_Sve2_MultiplyAddBySelectedScalar                                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyAddRotateComplex                                                 */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_MultiplyAddRotateComplexBySelectedScalar                                 */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex                      */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation | HW_Flag_HasRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
        /* NI_Sve2_MultiplyBySelectedScalar                                                 */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyBySelectedScalarWideningEven                                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd                               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract                          */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyBySelectedScalarWideningOdd                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd                                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract                           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingBySelectedScalarSaturateHigh                             */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingSaturateHigh                                             */      HW_Flag_Scalable,
        /* NI_Sve2_MultiplyDoublingWideningAndAddSaturateEven                               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyDoublingWideningAndAddSaturateEvenOdd                            */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyDoublingWideningAndAddSaturateOdd                                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEven                          */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEvenOdd                       */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateOdd                           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven          */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingWideningSaturateEven                                     */      HW_Flag_Scalable,
        /* NI_Sve2_MultiplyDoublingWideningSaturateEvenBySelectedScalar                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyDoublingWideningSaturateOdd                                      */      HW_Flag_Scalable,
        /* NI_Sve2_MultiplyDoublingWideningSaturateOddBySelectedScalar                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyRoundedDoublingBySelectedScalarSaturateHigh                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyRoundedDoublingSaturateAndAddHigh                                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyRoundedDoublingSaturateAndSubtractHigh                           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndAddHigh                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndSubtractHigh           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyRoundedDoublingSaturateHigh                                      */      HW_Flag_Scalable,
        /* NI_Sve2_MultiplySubtractBySelectedScalar                                         */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics | HW_Flag_LowVectorOperation,
        /* NI_Sve2_MultiplyWideningEven                                                     */      HW_Flag_Scalable,
        /* NI_Sve2_MultiplyWideningEvenAndAdd                                               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyWideningEvenAndSubtract                                          */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyWideningOdd                                                      */      HW_Flag_Scalable,
        /* NI_Sve2_MultiplyWideningOddAndAdd                                                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_MultiplyWideningOddAndSubtract                                           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_NegateSaturate                                                           */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_NoMatch                                                                  */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_ZeroingMaskedOperation,
        /* NI_Sve2_PolynomialMultiply                                                       */      HW_Flag_Scalable,
        /* NI_Sve2_PolynomialMultiplyWideningEven                                           */      HW_Flag_Scalable,
        /* NI_Sve2_PolynomialMultiplyWideningOdd                                            */      HW_Flag_Scalable,
        /* NI_Sve2_ReciprocalEstimate                                                       */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ReciprocalSqrtEstimate                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Scatter16BitNarrowingNonTemporal                                         */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Scatter16BitWithByteOffsetsNarrowingNonTemporal                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Scatter32BitNarrowingNonTemporal                                         */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Scatter32BitWithByteOffsetsNarrowingNonTemporal                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Scatter8BitNarrowingNonTemporal                                          */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_Scatter8BitWithByteOffsetsNarrowingNonTemporal                           */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ScatterNonTemporal                                                       */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ScatterWithByteOffsetsNonTemporal                                        */      HW_Flag_Scalable | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ShiftArithmeticRounded                                                   */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ShiftArithmeticRoundedSaturate                                           */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ShiftArithmeticSaturate                                                  */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_LowMaskedOperation,
        /* NI_Sve2_ShiftLeftAndInsert                                                       */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftLeftLogicalSaturate                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftLeftLogicalSaturateUnsigned                                         */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftLeftLogicalWideningEven                                             */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftLeftLogicalWideningOdd                                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftLogicalRounded                                                      */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftLogicalRoundedSaturate                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightAndInsert                                                      */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightArithmeticAdd                                                  */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightArithmeticNarrowingSaturateEven                                */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightArithmeticNarrowingSaturateOdd                                 */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedEven                        */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedOdd                         */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightArithmeticRounded                                              */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightArithmeticRoundedAdd                                           */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateEven                         */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateOdd                          */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedEven                 */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedOdd                  */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalAdd                                                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalNarrowingEven                                           */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightLogicalNarrowingOdd                                            */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalNarrowingSaturateEven                                   */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightLogicalNarrowingSaturateOdd                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalRounded                                                 */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalRoundedAdd                                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalRoundedNarrowingEven                                    */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightLogicalRoundedNarrowingOdd                                     */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateEven                            */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand,
        /* NI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateOdd                             */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_SubtractBorrowWideningEven                                               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_SubtractBorrowWideningOdd                                                */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_SubtractHighNarrowingEven                                                */      HW_Flag_Scalable,
        /* NI_Sve2_SubtractHighNarrowingOdd                                                 */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_SubtractRoundedHighNarrowingEven                                         */      HW_Flag_Scalable,
        /* NI_Sve2_SubtractRoundedHighNarrowingOdd                                          */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_SubtractSaturate                                                         */      HW_Flag_Scalable | HW_Flag_EmbeddedMaskedOperation | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_SubtractWideningEven                                                     */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_SubtractWideningEvenOdd                                                  */      HW_Flag_Scalable,
        /* NI_Sve2_SubtractWideningOdd                                                      */      HW_Flag_Scalable | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_SubtractWideningOddEven                                                  */      HW_Flag_Scalable,
        /* NI_Sve2_VectorTableLookup                                                        */      HW_Flag_Scalable | HW_Flag_NeedsConsecutiveRegisters | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
        /* NI_Sve2_VectorTableLookupExtension                                               */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_Xor                                                                      */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_HasRMWSemantics,
        /* NI_Sve2_XorRotateRight                                                           */      HW_Flag_Scalable | HW_Flag_HasRMWSemantics | HW_Flag_HasImmediateOperand,
        /* NI_Sve_ConditionalExtractAfterLastActiveElementScalar                            */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConditionalExtractLastActiveElementScalar                                 */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConvertMaskToVector                                                       */      HW_Flag_Scalable,
        /* NI_Sve_ConvertVectorToMask                                                       */      HW_Flag_Scalable | HW_Flag_ExplicitMaskedOperation | HW_Flag_ReturnsPerElementMask | HW_Flag_LowMaskedOperation,
        /* NI_Sve_ConversionTrueMask                                                        */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_SaturatingDecrementBy16BitElementCountScalar                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingDecrementBy32BitElementCountScalar                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingDecrementBy64BitElementCountScalar                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy16BitElementCountScalar                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy32BitElementCountScalar                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_SaturatingIncrementBy64BitElementCountScalar                              */      HW_Flag_Scalable | HW_Flag_HasImmediateOperand | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_HasRMWSemantics,
        /* NI_Sve_StoreAndZipx2                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_Sve_StoreAndZipx3                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_Sve_StoreAndZipx4                                                             */      HW_Flag_Scalable | HW_Flag_SpecialCodeGen | HW_Flag_ExplicitMaskedOperation | HW_Flag_LowMaskedOperation | HW_Flag_NeedsConsecutiveRegisters,
        /* NI_Sve_And_Predicates                                                            */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask | HW_Flag_EmbeddedMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_BitwiseClear_Predicates                                                   */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask | HW_Flag_EmbeddedMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_Or_Predicates                                                             */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask | HW_Flag_EmbeddedMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_Xor_Predicates                                                            */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask | HW_Flag_EmbeddedMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ConditionalSelect_Predicates                                              */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask | HW_Flag_ExplicitMaskedOperation | HW_Flag_SpecialCodeGen,
        /* NI_Sve_ZipHigh_Predicates                                                        */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_ZipLow_Predicates                                                         */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_UnzipEven_Predicates                                                      */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_UnzipOdd_Predicates                                                       */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_TransposeEven_Predicates                                                  */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_TransposeOdd_Predicates                                                   */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
        /* NI_Sve_ReverseElement_Predicates                                                 */      HW_Flag_Scalable | HW_Flag_ReturnsPerElementMask,
    ];

#if DEBUG
    private static string[] s_names = [
        "Abs",																		// NI_Vector64_Abs
        "AddSaturate",																// NI_Vector64_AddSaturate
        "AndNot",																	// NI_Vector64_AndNot
        "As",																		// NI_Vector64_As
        "AsByte",																	// NI_Vector64_AsByte
        "AsDouble",																	// NI_Vector64_AsDouble
        "AsInt16",																	// NI_Vector64_AsInt16
        "AsInt32",																	// NI_Vector64_AsInt32
        "AsInt64",																	// NI_Vector64_AsInt64
        "AsNInt",																	// NI_Vector64_AsNInt
        "AsNUInt",																	// NI_Vector64_AsNUInt
        "AsSByte",																	// NI_Vector64_AsSByte
        "AsSingle",																	// NI_Vector64_AsSingle
        "AsUInt16",																	// NI_Vector64_AsUInt16
        "AsUInt32",																	// NI_Vector64_AsUInt32
        "AsUInt64",																	// NI_Vector64_AsUInt64
        "Ceiling",																	// NI_Vector64_Ceiling
        "ConditionalSelect",														// NI_Vector64_ConditionalSelect
        "ConvertToDouble",															// NI_Vector64_ConvertToDouble
        "ConvertToInt32",															// NI_Vector64_ConvertToInt32
        "ConvertToInt32Native",														// NI_Vector64_ConvertToInt32Native
        "ConvertToInt64",															// NI_Vector64_ConvertToInt64
        "ConvertToInt64Native",														// NI_Vector64_ConvertToInt64Native
        "ConvertToSingle",															// NI_Vector64_ConvertToSingle
        "ConvertToUInt32",															// NI_Vector64_ConvertToUInt32
        "ConvertToUInt32Native",													// NI_Vector64_ConvertToUInt32Native
        "ConvertToUInt64",															// NI_Vector64_ConvertToUInt64
        "ConvertToUInt64Native",													// NI_Vector64_ConvertToUInt64Native
        "Create",																	// NI_Vector64_Create
        "CreateScalar",																// NI_Vector64_CreateScalar
        "CreateScalarUnsafe",														// NI_Vector64_CreateScalarUnsafe
        "CreateSequence",															// NI_Vector64_CreateSequence
        "Dot",																		// NI_Vector64_Dot
        "Equals",																	// NI_Vector64_Equals
        "EqualsAny",																// NI_Vector64_EqualsAny
        "ExtractMostSignificantBits",												// NI_Vector64_ExtractMostSignificantBits
        "Floor",																	// NI_Vector64_Floor
        "FusedMultiplyAdd",															// NI_Vector64_FusedMultiplyAdd
        "GetElement",																// NI_Vector64_GetElement
        "GreaterThan",																// NI_Vector64_GreaterThan
        "GreaterThanAll",															// NI_Vector64_GreaterThanAll
        "GreaterThanAny",															// NI_Vector64_GreaterThanAny
        "GreaterThanOrEqual",														// NI_Vector64_GreaterThanOrEqual
        "GreaterThanOrEqualAll",													// NI_Vector64_GreaterThanOrEqualAll
        "GreaterThanOrEqualAny",													// NI_Vector64_GreaterThanOrEqualAny
        "IsEvenInteger",															// NI_Vector64_IsEvenInteger
        "IsFinite",																	// NI_Vector64_IsFinite
        "IsInfinity",																// NI_Vector64_IsInfinity
        "IsInteger",																// NI_Vector64_IsInteger
        "IsNaN",																	// NI_Vector64_IsNaN
        "IsNegative",																// NI_Vector64_IsNegative
        "IsNegativeInfinity",														// NI_Vector64_IsNegativeInfinity
        "IsNormal",																	// NI_Vector64_IsNormal
        "IsOddInteger",																// NI_Vector64_IsOddInteger
        "IsPositive",																// NI_Vector64_IsPositive
        "IsPositiveInfinity",														// NI_Vector64_IsPositiveInfinity
        "IsSubnormal",																// NI_Vector64_IsSubnormal
        "IsZero",																	// NI_Vector64_IsZero
        "LessThan",																	// NI_Vector64_LessThan
        "LessThanAll",																// NI_Vector64_LessThanAll
        "LessThanAny",																// NI_Vector64_LessThanAny
        "LessThanOrEqual",															// NI_Vector64_LessThanOrEqual
        "LessThanOrEqualAll",														// NI_Vector64_LessThanOrEqualAll
        "LessThanOrEqualAny",														// NI_Vector64_LessThanOrEqualAny
        "LoadAligned",																// NI_Vector64_LoadAligned
        "LoadAlignedNonTemporal",													// NI_Vector64_LoadAlignedNonTemporal
        "LoadUnsafe",																// NI_Vector64_LoadUnsafe
        "Max",																		// NI_Vector64_Max
        "MaxMagnitude",																// NI_Vector64_MaxMagnitude
        "MaxMagnitudeNumber",														// NI_Vector64_MaxMagnitudeNumber
        "MaxNative",																// NI_Vector64_MaxNative
        "MaxNumber",																// NI_Vector64_MaxNumber
        "Min",																		// NI_Vector64_Min
        "MinMagnitude",																// NI_Vector64_MinMagnitude
        "MinMagnitudeNumber",														// NI_Vector64_MinMagnitudeNumber
        "MinNative",																// NI_Vector64_MinNative
        "MinNumber",																// NI_Vector64_MinNumber
        "MultiplyAddEstimate",														// NI_Vector64_MultiplyAddEstimate
        "Narrow",																	// NI_Vector64_Narrow
        "NarrowWithSaturation",														// NI_Vector64_NarrowWithSaturation
        "Round",																	// NI_Vector64_Round
        "ShiftLeft",																// NI_Vector64_ShiftLeft
        "Shuffle",																	// NI_Vector64_Shuffle
        "ShuffleNative",															// NI_Vector64_ShuffleNative
        "ShuffleNativeFallback",													// NI_Vector64_ShuffleNativeFallback
        "Sqrt",																		// NI_Vector64_Sqrt
        "StoreAligned",																// NI_Vector64_StoreAligned
        "StoreAlignedNonTemporal",													// NI_Vector64_StoreAlignedNonTemporal
        "StoreUnsafe",																// NI_Vector64_StoreUnsafe
        "SubtractSaturate",															// NI_Vector64_SubtractSaturate
        "Sum",																		// NI_Vector64_Sum
        "ToScalar",																	// NI_Vector64_ToScalar
        "ToVector128",																// NI_Vector64_ToVector128
        "ToVector128Unsafe",														// NI_Vector64_ToVector128Unsafe
        "Truncate",																	// NI_Vector64_Truncate
        "WidenLower",																// NI_Vector64_WidenLower
        "WidenUpper",																// NI_Vector64_WidenUpper
        "WithElement",																// NI_Vector64_WithElement
        "get_AllBitsSet",															// NI_Vector64_get_AllBitsSet
        "get_E",																	// NI_Vector64_get_E
        "get_Epsilon",																// NI_Vector64_get_Epsilon
        "get_Indices",																// NI_Vector64_get_Indices
        "get_NaN",																	// NI_Vector64_get_NaN
        "get_NegativeInfinity",														// NI_Vector64_get_NegativeInfinity
        "get_NegativeOne",															// NI_Vector64_get_NegativeOne
        "get_NegativeZero",															// NI_Vector64_get_NegativeZero
        "get_One",																	// NI_Vector64_get_One
        "get_Pi",																	// NI_Vector64_get_Pi
        "get_PositiveInfinity",														// NI_Vector64_get_PositiveInfinity
        "get_Tau",																	// NI_Vector64_get_Tau
        "get_Zero",																	// NI_Vector64_get_Zero
        "op_Addition",																// NI_Vector64_op_Addition
        "op_BitwiseAnd",															// NI_Vector64_op_BitwiseAnd
        "op_BitwiseOr",																// NI_Vector64_op_BitwiseOr
        "op_Division",																// NI_Vector64_op_Division
        "op_Equality",																// NI_Vector64_op_Equality
        "op_ExclusiveOr",															// NI_Vector64_op_ExclusiveOr
        "op_Inequality",															// NI_Vector64_op_Inequality
        "op_LeftShift",																// NI_Vector64_op_LeftShift
        "op_Multiply",																// NI_Vector64_op_Multiply
        "op_OnesComplement",														// NI_Vector64_op_OnesComplement
        "op_RightShift",															// NI_Vector64_op_RightShift
        "op_Subtraction",															// NI_Vector64_op_Subtraction
        "op_UnaryNegation",															// NI_Vector64_op_UnaryNegation
        "op_UnaryPlus",																// NI_Vector64_op_UnaryPlus
        "op_UnsignedRightShift",													// NI_Vector64_op_UnsignedRightShift
        "Abs",																		// NI_Vector128_Abs
        "AddSaturate",																// NI_Vector128_AddSaturate
        "AndNot",																	// NI_Vector128_AndNot
        "As",																		// NI_Vector128_As
        "AsByte",																	// NI_Vector128_AsByte
        "AsDouble",																	// NI_Vector128_AsDouble
        "AsInt16",																	// NI_Vector128_AsInt16
        "AsInt32",																	// NI_Vector128_AsInt32
        "AsInt64",																	// NI_Vector128_AsInt64
        "AsNInt",																	// NI_Vector128_AsNInt
        "AsNUInt",																	// NI_Vector128_AsNUInt
        "AsSByte",																	// NI_Vector128_AsSByte
        "AsSingle",																	// NI_Vector128_AsSingle
        "AsUInt16",																	// NI_Vector128_AsUInt16
        "AsUInt32",																	// NI_Vector128_AsUInt32
        "AsUInt64",																	// NI_Vector128_AsUInt64
        "AsVector",																	// NI_Vector128_AsVector
        "AsVector128",																// NI_Vector128_AsVector128
        "AsVector128Unsafe",														// NI_Vector128_AsVector128Unsafe
        "AsVector2",																// NI_Vector128_AsVector2
        "AsVector3",																// NI_Vector128_AsVector3
        "AsVector4",																// NI_Vector128_AsVector4
        "Ceiling",																	// NI_Vector128_Ceiling
        "ConditionalSelect",														// NI_Vector128_ConditionalSelect
        "ConvertToDouble",															// NI_Vector128_ConvertToDouble
        "ConvertToInt32",															// NI_Vector128_ConvertToInt32
        "ConvertToInt32Native",														// NI_Vector128_ConvertToInt32Native
        "ConvertToInt64",															// NI_Vector128_ConvertToInt64
        "ConvertToInt64Native",														// NI_Vector128_ConvertToInt64Native
        "ConvertToSingle",															// NI_Vector128_ConvertToSingle
        "ConvertToUInt32",															// NI_Vector128_ConvertToUInt32
        "ConvertToUInt32Native",													// NI_Vector128_ConvertToUInt32Native
        "ConvertToUInt64",															// NI_Vector128_ConvertToUInt64
        "ConvertToUInt64Native",													// NI_Vector128_ConvertToUInt64Native
        "Create",																	// NI_Vector128_Create
        "CreateScalar",																// NI_Vector128_CreateScalar
        "CreateScalarUnsafe",														// NI_Vector128_CreateScalarUnsafe
        "CreateSequence",															// NI_Vector128_CreateSequence
        "Dot",																		// NI_Vector128_Dot
        "Equals",																	// NI_Vector128_Equals
        "EqualsAny",																// NI_Vector128_EqualsAny
        "ExtractMostSignificantBits",												// NI_Vector128_ExtractMostSignificantBits
        "Floor",																	// NI_Vector128_Floor
        "FusedMultiplyAdd",															// NI_Vector128_FusedMultiplyAdd
        "GetElement",																// NI_Vector128_GetElement
        "GetLower",																	// NI_Vector128_GetLower
        "GetUpper",																	// NI_Vector128_GetUpper
        "GreaterThan",																// NI_Vector128_GreaterThan
        "GreaterThanAll",															// NI_Vector128_GreaterThanAll
        "GreaterThanAny",															// NI_Vector128_GreaterThanAny
        "GreaterThanOrEqual",														// NI_Vector128_GreaterThanOrEqual
        "GreaterThanOrEqualAll",													// NI_Vector128_GreaterThanOrEqualAll
        "GreaterThanOrEqualAny",													// NI_Vector128_GreaterThanOrEqualAny
        "IsEvenInteger",															// NI_Vector128_IsEvenInteger
        "IsFinite",																	// NI_Vector128_IsFinite
        "IsInfinity",																// NI_Vector128_IsInfinity
        "IsInteger",																// NI_Vector128_IsInteger
        "IsNaN",																	// NI_Vector128_IsNaN
        "IsNegative",																// NI_Vector128_IsNegative
        "IsNegativeInfinity",														// NI_Vector128_IsNegativeInfinity
        "IsNormal",																	// NI_Vector128_IsNormal
        "IsOddInteger",																// NI_Vector128_IsOddInteger
        "IsPositive",																// NI_Vector128_IsPositive
        "IsPositiveInfinity",														// NI_Vector128_IsPositiveInfinity
        "IsSubnormal",																// NI_Vector128_IsSubnormal
        "IsZero",																	// NI_Vector128_IsZero
        "LessThan",																	// NI_Vector128_LessThan
        "LessThanAll",																// NI_Vector128_LessThanAll
        "LessThanAny",																// NI_Vector128_LessThanAny
        "LessThanOrEqual",															// NI_Vector128_LessThanOrEqual
        "LessThanOrEqualAll",														// NI_Vector128_LessThanOrEqualAll
        "LessThanOrEqualAny",														// NI_Vector128_LessThanOrEqualAny
        "LoadAligned",																// NI_Vector128_LoadAligned
        "LoadAlignedNonTemporal",													// NI_Vector128_LoadAlignedNonTemporal
        "LoadUnsafe",																// NI_Vector128_LoadUnsafe
        "Max",																		// NI_Vector128_Max
        "MaxMagnitude",																// NI_Vector128_MaxMagnitude
        "MaxMagnitudeNumber",														// NI_Vector128_MaxMagnitudeNumber
        "MaxNative",																// NI_Vector128_MaxNative
        "MaxNumber",																// NI_Vector128_MaxNumber
        "Min",																		// NI_Vector128_Min
        "MinMagnitude",																// NI_Vector128_MinMagnitude
        "MinMagnitudeNumber",														// NI_Vector128_MinMagnitudeNumber
        "MinNative",																// NI_Vector128_MinNative
        "MinNumber",																// NI_Vector128_MinNumber
        "MultiplyAddEstimate",														// NI_Vector128_MultiplyAddEstimate
        "Narrow",																	// NI_Vector128_Narrow
        "NarrowWithSaturation",														// NI_Vector128_NarrowWithSaturation
        "Round",																	// NI_Vector128_Round
        "ShiftLeft",																// NI_Vector128_ShiftLeft
        "Shuffle",																	// NI_Vector128_Shuffle
        "ShuffleNative",															// NI_Vector128_ShuffleNative
        "ShuffleNativeFallback",													// NI_Vector128_ShuffleNativeFallback
        "Sqrt",																		// NI_Vector128_Sqrt
        "StoreAligned",																// NI_Vector128_StoreAligned
        "StoreAlignedNonTemporal",													// NI_Vector128_StoreAlignedNonTemporal
        "StoreUnsafe",																// NI_Vector128_StoreUnsafe
        "SubtractSaturate",															// NI_Vector128_SubtractSaturate
        "Sum",																		// NI_Vector128_Sum
        "ToScalar",																	// NI_Vector128_ToScalar
        "Truncate",																	// NI_Vector128_Truncate
        "WidenLower",																// NI_Vector128_WidenLower
        "WidenUpper",																// NI_Vector128_WidenUpper
        "WithElement",																// NI_Vector128_WithElement
        "WithLower",																// NI_Vector128_WithLower
        "WithUpper",																// NI_Vector128_WithUpper
        "get_AllBitsSet",															// NI_Vector128_get_AllBitsSet
        "get_E",																	// NI_Vector128_get_E
        "get_Epsilon",																// NI_Vector128_get_Epsilon
        "get_Indices",																// NI_Vector128_get_Indices
        "get_NaN",																	// NI_Vector128_get_NaN
        "get_NegativeInfinity",														// NI_Vector128_get_NegativeInfinity
        "get_NegativeOne",															// NI_Vector128_get_NegativeOne
        "get_NegativeZero",															// NI_Vector128_get_NegativeZero
        "get_One",																	// NI_Vector128_get_One
        "get_Pi",																	// NI_Vector128_get_Pi
        "get_PositiveInfinity",														// NI_Vector128_get_PositiveInfinity
        "get_Tau",																	// NI_Vector128_get_Tau
        "get_Zero",																	// NI_Vector128_get_Zero
        "op_Addition",																// NI_Vector128_op_Addition
        "op_BitwiseAnd",															// NI_Vector128_op_BitwiseAnd
        "op_BitwiseOr",																// NI_Vector128_op_BitwiseOr
        "op_Division",																// NI_Vector128_op_Division
        "op_Equality",																// NI_Vector128_op_Equality
        "op_ExclusiveOr",															// NI_Vector128_op_ExclusiveOr
        "op_Inequality",															// NI_Vector128_op_Inequality
        "op_LeftShift",																// NI_Vector128_op_LeftShift
        "op_Multiply",																// NI_Vector128_op_Multiply
        "op_OnesComplement",														// NI_Vector128_op_OnesComplement
        "op_RightShift",															// NI_Vector128_op_RightShift
        "op_Subtraction",															// NI_Vector128_op_Subtraction
        "op_UnaryNegation",															// NI_Vector128_op_UnaryNegation
        "op_UnaryPlus",																// NI_Vector128_op_UnaryPlus
        "op_UnsignedRightShift",													// NI_Vector128_op_UnsignedRightShift
        "Abs",																		// NI_AdvSimd_Abs
        "AbsSaturate",																// NI_AdvSimd_AbsSaturate
        "AbsScalar",																// NI_AdvSimd_AbsScalar
        "AbsoluteCompareGreaterThan",												// NI_AdvSimd_AbsoluteCompareGreaterThan
        "AbsoluteCompareGreaterThanOrEqual",										// NI_AdvSimd_AbsoluteCompareGreaterThanOrEqual
        "AbsoluteCompareLessThan",													// NI_AdvSimd_AbsoluteCompareLessThan
        "AbsoluteCompareLessThanOrEqual",											// NI_AdvSimd_AbsoluteCompareLessThanOrEqual
        "AbsoluteDifference",														// NI_AdvSimd_AbsoluteDifference
        "AbsoluteDifferenceAdd",													// NI_AdvSimd_AbsoluteDifferenceAdd
        "AbsoluteDifferenceWideningLower",											// NI_AdvSimd_AbsoluteDifferenceWideningLower
        "AbsoluteDifferenceWideningLowerAndAdd",									// NI_AdvSimd_AbsoluteDifferenceWideningLowerAndAdd
        "AbsoluteDifferenceWideningUpper",											// NI_AdvSimd_AbsoluteDifferenceWideningUpper
        "AbsoluteDifferenceWideningUpperAndAdd",									// NI_AdvSimd_AbsoluteDifferenceWideningUpperAndAdd
        "Add",																		// NI_AdvSimd_Add
        "AddHighNarrowingLower",													// NI_AdvSimd_AddHighNarrowingLower
        "AddHighNarrowingUpper",													// NI_AdvSimd_AddHighNarrowingUpper
        "AddPairwise",																// NI_AdvSimd_AddPairwise
        "AddPairwiseWidening",														// NI_AdvSimd_AddPairwiseWidening
        "AddPairwiseWideningAndAdd",												// NI_AdvSimd_AddPairwiseWideningAndAdd
        "AddPairwiseWideningAndAddScalar",											// NI_AdvSimd_AddPairwiseWideningAndAddScalar
        "AddPairwiseWideningScalar",												// NI_AdvSimd_AddPairwiseWideningScalar
        "AddRoundedHighNarrowingLower",												// NI_AdvSimd_AddRoundedHighNarrowingLower
        "AddRoundedHighNarrowingUpper",												// NI_AdvSimd_AddRoundedHighNarrowingUpper
        "AddSaturate",																// NI_AdvSimd_AddSaturate
        "AddSaturateScalar",														// NI_AdvSimd_AddSaturateScalar
        "AddScalar",																// NI_AdvSimd_AddScalar
        "AddWideningLower",															// NI_AdvSimd_AddWideningLower
        "AddWideningUpper",															// NI_AdvSimd_AddWideningUpper
        "And",																		// NI_AdvSimd_And
        "BitwiseClear",																// NI_AdvSimd_BitwiseClear
        "BitwiseSelect",															// NI_AdvSimd_BitwiseSelect
        "Ceiling",																	// NI_AdvSimd_Ceiling
        "CeilingScalar",															// NI_AdvSimd_CeilingScalar
        "CompareEqual",																// NI_AdvSimd_CompareEqual
        "CompareGreaterThan",														// NI_AdvSimd_CompareGreaterThan
        "CompareGreaterThanOrEqual",												// NI_AdvSimd_CompareGreaterThanOrEqual
        "CompareLessThan",															// NI_AdvSimd_CompareLessThan
        "CompareLessThanOrEqual",													// NI_AdvSimd_CompareLessThanOrEqual
        "CompareTest",																// NI_AdvSimd_CompareTest
        "ConvertToInt32RoundAwayFromZero",											// NI_AdvSimd_ConvertToInt32RoundAwayFromZero
        "ConvertToInt32RoundAwayFromZeroScalar",									// NI_AdvSimd_ConvertToInt32RoundAwayFromZeroScalar
        "ConvertToInt32RoundToEven",												// NI_AdvSimd_ConvertToInt32RoundToEven
        "ConvertToInt32RoundToEvenScalar",											// NI_AdvSimd_ConvertToInt32RoundToEvenScalar
        "ConvertToInt32RoundToNegativeInfinity",									// NI_AdvSimd_ConvertToInt32RoundToNegativeInfinity
        "ConvertToInt32RoundToNegativeInfinityScalar",								// NI_AdvSimd_ConvertToInt32RoundToNegativeInfinityScalar
        "ConvertToInt32RoundToPositiveInfinity",									// NI_AdvSimd_ConvertToInt32RoundToPositiveInfinity
        "ConvertToInt32RoundToPositiveInfinityScalar",								// NI_AdvSimd_ConvertToInt32RoundToPositiveInfinityScalar
        "ConvertToInt32RoundToZero",												// NI_AdvSimd_ConvertToInt32RoundToZero
        "ConvertToInt32RoundToZeroScalar",											// NI_AdvSimd_ConvertToInt32RoundToZeroScalar
        "ConvertToSingle",															// NI_AdvSimd_ConvertToSingle
        "ConvertToSingleScalar",													// NI_AdvSimd_ConvertToSingleScalar
        "ConvertToUInt32RoundAwayFromZero",											// NI_AdvSimd_ConvertToUInt32RoundAwayFromZero
        "ConvertToUInt32RoundAwayFromZeroScalar",									// NI_AdvSimd_ConvertToUInt32RoundAwayFromZeroScalar
        "ConvertToUInt32RoundToEven",												// NI_AdvSimd_ConvertToUInt32RoundToEven
        "ConvertToUInt32RoundToEvenScalar",											// NI_AdvSimd_ConvertToUInt32RoundToEvenScalar
        "ConvertToUInt32RoundToNegativeInfinity",									// NI_AdvSimd_ConvertToUInt32RoundToNegativeInfinity
        "ConvertToUInt32RoundToNegativeInfinityScalar",								// NI_AdvSimd_ConvertToUInt32RoundToNegativeInfinityScalar
        "ConvertToUInt32RoundToPositiveInfinity",									// NI_AdvSimd_ConvertToUInt32RoundToPositiveInfinity
        "ConvertToUInt32RoundToPositiveInfinityScalar",								// NI_AdvSimd_ConvertToUInt32RoundToPositiveInfinityScalar
        "ConvertToUInt32RoundToZero",												// NI_AdvSimd_ConvertToUInt32RoundToZero
        "ConvertToUInt32RoundToZeroScalar",											// NI_AdvSimd_ConvertToUInt32RoundToZeroScalar
        "DivideScalar",																// NI_AdvSimd_DivideScalar
        "DuplicateSelectedScalarToVector128",										// NI_AdvSimd_DuplicateSelectedScalarToVector128
        "DuplicateSelectedScalarToVector64",										// NI_AdvSimd_DuplicateSelectedScalarToVector64
        "DuplicateToVector128",														// NI_AdvSimd_DuplicateToVector128
        "DuplicateToVector64",														// NI_AdvSimd_DuplicateToVector64
        "Extract",																	// NI_AdvSimd_Extract
        "ExtractNarrowingLower",													// NI_AdvSimd_ExtractNarrowingLower
        "ExtractNarrowingSaturateLower",											// NI_AdvSimd_ExtractNarrowingSaturateLower
        "ExtractNarrowingSaturateUnsignedLower",									// NI_AdvSimd_ExtractNarrowingSaturateUnsignedLower
        "ExtractNarrowingSaturateUnsignedUpper",									// NI_AdvSimd_ExtractNarrowingSaturateUnsignedUpper
        "ExtractNarrowingSaturateUpper",											// NI_AdvSimd_ExtractNarrowingSaturateUpper
        "ExtractNarrowingUpper",													// NI_AdvSimd_ExtractNarrowingUpper
        "ExtractVector128",															// NI_AdvSimd_ExtractVector128
        "ExtractVector64",															// NI_AdvSimd_ExtractVector64
        "Floor",																	// NI_AdvSimd_Floor
        "FloorScalar",																// NI_AdvSimd_FloorScalar
        "FusedAddHalving",															// NI_AdvSimd_FusedAddHalving
        "FusedAddRoundedHalving",													// NI_AdvSimd_FusedAddRoundedHalving
        "FusedMultiplyAdd",															// NI_AdvSimd_FusedMultiplyAdd
        "FusedMultiplyAddNegatedScalar",											// NI_AdvSimd_FusedMultiplyAddNegatedScalar
        "FusedMultiplyAddScalar",													// NI_AdvSimd_FusedMultiplyAddScalar
        "FusedMultiplySubtract",													// NI_AdvSimd_FusedMultiplySubtract
        "FusedMultiplySubtractNegatedScalar",										// NI_AdvSimd_FusedMultiplySubtractNegatedScalar
        "FusedMultiplySubtractScalar",												// NI_AdvSimd_FusedMultiplySubtractScalar
        "FusedSubtractHalving",														// NI_AdvSimd_FusedSubtractHalving
        "Insert",																	// NI_AdvSimd_Insert
        "InsertScalar",																// NI_AdvSimd_InsertScalar
        "LeadingSignCount",															// NI_AdvSimd_LeadingSignCount
        "LeadingZeroCount",															// NI_AdvSimd_LeadingZeroCount
        "Load2xVector64",															// NI_AdvSimd_Load2xVector64
        "Load2xVector64AndUnzip",													// NI_AdvSimd_Load2xVector64AndUnzip
        "Load3xVector64",															// NI_AdvSimd_Load3xVector64
        "Load3xVector64AndUnzip",													// NI_AdvSimd_Load3xVector64AndUnzip
        "Load4xVector64",															// NI_AdvSimd_Load4xVector64
        "Load4xVector64AndUnzip",													// NI_AdvSimd_Load4xVector64AndUnzip
        "LoadAndInsertScalar",														// NI_AdvSimd_LoadAndInsertScalar
        "LoadAndInsertScalarVector64x2",											// NI_AdvSimd_LoadAndInsertScalarVector64x2
        "LoadAndInsertScalarVector64x3",											// NI_AdvSimd_LoadAndInsertScalarVector64x3
        "LoadAndInsertScalarVector64x4",											// NI_AdvSimd_LoadAndInsertScalarVector64x4
        "LoadAndReplicateToVector128",												// NI_AdvSimd_LoadAndReplicateToVector128
        "LoadAndReplicateToVector64",												// NI_AdvSimd_LoadAndReplicateToVector64
        "LoadAndReplicateToVector64x2",												// NI_AdvSimd_LoadAndReplicateToVector64x2
        "LoadAndReplicateToVector64x3",												// NI_AdvSimd_LoadAndReplicateToVector64x3
        "LoadAndReplicateToVector64x4",												// NI_AdvSimd_LoadAndReplicateToVector64x4
        "LoadVector128",															// NI_AdvSimd_LoadVector128
        "LoadVector64",																// NI_AdvSimd_LoadVector64
        "Max",																		// NI_AdvSimd_Max
        "MaxNumber",																// NI_AdvSimd_MaxNumber
        "MaxNumberScalar",															// NI_AdvSimd_MaxNumberScalar
        "MaxPairwise",																// NI_AdvSimd_MaxPairwise
        "Min",																		// NI_AdvSimd_Min
        "MinNumber",																// NI_AdvSimd_MinNumber
        "MinNumberScalar",															// NI_AdvSimd_MinNumberScalar
        "MinPairwise",																// NI_AdvSimd_MinPairwise
        "Multiply",																	// NI_AdvSimd_Multiply
        "MultiplyAdd",																// NI_AdvSimd_MultiplyAdd
        "MultiplyAddByScalar",														// NI_AdvSimd_MultiplyAddByScalar
        "MultiplyAddBySelectedScalar",												// NI_AdvSimd_MultiplyAddBySelectedScalar
        "MultiplyByScalar",															// NI_AdvSimd_MultiplyByScalar
        "MultiplyBySelectedScalar",													// NI_AdvSimd_MultiplyBySelectedScalar
        "MultiplyBySelectedScalarWideningLower",									// NI_AdvSimd_MultiplyBySelectedScalarWideningLower
        "MultiplyBySelectedScalarWideningLowerAndAdd",								// NI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndAdd
        "MultiplyBySelectedScalarWideningLowerAndSubtract",							// NI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndSubtract
        "MultiplyBySelectedScalarWideningUpper",									// NI_AdvSimd_MultiplyBySelectedScalarWideningUpper
        "MultiplyBySelectedScalarWideningUpperAndAdd",								// NI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndAdd
        "MultiplyBySelectedScalarWideningUpperAndSubtract",							// NI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndSubtract
        "MultiplyDoublingByScalarSaturateHigh",										// NI_AdvSimd_MultiplyDoublingByScalarSaturateHigh
        "MultiplyDoublingBySelectedScalarSaturateHigh",								// NI_AdvSimd_MultiplyDoublingBySelectedScalarSaturateHigh
        "MultiplyDoublingSaturateHigh",												// NI_AdvSimd_MultiplyDoublingSaturateHigh
        "MultiplyDoublingWideningLowerAndAddSaturate",								// NI_AdvSimd_MultiplyDoublingWideningLowerAndAddSaturate
        "MultiplyDoublingWideningLowerAndSubtractSaturate",							// NI_AdvSimd_MultiplyDoublingWideningLowerAndSubtractSaturate
        "MultiplyDoublingWideningLowerByScalarAndAddSaturate",						// NI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndAddSaturate
        "MultiplyDoublingWideningLowerByScalarAndSubtractSaturate",					// NI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndSubtractSaturate
        "MultiplyDoublingWideningLowerBySelectedScalarAndAddSaturate",				// NI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndAddSaturate
        "MultiplyDoublingWideningLowerBySelectedScalarAndSubtractSaturate",			// NI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndSubtractSaturate
        "MultiplyDoublingWideningSaturateLower",									// NI_AdvSimd_MultiplyDoublingWideningSaturateLower
        "MultiplyDoublingWideningSaturateLowerByScalar",							// NI_AdvSimd_MultiplyDoublingWideningSaturateLowerByScalar
        "MultiplyDoublingWideningSaturateLowerBySelectedScalar",					// NI_AdvSimd_MultiplyDoublingWideningSaturateLowerBySelectedScalar
        "MultiplyDoublingWideningSaturateUpper",									// NI_AdvSimd_MultiplyDoublingWideningSaturateUpper
        "MultiplyDoublingWideningSaturateUpperByScalar",							// NI_AdvSimd_MultiplyDoublingWideningSaturateUpperByScalar
        "MultiplyDoublingWideningSaturateUpperBySelectedScalar",					// NI_AdvSimd_MultiplyDoublingWideningSaturateUpperBySelectedScalar
        "MultiplyDoublingWideningUpperAndAddSaturate",								// NI_AdvSimd_MultiplyDoublingWideningUpperAndAddSaturate
        "MultiplyDoublingWideningUpperAndSubtractSaturate",							// NI_AdvSimd_MultiplyDoublingWideningUpperAndSubtractSaturate
        "MultiplyDoublingWideningUpperByScalarAndAddSaturate",						// NI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndAddSaturate
        "MultiplyDoublingWideningUpperByScalarAndSubtractSaturate",					// NI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndSubtractSaturate
        "MultiplyDoublingWideningUpperBySelectedScalarAndAddSaturate",				// NI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndAddSaturate
        "MultiplyDoublingWideningUpperBySelectedScalarAndSubtractSaturate",			// NI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndSubtractSaturate
        "MultiplyRoundedDoublingByScalarSaturateHigh",								// NI_AdvSimd_MultiplyRoundedDoublingByScalarSaturateHigh
        "MultiplyRoundedDoublingBySelectedScalarSaturateHigh",						// NI_AdvSimd_MultiplyRoundedDoublingBySelectedScalarSaturateHigh
        "MultiplyRoundedDoublingSaturateHigh",										// NI_AdvSimd_MultiplyRoundedDoublingSaturateHigh
        "MultiplyScalar",															// NI_AdvSimd_MultiplyScalar
        "MultiplyScalarBySelectedScalar",											// NI_AdvSimd_MultiplyScalarBySelectedScalar
        "MultiplySubtract",															// NI_AdvSimd_MultiplySubtract
        "MultiplySubtractByScalar",													// NI_AdvSimd_MultiplySubtractByScalar
        "MultiplySubtractBySelectedScalar",											// NI_AdvSimd_MultiplySubtractBySelectedScalar
        "MultiplyWideningLower",													// NI_AdvSimd_MultiplyWideningLower
        "MultiplyWideningLowerAndAdd",												// NI_AdvSimd_MultiplyWideningLowerAndAdd
        "MultiplyWideningLowerAndSubtract",											// NI_AdvSimd_MultiplyWideningLowerAndSubtract
        "MultiplyWideningUpper",													// NI_AdvSimd_MultiplyWideningUpper
        "MultiplyWideningUpperAndAdd",												// NI_AdvSimd_MultiplyWideningUpperAndAdd
        "MultiplyWideningUpperAndSubtract",											// NI_AdvSimd_MultiplyWideningUpperAndSubtract
        "Negate",																	// NI_AdvSimd_Negate
        "NegateSaturate",															// NI_AdvSimd_NegateSaturate
        "NegateScalar",																// NI_AdvSimd_NegateScalar
        "Not",																		// NI_AdvSimd_Not
        "Or",																		// NI_AdvSimd_Or
        "OrNot",																	// NI_AdvSimd_OrNot
        "PolynomialMultiply",														// NI_AdvSimd_PolynomialMultiply
        "PolynomialMultiplyWideningLower",											// NI_AdvSimd_PolynomialMultiplyWideningLower
        "PolynomialMultiplyWideningUpper",											// NI_AdvSimd_PolynomialMultiplyWideningUpper
        "PopCount",																	// NI_AdvSimd_PopCount
        "ReciprocalEstimate",														// NI_AdvSimd_ReciprocalEstimate
        "ReciprocalSquareRootEstimate",												// NI_AdvSimd_ReciprocalSquareRootEstimate
        "ReciprocalSquareRootStep",													// NI_AdvSimd_ReciprocalSquareRootStep
        "ReciprocalStep",															// NI_AdvSimd_ReciprocalStep
        "ReverseElement16",															// NI_AdvSimd_ReverseElement16
        "ReverseElement32",															// NI_AdvSimd_ReverseElement32
        "ReverseElement8",															// NI_AdvSimd_ReverseElement8
        "RoundAwayFromZero",														// NI_AdvSimd_RoundAwayFromZero
        "RoundAwayFromZeroScalar",													// NI_AdvSimd_RoundAwayFromZeroScalar
        "RoundToNearest",															// NI_AdvSimd_RoundToNearest
        "RoundToNearestScalar",														// NI_AdvSimd_RoundToNearestScalar
        "RoundToNegativeInfinity",													// NI_AdvSimd_RoundToNegativeInfinity
        "RoundToNegativeInfinityScalar",											// NI_AdvSimd_RoundToNegativeInfinityScalar
        "RoundToPositiveInfinity",													// NI_AdvSimd_RoundToPositiveInfinity
        "RoundToPositiveInfinityScalar",											// NI_AdvSimd_RoundToPositiveInfinityScalar
        "RoundToZero",																// NI_AdvSimd_RoundToZero
        "RoundToZeroScalar",														// NI_AdvSimd_RoundToZeroScalar
        "ShiftArithmetic",															// NI_AdvSimd_ShiftArithmetic
        "ShiftArithmeticRounded",													// NI_AdvSimd_ShiftArithmeticRounded
        "ShiftArithmeticRoundedSaturate",											// NI_AdvSimd_ShiftArithmeticRoundedSaturate
        "ShiftArithmeticRoundedSaturateScalar",										// NI_AdvSimd_ShiftArithmeticRoundedSaturateScalar
        "ShiftArithmeticRoundedScalar",												// NI_AdvSimd_ShiftArithmeticRoundedScalar
        "ShiftArithmeticSaturate",													// NI_AdvSimd_ShiftArithmeticSaturate
        "ShiftArithmeticSaturateScalar",											// NI_AdvSimd_ShiftArithmeticSaturateScalar
        "ShiftArithmeticScalar",													// NI_AdvSimd_ShiftArithmeticScalar
        "ShiftLeftAndInsert",														// NI_AdvSimd_ShiftLeftAndInsert
        "ShiftLeftAndInsertScalar",													// NI_AdvSimd_ShiftLeftAndInsertScalar
        "ShiftLeftLogical",															// NI_AdvSimd_ShiftLeftLogical
        "ShiftLeftLogicalSaturate",													// NI_AdvSimd_ShiftLeftLogicalSaturate
        "ShiftLeftLogicalSaturateScalar",											// NI_AdvSimd_ShiftLeftLogicalSaturateScalar
        "ShiftLeftLogicalSaturateUnsigned",											// NI_AdvSimd_ShiftLeftLogicalSaturateUnsigned
        "ShiftLeftLogicalSaturateUnsignedScalar",									// NI_AdvSimd_ShiftLeftLogicalSaturateUnsignedScalar
        "ShiftLeftLogicalScalar",													// NI_AdvSimd_ShiftLeftLogicalScalar
        "ShiftLeftLogicalWideningLower",											// NI_AdvSimd_ShiftLeftLogicalWideningLower
        "ShiftLeftLogicalWideningUpper",											// NI_AdvSimd_ShiftLeftLogicalWideningUpper
        "ShiftLogical",																// NI_AdvSimd_ShiftLogical
        "ShiftLogicalRounded",														// NI_AdvSimd_ShiftLogicalRounded
        "ShiftLogicalRoundedSaturate",												// NI_AdvSimd_ShiftLogicalRoundedSaturate
        "ShiftLogicalRoundedSaturateScalar",										// NI_AdvSimd_ShiftLogicalRoundedSaturateScalar
        "ShiftLogicalRoundedScalar",												// NI_AdvSimd_ShiftLogicalRoundedScalar
        "ShiftLogicalSaturate",														// NI_AdvSimd_ShiftLogicalSaturate
        "ShiftLogicalSaturateScalar",												// NI_AdvSimd_ShiftLogicalSaturateScalar
        "ShiftLogicalScalar",														// NI_AdvSimd_ShiftLogicalScalar
        "ShiftRightAndInsert",														// NI_AdvSimd_ShiftRightAndInsert
        "ShiftRightAndInsertScalar",												// NI_AdvSimd_ShiftRightAndInsertScalar
        "ShiftRightArithmetic",														// NI_AdvSimd_ShiftRightArithmetic
        "ShiftRightArithmeticAdd",													// NI_AdvSimd_ShiftRightArithmeticAdd
        "ShiftRightArithmeticAddScalar",											// NI_AdvSimd_ShiftRightArithmeticAddScalar
        "ShiftRightArithmeticNarrowingSaturateLower",								// NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateLower
        "ShiftRightArithmeticNarrowingSaturateUnsignedLower",						// NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedLower
        "ShiftRightArithmeticNarrowingSaturateUnsignedUpper",						// NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedUpper
        "ShiftRightArithmeticNarrowingSaturateUpper",								// NI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUpper
        "ShiftRightArithmeticRounded",												// NI_AdvSimd_ShiftRightArithmeticRounded
        "ShiftRightArithmeticRoundedAdd",											// NI_AdvSimd_ShiftRightArithmeticRoundedAdd
        "ShiftRightArithmeticRoundedAddScalar",										// NI_AdvSimd_ShiftRightArithmeticRoundedAddScalar
        "ShiftRightArithmeticRoundedNarrowingSaturateLower",						// NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateLower
        "ShiftRightArithmeticRoundedNarrowingSaturateUnsignedLower",				// NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedLower
        "ShiftRightArithmeticRoundedNarrowingSaturateUnsignedUpper",				// NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedUpper
        "ShiftRightArithmeticRoundedNarrowingSaturateUpper",						// NI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUpper
        "ShiftRightArithmeticRoundedScalar",										// NI_AdvSimd_ShiftRightArithmeticRoundedScalar
        "ShiftRightArithmeticScalar",												// NI_AdvSimd_ShiftRightArithmeticScalar
        "ShiftRightLogical",														// NI_AdvSimd_ShiftRightLogical
        "ShiftRightLogicalAdd",														// NI_AdvSimd_ShiftRightLogicalAdd
        "ShiftRightLogicalAddScalar",												// NI_AdvSimd_ShiftRightLogicalAddScalar
        "ShiftRightLogicalNarrowingLower",											// NI_AdvSimd_ShiftRightLogicalNarrowingLower
        "ShiftRightLogicalNarrowingSaturateLower",									// NI_AdvSimd_ShiftRightLogicalNarrowingSaturateLower
        "ShiftRightLogicalNarrowingSaturateUpper",									// NI_AdvSimd_ShiftRightLogicalNarrowingSaturateUpper
        "ShiftRightLogicalNarrowingUpper",											// NI_AdvSimd_ShiftRightLogicalNarrowingUpper
        "ShiftRightLogicalRounded",													// NI_AdvSimd_ShiftRightLogicalRounded
        "ShiftRightLogicalRoundedAdd",												// NI_AdvSimd_ShiftRightLogicalRoundedAdd
        "ShiftRightLogicalRoundedAddScalar",										// NI_AdvSimd_ShiftRightLogicalRoundedAddScalar
        "ShiftRightLogicalRoundedNarrowingLower",									// NI_AdvSimd_ShiftRightLogicalRoundedNarrowingLower
        "ShiftRightLogicalRoundedNarrowingSaturateLower",							// NI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateLower
        "ShiftRightLogicalRoundedNarrowingSaturateUpper",							// NI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateUpper
        "ShiftRightLogicalRoundedNarrowingUpper",									// NI_AdvSimd_ShiftRightLogicalRoundedNarrowingUpper
        "ShiftRightLogicalRoundedScalar",											// NI_AdvSimd_ShiftRightLogicalRoundedScalar
        "ShiftRightLogicalScalar",													// NI_AdvSimd_ShiftRightLogicalScalar
        "SignExtendWideningLower",													// NI_AdvSimd_SignExtendWideningLower
        "SignExtendWideningUpper",													// NI_AdvSimd_SignExtendWideningUpper
        "SqrtScalar",																// NI_AdvSimd_SqrtScalar
        "Store",																	// NI_AdvSimd_Store
        "StoreSelectedScalar",														// NI_AdvSimd_StoreSelectedScalar
        "StoreVectorAndZip",														// NI_AdvSimd_StoreVectorAndZip
        "Subtract",																	// NI_AdvSimd_Subtract
        "SubtractHighNarrowingLower",												// NI_AdvSimd_SubtractHighNarrowingLower
        "SubtractHighNarrowingUpper",												// NI_AdvSimd_SubtractHighNarrowingUpper
        "SubtractRoundedHighNarrowingLower",										// NI_AdvSimd_SubtractRoundedHighNarrowingLower
        "SubtractRoundedHighNarrowingUpper",										// NI_AdvSimd_SubtractRoundedHighNarrowingUpper
        "SubtractSaturate",															// NI_AdvSimd_SubtractSaturate
        "SubtractSaturateScalar",													// NI_AdvSimd_SubtractSaturateScalar
        "SubtractScalar",															// NI_AdvSimd_SubtractScalar
        "SubtractWideningLower",													// NI_AdvSimd_SubtractWideningLower
        "SubtractWideningUpper",													// NI_AdvSimd_SubtractWideningUpper
        "VectorTableLookup",														// NI_AdvSimd_VectorTableLookup
        "VectorTableLookupExtension",												// NI_AdvSimd_VectorTableLookupExtension
        "Xor",																		// NI_AdvSimd_Xor
        "ZeroExtendWideningLower",													// NI_AdvSimd_ZeroExtendWideningLower
        "ZeroExtendWideningUpper",													// NI_AdvSimd_ZeroExtendWideningUpper
        "Abs",																		// NI_AdvSimd_Arm64_Abs
        "AbsSaturate",																// NI_AdvSimd_Arm64_AbsSaturate
        "AbsSaturateScalar",														// NI_AdvSimd_Arm64_AbsSaturateScalar
        "AbsScalar",																// NI_AdvSimd_Arm64_AbsScalar
        "AbsoluteCompareGreaterThan",												// NI_AdvSimd_Arm64_AbsoluteCompareGreaterThan
        "AbsoluteCompareGreaterThanOrEqual",										// NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqual
        "AbsoluteCompareGreaterThanOrEqualScalar",									// NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqualScalar
        "AbsoluteCompareGreaterThanScalar",											// NI_AdvSimd_Arm64_AbsoluteCompareGreaterThanScalar
        "AbsoluteCompareLessThan",													// NI_AdvSimd_Arm64_AbsoluteCompareLessThan
        "AbsoluteCompareLessThanOrEqual",											// NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqual
        "AbsoluteCompareLessThanOrEqualScalar",										// NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqualScalar
        "AbsoluteCompareLessThanScalar",											// NI_AdvSimd_Arm64_AbsoluteCompareLessThanScalar
        "AbsoluteDifference",														// NI_AdvSimd_Arm64_AbsoluteDifference
        "AbsoluteDifferenceScalar",													// NI_AdvSimd_Arm64_AbsoluteDifferenceScalar
        "Add",																		// NI_AdvSimd_Arm64_Add
        "AddAcross",																// NI_AdvSimd_Arm64_AddAcross
        "AddAcrossWidening",														// NI_AdvSimd_Arm64_AddAcrossWidening
        "AddPairwise",																// NI_AdvSimd_Arm64_AddPairwise
        "AddPairwiseScalar",														// NI_AdvSimd_Arm64_AddPairwiseScalar
        "AddSaturate",																// NI_AdvSimd_Arm64_AddSaturate
        "AddSaturateScalar",														// NI_AdvSimd_Arm64_AddSaturateScalar
        "Ceiling",																	// NI_AdvSimd_Arm64_Ceiling
        "CompareEqual",																// NI_AdvSimd_Arm64_CompareEqual
        "CompareEqualScalar",														// NI_AdvSimd_Arm64_CompareEqualScalar
        "CompareGreaterThan",														// NI_AdvSimd_Arm64_CompareGreaterThan
        "CompareGreaterThanOrEqual",												// NI_AdvSimd_Arm64_CompareGreaterThanOrEqual
        "CompareGreaterThanOrEqualScalar",											// NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar
        "CompareGreaterThanScalar",													// NI_AdvSimd_Arm64_CompareGreaterThanScalar
        "CompareLessThan",															// NI_AdvSimd_Arm64_CompareLessThan
        "CompareLessThanOrEqual",													// NI_AdvSimd_Arm64_CompareLessThanOrEqual
        "CompareLessThanOrEqualScalar",												// NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar
        "CompareLessThanScalar",													// NI_AdvSimd_Arm64_CompareLessThanScalar
        "CompareTest",																// NI_AdvSimd_Arm64_CompareTest
        "CompareTestScalar",														// NI_AdvSimd_Arm64_CompareTestScalar
        "ConvertToDouble",															// NI_AdvSimd_Arm64_ConvertToDouble
        "ConvertToDoubleScalar",													// NI_AdvSimd_Arm64_ConvertToDoubleScalar
        "ConvertToDoubleUpper",														// NI_AdvSimd_Arm64_ConvertToDoubleUpper
        "ConvertToInt64RoundAwayFromZero",											// NI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZero
        "ConvertToInt64RoundAwayFromZeroScalar",									// NI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZeroScalar
        "ConvertToInt64RoundToEven",												// NI_AdvSimd_Arm64_ConvertToInt64RoundToEven
        "ConvertToInt64RoundToEvenScalar",											// NI_AdvSimd_Arm64_ConvertToInt64RoundToEvenScalar
        "ConvertToInt64RoundToNegativeInfinity",									// NI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinity
        "ConvertToInt64RoundToNegativeInfinityScalar",								// NI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinityScalar
        "ConvertToInt64RoundToPositiveInfinity",									// NI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinity
        "ConvertToInt64RoundToPositiveInfinityScalar",								// NI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinityScalar
        "ConvertToInt64RoundToZero",												// NI_AdvSimd_Arm64_ConvertToInt64RoundToZero
        "ConvertToInt64RoundToZeroScalar",											// NI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar
        "ConvertToSingleLower",														// NI_AdvSimd_Arm64_ConvertToSingleLower
        "ConvertToSingleRoundToOddLower",											// NI_AdvSimd_Arm64_ConvertToSingleRoundToOddLower
        "ConvertToSingleRoundToOddUpper",											// NI_AdvSimd_Arm64_ConvertToSingleRoundToOddUpper
        "ConvertToSingleUpper",														// NI_AdvSimd_Arm64_ConvertToSingleUpper
        "ConvertToUInt64RoundAwayFromZero",											// NI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZero
        "ConvertToUInt64RoundAwayFromZeroScalar",									// NI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZeroScalar
        "ConvertToUInt64RoundToEven",												// NI_AdvSimd_Arm64_ConvertToUInt64RoundToEven
        "ConvertToUInt64RoundToEvenScalar",											// NI_AdvSimd_Arm64_ConvertToUInt64RoundToEvenScalar
        "ConvertToUInt64RoundToNegativeInfinity",									// NI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinity
        "ConvertToUInt64RoundToNegativeInfinityScalar",								// NI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinityScalar
        "ConvertToUInt64RoundToPositiveInfinity",									// NI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinity
        "ConvertToUInt64RoundToPositiveInfinityScalar",								// NI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinityScalar
        "ConvertToUInt64RoundToZero",												// NI_AdvSimd_Arm64_ConvertToUInt64RoundToZero
        "ConvertToUInt64RoundToZeroScalar",											// NI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar
        "Divide",																	// NI_AdvSimd_Arm64_Divide
        "DuplicateSelectedScalarToVector128",										// NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128
        "DuplicateToVector128",														// NI_AdvSimd_Arm64_DuplicateToVector128
        "DuplicateToVector64",														// NI_AdvSimd_Arm64_DuplicateToVector64
        "ExtractNarrowingSaturateScalar",											// NI_AdvSimd_Arm64_ExtractNarrowingSaturateScalar
        "ExtractNarrowingSaturateUnsignedScalar",									// NI_AdvSimd_Arm64_ExtractNarrowingSaturateUnsignedScalar
        "Floor",																	// NI_AdvSimd_Arm64_Floor
        "FusedMultiplyAdd",															// NI_AdvSimd_Arm64_FusedMultiplyAdd
        "FusedMultiplyAddByScalar",													// NI_AdvSimd_Arm64_FusedMultiplyAddByScalar
        "FusedMultiplyAddBySelectedScalar",											// NI_AdvSimd_Arm64_FusedMultiplyAddBySelectedScalar
        "FusedMultiplyAddScalarBySelectedScalar",									// NI_AdvSimd_Arm64_FusedMultiplyAddScalarBySelectedScalar
        "FusedMultiplySubtract",													// NI_AdvSimd_Arm64_FusedMultiplySubtract
        "FusedMultiplySubtractByScalar",											// NI_AdvSimd_Arm64_FusedMultiplySubtractByScalar
        "FusedMultiplySubtractBySelectedScalar",									// NI_AdvSimd_Arm64_FusedMultiplySubtractBySelectedScalar
        "FusedMultiplySubtractScalarBySelectedScalar",								// NI_AdvSimd_Arm64_FusedMultiplySubtractScalarBySelectedScalar
        "InsertSelectedScalar",														// NI_AdvSimd_Arm64_InsertSelectedScalar
        "Load2xVector128",															// NI_AdvSimd_Arm64_Load2xVector128
        "Load2xVector128AndUnzip",													// NI_AdvSimd_Arm64_Load2xVector128AndUnzip
        "Load3xVector128",															// NI_AdvSimd_Arm64_Load3xVector128
        "Load3xVector128AndUnzip",													// NI_AdvSimd_Arm64_Load3xVector128AndUnzip
        "Load4xVector128",															// NI_AdvSimd_Arm64_Load4xVector128
        "Load4xVector128AndUnzip",													// NI_AdvSimd_Arm64_Load4xVector128AndUnzip
        "LoadAndInsertScalar",														// NI_AdvSimd_Arm64_LoadAndInsertScalar
        "LoadAndInsertScalarVector128x2",											// NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2
        "LoadAndInsertScalarVector128x3",											// NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3
        "LoadAndInsertScalarVector128x4",											// NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4
        "LoadAndReplicateToVector128",												// NI_AdvSimd_Arm64_LoadAndReplicateToVector128
        "LoadAndReplicateToVector128x2",											// NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2
        "LoadAndReplicateToVector128x3",											// NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3
        "LoadAndReplicateToVector128x4",											// NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4
        "LoadPairScalarVector64",													// NI_AdvSimd_Arm64_LoadPairScalarVector64
        "LoadPairScalarVector64NonTemporal",										// NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal
        "LoadPairVector128",														// NI_AdvSimd_Arm64_LoadPairVector128
        "LoadPairVector128NonTemporal",												// NI_AdvSimd_Arm64_LoadPairVector128NonTemporal
        "LoadPairVector64",															// NI_AdvSimd_Arm64_LoadPairVector64
        "LoadPairVector64NonTemporal",												// NI_AdvSimd_Arm64_LoadPairVector64NonTemporal
        "Max",																		// NI_AdvSimd_Arm64_Max
        "MaxAcross",																// NI_AdvSimd_Arm64_MaxAcross
        "MaxNumber",																// NI_AdvSimd_Arm64_MaxNumber
        "MaxNumberAcross",															// NI_AdvSimd_Arm64_MaxNumberAcross
        "MaxNumberPairwise",														// NI_AdvSimd_Arm64_MaxNumberPairwise
        "MaxNumberPairwiseScalar",													// NI_AdvSimd_Arm64_MaxNumberPairwiseScalar
        "MaxPairwise",																// NI_AdvSimd_Arm64_MaxPairwise
        "MaxPairwiseScalar",														// NI_AdvSimd_Arm64_MaxPairwiseScalar
        "MaxScalar",																// NI_AdvSimd_Arm64_MaxScalar
        "Min",																		// NI_AdvSimd_Arm64_Min
        "MinAcross",																// NI_AdvSimd_Arm64_MinAcross
        "MinNumber",																// NI_AdvSimd_Arm64_MinNumber
        "MinNumberAcross",															// NI_AdvSimd_Arm64_MinNumberAcross
        "MinNumberPairwise",														// NI_AdvSimd_Arm64_MinNumberPairwise
        "MinNumberPairwiseScalar",													// NI_AdvSimd_Arm64_MinNumberPairwiseScalar
        "MinPairwise",																// NI_AdvSimd_Arm64_MinPairwise
        "MinPairwiseScalar",														// NI_AdvSimd_Arm64_MinPairwiseScalar
        "MinScalar",																// NI_AdvSimd_Arm64_MinScalar
        "Multiply",																	// NI_AdvSimd_Arm64_Multiply
        "MultiplyByScalar",															// NI_AdvSimd_Arm64_MultiplyByScalar
        "MultiplyBySelectedScalar",													// NI_AdvSimd_Arm64_MultiplyBySelectedScalar
        "MultiplyDoublingSaturateHighScalar",										// NI_AdvSimd_Arm64_MultiplyDoublingSaturateHighScalar
        "MultiplyDoublingScalarBySelectedScalarSaturateHigh",						// NI_AdvSimd_Arm64_MultiplyDoublingScalarBySelectedScalarSaturateHigh
        "MultiplyDoublingWideningAndAddSaturateScalar",								// NI_AdvSimd_Arm64_MultiplyDoublingWideningAndAddSaturateScalar
        "MultiplyDoublingWideningAndSubtractSaturateScalar",						// NI_AdvSimd_Arm64_MultiplyDoublingWideningAndSubtractSaturateScalar
        "MultiplyDoublingWideningSaturateScalar",									// NI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalar
        "MultiplyDoublingWideningSaturateScalarBySelectedScalar",					// NI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalarBySelectedScalar
        "MultiplyDoublingWideningScalarBySelectedScalarAndAddSaturate",				// NI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndAddSaturate
        "MultiplyDoublingWideningScalarBySelectedScalarAndSubtractSaturate",		// NI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndSubtractSaturate
        "MultiplyExtended",															// NI_AdvSimd_Arm64_MultiplyExtended
        "MultiplyExtendedByScalar",													// NI_AdvSimd_Arm64_MultiplyExtendedByScalar
        "MultiplyExtendedBySelectedScalar",											// NI_AdvSimd_Arm64_MultiplyExtendedBySelectedScalar
        "MultiplyExtendedScalar",													// NI_AdvSimd_Arm64_MultiplyExtendedScalar
        "MultiplyExtendedScalarBySelectedScalar",									// NI_AdvSimd_Arm64_MultiplyExtendedScalarBySelectedScalar
        "MultiplyRoundedDoublingSaturateHighScalar",								// NI_AdvSimd_Arm64_MultiplyRoundedDoublingSaturateHighScalar
        "MultiplyRoundedDoublingScalarBySelectedScalarSaturateHigh",				// NI_AdvSimd_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarSaturateHigh
        "MultiplyScalarBySelectedScalar",											// NI_AdvSimd_Arm64_MultiplyScalarBySelectedScalar
        "Negate",																	// NI_AdvSimd_Arm64_Negate
        "NegateSaturate",															// NI_AdvSimd_Arm64_NegateSaturate
        "NegateSaturateScalar",														// NI_AdvSimd_Arm64_NegateSaturateScalar
        "NegateScalar",																// NI_AdvSimd_Arm64_NegateScalar
        "ReciprocalEstimate",														// NI_AdvSimd_Arm64_ReciprocalEstimate
        "ReciprocalEstimateScalar",													// NI_AdvSimd_Arm64_ReciprocalEstimateScalar
        "ReciprocalExponentScalar",													// NI_AdvSimd_Arm64_ReciprocalExponentScalar
        "ReciprocalSquareRootEstimate",												// NI_AdvSimd_Arm64_ReciprocalSquareRootEstimate
        "ReciprocalSquareRootEstimateScalar",										// NI_AdvSimd_Arm64_ReciprocalSquareRootEstimateScalar
        "ReciprocalSquareRootStep",													// NI_AdvSimd_Arm64_ReciprocalSquareRootStep
        "ReciprocalSquareRootStepScalar",											// NI_AdvSimd_Arm64_ReciprocalSquareRootStepScalar
        "ReciprocalStep",															// NI_AdvSimd_Arm64_ReciprocalStep
        "ReciprocalStepScalar",														// NI_AdvSimd_Arm64_ReciprocalStepScalar
        "ReverseElementBits",														// NI_AdvSimd_Arm64_ReverseElementBits
        "RoundAwayFromZero",														// NI_AdvSimd_Arm64_RoundAwayFromZero
        "RoundToNearest",															// NI_AdvSimd_Arm64_RoundToNearest
        "RoundToNegativeInfinity",													// NI_AdvSimd_Arm64_RoundToNegativeInfinity
        "RoundToPositiveInfinity",													// NI_AdvSimd_Arm64_RoundToPositiveInfinity
        "RoundToZero",																// NI_AdvSimd_Arm64_RoundToZero
        "ShiftArithmeticRoundedSaturateScalar",										// NI_AdvSimd_Arm64_ShiftArithmeticRoundedSaturateScalar
        "ShiftArithmeticSaturateScalar",											// NI_AdvSimd_Arm64_ShiftArithmeticSaturateScalar
        "ShiftLeftLogicalSaturateScalar",											// NI_AdvSimd_Arm64_ShiftLeftLogicalSaturateScalar
        "ShiftLeftLogicalSaturateUnsignedScalar",									// NI_AdvSimd_Arm64_ShiftLeftLogicalSaturateUnsignedScalar
        "ShiftLogicalRoundedSaturateScalar",										// NI_AdvSimd_Arm64_ShiftLogicalRoundedSaturateScalar
        "ShiftLogicalSaturateScalar",												// NI_AdvSimd_Arm64_ShiftLogicalSaturateScalar
        "ShiftRightArithmeticNarrowingSaturateScalar",								// NI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateScalar
        "ShiftRightArithmeticNarrowingSaturateUnsignedScalar",						// NI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateUnsignedScalar
        "ShiftRightArithmeticRoundedNarrowingSaturateScalar",						// NI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateScalar
        "ShiftRightArithmeticRoundedNarrowingSaturateUnsignedScalar",				// NI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedScalar
        "ShiftRightLogicalNarrowingSaturateScalar",									// NI_AdvSimd_Arm64_ShiftRightLogicalNarrowingSaturateScalar
        "ShiftRightLogicalRoundedNarrowingSaturateScalar",							// NI_AdvSimd_Arm64_ShiftRightLogicalRoundedNarrowingSaturateScalar
        "Sqrt",																		// NI_AdvSimd_Arm64_Sqrt
        "Store",																	// NI_AdvSimd_Arm64_Store
        "StorePair",																// NI_AdvSimd_Arm64_StorePair
        "StorePairNonTemporal",														// NI_AdvSimd_Arm64_StorePairNonTemporal
        "StorePairScalar",															// NI_AdvSimd_Arm64_StorePairScalar
        "StorePairScalarNonTemporal",												// NI_AdvSimd_Arm64_StorePairScalarNonTemporal
        "StoreSelectedScalar",														// NI_AdvSimd_Arm64_StoreSelectedScalar
        "StoreVectorAndZip",														// NI_AdvSimd_Arm64_StoreVectorAndZip
        "Subtract",																	// NI_AdvSimd_Arm64_Subtract
        "SubtractSaturateScalar",													// NI_AdvSimd_Arm64_SubtractSaturateScalar
        "TransposeEven",															// NI_AdvSimd_Arm64_TransposeEven
        "TransposeOdd",																// NI_AdvSimd_Arm64_TransposeOdd
        "UnzipEven",																// NI_AdvSimd_Arm64_UnzipEven
        "UnzipOdd",																	// NI_AdvSimd_Arm64_UnzipOdd
        "VectorTableLookup",														// NI_AdvSimd_Arm64_VectorTableLookup
        "VectorTableLookupExtension",												// NI_AdvSimd_Arm64_VectorTableLookupExtension
        "ZipHigh",																	// NI_AdvSimd_Arm64_ZipHigh
        "ZipLow",																	// NI_AdvSimd_Arm64_ZipLow
        "Decrypt",																	// NI_Aes_Decrypt
        "Encrypt",																	// NI_Aes_Encrypt
        "InverseMixColumns",														// NI_Aes_InverseMixColumns
        "MixColumns",																// NI_Aes_MixColumns
        "PolynomialMultiplyWideningLower",											// NI_Aes_PolynomialMultiplyWideningLower
        "PolynomialMultiplyWideningUpper",											// NI_Aes_PolynomialMultiplyWideningUpper
        "LeadingZeroCount",															// NI_ArmBase_LeadingZeroCount
        "ReverseElementBits",														// NI_ArmBase_ReverseElementBits
        "Yield",																	// NI_ArmBase_Yield
        "LeadingSignCount",															// NI_ArmBase_Arm64_LeadingSignCount
        "LeadingZeroCount",															// NI_ArmBase_Arm64_LeadingZeroCount
        "MultiplyHigh",																// NI_ArmBase_Arm64_MultiplyHigh
        "MultiplyLongAdd",															// NI_ArmBase_Arm64_MultiplyLongAdd
        "MultiplyLongNeg",															// NI_ArmBase_Arm64_MultiplyLongNeg
        "MultiplyLongSub",															// NI_ArmBase_Arm64_MultiplyLongSub
        "ReverseElementBits",														// NI_ArmBase_Arm64_ReverseElementBits
        "ComputeCrc32",																// NI_Crc32_ComputeCrc32
        "ComputeCrc32C",															// NI_Crc32_ComputeCrc32C
        "ComputeCrc32",																// NI_Crc32_Arm64_ComputeCrc32
        "ComputeCrc32C",															// NI_Crc32_Arm64_ComputeCrc32C
        "DotProduct",																// NI_Dp_DotProduct
        "DotProductBySelectedQuadruplet",											// NI_Dp_DotProductBySelectedQuadruplet
        "MultiplyRoundedDoublingAndAddSaturateHigh",								// NI_Rdm_MultiplyRoundedDoublingAndAddSaturateHigh
        "MultiplyRoundedDoublingAndSubtractSaturateHigh",							// NI_Rdm_MultiplyRoundedDoublingAndSubtractSaturateHigh
        "MultiplyRoundedDoublingBySelectedScalarAndAddSaturateHigh",				// NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndAddSaturateHigh
        "MultiplyRoundedDoublingBySelectedScalarAndSubtractSaturateHigh",			// NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndSubtractSaturateHigh
        "MultiplyRoundedDoublingAndAddSaturateHighScalar",							// NI_Rdm_Arm64_MultiplyRoundedDoublingAndAddSaturateHighScalar
        "MultiplyRoundedDoublingAndSubtractSaturateHighScalar",						// NI_Rdm_Arm64_MultiplyRoundedDoublingAndSubtractSaturateHighScalar
        "MultiplyRoundedDoublingScalarBySelectedScalarAndAddSaturateHigh",			// NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndAddSaturateHigh
        "MultiplyRoundedDoublingScalarBySelectedScalarAndSubtractSaturateHigh",		// NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndSubtractSaturateHigh
        "FixedRotate",																// NI_Sha1_FixedRotate
        "HashUpdateChoose",															// NI_Sha1_HashUpdateChoose
        "HashUpdateMajority",														// NI_Sha1_HashUpdateMajority
        "HashUpdateParity",															// NI_Sha1_HashUpdateParity
        "ScheduleUpdate0",															// NI_Sha1_ScheduleUpdate0
        "ScheduleUpdate1",															// NI_Sha1_ScheduleUpdate1
        "HashUpdate1",																// NI_Sha256_HashUpdate1
        "HashUpdate2",																// NI_Sha256_HashUpdate2
        "ScheduleUpdate0",															// NI_Sha256_ScheduleUpdate0
        "ScheduleUpdate1",															// NI_Sha256_ScheduleUpdate1
        "Abs",																		// NI_Sve_Abs
        "AbsoluteCompareGreaterThan",												// NI_Sve_AbsoluteCompareGreaterThan
        "AbsoluteCompareGreaterThanOrEqual",										// NI_Sve_AbsoluteCompareGreaterThanOrEqual
        "AbsoluteCompareLessThan",													// NI_Sve_AbsoluteCompareLessThan
        "AbsoluteCompareLessThanOrEqual",											// NI_Sve_AbsoluteCompareLessThanOrEqual
        "AbsoluteDifference",														// NI_Sve_AbsoluteDifference
        "Add",																		// NI_Sve_Add
        "AddAcross",																// NI_Sve_AddAcross
        "AddRotateComplex",															// NI_Sve_AddRotateComplex
        "AddSaturate",																// NI_Sve_AddSaturate
        "AddSequentialAcross",														// NI_Sve_AddSequentialAcross
        "And",																		// NI_Sve_And
        "AndAcross",																// NI_Sve_AndAcross
        "BitwiseClear",																// NI_Sve_BitwiseClear
        "BooleanNot",																// NI_Sve_BooleanNot
        "Compact",																	// NI_Sve_Compact
        "CompareEqual",																// NI_Sve_CompareEqual
        "CompareGreaterThan",														// NI_Sve_CompareGreaterThan
        "CompareGreaterThanOrEqual",												// NI_Sve_CompareGreaterThanOrEqual
        "CompareLessThan",															// NI_Sve_CompareLessThan
        "CompareLessThanOrEqual",													// NI_Sve_CompareLessThanOrEqual
        "CompareNotEqualTo",														// NI_Sve_CompareNotEqualTo
        "CompareUnordered",															// NI_Sve_CompareUnordered
        "Compute16BitAddresses",													// NI_Sve_Compute16BitAddresses
        "Compute32BitAddresses",													// NI_Sve_Compute32BitAddresses
        "Compute64BitAddresses",													// NI_Sve_Compute64BitAddresses
        "Compute8BitAddresses",														// NI_Sve_Compute8BitAddresses
        "ConditionalExtractAfterLastActiveElement",									// NI_Sve_ConditionalExtractAfterLastActiveElement
        "ConditionalExtractAfterLastActiveElementAndReplicate",						// NI_Sve_ConditionalExtractAfterLastActiveElementAndReplicate
        "ConditionalExtractLastActiveElement",										// NI_Sve_ConditionalExtractLastActiveElement
        "ConditionalExtractLastActiveElementAndReplicate",							// NI_Sve_ConditionalExtractLastActiveElementAndReplicate
        "ConditionalSelect",														// NI_Sve_ConditionalSelect
        "ConvertToDouble",															// NI_Sve_ConvertToDouble
        "ConvertToInt32",															// NI_Sve_ConvertToInt32
        "ConvertToInt64",															// NI_Sve_ConvertToInt64
        "ConvertToSingle",															// NI_Sve_ConvertToSingle
        "ConvertToUInt32",															// NI_Sve_ConvertToUInt32
        "ConvertToUInt64",															// NI_Sve_ConvertToUInt64
        "Count16BitElements",														// NI_Sve_Count16BitElements
        "Count32BitElements",														// NI_Sve_Count32BitElements
        "Count64BitElements",														// NI_Sve_Count64BitElements
        "Count8BitElements",														// NI_Sve_Count8BitElements
        "CreateBreakAfterMask",														// NI_Sve_CreateBreakAfterMask
        "CreateBreakAfterPropagateMask",											// NI_Sve_CreateBreakAfterPropagateMask
        "CreateBreakBeforeMask",													// NI_Sve_CreateBreakBeforeMask
        "CreateBreakBeforePropagateMask",											// NI_Sve_CreateBreakBeforePropagateMask
        "CreateBreakPropagateMask",													// NI_Sve_CreateBreakPropagateMask
        "CreateFalseMaskByte",														// NI_Sve_CreateFalseMaskByte
        "CreateFalseMaskDouble",													// NI_Sve_CreateFalseMaskDouble
        "CreateFalseMaskInt16",														// NI_Sve_CreateFalseMaskInt16
        "CreateFalseMaskInt32",														// NI_Sve_CreateFalseMaskInt32
        "CreateFalseMaskInt64",														// NI_Sve_CreateFalseMaskInt64
        "CreateFalseMaskSByte",														// NI_Sve_CreateFalseMaskSByte
        "CreateFalseMaskSingle",													// NI_Sve_CreateFalseMaskSingle
        "CreateFalseMaskUInt16",													// NI_Sve_CreateFalseMaskUInt16
        "CreateFalseMaskUInt32",													// NI_Sve_CreateFalseMaskUInt32
        "CreateFalseMaskUInt64",													// NI_Sve_CreateFalseMaskUInt64
        "CreateMaskForFirstActiveElement",											// NI_Sve_CreateMaskForFirstActiveElement
        "CreateMaskForNextActiveElement",											// NI_Sve_CreateMaskForNextActiveElement
        "CreateTrueMaskByte",														// NI_Sve_CreateTrueMaskByte
        "CreateTrueMaskDouble",														// NI_Sve_CreateTrueMaskDouble
        "CreateTrueMaskInt16",														// NI_Sve_CreateTrueMaskInt16
        "CreateTrueMaskInt32",														// NI_Sve_CreateTrueMaskInt32
        "CreateTrueMaskInt64",														// NI_Sve_CreateTrueMaskInt64
        "CreateTrueMaskSByte",														// NI_Sve_CreateTrueMaskSByte
        "CreateTrueMaskSingle",														// NI_Sve_CreateTrueMaskSingle
        "CreateTrueMaskUInt16",														// NI_Sve_CreateTrueMaskUInt16
        "CreateTrueMaskUInt32",														// NI_Sve_CreateTrueMaskUInt32
        "CreateTrueMaskUInt64",														// NI_Sve_CreateTrueMaskUInt64
        "CreateWhileLessThanMaskByte",												// NI_Sve_CreateWhileLessThanMaskByte
        "CreateWhileLessThanMaskDouble",											// NI_Sve_CreateWhileLessThanMaskDouble
        "CreateWhileLessThanMaskInt16",												// NI_Sve_CreateWhileLessThanMaskInt16
        "CreateWhileLessThanMaskInt32",												// NI_Sve_CreateWhileLessThanMaskInt32
        "CreateWhileLessThanMaskInt64",												// NI_Sve_CreateWhileLessThanMaskInt64
        "CreateWhileLessThanMaskSByte",												// NI_Sve_CreateWhileLessThanMaskSByte
        "CreateWhileLessThanMaskSingle",											// NI_Sve_CreateWhileLessThanMaskSingle
        "CreateWhileLessThanMaskUInt16",											// NI_Sve_CreateWhileLessThanMaskUInt16
        "CreateWhileLessThanMaskUInt32",											// NI_Sve_CreateWhileLessThanMaskUInt32
        "CreateWhileLessThanMaskUInt64",											// NI_Sve_CreateWhileLessThanMaskUInt64
        "CreateWhileLessThanOrEqualMaskByte",										// NI_Sve_CreateWhileLessThanOrEqualMaskByte
        "CreateWhileLessThanOrEqualMaskDouble",										// NI_Sve_CreateWhileLessThanOrEqualMaskDouble
        "CreateWhileLessThanOrEqualMaskInt16",										// NI_Sve_CreateWhileLessThanOrEqualMaskInt16
        "CreateWhileLessThanOrEqualMaskInt32",										// NI_Sve_CreateWhileLessThanOrEqualMaskInt32
        "CreateWhileLessThanOrEqualMaskInt64",										// NI_Sve_CreateWhileLessThanOrEqualMaskInt64
        "CreateWhileLessThanOrEqualMaskSByte",										// NI_Sve_CreateWhileLessThanOrEqualMaskSByte
        "CreateWhileLessThanOrEqualMaskSingle",										// NI_Sve_CreateWhileLessThanOrEqualMaskSingle
        "CreateWhileLessThanOrEqualMaskUInt16",										// NI_Sve_CreateWhileLessThanOrEqualMaskUInt16
        "CreateWhileLessThanOrEqualMaskUInt32",										// NI_Sve_CreateWhileLessThanOrEqualMaskUInt32
        "CreateWhileLessThanOrEqualMaskUInt64",										// NI_Sve_CreateWhileLessThanOrEqualMaskUInt64
        "Divide",																	// NI_Sve_Divide
        "DotProduct",																// NI_Sve_DotProduct
        "DotProductBySelectedScalar",												// NI_Sve_DotProductBySelectedScalar
        "DuplicateSelectedScalarToVector",											// NI_Sve_DuplicateSelectedScalarToVector
        "ExtractAfterLastActiveElement",											// NI_Sve_ExtractAfterLastActiveElement
        "ExtractAfterLastActiveElementScalar",										// NI_Sve_ExtractAfterLastActiveElementScalar
        "ExtractLastActiveElement",													// NI_Sve_ExtractLastActiveElement
        "ExtractLastActiveElementScalar",											// NI_Sve_ExtractLastActiveElementScalar
        "ExtractVector",															// NI_Sve_ExtractVector
        "FloatingPointExponentialAccelerator",										// NI_Sve_FloatingPointExponentialAccelerator
        "FusedMultiplyAdd",															// NI_Sve_FusedMultiplyAdd
        "FusedMultiplyAddBySelectedScalar",											// NI_Sve_FusedMultiplyAddBySelectedScalar
        "FusedMultiplyAddNegated",													// NI_Sve_FusedMultiplyAddNegated
        "FusedMultiplySubtract",													// NI_Sve_FusedMultiplySubtract
        "FusedMultiplySubtractBySelectedScalar",									// NI_Sve_FusedMultiplySubtractBySelectedScalar
        "FusedMultiplySubtractNegated",												// NI_Sve_FusedMultiplySubtractNegated
        "GatherPrefetch16Bit",														// NI_Sve_GatherPrefetch16Bit
        "GatherPrefetch32Bit",														// NI_Sve_GatherPrefetch32Bit
        "GatherPrefetch64Bit",														// NI_Sve_GatherPrefetch64Bit
        "GatherPrefetch8Bit",														// NI_Sve_GatherPrefetch8Bit
        "GatherVector",																// NI_Sve_GatherVector
        "GatherVectorByteZeroExtend",												// NI_Sve_GatherVectorByteZeroExtend
        "GatherVectorByteZeroExtendFirstFaulting",									// NI_Sve_GatherVectorByteZeroExtendFirstFaulting
        "GatherVectorFirstFaulting",												// NI_Sve_GatherVectorFirstFaulting
        "GatherVectorInt16SignExtend",												// NI_Sve_GatherVectorInt16SignExtend
        "GatherVectorInt16SignExtendFirstFaulting",									// NI_Sve_GatherVectorInt16SignExtendFirstFaulting
        "GatherVectorInt16WithByteOffsetsSignExtend",								// NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend
        "GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting",					// NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting
        "GatherVectorInt32SignExtend",												// NI_Sve_GatherVectorInt32SignExtend
        "GatherVectorInt32SignExtendFirstFaulting",									// NI_Sve_GatherVectorInt32SignExtendFirstFaulting
        "GatherVectorInt32WithByteOffsetsSignExtend",								// NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend
        "GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting",					// NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting
        "GatherVectorSByteSignExtend",												// NI_Sve_GatherVectorSByteSignExtend
        "GatherVectorSByteSignExtendFirstFaulting",									// NI_Sve_GatherVectorSByteSignExtendFirstFaulting
        "GatherVectorUInt16WithByteOffsetsZeroExtend",								// NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend
        "GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting",					// NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting
        "GatherVectorUInt16ZeroExtend",												// NI_Sve_GatherVectorUInt16ZeroExtend
        "GatherVectorUInt16ZeroExtendFirstFaulting",								// NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting
        "GatherVectorUInt32WithByteOffsetsZeroExtend",								// NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend
        "GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting",					// NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting
        "GatherVectorUInt32ZeroExtend",												// NI_Sve_GatherVectorUInt32ZeroExtend
        "GatherVectorUInt32ZeroExtendFirstFaulting",								// NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting
        "GatherVectorWithByteOffsetFirstFaulting",									// NI_Sve_GatherVectorWithByteOffsetFirstFaulting
        "GatherVectorWithByteOffsets",												// NI_Sve_GatherVectorWithByteOffsets
        "GetActiveElementCount",													// NI_Sve_GetActiveElementCount
        "GetFfrByte",																// NI_Sve_GetFfrByte
        "GetFfrDouble",																// NI_Sve_GetFfrDouble
        "GetFfrInt16",																// NI_Sve_GetFfrInt16
        "GetFfrInt32",																// NI_Sve_GetFfrInt32
        "GetFfrInt64",																// NI_Sve_GetFfrInt64
        "GetFfrSByte",																// NI_Sve_GetFfrSByte
        "GetFfrSingle",																// NI_Sve_GetFfrSingle
        "GetFfrUInt16",																// NI_Sve_GetFfrUInt16
        "GetFfrUInt32",																// NI_Sve_GetFfrUInt32
        "GetFfrUInt64",																// NI_Sve_GetFfrUInt64
        "InsertIntoShiftedVector",													// NI_Sve_InsertIntoShiftedVector
        "LeadingSignCount",															// NI_Sve_LeadingSignCount
        "LeadingZeroCount",															// NI_Sve_LeadingZeroCount
        "Load2xVectorAndUnzip",														// NI_Sve_Load2xVectorAndUnzip
        "Load3xVectorAndUnzip",														// NI_Sve_Load3xVectorAndUnzip
        "Load4xVectorAndUnzip",														// NI_Sve_Load4xVectorAndUnzip
        "LoadVector",																// NI_Sve_LoadVector
        "LoadVector128AndReplicateToVector",										// NI_Sve_LoadVector128AndReplicateToVector
        "LoadVectorByteNonFaultingZeroExtendToInt16",								// NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16
        "LoadVectorByteNonFaultingZeroExtendToInt32",								// NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32
        "LoadVectorByteNonFaultingZeroExtendToInt64",								// NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64
        "LoadVectorByteNonFaultingZeroExtendToUInt16",								// NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16
        "LoadVectorByteNonFaultingZeroExtendToUInt32",								// NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32
        "LoadVectorByteNonFaultingZeroExtendToUInt64",								// NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64
        "LoadVectorByteZeroExtendFirstFaulting",									// NI_Sve_LoadVectorByteZeroExtendFirstFaulting
        "LoadVectorByteZeroExtendToInt16",											// NI_Sve_LoadVectorByteZeroExtendToInt16
        "LoadVectorByteZeroExtendToInt32",											// NI_Sve_LoadVectorByteZeroExtendToInt32
        "LoadVectorByteZeroExtendToInt64",											// NI_Sve_LoadVectorByteZeroExtendToInt64
        "LoadVectorByteZeroExtendToUInt16",											// NI_Sve_LoadVectorByteZeroExtendToUInt16
        "LoadVectorByteZeroExtendToUInt32",											// NI_Sve_LoadVectorByteZeroExtendToUInt32
        "LoadVectorByteZeroExtendToUInt64",											// NI_Sve_LoadVectorByteZeroExtendToUInt64
        "LoadVectorFirstFaulting",													// NI_Sve_LoadVectorFirstFaulting
        "LoadVectorInt16NonFaultingSignExtendToInt32",								// NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32
        "LoadVectorInt16NonFaultingSignExtendToInt64",								// NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64
        "LoadVectorInt16NonFaultingSignExtendToUInt32",								// NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32
        "LoadVectorInt16NonFaultingSignExtendToUInt64",								// NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64
        "LoadVectorInt16SignExtendFirstFaulting",									// NI_Sve_LoadVectorInt16SignExtendFirstFaulting
        "LoadVectorInt16SignExtendToInt32",											// NI_Sve_LoadVectorInt16SignExtendToInt32
        "LoadVectorInt16SignExtendToInt64",											// NI_Sve_LoadVectorInt16SignExtendToInt64
        "LoadVectorInt16SignExtendToUInt32",										// NI_Sve_LoadVectorInt16SignExtendToUInt32
        "LoadVectorInt16SignExtendToUInt64",										// NI_Sve_LoadVectorInt16SignExtendToUInt64
        "LoadVectorInt32NonFaultingSignExtendToInt64",								// NI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64
        "LoadVectorInt32NonFaultingSignExtendToUInt64",								// NI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64
        "LoadVectorInt32SignExtendFirstFaulting",									// NI_Sve_LoadVectorInt32SignExtendFirstFaulting
        "LoadVectorInt32SignExtendToInt64",											// NI_Sve_LoadVectorInt32SignExtendToInt64
        "LoadVectorInt32SignExtendToUInt64",										// NI_Sve_LoadVectorInt32SignExtendToUInt64
        "LoadVectorNonFaulting",													// NI_Sve_LoadVectorNonFaulting
        "LoadVectorNonTemporal",													// NI_Sve_LoadVectorNonTemporal
        "LoadVectorSByteNonFaultingSignExtendToInt16",								// NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16
        "LoadVectorSByteNonFaultingSignExtendToInt32",								// NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32
        "LoadVectorSByteNonFaultingSignExtendToInt64",								// NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64
        "LoadVectorSByteNonFaultingSignExtendToUInt16",								// NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16
        "LoadVectorSByteNonFaultingSignExtendToUInt32",								// NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32
        "LoadVectorSByteNonFaultingSignExtendToUInt64",								// NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64
        "LoadVectorSByteSignExtendFirstFaulting",									// NI_Sve_LoadVectorSByteSignExtendFirstFaulting
        "LoadVectorSByteSignExtendToInt16",											// NI_Sve_LoadVectorSByteSignExtendToInt16
        "LoadVectorSByteSignExtendToInt32",											// NI_Sve_LoadVectorSByteSignExtendToInt32
        "LoadVectorSByteSignExtendToInt64",											// NI_Sve_LoadVectorSByteSignExtendToInt64
        "LoadVectorSByteSignExtendToUInt16",										// NI_Sve_LoadVectorSByteSignExtendToUInt16
        "LoadVectorSByteSignExtendToUInt32",										// NI_Sve_LoadVectorSByteSignExtendToUInt32
        "LoadVectorSByteSignExtendToUInt64",										// NI_Sve_LoadVectorSByteSignExtendToUInt64
        "LoadVectorUInt16NonFaultingZeroExtendToInt32",								// NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32
        "LoadVectorUInt16NonFaultingZeroExtendToInt64",								// NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64
        "LoadVectorUInt16NonFaultingZeroExtendToUInt32",							// NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32
        "LoadVectorUInt16NonFaultingZeroExtendToUInt64",							// NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64
        "LoadVectorUInt16ZeroExtendFirstFaulting",									// NI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting
        "LoadVectorUInt16ZeroExtendToInt32",										// NI_Sve_LoadVectorUInt16ZeroExtendToInt32
        "LoadVectorUInt16ZeroExtendToInt64",										// NI_Sve_LoadVectorUInt16ZeroExtendToInt64
        "LoadVectorUInt16ZeroExtendToUInt32",										// NI_Sve_LoadVectorUInt16ZeroExtendToUInt32
        "LoadVectorUInt16ZeroExtendToUInt64",										// NI_Sve_LoadVectorUInt16ZeroExtendToUInt64
        "LoadVectorUInt32NonFaultingZeroExtendToInt64",								// NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64
        "LoadVectorUInt32NonFaultingZeroExtendToUInt64",							// NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64
        "LoadVectorUInt32ZeroExtendFirstFaulting",									// NI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting
        "LoadVectorUInt32ZeroExtendToInt64",										// NI_Sve_LoadVectorUInt32ZeroExtendToInt64
        "LoadVectorUInt32ZeroExtendToUInt64",										// NI_Sve_LoadVectorUInt32ZeroExtendToUInt64
        "Max",																		// NI_Sve_Max
        "MaxAcross",																// NI_Sve_MaxAcross
        "MaxNumber",																// NI_Sve_MaxNumber
        "MaxNumberAcross",															// NI_Sve_MaxNumberAcross
        "Min",																		// NI_Sve_Min
        "MinAcross",																// NI_Sve_MinAcross
        "MinNumber",																// NI_Sve_MinNumber
        "MinNumberAcross",															// NI_Sve_MinNumberAcross
        "Multiply",																	// NI_Sve_Multiply
        "MultiplyAdd",																// NI_Sve_MultiplyAdd
        "MultiplyAddRotateComplex",													// NI_Sve_MultiplyAddRotateComplex
        "MultiplyAddRotateComplexBySelectedScalar",									// NI_Sve_MultiplyAddRotateComplexBySelectedScalar
        "MultiplyBySelectedScalar",													// NI_Sve_MultiplyBySelectedScalar
        "MultiplyExtended",															// NI_Sve_MultiplyExtended
        "MultiplySubtract",															// NI_Sve_MultiplySubtract
        "Negate",																	// NI_Sve_Negate
        "Not",																		// NI_Sve_Not
        "Or",																		// NI_Sve_Or
        "OrAcross",																	// NI_Sve_OrAcross
        "PopCount",																	// NI_Sve_PopCount
        "Prefetch16Bit",															// NI_Sve_Prefetch16Bit
        "Prefetch32Bit",															// NI_Sve_Prefetch32Bit
        "Prefetch64Bit",															// NI_Sve_Prefetch64Bit
        "Prefetch8Bit",																// NI_Sve_Prefetch8Bit
        "ReciprocalEstimate",														// NI_Sve_ReciprocalEstimate
        "ReciprocalExponent",														// NI_Sve_ReciprocalExponent
        "ReciprocalSqrtEstimate",													// NI_Sve_ReciprocalSqrtEstimate
        "ReciprocalSqrtStep",														// NI_Sve_ReciprocalSqrtStep
        "ReciprocalStep",															// NI_Sve_ReciprocalStep
        "ReverseBits",																// NI_Sve_ReverseBits
        "ReverseElement",															// NI_Sve_ReverseElement
        "ReverseElement16",															// NI_Sve_ReverseElement16
        "ReverseElement32",															// NI_Sve_ReverseElement32
        "ReverseElement8",															// NI_Sve_ReverseElement8
        "RoundAwayFromZero",														// NI_Sve_RoundAwayFromZero
        "RoundToNearest",															// NI_Sve_RoundToNearest
        "RoundToNegativeInfinity",													// NI_Sve_RoundToNegativeInfinity
        "RoundToPositiveInfinity",													// NI_Sve_RoundToPositiveInfinity
        "RoundToZero",																// NI_Sve_RoundToZero
        "SaturatingDecrementBy16BitElementCount",									// NI_Sve_SaturatingDecrementBy16BitElementCount
        "SaturatingDecrementBy32BitElementCount",									// NI_Sve_SaturatingDecrementBy32BitElementCount
        "SaturatingDecrementBy64BitElementCount",									// NI_Sve_SaturatingDecrementBy64BitElementCount
        "SaturatingDecrementBy8BitElementCount",									// NI_Sve_SaturatingDecrementBy8BitElementCount
        "SaturatingDecrementByActiveElementCount",									// NI_Sve_SaturatingDecrementByActiveElementCount
        "SaturatingIncrementBy16BitElementCount",									// NI_Sve_SaturatingIncrementBy16BitElementCount
        "SaturatingIncrementBy32BitElementCount",									// NI_Sve_SaturatingIncrementBy32BitElementCount
        "SaturatingIncrementBy64BitElementCount",									// NI_Sve_SaturatingIncrementBy64BitElementCount
        "SaturatingIncrementBy8BitElementCount",									// NI_Sve_SaturatingIncrementBy8BitElementCount
        "SaturatingIncrementByActiveElementCount",									// NI_Sve_SaturatingIncrementByActiveElementCount
        "Scale",																	// NI_Sve_Scale
        "Scatter",																	// NI_Sve_Scatter
        "Scatter16BitNarrowing",													// NI_Sve_Scatter16BitNarrowing
        "Scatter16BitWithByteOffsetsNarrowing",										// NI_Sve_Scatter16BitWithByteOffsetsNarrowing
        "Scatter32BitNarrowing",													// NI_Sve_Scatter32BitNarrowing
        "Scatter32BitWithByteOffsetsNarrowing",										// NI_Sve_Scatter32BitWithByteOffsetsNarrowing
        "Scatter8BitNarrowing",														// NI_Sve_Scatter8BitNarrowing
        "Scatter8BitWithByteOffsetsNarrowing",										// NI_Sve_Scatter8BitWithByteOffsetsNarrowing
        "ScatterWithByteOffsets",													// NI_Sve_ScatterWithByteOffsets
        "SetFfr",																	// NI_Sve_SetFfr
        "ShiftLeftLogical",															// NI_Sve_ShiftLeftLogical
        "ShiftRightArithmetic",														// NI_Sve_ShiftRightArithmetic
        "ShiftRightArithmeticForDivide",											// NI_Sve_ShiftRightArithmeticForDivide
        "ShiftRightLogical",														// NI_Sve_ShiftRightLogical
        "SignExtend16",																// NI_Sve_SignExtend16
        "SignExtend32",																// NI_Sve_SignExtend32
        "SignExtend8",																// NI_Sve_SignExtend8
        "SignExtendWideningLower",													// NI_Sve_SignExtendWideningLower
        "SignExtendWideningUpper",													// NI_Sve_SignExtendWideningUpper
        "Splice",																	// NI_Sve_Splice
        "Sqrt",																		// NI_Sve_Sqrt
        "StoreAndZip",																// NI_Sve_StoreAndZip
        "StoreNarrowing",															// NI_Sve_StoreNarrowing
        "StoreNonTemporal",															// NI_Sve_StoreNonTemporal
        "Subtract",																	// NI_Sve_Subtract
        "SubtractSaturate",															// NI_Sve_SubtractSaturate
        "TestAnyTrue",																// NI_Sve_TestAnyTrue
        "TestFirstTrue",															// NI_Sve_TestFirstTrue
        "TestLastTrue",																// NI_Sve_TestLastTrue
        "TransposeEven",															// NI_Sve_TransposeEven
        "TransposeOdd",																// NI_Sve_TransposeOdd
        "TrigonometricMultiplyAddCoefficient",										// NI_Sve_TrigonometricMultiplyAddCoefficient
        "TrigonometricSelectCoefficient",											// NI_Sve_TrigonometricSelectCoefficient
        "TrigonometricStartingValue",												// NI_Sve_TrigonometricStartingValue
        "UnzipEven",																// NI_Sve_UnzipEven
        "UnzipOdd",																	// NI_Sve_UnzipOdd
        "VectorTableLookup",														// NI_Sve_VectorTableLookup
        "Xor",																		// NI_Sve_Xor
        "XorAcross",																// NI_Sve_XorAcross
        "ZeroExtend16",																// NI_Sve_ZeroExtend16
        "ZeroExtend32",																// NI_Sve_ZeroExtend32
        "ZeroExtend8",																// NI_Sve_ZeroExtend8
        "ZeroExtendWideningLower",													// NI_Sve_ZeroExtendWideningLower
        "ZeroExtendWideningUpper",													// NI_Sve_ZeroExtendWideningUpper
        "ZipHigh",																	// NI_Sve_ZipHigh
        "ZipLow",																	// NI_Sve_ZipLow
        "AbsSaturate",																// NI_Sve2_AbsSaturate
        "AbsoluteDifferenceAdd",													// NI_Sve2_AbsoluteDifferenceAdd
        "AbsoluteDifferenceWideningEven",											// NI_Sve2_AbsoluteDifferenceWideningEven
        "AbsoluteDifferenceWideningLowerAndAddEven",								// NI_Sve2_AbsoluteDifferenceWideningLowerAndAddEven
        "AbsoluteDifferenceWideningLowerAndAddOdd",									// NI_Sve2_AbsoluteDifferenceWideningLowerAndAddOdd
        "AbsoluteDifferenceWideningOdd",											// NI_Sve2_AbsoluteDifferenceWideningOdd
        "AddCarryWideningEven",														// NI_Sve2_AddCarryWideningEven
        "AddCarryWideningOdd",														// NI_Sve2_AddCarryWideningOdd
        "AddHighNarrowingEven",														// NI_Sve2_AddHighNarrowingEven
        "AddHighNarrowingOdd",														// NI_Sve2_AddHighNarrowingOdd
        "AddPairwise",																// NI_Sve2_AddPairwise
        "AddPairwiseWideningAndAdd",												// NI_Sve2_AddPairwiseWideningAndAdd
        "AddRotateComplex",															// NI_Sve2_AddRotateComplex
        "AddRoundedHighNarrowingEven",												// NI_Sve2_AddRoundedHighNarrowingEven
        "AddRoundedHighNarrowingOdd",												// NI_Sve2_AddRoundedHighNarrowingOdd
        "AddSaturate",																// NI_Sve2_AddSaturate
        "AddSaturateRotateComplex",													// NI_Sve2_AddSaturateRotateComplex
        "AddWideningEven",															// NI_Sve2_AddWideningEven
        "AddWideningEvenOdd",														// NI_Sve2_AddWideningEvenOdd
        "AddWideningOdd",															// NI_Sve2_AddWideningOdd
        "BitwiseClearXor",															// NI_Sve2_BitwiseClearXor
        "BitwiseSelect",															// NI_Sve2_BitwiseSelect
        "BitwiseSelectLeftInverted",												// NI_Sve2_BitwiseSelectLeftInverted
        "BitwiseSelectRightInverted",												// NI_Sve2_BitwiseSelectRightInverted
        "ConvertToDoubleOdd",														// NI_Sve2_ConvertToDoubleOdd
        "ConvertToSingleEvenRoundToOdd",											// NI_Sve2_ConvertToSingleEvenRoundToOdd
        "ConvertToSingleOdd",														// NI_Sve2_ConvertToSingleOdd
        "ConvertToSingleOddRoundToOdd",												// NI_Sve2_ConvertToSingleOddRoundToOdd
        "CountMatchingElements",													// NI_Sve2_CountMatchingElements
        "CountMatchingElementsIn128BitSegments",									// NI_Sve2_CountMatchingElementsIn128BitSegments
        "CreateWhileGreaterThanMaskByte",											// NI_Sve2_CreateWhileGreaterThanMaskByte
        "CreateWhileGreaterThanMaskDouble",											// NI_Sve2_CreateWhileGreaterThanMaskDouble
        "CreateWhileGreaterThanMaskInt16",											// NI_Sve2_CreateWhileGreaterThanMaskInt16
        "CreateWhileGreaterThanMaskInt32",											// NI_Sve2_CreateWhileGreaterThanMaskInt32
        "CreateWhileGreaterThanMaskInt64",											// NI_Sve2_CreateWhileGreaterThanMaskInt64
        "CreateWhileGreaterThanMaskSByte",											// NI_Sve2_CreateWhileGreaterThanMaskSByte
        "CreateWhileGreaterThanMaskSingle",											// NI_Sve2_CreateWhileGreaterThanMaskSingle
        "CreateWhileGreaterThanMaskUInt16",											// NI_Sve2_CreateWhileGreaterThanMaskUInt16
        "CreateWhileGreaterThanMaskUInt32",											// NI_Sve2_CreateWhileGreaterThanMaskUInt32
        "CreateWhileGreaterThanMaskUInt64",											// NI_Sve2_CreateWhileGreaterThanMaskUInt64
        "CreateWhileGreaterThanOrEqualMaskByte",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskByte
        "CreateWhileGreaterThanOrEqualMaskDouble",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskDouble
        "CreateWhileGreaterThanOrEqualMaskInt16",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt16
        "CreateWhileGreaterThanOrEqualMaskInt32",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt32
        "CreateWhileGreaterThanOrEqualMaskInt64",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt64
        "CreateWhileGreaterThanOrEqualMaskSByte",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskSByte
        "CreateWhileGreaterThanOrEqualMaskSingle",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskSingle
        "CreateWhileGreaterThanOrEqualMaskUInt16",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt16
        "CreateWhileGreaterThanOrEqualMaskUInt32",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt32
        "CreateWhileGreaterThanOrEqualMaskUInt64",									// NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt64
        "CreateWhileReadAfterWriteMaskByte",										// NI_Sve2_CreateWhileReadAfterWriteMaskByte
        "CreateWhileReadAfterWriteMaskDouble",										// NI_Sve2_CreateWhileReadAfterWriteMaskDouble
        "CreateWhileReadAfterWriteMaskInt16",										// NI_Sve2_CreateWhileReadAfterWriteMaskInt16
        "CreateWhileReadAfterWriteMaskInt32",										// NI_Sve2_CreateWhileReadAfterWriteMaskInt32
        "CreateWhileReadAfterWriteMaskInt64",										// NI_Sve2_CreateWhileReadAfterWriteMaskInt64
        "CreateWhileReadAfterWriteMaskSByte",										// NI_Sve2_CreateWhileReadAfterWriteMaskSByte
        "CreateWhileReadAfterWriteMaskSingle",										// NI_Sve2_CreateWhileReadAfterWriteMaskSingle
        "CreateWhileReadAfterWriteMaskUInt16",										// NI_Sve2_CreateWhileReadAfterWriteMaskUInt16
        "CreateWhileReadAfterWriteMaskUInt32",										// NI_Sve2_CreateWhileReadAfterWriteMaskUInt32
        "CreateWhileReadAfterWriteMaskUInt64",										// NI_Sve2_CreateWhileReadAfterWriteMaskUInt64
        "DotProductRotateComplex",													// NI_Sve2_DotProductRotateComplex
        "DotProductRotateComplexBySelectedIndex",									// NI_Sve2_DotProductRotateComplexBySelectedIndex
        "FusedAddHalving",															// NI_Sve2_FusedAddHalving
        "FusedAddRoundedHalving",													// NI_Sve2_FusedAddRoundedHalving
        "FusedSubtractHalving",														// NI_Sve2_FusedSubtractHalving
        "GatherVectorByteZeroExtendNonTemporal",									// NI_Sve2_GatherVectorByteZeroExtendNonTemporal
        "GatherVectorInt16SignExtendNonTemporal",									// NI_Sve2_GatherVectorInt16SignExtendNonTemporal
        "GatherVectorInt16WithByteOffsetsSignExtendNonTemporal",					// NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal
        "GatherVectorInt32SignExtendNonTemporal",									// NI_Sve2_GatherVectorInt32SignExtendNonTemporal
        "GatherVectorInt32WithByteOffsetsSignExtendNonTemporal",					// NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal
        "GatherVectorNonTemporal",													// NI_Sve2_GatherVectorNonTemporal
        "GatherVectorSByteSignExtendNonTemporal",									// NI_Sve2_GatherVectorSByteSignExtendNonTemporal
        "GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal",					// NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal
        "GatherVectorUInt16ZeroExtendNonTemporal",									// NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal
        "GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal",					// NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal
        "GatherVectorUInt32ZeroExtendNonTemporal",									// NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal
        "GatherVectorWithByteOffsetsNonTemporal",									// NI_Sve2_GatherVectorWithByteOffsetsNonTemporal
        "InterleavingXorEvenOdd",													// NI_Sve2_InterleavingXorEvenOdd
        "InterleavingXorOddEven",													// NI_Sve2_InterleavingXorOddEven
        "Log2",																		// NI_Sve2_Log2
        "Match",																	// NI_Sve2_Match
        "MaxNumberPairwise",														// NI_Sve2_MaxNumberPairwise
        "MaxPairwise",																// NI_Sve2_MaxPairwise
        "MinNumberPairwise",														// NI_Sve2_MinNumberPairwise
        "MinPairwise",																// NI_Sve2_MinPairwise
        "MultiplyAddBySelectedScalar",												// NI_Sve2_MultiplyAddBySelectedScalar
        "MultiplyAddRotateComplex",													// NI_Sve2_MultiplyAddRotateComplex
        "MultiplyAddRotateComplexBySelectedScalar",									// NI_Sve2_MultiplyAddRotateComplexBySelectedScalar
        "MultiplyAddRoundedDoublingSaturateHighRotateComplex",						// NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex
        "MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar",		// NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar
        "MultiplyBySelectedScalar",													// NI_Sve2_MultiplyBySelectedScalar
        "MultiplyBySelectedScalarWideningEven",										// NI_Sve2_MultiplyBySelectedScalarWideningEven
        "MultiplyBySelectedScalarWideningEvenAndAdd",								// NI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd
        "MultiplyBySelectedScalarWideningEvenAndSubtract",							// NI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract
        "MultiplyBySelectedScalarWideningOdd",										// NI_Sve2_MultiplyBySelectedScalarWideningOdd
        "MultiplyBySelectedScalarWideningOddAndAdd",								// NI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd
        "MultiplyBySelectedScalarWideningOddAndSubtract",							// NI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract
        "MultiplyDoublingBySelectedScalarSaturateHigh",								// NI_Sve2_MultiplyDoublingBySelectedScalarSaturateHigh
        "MultiplyDoublingSaturateHigh",												// NI_Sve2_MultiplyDoublingSaturateHigh
        "MultiplyDoublingWideningAndAddSaturateEven",								// NI_Sve2_MultiplyDoublingWideningAndAddSaturateEven
        "MultiplyDoublingWideningAndAddSaturateEvenOdd",							// NI_Sve2_MultiplyDoublingWideningAndAddSaturateEvenOdd
        "MultiplyDoublingWideningAndAddSaturateOdd",								// NI_Sve2_MultiplyDoublingWideningAndAddSaturateOdd
        "MultiplyDoublingWideningAndSubtractSaturateEven",							// NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEven
        "MultiplyDoublingWideningAndSubtractSaturateEvenOdd",						// NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEvenOdd
        "MultiplyDoublingWideningAndSubtractSaturateOdd",							// NI_Sve2_MultiplyDoublingWideningAndSubtractSaturateOdd
        "MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven",				// NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven
        "MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd",				// NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd
        "MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven",			// NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven
        "MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd",			// NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd
        "MultiplyDoublingWideningSaturateEven",										// NI_Sve2_MultiplyDoublingWideningSaturateEven
        "MultiplyDoublingWideningSaturateEvenBySelectedScalar",						// NI_Sve2_MultiplyDoublingWideningSaturateEvenBySelectedScalar
        "MultiplyDoublingWideningSaturateOdd",										// NI_Sve2_MultiplyDoublingWideningSaturateOdd
        "MultiplyDoublingWideningSaturateOddBySelectedScalar",						// NI_Sve2_MultiplyDoublingWideningSaturateOddBySelectedScalar
        "MultiplyRoundedDoublingBySelectedScalarSaturateHigh",						// NI_Sve2_MultiplyRoundedDoublingBySelectedScalarSaturateHigh
        "MultiplyRoundedDoublingSaturateAndAddHigh",								// NI_Sve2_MultiplyRoundedDoublingSaturateAndAddHigh
        "MultiplyRoundedDoublingSaturateAndSubtractHigh",							// NI_Sve2_MultiplyRoundedDoublingSaturateAndSubtractHigh
        "MultiplyRoundedDoublingSaturateBySelectedScalarAndAddHigh",				// NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndAddHigh
        "MultiplyRoundedDoublingSaturateBySelectedScalarAndSubtractHigh",			// NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndSubtractHigh
        "MultiplyRoundedDoublingSaturateHigh",										// NI_Sve2_MultiplyRoundedDoublingSaturateHigh
        "MultiplySubtractBySelectedScalar",											// NI_Sve2_MultiplySubtractBySelectedScalar
        "MultiplyWideningEven",														// NI_Sve2_MultiplyWideningEven
        "MultiplyWideningEvenAndAdd",												// NI_Sve2_MultiplyWideningEvenAndAdd
        "MultiplyWideningEvenAndSubtract",											// NI_Sve2_MultiplyWideningEvenAndSubtract
        "MultiplyWideningOdd",														// NI_Sve2_MultiplyWideningOdd
        "MultiplyWideningOddAndAdd",												// NI_Sve2_MultiplyWideningOddAndAdd
        "MultiplyWideningOddAndSubtract",											// NI_Sve2_MultiplyWideningOddAndSubtract
        "NegateSaturate",															// NI_Sve2_NegateSaturate
        "NoMatch",																	// NI_Sve2_NoMatch
        "PolynomialMultiply",														// NI_Sve2_PolynomialMultiply
        "PolynomialMultiplyWideningEven",											// NI_Sve2_PolynomialMultiplyWideningEven
        "PolynomialMultiplyWideningOdd",											// NI_Sve2_PolynomialMultiplyWideningOdd
        "ReciprocalEstimate",														// NI_Sve2_ReciprocalEstimate
        "ReciprocalSqrtEstimate",													// NI_Sve2_ReciprocalSqrtEstimate
        "Scatter16BitNarrowingNonTemporal",											// NI_Sve2_Scatter16BitNarrowingNonTemporal
        "Scatter16BitWithByteOffsetsNarrowingNonTemporal",							// NI_Sve2_Scatter16BitWithByteOffsetsNarrowingNonTemporal
        "Scatter32BitNarrowingNonTemporal",											// NI_Sve2_Scatter32BitNarrowingNonTemporal
        "Scatter32BitWithByteOffsetsNarrowingNonTemporal",							// NI_Sve2_Scatter32BitWithByteOffsetsNarrowingNonTemporal
        "Scatter8BitNarrowingNonTemporal",											// NI_Sve2_Scatter8BitNarrowingNonTemporal
        "Scatter8BitWithByteOffsetsNarrowingNonTemporal",							// NI_Sve2_Scatter8BitWithByteOffsetsNarrowingNonTemporal
        "ScatterNonTemporal",														// NI_Sve2_ScatterNonTemporal
        "ScatterWithByteOffsetsNonTemporal",										// NI_Sve2_ScatterWithByteOffsetsNonTemporal
        "ShiftArithmeticRounded",													// NI_Sve2_ShiftArithmeticRounded
        "ShiftArithmeticRoundedSaturate",											// NI_Sve2_ShiftArithmeticRoundedSaturate
        "ShiftArithmeticSaturate",													// NI_Sve2_ShiftArithmeticSaturate
        "ShiftLeftAndInsert",														// NI_Sve2_ShiftLeftAndInsert
        "ShiftLeftLogicalSaturate",													// NI_Sve2_ShiftLeftLogicalSaturate
        "ShiftLeftLogicalSaturateUnsigned",											// NI_Sve2_ShiftLeftLogicalSaturateUnsigned
        "ShiftLeftLogicalWideningEven",												// NI_Sve2_ShiftLeftLogicalWideningEven
        "ShiftLeftLogicalWideningOdd",												// NI_Sve2_ShiftLeftLogicalWideningOdd
        "ShiftLogicalRounded",														// NI_Sve2_ShiftLogicalRounded
        "ShiftLogicalRoundedSaturate",												// NI_Sve2_ShiftLogicalRoundedSaturate
        "ShiftRightAndInsert",														// NI_Sve2_ShiftRightAndInsert
        "ShiftRightArithmeticAdd",													// NI_Sve2_ShiftRightArithmeticAdd
        "ShiftRightArithmeticNarrowingSaturateEven",								// NI_Sve2_ShiftRightArithmeticNarrowingSaturateEven
        "ShiftRightArithmeticNarrowingSaturateOdd",									// NI_Sve2_ShiftRightArithmeticNarrowingSaturateOdd
        "ShiftRightArithmeticNarrowingSaturateUnsignedEven",						// NI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedEven
        "ShiftRightArithmeticNarrowingSaturateUnsignedOdd",							// NI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedOdd
        "ShiftRightArithmeticRounded",												// NI_Sve2_ShiftRightArithmeticRounded
        "ShiftRightArithmeticRoundedAdd",											// NI_Sve2_ShiftRightArithmeticRoundedAdd
        "ShiftRightArithmeticRoundedNarrowingSaturateEven",							// NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateEven
        "ShiftRightArithmeticRoundedNarrowingSaturateOdd",							// NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateOdd
        "ShiftRightArithmeticRoundedNarrowingSaturateUnsignedEven",					// NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedEven
        "ShiftRightArithmeticRoundedNarrowingSaturateUnsignedOdd",					// NI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedOdd
        "ShiftRightLogicalAdd",														// NI_Sve2_ShiftRightLogicalAdd
        "ShiftRightLogicalNarrowingEven",											// NI_Sve2_ShiftRightLogicalNarrowingEven
        "ShiftRightLogicalNarrowingOdd",											// NI_Sve2_ShiftRightLogicalNarrowingOdd
        "ShiftRightLogicalNarrowingSaturateEven",									// NI_Sve2_ShiftRightLogicalNarrowingSaturateEven
        "ShiftRightLogicalNarrowingSaturateOdd",									// NI_Sve2_ShiftRightLogicalNarrowingSaturateOdd
        "ShiftRightLogicalRounded",													// NI_Sve2_ShiftRightLogicalRounded
        "ShiftRightLogicalRoundedAdd",												// NI_Sve2_ShiftRightLogicalRoundedAdd
        "ShiftRightLogicalRoundedNarrowingEven",									// NI_Sve2_ShiftRightLogicalRoundedNarrowingEven
        "ShiftRightLogicalRoundedNarrowingOdd",										// NI_Sve2_ShiftRightLogicalRoundedNarrowingOdd
        "ShiftRightLogicalRoundedNarrowingSaturateEven",							// NI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateEven
        "ShiftRightLogicalRoundedNarrowingSaturateOdd",								// NI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateOdd
        "SubtractBorrowWideningEven",												// NI_Sve2_SubtractBorrowWideningEven
        "SubtractBorrowWideningOdd",												// NI_Sve2_SubtractBorrowWideningOdd
        "SubtractHighNarrowingEven",												// NI_Sve2_SubtractHighNarrowingEven
        "SubtractHighNarrowingOdd",													// NI_Sve2_SubtractHighNarrowingOdd
        "SubtractRoundedHighNarrowingEven",											// NI_Sve2_SubtractRoundedHighNarrowingEven
        "SubtractRoundedHighNarrowingOdd",											// NI_Sve2_SubtractRoundedHighNarrowingOdd
        "SubtractSaturate",															// NI_Sve2_SubtractSaturate
        "SubtractWideningEven",														// NI_Sve2_SubtractWideningEven
        "SubtractWideningEvenOdd",													// NI_Sve2_SubtractWideningEvenOdd
        "SubtractWideningOdd",														// NI_Sve2_SubtractWideningOdd
        "SubtractWideningOddEven",													// NI_Sve2_SubtractWideningOddEven
        "VectorTableLookup",														// NI_Sve2_VectorTableLookup
        "VectorTableLookupExtension",												// NI_Sve2_VectorTableLookupExtension
        "Xor",																		// NI_Sve2_Xor
        "XorRotateRight",															// NI_Sve2_XorRotateRight
        "ConditionalExtractAfterLastActiveElementScalar",							// NI_Sve_ConditionalExtractAfterLastActiveElementScalar
        "ConditionalExtractLastActiveElementScalar",								// NI_Sve_ConditionalExtractLastActiveElementScalar
        "ConvertMaskToVector",														// NI_Sve_ConvertMaskToVector
        "ConvertVectorToMask",														// NI_Sve_ConvertVectorToMask
        "ConversionTrueMask",														// NI_Sve_ConversionTrueMask
        "SaturatingDecrementBy16BitElementCountScalar",								// NI_Sve_SaturatingDecrementBy16BitElementCountScalar
        "SaturatingDecrementBy32BitElementCountScalar",								// NI_Sve_SaturatingDecrementBy32BitElementCountScalar
        "SaturatingDecrementBy64BitElementCountScalar",								// NI_Sve_SaturatingDecrementBy64BitElementCountScalar
        "SaturatingIncrementBy16BitElementCountScalar",								// NI_Sve_SaturatingIncrementBy16BitElementCountScalar
        "SaturatingIncrementBy32BitElementCountScalar",								// NI_Sve_SaturatingIncrementBy32BitElementCountScalar
        "SaturatingIncrementBy64BitElementCountScalar",								// NI_Sve_SaturatingIncrementBy64BitElementCountScalar
        "StoreAndZipx2",															// NI_Sve_StoreAndZipx2
        "StoreAndZipx3",															// NI_Sve_StoreAndZipx3
        "StoreAndZipx4",															// NI_Sve_StoreAndZipx4
        "And_Predicates",															// NI_Sve_And_Predicates
        "BitwiseClear_Predicates",													// NI_Sve_BitwiseClear_Predicates
        "Or_Predicates",															// NI_Sve_Or_Predicates
        "Xor_Predicates",															// NI_Sve_Xor_Predicates
        "ConditionalSelect_Predicates",												// NI_Sve_ConditionalSelect_Predicates
        "ZipHigh_Predicates",														// NI_Sve_ZipHigh_Predicates
        "ZipLow_Predicates",														// NI_Sve_ZipLow_Predicates
        "UnzipEven_Predicates",														// NI_Sve_UnzipEven_Predicates
        "UnzipOdd_Predicates",														// NI_Sve_UnzipOdd_Predicates
        "TransposeEven_Predicates",													// NI_Sve_TransposeEven_Predicates
        "TransposeOdd_Predicates",													// NI_Sve_TransposeOdd_Predicates
        "ReverseElement_Predicates",												// NI_Sve_ReverseElement_Predicates
    ];
#endif
}
#endif
