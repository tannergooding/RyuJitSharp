// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.insScalableOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsSve_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_pmov:
            {
                if (opt != INS_OPTS_SCALABLE_B)
                {
                    assert(insOptsScalableStandard(opt));
                    emitInsSve_R_R_I(INS_sve_pmov, attr, reg1, reg2, 0, opt, sopt);
                    return;
                }
                if (isPredicateRegister(reg1))
                {
                    assert(isVectorRegister(reg2));
                    fmt = IF_SVE_CE_2A;
                }
                else
                {
                    assert(isVectorRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    fmt = IF_SVE_CF_2A;
                }
                break;
            }

            case INS_sve_movs:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                fmt = IF_SVE_CZ_4A_A;
                break;
            }

            case INS_sve_mov:
            {
                if (isGeneralRegisterOrSP(reg2))
                {
                    assert(insScalableOptsNone(sopt));
                    assert(insOptsScalableStandard(opt));
                    assert(isVectorRegister(reg1));
#if DEBUG
                    if (opt == INS_OPTS_SCALABLE_D)
                    {
                        assert(size == EA_8BYTE);
                    }
                    else
                    {
                        assert(size == EA_4BYTE);
                    }
#endif
                    reg2 = encodingSPtoZR(reg2);
                    fmt = IF_SVE_CB_2A;
                }
                else if (isVectorRegister(reg1))
                {
                    assert(insOptsScalable(opt));
                    assert(insScalableOptsNone(sopt));
                    assert(isVectorRegister(reg2));
                    fmt = IF_SVE_AU_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                    assert(isPredicateRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    fmt = IF_SVE_CZ_4A_L;
                }
                break;
            }

            case INS_sve_insr:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                if (isVectorRegister(reg2))
                {
                    fmt = IF_SVE_CC_2A;
                }
                else if (isGeneralRegisterOrZR(reg2))
                {
                    fmt = IF_SVE_CD_2A;
                }
                else
                {
                    unreached();
                }
                break;
            }

            case INS_sve_pfirst:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                fmt = IF_SVE_DD_2A;
                break;
            }

            case INS_sve_pnext:
            {
                assert(insOptsScalableStandard(opt));
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_DF_2A;
                break;
            }

            case INS_sve_punpkhi:
            case INS_sve_punpklo:
            {
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                fmt = IF_SVE_CK_2A;
                break;
            }

            case INS_sve_rdffr:
            case INS_sve_rdffrs:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                fmt = IF_SVE_DG_2A;
                break;
            }

            case INS_sve_rev:
            {
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg1))
                {
                    assert(insOptsScalableStandard(opt));
                    assert(isVectorRegister(reg2));
                    assert(isScalableVectorSize(size));
                    fmt = IF_SVE_CG_2A;
                }
                else
                {
                    assert(insOptsScalableStandard(opt));
                    assert(isPredicateRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    fmt = IF_SVE_CJ_2A;
                }
                break;
            }

            case INS_sve_ptest:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                fmt = IF_SVE_DI_2A;
                break;
            }

            case INS_sve_cntp:
            {
                assert(isScalableVectorSize(size));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsWithVectorLength(sopt));
                assert(isGeneralRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_DL_2A;
                break;
            }

            case INS_sve_incp:
            case INS_sve_decp:
            {
                assert(isPredicateRegister(reg2));
                if (isGeneralRegister(reg1))
                {
                    assert(insOptsScalableStandard(opt));
                    assert(size == EA_8BYTE);
                    fmt = IF_SVE_DM_2A;
                }
                else
                {
                    assert(insOptsScalableAtLeastHalf(opt));
                    assert(isVectorRegister(reg1));
                    assert(isScalableVectorSize(size));
                    fmt = IF_SVE_DN_2A;
                }
                break;
            }

            case INS_sve_sqincp:
            case INS_sve_uqincp:
            case INS_sve_sqdecp:
            case INS_sve_uqdecp:
            {
                assert(isPredicateRegister(reg2));
                if (isGeneralRegister(reg1))
                {
                    assert(insOptsScalableStandard(opt));
                    assert(isValidGeneralDatasize(size));
                    fmt = IF_SVE_DO_2A;
                }
                else
                {
                    assert(insOptsScalableAtLeastHalf(opt));
                    assert(isVectorRegister(reg1));
                    assert(isScalableVectorSize(size));
                    fmt = IF_SVE_DP_2A;
                }
                break;
            }

            case INS_sve_ctermeq:
            case INS_sve_ctermne:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isValidGeneralDatasize(size));
                fmt = IF_SVE_DS_2A;
                break;
            }

            case INS_sve_sqcvtn:
            case INS_sve_uqcvtn:
            case INS_sve_sqcvtun:
            {
                assert(insOptsNone(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isEvenRegister(reg2));
                fmt = IF_SVE_FZ_2A;
                break;
            }

            case INS_sve_fcvtn:
            case INS_sve_bfcvtn:
            case INS_sve_fcvtnt:
            case INS_sve_fcvtnb:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(insOptsNone(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isEvenRegister(reg2));
                fmt = IF_SVE_HG_2A;
                break;
            }

            case INS_sve_sqxtnb:
            case INS_sve_sqxtnt:
            case INS_sve_uqxtnb:
            case INS_sve_uqxtnt:
            case INS_sve_sqxtunb:
            case INS_sve_sqxtunt:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(optGetSveElemsize(opt) != EA_8BYTE);
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_GD_2A;
                break;
            }

            case INS_sve_aese:
            case INS_sve_aesd:
            case INS_sve_sm4e:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
#if DEBUG
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert(ins == INS_sve_sm4e);
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                }
#endif
                fmt = IF_SVE_GK_2A;
                break;
            }

            case INS_sve_frecpe:
            case INS_sve_frsqrte:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_HF_2A;
                break;
            }

            case INS_sve_sunpkhi:
            case INS_sve_sunpklo:
            case INS_sve_uunpkhi:
            case INS_sve_uunpklo:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWide(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_CH_2A;
                break;
            }

            case INS_sve_fexpa:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_BJ_2A;
                break;
            }

            case INS_sve_dup:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrSP(reg2));
#if DEBUG
                if (opt == INS_OPTS_SCALABLE_D)
                {
                    assert(size == EA_8BYTE);
                }
                else
                {
                    assert(size == EA_4BYTE);
                }
#endif
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_SVE_CB_2A;
                // MOV is the preferred disassembly for DUP.
                ins = INS_sve_mov;
                break;
            }

            case INS_sve_bf1cvt:
            case INS_sve_bf1cvtlt:
            case INS_sve_bf2cvt:
            case INS_sve_bf2cvtlt:
            case INS_sve_f1cvt:
            case INS_sve_f1cvtlt:
            case INS_sve_f2cvt:
            case INS_sve_f2cvtlt:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_HH_2A;
                unreached(); // TODO-SVE: Not yet supported.
                break;
            }

            case INS_sve_movprfx:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsNone(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_BI_2A;
                break;
            }

            case INS_sve_fmov:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_BV_2B;
                // CPY aliases FMOV, and MOV aliases CPY; MOV is the preferred disassembly.
                ins = INS_sve_mov;
                break;
            }

            case INS_sve_ldr:
            case INS_sve_str:
            {
                // The register-only memory forms use the immediate encoding with a zero offset.
                emitInsSve_R_R_I(ins, attr, reg1, reg2, 0, opt, sopt);
                return;
            }

            default:
            {
                unreached();
                break;
            }
        }
        assert(fmt != IF_NONE);

        instrDesc id;
        if (insScalableOptsWithVectorLength(sopt))
        {
            id = emitNewInstr(attr);
            id.idVectorLength4x(sopt == INS_SCALABLE_OPTS_VL_4X);
        }
        else
        {
            id = emitNewInstrSmall(attr);
        }
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitInsSve_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        nint imm, insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var hasShift = false;
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_asr:
            case INS_sve_lsl:
            case INS_sve_lsr:
            case INS_sve_srshr:
            case INS_sve_sqshl:
            case INS_sve_urshr:
            case INS_sve_sqshlu:
            case INS_sve_uqshl:
            case INS_sve_asrd:
            {
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(isValidVectorShiftAmount(imm, optGetSveElemsize(opt), isRightShift));
                assert(insOptsScalableStandard(opt));
                assert(isScalableVectorSize(size));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    assert((ins == INS_sve_asr) || (ins == INS_sve_lsl) || (ins == INS_sve_lsr));
                    assert(isVectorRegister(reg1));
                    fmt = IF_SVE_BF_2A;
                }
                else
                {
                    assert(isVectorRegister(reg1));
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_AM_2A;
                }
                break;
            }

            case INS_sve_xar:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                switch (opt)
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimmFrom1(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimmFrom1(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimmFrom1(imm, 5));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimmFrom1(imm, 6));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                fmt = IF_SVE_AW_2A;
                break;
            }

            case INS_sve_index:
            {
                assert(insOptsScalable(opt));
                assert(isVectorRegister(reg1));
                assert(isValidSimm(imm, 5));
                assert(isIntegerRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                if (sopt == INS_SCALABLE_OPTS_IMM_FIRST)
                {
                    fmt = IF_SVE_AY_2A;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    fmt = IF_SVE_AZ_2A;
                }
                break;
            }

            case INS_sve_addvl:
            case INS_sve_addpl:
            {
                assert(insOptsNone(opt));
                assert(size == EA_8BYTE);
                assert(isGeneralRegisterOrSP(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                assert(isValidSimm(imm, 6));
                reg1 = encodingSPtoZR(reg1);
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_SVE_BB_2A;
                break;
            }

            case INS_sve_mov:
            {
                if (isVectorRegister(reg2))
                {
                    emitInsSve_R_R_I(INS_sve_dup, attr, reg1, reg2, imm, opt, sopt);
                    return;
                }
                goto case INS_sve_cpy;
            }

            case INS_sve_cpy:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                if (!isValidSimm(imm, 8))
                {
                    assert(isValidSimm_MultipleOf(imm, 8, 256));
                    assert(insOptsScalableAtLeastHalf(opt));
                    hasShift = true;
                    imm >>= 8;
                }
                if (sopt == INS_SCALABLE_OPTS_PREDICATE_MERGE)
                {
                    fmt = IF_SVE_BV_2A_J;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    fmt = IF_SVE_BV_2A;
                }
                ins = INS_sve_mov;
                break;
            }

            case INS_sve_dup:
            {
                assert(insOptsScalable(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidBroadcastImm(imm, optGetSveElemsize(opt)));
                fmt = IF_SVE_BW_2A;
                ins = INS_sve_mov;
                break;
            }

            case INS_sve_pmov:
            {
                if (isPredicateRegister(reg1))
                {
                    assert(isVectorRegister(reg2));
                    switch (opt)
                    {
                        case INS_OPTS_SCALABLE_D:
                        {
                            assert(isValidUimm(imm, 3));
                            fmt = IF_SVE_CE_2B;
                            break;
                        }

                        case INS_OPTS_SCALABLE_S:
                        {
                            assert(isValidUimm(imm, 2));
                            fmt = IF_SVE_CE_2D;
                            break;
                        }

                        case INS_OPTS_SCALABLE_H:
                        {
                            assert(isValidUimm(imm, 1));
                            fmt = IF_SVE_CE_2C;
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }
                else
                {
                    assert(isVectorRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    switch (opt)
                    {
                        case INS_OPTS_SCALABLE_D:
                        {
                            assert(isValidUimm(imm, 3));
                            fmt = IF_SVE_CF_2B;
                            break;
                        }

                        case INS_OPTS_SCALABLE_S:
                        {
                            assert(isValidUimm(imm, 2));
                            fmt = IF_SVE_CF_2D;
                            break;
                        }

                        case INS_OPTS_SCALABLE_H:
                        {
                            assert(isValidUimm(imm, 1));
                            fmt = IF_SVE_CF_2C;
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }
                break;
            }

            case INS_sve_sqrshrn:
            case INS_sve_sqrshrun:
            case INS_sve_uqrshrn:
            {
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isEvenRegister(reg2));
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isRightShift);
                assert(isValidVectorShiftAmount(imm, EA_4BYTE, isRightShift));
                fmt = IF_SVE_GA_2A;
                break;
            }

            case INS_sve_pext:
            {
                assert(insOptsScalableStandard(opt));
                assert(isPredicateRegister(reg1));
                assert(isHighPredicateRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                if (sopt == INS_SCALABLE_OPTS_WITH_PREDICATE_PAIR)
                {
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_DW_2B;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_DW_2A;
                }
                break;
            }

            case INS_sve_sshllb:
            case INS_sve_sshllt:
            case INS_sve_ushllb:
            case INS_sve_ushllt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                switch (opt)
                {
                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimm(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimm(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimm(imm, 5));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                fmt = IF_SVE_FR_2A;
                break;
            }

            case INS_sve_sqshrunb:
            case INS_sve_sqshrunt:
            case INS_sve_sqrshrunb:
            case INS_sve_sqrshrunt:
            case INS_sve_shrnb:
            case INS_sve_shrnt:
            case INS_sve_rshrnb:
            case INS_sve_rshrnt:
            case INS_sve_sqshrnb:
            case INS_sve_sqshrnt:
            case INS_sve_sqrshrnb:
            case INS_sve_sqrshrnt:
            case INS_sve_uqshrnb:
            case INS_sve_uqshrnt:
            case INS_sve_uqrshrnb:
            case INS_sve_uqrshrnt:
            {
                assert(insOptsScalableWide(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                switch (opt)
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimmFrom1(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimmFrom1(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimmFrom1(imm, 5));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                fmt = IF_SVE_GB_2A;
                break;
            }

            case INS_sve_cadd:
            case INS_sve_sqcadd:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                assert(isValidRot(emitDecodeRotationImm90_or_270(imm)));
                fmt = IF_SVE_FV_2A;
                break;
            }

            case INS_sve_ftmad:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidUimm(imm, 3));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_HN_2A;
                break;
            }

            case INS_sve_ldr:
            {
                assert(insOptsNone(opt));
                assert(isScalableVectorSize(size));
                assert(isGeneralRegisterOrSP(reg2));
                assert(insScalableOptsNone(sopt));
                assert(isValidSimm(imm, 9));
                reg2 = encodingSPtoZR(reg2);
                if (isVectorRegister(reg1))
                {
                    fmt = IF_SVE_IE_2A;
                }
                else
                {
                    assert(isPredicateRegister(reg1));
                    fmt = IF_SVE_ID_2A;
                }
                break;
            }

            case INS_sve_str:
            {
                assert(insOptsNone(opt));
                assert(isScalableVectorSize(size));
                assert(isGeneralRegisterOrSP(reg2));
                assert(insScalableOptsNone(sopt));
                assert(isValidSimm(imm, 9));
                reg2 = encodingSPtoZR(reg2);
                if (isVectorRegister(reg1))
                {
                    fmt = IF_SVE_JH_2A;
                }
                else
                {
                    assert(isPredicateRegister(reg1));
                    fmt = IF_SVE_JG_2A;
                }
                break;
            }

            case INS_sve_sli:
            case INS_sve_sri:
            {
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(isValidVectorShiftAmount(imm, optGetSveElemsize(opt), isRightShift));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_FT_2A;
                break;
            }

            case INS_sve_srsra:
            case INS_sve_ssra:
            case INS_sve_ursra:
            case INS_sve_usra:
            {
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(isValidVectorShiftAmount(imm, optGetSveElemsize(opt), isRightShift));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_FU_2A;
                break;
            }

            case INS_sve_ext:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidUimm(imm, 8));
                if (sopt == INS_SCALABLE_OPTS_WITH_VECTOR_PAIR)
                {
                    fmt = IF_SVE_BQ_2A;
                    unreached(); // TODO-SVE: Not yet supported.
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    fmt = IF_SVE_BQ_2B;
                }
                break;
            }

            case INS_sve_dupq:
            {
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
#if DEBUG
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
                        break;
                    }
                }
#endif
                fmt = IF_SVE_BX_2A;
                break;
            }

            case INS_sve_extq:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isScalableVectorSize(size));
                assert(isValidUimm(imm, 4));
                fmt = IF_SVE_BY_2A;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        // Small descriptors have no shift bit. A shifted immediate must use a normal descriptor
        // even when its reduced value fits the small constant field.
        var id = !hasShift ? emitNewInstrSC(attr, imm) : emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idHasShift(hasShift);

        dispIns(id);
        appendToCurIG(id);
    }

    private static nint emitDecodeRotationImm90_or_270(nint imm)
    {
        assert(emitIsValidEncodedRotationImm0_to_270(imm));
        switch (imm)
        {
            case 0:
            {
                return 90;
            }

            case 1:
            {
                return 270;
            }

            default:
            {
                break;
            }
        }

        return 0;
    }
}
#endif
