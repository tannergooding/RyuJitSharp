// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
global using static RyuJitSharp.insOpts;

namespace RyuJitSharp;

public enum insOpts : uint
{
    INS_OPTS_NONE,

    INS_OPTS_PRE_INDEX,
    INS_OPTS_POST_INDEX,

    INS_OPTS_LSL12,

    INS_OPTS_LSL = 4,
    INS_OPTS_LSR,
    INS_OPTS_ASR,
    INS_OPTS_ROR,

    INS_OPTS_UXTB = 8,
    INS_OPTS_UXTH,
    INS_OPTS_UXTW,
    INS_OPTS_UXTX,
    INS_OPTS_SXTB,
    INS_OPTS_SXTH,
    INS_OPTS_SXTW,
    INS_OPTS_SXTX,

    INS_OPTS_8B = 16,
    INS_OPTS_16B,
    INS_OPTS_4H,
    INS_OPTS_8H,
    INS_OPTS_2S,
    INS_OPTS_4S,
    INS_OPTS_1D,
    INS_OPTS_2D,

    INS_OPTS_SCALABLE_B,
    INS_OPTS_SCALABLE_H,
    INS_OPTS_SCALABLE_S,
    INS_OPTS_SCALABLE_D,
    INS_OPTS_SCALABLE_Q,

    INS_OPTS_SCALABLE_S_UXTW,
    INS_OPTS_SCALABLE_S_SXTW,
    INS_OPTS_SCALABLE_D_UXTW,
    INS_OPTS_SCALABLE_D_SXTW,

    INS_OPTS_MSL,

    INS_OPTS_S_TO_4BYTE,
    INS_OPTS_D_TO_4BYTE,

    INS_OPTS_S_TO_8BYTE,
    INS_OPTS_D_TO_8BYTE,

    INS_OPTS_H_TO_4BYTE,
    INS_OPTS_H_TO_8BYTE,

    INS_OPTS_4BYTE_TO_S,
    INS_OPTS_4BYTE_TO_D,

    INS_OPTS_8BYTE_TO_S,
    INS_OPTS_8BYTE_TO_D,

    INS_OPTS_4BYTE_TO_H,
    INS_OPTS_8BYTE_TO_H,

    INS_OPTS_S_TO_D,
    INS_OPTS_D_TO_S,

    INS_OPTS_H_TO_S,
    INS_OPTS_H_TO_D,

    INS_OPTS_S_TO_H,
    INS_OPTS_D_TO_H,

#if FEATURE_LOOP_ALIGN
    INS_OPTS_ALIGN,
#endif
}
#endif
