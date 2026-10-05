// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForJTrue(GenTreeUnOp jtrue)
    {
        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block.Kind is BBJ_COND);

        var op = jtrue.Op1;
        var reg = genConsumeReg(op);
        Emitter.emitIns_J_R(INS_cbnz, op.Type.EmitActualSize, block.TrueTarget, reg);

        var falseTarget = block.FalseTarget;
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }

    public void genCodeForSelect(GenTreeOp tree)
    {
        assert(tree.Oper is GT_SELECT or GT_SELECTCC or GT_SELECT_INC or GT_SELECT_INCCC or
            GT_SELECT_INV or GT_SELECT_INVCC or GT_SELECT_NEG or GT_SELECT_NEGCC);

        GenTree? opcond = null;
        var ins = INS_csel;
        var op1 = tree.Op1;
        var op2 = tree.Op2;

        if (tree.Oper is GT_SELECT_INV or GT_SELECT_INVCC)
        {
            ins = op2 is null ? INS_cinv : INS_csinv;
        }
        else if (tree.Oper is GT_SELECT_NEG or GT_SELECT_NEGCC)
        {
            ins = op2 is null ? INS_cneg : INS_csneg;
        }
        else if (tree.Oper is GT_SELECT_INC or GT_SELECT_INCCC)
        {
            ins = op2 is null ? INS_cinc : INS_csinc;
        }

        if (tree.Oper is GT_SELECT or GT_SELECT_INV or GT_SELECT_NEG)
        {
            opcond = tree.AsConditional().Cond;
            genConsumeRegs(opcond);
        }

        if (op2 is not null)
        {
            var op1Type = genActualType(op1.Type);
            var op2Type = genActualType(op2.Type);
            assert(genTypeSize(op1Type) == genTypeSize(op2Type));
        }

        assert(!op1.IsUsedFromMemory);

        var emit = Emitter;
        GenCondition cond;
        if (opcond is not null)
        {
            emit.emitIns_R_I(INS_cmp, opcond.Type.EmitActualSize, opcond.RegNum, 0);
            cond = new GenCondition(GenCondition.CodeKind.NE);
        }
        else
        {
            assert(tree.Oper is GT_SELECTCC or GT_SELECT_INCCC or GT_SELECT_INVCC or GT_SELECT_NEGCC);
            cond = tree.AsOpCC().Condition;
        }

        assert(!op1.IsContained || op1.IsIntegralConst(0));
        assert(op2 is null || !op2.IsContained || op2.IsIntegralConst(0));

        var targetReg = tree.RegNum;
        var srcReg1 = op1.IsIntegralConst(0) ? REG_ZR : genConsumeReg(op1);
        var prevDesc = GenConditionDesc.Get(cond);
        var attr = emitActualTypeSize(tree);
        regNumber srcReg2;

        if (op2 is null)
        {
            srcReg2 = srcReg1;
            emit.emitIns_R_R_COND(ins, attr, targetReg, srcReg1, JumpKindToInsCond(prevDesc.JumpKind1));
        }
        else
        {
            srcReg2 = op2.IsIntegralConst(0) ? REG_ZR : genConsumeReg(op2);
            emit.emitIns_R_R_R_COND(ins, attr, targetReg, srcReg1, srcReg2, JumpKindToInsCond(prevDesc.JumpKind1));
        }

        // Compound floating-point conditions need a second select for their unordered case.
        if (prevDesc.Oper is GT_AND)
        {
            ins = (ins is INS_csinv or INS_csneg) ? ins : INS_csel;
            emit.emitIns_R_R_R_COND(ins, attr, targetReg, targetReg, srcReg2,
                JumpKindToInsCond(prevDesc.JumpKind2));
        }
        else if (prevDesc.Oper is GT_OR)
        {
            ins = ins is INS_csinc ? ins : INS_csel;
            emit.emitIns_R_R_R_COND(ins, attr, targetReg, srcReg1, targetReg,
                JumpKindToInsCond(prevDesc.JumpKind2));
        }

        _regSet.verifyRegUsed(targetReg);
        genProduceReg(tree);
    }
}
#endif
