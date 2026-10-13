// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isArm32LowRegister(regNumber reg)
    {
        return reg <= REG_ARM32_R7;
    }

    private static bool isArm32GeneralRegister(regNumber reg)
    {
        return reg <= REG_ARM32_R15;
    }

    private static bool isArm32FloatRegister(regNumber reg)
    {
        return (uint)reg >= (uint)REG_ARM32_F0 && (uint)reg <= (uint)REG_ARM32_F31;
    }

    private static bool isArm32DoubleRegister(regNumber reg)
    {
        return isArm32FloatRegister(reg) && ((uint)reg % 2) == 0;
    }

    private static bool isArm32MovInstruction(instruction ins)
    {
        return ins is INS_mov or INS_sxtb or INS_sxth or INS_uxtb or INS_uxth or
            INS_vmov or INS_vmov_i2f or INS_vmov_f2i;
    }

    private static bool isArm32ModifiedImmediate(int value)
    {
        return TargetCostEstimation.IsArm32ModifiedImmediate(value);
    }

    private static uint unsignedAbsArm32(int value)
    {
        return value < 0 ? unchecked(0u - (uint)value) : (uint)value;
    }

    private static bool insSetsFlagsArm32(insFlags flags)
    {
        return flags != INS_FLAGS_NOT_SET;
    }

    private static bool insDoesNotSetFlagsArm32(insFlags flags)
    {
        return flags != INS_FLAGS_SET;
    }

    private static insFlags insMustSetFlagsArm32(insFlags flags)
    {
        return flags == INS_FLAGS_SET ? INS_FLAGS_SET : INS_FLAGS_NOT_SET;
    }

    private static bool insOptsNoneArm32(insOpts opt)
    {
        return opt == INS_OPTS_NONE;
    }

    private static bool insOptAnyIncArm32(insOpts opt)
    {
        return opt == INS_OPTS_LDST_PRE_DEC || opt == INS_OPTS_LDST_POST_INC;
    }

    private static bool insOptsPostIncArm32(insOpts opt)
    {
        return opt == INS_OPTS_LDST_POST_INC;
    }

    private static bool insOptAnyShiftArm32(insOpts opt)
    {
        var value = (uint)opt;
        return value >= (uint)INS_OPTS_RRX && value <= (uint)INS_OPTS_ROR;
    }

    private static bool insOptsRRXArm32(insOpts opt)
    {
        return opt == INS_OPTS_RRX;
    }

    private static bool insOptsLSLArm32(insOpts opt)
    {
        return opt == INS_OPTS_LSL;
    }

    private insFormat emitInsFormatArm32(instruction ins)
    {
        RequireArm32Target();
        var ordinal = InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins);
        assert((uint)ordinal < (uint)s_armInstructionFormats.Length);
        assert(s_armInstructionFormats[ordinal] != IF_NONE);
        return s_armInstructionFormats[ordinal];
    }

    private static insSize emitInsSize(insFormat format)
    {
        if ((format >= IF_ARM32_T1_A) && (format < IF_ARM32_T2_A))
        {
            return ISZ_16BIT;
        }

        if ((format >= IF_ARM32_T2_A) && (format < IF_ARM32_ARM_INVALID))
        {
            return ISZ_32BIT;
        }

        if (format == IF_ARM32_LARGEJMP)
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
        bool hasLr;
        var hasPc = false;
        var useThumb2 = false;
        bool isSingleBit;

        switch (ins)
        {
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
                    format = IF_ARM32_T1_L1;
                }
                else if (!isSingleBit)
                {
                    format = IF_ARM32_T2_I1;
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
                    format = IF_ARM32_T2_B;
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

        assert((format == IF_ARM32_T1_B) || (format == IF_ARM32_T1_L0) || (format == IF_ARM32_T1_L1)
            || (format == IF_ARM32_T2_I1) || (format == IF_ARM32_T2_B));

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
                if (isArm32LowRegister(reg))
                {
                    recordArm32InsI(ins, attr, (nint)(1 << (int)reg));
                    return;
                }

                assert(size == EA_PTRSIZE);
                format = IF_ARM32_T2_E2;
                break;
            }

            case INS_vmrs:
            {
                assert(size == EA_PTRSIZE);
                format = IF_ARM32_T2_E2;
                break;
            }

            case INS_bx:
            {
                assert(size == EA_PTRSIZE);
                format = IF_ARM32_T1_D1;
                break;
            }

            case INS_rsb:
            case INS_mvn:
            {
                recordArm32InsRRI(ins, attr, reg, reg, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                return;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_ARM32_T1_D1) || (format == IF_ARM32_T2_E2));
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
                assert(reg != REG_ARM32_PC);
                if ((reg == TargetRuntimeMetadata.StackPointerBaseRegister) && insDoesNotSetFlagsArm32(flags) && ((imm & 0x01fc) == imm))
                {
                    format = IF_ARM32_T1_F;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else if (isArm32LowRegister(reg) && insSetsFlagsArm32(flags) && (unsignedAbsArm32(imm) <= 0x00ff))
                {
                    if (imm < 0)
                    {
                        ins = ins == INS_add ? INS_sub : INS_add;
                        imm = unchecked(-imm);
                    }

                    format = IF_ARM32_T1_J0;
                    recordedFlags = INS_FLAGS_SET;
                }
                else
                {
                    recordArm32InsRRI(ins, attr, reg, reg, imm, flags, INS_OPTS_NONE);
                    return;
                }
                break;
            }

            case INS_adc:
            {
                assert(reg != REG_ARM32_PC);
                recordArm32InsRRI(ins, attr, reg, reg, imm, flags, INS_OPTS_NONE);
                return;
            }

            case INS_vpush:
            case INS_vpop:
            {
                assert(imm > 0);
                if (attr == EA_8BYTE)
                {
                    assert(isArm32DoubleRegister(reg));
                    assert(imm <= 16);
                    imm *= 2;
                }
                else
                {
                    assert(attr == EA_4BYTE);
                    assert(isArm32FloatRegister(reg));
                    assert(imm <= 16);
                }

                assert((((int)reg - (int)REG_ARM32_F0) + imm) <= 32);
                imm *= 4;
                if (ins == INS_vpush)
                {
                    imm = unchecked(-imm);
                }

                recordedFlags = INS_FLAGS_NOT_SET;
                format = IF_ARM32_T2_VLDST;
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

                if (!isArm32LowRegister(reg))
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
                    format = IF_ARM32_T1_J1;
                }
                else if (!onlyThumb1)
                {
                    format = IF_ARM32_T2_I0;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }

                if (format == IF_ARM32_T2_I0)
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
                assert(reg != REG_ARM32_PC);
                recordArm32InsRRI(ins, attr, reg, reg, imm, flags, INS_OPTS_NONE);
                return;
            }

            case INS_mov:
            {
                assert(!EA_IS_CNS_RELOC(attr));
                if (isArm32LowRegister(reg) && insSetsFlagsArm32(flags) && ((imm & 0x00ff) == imm))
                {
                    format = IF_ARM32_T1_J0;
                    recordedFlags = INS_FLAGS_SET;
                }
                else if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L1;
                    recordedFlags = insMustSetFlagsArm32(flags);
                }
                else if (isArm32ModifiedImmediate(~imm))
                {
                    ins = INS_mvn;
                    imm = ~imm;
                    format = IF_ARM32_T2_L1;
                    recordedFlags = insMustSetFlagsArm32(flags);
                }
                else if (insDoesNotSetFlagsArm32(flags) && ((imm & 0x0000ffff) == imm))
                {
                    ins = INS_movw;
                    format = IF_ARM32_T2_N;
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
                assert(insDoesNotSetFlagsArm32(flags));
                if ((imm & 0x0000ffff) == imm)
                {
                    format = IF_ARM32_T2_N;
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
                if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L1;
                    recordedFlags = insMustSetFlagsArm32(flags);
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
                assert(reg != REG_ARM32_PC);
                assert(!EA_IS_CNS_RELOC(attr));
                assert(insSetsFlagsArm32(flags));
                recordedFlags = INS_FLAGS_SET;
                if (isArm32LowRegister(reg) && ((imm & 0x00ff) == imm))
                {
                    format = IF_ARM32_T1_J0;
                }
                else if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L2;
                }
                else if (isArm32ModifiedImmediate(unchecked(-imm)))
                {
                    ins = INS_cmn;
                    format = IF_ARM32_T2_L2;
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
                assert(reg != REG_ARM32_PC);
                assert(insSetsFlagsArm32(flags));
                recordedFlags = INS_FLAGS_SET;
                if (isArm32ModifiedImmediate(imm))
                {
                    format = IF_ARM32_T2_L2;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }
                break;
            }

            case INS_pld:
            case INS_pldw:
            {
                assert(insDoesNotSetFlagsArm32(flags));
                recordedFlags = INS_FLAGS_NOT_SET;
                if ((imm >= 0) && (imm <= 0x0fff))
                {
                    format = IF_ARM32_T2_K2;
                }
                else if ((imm < 0) && (unchecked(-imm) <= 0x00ff))
                {
                    imm = unchecked(-imm);
                    format = IF_ARM32_T2_H2;
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

        assert((format == IF_ARM32_T1_F) || (format == IF_ARM32_T1_J0) || (format == IF_ARM32_T1_J1) ||
            (format == IF_ARM32_T2_H2) || (format == IF_ARM32_T2_I0) || (format == IF_ARM32_T2_K2) ||
            (format == IF_ARM32_T2_K3) || (format == IF_ARM32_T2_L1) || (format == IF_ARM32_T2_L2) ||
            (format == IF_ARM32_T2_M1) || (format == IF_ARM32_T2_N) || (format == IF_ARM32_T2_VLDST));
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
        assert(isArm32MovInstruction(ins));

        var size = EA_SIZE(attr);
        insFormat format;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_mov:
            {
                if (insDoesNotSetFlagsArm32(flags))
                {
                    if (canSkip && (dstReg == srcReg))
                    {
                        return false;
                    }

                    format = IF_ARM32_T1_D0;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else
                {
                    recordedFlags = INS_FLAGS_SET;
                    format = isArm32LowRegister(dstReg) && isArm32LowRegister(srcReg) ? IF_ARM32_T1_E : IF_ARM32_T2_C3;
                }
                break;
            }

            case INS_vmov:
            {
                assert(dstReg != REG_ARM32_PC);
                assert(srcReg != REG_ARM32_PC);
                if (canSkip && (dstReg == srcReg))
                {
                    return false;
                }

                if (size == EA_8BYTE)
                {
                    assert(isArm32DoubleRegister(dstReg));
                    assert(isArm32DoubleRegister(srcReg));
                }
                else
                {
                    assert(isArm32FloatRegister(dstReg));
                    assert(isArm32FloatRegister(srcReg));
                }

                format = IF_ARM32_T2_VFP2;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_i2f:
            {
                assert(srcReg != REG_ARM32_PC);
                assert(isArm32FloatRegister(dstReg));
                assert(isArm32GeneralRegister(srcReg));
                format = IF_ARM32_T2_VMOVS;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_f2i:
            {
                assert(dstReg != REG_ARM32_PC);
                assert(isArm32GeneralRegister(dstReg));
                assert(isArm32FloatRegister(srcReg));
                format = IF_ARM32_T2_VMOVS;
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

                assert(dstReg != REG_ARM32_PC);
                assert(srcReg != REG_ARM32_PC);
                assert(insDoesNotSetFlagsArm32(flags));
                if (isArm32LowRegister(dstReg) && isArm32LowRegister(srcReg))
                {
                    format = IF_ARM32_T1_E;
                    recordedFlags = INS_FLAGS_NOT_SET;
                }
                else
                {
                    recordArm32InsRRI(ins, attr, dstReg, srcReg, 0, INS_FLAGS_NOT_SET, INS_OPTS_NONE);
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

        assert((format == IF_ARM32_T1_D0) || (format == IF_ARM32_T1_E) || (format == IF_ARM32_T2_C3) ||
            (format == IF_ARM32_T2_VFP2) || (format == IF_ARM32_T2_VMOVS));
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
        RequireArm32Target();
        assert(insOptsNoneArm32(opt));

        if (isArm32MovInstruction(ins))
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
                assert(reg1 != REG_ARM32_PC);
                if (insDoesNotSetFlagsArm32(flags))
                {
                    format = IF_ARM32_T1_D0;
                    recordedFlags = INS_FLAGS_NOT_SET;
                    break;
                }

                recordArm32InsRRR(ins, attr, reg1, reg1, reg2, flags);
                return;
            }

            case INS_sub:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                recordArm32InsRRR(ins, attr, reg1, reg1, reg2, flags);
                return;
            }

            case INS_cmp:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insSetsFlagsArm32(flags));
                recordedFlags = INS_FLAGS_SET;
                format = isArm32LowRegister(reg1) && isArm32LowRegister(reg2) ? IF_ARM32_T1_E : IF_ARM32_T1_D0;
                break;
            }

            case INS_vcvt_d2i:
            case INS_vcvt_d2u:
            case INS_vcvt_d2f:
            {
                assert(isArm32FloatRegister(reg1));
                assert(isArm32DoubleRegister(reg2));
                goto VCVT_COMMON;
            }

            case INS_vcvt_f2d:
            case INS_vcvt_u2d:
            case INS_vcvt_i2d:
            {
                assert(isArm32DoubleRegister(reg1));
                assert(isArm32FloatRegister(reg2));
                goto VCVT_COMMON;
            }

            case INS_vcvt_u2f:
            case INS_vcvt_i2f:
            case INS_vcvt_f2i:
            case INS_vcvt_f2u:
            {
                assert(size == EA_4BYTE);
                assert(isArm32FloatRegister(reg1));
                assert(isArm32FloatRegister(reg2));
                goto VCVT_COMMON;
            }

            case INS_vabs:
            case INS_vsqrt:
            case INS_vcmp:
            case INS_vneg:
            {
                if (size == EA_8BYTE)
                {
                    assert(isArm32DoubleRegister(reg1));
                    assert(isArm32DoubleRegister(reg2));
                }
                else
                {
                    assert(isArm32FloatRegister(reg1));
                    assert(isArm32FloatRegister(reg2));
                }

                goto VCVT_COMMON;
            }

            case INS_vadd:
            case INS_vmul:
            case INS_vsub:
            case INS_vdiv:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
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
                recordArm32InsRRI(ins, attr, reg1, reg2, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                return;
            }

            case INS_adc:
            case INS_and:
            case INS_bic:
            case INS_eor:
            case INS_orr:
            case INS_sbc:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                if (insSetsFlagsArm32(flags) && isArm32LowRegister(reg1) && isArm32LowRegister(reg2))
                {
                    format = IF_ARM32_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                goto case INS_orn;
            }

            case INS_orn:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                recordArm32InsRRRImm(ins, attr, reg1, reg1, reg2, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                if (insSetsFlagsArm32(flags) && isArm32LowRegister(reg1) && isArm32LowRegister(reg2))
                {
                    format = IF_ARM32_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                goto case INS_ror;
            }

            case INS_ror:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                format = IF_ARM32_T2_C4;
                recordedFlags = insMustSetFlagsArm32(flags);
                break;
            }

            case INS_mul:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                if ((insMustSetFlagsArm32(flags) == INS_FLAGS_SET) && isArm32LowRegister(reg1) && (reg1 == reg2))
                {
                    format = IF_ARM32_T1_E;
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                if (insSetsFlagsArm32(flags) && isArm32LowRegister(reg1) && isArm32LowRegister(reg2))
                {
                    format = IF_ARM32_T1_E;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                recordArm32InsRRI(ins, attr, reg1, reg2, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_tbb:
            case INS_tbh:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_C9;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_clz:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_C10;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrexb:
            case INS_strexb:
            case INS_ldrexh:
            case INS_strexh:
            {
                assert(size == EA_4BYTE);
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_E1;
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
        format = IF_ARM32_T2_VFP2;
        recordedFlags = INS_FLAGS_NOT_SET;

    RECORD:
        assert((format == IF_ARM32_T1_D0) || (format == IF_ARM32_T1_E) || (format == IF_ARM32_T2_C3) ||
            (format == IF_ARM32_T2_C9) || (format == IF_ARM32_T2_C10) || (format == IF_ARM32_T2_VFP2) ||
            (format == IF_ARM32_T2_VMOVD) || (format == IF_ARM32_T2_VMOVS) || (format == IF_ARM32_T2_E1));
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
        insFormat format;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_add:
            {
                if (reg3 == TargetRuntimeMetadata.StackPointerBaseRegister)
                {
                    (reg2, reg3) = (TargetRuntimeMetadata.StackPointerBaseRegister, reg2);
                }

                goto case INS_sub;
            }

            case INS_sub:
            {
                assert(reg3 != TargetRuntimeMetadata.StackPointerBaseRegister);
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert((reg3 != REG_ARM32_PC) || (ins == INS_add));
                if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && isArm32LowRegister(reg3) && insSetsFlagsArm32(flags))
                {
                    format = IF_ARM32_T1_H;
                    recordedFlags = INS_FLAGS_SET;
                    break;
                }

                if ((ins == INS_add) && insDoesNotSetFlagsArm32(flags))
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                if (reg1 == reg2)
                {
                    recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                    return;
                }

                goto case INS_orn;
            }

            case INS_orn:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                recordArm32InsRRRImm(ins, attr, reg1, reg2, reg3, 0, flags, INS_OPTS_NONE);
                return;
            }

            case INS_asr:
            case INS_lsl:
            case INS_lsr:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                if ((reg1 == reg2) && insSetsFlagsArm32(flags) && isArm32LowRegister(reg1) && isArm32LowRegister(reg3))
                {
                    recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                    return;
                }

                goto case INS_ror;
            }

            case INS_ror:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                format = IF_ARM32_T2_C4;
                recordedFlags = insMustSetFlagsArm32(flags);
                break;
            }

            case INS_mul:
            {
                if (insMustSetFlagsArm32(flags) == INS_FLAGS_SET)
                {
                    assert(reg1 != REG_ARM32_PC);
                    assert(reg2 != REG_ARM32_PC);
                    assert(reg3 != REG_ARM32_PC);

                    if ((reg1 == reg2) && isArm32LowRegister(reg1))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg3, flags, INS_OPTS_NONE);
                        return;
                    }
                    if ((reg1 == reg3) && isArm32LowRegister(reg1))
                    {
                        recordArm32InsRR(ins, attr, reg1, reg2, flags, INS_OPTS_NONE);
                        return;
                    }

                    assert(false, "Instruction cannot be encoded.");
                    unreached();
                    return;
                }

                if (!Configuration.USE_HELPERS_FOR_INT_DIV)
                {
                    goto case INS_sdiv;
                }

                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_C5;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_sdiv:
            case INS_udiv:
            {
                if (Configuration.USE_HELPERS_FOR_INT_DIV)
                {
                    unreached();
                    return;
                }

                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_C5;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

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
                assert(insDoesNotSetFlagsArm32(flags));
                if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && isArm32LowRegister(reg3))
                {
                    format = IF_ARM32_T1_H;
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
                    assert(isArm32DoubleRegister(reg1));
                    assert(isArm32DoubleRegister(reg2));
                    assert(isArm32DoubleRegister(reg3));
                }
                else
                {
                    assert(isArm32FloatRegister(reg1));
                    assert(isArm32FloatRegister(reg2));
                    assert(isArm32FloatRegister(reg3));
                }

                format = IF_ARM32_T2_VFP3;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_i2d:
            {
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                assert(isArm32DoubleRegister(reg1));
                assert(isArm32GeneralRegister(reg2));
                assert(isArm32GeneralRegister(reg3));
                format = IF_ARM32_T2_VMOVD;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_vmov_d2i:
            {
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(isArm32GeneralRegister(reg1));
                assert(isArm32GeneralRegister(reg2));
                assert(isArm32DoubleRegister(reg3));
                format = IF_ARM32_T2_VMOVD;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrexd:
            case INS_strexd:
            {
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_G1;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_ARM32_T1_H) || (format == IF_ARM32_T2_C4) || (format == IF_ARM32_T2_C5) ||
            (format == IF_ARM32_T2_VFP3) || (format == IF_ARM32_T2_VMOVD) || (format == IF_ARM32_T2_G1));
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
                    if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && isArm32LowRegister(reg3) && insSetsFlagsArm32(flags))
                    {
                        recordArm32InsRRR(ins, attr, reg1, reg2, reg3, flags);
                        return;
                    }

                    if ((ins == INS_add) && insDoesNotSetFlagsArm32(flags))
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
                assert(reg1 != REG_ARM32_PC);
                assert(reg2 != REG_ARM32_PC);
                assert(reg3 != REG_ARM32_PC);
                assert((imm >= 0) && (imm <= 31));
                assert(!insOptAnyIncArm32(opt));

                if (imm == 0)
                {
                    if (opt == INS_OPTS_LSL)
                    {
                        opt = INS_OPTS_NONE;
                    }

                    assert(insOptsNoneArm32(opt));
                    if (isArm32LowRegister(reg1) && isArm32LowRegister(reg2) && isArm32LowRegister(reg3) &&
                        insSetsFlagsArm32(flags))
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
                    assert(insOptAnyShiftArm32(opt));
                    if (insOptsRRXArm32(opt))
                    {
                        assert(imm == 1);
                    }
                }

                format = IF_ARM32_T2_C0;
                recordedFlags = insMustSetFlagsArm32(flags);
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
                assert(insDoesNotSetFlagsArm32(flags));
                assert((imm & 0x0003) == imm);

                if ((imm == 0) && insOptsNoneArm32(opt) && isArm32LowRegister(reg1) &&
                    isArm32LowRegister(reg2) && isArm32LowRegister(reg3))
                {
                    recordArm32InsRRR(ins, attr, reg1, reg2, reg3, flags);
                    return;
                }

                assert(insOptsNoneArm32(opt) || insOptsLSLArm32(opt));
                format = IF_ARM32_T2_E0;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            case INS_ldrd:
            case INS_strd:
            {
                assert(insDoesNotSetFlagsArm32(flags));
                assert((imm & 0x03) == 0);
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
                }
                else
                {
                    assert(insOptsNoneArm32(opt));
                }

                if (unsignedAbsArm32(imm) <= 0x03fc)
                {
                    imm >>= 2;
                    format = IF_ARM32_T2_G0;
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

        assert((format == IF_ARM32_T2_C0) || (format == IF_ARM32_T2_E0) || (format == IF_ARM32_T2_G0));
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
                format = IF_ARM32_T2_F1;
                break;
            }

            case INS_mla:
            case INS_mls:
            {
                format = IF_ARM32_T2_F2;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(reg1 != REG_ARM32_PC);
        assert(reg2 != REG_ARM32_PC);
        assert(reg3 != REG_ARM32_PC);
        assert(reg4 != REG_ARM32_PC);

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
        RequireArm32Target();
        insFormat format;
        int encodedImmediate;
        insFlags recordedFlags;

        switch (ins)
        {
            case INS_bfc:
            {
                assert(reg != REG_ARM32_PC);

                var leastSignificantBit = imm1;
                var mostSignificantBit = unchecked(leastSignificantBit + imm2 - 1);
                assert((leastSignificantBit >= 0) && (leastSignificantBit <= 31));
                assert((mostSignificantBit >= 0) && (mostSignificantBit <= 31));
                assert(mostSignificantBit >= leastSignificantBit);

                encodedImmediate = unchecked((leastSignificantBit << 5) | mostSignificantBit);
                assert(insDoesNotSetFlagsArm32(flags));
                format = IF_ARM32_T2_D1;
                recordedFlags = INS_FLAGS_NOT_SET;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(format == IF_ARM32_T2_D1);
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
