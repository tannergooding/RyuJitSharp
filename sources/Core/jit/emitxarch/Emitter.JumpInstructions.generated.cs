// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private static ReadOnlySpan<instruction> emitJumpKindInstructions => [
        INS_nop, // EJ_NONE
#if TARGET_XARCH
        INS_jmp, // EJ_jmp
        INS_jo, // EJ_jo
        INS_jno, // EJ_jno
        INS_jb, // EJ_jb
        INS_jae, // EJ_jae
        INS_je, // EJ_je
        INS_jne, // EJ_jne
        INS_jbe, // EJ_jbe
        INS_ja, // EJ_ja
        INS_js, // EJ_js
        INS_jns, // EJ_jns
        INS_jp, // EJ_jp
        INS_jnp, // EJ_jnp
        INS_jl, // EJ_jl
        INS_jge, // EJ_jge
        INS_jle, // EJ_jle
        INS_jg, // EJ_jg
#elif TARGET_ARMARCH
        INS_b, // EJ_jmp
        INS_beq, // EJ_eq
        INS_bne, // EJ_ne
        INS_bhs, // EJ_hs
        INS_blo, // EJ_lo
        INS_bmi, // EJ_mi
        INS_bpl, // EJ_pl
        INS_bvs, // EJ_vs
        INS_bvc, // EJ_vc
        INS_bhi, // EJ_hi
        INS_bls, // EJ_ls
        INS_bge, // EJ_ge
        INS_blt, // EJ_lt
        INS_bgt, // EJ_gt
        INS_ble, // EJ_le
#elif TARGET_LOONGARCH64
        INS_b, // EJ_jmp
        INS_beq, // EJ_eq
        INS_bne, // EJ_ne
#elif TARGET_RISCV64
        INS_j, // EJ_jmp
        INS_beq, // EJ_eq
        INS_bne, // EJ_ne
#elif TARGET_WASM
        INS_br, // EJ_jmp
        INS_br_if, // EJ_jmpif
#else
#error Unsupported or unset target architecture
#endif

        INS_call, // EJ_COUNT
    ];

    private static ReadOnlySpan<emitJumpKind> emitReverseJumpKinds => [
        EJ_NONE,
#if TARGET_XARCH
        EJ_jmp, // EJ_jmp
        EJ_jno, // EJ_jo
        EJ_jo, // EJ_jno
        EJ_jae, // EJ_jb
        EJ_jb, // EJ_jae
        EJ_jne, // EJ_je
        EJ_je, // EJ_jne
        EJ_ja, // EJ_jbe
        EJ_jbe, // EJ_ja
        EJ_jns, // EJ_js
        EJ_js, // EJ_jns
        EJ_jnp, // EJ_jp
        EJ_jp, // EJ_jnp
        EJ_jge, // EJ_jl
        EJ_jl, // EJ_jge
        EJ_jg, // EJ_jle
        EJ_jle, // EJ_jg
#elif TARGET_ARMARCH
        EJ_jmp, // EJ_jmp
        EJ_ne, // EJ_eq
        EJ_eq, // EJ_ne
        EJ_lo, // EJ_hs
        EJ_hs, // EJ_lo
        EJ_pl, // EJ_mi
        EJ_mi, // EJ_pl
        EJ_vc, // EJ_vs
        EJ_vs, // EJ_vc
        EJ_ls, // EJ_hi
        EJ_hi, // EJ_ls
        EJ_lt, // EJ_ge
        EJ_ge, // EJ_lt
        EJ_le, // EJ_gt
        EJ_gt, // EJ_le
#elif TARGET_LOONGARCH64
        EJ_jmp, // EJ_jmp
        EJ_ne, // EJ_eq
        EJ_eq, // EJ_ne
#elif TARGET_RISCV64
        EJ_jmp, // EJ_jmp
        EJ_ne, // EJ_eq
        EJ_eq, // EJ_ne
#elif TARGET_WASM
        EJ_br, // EJ_jmp
        EJ_br_if, // EJ_jmpif
#else
#error Unsupported or unset target architecture
#endif
    ];
#endif
}