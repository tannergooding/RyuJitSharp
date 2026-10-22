// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        RequireArm32Target();
        var size = EA_SIZE(attr);
        var format = IF_NONE;
        insFlags recordedFlags;

        if (ins == INS_lea)
        {
            ins = INS_add;
        }

        switch (ins)
        {
            case INS_add:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));
                if ((reg2 == TargetRuntimeMetadata.StackPointerBaseRegister) && insDoesNotSetFlagsArm32(flags) && ((imm & 0x03fc) == imm))
                {
                    if ((reg1 == TargetRuntimeMetadata.StackPointerBaseRegister) && ((imm & 0x01fc) == imm))
                    {
                        recordArm32InsRI(ins, attr, reg1, imm, flags, GTF_EMPTY);
                        return;
                    }
                    if (isArm32LowRegister(reg1))
                    {
                        format = IF_ARM32_T1_J2;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                }

                goto case INS_sub;
            }

            case INS_sub:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));

                if ((imm == 0) && insDoesNotSetFlagsArm32(flags))
                {
                    _ = recordArm32InsMov(INS_mov, attr, reg1, reg2, canSkip: true, flags);
                    return;
                }
                if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && insSetsFlagsArm32(flags) &&
                    (unsignedAbsArm32(imm) <= 0x0007))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    format = IF_ARM32_T1_G;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }
                if ((reg1 == reg2) && isArm32LowRegister(reg1) && insSetsFlagsArm32(flags) &&
                    (unsignedAbsArm32(imm) <= 0x00ff))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    recordArm32InsRI(ins, attr, reg1, imm, flags, GTF_EMPTY);
                    return;
                }
                if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L0;
                    recordedFlags = insMustSetFlagsArm32(flags);
                    break;
                }
                if (isArm32ModifiedImmediate(unchecked(-imm)))
                {
                    assert((ins == INS_add) || (ins == INS_sub));
                    ins = ins == INS_add ? INS_sub : INS_add;
                    imm = unchecked(-imm);
                    format = IF_ARM32_T2_L0;
                    recordedFlags = insMustSetFlagsArm32(flags);
                    break;
                }
                if (insDoesNotSetFlagsArm32(flags) && (unsignedAbsArm32(imm) <= 0x0fff))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    ins = ins == INS_add ? INS_addw : INS_subw;
                    format = IF_ARM32_T2_M0;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }
                if (insDoesNotSetFlagsArm32(flags) && (reg1 != TargetRuntimeMetadata.StackPointerBaseRegister))
                {
                    var materializedImm = ins == INS_sub ? unchecked(-imm) : imm;
                    codeGen.instGen_Set_Reg_To_Imm(attr, reg1, materializedImm);
                    recordArm32InsRR(INS_add, attr, reg1, reg2, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));
                if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L0;
                    recordedFlags = insMustSetFlagsArm32(flags);
                }
                else if (isArm32ModifiedImmediate(~imm))
                {
                    format = IF_ARM32_T2_L0;
                    recordedFlags = insMustSetFlagsArm32(flags);
                    imm = ~imm;
                    switch (ins)
                    {
                        case INS_and:
                            ins = INS_bic;
                            break;
                        case INS_bic:
                            ins = INS_and;
                            break;
                        case INS_orr:
                            ins = INS_orn;
                            break;
                        case INS_orn:
                            ins = INS_orr;
                            break;
                        default:
                            JitExceptionHelpers.ThrowInvalidOperationException();
                            ins = default;
                            break;
                    }
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));
                if ((imm == 0) && isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && insSetsFlagsArm32(flags))
                {
                    format = IF_ARM32_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                goto case INS_adc;
            }

            case INS_adc:
            case INS_eor:
            case INS_sbc:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));
                if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L0;
                    recordedFlags = insMustSetFlagsArm32(flags);
                    break;
                }

                assert(false, "Instruction cannot be encoded.");
                unreached();
                return;
            }

            case INS_adr:
            {
                assert(insOptsNoneArm32(opt));
                assert(insDoesNotSetFlagsArm32(flags));
                assert(reg2 == REG_ARM32_PC);
                recordedFlags = INS_FLAGS_NOT_SET;
                if (isArm32LowRegister(reg1) && ((imm & 0x00ff) == imm))
                {
                    format = IF_ARM32_T1_J3;
                }
                else if ((imm & 0x0fff) == imm)
                {
                    format = IF_ARM32_T2_M1;
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert((imm >= 0) && (imm <= 31));
                assert(!insOptAnyIncArm32(opt));
                if (imm == 0)
                {
                    assert(insOptsNoneArm32(opt));
                    if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && insSetsFlagsArm32(flags))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
                    }
                }
                else
                {
                    assert(insOptAnyShiftArm32(opt));
                }

                format = IF_ARM32_T2_C1;
                recordedFlags = insMustSetFlagsArm32(flags);
                break;
            }

            case INS_cmp:
            case INS_cmn:
            case INS_teq:
            case INS_tst:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insSetsFlagsArm32(flags));
                assert((imm >= 0) && (imm <= 31));
                assert(!insOptAnyIncArm32(opt));
                if (imm == 0)
                {
                    assert(insOptsNoneArm32(opt));
                    if (ins == INS_cmp ||
                        ((ins is INS_cmn or INS_tst) && isArm32LowRegister(reg1) && isArm32LowRegister(reg2)))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
                    }
                }
                else
                {
                    assert(insOptAnyShiftArm32(opt));
                    if (insOptsRRXArm32(opt))
                    {
                        assert(imm == 1);
                    }
                }

                format = IF_ARM32_T2_C8;
                recordedFlags = INS_FLAGS_SET;
                break;
            }

            case INS_ror:
            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));
                imm &= 0x1f;
                if (imm == 0)
                {
                    _ = recordArm32InsMov(INS_mov, attr, reg1, reg2,
                        canSkip: insMustSetFlagsArm32(flags) == INS_FLAGS_NOT_SET, flags: flags);
                    return;
                }

                if (insSetsFlagsArm32(flags) && (ins != INS_ror) &&
                    isArm32LowRegister(reg1) && isArm32LowRegister(reg2))
                {
                    format = IF_ARM32_T1_C;
                    recordedFlags = INS_FLAGS_SET;
                }
                else
                {
                    format = IF_ARM32_T2_C2;
                    recordedFlags = insMustSetFlagsArm32(flags);
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insOptsNoneArm32(opt));
                assert(insDoesNotSetFlagsArm32(flags));
                assert((imm & 0x018) == imm);
                if ((imm == 0) && isArm32LowRegister(reg1) && isArm32LowRegister(reg2))
                {
                    recordArm32InsRR(ins, attr, reg1, reg2, INS_FLAGS_NOT_SET, INS_OPTS_NONE);
                    return;
                }

                format = IF_ARM32_T2_C6;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_pld:
            case INS_pldw:
            {
                assert(insOptsNoneArm32(opt));
                assert(insDoesNotSetFlagsArm32(flags));
                assert((imm & 0x003) == imm);
                format = IF_ARM32_T2_C7;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrb:
            case INS_strb:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlagsArm32(flags));
                if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && insOptsNoneArm32(opt) &&
                    ((imm & 0x001f) == imm))
                {
                    format = IF_ARM32_T1_C;
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
                assert(insDoesNotSetFlagsArm32(flags));
                if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && insOptsNoneArm32(opt) &&
                    ((imm & 0x003e) == imm))
                {
                    format = IF_ARM32_T1_C;
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
                assert(insDoesNotSetFlagsArm32(flags));
                var absoluteOffset = unsignedAbsArm32(imm);
                assert((absoluteOffset & 0x03fc) == absoluteOffset);
                if (insOptAnyIncArm32(opt))
                {
                    if (insOptsPostIncArm32(opt))
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
                    assert(insOptsNoneArm32(opt));
                }

                recordedFlags = INS_FLAGS_NOT_SET;
                format = IF_ARM32_T2_VLDST;
                break;
            }

            case INS_ldr:
            case INS_str:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlagsArm32(flags));
                if (isArm32LowRegister(reg1) && insOptsNoneArm32(opt) && ((imm & 0x03fc) == imm))
                {
                    if (reg2 == TargetRuntimeMetadata.StackPointerBaseRegister)
                    {
                        format = IF_ARM32_T1_J2;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                    if ((reg2 == REG_ARM32_PC) && (ins == INS_ldr))
                    {
                        format = IF_ARM32_T1_J3;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                    if (isArm32LowRegister(reg2) && ((imm & 0x007c) == imm))
                    {
                        format = IF_ARM32_T1_C;
                        recordedFlags = INS_FLAGS_NOT_SET;
                        break;
                    }
                }

                goto COMMON_THUMB2_LDST;
            }

        COMMON_THUMB2_LDST:
            assert(format == IF_NONE);
            assert(insDoesNotSetFlagsArm32(flags));
            recordedFlags = INS_FLAGS_NOT_SET;
            if (insOptAnyIncArm32(opt))
            {
                if (insOptsPostIncArm32(opt))
                {
                    assert(imm > 0);
                }
                else
                {
                    assert(imm < 0);
                }

                if (unsignedAbsArm32(imm) <= 0x00ff)
                {
                    format = IF_ARM32_T2_H0;
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
                assert(insOptsNoneArm32(opt));
                if ((reg2 == REG_ARM32_PC) && (unsignedAbsArm32(imm) <= 0x0fff))
                {
                    format = IF_ARM32_T2_K4;
                }
                else if ((imm & 0x0fff) == imm)
                {
                    format = IF_ARM32_T2_K1;
                }
                else if (unsignedAbsArm32(imm) <= 0x00ff)
                {
                    format = IF_ARM32_T2_H0;
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
                assert(insOptsNoneArm32(opt));
                assert(insDoesNotSetFlagsArm32(flags));
                recordedFlags = INS_FLAGS_NOT_SET;
                if ((imm & 0x03fc) == imm)
                {
                    format = IF_ARM32_T2_H0;
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

        assert((format == IF_ARM32_T1_C) || (format == IF_ARM32_T1_E) || (format == IF_ARM32_T1_G) ||
            (format == IF_ARM32_T1_J2) || (format == IF_ARM32_T1_J3) || (format == IF_ARM32_T2_C1) ||
            (format == IF_ARM32_T2_C2) || (format == IF_ARM32_T2_C6) || (format == IF_ARM32_T2_C7) ||
            (format == IF_ARM32_T2_C8) || (format == IF_ARM32_T2_H0) || (format == IF_ARM32_T2_H1) ||
            (format == IF_ARM32_T2_K1) || (format == IF_ARM32_T2_K4) || (format == IF_ARM32_T2_L0) ||
            (format == IF_ARM32_T2_M0) || (format == IF_ARM32_T2_VLDST) || (format == IF_ARM32_T2_M1));
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
