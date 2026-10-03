// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
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
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Type = genActualType(op1.Type);
        var op2Type = genActualType(op2.Type);

        assert(!op1.IsUsedFromMemory);
        assert(!op2.IsUsedFromMemory);

        var cmpSize = op1Type.EmitSize;

        assert(op1Type.Size == op2Type.Size);

        var emit = Emitter;
        var targetReg = tree.RegNum;

        if (varTypeIsFloating(op1Type))
        {
            assert(tree.Oper is GT_LT or GT_LE or GT_EQ or GT_NE or GT_GT or GT_GE);
            var isUnordered = (tree.Flags & GTF_RELOP_NAN_UN) != 0;

            if (isUnordered)
            {
                if (tree.Oper is GT_LT)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cult_s : INS_fcmp_cult_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_LE)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cule_s : INS_fcmp_cule_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_EQ)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cueq_s : INS_fcmp_cueq_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_NE)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cune_s : INS_fcmp_cune_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_GT)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cult_s : INS_fcmp_cult_d,
                        cmpSize,
                        op2.RegNum,
                        op1.RegNum,
                        1);
                }
                else if (tree.Oper is GT_GE)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cule_s : INS_fcmp_cule_d,
                        cmpSize,
                        op2.RegNum,
                        op1.RegNum,
                        1);
                }
            }
            else
            {
                if (tree.Oper is GT_LT)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_clt_s : INS_fcmp_clt_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_LE)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cle_s : INS_fcmp_cle_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_EQ)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_ceq_s : INS_fcmp_ceq_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_NE)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cne_s : INS_fcmp_cne_d,
                        cmpSize,
                        op1.RegNum,
                        op2.RegNum,
                        1);
                }
                else if (tree.Oper is GT_GT)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_clt_s : INS_fcmp_clt_d,
                        cmpSize,
                        op2.RegNum,
                        op1.RegNum,
                        1);
                }
                else if (tree.Oper is GT_GE)
                {
                    emit.emitIns_R_R_I(
                        cmpSize == EA_4BYTE ? INS_fcmp_cle_s : INS_fcmp_cle_d,
                        cmpSize,
                        op2.RegNum,
                        op1.RegNum,
                        1);
                }
            }

            if (targetReg != REG_NA)
            {
                assert(!tree.TypeIs(TYP_VOID));
                assert(Emitter.isGeneralRegister(targetReg));

                emit.emitIns_R_I(INS_movcf2gr, EA_PTRSIZE, targetReg, 1);
                genProduceReg(tree);
            }
        }
        else
        {
            assert(targetReg != REG_NA);
            assert(!tree.TypeIs(TYP_VOID));

            assert(!op1.IsContainedIntOrIImmed);
            assert(tree.Oper is GT_LT or GT_LE or GT_EQ or GT_NE or GT_GT or GT_GE);

            var isUnsigned = tree.IsUnsigned;
            var regOp1 = op1.RegNum;

            if (op2.IsContainedIntOrIImmed)
            {
                var imm = op2.AsIntCon().IconValue;

                switch (cmpSize)
                {
                    case EA_4BYTE:
                    {
                        var tmpRegOp1 = REG_R21;
                        assert(regOp1 != tmpRegOp1);
                        if (isUnsigned)
                        {
                            imm = unchecked((nint)(uint)imm);

                            emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, tmpRegOp1, regOp1, 31, 0);
                        }
                        else
                        {
                            imm = unchecked((nint)(int)imm);
                            emit.emitIns_R_R_I(INS_slli_w, EA_8BYTE, tmpRegOp1, regOp1, 0);
                        }

                        regOp1 = tmpRegOp1;
                        break;
                    }
                    case EA_8BYTE:
                    {
                        break;
                    }
                    case EA_1BYTE:
                    {
                        if (isUnsigned)
                        {
                            imm = unchecked((byte)imm);
                        }
                        else
                        {
                            imm = unchecked((sbyte)imm);
                        }

                        break;
                    }
                    default:
                    {
                        unreached();
                        break;
                    }
                }

                if (tree.Oper is GT_LT)
                {
                    if (!isUnsigned && Emitter.isValidSimm12(imm))
                    {
                        emit.emitIns_R_R_I(INS_slti, EA_PTRSIZE, targetReg, regOp1, imm);
                    }
                    else if (isUnsigned && Emitter.isValidUimm11(imm))
                    {
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, regOp1, imm);
                    }
                    else
                    {
                        emit.emitIns_I_la(EA_PTRSIZE, REG_RA, imm);
                        emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_PTRSIZE, targetReg, regOp1, REG_RA);
                    }
                }
                else if (tree.Oper is GT_LE)
                {
                    var incrementedImm = unchecked(imm + 1);
                    if (!isUnsigned && Emitter.isValidSimm12(incrementedImm))
                    {
                        emit.emitIns_R_R_I(INS_slti, EA_PTRSIZE, targetReg, regOp1, incrementedImm);
                    }
                    else if (isUnsigned && Emitter.isValidUimm11(incrementedImm) && (imm != ~(nint)0))
                    {
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, regOp1, incrementedImm);
                    }
                    else
                    {
                        assert(!(!isUnsigned && (imm == nint.MaxValue)));
                        if (isUnsigned && (imm == ~(nint)0))
                        {
                            emit.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, targetReg, REG_R0, 1);
                        }
                        else
                        {
                            emit.emitIns_I_la(EA_PTRSIZE, REG_RA, incrementedImm);
                            emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_PTRSIZE, targetReg, regOp1, REG_RA);
                        }
                    }
                }
                else if (tree.Oper is GT_GT)
                {
                    var incrementedImm = unchecked(imm + 1);
                    if (!isUnsigned && Emitter.isValidSimm12(incrementedImm))
                    {
                        emit.emitIns_R_R_I(INS_slti, EA_PTRSIZE, targetReg, regOp1, incrementedImm);
                        emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, targetReg, 1);
                    }
                    else if (isUnsigned && Emitter.isValidUimm11(incrementedImm) && (imm != ~(nint)0))
                    {
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, regOp1, incrementedImm);
                        emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, targetReg, 1);
                    }
                    else
                    {
                        emit.emitIns_I_la(EA_PTRSIZE, REG_RA, imm);
                        emit.emitIns_R_R_R(IsUnsigned ? INS_sltu : INS_slt, EA_PTRSIZE, targetReg, REG_RA, regOp1);
                    }
                }
                else if (tree.Oper is GT_GE)
                {
                    if (!isUnsigned && Emitter.isValidSimm12(imm))
                    {
                        emit.emitIns_R_R_I(INS_slti, EA_PTRSIZE, targetReg, regOp1, imm);
                    }
                    else if (isUnsigned && Emitter.isValidUimm11(imm))
                    {
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, regOp1, imm);
                    }
                    else
                    {
                        emit.emitIns_I_la(EA_PTRSIZE, REG_RA, imm);
                        emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_PTRSIZE, targetReg, regOp1, REG_RA);
                    }

                    emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, targetReg, 1);
                }
                else if (tree.Oper is GT_NE)
                {
                    if (imm == 0)
                    {
                        emit.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, targetReg, REG_R0, regOp1);
                    }
                    else if (Emitter.isValidUimm12(imm))
                    {
                        emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, regOp1, imm);
                        emit.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, targetReg, REG_R0, targetReg);
                    }
                    else if (Emitter.isValidSimm12(imm) && (imm != -2048))
                    {
                        var ins = cmpSize == EA_4BYTE ? INS_addi_w : INS_addi_d;
                        emit.emitIns_R_R_I(ins, EA_PTRSIZE, targetReg, regOp1, -imm);
                        emit.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, targetReg, REG_R0, targetReg);
                    }
                    else
                    {
                        emit.emitIns_I_la(EA_PTRSIZE, REG_RA, imm);
                        emit.emitIns_R_R_R(INS_xor, EA_PTRSIZE, targetReg, regOp1, REG_RA);
                        emit.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, targetReg, REG_R0, targetReg);
                    }
                }
                else if (tree.Oper is GT_EQ)
                {
                    if (imm == 0)
                    {
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, regOp1, 1);
                    }
                    else if (Emitter.isValidUimm12(imm))
                    {
                        emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, regOp1, imm);
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, targetReg, 1);
                    }
                    else if (Emitter.isValidSimm12(imm) && (imm != -2048))
                    {
                        var ins = cmpSize == EA_4BYTE ? INS_addi_w : INS_addi_d;
                        emit.emitIns_R_R_I(ins, EA_PTRSIZE, targetReg, regOp1, -imm);
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, targetReg, 1);
                    }
                    else
                    {
                        emit.emitIns_I_la(EA_PTRSIZE, REG_RA, imm);
                        emit.emitIns_R_R_R(INS_xor, EA_PTRSIZE, targetReg, regOp1, REG_RA);
                        emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, targetReg, 1);
                    }
                }
            }
            else
            {
                var regOp2 = op2.RegNum;

                if (cmpSize == EA_4BYTE)
                {
                    var tmpRegOp1 = REG_RA;
                    var tmpRegOp2 = REG_R21;
                    assert(regOp1 != tmpRegOp2);
                    assert(regOp2 != tmpRegOp2);

                    if (isUnsigned)
                    {
                        emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, tmpRegOp1, regOp1, 31, 0);
                        emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, tmpRegOp2, regOp2, 31, 0);
                    }
                    else
                    {
                        emit.emitIns_R_R_I(INS_slli_w, EA_8BYTE, tmpRegOp1, regOp1, 0);
                        emit.emitIns_R_R_I(INS_slli_w, EA_8BYTE, tmpRegOp2, regOp2, 0);
                    }

                    regOp1 = tmpRegOp1;
                    regOp2 = tmpRegOp2;
                }

                if (tree.Oper is GT_LT)
                {
                    emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_8BYTE, targetReg, regOp1, regOp2);
                }
                else if (tree.Oper is GT_LE)
                {
                    emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_8BYTE, targetReg, regOp2, regOp1);
                    emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, targetReg, 1);
                }
                else if (tree.Oper is GT_GT)
                {
                    emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_8BYTE, targetReg, regOp2, regOp1);
                }
                else if (tree.Oper is GT_GE)
                {
                    emit.emitIns_R_R_R(isUnsigned ? INS_sltu : INS_slt, EA_8BYTE, targetReg, regOp1, regOp2);
                    emit.emitIns_R_R_I(INS_xori, EA_PTRSIZE, targetReg, targetReg, 1);
                }
                else if (tree.Oper is GT_NE)
                {
                    emit.emitIns_R_R_R(INS_xor, EA_PTRSIZE, targetReg, regOp1, regOp2);
                    emit.emitIns_R_R_R(INS_sltu, EA_PTRSIZE, targetReg, REG_R0, targetReg);
                }
                else if (tree.Oper is GT_EQ)
                {
                    emit.emitIns_R_R_R(INS_xor, EA_PTRSIZE, targetReg, regOp1, regOp2);
                    emit.emitIns_R_R_I(INS_sltui, EA_PTRSIZE, targetReg, targetReg, 1);
                }
            }

            genProduceReg(tree);
        }
    }

    public void genCodeForJumpCompare(GenTreeOpCC tree)
    {
        var currentBlock = _compiler.compCurBB;
        assert(currentBlock is not null);
        var block = currentBlock!;
        assert(block.KindIs(BBJ_COND));
        assert(tree.Oper is GT_JCMP);
        assert(!varTypeIsFloating(tree.Type));
        assert(tree.TypeIs(TYP_VOID));
        assert(tree.RegNum == REG_NA);

        var op1 = tree.Op1;
        var op2 = tree.Op2;
        assert(!op1.IsUsedFromMemory);
        assert(!op2.IsUsedFromMemory);
        assert(!op1.IsContainedIntOrIImmed);

        var op1Type = genActualType(op1.Type);
        var op2Type = genActualType(op2.Type);
        assert(genTypeSize(op1Type) == genTypeSize(op2Type));
        assert(varTypeIsIntegralOrI(op1Type));

        genConsumeOperands(tree);

        var emit = Emitter;
        instruction ins = INS_invalid;
        var regs = 0;
        var cond = tree.Condition;
        var cmpSize = (emitAttr)genTypeSize(op1Type);
        var regOp1 = op1.RegNum;

        if (op2.IsContainedIntOrIImmed)
        {
            var imm = op2.AsIntCon().IconValue;

            if (imm != 0)
            {
                switch (cmpSize)
                {
                    case EA_4BYTE:
                    {
                        assert(regOp1 != REG_R21);
                        if (cond.IsUnsigned)
                        {
                            imm = unchecked((nint)(uint)imm);
                            emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, REG_R21, regOp1, 31, 0);
                        }
                        else
                        {
                            imm = unchecked((nint)(int)imm);
                            emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, REG_R21, regOp1, 0);
                        }
                        regOp1 = REG_R21;
                        break;
                    }
                    case EA_8BYTE:
                    {
                        break;
                    }
                    case EA_1BYTE:
                    {
                        imm = cond.IsUnsigned
                            ? unchecked((nint)(byte)imm)
                            : unchecked((nint)(sbyte)imm);
                        break;
                    }
                    default:
                    {
                        unreached();
                        break;
                    }
                }

                var con = op2.AsIntCon();
                var attr = emitActualTypeSize(op2Type);
                if (con.ImmedValNeedsReloc(_compiler))
                {
                    attr = EA_SET_FLG(attr, EA_CNS_RELOC_FLG);
                }

                if (op2Type is TYP_BYREF)
                {
                    attr = EA_SET_FLG(attr, EA_BYREF_FLG);
                }

#if DEBUG
                instGen_Set_Reg_To_Imm(attr, REG_RA, imm, targetHandle: unchecked((nuint)con.TargetHandle),
                    gtFlags: con.Flags);
#else
                instGen_Set_Reg_To_Imm(attr, REG_RA, imm);
#endif
                _regSet.verifyRegUsed(REG_RA);
                regs = (int)REG_RA << 5;
            }
            else if (cmpSize is EA_4BYTE)
            {
                assert(regOp1 != REG_R21);
                if (cond.IsUnsigned)
                {
                    emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, REG_R21, regOp1, 31, 0);
                }
                else
                {
                    emit.emitIns_R_R_I(INS_slli_w, EA_8BYTE, REG_R21, regOp1, 0);
                }
                regOp1 = REG_R21;
            }

            switch (cond.Code)
            {
                case GenCondition.CodeKind.EQ:
                {
                    regs |= (int)regOp1;
                    ins = imm != 0 ? INS_beq : INS_beqz;
                    break;
                }
                case GenCondition.CodeKind.NE:
                {
                    regs |= (int)regOp1;
                    ins = imm != 0 ? INS_bne : INS_bnez;
                    break;
                }
                case GenCondition.CodeKind.UGE:
                case GenCondition.CodeKind.SGE:
                {
                    regs |= (int)regOp1;
                    ins = cond.IsUnsigned ? INS_bgeu : INS_bge;
                    break;
                }
                case GenCondition.CodeKind.UGT:
                case GenCondition.CodeKind.SGT:
                {
                    regs = imm != 0 ? (((int)regOp1 << 5) | (int)REG_RA) : ((int)regOp1 << 5);
                    ins = cond.IsUnsigned ? INS_bltu : INS_blt;
                    break;
                }
                case GenCondition.CodeKind.ULT:
                case GenCondition.CodeKind.SLT:
                {
                    regs |= (int)regOp1;
                    ins = cond.IsUnsigned ? INS_bltu : INS_blt;
                    break;
                }
                case GenCondition.CodeKind.ULE:
                case GenCondition.CodeKind.SLE:
                {
                    regs = imm != 0 ? (((int)regOp1 << 5) | (int)REG_RA) : ((int)regOp1 << 5);
                    ins = cond.IsUnsigned ? INS_bgeu : INS_bge;
                    break;
                }
                default:
                {
                    NO_WAY("unexpected condition type");
                    break;
                }
            }
        }
        else
        {
            var regOp2 = op2.RegNum;
            if (cmpSize is EA_4BYTE)
            {
                var tmpRegOp1 = REG_RA;
                var tmpRegOp2 = REG_R21;
                assert(regOp1 != tmpRegOp2);
                assert(regOp2 != tmpRegOp2);

                if (cond.IsUnsigned)
                {
                    emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, tmpRegOp1, regOp1, 31, 0);
                    emit.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, tmpRegOp2, regOp2, 31, 0);
                }
                else
                {
                    emit.emitIns_R_R_I(INS_slli_w, EA_8BYTE, tmpRegOp1, regOp1, 0);
                    emit.emitIns_R_R_I(INS_slli_w, EA_8BYTE, tmpRegOp2, regOp2, 0);
                }

                regOp1 = tmpRegOp1;
                regOp2 = tmpRegOp2;
            }

            switch (cond.Code)
            {
                case GenCondition.CodeKind.EQ:
                {
                    regs = ((int)regOp1 << 5) | (int)regOp2;
                    ins = INS_beq;
                    break;
                }
                case GenCondition.CodeKind.NE:
                {
                    regs = ((int)regOp1 << 5) | (int)regOp2;
                    ins = INS_bne;
                    break;
                }
                case GenCondition.CodeKind.UGE:
                case GenCondition.CodeKind.SGE:
                {
                    regs = (int)regOp1 | ((int)regOp2 << 5);
                    ins = cond.IsUnsigned ? INS_bgeu : INS_bge;
                    break;
                }
                case GenCondition.CodeKind.UGT:
                case GenCondition.CodeKind.SGT:
                {
                    regs = ((int)regOp1 << 5) | (int)regOp2;
                    ins = cond.IsUnsigned ? INS_bltu : INS_blt;
                    break;
                }
                case GenCondition.CodeKind.ULT:
                case GenCondition.CodeKind.SLT:
                {
                    regs = (int)regOp1 | ((int)regOp2 << 5);
                    ins = cond.IsUnsigned ? INS_bltu : INS_blt;
                    break;
                }
                case GenCondition.CodeKind.ULE:
                case GenCondition.CodeKind.SLE:
                {
                    regs = ((int)regOp1 << 5) | (int)regOp2;
                    ins = cond.IsUnsigned ? INS_bgeu : INS_bge;
                    break;
                }
                default:
                {
                    NO_WAY("unexpected condition type-regs");
                    break;
                }
            }
        }

        assert(ins != INS_invalid);
        assert(regs != 0);
        emit.emitIns_J(ins, block.TrueTarget, regs);

        var falseTarget = block.FalseTarget;
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }
}
#endif
