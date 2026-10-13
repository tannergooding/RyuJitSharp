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
        if (Configuration.TARGET_XARCH)
        {
            return XarchMap;
        }

        if (Configuration.TARGET_ARM || Configuration.TARGET_ARM64)
        {
            return ArmArchMap;
        }

        throw new NotImplementedException("Condition instruction mapping is not ported for this target.");
    }

    // UCOMIS sets ZF/PF/CF to 111 for unordered, 000 for greater, 001 for
    // less, and 100 for equal. Conditions sharing the unordered ZF/CF bits
    // therefore need a parity check combined with their ordinary comparison.
    // GT_AND/GT_OR describe short-circuit combinations, not emitted ALU operations.
    private static readonly GenConditionDesc[] XarchMap =
    [
        default, // NONE
        default, // 1
        new(EJ_XARCH_jl), // SLT
        new(EJ_XARCH_jle), // SLE
        new(EJ_XARCH_jge), // SGE
        new(EJ_XARCH_jg), // SGT
        new(EJ_XARCH_js), // S
        new(EJ_XARCH_jns), // NS

        new(EJ_XARCH_je), // EQ
        new(EJ_XARCH_jne), // NE
        new(EJ_XARCH_jb), // ULT
        new(EJ_XARCH_jbe), // ULE
        new(EJ_XARCH_jae), // UGE
        new(EJ_XARCH_ja), // UGT
        new(EJ_XARCH_jb), // C
        new(EJ_XARCH_jae), // NC

        new(EJ_XARCH_jnp, GT_AND, EJ_XARCH_je), // FEQ
        new(EJ_XARCH_jne), // FNE
        new(EJ_XARCH_jnp, GT_AND, EJ_XARCH_jb), // FLT
        new(EJ_XARCH_jnp, GT_AND, EJ_XARCH_jbe), // FLE
        new(EJ_XARCH_jae), // FGE
        new(EJ_XARCH_ja), // FGT
        new(EJ_XARCH_jo), // O
        new(EJ_XARCH_jno), // NO

        new(EJ_XARCH_je), // FEQU
        new(EJ_XARCH_jp, GT_OR, EJ_XARCH_jne), // FNEU
        new(EJ_XARCH_jb), // FLTU
        new(EJ_XARCH_jbe), // FLEU
        new(EJ_XARCH_jp, GT_OR, EJ_XARCH_jae), // FGEU
        new(EJ_XARCH_jp, GT_OR, EJ_XARCH_ja), // FGTU
        new(EJ_XARCH_jp), // P
        new(EJ_XARCH_jnp), // NP
    ];

    private static readonly GenConditionDesc[] ArmArchMap =
    [
        default, // NONE
        default, // 1
        new(EJ_ARMARCH_lt), // SLT
        new(EJ_ARMARCH_le), // SLE
        new(EJ_ARMARCH_ge), // SGE
        new(EJ_ARMARCH_gt), // SGT
        new(EJ_ARMARCH_mi), // S
        new(EJ_ARMARCH_pl), // NS

        new(EJ_ARMARCH_eq), // EQ
        new(EJ_ARMARCH_ne), // NE
        new(EJ_ARMARCH_lo), // ULT
        new(EJ_ARMARCH_ls), // ULE
        new(EJ_ARMARCH_hs), // UGE
        new(EJ_ARMARCH_hi), // UGT
        new(EJ_ARMARCH_hs), // C
        new(EJ_ARMARCH_lo), // NC

        new(EJ_ARMARCH_eq), // FEQ
        new(EJ_ARMARCH_gt, GT_AND, EJ_ARMARCH_lo), // FNE
        new(EJ_ARMARCH_lo), // FLT
        new(EJ_ARMARCH_ls), // FLE
        new(EJ_ARMARCH_ge), // FGE
        new(EJ_ARMARCH_gt), // FGT
        new(EJ_ARMARCH_vs), // O
        new(EJ_ARMARCH_vc), // NO

        new(EJ_ARMARCH_eq, GT_OR, EJ_ARMARCH_vs), // FEQU
        new(EJ_ARMARCH_ne), // FNEU
        new(EJ_ARMARCH_lt), // FLTU
        new(EJ_ARMARCH_le), // FLEU
        new(EJ_ARMARCH_hs), // FGEU
        new(EJ_ARMARCH_hi), // FGTU
        default, // P
        default, // NP
    ];
}
