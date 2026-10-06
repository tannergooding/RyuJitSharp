// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;

namespace RyuJitSharp;

public partial class Emitter
{
    private static ReadOnlySpan<instruction> emitJumpKindInstructions => [
        INS_nop, // EJ_NONE
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
    ];

    private static ReadOnlySpan<emitJumpKind> emitReverseJumpKinds => [
        emitJumpKind.EJ_NONE,
        emitJumpKind.EJ_jmp, // EJ_jmp
        emitJumpKind.EJ_ne, // EJ_eq
        emitJumpKind.EJ_eq, // EJ_ne
        emitJumpKind.EJ_lo, // EJ_hs
        emitJumpKind.EJ_hs, // EJ_lo
        emitJumpKind.EJ_pl, // EJ_mi
        emitJumpKind.EJ_mi, // EJ_pl
        emitJumpKind.EJ_vc, // EJ_vs
        emitJumpKind.EJ_vs, // EJ_vc
        emitJumpKind.EJ_ls, // EJ_hi
        emitJumpKind.EJ_hi, // EJ_ls
        emitJumpKind.EJ_lt, // EJ_ge
        emitJumpKind.EJ_ge, // EJ_lt
        emitJumpKind.EJ_le, // EJ_gt
        emitJumpKind.EJ_gt, // EJ_le
    ];
}
#endif
