// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.emitJumpKind;

namespace RyuJitSharp;

public enum emitJumpKind
{
    EJ_NONE = 0,
    EJ_jmp = 1,
    EJ_XARCH_jo = 2,
    EJ_XARCH_jno = 3,
    EJ_XARCH_jb = 4,
    EJ_XARCH_jae = 5,
    EJ_XARCH_je = 6,
    EJ_XARCH_jne = 7,
    EJ_XARCH_jbe = 8,
    EJ_XARCH_ja = 9,
    EJ_XARCH_js = 10,
    EJ_XARCH_jns = 11,
    EJ_XARCH_jp = 12,
    EJ_XARCH_jnp = 13,
    EJ_XARCH_jl = 14,
    EJ_XARCH_jge = 15,
    EJ_XARCH_jle = 16,
    EJ_XARCH_jg = 17,
    EJ_XARCH_COUNT = 18,
    EJ_ARMARCH_eq = 2,
    EJ_ARMARCH_ne = 3,
    EJ_ARMARCH_hs = 4,
    EJ_ARMARCH_lo = 5,
    EJ_ARMARCH_mi = 6,
    EJ_ARMARCH_pl = 7,
    EJ_ARMARCH_vs = 8,
    EJ_ARMARCH_vc = 9,
    EJ_ARMARCH_hi = 10,
    EJ_ARMARCH_ls = 11,
    EJ_ARMARCH_ge = 12,
    EJ_ARMARCH_lt = 13,
    EJ_ARMARCH_gt = 14,
    EJ_ARMARCH_le = 15,
    EJ_ARMARCH_COUNT = 16,
    EJ_LOONGARCH64_eq = 2,
    EJ_LOONGARCH64_ne = 3,
    EJ_LOONGARCH64_COUNT = 4,
    EJ_RISCV64_eq = 2,
    EJ_RISCV64_ne = 3,
    EJ_RISCV64_COUNT = 4,
    EJ_WASM_jmpif = 2,
    EJ_WASM_COUNT = 3,

}