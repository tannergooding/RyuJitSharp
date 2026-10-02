// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if (TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64) && !TARGET_XARCH && !TARGET_ARM64 && !TARGET_WASM
global using static RyuJitSharp.insOpts;

namespace RyuJitSharp;

public enum insOpts : uint
{
    INS_OPTS_NONE,

#if TARGET_ARM
    INS_OPTS_LDST_PRE_DEC,
    INS_OPTS_LDST_POST_INC,

    INS_OPTS_RRX,
    INS_OPTS_LSL,
    INS_OPTS_LSR,
    INS_OPTS_ASR,
    INS_OPTS_ROR,
#elif TARGET_LOONGARCH64
    INS_OPTS_RC,     // see ::emitIns_R_C().
    INS_OPTS_RL,     // see ::emitIns_R_L().
    INS_OPTS_JIRL,   // see ::emitIns_J_R().
    INS_OPTS_J,      // see ::emitIns_J().
    INS_OPTS_J_cond, // see ::emitIns_J_cond_la().
    INS_OPTS_I,      // see ::emitIns_I_la().
    INS_OPTS_C,      // see ::emitIns_Call().
    INS_OPTS_RELOC,  // see ::emitIns_R_AI().
#elif TARGET_RISCV64
    INS_OPTS_RC,    // see ::emitIns_R_C().
    INS_OPTS_RL,    // see ::emitIns_R_L().
    INS_OPTS_JUMP,  // see ::emitIns_J and ::emitIns_J_cond_la().
    INS_OPTS_I,     // see ::emitLoadImmediate().
    INS_OPTS_C,     // see ::emitIns_Call().
    INS_OPTS_RELOC, // see ::emitIns_R_AI().
#endif
}
#endif
