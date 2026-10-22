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
        ReadOnlySpan<emitJumpKind> jumpKind1;
        ReadOnlySpan<genTreeOps> oper;
        ReadOnlySpan<emitJumpKind> jumpKind2;
        if (Configuration.TARGET_XARCH)
        {
            jumpKind1 = XarchJumpKind1;
            oper = XarchOper;
            jumpKind2 = XarchJumpKind2;
        }
        else if (Configuration.TARGET_ARM || Configuration.TARGET_ARM64)
        {
            jumpKind1 = ArmArchJumpKind1;
            oper = ArmArchOper;
            jumpKind2 = ArmArchJumpKind2;
        }
        else
        {
            JitExceptionHelpers.ThrowNotImplementedException(FatalJitErrorCode.ConditionInstructionMappingIsNotPortedForThisTarget);
            return default;
        }

        assert((uint)condition.Code < (uint)jumpKind1.Length);
        var index = (int)condition.Code;
        var desc = new GenConditionDesc(jumpKind1[index], oper[index], jumpKind2[index]);
        assert(desc.JumpKind1 is not EJ_NONE);
        assert(desc.Oper is GT_NONE or GT_AND or GT_OR);
        assert((desc.Oper is GT_NONE) == (desc.JumpKind2 is EJ_NONE));

        return desc;
    }

    public static emitJumpKind GetJumpKind1(GenCondition condition)
    {
        ReadOnlySpan<emitJumpKind> jumpKind1;
        if (Configuration.TARGET_XARCH)
        {
            jumpKind1 = XarchJumpKind1;
        }
        else if (Configuration.TARGET_ARM || Configuration.TARGET_ARM64)
        {
            jumpKind1 = ArmArchJumpKind1;
        }
        else
        {
            JitExceptionHelpers.ThrowNotImplementedException(FatalJitErrorCode.ConditionInstructionMappingIsNotPortedForThisTarget);
            return default;
        }

        assert((uint)condition.Code < (uint)jumpKind1.Length);
        var result = jumpKind1[(int)condition.Code];
        assert(result is not EJ_NONE);

        return result;
    }

    public static genTreeOps GetOper(GenCondition condition)
    {
        ReadOnlySpan<genTreeOps> oper;
        if (Configuration.TARGET_XARCH)
        {
            oper = XarchOper;
        }
        else if (Configuration.TARGET_ARM || Configuration.TARGET_ARM64)
        {
            oper = ArmArchOper;
        }
        else
        {
            JitExceptionHelpers.ThrowNotImplementedException(FatalJitErrorCode.ConditionInstructionMappingIsNotPortedForThisTarget);
            return default;
        }

        assert((uint)condition.Code < (uint)oper.Length);
        var result = oper[(int)condition.Code];
        assert(result is GT_NONE or GT_AND or GT_OR);

        return result;
    }

    // UCOMIS sets ZF/PF/CF to 111 for unordered, 000 for greater, 001 for
    // less, and 100 for equal. Conditions sharing the unordered ZF/CF bits
    // therefore need a parity check combined with their ordinary comparison.
    // GT_AND/GT_OR describe short-circuit combinations, not emitted ALU operations.
    private static ReadOnlySpan<emitJumpKind> XarchJumpKind1 => [
        EJ_NONE, // NONE
        EJ_NONE, // 1
        EJ_XARCH_jl, // SLT
        EJ_XARCH_jle, // SLE
        EJ_XARCH_jge, // SGE
        EJ_XARCH_jg, // SGT
        EJ_XARCH_js, // S
        EJ_XARCH_jns, // NS
        EJ_XARCH_je, // EQ
        EJ_XARCH_jne, // NE
        EJ_XARCH_jb, // ULT
        EJ_XARCH_jbe, // ULE
        EJ_XARCH_jae, // UGE
        EJ_XARCH_ja, // UGT
        EJ_XARCH_jb, // C
        EJ_XARCH_jae, // NC
        EJ_XARCH_jnp, // FEQ
        EJ_XARCH_jne, // FNE
        EJ_XARCH_jnp, // FLT
        EJ_XARCH_jnp, // FLE
        EJ_XARCH_jae, // FGE
        EJ_XARCH_ja, // FGT
        EJ_XARCH_jo, // O
        EJ_XARCH_jno, // NO
        EJ_XARCH_je, // FEQU
        EJ_XARCH_jp, // FNEU
        EJ_XARCH_jb, // FLTU
        EJ_XARCH_jbe, // FLEU
        EJ_XARCH_jp, // FGEU
        EJ_XARCH_jp, // FGTU
        EJ_XARCH_jp, // P
        EJ_XARCH_jnp, // NP
    ];

    private static ReadOnlySpan<genTreeOps> XarchOper => [
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_AND,
        GT_NONE,
        GT_AND,
        GT_AND,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_OR,
        GT_NONE,
        GT_NONE,
        GT_OR,
        GT_OR,
        GT_NONE,
        GT_NONE,
    ];

    private static ReadOnlySpan<emitJumpKind> XarchJumpKind2 => [
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_XARCH_je,
        EJ_NONE,
        EJ_XARCH_jb,
        EJ_XARCH_jbe,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_XARCH_jne,
        EJ_NONE,
        EJ_NONE,
        EJ_XARCH_jae,
        EJ_XARCH_ja,
        EJ_NONE,
        EJ_NONE,
    ];

    private static ReadOnlySpan<emitJumpKind> ArmArchJumpKind1 => [
        EJ_NONE, // NONE
        EJ_NONE, // 1
        EJ_ARMARCH_lt, // SLT
        EJ_ARMARCH_le, // SLE
        EJ_ARMARCH_ge, // SGE
        EJ_ARMARCH_gt, // SGT
        EJ_ARMARCH_mi, // S
        EJ_ARMARCH_pl, // NS
        EJ_ARMARCH_eq, // EQ
        EJ_ARMARCH_ne, // NE
        EJ_ARMARCH_lo, // ULT
        EJ_ARMARCH_ls, // ULE
        EJ_ARMARCH_hs, // UGE
        EJ_ARMARCH_hi, // UGT
        EJ_ARMARCH_hs, // C
        EJ_ARMARCH_lo, // NC
        EJ_ARMARCH_eq, // FEQ
        EJ_ARMARCH_gt, // FNE
        EJ_ARMARCH_lo, // FLT
        EJ_ARMARCH_ls, // FLE
        EJ_ARMARCH_ge, // FGE
        EJ_ARMARCH_gt, // FGT
        EJ_ARMARCH_vs, // O
        EJ_ARMARCH_vc, // NO
        EJ_ARMARCH_eq, // FEQU
        EJ_ARMARCH_ne, // FNEU
        EJ_ARMARCH_lt, // FLTU
        EJ_ARMARCH_le, // FLEU
        EJ_ARMARCH_hs, // FGEU
        EJ_ARMARCH_hi, // FGTU
        EJ_NONE, // P
        EJ_NONE, // NP
    ];

    private static ReadOnlySpan<genTreeOps> ArmArchOper => [
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_AND,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_OR,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
        GT_NONE,
    ];

    private static ReadOnlySpan<emitJumpKind> ArmArchJumpKind2 => [
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_ARMARCH_lo,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_ARMARCH_vs,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
        EJ_NONE,
    ];
}
