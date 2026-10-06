// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly struct GenConditionDesc
{
    private GenConditionDesc(emitJumpKind jumpKind1, genTreeOps oper = GT_NONE, emitJumpKind jumpKind2 = EJ_NONE)
    {
        JumpKind1 = jumpKind1;
        Oper = oper;
        JumpKind2 = jumpKind2;
    }

    public emitJumpKind JumpKind1 { get; }

    public genTreeOps Oper { get; }

    public emitJumpKind JumpKind2 { get; }

    public static GenConditionDesc Get(GenCondition condition)
    {
        var map = GetMap();
        assert((uint)condition.Code < (uint)map.Length);
        var desc = map[(int)condition.Code];
        assert(desc.JumpKind1 is not EJ_NONE);
        assert(desc.Oper is GT_NONE or GT_AND or GT_OR);
        assert((desc.Oper is GT_NONE) == (desc.JumpKind2 is EJ_NONE));

        return desc;
    }

    private static ReadOnlySpan<GenConditionDesc> GetMap()
    {
#if TARGET_XARCH || TARGET_ARM64
        return s_map;
#else
        throw new NotImplementedException("Condition instruction mapping is not ported for this target.");
#endif
    }

#if TARGET_XARCH
    // UCOMIS sets ZF/PF/CF to 111 for unordered, 000 for greater, 001 for
    // less, and 100 for equal. Conditions sharing the unordered ZF/CF bits
    // therefore need a parity check combined with their ordinary comparison.
    // GT_AND/GT_OR describe short-circuit combinations, not emitted ALU operations.
    private static readonly GenConditionDesc[] s_map = [
        default, // NONE
        default, // 1
        new(EJ_jl), // SLT
        new(EJ_jle), // SLE
        new(EJ_jge), // SGE
        new(EJ_jg), // SGT
        new(EJ_js), // S
        new(EJ_jns), // NS

        new(EJ_je), // EQ
        new(EJ_jne), // NE
        new(EJ_jb), // ULT
        new(EJ_jbe), // ULE
        new(EJ_jae), // UGE
        new(EJ_ja), // UGT
        new(EJ_jb), // C
        new(EJ_jae), // NC

        new(EJ_jnp, GT_AND, EJ_je), // FEQ
        new(EJ_jne), // FNE
        new(EJ_jnp, GT_AND, EJ_jb), // FLT
        new(EJ_jnp, GT_AND, EJ_jbe), // FLE
        new(EJ_jae), // FGE
        new(EJ_ja), // FGT
        new(EJ_jo), // O
        new(EJ_jno), // NO

        new(EJ_je), // FEQU
        new(EJ_jp, GT_OR, EJ_jne), // FNEU
        new(EJ_jb), // FLTU
        new(EJ_jbe), // FLEU
        new(EJ_jp, GT_OR, EJ_jae), // FGEU
        new(EJ_jp, GT_OR, EJ_ja), // FGTU
        new(EJ_jp), // P
        new(EJ_jnp), // NP
    ];
#elif TARGET_ARM64
    private static readonly GenConditionDesc[] s_map = [
        default, // NONE
        default, // 1
        new(EJ_lt), // SLT
        new(EJ_le), // SLE
        new(EJ_ge), // SGE
        new(EJ_gt), // SGT
        new(EJ_mi), // S
        new(EJ_pl), // NS

        new(EJ_eq), // EQ
        new(EJ_ne), // NE
        new(EJ_lo), // ULT
        new(EJ_ls), // ULE
        new(EJ_hs), // UGE
        new(EJ_hi), // UGT
        new(EJ_hs), // C
        new(EJ_lo), // NC

        new(EJ_eq), // FEQ
        new(EJ_gt, GT_AND, EJ_lo), // FNE
        new(EJ_lo), // FLT
        new(EJ_ls), // FLE
        new(EJ_ge), // FGE
        new(EJ_gt), // FGT
        new(EJ_vs), // O
        new(EJ_vc), // NO

        new(EJ_eq, GT_OR, EJ_vs), // FEQU
        new(EJ_ne), // FNEU
        new(EJ_lt), // FLTU
        new(EJ_le), // FLEU
        new(EJ_hs), // FGEU
        new(EJ_hi), // FGTU
        default, // P
        default, // NP
    ];
#endif
}
