// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.ReadyToRunInstructionSet;

namespace RyuJitSharp;

public enum ReadyToRunInstructionSet
{
    READYTORUN_INSTRUCTION_Sse = 1,

    READYTORUN_INSTRUCTION_Sse2 = 2,

    READYTORUN_INSTRUCTION_Sse3 = 3,

    READYTORUN_INSTRUCTION_Ssse3 = 4,

    READYTORUN_INSTRUCTION_Sse41 = 5,

    READYTORUN_INSTRUCTION_Sse42 = 6,

    READYTORUN_INSTRUCTION_Avx = 7,

    READYTORUN_INSTRUCTION_Avx2 = 8,

    READYTORUN_INSTRUCTION_Aes = 9,

    READYTORUN_INSTRUCTION_Bmi1 = 10,

    READYTORUN_INSTRUCTION_Bmi2 = 11,

    READYTORUN_INSTRUCTION_Fma = 12,

    READYTORUN_INSTRUCTION_Lzcnt = 13,

    READYTORUN_INSTRUCTION_Pclmulqdq = 14,

    READYTORUN_INSTRUCTION_Popcnt = 15,

    READYTORUN_INSTRUCTION_ArmBase = 16,

    READYTORUN_INSTRUCTION_AdvSimd = 17,

    READYTORUN_INSTRUCTION_Crc32 = 18,

    READYTORUN_INSTRUCTION_Sha1 = 19,

    READYTORUN_INSTRUCTION_Sha256 = 20,

    READYTORUN_INSTRUCTION_Atomics = 21,

    READYTORUN_INSTRUCTION_X86Base = 22,

    READYTORUN_INSTRUCTION_Dp = 23,

    READYTORUN_INSTRUCTION_Rdm = 24,

    READYTORUN_INSTRUCTION_AvxVnni = 25,

    READYTORUN_INSTRUCTION_Rcpc = 26,

    READYTORUN_INSTRUCTION_Movbe = 27,

    READYTORUN_INSTRUCTION_X86Serialize = 28,

    READYTORUN_INSTRUCTION_Avx512F = 29,

    READYTORUN_INSTRUCTION_Avx512F_VL = 30,

    READYTORUN_INSTRUCTION_Avx512BW = 31,

    READYTORUN_INSTRUCTION_Avx512BW_VL = 32,

    READYTORUN_INSTRUCTION_Avx512CD = 33,

    READYTORUN_INSTRUCTION_Avx512CD_VL = 34,

    READYTORUN_INSTRUCTION_Avx512DQ = 35,

    READYTORUN_INSTRUCTION_Avx512DQ_VL = 36,

    READYTORUN_INSTRUCTION_Avx512Vbmi = 37,

    READYTORUN_INSTRUCTION_Avx512Vbmi_VL = 38,

    READYTORUN_INSTRUCTION_VectorT128 = 39,

    READYTORUN_INSTRUCTION_VectorT256 = 40,

    READYTORUN_INSTRUCTION_VectorT512 = 41,

    READYTORUN_INSTRUCTION_Rcpc2 = 42,

    READYTORUN_INSTRUCTION_Sve = 43,
}
