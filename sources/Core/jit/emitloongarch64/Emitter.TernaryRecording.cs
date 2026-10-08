// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Emitter
{
    public regNumber emitInsBinary(instruction ins, emitAttr attr, GenTree dst, GenTree src)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED,
            "LoongArch64 binary instruction recording is unused and not ported.");
    }

    // The caller consumes non-contained sources and produces the destination.
    public regNumber emitInsTernary(instruction ins, emitAttr attr, GenTree dst, GenTree src1, GenTree src2)
    {
        assert(!dst.IsContained);

        var intConst = (GenTreeIntConCommon?)null;
        var nonIntReg = src1;
        var needCheckOv = dst.HasOverflowCheckEx;

        if (varTypeIsFloating(dst.Type))
        {
            assert(!src1.IsContained);
            assert(!src2.IsContained);
        }
        else
        {
            assert(!src2.IsContained || src2.IsContainedIntOrIImmed);

            if (src2.IsContainedIntOrIImmed)
            {
                intConst = src2.AsIntConCommon();
                nonIntReg = src1;
            }
            else if (dst.Oper.IsCommutative)
            {
                assert(!src1.IsContained || src1.IsContainedIntOrIImmed);

                if (src1.IsContainedIntOrIImmed)
                {
                    assert(!src2.IsContainedIntOrIImmed);
                    intConst = src1.AsIntConCommon();
                    nonIntReg = src2;
                }
            }
            else
            {
                assert(!src1.IsContained);
            }
        }

#if DEBUG
        if (needCheckOv)
        {
            switch (ins)
            {
                case INS_add_d:
                {
                    assert(attr == EA_8BYTE);
                    break;
                }
                case INS_add_w:
                {
                    assert(attr == EA_4BYTE);
                    break;
                }
                case INS_addi_d:
                case INS_addi_w:
                {
                    assert(intConst is not null);
                    break;
                }
                case INS_sub_d:
                {
                    assert(attr == EA_8BYTE);
                    break;
                }
                case INS_sub_w:
                {
                    assert(attr == EA_4BYTE);
                    break;
                }
                case INS_mul_d:
                case INS_mulh_d:
                case INS_mulh_du:
                {
                    assert(attr == EA_8BYTE);
                    assert(intConst is null);
                    break;
                }
                case INS_mul_w:
                case INS_mulw_d_w:
                case INS_mulh_w:
                case INS_mulh_wu:
                case INS_mulw_d_wu:
                {
                    assert(attr == EA_4BYTE);
                    assert(intConst is null);
                    break;
                }
                default:
                {
                    assert(false, $"LOONGARCH64-Invalid ins for overflow check: {ins}");
                    break;
                }
            }
        }
#endif

        var dstReg = dst.RegNum;

        if (intConst is not null)
        {
            var imm = intConst.IconValue;
            if (ins is INS_andi or INS_ori or INS_xori)
            {
                assert(isValidUimm12(imm));
            }
            else
            {
                assert(isValidSimm12(imm));
            }

            if (ins is INS_sub_d)
            {
                assert(attr == EA_8BYTE);
                assert(imm != -2048);
                ins = INS_addi_d;
                imm = -imm;
            }
            else if (ins is INS_sub_w)
            {
                assert(attr == EA_4BYTE);
                assert(imm != -2048);
                ins = INS_addi_w;
                imm = -imm;
            }

            assert(ins is INS_addi_d or INS_addi_w or INS_andi or INS_ori or INS_xori);

            if (needCheckOv)
            {
                emitIns_R_R_I(INS_ori, attr, REG_R21, nonIntReg.RegNum, 0);
            }

            emitIns_R_R_I(ins, attr, dstReg, nonIntReg.RegNum, imm);

            if (needCheckOv)
            {
                if (ins is INS_addi_d or INS_addi_w)
                {
                    if (dst.AsOp().IsUnsigned)
                    {
                        codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bltu, dstReg, null, REG_R21);
                    }
                    else if (imm > 0)
                    {
                        var tmpLabel = codeGen.genCreateTempLabel();
                        emitIns_J_cond_la(INS_bge, tmpLabel, REG_R0, REG_R21);
                        emitIns_R_R_I(INS_slti, EA_PTRSIZE, REG_R21, dstReg, imm);
                        codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, REG_R21);
                        codeGen.genDefineTempLabel(tmpLabel);
                    }
                    else if (imm < 0)
                    {
                        var tmpLabel = codeGen.genCreateTempLabel();
                        emitIns_J_cond_la(INS_bge, tmpLabel, REG_R21, REG_R0);
                        emitIns_R_R_I(INS_addi_d, attr, REG_R21, REG_R0, imm);
                        codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_blt, REG_R21, null, dstReg);
                        codeGen.genDefineTempLabel(tmpLabel);
                    }
                }
                else
                {
                    assert(false, "unimplemented on LOONGARCH yet");
                }
            }
        }
        else if (varTypeIsFloating(dst.Type))
        {
            emitIns_R_R_R(ins, attr, dstReg, src1.RegNum, src2.RegNum);
        }
        else if (dst.OperIs(GT_MUL))
        {
            if (!needCheckOv)
            {
                emitIns_R_R_R(ins, attr, dstReg, src1.RegNum, src2.RegNum);
            }
            else
            {
                assert(REG_R21 != dstReg);
                assert(REG_R21 != src1.RegNum);
                assert(REG_R21 != src2.RegNum);
                assert(REG_RA != dstReg);
                assert(REG_RA != src1.RegNum);
                assert(REG_RA != src2.RegNum);

                var isUnsigned = dst.AsOp().IsUnsigned;
                instruction highIns;
                if (attr == EA_8BYTE)
                {
                    highIns = isUnsigned ? INS_mulh_du : INS_mulh_d;
                }
                else
                {
                    highIns = isUnsigned ? INS_mulh_wu : INS_mulh_w;
                }

                emitIns_R_R_R(highIns, EA_8BYTE, REG_R21, src1.RegNum, src2.RegNum);
                emitIns_R_R_R(ins, attr, dstReg, src1.RegNum, src2.RegNum);

                regNumber compareReg;
                if (isUnsigned)
                {
                    compareReg = REG_R0;
                }
                else
                {
                    var shift = (EA_SIZE(attr) == EA_8BYTE) ? 63 : 31;
                    emitIns_R_R_I(
                        (EA_SIZE(attr) == EA_8BYTE) ? INS_srai_d : INS_srai_w,
                        attr,
                        REG_RA,
                        dstReg,
                        shift);
                    compareReg = REG_RA;
                }

                codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, REG_R21, null, compareReg);
            }
        }
        else if (dst.OperIs(GT_AND, GT_AND_NOT) || dst.OperIs(GT_OR, GT_XOR))
        {
            emitIns_R_R_R(ins, attr, dstReg, src1.RegNum, src2.RegNum);

            // TODO-LOONGARCH64-CQ: sign extending dst for 32-bit data is too conservative.
            if (EA_SIZE(attr) == EA_4BYTE)
            {
                emitIns_R_R_I(INS_slli_w, attr, dstReg, dstReg, 0);
            }
        }
        else
        {
            assert(dst.OperIs(GT_ADD, GT_SUB));

            var regOp1 = src1.RegNum;
            var regOp2 = src2.RegNum;
            var saveOperReg1 = REG_NA;
            var saveOperReg2 = REG_NA;

            if (needCheckOv)
            {
                assert(!varTypeIsFloating(dst.Type));
                assert(REG_R21 != dstReg);
                assert(REG_RA != dstReg);

                if (dst.OperIs(GT_ADD))
                {
                    saveOperReg1 = regOp1;
                    if (dstReg == regOp1)
                    {
                        saveOperReg1 = regOp2;
                        if (regOp1 == regOp2)
                        {
                            assert(REG_R21 != regOp1);
                            assert(REG_RA != regOp1);
                            emitIns_R_R_I(INS_ori, attr, REG_R21, regOp1, 0);
                            saveOperReg1 = REG_R21;
                        }
                    }
                }
                else if (dstReg == regOp1)
                {
                    assert(REG_R21 != regOp1);
                    assert(REG_RA != regOp1);
                    saveOperReg1 = REG_R21;
                    emitIns_R_R_I(INS_ori, attr, REG_R21, regOp1, 0);
                }
                else
                {
                    saveOperReg1 = regOp1;
                }

                if (!dst.AsOp().IsUnsigned)
                {
                    saveOperReg2 = codeGen.InternalRegisters.GetSingle(dst);
                    assert((saveOperReg2 != REG_RA) && (saveOperReg2 != REG_R21));
                    assert(REG_RA != regOp1);
                    assert(saveOperReg2 != regOp2);

                    var shift = (attr == EA_4BYTE) ? 31 : 63;
                    if (dst.OperIs(GT_ADD))
                    {
                        emitIns_R_R_I(INS_srli_d, attr, REG_RA, regOp1, shift);
                    }

                    emitIns_R_R_I(INS_srli_d, attr, saveOperReg2, regOp2, shift);
                }
            }

            emitIns_R_R_R(ins, attr, dstReg, regOp1, regOp2);

            if (needCheckOv)
            {
                if (dst.AsOp().IsUnsigned)
                {
                    codeGen.genJumpToThrowHlpBlk_la(
                        SCK_OVERFLOW,
                        INS_bltu,
                        dst.OperIs(GT_ADD) ? dstReg : saveOperReg1,
                        null,
                        dst.OperIs(GT_ADD) ? saveOperReg1 : dstReg);
                }
                else
                {
                    if (dst.OperIs(GT_SUB))
                    {
                        var shift = (attr == EA_4BYTE) ? 31 : 63;
                        emitIns_R_R_I(INS_srli_d, attr, REG_RA, dstReg, shift);
                    }

                    emitIns_R_R_R(INS_xor, attr, REG_RA, REG_RA, saveOperReg2);
                    if (attr == EA_4BYTE)
                    {
                        emitIns_R_R_I(INS_andi, attr, REG_RA, REG_RA, 1);
                        emitIns_R_R_I(INS_andi, attr, saveOperReg2, saveOperReg2, 1);
                    }

                    var tmpLabel1 = codeGen.genCreateTempLabel();
                    var tmpLabel2 = codeGen.genCreateTempLabel();
                    var tmpLabel3 = codeGen.genCreateTempLabel();

                    emitIns_J_cond_la(INS_bne, tmpLabel1, REG_RA, REG_R0);
                    emitIns_J_cond_la(INS_bne, tmpLabel3, saveOperReg2, REG_R0);
                    emitIns_J_cond_la(
                        INS_bge,
                        tmpLabel1,
                        dst.OperIs(GT_ADD) ? dstReg : saveOperReg1,
                        dst.OperIs(GT_ADD) ? saveOperReg1 : dstReg);

                    codeGen.genDefineTempLabel(tmpLabel2);
                    codeGen.genJumpToThrowHlpBlk(EJ_jmp, SCK_OVERFLOW);
                    codeGen.genDefineTempLabel(tmpLabel3);

                    emitIns_J_cond_la(
                        INS_blt,
                        tmpLabel2,
                        dst.OperIs(GT_ADD) ? saveOperReg1 : dstReg,
                        dst.OperIs(GT_ADD) ? dstReg : saveOperReg1);

                    codeGen.genDefineTempLabel(tmpLabel1);
                }
            }
        }

        return dstReg;
    }
}
#endif
