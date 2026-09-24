// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
#if TARGET_XARCH
        assert((uint)condition.Code < (uint)s_map.Length);
        var desc = s_map[(int)condition.Code];
        assert(desc.JumpKind1 is not EJ_NONE);
        assert(desc.Oper is GT_NONE or GT_AND or GT_OR);
        assert((desc.Oper is GT_NONE) == (desc.JumpKind2 is EJ_NONE));

        return desc;
#else
        throw new System.NotImplementedException("Non-xarch condition instruction mapping is not ported.");
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
#endif
}
