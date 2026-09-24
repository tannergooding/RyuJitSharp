// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
global using static RyuJitSharp.insFlags;

using System;

namespace RyuJitSharp;

[Flags]
public enum insFlags : ulong
{
    INS_FLAGS_None = 0,

    Reads_OF = 1UL << 0,
    Reads_SF = 1UL << 1,
    Reads_ZF = 1UL << 2,
    Reads_PF = 1UL << 3,
    Reads_CF = 1UL << 4,
    Reads_DF = 1UL << 5,

    Writes_OF = 1UL << 6,
    Writes_SF = 1UL << 7,
    Writes_ZF = 1UL << 8,
    Writes_AF = 1UL << 9,
    Writes_PF = 1UL << 10,
    Writes_CF = 1UL << 11,

    Resets_OF = 1UL << 12,
    Resets_SF = 1UL << 13,
    Resets_ZF = 1UL << 14,
    Resets_AF = 1UL << 15,
    Resets_PF = 1UL << 16,
    Resets_CF = 1UL << 17,

    Undefined_OF = 1UL << 18,
    Undefined_SF = 1UL << 19,
    Undefined_ZF = 1UL << 20,
    Undefined_AF = 1UL << 21,
    Undefined_PF = 1UL << 22,
    Undefined_CF = 1UL << 23,

    Restore_SF_ZF_AF_PF_CF = 1UL << 24,
    INS_FLAGS_X87Instr = 1UL << 25,
    INS_FLAGS_IsDstDstSrcAVXInstruction = 1UL << 26,
    INS_FLAGS_IsDstSrcSrcAVXInstruction = 1UL << 27,
    INS_FLAGS_Is3OperandInstructionMask =
        INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsDstSrcSrcAVXInstruction,

    // Either operand order can produce a smaller encoding for these instructions.
    INS_FLAGS_IsAvxCommutative = 1UL << 28,
    INS_FLAGS_HasWBit = 1UL << 29,
    INS_FLAGS_HasSBit = 1UL << 30,

    // Scalar or broadcast load width; absent flags leave the size to emitAttr.
    Input_8Bit = 1UL << 31,
    Input_16Bit = 1UL << 32,
    Input_32Bit = 1UL << 33,
    Input_64Bit = 1UL << 34,
    Input_Mask = 0xFUL << 31,

    REX_W0 = 1UL << 35,
    REX_W1 = 1UL << 36,
    REX_WX = 1UL << 37,
    REX_W0_EVEX = REX_W0,
    // EVEX requires W1, while other encodings use W0 or ignore W.
    REX_W1_EVEX = 1UL << 38,
    REX_WIG = REX_W0,
    Encoding_VEX = 1UL << 39,
    Encoding_EVEX = 1UL << 40,
    KInstruction = 1UL << 41,
    KInstructionWithLBit = 1UL << 42,

    // IDs confined to APX EVEX, not legacy IDs that merely gain NDD/NF forms.
    Encoding_EVEX_APX_ONLY = 1UL << 43,
    Encoding_REX2 = 1UL << 44,
    INS_FLAGS_HasNDD = 1UL << 45,
    INS_FLAGS_HasNF = 1UL << 46,
    INS_FLAGS_ApxEvexMask = Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD | INS_FLAGS_HasNF,

    // Base kmask size for a 128-bit vector, used for embedded masking.
    KMask_Base1 = 1UL << 47,
    KMask_Base2 = 1UL << 48,
    KMask_Base4 = 1UL << 49,
    KMask_Base8 = 1UL << 50,
    KMask_Base16 = 1UL << 51,
    KMask_BaseMask = 0x1FUL << 47,

    INS_FLAGS_HasPseudoName = 1UL << 52,
    INS_FLAGS_DONT_CARE = 0,
}
#endif
