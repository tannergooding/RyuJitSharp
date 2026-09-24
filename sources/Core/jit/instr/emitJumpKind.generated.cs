// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.emitJumpKind;

namespace RyuJitSharp;

public enum emitJumpKind
{
    EJ_NONE,
#if TARGET_XARCH
    EJ_jmp,
    EJ_jo,
    EJ_jno,
    EJ_jb,
    EJ_jae,
    EJ_je,
    EJ_jne,
    EJ_jbe,
    EJ_ja,
    EJ_js,
    EJ_jns,
    EJ_jp,
    EJ_jnp,
    EJ_jl,
    EJ_jge,
    EJ_jle,
    EJ_jg,
#elif TARGET_ARMARCH
    EJ_jmp,
    EJ_eq,
    EJ_ne,
    EJ_hs,
    EJ_lo,
    EJ_mi,
    EJ_pl,
    EJ_vs,
    EJ_vc,
    EJ_hi,
    EJ_ls,
    EJ_ge,
    EJ_lt,
    EJ_gt,
    EJ_le,
#elif TARGET_LOONGARCH64
    EJ_jmp,
    EJ_eq,
    EJ_ne,
#elif TARGET_RISCV64
    EJ_jmp,
    EJ_eq,
    EJ_ne,
#elif TARGET_WASM
    EJ_jmp,
    EJ_jmpif,
#else
#error Unsupported or unset target architecture
#endif

    EJ_COUNT,
}