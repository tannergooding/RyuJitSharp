// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSveMovOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsSve_R_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, nint imm1, nint imm2, insOpts opt)
    {
        insFormat fmt;
        nint imm;
        switch (ins)
        {
            case INS_sve_cdot:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                assert(isValidRot(emitDecodeRotationImm0_to_270(imm2)));

                imm = unchecked((imm1 << 2) | imm2);
                if (opt == INS_OPTS_SCALABLE_B)
                {
                    assert(isValidUimm(imm1, 2));
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    fmt = IF_SVE_FA_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_H);
                    assert(isValidUimm(imm1, 1));
                    fmt = IF_SVE_FA_3B;
                }
                break;
            }

            case INS_sve_cmla:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                assert(isValidRot(emitDecodeRotationImm0_to_270(imm2)));
                // Rotation is already encoded in the low two bits; the lane index occupies the remaining bits.
                imm = unchecked((imm1 << 2) | imm2);
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert(isValidUimm(imm1, 2));
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    fmt = IF_SVE_FB_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_S);
                    assert(isValidUimm(imm1, 1));
                    fmt = IF_SVE_FB_3B;
                }
                break;
            }

            case INS_sve_sqrdcmlah:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                assert(isValidRot(emitDecodeRotationImm0_to_270(imm2)));

                imm = unchecked((imm1 << 2) | imm2);
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert(isValidUimm(imm1, 2));
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    fmt = IF_SVE_FC_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_S);
                    assert(isValidUimm(imm1, 1));
                    fmt = IF_SVE_FC_3B;
                }
                break;
            }

            case INS_sve_fcmla:
            {
                assert(opt == INS_OPTS_SCALABLE_S);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                assert(isValidUimm(imm1, 1));
                assert(emitIsValidEncodedRotationImm0_to_270(imm2));
                imm = unchecked((imm1 << 2) | imm2);
                fmt = IF_SVE_GV_3A;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitInsSve_R_R_R_R(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, regNumber reg4, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_fcvt:
            case INS_sve_fcvtx:
            case INS_sve_fcvtzs:
            case INS_sve_fcvtzu:
            case INS_sve_scvtf:
            case INS_sve_ucvtf:
            {
                // Embedded masked convert instructions.
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true,
                    insOptsScalableStandard(opt) ? opt : optGetSveInsOpt(optGetDstsize(opt)), mopt, reg2);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_fcvtlt:
            case INS_sve_fcvtnt:
            case INS_sve_fcvtxnt:
            {
                // Embedded masked convert instructions that cannot use movprfx.
                assert(insOptsConvertFloatToFloat(opt));
                assert(insSveMovOptsUnpredicated(mopt));
                emitInsSve_Mov(INS_sve_mov, EA_SCALABLE, reg1, reg3, true,
                    optGetSveInsOpt(optGetDstsize(opt)), mopt, reg2);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_abs:
            case INS_sve_cls:
            case INS_sve_clz:
            case INS_sve_cnot:
            case INS_sve_cnt:
            case INS_sve_fabs:
            case INS_sve_fexpa:
            case INS_sve_flogb:
            case INS_sve_fneg:
            case INS_sve_frecpx:
            case INS_sve_frinta:
            case INS_sve_frinti:
            case INS_sve_frintm:
            case INS_sve_frintn:
            case INS_sve_frintp:
            case INS_sve_frintx:
            case INS_sve_frintz:
            case INS_sve_fsqrt:
            case INS_sve_neg:
            case INS_sve_not:
            case INS_sve_rbit:
            case INS_sve_revb:
            case INS_sve_revd:
            case INS_sve_revh:
            case INS_sve_revw:
            case INS_sve_sqabs:
            case INS_sve_sqneg:
            case INS_sve_sxtb:
            case INS_sve_sxth:
            case INS_sve_sxtw:
            case INS_sve_urecpe:
            case INS_sve_ursqrte:
            case INS_sve_uxtb:
            case INS_sve_uxth:
            case INS_sve_uxtw:
            {
                // Embedded masked instructions with a single operand.
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, opt, mopt, reg2);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_brkn:
            {
                // RMW on the second source register.
                assert(insSveMovOptsUnpredicated(mopt));
                emitInsSve_Mov(INS_sve_mov, EA_SCALABLE, reg1, reg4, true, opt, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg3, opt, sopt);
                return;
            }

            case INS_sve_fadda:
            {
                emitIns_Mov(INS_fmov, optGetSveElemsize(opt), reg1, reg3, true);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_addp:
            case INS_sve_faddp:
            case INS_sve_fmaxnmp:
            case INS_sve_fmaxp:
            case INS_sve_fminnmp:
            case INS_sve_fminp:
            case INS_sve_smaxp:
            case INS_sve_sminp:
            case INS_sve_umaxp:
            case INS_sve_uminp:
            {
                // Unpredicated movprfx only.
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, INS_OPTS_NONE, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_and:
            case INS_sve_bic:
            case INS_sve_eor:
            case INS_sve_orr:
            {
                if (isPredicateRegister(reg1))
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                    assert(isPredicateRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    assert(isPredicateRegister(reg3));
                    assert(isPredicateRegister(reg4));
                    fmt = IF_SVE_CZ_4A;
                    break;
                }
                goto case INS_sve_add;
            }

            case INS_sve_add:
            case INS_sve_asr:
            case INS_sve_fabd:
            case INS_sve_fadd:
            case INS_sve_fdiv:
            case INS_sve_fmax:
            case INS_sve_fmaxnm:
            case INS_sve_fmin:
            case INS_sve_fminnm:
            case INS_sve_fmul:
            case INS_sve_fmulx:
            case INS_sve_fscale:
            case INS_sve_fsub:
            case INS_sve_lsl:
            case INS_sve_lsr:
            case INS_sve_mul:
            case INS_sve_sabd:
            case INS_sve_sadalp:
            case INS_sve_sdiv:
            case INS_sve_shadd:
            case INS_sve_shsub:
            case INS_sve_smax:
            case INS_sve_smin:
            case INS_sve_sqadd:
            case INS_sve_sqrshl:
            case INS_sve_sqshl:
            case INS_sve_sqsub:
            case INS_sve_srhadd:
            case INS_sve_srshl:
            case INS_sve_sub:
            case INS_sve_suqadd:
            case INS_sve_uabd:
            case INS_sve_uadalp:
            case INS_sve_udiv:
            case INS_sve_uhadd:
            case INS_sve_uhsub:
            case INS_sve_umax:
            case INS_sve_umin:
            case INS_sve_uqadd:
            case INS_sve_uqrshl:
            case INS_sve_uqshl:
            case INS_sve_uqsub:
            case INS_sve_urhadd:
            case INS_sve_urshl:
            case INS_sve_usqadd:
            {
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, opt, mopt, reg2);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_clasta:
            case INS_sve_clastb:
            {
                if (isGeneralRegisterOrZR(reg1))
                {
                    emitIns_Mov(INS_mov, attr, reg1, reg3, true);
                    emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                    return;
                }
                else if (sopt == INS_SCALABLE_OPTS_WITH_SIMD_SCALAR)
                {
                    emitIns_Mov(INS_sve_mov, EA_SCALABLE, reg1, reg3, true, opt);
                    emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                    return;
                }
                goto case INS_sve_splice;
            }

            case INS_sve_splice:
            {
                // Explicit masked RMW instructions.
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg2, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, INS_OPTS_NONE, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg4, opt, sopt);
                return;
            }

            case INS_sve_adclb:
            case INS_sve_adclt:
            {
                // RMW instructions destructive on the third source register.
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg4, reg2, reg3));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg4, true, INS_OPTS_NONE, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg3, opt, sopt);
                return;
            }

            case INS_sve_addhnt:
            case INS_sve_raddhnt:
            case INS_sve_rsubhnt:
            case INS_sve_subhnt:
            case INS_sve_tbx:
            {
                // These RMW instructions do not support movprfx.
                emitIns_Mov(INS_sve_mov, attr, reg1, reg2, true, opt);
                emitInsSve_R_R_R(ins, attr, reg1, reg3, reg4, opt, sopt);
                return;
            }

            case INS_sve_bcax:
            case INS_sve_bsl:
            case INS_sve_bsl1n:
            case INS_sve_bsl2n:
            case INS_sve_eor3:
            case INS_sve_eorbt:
            case INS_sve_eortb:
            case INS_sve_saba:
            case INS_sve_sabalb:
            case INS_sve_sabalt:
            case INS_sve_sbclb:
            case INS_sve_sbclt:
            case INS_sve_sdot:
            case INS_sve_smlalb:
            case INS_sve_smlalt:
            case INS_sve_smlslb:
            case INS_sve_smlslt:
            case INS_sve_sqdmlalb:
            case INS_sve_sqdmlalbt:
            case INS_sve_sqdmlalt:
            case INS_sve_sqdmlslb:
            case INS_sve_sqdmlslbt:
            case INS_sve_sqdmlslt:
            case INS_sve_sqrdmlah:
            case INS_sve_sqrdmlsh:
            case INS_sve_uaba:
            case INS_sve_uabalb:
            case INS_sve_uabalt:
            case INS_sve_udot:
            case INS_sve_umlalb:
            case INS_sve_umlalt:
            case INS_sve_umlslb:
            case INS_sve_umlslt:
            {
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg2, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg2, true, INS_OPTS_NONE, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_R(ins, attr, reg1, reg3, reg4, opt, sopt);
                return;
            }

            case INS_sve_sel:
            {
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg1))
                {
                    if (reg1 == reg4)
                    {
                        // mov is the preferred alias for sel.
                        emitInsSve_R_R_R(INS_sve_mov, attr, reg1, reg2, reg3, opt, INS_SCALABLE_OPTS_PREDICATE_MERGE);
                        return;
                    }
                    assert(insOptsScalableStandard(opt));
                    assert(isPredicateRegister(reg2));
                    assert(isVectorRegister(reg3));
                    assert(isVectorRegister(reg4));
                    fmt = IF_SVE_CW_4A;
                }
                else
                {
                    assert(insOptsScalable(opt));
                    assert(isPredicateRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    assert(isPredicateRegister(reg3));
                    assert(isPredicateRegister(reg4));
                    // Predicate SEL is bitwise; the byte encoding preserves all lane arrangements.
                    opt = INS_OPTS_SCALABLE_B;
                    fmt = IF_SVE_CZ_4A;
                }
                break;
            }

            case INS_sve_cmpeq:
            case INS_sve_cmpgt:
            case INS_sve_cmpge:
            case INS_sve_cmphi:
            case INS_sve_cmphs:
            case INS_sve_cmpne:
            case INS_sve_cmple:
            case INS_sve_cmplo:
            case INS_sve_cmpls:
            case INS_sve_cmplt:
            {
                assert(isPredicateRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isScalableVectorSize(attr));
                if (sopt == INS_SCALABLE_OPTS_WIDE)
                {
                    assert(insOptsScalableWide(opt));
                    fmt = IF_SVE_CX_4A_A;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    assert(insOptsScalableStandard(opt));
                    fmt = IF_SVE_CX_4A;
                }
                break;
            }

            case INS_sve_ands:
            case INS_sve_orn:
            case INS_sve_bics:
            case INS_sve_eors:
            case INS_sve_nor:
            case INS_sve_nand:
            case INS_sve_orrs:
            case INS_sve_orns:
            case INS_sve_nors:
            case INS_sve_nands:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                assert(isPredicateRegister(reg4));
                fmt = IF_SVE_CZ_4A;
                break;
            }

            case INS_sve_brkpa:
            case INS_sve_brkpb:
            case INS_sve_brkpas:
            case INS_sve_brkpbs:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                assert(isPredicateRegister(reg4));
                fmt = IF_SVE_DA_4A;
                break;
            }

            case INS_sve_fcmeq:
            case INS_sve_fcmge:
            case INS_sve_facge:
            case INS_sve_fcmgt:
            case INS_sve_facgt:
            case INS_sve_fcmlt:
            case INS_sve_fcmle:
            case INS_sve_fcmne:
            case INS_sve_fcmuo:
            case INS_sve_facle:
            case INS_sve_faclt:
            {
                assert(insOptsScalableFloat(opt));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isPredicateRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isScalableVectorSize(attr));
                fmt = IF_SVE_HT_4A;
                break;
            }

            case INS_sve_match:
            case INS_sve_nmatch:
            {
                assert(insOptsScalableAtMaxHalf(opt));
                assert(isPredicateRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isScalableVectorSize(attr));
                fmt = IF_SVE_GE_4A;
                break;
            }

            case INS_sve_mla:
            case INS_sve_mls:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_AR_4A;
                break;
            }

            case INS_sve_histcnt:
            {
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_GI_4A;
                break;
            }

            case INS_sve_fmla:
            case INS_sve_fmls:
            case INS_sve_fnmla:
            case INS_sve_fnmls:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_HU_4A;
                break;
            }

            case INS_sve_mad:
            case INS_sve_msb:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_AS_4A;
                break;
            }

            case INS_sve_st1b:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                assert(insScalableOptsNone(sopt));
                if (insOptsScalableStandard(opt))
                {
                    if (isGeneralRegister(reg4))
                    {
                        fmt = IF_SVE_JD_4A;
                    }
                    else
                    {
                        assert(isVectorRegister(reg4));
                        fmt = IF_SVE_JK_4B;
                    }
                }
                else
                {
                    assert(insOptsScalable32bitExtends(opt));
                    switch (opt)
                    {
                        case INS_OPTS_SCALABLE_S_UXTW:
                        case INS_OPTS_SCALABLE_S_SXTW:
                        {
                            fmt = IF_SVE_JK_4A_B;
                            break;
                        }

                        case INS_OPTS_SCALABLE_D_UXTW:
                        case INS_OPTS_SCALABLE_D_SXTW:
                        {
                            fmt = IF_SVE_JK_4A;
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid options for scalable");
                            break;
                        }
                    }
                }
                break;
            }

            case INS_sve_st1h:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                if (insOptsScalableStandard(opt))
                {
                    if (sopt == INS_SCALABLE_OPTS_LSL_N)
                    {
                        if (isGeneralRegister(reg4))
                        {
                            // st1h is reserved for scalable B.
                            assert((ins != INS_sve_st1h) || insOptsScalableAtLeastHalf(opt),
                                conditionExpression: "(ins == INS_sve_st1h) ? insOptsScalableAtLeastHalf(opt) : true");
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            fmt = IF_SVE_JD_4A;
                        }
                        else
                        {
                            assert(isVectorRegister(reg4));
                            fmt = IF_SVE_JJ_4B;
                        }
                    }
                    else
                    {
                        assert(isVectorRegister(reg4));
                        assert(insScalableOptsNone(sopt));
                        fmt = IF_SVE_JJ_4B_E;
                    }
                }
                else
                {
                    assert(insOptsScalable32bitExtends(opt));
                    switch (opt)
                    {
                        case INS_OPTS_SCALABLE_S_UXTW:
                        case INS_OPTS_SCALABLE_S_SXTW:
                        {
                            if (insScalableOptsNone(sopt))
                            {
                                fmt = IF_SVE_JJ_4A_D;
                            }
                            else
                            {
                                assert(sopt == INS_SCALABLE_OPTS_MOD_N);
                                fmt = IF_SVE_JJ_4A;
                            }
                            break;
                        }

                        case INS_OPTS_SCALABLE_D_UXTW:
                        case INS_OPTS_SCALABLE_D_SXTW:
                        {
                            if (insScalableOptsNone(sopt))
                            {
                                fmt = IF_SVE_JJ_4A_C;
                            }
                            else
                            {
                                assert(sopt == INS_SCALABLE_OPTS_MOD_N);
                                fmt = IF_SVE_JJ_4A_B;
                            }
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid options for scalable");
                            break;
                        }
                    }
                }
                break;
            }

            case INS_sve_st1w:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                if (insOptsScalableStandard(opt))
                {
                    if (sopt == INS_SCALABLE_OPTS_LSL_N)
                    {
                        if (isGeneralRegister(reg4))
                        {
                            fmt = IF_SVE_JD_4B;
                        }
                        else
                        {
                            assert(isVectorRegister(reg4));
                            fmt = IF_SVE_JJ_4B;
                        }
                    }
                    else
                    {
                        assert(isVectorRegister(reg4));
                        assert(insScalableOptsNone(sopt));
                        fmt = IF_SVE_JJ_4B_E;
                    }
                }
                else if (opt == INS_OPTS_SCALABLE_Q)
                {
                    assert(isGeneralRegister(reg4));
                    assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                    fmt = IF_SVE_JD_4C;
                }
                else
                {
                    assert(insOptsScalable32bitExtends(opt));
                    assert(isVectorRegister(reg4));
                    switch (opt)
                    {
                        case INS_OPTS_SCALABLE_S_UXTW:
                        case INS_OPTS_SCALABLE_S_SXTW:
                        {
                            if (insScalableOptsNone(sopt))
                            {
                                fmt = IF_SVE_JJ_4A_D;
                            }
                            else
                            {
                                assert(sopt == INS_SCALABLE_OPTS_MOD_N);
                                fmt = IF_SVE_JJ_4A;
                            }
                            break;
                        }

                        case INS_OPTS_SCALABLE_D_UXTW:
                        case INS_OPTS_SCALABLE_D_SXTW:
                        {
                            if (insScalableOptsNone(sopt))
                            {
                                fmt = IF_SVE_JJ_4A_C;
                            }
                            else
                            {
                                assert(sopt == INS_SCALABLE_OPTS_MOD_N);
                                fmt = IF_SVE_JJ_4A_B;
                            }
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid options for scalable");
                            break;
                        }
                    }
                }
                break;
            }

            case INS_sve_st1d:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                if (isGeneralRegister(reg4))
                {
                    assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                    if (opt == INS_OPTS_SCALABLE_Q)
                    {
                        fmt = IF_SVE_JD_4C_A;
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        fmt = IF_SVE_JD_4C;
                    }
                }
                else
                {
                    assert(isVectorRegister(reg4));
                    if (opt == INS_OPTS_SCALABLE_D)
                    {
                        if (sopt == INS_SCALABLE_OPTS_LSL_N)
                        {
                            fmt = IF_SVE_JJ_4B;
                        }
                        else
                        {
                            assert(insScalableOptsNone(sopt));
                            fmt = IF_SVE_JJ_4B_C;
                        }
                    }
                    else
                    {
                        assert(insOptsScalable32bitExtends(opt));
                        switch (opt)
                        {
                            case INS_OPTS_SCALABLE_D_UXTW:
                            case INS_OPTS_SCALABLE_D_SXTW:
                            {
                                if (sopt == INS_SCALABLE_OPTS_MOD_N)
                                {
                                    fmt = IF_SVE_JJ_4A;
                                }
                                else
                                {
                                    assert(insScalableOptsNone(sopt));
                                    fmt = IF_SVE_JJ_4A_B;
                                }
                                break;
                            }

                            default:
                            {
                                assert(false, "Invalid options for scalable");
                                break;
                            }
                        }
                    }
                }
                break;
            }

            case INS_sve_ld1b:
            case INS_sve_ld1sb:
            case INS_sve_ldff1b:
            case INS_sve_ldff1sb:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                assert(insScalableOptsNone(sopt));
                if (isGeneralRegisterOrZR(reg4))
                {
                    switch (ins)
                    {
                        case INS_sve_ldff1b:
                        {
                            assert(insOptsScalableStandard(opt));
                            fmt = IF_SVE_IG_4A_E;
                            break;
                        }

                        case INS_sve_ldff1sb:
                        {
                            assert(insOptsScalableAtLeastHalf(opt));
                            fmt = IF_SVE_IG_4A_D;
                            break;
                        }

                        case INS_sve_ld1sb:
                        {
                            assert(insOptsScalableAtLeastHalf(opt));
                            fmt = IF_SVE_IK_4A_F;
                            break;
                        }

                        case INS_sve_ld1b:
                        {
                            assert(insOptsScalableStandard(opt));
                            fmt = IF_SVE_IK_4A_H;
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid instruction");
                            break;
                        }
                    }
                }
                else
                {
                    assert(isVectorRegister(reg4));
                    if (insOptsScalableDoubleWord32bitExtends(opt))
                    {
                        fmt = IF_SVE_HW_4A;
                    }
                    else if (insOptsScalableSingleWord32bitExtends(opt))
                    {
                        fmt = IF_SVE_HW_4A_A;
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        fmt = IF_SVE_HW_4B;
                    }
                }
                break;
            }

            case INS_sve_ld1h:
            case INS_sve_ld1sh:
            case INS_sve_ldff1h:
            case INS_sve_ldff1sh:
            case INS_sve_ld1w:
            case INS_sve_ldff1w:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                if (isGeneralRegisterOrZR(reg4))
                {
                    assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                    switch (ins)
                    {
                        case INS_sve_ldff1h:
                        {
                            assert(insOptsScalableStandard(opt));
                            fmt = IF_SVE_IG_4A_G;
                            break;
                        }

                        case INS_sve_ldff1sh:
                        case INS_sve_ldff1w:
                        {
                            assert(insOptsScalableWords(opt));
                            fmt = IF_SVE_IG_4A_F;
                            break;
                        }

                        case INS_sve_ld1w:
                        {
                            assert(insOptsScalableWordsOrQuadwords(opt));
                            fmt = IF_SVE_II_4A_H;
                            break;
                        }

                        case INS_sve_ld1sh:
                        {
                            assert(insOptsScalableWords(opt));
                            fmt = IF_SVE_IK_4A_G;
                            break;
                        }

                        case INS_sve_ld1h:
                        {
                            assert(insOptsScalableAtLeastHalf(opt));
                            fmt = IF_SVE_IK_4A_I;
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid instruction");
                            break;
                        }
                    }
                }
                else
                {
                    assert(isVectorRegister(reg4));
                    if (insOptsScalableDoubleWord32bitExtends(opt))
                    {
                        if (sopt == INS_SCALABLE_OPTS_MOD_N)
                        {
                            fmt = IF_SVE_HW_4A_A;
                        }
                        else
                        {
                            assert(insScalableOptsNone(sopt));
                            fmt = IF_SVE_HW_4A_B;
                        }
                    }
                    else if (insOptsScalableSingleWord32bitExtends(opt))
                    {
                        if (sopt == INS_SCALABLE_OPTS_MOD_N)
                        {
                            fmt = IF_SVE_HW_4A;
                        }
                        else
                        {
                            assert(insScalableOptsNone(sopt));
                            fmt = IF_SVE_HW_4A_C;
                        }
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        if (sopt == INS_SCALABLE_OPTS_LSL_N)
                        {
                            fmt = IF_SVE_HW_4B;
                        }
                        else
                        {
                            assert(insScalableOptsNone(sopt));
                            fmt = IF_SVE_HW_4B_D;
                        }
                    }
                }
                break;
            }

            case INS_sve_ld1d:
            case INS_sve_ld1sw:
            case INS_sve_ldff1d:
            case INS_sve_ldff1sw:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isScalableVectorSize(size));
                if (isGeneralRegisterOrZR(reg4))
                {
                    assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                    if (opt == INS_OPTS_SCALABLE_Q)
                    {
                        assert(reg4 != REG_ZR);
                        assert(ins == INS_sve_ld1d);
                        fmt = IF_SVE_II_4A_B;
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        switch (ins)
                        {
                            case INS_sve_ldff1d:
                            case INS_sve_ldff1sw:
                            {
                                fmt = IF_SVE_IG_4A;
                                break;
                            }

                            case INS_sve_ld1d:
                            {
                                assert(reg4 != REG_ZR);
                                fmt = IF_SVE_II_4A;
                                break;
                            }

                            case INS_sve_ld1sw:
                            {
                                assert(reg4 != REG_ZR);
                                fmt = IF_SVE_IK_4A;
                                break;
                            }

                            default:
                            {
                                assert(false, "Invalid instruction");
                                break;
                            }
                        }
                    }
                }
                else if (insOptsScalableDoubleWord32bitExtends(opt))
                {
                    assert(isVectorRegister(reg4));
                    if (sopt == INS_SCALABLE_OPTS_MOD_N)
                    {
                        fmt = IF_SVE_IU_4A;
                    }
                    else
                    {
                        assert(insScalableOptsNone(sopt));
                        if (ins == INS_sve_ld1d)
                        {
                            fmt = IF_SVE_IU_4A_C;
                        }
                        else
                        {
                            fmt = IF_SVE_IU_4A_A;
                        }
                    }
                }
                else if (sopt == INS_SCALABLE_OPTS_LSL_N)
                {
                    assert(isVectorRegister(reg4));
                    assert(opt == INS_OPTS_SCALABLE_D);
                    fmt = IF_SVE_IU_4B;
                }
                else
                {
                    assert(isVectorRegister(reg4));
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(insScalableOptsNone(sopt));
                    if (ins == INS_sve_ld1d)
                    {
                        fmt = IF_SVE_IU_4B_D;
                    }
                    else
                    {
                        fmt = IF_SVE_IU_4B_B;
                    }
                }
                break;
            }

            case INS_sve_ldnt1b:
            case INS_sve_ldnt1h:
            case INS_sve_ldnt1w:
            case INS_sve_ldnt1d:
            case INS_sve_ldnt1sb:
            case INS_sve_ldnt1sh:
            case INS_sve_ldnt1sw:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isScalableVectorSize(size));
                if (isGeneralRegister(reg3))
                {
                    assert(isGeneralRegister(reg4));
#if DEBUG
                    switch (ins)
                    {
                        case INS_sve_ldnt1b:
                        {
                            assert(opt == INS_OPTS_SCALABLE_B);
                            assert(insScalableOptsNone(sopt));
                            break;
                        }

                        case INS_sve_ldnt1h:
                        {
                            assert(opt == INS_OPTS_SCALABLE_H);
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            break;
                        }

                        case INS_sve_ldnt1w:
                        {
                            assert(opt == INS_OPTS_SCALABLE_S);
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            break;
                        }

                        case INS_sve_ldnt1d:
                        {
                            assert(opt == INS_OPTS_SCALABLE_D);
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid instruction");
                            break;
                        }
                    }
#endif
                    fmt = IF_SVE_IN_4A;
                }
                else if ((ins == INS_sve_ldnt1d) || (ins == INS_sve_ldnt1sw))
                {
                    assert(insOptsScalableWords(opt));
                    assert(isVectorRegister(reg3));
                    assert(isGeneralRegisterOrZR(reg4));
                    assert(insScalableOptsNone(sopt));
                    assert(opt == INS_OPTS_SCALABLE_D);
                    fmt = IF_SVE_IX_4A;
                }
                else
                {
                    assert(insOptsScalableWords(opt));
                    assert(isVectorRegister(reg3));
                    assert(isGeneralRegisterOrZR(reg4));
                    assert(insScalableOptsNone(sopt));
                    if (opt == INS_OPTS_SCALABLE_S)
                    {
                        fmt = IF_SVE_IF_4A;
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        fmt = IF_SVE_IF_4A_A;
                    }
                }
                break;
            }

            case INS_sve_ld1rob:
            case INS_sve_ld1roh:
            case INS_sve_ld1row:
            case INS_sve_ld1rod:
            case INS_sve_ld1rqb:
            case INS_sve_ld1rqh:
            case INS_sve_ld1rqw:
            case INS_sve_ld1rqd:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(isScalableVectorSize(size));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_ld1rob:
                    case INS_sve_ld1rqb:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        assert(insScalableOptsNone(sopt));
                        break;
                    }

                    case INS_sve_ld1roh:
                    case INS_sve_ld1rqh:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    case INS_sve_ld1row:
                    case INS_sve_ld1rqw:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    case INS_sve_ld1rod:
                    case INS_sve_ld1rqd:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_IP_4A;
                break;
            }

            case INS_sve_ld1q:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isGeneralRegisterOrZR(reg4));
                assert(isScalableVectorSize(size));
                assert(opt == INS_OPTS_SCALABLE_Q);
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_IW_4A;
                break;
            }

            case INS_sve_ld2q:
            case INS_sve_ld3q:
            case INS_sve_ld4q:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(isScalableVectorSize(size));
                assert(opt == INS_OPTS_SCALABLE_Q);
                assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                fmt = IF_SVE_IR_4A;
                break;
            }

            case INS_sve_ld2b:
            case INS_sve_ld3b:
            case INS_sve_ld4b:
            case INS_sve_ld2h:
            case INS_sve_ld3h:
            case INS_sve_ld4h:
            case INS_sve_ld2w:
            case INS_sve_ld3w:
            case INS_sve_ld4w:
            case INS_sve_ld2d:
            case INS_sve_ld3d:
            case INS_sve_ld4d:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(isScalableVectorSize(size));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_ld2b:
                    case INS_sve_ld3b:
                    case INS_sve_ld4b:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        assert(insScalableOptsNone(sopt));
                        break;
                    }

                    case INS_sve_ld2h:
                    case INS_sve_ld3h:
                    case INS_sve_ld4h:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    case INS_sve_ld2w:
                    case INS_sve_ld3w:
                    case INS_sve_ld4w:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    case INS_sve_ld2d:
                    case INS_sve_ld3d:
                    case INS_sve_ld4d:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_IT_4A;
                break;
            }

            case INS_sve_st1q:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isGeneralRegisterOrZR(reg4));
                assert(isScalableVectorSize(size));
                assert(opt == INS_OPTS_SCALABLE_Q);
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_IY_4A;
                break;
            }

            case INS_sve_stnt1b:
            case INS_sve_stnt1h:
            case INS_sve_stnt1w:
            case INS_sve_stnt1d:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isScalableVectorSize(size));
                if (isGeneralRegister(reg3))
                {
                    assert(isGeneralRegister(reg4));
#if DEBUG
                    switch (ins)
                    {
                        case INS_sve_stnt1b:
                        {
                            assert(opt == INS_OPTS_SCALABLE_B);
                            assert(insScalableOptsNone(sopt));
                            break;
                        }

                        case INS_sve_stnt1h:
                        {
                            assert(opt == INS_OPTS_SCALABLE_H);
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            break;
                        }

                        case INS_sve_stnt1w:
                        {
                            assert(opt == INS_OPTS_SCALABLE_S);
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            break;
                        }

                        case INS_sve_stnt1d:
                        {
                            assert(opt == INS_OPTS_SCALABLE_D);
                            assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid instruction");
                            break;
                        }
                    }
#endif
                    fmt = IF_SVE_JB_4A;
                }
                else
                {
                    assert(isVectorRegister(reg3));
                    assert(isGeneralRegisterOrZR(reg4));
                    assert(isScalableVectorSize(size));
                    assert(insScalableOptsNone(sopt));
                    if (opt == INS_OPTS_SCALABLE_S)
                    {
                        fmt = IF_SVE_IZ_4A;
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        if (ins == INS_sve_stnt1d)
                        {
                            fmt = IF_SVE_JA_4A;
                        }
                        else
                        {
                            fmt = IF_SVE_IZ_4A_A;
                        }
                    }
                }
                break;
            }

            case INS_sve_st2b:
            case INS_sve_st3b:
            case INS_sve_st4b:
            case INS_sve_st2h:
            case INS_sve_st3h:
            case INS_sve_st4h:
            case INS_sve_st2w:
            case INS_sve_st3w:
            case INS_sve_st4w:
            case INS_sve_st2d:
            case INS_sve_st3d:
            case INS_sve_st4d:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(isScalableVectorSize(size));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_st2b:
                    case INS_sve_st3b:
                    case INS_sve_st4b:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        assert(insScalableOptsNone(sopt));
                        break;
                    }

                    case INS_sve_st2h:
                    case INS_sve_st3h:
                    case INS_sve_st4h:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    case INS_sve_st2w:
                    case INS_sve_st3w:
                    case INS_sve_st4w:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    case INS_sve_st2d:
                    case INS_sve_st3d:
                    case INS_sve_st4d:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_JC_4A;
                break;
            }

            case INS_sve_st2q:
            case INS_sve_st3q:
            case INS_sve_st4q:
            {
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(isScalableVectorSize(size));
                assert(opt == INS_OPTS_SCALABLE_Q);
                fmt = IF_SVE_JF_4A;
                break;
            }

            case INS_sve_bfmla:
            case INS_sve_bfmls:
            {
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                fmt = IF_SVE_HU_4B;
                break;
            }

            case INS_sve_fmad:
            case INS_sve_fmsb:
            case INS_sve_fnmad:
            case INS_sve_fnmsb:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                fmt = IF_SVE_HV_4A;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
        assert(fmt != IF_NONE);

        // Use preferred comparison aliases, exchanging source operands before recording.
        switch (ins)
        {
            case INS_sve_cmple:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_cmpge;
                break;
            }

            case INS_sve_cmplo:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_cmphi;
                break;
            }

            case INS_sve_cmpls:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_cmphs;
                break;
            }

            case INS_sve_cmplt:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_cmpgt;
                break;
            }

            case INS_sve_facle:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_facge;
                break;
            }

            case INS_sve_faclt:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_facgt;
                break;
            }

            case INS_sve_fcmle:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_fcmge;
                break;
            }

            case INS_sve_fcmlt:
            {
                (reg3, reg4) = (reg4, reg3);
                ins = INS_sve_fcmgt;
                break;
            }

            default:
            {
                break;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idReg4(reg4);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitInsSve_R_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, regNumber reg4, nint imm, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_fcadd:
            case INS_sve_fmla:
            case INS_sve_fmls:
            case INS_sve_fmul:
            case INS_sve_mla:
            case INS_sve_mls:
            {
                if (isPredicateRegister(reg2))
                {
                    // Embedded masked RMW instructions.
                    assert(isValidMovprfxReg(mopt, reg1, reg3, reg4));
                    emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, opt, mopt, reg2);
                    emitInsSve_R_R_R_I(ins, attr, reg1, reg2, reg4, imm, opt);
                    return;
                }
                goto case INS_sve_cdot;
            }

            case INS_sve_cdot:
            case INS_sve_cmla:
            case INS_sve_sdot:
            case INS_sve_smlalb:
            case INS_sve_smlalt:
            case INS_sve_smlslb:
            case INS_sve_smlslt:
            case INS_sve_sqdmlalb:
            case INS_sve_sqdmlalt:
            case INS_sve_sqdmlslb:
            case INS_sve_sqdmlslt:
            case INS_sve_sqrdcmlah:
            case INS_sve_sqrdmlah:
            case INS_sve_sqrdmlsh:
            case INS_sve_udot:
            case INS_sve_umlalb:
            case INS_sve_umlalt:
            case INS_sve_umlslb:
            case INS_sve_umlslt:
            {
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg2, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg2, true, INS_OPTS_NONE, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_R_I(ins, attr, reg1, reg3, reg4, imm, opt);
                return;
            }

            case INS_sve_fcmla:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(isScalableVectorSize(size));
                assert(emitIsValidEncodedRotationImm0_to_270(imm));
                fmt = IF_SVE_GT_4A;
                break;
            }

            case INS_sve_psel:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(insOptsScalableStandard(opt));
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert((REG_R12 <= reg4) && (reg4 <= REG_R15));
                switch (opt)
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimm(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimm(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimm(imm, 2));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimm(imm, 1));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                fmt = IF_SVE_DV_4A;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idReg4(reg4);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
