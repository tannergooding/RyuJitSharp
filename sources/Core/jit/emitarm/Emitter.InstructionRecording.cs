// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private insFormat emitInsFormat(instruction ins)
    {
        assert((uint)ins < (uint)s_instructionFormats.Length);
        assert(s_instructionFormats[(int)ins] != IF_NONE);
        return s_instructionFormats[(int)ins];
    }

    private static insSize emitInsSize(insFormat format)
    {
        if ((format >= IF_T1_A) && (format < IF_T2_A))
        {
            return ISZ_16BIT;
        }

        if ((format >= IF_T2_A) && (format < IF_INVALID))
        {
            return ISZ_32BIT;
        }

        if (format == IF_LARGEJMP)
        {
            return ISZ_48BIT;
        }

        assert(false, "!\"Invalid insFormat\"");
        return ISZ_48BIT;
    }

    private void recordArm32InsI(instruction ins, emitAttr attr, nint value)
    {
        var imm = unchecked((int)value);
        var format = IF_NONE;
        var hasLr = false;
        var hasPc = false;
        var useThumb2 = false;
        var isSingleBit = false;

        switch (ins)
        {
#if FEATURE_ITINSTRUCTION
            case INS_it:
            case INS_itt:
            case INS_ite:
            case INS_ittt:
            case INS_itte:
            case INS_itet:
            case INS_itee:
            case INS_itttt:
            case INS_ittte:
            case INS_ittet:
            case INS_ittee:
            case INS_itett:
            case INS_itete:
            case INS_iteet:
            case INS_iteee:
            {
                assert((imm & 0x0f) == imm);
                format = IF_T1_B;
                attr = EA_4BYTE;
                break;
            }
#endif

            case INS_push:
            case INS_pop:
            {
                if (ins == INS_push)
                {
                    assert((imm & 0xa000) == 0);
                    hasLr = (imm & 0x4000) != 0;
                }
                else
                {
                    assert((imm & 0x2000) == 0);
                    assert((imm & 0xc000) != 0xc000);
                    hasPc = (imm & 0x8000) != 0;
                    hasLr = (imm & 0x4000) != 0;
                    useThumb2 = hasLr;
                }

                isSingleBit = (imm != 0) && (((imm - 1) & imm) == 0);
                imm &= ~0xe000;

                if (((imm & 0x00ff) == imm) && !useThumb2)
                {
                    format = IF_T1_L1;
                }
                else if (!isSingleBit)
                {
                    format = IF_T2_I1;
                }
                else
                {
                    if (hasLr)
                    {
                        imm |= 0x4000;
                    }

                    var reg = (regNumber)System.Numerics.BitOperations.TrailingZeroCount(unchecked((uint)imm));
                    recordArm32InsR(ins, attr, reg);
                    return;
                }

                imm <<= 2;
                if (hasPc)
                {
                    imm |= 2;
                }
                if (hasLr)
                {
                    imm |= 1;
                }
                assert(imm != 0);
                break;
            }

            case INS_dmb:
            case INS_ism:
            {
                if ((imm & 0x000f) == imm)
                {
                    format = IF_T2_B;
                    attr = EA_4BYTE;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                }
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T1_B) || (format == IF_T1_L0) || (format == IF_T1_L1)
            || (format == IF_T2_I1) || (format == IF_T2_B));

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));

        dispIns(id);
        appendToCurIG(id);
    }

    private void recordArm32InsR(instruction ins, emitAttr attr, regNumber reg)
    {
        var size = EA_SIZE(attr);
        insFormat format;

        switch (ins)
        {
            case INS_pop:
            case INS_push:
            {
                if (isLowRegister(reg))
                {
                    recordArm32InsI(ins, attr, (nint)(1 << (int)reg));
                    return;
                }

                assert(size == EA_PTRSIZE);
                format = IF_T2_E2;
                break;
            }

            case INS_vmrs:
            {
                assert(size == EA_PTRSIZE);
                format = IF_T2_E2;
                break;
            }

            case INS_bx:
            {
                assert(size == EA_PTRSIZE);
                format = IF_T1_D1;
                break;
            }

            case INS_rsb:
            case INS_mvn:
            {
                emitIns_R_R_I(ins, attr, reg, reg, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                return;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T1_D1) || (format == IF_T2_E2));
        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }

    private void recordArm32InsRI(instruction ins, emitAttr attr, regNumber reg, int imm, insFlags flags
#if DEBUG
        , GenTreeFlags gtFlags
#endif
        )
    {
        var format = IF_NONE;
        var recordedFlags = INS_FLAGS_DONT_CARE;

        switch (ins)
        {
            case INS_add:
            case INS_sub:
            {
                assert(reg != REG_PC);
                if ((reg == REG_SP) && insDoesNotSetFlags(flags) && ((imm & 0x01fc) == imm))
                {
                    format = IF_T1_F;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else if (isLowRegister(reg) && insSetsFlags(flags) && (unsigned_abs(imm) <= 0x00ff))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    format = IF_T1_J0;
                    recordedFlags = INS_FLAGS_SET;
                }
                else
                {
                    emitIns_R_R_I(ins, attr, reg, reg, imm, flags);
                    return;
                }
                break;
            }

            case INS_adc:
            {
                assert(reg != REG_PC);
                emitIns_R_R_I(ins, attr, reg, reg, imm, flags);
                return;
            }

            case INS_vpush:
            case INS_vpop:
            {
                assert(imm > 0);
                if (attr == EA_8BYTE)
                {
                    assert(isDoubleReg(reg));
                    assert(imm <= 16);
                    imm *= 2;
                }
                else
                {
                    assert(attr == EA_4BYTE);
                    assert(isFloatReg(reg));
                    assert(imm <= 16);
                }

                assert((((int)reg - (int)REG_F0) + imm) <= 32);
                imm *= 4;
                if (ins == INS_vpush)
                {
                    imm = unchecked(-imm);
                }

                recordedFlags = INS_FLAGS_NOT_SET;
                format = IF_T2_VLDST;
                break;
            }

            case INS_stm:
            {
                recordedFlags = INS_FLAGS_NOT_SET;
                var hasLr = false;
                var hasPc = false;
                var useThumb2 = false;
                var onlyThumb1 = false;

                assert((imm & 0x2000) == 0);
                assert((imm & 0xc000) != 0xc000);
                assert((imm & 0xffff0000) == 0);

                hasPc = (imm & 0x8000) != 0;
                if ((imm & 0x4000) != 0)
                {
                    hasLr = true;
                    useThumb2 = true;
                }

                if (!isLowRegister(reg))
                {
                    useThumb2 = true;
                }

                if (((unchecked(imm - 1) & imm) == 0) &&
                    (((imm == 0) && !hasLr) || (!hasPc && !hasLr)))
                {
                    onlyThumb1 = true;
                }

                imm &= ~0xe000;
                if (((imm & 0x00ff) == imm) && !useThumb2)
                {
                    format = IF_T1_J1;
                }
                else if (!onlyThumb1)
                {
                    format = IF_T2_I0;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }

                if (format == IF_T2_I0)
                {
                    imm <<= 2;
                    if (hasPc)
                    {
                        imm |= 2;
                    }
                    if (hasLr)
                    {
                        imm |= 1;
                    }
                }

                assert(imm != 0);
                break;
            }

            case INS_and:
            case INS_bic:
            case INS_eor:
            case INS_orr:
            case INS_orn:
            case INS_rsb:
            case INS_sbc:
            case INS_ror:
            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg != REG_PC);
                emitIns_R_R_I(ins, attr, reg, reg, imm, flags);
                return;
            }

            case INS_mov:
            {
                assert(!EA_IS_CNS_RELOC(attr));
                if (isLowRegister(reg) && insSetsFlags(flags) && ((imm & 0x00ff) == imm))
                {
                    format = IF_T1_J0;
                    recordedFlags = INS_FLAGS_SET;
                }
                else if (isModImmConst(imm))
                {
                    format = IF_T2_L1;
                    recordedFlags = insMustSetFlags(flags);
                }
                else if (isModImmConst(~imm))
                {
                    ins = INS_mvn;
                    imm = ~imm;
                    format = IF_T2_L1;
                    recordedFlags = insMustSetFlags(flags);
                }
                else if (insDoesNotSetFlags(flags) && ((imm & 0x0000ffff) == imm))
                {
                    ins = INS_movw;
                    format = IF_T2_N;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            case INS_movw:
            case INS_movt:
            {
                assert(!EA_IS_RELOC(attr));
                assert(insDoesNotSetFlags(flags));
                if ((imm & 0x0000ffff) == imm)
                {
                    format = IF_T2_N;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            case INS_mvn:
            {
                if (isModImmConst(imm))
                {
                    format = IF_T2_L1;
                    recordedFlags = insMustSetFlags(flags);
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            case INS_cmp:
            {
                assert(reg != REG_PC);
                assert(!EA_IS_CNS_RELOC(attr));
                assert(insSetsFlags(flags));
                recordedFlags = INS_FLAGS_SET;
                if (isLowRegister(reg) && ((imm & 0x00ff) == imm))
                {
                    format = IF_T1_J0;
                }
                else if (isModImmConst(imm))
                {
                    format = IF_T2_L2;
                }
                else if (isModImmConst(unchecked(-imm)))
                {
                    ins = INS_cmn;
                    format = IF_T2_L2;
                    imm = unchecked(-imm);
                }
                else
                {
                    assert(false, "Immediate does not fit the compare instruction.");
                    unreached();
                    return;
                }
                break;
            }

            case INS_cmn:
            case INS_tst:
            case INS_teq:
            {
                assert(reg != REG_PC);
                assert(insSetsFlags(flags));
                recordedFlags = INS_FLAGS_SET;
                if (isModImmConst(imm))
                {
                    format = IF_T2_L2;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

#if FEATURE_PLI_INSTRUCTION
            case INS_pli:
            {
                assert(insDoesNotSetFlags(flags));
                if ((reg == REG_SP) && (unsigned_abs(imm) <= 0x0fff))
                {
                    format = IF_T2_K3;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }

                goto case INS_pld;
            }
#endif

            case INS_pld:
            case INS_pldw:
            {
                assert(insDoesNotSetFlags(flags));
                recordedFlags = INS_FLAGS_NOT_SET;
                if ((imm >= 0) && (imm <= 0x0fff))
                {
                    format = IF_T2_K2;
                }
                else if ((imm < 0) && (unchecked(-imm) <= 0x00ff))
                {
                    imm = unchecked(-imm);
                    format = IF_T2_H2;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T1_F) || (format == IF_T1_J0) || (format == IF_T1_J1) ||
            (format == IF_T2_H2) || (format == IF_T2_I0) || (format == IF_T2_K2) ||
            (format == IF_T2_K3) || (format == IF_T2_L1) || (format == IF_T2_L2) ||
            (format == IF_T2_M1) || (format == IF_T2_N) || (format == IF_T2_VLDST));
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idReg1(reg);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idFlags = gtFlags;
#endif

        dispIns(id);
        appendToCurIG(id);
    }

    private bool recordArm32InsMov(instruction ins, emitAttr attr, regNumber dstReg, regNumber srcReg,
        bool canSkip, insFlags flags)
    {
        assert(IsMovInstruction(ins));

        var size = EA_SIZE(attr);
        insFormat format;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_mov:
            {
                if (insDoesNotSetFlags(flags))
                {
                    if (canSkip && (dstReg == srcReg))
                    {
                        return false;
                    }

                    format = IF_T1_D0;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else
                {
                    recordedFlags = INS_FLAGS_SET;
                    format = isLowRegister(dstReg) && isLowRegister(srcReg) ? IF_T1_E : IF_T2_C3;
                }
                break;
            }

            case INS_vmov:
            {
                assert(dstReg != REG_PC);
                assert(srcReg != REG_PC);
                if (canSkip && (dstReg == srcReg))
                {
                    return false;
                }

                if (size == EA_8BYTE)
                {
                    assert(isDoubleReg(dstReg));
                    assert(isDoubleReg(srcReg));
                }
                else
                {
                    assert(isFloatReg(dstReg));
                    assert(isFloatReg(srcReg));
                }

                format = IF_T2_VFP2;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_i2f:
            {
                assert(srcReg != REG_PC);
                assert(isFloatReg(dstReg));
                assert(isGeneralRegister(srcReg));
                format = IF_T2_VMOVS;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_f2i:
            {
                assert(dstReg != REG_PC);
                assert(isGeneralRegister(dstReg));
                assert(isFloatReg(srcReg));
                format = IF_T2_VMOVS;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_sxtb:
            case INS_uxtb:
            {
                assert(size == EA_4BYTE);
                goto case INS_sxth;
            }

            case INS_sxth:
            case INS_uxth:
            {
                assert(size == EA_4BYTE);
                if (canSkip && (dstReg == srcReg))
                {
                    return false;
                }

                assert(dstReg != REG_PC);
                assert(srcReg != REG_PC);
                assert(insDoesNotSetFlags(flags));
                if (isLowRegister(dstReg) && isLowRegister(srcReg))
                {
                    format = IF_T1_E;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else
                {
                    emitIns_R_R_I(ins, attr, dstReg, srcReg, 0, INS_FLAGS_NOT_SET);
                    return true;
                }
                break;
            }

            default:
            {
                unreached();
                return false;
            }
        }

        assert((format == IF_T1_D0) || (format == IF_T1_E) || (format == IF_T2_C3) ||
            (format == IF_T2_VFP2) || (format == IF_T2_VMOVS));
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idReg1(dstReg);
        id.idReg2(srcReg);

        dispIns(id);
        appendToCurIG(id);
        return true;
    }

    private void recordArm32InsRR(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        insFlags flags, insOpts opt)
    {
        assert(insOptsNone(opt));

        if (IsMovInstruction(ins))
        {
            assert(false, "Use emitIns_Mov to preserve move-elision behavior.");
            _ = recordArm32InsMov(ins, attr, reg1, reg2, canSkip: false, flags);
            unreached();
            return;
        }

        var size = EA_SIZE(attr);
        insFormat format;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_add:
            {
                assert(reg1 != REG_PC);
                if (insDoesNotSetFlags(flags))
                {
                    format = IF_T1_D0;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }

                recordArm32InsRRR(ins, attr, reg1, reg1, reg2, flags);
                return;
            }

            case INS_sub:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                recordArm32InsRRR(ins, attr, reg1, reg1, reg2, flags);
                return;
            }

            case INS_cmp:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insSetsFlags(flags));
                recordedFlags = INS_FLAGS_SET;
                format = isLowRegister(reg1) && isLowRegister(reg2) ? IF_T1_E : IF_T1_D0;
                break;
            }

            case INS_vcvt_d2i:
            case INS_vcvt_d2u:
            case INS_vcvt_d2f:
            {
                assert(isFloatReg(reg1));
                assert(isDoubleReg(reg2));
                goto VCVT_COMMON;
            }

            case INS_vcvt_f2d:
            case INS_vcvt_u2d:
            case INS_vcvt_i2d:
            {
                assert(isDoubleReg(reg1));
                assert(isFloatReg(reg2));
                goto VCVT_COMMON;
            }

            case INS_vcvt_u2f:
            case INS_vcvt_i2f:
            case INS_vcvt_f2i:
            case INS_vcvt_f2u:
            {
                assert(size == EA_4BYTE);
                assert(isFloatReg(reg1));
                assert(isFloatReg(reg2));
                goto VCVT_COMMON;
            }

            case INS_vabs:
            case INS_vsqrt:
            case INS_vcmp:
            case INS_vneg:
            {
                if (size == EA_8BYTE)
                {
                    assert(isDoubleReg(reg1));
                    assert(isDoubleReg(reg2));
                }
                else
                {
                    assert(isFloatReg(reg1));
                    assert(isFloatReg(reg2));
                }

                goto VCVT_COMMON;
            }

            case INS_vadd:
            case INS_vmul:
            case INS_vsub:
            case INS_vdiv:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                recordArm32InsRRR(ins, attr, reg1, reg1, reg2, INS_FLAGS_DONT_CARE);
                return;
            }

            case INS_vldr:
            case INS_vstr:
            case INS_ldr:
            case INS_ldrb:
            case INS_ldrsb:
            case INS_ldrh:
            case INS_ldrsh:
            case INS_str:
            case INS_strb:
            case INS_strh:
            {
                emitIns_R_R_I(ins, attr, reg1, reg2, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                return;
            }

            case INS_adc:
            case INS_and:
            case INS_bic:
            case INS_eor:
            case INS_orr:
            case INS_sbc:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                if (insSetsFlags(flags) && isLowRegister(reg1) && isLowRegister(reg2))
                {
                    format = IF_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                goto case INS_orn;
            }

            case INS_orn:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                recordArm32InsRRRImm(ins, attr, reg1, reg1, reg2, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                if (insSetsFlags(flags) && isLowRegister(reg1) && isLowRegister(reg2))
                {
                    format = IF_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                goto case INS_ror;
            }

            case INS_ror:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                format = IF_T2_C4;
                recordedFlags = insMustSetFlags(flags);
                break;
            }

            case INS_mul:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                if ((insMustSetFlags(flags) == INS_FLAGS_SET) && isLowRegister(reg1) && (reg1 == reg2))
                {
                    format = IF_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                recordArm32InsRRR(ins, attr, reg1, reg2, reg1, flags);
                return;
            }

            case INS_mvn:
            case INS_cmn:
            case INS_tst:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                if (insSetsFlags(flags) && isLowRegister(reg1) && isLowRegister(reg2))
                {
                    format = IF_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                emitIns_R_R_I(ins, attr, reg1, reg2, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_tbb:
            case INS_tbh:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_C9;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_clz:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_C10;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrexb:
            case INS_strexb:
            case INS_ldrexh:
            case INS_strexh:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_E1;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        goto RECORD;

    VCVT_COMMON:
        format = IF_T2_VFP2;
        recordedFlags = INS_FLAGS_NOT_SET;

    RECORD:
        assert((format == IF_T1_D0) || (format == IF_T1_E) || (format == IF_T2_C3) ||
            (format == IF_T2_C9) || (format == IF_T2_C10) || (format == IF_T2_VFP2) ||
            (format == IF_T2_VMOVD) || (format == IF_T2_VMOVS) || (format == IF_T2_E1));
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }

    private void recordArm32InsRRR(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, insFlags flags)
    {
        var size = EA_SIZE(attr);
        var format = IF_NONE;
        var recordedFlags = INS_FLAGS_DONT_CARE;

        switch (ins)
        {
            case INS_add:
            {
                if (reg3 == REG_SP)
                {
                    (reg2, reg3) = (REG_SP, reg2);
                }

                goto case INS_sub;
            }

            case INS_sub:
            {
                assert(reg3 != REG_SP);
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert((reg3 != REG_PC) || (ins == INS_add));
                if (isLowRegister(reg1) && isLowRegister(reg2) && isLowRegister(reg3) && insSetsFlags(flags))
                {
                    format = IF_T1_H;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                if ((ins == INS_add) && insDoesNotSetFlags(flags))
                {
                    if (reg1 == reg2)
                    {
                        recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                        return;
                    }
                    if (reg1 == reg3)
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
                    }
                }

                recordArm32InsRRRImm(ins, attr, reg1, reg2, reg3, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_adc:
            case INS_and:
            case INS_bic:
            case INS_eor:
            case INS_orr:
            case INS_sbc:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                if (reg1 == reg2)
                {
                    recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                    return;
                }

                goto case INS_orn;
            }

            case INS_orn:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                recordArm32InsRRRImm(ins, attr, reg1, reg2, reg3, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                if ((reg1 == reg2) && insSetsFlags(flags) && isLowRegister(reg1) && isLowRegister(reg3))
                {
                    recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                    return;
                }

                goto case INS_ror;
            }

            case INS_ror:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                format = IF_T2_C4;
                recordedFlags = insMustSetFlags(flags);
                break;
            }

            case INS_mul:
            {
                if (insMustSetFlags(flags) == INS_FLAGS_SET)
                {
                    assert(reg1 != REG_PC);
                    assert(reg2 != REG_PC);
                    assert(reg3 != REG_PC);

                    if ((reg1 == reg2) && isLowRegister(reg1))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                        return;
                    }
                    if ((reg1 == reg3) && isLowRegister(reg1))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
                    }

                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }

#if !USE_HELPERS_FOR_INT_DIV
                goto case INS_sdiv;
#else
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_C5;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
#endif
            }

#if !USE_HELPERS_FOR_INT_DIV
            case INS_sdiv:
            case INS_udiv:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_C5;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }
#endif

            case INS_ldrb:
            case INS_strb:
            case INS_ldrsb:
            case INS_ldrsh:
            case INS_ldrh:
            case INS_strh:
            case INS_ldr:
            case INS_str:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                if (isLowRegister(reg1) && isLowRegister(reg2) && isLowRegister(reg3))
                {
                    format = IF_T1_H;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }

                recordArm32InsRRRImm(ins, attr, reg1, reg2, reg3, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_vadd:
            case INS_vmul:
            case INS_vsub:
            case INS_vdiv:
            {
                if (size == EA_8BYTE)
                {
                    assert(isDoubleReg(reg1));
                    assert(isDoubleReg(reg2));
                    assert(isDoubleReg(reg3));
                }
                else
                {
                    assert(isFloatReg(reg1));
                    assert(isFloatReg(reg2));
                    assert(isFloatReg(reg3));
                }

                format = IF_T2_VFP3;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_i2d:
            {
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                assert(isDoubleReg(reg1));
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                format = IF_T2_VMOVD;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_d2i:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isDoubleReg(reg3));
                format = IF_T2_VMOVD;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrexd:
            case INS_strexd:
            {
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_G1;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T1_H) || (format == IF_T2_C4) || (format == IF_T2_C5) ||
            (format == IF_T2_VFP3) || (format == IF_T2_VMOVD) || (format == IF_T2_G1));
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);

        dispIns(id);
        appendToCurIG(id);
    }

    private void recordArm32InsRRRImm(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, int imm, insFlags flags, insOpts opt)
    {
        var size = EA_SIZE(attr);
        insFormat format;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_add:
            case INS_sub:
            {
                if (imm == 0)
                {
                    if (isLowRegister(reg1) && isLowRegister(reg2) && isLowRegister(reg3) && insSetsFlags(flags))
                    {
                        recordArm32InsRRR(ins, attr, reg1, reg2, reg3, flags);
                        return;
                    }

                    if ((ins == INS_add) && insDoesNotSetFlags(flags))
                    {
                        if (reg1 == reg2)
                        {
                            recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                            return;
                        }
                        if (reg1 == reg3)
                        {
                            recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                            return;
                        }
                    }
                }

                goto case INS_adc;
            }

            case INS_adc:
            case INS_and:
            case INS_bic:
            case INS_eor:
            case INS_orn:
            case INS_orr:
            case INS_sbc:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(reg3 != REG_PC);
                assert((imm >= 0) && (imm <= 31));
                assert(!insOptAnyInc(opt));

                if (imm == 0)
                {
                    if (opt == INS_OPTS_LSL)
                    {
                        opt = INS_OPTS_NONE;
                    }

                    assert(insOptsNone(opt));
                    if (isLowRegister(reg1) && isLowRegister(reg2) && isLowRegister(reg3) &&
                        insSetsFlags(flags))
                    {
                        if (reg1 == reg2)
                        {
                            recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                            return;
                        }
                        if ((reg1 == reg3) && (ins is not INS_bic and not INS_orn and not INS_sbc))
                        {
                            recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                            return;
                        }
                    }
                }
                else
                {
                    assert(insOptAnyShift(opt));
                    if (insOptsRRX(opt))
                    {
                        assert(imm == 1);
                    }
                }

                format = IF_T2_C0;
                recordedFlags = insMustSetFlags(flags);
                break;
            }

            case INS_ldrb:
            case INS_ldrsb:
            case INS_strb:
            case INS_ldrh:
            case INS_ldrsh:
            case INS_strh:
            case INS_ldr:
            case INS_str:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                assert((imm & 0x0003) == imm);

                if ((imm == 0) && insOptsNone(opt) && isLowRegister(reg1) &&
                    isLowRegister(reg2) && isLowRegister(reg3))
                {
                    recordArm32InsRRR(ins, attr, reg1, reg2, reg3, flags);
                    return;
                }

                assert(insOptsNone(opt) || insOptsLSL(opt));
                format = IF_T2_E0;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrd:
            case INS_strd:
            {
                assert(insDoesNotSetFlags(flags));
                assert((imm & 0x03) == 0);
                recordedFlags = INS_FLAGS_NOT_SET;

                if (insOptAnyInc(opt))
                {
                    if (insOptsPostInc(opt))
                    {
                        assert(imm > 0);
                    }
                    else
                    {
                        assert(imm < 0);
                    }
                }
                else
                {
                    assert(insOptsNone(opt));
                }

                if (unsigned_abs(imm) <= 0x03fc)
                {
                    imm >>= 2;
                    format = IF_T2_G0;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T2_C0) || (format == IF_T2_E0) || (format == IF_T2_G0));
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);

        dispIns(id);
        appendToCurIG(id);
    }

    private void recordArm32InsRRRR(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, regNumber reg4)
    {
        insFormat format;
        switch (ins)
        {
            case INS_smull:
            case INS_umull:
            case INS_smlal:
            case INS_umlal:
            {
                assert(reg1 != reg2);
                format = IF_T2_F1;
                break;
            }

            case INS_mla:
            case INS_mls:
            {
                format = IF_T2_F2;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(reg1 != REG_PC);
        assert(reg2 != REG_PC);
        assert(reg3 != REG_PC);
        assert(reg4 != REG_PC);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(INS_FLAGS_NOT_SET);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idReg4(reg4);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_I_I(instruction ins, emitAttr attr, regNumber reg, int imm1, int imm2,
        insFlags flags = INS_FLAGS_DONT_CARE)
    {
        insFormat format;
        int encodedImmediate;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_bfc:
            {
                assert(reg != REG_PC);

                var leastSignificantBit = imm1;
                var mostSignificantBit = unchecked(leastSignificantBit + imm2 - 1);
                assert((leastSignificantBit >= 0) && (leastSignificantBit <= 31));
                assert((mostSignificantBit >= 0) && (mostSignificantBit <= 31));
                assert(mostSignificantBit >= leastSignificantBit);

                encodedImmediate = unchecked((leastSignificantBit << 5) | mostSignificantBit);
                assert(insDoesNotSetFlags(flags));
                format = IF_T2_D1;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(format == IF_T2_D1);
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrSC(attr, encodedImmediate);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
