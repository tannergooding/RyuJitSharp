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
}
#endif
