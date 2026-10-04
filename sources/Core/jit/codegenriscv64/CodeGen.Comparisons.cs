// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCompare(GenTreeOp tree)
    {
        assert(tree.Oper.IsCompare);

        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Type = genActualType(op1.Type);

        assert(!op1.IsUsedFromMemory);
        assert(!op2.IsUsedFromMemory);

        var cmpSize = EA_SIZE(op1Type.EmitSize);
        assert(cmpSize is EA_4BYTE or EA_8BYTE);

        var emit = Emitter;
        var targetReg = tree.RegNum;

        assert(targetReg != REG_NA);
        assert(tree.Type is not TYP_VOID);

        var isReversed = false;
        if (varTypeIsFloating(op1Type))
        {
            assert(!op1.IsContainedIntOrIImmed && !op2.IsContainedIntOrIImmed);
            assert(op1.Type == op2.Type);

            var oper = tree.Oper;
            isReversed = (tree.Flags & GTF_RELOP_NAN_UN) != 0;
            if (isReversed)
            {
                oper = oper.ReverseRelop;
            }

            if (oper is GT_GT or GT_GE)
            {
                oper = oper.SwapRelop;
                (op1, op2) = (op2, op1);
            }

            var instr = INS_none;
            switch (oper)
            {
                case GT_LT:
                {
                    instr = cmpSize == EA_4BYTE ? INS_flt_s : INS_flt_d;
                    break;
                }
                case GT_LE:
                {
                    instr = cmpSize == EA_4BYTE ? INS_fle_s : INS_fle_d;
                    break;
                }
                case GT_EQ:
                {
                    instr = cmpSize == EA_4BYTE ? INS_feq_s : INS_feq_d;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }

            emit.emitIns_R_R_R(instr, cmpSize, targetReg, op1.RegNum, op2.RegNum);
        }
        else
        {
            var isUnsigned = tree.IsUnsigned;

            var oper = tree.Oper;
            if (oper is GT_EQ or GT_NE)
            {
                assert(op2.IsContainedIntOrIImmed && op2.IsIntegralConst(0));
                oper = oper is GT_EQ ? GT_LE : GT_GT;
                isUnsigned = true;
            }

            if ((oper is GT_LE) && op2.IsContainedIntOrIImmed)
            {
                oper = GT_LT;
                assert(op2.AsIntCon().IconValue == 0);
                op2.AsIntCon().IconValue = 1;
            }

            isReversed = oper is GT_LE or GT_GE;
            if (isReversed)
            {
                oper = oper.ReverseRelop;
            }

            if (oper is GT_GT)
            {
#if DEBUG
                oper = oper.SwapRelop;
#endif
                (op1, op2) = (op2, op1);
            }

            assert(oper is GT_LT);

            assert(!op1.IsContainedIntOrIImmed || op1.IsIntegralConst(0));
            var reg1 = op1.IsContainedIntOrIImmed ? REG_ZERO : op1.RegNum;
            if (op2.IsContainedIntOrIImmed)
            {
                var slti = isUnsigned ? INS_sltiu : INS_slti;
                emit.emitIns_R_R_I(slti, EA_PTRSIZE, targetReg, reg1, op2.AsIntCon().IconValue);
            }
            else
            {
                var slt = isUnsigned ? INS_sltu : INS_slt;
                emit.emitIns_R_R_R(slt, EA_PTRSIZE, targetReg, reg1, op2.RegNum);
            }
        }

        if (isReversed)
        {
            emit.emitIns_R_R_I(INS_xori, EA_8BYTE, targetReg, targetReg, 1);
        }

        genProduceReg(tree);
    }

    public void genCodeForJumpCompare(GenTreeOpCC tree)
    {
        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block!.Kind is BBJ_COND);
        assert(tree.Oper is GT_JCMP);
        assert(tree.Type is TYP_VOID);
        assert(tree.RegNum is REG_NA);

        var op1 = tree.Op1;
        var op2 = tree.Op2;
        assert(!op1.IsUsedFromMemory);
        assert(!op2.IsUsedFromMemory);
        assert(!op1.IsContainedIntOrIImmed || op1.IsIntegralConst(0));
        assert(!op2.IsContainedIntOrIImmed || op2.IsIntegralConst(0));

        var condition = tree.Condition;
        genConsumeOperands(tree);
        var reg1 = op1.IsContainedIntOrIImmed ? REG_R0 : op1.RegNum;
        var reg2 = op2.IsContainedIntOrIImmed ? REG_R0 : op2.RegNum;
        if (condition.Code is GenCondition.CodeKind.SGT or GenCondition.CodeKind.UGT or
            GenCondition.CodeKind.SLE or GenCondition.CodeKind.ULE)
        {
            condition = GenCondition.Swap(condition);
            (reg1, reg2) = (reg2, reg1);
        }

        var ins = INS_invalid;
        switch (condition.Code)
        {
            case GenCondition.CodeKind.EQ:
            {
                ins = INS_beq;
                break;
            }
            case GenCondition.CodeKind.NE:
            {
                ins = INS_bne;
                break;
            }
            case GenCondition.CodeKind.SGE:
            {
                ins = INS_bge;
                break;
            }
            case GenCondition.CodeKind.UGE:
            {
                ins = INS_bgeu;
                break;
            }
            case GenCondition.CodeKind.SLT:
            {
                ins = INS_blt;
                break;
            }
            case GenCondition.CodeKind.ULT:
            {
                ins = INS_bltu;
                break;
            }
            default:
            {
                NO_WAY("unexpected branch condition");
                break;
            }
        }

        assert(Emitter.isGeneralRegister(reg1));
        assert(Emitter.isGeneralRegister(reg2));
        Emitter.emitIns_J_cond_la(ins, block!.TrueTarget, reg1, reg2);

        var falseTarget = block.FalseTarget;
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }
}
#endif
