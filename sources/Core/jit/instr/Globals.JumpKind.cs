// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static emitJumpKind EJ_jo => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jo
        : UnsupportedJumpKind(nameof(EJ_jo));

    public static emitJumpKind EJ_jno => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jno
        : UnsupportedJumpKind(nameof(EJ_jno));

    public static emitJumpKind EJ_jb => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jb
        : UnsupportedJumpKind(nameof(EJ_jb));

    public static emitJumpKind EJ_jae => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jae
        : UnsupportedJumpKind(nameof(EJ_jae));

    public static emitJumpKind EJ_je => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_je
        : UnsupportedJumpKind(nameof(EJ_je));

    public static emitJumpKind EJ_jne => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jne
        : UnsupportedJumpKind(nameof(EJ_jne));

    public static emitJumpKind EJ_jbe => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jbe
        : UnsupportedJumpKind(nameof(EJ_jbe));

    public static emitJumpKind EJ_ja => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_ja
        : UnsupportedJumpKind(nameof(EJ_ja));

    public static emitJumpKind EJ_js => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_js
        : UnsupportedJumpKind(nameof(EJ_js));

    public static emitJumpKind EJ_jns => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jns
        : UnsupportedJumpKind(nameof(EJ_jns));

    public static emitJumpKind EJ_jp => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jp
        : UnsupportedJumpKind(nameof(EJ_jp));

    public static emitJumpKind EJ_jnp => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jnp
        : UnsupportedJumpKind(nameof(EJ_jnp));

    public static emitJumpKind EJ_jl => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jl
        : UnsupportedJumpKind(nameof(EJ_jl));

    public static emitJumpKind EJ_jge => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jge
        : UnsupportedJumpKind(nameof(EJ_jge));

    public static emitJumpKind EJ_jle => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jle
        : UnsupportedJumpKind(nameof(EJ_jle));

    public static emitJumpKind EJ_jg => Configuration.TARGET_XARCH
        ? emitJumpKind.EJ_XARCH_jg
        : UnsupportedJumpKind(nameof(EJ_jg));

    public static emitJumpKind EJ_eq =>
        Configuration.TARGET_XARCH ? emitJumpKind.EJ_XARCH_je :
        Configuration.TARGET_ARMARCH ? emitJumpKind.EJ_ARMARCH_eq :
        Configuration.TARGET_LOONGARCH64 ? emitJumpKind.EJ_LOONGARCH64_eq :
        Configuration.TARGET_RISCV64 ? emitJumpKind.EJ_RISCV64_eq :
        UnsupportedJumpKind(nameof(EJ_eq));

    public static emitJumpKind EJ_ne =>
        Configuration.TARGET_XARCH ? emitJumpKind.EJ_XARCH_jne :
        Configuration.TARGET_ARMARCH ? emitJumpKind.EJ_ARMARCH_ne :
        Configuration.TARGET_LOONGARCH64 ? emitJumpKind.EJ_LOONGARCH64_ne :
        Configuration.TARGET_RISCV64 ? emitJumpKind.EJ_RISCV64_ne :
        UnsupportedJumpKind(nameof(EJ_ne));

    public static emitJumpKind EJ_hs => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_hs
        : UnsupportedJumpKind(nameof(EJ_hs));

    public static emitJumpKind EJ_lo => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_lo
        : UnsupportedJumpKind(nameof(EJ_lo));

    public static emitJumpKind EJ_mi => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_mi
        : UnsupportedJumpKind(nameof(EJ_mi));

    public static emitJumpKind EJ_pl => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_pl
        : UnsupportedJumpKind(nameof(EJ_pl));

    public static emitJumpKind EJ_vs => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_vs
        : UnsupportedJumpKind(nameof(EJ_vs));

    public static emitJumpKind EJ_vc => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_vc
        : UnsupportedJumpKind(nameof(EJ_vc));

    public static emitJumpKind EJ_hi => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_hi
        : UnsupportedJumpKind(nameof(EJ_hi));

    public static emitJumpKind EJ_ls => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_ls
        : UnsupportedJumpKind(nameof(EJ_ls));

    public static emitJumpKind EJ_ge => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_ge
        : UnsupportedJumpKind(nameof(EJ_ge));

    public static emitJumpKind EJ_lt => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_lt
        : UnsupportedJumpKind(nameof(EJ_lt));

    public static emitJumpKind EJ_gt => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_gt
        : UnsupportedJumpKind(nameof(EJ_gt));

    public static emitJumpKind EJ_le => Configuration.TARGET_ARMARCH
        ? emitJumpKind.EJ_ARMARCH_le
        : UnsupportedJumpKind(nameof(EJ_le));

    public static emitJumpKind EJ_jmpif => Configuration.TARGET_WASM
        ? emitJumpKind.EJ_WASM_jmpif
        : UnsupportedJumpKind(nameof(EJ_jmpif));

    private static emitJumpKind UnsupportedJumpKind(string jumpKind)
    {
        throw new FatalJitException(CORJIT_SKIPPED, $"{jumpKind} is not supported for this target.");
    }
}
