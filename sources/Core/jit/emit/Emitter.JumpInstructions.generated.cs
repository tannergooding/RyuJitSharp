// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    private static ReadOnlySpan<instruction> s_emitJumpKindInstructionsXARCH => [
        INS_nop, // EJ_NONE
        INS_jmp, // EJ_jmp
        INS_jo, // EJ_XARCH_jo
        INS_jno, // EJ_XARCH_jno
        INS_jb, // EJ_XARCH_jb
        INS_jae, // EJ_XARCH_jae
        INS_je, // EJ_XARCH_je
        INS_jne, // EJ_XARCH_jne
        INS_jbe, // EJ_XARCH_jbe
        INS_ja, // EJ_XARCH_ja
        INS_js, // EJ_XARCH_js
        INS_jns, // EJ_XARCH_jns
        INS_jp, // EJ_XARCH_jp
        INS_jnp, // EJ_XARCH_jnp
        INS_jl, // EJ_XARCH_jl
        INS_jge, // EJ_XARCH_jge
        INS_jle, // EJ_XARCH_jle
        INS_jg, // EJ_XARCH_jg
        INS_call, // EJ_XARCH_COUNT
    ];
    private static ReadOnlySpan<instruction> s_emitJumpKindInstructionsARMARCH => [
        INS_nop, // EJ_NONE
        INS_b, // EJ_jmp
        INS_beq, // EJ_ARMARCH_eq
        INS_bne, // EJ_ARMARCH_ne
        INS_bhs, // EJ_ARMARCH_hs
        INS_blo, // EJ_ARMARCH_lo
        INS_bmi, // EJ_ARMARCH_mi
        INS_bpl, // EJ_ARMARCH_pl
        INS_bvs, // EJ_ARMARCH_vs
        INS_bvc, // EJ_ARMARCH_vc
        INS_bhi, // EJ_ARMARCH_hi
        INS_bls, // EJ_ARMARCH_ls
        INS_bge, // EJ_ARMARCH_ge
        INS_blt, // EJ_ARMARCH_lt
        INS_bgt, // EJ_ARMARCH_gt
        INS_ble, // EJ_ARMARCH_le
    ];
    private static ReadOnlySpan<instruction> s_emitJumpKindInstructionsLOONGARCH64 => [
        INS_nop, // EJ_NONE
        INS_b, // EJ_jmp
        INS_beq, // EJ_LOONGARCH64_eq
        INS_bne, // EJ_LOONGARCH64_ne
    ];
    private static ReadOnlySpan<instruction> s_emitJumpKindInstructionsRISCV64 => [
        INS_nop, // EJ_NONE
        INS_j, // EJ_jmp
        INS_beq, // EJ_RISCV64_eq
        INS_bne, // EJ_RISCV64_ne
    ];
    private static ReadOnlySpan<instruction> s_emitJumpKindInstructionsWASM => [
        INS_nop, // EJ_NONE
        INS_br, // EJ_jmp
        INS_br_if, // EJ_WASM_jmpif
    ];


    private static ReadOnlySpan<emitJumpKind> s_emitReverseJumpKindsXARCH => [
        EJ_NONE, // EJ_NONE
        EJ_jmp, // EJ_jmp
        EJ_XARCH_jno, // EJ_XARCH_jo
        EJ_XARCH_jo, // EJ_XARCH_jno
        EJ_XARCH_jae, // EJ_XARCH_jb
        EJ_XARCH_jb, // EJ_XARCH_jae
        EJ_XARCH_jne, // EJ_XARCH_je
        EJ_XARCH_je, // EJ_XARCH_jne
        EJ_XARCH_ja, // EJ_XARCH_jbe
        EJ_XARCH_jbe, // EJ_XARCH_ja
        EJ_XARCH_jns, // EJ_XARCH_js
        EJ_XARCH_js, // EJ_XARCH_jns
        EJ_XARCH_jnp, // EJ_XARCH_jp
        EJ_XARCH_jp, // EJ_XARCH_jnp
        EJ_XARCH_jge, // EJ_XARCH_jl
        EJ_XARCH_jl, // EJ_XARCH_jge
        EJ_XARCH_jg, // EJ_XARCH_jle
        EJ_XARCH_jle, // EJ_XARCH_jg
    ];
    private static ReadOnlySpan<emitJumpKind> s_emitReverseJumpKindsARMARCH => [
        EJ_NONE, // EJ_NONE
        EJ_jmp, // EJ_jmp
        EJ_ARMARCH_ne, // EJ_ARMARCH_eq
        EJ_ARMARCH_eq, // EJ_ARMARCH_ne
        EJ_ARMARCH_lo, // EJ_ARMARCH_hs
        EJ_ARMARCH_hs, // EJ_ARMARCH_lo
        EJ_ARMARCH_pl, // EJ_ARMARCH_mi
        EJ_ARMARCH_mi, // EJ_ARMARCH_pl
        EJ_ARMARCH_vc, // EJ_ARMARCH_vs
        EJ_ARMARCH_vs, // EJ_ARMARCH_vc
        EJ_ARMARCH_ls, // EJ_ARMARCH_hi
        EJ_ARMARCH_hi, // EJ_ARMARCH_ls
        EJ_ARMARCH_lt, // EJ_ARMARCH_ge
        EJ_ARMARCH_ge, // EJ_ARMARCH_lt
        EJ_ARMARCH_le, // EJ_ARMARCH_gt
        EJ_ARMARCH_gt, // EJ_ARMARCH_le
    ];
    private static ReadOnlySpan<emitJumpKind> s_emitReverseJumpKindsLOONGARCH64 => [
        EJ_NONE, // EJ_NONE
        EJ_jmp, // EJ_jmp
        EJ_LOONGARCH64_ne, // EJ_LOONGARCH64_eq
        EJ_LOONGARCH64_eq, // EJ_LOONGARCH64_ne
    ];
    private static ReadOnlySpan<emitJumpKind> s_emitReverseJumpKindsRISCV64 => [
        EJ_NONE, // EJ_NONE
        EJ_jmp, // EJ_jmp
        EJ_RISCV64_ne, // EJ_RISCV64_eq
        EJ_RISCV64_eq, // EJ_RISCV64_ne
    ];
    private static ReadOnlySpan<emitJumpKind> s_emitReverseJumpKindsWASM => [
        EJ_NONE, // EJ_NONE
        EJ_NONE, // EJ_jmp
        EJ_NONE, // EJ_WASM_jmpif
    ];


    private static ReadOnlySpan<instruction> emitJumpKindInstructions
    {
        get
        {
            if (Configuration.TARGET_XARCH)
            {
                return s_emitJumpKindInstructionsXARCH;
            }

            if (Configuration.TARGET_ARMARCH)
            {
                return s_emitJumpKindInstructionsARMARCH;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return s_emitJumpKindInstructionsLOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return s_emitJumpKindInstructionsRISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return s_emitJumpKindInstructionsWASM;
            }

            FatalJitException.ThrowFatalJitException(
                CORJIT_SKIPPED,
                "Jump-kind instruction mapping is not supported for this target.");
            return default;
        }
    }

    private static ReadOnlySpan<emitJumpKind> emitReverseJumpKinds
    {
        get
        {
            if (Configuration.TARGET_XARCH)
            {
                return s_emitReverseJumpKindsXARCH;
            }

            if (Configuration.TARGET_ARMARCH)
            {
                return s_emitReverseJumpKindsARMARCH;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return s_emitReverseJumpKindsLOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return s_emitReverseJumpKindsRISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return s_emitReverseJumpKindsWASM;
            }

            FatalJitException.ThrowFatalJitException(
                CORJIT_SKIPPED,
                "Reverse jump-kind mapping is not supported for this target.");
            return default;
        }
    }
}