// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_InstructionSet;

namespace RyuJitSharp;

public enum CORINFO_InstructionSet
{
    InstructionSet_ILLEGAL = 0,
    InstructionSet_NONE = 127,

    //
    // TARGET_ARMARCH
    //

    InstructionSet_ARMARCH_ArmBase = 1,
    InstructionSet_ARMARCH_AdvSimd = 2,
    InstructionSet_ARMARCH_Aes = 3,
    InstructionSet_ARMARCH_Crc32 = 4,
    InstructionSet_ARMARCH_Dp = 5,
    InstructionSet_ARMARCH_Rdm = 6,
    InstructionSet_ARMARCH_Sha1 = 7,
    InstructionSet_ARMARCH_Sha256 = 8,
    InstructionSet_ARMARCH_Atomics = 9,
    InstructionSet_ARMARCH_Vector64 = 10,
    InstructionSet_ARMARCH_Vector128 = 11,
    InstructionSet_ARMARCH_Dczva = 12,
    InstructionSet_ARMARCH_Rcpc = 13,
    InstructionSet_ARMARCH_VectorT128 = 14,
    InstructionSet_ARMARCH_Rcpc2 = 15,
    InstructionSet_ARMARCH_Sve = 16,
    InstructionSet_ARMARCH_ArmBase_Arm64 = 17,
    InstructionSet_ARMARCH_AdvSimd_Arm64 = 18,
    InstructionSet_ARMARCH_Aes_Arm64 = 19,
    InstructionSet_ARMARCH_Crc32_Arm64 = 20,
    InstructionSet_ARMARCH_Dp_Arm64 = 21,
    InstructionSet_ARMARCH_Rdm_Arm64 = 22,
    InstructionSet_ARMARCH_Sha1_Arm64 = 23,
    InstructionSet_ARMARCH_Sha256_Arm64 = 24,
    InstructionSet_ARMARCH_Sve_Arm64 = 25,

    //
    // TARGET_XARCH
    //

    InstructionSet_XARCH_X86Base = 1,
    InstructionSet_XARCH_SSE = 2,
    InstructionSet_XARCH_SSE2 = 3,
    InstructionSet_XARCH_SSE3 = 4,
    InstructionSet_XARCH_SSSE3 = 5,
    InstructionSet_XARCH_SSE41 = 6,
    InstructionSet_XARCH_SSE42 = 7,
    InstructionSet_XARCH_AVX = 8,
    InstructionSet_XARCH_AVX2 = 9,
    InstructionSet_XARCH_AES = 10,
    InstructionSet_XARCH_BMI1 = 11,
    InstructionSet_XARCH_BMI2 = 12,
    InstructionSet_XARCH_FMA = 13,
    InstructionSet_XARCH_LZCNT = 14,
    InstructionSet_XARCH_PCLMULQDQ = 15,
    InstructionSet_XARCH_POPCNT = 16,
    InstructionSet_XARCH_Vector128 = 17,
    InstructionSet_XARCH_Vector256 = 18,
    InstructionSet_XARCH_Vector512 = 19,
    InstructionSet_XARCH_AVXVNNI = 20,
    InstructionSet_XARCH_MOVBE = 21,
    InstructionSet_XARCH_X86Serialize = 22,
    InstructionSet_XARCH_AVX512F = 23,
    InstructionSet_XARCH_AVX512F_VL = 24,
    InstructionSet_XARCH_AVX512BW = 25,
    InstructionSet_XARCH_AVX512BW_VL = 26,
    InstructionSet_XARCH_AVX512CD = 27,
    InstructionSet_XARCH_AVX512CD_VL = 28,
    InstructionSet_XARCH_AVX512DQ = 29,
    InstructionSet_XARCH_AVX512DQ_VL = 30,
    InstructionSet_XARCH_AVX512VBMI = 31,
    InstructionSet_XARCH_AVX512VBMI_VL = 32,
    InstructionSet_XARCH_VectorT128 = 33,
    InstructionSet_XARCH_VectorT256 = 34,
    InstructionSet_XARCH_VectorT512 = 35,
    InstructionSet_XARCH_X86Base_X64 = 36,
    InstructionSet_XARCH_SSE_X64 = 37,
    InstructionSet_XARCH_SSE2_X64 = 38,
    InstructionSet_XARCH_SSE3_X64 = 39,
    InstructionSet_XARCH_SSSE3_X64 = 40,
    InstructionSet_XARCH_SSE41_X64 = 41,
    InstructionSet_XARCH_SSE42_X64 = 42,
    InstructionSet_XARCH_AVX_X64 = 43,
    InstructionSet_XARCH_AVX2_X64 = 44,
    InstructionSet_XARCH_AES_X64 = 45,
    InstructionSet_XARCH_BMI1_X64 = 46,
    InstructionSet_XARCH_BMI2_X64 = 47,
    InstructionSet_XARCH_FMA_X64 = 48,
    InstructionSet_XARCH_LZCNT_X64 = 49,
    InstructionSet_XARCH_PCLMULQDQ_X64 = 50,
    InstructionSet_XARCH_POPCNT_X64 = 51,
    InstructionSet_XARCH_AVXVNNI_X64 = 52,
    InstructionSet_XARCH_MOVBE_X64 = 53,
    InstructionSet_XARCH_X86Serialize_X64 = 54,
    InstructionSet_XARCH_AVX512F_X64 = 55,
    InstructionSet_XARCH_AVX512F_VL_X64 = 56,
    InstructionSet_XARCH_AVX512BW_X64 = 57,
    InstructionSet_XARCH_AVX512BW_VL_X64 = 58,
    InstructionSet_XARCH_AVX512CD_X64 = 59,
    InstructionSet_XARCH_AVX512CD_VL_X64 = 60,
    InstructionSet_XARCH_AVX512DQ_X64 = 61,
    InstructionSet_XARCH_AVX512DQ_VL_X64 = 62,
    InstructionSet_XARCH_AVX512VBMI_X64 = 63,
    InstructionSet_XARCH_AVX512VBMI_VL_X64 = 64,
}
