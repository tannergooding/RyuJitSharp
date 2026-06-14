// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;

namespace RyuJitSharp;

public partial struct HWIntrinsicInfo
{
    private static ReadOnlySpan<HWIntrinsicCategory> s_categories => [
         HW_Category_Helper,            // NI_Vector128_Abs
         HW_Category_Helper,            // NI_Vector128_AddSaturate
         HW_Category_Helper,            // NI_Vector128_AndNot
         HW_Category_Helper,            // NI_Vector128_As
         HW_Category_Helper,            // NI_Vector128_AsByte
         HW_Category_Helper,            // NI_Vector128_AsDouble
         HW_Category_Helper,            // NI_Vector128_AsInt16
         HW_Category_Helper,            // NI_Vector128_AsInt32
         HW_Category_Helper,            // NI_Vector128_AsInt64
         HW_Category_Helper,            // NI_Vector128_AsNInt
         HW_Category_Helper,            // NI_Vector128_AsNUInt
         HW_Category_Helper,            // NI_Vector128_AsSByte
         HW_Category_Helper,            // NI_Vector128_AsSingle
         HW_Category_Helper,            // NI_Vector128_AsUInt16
         HW_Category_Helper,            // NI_Vector128_AsUInt32
         HW_Category_Helper,            // NI_Vector128_AsUInt64
         HW_Category_Helper,            // NI_Vector128_AsVector
         HW_Category_Helper,            // NI_Vector128_AsVector128
         HW_Category_SimpleSIMD,        // NI_Vector128_AsVector128Unsafe
         HW_Category_SimpleSIMD,        // NI_Vector128_AsVector2
         HW_Category_SimpleSIMD,        // NI_Vector128_AsVector3
         HW_Category_Helper,            // NI_Vector128_AsVector4
         HW_Category_Helper,            // NI_Vector128_Ceiling
         HW_Category_Helper,            // NI_Vector128_ConditionalSelect
         HW_Category_Helper,            // NI_Vector128_ConvertToDouble
         HW_Category_Helper,            // NI_Vector128_ConvertToInt32
         HW_Category_Helper,            // NI_Vector128_ConvertToInt32Native
         HW_Category_Helper,            // NI_Vector128_ConvertToInt64
         HW_Category_Helper,            // NI_Vector128_ConvertToInt64Native
         HW_Category_Helper,            // NI_Vector128_ConvertToSingle
         HW_Category_Helper,            // NI_Vector128_ConvertToUInt32
         HW_Category_Helper,            // NI_Vector128_ConvertToUInt32Native
         HW_Category_Helper,            // NI_Vector128_ConvertToUInt64
         HW_Category_Helper,            // NI_Vector128_ConvertToUInt64Native
         HW_Category_Helper,            // NI_Vector128_Create
         HW_Category_SIMDScalar,        // NI_Vector128_CreateScalar
         HW_Category_SIMDScalar,        // NI_Vector128_CreateScalarUnsafe
         HW_Category_Helper,            // NI_Vector128_CreateSequence
         HW_Category_Helper,            // NI_Vector128_Dot
         HW_Category_Helper,            // NI_Vector128_Equals
         HW_Category_Helper,            // NI_Vector128_EqualsAny
         HW_Category_Helper,            // NI_Vector128_ExtractMostSignificantBits
         HW_Category_Helper,            // NI_Vector128_Floor
         HW_Category_Helper,            // NI_Vector128_FusedMultiplyAdd
         HW_Category_Helper,            // NI_Vector128_GetElement
         HW_Category_Helper,            // NI_Vector128_GreaterThan
         HW_Category_Helper,            // NI_Vector128_GreaterThanAll
         HW_Category_Helper,            // NI_Vector128_GreaterThanAny
         HW_Category_Helper,            // NI_Vector128_GreaterThanOrEqual
         HW_Category_Helper,            // NI_Vector128_GreaterThanOrEqualAll
         HW_Category_Helper,            // NI_Vector128_GreaterThanOrEqualAny
         HW_Category_Helper,            // NI_Vector128_IsEvenInteger
         HW_Category_Helper,            // NI_Vector128_IsFinite
         HW_Category_Helper,            // NI_Vector128_IsInfinity
         HW_Category_Helper,            // NI_Vector128_IsInteger
         HW_Category_Helper,            // NI_Vector128_IsNaN
         HW_Category_Helper,            // NI_Vector128_IsNegative
         HW_Category_Helper,            // NI_Vector128_IsNegativeInfinity
         HW_Category_Helper,            // NI_Vector128_IsNormal
         HW_Category_Helper,            // NI_Vector128_IsOddInteger
         HW_Category_Helper,            // NI_Vector128_IsPositive
         HW_Category_Helper,            // NI_Vector128_IsPositiveInfinity
         HW_Category_Helper,            // NI_Vector128_IsSubnormal
         HW_Category_Helper,            // NI_Vector128_IsZero
         HW_Category_Helper,            // NI_Vector128_LessThan
         HW_Category_Helper,            // NI_Vector128_LessThanAll
         HW_Category_Helper,            // NI_Vector128_LessThanAny
         HW_Category_Helper,            // NI_Vector128_LessThanOrEqual
         HW_Category_Helper,            // NI_Vector128_LessThanOrEqualAll
         HW_Category_Helper,            // NI_Vector128_LessThanOrEqualAny
         HW_Category_Helper,            // NI_Vector128_LoadAligned
         HW_Category_Helper,            // NI_Vector128_LoadAlignedNonTemporal
         HW_Category_Helper,            // NI_Vector128_LoadUnsafe
         HW_Category_Helper,            // NI_Vector128_Max
         HW_Category_Helper,            // NI_Vector128_MaxMagnitude
         HW_Category_Helper,            // NI_Vector128_MaxMagnitudeNumber
         HW_Category_Helper,            // NI_Vector128_MaxNative
         HW_Category_Helper,            // NI_Vector128_MaxNumber
         HW_Category_Helper,            // NI_Vector128_Min
         HW_Category_Helper,            // NI_Vector128_MinMagnitude
         HW_Category_Helper,            // NI_Vector128_MinMagnitudeNumber
         HW_Category_Helper,            // NI_Vector128_MinNative
         HW_Category_Helper,            // NI_Vector128_MinNumber
         HW_Category_Helper,            // NI_Vector128_MultiplyAddEstimate
         HW_Category_Helper,            // NI_Vector128_Narrow
         HW_Category_Helper,            // NI_Vector128_NarrowWithSaturation
         HW_Category_Helper,            // NI_Vector128_Round
         HW_Category_Helper,            // NI_Vector128_ShiftLeft
         HW_Category_Helper,            // NI_Vector128_Shuffle
         HW_Category_Helper,            // NI_Vector128_ShuffleNative
         HW_Category_Helper,            // NI_Vector128_ShuffleNativeFallback
         HW_Category_Helper,            // NI_Vector128_Sqrt
         HW_Category_Helper,            // NI_Vector128_StoreAligned
         HW_Category_Helper,            // NI_Vector128_StoreAlignedNonTemporal
         HW_Category_Helper,            // NI_Vector128_StoreUnsafe
         HW_Category_Helper,            // NI_Vector128_SubtractSaturate
         HW_Category_Helper,            // NI_Vector128_Sum
         HW_Category_SIMDScalar,        // NI_Vector128_ToScalar
         HW_Category_SimpleSIMD,        // NI_Vector128_ToVector256
         HW_Category_SimpleSIMD,        // NI_Vector128_ToVector256Unsafe
         HW_Category_SimpleSIMD,        // NI_Vector128_ToVector512
         HW_Category_Helper,            // NI_Vector128_Truncate
         HW_Category_Helper,            // NI_Vector128_WidenLower
         HW_Category_Helper,            // NI_Vector128_WidenUpper
         HW_Category_Helper,            // NI_Vector128_WithElement
         HW_Category_Helper,            // NI_Vector128_get_AllBitsSet
         HW_Category_Helper,            // NI_Vector128_get_E
         HW_Category_Helper,            // NI_Vector128_get_Epsilon
         HW_Category_Helper,            // NI_Vector128_get_Indices
         HW_Category_Helper,            // NI_Vector128_get_NaN
         HW_Category_Helper,            // NI_Vector128_get_NegativeInfinity
         HW_Category_Helper,            // NI_Vector128_get_NegativeOne
         HW_Category_Helper,            // NI_Vector128_get_NegativeZero
         HW_Category_Helper,            // NI_Vector128_get_One
         HW_Category_Helper,            // NI_Vector128_get_Pi
         HW_Category_Helper,            // NI_Vector128_get_PositiveInfinity
         HW_Category_Helper,            // NI_Vector128_get_Tau
         HW_Category_Helper,            // NI_Vector128_get_Zero
         HW_Category_Helper,            // NI_Vector128_op_Addition
         HW_Category_Helper,            // NI_Vector128_op_BitwiseAnd
         HW_Category_Helper,            // NI_Vector128_op_BitwiseOr
         HW_Category_Helper,            // NI_Vector128_op_Division
         HW_Category_Helper,            // NI_Vector128_op_Equality
         HW_Category_Helper,            // NI_Vector128_op_ExclusiveOr
         HW_Category_Helper,            // NI_Vector128_op_Inequality
         HW_Category_Helper,            // NI_Vector128_op_LeftShift
         HW_Category_Helper,            // NI_Vector128_op_Multiply
         HW_Category_Helper,            // NI_Vector128_op_OnesComplement
         HW_Category_Helper,            // NI_Vector128_op_RightShift
         HW_Category_Helper,            // NI_Vector128_op_Subtraction
         HW_Category_Helper,            // NI_Vector128_op_UnaryNegation
         HW_Category_Helper,            // NI_Vector128_op_UnaryPlus
         HW_Category_Helper,            // NI_Vector128_op_UnsignedRightShift
         HW_Category_Helper,            // NI_Vector256_Abs
         HW_Category_Helper,            // NI_Vector256_AddSaturate
         HW_Category_Helper,            // NI_Vector256_AndNot
         HW_Category_Helper,            // NI_Vector256_As
         HW_Category_Helper,            // NI_Vector256_AsByte
         HW_Category_Helper,            // NI_Vector256_AsDouble
         HW_Category_Helper,            // NI_Vector256_AsInt16
         HW_Category_Helper,            // NI_Vector256_AsInt32
         HW_Category_Helper,            // NI_Vector256_AsInt64
         HW_Category_Helper,            // NI_Vector256_AsNInt
         HW_Category_Helper,            // NI_Vector256_AsNUInt
         HW_Category_Helper,            // NI_Vector256_AsSByte
         HW_Category_Helper,            // NI_Vector256_AsSingle
         HW_Category_Helper,            // NI_Vector256_AsUInt16
         HW_Category_Helper,            // NI_Vector256_AsUInt32
         HW_Category_Helper,            // NI_Vector256_AsUInt64
         HW_Category_Helper,            // NI_Vector256_AsVector
         HW_Category_Helper,            // NI_Vector256_AsVector256
         HW_Category_Helper,            // NI_Vector256_Ceiling
         HW_Category_Helper,            // NI_Vector256_ConditionalSelect
         HW_Category_Helper,            // NI_Vector256_ConvertToDouble
         HW_Category_Helper,            // NI_Vector256_ConvertToInt32
         HW_Category_Helper,            // NI_Vector256_ConvertToInt32Native
         HW_Category_Helper,            // NI_Vector256_ConvertToInt64
         HW_Category_Helper,            // NI_Vector256_ConvertToInt64Native
         HW_Category_Helper,            // NI_Vector256_ConvertToSingle
         HW_Category_Helper,            // NI_Vector256_ConvertToUInt32
         HW_Category_Helper,            // NI_Vector256_ConvertToUInt32Native
         HW_Category_Helper,            // NI_Vector256_ConvertToUInt64
         HW_Category_Helper,            // NI_Vector256_ConvertToUInt64Native
         HW_Category_Helper,            // NI_Vector256_Create
         HW_Category_SIMDScalar,        // NI_Vector256_CreateScalar
         HW_Category_SIMDScalar,        // NI_Vector256_CreateScalarUnsafe
         HW_Category_Helper,            // NI_Vector256_CreateSequence
         HW_Category_Helper,            // NI_Vector256_Dot
         HW_Category_Helper,            // NI_Vector256_Equals
         HW_Category_Helper,            // NI_Vector256_EqualsAny
         HW_Category_Helper,            // NI_Vector256_ExtractMostSignificantBits
         HW_Category_Helper,            // NI_Vector256_Floor
         HW_Category_Helper,            // NI_Vector256_FusedMultiplyAdd
         HW_Category_Helper,            // NI_Vector256_GetElement
         HW_Category_SimpleSIMD,        // NI_Vector256_GetLower
         HW_Category_Helper,            // NI_Vector256_GetUpper
         HW_Category_Helper,            // NI_Vector256_GreaterThan
         HW_Category_Helper,            // NI_Vector256_GreaterThanAll
         HW_Category_Helper,            // NI_Vector256_GreaterThanAny
         HW_Category_Helper,            // NI_Vector256_GreaterThanOrEqual
         HW_Category_Helper,            // NI_Vector256_GreaterThanOrEqualAll
         HW_Category_Helper,            // NI_Vector256_GreaterThanOrEqualAny
         HW_Category_Helper,            // NI_Vector256_IsEvenInteger
         HW_Category_Helper,            // NI_Vector256_IsFinite
         HW_Category_Helper,            // NI_Vector256_IsInfinity
         HW_Category_Helper,            // NI_Vector256_IsInteger
         HW_Category_Helper,            // NI_Vector256_IsNaN
         HW_Category_Helper,            // NI_Vector256_IsNegative
         HW_Category_Helper,            // NI_Vector256_IsNegativeInfinity
         HW_Category_Helper,            // NI_Vector256_IsNormal
         HW_Category_Helper,            // NI_Vector256_IsOddInteger
         HW_Category_Helper,            // NI_Vector256_IsPositive
         HW_Category_Helper,            // NI_Vector256_IsPositiveInfinity
         HW_Category_Helper,            // NI_Vector256_IsSubnormal
         HW_Category_Helper,            // NI_Vector256_IsZero
         HW_Category_Helper,            // NI_Vector256_LessThan
         HW_Category_Helper,            // NI_Vector256_LessThanAll
         HW_Category_Helper,            // NI_Vector256_LessThanAny
         HW_Category_Helper,            // NI_Vector256_LessThanOrEqual
         HW_Category_Helper,            // NI_Vector256_LessThanOrEqualAll
         HW_Category_Helper,            // NI_Vector256_LessThanOrEqualAny
         HW_Category_Helper,            // NI_Vector256_LoadAligned
         HW_Category_Helper,            // NI_Vector256_LoadAlignedNonTemporal
         HW_Category_Helper,            // NI_Vector256_LoadUnsafe
         HW_Category_Helper,            // NI_Vector256_Max
         HW_Category_Helper,            // NI_Vector256_MaxMagnitude
         HW_Category_Helper,            // NI_Vector256_MaxMagnitudeNumber
         HW_Category_Helper,            // NI_Vector256_MaxNative
         HW_Category_Helper,            // NI_Vector256_MaxNumber
         HW_Category_Helper,            // NI_Vector256_Min
         HW_Category_Helper,            // NI_Vector256_MinMagnitude
         HW_Category_Helper,            // NI_Vector256_MinMagnitudeNumber
         HW_Category_Helper,            // NI_Vector256_MinNative
         HW_Category_Helper,            // NI_Vector256_MinNumber
         HW_Category_Helper,            // NI_Vector256_MultiplyAddEstimate
         HW_Category_Helper,            // NI_Vector256_Narrow
         HW_Category_Helper,            // NI_Vector256_NarrowWithSaturation
         HW_Category_Helper,            // NI_Vector256_Round
         HW_Category_Helper,            // NI_Vector256_ShiftLeft
         HW_Category_Helper,            // NI_Vector256_Shuffle
         HW_Category_Helper,            // NI_Vector256_ShuffleNative
         HW_Category_Helper,            // NI_Vector256_ShuffleNativeFallback
         HW_Category_Helper,            // NI_Vector256_Sqrt
         HW_Category_Helper,            // NI_Vector256_StoreAligned
         HW_Category_Helper,            // NI_Vector256_StoreAlignedNonTemporal
         HW_Category_Helper,            // NI_Vector256_StoreUnsafe
         HW_Category_Helper,            // NI_Vector256_SubtractSaturate
         HW_Category_Helper,            // NI_Vector256_Sum
         HW_Category_SIMDScalar,        // NI_Vector256_ToScalar
         HW_Category_SimpleSIMD,        // NI_Vector256_ToVector512
         HW_Category_SimpleSIMD,        // NI_Vector256_ToVector512Unsafe
         HW_Category_Helper,            // NI_Vector256_Truncate
         HW_Category_Helper,            // NI_Vector256_WidenLower
         HW_Category_Helper,            // NI_Vector256_WidenUpper
         HW_Category_Helper,            // NI_Vector256_WithElement
         HW_Category_Helper,            // NI_Vector256_WithLower
         HW_Category_Helper,            // NI_Vector256_WithUpper
         HW_Category_Helper,            // NI_Vector256_get_AllBitsSet
         HW_Category_Helper,            // NI_Vector256_get_E
         HW_Category_Helper,            // NI_Vector256_get_Epsilon
         HW_Category_Helper,            // NI_Vector256_get_Indices
         HW_Category_Helper,            // NI_Vector256_get_NaN
         HW_Category_Helper,            // NI_Vector256_get_NegativeInfinity
         HW_Category_Helper,            // NI_Vector256_get_NegativeOne
         HW_Category_Helper,            // NI_Vector256_get_NegativeZero
         HW_Category_Helper,            // NI_Vector256_get_One
         HW_Category_Helper,            // NI_Vector256_get_Pi
         HW_Category_Helper,            // NI_Vector256_get_PositiveInfinity
         HW_Category_Helper,            // NI_Vector256_get_Tau
         HW_Category_Helper,            // NI_Vector256_get_Zero
         HW_Category_Helper,            // NI_Vector256_op_Addition
         HW_Category_Helper,            // NI_Vector256_op_BitwiseAnd
         HW_Category_Helper,            // NI_Vector256_op_BitwiseOr
         HW_Category_Helper,            // NI_Vector256_op_Division
         HW_Category_Helper,            // NI_Vector256_op_Equality
         HW_Category_Helper,            // NI_Vector256_op_ExclusiveOr
         HW_Category_Helper,            // NI_Vector256_op_Inequality
         HW_Category_Helper,            // NI_Vector256_op_LeftShift
         HW_Category_Helper,            // NI_Vector256_op_Multiply
         HW_Category_Helper,            // NI_Vector256_op_OnesComplement
         HW_Category_Helper,            // NI_Vector256_op_RightShift
         HW_Category_Helper,            // NI_Vector256_op_Subtraction
         HW_Category_Helper,            // NI_Vector256_op_UnaryNegation
         HW_Category_Helper,            // NI_Vector256_op_UnaryPlus
         HW_Category_Helper,            // NI_Vector256_op_UnsignedRightShift
         HW_Category_Helper,            // NI_Vector512_Abs
         HW_Category_Helper,            // NI_Vector512_AddSaturate
         HW_Category_Helper,            // NI_Vector512_AndNot
         HW_Category_Helper,            // NI_Vector512_As
         HW_Category_Helper,            // NI_Vector512_AsByte
         HW_Category_Helper,            // NI_Vector512_AsDouble
         HW_Category_Helper,            // NI_Vector512_AsInt16
         HW_Category_Helper,            // NI_Vector512_AsInt32
         HW_Category_Helper,            // NI_Vector512_AsInt64
         HW_Category_Helper,            // NI_Vector512_AsNInt
         HW_Category_Helper,            // NI_Vector512_AsNUInt
         HW_Category_Helper,            // NI_Vector512_AsSByte
         HW_Category_Helper,            // NI_Vector512_AsSingle
         HW_Category_Helper,            // NI_Vector512_AsUInt16
         HW_Category_Helper,            // NI_Vector512_AsUInt32
         HW_Category_Helper,            // NI_Vector512_AsUInt64
         HW_Category_Helper,            // NI_Vector512_AsVector
         HW_Category_Helper,            // NI_Vector512_AsVector512
         HW_Category_Helper,            // NI_Vector512_Ceiling
         HW_Category_Helper,            // NI_Vector512_ConditionalSelect
         HW_Category_Helper,            // NI_Vector512_ConvertToDouble
         HW_Category_Helper,            // NI_Vector512_ConvertToInt32
         HW_Category_Helper,            // NI_Vector512_ConvertToInt32Native
         HW_Category_Helper,            // NI_Vector512_ConvertToInt64
         HW_Category_Helper,            // NI_Vector512_ConvertToInt64Native
         HW_Category_Helper,            // NI_Vector512_ConvertToSingle
         HW_Category_Helper,            // NI_Vector512_ConvertToUInt32
         HW_Category_Helper,            // NI_Vector512_ConvertToUInt32Native
         HW_Category_Helper,            // NI_Vector512_ConvertToUInt64
         HW_Category_Helper,            // NI_Vector512_ConvertToUInt64Native
         HW_Category_Helper,            // NI_Vector512_Create
         HW_Category_SIMDScalar,        // NI_Vector512_CreateScalar
         HW_Category_SIMDScalar,        // NI_Vector512_CreateScalarUnsafe
         HW_Category_Helper,            // NI_Vector512_CreateSequence
         HW_Category_Helper,            // NI_Vector512_Dot
         HW_Category_Helper,            // NI_Vector512_Equals
         HW_Category_Helper,            // NI_Vector512_EqualsAny
         HW_Category_Helper,            // NI_Vector512_ExtractMostSignificantBits
         HW_Category_Helper,            // NI_Vector512_Floor
         HW_Category_Helper,            // NI_Vector512_FusedMultiplyAdd
         HW_Category_Helper,            // NI_Vector512_GetElement
         HW_Category_SimpleSIMD,        // NI_Vector512_GetLower
         HW_Category_SimpleSIMD,        // NI_Vector512_GetLower128
         HW_Category_Helper,            // NI_Vector512_GetUpper
         HW_Category_Helper,            // NI_Vector512_GreaterThan
         HW_Category_Helper,            // NI_Vector512_GreaterThanAll
         HW_Category_Helper,            // NI_Vector512_GreaterThanAny
         HW_Category_Helper,            // NI_Vector512_GreaterThanOrEqual
         HW_Category_Helper,            // NI_Vector512_GreaterThanOrEqualAll
         HW_Category_Helper,            // NI_Vector512_GreaterThanOrEqualAny
         HW_Category_Helper,            // NI_Vector512_IsEvenInteger
         HW_Category_Helper,            // NI_Vector512_IsFinite
         HW_Category_Helper,            // NI_Vector512_IsInfinity
         HW_Category_Helper,            // NI_Vector512_IsInteger
         HW_Category_Helper,            // NI_Vector512_IsNaN
         HW_Category_Helper,            // NI_Vector512_IsNegative
         HW_Category_Helper,            // NI_Vector512_IsNegativeInfinity
         HW_Category_Helper,            // NI_Vector512_IsNormal
         HW_Category_Helper,            // NI_Vector512_IsOddInteger
         HW_Category_Helper,            // NI_Vector512_IsPositive
         HW_Category_Helper,            // NI_Vector512_IsPositiveInfinity
         HW_Category_Helper,            // NI_Vector512_IsSubnormal
         HW_Category_Helper,            // NI_Vector512_IsZero
         HW_Category_Helper,            // NI_Vector512_LessThan
         HW_Category_Helper,            // NI_Vector512_LessThanAll
         HW_Category_Helper,            // NI_Vector512_LessThanAny
         HW_Category_Helper,            // NI_Vector512_LessThanOrEqual
         HW_Category_Helper,            // NI_Vector512_LessThanOrEqualAll
         HW_Category_Helper,            // NI_Vector512_LessThanOrEqualAny
         HW_Category_Helper,            // NI_Vector512_LoadAligned
         HW_Category_Helper,            // NI_Vector512_LoadAlignedNonTemporal
         HW_Category_Helper,            // NI_Vector512_LoadUnsafe
         HW_Category_Helper,            // NI_Vector512_Max
         HW_Category_Helper,            // NI_Vector512_MaxMagnitude
         HW_Category_Helper,            // NI_Vector512_MaxMagnitudeNumber
         HW_Category_Helper,            // NI_Vector512_MaxNative
         HW_Category_Helper,            // NI_Vector512_MaxNumber
         HW_Category_Helper,            // NI_Vector512_Min
         HW_Category_Helper,            // NI_Vector512_MinMagnitude
         HW_Category_Helper,            // NI_Vector512_MinMagnitudeNumber
         HW_Category_Helper,            // NI_Vector512_MinNative
         HW_Category_Helper,            // NI_Vector512_MinNumber
         HW_Category_Helper,            // NI_Vector512_MultiplyAddEstimate
         HW_Category_Helper,            // NI_Vector512_Narrow
         HW_Category_Helper,            // NI_Vector512_NarrowWithSaturation
         HW_Category_Helper,            // NI_Vector512_Round
         HW_Category_Helper,            // NI_Vector512_ShiftLeft
         HW_Category_Helper,            // NI_Vector512_Shuffle
         HW_Category_Helper,            // NI_Vector512_ShuffleNative
         HW_Category_Helper,            // NI_Vector512_ShuffleNativeFallback
         HW_Category_Helper,            // NI_Vector512_Sqrt
         HW_Category_Helper,            // NI_Vector512_StoreAligned
         HW_Category_Helper,            // NI_Vector512_StoreAlignedNonTemporal
         HW_Category_Helper,            // NI_Vector512_StoreUnsafe
         HW_Category_Helper,            // NI_Vector512_SubtractSaturate
         HW_Category_Helper,            // NI_Vector512_Sum
         HW_Category_SIMDScalar,        // NI_Vector512_ToScalar
         HW_Category_Helper,            // NI_Vector512_Truncate
         HW_Category_Helper,            // NI_Vector512_WidenLower
         HW_Category_Helper,            // NI_Vector512_WidenUpper
         HW_Category_Helper,            // NI_Vector512_WithElement
         HW_Category_Helper,            // NI_Vector512_WithLower
         HW_Category_Helper,            // NI_Vector512_WithUpper
         HW_Category_Helper,            // NI_Vector512_get_AllBitsSet
         HW_Category_Helper,            // NI_Vector512_get_E
         HW_Category_Helper,            // NI_Vector512_get_Epsilon
         HW_Category_Helper,            // NI_Vector512_get_Indices
         HW_Category_Helper,            // NI_Vector512_get_NaN
         HW_Category_Helper,            // NI_Vector512_get_NegativeInfinity
         HW_Category_Helper,            // NI_Vector512_get_NegativeOne
         HW_Category_Helper,            // NI_Vector512_get_NegativeZero
         HW_Category_Helper,            // NI_Vector512_get_One
         HW_Category_Helper,            // NI_Vector512_get_Pi
         HW_Category_Helper,            // NI_Vector512_get_PositiveInfinity
         HW_Category_Helper,            // NI_Vector512_get_Tau
         HW_Category_Helper,            // NI_Vector512_get_Zero
         HW_Category_Helper,            // NI_Vector512_op_Addition
         HW_Category_Helper,            // NI_Vector512_op_BitwiseAnd
         HW_Category_Helper,            // NI_Vector512_op_BitwiseOr
         HW_Category_Helper,            // NI_Vector512_op_Division
         HW_Category_Helper,            // NI_Vector512_op_Equality
         HW_Category_Helper,            // NI_Vector512_op_ExclusiveOr
         HW_Category_Helper,            // NI_Vector512_op_Inequality
         HW_Category_Helper,            // NI_Vector512_op_LeftShift
         HW_Category_Helper,            // NI_Vector512_op_Multiply
         HW_Category_Helper,            // NI_Vector512_op_OnesComplement
         HW_Category_Helper,            // NI_Vector512_op_RightShift
         HW_Category_Helper,            // NI_Vector512_op_Subtraction
         HW_Category_Helper,            // NI_Vector512_op_UnaryNegation
         HW_Category_Helper,            // NI_Vector512_op_UnaryPlus
         HW_Category_Helper,            // NI_Vector512_op_UnsignedRightShift
        HW_Category_SimpleSIMD,         // NI_X86Base_Abs
        HW_Category_SimpleSIMD,         // NI_X86Base_Add
        HW_Category_SimpleSIMD,         // NI_X86Base_AddSaturate
        HW_Category_SIMDScalar,         // NI_X86Base_AddScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_AddSubtract
        HW_Category_IMM,                // NI_X86Base_AlignRight
        HW_Category_SimpleSIMD,         // NI_X86Base_And
        HW_Category_SimpleSIMD,         // NI_X86Base_AndNot
        HW_Category_SimpleSIMD,         // NI_X86Base_Average
        HW_Category_Scalar,             // NI_X86Base_BitScanForward
        HW_Category_Scalar,             // NI_X86Base_BitScanReverse
        HW_Category_IMM,                // NI_X86Base_Blend
        HW_Category_SimpleSIMD,         // NI_X86Base_BlendVariable
        HW_Category_SimpleSIMD,         // NI_X86Base_Ceiling
        HW_Category_SIMDScalar,         // NI_X86Base_CeilingScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareGreaterThan
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareGreaterThanOrEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareLessThan
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareLessThanOrEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareNotEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareNotGreaterThan
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareNotGreaterThanOrEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareNotLessThan
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareNotLessThanOrEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareOrdered
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarGreaterThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarGreaterThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarLessThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarLessThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarNotEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarNotGreaterThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarNotGreaterThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarNotLessThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarNotLessThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrdered
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrderedEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrderedGreaterThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrderedGreaterThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrderedLessThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrderedLessThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarOrderedNotEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnordered
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnorderedEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnorderedGreaterThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnorderedLessThan
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnorderedLessThanOrEqual
        HW_Category_SIMDScalar,         // NI_X86Base_CompareScalarUnorderedNotEqual
        HW_Category_SimpleSIMD,         // NI_X86Base_CompareUnordered
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertScalarToVector128Double
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertScalarToVector128Int32
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertScalarToVector128Single
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertScalarToVector128UInt32
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertToInt32
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertToInt32WithTruncation
        HW_Category_SIMDScalar,         // NI_X86Base_ConvertToUInt32
        HW_Category_SimpleSIMD,         // NI_X86Base_ConvertToVector128Double
        HW_Category_SimpleSIMD,         // NI_X86Base_ConvertToVector128Int16
        HW_Category_SimpleSIMD,         // NI_X86Base_ConvertToVector128Int32
        HW_Category_SimpleSIMD,         // NI_X86Base_ConvertToVector128Int32WithTruncation
        HW_Category_SimpleSIMD,         // NI_X86Base_ConvertToVector128Int64
        HW_Category_SimpleSIMD,         // NI_X86Base_ConvertToVector128Single
        HW_Category_Scalar,             // NI_X86Base_Crc32
        HW_Category_Scalar,             // NI_X86Base_DivRem
        HW_Category_SimpleSIMD,         // NI_X86Base_Divide
        HW_Category_SIMDScalar,         // NI_X86Base_DivideScalar
        HW_Category_IMM,                // NI_X86Base_DotProduct
        HW_Category_IMM,                // NI_X86Base_Extract
        HW_Category_SimpleSIMD,         // NI_X86Base_Floor
        HW_Category_SIMDScalar,         // NI_X86Base_FloorScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_HorizontalAdd
        HW_Category_SimpleSIMD,         // NI_X86Base_HorizontalAddSaturate
        HW_Category_SimpleSIMD,         // NI_X86Base_HorizontalSubtract
        HW_Category_SimpleSIMD,         // NI_X86Base_HorizontalSubtractSaturate
        HW_Category_IMM,                // NI_X86Base_Insert
        HW_Category_MemoryLoad,         // NI_X86Base_LoadAlignedVector128
        HW_Category_MemoryLoad,         // NI_X86Base_LoadAlignedVector128NonTemporal
        HW_Category_MemoryLoad,         // NI_X86Base_LoadAndDuplicateToVector128
        HW_Category_MemoryLoad,         // NI_X86Base_LoadDquVector128
        HW_Category_Special,            // NI_X86Base_LoadFence
        HW_Category_MemoryLoad,         // NI_X86Base_LoadHigh
        HW_Category_MemoryLoad,         // NI_X86Base_LoadLow
        HW_Category_MemoryLoad,         // NI_X86Base_LoadScalarVector128
        HW_Category_Helper,             // NI_X86Base_LoadVector128
        HW_Category_MemoryStore,        // NI_X86Base_MaskMove
        HW_Category_SimpleSIMD,         // NI_X86Base_Max
        HW_Category_SIMDScalar,         // NI_X86Base_MaxScalar
        HW_Category_Special,            // NI_X86Base_MemoryFence
        HW_Category_SimpleSIMD,         // NI_X86Base_Min
        HW_Category_SimpleSIMD,         // NI_X86Base_MinHorizontal
        HW_Category_SIMDScalar,         // NI_X86Base_MinScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_MoveAndDuplicate
        HW_Category_SimpleSIMD,         // NI_X86Base_MoveHighAndDuplicate
        HW_Category_SimpleSIMD,         // NI_X86Base_MoveHighToLow
        HW_Category_SimpleSIMD,         // NI_X86Base_MoveLowAndDuplicate
        HW_Category_SimpleSIMD,         // NI_X86Base_MoveLowToHigh
        HW_Category_SimpleSIMD,         // NI_X86Base_MoveMask
        HW_Category_SIMDScalar,         // NI_X86Base_MoveScalar
        HW_Category_IMM,                // NI_X86Base_MultipleSumAbsoluteDifferences
        HW_Category_SimpleSIMD,         // NI_X86Base_Multiply
        HW_Category_SimpleSIMD,         // NI_X86Base_MultiplyAddAdjacent
        HW_Category_SimpleSIMD,         // NI_X86Base_MultiplyHigh
        HW_Category_SimpleSIMD,         // NI_X86Base_MultiplyHighRoundScale
        HW_Category_SimpleSIMD,         // NI_X86Base_MultiplyLow
        HW_Category_SIMDScalar,         // NI_X86Base_MultiplyScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_Or
        HW_Category_SimpleSIMD,         // NI_X86Base_PackSignedSaturate
        HW_Category_SimpleSIMD,         // NI_X86Base_PackUnsignedSaturate
        HW_Category_Special,            // NI_X86Base_Pause
        HW_Category_Scalar,             // NI_X86Base_PopCount
        HW_Category_Special,            // NI_X86Base_Prefetch0
        HW_Category_Special,            // NI_X86Base_Prefetch1
        HW_Category_Special,            // NI_X86Base_Prefetch2
        HW_Category_Special,            // NI_X86Base_PrefetchNonTemporal
        HW_Category_SimpleSIMD,         // NI_X86Base_Reciprocal
        HW_Category_SIMDScalar,         // NI_X86Base_ReciprocalScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_ReciprocalSqrt
        HW_Category_SIMDScalar,         // NI_X86Base_ReciprocalSqrtScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_RoundCurrentDirection
        HW_Category_SIMDScalar,         // NI_X86Base_RoundCurrentDirectionScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_RoundToNearestInteger
        HW_Category_SIMDScalar,         // NI_X86Base_RoundToNearestIntegerScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_RoundToNegativeInfinity
        HW_Category_SIMDScalar,         // NI_X86Base_RoundToNegativeInfinityScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_RoundToPositiveInfinity
        HW_Category_SIMDScalar,         // NI_X86Base_RoundToPositiveInfinityScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_RoundToZero
        HW_Category_SIMDScalar,         // NI_X86Base_RoundToZeroScalar
        HW_Category_IMM,                // NI_X86Base_ShiftLeftLogical
        HW_Category_IMM,                // NI_X86Base_ShiftLeftLogical128BitLane
        HW_Category_IMM,                // NI_X86Base_ShiftRightArithmetic
        HW_Category_IMM,                // NI_X86Base_ShiftRightLogical
        HW_Category_IMM,                // NI_X86Base_ShiftRightLogical128BitLane
        HW_Category_IMM,                // NI_X86Base_Shuffle
        HW_Category_IMM,                // NI_X86Base_ShuffleHigh
        HW_Category_IMM,                // NI_X86Base_ShuffleLow
        HW_Category_SimpleSIMD,         // NI_X86Base_Sign
        HW_Category_SimpleSIMD,         // NI_X86Base_Sqrt
        HW_Category_SIMDScalar,         // NI_X86Base_SqrtScalar
        HW_Category_Helper,             // NI_X86Base_Store
        HW_Category_MemoryStore,        // NI_X86Base_StoreAligned
        HW_Category_MemoryStore,        // NI_X86Base_StoreAlignedNonTemporal
        HW_Category_Special,            // NI_X86Base_StoreFence
        HW_Category_MemoryStore,        // NI_X86Base_StoreHigh
        HW_Category_MemoryStore,        // NI_X86Base_StoreLow
        HW_Category_MemoryStore,        // NI_X86Base_StoreNonTemporal
        HW_Category_MemoryStore,        // NI_X86Base_StoreScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_Subtract
        HW_Category_SimpleSIMD,         // NI_X86Base_SubtractSaturate
        HW_Category_SIMDScalar,         // NI_X86Base_SubtractScalar
        HW_Category_SimpleSIMD,         // NI_X86Base_SumAbsoluteDifferences
        HW_Category_SimpleSIMD,         // NI_X86Base_TestC
        HW_Category_SimpleSIMD,         // NI_X86Base_TestNotZAndNotC
        HW_Category_SimpleSIMD,         // NI_X86Base_TestZ
        HW_Category_SimpleSIMD,         // NI_X86Base_UnpackHigh
        HW_Category_SimpleSIMD,         // NI_X86Base_UnpackLow
        HW_Category_SimpleSIMD,         // NI_X86Base_Xor
        HW_Category_Scalar,             // NI_X86Base_X64_BigMul
        HW_Category_Scalar,             // NI_X86Base_X64_BitScanForward
        HW_Category_Scalar,             // NI_X86Base_X64_BitScanReverse
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertScalarToVector128Double
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertScalarToVector128Int64
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertScalarToVector128Single
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertScalarToVector128UInt64
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertToInt64
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertToInt64WithTruncation
        HW_Category_SIMDScalar,         // NI_X86Base_X64_ConvertToUInt64
        HW_Category_Scalar,             // NI_X86Base_X64_Crc32
        HW_Category_Scalar,             // NI_X86Base_X64_DivRem
        HW_Category_IMM,                // NI_X86Base_X64_Extract
        HW_Category_IMM,                // NI_X86Base_X64_Insert
        HW_Category_Scalar,             // NI_X86Base_X64_PopCount
        HW_Category_MemoryStore,        // NI_X86Base_X64_StoreNonTemporal
        HW_Category_SimpleSIMD,         // NI_AVX_Add
        HW_Category_SimpleSIMD,         // NI_AVX_AddSubtract
        HW_Category_SimpleSIMD,         // NI_AVX_And
        HW_Category_SimpleSIMD,         // NI_AVX_AndNot
        HW_Category_IMM,                // NI_AVX_Blend
        HW_Category_SimpleSIMD,         // NI_AVX_BlendVariable
        HW_Category_MemoryLoad,         // NI_AVX_BroadcastScalarToVector128
        HW_Category_MemoryLoad,         // NI_AVX_BroadcastScalarToVector256
        HW_Category_MemoryLoad,         // NI_AVX_BroadcastVector128ToVector256
        HW_Category_SimpleSIMD,         // NI_AVX_Ceiling
        HW_Category_IMM,                // NI_AVX_Compare
        HW_Category_SimpleSIMD,         // NI_AVX_CompareEqual
        HW_Category_SimpleSIMD,         // NI_AVX_CompareGreaterThan
        HW_Category_SimpleSIMD,         // NI_AVX_CompareGreaterThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX_CompareLessThan
        HW_Category_SimpleSIMD,         // NI_AVX_CompareLessThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX_CompareNotEqual
        HW_Category_SimpleSIMD,         // NI_AVX_CompareNotGreaterThan
        HW_Category_SimpleSIMD,         // NI_AVX_CompareNotGreaterThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX_CompareNotLessThan
        HW_Category_SimpleSIMD,         // NI_AVX_CompareNotLessThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX_CompareOrdered
        HW_Category_IMM,                // NI_AVX_CompareScalar
        HW_Category_SimpleSIMD,         // NI_AVX_CompareUnordered
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector128Int32
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector128Int32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector128Single
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector256Double
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector256Int32
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector256Int32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX_ConvertToVector256Single
        HW_Category_SimpleSIMD,         // NI_AVX_Divide
        HW_Category_IMM,                // NI_AVX_DotProduct
        HW_Category_SimpleSIMD,         // NI_AVX_DuplicateEvenIndexed
        HW_Category_SimpleSIMD,         // NI_AVX_DuplicateOddIndexed
        HW_Category_IMM,                // NI_AVX_ExtractVector128
        HW_Category_SimpleSIMD,         // NI_AVX_Floor
        HW_Category_SimpleSIMD,         // NI_AVX_HorizontalAdd
        HW_Category_SimpleSIMD,         // NI_AVX_HorizontalSubtract
        HW_Category_IMM,                // NI_AVX_InsertVector128
        HW_Category_MemoryLoad,         // NI_AVX_LoadAlignedVector256
        HW_Category_MemoryLoad,         // NI_AVX_LoadDquVector256
        HW_Category_Helper,             // NI_AVX_LoadVector256
        HW_Category_MemoryLoad,         // NI_AVX_MaskLoad
        HW_Category_MemoryStore,        // NI_AVX_MaskStore
        HW_Category_SimpleSIMD,         // NI_AVX_Max
        HW_Category_SimpleSIMD,         // NI_AVX_Min
        HW_Category_SimpleSIMD,         // NI_AVX_MoveMask
        HW_Category_SimpleSIMD,         // NI_AVX_Multiply
        HW_Category_SimpleSIMD,         // NI_AVX_Or
        HW_Category_IMM,                // NI_AVX_Permute
        HW_Category_IMM,                // NI_AVX_Permute2x128
        HW_Category_SimpleSIMD,         // NI_AVX_PermuteVar
        HW_Category_SimpleSIMD,         // NI_AVX_Reciprocal
        HW_Category_SimpleSIMD,         // NI_AVX_ReciprocalSqrt
        HW_Category_SimpleSIMD,         // NI_AVX_RoundCurrentDirection
        HW_Category_SimpleSIMD,         // NI_AVX_RoundToNearestInteger
        HW_Category_SimpleSIMD,         // NI_AVX_RoundToNegativeInfinity
        HW_Category_SimpleSIMD,         // NI_AVX_RoundToPositiveInfinity
        HW_Category_SimpleSIMD,         // NI_AVX_RoundToZero
        HW_Category_IMM,                // NI_AVX_Shuffle
        HW_Category_SimpleSIMD,         // NI_AVX_Sqrt
        HW_Category_Helper,             // NI_AVX_Store
        HW_Category_MemoryStore,        // NI_AVX_StoreAligned
        HW_Category_MemoryStore,        // NI_AVX_StoreAlignedNonTemporal
        HW_Category_SimpleSIMD,         // NI_AVX_Subtract
        HW_Category_SimpleSIMD,         // NI_AVX_TestC
        HW_Category_SimpleSIMD,         // NI_AVX_TestNotZAndNotC
        HW_Category_SimpleSIMD,         // NI_AVX_TestZ
        HW_Category_SimpleSIMD,         // NI_AVX_UnpackHigh
        HW_Category_SimpleSIMD,         // NI_AVX_UnpackLow
        HW_Category_SimpleSIMD,         // NI_AVX_Xor
        HW_Category_SimpleSIMD,         // NI_AVX2_Abs
        HW_Category_SimpleSIMD,         // NI_AVX2_Add
        HW_Category_SimpleSIMD,         // NI_AVX2_AddSaturate
        HW_Category_IMM,                // NI_AVX2_AlignRight
        HW_Category_SimpleSIMD,         // NI_AVX2_And
        HW_Category_Special,            // NI_AVX2_AndNot
        HW_Category_SimpleSIMD,         // NI_AVX2_Average
        HW_Category_Scalar,             // NI_AVX2_BitFieldExtract
        HW_Category_IMM,                // NI_AVX2_Blend
        HW_Category_SimpleSIMD,         // NI_AVX2_BlendVariable
        HW_Category_SIMDScalar,         // NI_AVX2_BroadcastScalarToVector128
        HW_Category_SIMDScalar,         // NI_AVX2_BroadcastScalarToVector256
        HW_Category_MemoryLoad,         // NI_AVX2_BroadcastVector128ToVector256
        HW_Category_SimpleSIMD,         // NI_AVX2_CompareEqual
        HW_Category_SimpleSIMD,         // NI_AVX2_CompareGreaterThan
        HW_Category_SimpleSIMD,         // NI_AVX2_CompareLessThan
        HW_Category_SIMDScalar,         // NI_AVX2_ConvertToInt32
        HW_Category_SIMDScalar,         // NI_AVX2_ConvertToUInt32
        HW_Category_IMM,                // NI_AVX2_ConvertToVector128Half
        HW_Category_SimpleSIMD,         // NI_AVX2_ConvertToVector128Single
        HW_Category_IMM,                // NI_AVX2_ConvertToVector256Half
        HW_Category_SimpleSIMD,         // NI_AVX2_ConvertToVector256Int16
        HW_Category_SimpleSIMD,         // NI_AVX2_ConvertToVector256Int32
        HW_Category_SimpleSIMD,         // NI_AVX2_ConvertToVector256Int64
        HW_Category_SimpleSIMD,         // NI_AVX2_ConvertToVector256Single
        HW_Category_Scalar,             // NI_AVX2_ExtractLowestSetBit
        HW_Category_IMM,                // NI_AVX2_ExtractVector128
        HW_Category_IMM,                // NI_AVX2_GatherMaskVector128
        HW_Category_IMM,                // NI_AVX2_GatherMaskVector256
        HW_Category_IMM,                // NI_AVX2_GatherVector128
        HW_Category_IMM,                // NI_AVX2_GatherVector256
        HW_Category_Scalar,             // NI_AVX2_GetMaskUpToLowestSetBit
        HW_Category_SimpleSIMD,         // NI_AVX2_HorizontalAdd
        HW_Category_SimpleSIMD,         // NI_AVX2_HorizontalAddSaturate
        HW_Category_SimpleSIMD,         // NI_AVX2_HorizontalSubtract
        HW_Category_SimpleSIMD,         // NI_AVX2_HorizontalSubtractSaturate
        HW_Category_IMM,                // NI_AVX2_InsertVector128
        HW_Category_Scalar,             // NI_AVX2_LeadingZeroCount
        HW_Category_MemoryLoad,         // NI_AVX2_LoadAlignedVector256NonTemporal
        HW_Category_MemoryLoad,         // NI_AVX2_MaskLoad
        HW_Category_MemoryStore,        // NI_AVX2_MaskStore
        HW_Category_SimpleSIMD,         // NI_AVX2_Max
        HW_Category_SimpleSIMD,         // NI_AVX2_Min
        HW_Category_SimpleSIMD,         // NI_AVX2_MoveMask
        HW_Category_IMM,                // NI_AVX2_MultipleSumAbsoluteDifferences
        HW_Category_SimpleSIMD,         // NI_AVX2_Multiply
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyAdd
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyAddAdjacent
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyAddNegated
        HW_Category_SIMDScalar,         // NI_AVX2_MultiplyAddNegatedScalar
        HW_Category_SIMDScalar,         // NI_AVX2_MultiplyAddScalar
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyAddSubtract
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyHigh
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyHighRoundScale
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplyLow
        HW_Category_Scalar,             // NI_AVX2_MultiplyNoFlags
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplySubtract
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplySubtractAdd
        HW_Category_SimpleSIMD,         // NI_AVX2_MultiplySubtractNegated
        HW_Category_SIMDScalar,         // NI_AVX2_MultiplySubtractNegatedScalar
        HW_Category_SIMDScalar,         // NI_AVX2_MultiplySubtractScalar
        HW_Category_SimpleSIMD,         // NI_AVX2_Or
        HW_Category_SimpleSIMD,         // NI_AVX2_PackSignedSaturate
        HW_Category_SimpleSIMD,         // NI_AVX2_PackUnsignedSaturate
        HW_Category_Scalar,             // NI_AVX2_ParallelBitDeposit
        HW_Category_Scalar,             // NI_AVX2_ParallelBitExtract
        HW_Category_IMM,                // NI_AVX2_Permute2x128
        HW_Category_IMM,                // NI_AVX2_Permute4x64
        HW_Category_SimpleSIMD,         // NI_AVX2_PermuteVar8x32
        HW_Category_Scalar,             // NI_AVX2_ResetLowestSetBit
        HW_Category_IMM,                // NI_AVX2_ShiftLeftLogical
        HW_Category_IMM,                // NI_AVX2_ShiftLeftLogical128BitLane
        HW_Category_SimpleSIMD,         // NI_AVX2_ShiftLeftLogicalVariable
        HW_Category_IMM,                // NI_AVX2_ShiftRightArithmetic
        HW_Category_SimpleSIMD,         // NI_AVX2_ShiftRightArithmeticVariable
        HW_Category_IMM,                // NI_AVX2_ShiftRightLogical
        HW_Category_IMM,                // NI_AVX2_ShiftRightLogical128BitLane
        HW_Category_SimpleSIMD,         // NI_AVX2_ShiftRightLogicalVariable
        HW_Category_IMM,                // NI_AVX2_Shuffle
        HW_Category_IMM,                // NI_AVX2_ShuffleHigh
        HW_Category_IMM,                // NI_AVX2_ShuffleLow
        HW_Category_SimpleSIMD,         // NI_AVX2_Sign
        HW_Category_SimpleSIMD,         // NI_AVX2_Subtract
        HW_Category_SimpleSIMD,         // NI_AVX2_SubtractSaturate
        HW_Category_SimpleSIMD,         // NI_AVX2_SumAbsoluteDifferences
        HW_Category_Scalar,             // NI_AVX2_TrailingZeroCount
        HW_Category_SimpleSIMD,         // NI_AVX2_UnpackHigh
        HW_Category_SimpleSIMD,         // NI_AVX2_UnpackLow
        HW_Category_SimpleSIMD,         // NI_AVX2_Xor
        HW_Category_Scalar,             // NI_AVX2_ZeroHighBits
        HW_Category_Scalar,             // NI_AVX2_X64_AndNot
        HW_Category_Scalar,             // NI_AVX2_X64_BitFieldExtract
        HW_Category_Scalar,             // NI_AVX2_X64_ExtractLowestSetBit
        HW_Category_Scalar,             // NI_AVX2_X64_GetMaskUpToLowestSetBit
        HW_Category_Scalar,             // NI_AVX2_X64_LeadingZeroCount
        HW_Category_Scalar,             // NI_AVX2_X64_MultiplyNoFlags
        HW_Category_Scalar,             // NI_AVX2_X64_ParallelBitDeposit
        HW_Category_Scalar,             // NI_AVX2_X64_ParallelBitExtract
        HW_Category_Scalar,             // NI_AVX2_X64_ResetLowestSetBit
        HW_Category_Scalar,             // NI_AVX2_X64_TrailingZeroCount
        HW_Category_Scalar,             // NI_AVX2_X64_ZeroHighBits
        HW_Category_SimpleSIMD,         // NI_AVX512_Abs
        HW_Category_SimpleSIMD,         // NI_AVX512_Add
        HW_Category_SimpleSIMD,         // NI_AVX512_AddSaturate
        HW_Category_SIMDScalar,         // NI_AVX512_AddScalar
        HW_Category_IMM,                // NI_AVX512_AlignRight
        HW_Category_IMM,                // NI_AVX512_AlignRight32
        HW_Category_IMM,                // NI_AVX512_AlignRight64
        HW_Category_SimpleSIMD,         // NI_AVX512_And
        HW_Category_SimpleSIMD,         // NI_AVX512_AndNot
        HW_Category_SimpleSIMD,         // NI_AVX512_Average
        HW_Category_SimpleSIMD,         // NI_AVX512_BlendVariable
        HW_Category_SimpleSIMD,         // NI_AVX512_BroadcastPairScalarToVector128
        HW_Category_SimpleSIMD,         // NI_AVX512_BroadcastPairScalarToVector256
        HW_Category_SimpleSIMD,         // NI_AVX512_BroadcastPairScalarToVector512
        HW_Category_SIMDScalar,         // NI_AVX512_BroadcastScalarToVector512
        HW_Category_MemoryLoad,         // NI_AVX512_BroadcastVector128ToVector512
        HW_Category_MemoryLoad,         // NI_AVX512_BroadcastVector256ToVector512
        HW_Category_IMM,                // NI_AVX512_Classify
        HW_Category_IMM,                // NI_AVX512_ClassifyScalar
        HW_Category_IMM,                // NI_AVX512_Compare
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareEqual
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareGreaterThan
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareGreaterThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareLessThan
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareLessThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotEqual
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotGreaterThan
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotGreaterThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotLessThan
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotLessThanOrEqual
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareOrdered
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareUnordered
        HW_Category_SimpleSIMD,         // NI_AVX512_Compress
        HW_Category_MemoryStore,        // NI_AVX512_CompressStore
        HW_Category_SIMDScalar,         // NI_AVX512_ConvertScalarToVector128Double
        HW_Category_SIMDScalar,         // NI_AVX512_ConvertScalarToVector128Single
        HW_Category_SIMDScalar,         // NI_AVX512_ConvertToInt32
        HW_Category_SIMDScalar,         // NI_AVX512_ConvertToUInt32
        HW_Category_SIMDScalar,         // NI_AVX512_ConvertToUInt32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Byte
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128ByteWithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Double
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Int16
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Int16WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Int32
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Int32WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Int64
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Int64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128SByte
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128SByteWithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128Single
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt16
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt16WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt32
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt32WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt64
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector128UInt64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Byte
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256ByteWithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Double
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int16
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int16WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int32
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int32WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int64
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Int64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256SByte
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256SByteWithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256Single
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt16
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt16WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt32
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt32WithSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt64
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector256UInt64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Double
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Int16
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Int32
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Int32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Int64
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Int64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512Single
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512UInt16
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512UInt32
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512UInt32WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512UInt64
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertToVector512UInt64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512_DetectConflicts
        HW_Category_SimpleSIMD,         // NI_AVX512_Divide
        HW_Category_SIMDScalar,         // NI_AVX512_DivideScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_DuplicateEvenIndexed
        HW_Category_SimpleSIMD,         // NI_AVX512_DuplicateOddIndexed
        HW_Category_SimpleSIMD,         // NI_AVX512_Expand
        HW_Category_MemoryLoad,         // NI_AVX512_ExpandLoad
        HW_Category_IMM,                // NI_AVX512_ExtractVector128
        HW_Category_IMM,                // NI_AVX512_ExtractVector256
        HW_Category_IMM,                // NI_AVX512_Fixup
        HW_Category_IMM,                // NI_AVX512_FixupScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_FusedMultiplyAdd
        HW_Category_SimpleSIMD,         // NI_AVX512_FusedMultiplyAddNegated
        HW_Category_SIMDScalar,         // NI_AVX512_FusedMultiplyAddNegatedScalar
        HW_Category_SIMDScalar,         // NI_AVX512_FusedMultiplyAddScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_FusedMultiplyAddSubtract
        HW_Category_SimpleSIMD,         // NI_AVX512_FusedMultiplySubtract
        HW_Category_SimpleSIMD,         // NI_AVX512_FusedMultiplySubtractAdd
        HW_Category_SimpleSIMD,         // NI_AVX512_FusedMultiplySubtractNegated
        HW_Category_SIMDScalar,         // NI_AVX512_FusedMultiplySubtractNegatedScalar
        HW_Category_SIMDScalar,         // NI_AVX512_FusedMultiplySubtractScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_GetExponent
        HW_Category_SIMDScalar,         // NI_AVX512_GetExponentScalar
        HW_Category_IMM,                // NI_AVX512_GetMantissa
        HW_Category_IMM,                // NI_AVX512_GetMantissaScalar
        HW_Category_IMM,                // NI_AVX512_InsertVector128
        HW_Category_IMM,                // NI_AVX512_InsertVector256
        HW_Category_SimpleSIMD,         // NI_AVX512_LeadingZeroCount
        HW_Category_MemoryLoad,         // NI_AVX512_LoadAlignedVector512
        HW_Category_MemoryLoad,         // NI_AVX512_LoadAlignedVector512NonTemporal
        HW_Category_Helper,             // NI_AVX512_LoadVector512
        HW_Category_MemoryLoad,         // NI_AVX512_MaskLoad
        HW_Category_MemoryLoad,         // NI_AVX512_MaskLoadAligned
        HW_Category_MemoryStore,        // NI_AVX512_MaskStore
        HW_Category_MemoryStore,        // NI_AVX512_MaskStoreAligned
        HW_Category_SimpleSIMD,         // NI_AVX512_Max
        HW_Category_SimpleSIMD,         // NI_AVX512_Min
        HW_Category_SimpleSIMD,         // NI_AVX512_MoveMask
        HW_Category_SimpleSIMD,         // NI_AVX512_Multiply
        HW_Category_SimpleSIMD,         // NI_AVX512_MultiplyAddAdjacent
        HW_Category_SimpleSIMD,         // NI_AVX512_MultiplyHigh
        HW_Category_SimpleSIMD,         // NI_AVX512_MultiplyHighRoundScale
        HW_Category_SimpleSIMD,         // NI_AVX512_MultiplyLow
        HW_Category_SIMDScalar,         // NI_AVX512_MultiplyScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_Or
        HW_Category_SimpleSIMD,         // NI_AVX512_PackSignedSaturate
        HW_Category_SimpleSIMD,         // NI_AVX512_PackUnsignedSaturate
        HW_Category_IMM,                // NI_AVX512_Permute2x64
        HW_Category_IMM,                // NI_AVX512_Permute4x32
        HW_Category_IMM,                // NI_AVX512_Permute4x64
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar16x16
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar16x16x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar16x32
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar16x32x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar2x64
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar2x64x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar32x16
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar32x16x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar4x32
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar4x32x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar4x64
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar4x64x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar8x16
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar8x16x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar8x32x2
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar8x64
        HW_Category_SimpleSIMD,         // NI_AVX512_PermuteVar8x64x2
        HW_Category_IMM,                // NI_AVX512_Range
        HW_Category_IMM,                // NI_AVX512_RangeScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_Reciprocal14
        HW_Category_SimpleSIMD,         // NI_AVX512_Reciprocal14Scalar
        HW_Category_SimpleSIMD,         // NI_AVX512_ReciprocalSqrt14
        HW_Category_SimpleSIMD,         // NI_AVX512_ReciprocalSqrt14Scalar
        HW_Category_IMM,                // NI_AVX512_Reduce
        HW_Category_IMM,                // NI_AVX512_ReduceScalar
        HW_Category_IMM,                // NI_AVX512_RotateLeft
        HW_Category_SimpleSIMD,         // NI_AVX512_RotateLeftVariable
        HW_Category_IMM,                // NI_AVX512_RotateRight
        HW_Category_SimpleSIMD,         // NI_AVX512_RotateRightVariable
        HW_Category_IMM,                // NI_AVX512_RoundScale
        HW_Category_IMM,                // NI_AVX512_RoundScaleScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_Scale
        HW_Category_SIMDScalar,         // NI_AVX512_ScaleScalar
        HW_Category_IMM,                // NI_AVX512_ShiftLeftLogical
        HW_Category_IMM,                // NI_AVX512_ShiftLeftLogical128BitLane
        HW_Category_SimpleSIMD,         // NI_AVX512_ShiftLeftLogicalVariable
        HW_Category_IMM,                // NI_AVX512_ShiftRightArithmetic
        HW_Category_SimpleSIMD,         // NI_AVX512_ShiftRightArithmeticVariable
        HW_Category_IMM,                // NI_AVX512_ShiftRightLogical
        HW_Category_IMM,                // NI_AVX512_ShiftRightLogical128BitLane
        HW_Category_SimpleSIMD,         // NI_AVX512_ShiftRightLogicalVariable
        HW_Category_IMM,                // NI_AVX512_Shuffle
        HW_Category_IMM,                // NI_AVX512_Shuffle2x128
        HW_Category_IMM,                // NI_AVX512_Shuffle4x128
        HW_Category_IMM,                // NI_AVX512_ShuffleHigh
        HW_Category_IMM,                // NI_AVX512_ShuffleLow
        HW_Category_SimpleSIMD,         // NI_AVX512_Sqrt
        HW_Category_SIMDScalar,         // NI_AVX512_SqrtScalar
        HW_Category_Helper,             // NI_AVX512_Store
        HW_Category_MemoryStore,        // NI_AVX512_StoreAligned
        HW_Category_MemoryStore,        // NI_AVX512_StoreAlignedNonTemporal
        HW_Category_SimpleSIMD,         // NI_AVX512_Subtract
        HW_Category_SimpleSIMD,         // NI_AVX512_SubtractSaturate
        HW_Category_SIMDScalar,         // NI_AVX512_SubtractScalar
        HW_Category_SimpleSIMD,         // NI_AVX512_SumAbsoluteDifferences
        HW_Category_IMM,                // NI_AVX512_SumAbsoluteDifferencesInBlock32
        HW_Category_IMM,                // NI_AVX512_TernaryLogic
        HW_Category_SimpleSIMD,         // NI_AVX512_UnpackHigh
        HW_Category_SimpleSIMD,         // NI_AVX512_UnpackLow
        HW_Category_SimpleSIMD,         // NI_AVX512_Xor
        HW_Category_SIMDScalar,         // NI_AVX512_X64_ConvertScalarToVector128Double
        HW_Category_SIMDScalar,         // NI_AVX512_X64_ConvertScalarToVector128Single
        HW_Category_SIMDScalar,         // NI_AVX512_X64_ConvertToInt64
        HW_Category_SIMDScalar,         // NI_AVX512_X64_ConvertToUInt64
        HW_Category_SIMDScalar,         // NI_AVX512_X64_ConvertToUInt64WithTruncation
        HW_Category_SimpleSIMD,         // NI_AVX512v2_MultiShift
        HW_Category_SimpleSIMD,         // NI_AVX512v2_PermuteVar16x8
        HW_Category_SimpleSIMD,         // NI_AVX512v2_PermuteVar16x8x2
        HW_Category_SimpleSIMD,         // NI_AVX512v2_PermuteVar32x8
        HW_Category_SimpleSIMD,         // NI_AVX512v2_PermuteVar32x8x2
        HW_Category_SimpleSIMD,         // NI_AVX512v2_PermuteVar64x8
        HW_Category_SimpleSIMD,         // NI_AVX512v2_PermuteVar64x8x2
        HW_Category_SimpleSIMD,         // NI_AVX512v3_Compress
        HW_Category_MemoryStore,        // NI_AVX512v3_CompressStore
        HW_Category_SimpleSIMD,         // NI_AVX512v3_Expand
        HW_Category_MemoryLoad,         // NI_AVX512v3_ExpandLoad
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32
        HW_Category_SIMDScalar,         // NI_AVX10v2_ConvertToInt32WithTruncatedSaturation
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32
        HW_Category_SIMDScalar,         // NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation
        HW_Category_SimpleSIMD,         // NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation
        HW_Category_IMM,                // NI_AVX10v2_MinMax
        HW_Category_IMM,                // NI_AVX10v2_MinMaxScalar
        HW_Category_SIMDScalar,         // NI_AVX10v2_MoveScalar
        HW_Category_IMM,                // NI_AVX10v2_MultipleSumAbsoluteDifferences
        HW_Category_MemoryStore,        // NI_AVX10v2_StoreScalar
        HW_Category_SIMDScalar,         // NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation
        HW_Category_SIMDScalar,         // NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation
        HW_Category_SimpleSIMD,         // NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction
        HW_Category_SimpleSIMD,         // NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction
        HW_Category_SimpleSIMD,         // NI_AVX512BMM_ReverseBits
        HW_Category_SimpleSIMD,         // NI_AVXVNNI_MultiplyWideningAndAdd
        HW_Category_SimpleSIMD,         // NI_AVXVNNI_MultiplyWideningAndAddSaturate
        HW_Category_SimpleSIMD,         // NI_AVXVNNIINT_MultiplyWideningAndAdd
        HW_Category_SimpleSIMD,         // NI_AVXVNNIINT_MultiplyWideningAndAddSaturate
        HW_Category_SimpleSIMD,         // NI_AVXVNNIINT_V512_MultiplyWideningAndAdd
        HW_Category_SimpleSIMD,         // NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate
        HW_Category_IMM,                // NI_AES_CarrylessMultiply
        HW_Category_SimpleSIMD,         // NI_AES_Decrypt
        HW_Category_SimpleSIMD,         // NI_AES_DecryptLast
        HW_Category_SimpleSIMD,         // NI_AES_Encrypt
        HW_Category_SimpleSIMD,         // NI_AES_EncryptLast
        HW_Category_SimpleSIMD,         // NI_AES_InverseMixColumns
        HW_Category_IMM,                // NI_AES_KeygenAssist
        HW_Category_IMM,                // NI_AES_V256_CarrylessMultiply
        HW_Category_IMM,                // NI_AES_V512_CarrylessMultiply
        HW_Category_Special,            // NI_X86Serialize_Serialize
        HW_Category_IMM,                // NI_GFNI_GaloisFieldAffineTransform
        HW_Category_IMM,                // NI_GFNI_GaloisFieldAffineTransformInverse
        HW_Category_SimpleSIMD,         // NI_GFNI_GaloisFieldMultiply
        HW_Category_IMM,                // NI_GFNI_V256_GaloisFieldAffineTransform
        HW_Category_IMM,                // NI_GFNI_V256_GaloisFieldAffineTransformInverse
        HW_Category_SimpleSIMD,         // NI_GFNI_V256_GaloisFieldMultiply
        HW_Category_IMM,                // NI_GFNI_V512_GaloisFieldAffineTransform
        HW_Category_IMM,                // NI_GFNI_V512_GaloisFieldAffineTransformInverse
        HW_Category_SimpleSIMD,         // NI_GFNI_V512_GaloisFieldMultiply
        HW_Category_SIMDScalar,         // NI_X86Base_COMIS
        HW_Category_SimpleSIMD,         // NI_X86Base_PTEST
        HW_Category_SIMDScalar,         // NI_X86Base_UCOMIS
        HW_Category_SimpleSIMD,         // NI_AVX_PTEST
        HW_Category_SimpleSIMD,         // NI_AVX2_AndNotVector
        HW_Category_Scalar,             // NI_AVX2_AndNotScalar
        HW_Category_Special,            // NI_AVX512_KORTEST
        HW_Category_Special,            // NI_AVX512_KTEST
        HW_Category_SimpleSIMD,         // NI_AVX512_PTESTM
        HW_Category_SimpleSIMD,         // NI_AVX512_PTESTNM
        HW_Category_SimpleSIMD,         // NI_AVX512_AddMask
        HW_Category_SimpleSIMD,         // NI_AVX512_AndMask
        HW_Category_SimpleSIMD,         // NI_AVX512_AndNotMask
        HW_Category_SimpleSIMD,         // NI_AVX512_BlendVariableMask
        HW_Category_IMM,                // NI_AVX512_ClassifyMask
        HW_Category_IMM,                // NI_AVX512_ClassifyScalarMask
        HW_Category_IMM,                // NI_AVX512_CompareMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareEqualMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareGreaterThanMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareGreaterThanOrEqualMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareLessThanMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareLessThanOrEqualMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotEqualMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotGreaterThanMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotGreaterThanOrEqualMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotLessThanMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareNotLessThanOrEqualMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareOrderedMask
        HW_Category_IMM,                // NI_AVX512_CompareScalarMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompareUnorderedMask
        HW_Category_SimpleSIMD,         // NI_AVX512_CompressMask
        HW_Category_MemoryStore,        // NI_AVX512_CompressStoreMask
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertMaskToVector
        HW_Category_SimpleSIMD,         // NI_AVX512_ConvertVectorToMask
        HW_Category_MemoryLoad,         // NI_AVX512_ExpandLoadMask
        HW_Category_SimpleSIMD,         // NI_AVX512_ExpandMask
        HW_Category_MemoryLoad,         // NI_AVX512_MaskLoadMask
        HW_Category_MemoryLoad,         // NI_AVX512_MaskLoadAlignedMask
        HW_Category_MemoryStore,        // NI_AVX512_MaskStoreMask
        HW_Category_MemoryStore,        // NI_AVX512_MaskStoreAlignedMask
        HW_Category_SimpleSIMD,         // NI_AVX512_NotMask
        HW_Category_SimpleSIMD,         // NI_AVX512_OrMask
        HW_Category_IMM,                // NI_AVX512_ShiftLeftMask
        HW_Category_IMM,                // NI_AVX512_ShiftRightMask
        HW_Category_SimpleSIMD,         // NI_AVX512_XorMask
        HW_Category_SimpleSIMD,         // NI_AVX512_XnorMask
    ];

    private static ReadOnlySpan<HWIntrinsicFlag> s_flags => [
          /* NI_Vector128_Abs                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_AddSaturate                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_AndNot                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_As                                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsByte                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsDouble                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsInt16                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsInt32                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsInt64                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsNInt                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsNUInt                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsSByte                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsSingle                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsUInt16                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsUInt32                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsUInt64                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsVector                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsVector128                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_AsVector128Unsafe                                         */      HW_Flag_SpecialImport | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics | HW_Flag_NoContainment,
          /* NI_Vector128_AsVector2                                                 */      HW_Flag_SpecialImport | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_Vector128_AsVector3                                                 */      HW_Flag_SpecialImport | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_Vector128_AsVector4                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_Ceiling                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_ConditionalSelect                                         */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector128_ConvertToDouble                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToInt32                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToInt32Native                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToInt64                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToInt64Native                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToSingle                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToUInt32                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToUInt32Native                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToUInt64                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ConvertToUInt64Native                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_Create                                                    */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector128_CreateScalar                                              */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_Vector128_CreateScalarUnsafe                                        */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_Vector128_CreateSequence                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_Dot                                                       */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_Equals                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_EqualsAny                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ExtractMostSignificantBits                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector128_Floor                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_FusedMultiplyAdd                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_GetElement                                                */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_GreaterThan                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_GreaterThanAll                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_GreaterThanAny                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_GreaterThanOrEqual                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_GreaterThanOrEqualAll                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_GreaterThanOrEqualAny                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_IsEvenInteger                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsFinite                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsInfinity                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsInteger                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsNaN                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsNegative                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsNegativeInfinity                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsNormal                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsOddInteger                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsPositive                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsPositiveInfinity                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsSubnormal                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_IsZero                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_LessThan                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_LessThanAll                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_LessThanAny                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_LessThanOrEqual                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_LessThanOrEqualAll                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_LessThanOrEqualAny                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_LoadAligned                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_LoadAlignedNonTemporal                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_LoadUnsafe                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_Max                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MaxMagnitude                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MaxMagnitudeNumber                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MaxNative                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MaxNumber                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_Min                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MinMagnitude                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MinMagnitudeNumber                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MinNative                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MinNumber                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_MultiplyAddEstimate                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_Narrow                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_NarrowWithSaturation                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_Round                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_ShiftLeft                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_Shuffle                                                   */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector128_ShuffleNative                                             */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector128_ShuffleNativeFallback                                     */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector128_Sqrt                                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_StoreAligned                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_StoreAlignedNonTemporal                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_StoreUnsafe                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_SubtractSaturate                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_Sum                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_ToScalar                                                  */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_Vector128_ToVector256                                               */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector128_ToVector256Unsafe                                         */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector128_ToVector512                                               */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector128_Truncate                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_WidenLower                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_WidenUpper                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_WithElement                                               */      HW_Flag_SpecialImport | HW_Flag_NoContainment | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector128_get_AllBitsSet                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_E                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_Epsilon                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_Indices                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_NaN                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_NegativeInfinity                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_NegativeOne                                           */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_NegativeZero                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_One                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_Pi                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_PositiveInfinity                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_Tau                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_get_Zero                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_Addition                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_BitwiseAnd                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_BitwiseOr                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_Division                                               */      HW_Flag_SpecialSideEffect_Other | HW_Flag_SpecialImport,
          /* NI_Vector128_op_Equality                                               */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector128_op_ExclusiveOr                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_Inequality                                             */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector128_op_LeftShift                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_Multiply                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_OnesComplement                                         */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_RightShift                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_Subtraction                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_UnaryNegation                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_UnaryPlus                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector128_op_UnsignedRightShift                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_Abs                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_AddSaturate                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_AndNot                                                    */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_As                                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsByte                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsDouble                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsInt16                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsInt32                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsInt64                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsNInt                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsNUInt                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsSByte                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsSingle                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsUInt16                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsUInt32                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsUInt64                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsVector                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_AsVector256                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_Ceiling                                                   */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_ConditionalSelect                                         */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_ConvertToDouble                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ConvertToInt32                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_ConvertToInt32Native                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_ConvertToInt64                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ConvertToInt64Native                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ConvertToSingle                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_ConvertToUInt32                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ConvertToUInt32Native                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ConvertToUInt64                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ConvertToUInt64Native                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_Create                                                    */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_CreateScalar                                              */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_CreateScalarUnsafe                                        */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_CreateSequence                                            */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_Dot                                                       */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_Equals                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_EqualsAny                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ExtractMostSignificantBits                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector256_Floor                                                     */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_FusedMultiplyAdd                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_GetElement                                                */      HW_Flag_SpecialImport | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_GetLower                                                  */      HW_Flag_SpecialCodeGen | HW_Flag_AvxOnlyCompatible | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector256_GetUpper                                                  */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_GreaterThan                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_GreaterThanAll                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_GreaterThanAny                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_GreaterThanOrEqual                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_GreaterThanOrEqualAll                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_GreaterThanOrEqualAny                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_IsEvenInteger                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsFinite                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsInfinity                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsInteger                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsNaN                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsNegative                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsNegativeInfinity                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsNormal                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsOddInteger                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsPositive                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsPositiveInfinity                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsSubnormal                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_IsZero                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_LessThan                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_LessThanAll                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_LessThanAny                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_LessThanOrEqual                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_LessThanOrEqualAll                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_LessThanOrEqualAny                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_LoadAligned                                               */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_LoadAlignedNonTemporal                                    */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_LoadUnsafe                                                */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_Max                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MaxMagnitude                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MaxMagnitudeNumber                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MaxNative                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MaxNumber                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_Min                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MinMagnitude                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MinMagnitudeNumber                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MinNative                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MinNumber                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_MultiplyAddEstimate                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_Narrow                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_NarrowWithSaturation                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_Round                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_ShiftLeft                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_Shuffle                                                   */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector256_ShuffleNative                                             */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector256_ShuffleNativeFallback                                     */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector256_Sqrt                                                      */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_StoreAligned                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_StoreAlignedNonTemporal                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_StoreUnsafe                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_SubtractSaturate                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_Sum                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_ToScalar                                                  */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_ToVector512                                               */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector256_ToVector512Unsafe                                         */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector256_Truncate                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_WidenLower                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_WidenUpper                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector256_WithElement                                               */      HW_Flag_SpecialImport | HW_Flag_NoContainment | HW_Flag_BaseTypeFromFirstArg | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_WithLower                                                 */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_WithUpper                                                 */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_get_AllBitsSet                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_E                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_Epsilon                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_Indices                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_NaN                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_NegativeInfinity                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_NegativeOne                                           */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_NegativeZero                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_One                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_Pi                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_PositiveInfinity                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_Tau                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_get_Zero                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_Addition                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_BitwiseAnd                                             */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_op_BitwiseOr                                              */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_op_Division                                               */      HW_Flag_SpecialSideEffect_Other | HW_Flag_SpecialImport,
          /* NI_Vector256_op_Equality                                               */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector256_op_ExclusiveOr                                            */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_op_Inequality                                             */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector256_op_LeftShift                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_Multiply                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_OnesComplement                                         */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_op_RightShift                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_Subtraction                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_UnaryNegation                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector256_op_UnaryPlus                                              */      HW_Flag_InvalidNodeId | HW_Flag_AvxOnlyCompatible,
          /* NI_Vector256_op_UnsignedRightShift                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Abs                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_AddSaturate                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_AndNot                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_As                                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsByte                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsDouble                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsInt16                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsInt32                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsInt64                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsNInt                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsNUInt                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsSByte                                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsSingle                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsUInt16                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsUInt32                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsUInt64                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsVector                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_AsVector512                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_Ceiling                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_ConditionalSelect                                         */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector512_ConvertToDouble                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToInt32                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToInt32Native                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToInt64                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToInt64Native                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToSingle                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToUInt32                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToUInt32Native                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToUInt64                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ConvertToUInt64Native                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_Create                                                    */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector512_CreateScalar                                              */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
          /* NI_Vector512_CreateScalarUnsafe                                        */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
          /* NI_Vector512_CreateSequence                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Dot                                                       */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_Equals                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_EqualsAny                                                 */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ExtractMostSignificantBits                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_Floor                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_FusedMultiplyAdd                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_GetElement                                                */      HW_Flag_SpecialImport | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GetLower                                                  */      HW_Flag_SpecialCodeGen | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_Vector512_GetLower128                                               */      HW_Flag_SpecialCodeGen | HW_Flag_NormalizeSmallTypeToInt | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GetUpper                                                  */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector512_GreaterThan                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GreaterThanAll                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GreaterThanAny                                            */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GreaterThanOrEqual                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GreaterThanOrEqualAll                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_GreaterThanOrEqualAny                                     */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_IsEvenInteger                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsFinite                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsInfinity                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsInteger                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsNaN                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsNegative                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsNegativeInfinity                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsNormal                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsOddInteger                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsPositive                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsPositiveInfinity                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsSubnormal                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_IsZero                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_LessThan                                                  */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_LessThanAll                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_LessThanAny                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_LessThanOrEqual                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_LessThanOrEqualAll                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_LessThanOrEqualAny                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_LoadAligned                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_LoadAlignedNonTemporal                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_LoadUnsafe                                                */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Max                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MaxMagnitude                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MaxMagnitudeNumber                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MaxNative                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MaxNumber                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Min                                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MinMagnitude                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MinMagnitudeNumber                                        */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MinNative                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MinNumber                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_MultiplyAddEstimate                                       */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Narrow                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_NarrowWithSaturation                                      */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_Round                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_ShiftLeft                                                 */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Shuffle                                                   */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector512_ShuffleNative                                             */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector512_ShuffleNativeFallback                                     */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector512_Sqrt                                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_StoreAligned                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_StoreAlignedNonTemporal                                   */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_StoreUnsafe                                               */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_SubtractSaturate                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_Sum                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_ToScalar                                                  */      HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_Truncate                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_WidenLower                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_WidenUpper                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_WithElement                                               */      HW_Flag_SpecialImport | HW_Flag_NoContainment | HW_Flag_BaseTypeFromFirstArg,
          /* NI_Vector512_WithLower                                                 */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector512_WithUpper                                                 */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen,
          /* NI_Vector512_get_AllBitsSet                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_E                                                     */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_Epsilon                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_Indices                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_NaN                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_NegativeInfinity                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_NegativeOne                                           */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_NegativeZero                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_One                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_Pi                                                    */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_PositiveInfinity                                      */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_Tau                                                   */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_get_Zero                                                  */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_Addition                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_BitwiseAnd                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_BitwiseOr                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_Division                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_Equality                                               */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector512_op_ExclusiveOr                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_Inequality                                             */      HW_Flag_SpecialImport | HW_Flag_NoCodeGen | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_Vector512_op_LeftShift                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_Multiply                                               */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_OnesComplement                                         */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_RightShift                                             */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_Subtraction                                            */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_UnaryNegation                                          */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_UnaryPlus                                              */      HW_Flag_InvalidNodeId,
          /* NI_Vector512_op_UnsignedRightShift                                     */      HW_Flag_InvalidNodeId,
          /* NI_X86Base_Abs                                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_Add                                                         */      HW_Flag_Commutative,
          /* NI_X86Base_AddSaturate                                                 */      HW_Flag_Commutative,
          /* NI_X86Base_AddScalar                                                   */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_AddSubtract                                                 */      HW_Flag_NoEvexSemantics,
          /* NI_X86Base_AlignRight                                                  */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_And                                                         */      HW_Flag_Commutative | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_AndNot                                                      */      HW_Flag_SpecialImport | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_Average                                                     */      HW_Flag_Commutative,
          /* NI_X86Base_BitScanForward                                              */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_BitScanReverse                                              */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_Blend                                                       */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_BlendVariable                                               */      HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt | HW_Flag_SpecialImport,
          /* NI_X86Base_Ceiling                                                     */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CeilingScalar                                               */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_CompareEqual                                                */      HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareGreaterThan                                          */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareGreaterThanOrEqual                                   */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareLessThan                                             */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareLessThanOrEqual                                      */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareNotEqual                                             */      HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareNotGreaterThan                                       */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareNotGreaterThanOrEqual                                */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareNotLessThan                                          */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareNotLessThanOrEqual                                   */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareOrdered                                              */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_CompareScalarEqual                                          */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarGreaterThan                                    */      HW_Flag_SpecialImport | HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarGreaterThanOrEqual                             */      HW_Flag_SpecialImport | HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarLessThan                                       */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarLessThanOrEqual                                */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarNotEqual                                       */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarNotGreaterThan                                 */      HW_Flag_SpecialImport | HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarNotGreaterThanOrEqual                          */      HW_Flag_SpecialImport | HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarNotLessThan                                    */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarNotLessThanOrEqual                             */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarOrdered                                        */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarOrderedEqual                                   */      HW_Flag_Commutative | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarOrderedGreaterThan                             */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarOrderedGreaterThanOrEqual                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarOrderedLessThan                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarOrderedLessThanOrEqual                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarOrderedNotEqual                                */      HW_Flag_Commutative | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarUnordered                                      */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_CompareScalarUnorderedEqual                                 */      HW_Flag_Commutative | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarUnorderedGreaterThan                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarUnorderedLessThan                              */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarUnorderedLessThanOrEqual                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareScalarUnorderedNotEqual                              */      HW_Flag_Commutative | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_CompareUnordered                                            */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_X86Base_ConvertScalarToVector128Double                              */      HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_ConvertScalarToVector128Int32                               */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertScalarToVector128Single                              */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_CopyUpperBits,
          /* NI_X86Base_ConvertScalarToVector128UInt32                              */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertToInt32                                              */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertToInt32WithTruncation                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertToUInt32                                             */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertToVector128Double                                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertToVector128Int16                                     */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_MaybeMemoryLoad,
          /* NI_X86Base_ConvertToVector128Int32                                     */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_MaybeMemoryLoad,
          /* NI_X86Base_ConvertToVector128Int32WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_ConvertToVector128Int64                                     */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_MaybeMemoryLoad,
          /* NI_X86Base_ConvertToVector128Single                                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_Crc32                                                       */      HW_Flag_NoFloatingPointUsed | HW_Flag_RmwIntrinsic,
          /* NI_X86Base_DivRem                                                      */      HW_Flag_NoFloatingPointUsed | HW_Flag_BaseTypeFromSecondArg | HW_Flag_MultiReg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_RmwIntrinsic,
          /* NI_X86Base_Divide                                                      */      HW_Flag_NoFlag,
          /* NI_X86Base_DivideScalar                                                */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_DotProduct                                                  */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_Extract                                                     */      HW_Flag_FullRangeIMM | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_Floor                                                       */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_FloorScalar                                                 */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_HorizontalAdd                                               */      HW_Flag_NoEvexSemantics,
          /* NI_X86Base_HorizontalAddSaturate                                       */      HW_Flag_NoEvexSemantics,
          /* NI_X86Base_HorizontalSubtract                                          */      HW_Flag_NoEvexSemantics,
          /* NI_X86Base_HorizontalSubtractSaturate                                  */      HW_Flag_NoEvexSemantics,
          /* NI_X86Base_Insert                                                      */      HW_Flag_FullRangeIMM | HW_Flag_CanBenefitFromConstantProp,
          /* NI_X86Base_LoadAlignedVector128                                        */      HW_Flag_NoRMWSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_LoadAlignedVector128NonTemporal                             */      HW_Flag_NoRMWSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_LoadAndDuplicateToVector128                                 */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_LoadDquVector128                                            */      HW_Flag_NoRMWSemantics | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_LoadFence                                                   */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Barrier,
          /* NI_X86Base_LoadHigh                                                    */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_LoadLow                                                     */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_LoadScalarVector128                                         */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_LoadVector128                                               */      HW_Flag_InvalidNodeId,
          /* NI_X86Base_MaskMove                                                    */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_Max                                                         */      HW_Flag_MaybeCommutative,
          /* NI_X86Base_MaxScalar                                                   */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_MemoryFence                                                 */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Barrier,
          /* NI_X86Base_Min                                                         */      HW_Flag_MaybeCommutative,
          /* NI_X86Base_MinHorizontal                                               */      HW_Flag_NoRMWSemantics | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_MinScalar                                                   */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_MoveAndDuplicate                                            */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_MoveHighAndDuplicate                                        */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_MoveHighToLow                                               */      HW_Flag_NoContainment,
          /* NI_X86Base_MoveLowAndDuplicate                                         */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_MoveLowToHigh                                               */      HW_Flag_NoContainment,
          /* NI_X86Base_MoveMask                                                    */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_MoveScalar                                                  */      HW_Flag_NoContainment,
          /* NI_X86Base_MultipleSumAbsoluteDifferences                              */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_Multiply                                                    */      HW_Flag_Commutative,
          /* NI_X86Base_MultiplyAddAdjacent                                         */      HW_Flag_MaybeCommutative,
          /* NI_X86Base_MultiplyHigh                                                */      HW_Flag_Commutative,
          /* NI_X86Base_MultiplyHighRoundScale                                      */      HW_Flag_NoFlag,
          /* NI_X86Base_MultiplyLow                                                 */      HW_Flag_Commutative,
          /* NI_X86Base_MultiplyScalar                                              */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_Or                                                          */      HW_Flag_Commutative | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_PackSignedSaturate                                          */      HW_Flag_NoFlag,
          /* NI_X86Base_PackUnsignedSaturate                                        */      HW_Flag_NoFlag,
          /* NI_X86Base_Pause                                                       */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Other,
          /* NI_X86Base_PopCount                                                    */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_Prefetch0                                                   */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Other,
          /* NI_X86Base_Prefetch1                                                   */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Other,
          /* NI_X86Base_Prefetch2                                                   */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Other,
          /* NI_X86Base_PrefetchNonTemporal                                         */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Other,
          /* NI_X86Base_Reciprocal                                                  */      HW_Flag_NoRMWSemantics | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_ReciprocalScalar                                            */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_ReciprocalSqrt                                              */      HW_Flag_NoRMWSemantics | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_ReciprocalSqrtScalar                                        */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_RoundCurrentDirection                                       */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_RoundCurrentDirectionScalar                                 */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_RoundToNearestInteger                                       */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_RoundToNearestIntegerScalar                                 */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_RoundToNegativeInfinity                                     */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_RoundToNegativeInfinityScalar                               */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_RoundToPositiveInfinity                                     */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_RoundToPositiveInfinityScalar                               */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_RoundToZero                                                 */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_RoundToZeroScalar                                           */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_ShiftLeftLogical                                            */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_X86Base_ShiftLeftLogical128BitLane                                  */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_ShiftRightArithmetic                                        */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_X86Base_ShiftRightLogical                                           */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_X86Base_ShiftRightLogical128BitLane                                 */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_Shuffle                                                     */      HW_Flag_MaybeIMM | HW_Flag_FullRangeIMM,
          /* NI_X86Base_ShuffleHigh                                                 */      HW_Flag_FullRangeIMM,
          /* NI_X86Base_ShuffleLow                                                  */      HW_Flag_FullRangeIMM,
          /* NI_X86Base_Sign                                                        */      HW_Flag_NoEvexSemantics,
          /* NI_X86Base_Sqrt                                                        */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_SqrtScalar                                                  */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_Store                                                       */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_StoreAligned                                                */      HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_StoreAlignedNonTemporal                                     */      HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_StoreFence                                                  */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Barrier,
          /* NI_X86Base_StoreHigh                                                   */      HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_StoreLow                                                    */      HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_StoreNonTemporal                                            */      HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_StoreScalar                                                 */      HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_Subtract                                                    */      HW_Flag_NoFlag,
          /* NI_X86Base_SubtractSaturate                                            */      HW_Flag_NoFlag,
          /* NI_X86Base_SubtractScalar                                              */      HW_Flag_CopyUpperBits,
          /* NI_X86Base_SumAbsoluteDifferences                                      */      HW_Flag_NoFlag,
          /* NI_X86Base_TestC                                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_TestNotZAndNotC                                             */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_TestZ                                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_UnpackHigh                                                  */      HW_Flag_NoFlag,
          /* NI_X86Base_UnpackLow                                                   */      HW_Flag_NoFlag,
          /* NI_X86Base_Xor                                                         */      HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_X86Base_X64_BigMul                                                  */      HW_Flag_NoFloatingPointUsed | HW_Flag_BaseTypeFromSecondArg | HW_Flag_MultiReg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_RmwIntrinsic | HW_Flag_Commutative,
          /* NI_X86Base_X64_BitScanForward                                          */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_X64_BitScanReverse                                          */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_X64_ConvertScalarToVector128Double                          */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromSecondArg,
          /* NI_X86Base_X64_ConvertScalarToVector128Int64                           */      HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_ConvertScalarToVector128Single                          */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_CopyUpperBits | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_ConvertScalarToVector128UInt64                          */      HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_ConvertToInt64                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_ConvertToInt64WithTruncation                            */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_ConvertToUInt64                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_Crc32                                                   */      HW_Flag_NoFloatingPointUsed | HW_Flag_RmwIntrinsic,
          /* NI_X86Base_X64_DivRem                                                  */      HW_Flag_NoFloatingPointUsed | HW_Flag_BaseTypeFromSecondArg | HW_Flag_MultiReg | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen | HW_Flag_RmwIntrinsic,
          /* NI_X86Base_X64_Extract                                                 */      HW_Flag_FullRangeIMM | HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_NoRMWSemantics,
          /* NI_X86Base_X64_Insert                                                  */      HW_Flag_FullRangeIMM | HW_Flag_CanBenefitFromConstantProp,
          /* NI_X86Base_X64_PopCount                                                */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_X86Base_X64_StoreNonTemporal                                        */      HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX_Add                                                             */      HW_Flag_Commutative,
          /* NI_AVX_AddSubtract                                                     */      HW_Flag_NoEvexSemantics,
          /* NI_AVX_And                                                             */      HW_Flag_Commutative,
          /* NI_AVX_AndNot                                                          */      HW_Flag_SpecialImport,
          /* NI_AVX_Blend                                                           */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AVX_BlendVariable                                                   */      HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport | HW_Flag_SpecialImport,
          /* NI_AVX_BroadcastScalarToVector128                                      */      HW_Flag_NoFlag,
          /* NI_AVX_BroadcastScalarToVector256                                      */      HW_Flag_NoFlag,
          /* NI_AVX_BroadcastVector128ToVector256                                   */      HW_Flag_NoFlag,
          /* NI_AVX_Ceiling                                                         */      HW_Flag_NoFlag,
          /* NI_AVX_Compare                                                         */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareEqual                                                    */      HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareGreaterThan                                              */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareGreaterThanOrEqual                                       */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareLessThan                                                 */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareLessThanOrEqual                                          */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareNotEqual                                                 */      HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareNotGreaterThan                                           */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareNotGreaterThanOrEqual                                    */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareNotLessThan                                              */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareNotLessThanOrEqual                                       */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareOrdered                                                  */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareScalar                                                   */      HW_Flag_CopyUpperBits | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_CompareUnordered                                                */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX_ConvertToVector128Int32                                         */      HW_Flag_NoFlag,
          /* NI_AVX_ConvertToVector128Int32WithTruncation                           */      HW_Flag_NoFlag,
          /* NI_AVX_ConvertToVector128Single                                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX_ConvertToVector256Double                                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX_ConvertToVector256Int32                                         */      HW_Flag_NoFlag,
          /* NI_AVX_ConvertToVector256Int32WithTruncation                           */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX_ConvertToVector256Single                                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX_Divide                                                          */      HW_Flag_NoFlag,
          /* NI_AVX_DotProduct                                                      */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AVX_DuplicateEvenIndexed                                            */      HW_Flag_NoFlag,
          /* NI_AVX_DuplicateOddIndexed                                             */      HW_Flag_NoFlag,
          /* NI_AVX_ExtractVector128                                                */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_Floor                                                           */      HW_Flag_NoFlag,
          /* NI_AVX_HorizontalAdd                                                   */      HW_Flag_NoEvexSemantics,
          /* NI_AVX_HorizontalSubtract                                              */      HW_Flag_NoEvexSemantics,
          /* NI_AVX_InsertVector128                                                 */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_LoadAlignedVector256                                            */      HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_LoadDquVector256                                                */      HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_LoadVector256                                                   */      HW_Flag_InvalidNodeId,
          /* NI_AVX_MaskLoad                                                        */      HW_Flag_NoEvexSemantics,
          /* NI_AVX_MaskStore                                                       */      HW_Flag_NoContainment | HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoEvexSemantics,
          /* NI_AVX_Max                                                             */      HW_Flag_MaybeCommutative,
          /* NI_AVX_Min                                                             */      HW_Flag_MaybeCommutative,
          /* NI_AVX_MoveMask                                                        */      HW_Flag_NoContainment | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics,
          /* NI_AVX_Multiply                                                        */      HW_Flag_Commutative,
          /* NI_AVX_Or                                                              */      HW_Flag_Commutative,
          /* NI_AVX_Permute                                                         */      HW_Flag_FullRangeIMM,
          /* NI_AVX_Permute2x128                                                    */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AVX_PermuteVar                                                      */      HW_Flag_NoFlag,
          /* NI_AVX_Reciprocal                                                      */      HW_Flag_NoEvexSemantics,
          /* NI_AVX_ReciprocalSqrt                                                  */      HW_Flag_NoEvexSemantics,
          /* NI_AVX_RoundCurrentDirection                                           */      HW_Flag_NoFlag,
          /* NI_AVX_RoundToNearestInteger                                           */      HW_Flag_NoFlag,
          /* NI_AVX_RoundToNegativeInfinity                                         */      HW_Flag_NoFlag,
          /* NI_AVX_RoundToPositiveInfinity                                         */      HW_Flag_NoFlag,
          /* NI_AVX_RoundToZero                                                     */      HW_Flag_NoFlag,
          /* NI_AVX_Shuffle                                                         */      HW_Flag_FullRangeIMM,
          /* NI_AVX_Sqrt                                                            */      HW_Flag_NoFlag,
          /* NI_AVX_Store                                                           */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX_StoreAligned                                                    */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_StoreAlignedNonTemporal                                         */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_Subtract                                                        */      HW_Flag_NoFlag,
          /* NI_AVX_TestC                                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_TestNotZAndNotC                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_TestZ                                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX_UnpackHigh                                                      */      HW_Flag_NoFlag,
          /* NI_AVX_UnpackLow                                                       */      HW_Flag_NoFlag,
          /* NI_AVX_Xor                                                             */      HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_AVX2_Abs                                                            */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX2_Add                                                            */      HW_Flag_Commutative,
          /* NI_AVX2_AddSaturate                                                    */      HW_Flag_Commutative,
          /* NI_AVX2_AlignRight                                                     */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_And                                                            */      HW_Flag_Commutative | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_AndNot                                                         */      HW_Flag_InvalidNodeId | HW_Flag_NoFloatingPointUsed,
          /* NI_AVX2_Average                                                        */      HW_Flag_Commutative,
          /* NI_AVX2_BitFieldExtract                                                */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_Blend                                                          */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_BlendVariable                                                  */      HW_Flag_NoEvexSemantics | HW_Flag_NormalizeSmallTypeToInt | HW_Flag_SpecialImport,
          /* NI_AVX2_BroadcastScalarToVector128                                     */      HW_Flag_MaybeMemoryLoad,
          /* NI_AVX2_BroadcastScalarToVector256                                     */      HW_Flag_MaybeMemoryLoad,
          /* NI_AVX2_BroadcastVector128ToVector256                                  */      HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_CompareEqual                                                   */      HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX2_CompareGreaterThan                                             */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX2_CompareLessThan                                                */      HW_Flag_ReturnsPerElementMask | HW_Flag_NoEvexSemantics | HW_Flag_SpecialImport,
          /* NI_AVX2_ConvertToInt32                                                 */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX2_ConvertToUInt32                                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX2_ConvertToVector128Half                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_FullRangeIMM,
          /* NI_AVX2_ConvertToVector128Single                                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX2_ConvertToVector256Half                                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_FullRangeIMM,
          /* NI_AVX2_ConvertToVector256Int16                                        */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_MaybeMemoryLoad,
          /* NI_AVX2_ConvertToVector256Int32                                        */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_MaybeMemoryLoad,
          /* NI_AVX2_ConvertToVector256Int64                                        */      HW_Flag_SpecialCodeGen | HW_Flag_BaseTypeFromFirstArg | HW_Flag_MaybeMemoryLoad,
          /* NI_AVX2_ConvertToVector256Single                                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX2_ExtractLowestSetBit                                            */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_ExtractVector128                                               */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_GatherMaskVector128                                            */      HW_Flag_MaybeMemoryLoad | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_NoContainment | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_GatherMaskVector256                                            */      HW_Flag_MaybeMemoryLoad | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_NoContainment | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_GatherVector128                                                */      HW_Flag_MaybeMemoryLoad | HW_Flag_SpecialCodeGen | HW_Flag_NoContainment | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_GatherVector256                                                */      HW_Flag_MaybeMemoryLoad | HW_Flag_SpecialCodeGen | HW_Flag_NoContainment | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_GetMaskUpToLowestSetBit                                        */      HW_Flag_NoFloatingPointUsed,
          /* NI_AVX2_HorizontalAdd                                                  */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_HorizontalAddSaturate                                          */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_HorizontalSubtract                                             */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_HorizontalSubtractSaturate                                     */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_InsertVector128                                                */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_LeadingZeroCount                                               */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_AVX2_LoadAlignedVector256NonTemporal                                */      HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_MaskLoad                                                       */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_MaskStore                                                      */      HW_Flag_NoContainment | HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_Max                                                            */      HW_Flag_Commutative,
          /* NI_AVX2_Min                                                            */      HW_Flag_Commutative,
          /* NI_AVX2_MoveMask                                                       */      HW_Flag_NoContainment | HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_MultipleSumAbsoluteDifferences                                 */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_Multiply                                                       */      HW_Flag_Commutative,
          /* NI_AVX2_MultiplyAdd                                                    */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic,
          /* NI_AVX2_MultiplyAddAdjacent                                            */      HW_Flag_NoFlag,
          /* NI_AVX2_MultiplyAddNegated                                             */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic,
          /* NI_AVX2_MultiplyAddNegatedScalar                                       */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_CopyUpperBits,
          /* NI_AVX2_MultiplyAddScalar                                              */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_CopyUpperBits,
          /* NI_AVX2_MultiplyAddSubtract                                            */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic,
          /* NI_AVX2_MultiplyHigh                                                   */      HW_Flag_Commutative,
          /* NI_AVX2_MultiplyHighRoundScale                                         */      HW_Flag_NoFlag,
          /* NI_AVX2_MultiplyLow                                                    */      HW_Flag_Commutative,
          /* NI_AVX2_MultiplyNoFlags                                                */      HW_Flag_NoContainment | HW_Flag_MaybeMemoryStore | HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics | HW_Flag_MaybeCommutative,
          /* NI_AVX2_MultiplySubtract                                               */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic,
          /* NI_AVX2_MultiplySubtractAdd                                            */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic,
          /* NI_AVX2_MultiplySubtractNegated                                        */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic,
          /* NI_AVX2_MultiplySubtractNegatedScalar                                  */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_CopyUpperBits,
          /* NI_AVX2_MultiplySubtractScalar                                         */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_CopyUpperBits,
          /* NI_AVX2_Or                                                             */      HW_Flag_Commutative | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_PackSignedSaturate                                             */      HW_Flag_NoFlag,
          /* NI_AVX2_PackUnsignedSaturate                                           */      HW_Flag_NoFlag,
          /* NI_AVX2_ParallelBitDeposit                                             */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_ParallelBitExtract                                             */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_Permute2x128                                                   */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_Permute4x64                                                    */      HW_Flag_FullRangeIMM,
          /* NI_AVX2_PermuteVar8x32                                                 */      HW_Flag_SpecialImport,
          /* NI_AVX2_ResetLowestSetBit                                              */      HW_Flag_NoFloatingPointUsed,
          /* NI_AVX2_ShiftLeftLogical                                               */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX2_ShiftLeftLogical128BitLane                                     */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_ShiftLeftLogicalVariable                                       */      HW_Flag_NoFlag,
          /* NI_AVX2_ShiftRightArithmetic                                           */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX2_ShiftRightArithmeticVariable                                   */      HW_Flag_NoFlag,
          /* NI_AVX2_ShiftRightLogical                                              */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX2_ShiftRightLogical128BitLane                                    */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_ShiftRightLogicalVariable                                      */      HW_Flag_NoFlag,
          /* NI_AVX2_Shuffle                                                        */      HW_Flag_FullRangeIMM | HW_Flag_MaybeIMM,
          /* NI_AVX2_ShuffleHigh                                                    */      HW_Flag_FullRangeIMM,
          /* NI_AVX2_ShuffleLow                                                     */      HW_Flag_FullRangeIMM,
          /* NI_AVX2_Sign                                                           */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_Subtract                                                       */      HW_Flag_NoFlag,
          /* NI_AVX2_SubtractSaturate                                               */      HW_Flag_NoFlag,
          /* NI_AVX2_SumAbsoluteDifferences                                         */      HW_Flag_NoFlag,
          /* NI_AVX2_TrailingZeroCount                                              */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen,
          /* NI_AVX2_UnpackHigh                                                     */      HW_Flag_NoFlag,
          /* NI_AVX2_UnpackLow                                                      */      HW_Flag_NoFlag,
          /* NI_AVX2_Xor                                                            */      HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_ZeroHighBits                                                   */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialImport | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_X64_AndNot                                                     */      HW_Flag_SpecialImport | HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_X64_BitFieldExtract                                            */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_X64_ExtractLowestSetBit                                        */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_X64_GetMaskUpToLowestSetBit                                    */      HW_Flag_NoFloatingPointUsed,
          /* NI_AVX2_X64_LeadingZeroCount                                           */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoRMWSemantics | HW_Flag_SpecialCodeGen,
          /* NI_AVX2_X64_MultiplyNoFlags                                            */      HW_Flag_NoContainment | HW_Flag_MaybeMemoryStore | HW_Flag_SpecialCodeGen | HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics | HW_Flag_MaybeCommutative,
          /* NI_AVX2_X64_ParallelBitDeposit                                         */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_X64_ParallelBitExtract                                         */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX2_X64_ResetLowestSetBit                                          */      HW_Flag_NoFloatingPointUsed,
          /* NI_AVX2_X64_TrailingZeroCount                                          */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialCodeGen,
          /* NI_AVX2_X64_ZeroHighBits                                               */      HW_Flag_NoFloatingPointUsed | HW_Flag_SpecialImport | HW_Flag_NoEvexSemantics,
          /* NI_AVX512_Abs                                                          */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_Add                                                          */      HW_Flag_MaybeCommutative | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_AddSaturate                                                  */      HW_Flag_Commutative,
          /* NI_AVX512_AddScalar                                                    */      HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_AlignRight                                                   */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_AlignRight32                                                 */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_AlignRight64                                                 */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_And                                                          */      HW_Flag_Commutative | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_AndNot                                                       */      HW_Flag_SpecialImport | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_Average                                                      */      HW_Flag_Commutative,
          /* NI_AVX512_BlendVariable                                                */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_BroadcastPairScalarToVector128                               */      HW_Flag_NoFlag,
          /* NI_AVX512_BroadcastPairScalarToVector256                               */      HW_Flag_NoFlag,
          /* NI_AVX512_BroadcastPairScalarToVector512                               */      HW_Flag_NoFlag,
          /* NI_AVX512_BroadcastScalarToVector512                                   */      HW_Flag_NoFlag,
          /* NI_AVX512_BroadcastVector128ToVector512                                */      HW_Flag_NoFlag,
          /* NI_AVX512_BroadcastVector256ToVector512                                */      HW_Flag_NoFlag,
          /* NI_AVX512_Classify                                                     */      HW_Flag_InvalidNodeId | HW_Flag_FullRangeIMM,
          /* NI_AVX512_ClassifyScalar                                               */      HW_Flag_InvalidNodeId | HW_Flag_FullRangeIMM,
          /* NI_AVX512_Compare                                                      */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareEqual                                                 */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareGreaterThan                                           */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareGreaterThanOrEqual                                    */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareLessThan                                              */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareLessThanOrEqual                                       */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareNotEqual                                              */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareNotGreaterThan                                        */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareNotGreaterThanOrEqual                                 */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareNotLessThan                                           */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareNotLessThanOrEqual                                    */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareOrdered                                               */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompareUnordered                                             */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_Compress                                                     */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_CompressStore                                                */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_ConvertScalarToVector128Double                               */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_CopyUpperBits,
          /* NI_AVX512_ConvertScalarToVector128Single                               */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToInt32                                               */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToUInt32                                              */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToUInt32WithTruncation                                */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Byte                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128ByteWithSaturation                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Double                                     */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector128Int16                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Int16WithSaturation                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Int32                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Int32WithSaturation                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Int64                                      */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector128Int64WithTruncation                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector128SByte                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128SByteWithSaturation                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128Single                                     */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector128UInt16                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128UInt16WithSaturation                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128UInt32                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128UInt32WithSaturation                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector128UInt32WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector128UInt64                                     */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector128UInt64WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256Byte                                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256ByteWithSaturation                         */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256Double                                     */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256Int16                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256Int16WithSaturation                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256Int32                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector256Int32WithSaturation                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256Int32WithTruncation                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256Int64                                      */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256Int64WithTruncation                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256SByte                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256SByteWithSaturation                        */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256Single                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector256UInt16                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256UInt16WithSaturation                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256UInt32                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector256UInt32WithSaturation                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ConvertToVector256UInt32WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256UInt64                                     */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector256UInt64WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector512Double                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector512Int16                                      */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector512Int32                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector512Int32WithTruncation                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector512Int64                                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector512Int64WithTruncation                        */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector512Single                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector512UInt16                                     */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector512UInt32                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector512UInt32WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_ConvertToVector512UInt64                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ConvertToVector512UInt64WithTruncation                       */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX512_DetectConflicts                                              */      HW_Flag_NoFlag,
          /* NI_AVX512_Divide                                                       */      HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_DivideScalar                                                 */      HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_DuplicateEvenIndexed                                         */      HW_Flag_NoFlag,
          /* NI_AVX512_DuplicateOddIndexed                                          */      HW_Flag_NoFlag,
          /* NI_AVX512_Expand                                                       */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_ExpandLoad                                                   */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_ExtractVector128                                             */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_ExtractVector256                                             */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_Fixup                                                        */      HW_Flag_SpecialImport | HW_Flag_FullRangeIMM,
          /* NI_AVX512_FixupScalar                                                  */      HW_Flag_SpecialImport | HW_Flag_FullRangeIMM | HW_Flag_CopyUpperBits,
          /* NI_AVX512_FusedMultiplyAdd                                             */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_FusedMultiplyAddNegated                                      */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_FusedMultiplyAddNegatedScalar                                */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible | HW_Flag_CopyUpperBits,
          /* NI_AVX512_FusedMultiplyAddScalar                                       */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible | HW_Flag_CopyUpperBits,
          /* NI_AVX512_FusedMultiplyAddSubtract                                     */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_FusedMultiplySubtract                                        */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_FusedMultiplySubtractAdd                                     */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_FusedMultiplySubtractNegated                                 */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_FusedMultiplySubtractNegatedScalar                           */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible | HW_Flag_CopyUpperBits,
          /* NI_AVX512_FusedMultiplySubtractScalar                                  */      HW_Flag_SpecialCodeGen | HW_Flag_FmaIntrinsic | HW_Flag_RmwIntrinsic | HW_Flag_EmbRoundingCompatible | HW_Flag_CopyUpperBits,
          /* NI_AVX512_GetExponent                                                  */      HW_Flag_NoFlag,
          /* NI_AVX512_GetExponentScalar                                            */      HW_Flag_CopyUpperBits,
          /* NI_AVX512_GetMantissa                                                  */      HW_Flag_NoFlag,
          /* NI_AVX512_GetMantissaScalar                                            */      HW_Flag_CopyUpperBits,
          /* NI_AVX512_InsertVector128                                              */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_InsertVector256                                              */      HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_LeadingZeroCount                                             */      HW_Flag_NoFlag,
          /* NI_AVX512_LoadAlignedVector512                                         */      HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_LoadAlignedVector512NonTemporal                              */      HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_LoadVector512                                                */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_MaskLoad                                                     */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_MaskLoadAligned                                              */      HW_Flag_InvalidNodeId,
          /* NI_AVX512_MaskStore                                                    */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_MaskStoreAligned                                             */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_Max                                                          */      HW_Flag_MaybeCommutative,
          /* NI_AVX512_Min                                                          */      HW_Flag_MaybeCommutative,
          /* NI_AVX512_MoveMask                                                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_NoContainment | HW_Flag_SpecialImport | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_Multiply                                                     */      HW_Flag_MaybeCommutative | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_MultiplyAddAdjacent                                          */      HW_Flag_NoFlag,
          /* NI_AVX512_MultiplyHigh                                                 */      HW_Flag_Commutative,
          /* NI_AVX512_MultiplyHighRoundScale                                       */      HW_Flag_NoFlag,
          /* NI_AVX512_MultiplyLow                                                  */      HW_Flag_Commutative,
          /* NI_AVX512_MultiplyScalar                                               */      HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_Or                                                           */      HW_Flag_Commutative | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_PackSignedSaturate                                           */      HW_Flag_NoFlag,
          /* NI_AVX512_PackUnsignedSaturate                                         */      HW_Flag_NoFlag,
          /* NI_AVX512_Permute2x64                                                  */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_Permute4x32                                                  */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_Permute4x64                                                  */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_PermuteVar16x16                                              */      HW_Flag_SpecialImport,
          /* NI_AVX512_PermuteVar16x16x2                                            */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar16x32                                              */      HW_Flag_SpecialImport,
          /* NI_AVX512_PermuteVar16x32x2                                            */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar2x64                                               */      HW_Flag_NoFlag,
          /* NI_AVX512_PermuteVar2x64x2                                             */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar32x16                                              */      HW_Flag_SpecialImport,
          /* NI_AVX512_PermuteVar32x16x2                                            */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar4x32                                               */      HW_Flag_NoFlag,
          /* NI_AVX512_PermuteVar4x32x2                                             */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar4x64                                               */      HW_Flag_SpecialImport,
          /* NI_AVX512_PermuteVar4x64x2                                             */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar8x16                                               */      HW_Flag_SpecialImport,
          /* NI_AVX512_PermuteVar8x16x2                                             */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar8x32x2                                             */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_PermuteVar8x64                                               */      HW_Flag_SpecialImport,
          /* NI_AVX512_PermuteVar8x64x2                                             */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512_Range                                                        */      HW_Flag_NoFlag,
          /* NI_AVX512_RangeScalar                                                  */      HW_Flag_CopyUpperBits,
          /* NI_AVX512_Reciprocal14                                                 */      HW_Flag_NoFlag,
          /* NI_AVX512_Reciprocal14Scalar                                           */      HW_Flag_CopyUpperBits,
          /* NI_AVX512_ReciprocalSqrt14                                             */      HW_Flag_NoFlag,
          /* NI_AVX512_ReciprocalSqrt14Scalar                                       */      HW_Flag_CopyUpperBits,
          /* NI_AVX512_Reduce                                                       */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_ReduceScalar                                                 */      HW_Flag_FullRangeIMM | HW_Flag_CopyUpperBits,
          /* NI_AVX512_RotateLeft                                                   */      HW_Flag_MaybeIMM | HW_Flag_MaybeNoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX512_RotateLeftVariable                                           */      HW_Flag_NoFlag,
          /* NI_AVX512_RotateRight                                                  */      HW_Flag_MaybeIMM | HW_Flag_MaybeNoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX512_RotateRightVariable                                          */      HW_Flag_NoFlag,
          /* NI_AVX512_RoundScale                                                   */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_RoundScaleScalar                                             */      HW_Flag_FullRangeIMM | HW_Flag_CopyUpperBits,
          /* NI_AVX512_Scale                                                        */      HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ScaleScalar                                                  */      HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_ShiftLeftLogical                                             */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShiftLeftLogical128BitLane                                   */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShiftLeftLogicalVariable                                     */      HW_Flag_NoFlag,
          /* NI_AVX512_ShiftRightArithmetic                                         */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShiftRightArithmeticVariable                                 */      HW_Flag_NoFlag,
          /* NI_AVX512_ShiftRightLogical                                            */      HW_Flag_MaybeIMM | HW_Flag_NoJmpTableIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShiftRightLogical128BitLane                                  */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShiftRightLogicalVariable                                    */      HW_Flag_NoFlag,
          /* NI_AVX512_Shuffle                                                      */      HW_Flag_MaybeIMM | HW_Flag_FullRangeIMM,
          /* NI_AVX512_Shuffle2x128                                                 */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_Shuffle4x128                                                 */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShuffleHigh                                                  */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_ShuffleLow                                                   */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_Sqrt                                                         */      HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_SqrtScalar                                                   */      HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_Store                                                        */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_StoreAligned                                                 */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_StoreAlignedNonTemporal                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_Subtract                                                     */      HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_SubtractSaturate                                             */      HW_Flag_NoFlag,
          /* NI_AVX512_SubtractScalar                                               */      HW_Flag_CopyUpperBits | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_SumAbsoluteDifferences                                       */      HW_Flag_NoFlag,
          /* NI_AVX512_SumAbsoluteDifferencesInBlock32                              */      HW_Flag_FullRangeIMM,
          /* NI_AVX512_TernaryLogic                                                 */      HW_Flag_SpecialImport | HW_Flag_FullRangeIMM | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_UnpackHigh                                                   */      HW_Flag_NoFlag,
          /* NI_AVX512_UnpackLow                                                    */      HW_Flag_NoFlag,
          /* NI_AVX512_Xor                                                          */      HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp | HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX512_X64_ConvertScalarToVector128Double                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_CopyUpperBits | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_X64_ConvertScalarToVector128Single                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_CopyUpperBits | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_X64_ConvertToInt64                                           */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_X64_ConvertToUInt64                                          */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX512_X64_ConvertToUInt64WithTruncation                            */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512v2_MultiShift                                                 */      HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512v2_PermuteVar16x8                                             */      HW_Flag_SpecialImport,
          /* NI_AVX512v2_PermuteVar16x8x2                                           */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512v2_PermuteVar32x8                                             */      HW_Flag_SpecialImport,
          /* NI_AVX512v2_PermuteVar32x8x2                                           */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512v2_PermuteVar64x8                                             */      HW_Flag_SpecialImport,
          /* NI_AVX512v2_PermuteVar64x8x2                                           */      HW_Flag_SpecialCodeGen | HW_Flag_PermuteVar2x | HW_Flag_RmwIntrinsic,
          /* NI_AVX512v3_Compress                                                   */      HW_Flag_InvalidNodeId,
          /* NI_AVX512v3_CompressStore                                              */      HW_Flag_InvalidNodeId | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512v3_Expand                                                     */      HW_Flag_InvalidNodeId,
          /* NI_AVX512v3_ExpandLoad                                                 */      HW_Flag_InvalidNodeId,
          /* NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32             */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX10v2_ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32    */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_ConvertToInt32WithTruncatedSaturation                       */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32            */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_EmbRoundingCompatible,
          /* NI_AVX10v2_ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32   */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation                      */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation                 */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation                 */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation                */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation                */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_MinMax                                                      */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_MinMaxScalar                                                */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVX10v2_MoveScalar                                                  */      HW_Flag_NoContainment,
          /* NI_AVX10v2_MultipleSumAbsoluteDifferences                              */      HW_Flag_FullRangeIMM,
          /* NI_AVX10v2_StoreScalar                                                 */      HW_Flag_NoRMWSemantics | HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation                   */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation                  */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_SpecialCodeGen,
          /* NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction                     */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_RmwIntrinsic,
          /* NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction                    */      HW_Flag_BaseTypeFromFirstArg | HW_Flag_RmwIntrinsic,
          /* NI_AVX512BMM_ReverseBits                                               */      HW_Flag_BaseTypeFromFirstArg,
          /* NI_AVXVNNI_MultiplyWideningAndAdd                                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoEvexSemantics,
          /* NI_AVXVNNI_MultiplyWideningAndAddSaturate                              */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_NoEvexSemantics,
          /* NI_AVXVNNIINT_MultiplyWideningAndAdd                                   */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
          /* NI_AVXVNNIINT_MultiplyWideningAndAddSaturate                           */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
          /* NI_AVXVNNIINT_V512_MultiplyWideningAndAdd                              */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
          /* NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate                      */      HW_Flag_BaseTypeFromSecondArg | HW_Flag_SpecialCodeGen | HW_Flag_SpecialImport,
          /* NI_AES_CarrylessMultiply                                               */      HW_Flag_FullRangeIMM,
          /* NI_AES_Decrypt                                                         */      HW_Flag_NoEvexSemantics,
          /* NI_AES_DecryptLast                                                     */      HW_Flag_NoEvexSemantics,
          /* NI_AES_Encrypt                                                         */      HW_Flag_NoEvexSemantics,
          /* NI_AES_EncryptLast                                                     */      HW_Flag_NoEvexSemantics,
          /* NI_AES_InverseMixColumns                                               */      HW_Flag_NoEvexSemantics,
          /* NI_AES_KeygenAssist                                                    */      HW_Flag_FullRangeIMM | HW_Flag_NoEvexSemantics,
          /* NI_AES_V256_CarrylessMultiply                                          */      HW_Flag_FullRangeIMM,
          /* NI_AES_V512_CarrylessMultiply                                          */      HW_Flag_FullRangeIMM,
          /* NI_X86Serialize_Serialize                                              */      HW_Flag_NoContainment | HW_Flag_NoRMWSemantics | HW_Flag_SpecialSideEffect_Barrier,
          /* NI_GFNI_GaloisFieldAffineTransform                                     */      HW_Flag_FullRangeIMM,
          /* NI_GFNI_GaloisFieldAffineTransformInverse                              */      HW_Flag_FullRangeIMM,
          /* NI_GFNI_GaloisFieldMultiply                                            */      HW_Flag_NoFlag,
          /* NI_GFNI_V256_GaloisFieldAffineTransform                                */      HW_Flag_FullRangeIMM,
          /* NI_GFNI_V256_GaloisFieldAffineTransformInverse                         */      HW_Flag_FullRangeIMM,
          /* NI_GFNI_V256_GaloisFieldMultiply                                       */      HW_Flag_NoFlag,
          /* NI_GFNI_V512_GaloisFieldAffineTransform                                */      HW_Flag_FullRangeIMM,
          /* NI_GFNI_V512_GaloisFieldAffineTransformInverse                         */      HW_Flag_FullRangeIMM,
          /* NI_GFNI_V512_GaloisFieldMultiply                                       */      HW_Flag_NoFlag,
          /* NI_X86Base_COMIS                                                       */      HW_Flag_NoRMWSemantics,
          /* NI_X86Base_PTEST                                                       */      HW_Flag_NoRMWSemantics | HW_Flag_NoEvexSemantics,
          /* NI_X86Base_UCOMIS                                                      */      HW_Flag_NoRMWSemantics,
          /* NI_AVX_PTEST                                                           */      HW_Flag_NoEvexSemantics,
          /* NI_AVX2_AndNotVector                                                   */      HW_Flag_NormalizeSmallTypeToInt,
          /* NI_AVX2_AndNotScalar                                                   */      HW_Flag_NoFloatingPointUsed | HW_Flag_NoEvexSemantics,
          /* NI_AVX512_KORTEST                                                      */      HW_Flag_NoContainment,
          /* NI_AVX512_KTEST                                                        */      HW_Flag_NoContainment,
          /* NI_AVX512_PTESTM                                                       */      HW_Flag_Commutative,
          /* NI_AVX512_PTESTNM                                                      */      HW_Flag_Commutative,
          /* NI_AVX512_AddMask                                                      */      HW_Flag_NoContainment | HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_AndMask                                                      */      HW_Flag_NoContainment | HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_AndNotMask                                                   */      HW_Flag_NoContainment | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_BlendVariableMask                                            */      HW_Flag_NoFlag,
          /* NI_AVX512_ClassifyMask                                                 */      HW_Flag_ReturnsPerElementMask | HW_Flag_FullRangeIMM,
          /* NI_AVX512_ClassifyScalarMask                                           */      HW_Flag_ReturnsPerElementMask | HW_Flag_FullRangeIMM,
          /* NI_AVX512_CompareMask                                                  */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareEqualMask                                             */      HW_Flag_ReturnsPerElementMask | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_AVX512_CompareGreaterThanMask                                       */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareGreaterThanOrEqualMask                                */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareLessThanMask                                          */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareLessThanOrEqualMask                                   */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareNotEqualMask                                          */      HW_Flag_ReturnsPerElementMask | HW_Flag_Commutative | HW_Flag_CanBenefitFromConstantProp,
          /* NI_AVX512_CompareNotGreaterThanMask                                    */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareNotGreaterThanOrEqualMask                             */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareNotLessThanMask                                       */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareNotLessThanOrEqualMask                                */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareOrderedMask                                           */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareScalarMask                                            */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompareUnorderedMask                                         */      HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_CompressMask                                                 */      HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_CompressStoreMask                                            */      HW_Flag_NoFlag,
          /* NI_AVX512_ConvertMaskToVector                                          */      HW_Flag_NoContainment | HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_ConvertVectorToMask                                          */      HW_Flag_NoContainment | HW_Flag_ReturnsPerElementMask,
          /* NI_AVX512_ExpandLoadMask                                               */      HW_Flag_NoFlag,
          /* NI_AVX512_ExpandMask                                                   */      HW_Flag_NoFlag,
          /* NI_AVX512_MaskLoadMask                                                 */      HW_Flag_NoFlag,
          /* NI_AVX512_MaskLoadAlignedMask                                          */      HW_Flag_NoFlag,
          /* NI_AVX512_MaskStoreMask                                                */      HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_MaskStoreAlignedMask                                         */      HW_Flag_BaseTypeFromSecondArg,
          /* NI_AVX512_NotMask                                                      */      HW_Flag_NoContainment | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_OrMask                                                       */      HW_Flag_NoContainment | HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ShiftLeftMask                                                */      HW_Flag_FullRangeIMM | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_ShiftRightMask                                               */      HW_Flag_FullRangeIMM | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_XorMask                                                      */      HW_Flag_NoContainment | HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
          /* NI_AVX512_XnorMask                                                     */      HW_Flag_NoContainment | HW_Flag_Commutative | HW_Flag_ReturnsPerElementMask | HW_Flag_SpecialCodeGen,
    ];

    private static ReadOnlySpan<byte> s_fltCosts => [
        byte.MaxValue,          // NI_Vector128_Abs
        byte.MaxValue,          // NI_Vector128_AddSaturate
        byte.MaxValue,          // NI_Vector128_AndNot
        byte.MaxValue,          // NI_Vector128_As
        byte.MaxValue,          // NI_Vector128_AsByte
        byte.MaxValue,          // NI_Vector128_AsDouble
        byte.MaxValue,          // NI_Vector128_AsInt16
        byte.MaxValue,          // NI_Vector128_AsInt32
        byte.MaxValue,          // NI_Vector128_AsInt64
        byte.MaxValue,          // NI_Vector128_AsNInt
        byte.MaxValue,          // NI_Vector128_AsNUInt
        byte.MaxValue,          // NI_Vector128_AsSByte
        byte.MaxValue,          // NI_Vector128_AsSingle
        byte.MaxValue,          // NI_Vector128_AsUInt16
        byte.MaxValue,          // NI_Vector128_AsUInt32
        byte.MaxValue,          // NI_Vector128_AsUInt64
        byte.MaxValue,          // NI_Vector128_AsVector
        byte.MaxValue,          // NI_Vector128_AsVector128
        1,                      // NI_Vector128_AsVector128Unsafe
        1,                      // NI_Vector128_AsVector2
        1,                      // NI_Vector128_AsVector3
        byte.MaxValue,          // NI_Vector128_AsVector4
        byte.MaxValue,          // NI_Vector128_Ceiling
        byte.MaxValue,          // NI_Vector128_ConditionalSelect
        byte.MaxValue,          // NI_Vector128_ConvertToDouble
        byte.MaxValue,          // NI_Vector128_ConvertToInt32
        byte.MaxValue,          // NI_Vector128_ConvertToInt32Native
        byte.MaxValue,          // NI_Vector128_ConvertToInt64
        byte.MaxValue,          // NI_Vector128_ConvertToInt64Native
        byte.MaxValue,          // NI_Vector128_ConvertToSingle
        byte.MaxValue,          // NI_Vector128_ConvertToUInt32
        byte.MaxValue,          // NI_Vector128_ConvertToUInt32Native
        byte.MaxValue,          // NI_Vector128_ConvertToUInt64
        byte.MaxValue,          // NI_Vector128_ConvertToUInt64Native
        byte.MaxValue,          // NI_Vector128_Create
        byte.MaxValue,          // NI_Vector128_CreateScalar
        byte.MaxValue,          // NI_Vector128_CreateScalarUnsafe
        byte.MaxValue,          // NI_Vector128_CreateSequence
        byte.MaxValue,          // NI_Vector128_Dot
        byte.MaxValue,          // NI_Vector128_Equals
        byte.MaxValue,          // NI_Vector128_EqualsAny
        byte.MaxValue,          // NI_Vector128_ExtractMostSignificantBits
        byte.MaxValue,          // NI_Vector128_Floor
        byte.MaxValue,          // NI_Vector128_FusedMultiplyAdd
        byte.MaxValue,          // NI_Vector128_GetElement
        byte.MaxValue,          // NI_Vector128_GreaterThan
        byte.MaxValue,          // NI_Vector128_GreaterThanAll
        byte.MaxValue,          // NI_Vector128_GreaterThanAny
        byte.MaxValue,          // NI_Vector128_GreaterThanOrEqual
        byte.MaxValue,          // NI_Vector128_GreaterThanOrEqualAll
        byte.MaxValue,          // NI_Vector128_GreaterThanOrEqualAny
        byte.MaxValue,          // NI_Vector128_IsEvenInteger
        byte.MaxValue,          // NI_Vector128_IsFinite
        byte.MaxValue,          // NI_Vector128_IsInfinity
        byte.MaxValue,          // NI_Vector128_IsInteger
        byte.MaxValue,          // NI_Vector128_IsNaN
        byte.MaxValue,          // NI_Vector128_IsNegative
        byte.MaxValue,          // NI_Vector128_IsNegativeInfinity
        byte.MaxValue,          // NI_Vector128_IsNormal
        byte.MaxValue,          // NI_Vector128_IsOddInteger
        byte.MaxValue,          // NI_Vector128_IsPositive
        byte.MaxValue,          // NI_Vector128_IsPositiveInfinity
        byte.MaxValue,          // NI_Vector128_IsSubnormal
        byte.MaxValue,          // NI_Vector128_IsZero
        byte.MaxValue,          // NI_Vector128_LessThan
        byte.MaxValue,          // NI_Vector128_LessThanAll
        byte.MaxValue,          // NI_Vector128_LessThanAny
        byte.MaxValue,          // NI_Vector128_LessThanOrEqual
        byte.MaxValue,          // NI_Vector128_LessThanOrEqualAll
        byte.MaxValue,          // NI_Vector128_LessThanOrEqualAny
        byte.MaxValue,          // NI_Vector128_LoadAligned
        byte.MaxValue,          // NI_Vector128_LoadAlignedNonTemporal
        byte.MaxValue,          // NI_Vector128_LoadUnsafe
        byte.MaxValue,          // NI_Vector128_Max
        byte.MaxValue,          // NI_Vector128_MaxMagnitude
        byte.MaxValue,          // NI_Vector128_MaxMagnitudeNumber
        byte.MaxValue,          // NI_Vector128_MaxNative
        byte.MaxValue,          // NI_Vector128_MaxNumber
        byte.MaxValue,          // NI_Vector128_Min
        byte.MaxValue,          // NI_Vector128_MinMagnitude
        byte.MaxValue,          // NI_Vector128_MinMagnitudeNumber
        byte.MaxValue,          // NI_Vector128_MinNative
        byte.MaxValue,          // NI_Vector128_MinNumber
        byte.MaxValue,          // NI_Vector128_MultiplyAddEstimate
        byte.MaxValue,          // NI_Vector128_Narrow
        byte.MaxValue,          // NI_Vector128_NarrowWithSaturation
        byte.MaxValue,          // NI_Vector128_Round
        byte.MaxValue,          // NI_Vector128_ShiftLeft
        byte.MaxValue,          // NI_Vector128_Shuffle
        byte.MaxValue,          // NI_Vector128_ShuffleNative
        byte.MaxValue,          // NI_Vector128_ShuffleNativeFallback
        byte.MaxValue,          // NI_Vector128_Sqrt
        byte.MaxValue,          // NI_Vector128_StoreAligned
        byte.MaxValue,          // NI_Vector128_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_Vector128_StoreUnsafe
        byte.MaxValue,          // NI_Vector128_SubtractSaturate
        byte.MaxValue,          // NI_Vector128_Sum
        byte.MaxValue,          // NI_Vector128_ToScalar
        byte.MaxValue,          // NI_Vector128_ToVector256
        byte.MaxValue,          // NI_Vector128_ToVector256Unsafe
        byte.MaxValue,          // NI_Vector128_ToVector512
        byte.MaxValue,          // NI_Vector128_Truncate
        byte.MaxValue,          // NI_Vector128_WidenLower
        byte.MaxValue,          // NI_Vector128_WidenUpper
        byte.MaxValue,          // NI_Vector128_WithElement
        byte.MaxValue,          // NI_Vector128_get_AllBitsSet
        byte.MaxValue,          // NI_Vector128_get_E
        byte.MaxValue,          // NI_Vector128_get_Epsilon
        byte.MaxValue,          // NI_Vector128_get_Indices
        byte.MaxValue,          // NI_Vector128_get_NaN
        byte.MaxValue,          // NI_Vector128_get_NegativeInfinity
        byte.MaxValue,          // NI_Vector128_get_NegativeOne
        byte.MaxValue,          // NI_Vector128_get_NegativeZero
        byte.MaxValue,          // NI_Vector128_get_One
        byte.MaxValue,          // NI_Vector128_get_Pi
        byte.MaxValue,          // NI_Vector128_get_PositiveInfinity
        byte.MaxValue,          // NI_Vector128_get_Tau
        byte.MaxValue,          // NI_Vector128_get_Zero
        byte.MaxValue,          // NI_Vector128_op_Addition
        byte.MaxValue,          // NI_Vector128_op_BitwiseAnd
        byte.MaxValue,          // NI_Vector128_op_BitwiseOr
        byte.MaxValue,          // NI_Vector128_op_Division
        byte.MaxValue,          // NI_Vector128_op_Equality
        byte.MaxValue,          // NI_Vector128_op_ExclusiveOr
        byte.MaxValue,          // NI_Vector128_op_Inequality
        byte.MaxValue,          // NI_Vector128_op_LeftShift
        byte.MaxValue,          // NI_Vector128_op_Multiply
        byte.MaxValue,          // NI_Vector128_op_OnesComplement
        byte.MaxValue,          // NI_Vector128_op_RightShift
        byte.MaxValue,          // NI_Vector128_op_Subtraction
        byte.MaxValue,          // NI_Vector128_op_UnaryNegation
        byte.MaxValue,          // NI_Vector128_op_UnaryPlus
        byte.MaxValue,          // NI_Vector128_op_UnsignedRightShift
        byte.MaxValue,          // NI_Vector256_Abs
        byte.MaxValue,          // NI_Vector256_AddSaturate
        byte.MaxValue,          // NI_Vector256_AndNot
        byte.MaxValue,          // NI_Vector256_As
        byte.MaxValue,          // NI_Vector256_AsByte
        byte.MaxValue,          // NI_Vector256_AsDouble
        byte.MaxValue,          // NI_Vector256_AsInt16
        byte.MaxValue,          // NI_Vector256_AsInt32
        byte.MaxValue,          // NI_Vector256_AsInt64
        byte.MaxValue,          // NI_Vector256_AsNInt
        byte.MaxValue,          // NI_Vector256_AsNUInt
        byte.MaxValue,          // NI_Vector256_AsSByte
        byte.MaxValue,          // NI_Vector256_AsSingle
        byte.MaxValue,          // NI_Vector256_AsUInt16
        byte.MaxValue,          // NI_Vector256_AsUInt32
        byte.MaxValue,          // NI_Vector256_AsUInt64
        byte.MaxValue,          // NI_Vector256_AsVector
        byte.MaxValue,          // NI_Vector256_AsVector256
        byte.MaxValue,          // NI_Vector256_Ceiling
        byte.MaxValue,          // NI_Vector256_ConditionalSelect
        byte.MaxValue,          // NI_Vector256_ConvertToDouble
        byte.MaxValue,          // NI_Vector256_ConvertToInt32
        byte.MaxValue,          // NI_Vector256_ConvertToInt32Native
        byte.MaxValue,          // NI_Vector256_ConvertToInt64
        byte.MaxValue,          // NI_Vector256_ConvertToInt64Native
        byte.MaxValue,          // NI_Vector256_ConvertToSingle
        byte.MaxValue,          // NI_Vector256_ConvertToUInt32
        byte.MaxValue,          // NI_Vector256_ConvertToUInt32Native
        byte.MaxValue,          // NI_Vector256_ConvertToUInt64
        byte.MaxValue,          // NI_Vector256_ConvertToUInt64Native
        byte.MaxValue,          // NI_Vector256_Create
        byte.MaxValue,          // NI_Vector256_CreateScalar
        byte.MaxValue,          // NI_Vector256_CreateScalarUnsafe
        byte.MaxValue,          // NI_Vector256_CreateSequence
        byte.MaxValue,          // NI_Vector256_Dot
        byte.MaxValue,          // NI_Vector256_Equals
        byte.MaxValue,          // NI_Vector256_EqualsAny
        byte.MaxValue,          // NI_Vector256_ExtractMostSignificantBits
        byte.MaxValue,          // NI_Vector256_Floor
        byte.MaxValue,          // NI_Vector256_FusedMultiplyAdd
        byte.MaxValue,          // NI_Vector256_GetElement
        byte.MaxValue,          // NI_Vector256_GetLower
        byte.MaxValue,          // NI_Vector256_GetUpper
        byte.MaxValue,          // NI_Vector256_GreaterThan
        byte.MaxValue,          // NI_Vector256_GreaterThanAll
        byte.MaxValue,          // NI_Vector256_GreaterThanAny
        byte.MaxValue,          // NI_Vector256_GreaterThanOrEqual
        byte.MaxValue,          // NI_Vector256_GreaterThanOrEqualAll
        byte.MaxValue,          // NI_Vector256_GreaterThanOrEqualAny
        byte.MaxValue,          // NI_Vector256_IsEvenInteger
        byte.MaxValue,          // NI_Vector256_IsFinite
        byte.MaxValue,          // NI_Vector256_IsInfinity
        byte.MaxValue,          // NI_Vector256_IsInteger
        byte.MaxValue,          // NI_Vector256_IsNaN
        byte.MaxValue,          // NI_Vector256_IsNegative
        byte.MaxValue,          // NI_Vector256_IsNegativeInfinity
        byte.MaxValue,          // NI_Vector256_IsNormal
        byte.MaxValue,          // NI_Vector256_IsOddInteger
        byte.MaxValue,          // NI_Vector256_IsPositive
        byte.MaxValue,          // NI_Vector256_IsPositiveInfinity
        byte.MaxValue,          // NI_Vector256_IsSubnormal
        byte.MaxValue,          // NI_Vector256_IsZero
        byte.MaxValue,          // NI_Vector256_LessThan
        byte.MaxValue,          // NI_Vector256_LessThanAll
        byte.MaxValue,          // NI_Vector256_LessThanAny
        byte.MaxValue,          // NI_Vector256_LessThanOrEqual
        byte.MaxValue,          // NI_Vector256_LessThanOrEqualAll
        byte.MaxValue,          // NI_Vector256_LessThanOrEqualAny
        byte.MaxValue,          // NI_Vector256_LoadAligned
        byte.MaxValue,          // NI_Vector256_LoadAlignedNonTemporal
        byte.MaxValue,          // NI_Vector256_LoadUnsafe
        byte.MaxValue,          // NI_Vector256_Max
        byte.MaxValue,          // NI_Vector256_MaxMagnitude
        byte.MaxValue,          // NI_Vector256_MaxMagnitudeNumber
        byte.MaxValue,          // NI_Vector256_MaxNative
        byte.MaxValue,          // NI_Vector256_MaxNumber
        byte.MaxValue,          // NI_Vector256_Min
        byte.MaxValue,          // NI_Vector256_MinMagnitude
        byte.MaxValue,          // NI_Vector256_MinMagnitudeNumber
        byte.MaxValue,          // NI_Vector256_MinNative
        byte.MaxValue,          // NI_Vector256_MinNumber
        byte.MaxValue,          // NI_Vector256_MultiplyAddEstimate
        byte.MaxValue,          // NI_Vector256_Narrow
        byte.MaxValue,          // NI_Vector256_NarrowWithSaturation
        byte.MaxValue,          // NI_Vector256_Round
        byte.MaxValue,          // NI_Vector256_ShiftLeft
        byte.MaxValue,          // NI_Vector256_Shuffle
        byte.MaxValue,          // NI_Vector256_ShuffleNative
        byte.MaxValue,          // NI_Vector256_ShuffleNativeFallback
        byte.MaxValue,          // NI_Vector256_Sqrt
        byte.MaxValue,          // NI_Vector256_StoreAligned
        byte.MaxValue,          // NI_Vector256_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_Vector256_StoreUnsafe
        byte.MaxValue,          // NI_Vector256_SubtractSaturate
        byte.MaxValue,          // NI_Vector256_Sum
        byte.MaxValue,          // NI_Vector256_ToScalar
        byte.MaxValue,          // NI_Vector256_ToVector512
        byte.MaxValue,          // NI_Vector256_ToVector512Unsafe
        byte.MaxValue,          // NI_Vector256_Truncate
        byte.MaxValue,          // NI_Vector256_WidenLower
        byte.MaxValue,          // NI_Vector256_WidenUpper
        byte.MaxValue,          // NI_Vector256_WithElement
        byte.MaxValue,          // NI_Vector256_WithLower
        byte.MaxValue,          // NI_Vector256_WithUpper
        byte.MaxValue,          // NI_Vector256_get_AllBitsSet
        byte.MaxValue,          // NI_Vector256_get_E
        byte.MaxValue,          // NI_Vector256_get_Epsilon
        byte.MaxValue,          // NI_Vector256_get_Indices
        byte.MaxValue,          // NI_Vector256_get_NaN
        byte.MaxValue,          // NI_Vector256_get_NegativeInfinity
        byte.MaxValue,          // NI_Vector256_get_NegativeOne
        byte.MaxValue,          // NI_Vector256_get_NegativeZero
        byte.MaxValue,          // NI_Vector256_get_One
        byte.MaxValue,          // NI_Vector256_get_Pi
        byte.MaxValue,          // NI_Vector256_get_PositiveInfinity
        byte.MaxValue,          // NI_Vector256_get_Tau
        byte.MaxValue,          // NI_Vector256_get_Zero
        byte.MaxValue,          // NI_Vector256_op_Addition
        byte.MaxValue,          // NI_Vector256_op_BitwiseAnd
        byte.MaxValue,          // NI_Vector256_op_BitwiseOr
        byte.MaxValue,          // NI_Vector256_op_Division
        byte.MaxValue,          // NI_Vector256_op_Equality
        byte.MaxValue,          // NI_Vector256_op_ExclusiveOr
        byte.MaxValue,          // NI_Vector256_op_Inequality
        byte.MaxValue,          // NI_Vector256_op_LeftShift
        byte.MaxValue,          // NI_Vector256_op_Multiply
        byte.MaxValue,          // NI_Vector256_op_OnesComplement
        byte.MaxValue,          // NI_Vector256_op_RightShift
        byte.MaxValue,          // NI_Vector256_op_Subtraction
        byte.MaxValue,          // NI_Vector256_op_UnaryNegation
        byte.MaxValue,          // NI_Vector256_op_UnaryPlus
        byte.MaxValue,          // NI_Vector256_op_UnsignedRightShift
        byte.MaxValue,          // NI_Vector512_Abs
        byte.MaxValue,          // NI_Vector512_AddSaturate
        byte.MaxValue,          // NI_Vector512_AndNot
        byte.MaxValue,          // NI_Vector512_As
        byte.MaxValue,          // NI_Vector512_AsByte
        byte.MaxValue,          // NI_Vector512_AsDouble
        byte.MaxValue,          // NI_Vector512_AsInt16
        byte.MaxValue,          // NI_Vector512_AsInt32
        byte.MaxValue,          // NI_Vector512_AsInt64
        byte.MaxValue,          // NI_Vector512_AsNInt
        byte.MaxValue,          // NI_Vector512_AsNUInt
        byte.MaxValue,          // NI_Vector512_AsSByte
        byte.MaxValue,          // NI_Vector512_AsSingle
        byte.MaxValue,          // NI_Vector512_AsUInt16
        byte.MaxValue,          // NI_Vector512_AsUInt32
        byte.MaxValue,          // NI_Vector512_AsUInt64
        byte.MaxValue,          // NI_Vector512_AsVector
        byte.MaxValue,          // NI_Vector512_AsVector512
        byte.MaxValue,          // NI_Vector512_Ceiling
        byte.MaxValue,          // NI_Vector512_ConditionalSelect
        byte.MaxValue,          // NI_Vector512_ConvertToDouble
        byte.MaxValue,          // NI_Vector512_ConvertToInt32
        byte.MaxValue,          // NI_Vector512_ConvertToInt32Native
        byte.MaxValue,          // NI_Vector512_ConvertToInt64
        byte.MaxValue,          // NI_Vector512_ConvertToInt64Native
        byte.MaxValue,          // NI_Vector512_ConvertToSingle
        byte.MaxValue,          // NI_Vector512_ConvertToUInt32
        byte.MaxValue,          // NI_Vector512_ConvertToUInt32Native
        byte.MaxValue,          // NI_Vector512_ConvertToUInt64
        byte.MaxValue,          // NI_Vector512_ConvertToUInt64Native
        byte.MaxValue,          // NI_Vector512_Create
        byte.MaxValue,          // NI_Vector512_CreateScalar
        byte.MaxValue,          // NI_Vector512_CreateScalarUnsafe
        byte.MaxValue,          // NI_Vector512_CreateSequence
        byte.MaxValue,          // NI_Vector512_Dot
        byte.MaxValue,          // NI_Vector512_Equals
        byte.MaxValue,          // NI_Vector512_EqualsAny
        byte.MaxValue,          // NI_Vector512_ExtractMostSignificantBits
        byte.MaxValue,          // NI_Vector512_Floor
        byte.MaxValue,          // NI_Vector512_FusedMultiplyAdd
        byte.MaxValue,          // NI_Vector512_GetElement
        byte.MaxValue,          // NI_Vector512_GetLower
        byte.MaxValue,          // NI_Vector512_GetLower128
        byte.MaxValue,          // NI_Vector512_GetUpper
        byte.MaxValue,          // NI_Vector512_GreaterThan
        byte.MaxValue,          // NI_Vector512_GreaterThanAll
        byte.MaxValue,          // NI_Vector512_GreaterThanAny
        byte.MaxValue,          // NI_Vector512_GreaterThanOrEqual
        byte.MaxValue,          // NI_Vector512_GreaterThanOrEqualAll
        byte.MaxValue,          // NI_Vector512_GreaterThanOrEqualAny
        byte.MaxValue,          // NI_Vector512_IsEvenInteger
        byte.MaxValue,          // NI_Vector512_IsFinite
        byte.MaxValue,          // NI_Vector512_IsInfinity
        byte.MaxValue,          // NI_Vector512_IsInteger
        byte.MaxValue,          // NI_Vector512_IsNaN
        byte.MaxValue,          // NI_Vector512_IsNegative
        byte.MaxValue,          // NI_Vector512_IsNegativeInfinity
        byte.MaxValue,          // NI_Vector512_IsNormal
        byte.MaxValue,          // NI_Vector512_IsOddInteger
        byte.MaxValue,          // NI_Vector512_IsPositive
        byte.MaxValue,          // NI_Vector512_IsPositiveInfinity
        byte.MaxValue,          // NI_Vector512_IsSubnormal
        byte.MaxValue,          // NI_Vector512_IsZero
        byte.MaxValue,          // NI_Vector512_LessThan
        byte.MaxValue,          // NI_Vector512_LessThanAll
        byte.MaxValue,          // NI_Vector512_LessThanAny
        byte.MaxValue,          // NI_Vector512_LessThanOrEqual
        byte.MaxValue,          // NI_Vector512_LessThanOrEqualAll
        byte.MaxValue,          // NI_Vector512_LessThanOrEqualAny
        byte.MaxValue,          // NI_Vector512_LoadAligned
        byte.MaxValue,          // NI_Vector512_LoadAlignedNonTemporal
        byte.MaxValue,          // NI_Vector512_LoadUnsafe
        byte.MaxValue,          // NI_Vector512_Max
        byte.MaxValue,          // NI_Vector512_MaxMagnitude
        byte.MaxValue,          // NI_Vector512_MaxMagnitudeNumber
        byte.MaxValue,          // NI_Vector512_MaxNative
        byte.MaxValue,          // NI_Vector512_MaxNumber
        byte.MaxValue,          // NI_Vector512_Min
        byte.MaxValue,          // NI_Vector512_MinMagnitude
        byte.MaxValue,          // NI_Vector512_MinMagnitudeNumber
        byte.MaxValue,          // NI_Vector512_MinNative
        byte.MaxValue,          // NI_Vector512_MinNumber
        byte.MaxValue,          // NI_Vector512_MultiplyAddEstimate
        byte.MaxValue,          // NI_Vector512_Narrow
        byte.MaxValue,          // NI_Vector512_NarrowWithSaturation
        byte.MaxValue,          // NI_Vector512_Round
        byte.MaxValue,          // NI_Vector512_ShiftLeft
        byte.MaxValue,          // NI_Vector512_Shuffle
        byte.MaxValue,          // NI_Vector512_ShuffleNative
        byte.MaxValue,          // NI_Vector512_ShuffleNativeFallback
        byte.MaxValue,          // NI_Vector512_Sqrt
        byte.MaxValue,          // NI_Vector512_StoreAligned
        byte.MaxValue,          // NI_Vector512_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_Vector512_StoreUnsafe
        byte.MaxValue,          // NI_Vector512_SubtractSaturate
        byte.MaxValue,          // NI_Vector512_Sum
        byte.MaxValue,          // NI_Vector512_ToScalar
        byte.MaxValue,          // NI_Vector512_Truncate
        byte.MaxValue,          // NI_Vector512_WidenLower
        byte.MaxValue,          // NI_Vector512_WidenUpper
        byte.MaxValue,          // NI_Vector512_WithElement
        byte.MaxValue,          // NI_Vector512_WithLower
        byte.MaxValue,          // NI_Vector512_WithUpper
        byte.MaxValue,          // NI_Vector512_get_AllBitsSet
        byte.MaxValue,          // NI_Vector512_get_E
        byte.MaxValue,          // NI_Vector512_get_Epsilon
        byte.MaxValue,          // NI_Vector512_get_Indices
        byte.MaxValue,          // NI_Vector512_get_NaN
        byte.MaxValue,          // NI_Vector512_get_NegativeInfinity
        byte.MaxValue,          // NI_Vector512_get_NegativeOne
        byte.MaxValue,          // NI_Vector512_get_NegativeZero
        byte.MaxValue,          // NI_Vector512_get_One
        byte.MaxValue,          // NI_Vector512_get_Pi
        byte.MaxValue,          // NI_Vector512_get_PositiveInfinity
        byte.MaxValue,          // NI_Vector512_get_Tau
        byte.MaxValue,          // NI_Vector512_get_Zero
        byte.MaxValue,          // NI_Vector512_op_Addition
        byte.MaxValue,          // NI_Vector512_op_BitwiseAnd
        byte.MaxValue,          // NI_Vector512_op_BitwiseOr
        byte.MaxValue,          // NI_Vector512_op_Division
        byte.MaxValue,          // NI_Vector512_op_Equality
        byte.MaxValue,          // NI_Vector512_op_ExclusiveOr
        byte.MaxValue,          // NI_Vector512_op_Inequality
        byte.MaxValue,          // NI_Vector512_op_LeftShift
        byte.MaxValue,          // NI_Vector512_op_Multiply
        byte.MaxValue,          // NI_Vector512_op_OnesComplement
        byte.MaxValue,          // NI_Vector512_op_RightShift
        byte.MaxValue,          // NI_Vector512_op_Subtraction
        byte.MaxValue,          // NI_Vector512_op_UnaryNegation
        byte.MaxValue,          // NI_Vector512_op_UnaryPlus
        byte.MaxValue,          // NI_Vector512_op_UnsignedRightShift
        byte.MaxValue,          // NI_X86Base_Abs
        4,                      // NI_X86Base_Add
        byte.MaxValue,          // NI_X86Base_AddSaturate
        4,                      // NI_X86Base_AddScalar
        4,                      // NI_X86Base_AddSubtract
        byte.MaxValue,          // NI_X86Base_AlignRight
        1,                      // NI_X86Base_And
        1,                      // NI_X86Base_AndNot
        byte.MaxValue,          // NI_X86Base_Average
        byte.MaxValue,          // NI_X86Base_BitScanForward
        byte.MaxValue,          // NI_X86Base_BitScanReverse
        1,                      // NI_X86Base_Blend
        1,                      // NI_X86Base_BlendVariable
        8,                      // NI_X86Base_Ceiling
        8,                      // NI_X86Base_CeilingScalar
        4,                      // NI_X86Base_CompareEqual
        4,                      // NI_X86Base_CompareGreaterThan
        4,                      // NI_X86Base_CompareGreaterThanOrEqual
        4,                      // NI_X86Base_CompareLessThan
        4,                      // NI_X86Base_CompareLessThanOrEqual
        4,                      // NI_X86Base_CompareNotEqual
        4,                      // NI_X86Base_CompareNotGreaterThan
        4,                      // NI_X86Base_CompareNotGreaterThanOrEqual
        4,                      // NI_X86Base_CompareNotLessThan
        4,                      // NI_X86Base_CompareNotLessThanOrEqual
        4,                      // NI_X86Base_CompareOrdered
        4,                      // NI_X86Base_CompareScalarEqual
        4,                      // NI_X86Base_CompareScalarGreaterThan
        4,                      // NI_X86Base_CompareScalarGreaterThanOrEqual
        4,                      // NI_X86Base_CompareScalarLessThan
        4,                      // NI_X86Base_CompareScalarLessThanOrEqual
        4,                      // NI_X86Base_CompareScalarNotEqual
        4,                      // NI_X86Base_CompareScalarNotGreaterThan
        4,                      // NI_X86Base_CompareScalarNotGreaterThanOrEqual
        4,                      // NI_X86Base_CompareScalarNotLessThan
        4,                      // NI_X86Base_CompareScalarNotLessThanOrEqual
        4,                      // NI_X86Base_CompareScalarOrdered
        3,                      // NI_X86Base_CompareScalarOrderedEqual
        3,                      // NI_X86Base_CompareScalarOrderedGreaterThan
        3,                      // NI_X86Base_CompareScalarOrderedGreaterThanOrEqual
        3,                      // NI_X86Base_CompareScalarOrderedLessThan
        3,                      // NI_X86Base_CompareScalarOrderedLessThanOrEqual
        3,                      // NI_X86Base_CompareScalarOrderedNotEqual
        4,                      // NI_X86Base_CompareScalarUnordered
        3,                      // NI_X86Base_CompareScalarUnorderedEqual
        3,                      // NI_X86Base_CompareScalarUnorderedGreaterThan
        3,                      // NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual
        3,                      // NI_X86Base_CompareScalarUnorderedLessThan
        3,                      // NI_X86Base_CompareScalarUnorderedLessThanOrEqual
        3,                      // NI_X86Base_CompareScalarUnorderedNotEqual
        4,                      // NI_X86Base_CompareUnordered
        4,                      // NI_X86Base_ConvertScalarToVector128Double
        byte.MaxValue,          // NI_X86Base_ConvertScalarToVector128Int32
        4,                      // NI_X86Base_ConvertScalarToVector128Single
        byte.MaxValue,          // NI_X86Base_ConvertScalarToVector128UInt32
        7,                      // NI_X86Base_ConvertToInt32
        7,                      // NI_X86Base_ConvertToInt32WithTruncation
        byte.MaxValue,          // NI_X86Base_ConvertToUInt32
        5,                      // NI_X86Base_ConvertToVector128Double
        byte.MaxValue,          // NI_X86Base_ConvertToVector128Int16
        4,                      // NI_X86Base_ConvertToVector128Int32
        4,                      // NI_X86Base_ConvertToVector128Int32WithTruncation
        byte.MaxValue,          // NI_X86Base_ConvertToVector128Int64
        5,                      // NI_X86Base_ConvertToVector128Single
        byte.MaxValue,          // NI_X86Base_Crc32
        byte.MaxValue,          // NI_X86Base_DivRem
        byte.MaxValue,          // NI_X86Base_Divide
        byte.MaxValue,          // NI_X86Base_DivideScalar
        byte.MaxValue,          // NI_X86Base_DotProduct
        4,                      // NI_X86Base_Extract
        8,                      // NI_X86Base_Floor
        8,                      // NI_X86Base_FloorScalar
        6,                      // NI_X86Base_HorizontalAdd
        byte.MaxValue,          // NI_X86Base_HorizontalAddSaturate
        6,                      // NI_X86Base_HorizontalSubtract
        byte.MaxValue,          // NI_X86Base_HorizontalSubtractSaturate
        1,                      // NI_X86Base_Insert
        byte.MaxValue,          // NI_X86Base_LoadAlignedVector128
        byte.MaxValue,          // NI_X86Base_LoadAlignedVector128NonTemporal
        byte.MaxValue,          // NI_X86Base_LoadAndDuplicateToVector128
        byte.MaxValue,          // NI_X86Base_LoadDquVector128
        byte.MaxValue,          // NI_X86Base_LoadFence
        byte.MaxValue,          // NI_X86Base_LoadHigh
        byte.MaxValue,          // NI_X86Base_LoadLow
        byte.MaxValue,          // NI_X86Base_LoadScalarVector128
        byte.MaxValue,          // NI_X86Base_LoadVector128
        byte.MaxValue,          // NI_X86Base_MaskMove
        4,                      // NI_X86Base_Max
        4,                      // NI_X86Base_MaxScalar
        byte.MaxValue,          // NI_X86Base_MemoryFence
        4,                      // NI_X86Base_Min
        byte.MaxValue,          // NI_X86Base_MinHorizontal
        4,                      // NI_X86Base_MinScalar
        1,                      // NI_X86Base_MoveAndDuplicate
        1,                      // NI_X86Base_MoveHighAndDuplicate
        1,                      // NI_X86Base_MoveHighToLow
        1,                      // NI_X86Base_MoveLowAndDuplicate
        1,                      // NI_X86Base_MoveLowToHigh
        3,                      // NI_X86Base_MoveMask
        1,                      // NI_X86Base_MoveScalar
        byte.MaxValue,          // NI_X86Base_MultipleSumAbsoluteDifferences
        4,                      // NI_X86Base_Multiply
        byte.MaxValue,          // NI_X86Base_MultiplyAddAdjacent
        byte.MaxValue,          // NI_X86Base_MultiplyHigh
        byte.MaxValue,          // NI_X86Base_MultiplyHighRoundScale
        byte.MaxValue,          // NI_X86Base_MultiplyLow
        4,                      // NI_X86Base_MultiplyScalar
        1,                      // NI_X86Base_Or
        byte.MaxValue,          // NI_X86Base_PackSignedSaturate
        byte.MaxValue,          // NI_X86Base_PackUnsignedSaturate
        byte.MaxValue,          // NI_X86Base_Pause
        byte.MaxValue,          // NI_X86Base_PopCount
        byte.MaxValue,          // NI_X86Base_Prefetch0
        byte.MaxValue,          // NI_X86Base_Prefetch1
        byte.MaxValue,          // NI_X86Base_Prefetch2
        byte.MaxValue,          // NI_X86Base_PrefetchNonTemporal
        4,                      // NI_X86Base_Reciprocal
        4,                      // NI_X86Base_ReciprocalScalar
        4,                      // NI_X86Base_ReciprocalSqrt
        4,                      // NI_X86Base_ReciprocalSqrtScalar
        8,                      // NI_X86Base_RoundCurrentDirection
        8,                      // NI_X86Base_RoundCurrentDirectionScalar
        8,                      // NI_X86Base_RoundToNearestInteger
        8,                      // NI_X86Base_RoundToNearestIntegerScalar
        8,                      // NI_X86Base_RoundToNegativeInfinity
        8,                      // NI_X86Base_RoundToNegativeInfinityScalar
        8,                      // NI_X86Base_RoundToPositiveInfinity
        8,                      // NI_X86Base_RoundToPositiveInfinityScalar
        8,                      // NI_X86Base_RoundToZero
        8,                      // NI_X86Base_RoundToZeroScalar
        byte.MaxValue,          // NI_X86Base_ShiftLeftLogical
        byte.MaxValue,          // NI_X86Base_ShiftLeftLogical128BitLane
        byte.MaxValue,          // NI_X86Base_ShiftRightArithmetic
        byte.MaxValue,          // NI_X86Base_ShiftRightLogical
        byte.MaxValue,          // NI_X86Base_ShiftRightLogical128BitLane
        1,                      // NI_X86Base_Shuffle
        byte.MaxValue,          // NI_X86Base_ShuffleHigh
        byte.MaxValue,          // NI_X86Base_ShuffleLow
        byte.MaxValue,          // NI_X86Base_Sign
        byte.MaxValue,          // NI_X86Base_Sqrt
        byte.MaxValue,          // NI_X86Base_SqrtScalar
        byte.MaxValue,          // NI_X86Base_Store
        byte.MaxValue,          // NI_X86Base_StoreAligned
        byte.MaxValue,          // NI_X86Base_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_X86Base_StoreFence
        byte.MaxValue,          // NI_X86Base_StoreHigh
        byte.MaxValue,          // NI_X86Base_StoreLow
        byte.MaxValue,          // NI_X86Base_StoreNonTemporal
        byte.MaxValue,          // NI_X86Base_StoreScalar
        4,                      // NI_X86Base_Subtract
        byte.MaxValue,          // NI_X86Base_SubtractSaturate
        4,                      // NI_X86Base_SubtractScalar
        byte.MaxValue,          // NI_X86Base_SumAbsoluteDifferences
        byte.MaxValue,          // NI_X86Base_TestC
        byte.MaxValue,          // NI_X86Base_TestNotZAndNotC
        byte.MaxValue,          // NI_X86Base_TestZ
        1,                      // NI_X86Base_UnpackHigh
        1,                      // NI_X86Base_UnpackLow
        1,                      // NI_X86Base_Xor
        byte.MaxValue,          // NI_X86Base_X64_BigMul
        byte.MaxValue,          // NI_X86Base_X64_BitScanForward
        byte.MaxValue,          // NI_X86Base_X64_BitScanReverse
        byte.MaxValue,          // NI_X86Base_X64_ConvertScalarToVector128Double
        byte.MaxValue,          // NI_X86Base_X64_ConvertScalarToVector128Int64
        byte.MaxValue,          // NI_X86Base_X64_ConvertScalarToVector128Single
        byte.MaxValue,          // NI_X86Base_X64_ConvertScalarToVector128UInt64
        7,                      // NI_X86Base_X64_ConvertToInt64
        7,                      // NI_X86Base_X64_ConvertToInt64WithTruncation
        byte.MaxValue,          // NI_X86Base_X64_ConvertToUInt64
        byte.MaxValue,          // NI_X86Base_X64_Crc32
        byte.MaxValue,          // NI_X86Base_X64_DivRem
        byte.MaxValue,          // NI_X86Base_X64_Extract
        byte.MaxValue,          // NI_X86Base_X64_Insert
        byte.MaxValue,          // NI_X86Base_X64_PopCount
        byte.MaxValue,          // NI_X86Base_X64_StoreNonTemporal
        4,                      // NI_AVX_Add
        4,                      // NI_AVX_AddSubtract
        1,                      // NI_AVX_And
        1,                      // NI_AVX_AndNot
        1,                      // NI_AVX_Blend
        1,                      // NI_AVX_BlendVariable
        byte.MaxValue,          // NI_AVX_BroadcastScalarToVector128
        byte.MaxValue,          // NI_AVX_BroadcastScalarToVector256
        byte.MaxValue,          // NI_AVX_BroadcastVector128ToVector256
        8,                      // NI_AVX_Ceiling
        4,                      // NI_AVX_Compare
        4,                      // NI_AVX_CompareEqual
        4,                      // NI_AVX_CompareGreaterThan
        4,                      // NI_AVX_CompareGreaterThanOrEqual
        4,                      // NI_AVX_CompareLessThan
        4,                      // NI_AVX_CompareLessThanOrEqual
        4,                      // NI_AVX_CompareNotEqual
        4,                      // NI_AVX_CompareNotGreaterThan
        4,                      // NI_AVX_CompareNotGreaterThanOrEqual
        4,                      // NI_AVX_CompareNotLessThan
        4,                      // NI_AVX_CompareNotLessThanOrEqual
        4,                      // NI_AVX_CompareOrdered
        4,                      // NI_AVX_CompareScalar
        4,                      // NI_AVX_CompareUnordered
        7,                      // NI_AVX_ConvertToVector128Int32
        byte.MaxValue,          // NI_AVX_ConvertToVector128Int32WithTruncation
        7,                      // NI_AVX_ConvertToVector128Single
        7,                      // NI_AVX_ConvertToVector256Double
        4,                      // NI_AVX_ConvertToVector256Int32
        4,                      // NI_AVX_ConvertToVector256Int32WithTruncation
        byte.MaxValue,          // NI_AVX_ConvertToVector256Single
        byte.MaxValue,          // NI_AVX_Divide
        13,                     // NI_AVX_DotProduct
        1,                      // NI_AVX_DuplicateEvenIndexed
        1,                      // NI_AVX_DuplicateOddIndexed
        3,                      // NI_AVX_ExtractVector128
        8,                      // NI_AVX_Floor
        6,                      // NI_AVX_HorizontalAdd
        6,                      // NI_AVX_HorizontalSubtract
        3,                      // NI_AVX_InsertVector128
        byte.MaxValue,          // NI_AVX_LoadAlignedVector256
        byte.MaxValue,          // NI_AVX_LoadDquVector256
        byte.MaxValue,          // NI_AVX_LoadVector256
        byte.MaxValue,          // NI_AVX_MaskLoad
        byte.MaxValue,          // NI_AVX_MaskStore
        4,                      // NI_AVX_Max
        4,                      // NI_AVX_Min
        5,                      // NI_AVX_MoveMask
        4,                      // NI_AVX_Multiply
        1,                      // NI_AVX_Or
        1,                      // NI_AVX_Permute
        3,                      // NI_AVX_Permute2x128
        1,                      // NI_AVX_PermuteVar
        4,                      // NI_AVX_Reciprocal
        4,                      // NI_AVX_ReciprocalSqrt
        8,                      // NI_AVX_RoundCurrentDirection
        8,                      // NI_AVX_RoundToNearestInteger
        8,                      // NI_AVX_RoundToNegativeInfinity
        8,                      // NI_AVX_RoundToPositiveInfinity
        8,                      // NI_AVX_RoundToZero
        1,                      // NI_AVX_Shuffle
        byte.MaxValue,          // NI_AVX_Sqrt
        byte.MaxValue,          // NI_AVX_Store
        byte.MaxValue,          // NI_AVX_StoreAligned
        byte.MaxValue,          // NI_AVX_StoreAlignedNonTemporal
        4,                      // NI_AVX_Subtract
        byte.MaxValue,          // NI_AVX_TestC
        byte.MaxValue,          // NI_AVX_TestNotZAndNotC
        byte.MaxValue,          // NI_AVX_TestZ
        1,                      // NI_AVX_UnpackHigh
        1,                      // NI_AVX_UnpackLow
        1,                      // NI_AVX_Xor
        byte.MaxValue,          // NI_AVX2_Abs
        byte.MaxValue,          // NI_AVX2_Add
        byte.MaxValue,          // NI_AVX2_AddSaturate
        byte.MaxValue,          // NI_AVX2_AlignRight
        byte.MaxValue,          // NI_AVX2_And
        byte.MaxValue,          // NI_AVX2_AndNot
        byte.MaxValue,          // NI_AVX2_Average
        byte.MaxValue,          // NI_AVX2_BitFieldExtract
        byte.MaxValue,          // NI_AVX2_Blend
        byte.MaxValue,          // NI_AVX2_BlendVariable
        1,                      // NI_AVX2_BroadcastScalarToVector128
        3,                      // NI_AVX2_BroadcastScalarToVector256
        byte.MaxValue,          // NI_AVX2_BroadcastVector128ToVector256
        byte.MaxValue,          // NI_AVX2_CompareEqual
        byte.MaxValue,          // NI_AVX2_CompareGreaterThan
        byte.MaxValue,          // NI_AVX2_CompareLessThan
        byte.MaxValue,          // NI_AVX2_ConvertToInt32
        byte.MaxValue,          // NI_AVX2_ConvertToUInt32
        5,                      // NI_AVX2_ConvertToVector128Half
        byte.MaxValue,          // NI_AVX2_ConvertToVector128Single
        byte.MaxValue,          // NI_AVX2_ConvertToVector256Half
        byte.MaxValue,          // NI_AVX2_ConvertToVector256Int16
        byte.MaxValue,          // NI_AVX2_ConvertToVector256Int32
        byte.MaxValue,          // NI_AVX2_ConvertToVector256Int64
        7,                      // NI_AVX2_ConvertToVector256Single
        byte.MaxValue,          // NI_AVX2_ExtractLowestSetBit
        byte.MaxValue,          // NI_AVX2_ExtractVector128
        byte.MaxValue,          // NI_AVX2_GatherMaskVector128
        byte.MaxValue,          // NI_AVX2_GatherMaskVector256
        byte.MaxValue,          // NI_AVX2_GatherVector128
        byte.MaxValue,          // NI_AVX2_GatherVector256
        byte.MaxValue,          // NI_AVX2_GetMaskUpToLowestSetBit
        byte.MaxValue,          // NI_AVX2_HorizontalAdd
        byte.MaxValue,          // NI_AVX2_HorizontalAddSaturate
        byte.MaxValue,          // NI_AVX2_HorizontalSubtract
        byte.MaxValue,          // NI_AVX2_HorizontalSubtractSaturate
        byte.MaxValue,          // NI_AVX2_InsertVector128
        byte.MaxValue,          // NI_AVX2_LeadingZeroCount
        byte.MaxValue,          // NI_AVX2_LoadAlignedVector256NonTemporal
        byte.MaxValue,          // NI_AVX2_MaskLoad
        byte.MaxValue,          // NI_AVX2_MaskStore
        byte.MaxValue,          // NI_AVX2_Max
        byte.MaxValue,          // NI_AVX2_Min
        byte.MaxValue,          // NI_AVX2_MoveMask
        byte.MaxValue,          // NI_AVX2_MultipleSumAbsoluteDifferences
        byte.MaxValue,          // NI_AVX2_Multiply
        4,                      // NI_AVX2_MultiplyAdd
        byte.MaxValue,          // NI_AVX2_MultiplyAddAdjacent
        4,                      // NI_AVX2_MultiplyAddNegated
        4,                      // NI_AVX2_MultiplyAddNegatedScalar
        4,                      // NI_AVX2_MultiplyAddScalar
        4,                      // NI_AVX2_MultiplyAddSubtract
        byte.MaxValue,          // NI_AVX2_MultiplyHigh
        byte.MaxValue,          // NI_AVX2_MultiplyHighRoundScale
        byte.MaxValue,          // NI_AVX2_MultiplyLow
        byte.MaxValue,          // NI_AVX2_MultiplyNoFlags
        4,                      // NI_AVX2_MultiplySubtract
        4,                      // NI_AVX2_MultiplySubtractAdd
        4,                      // NI_AVX2_MultiplySubtractNegated
        4,                      // NI_AVX2_MultiplySubtractNegatedScalar
        4,                      // NI_AVX2_MultiplySubtractScalar
        byte.MaxValue,          // NI_AVX2_Or
        byte.MaxValue,          // NI_AVX2_PackSignedSaturate
        byte.MaxValue,          // NI_AVX2_PackUnsignedSaturate
        byte.MaxValue,          // NI_AVX2_ParallelBitDeposit
        byte.MaxValue,          // NI_AVX2_ParallelBitExtract
        byte.MaxValue,          // NI_AVX2_Permute2x128
        3,                      // NI_AVX2_Permute4x64
        3,                      // NI_AVX2_PermuteVar8x32
        byte.MaxValue,          // NI_AVX2_ResetLowestSetBit
        byte.MaxValue,          // NI_AVX2_ShiftLeftLogical
        byte.MaxValue,          // NI_AVX2_ShiftLeftLogical128BitLane
        byte.MaxValue,          // NI_AVX2_ShiftLeftLogicalVariable
        byte.MaxValue,          // NI_AVX2_ShiftRightArithmetic
        byte.MaxValue,          // NI_AVX2_ShiftRightArithmeticVariable
        byte.MaxValue,          // NI_AVX2_ShiftRightLogical
        byte.MaxValue,          // NI_AVX2_ShiftRightLogical128BitLane
        byte.MaxValue,          // NI_AVX2_ShiftRightLogicalVariable
        byte.MaxValue,          // NI_AVX2_Shuffle
        byte.MaxValue,          // NI_AVX2_ShuffleHigh
        byte.MaxValue,          // NI_AVX2_ShuffleLow
        byte.MaxValue,          // NI_AVX2_Sign
        byte.MaxValue,          // NI_AVX2_Subtract
        byte.MaxValue,          // NI_AVX2_SubtractSaturate
        byte.MaxValue,          // NI_AVX2_SumAbsoluteDifferences
        byte.MaxValue,          // NI_AVX2_TrailingZeroCount
        byte.MaxValue,          // NI_AVX2_UnpackHigh
        byte.MaxValue,          // NI_AVX2_UnpackLow
        byte.MaxValue,          // NI_AVX2_Xor
        byte.MaxValue,          // NI_AVX2_ZeroHighBits
        byte.MaxValue,          // NI_AVX2_X64_AndNot
        byte.MaxValue,          // NI_AVX2_X64_BitFieldExtract
        byte.MaxValue,          // NI_AVX2_X64_ExtractLowestSetBit
        byte.MaxValue,          // NI_AVX2_X64_GetMaskUpToLowestSetBit
        byte.MaxValue,          // NI_AVX2_X64_LeadingZeroCount
        byte.MaxValue,          // NI_AVX2_X64_MultiplyNoFlags
        byte.MaxValue,          // NI_AVX2_X64_ParallelBitDeposit
        byte.MaxValue,          // NI_AVX2_X64_ParallelBitExtract
        byte.MaxValue,          // NI_AVX2_X64_ResetLowestSetBit
        byte.MaxValue,          // NI_AVX2_X64_TrailingZeroCount
        byte.MaxValue,          // NI_AVX2_X64_ZeroHighBits
        byte.MaxValue,          // NI_AVX512_Abs
        4,                      // NI_AVX512_Add
        byte.MaxValue,          // NI_AVX512_AddSaturate
        4,                      // NI_AVX512_AddScalar
        byte.MaxValue,          // NI_AVX512_AlignRight
        byte.MaxValue,          // NI_AVX512_AlignRight32
        byte.MaxValue,          // NI_AVX512_AlignRight64
        1,                      // NI_AVX512_And
        1,                      // NI_AVX512_AndNot
        byte.MaxValue,          // NI_AVX512_Average
        byte.MaxValue,          // NI_AVX512_BlendVariable
        byte.MaxValue,          // NI_AVX512_BroadcastPairScalarToVector128
        3,                      // NI_AVX512_BroadcastPairScalarToVector256
        3,                      // NI_AVX512_BroadcastPairScalarToVector512
        3,                      // NI_AVX512_BroadcastScalarToVector512
        byte.MaxValue,          // NI_AVX512_BroadcastVector128ToVector512
        byte.MaxValue,          // NI_AVX512_BroadcastVector256ToVector512
        byte.MaxValue,          // NI_AVX512_Classify
        byte.MaxValue,          // NI_AVX512_ClassifyScalar
        byte.MaxValue,          // NI_AVX512_Compare
        byte.MaxValue,          // NI_AVX512_CompareEqual
        byte.MaxValue,          // NI_AVX512_CompareGreaterThan
        byte.MaxValue,          // NI_AVX512_CompareGreaterThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareLessThan
        byte.MaxValue,          // NI_AVX512_CompareLessThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareNotEqual
        byte.MaxValue,          // NI_AVX512_CompareNotGreaterThan
        byte.MaxValue,          // NI_AVX512_CompareNotGreaterThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareNotLessThan
        byte.MaxValue,          // NI_AVX512_CompareNotLessThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareOrdered
        byte.MaxValue,          // NI_AVX512_CompareUnordered
        byte.MaxValue,          // NI_AVX512_Compress
        byte.MaxValue,          // NI_AVX512_CompressStore
        byte.MaxValue,          // NI_AVX512_ConvertScalarToVector128Double
        4,                      // NI_AVX512_ConvertScalarToVector128Single
        7,                      // NI_AVX512_ConvertToInt32
        7,                      // NI_AVX512_ConvertToUInt32
        7,                      // NI_AVX512_ConvertToUInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Byte
        byte.MaxValue,          // NI_AVX512_ConvertToVector128ByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Double
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int16
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int32
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int64
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128SByte
        byte.MaxValue,          // NI_AVX512_ConvertToVector128SByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Single
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt16
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt32
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt64
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Byte
        byte.MaxValue,          // NI_AVX512_ConvertToVector256ByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Double
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int16
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int32
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int64
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256SByte
        byte.MaxValue,          // NI_AVX512_ConvertToVector256SByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Single
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt16
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt32
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt64
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Double
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Int16
        4,                      // NI_AVX512_ConvertToVector512Int32
        4,                      // NI_AVX512_ConvertToVector512Int32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Int64
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Int64WithTruncation
        4,                      // NI_AVX512_ConvertToVector512Single
        byte.MaxValue,          // NI_AVX512_ConvertToVector512UInt16
        4,                      // NI_AVX512_ConvertToVector512UInt32
        4,                      // NI_AVX512_ConvertToVector512UInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector512UInt64
        byte.MaxValue,          // NI_AVX512_ConvertToVector512UInt64WithTruncation
        byte.MaxValue,          // NI_AVX512_DetectConflicts
        byte.MaxValue,          // NI_AVX512_Divide
        byte.MaxValue,          // NI_AVX512_DivideScalar
        1,                      // NI_AVX512_DuplicateEvenIndexed
        1,                      // NI_AVX512_DuplicateOddIndexed
        byte.MaxValue,          // NI_AVX512_Expand
        byte.MaxValue,          // NI_AVX512_ExpandLoad
        3,                      // NI_AVX512_ExtractVector128
        3,                      // NI_AVX512_ExtractVector256
        4,                      // NI_AVX512_Fixup
        4,                      // NI_AVX512_FixupScalar
        4,                      // NI_AVX512_FusedMultiplyAdd
        4,                      // NI_AVX512_FusedMultiplyAddNegated
        4,                      // NI_AVX512_FusedMultiplyAddNegatedScalar
        4,                      // NI_AVX512_FusedMultiplyAddScalar
        4,                      // NI_AVX512_FusedMultiplyAddSubtract
        4,                      // NI_AVX512_FusedMultiplySubtract
        4,                      // NI_AVX512_FusedMultiplySubtractAdd
        4,                      // NI_AVX512_FusedMultiplySubtractNegated
        4,                      // NI_AVX512_FusedMultiplySubtractNegatedScalar
        4,                      // NI_AVX512_FusedMultiplySubtractScalar
        4,                      // NI_AVX512_GetExponent
        4,                      // NI_AVX512_GetExponentScalar
        4,                      // NI_AVX512_GetMantissa
        4,                      // NI_AVX512_GetMantissaScalar
        3,                      // NI_AVX512_InsertVector128
        3,                      // NI_AVX512_InsertVector256
        byte.MaxValue,          // NI_AVX512_LeadingZeroCount
        byte.MaxValue,          // NI_AVX512_LoadAlignedVector512
        byte.MaxValue,          // NI_AVX512_LoadAlignedVector512NonTemporal
        byte.MaxValue,          // NI_AVX512_LoadVector512
        byte.MaxValue,          // NI_AVX512_MaskLoad
        byte.MaxValue,          // NI_AVX512_MaskLoadAligned
        byte.MaxValue,          // NI_AVX512_MaskStore
        byte.MaxValue,          // NI_AVX512_MaskStoreAligned
        4,                      // NI_AVX512_Max
        4,                      // NI_AVX512_Min
        3,                      // NI_AVX512_MoveMask
        4,                      // NI_AVX512_Multiply
        byte.MaxValue,          // NI_AVX512_MultiplyAddAdjacent
        byte.MaxValue,          // NI_AVX512_MultiplyHigh
        byte.MaxValue,          // NI_AVX512_MultiplyHighRoundScale
        byte.MaxValue,          // NI_AVX512_MultiplyLow
        4,                      // NI_AVX512_MultiplyScalar
        1,                      // NI_AVX512_Or
        byte.MaxValue,          // NI_AVX512_PackSignedSaturate
        byte.MaxValue,          // NI_AVX512_PackUnsignedSaturate
        1,                      // NI_AVX512_Permute2x64
        1,                      // NI_AVX512_Permute4x32
        3,                      // NI_AVX512_Permute4x64
        byte.MaxValue,          // NI_AVX512_PermuteVar16x16
        byte.MaxValue,          // NI_AVX512_PermuteVar16x16x2
        3,                      // NI_AVX512_PermuteVar16x32
        3,                      // NI_AVX512_PermuteVar16x32x2
        1,                      // NI_AVX512_PermuteVar2x64
        3,                      // NI_AVX512_PermuteVar2x64x2
        byte.MaxValue,          // NI_AVX512_PermuteVar32x16
        byte.MaxValue,          // NI_AVX512_PermuteVar32x16x2
        1,                      // NI_AVX512_PermuteVar4x32
        3,                      // NI_AVX512_PermuteVar4x32x2
        3,                      // NI_AVX512_PermuteVar4x64
        3,                      // NI_AVX512_PermuteVar4x64x2
        byte.MaxValue,          // NI_AVX512_PermuteVar8x16
        byte.MaxValue,          // NI_AVX512_PermuteVar8x16x2
        3,                      // NI_AVX512_PermuteVar8x32x2
        3,                      // NI_AVX512_PermuteVar8x64
        3,                      // NI_AVX512_PermuteVar8x64x2
        4,                      // NI_AVX512_Range
        4,                      // NI_AVX512_RangeScalar
        byte.MaxValue,          // NI_AVX512_Reciprocal14
        byte.MaxValue,          // NI_AVX512_Reciprocal14Scalar
        byte.MaxValue,          // NI_AVX512_ReciprocalSqrt14
        byte.MaxValue,          // NI_AVX512_ReciprocalSqrt14Scalar
        4,                      // NI_AVX512_Reduce
        4,                      // NI_AVX512_ReduceScalar
        byte.MaxValue,          // NI_AVX512_RotateLeft
        byte.MaxValue,          // NI_AVX512_RotateLeftVariable
        byte.MaxValue,          // NI_AVX512_RotateRight
        byte.MaxValue,          // NI_AVX512_RotateRightVariable
        8,                      // NI_AVX512_RoundScale
        8,                      // NI_AVX512_RoundScaleScalar
        4,                      // NI_AVX512_Scale
        4,                      // NI_AVX512_ScaleScalar
        byte.MaxValue,          // NI_AVX512_ShiftLeftLogical
        byte.MaxValue,          // NI_AVX512_ShiftLeftLogical128BitLane
        byte.MaxValue,          // NI_AVX512_ShiftLeftLogicalVariable
        byte.MaxValue,          // NI_AVX512_ShiftRightArithmetic
        byte.MaxValue,          // NI_AVX512_ShiftRightArithmeticVariable
        byte.MaxValue,          // NI_AVX512_ShiftRightLogical
        byte.MaxValue,          // NI_AVX512_ShiftRightLogical128BitLane
        byte.MaxValue,          // NI_AVX512_ShiftRightLogicalVariable
        1,                      // NI_AVX512_Shuffle
        3,                      // NI_AVX512_Shuffle2x128
        3,                      // NI_AVX512_Shuffle4x128
        byte.MaxValue,          // NI_AVX512_ShuffleHigh
        byte.MaxValue,          // NI_AVX512_ShuffleLow
        byte.MaxValue,          // NI_AVX512_Sqrt
        byte.MaxValue,          // NI_AVX512_SqrtScalar
        byte.MaxValue,          // NI_AVX512_Store
        byte.MaxValue,          // NI_AVX512_StoreAligned
        byte.MaxValue,          // NI_AVX512_StoreAlignedNonTemporal
        4,                      // NI_AVX512_Subtract
        byte.MaxValue,          // NI_AVX512_SubtractSaturate
        4,                      // NI_AVX512_SubtractScalar
        byte.MaxValue,          // NI_AVX512_SumAbsoluteDifferences
        byte.MaxValue,          // NI_AVX512_SumAbsoluteDifferencesInBlock32
        1,                      // NI_AVX512_TernaryLogic
        1,                      // NI_AVX512_UnpackHigh
        1,                      // NI_AVX512_UnpackLow
        1,                      // NI_AVX512_Xor
        byte.MaxValue,          // NI_AVX512_X64_ConvertScalarToVector128Double
        byte.MaxValue,          // NI_AVX512_X64_ConvertScalarToVector128Single
        7,                      // NI_AVX512_X64_ConvertToInt64
        7,                      // NI_AVX512_X64_ConvertToUInt64
        7,                      // NI_AVX512_X64_ConvertToUInt64WithTruncation
        byte.MaxValue,          // NI_AVX512v2_MultiShift
        byte.MaxValue,          // NI_AVX512v2_PermuteVar16x8
        byte.MaxValue,          // NI_AVX512v2_PermuteVar16x8x2
        byte.MaxValue,          // NI_AVX512v2_PermuteVar32x8
        byte.MaxValue,          // NI_AVX512v2_PermuteVar32x8x2
        byte.MaxValue,          // NI_AVX512v2_PermuteVar64x8
        byte.MaxValue,          // NI_AVX512v2_PermuteVar64x8x2
        byte.MaxValue,          // NI_AVX512v3_Compress
        byte.MaxValue,          // NI_AVX512v3_CompressStore
        byte.MaxValue,          // NI_AVX512v3_Expand
        byte.MaxValue,          // NI_AVX512v3_ExpandLoad
        4,                      // NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32
        4,                      // NI_AVX10v2_ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32
        7,                      // NI_AVX10v2_ConvertToInt32WithTruncatedSaturation
        4,                      // NI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32
        4,                      // NI_AVX10v2_ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32
        7,                      // NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation
        4,                      // NI_AVX10v2_MinMax
        4,                      // NI_AVX10v2_MinMaxScalar
        byte.MaxValue,          // NI_AVX10v2_MoveScalar
        byte.MaxValue,          // NI_AVX10v2_MultipleSumAbsoluteDifferences
        byte.MaxValue,          // NI_AVX10v2_StoreScalar
        7,                      // NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation
        7,                      // NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction
        byte.MaxValue,          // NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction
        byte.MaxValue,          // NI_AVX512BMM_ReverseBits
        byte.MaxValue,          // NI_AVXVNNI_MultiplyWideningAndAdd
        byte.MaxValue,          // NI_AVXVNNI_MultiplyWideningAndAddSaturate
        byte.MaxValue,          // NI_AVXVNNIINT_MultiplyWideningAndAdd
        byte.MaxValue,          // NI_AVXVNNIINT_MultiplyWideningAndAddSaturate
        byte.MaxValue,          // NI_AVXVNNIINT_V512_MultiplyWideningAndAdd
        byte.MaxValue,          // NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate
        byte.MaxValue,          // NI_AES_CarrylessMultiply
        byte.MaxValue,          // NI_AES_Decrypt
        byte.MaxValue,          // NI_AES_DecryptLast
        byte.MaxValue,          // NI_AES_Encrypt
        byte.MaxValue,          // NI_AES_EncryptLast
        byte.MaxValue,          // NI_AES_InverseMixColumns
        byte.MaxValue,          // NI_AES_KeygenAssist
        byte.MaxValue,          // NI_AES_V256_CarrylessMultiply
        byte.MaxValue,          // NI_AES_V512_CarrylessMultiply
        byte.MaxValue,          // NI_X86Serialize_Serialize
        byte.MaxValue,          // NI_GFNI_GaloisFieldAffineTransform
        byte.MaxValue,          // NI_GFNI_GaloisFieldAffineTransformInverse
        byte.MaxValue,          // NI_GFNI_GaloisFieldMultiply
        byte.MaxValue,          // NI_GFNI_V256_GaloisFieldAffineTransform
        byte.MaxValue,          // NI_GFNI_V256_GaloisFieldAffineTransformInverse
        byte.MaxValue,          // NI_GFNI_V256_GaloisFieldMultiply
        byte.MaxValue,          // NI_GFNI_V512_GaloisFieldAffineTransform
        byte.MaxValue,          // NI_GFNI_V512_GaloisFieldAffineTransformInverse
        byte.MaxValue,          // NI_GFNI_V512_GaloisFieldMultiply
        3,                      // NI_X86Base_COMIS
        byte.MaxValue,          // NI_X86Base_PTEST
        3,                      // NI_X86Base_UCOMIS
        byte.MaxValue,          // NI_AVX_PTEST
        byte.MaxValue,          // NI_AVX2_AndNotVector
        byte.MaxValue,          // NI_AVX2_AndNotScalar
        4,                      // NI_AVX512_KORTEST
        4,                      // NI_AVX512_KTEST
        4,                      // NI_AVX512_PTESTM
        4,                      // NI_AVX512_PTESTNM
        4,                      // NI_AVX512_AddMask
        1,                      // NI_AVX512_AndMask
        1,                      // NI_AVX512_AndNotMask
        1,                      // NI_AVX512_BlendVariableMask
        3,                      // NI_AVX512_ClassifyMask
        3,                      // NI_AVX512_ClassifyScalarMask
        4,                      // NI_AVX512_CompareMask
        4,                      // NI_AVX512_CompareEqualMask
        4,                      // NI_AVX512_CompareGreaterThanMask
        4,                      // NI_AVX512_CompareGreaterThanOrEqualMask
        4,                      // NI_AVX512_CompareLessThanMask
        4,                      // NI_AVX512_CompareLessThanOrEqualMask
        4,                      // NI_AVX512_CompareNotEqualMask
        4,                      // NI_AVX512_CompareNotGreaterThanMask
        4,                      // NI_AVX512_CompareNotGreaterThanOrEqualMask
        4,                      // NI_AVX512_CompareNotLessThanMask
        4,                      // NI_AVX512_CompareNotLessThanOrEqualMask
        4,                      // NI_AVX512_CompareOrderedMask
        4,                      // NI_AVX512_CompareScalarMask
        4,                      // NI_AVX512_CompareUnorderedMask
        3,                      // NI_AVX512_CompressMask
        byte.MaxValue,          // NI_AVX512_CompressStoreMask
        byte.MaxValue,          // NI_AVX512_ConvertMaskToVector
        3,                      // NI_AVX512_ConvertVectorToMask
        byte.MaxValue,          // NI_AVX512_ExpandLoadMask
        3,                      // NI_AVX512_ExpandMask
        byte.MaxValue,          // NI_AVX512_MaskLoadMask
        byte.MaxValue,          // NI_AVX512_MaskLoadAlignedMask
        byte.MaxValue,          // NI_AVX512_MaskStoreMask
        byte.MaxValue,          // NI_AVX512_MaskStoreAlignedMask
        1,                      // NI_AVX512_NotMask
        1,                      // NI_AVX512_OrMask
        4,                      // NI_AVX512_ShiftLeftMask
        4,                      // NI_AVX512_ShiftRightMask
        1,                      // NI_AVX512_XorMask
        1,                      // NI_AVX512_XnorMask
    ];

    private static ReadOnlySpan<byte> s_intCosts => [
        byte.MaxValue,          // NI_Vector128_Abs        
        byte.MaxValue,          // NI_Vector128_AddSaturate
        byte.MaxValue,          // NI_Vector128_AndNot
        byte.MaxValue,          // NI_Vector128_As
        byte.MaxValue,          // NI_Vector128_AsByte
        byte.MaxValue,          // NI_Vector128_AsDouble
        byte.MaxValue,          // NI_Vector128_AsInt16
        byte.MaxValue,          // NI_Vector128_AsInt32
        byte.MaxValue,          // NI_Vector128_AsInt64
        byte.MaxValue,          // NI_Vector128_AsNInt
        byte.MaxValue,          // NI_Vector128_AsNUInt
        byte.MaxValue,          // NI_Vector128_AsSByte
        byte.MaxValue,          // NI_Vector128_AsSingle
        byte.MaxValue,          // NI_Vector128_AsUInt16
        byte.MaxValue,          // NI_Vector128_AsUInt32
        byte.MaxValue,          // NI_Vector128_AsUInt64
        byte.MaxValue,          // NI_Vector128_AsVector
        byte.MaxValue,          // NI_Vector128_AsVector128
        byte.MaxValue,          // NI_Vector128_AsVector128Unsafe
        byte.MaxValue,          // NI_Vector128_AsVector2
        byte.MaxValue,          // NI_Vector128_AsVector3
        byte.MaxValue,          // NI_Vector128_AsVector4
        byte.MaxValue,          // NI_Vector128_Ceiling
        byte.MaxValue,          // NI_Vector128_ConditionalSelect
        byte.MaxValue,          // NI_Vector128_ConvertToDouble
        byte.MaxValue,          // NI_Vector128_ConvertToInt32
        byte.MaxValue,          // NI_Vector128_ConvertToInt32Native
        byte.MaxValue,          // NI_Vector128_ConvertToInt64
        byte.MaxValue,          // NI_Vector128_ConvertToInt64Native
        byte.MaxValue,          // NI_Vector128_ConvertToSingle
        byte.MaxValue,          // NI_Vector128_ConvertToUInt32
        byte.MaxValue,          // NI_Vector128_ConvertToUInt32Native
        byte.MaxValue,          // NI_Vector128_ConvertToUInt64
        byte.MaxValue,          // NI_Vector128_ConvertToUInt64Native
        byte.MaxValue,          // NI_Vector128_Create
        byte.MaxValue,          // NI_Vector128_CreateScalar
        byte.MaxValue,          // NI_Vector128_CreateScalarUnsafe
        byte.MaxValue,          // NI_Vector128_CreateSequence
        byte.MaxValue,          // NI_Vector128_Dot
        byte.MaxValue,          // NI_Vector128_Equals
        byte.MaxValue,          // NI_Vector128_EqualsAny
        byte.MaxValue,          // NI_Vector128_ExtractMostSignificantBits
        byte.MaxValue,          // NI_Vector128_Floor
        byte.MaxValue,          // NI_Vector128_FusedMultiplyAdd
        byte.MaxValue,          // NI_Vector128_GetElement
        byte.MaxValue,          // NI_Vector128_GreaterThan
        byte.MaxValue,          // NI_Vector128_GreaterThanAll
        byte.MaxValue,          // NI_Vector128_GreaterThanAny
        byte.MaxValue,          // NI_Vector128_GreaterThanOrEqual
        byte.MaxValue,          // NI_Vector128_GreaterThanOrEqualAll
        byte.MaxValue,          // NI_Vector128_GreaterThanOrEqualAny
        byte.MaxValue,          // NI_Vector128_IsEvenInteger
        byte.MaxValue,          // NI_Vector128_IsFinite
        byte.MaxValue,          // NI_Vector128_IsInfinity
        byte.MaxValue,          // NI_Vector128_IsInteger
        byte.MaxValue,          // NI_Vector128_IsNaN
        byte.MaxValue,          // NI_Vector128_IsNegative
        byte.MaxValue,          // NI_Vector128_IsNegativeInfinity
        byte.MaxValue,          // NI_Vector128_IsNormal
        byte.MaxValue,          // NI_Vector128_IsOddInteger
        byte.MaxValue,          // NI_Vector128_IsPositive
        byte.MaxValue,          // NI_Vector128_IsPositiveInfinity
        byte.MaxValue,          // NI_Vector128_IsSubnormal
        byte.MaxValue,          // NI_Vector128_IsZero
        byte.MaxValue,          // NI_Vector128_LessThan
        byte.MaxValue,          // NI_Vector128_LessThanAll
        byte.MaxValue,          // NI_Vector128_LessThanAny
        byte.MaxValue,          // NI_Vector128_LessThanOrEqual
        byte.MaxValue,          // NI_Vector128_LessThanOrEqualAll
        byte.MaxValue,          // NI_Vector128_LessThanOrEqualAny
        byte.MaxValue,          // NI_Vector128_LoadAligned
        byte.MaxValue,          // NI_Vector128_LoadAlignedNonTemporal
        byte.MaxValue,          // NI_Vector128_LoadUnsafe
        byte.MaxValue,          // NI_Vector128_Max
        byte.MaxValue,          // NI_Vector128_MaxMagnitude
        byte.MaxValue,          // NI_Vector128_MaxMagnitudeNumber
        byte.MaxValue,          // NI_Vector128_MaxNative
        byte.MaxValue,          // NI_Vector128_MaxNumber
        byte.MaxValue,          // NI_Vector128_Min
        byte.MaxValue,          // NI_Vector128_MinMagnitude
        byte.MaxValue,          // NI_Vector128_MinMagnitudeNumber
        byte.MaxValue,          // NI_Vector128_MinNative
        byte.MaxValue,          // NI_Vector128_MinNumber
        byte.MaxValue,          // NI_Vector128_MultiplyAddEstimate
        byte.MaxValue,          // NI_Vector128_Narrow
        byte.MaxValue,          // NI_Vector128_NarrowWithSaturation
        byte.MaxValue,          // NI_Vector128_Round
        byte.MaxValue,          // NI_Vector128_ShiftLeft
        byte.MaxValue,          // NI_Vector128_Shuffle
        byte.MaxValue,          // NI_Vector128_ShuffleNative
        byte.MaxValue,          // NI_Vector128_ShuffleNativeFallback
        byte.MaxValue,          // NI_Vector128_Sqrt
        byte.MaxValue,          // NI_Vector128_StoreAligned
        byte.MaxValue,          // NI_Vector128_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_Vector128_StoreUnsafe
        byte.MaxValue,          // NI_Vector128_SubtractSaturate
        byte.MaxValue,          // NI_Vector128_Sum
        byte.MaxValue,          // NI_Vector128_ToScalar
        byte.MaxValue,          // NI_Vector128_ToVector256
        byte.MaxValue,          // NI_Vector128_ToVector256Unsafe
        byte.MaxValue,          // NI_Vector128_ToVector512
        byte.MaxValue,          // NI_Vector128_Truncate
        byte.MaxValue,          // NI_Vector128_WidenLower
        byte.MaxValue,          // NI_Vector128_WidenUpper
        byte.MaxValue,          // NI_Vector128_WithElement
        byte.MaxValue,          // NI_Vector128_get_AllBitsSet
        byte.MaxValue,          // NI_Vector128_get_E
        byte.MaxValue,          // NI_Vector128_get_Epsilon
        byte.MaxValue,          // NI_Vector128_get_Indices
        byte.MaxValue,          // NI_Vector128_get_NaN
        byte.MaxValue,          // NI_Vector128_get_NegativeInfinity
        byte.MaxValue,          // NI_Vector128_get_NegativeOne
        byte.MaxValue,          // NI_Vector128_get_NegativeZero
        byte.MaxValue,          // NI_Vector128_get_One
        byte.MaxValue,          // NI_Vector128_get_Pi
        byte.MaxValue,          // NI_Vector128_get_PositiveInfinity
        byte.MaxValue,          // NI_Vector128_get_Tau
        byte.MaxValue,          // NI_Vector128_get_Zero
        byte.MaxValue,          // NI_Vector128_op_Addition
        byte.MaxValue,          // NI_Vector128_op_BitwiseAnd
        byte.MaxValue,          // NI_Vector128_op_BitwiseOr
        byte.MaxValue,          // NI_Vector128_op_Division
        byte.MaxValue,          // NI_Vector128_op_Equality
        byte.MaxValue,          // NI_Vector128_op_ExclusiveOr
        byte.MaxValue,          // NI_Vector128_op_Inequality
        byte.MaxValue,          // NI_Vector128_op_LeftShift
        byte.MaxValue,          // NI_Vector128_op_Multiply
        byte.MaxValue,          // NI_Vector128_op_OnesComplement
        byte.MaxValue,          // NI_Vector128_op_RightShift
        byte.MaxValue,          // NI_Vector128_op_Subtraction
        byte.MaxValue,          // NI_Vector128_op_UnaryNegation
        byte.MaxValue,          // NI_Vector128_op_UnaryPlus
        byte.MaxValue,          // NI_Vector128_op_UnsignedRightShift
        byte.MaxValue,          // NI_Vector256_Abs
        byte.MaxValue,          // NI_Vector256_AddSaturate
        byte.MaxValue,          // NI_Vector256_AndNot
        byte.MaxValue,          // NI_Vector256_As
        byte.MaxValue,          // NI_Vector256_AsByte
        byte.MaxValue,          // NI_Vector256_AsDouble
        byte.MaxValue,          // NI_Vector256_AsInt16
        byte.MaxValue,          // NI_Vector256_AsInt32
        byte.MaxValue,          // NI_Vector256_AsInt64
        byte.MaxValue,          // NI_Vector256_AsNInt
        byte.MaxValue,          // NI_Vector256_AsNUInt
        byte.MaxValue,          // NI_Vector256_AsSByte
        byte.MaxValue,          // NI_Vector256_AsSingle
        byte.MaxValue,          // NI_Vector256_AsUInt16
        byte.MaxValue,          // NI_Vector256_AsUInt32
        byte.MaxValue,          // NI_Vector256_AsUInt64
        byte.MaxValue,          // NI_Vector256_AsVector
        byte.MaxValue,          // NI_Vector256_AsVector256
        byte.MaxValue,          // NI_Vector256_Ceiling
        byte.MaxValue,          // NI_Vector256_ConditionalSelect
        byte.MaxValue,          // NI_Vector256_ConvertToDouble
        byte.MaxValue,          // NI_Vector256_ConvertToInt32
        byte.MaxValue,          // NI_Vector256_ConvertToInt32Native
        byte.MaxValue,          // NI_Vector256_ConvertToInt64
        byte.MaxValue,          // NI_Vector256_ConvertToInt64Native
        byte.MaxValue,          // NI_Vector256_ConvertToSingle
        byte.MaxValue,          // NI_Vector256_ConvertToUInt32
        byte.MaxValue,          // NI_Vector256_ConvertToUInt32Native
        byte.MaxValue,          // NI_Vector256_ConvertToUInt64
        byte.MaxValue,          // NI_Vector256_ConvertToUInt64Native
        byte.MaxValue,          // NI_Vector256_Create
        byte.MaxValue,          // NI_Vector256_CreateScalar
        byte.MaxValue,          // NI_Vector256_CreateScalarUnsafe
        byte.MaxValue,          // NI_Vector256_CreateSequence
        byte.MaxValue,          // NI_Vector256_Dot
        byte.MaxValue,          // NI_Vector256_Equals
        byte.MaxValue,          // NI_Vector256_EqualsAny
        byte.MaxValue,          // NI_Vector256_ExtractMostSignificantBits
        byte.MaxValue,          // NI_Vector256_Floor
        byte.MaxValue,          // NI_Vector256_FusedMultiplyAdd
        byte.MaxValue,          // NI_Vector256_GetElement
        byte.MaxValue,          // NI_Vector256_GetLower
        byte.MaxValue,          // NI_Vector256_GetUpper
        byte.MaxValue,          // NI_Vector256_GreaterThan
        byte.MaxValue,          // NI_Vector256_GreaterThanAll
        byte.MaxValue,          // NI_Vector256_GreaterThanAny
        byte.MaxValue,          // NI_Vector256_GreaterThanOrEqual
        byte.MaxValue,          // NI_Vector256_GreaterThanOrEqualAll
        byte.MaxValue,          // NI_Vector256_GreaterThanOrEqualAny
        byte.MaxValue,          // NI_Vector256_IsEvenInteger
        byte.MaxValue,          // NI_Vector256_IsFinite
        byte.MaxValue,          // NI_Vector256_IsInfinity
        byte.MaxValue,          // NI_Vector256_IsInteger
        byte.MaxValue,          // NI_Vector256_IsNaN
        byte.MaxValue,          // NI_Vector256_IsNegative
        byte.MaxValue,          // NI_Vector256_IsNegativeInfinity
        byte.MaxValue,          // NI_Vector256_IsNormal
        byte.MaxValue,          // NI_Vector256_IsOddInteger
        byte.MaxValue,          // NI_Vector256_IsPositive
        byte.MaxValue,          // NI_Vector256_IsPositiveInfinity
        byte.MaxValue,          // NI_Vector256_IsSubnormal
        byte.MaxValue,          // NI_Vector256_IsZero
        byte.MaxValue,          // NI_Vector256_LessThan
        byte.MaxValue,          // NI_Vector256_LessThanAll
        byte.MaxValue,          // NI_Vector256_LessThanAny
        byte.MaxValue,          // NI_Vector256_LessThanOrEqual
        byte.MaxValue,          // NI_Vector256_LessThanOrEqualAll
        byte.MaxValue,          // NI_Vector256_LessThanOrEqualAny
        byte.MaxValue,          // NI_Vector256_LoadAligned
        byte.MaxValue,          // NI_Vector256_LoadAlignedNonTemporal
        byte.MaxValue,          // NI_Vector256_LoadUnsafe
        byte.MaxValue,          // NI_Vector256_Max
        byte.MaxValue,          // NI_Vector256_MaxMagnitude
        byte.MaxValue,          // NI_Vector256_MaxMagnitudeNumber
        byte.MaxValue,          // NI_Vector256_MaxNative
        byte.MaxValue,          // NI_Vector256_MaxNumber
        byte.MaxValue,          // NI_Vector256_Min
        byte.MaxValue,          // NI_Vector256_MinMagnitude
        byte.MaxValue,          // NI_Vector256_MinMagnitudeNumber
        byte.MaxValue,          // NI_Vector256_MinNative
        byte.MaxValue,          // NI_Vector256_MinNumber
        byte.MaxValue,          // NI_Vector256_MultiplyAddEstimate
        byte.MaxValue,          // NI_Vector256_Narrow
        byte.MaxValue,          // NI_Vector256_NarrowWithSaturation
        byte.MaxValue,          // NI_Vector256_Round
        byte.MaxValue,          // NI_Vector256_ShiftLeft
        byte.MaxValue,          // NI_Vector256_Shuffle
        byte.MaxValue,          // NI_Vector256_ShuffleNative
        byte.MaxValue,          // NI_Vector256_ShuffleNativeFallback
        byte.MaxValue,          // NI_Vector256_Sqrt
        byte.MaxValue,          // NI_Vector256_StoreAligned
        byte.MaxValue,          // NI_Vector256_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_Vector256_StoreUnsafe
        byte.MaxValue,          // NI_Vector256_SubtractSaturate
        byte.MaxValue,          // NI_Vector256_Sum
        byte.MaxValue,          // NI_Vector256_ToScalar
        byte.MaxValue,          // NI_Vector256_ToVector512
        byte.MaxValue,          // NI_Vector256_ToVector512Unsafe
        byte.MaxValue,          // NI_Vector256_Truncate
        byte.MaxValue,          // NI_Vector256_WidenLower
        byte.MaxValue,          // NI_Vector256_WidenUpper
        byte.MaxValue,          // NI_Vector256_WithElement
        byte.MaxValue,          // NI_Vector256_WithLower
        byte.MaxValue,          // NI_Vector256_WithUpper
        byte.MaxValue,          // NI_Vector256_get_AllBitsSet
        byte.MaxValue,          // NI_Vector256_get_E
        byte.MaxValue,          // NI_Vector256_get_Epsilon
        byte.MaxValue,          // NI_Vector256_get_Indices
        byte.MaxValue,          // NI_Vector256_get_NaN
        byte.MaxValue,          // NI_Vector256_get_NegativeInfinity
        byte.MaxValue,          // NI_Vector256_get_NegativeOne
        byte.MaxValue,          // NI_Vector256_get_NegativeZero
        byte.MaxValue,          // NI_Vector256_get_One
        byte.MaxValue,          // NI_Vector256_get_Pi
        byte.MaxValue,          // NI_Vector256_get_PositiveInfinity
        byte.MaxValue,          // NI_Vector256_get_Tau
        byte.MaxValue,          // NI_Vector256_get_Zero
        byte.MaxValue,          // NI_Vector256_op_Addition
        byte.MaxValue,          // NI_Vector256_op_BitwiseAnd
        byte.MaxValue,          // NI_Vector256_op_BitwiseOr
        byte.MaxValue,          // NI_Vector256_op_Division
        byte.MaxValue,          // NI_Vector256_op_Equality
        byte.MaxValue,          // NI_Vector256_op_ExclusiveOr
        byte.MaxValue,          // NI_Vector256_op_Inequality
        byte.MaxValue,          // NI_Vector256_op_LeftShift
        byte.MaxValue,          // NI_Vector256_op_Multiply
        byte.MaxValue,          // NI_Vector256_op_OnesComplement
        byte.MaxValue,          // NI_Vector256_op_RightShift
        byte.MaxValue,          // NI_Vector256_op_Subtraction
        byte.MaxValue,          // NI_Vector256_op_UnaryNegation
        byte.MaxValue,          // NI_Vector256_op_UnaryPlus
        byte.MaxValue,          // NI_Vector256_op_UnsignedRightShift
        byte.MaxValue,          // NI_Vector512_Abs
        byte.MaxValue,          // NI_Vector512_AddSaturate
        byte.MaxValue,          // NI_Vector512_AndNot
        byte.MaxValue,          // NI_Vector512_As
        byte.MaxValue,          // NI_Vector512_AsByte
        byte.MaxValue,          // NI_Vector512_AsDouble
        byte.MaxValue,          // NI_Vector512_AsInt16
        byte.MaxValue,          // NI_Vector512_AsInt32
        byte.MaxValue,          // NI_Vector512_AsInt64
        byte.MaxValue,          // NI_Vector512_AsNInt
        byte.MaxValue,          // NI_Vector512_AsNUInt
        byte.MaxValue,          // NI_Vector512_AsSByte
        byte.MaxValue,          // NI_Vector512_AsSingle
        byte.MaxValue,          // NI_Vector512_AsUInt16
        byte.MaxValue,          // NI_Vector512_AsUInt32
        byte.MaxValue,          // NI_Vector512_AsUInt64
        byte.MaxValue,          // NI_Vector512_AsVector
        byte.MaxValue,          // NI_Vector512_AsVector512
        byte.MaxValue,          // NI_Vector512_Ceiling
        byte.MaxValue,          // NI_Vector512_ConditionalSelect
        byte.MaxValue,          // NI_Vector512_ConvertToDouble
        byte.MaxValue,          // NI_Vector512_ConvertToInt32
        byte.MaxValue,          // NI_Vector512_ConvertToInt32Native
        byte.MaxValue,          // NI_Vector512_ConvertToInt64
        byte.MaxValue,          // NI_Vector512_ConvertToInt64Native
        byte.MaxValue,          // NI_Vector512_ConvertToSingle
        byte.MaxValue,          // NI_Vector512_ConvertToUInt32
        byte.MaxValue,          // NI_Vector512_ConvertToUInt32Native
        byte.MaxValue,          // NI_Vector512_ConvertToUInt64
        byte.MaxValue,          // NI_Vector512_ConvertToUInt64Native
        byte.MaxValue,          // NI_Vector512_Create
        byte.MaxValue,          // NI_Vector512_CreateScalar
        byte.MaxValue,          // NI_Vector512_CreateScalarUnsafe
        byte.MaxValue,          // NI_Vector512_CreateSequence
        byte.MaxValue,          // NI_Vector512_Dot
        byte.MaxValue,          // NI_Vector512_Equals
        byte.MaxValue,          // NI_Vector512_EqualsAny
        byte.MaxValue,          // NI_Vector512_ExtractMostSignificantBits
        byte.MaxValue,          // NI_Vector512_Floor
        byte.MaxValue,          // NI_Vector512_FusedMultiplyAdd
        byte.MaxValue,          // NI_Vector512_GetElement
        byte.MaxValue,          // NI_Vector512_GetLower
        byte.MaxValue,          // NI_Vector512_GetLower128
        byte.MaxValue,          // NI_Vector512_GetUpper
        byte.MaxValue,          // NI_Vector512_GreaterThan
        byte.MaxValue,          // NI_Vector512_GreaterThanAll
        byte.MaxValue,          // NI_Vector512_GreaterThanAny
        byte.MaxValue,          // NI_Vector512_GreaterThanOrEqual
        byte.MaxValue,          // NI_Vector512_GreaterThanOrEqualAll
        byte.MaxValue,          // NI_Vector512_GreaterThanOrEqualAny
        byte.MaxValue,          // NI_Vector512_IsEvenInteger
        byte.MaxValue,          // NI_Vector512_IsFinite
        byte.MaxValue,          // NI_Vector512_IsInfinity
        byte.MaxValue,          // NI_Vector512_IsInteger
        byte.MaxValue,          // NI_Vector512_IsNaN
        byte.MaxValue,          // NI_Vector512_IsNegative
        byte.MaxValue,          // NI_Vector512_IsNegativeInfinity
        byte.MaxValue,          // NI_Vector512_IsNormal
        byte.MaxValue,          // NI_Vector512_IsOddInteger
        byte.MaxValue,          // NI_Vector512_IsPositive
        byte.MaxValue,          // NI_Vector512_IsPositiveInfinity
        byte.MaxValue,          // NI_Vector512_IsSubnormal
        byte.MaxValue,          // NI_Vector512_IsZero
        byte.MaxValue,          // NI_Vector512_LessThan
        byte.MaxValue,          // NI_Vector512_LessThanAll
        byte.MaxValue,          // NI_Vector512_LessThanAny
        byte.MaxValue,          // NI_Vector512_LessThanOrEqual
        byte.MaxValue,          // NI_Vector512_LessThanOrEqualAll
        byte.MaxValue,          // NI_Vector512_LessThanOrEqualAny
        byte.MaxValue,          // NI_Vector512_LoadAligned
        byte.MaxValue,          // NI_Vector512_LoadAlignedNonTemporal
        byte.MaxValue,          // NI_Vector512_LoadUnsafe
        byte.MaxValue,          // NI_Vector512_Max
        byte.MaxValue,          // NI_Vector512_MaxMagnitude
        byte.MaxValue,          // NI_Vector512_MaxMagnitudeNumber
        byte.MaxValue,          // NI_Vector512_MaxNative
        byte.MaxValue,          // NI_Vector512_MaxNumber
        byte.MaxValue,          // NI_Vector512_Min
        byte.MaxValue,          // NI_Vector512_MinMagnitude
        byte.MaxValue,          // NI_Vector512_MinMagnitudeNumber
        byte.MaxValue,          // NI_Vector512_MinNative
        byte.MaxValue,          // NI_Vector512_MinNumber
        byte.MaxValue,          // NI_Vector512_MultiplyAddEstimate
        byte.MaxValue,          // NI_Vector512_Narrow
        byte.MaxValue,          // NI_Vector512_NarrowWithSaturation
        byte.MaxValue,          // NI_Vector512_Round
        byte.MaxValue,          // NI_Vector512_ShiftLeft
        byte.MaxValue,          // NI_Vector512_Shuffle
        byte.MaxValue,          // NI_Vector512_ShuffleNative
        byte.MaxValue,          // NI_Vector512_ShuffleNativeFallback
        byte.MaxValue,          // NI_Vector512_Sqrt
        byte.MaxValue,          // NI_Vector512_StoreAligned
        byte.MaxValue,          // NI_Vector512_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_Vector512_StoreUnsafe
        byte.MaxValue,          // NI_Vector512_SubtractSaturate
        byte.MaxValue,          // NI_Vector512_Sum
        byte.MaxValue,          // NI_Vector512_ToScalar
        byte.MaxValue,          // NI_Vector512_Truncate
        byte.MaxValue,          // NI_Vector512_WidenLower
        byte.MaxValue,          // NI_Vector512_WidenUpper
        byte.MaxValue,          // NI_Vector512_WithElement
        byte.MaxValue,          // NI_Vector512_WithLower
        byte.MaxValue,          // NI_Vector512_WithUpper
        byte.MaxValue,          // NI_Vector512_get_AllBitsSet
        byte.MaxValue,          // NI_Vector512_get_E
        byte.MaxValue,          // NI_Vector512_get_Epsilon
        byte.MaxValue,          // NI_Vector512_get_Indices
        byte.MaxValue,          // NI_Vector512_get_NaN
        byte.MaxValue,          // NI_Vector512_get_NegativeInfinity
        byte.MaxValue,          // NI_Vector512_get_NegativeOne
        byte.MaxValue,          // NI_Vector512_get_NegativeZero
        byte.MaxValue,          // NI_Vector512_get_One
        byte.MaxValue,          // NI_Vector512_get_Pi
        byte.MaxValue,          // NI_Vector512_get_PositiveInfinity
        byte.MaxValue,          // NI_Vector512_get_Tau
        byte.MaxValue,          // NI_Vector512_get_Zero
        byte.MaxValue,          // NI_Vector512_op_Addition
        byte.MaxValue,          // NI_Vector512_op_BitwiseAnd
        byte.MaxValue,          // NI_Vector512_op_BitwiseOr
        byte.MaxValue,          // NI_Vector512_op_Division
        byte.MaxValue,          // NI_Vector512_op_Equality
        byte.MaxValue,          // NI_Vector512_op_ExclusiveOr
        byte.MaxValue,          // NI_Vector512_op_Inequality
        byte.MaxValue,          // NI_Vector512_op_LeftShift
        byte.MaxValue,          // NI_Vector512_op_Multiply
        byte.MaxValue,          // NI_Vector512_op_OnesComplement
        byte.MaxValue,          // NI_Vector512_op_RightShift
        byte.MaxValue,          // NI_Vector512_op_Subtraction
        byte.MaxValue,          // NI_Vector512_op_UnaryNegation
        byte.MaxValue,          // NI_Vector512_op_UnaryPlus
        byte.MaxValue,          // NI_Vector512_op_UnsignedRightShift
        1,                      // NI_X86Base_Abs
        1,                      // NI_X86Base_Add
        1,                      // NI_X86Base_AddSaturate
        byte.MaxValue,          // NI_X86Base_AddScalar
        byte.MaxValue,          // NI_X86Base_AddSubtract
        1,                      // NI_X86Base_AlignRight
        1,                      // NI_X86Base_And
        1,                      // NI_X86Base_AndNot
        1,                      // NI_X86Base_Average
        3,                      // NI_X86Base_BitScanForward
        3,                      // NI_X86Base_BitScanReverse
        1,                      // NI_X86Base_Blend
        1,                      // NI_X86Base_BlendVariable
        byte.MaxValue,          // NI_X86Base_Ceiling
        byte.MaxValue,          // NI_X86Base_CeilingScalar
        1,                      // NI_X86Base_CompareEqual
        1,                      // NI_X86Base_CompareGreaterThan
        byte.MaxValue,          // NI_X86Base_CompareGreaterThanOrEqual
        1,                      // NI_X86Base_CompareLessThan
        byte.MaxValue,          // NI_X86Base_CompareLessThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareNotEqual
        byte.MaxValue,          // NI_X86Base_CompareNotGreaterThan
        byte.MaxValue,          // NI_X86Base_CompareNotGreaterThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareNotLessThan
        byte.MaxValue,          // NI_X86Base_CompareNotLessThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareOrdered
        byte.MaxValue,          // NI_X86Base_CompareScalarEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarGreaterThan
        byte.MaxValue,          // NI_X86Base_CompareScalarGreaterThanOrEqual
        1,                      // NI_X86Base_CompareScalarLessThan
        byte.MaxValue,          // NI_X86Base_CompareScalarLessThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarNotEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarNotGreaterThan
        byte.MaxValue,          // NI_X86Base_CompareScalarNotGreaterThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarNotLessThan
        byte.MaxValue,          // NI_X86Base_CompareScalarNotLessThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarOrdered
        byte.MaxValue,          // NI_X86Base_CompareScalarOrderedEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarOrderedGreaterThan
        byte.MaxValue,          // NI_X86Base_CompareScalarOrderedGreaterThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarOrderedLessThan
        byte.MaxValue,          // NI_X86Base_CompareScalarOrderedLessThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarOrderedNotEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarUnordered
        byte.MaxValue,          // NI_X86Base_CompareScalarUnorderedEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarUnorderedGreaterThan
        byte.MaxValue,          // NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarUnorderedLessThan
        byte.MaxValue,          // NI_X86Base_CompareScalarUnorderedLessThanOrEqual
        byte.MaxValue,          // NI_X86Base_CompareScalarUnorderedNotEqual
        byte.MaxValue,          // NI_X86Base_CompareUnordered
        5,                      // NI_X86Base_ConvertScalarToVector128Double
        3,                      // NI_X86Base_ConvertScalarToVector128Int32
        5,                      // NI_X86Base_ConvertScalarToVector128Single
        3,                      // NI_X86Base_ConvertScalarToVector128UInt32
        3,                      // NI_X86Base_ConvertToInt32
        byte.MaxValue,          // NI_X86Base_ConvertToInt32WithTruncation
        3,                      // NI_X86Base_ConvertToUInt32
        5,                      // NI_X86Base_ConvertToVector128Double
        1,                      // NI_X86Base_ConvertToVector128Int16
        1,                      // NI_X86Base_ConvertToVector128Int32
        byte.MaxValue,          // NI_X86Base_ConvertToVector128Int32WithTruncation
        1,                      // NI_X86Base_ConvertToVector128Int64
        4,                      // NI_X86Base_ConvertToVector128Single
        3,                      // NI_X86Base_Crc32
        25,                     // NI_X86Base_DivRem
        byte.MaxValue,          // NI_X86Base_Divide
        byte.MaxValue,          // NI_X86Base_DivideScalar
        byte.MaxValue,          // NI_X86Base_DotProduct
        4,                      // NI_X86Base_Extract
        byte.MaxValue,          // NI_X86Base_Floor
        byte.MaxValue,          // NI_X86Base_FloorScalar
        3,                      // NI_X86Base_HorizontalAdd
        3,                      // NI_X86Base_HorizontalAddSaturate
        3,                      // NI_X86Base_HorizontalSubtract
        3,                      // NI_X86Base_HorizontalSubtractSaturate
        4,                      // NI_X86Base_Insert
        byte.MaxValue,          // NI_X86Base_LoadAlignedVector128
        byte.MaxValue,          // NI_X86Base_LoadAlignedVector128NonTemporal
        byte.MaxValue,          // NI_X86Base_LoadAndDuplicateToVector128
        byte.MaxValue,          // NI_X86Base_LoadDquVector128
        byte.MaxValue,          // NI_X86Base_LoadFence
        byte.MaxValue,          // NI_X86Base_LoadHigh
        byte.MaxValue,          // NI_X86Base_LoadLow
        byte.MaxValue,          // NI_X86Base_LoadScalarVector128
        byte.MaxValue,          // NI_X86Base_LoadVector128
        byte.MaxValue,          // NI_X86Base_MaskMove
        1,                      // NI_X86Base_Max
        byte.MaxValue,          // NI_X86Base_MaxScalar
        byte.MaxValue,          // NI_X86Base_MemoryFence
        1,                      // NI_X86Base_Min
        4,                      // NI_X86Base_MinHorizontal
        byte.MaxValue,          // NI_X86Base_MinScalar
        byte.MaxValue,          // NI_X86Base_MoveAndDuplicate
        byte.MaxValue,          // NI_X86Base_MoveHighAndDuplicate
        byte.MaxValue,          // NI_X86Base_MoveHighToLow
        byte.MaxValue,          // NI_X86Base_MoveLowAndDuplicate
        byte.MaxValue,          // NI_X86Base_MoveLowToHigh
        3,                      // NI_X86Base_MoveMask
        1,                      // NI_X86Base_MoveScalar
        3,                      // NI_X86Base_MultipleSumAbsoluteDifferences
        5,                      // NI_X86Base_Multiply
        5,                      // NI_X86Base_MultiplyAddAdjacent
        5,                      // NI_X86Base_MultiplyHigh
        5,                      // NI_X86Base_MultiplyHighRoundScale
        byte.MaxValue,          // NI_X86Base_MultiplyLow
        byte.MaxValue,          // NI_X86Base_MultiplyScalar
        1,                      // NI_X86Base_Or
        1,                      // NI_X86Base_PackSignedSaturate
        1,                      // NI_X86Base_PackUnsignedSaturate
        byte.MaxValue,          // NI_X86Base_Pause
        3,                      // NI_X86Base_PopCount
        byte.MaxValue,          // NI_X86Base_Prefetch0
        byte.MaxValue,          // NI_X86Base_Prefetch1
        byte.MaxValue,          // NI_X86Base_Prefetch2
        byte.MaxValue,          // NI_X86Base_PrefetchNonTemporal
        byte.MaxValue,          // NI_X86Base_Reciprocal
        byte.MaxValue,          // NI_X86Base_ReciprocalScalar
        byte.MaxValue,          // NI_X86Base_ReciprocalSqrt
        byte.MaxValue,          // NI_X86Base_ReciprocalSqrtScalar
        byte.MaxValue,          // NI_X86Base_RoundCurrentDirection
        byte.MaxValue,          // NI_X86Base_RoundCurrentDirectionScalar
        byte.MaxValue,          // NI_X86Base_RoundToNearestInteger
        byte.MaxValue,          // NI_X86Base_RoundToNearestIntegerScalar
        byte.MaxValue,          // NI_X86Base_RoundToNegativeInfinity
        byte.MaxValue,          // NI_X86Base_RoundToNegativeInfinityScalar
        byte.MaxValue,          // NI_X86Base_RoundToPositiveInfinity
        byte.MaxValue,          // NI_X86Base_RoundToPositiveInfinityScalar
        byte.MaxValue,          // NI_X86Base_RoundToZero
        byte.MaxValue,          // NI_X86Base_RoundToZeroScalar
        1,                      // NI_X86Base_ShiftLeftLogical
        1,                      // NI_X86Base_ShiftLeftLogical128BitLane
        1,                      // NI_X86Base_ShiftRightArithmetic
        1,                      // NI_X86Base_ShiftRightLogical
        1,                      // NI_X86Base_ShiftRightLogical128BitLane
        1,                      // NI_X86Base_Shuffle
        1,                      // NI_X86Base_ShuffleHigh
        1,                      // NI_X86Base_ShuffleLow
        1,                      // NI_X86Base_Sign
        byte.MaxValue,          // NI_X86Base_Sqrt
        byte.MaxValue,          // NI_X86Base_SqrtScalar
        byte.MaxValue,          // NI_X86Base_Store
        byte.MaxValue,          // NI_X86Base_StoreAligned
        byte.MaxValue,          // NI_X86Base_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_X86Base_StoreFence
        byte.MaxValue,          // NI_X86Base_StoreHigh
        byte.MaxValue,          // NI_X86Base_StoreLow
        byte.MaxValue,          // NI_X86Base_StoreNonTemporal
        byte.MaxValue,          // NI_X86Base_StoreScalar
        1,                      // NI_X86Base_Subtract
        1,                      // NI_X86Base_SubtractSaturate
        byte.MaxValue,          // NI_X86Base_SubtractScalar
        3,                      // NI_X86Base_SumAbsoluteDifferences
        4,                      // NI_X86Base_TestC
        4,                      // NI_X86Base_TestNotZAndNotC
        4,                      // NI_X86Base_TestZ
        1,                      // NI_X86Base_UnpackHigh
        1,                      // NI_X86Base_UnpackLow
        1,                      // NI_X86Base_Xor
        4,                      // NI_X86Base_X64_BigMul
        3,                      // NI_X86Base_X64_BitScanForward
        3,                      // NI_X86Base_X64_BitScanReverse
        5,                      // NI_X86Base_X64_ConvertScalarToVector128Double
        3,                      // NI_X86Base_X64_ConvertScalarToVector128Int64
        5,                      // NI_X86Base_X64_ConvertScalarToVector128Single
        3,                      // NI_X86Base_X64_ConvertScalarToVector128UInt64
        3,                      // NI_X86Base_X64_ConvertToInt64
        byte.MaxValue,          // NI_X86Base_X64_ConvertToInt64WithTruncation
        3,                      // NI_X86Base_X64_ConvertToUInt64
        3,                      // NI_X86Base_X64_Crc32
        57,                     // NI_X86Base_X64_DivRem
        4,                      // NI_X86Base_X64_Extract
        4,                      // NI_X86Base_X64_Insert
        3,                      // NI_X86Base_X64_PopCount
        byte.MaxValue,          // NI_X86Base_X64_StoreNonTemporal
        byte.MaxValue,          // NI_AVX_Add
        byte.MaxValue,          // NI_AVX_AddSubtract
        byte.MaxValue,          // NI_AVX_And
        byte.MaxValue,          // NI_AVX_AndNot
        byte.MaxValue,          // NI_AVX_Blend
        byte.MaxValue,          // NI_AVX_BlendVariable
        byte.MaxValue,          // NI_AVX_BroadcastScalarToVector128
        byte.MaxValue,          // NI_AVX_BroadcastScalarToVector256
        byte.MaxValue,          // NI_AVX_BroadcastVector128ToVector256
        byte.MaxValue,          // NI_AVX_Ceiling
        byte.MaxValue,          // NI_AVX_Compare
        byte.MaxValue,          // NI_AVX_CompareEqual
        byte.MaxValue,          // NI_AVX_CompareGreaterThan
        byte.MaxValue,          // NI_AVX_CompareGreaterThanOrEqual
        byte.MaxValue,          // NI_AVX_CompareLessThan
        byte.MaxValue,          // NI_AVX_CompareLessThanOrEqual
        byte.MaxValue,          // NI_AVX_CompareNotEqual
        byte.MaxValue,          // NI_AVX_CompareNotGreaterThan
        byte.MaxValue,          // NI_AVX_CompareNotGreaterThanOrEqual
        byte.MaxValue,          // NI_AVX_CompareNotLessThan
        byte.MaxValue,          // NI_AVX_CompareNotLessThanOrEqual
        byte.MaxValue,          // NI_AVX_CompareOrdered
        byte.MaxValue,          // NI_AVX_CompareScalar
        byte.MaxValue,          // NI_AVX_CompareUnordered
        7,                      // NI_AVX_ConvertToVector128Int32
        7,                      // NI_AVX_ConvertToVector128Int32WithTruncation
        byte.MaxValue,          // NI_AVX_ConvertToVector128Single
        7,                      // NI_AVX_ConvertToVector256Double
        4,                      // NI_AVX_ConvertToVector256Int32
        byte.MaxValue,          // NI_AVX_ConvertToVector256Int32WithTruncation
        4,                      // NI_AVX_ConvertToVector256Single
        byte.MaxValue,          // NI_AVX_Divide
        byte.MaxValue,          // NI_AVX_DotProduct
        byte.MaxValue,          // NI_AVX_DuplicateEvenIndexed
        byte.MaxValue,          // NI_AVX_DuplicateOddIndexed
        3,                      // NI_AVX_ExtractVector128
        byte.MaxValue,          // NI_AVX_Floor
        byte.MaxValue,          // NI_AVX_HorizontalAdd
        byte.MaxValue,          // NI_AVX_HorizontalSubtract
        3,                      // NI_AVX_InsertVector128
        byte.MaxValue,          // NI_AVX_LoadAlignedVector256
        byte.MaxValue,          // NI_AVX_LoadDquVector256
        byte.MaxValue,          // NI_AVX_LoadVector256
        byte.MaxValue,          // NI_AVX_MaskLoad
        byte.MaxValue,          // NI_AVX_MaskStore
        byte.MaxValue,          // NI_AVX_Max
        byte.MaxValue,          // NI_AVX_Min
        byte.MaxValue,          // NI_AVX_MoveMask
        byte.MaxValue,          // NI_AVX_Multiply
        byte.MaxValue,          // NI_AVX_Or
        byte.MaxValue,          // NI_AVX_Permute
        3,                      // NI_AVX_Permute2x128
        byte.MaxValue,          // NI_AVX_PermuteVar
        byte.MaxValue,          // NI_AVX_Reciprocal
        byte.MaxValue,          // NI_AVX_ReciprocalSqrt
        byte.MaxValue,          // NI_AVX_RoundCurrentDirection
        byte.MaxValue,          // NI_AVX_RoundToNearestInteger
        byte.MaxValue,          // NI_AVX_RoundToNegativeInfinity
        byte.MaxValue,          // NI_AVX_RoundToPositiveInfinity
        byte.MaxValue,          // NI_AVX_RoundToZero
        byte.MaxValue,          // NI_AVX_Shuffle
        byte.MaxValue,          // NI_AVX_Sqrt
        byte.MaxValue,          // NI_AVX_Store
        byte.MaxValue,          // NI_AVX_StoreAligned
        byte.MaxValue,          // NI_AVX_StoreAlignedNonTemporal
        byte.MaxValue,          // NI_AVX_Subtract
        6,                      // NI_AVX_TestC
        6,                      // NI_AVX_TestNotZAndNotC
        6,                      // NI_AVX_TestZ
        byte.MaxValue,          // NI_AVX_UnpackHigh
        byte.MaxValue,          // NI_AVX_UnpackLow
        byte.MaxValue,          // NI_AVX_Xor
        1,                      // NI_AVX2_Abs
        1,                      // NI_AVX2_Add
        1,                      // NI_AVX2_AddSaturate
        1,                      // NI_AVX2_AlignRight
        1,                      // NI_AVX2_And
        byte.MaxValue,          // NI_AVX2_AndNot
        1,                      // NI_AVX2_Average
        2,                      // NI_AVX2_BitFieldExtract
        1,                      // NI_AVX2_Blend
        1,                      // NI_AVX2_BlendVariable
        1,                      // NI_AVX2_BroadcastScalarToVector128
        3,                      // NI_AVX2_BroadcastScalarToVector256
        byte.MaxValue,          // NI_AVX2_BroadcastVector128ToVector256
        1,                      // NI_AVX2_CompareEqual
        1,                      // NI_AVX2_CompareGreaterThan
        1,                      // NI_AVX2_CompareLessThan
        3,                      // NI_AVX2_ConvertToInt32
        3,                      // NI_AVX2_ConvertToUInt32
        byte.MaxValue,          // NI_AVX2_ConvertToVector128Half
        5,                      // NI_AVX2_ConvertToVector128Single
        byte.MaxValue,          // NI_AVX2_ConvertToVector256Half
        3,                      // NI_AVX2_ConvertToVector256Int16
        3,                      // NI_AVX2_ConvertToVector256Int32
        3,                      // NI_AVX2_ConvertToVector256Int64
        7,                      // NI_AVX2_ConvertToVector256Single
        1,                      // NI_AVX2_ExtractLowestSetBit
        3,                      // NI_AVX2_ExtractVector128
        byte.MaxValue,          // NI_AVX2_GatherMaskVector128
        byte.MaxValue,          // NI_AVX2_GatherMaskVector256
        byte.MaxValue,          // NI_AVX2_GatherVector128
        byte.MaxValue,          // NI_AVX2_GatherVector256
        1,                      // NI_AVX2_GetMaskUpToLowestSetBit
        3,                      // NI_AVX2_HorizontalAdd
        3,                      // NI_AVX2_HorizontalAddSaturate
        3,                      // NI_AVX2_HorizontalSubtract
        3,                      // NI_AVX2_HorizontalSubtractSaturate
        3,                      // NI_AVX2_InsertVector128
        3,                      // NI_AVX2_LeadingZeroCount
        byte.MaxValue,          // NI_AVX2_LoadAlignedVector256NonTemporal
        byte.MaxValue,          // NI_AVX2_MaskLoad
        byte.MaxValue,          // NI_AVX2_MaskStore
        1,                      // NI_AVX2_Max
        1,                      // NI_AVX2_Min
        4,                      // NI_AVX2_MoveMask
        3,                      // NI_AVX2_MultipleSumAbsoluteDifferences
        5,                      // NI_AVX2_Multiply
        byte.MaxValue,          // NI_AVX2_MultiplyAdd
        5,                      // NI_AVX2_MultiplyAddAdjacent
        byte.MaxValue,          // NI_AVX2_MultiplyAddNegated
        byte.MaxValue,          // NI_AVX2_MultiplyAddNegatedScalar
        byte.MaxValue,          // NI_AVX2_MultiplyAddScalar
        byte.MaxValue,          // NI_AVX2_MultiplyAddSubtract
        5,                      // NI_AVX2_MultiplyHigh
        5,                      // NI_AVX2_MultiplyHighRoundScale
        byte.MaxValue,          // NI_AVX2_MultiplyLow
        4,                      // NI_AVX2_MultiplyNoFlags
        byte.MaxValue,          // NI_AVX2_MultiplySubtract
        byte.MaxValue,          // NI_AVX2_MultiplySubtractAdd
        byte.MaxValue,          // NI_AVX2_MultiplySubtractNegated
        byte.MaxValue,          // NI_AVX2_MultiplySubtractNegatedScalar
        byte.MaxValue,          // NI_AVX2_MultiplySubtractScalar
        1,                      // NI_AVX2_Or
        1,                      // NI_AVX2_PackSignedSaturate
        1,                      // NI_AVX2_PackUnsignedSaturate
        3,                      // NI_AVX2_ParallelBitDeposit
        3,                      // NI_AVX2_ParallelBitExtract
        3,                      // NI_AVX2_Permute2x128
        3,                      // NI_AVX2_Permute4x64
        3,                      // NI_AVX2_PermuteVar8x32
        1,                      // NI_AVX2_ResetLowestSetBit
        1,                      // NI_AVX2_ShiftLeftLogical
        1,                      // NI_AVX2_ShiftLeftLogical128BitLane
        2,                      // NI_AVX2_ShiftLeftLogicalVariable
        1,                      // NI_AVX2_ShiftRightArithmetic
        2,                      // NI_AVX2_ShiftRightArithmeticVariable
        1,                      // NI_AVX2_ShiftRightLogical
        1,                      // NI_AVX2_ShiftRightLogical128BitLane
        2,                      // NI_AVX2_ShiftRightLogicalVariable
        1,                      // NI_AVX2_Shuffle
        1,                      // NI_AVX2_ShuffleHigh
        1,                      // NI_AVX2_ShuffleLow
        1,                      // NI_AVX2_Sign
        1,                      // NI_AVX2_Subtract
        1,                      // NI_AVX2_SubtractSaturate
        3,                      // NI_AVX2_SumAbsoluteDifferences
        3,                      // NI_AVX2_TrailingZeroCount
        1,                      // NI_AVX2_UnpackHigh
        1,                      // NI_AVX2_UnpackLow
        1,                      // NI_AVX2_Xor
        1,                      // NI_AVX2_ZeroHighBits
        1,                      // NI_AVX2_X64_AndNot
        2,                      // NI_AVX2_X64_BitFieldExtract
        1,                      // NI_AVX2_X64_ExtractLowestSetBit
        1,                      // NI_AVX2_X64_GetMaskUpToLowestSetBit
        3,                      // NI_AVX2_X64_LeadingZeroCount
        4,                      // NI_AVX2_X64_MultiplyNoFlags
        3,                      // NI_AVX2_X64_ParallelBitDeposit
        3,                      // NI_AVX2_X64_ParallelBitExtract
        1,                      // NI_AVX2_X64_ResetLowestSetBit
        3,                      // NI_AVX2_X64_TrailingZeroCount
        1,                      // NI_AVX2_X64_ZeroHighBits
        1,                      // NI_AVX512_Abs
        1,                      // NI_AVX512_Add
        1,                      // NI_AVX512_AddSaturate
        byte.MaxValue,          // NI_AVX512_AddScalar
        1,                      // NI_AVX512_AlignRight
        byte.MaxValue,          // NI_AVX512_AlignRight32
        byte.MaxValue,          // NI_AVX512_AlignRight64
        1,                      // NI_AVX512_And
        1,                      // NI_AVX512_AndNot
        1,                      // NI_AVX512_Average
        byte.MaxValue,          // NI_AVX512_BlendVariable
        1,                      // NI_AVX512_BroadcastPairScalarToVector128
        3,                      // NI_AVX512_BroadcastPairScalarToVector256
        3,                      // NI_AVX512_BroadcastPairScalarToVector512
        3,                      // NI_AVX512_BroadcastScalarToVector512
        byte.MaxValue,          // NI_AVX512_BroadcastVector128ToVector512
        byte.MaxValue,          // NI_AVX512_BroadcastVector256ToVector512
        byte.MaxValue,          // NI_AVX512_Classify
        byte.MaxValue,          // NI_AVX512_ClassifyScalar
        byte.MaxValue,          // NI_AVX512_Compare
        byte.MaxValue,          // NI_AVX512_CompareEqual
        byte.MaxValue,          // NI_AVX512_CompareGreaterThan
        byte.MaxValue,          // NI_AVX512_CompareGreaterThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareLessThan
        byte.MaxValue,          // NI_AVX512_CompareLessThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareNotEqual
        byte.MaxValue,          // NI_AVX512_CompareNotGreaterThan
        byte.MaxValue,          // NI_AVX512_CompareNotGreaterThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareNotLessThan
        byte.MaxValue,          // NI_AVX512_CompareNotLessThanOrEqual
        byte.MaxValue,          // NI_AVX512_CompareOrdered
        byte.MaxValue,          // NI_AVX512_CompareUnordered
        byte.MaxValue,          // NI_AVX512_Compress
        byte.MaxValue,          // NI_AVX512_CompressStore
        5,                      // NI_AVX512_ConvertScalarToVector128Double
        5,                      // NI_AVX512_ConvertScalarToVector128Single
        byte.MaxValue,          // NI_AVX512_ConvertToInt32
        byte.MaxValue,          // NI_AVX512_ConvertToUInt32
        byte.MaxValue,          // NI_AVX512_ConvertToUInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Byte
        byte.MaxValue,          // NI_AVX512_ConvertToVector128ByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Double
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int16
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int32
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int64
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Int64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128SByte
        byte.MaxValue,          // NI_AVX512_ConvertToVector128SByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128Single
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt16
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt32
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt64
        byte.MaxValue,          // NI_AVX512_ConvertToVector128UInt64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Byte
        byte.MaxValue,          // NI_AVX512_ConvertToVector256ByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Double
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int16
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int32
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int64
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Int64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256SByte
        byte.MaxValue,          // NI_AVX512_ConvertToVector256SByteWithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256Single
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt16
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt16WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt32
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt32WithSaturation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt32WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt64
        byte.MaxValue,          // NI_AVX512_ConvertToVector256UInt64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Double
        3,                      // NI_AVX512_ConvertToVector512Int16
        3,                      // NI_AVX512_ConvertToVector512Int32
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Int32WithTruncation
        3,                      // NI_AVX512_ConvertToVector512Int64
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Int64WithTruncation
        byte.MaxValue,          // NI_AVX512_ConvertToVector512Single
        3,                      // NI_AVX512_ConvertToVector512UInt16
        3,                      // NI_AVX512_ConvertToVector512UInt32
        byte.MaxValue,          // NI_AVX512_ConvertToVector512UInt32WithTruncation
        3,                      // NI_AVX512_ConvertToVector512UInt64
        byte.MaxValue,          // NI_AVX512_ConvertToVector512UInt64WithTruncation
        byte.MaxValue,          // NI_AVX512_DetectConflicts
        byte.MaxValue,          // NI_AVX512_Divide
        byte.MaxValue,          // NI_AVX512_DivideScalar
        byte.MaxValue,          // NI_AVX512_DuplicateEvenIndexed
        byte.MaxValue,          // NI_AVX512_DuplicateOddIndexed
        byte.MaxValue,          // NI_AVX512_Expand
        byte.MaxValue,          // NI_AVX512_ExpandLoad
        3,                      // NI_AVX512_ExtractVector128
        3,                      // NI_AVX512_ExtractVector256
        byte.MaxValue,          // NI_AVX512_Fixup
        byte.MaxValue,          // NI_AVX512_FixupScalar
        byte.MaxValue,          // NI_AVX512_FusedMultiplyAdd
        byte.MaxValue,          // NI_AVX512_FusedMultiplyAddNegated
        byte.MaxValue,          // NI_AVX512_FusedMultiplyAddNegatedScalar
        byte.MaxValue,          // NI_AVX512_FusedMultiplyAddScalar
        byte.MaxValue,          // NI_AVX512_FusedMultiplyAddSubtract
        byte.MaxValue,          // NI_AVX512_FusedMultiplySubtract
        byte.MaxValue,          // NI_AVX512_FusedMultiplySubtractAdd
        byte.MaxValue,          // NI_AVX512_FusedMultiplySubtractNegated
        byte.MaxValue,          // NI_AVX512_FusedMultiplySubtractNegatedScalar
        byte.MaxValue,          // NI_AVX512_FusedMultiplySubtractScalar
        byte.MaxValue,          // NI_AVX512_GetExponent
        byte.MaxValue,          // NI_AVX512_GetExponentScalar
        byte.MaxValue,          // NI_AVX512_GetMantissa
        byte.MaxValue,          // NI_AVX512_GetMantissaScalar
        3,                      // NI_AVX512_InsertVector128
        3,                      // NI_AVX512_InsertVector256
        4,                      // NI_AVX512_LeadingZeroCount
        byte.MaxValue,          // NI_AVX512_LoadAlignedVector512
        byte.MaxValue,          // NI_AVX512_LoadAlignedVector512NonTemporal
        byte.MaxValue,          // NI_AVX512_LoadVector512
        byte.MaxValue,          // NI_AVX512_MaskLoad
        byte.MaxValue,          // NI_AVX512_MaskLoadAligned
        byte.MaxValue,          // NI_AVX512_MaskStore
        byte.MaxValue,          // NI_AVX512_MaskStoreAligned
        1,                      // NI_AVX512_Max
        1,                      // NI_AVX512_Min
        3,                      // NI_AVX512_MoveMask
        5,                      // NI_AVX512_Multiply
        5,                      // NI_AVX512_MultiplyAddAdjacent
        5,                      // NI_AVX512_MultiplyHigh
        5,                      // NI_AVX512_MultiplyHighRoundScale
        5,                      // NI_AVX512_MultiplyLow
        byte.MaxValue,          // NI_AVX512_MultiplyScalar
        1,                      // NI_AVX512_Or
        1,                      // NI_AVX512_PackSignedSaturate
        1,                      // NI_AVX512_PackUnsignedSaturate
        byte.MaxValue,          // NI_AVX512_Permute2x64
        byte.MaxValue,          // NI_AVX512_Permute4x32
        3,                      // NI_AVX512_Permute4x64
        6,                      // NI_AVX512_PermuteVar16x16
        6,                      // NI_AVX512_PermuteVar16x16x2
        3,                      // NI_AVX512_PermuteVar16x32
        3,                      // NI_AVX512_PermuteVar16x32x2
        byte.MaxValue,          // NI_AVX512_PermuteVar2x64
        3,                      // NI_AVX512_PermuteVar2x64x2
        6,                      // NI_AVX512_PermuteVar32x16
        6,                      // NI_AVX512_PermuteVar32x16x2
        byte.MaxValue,          // NI_AVX512_PermuteVar4x32
        3,                      // NI_AVX512_PermuteVar4x32x2
        3,                      // NI_AVX512_PermuteVar4x64
        3,                      // NI_AVX512_PermuteVar4x64x2
        6,                      // NI_AVX512_PermuteVar8x16
        6,                      // NI_AVX512_PermuteVar8x16x2
        3,                      // NI_AVX512_PermuteVar8x32x2
        3,                      // NI_AVX512_PermuteVar8x64
        3,                      // NI_AVX512_PermuteVar8x64x2
        byte.MaxValue,          // NI_AVX512_Range
        byte.MaxValue,          // NI_AVX512_RangeScalar
        byte.MaxValue,          // NI_AVX512_Reciprocal14
        byte.MaxValue,          // NI_AVX512_Reciprocal14Scalar
        byte.MaxValue,          // NI_AVX512_ReciprocalSqrt14
        byte.MaxValue,          // NI_AVX512_ReciprocalSqrt14Scalar
        byte.MaxValue,          // NI_AVX512_Reduce
        byte.MaxValue,          // NI_AVX512_ReduceScalar
        1,                      // NI_AVX512_RotateLeft
        1,                      // NI_AVX512_RotateLeftVariable
        1,                      // NI_AVX512_RotateRight
        1,                      // NI_AVX512_RotateRightVariable
        byte.MaxValue,          // NI_AVX512_RoundScale
        byte.MaxValue,          // NI_AVX512_RoundScaleScalar
        byte.MaxValue,          // NI_AVX512_Scale
        byte.MaxValue,          // NI_AVX512_ScaleScalar
        1,                      // NI_AVX512_ShiftLeftLogical
        1,                      // NI_AVX512_ShiftLeftLogical128BitLane
        1,                      // NI_AVX512_ShiftLeftLogicalVariable
        1,                      // NI_AVX512_ShiftRightArithmetic
        1,                      // NI_AVX512_ShiftRightArithmeticVariable
        1,                      // NI_AVX512_ShiftRightLogical
        1,                      // NI_AVX512_ShiftRightLogical128BitLane
        1,                      // NI_AVX512_ShiftRightLogicalVariable
        1,                      // NI_AVX512_Shuffle
        3,                      // NI_AVX512_Shuffle2x128
        3,                      // NI_AVX512_Shuffle4x128
        1,                      // NI_AVX512_ShuffleHigh
        1,                      // NI_AVX512_ShuffleLow
        byte.MaxValue,          // NI_AVX512_Sqrt
        byte.MaxValue,          // NI_AVX512_SqrtScalar
        byte.MaxValue,          // NI_AVX512_Store
        byte.MaxValue,          // NI_AVX512_StoreAligned
        byte.MaxValue,          // NI_AVX512_StoreAlignedNonTemporal
        1,                      // NI_AVX512_Subtract
        1,                      // NI_AVX512_SubtractSaturate
        byte.MaxValue,          // NI_AVX512_SubtractScalar
        3,                      // NI_AVX512_SumAbsoluteDifferences
        3,                      // NI_AVX512_SumAbsoluteDifferencesInBlock32
        1,                      // NI_AVX512_TernaryLogic
        1,                      // NI_AVX512_UnpackHigh
        1,                      // NI_AVX512_UnpackLow
        1,                      // NI_AVX512_Xor
        5,                      // NI_AVX512_X64_ConvertScalarToVector128Double
        5,                      // NI_AVX512_X64_ConvertScalarToVector128Single
        byte.MaxValue,          // NI_AVX512_X64_ConvertToInt64
        byte.MaxValue,          // NI_AVX512_X64_ConvertToUInt64
        byte.MaxValue,          // NI_AVX512_X64_ConvertToUInt64WithTruncation
        3,                      // NI_AVX512v2_MultiShift
        3,                      // NI_AVX512v2_PermuteVar16x8
        4,                      // NI_AVX512v2_PermuteVar16x8x2
        3,                      // NI_AVX512v2_PermuteVar32x8
        4,                      // NI_AVX512v2_PermuteVar32x8x2
        3,                      // NI_AVX512v2_PermuteVar64x8
        4,                      // NI_AVX512v2_PermuteVar64x8x2
        byte.MaxValue,          // NI_AVX512v3_Compress
        byte.MaxValue,          // NI_AVX512v3_CompressStore
        byte.MaxValue,          // NI_AVX512v3_Expand
        byte.MaxValue,          // NI_AVX512v3_ExpandLoad
        byte.MaxValue,          // NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32
        byte.MaxValue,          // NI_AVX10v2_ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32
        byte.MaxValue,          // NI_AVX10v2_ConvertToInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32
        byte.MaxValue,          // NI_AVX10v2_ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32
        byte.MaxValue,          // NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_MinMax
        byte.MaxValue,          // NI_AVX10v2_MinMaxScalar
        3,                      // NI_AVX10v2_MoveScalar
        3,                      // NI_AVX10v2_MultipleSumAbsoluteDifferences
        byte.MaxValue,          // NI_AVX10v2_StoreScalar
        byte.MaxValue,          // NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation
        byte.MaxValue,          // NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation
        1,                      // NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction
        1,                      // NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction
        1,                      // NI_AVX512BMM_ReverseBits
        5,                      // NI_AVXVNNI_MultiplyWideningAndAdd
        5,                      // NI_AVXVNNI_MultiplyWideningAndAddSaturate
        5,                      // NI_AVXVNNIINT_MultiplyWideningAndAdd
        5,                      // NI_AVXVNNIINT_MultiplyWideningAndAddSaturate
        5,                      // NI_AVXVNNIINT_V512_MultiplyWideningAndAdd
        5,                      // NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate
        7,                      // NI_AES_CarrylessMultiply
        4,                      // NI_AES_Decrypt
        4,                      // NI_AES_DecryptLast
        4,                      // NI_AES_Encrypt
        4,                      // NI_AES_EncryptLast
        8,                      // NI_AES_InverseMixColumns
        6,                      // NI_AES_KeygenAssist
        7,                      // NI_AES_V256_CarrylessMultiply
        7,                      // NI_AES_V512_CarrylessMultiply
        byte.MaxValue,          // NI_X86Serialize_Serialize
        3,                      // NI_GFNI_GaloisFieldAffineTransform
        3,                      // NI_GFNI_GaloisFieldAffineTransformInverse
        3,                      // NI_GFNI_GaloisFieldMultiply
        3,                      // NI_GFNI_V256_GaloisFieldAffineTransform
        3,                      // NI_GFNI_V256_GaloisFieldAffineTransformInverse
        3,                      // NI_GFNI_V256_GaloisFieldMultiply
        3,                      // NI_GFNI_V512_GaloisFieldAffineTransform
        3,                      // NI_GFNI_V512_GaloisFieldAffineTransformInverse
        3,                      // NI_GFNI_V512_GaloisFieldMultiply
        byte.MaxValue,          // NI_X86Base_COMIS
        4,                      // NI_X86Base_PTEST
        byte.MaxValue,          // NI_X86Base_UCOMIS
        byte.MaxValue,          // NI_AVX_PTEST
        1,                      // NI_AVX2_AndNotVector
        1,                      // NI_AVX2_AndNotScalar
        4,                      // NI_AVX512_KORTEST
        4,                      // NI_AVX512_KTEST
        4,                      // NI_AVX512_PTESTM
        4,                      // NI_AVX512_PTESTNM
        4,                      // NI_AVX512_AddMask
        1,                      // NI_AVX512_AndMask
        1,                      // NI_AVX512_AndNotMask
        1,                      // NI_AVX512_BlendVariableMask
        byte.MaxValue,          // NI_AVX512_ClassifyMask
        byte.MaxValue,          // NI_AVX512_ClassifyScalarMask
        byte.MaxValue,          // NI_AVX512_CompareMask
        1,                      // NI_AVX512_CompareEqualMask
        1,                      // NI_AVX512_CompareGreaterThanMask
        1,                      // NI_AVX512_CompareGreaterThanOrEqualMask
        1,                      // NI_AVX512_CompareLessThanMask
        1,                      // NI_AVX512_CompareLessThanOrEqualMask
        1,                      // NI_AVX512_CompareNotEqualMask
        1,                      // NI_AVX512_CompareNotGreaterThanMask
        1,                      // NI_AVX512_CompareNotGreaterThanOrEqualMask
        1,                      // NI_AVX512_CompareNotLessThanMask
        1,                      // NI_AVX512_CompareNotLessThanOrEqualMask
        byte.MaxValue,          // NI_AVX512_CompareOrderedMask
        byte.MaxValue,          // NI_AVX512_CompareScalarMask
        byte.MaxValue,          // NI_AVX512_CompareUnorderedMask
        3,                      // NI_AVX512_CompressMask
        byte.MaxValue,          // NI_AVX512_CompressStoreMask
        byte.MaxValue,          // NI_AVX512_ConvertMaskToVector
        3,                      // NI_AVX512_ConvertVectorToMask
        byte.MaxValue,          // NI_AVX512_ExpandLoadMask
        3,                      // NI_AVX512_ExpandMask
        byte.MaxValue,          // NI_AVX512_MaskLoadMask
        byte.MaxValue,          // NI_AVX512_MaskLoadAlignedMask
        byte.MaxValue,          // NI_AVX512_MaskStoreMask
        byte.MaxValue,          // NI_AVX512_MaskStoreAlignedMask
        1,                      // NI_AVX512_NotMask
        1,                      // NI_AVX512_OrMask
        4,                      // NI_AVX512_ShiftLeftMask
        4,                      // NI_AVX512_ShiftRightMask
        1,                      // NI_AVX512_XorMask
        1,                      // NI_AVX512_XnorMask
    ];

#if DEBUG
    private static string[] s_names = [
        "Abs",																// NI_Vector128_Abs
        "AddSaturate",														// NI_Vector128_AddSaturate
        "AndNot",															// NI_Vector128_AndNot
        "As",																// NI_Vector128_As
        "AsByte",															// NI_Vector128_AsByte
        "AsDouble",															// NI_Vector128_AsDouble
        "AsInt16",															// NI_Vector128_AsInt16
        "AsInt32",															// NI_Vector128_AsInt32
        "AsInt64",															// NI_Vector128_AsInt64
        "AsNInt",															// NI_Vector128_AsNInt
        "AsNUInt",															// NI_Vector128_AsNUInt
        "AsSByte",															// NI_Vector128_AsSByte
        "AsSingle",															// NI_Vector128_AsSingle
        "AsUInt16",															// NI_Vector128_AsUInt16
        "AsUInt32",															// NI_Vector128_AsUInt32
        "AsUInt64",															// NI_Vector128_AsUInt64
        "AsVector",															// NI_Vector128_AsVector
        "AsVector128",														// NI_Vector128_AsVector128
        "AsVector128Unsafe",												// NI_Vector128_AsVector128Unsafe
        "AsVector2",														// NI_Vector128_AsVector2
        "AsVector3",														// NI_Vector128_AsVector3
        "AsVector4",														// NI_Vector128_AsVector4
        "Ceiling",															// NI_Vector128_Ceiling
        "ConditionalSelect",												// NI_Vector128_ConditionalSelect
        "ConvertToDouble",													// NI_Vector128_ConvertToDouble
        "ConvertToInt32",													// NI_Vector128_ConvertToInt32
        "ConvertToInt32Native",												// NI_Vector128_ConvertToInt32Native
        "ConvertToInt64",													// NI_Vector128_ConvertToInt64
        "ConvertToInt64Native",												// NI_Vector128_ConvertToInt64Native
        "ConvertToSingle",													// NI_Vector128_ConvertToSingle
        "ConvertToUInt32",													// NI_Vector128_ConvertToUInt32
        "ConvertToUInt32Native",											// NI_Vector128_ConvertToUInt32Native
        "ConvertToUInt64",													// NI_Vector128_ConvertToUInt64
        "ConvertToUInt64Native",											// NI_Vector128_ConvertToUInt64Native
        "Create",															// NI_Vector128_Create
        "CreateScalar",														// NI_Vector128_CreateScalar
        "CreateScalarUnsafe",												// NI_Vector128_CreateScalarUnsafe
        "CreateSequence",													// NI_Vector128_CreateSequence
        "Dot",																// NI_Vector128_Dot
        "Equals",															// NI_Vector128_Equals
        "EqualsAny",														// NI_Vector128_EqualsAny
        "ExtractMostSignificantBits",										// NI_Vector128_ExtractMostSignificantBits
        "Floor",															// NI_Vector128_Floor
        "FusedMultiplyAdd",													// NI_Vector128_FusedMultiplyAdd
        "GetElement",														// NI_Vector128_GetElement
        "GreaterThan",														// NI_Vector128_GreaterThan
        "GreaterThanAll",													// NI_Vector128_GreaterThanAll
        "GreaterThanAny",													// NI_Vector128_GreaterThanAny
        "GreaterThanOrEqual",												// NI_Vector128_GreaterThanOrEqual
        "GreaterThanOrEqualAll",											// NI_Vector128_GreaterThanOrEqualAll
        "GreaterThanOrEqualAny",											// NI_Vector128_GreaterThanOrEqualAny
        "IsEvenInteger",													// NI_Vector128_IsEvenInteger
        "IsFinite",															// NI_Vector128_IsFinite
        "IsInfinity",														// NI_Vector128_IsInfinity
        "IsInteger",														// NI_Vector128_IsInteger
        "IsNaN",															// NI_Vector128_IsNaN
        "IsNegative",														// NI_Vector128_IsNegative
        "IsNegativeInfinity",												// NI_Vector128_IsNegativeInfinity
        "IsNormal",															// NI_Vector128_IsNormal
        "IsOddInteger",														// NI_Vector128_IsOddInteger
        "IsPositive",														// NI_Vector128_IsPositive
        "IsPositiveInfinity",												// NI_Vector128_IsPositiveInfinity
        "IsSubnormal",														// NI_Vector128_IsSubnormal
        "IsZero",															// NI_Vector128_IsZero
        "LessThan",															// NI_Vector128_LessThan
        "LessThanAll",														// NI_Vector128_LessThanAll
        "LessThanAny",														// NI_Vector128_LessThanAny
        "LessThanOrEqual",													// NI_Vector128_LessThanOrEqual
        "LessThanOrEqualAll",												// NI_Vector128_LessThanOrEqualAll
        "LessThanOrEqualAny",												// NI_Vector128_LessThanOrEqualAny
        "LoadAligned",														// NI_Vector128_LoadAligned
        "LoadAlignedNonTemporal",											// NI_Vector128_LoadAlignedNonTemporal
        "LoadUnsafe",														// NI_Vector128_LoadUnsafe
        "Max",																// NI_Vector128_Max
        "MaxMagnitude",														// NI_Vector128_MaxMagnitude
        "MaxMagnitudeNumber",												// NI_Vector128_MaxMagnitudeNumber
        "MaxNative",														// NI_Vector128_MaxNative
        "MaxNumber",														// NI_Vector128_MaxNumber
        "Min",																// NI_Vector128_Min
        "MinMagnitude",														// NI_Vector128_MinMagnitude
        "MinMagnitudeNumber",												// NI_Vector128_MinMagnitudeNumber
        "MinNative",														// NI_Vector128_MinNative
        "MinNumber",														// NI_Vector128_MinNumber
        "MultiplyAddEstimate",												// NI_Vector128_MultiplyAddEstimate
        "Narrow",															// NI_Vector128_Narrow
        "NarrowWithSaturation",												// NI_Vector128_NarrowWithSaturation
        "Round",															// NI_Vector128_Round
        "ShiftLeft",														// NI_Vector128_ShiftLeft
        "Shuffle",															// NI_Vector128_Shuffle
        "ShuffleNative",													// NI_Vector128_ShuffleNative
        "ShuffleNativeFallback",											// NI_Vector128_ShuffleNativeFallback
        "Sqrt",																// NI_Vector128_Sqrt
        "StoreAligned",														// NI_Vector128_StoreAligned
        "StoreAlignedNonTemporal",											// NI_Vector128_StoreAlignedNonTemporal
        "StoreUnsafe",														// NI_Vector128_StoreUnsafe
        "SubtractSaturate",													// NI_Vector128_SubtractSaturate
        "Sum",																// NI_Vector128_Sum
        "ToScalar",															// NI_Vector128_ToScalar
        "ToVector256",														// NI_Vector128_ToVector256
        "ToVector256Unsafe",												// NI_Vector128_ToVector256Unsafe
        "ToVector512",														// NI_Vector128_ToVector512
        "Truncate",															// NI_Vector128_Truncate
        "WidenLower",														// NI_Vector128_WidenLower
        "WidenUpper",														// NI_Vector128_WidenUpper
        "WithElement",														// NI_Vector128_WithElement
        "get_AllBitsSet",													// NI_Vector128_get_AllBitsSet
        "get_E",															// NI_Vector128_get_E
        "get_Epsilon",														// NI_Vector128_get_Epsilon
        "get_Indices",														// NI_Vector128_get_Indices
        "get_NaN",															// NI_Vector128_get_NaN
        "get_NegativeInfinity",												// NI_Vector128_get_NegativeInfinity
        "get_NegativeOne",													// NI_Vector128_get_NegativeOne
        "get_NegativeZero",													// NI_Vector128_get_NegativeZero
        "get_One",															// NI_Vector128_get_One
        "get_Pi",															// NI_Vector128_get_Pi
        "get_PositiveInfinity",												// NI_Vector128_get_PositiveInfinity
        "get_Tau",															// NI_Vector128_get_Tau
        "get_Zero",															// NI_Vector128_get_Zero
        "op_Addition",														// NI_Vector128_op_Addition
        "op_BitwiseAnd",													// NI_Vector128_op_BitwiseAnd
        "op_BitwiseOr",														// NI_Vector128_op_BitwiseOr
        "op_Division",														// NI_Vector128_op_Division
        "op_Equality",														// NI_Vector128_op_Equality
        "op_ExclusiveOr",													// NI_Vector128_op_ExclusiveOr
        "op_Inequality",													// NI_Vector128_op_Inequality
        "op_LeftShift",														// NI_Vector128_op_LeftShift
        "op_Multiply",														// NI_Vector128_op_Multiply
        "op_OnesComplement",												// NI_Vector128_op_OnesComplement
        "op_RightShift",													// NI_Vector128_op_RightShift
        "op_Subtraction",													// NI_Vector128_op_Subtraction
        "op_UnaryNegation",													// NI_Vector128_op_UnaryNegation
        "op_UnaryPlus",														// NI_Vector128_op_UnaryPlus
        "op_UnsignedRightShift",											// NI_Vector128_op_UnsignedRightShift
        "Abs",																// NI_Vector256_Abs
        "AddSaturate",														// NI_Vector256_AddSaturate
        "AndNot",															// NI_Vector256_AndNot
        "As",																// NI_Vector256_As
        "AsByte",															// NI_Vector256_AsByte
        "AsDouble",															// NI_Vector256_AsDouble
        "AsInt16",															// NI_Vector256_AsInt16
        "AsInt32",															// NI_Vector256_AsInt32
        "AsInt64",															// NI_Vector256_AsInt64
        "AsNInt",															// NI_Vector256_AsNInt
        "AsNUInt",															// NI_Vector256_AsNUInt
        "AsSByte",															// NI_Vector256_AsSByte
        "AsSingle",															// NI_Vector256_AsSingle
        "AsUInt16",															// NI_Vector256_AsUInt16
        "AsUInt32",															// NI_Vector256_AsUInt32
        "AsUInt64",															// NI_Vector256_AsUInt64
        "AsVector",															// NI_Vector256_AsVector
        "AsVector256",														// NI_Vector256_AsVector256
        "Ceiling",															// NI_Vector256_Ceiling
        "ConditionalSelect",												// NI_Vector256_ConditionalSelect
        "ConvertToDouble",													// NI_Vector256_ConvertToDouble
        "ConvertToInt32",													// NI_Vector256_ConvertToInt32
        "ConvertToInt32Native",												// NI_Vector256_ConvertToInt32Native
        "ConvertToInt64",													// NI_Vector256_ConvertToInt64
        "ConvertToInt64Native",												// NI_Vector256_ConvertToInt64Native
        "ConvertToSingle",													// NI_Vector256_ConvertToSingle
        "ConvertToUInt32",													// NI_Vector256_ConvertToUInt32
        "ConvertToUInt32Native",											// NI_Vector256_ConvertToUInt32Native
        "ConvertToUInt64",													// NI_Vector256_ConvertToUInt64
        "ConvertToUInt64Native",											// NI_Vector256_ConvertToUInt64Native
        "Create",															// NI_Vector256_Create
        "CreateScalar",														// NI_Vector256_CreateScalar
        "CreateScalarUnsafe",												// NI_Vector256_CreateScalarUnsafe
        "CreateSequence",													// NI_Vector256_CreateSequence
        "Dot",																// NI_Vector256_Dot
        "Equals",															// NI_Vector256_Equals
        "EqualsAny",														// NI_Vector256_EqualsAny
        "ExtractMostSignificantBits",										// NI_Vector256_ExtractMostSignificantBits
        "Floor",															// NI_Vector256_Floor
        "FusedMultiplyAdd",													// NI_Vector256_FusedMultiplyAdd
        "GetElement",														// NI_Vector256_GetElement
        "GetLower",															// NI_Vector256_GetLower
        "GetUpper",															// NI_Vector256_GetUpper
        "GreaterThan",														// NI_Vector256_GreaterThan
        "GreaterThanAll",													// NI_Vector256_GreaterThanAll
        "GreaterThanAny",													// NI_Vector256_GreaterThanAny
        "GreaterThanOrEqual",												// NI_Vector256_GreaterThanOrEqual
        "GreaterThanOrEqualAll",											// NI_Vector256_GreaterThanOrEqualAll
        "GreaterThanOrEqualAny",											// NI_Vector256_GreaterThanOrEqualAny
        "IsEvenInteger",													// NI_Vector256_IsEvenInteger
        "IsFinite",															// NI_Vector256_IsFinite
        "IsInfinity",														// NI_Vector256_IsInfinity
        "IsInteger",														// NI_Vector256_IsInteger
        "IsNaN",															// NI_Vector256_IsNaN
        "IsNegative",														// NI_Vector256_IsNegative
        "IsNegativeInfinity",												// NI_Vector256_IsNegativeInfinity
        "IsNormal",															// NI_Vector256_IsNormal
        "IsOddInteger",														// NI_Vector256_IsOddInteger
        "IsPositive",														// NI_Vector256_IsPositive
        "IsPositiveInfinity",												// NI_Vector256_IsPositiveInfinity
        "IsSubnormal",														// NI_Vector256_IsSubnormal
        "IsZero",															// NI_Vector256_IsZero
        "LessThan",															// NI_Vector256_LessThan
        "LessThanAll",														// NI_Vector256_LessThanAll
        "LessThanAny",														// NI_Vector256_LessThanAny
        "LessThanOrEqual",													// NI_Vector256_LessThanOrEqual
        "LessThanOrEqualAll",												// NI_Vector256_LessThanOrEqualAll
        "LessThanOrEqualAny",												// NI_Vector256_LessThanOrEqualAny
        "LoadAligned",														// NI_Vector256_LoadAligned
        "LoadAlignedNonTemporal",											// NI_Vector256_LoadAlignedNonTemporal
        "LoadUnsafe",														// NI_Vector256_LoadUnsafe
        "Max",																// NI_Vector256_Max
        "MaxMagnitude",														// NI_Vector256_MaxMagnitude
        "MaxMagnitudeNumber",												// NI_Vector256_MaxMagnitudeNumber
        "MaxNative",														// NI_Vector256_MaxNative
        "MaxNumber",														// NI_Vector256_MaxNumber
        "Min",																// NI_Vector256_Min
        "MinMagnitude",														// NI_Vector256_MinMagnitude
        "MinMagnitudeNumber",												// NI_Vector256_MinMagnitudeNumber
        "MinNative",														// NI_Vector256_MinNative
        "MinNumber",														// NI_Vector256_MinNumber
        "MultiplyAddEstimate",												// NI_Vector256_MultiplyAddEstimate
        "Narrow",															// NI_Vector256_Narrow
        "NarrowWithSaturation",												// NI_Vector256_NarrowWithSaturation
        "Round",															// NI_Vector256_Round
        "ShiftLeft",														// NI_Vector256_ShiftLeft
        "Shuffle",															// NI_Vector256_Shuffle
        "ShuffleNative",													// NI_Vector256_ShuffleNative
        "ShuffleNativeFallback",											// NI_Vector256_ShuffleNativeFallback
        "Sqrt",																// NI_Vector256_Sqrt
        "StoreAligned",														// NI_Vector256_StoreAligned
        "StoreAlignedNonTemporal",											// NI_Vector256_StoreAlignedNonTemporal
        "StoreUnsafe",														// NI_Vector256_StoreUnsafe
        "SubtractSaturate",													// NI_Vector256_SubtractSaturate
        "Sum",																// NI_Vector256_Sum
        "ToScalar",															// NI_Vector256_ToScalar
        "ToVector512",														// NI_Vector256_ToVector512
        "ToVector512Unsafe",												// NI_Vector256_ToVector512Unsafe
        "Truncate",															// NI_Vector256_Truncate
        "WidenLower",														// NI_Vector256_WidenLower
        "WidenUpper",														// NI_Vector256_WidenUpper
        "WithElement",														// NI_Vector256_WithElement
        "WithLower",														// NI_Vector256_WithLower
        "WithUpper",														// NI_Vector256_WithUpper
        "get_AllBitsSet",													// NI_Vector256_get_AllBitsSet
        "get_E",															// NI_Vector256_get_E
        "get_Epsilon",														// NI_Vector256_get_Epsilon
        "get_Indices",														// NI_Vector256_get_Indices
        "get_NaN",															// NI_Vector256_get_NaN
        "get_NegativeInfinity",												// NI_Vector256_get_NegativeInfinity
        "get_NegativeOne",													// NI_Vector256_get_NegativeOne
        "get_NegativeZero",													// NI_Vector256_get_NegativeZero
        "get_One",															// NI_Vector256_get_One
        "get_Pi",															// NI_Vector256_get_Pi
        "get_PositiveInfinity",												// NI_Vector256_get_PositiveInfinity
        "get_Tau",															// NI_Vector256_get_Tau
        "get_Zero",															// NI_Vector256_get_Zero
        "op_Addition",														// NI_Vector256_op_Addition
        "op_BitwiseAnd",													// NI_Vector256_op_BitwiseAnd
        "op_BitwiseOr",														// NI_Vector256_op_BitwiseOr
        "op_Division",														// NI_Vector256_op_Division
        "op_Equality",														// NI_Vector256_op_Equality
        "op_ExclusiveOr",													// NI_Vector256_op_ExclusiveOr
        "op_Inequality",													// NI_Vector256_op_Inequality
        "op_LeftShift",														// NI_Vector256_op_LeftShift
        "op_Multiply",														// NI_Vector256_op_Multiply
        "op_OnesComplement",												// NI_Vector256_op_OnesComplement
        "op_RightShift",													// NI_Vector256_op_RightShift
        "op_Subtraction",													// NI_Vector256_op_Subtraction
        "op_UnaryNegation",													// NI_Vector256_op_UnaryNegation
        "op_UnaryPlus",														// NI_Vector256_op_UnaryPlus
        "op_UnsignedRightShift",											// NI_Vector256_op_UnsignedRightShift
        "Abs",																// NI_Vector512_Abs
        "AddSaturate",														// NI_Vector512_AddSaturate
        "AndNot",															// NI_Vector512_AndNot
        "As",																// NI_Vector512_As
        "AsByte",															// NI_Vector512_AsByte
        "AsDouble",															// NI_Vector512_AsDouble
        "AsInt16",															// NI_Vector512_AsInt16
        "AsInt32",															// NI_Vector512_AsInt32
        "AsInt64",															// NI_Vector512_AsInt64
        "AsNInt",															// NI_Vector512_AsNInt
        "AsNUInt",															// NI_Vector512_AsNUInt
        "AsSByte",															// NI_Vector512_AsSByte
        "AsSingle",															// NI_Vector512_AsSingle
        "AsUInt16",															// NI_Vector512_AsUInt16
        "AsUInt32",															// NI_Vector512_AsUInt32
        "AsUInt64",															// NI_Vector512_AsUInt64
        "AsVector",															// NI_Vector512_AsVector
        "AsVector512",														// NI_Vector512_AsVector512
        "Ceiling",															// NI_Vector512_Ceiling
        "ConditionalSelect",												// NI_Vector512_ConditionalSelect
        "ConvertToDouble",													// NI_Vector512_ConvertToDouble
        "ConvertToInt32",													// NI_Vector512_ConvertToInt32
        "ConvertToInt32Native",												// NI_Vector512_ConvertToInt32Native
        "ConvertToInt64",													// NI_Vector512_ConvertToInt64
        "ConvertToInt64Native",												// NI_Vector512_ConvertToInt64Native
        "ConvertToSingle",													// NI_Vector512_ConvertToSingle
        "ConvertToUInt32",													// NI_Vector512_ConvertToUInt32
        "ConvertToUInt32Native",											// NI_Vector512_ConvertToUInt32Native
        "ConvertToUInt64",													// NI_Vector512_ConvertToUInt64
        "ConvertToUInt64Native",											// NI_Vector512_ConvertToUInt64Native
        "Create",															// NI_Vector512_Create
        "CreateScalar",														// NI_Vector512_CreateScalar
        "CreateScalarUnsafe",												// NI_Vector512_CreateScalarUnsafe
        "CreateSequence",													// NI_Vector512_CreateSequence
        "Dot",																// NI_Vector512_Dot
        "Equals",															// NI_Vector512_Equals
        "EqualsAny",														// NI_Vector512_EqualsAny
        "ExtractMostSignificantBits",										// NI_Vector512_ExtractMostSignificantBits
        "Floor",															// NI_Vector512_Floor
        "FusedMultiplyAdd",													// NI_Vector512_FusedMultiplyAdd
        "GetElement",														// NI_Vector512_GetElement
        "GetLower",															// NI_Vector512_GetLower
        "GetLower128",														// NI_Vector512_GetLower128
        "GetUpper",															// NI_Vector512_GetUpper
        "GreaterThan",														// NI_Vector512_GreaterThan
        "GreaterThanAll",													// NI_Vector512_GreaterThanAll
        "GreaterThanAny",													// NI_Vector512_GreaterThanAny
        "GreaterThanOrEqual",												// NI_Vector512_GreaterThanOrEqual
        "GreaterThanOrEqualAll",											// NI_Vector512_GreaterThanOrEqualAll
        "GreaterThanOrEqualAny",											// NI_Vector512_GreaterThanOrEqualAny
        "IsEvenInteger",													// NI_Vector512_IsEvenInteger
        "IsFinite",															// NI_Vector512_IsFinite
        "IsInfinity",														// NI_Vector512_IsInfinity
        "IsInteger",														// NI_Vector512_IsInteger
        "IsNaN",															// NI_Vector512_IsNaN
        "IsNegative",														// NI_Vector512_IsNegative
        "IsNegativeInfinity",												// NI_Vector512_IsNegativeInfinity
        "IsNormal",															// NI_Vector512_IsNormal
        "IsOddInteger",														// NI_Vector512_IsOddInteger
        "IsPositive",														// NI_Vector512_IsPositive
        "IsPositiveInfinity",												// NI_Vector512_IsPositiveInfinity
        "IsSubnormal",														// NI_Vector512_IsSubnormal
        "IsZero",															// NI_Vector512_IsZero
        "LessThan",															// NI_Vector512_LessThan
        "LessThanAll",														// NI_Vector512_LessThanAll
        "LessThanAny",														// NI_Vector512_LessThanAny
        "LessThanOrEqual",													// NI_Vector512_LessThanOrEqual
        "LessThanOrEqualAll",												// NI_Vector512_LessThanOrEqualAll
        "LessThanOrEqualAny",												// NI_Vector512_LessThanOrEqualAny
        "LoadAligned",														// NI_Vector512_LoadAligned
        "LoadAlignedNonTemporal",											// NI_Vector512_LoadAlignedNonTemporal
        "LoadUnsafe",														// NI_Vector512_LoadUnsafe
        "Max",																// NI_Vector512_Max
        "MaxMagnitude",														// NI_Vector512_MaxMagnitude
        "MaxMagnitudeNumber",												// NI_Vector512_MaxMagnitudeNumber
        "MaxNative",														// NI_Vector512_MaxNative
        "MaxNumber",														// NI_Vector512_MaxNumber
        "Min",																// NI_Vector512_Min
        "MinMagnitude",														// NI_Vector512_MinMagnitude
        "MinMagnitudeNumber",												// NI_Vector512_MinMagnitudeNumber
        "MinNative",														// NI_Vector512_MinNative
        "MinNumber",														// NI_Vector512_MinNumber
        "MultiplyAddEstimate",												// NI_Vector512_MultiplyAddEstimate
        "Narrow",															// NI_Vector512_Narrow
        "NarrowWithSaturation",												// NI_Vector512_NarrowWithSaturation
        "Round",															// NI_Vector512_Round
        "ShiftLeft",														// NI_Vector512_ShiftLeft
        "Shuffle",															// NI_Vector512_Shuffle
        "ShuffleNative",													// NI_Vector512_ShuffleNative
        "ShuffleNativeFallback",											// NI_Vector512_ShuffleNativeFallback
        "Sqrt",																// NI_Vector512_Sqrt
        "StoreAligned",														// NI_Vector512_StoreAligned
        "StoreAlignedNonTemporal",											// NI_Vector512_StoreAlignedNonTemporal
        "StoreUnsafe",														// NI_Vector512_StoreUnsafe
        "SubtractSaturate",													// NI_Vector512_SubtractSaturate
        "Sum",																// NI_Vector512_Sum
        "ToScalar",															// NI_Vector512_ToScalar
        "Truncate",															// NI_Vector512_Truncate
        "WidenLower",														// NI_Vector512_WidenLower
        "WidenUpper",														// NI_Vector512_WidenUpper
        "WithElement",														// NI_Vector512_WithElement
        "WithLower",														// NI_Vector512_WithLower
        "WithUpper",														// NI_Vector512_WithUpper
        "get_AllBitsSet",													// NI_Vector512_get_AllBitsSet
        "get_E",															// NI_Vector512_get_E
        "get_Epsilon",														// NI_Vector512_get_Epsilon
        "get_Indices",														// NI_Vector512_get_Indices
        "get_NaN",															// NI_Vector512_get_NaN
        "get_NegativeInfinity",												// NI_Vector512_get_NegativeInfinity
        "get_NegativeOne",													// NI_Vector512_get_NegativeOne
        "get_NegativeZero",													// NI_Vector512_get_NegativeZero
        "get_One",															// NI_Vector512_get_One
        "get_Pi",															// NI_Vector512_get_Pi
        "get_PositiveInfinity",												// NI_Vector512_get_PositiveInfinity
        "get_Tau",															// NI_Vector512_get_Tau
        "get_Zero",															// NI_Vector512_get_Zero
        "op_Addition",														// NI_Vector512_op_Addition
        "op_BitwiseAnd",													// NI_Vector512_op_BitwiseAnd
        "op_BitwiseOr",														// NI_Vector512_op_BitwiseOr
        "op_Division",														// NI_Vector512_op_Division
        "op_Equality",														// NI_Vector512_op_Equality
        "op_ExclusiveOr",													// NI_Vector512_op_ExclusiveOr
        "op_Inequality",													// NI_Vector512_op_Inequality
        "op_LeftShift",														// NI_Vector512_op_LeftShift
        "op_Multiply",														// NI_Vector512_op_Multiply
        "op_OnesComplement",												// NI_Vector512_op_OnesComplement
        "op_RightShift",													// NI_Vector512_op_RightShift
        "op_Subtraction",													// NI_Vector512_op_Subtraction
        "op_UnaryNegation",													// NI_Vector512_op_UnaryNegation
        "op_UnaryPlus",														// NI_Vector512_op_UnaryPlus
        "op_UnsignedRightShift",											// NI_Vector512_op_UnsignedRightShift
        "Abs",																// NI_X86Base_Abs
        "Add",																// NI_X86Base_Add
        "AddSaturate",														// NI_X86Base_AddSaturate
        "AddScalar",														// NI_X86Base_AddScalar
        "AddSubtract",														// NI_X86Base_AddSubtract
        "AlignRight",														// NI_X86Base_AlignRight
        "And",																// NI_X86Base_And
        "AndNot",															// NI_X86Base_AndNot
        "Average",															// NI_X86Base_Average
        "BitScanForward",													// NI_X86Base_BitScanForward
        "BitScanReverse",													// NI_X86Base_BitScanReverse
        "Blend",															// NI_X86Base_Blend
        "BlendVariable",													// NI_X86Base_BlendVariable
        "Ceiling",															// NI_X86Base_Ceiling
        "CeilingScalar",													// NI_X86Base_CeilingScalar
        "CompareEqual",														// NI_X86Base_CompareEqual
        "CompareGreaterThan",												// NI_X86Base_CompareGreaterThan
        "CompareGreaterThanOrEqual",										// NI_X86Base_CompareGreaterThanOrEqual
        "CompareLessThan",													// NI_X86Base_CompareLessThan
        "CompareLessThanOrEqual",											// NI_X86Base_CompareLessThanOrEqual
        "CompareNotEqual",													// NI_X86Base_CompareNotEqual
        "CompareNotGreaterThan",											// NI_X86Base_CompareNotGreaterThan
        "CompareNotGreaterThanOrEqual",										// NI_X86Base_CompareNotGreaterThanOrEqual
        "CompareNotLessThan",												// NI_X86Base_CompareNotLessThan
        "CompareNotLessThanOrEqual",										// NI_X86Base_CompareNotLessThanOrEqual
        "CompareOrdered",													// NI_X86Base_CompareOrdered
        "CompareScalarEqual",												// NI_X86Base_CompareScalarEqual
        "CompareScalarGreaterThan",											// NI_X86Base_CompareScalarGreaterThan
        "CompareScalarGreaterThanOrEqual",									// NI_X86Base_CompareScalarGreaterThanOrEqual
        "CompareScalarLessThan",											// NI_X86Base_CompareScalarLessThan
        "CompareScalarLessThanOrEqual",										// NI_X86Base_CompareScalarLessThanOrEqual
        "CompareScalarNotEqual",											// NI_X86Base_CompareScalarNotEqual
        "CompareScalarNotGreaterThan",										// NI_X86Base_CompareScalarNotGreaterThan
        "CompareScalarNotGreaterThanOrEqual",								// NI_X86Base_CompareScalarNotGreaterThanOrEqual
        "CompareScalarNotLessThan",											// NI_X86Base_CompareScalarNotLessThan
        "CompareScalarNotLessThanOrEqual",									// NI_X86Base_CompareScalarNotLessThanOrEqual
        "CompareScalarOrdered",												// NI_X86Base_CompareScalarOrdered
        "CompareScalarOrderedEqual",										// NI_X86Base_CompareScalarOrderedEqual
        "CompareScalarOrderedGreaterThan",									// NI_X86Base_CompareScalarOrderedGreaterThan
        "CompareScalarOrderedGreaterThanOrEqual",							// NI_X86Base_CompareScalarOrderedGreaterThanOrEqual
        "CompareScalarOrderedLessThan",										// NI_X86Base_CompareScalarOrderedLessThan
        "CompareScalarOrderedLessThanOrEqual",								// NI_X86Base_CompareScalarOrderedLessThanOrEqual
        "CompareScalarOrderedNotEqual",										// NI_X86Base_CompareScalarOrderedNotEqual
        "CompareScalarUnordered",											// NI_X86Base_CompareScalarUnordered
        "CompareScalarUnorderedEqual",										// NI_X86Base_CompareScalarUnorderedEqual
        "CompareScalarUnorderedGreaterThan",								// NI_X86Base_CompareScalarUnorderedGreaterThan
        "CompareScalarUnorderedGreaterThanOrEqual",							// NI_X86Base_CompareScalarUnorderedGreaterThanOrEqual
        "CompareScalarUnorderedLessThan",									// NI_X86Base_CompareScalarUnorderedLessThan
        "CompareScalarUnorderedLessThanOrEqual",							// NI_X86Base_CompareScalarUnorderedLessThanOrEqual
        "CompareScalarUnorderedNotEqual",									// NI_X86Base_CompareScalarUnorderedNotEqual
        "CompareUnordered",													// NI_X86Base_CompareUnordered
        "ConvertScalarToVector128Double",									// NI_X86Base_ConvertScalarToVector128Double
        "ConvertScalarToVector128Int32",									// NI_X86Base_ConvertScalarToVector128Int32
        "ConvertScalarToVector128Single",									// NI_X86Base_ConvertScalarToVector128Single
        "ConvertScalarToVector128UInt32",									// NI_X86Base_ConvertScalarToVector128UInt32
        "ConvertToInt32",													// NI_X86Base_ConvertToInt32
        "ConvertToInt32WithTruncation",										// NI_X86Base_ConvertToInt32WithTruncation
        "ConvertToUInt32",													// NI_X86Base_ConvertToUInt32
        "ConvertToVector128Double",											// NI_X86Base_ConvertToVector128Double
        "ConvertToVector128Int16",											// NI_X86Base_ConvertToVector128Int16
        "ConvertToVector128Int32",											// NI_X86Base_ConvertToVector128Int32
        "ConvertToVector128Int32WithTruncation",							// NI_X86Base_ConvertToVector128Int32WithTruncation
        "ConvertToVector128Int64",											// NI_X86Base_ConvertToVector128Int64
        "ConvertToVector128Single",											// NI_X86Base_ConvertToVector128Single
        "Crc32",															// NI_X86Base_Crc32
        "DivRem",															// NI_X86Base_DivRem
        "Divide",															// NI_X86Base_Divide
        "DivideScalar",														// NI_X86Base_DivideScalar
        "DotProduct",														// NI_X86Base_DotProduct
        "Extract",															// NI_X86Base_Extract
        "Floor",															// NI_X86Base_Floor
        "FloorScalar",														// NI_X86Base_FloorScalar
        "HorizontalAdd",													// NI_X86Base_HorizontalAdd
        "HorizontalAddSaturate",											// NI_X86Base_HorizontalAddSaturate
        "HorizontalSubtract",												// NI_X86Base_HorizontalSubtract
        "HorizontalSubtractSaturate",										// NI_X86Base_HorizontalSubtractSaturate
        "Insert",															// NI_X86Base_Insert
        "LoadAlignedVector128",												// NI_X86Base_LoadAlignedVector128
        "LoadAlignedVector128NonTemporal",									// NI_X86Base_LoadAlignedVector128NonTemporal
        "LoadAndDuplicateToVector128",										// NI_X86Base_LoadAndDuplicateToVector128
        "LoadDquVector128",													// NI_X86Base_LoadDquVector128
        "LoadFence",														// NI_X86Base_LoadFence
        "LoadHigh",															// NI_X86Base_LoadHigh
        "LoadLow",															// NI_X86Base_LoadLow
        "LoadScalarVector128",												// NI_X86Base_LoadScalarVector128
        "LoadVector128",													// NI_X86Base_LoadVector128
        "MaskMove",															// NI_X86Base_MaskMove
        "Max",																// NI_X86Base_Max
        "MaxScalar",														// NI_X86Base_MaxScalar
        "MemoryFence",														// NI_X86Base_MemoryFence
        "Min",																// NI_X86Base_Min
        "MinHorizontal",													// NI_X86Base_MinHorizontal
        "MinScalar",														// NI_X86Base_MinScalar
        "MoveAndDuplicate",													// NI_X86Base_MoveAndDuplicate
        "MoveHighAndDuplicate",												// NI_X86Base_MoveHighAndDuplicate
        "MoveHighToLow",													// NI_X86Base_MoveHighToLow
        "MoveLowAndDuplicate",												// NI_X86Base_MoveLowAndDuplicate
        "MoveLowToHigh",													// NI_X86Base_MoveLowToHigh
        "MoveMask",															// NI_X86Base_MoveMask
        "MoveScalar",														// NI_X86Base_MoveScalar
        "MultipleSumAbsoluteDifferences",									// NI_X86Base_MultipleSumAbsoluteDifferences
        "Multiply",															// NI_X86Base_Multiply
        "MultiplyAddAdjacent",												// NI_X86Base_MultiplyAddAdjacent
        "MultiplyHigh",														// NI_X86Base_MultiplyHigh
        "MultiplyHighRoundScale",											// NI_X86Base_MultiplyHighRoundScale
        "MultiplyLow",														// NI_X86Base_MultiplyLow
        "MultiplyScalar",													// NI_X86Base_MultiplyScalar
        "Or",																// NI_X86Base_Or
        "PackSignedSaturate",												// NI_X86Base_PackSignedSaturate
        "PackUnsignedSaturate",												// NI_X86Base_PackUnsignedSaturate
        "Pause",															// NI_X86Base_Pause
        "PopCount",															// NI_X86Base_PopCount
        "Prefetch0",														// NI_X86Base_Prefetch0
        "Prefetch1",														// NI_X86Base_Prefetch1
        "Prefetch2",														// NI_X86Base_Prefetch2
        "PrefetchNonTemporal",												// NI_X86Base_PrefetchNonTemporal
        "Reciprocal",														// NI_X86Base_Reciprocal
        "ReciprocalScalar",													// NI_X86Base_ReciprocalScalar
        "ReciprocalSqrt",													// NI_X86Base_ReciprocalSqrt
        "ReciprocalSqrtScalar",												// NI_X86Base_ReciprocalSqrtScalar
        "RoundCurrentDirection",											// NI_X86Base_RoundCurrentDirection
        "RoundCurrentDirectionScalar",										// NI_X86Base_RoundCurrentDirectionScalar
        "RoundToNearestInteger",											// NI_X86Base_RoundToNearestInteger
        "RoundToNearestIntegerScalar",										// NI_X86Base_RoundToNearestIntegerScalar
        "RoundToNegativeInfinity",											// NI_X86Base_RoundToNegativeInfinity
        "RoundToNegativeInfinityScalar",									// NI_X86Base_RoundToNegativeInfinityScalar
        "RoundToPositiveInfinity",											// NI_X86Base_RoundToPositiveInfinity
        "RoundToPositiveInfinityScalar",									// NI_X86Base_RoundToPositiveInfinityScalar
        "RoundToZero",														// NI_X86Base_RoundToZero
        "RoundToZeroScalar",												// NI_X86Base_RoundToZeroScalar
        "ShiftLeftLogical",													// NI_X86Base_ShiftLeftLogical
        "ShiftLeftLogical128BitLane",										// NI_X86Base_ShiftLeftLogical128BitLane
        "ShiftRightArithmetic",												// NI_X86Base_ShiftRightArithmetic
        "ShiftRightLogical",												// NI_X86Base_ShiftRightLogical
        "ShiftRightLogical128BitLane",										// NI_X86Base_ShiftRightLogical128BitLane
        "Shuffle",															// NI_X86Base_Shuffle
        "ShuffleHigh",														// NI_X86Base_ShuffleHigh
        "ShuffleLow",														// NI_X86Base_ShuffleLow
        "Sign",																// NI_X86Base_Sign
        "Sqrt",																// NI_X86Base_Sqrt
        "SqrtScalar",														// NI_X86Base_SqrtScalar
        "Store",															// NI_X86Base_Store
        "StoreAligned",														// NI_X86Base_StoreAligned
        "StoreAlignedNonTemporal",											// NI_X86Base_StoreAlignedNonTemporal
        "StoreFence",														// NI_X86Base_StoreFence
        "StoreHigh",														// NI_X86Base_StoreHigh
        "StoreLow",															// NI_X86Base_StoreLow
        "StoreNonTemporal",													// NI_X86Base_StoreNonTemporal
        "StoreScalar",														// NI_X86Base_StoreScalar
        "Subtract",															// NI_X86Base_Subtract
        "SubtractSaturate",													// NI_X86Base_SubtractSaturate
        "SubtractScalar",													// NI_X86Base_SubtractScalar
        "SumAbsoluteDifferences",											// NI_X86Base_SumAbsoluteDifferences
        "TestC",															// NI_X86Base_TestC
        "TestNotZAndNotC",													// NI_X86Base_TestNotZAndNotC
        "TestZ",															// NI_X86Base_TestZ
        "UnpackHigh",														// NI_X86Base_UnpackHigh
        "UnpackLow",														// NI_X86Base_UnpackLow
        "Xor",																// NI_X86Base_Xor
        "BigMul",															// NI_X86Base_X64_BigMul
        "BitScanForward",													// NI_X86Base_X64_BitScanForward
        "BitScanReverse",													// NI_X86Base_X64_BitScanReverse
        "ConvertScalarToVector128Double",									// NI_X86Base_X64_ConvertScalarToVector128Double
        "ConvertScalarToVector128Int64",									// NI_X86Base_X64_ConvertScalarToVector128Int64
        "ConvertScalarToVector128Single",									// NI_X86Base_X64_ConvertScalarToVector128Single
        "ConvertScalarToVector128UInt64",									// NI_X86Base_X64_ConvertScalarToVector128UInt64
        "ConvertToInt64",													// NI_X86Base_X64_ConvertToInt64
        "ConvertToInt64WithTruncation",										// NI_X86Base_X64_ConvertToInt64WithTruncation
        "ConvertToUInt64",													// NI_X86Base_X64_ConvertToUInt64
        "Crc32",															// NI_X86Base_X64_Crc32
        "DivRem",															// NI_X86Base_X64_DivRem
        "Extract",															// NI_X86Base_X64_Extract
        "Insert",															// NI_X86Base_X64_Insert
        "PopCount",															// NI_X86Base_X64_PopCount
        "StoreNonTemporal",													// NI_X86Base_X64_StoreNonTemporal
        "Add",																// NI_AVX_Add
        "AddSubtract",														// NI_AVX_AddSubtract
        "And",																// NI_AVX_And
        "AndNot",															// NI_AVX_AndNot
        "Blend",															// NI_AVX_Blend
        "BlendVariable",													// NI_AVX_BlendVariable
        "BroadcastScalarToVector128",										// NI_AVX_BroadcastScalarToVector128
        "BroadcastScalarToVector256",										// NI_AVX_BroadcastScalarToVector256
        "BroadcastVector128ToVector256",									// NI_AVX_BroadcastVector128ToVector256
        "Ceiling",															// NI_AVX_Ceiling
        "Compare",															// NI_AVX_Compare
        "CompareEqual",														// NI_AVX_CompareEqual
        "CompareGreaterThan",												// NI_AVX_CompareGreaterThan
        "CompareGreaterThanOrEqual",										// NI_AVX_CompareGreaterThanOrEqual
        "CompareLessThan",													// NI_AVX_CompareLessThan
        "CompareLessThanOrEqual",											// NI_AVX_CompareLessThanOrEqual
        "CompareNotEqual",													// NI_AVX_CompareNotEqual
        "CompareNotGreaterThan",											// NI_AVX_CompareNotGreaterThan
        "CompareNotGreaterThanOrEqual",										// NI_AVX_CompareNotGreaterThanOrEqual
        "CompareNotLessThan",												// NI_AVX_CompareNotLessThan
        "CompareNotLessThanOrEqual",										// NI_AVX_CompareNotLessThanOrEqual
        "CompareOrdered",													// NI_AVX_CompareOrdered
        "CompareScalar",													// NI_AVX_CompareScalar
        "CompareUnordered",													// NI_AVX_CompareUnordered
        "ConvertToVector128Int32",											// NI_AVX_ConvertToVector128Int32
        "ConvertToVector128Int32WithTruncation",							// NI_AVX_ConvertToVector128Int32WithTruncation
        "ConvertToVector128Single",											// NI_AVX_ConvertToVector128Single
        "ConvertToVector256Double",											// NI_AVX_ConvertToVector256Double
        "ConvertToVector256Int32",											// NI_AVX_ConvertToVector256Int32
        "ConvertToVector256Int32WithTruncation",							// NI_AVX_ConvertToVector256Int32WithTruncation
        "ConvertToVector256Single",											// NI_AVX_ConvertToVector256Single
        "Divide",															// NI_AVX_Divide
        "DotProduct",														// NI_AVX_DotProduct
        "DuplicateEvenIndexed",												// NI_AVX_DuplicateEvenIndexed
        "DuplicateOddIndexed",												// NI_AVX_DuplicateOddIndexed
        "ExtractVector128",													// NI_AVX_ExtractVector128
        "Floor",															// NI_AVX_Floor
        "HorizontalAdd",													// NI_AVX_HorizontalAdd
        "HorizontalSubtract",												// NI_AVX_HorizontalSubtract
        "InsertVector128",													// NI_AVX_InsertVector128
        "LoadAlignedVector256",												// NI_AVX_LoadAlignedVector256
        "LoadDquVector256",													// NI_AVX_LoadDquVector256
        "LoadVector256",													// NI_AVX_LoadVector256
        "MaskLoad",															// NI_AVX_MaskLoad
        "MaskStore",														// NI_AVX_MaskStore
        "Max",																// NI_AVX_Max
        "Min",																// NI_AVX_Min
        "MoveMask",															// NI_AVX_MoveMask
        "Multiply",															// NI_AVX_Multiply
        "Or",																// NI_AVX_Or
        "Permute",															// NI_AVX_Permute
        "Permute2x128",														// NI_AVX_Permute2x128
        "PermuteVar",														// NI_AVX_PermuteVar
        "Reciprocal",														// NI_AVX_Reciprocal
        "ReciprocalSqrt",													// NI_AVX_ReciprocalSqrt
        "RoundCurrentDirection",											// NI_AVX_RoundCurrentDirection
        "RoundToNearestInteger",											// NI_AVX_RoundToNearestInteger
        "RoundToNegativeInfinity",											// NI_AVX_RoundToNegativeInfinity
        "RoundToPositiveInfinity",											// NI_AVX_RoundToPositiveInfinity
        "RoundToZero",														// NI_AVX_RoundToZero
        "Shuffle",															// NI_AVX_Shuffle
        "Sqrt",																// NI_AVX_Sqrt
        "Store",															// NI_AVX_Store
        "StoreAligned",														// NI_AVX_StoreAligned
        "StoreAlignedNonTemporal",											// NI_AVX_StoreAlignedNonTemporal
        "Subtract",															// NI_AVX_Subtract
        "TestC",															// NI_AVX_TestC
        "TestNotZAndNotC",													// NI_AVX_TestNotZAndNotC
        "TestZ",															// NI_AVX_TestZ
        "UnpackHigh",														// NI_AVX_UnpackHigh
        "UnpackLow",														// NI_AVX_UnpackLow
        "Xor",																// NI_AVX_Xor
        "Abs",																// NI_AVX2_Abs
        "Add",																// NI_AVX2_Add
        "AddSaturate",														// NI_AVX2_AddSaturate
        "AlignRight",														// NI_AVX2_AlignRight
        "And",																// NI_AVX2_And
        "AndNot",															// NI_AVX2_AndNot
        "Average",															// NI_AVX2_Average
        "BitFieldExtract",													// NI_AVX2_BitFieldExtract
        "Blend",															// NI_AVX2_Blend
        "BlendVariable",													// NI_AVX2_BlendVariable
        "BroadcastScalarToVector128",										// NI_AVX2_BroadcastScalarToVector128
        "BroadcastScalarToVector256",										// NI_AVX2_BroadcastScalarToVector256
        "BroadcastVector128ToVector256",									// NI_AVX2_BroadcastVector128ToVector256
        "CompareEqual",														// NI_AVX2_CompareEqual
        "CompareGreaterThan",												// NI_AVX2_CompareGreaterThan
        "CompareLessThan",													// NI_AVX2_CompareLessThan
        "ConvertToInt32",													// NI_AVX2_ConvertToInt32
        "ConvertToUInt32",													// NI_AVX2_ConvertToUInt32
        "ConvertToVector128Half",											// NI_AVX2_ConvertToVector128Half
        "ConvertToVector128Single",											// NI_AVX2_ConvertToVector128Single
        "ConvertToVector256Half",											// NI_AVX2_ConvertToVector256Half
        "ConvertToVector256Int16",											// NI_AVX2_ConvertToVector256Int16
        "ConvertToVector256Int32",											// NI_AVX2_ConvertToVector256Int32
        "ConvertToVector256Int64",											// NI_AVX2_ConvertToVector256Int64
        "ConvertToVector256Single",											// NI_AVX2_ConvertToVector256Single
        "ExtractLowestSetBit",												// NI_AVX2_ExtractLowestSetBit
        "ExtractVector128",													// NI_AVX2_ExtractVector128
        "GatherMaskVector128",												// NI_AVX2_GatherMaskVector128
        "GatherMaskVector256",												// NI_AVX2_GatherMaskVector256
        "GatherVector128",													// NI_AVX2_GatherVector128
        "GatherVector256",													// NI_AVX2_GatherVector256
        "GetMaskUpToLowestSetBit",											// NI_AVX2_GetMaskUpToLowestSetBit
        "HorizontalAdd",													// NI_AVX2_HorizontalAdd
        "HorizontalAddSaturate",											// NI_AVX2_HorizontalAddSaturate
        "HorizontalSubtract",												// NI_AVX2_HorizontalSubtract
        "HorizontalSubtractSaturate",										// NI_AVX2_HorizontalSubtractSaturate
        "InsertVector128",													// NI_AVX2_InsertVector128
        "LeadingZeroCount",													// NI_AVX2_LeadingZeroCount
        "LoadAlignedVector256NonTemporal",									// NI_AVX2_LoadAlignedVector256NonTemporal
        "MaskLoad",															// NI_AVX2_MaskLoad
        "MaskStore",														// NI_AVX2_MaskStore
        "Max",																// NI_AVX2_Max
        "Min",																// NI_AVX2_Min
        "MoveMask",															// NI_AVX2_MoveMask
        "MultipleSumAbsoluteDifferences",									// NI_AVX2_MultipleSumAbsoluteDifferences
        "Multiply",															// NI_AVX2_Multiply
        "MultiplyAdd",														// NI_AVX2_MultiplyAdd
        "MultiplyAddAdjacent",												// NI_AVX2_MultiplyAddAdjacent
        "MultiplyAddNegated",												// NI_AVX2_MultiplyAddNegated
        "MultiplyAddNegatedScalar",											// NI_AVX2_MultiplyAddNegatedScalar
        "MultiplyAddScalar",												// NI_AVX2_MultiplyAddScalar
        "MultiplyAddSubtract",												// NI_AVX2_MultiplyAddSubtract
        "MultiplyHigh",														// NI_AVX2_MultiplyHigh
        "MultiplyHighRoundScale",											// NI_AVX2_MultiplyHighRoundScale
        "MultiplyLow",														// NI_AVX2_MultiplyLow
        "MultiplyNoFlags",													// NI_AVX2_MultiplyNoFlags
        "MultiplySubtract",													// NI_AVX2_MultiplySubtract
        "MultiplySubtractAdd",												// NI_AVX2_MultiplySubtractAdd
        "MultiplySubtractNegated",											// NI_AVX2_MultiplySubtractNegated
        "MultiplySubtractNegatedScalar",									// NI_AVX2_MultiplySubtractNegatedScalar
        "MultiplySubtractScalar",											// NI_AVX2_MultiplySubtractScalar
        "Or",																// NI_AVX2_Or
        "PackSignedSaturate",												// NI_AVX2_PackSignedSaturate
        "PackUnsignedSaturate",												// NI_AVX2_PackUnsignedSaturate
        "ParallelBitDeposit",												// NI_AVX2_ParallelBitDeposit
        "ParallelBitExtract",												// NI_AVX2_ParallelBitExtract
        "Permute2x128",														// NI_AVX2_Permute2x128
        "Permute4x64",														// NI_AVX2_Permute4x64
        "PermuteVar8x32",													// NI_AVX2_PermuteVar8x32
        "ResetLowestSetBit",												// NI_AVX2_ResetLowestSetBit
        "ShiftLeftLogical",													// NI_AVX2_ShiftLeftLogical
        "ShiftLeftLogical128BitLane",										// NI_AVX2_ShiftLeftLogical128BitLane
        "ShiftLeftLogicalVariable",											// NI_AVX2_ShiftLeftLogicalVariable
        "ShiftRightArithmetic",												// NI_AVX2_ShiftRightArithmetic
        "ShiftRightArithmeticVariable",										// NI_AVX2_ShiftRightArithmeticVariable
        "ShiftRightLogical",												// NI_AVX2_ShiftRightLogical
        "ShiftRightLogical128BitLane",										// NI_AVX2_ShiftRightLogical128BitLane
        "ShiftRightLogicalVariable",										// NI_AVX2_ShiftRightLogicalVariable
        "Shuffle",															// NI_AVX2_Shuffle
        "ShuffleHigh",														// NI_AVX2_ShuffleHigh
        "ShuffleLow",														// NI_AVX2_ShuffleLow
        "Sign",																// NI_AVX2_Sign
        "Subtract",															// NI_AVX2_Subtract
        "SubtractSaturate",													// NI_AVX2_SubtractSaturate
        "SumAbsoluteDifferences",											// NI_AVX2_SumAbsoluteDifferences
        "TrailingZeroCount",												// NI_AVX2_TrailingZeroCount
        "UnpackHigh",														// NI_AVX2_UnpackHigh
        "UnpackLow",														// NI_AVX2_UnpackLow
        "Xor",																// NI_AVX2_Xor
        "ZeroHighBits",														// NI_AVX2_ZeroHighBits
        "AndNot",															// NI_AVX2_X64_AndNot
        "BitFieldExtract",													// NI_AVX2_X64_BitFieldExtract
        "ExtractLowestSetBit",												// NI_AVX2_X64_ExtractLowestSetBit
        "GetMaskUpToLowestSetBit",											// NI_AVX2_X64_GetMaskUpToLowestSetBit
        "LeadingZeroCount",													// NI_AVX2_X64_LeadingZeroCount
        "MultiplyNoFlags",													// NI_AVX2_X64_MultiplyNoFlags
        "ParallelBitDeposit",												// NI_AVX2_X64_ParallelBitDeposit
        "ParallelBitExtract",												// NI_AVX2_X64_ParallelBitExtract
        "ResetLowestSetBit",												// NI_AVX2_X64_ResetLowestSetBit
        "TrailingZeroCount",												// NI_AVX2_X64_TrailingZeroCount
        "ZeroHighBits",														// NI_AVX2_X64_ZeroHighBits
        "Abs",																// NI_AVX512_Abs
        "Add",																// NI_AVX512_Add
        "AddSaturate",														// NI_AVX512_AddSaturate
        "AddScalar",														// NI_AVX512_AddScalar
        "AlignRight",														// NI_AVX512_AlignRight
        "AlignRight32",														// NI_AVX512_AlignRight32
        "AlignRight64",														// NI_AVX512_AlignRight64
        "And",																// NI_AVX512_And
        "AndNot",															// NI_AVX512_AndNot
        "Average",															// NI_AVX512_Average
        "BlendVariable",													// NI_AVX512_BlendVariable
        "BroadcastPairScalarToVector128",									// NI_AVX512_BroadcastPairScalarToVector128
        "BroadcastPairScalarToVector256",									// NI_AVX512_BroadcastPairScalarToVector256
        "BroadcastPairScalarToVector512",									// NI_AVX512_BroadcastPairScalarToVector512
        "BroadcastScalarToVector512",										// NI_AVX512_BroadcastScalarToVector512
        "BroadcastVector128ToVector512",									// NI_AVX512_BroadcastVector128ToVector512
        "BroadcastVector256ToVector512",									// NI_AVX512_BroadcastVector256ToVector512
        "Classify",															// NI_AVX512_Classify
        "ClassifyScalar",													// NI_AVX512_ClassifyScalar
        "Compare",															// NI_AVX512_Compare
        "CompareEqual",														// NI_AVX512_CompareEqual
        "CompareGreaterThan",												// NI_AVX512_CompareGreaterThan
        "CompareGreaterThanOrEqual",										// NI_AVX512_CompareGreaterThanOrEqual
        "CompareLessThan",													// NI_AVX512_CompareLessThan
        "CompareLessThanOrEqual",											// NI_AVX512_CompareLessThanOrEqual
        "CompareNotEqual",													// NI_AVX512_CompareNotEqual
        "CompareNotGreaterThan",											// NI_AVX512_CompareNotGreaterThan
        "CompareNotGreaterThanOrEqual",										// NI_AVX512_CompareNotGreaterThanOrEqual
        "CompareNotLessThan",												// NI_AVX512_CompareNotLessThan
        "CompareNotLessThanOrEqual",										// NI_AVX512_CompareNotLessThanOrEqual
        "CompareOrdered",													// NI_AVX512_CompareOrdered
        "CompareUnordered",													// NI_AVX512_CompareUnordered
        "Compress",															// NI_AVX512_Compress
        "CompressStore",													// NI_AVX512_CompressStore
        "ConvertScalarToVector128Double",									// NI_AVX512_ConvertScalarToVector128Double
        "ConvertScalarToVector128Single",									// NI_AVX512_ConvertScalarToVector128Single
        "ConvertToInt32",													// NI_AVX512_ConvertToInt32
        "ConvertToUInt32",													// NI_AVX512_ConvertToUInt32
        "ConvertToUInt32WithTruncation",									// NI_AVX512_ConvertToUInt32WithTruncation
        "ConvertToVector128Byte",											// NI_AVX512_ConvertToVector128Byte
        "ConvertToVector128ByteWithSaturation",								// NI_AVX512_ConvertToVector128ByteWithSaturation
        "ConvertToVector128Double",											// NI_AVX512_ConvertToVector128Double
        "ConvertToVector128Int16",											// NI_AVX512_ConvertToVector128Int16
        "ConvertToVector128Int16WithSaturation",							// NI_AVX512_ConvertToVector128Int16WithSaturation
        "ConvertToVector128Int32",											// NI_AVX512_ConvertToVector128Int32
        "ConvertToVector128Int32WithSaturation",							// NI_AVX512_ConvertToVector128Int32WithSaturation
        "ConvertToVector128Int64",											// NI_AVX512_ConvertToVector128Int64
        "ConvertToVector128Int64WithTruncation",							// NI_AVX512_ConvertToVector128Int64WithTruncation
        "ConvertToVector128SByte",											// NI_AVX512_ConvertToVector128SByte
        "ConvertToVector128SByteWithSaturation",							// NI_AVX512_ConvertToVector128SByteWithSaturation
        "ConvertToVector128Single",											// NI_AVX512_ConvertToVector128Single
        "ConvertToVector128UInt16",											// NI_AVX512_ConvertToVector128UInt16
        "ConvertToVector128UInt16WithSaturation",							// NI_AVX512_ConvertToVector128UInt16WithSaturation
        "ConvertToVector128UInt32",											// NI_AVX512_ConvertToVector128UInt32
        "ConvertToVector128UInt32WithSaturation",							// NI_AVX512_ConvertToVector128UInt32WithSaturation
        "ConvertToVector128UInt32WithTruncation",							// NI_AVX512_ConvertToVector128UInt32WithTruncation
        "ConvertToVector128UInt64",											// NI_AVX512_ConvertToVector128UInt64
        "ConvertToVector128UInt64WithTruncation",							// NI_AVX512_ConvertToVector128UInt64WithTruncation
        "ConvertToVector256Byte",											// NI_AVX512_ConvertToVector256Byte
        "ConvertToVector256ByteWithSaturation",								// NI_AVX512_ConvertToVector256ByteWithSaturation
        "ConvertToVector256Double",											// NI_AVX512_ConvertToVector256Double
        "ConvertToVector256Int16",											// NI_AVX512_ConvertToVector256Int16
        "ConvertToVector256Int16WithSaturation",							// NI_AVX512_ConvertToVector256Int16WithSaturation
        "ConvertToVector256Int32",											// NI_AVX512_ConvertToVector256Int32
        "ConvertToVector256Int32WithSaturation",							// NI_AVX512_ConvertToVector256Int32WithSaturation
        "ConvertToVector256Int32WithTruncation",							// NI_AVX512_ConvertToVector256Int32WithTruncation
        "ConvertToVector256Int64",											// NI_AVX512_ConvertToVector256Int64
        "ConvertToVector256Int64WithTruncation",							// NI_AVX512_ConvertToVector256Int64WithTruncation
        "ConvertToVector256SByte",											// NI_AVX512_ConvertToVector256SByte
        "ConvertToVector256SByteWithSaturation",							// NI_AVX512_ConvertToVector256SByteWithSaturation
        "ConvertToVector256Single",											// NI_AVX512_ConvertToVector256Single
        "ConvertToVector256UInt16",											// NI_AVX512_ConvertToVector256UInt16
        "ConvertToVector256UInt16WithSaturation",							// NI_AVX512_ConvertToVector256UInt16WithSaturation
        "ConvertToVector256UInt32",											// NI_AVX512_ConvertToVector256UInt32
        "ConvertToVector256UInt32WithSaturation",							// NI_AVX512_ConvertToVector256UInt32WithSaturation
        "ConvertToVector256UInt32WithTruncation",							// NI_AVX512_ConvertToVector256UInt32WithTruncation
        "ConvertToVector256UInt64",											// NI_AVX512_ConvertToVector256UInt64
        "ConvertToVector256UInt64WithTruncation",							// NI_AVX512_ConvertToVector256UInt64WithTruncation
        "ConvertToVector512Double",											// NI_AVX512_ConvertToVector512Double
        "ConvertToVector512Int16",											// NI_AVX512_ConvertToVector512Int16
        "ConvertToVector512Int32",											// NI_AVX512_ConvertToVector512Int32
        "ConvertToVector512Int32WithTruncation",							// NI_AVX512_ConvertToVector512Int32WithTruncation
        "ConvertToVector512Int64",											// NI_AVX512_ConvertToVector512Int64
        "ConvertToVector512Int64WithTruncation",							// NI_AVX512_ConvertToVector512Int64WithTruncation
        "ConvertToVector512Single",											// NI_AVX512_ConvertToVector512Single
        "ConvertToVector512UInt16",											// NI_AVX512_ConvertToVector512UInt16
        "ConvertToVector512UInt32",											// NI_AVX512_ConvertToVector512UInt32
        "ConvertToVector512UInt32WithTruncation",							// NI_AVX512_ConvertToVector512UInt32WithTruncation
        "ConvertToVector512UInt64",											// NI_AVX512_ConvertToVector512UInt64
        "ConvertToVector512UInt64WithTruncation",							// NI_AVX512_ConvertToVector512UInt64WithTruncation
        "DetectConflicts",													// NI_AVX512_DetectConflicts
        "Divide",															// NI_AVX512_Divide
        "DivideScalar",														// NI_AVX512_DivideScalar
        "DuplicateEvenIndexed",												// NI_AVX512_DuplicateEvenIndexed
        "DuplicateOddIndexed",												// NI_AVX512_DuplicateOddIndexed
        "Expand",															// NI_AVX512_Expand
        "ExpandLoad",														// NI_AVX512_ExpandLoad
        "ExtractVector128",													// NI_AVX512_ExtractVector128
        "ExtractVector256",													// NI_AVX512_ExtractVector256
        "Fixup",															// NI_AVX512_Fixup
        "FixupScalar",														// NI_AVX512_FixupScalar
        "FusedMultiplyAdd",													// NI_AVX512_FusedMultiplyAdd
        "FusedMultiplyAddNegated",											// NI_AVX512_FusedMultiplyAddNegated
        "FusedMultiplyAddNegatedScalar",									// NI_AVX512_FusedMultiplyAddNegatedScalar
        "FusedMultiplyAddScalar",											// NI_AVX512_FusedMultiplyAddScalar
        "FusedMultiplyAddSubtract",											// NI_AVX512_FusedMultiplyAddSubtract
        "FusedMultiplySubtract",											// NI_AVX512_FusedMultiplySubtract
        "FusedMultiplySubtractAdd",											// NI_AVX512_FusedMultiplySubtractAdd
        "FusedMultiplySubtractNegated",										// NI_AVX512_FusedMultiplySubtractNegated
        "FusedMultiplySubtractNegatedScalar",								// NI_AVX512_FusedMultiplySubtractNegatedScalar
        "FusedMultiplySubtractScalar",										// NI_AVX512_FusedMultiplySubtractScalar
        "GetExponent",														// NI_AVX512_GetExponent
        "GetExponentScalar",												// NI_AVX512_GetExponentScalar
        "GetMantissa",														// NI_AVX512_GetMantissa
        "GetMantissaScalar",												// NI_AVX512_GetMantissaScalar
        "InsertVector128",													// NI_AVX512_InsertVector128
        "InsertVector256",													// NI_AVX512_InsertVector256
        "LeadingZeroCount",													// NI_AVX512_LeadingZeroCount
        "LoadAlignedVector512",												// NI_AVX512_LoadAlignedVector512
        "LoadAlignedVector512NonTemporal",									// NI_AVX512_LoadAlignedVector512NonTemporal
        "LoadVector512",													// NI_AVX512_LoadVector512
        "MaskLoad",															// NI_AVX512_MaskLoad
        "MaskLoadAligned",													// NI_AVX512_MaskLoadAligned
        "MaskStore",														// NI_AVX512_MaskStore
        "MaskStoreAligned",													// NI_AVX512_MaskStoreAligned
        "Max",																// NI_AVX512_Max
        "Min",																// NI_AVX512_Min
        "MoveMask",															// NI_AVX512_MoveMask
        "Multiply",															// NI_AVX512_Multiply
        "MultiplyAddAdjacent",												// NI_AVX512_MultiplyAddAdjacent
        "MultiplyHigh",														// NI_AVX512_MultiplyHigh
        "MultiplyHighRoundScale",											// NI_AVX512_MultiplyHighRoundScale
        "MultiplyLow",														// NI_AVX512_MultiplyLow
        "MultiplyScalar",													// NI_AVX512_MultiplyScalar
        "Or",																// NI_AVX512_Or
        "PackSignedSaturate",												// NI_AVX512_PackSignedSaturate
        "PackUnsignedSaturate",												// NI_AVX512_PackUnsignedSaturate
        "Permute2x64",														// NI_AVX512_Permute2x64
        "Permute4x32",														// NI_AVX512_Permute4x32
        "Permute4x64",														// NI_AVX512_Permute4x64
        "PermuteVar16x16",													// NI_AVX512_PermuteVar16x16
        "PermuteVar16x16x2",												// NI_AVX512_PermuteVar16x16x2
        "PermuteVar16x32",													// NI_AVX512_PermuteVar16x32
        "PermuteVar16x32x2",												// NI_AVX512_PermuteVar16x32x2
        "PermuteVar2x64",													// NI_AVX512_PermuteVar2x64
        "PermuteVar2x64x2",													// NI_AVX512_PermuteVar2x64x2
        "PermuteVar32x16",													// NI_AVX512_PermuteVar32x16
        "PermuteVar32x16x2",												// NI_AVX512_PermuteVar32x16x2
        "PermuteVar4x32",													// NI_AVX512_PermuteVar4x32
        "PermuteVar4x32x2",													// NI_AVX512_PermuteVar4x32x2
        "PermuteVar4x64",													// NI_AVX512_PermuteVar4x64
        "PermuteVar4x64x2",													// NI_AVX512_PermuteVar4x64x2
        "PermuteVar8x16 ",													// NI_AVX512_PermuteVar8x16
        "PermuteVar8x16x2",													// NI_AVX512_PermuteVar8x16x2
        "PermuteVar8x32x2",													// NI_AVX512_PermuteVar8x32x2
        "PermuteVar8x64",													// NI_AVX512_PermuteVar8x64
        "PermuteVar8x64x2",													// NI_AVX512_PermuteVar8x64x2
        "Range",															// NI_AVX512_Range
        "RangeScalar",														// NI_AVX512_RangeScalar
        "Reciprocal14",														// NI_AVX512_Reciprocal14
        "Reciprocal14Scalar",												// NI_AVX512_Reciprocal14Scalar
        "ReciprocalSqrt14",													// NI_AVX512_ReciprocalSqrt14
        "ReciprocalSqrt14Scalar",											// NI_AVX512_ReciprocalSqrt14Scalar
        "Reduce",															// NI_AVX512_Reduce
        "ReduceScalar",														// NI_AVX512_ReduceScalar
        "RotateLeft",														// NI_AVX512_RotateLeft
        "RotateLeftVariable",												// NI_AVX512_RotateLeftVariable
        "RotateRight",														// NI_AVX512_RotateRight
        "RotateRightVariable",												// NI_AVX512_RotateRightVariable
        "RoundScale",														// NI_AVX512_RoundScale
        "RoundScaleScalar",													// NI_AVX512_RoundScaleScalar
        "Scale",															// NI_AVX512_Scale
        "ScaleScalar",														// NI_AVX512_ScaleScalar
        "ShiftLeftLogical",													// NI_AVX512_ShiftLeftLogical
        "ShiftLeftLogical128BitLane",										// NI_AVX512_ShiftLeftLogical128BitLane
        "ShiftLeftLogicalVariable",											// NI_AVX512_ShiftLeftLogicalVariable
        "ShiftRightArithmetic",												// NI_AVX512_ShiftRightArithmetic
        "ShiftRightArithmeticVariable",										// NI_AVX512_ShiftRightArithmeticVariable
        "ShiftRightLogical",												// NI_AVX512_ShiftRightLogical
        "ShiftRightLogical128BitLane",										// NI_AVX512_ShiftRightLogical128BitLane
        "ShiftRightLogicalVariable",										// NI_AVX512_ShiftRightLogicalVariable
        "Shuffle",															// NI_AVX512_Shuffle
        "Shuffle2x128",														// NI_AVX512_Shuffle2x128
        "Shuffle4x128",														// NI_AVX512_Shuffle4x128
        "ShuffleHigh",														// NI_AVX512_ShuffleHigh
        "ShuffleLow",														// NI_AVX512_ShuffleLow
        "Sqrt",																// NI_AVX512_Sqrt
        "SqrtScalar",														// NI_AVX512_SqrtScalar
        "Store",															// NI_AVX512_Store
        "StoreAligned",														// NI_AVX512_StoreAligned
        "StoreAlignedNonTemporal",											// NI_AVX512_StoreAlignedNonTemporal
        "Subtract",															// NI_AVX512_Subtract
        "SubtractSaturate",													// NI_AVX512_SubtractSaturate
        "SubtractScalar",													// NI_AVX512_SubtractScalar
        "SumAbsoluteDifferences",											// NI_AVX512_SumAbsoluteDifferences
        "SumAbsoluteDifferencesInBlock32",									// NI_AVX512_SumAbsoluteDifferencesInBlock32
        "TernaryLogic",														// NI_AVX512_TernaryLogic
        "UnpackHigh",														// NI_AVX512_UnpackHigh
        "UnpackLow",														// NI_AVX512_UnpackLow
        "Xor",																// NI_AVX512_Xor
        "ConvertScalarToVector128Double",									// NI_AVX512_X64_ConvertScalarToVector128Double
        "ConvertScalarToVector128Single",									// NI_AVX512_X64_ConvertScalarToVector128Single
        "ConvertToInt64",													// NI_AVX512_X64_ConvertToInt64
        "ConvertToUInt64",													// NI_AVX512_X64_ConvertToUInt64
        "ConvertToUInt64WithTruncation",									// NI_AVX512_X64_ConvertToUInt64WithTruncation
        "MultiShift",														// NI_AVX512v2_MultiShift
        "PermuteVar16x8",													// NI_AVX512v2_PermuteVar16x8
        "PermuteVar16x8x2",													// NI_AVX512v2_PermuteVar16x8x2
        "PermuteVar32x8",													// NI_AVX512v2_PermuteVar32x8
        "PermuteVar32x8x2",													// NI_AVX512v2_PermuteVar32x8x2
        "PermuteVar64x8",													// NI_AVX512v2_PermuteVar64x8
        "PermuteVar64x8x2",													// NI_AVX512v2_PermuteVar64x8x2
        "Compress",															// NI_AVX512v3_Compress
        "CompressStore",													// NI_AVX512v3_CompressStore
        "Expand",															// NI_AVX512v3_Expand
        "ExpandLoad",														// NI_AVX512v3_ExpandLoad
        "ConvertToByteWithSaturationAndZeroExtendToInt32",					// NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32
        "ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32",			// NI_AVX10v2_ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32
        "ConvertToInt32WithTruncatedSaturation",							// NI_AVX10v2_ConvertToInt32WithTruncatedSaturation
        "ConvertToSByteWithSaturationAndZeroExtendToInt32",					// NI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32
        "ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32",		// NI_AVX10v2_ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32
        "ConvertToUInt32WithTruncatedSaturation",							// NI_AVX10v2_ConvertToUInt32WithTruncatedSaturation
        "ConvertToVectorInt32WithTruncatedSaturation",						// NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation
        "ConvertToVectorInt64WithTruncatedSaturation",						// NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation
        "ConvertToVectorUInt32WithTruncatedSaturation",						// NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation
        "ConvertToVectorUInt64WithTruncatedSaturation",						// NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation
        "MinMax",															// NI_AVX10v2_MinMax
        "MinMaxScalar",														// NI_AVX10v2_MinMaxScalar
        "MoveScalar",														// NI_AVX10v2_MoveScalar
        "MultipleSumAbsoluteDifferences",									// NI_AVX10v2_MultipleSumAbsoluteDifferences
        "StoreScalar",														// NI_AVX10v2_StoreScalar
        "ConvertToInt64WithTruncatedSaturation",							// NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation
        "ConvertToUInt64WithTruncatedSaturation",							// NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation
        "BitMultiplyMatrix16x16WithOrReduction",							// NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction
        "BitMultiplyMatrix16x16WithXorReduction",							// NI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction
        "ReverseBits",														// NI_AVX512BMM_ReverseBits
        "MultiplyWideningAndAdd",											// NI_AVXVNNI_MultiplyWideningAndAdd
        "MultiplyWideningAndAddSaturate",									// NI_AVXVNNI_MultiplyWideningAndAddSaturate
        "MultiplyWideningAndAdd",											// NI_AVXVNNIINT_MultiplyWideningAndAdd
        "MultiplyWideningAndAddSaturate",									// NI_AVXVNNIINT_MultiplyWideningAndAddSaturate
        "MultiplyWideningAndAdd",											// NI_AVXVNNIINT_V512_MultiplyWideningAndAdd
        "MultiplyWideningAndAddSaturate",									// NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate
        "CarrylessMultiply",												// NI_AES_CarrylessMultiply
        "Decrypt",															// NI_AES_Decrypt
        "DecryptLast",														// NI_AES_DecryptLast
        "Encrypt",															// NI_AES_Encrypt
        "EncryptLast",														// NI_AES_EncryptLast
        "InverseMixColumns",												// NI_AES_InverseMixColumns
        "KeygenAssist",														// NI_AES_KeygenAssist
        "CarrylessMultiply",												// NI_AES_V256_CarrylessMultiply
        "CarrylessMultiply",												// NI_AES_V512_CarrylessMultiply
        "Serialize",														// NI_X86Serialize_Serialize
        "GaloisFieldAffineTransform",										// NI_GFNI_GaloisFieldAffineTransform
        "GaloisFieldAffineTransformInverse",								// NI_GFNI_GaloisFieldAffineTransformInverse
        "GaloisFieldMultiply",												// NI_GFNI_GaloisFieldMultiply
        "GaloisFieldAffineTransform",										// NI_GFNI_V256_GaloisFieldAffineTransform
        "GaloisFieldAffineTransformInverse",								// NI_GFNI_V256_GaloisFieldAffineTransformInverse
        "GaloisFieldMultiply",												// NI_GFNI_V256_GaloisFieldMultiply
        "GaloisFieldAffineTransform",										// NI_GFNI_V512_GaloisFieldAffineTransform
        "GaloisFieldAffineTransformInverse",								// NI_GFNI_V512_GaloisFieldAffineTransformInverse
        "GaloisFieldMultiply",												// NI_GFNI_V512_GaloisFieldMultiply
        "COMIS",															// NI_X86Base_COMIS
        "PTEST",															// NI_X86Base_PTEST
        "UCOMIS",															// NI_X86Base_UCOMIS
        "PTEST",															// NI_AVX_PTEST
        "AndNotVector",														// NI_AVX2_AndNotVector
        "AndNotScalar",														// NI_AVX2_AndNotScalar
        "KORTEST",															// NI_AVX512_KORTEST
        "KTEST",															// NI_AVX512_KTEST
        "PTESTM",															// NI_AVX512_PTESTM
        "PTESTNM",															// NI_AVX512_PTESTNM
        "AddMask",															// NI_AVX512_AddMask
        "AndMask",															// NI_AVX512_AndMask
        "AndNotMask",														// NI_AVX512_AndNotMask
        "BlendVariableMask",												// NI_AVX512_BlendVariableMask
        "ClassifyMask",														// NI_AVX512_ClassifyMask
        "ClassifyScalarMask",												// NI_AVX512_ClassifyScalarMask
        "CompareMask",														// NI_AVX512_CompareMask
        "CompareEqualMask",													// NI_AVX512_CompareEqualMask
        "CompareGreaterThanMask",											// NI_AVX512_CompareGreaterThanMask
        "CompareGreaterThanOrEqualMask",									// NI_AVX512_CompareGreaterThanOrEqualMask
        "CompareLessThanMask",												// NI_AVX512_CompareLessThanMask
        "CompareLessThanOrEqualMask",										// NI_AVX512_CompareLessThanOrEqualMask
        "CompareNotEqualMask",												// NI_AVX512_CompareNotEqualMask
        "CompareNotGreaterThanMask",										// NI_AVX512_CompareNotGreaterThanMask
        "CompareNotGreaterThanOrEqualMask",									// NI_AVX512_CompareNotGreaterThanOrEqualMask
        "CompareNotLessThanMask",											// NI_AVX512_CompareNotLessThanMask
        "CompareNotLessThanOrEqualMask",									// NI_AVX512_CompareNotLessThanOrEqualMask
        "CompareOrderedMask",												// NI_AVX512_CompareOrderedMask
        "CompareScalarMask",												// NI_AVX512_CompareScalarMask
        "CompareUnorderedMask",												// NI_AVX512_CompareUnorderedMask
        "CompressMask",														// NI_AVX512_CompressMask
        "CompressStoreMask",												// NI_AVX512_CompressStoreMask
        "ConvertMaskToVector",												// NI_AVX512_ConvertMaskToVector
        "ConvertVectorToMask",												// NI_AVX512_ConvertVectorToMask
        "ExpandLoadMask",													// NI_AVX512_ExpandLoadMask
        "ExpandMask",														// NI_AVX512_ExpandMask
        "MaskLoadMask",														// NI_AVX512_MaskLoadMask
        "MaskLoadAlignedMask",												// NI_AVX512_MaskLoadAlignedMask
        "MaskStoreMask",													// NI_AVX512_MaskStoreMask
        "MaskStoreAlignedMask",												// NI_AVX512_MaskStoreAlignedMask
        "NotMask",															// NI_AVX512_NotMask
        "OrMask",															// NI_AVX512_OrMask
        "ShiftLeftMask",													// NI_AVX512_ShiftLeftMask
        "ShiftRightMask",													// NI_AVX512_ShiftRightMask
        "XorMask",															// NI_AVX512_XorMask
        "XnorMask",															// NI_AVX512_XnorMask
    ];
#endif
}
#endif
