// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForJTrue(GenTreeUnOp jtrue)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Boolean branch generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block.Kind is BBJ_COND);
        var op = jtrue.Op1;
        var reg = genConsumeReg(op);
        inst_RV_RV(INS_test, reg, reg, op.Type.ActualType);
        inst_JMP(EJ_jne, block.TrueTarget);

        var falseTarget = block.FalseTarget;
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
#endif
    }

    public static instruction JumpKindToCmov(emitJumpKind condition)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Conditional-move mapping requires AMD64.");
#else
        ReadOnlySpan<instruction> table = [
            INS_none, INS_none, INS_cmovo, INS_cmovno, INS_cmovb, INS_cmovae, INS_cmove, INS_cmovne,
            INS_cmovbe, INS_cmova, INS_cmovs, INS_cmovns, INS_cmovp, INS_cmovnp, INS_cmovl, INS_cmovge,
            INS_cmovle, INS_cmovg,
        ];
        assert(unchecked((uint)condition) < (uint)table.Length);

        return table[(int)condition];
#endif
    }

    public void genCodeForSelect(GenTreeOp select)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Conditional selection generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(select.Oper is GT_SELECT or GT_SELECTCC);
        if (select.Oper is GT_SELECT)
        {
            genConsumeRegs(select.AsConditional().Cond);
        }
        genConsumeOperands(select);

        var dstReg = select.RegNum;
        var trueVal = select.Op1;
        var falseVal = select.Op2;
        var condition = new GenCondition(GenCondition.CodeKind.NE);
        if (select.Oper is GT_SELECT)
        {
            var cond = select.AsConditional().Cond;
            Emitter.emitIns_R_R(INS_test, cond.Type.EmitActualSize, cond.RegNum, cond.RegNum);
        }
        else
        {
            condition = select.AsOpCC().Condition;
        }

        if (falseVal.IsUsedFromReg && (falseVal.RegNum == dstReg))
        {
            (trueVal, falseVal) = (falseVal, trueVal);
            condition = GenCondition.Reverse(condition);
        }

        var dstMask = regMaskTP.CreateFromRegNum(dstReg, dstReg.SingleTypeMask);
        if ((trueVal.ContainedRegMask & dstMask) != RBM_NONE)
        {
            (trueVal, falseVal) = (falseVal, trueVal);
            condition = GenCondition.Reverse(condition);
        }

        var desc = GenConditionDesc.Get(condition);
        if ((desc.Oper is GT_AND) && ((falseVal.ContainedRegMask & dstMask) != RBM_NONE))
        {
            (trueVal, falseVal) = (falseVal, trueVal);
            condition = GenCondition.Reverse(condition);
            desc = GenConditionDesc.Get(condition);
        }

        inst_RV_TT(INS_mov, select.Type.EmitSize, dstReg, falseVal);
        assert(!trueVal.IsContained || trueVal.IsUsedFromMemory);
        assert((trueVal.ContainedRegMask & dstMask) == RBM_NONE);
        inst_RV_TT(JumpKindToCmov(desc.JumpKind1), select.Type.EmitSize, dstReg, trueVal);

        if (desc.Oper is GT_AND)
        {
            assert(falseVal.IsUsedFromReg);
            assert((falseVal.ContainedRegMask & dstMask) == RBM_NONE);
            inst_RV_TT(JumpKindToCmov(RyuJitSharp.Emitter.emitReverseJumpKind(desc.JumpKind2)),
                select.Type.EmitSize, dstReg, falseVal);
        }
        else if (desc.Oper is GT_OR)
        {
            assert(trueVal.IsUsedFromReg);
            inst_RV_TT(JumpKindToCmov(desc.JumpKind2), select.Type.EmitSize, dstReg, trueVal);
        }

        genProduceReg(select);
#endif
    }
}
#endif
