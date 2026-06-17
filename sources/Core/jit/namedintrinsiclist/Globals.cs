// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_XARCH
    public const NamedIntrinsic FIRST_NI_Vector128 = NI_Vector128_Abs;
    public const NamedIntrinsic LAST_NI_Vector128 = NI_Vector128_op_UnsignedRightShift;
    public const NamedIntrinsic FIRST_NI_Vector256 = NI_Vector256_Abs;
    public const NamedIntrinsic LAST_NI_Vector256 = NI_Vector256_op_UnsignedRightShift;
    public const NamedIntrinsic FIRST_NI_Vector512 = NI_Vector512_Abs;
    public const NamedIntrinsic LAST_NI_Vector512 = NI_Vector512_op_UnsignedRightShift;
    public const NamedIntrinsic FIRST_NI_X86Base = NI_X86Base_Abs;
    public const NamedIntrinsic LAST_NI_X86Base = NI_X86Base_Xor;
    public const NamedIntrinsic FIRST_NI_X86Base_X64 = NI_X86Base_X64_BigMul;
    public const NamedIntrinsic LAST_NI_X86Base_X64 = NI_X86Base_X64_StoreNonTemporal;
    public const NamedIntrinsic FIRST_NI_AVX = NI_AVX_Add;
    public const NamedIntrinsic LAST_NI_AVX = NI_AVX_Xor;
    public const NamedIntrinsic FIRST_NI_AVX2 = NI_AVX2_Abs;
    public const NamedIntrinsic LAST_NI_AVX2 = NI_AVX2_ZeroHighBits;
    public const NamedIntrinsic FIRST_NI_AVX2_X64 = NI_AVX2_X64_AndNot;
    public const NamedIntrinsic LAST_NI_AVX2_X64 = NI_AVX2_X64_ZeroHighBits;
    public const NamedIntrinsic FIRST_NI_AVX512 = NI_AVX512_Abs;
    public const NamedIntrinsic LAST_NI_AVX512 = NI_AVX512_Xor;
    public const NamedIntrinsic FIRST_NI_AVX512_X64 = NI_AVX512_X64_ConvertScalarToVector128Double;
    public const NamedIntrinsic LAST_NI_AVX512_X64 = NI_AVX512_X64_ConvertToUInt64WithTruncation;
    public const NamedIntrinsic FIRST_NI_AVX512v2 = NI_AVX512v2_MultiShift;
    public const NamedIntrinsic LAST_NI_AVX512v2 = NI_AVX512v2_PermuteVar64x8x2;
    public const NamedIntrinsic FIRST_NI_AVX512v3 = NI_AVX512v3_Compress;
    public const NamedIntrinsic LAST_NI_AVX512v3 = NI_AVX512v3_ExpandLoad;
    public const NamedIntrinsic FIRST_NI_AVX10v2 = NI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32;
    public const NamedIntrinsic LAST_NI_AVX10v2 = NI_AVX10v2_StoreScalar;
    public const NamedIntrinsic FIRST_NI_AVX10v2_X64 = NI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation;
    public const NamedIntrinsic LAST_NI_AVX10v2_X64 = NI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation;
    public const NamedIntrinsic FIRST_NI_AVX512BMM = NI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction;
    public const NamedIntrinsic LAST_NI_AVX512BMM = NI_AVX512BMM_ReverseBits;
    public const NamedIntrinsic FIRST_NI_AVXVNNI = NI_AVXVNNI_MultiplyWideningAndAdd;
    public const NamedIntrinsic LAST_NI_AVXVNNI = NI_AVXVNNI_MultiplyWideningAndAddSaturate;
    public const NamedIntrinsic FIRST_NI_AVXVNNIINT = NI_AVXVNNIINT_MultiplyWideningAndAdd;
    public const NamedIntrinsic LAST_NI_AVXVNNIINT = NI_AVXVNNIINT_MultiplyWideningAndAddSaturate;
    public const NamedIntrinsic FIRST_NI_AVXVNNIINT_V512 = NI_AVXVNNIINT_V512_MultiplyWideningAndAdd;
    public const NamedIntrinsic LAST_NI_AVXVNNIINT_V512 = NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate;
    public const NamedIntrinsic FIRST_NI_AES = NI_AES_CarrylessMultiply;
    public const NamedIntrinsic LAST_NI_AES = NI_AES_KeygenAssist;
    public const NamedIntrinsic FIRST_NI_AES_V256 = NI_AES_V256_CarrylessMultiply;
    public const NamedIntrinsic LAST_NI_AES_V256 = NI_AES_V256_CarrylessMultiply;
    public const NamedIntrinsic FIRST_NI_AES_V512 = NI_AES_V512_CarrylessMultiply;
    public const NamedIntrinsic LAST_NI_AES_V512 = NI_AES_V512_CarrylessMultiply;
    public const NamedIntrinsic FIRST_NI_X86Serialize = NI_X86Serialize_Serialize;
    public const NamedIntrinsic LAST_NI_X86Serialize = NI_X86Serialize_Serialize;
    public const NamedIntrinsic FIRST_NI_GFNI = NI_GFNI_GaloisFieldAffineTransform;
    public const NamedIntrinsic LAST_NI_GFNI = NI_GFNI_GaloisFieldMultiply;
    public const NamedIntrinsic FIRST_NI_GFNI_V256 = NI_GFNI_V256_GaloisFieldAffineTransform;
    public const NamedIntrinsic LAST_NI_GFNI_V256 = NI_GFNI_V256_GaloisFieldMultiply;
    public const NamedIntrinsic FIRST_NI_GFNI_V512 = NI_GFNI_V512_GaloisFieldAffineTransform;
    public const NamedIntrinsic LAST_NI_GFNI_V512 = NI_GFNI_V512_GaloisFieldMultiply;
#elif TARGET_ARM64
    public const NamedIntrinsic FIRST_NI_Vector64 = NI_Vector64_Abs;
    public const NamedIntrinsic LAST_NI_Vector64 = NI_Vector64_op_UnsignedRightShift;
    public const NamedIntrinsic FIRST_NI_Vector128 = NI_Vector128_Abs;
    public const NamedIntrinsic LAST_NI_Vector128 = NI_Vector128_op_UnsignedRightShift;
    public const NamedIntrinsic FIRST_NI_AdvSimd = NI_AdvSimd_Abs;
    public const NamedIntrinsic LAST_NI_AdvSimd = NI_AdvSimd_ZeroExtendWideningUpper;
    public const NamedIntrinsic FIRST_NI_AdvSimd_Arm64 = NI_AdvSimd_Arm64_Abs;
    public const NamedIntrinsic LAST_NI_AdvSimd_Arm64 = NI_AdvSimd_Arm64_ZipLow;
    public const NamedIntrinsic FIRST_NI_Aes = NI_Aes_Decrypt;
    public const NamedIntrinsic LAST_NI_Aes = NI_Aes_PolynomialMultiplyWideningUpper;
    public const NamedIntrinsic FIRST_NI_ArmBase = NI_ArmBase_LeadingZeroCount;
    public const NamedIntrinsic LAST_NI_ArmBase = NI_ArmBase_Yield;
    public const NamedIntrinsic FIRST_NI_ArmBase_Arm64 = NI_ArmBase_Arm64_LeadingSignCount;
    public const NamedIntrinsic LAST_NI_ArmBase_Arm64 = NI_ArmBase_Arm64_ReverseElementBits;
    public const NamedIntrinsic FIRST_NI_Crc32 = NI_Crc32_ComputeCrc32;
    public const NamedIntrinsic LAST_NI_Crc32 = NI_Crc32_ComputeCrc32C;
    public const NamedIntrinsic FIRST_NI_Crc32_Arm64 = NI_Crc32_Arm64_ComputeCrc32;
    public const NamedIntrinsic LAST_NI_Crc32_Arm64 = NI_Crc32_Arm64_ComputeCrc32C;
    public const NamedIntrinsic FIRST_NI_Dp = NI_Dp_DotProduct;
    public const NamedIntrinsic LAST_NI_Dp = NI_Dp_DotProductBySelectedQuadruplet;
    public const NamedIntrinsic FIRST_NI_Rdm = NI_Rdm_MultiplyRoundedDoublingAndAddSaturateHigh;
    public const NamedIntrinsic LAST_NI_Rdm = NI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndSubtractSaturateHigh;
    public const NamedIntrinsic FIRST_NI_Rdm_Arm64 = NI_Rdm_Arm64_MultiplyRoundedDoublingAndAddSaturateHighScalar;
    public const NamedIntrinsic LAST_NI_Rdm_Arm64 = NI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndSubtractSaturateHigh;
    public const NamedIntrinsic FIRST_NI_Sha1 = NI_Sha1_FixedRotate;
    public const NamedIntrinsic LAST_NI_Sha1 = NI_Sha1_ScheduleUpdate1;
    public const NamedIntrinsic FIRST_NI_Sha256 = NI_Sha256_HashUpdate1;
    public const NamedIntrinsic LAST_NI_Sha256 = NI_Sha256_ScheduleUpdate1;
    
    public const NamedIntrinsic FIRST_NI_Sve = NI_Sve_Abs;
    public const NamedIntrinsic LAST_NI_Sve = NI_Sve_ZipLow;
    public const NamedIntrinsic FIRST_NI_Sve2 = NI_Sve2_AbsSaturate;
    public const NamedIntrinsic LAST_NI_Sve2 = NI_Sve2_XorRotateRight;
    public const NamedIntrinsic SPECIAL_NI_Sve = NI_Sve_ConditionalExtractAfterLastActiveElementScalar;
    public const NamedIntrinsic FIRST_NI_VectorT = NI_Illegal;
    public const NamedIntrinsic LAST_NI_VectorT = NI_Illegal;
#endif
}
