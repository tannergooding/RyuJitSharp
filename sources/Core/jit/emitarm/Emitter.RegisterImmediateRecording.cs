// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
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
    private void recordArm32InsRRI(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int imm,
        insFlags flags, insOpts opt)
    {
        var size = EA_SIZE(attr);
        var format = IF_NONE;
        var recordedFlags = INS_FLAGS_DONT_CARE;

        if (ins == INS_lea)
        {
            ins = INS_add;
        }

        switch (ins)
        {
            case INS_add:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));
                if ((reg2 == REG_SP) && insDoesNotSetFlags(flags) && ((imm & 0x03fc) == imm))
                {
                    if ((reg1 == REG_SP) && ((imm & 0x01fc) == imm))
                    {
                        emitIns_R_I(ins, attr, reg1, imm, flags);
                        return;
                    }
                    if (isLowRegister(reg1))
                    {
                        format = IF_T1_J2;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                }

                goto case INS_sub;
            }

            case INS_sub:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));

                if ((imm == 0) && insDoesNotSetFlags(flags))
                {
                    _ = emitIns_Mov(INS_mov, attr, reg1, reg2, canSkip: true, flags: flags);
                    return;
                }
                if (isLowRegister(reg1) && isLowRegister(reg2) && insSetsFlags(flags) &&
                    (unsigned_abs(imm) <= 0x0007))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    format = IF_T1_G;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }
                if ((reg1 == reg2) && isLowRegister(reg1) && insSetsFlags(flags) &&
                    (unsigned_abs(imm) <= 0x00ff))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    emitIns_R_I(ins, attr, reg1, imm, flags);
                    return;
                }
                if (isModImmConst(imm))
                {
                    format = IF_T2_L0;
                    recordedFlags = insMustSetFlags(flags);
                    break;
                }
                if (isModImmConst(unchecked(-imm)))
                {
                    assert((ins == INS_add) || (ins == INS_sub));
                    ins = ins == INS_add ? INS_sub : INS_add;
                    imm = unchecked(-imm);
                    format = IF_T2_L0;
                    recordedFlags = insMustSetFlags(flags);
                    break;
                }
                if (insDoesNotSetFlags(flags) && (unsigned_abs(imm) <= 0x0fff))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    ins = ins == INS_add ? INS_addw : INS_subw;
                    format = IF_T2_M0;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }
                if (insDoesNotSetFlags(flags) && (reg1 != REG_SP))
                {
                    var materializedImm = ins == INS_sub ? unchecked(-imm) : imm;
                    codeGen.instGen_Set_Reg_To_Imm(attr, reg1, materializedImm);
                    emitIns_R_R(INS_add, attr, reg1, reg2);
                    return;
                }

                assert(false, "Instruction cannot be encoded.");
                unreached();
                return;
            }

            case INS_and:
            case INS_bic:
            case INS_orr:
            case INS_orn:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));
                if (isModImmConst(imm))
                {
                    format = IF_T2_L0;
                    recordedFlags = insMustSetFlags(flags);
                }
                else if (isModImmConst(~imm))
                {
                    format = IF_T2_L0;
                    recordedFlags = insMustSetFlags(flags);
                    imm = ~imm;
                    ins = ins switch
                    {
                        INS_and => INS_bic,
                        INS_bic => INS_and,
                        INS_orr => INS_orn,
                        INS_orn => INS_orr,
                        _ => throw new InvalidOperationException(),
                    };
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            case INS_rsb:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));
                if ((imm == 0) && isLowRegister(reg1) && isLowRegister(reg2) && insSetsFlags(flags))
                {
                    format = IF_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                goto case INS_adc;
            }

            case INS_adc:
            case INS_eor:
            case INS_sbc:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));
                if (isModImmConst(imm))
                {
                    format = IF_T2_L0;
                    recordedFlags = insMustSetFlags(flags);
                    break;
                }

                assert(false, "Instruction cannot be encoded.");
                unreached();
                return;
            }

            case INS_adr:
            {
                assert(insOptsNone(opt));
                assert(insDoesNotSetFlags(flags));
                assert(reg2 == REG_PC);
                recordedFlags = INS_FLAGS_NOT_SET;
                if (isLowRegister(reg1) && ((imm & 0x00ff) == imm))
                {
                    format = IF_T1_J3;
                }
                else if ((imm & 0x0fff) == imm)
                {
                    format = IF_T2_M1;
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
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert((imm >= 0) && (imm <= 31));
                assert(!insOptAnyInc(opt));
                if (imm == 0)
                {
                    assert(insOptsNone(opt));
                    if (isLowRegister(reg1) && isLowRegister(reg2) && insSetsFlags(flags))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
                    }
                }
                else
                {
                    assert(insOptAnyShift(opt));
                }

                format = IF_T2_C1;
                recordedFlags = insMustSetFlags(flags);
                break;
            }

            case INS_cmp:
            case INS_cmn:
            case INS_teq:
            case INS_tst:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insSetsFlags(flags));
                assert((imm >= 0) && (imm <= 31));
                assert(!insOptAnyInc(opt));
                if (imm == 0)
                {
                    assert(insOptsNone(opt));
                    if (ins == INS_cmp ||
                        ((ins is INS_cmn or INS_tst) && isLowRegister(reg1) && isLowRegister(reg2)))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
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

                format = IF_T2_C8;
                recordedFlags = INS_FLAGS_SET;
                break;
            }

            case INS_ror:
            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));
                imm &= 0x1f;
                if (imm == 0)
                {
                    _ = emitIns_Mov(INS_mov, attr, reg1, reg2,
                        canSkip: insMustSetFlags(flags) == INS_FLAGS_NOT_SET, flags: flags);
                    return;
                }

                if (insSetsFlags(flags) && (ins != INS_ror) &&
                    isLowRegister(reg1) && isLowRegister(reg2))
                {
                    format = IF_T1_C;
                    recordedFlags = INS_FLAGS_SET;
                }
                else
                {
                    format = IF_T2_C2;
                    recordedFlags = insMustSetFlags(flags);
                }
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
                assert(reg1 != REG_PC);
                assert(reg2 != REG_PC);
                assert(insOptsNone(opt));
                assert(insDoesNotSetFlags(flags));
                assert((imm & 0x018) == imm);
                if ((imm == 0) && isLowRegister(reg1) && isLowRegister(reg2))
                {
                    recordArm32InsRR(ins, attr, reg1, reg2, INS_FLAGS_NOT_SET, INS_OPTS_NONE);
                    return;
                }

                format = IF_T2_C6;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_pld:
            case INS_pldw:
#if FEATURE_PLI_INSTRUCTION
            case INS_pli:
#endif
            {
                assert(insOptsNone(opt));
                assert(insDoesNotSetFlags(flags));
                assert((imm & 0x003) == imm);
                format = IF_T2_C7;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrb:
            case INS_strb:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                if (isLowRegister(reg1) && isLowRegister(reg2) && insOptsNone(opt) &&
                    ((imm & 0x001f) == imm))
                {
                    format = IF_T1_C;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }

                goto COMMON_THUMB2_LDST;
            }

            case INS_ldrsb:
            {
                assert(size == EA_4BYTE);
                goto COMMON_THUMB2_LDST;
            }

            case INS_ldrh:
            case INS_strh:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                if (isLowRegister(reg1) && isLowRegister(reg2) && insOptsNone(opt) &&
                    ((imm & 0x003e) == imm))
                {
                    format = IF_T1_C;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }

                goto COMMON_THUMB2_LDST;
            }

            case INS_ldrsh:
            {
                assert(size == EA_4BYTE);
                goto COMMON_THUMB2_LDST;
            }

            case INS_vldr:
            case INS_vstr:
            case INS_vldm:
            case INS_vstm:
            {
                assert(format == IF_NONE);
                assert(insDoesNotSetFlags(flags));
                var absoluteOffset = unsigned_abs(imm);
                assert((absoluteOffset & 0x03fc) == absoluteOffset);
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

                recordedFlags = INS_FLAGS_NOT_SET;
                format = IF_T2_VLDST;
                break;
            }

            case INS_ldr:
            case INS_str:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlags(flags));
                if (isLowRegister(reg1) && insOptsNone(opt) && ((imm & 0x03fc) == imm))
                {
                    if (reg2 == REG_SP)
                    {
                        format = IF_T1_J2;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                    if ((reg2 == REG_PC) && (ins == INS_ldr))
                    {
                        format = IF_T1_J3;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                    if (isLowRegister(reg2) && ((imm & 0x007c) == imm))
                    {
                        format = IF_T1_C;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                }

                goto COMMON_THUMB2_LDST;
            }

        COMMON_THUMB2_LDST:
            assert(format == IF_NONE);
            assert(insDoesNotSetFlags(flags));
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

                if (unsigned_abs(imm) <= 0x00ff)
                {
                    format = IF_T2_H0;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
            }
            else
            {
                assert(insOptsNone(opt));
                if ((reg2 == REG_PC) && (unsigned_abs(imm) <= 0x0fff))
                {
                    format = IF_T2_K4;
                }
                else if ((imm & 0x0fff) == imm)
                {
                    format = IF_T2_K1;
                }
                else if (unsigned_abs(imm) <= 0x00ff)
                {
                    format = IF_T2_H0;
                }
                else
                {
                    var reservedReg = codeGen.rsGetRsvdReg();
                    codeGen.instGen_Set_Reg_To_Imm(EA_4BYTE, reservedReg, imm);
                    recordArm32InsRRR(ins, attr, reg1, reg2, reservedReg, flags);
                    return;
                }
            }
            break;

            case INS_ldrex:
            case INS_strex:
            {
                assert(insOptsNone(opt));
                assert(insDoesNotSetFlags(flags));
                recordedFlags = INS_FLAGS_NOT_SET;
                if ((imm & 0x03fc) == imm)
                {
                    format = IF_T2_H0;
                    break;
                }

                assert(false, "Instruction cannot be encoded.");
                unreached();
                return;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T1_C) || (format == IF_T1_E) || (format == IF_T1_G) ||
            (format == IF_T1_J2) || (format == IF_T1_J3) || (format == IF_T2_C1) ||
            (format == IF_T2_C2) || (format == IF_T2_C6) || (format == IF_T2_C7) ||
            (format == IF_T2_C8) || (format == IF_T2_H0) || (format == IF_T2_H1) ||
            (format == IF_T2_K1) || (format == IF_T2_K4) || (format == IF_T2_L0) ||
            (format == IF_T2_M0) || (format == IF_T2_VLDST) || (format == IF_T2_M1));
        assert(recordedFlags != INS_FLAGS_DONT_CARE);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(emitInsSize(format));
        id.idInsFlags(recordedFlags);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
