// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Emitter
{
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
                case INS_add:
                {
                    assert(attr == EA_8BYTE);
                    break;
                }
                case INS_addw:
                {
                    assert(attr == EA_4BYTE);
                    break;
                }
                case INS_addi:
                case INS_addiw:
                {
                    assert(intConst is not null);
                    break;
                }
                case INS_sub:
                {
                    assert(attr == EA_8BYTE);
                    break;
                }
                case INS_subw:
                {
                    assert(attr == EA_4BYTE);
                    break;
                }
                case INS_mul:
                case INS_mulh:
                case INS_mulhu:
                {
                    assert(attr == EA_8BYTE);
                    assert(intConst is null);
                    break;
                }
                case INS_mulw:
                {
                    assert(attr == EA_4BYTE);
                    assert(intConst is null);
                    break;
                }
                default:
                {
                    assert(false, $"RISCV64-Invalid ins for overflow check: {ins}");
                    break;
                }
            }
        }
#endif

        var dstReg = dst.RegNum;
        var src1Reg = src1.RegNum;
        var src2Reg = src2.RegNum;

        if (intConst is not null)
        {
            var imm = intConst.IconValue;
            assert(isValidSimm12(imm));

            if (ins is INS_sub)
            {
                assert(attr == EA_8BYTE);
                assert(imm != -2048);
                ins = INS_addi;
                imm = -imm;
            }
            else if (ins is INS_subw)
            {
                assert(attr == EA_4BYTE);
                assert(imm != -2048);
                ins = INS_addiw;
                imm = -imm;
            }
            else if (ins is INS_bseti or INS_bclri or INS_bexti or INS_binvi)
            {
                // Use base instructions where possible:
                // bexti rd, rs, 0 is equivalent to andi rd, rs, 1.
                // bseti/bclri/binvi with imm < 11 can use ori/andi/xori instead.
                var minBitIndex = ins is INS_bexti ? 1 : 11;
                var maxBitIndex = (int)EA_SIZE(src1.Type.EmitActualSize) * 8;
                if ((ins is not INS_bexti) && (attr == EA_4BYTE))
                {
                    // A single-bit operation on the sign bit changes sign extension.
                    maxBitIndex--;
                }

                assert(imm >= minBitIndex);
                assert(imm < maxBitIndex);
            }

            assert(ins is INS_addi or INS_addiw or INS_andi or INS_ori or INS_xori or
                INS_bseti or INS_bclri or INS_bexti or INS_binvi);

            var tempReg = needCheckOv ? codeGen.InternalRegisters.Extract(dst) : REG_NA;

            if (needCheckOv)
            {
                emitIns_R_R(INS_mov, attr, tempReg, nonIntReg.RegNum);
            }

            emitIns_R_R_I(ins, attr, dstReg, nonIntReg.RegNum, unchecked((int)imm));

            if (needCheckOv)
            {
                assert(ins is INS_addi or INS_addiw);

                if (dst.AsOp().IsUnsigned)
                {
                    codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bltu, dstReg, null, tempReg);
                }
                else if (imm > 0)
                {
                    // B > 0 and C > 0, if A < B, goto overflow.
                    var tmpLabel = codeGen.genCreateTempLabel();
                    emitIns_J_cond_la(INS_bge, tmpLabel, REG_R0, tempReg);
                    emitIns_R_R_I(INS_slti, EA_PTRSIZE, tempReg, dstReg, unchecked((int)imm));

                    codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg);

                    codeGen.genDefineTempLabel(tmpLabel);
                }
                else if (imm < 0)
                {
                    // B < 0 and C < 0, if A > B, goto overflow.
                    var tmpLabel = codeGen.genCreateTempLabel();
                    emitIns_J_cond_la(INS_bge, tmpLabel, tempReg, REG_R0);
                    emitIns_R_R_I(INS_addi, attr, tempReg, REG_R0, unchecked((int)imm));

                    codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_blt, tempReg, null, dstReg);

                    codeGen.genDefineTempLabel(tmpLabel);
                }
            }
        }
        else if (varTypeIsFloating(dst.Type))
        {
            emitIns_R_R_R(ins, attr, dstReg, src1Reg, src2Reg);
        }
        else
        {
            var tempReg = needCheckOv ? codeGen.InternalRegisters.Extract(dst) : REG_NA;

            switch (dst.Oper)
            {
                case GT_MUL:
                {
                    if (!needCheckOv && !dst.AsOp().IsUnsigned)
                    {
                        emitIns_R_R_R(ins, attr, dstReg, src1Reg, src2Reg);
                    }
                    else
                    {
                        if (needCheckOv)
                        {
                            assert(tempReg != dstReg);
                            assert(tempReg != src1Reg);
                            assert(tempReg != src2Reg);

                            assert(REG_RA != dstReg);
                            assert(REG_RA != src1Reg);
                            assert(REG_RA != src2Reg);

                            if (dst.AsOp().IsUnsigned)
                            {
                                if (attr == EA_4BYTE)
                                {
                                    emitIns_R_R_I(INS_slli, EA_8BYTE, tempReg, src1Reg, 32);
                                    emitIns_R_R_I(INS_slli, EA_8BYTE, REG_RA, src2Reg, 32);
                                    emitIns_R_R_R(INS_mulhu, EA_8BYTE, tempReg, tempReg, REG_RA);
                                    emitIns_R_R_I(INS_srai, attr, tempReg, tempReg, 32);
                                }
                                else
                                {
                                    emitIns_R_R_R(INS_mulhu, attr, tempReg, src1Reg, src2Reg);
                                }
                            }
                            else if (attr == EA_4BYTE)
                            {
                                emitIns_R_R_R(INS_mul, EA_8BYTE, tempReg, src1Reg, src2Reg);
                                emitIns_R_R_I(INS_srai, attr, tempReg, tempReg, 32);
                            }
                            else
                            {
                                emitIns_R_R_R(INS_mulh, attr, tempReg, src1Reg, src2Reg);
                            }
                        }

                        // n * n bytes will store n bytes result.
                        emitIns_R_R_R(ins, attr, dstReg, src1Reg, src2Reg);

                        if (needCheckOv)
                        {
                            assert(tempReg != dstReg);
                            assert(tempReg != src1Reg);
                            assert(tempReg != src2Reg);

                            if (dst.AsOp().IsUnsigned)
                            {
                                codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg);
                            }
                            else
                            {
                                var tempReg2 = codeGen.InternalRegisters.Extract(dst);
                                assert(tempReg2 != dstReg);
                                assert(tempReg2 != src1Reg);
                                assert(tempReg2 != src2Reg);

                                var shift = EA_SIZE(attr) == EA_8BYTE ? 63 : 31;
                                var shiftIns = EA_SIZE(attr) == EA_8BYTE ? INS_srai : INS_sraiw;
                                emitIns_R_R_I(shiftIns, attr, tempReg2, dstReg, shift);
                                codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, INS_bne, tempReg, null, tempReg2);
                            }
                        }
                    }

                    break;
                }

                case GT_AND:
                case GT_AND_NOT:
                case GT_OR:
                case GT_OR_NOT:
                case GT_XOR:
                case GT_XOR_NOT:
                case GT_BIT_SET:
                case GT_BIT_CLEAR:
                case GT_BIT_INVERT:
                {
                    emitIns_R_R_R(ins, attr, dstReg, src1Reg, src2Reg);

                    // TODO-RISCV64-CQ: sign-extending 32-bit results is conservative.
                    if (EA_SIZE(attr) == EA_4BYTE)
                    {
                        emitIns_R_R(INS_sext_w, attr, dstReg, dstReg);
                    }

                    break;
                }

                case GT_ADD:
                case GT_SUB:
                {
                    var regOp1 = src1Reg;
                    var regOp2 = src2Reg;
                    var saveOperReg1 = REG_NA;
                    var saveOperReg2 = REG_NA;

                    if (dst.AsOp().IsUnsigned && (attr == EA_8BYTE))
                    {
                        if (src1.Type is TYP_INT)
                        {
                            emitIns_R_R_I(INS_slli, EA_8BYTE, regOp1, regOp1, 32);
                            emitIns_R_R_I(INS_srli, EA_8BYTE, regOp1, regOp1, 32);
                        }

                        if (src2.Type is TYP_INT)
                        {
                            emitIns_R_R_I(INS_slli, EA_8BYTE, regOp2, regOp2, 32);
                            emitIns_R_R_I(INS_srli, EA_8BYTE, regOp2, regOp2, 32);
                        }
                    }

                    if (needCheckOv)
                    {
                        assert(!varTypeIsFloating(dst.Type));
                        assert(tempReg != dstReg);

                        if (dstReg == regOp1)
                        {
                            assert(tempReg != regOp1);
                            saveOperReg1 = tempReg;
                            saveOperReg2 = regOp1 == regOp2 ? tempReg : regOp2;
                            emitIns_R_R(INS_mov, attr, tempReg, regOp1);
                        }
                        else if (dstReg == regOp2)
                        {
                            assert(tempReg != regOp2);
                            saveOperReg1 = regOp1;
                            saveOperReg2 = tempReg;
                            emitIns_R_R(INS_mov, attr, tempReg, regOp2);
                        }
                        else
                        {
                            saveOperReg1 = regOp1;
                            saveOperReg2 = regOp2;
                        }
                    }

                    emitIns_R_R_R(ins, attr, dstReg, regOp1, regOp2);

                    /*
                        Check if A = B + C.
                        ADD: A = B + C
                        SUB: B = A - C
                        For addition, dst = src1 + src2.
                        For subtraction, src1 = dst + src2.
                    */
                    if (needCheckOv)
                    {
                        var resultReg = REG_NA;

                        if (dst.OperIs(GT_ADD))
                        {
                            resultReg = dstReg;
                            regOp1 = saveOperReg1;
                            regOp2 = saveOperReg2;
                        }
                        else
                        {
                            resultReg = saveOperReg1;
                            regOp1 = dstReg;
                            regOp2 = saveOperReg2;
                        }

                        var branchIns = INS_none;
                        var branchReg1 = REG_NA;
                        var branchReg2 = REG_NA;

                        if (dst.AsOp().IsUnsigned)
                        {
                            // If A < B, there was overflow.
                            branchIns = INS_bltu;
                            branchReg1 = resultReg;
                            branchReg2 = regOp1;
                        }
                        else
                        {
                            var tempReg1 = codeGen.InternalRegisters.GetSingle(dst);
                            branchIns = INS_bne;

                            if (attr == EA_4BYTE)
                            {
                                assert(src1.Type is not TYP_LONG);
                                assert(src2.Type is not TYP_LONG);

                                emitIns_R_R_R(INS_add, attr, tempReg1, regOp1, regOp2);

                                // A 64-bit sum differing from the 32-bit sum indicates overflow.
                                branchReg1 = resultReg;
                                branchReg2 = tempReg1;
                            }
                            else
                            {
                                assert(attr == EA_8BYTE);
                                assert(tempReg != tempReg1);

                                // The second temporary is already dead at this point.
                                var tempReg2 = tempReg;
                                emitIns_R_R_R(INS_slt, attr, tempReg1, resultReg, regOp1);
                                emitIns_R_R_I(INS_slti, attr, tempReg2, regOp2, 0);

                                // If ((A < B) != (C < 0)), there was overflow.
                                branchReg1 = tempReg1;
                                branchReg2 = tempReg2;
                            }
                        }

                        codeGen.genJumpToThrowHlpBlk_la(SCK_OVERFLOW, branchIns, branchReg1, null, branchReg2);
                    }

                    break;
                }

                default:
                {
                    NO_WAY("unexpected instruction within emitInsTernary!");
                    break;
                }
            }
        }

        return dstReg;
    }
}
#endif
