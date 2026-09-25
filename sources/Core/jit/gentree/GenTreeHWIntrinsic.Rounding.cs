// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class GenTreeHWIntrinsic
{
    public bool IsEmbeddedRoundingEnabled
    {
        get
        {
#if TARGET_XARCH
            var intrinsic = HWIntrinsicId;
            if ((HWIntrinsicInfo.lookupFlags(intrinsic) & HW_Flag_EmbRoundingCompatible) == 0)
            {
                return false;
            }

            var count = Operands.Length;
            return intrinsic switch {
                NI_AVX512_AddScalar or
                NI_AVX512_DivideScalar or
                NI_AVX512_MultiplyScalar or
                NI_AVX512_SubtractScalar or
                NI_AVX512_SqrtScalar => true,

                NI_AVX512_FusedMultiplyAdd or
                NI_AVX512_FusedMultiplyAddScalar or
                NI_AVX512_FusedMultiplyAddNegated or
                NI_AVX512_FusedMultiplyAddNegatedScalar or
                NI_AVX512_FusedMultiplyAddSubtract or
                NI_AVX512_FusedMultiplySubtract or
                NI_AVX512_FusedMultiplySubtractAdd or
                NI_AVX512_FusedMultiplySubtractNegated or
                NI_AVX512_FusedMultiplySubtractNegatedScalar or
                NI_AVX512_FusedMultiplySubtractScalar or
                NI_AVX10v1_FusedMultiplyAddScalar => count == 4,

                NI_AVX512_Add or
                NI_AVX512_Divide or
                NI_AVX512_Multiply or
                NI_AVX512_Subtract or
                NI_AVX512_Scale or
                NI_AVX512_ScaleScalar or
                NI_AVX512_ConvertScalarToVector128Single or
#if TARGET_AMD64
                NI_AVX512_X64_ConvertScalarToVector128Double or
                NI_AVX512_X64_ConvertScalarToVector128Single or
#endif
                NI_AVX10v1_AddScalar or
                NI_AVX10v1_DivideScalar or
                NI_AVX10v1_MultiplyScalar or
                NI_AVX10v1_SubtractScalar or
                NI_AVX10v1_ConvertScalarToVector128Half or
                NI_AVX10v1_ConvertScalarToVector128Single or
                NI_AVX10v1_ConvertScalarToVector128Double => count == 3,

                NI_AVX512_ConvertToInt32 or
                NI_AVX512_ConvertToUInt32 or
                NI_AVX512_ConvertToVector256Int32 or
                NI_AVX512_ConvertToVector256Single or
                NI_AVX512_ConvertToVector256UInt32 or
                NI_AVX512_ConvertToVector512Double or
                NI_AVX512_ConvertToVector512Int32 or
                NI_AVX512_ConvertToVector512Int64 or
                NI_AVX512_ConvertToVector512Single or
                NI_AVX512_ConvertToVector512UInt32 or
                NI_AVX512_ConvertToVector512UInt64 or
                NI_AVX512_Sqrt or
#if TARGET_AMD64
                NI_AVX512_X64_ConvertToInt64 or
                NI_AVX512_X64_ConvertToUInt64 or
#endif
                NI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32 or
                NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32 => count == 2,

                _ => throw new InvalidOperationException("Unexpected embedded-rounding intrinsic."),
            };
#else
            return false;
#endif
        }
    }
}
