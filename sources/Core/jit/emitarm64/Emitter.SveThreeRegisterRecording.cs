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
    public void emitInsSve_R_R_R(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        var size = EA_SIZE(attr);
        var pmerge = false;
        var vectorLength4x = false;
        var fmt = IF_NONE;

        switch (ins)
        {
            case INS_sve_pfirst:
            case INS_sve_pnext:
            {
                // Explicit masked RMW predicate instructions.
                assert(insSveMovOptsUnpredicated(mopt));
                emitInsSve_Mov(INS_sve_mov, EA_SCALABLE, reg1, reg3, true, INS_OPTS_NONE, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R(ins, attr, reg1, reg2, opt, sopt);
                return;
            }

            case INS_sve_sqincp:
            case INS_sve_uqincp:
            case INS_sve_sqdecp:
            case INS_sve_uqdecp:
            {
                if (isGeneralRegister(reg1))
                {
                    // Scalar variant, with 64-bit destination register.
                    assert(isGeneralRegister(reg2));
                    emitIns_Mov(INS_mov, EA_8BYTE, reg1, reg2, true);
                    emitInsSve_R_R(ins, attr, reg1, reg3, opt, sopt);
                    return;
                }
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                goto case INS_sve_insr;
            }

            case INS_sve_insr:
            {
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg2, reg3));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg2, true, INS_OPTS_NONE, mopt);
                emitInsSve_R_R(ins, attr, reg1, reg3, opt, sopt);
                return;
            }

            case INS_sve_sm4e:
            case INS_sve_sqxtnt:
            case INS_sve_uqxtnt:
            case INS_sve_sqxtunt:
            {
                // These RMW instructions do not support movprfx.
                assert(insSveMovOptsUnpredicated(mopt));
                emitIns_Mov(INS_sve_mov, attr, reg1, reg2, true, opt);
                emitInsSve_R_R(ins, attr, reg1, reg3, opt, sopt);
                return;
            }

            case INS_sve_and:
            case INS_sve_bic:
            case INS_sve_eor:
            case INS_sve_orr:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    // The .D-only encoding operates on bits, not lanes, so all standard sizes are supported.
                    assert(insOptsScalableStandard(opt));
                    fmt = IF_SVE_AU_3A;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_AA_3A;
                }
                break;
            }

            case INS_sve_add:
            case INS_sve_sub:
            case INS_sve_subr:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    assert(ins != INS_sve_subr);
                    fmt = IF_SVE_AT_3A;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_AA_3A;
                }
                break;
            }

            case INS_sve_addpt:
            case INS_sve_subpt:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    fmt = IF_SVE_AT_3B;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_AB_3B;
                }
                break;
            }

            case INS_sve_sdiv:
            case INS_sve_sdivr:
            case INS_sve_udiv:
            case INS_sve_udivr:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableWords(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AC_3A;
                break;
            }

            case INS_sve_sabd:
            case INS_sve_smax:
            case INS_sve_smin:
            case INS_sve_uabd:
            case INS_sve_umax:
            case INS_sve_umin:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AA_3A;
                break;
            }

            case INS_sve_mul:
            case INS_sve_smulh:
            case INS_sve_umulh:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    fmt = IF_SVE_AT_3A;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_AA_3A;
                }
                break;
            }

            case INS_sve_pmul:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_BD_3B;
                break;
            }

            case INS_sve_andv:
            case INS_sve_eorv:
            case INS_sve_orv:
            {
                assert(isFloatReg(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AF_3A;
                break;
            }

            case INS_sve_andqv:
            case INS_sve_eorqv:
            case INS_sve_orqv:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AG_3A;
                break;
            }

            case INS_sve_movprfx:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                if (sopt == INS_SCALABLE_OPTS_PREDICATE_MERGE)
                {
                    pmerge = true;
                }
                fmt = IF_SVE_AH_3A;
                break;
            }

            case INS_sve_saddv:
            {
                assert(isFloatReg(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableWide(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AI_3A;
                break;
            }

            case INS_sve_uaddv:
            {
                assert(isFloatReg(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AI_3A;
                break;
            }

            case INS_sve_addqv:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AJ_3A;
                break;
            }

            case INS_sve_smaxv:
            case INS_sve_sminv:
            case INS_sve_umaxv:
            case INS_sve_uminv:
            {
                assert(isFloatReg(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AK_3A;
                break;
            }

            case INS_sve_smaxqv:
            case INS_sve_sminqv:
            case INS_sve_umaxqv:
            case INS_sve_uminqv:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AL_3A;
                break;
            }

            case INS_sve_asrr:
            case INS_sve_lslr:
            case INS_sve_lsrr:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AA_3A;
                break;
            }

            case INS_sve_asr:
            case INS_sve_lsl:
            case INS_sve_lsr:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                if (sopt == INS_SCALABLE_OPTS_WIDE)
                {
                    assert(isLowPredicateRegister(reg2));
                    assert(insOptsScalableWide(opt));
                    fmt = IF_SVE_AO_3A;
                }
                else if (isVectorRegister(reg2))
                {
                    assert(insScalableOptsNone(sopt));
                    assert(insOptsScalableWide(opt));
                    fmt = IF_SVE_BG_3A;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    assert(insScalableOptsNone(sopt));
                    assert(insOptsScalableStandard(opt));
                    fmt = IF_SVE_AA_3A;
                }
                break;
            }

            case INS_sve_uzp1:
            case INS_sve_trn1:
            case INS_sve_zip1:
            case INS_sve_uzp2:
            case INS_sve_trn2:
            case INS_sve_zip2:
            {
                assert(insOptsScalable(opt));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg1))
                {
                    assert(isVectorRegister(reg2));
                    assert(isVectorRegister(reg3));
                    if (opt == INS_OPTS_SCALABLE_Q)
                    {
                        fmt = IF_SVE_BR_3B;
                    }
                    else
                    {
                        assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                        fmt = IF_SVE_AT_3A;
                    }
                }
                else
                {
                    assert(isPredicateRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    assert(isPredicateRegister(reg3));
                    fmt = IF_SVE_CI_3A;
                }
                break;
            }

            case INS_sve_tbl:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                if (sopt == INS_SCALABLE_OPTS_WITH_VECTOR_PAIR)
                {
                    fmt = IF_SVE_BZ_3A_A;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    fmt = IF_SVE_BZ_3A;
                }
                break;
            }

            case INS_sve_tbx:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_BZ_3A;
                break;
            }

            case INS_sve_tbxq:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_sdot:
            case INS_sve_udot:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    fmt = IF_SVE_EF_3A;
                }
                else
                {
                    fmt = IF_SVE_EH_3A;
                    assert(insOptsScalableWords(opt));
                    assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                }
                break;
            }

            case INS_sve_usdot:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_EI_3A;
                break;
            }

            case INS_sve_smlalb:
            case INS_sve_smlalt:
            case INS_sve_umlalb:
            case INS_sve_umlalt:
            case INS_sve_smlslb:
            case INS_sve_smlslt:
            case INS_sve_umlslb:
            case INS_sve_umlslt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EL_3A;
                break;
            }

            case INS_sve_sqrdmlah:
            case INS_sve_sqrdmlsh:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EM_3A;
                break;
            }

            case INS_sve_sqdmlalbt:
            case INS_sve_sqdmlslbt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EL_3A;
                break;
            }

            case INS_sve_sqdmlalb:
            case INS_sve_sqdmlalt:
            case INS_sve_sqdmlslb:
            case INS_sve_sqdmlslt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EL_3A;
                break;
            }

            case INS_sve_sclamp:
            case INS_sve_uclamp:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_zipq1:
            case INS_sve_zipq2:
            case INS_sve_uzpq1:
            case INS_sve_uzpq2:
            case INS_sve_tblq:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EX_3A;
                break;
            }

            case INS_sve_saddlb:
            case INS_sve_saddlt:
            case INS_sve_uaddlb:
            case INS_sve_uaddlt:
            case INS_sve_ssublb:
            case INS_sve_ssublt:
            case INS_sve_usublb:
            case INS_sve_usublt:
            case INS_sve_sabdlb:
            case INS_sve_sabdlt:
            case INS_sve_uabdlb:
            case INS_sve_uabdlt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_FL_3A;
                break;
            }

            case INS_sve_saddwb:
            case INS_sve_saddwt:
            case INS_sve_uaddwb:
            case INS_sve_uaddwt:
            case INS_sve_ssubwb:
            case INS_sve_ssubwt:
            case INS_sve_usubwb:
            case INS_sve_usubwt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_FM_3A;
                break;
            }

            case INS_sve_smullb:
            case INS_sve_smullt:
            case INS_sve_umullb:
            case INS_sve_umullt:
            case INS_sve_sqdmullb:
            case INS_sve_sqdmullt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_FL_3A;
                break;
            }

            case INS_sve_pmullb:
            case INS_sve_pmullt:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_Q)
                {
                    fmt = IF_SVE_FN_3B;
                }
                else
                {
                    assert((opt == INS_OPTS_SCALABLE_H) || (opt == INS_OPTS_SCALABLE_D));
                    assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                    fmt = IF_SVE_FL_3A;
                }
                break;
            }

            case INS_sve_smmla:
            case INS_sve_usmmla:
            case INS_sve_ummla:
            {
                assert(opt == INS_OPTS_SCALABLE_S);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_FO_3A;
                break;
            }

            case INS_sve_rax1:
            case INS_sve_sm4ekey:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (ins == INS_sve_rax1)
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_S);
                }
                fmt = IF_SVE_GJ_3A;
                break;
            }

            case INS_sve_fmlalb:
            case INS_sve_fmlalt:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_B)
                {
                    unreached(); // TODO-SVE: Not yet supported.
                    fmt = IF_SVE_GN_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_H);
                    fmt = IF_SVE_HB_3A;
                }
                break;
            }

            case INS_sve_fmlslb:
            case INS_sve_fmlslt:
            case INS_sve_bfmlalb:
            case INS_sve_bfmlalt:
            case INS_sve_bfmlslb:
            case INS_sve_bfmlslt:
            {
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HB_3A;
                break;
            }

            case INS_sve_bfmmla:
            {
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HD_3A;
                break;
            }

            case INS_sve_fmmla:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HD_3A_A;
                break;
            }

            case INS_sve_fmlallbb:
            case INS_sve_fmlallbt:
            case INS_sve_fmlalltb:
            case INS_sve_fmlalltt:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_GO_3A;
                break;
            }

            case INS_sve_bfclamp:
            {
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_GW_3B;
                break;
            }

            case INS_sve_bfdot:
            {
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HA_3A;
                break;
            }

            case INS_sve_fdot:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    fmt = IF_SVE_HA_3A;
                }
                else if (opt == INS_OPTS_SCALABLE_B)
                {
                    unreached(); // TODO-SVE: Not yet supported.
                    fmt = IF_SVE_HA_3A_E;
                }
                else
                {
                    unreached(); // TODO-SVE: Not yet supported.
                    assert(insOptsNone(opt));
                    fmt = IF_SVE_HA_3A_F;
                }
                break;
            }

            case INS_sve_eorbt:
            case INS_sve_eortb:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_bext:
            case INS_sve_bdep:
            case INS_sve_bgrp:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_saddlbt:
            case INS_sve_ssublbt:
            case INS_sve_ssubltb:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_FL_3A;
                break;
            }

            case INS_sve_saba:
            case INS_sve_uaba:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_FW_3A;
                break;
            }

            case INS_sve_sabalb:
            case INS_sve_sabalt:
            case INS_sve_uabalb:
            case INS_sve_uabalt:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EL_3A;
                break;
            }

            case INS_sve_addhnb:
            case INS_sve_addhnt:
            case INS_sve_raddhnb:
            case INS_sve_raddhnt:
            case INS_sve_subhnb:
            case INS_sve_subhnt:
            case INS_sve_rsubhnb:
            case INS_sve_rsubhnt:
            {
                assert(insOptsScalableWide(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_GC_3A;
                break;
            }

            case INS_sve_histseg:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_GF_3A;
                break;
            }

            case INS_sve_fclamp:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_not:
            {
                assert(insScalableOptsNone(sopt));
                if (isPredicateRegister(reg1))
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                    assert(isPredicateRegister(reg2));
                    assert(isPredicateRegister(reg3));
                    fmt = IF_SVE_CZ_4A;
                }
                else
                {
                    assert(isVectorRegister(reg1));
                    assert(isLowPredicateRegister(reg2));
                    assert(isVectorRegister(reg3));
                    assert(insOptsScalableStandard(opt));
                    fmt = IF_SVE_AP_3A;
                }
                break;
            }

            case INS_sve_nots:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                fmt = IF_SVE_CZ_4A;
                break;
            }

            case INS_sve_clz:
            case INS_sve_cls:
            case INS_sve_cnt:
            case INS_sve_cnot:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AP_3A;
                break;
            }

            case INS_sve_fabs:
            case INS_sve_fneg:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AP_3A;
                break;
            }

            case INS_sve_abs:
            case INS_sve_neg:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AQ_3A;
                break;
            }

            case INS_sve_sxtb:
            case INS_sve_uxtb:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AQ_3A;
                break;
            }

            case INS_sve_sxth:
            case INS_sve_uxth:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableWords(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AQ_3A;
                break;
            }

            case INS_sve_sxtw:
            case INS_sve_uxtw:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AQ_3A;
                break;
            }

            case INS_sve_index:
            {
                assert(isValidScalarDatasize(size));
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                assert(isGeneralRegisterOrZR(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_BA_3A;
                break;
            }

            case INS_sve_sqdmulh:
            case INS_sve_sqrdmulh:
            {
                assert(isScalableVectorSize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_ftssel:
            {
                assert(isScalableVectorSize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_compact:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableWords(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_CL_3A;
                break;
            }

            case INS_sve_clasta:
            case INS_sve_clastb:
            {
                assert(insOptsScalableStandard(opt));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                if (isGeneralRegister(reg1))
                {
                    assert(insScalableOptsNone(sopt));
                    assert(isValidScalarDatasize(size));
                    fmt = IF_SVE_CO_3A;
                }
                else if (sopt == INS_SCALABLE_OPTS_WITH_SIMD_SCALAR)
                {
                    assert(isFloatReg(reg1));
                    assert(isScalableVectorSize(size));
                    fmt = IF_SVE_CN_3A;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    assert(isVectorRegister(reg1));
                    fmt = IF_SVE_CM_3A;
                }
                break;
            }

            case INS_sve_cpy:
            case INS_sve_mov:
            {
                assert(insOptsScalableStandard(opt));
                if (isVectorRegister(reg1))
                {
                    if (sopt == INS_SCALABLE_OPTS_PREDICATE_MERGE)
                    {
                        assert(isPredicateRegister(reg2));
                        assert(isVectorRegister(reg3));
                        fmt = IF_SVE_CW_4A;
                    }
                    else if (sopt == INS_SCALABLE_OPTS_WITH_SIMD_SCALAR)
                    {
                        assert(isLowPredicateRegister(reg2));
                        assert(isVectorRegister(reg3));
                        fmt = IF_SVE_CP_3A;
                        // MOV is an alias for CPY, and is always the preferred disassembly.
                        ins = INS_sve_mov;
                    }
                    else if (isLowPredicateRegister(reg2))
                    {
                        assert(isGeneralRegisterOrSP(reg3));
                        assert(insScalableOptsNone(sopt));
                        fmt = IF_SVE_CQ_3A;
                        reg3 = encodingSPtoZR(reg3);
                        // MOV is an alias for CPY, and is always the preferred disassembly.
                        ins = INS_sve_mov;
                    }
                    else
                    {
                        assert(insScalableOptsNone(sopt));
                        assert(ins == INS_sve_mov);
                        assert(isVectorRegister(reg2));
                        assert(isVectorRegister(reg3));
                        fmt = IF_SVE_AU_3A;
                        // ORR is an alias for MOV, and is always the preferred disassembly.
                        ins = INS_sve_orr;
                    }
                }
                else if (isPredicateRegister(reg3))
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                    assert(isPredicateRegister(reg1));
                    assert(isPredicateRegister(reg2));
                    fmt = sopt == INS_SCALABLE_OPTS_PREDICATE_MERGE ? IF_SVE_CZ_4A_K : IF_SVE_CZ_4A;
                    // MOV is an alias for CPY, and is always the preferred disassembly.
                    ins = INS_sve_mov;
                }
                else
                {
                    unreached();
                }
                break;
            }

            case INS_sve_lasta:
            case INS_sve_lastb:
            {
                assert(insOptsScalableStandard(opt));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                if (isGeneralRegister(reg1))
                {
                    assert(insScalableOptsNone(sopt));
                    assert(isGeneralRegister(reg1));
                    fmt = IF_SVE_CS_3A;
                }
                else if (sopt == INS_SCALABLE_OPTS_WITH_SIMD_SCALAR)
                {
                    assert(isVectorRegister(reg1));
                    fmt = IF_SVE_CR_3A;
                }
                break;
            }

            case INS_sve_revd:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_CT_3A;
                break;
            }

            case INS_sve_rbit:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_CU_3A;
                break;
            }

            case INS_sve_revb:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_CU_3A;
                break;
            }

            case INS_sve_revh:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableWords(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_CU_3A;
                break;
            }

            case INS_sve_revw:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_CU_3A;
                break;
            }

            case INS_sve_splice:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                // Only the destructive version is supported; remove when the constructive version is added.
                // https://github.com/dotnet/runtime/issues/103850
                assert(sopt != INS_SCALABLE_OPTS_WITH_VECTOR_PAIR);
                fmt = sopt == INS_SCALABLE_OPTS_WITH_VECTOR_PAIR ? IF_SVE_CV_3A : IF_SVE_CV_3B;
                break;
            }

            case INS_sve_brka:
            case INS_sve_brkb:
            {
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                assert(insOptsScalableStandard(opt));
                if (sopt == INS_SCALABLE_OPTS_PREDICATE_MERGE)
                {
                    pmerge = true;
                }
                fmt = IF_SVE_DB_3A;
                break;
            }

            case INS_sve_brkas:
            case INS_sve_brkbs:
            {
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                fmt = IF_SVE_DB_3B;
                break;
            }

            case INS_sve_brkn:
            case INS_sve_brkns:
            {
                assert(insOptsScalable(opt));
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                opt = INS_OPTS_SCALABLE_B;
                fmt = IF_SVE_DC_3A;
                break;
            }

            case INS_sve_cntp:
            {
                assert(isScalableVectorSize(size));
                assert(isGeneralRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_DK_3A;
                break;
            }

            case INS_sve_shadd:
            case INS_sve_shsub:
            case INS_sve_shsubr:
            case INS_sve_srhadd:
            case INS_sve_uhadd:
            case INS_sve_uhsub:
            case INS_sve_uhsubr:
            case INS_sve_urhadd:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AA_3A;
                break;
            }

            case INS_sve_sadalp:
            case INS_sve_uadalp:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_EQ_3A;
                break;
            }

            case INS_sve_addp:
            case INS_sve_smaxp:
            case INS_sve_sminp:
            case INS_sve_umaxp:
            case INS_sve_uminp:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AA_3A;
                break;
            }

            case INS_sve_sqabs:
            case INS_sve_sqneg:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_ES_3A;
                break;
            }

            case INS_sve_urecpe:
            case INS_sve_ursqrte:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(opt == INS_OPTS_SCALABLE_S);
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_ES_3A;
                break;
            }

            case INS_sve_sqadd:
            case INS_sve_sqsub:
            case INS_sve_uqadd:
            case INS_sve_uqsub:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(isScalableVectorSize(size));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    fmt = IF_SVE_AT_3A;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_AA_3A;
                }
                break;
            }

            case INS_sve_sqsubr:
            case INS_sve_suqadd:
            case INS_sve_uqsubr:
            case INS_sve_usqadd:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_AA_3A;
                break;
            }

            case INS_sve_sqrshl:
            case INS_sve_sqrshlr:
            case INS_sve_sqshl:
            case INS_sve_sqshlr:
            case INS_sve_srshl:
            case INS_sve_srshlr:
            case INS_sve_uqrshl:
            case INS_sve_uqrshlr:
            case INS_sve_uqshl:
            case INS_sve_uqshlr:
            case INS_sve_urshl:
            case INS_sve_urshlr:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableStandard(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_AA_3A;
                break;
            }

            case INS_sve_fcvtnt:
            case INS_sve_fcvtlt:
            {
                assert(insOptsConvertFloatStepwise(opt));
                goto case INS_sve_fcvtxnt;
            }

            case INS_sve_fcvtxnt:
            case INS_sve_bfcvtnt:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_GQ_3A;
                break;
            }

            case INS_sve_faddp:
            case INS_sve_fmaxnmp:
            case INS_sve_fmaxp:
            case INS_sve_fminnmp:
            case INS_sve_fminp:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_GR_3A;
                break;
            }

            case INS_sve_faddqv:
            case INS_sve_fmaxnmqv:
            case INS_sve_fminnmqv:
            case INS_sve_fmaxqv:
            case INS_sve_fminqv:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_GS_3A;
                break;
            }

            case INS_sve_fmaxnmv:
            case INS_sve_fmaxv:
            case INS_sve_fminnmv:
            case INS_sve_fminv:
            case INS_sve_faddv:
            {
                assert(isFloatReg(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(isScalableVectorSize(size));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_HE_3A;
                break;
            }

            case INS_sve_fadda:
            {
                assert(isFloatReg(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(isScalableVectorSize(size));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_HJ_3A;
                break;
            }

            case INS_sve_frecps:
            case INS_sve_frsqrts:
            case INS_sve_ftsmul:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_AT_3A;
                break;
            }

            case INS_sve_fadd:
            case INS_sve_fsub:
            case INS_sve_fmul:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    fmt = IF_SVE_AT_3A;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_HL_3A;
                }
                break;
            }

            case INS_sve_fabd:
            case INS_sve_fdiv:
            case INS_sve_fdivr:
            case INS_sve_fmax:
            case INS_sve_fmaxnm:
            case INS_sve_fmin:
            case INS_sve_fminnm:
            case INS_sve_fmulx:
            case INS_sve_fscale:
            case INS_sve_fsubr:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_HL_3A;
                break;
            }

            case INS_sve_famax:
            case INS_sve_famin:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_HL_3A;
                break;
            }

            case INS_sve_bfmul:
            case INS_sve_bfadd:
            case INS_sve_bfsub:
            case INS_sve_bfmaxnm:
            case INS_sve_bfminnm:
            case INS_sve_bfmax:
            case INS_sve_bfmin:
            {
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg3));
                assert(insScalableOptsNone(sopt));
                if (isVectorRegister(reg2))
                {
                    fmt = IF_SVE_HK_3B;
                }
                else
                {
                    assert(isLowPredicateRegister(reg2));
                    fmt = IF_SVE_HL_3B;
                }
                break;
            }

            case INS_sve_bsl:
            case INS_sve_eor3:
            case INS_sve_bcax:
            case INS_sve_bsl1n:
            case INS_sve_bsl2n:
            case INS_sve_nbsl:
            {
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_AV_3A;
                break;
            }

            case INS_sve_frintn:
            case INS_sve_frintm:
            case INS_sve_frintp:
            case INS_sve_frintz:
            case INS_sve_frinta:
            case INS_sve_frintx:
            case INS_sve_frinti:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_HQ_3A;
                break;
            }

            case INS_sve_bfcvt:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HO_3A;
                break;
            }

            case INS_sve_fcvt:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HO_3B;
                break;
            }

            case INS_sve_fcvtx:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HO_3C;
                break;
            }

            case INS_sve_fcvtzs:
            case INS_sve_fcvtzu:
            {
                assert(insOptsScalableFloat(opt) || (opt == INS_OPTS_H_TO_S) || (opt == INS_OPTS_H_TO_D)
                    || (opt == INS_OPTS_S_TO_D) || (opt == INS_OPTS_D_TO_S));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HP_3B;
                break;
            }

            case INS_sve_scvtf:
            case INS_sve_ucvtf:
            {
                assert(insOptsScalableAtLeastHalf(opt) || (opt == INS_OPTS_S_TO_H) || (opt == INS_OPTS_S_TO_D)
                    || (opt == INS_OPTS_D_TO_H) || (opt == INS_OPTS_D_TO_S));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_HS_3A;
                break;
            }

            case INS_sve_frecpx:
            case INS_sve_fsqrt:
            {
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsScalableFloat(opt));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_HR_3A;
                break;
            }

            case INS_sve_whilege:
            case INS_sve_whilegt:
            case INS_sve_whilelt:
            case INS_sve_whilele:
            case INS_sve_whilehs:
            case INS_sve_whilehi:
            case INS_sve_whilelo:
            case INS_sve_whilels:
            {
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                assert(insOptsScalableStandard(opt));
                if (insScalableOptsNone(sopt))
                {
                    assert(isPredicateRegister(reg1));
                    assert(isValidGeneralDatasize(size));
                    fmt = IF_SVE_DT_3A;
                }
                else if (insScalableOptsWithPredicatePair(sopt))
                {
                    assert(isLowPredicateRegister(reg1));
                    assert(size == EA_8BYTE);
                    fmt = IF_SVE_DX_3A;
                }
                else
                {
                    assert(insScalableOptsWithVectorLength(sopt));
                    assert(isHighPredicateRegister(reg1));
                    assert(size == EA_8BYTE);
                    vectorLength4x = sopt == INS_SCALABLE_OPTS_VL_4X;
                    fmt = IF_SVE_DY_3A;
                }
                break;
            }

            case INS_sve_whilewr:
            case INS_sve_whilerw:
            {
                assert(insOptsScalableStandard(opt));
                assert(isPredicateRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(size == EA_8BYTE);
                assert(isGeneralRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                assert(insScalableOptsNone(sopt));
                fmt = IF_SVE_DU_3A;
                break;
            }

            case INS_sve_movs:
            {
                assert(insOptsScalable(opt));
                assert(isPredicateRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isPredicateRegister(reg3));
                fmt = IF_SVE_CZ_4A;
                break;
            }

            case INS_sve_adclb:
            case INS_sve_adclt:
            case INS_sve_sbclb:
            case INS_sve_sbclt:
            {
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_FY_3A;
                break;
            }

            case INS_sve_mlapt:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(insOptsNone(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_EW_3A;
                break;
            }

            case INS_sve_madpt:
            {
                unreached(); // TODO-SVE: Not yet supported.
                assert(insOptsNone(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_SVE_EW_3B;
                break;
            }

            case INS_sve_fcmeq:
            case INS_sve_fcmge:
            case INS_sve_fcmgt:
            case INS_sve_fcmlt:
            case INS_sve_fcmle:
            case INS_sve_fcmne:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                assert(isPredicateRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_HI_3A;
                break;
            }

            case INS_sve_flogb:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isScalableVectorSize(size));
                fmt = IF_SVE_HP_3A;
                break;
            }

            case INS_sve_ld1b:
            case INS_sve_ld1sb:
            case INS_sve_ld1h:
            case INS_sve_ld1sh:
            case INS_sve_ld1w:
            case INS_sve_ld1sw:
            case INS_sve_ld1d:
            case INS_sve_ldnf1b:
            case INS_sve_ldnf1sb:
            case INS_sve_ldnf1h:
            case INS_sve_ldnf1sh:
            case INS_sve_ldnf1w:
            case INS_sve_ldnf1sw:
            case INS_sve_ldnf1d:
            case INS_sve_ldnt1b:
            case INS_sve_ldnt1h:
            case INS_sve_ldnt1w:
            case INS_sve_ldnt1d:
            case INS_sve_ld1rqb:
            case INS_sve_ld1rqh:
            case INS_sve_ld1rqw:
            case INS_sve_ld1rqd:
            {
                emitIns_R_R_R_I(ins, size, reg1, reg2, reg3, 0, opt);
                return;
            }

            default:
            {
                unreached();
                break;
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        if (pmerge)
        {
            id.idPredicateReg2Merge(pmerge);
        }
        else if (vectorLength4x)
        {
            id.idVectorLength4x(vectorLength4x);
        }

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitInsSve_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, nint imm, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;

        switch (ins)
        {
            case INS_sve_asr:
            case INS_sve_asrd:
            case INS_sve_lsl:
            case INS_sve_lsr:
            case INS_sve_sqshl:
            case INS_sve_sqshlu:
            case INS_sve_srshr:
            case INS_sve_uqshl:
            case INS_sve_urshr:
            {
                // Embedded masked RMW instructions.
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, opt, mopt, reg2);
                emitInsSve_R_R_I(ins, attr, reg1, reg2, imm, opt, sopt);
                return;
            }

            case INS_sve_ext:
            case INS_sve_rshrnt:
            case INS_sve_shrnt:
            case INS_sve_sli:
            case INS_sve_sqrshrnt:
            case INS_sve_sqrshrunt:
            case INS_sve_sqshrnt:
            case INS_sve_sqshrunt:
            case INS_sve_sri:
            case INS_sve_uqshrnt:
            case INS_sve_uqrshrnt:
            {
                // These RMW instructions do not support movprfx.
                assert(insSveMovOptsUnpredicated(mopt));
                emitInsSve_Mov(INS_sve_mov, attr, reg1, reg2, true, opt, INS_SVE_MOV_OPTS_UNPRED);
                emitInsSve_R_R_I(ins, attr, reg1, reg3, imm, opt, sopt);
                return;
            }

            case INS_sve_cadd:
            case INS_sve_ftmad:
            case INS_sve_sqcadd:
            case INS_sve_srsra:
            case INS_sve_ssra:
            case INS_sve_ursra:
            case INS_sve_usra:
            case INS_sve_xar:
            {
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg2, reg3));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg2, true, INS_OPTS_NONE, mopt);
                emitInsSve_R_R_I(ins, attr, reg1, reg3, imm, opt, sopt);
                return;
            }

            case INS_sve_adr:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm(imm, 2));
                switch (opt)
                {
                    case INS_OPTS_SCALABLE_S:
                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                        fmt = IF_SVE_BH_3A;
                        break;
                    }

                    case INS_OPTS_SCALABLE_D_SXTW:
                    {
                        fmt = IF_SVE_BH_3B;
                        break;
                    }

                    case INS_OPTS_SCALABLE_D_UXTW:
                    {
                        fmt = IF_SVE_BH_3B_A;
                        break;
                    }

                    default:
                    {
                        assert(false, "invalid instruction");
                        break;
                    }
                }
                break;
            }

            case INS_sve_cmpeq:
            case INS_sve_cmpgt:
            case INS_sve_cmpge:
            case INS_sve_cmpne:
            case INS_sve_cmple:
            case INS_sve_cmplt:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isPredicateRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidSimm(imm, 5));
                fmt = IF_SVE_CY_3A;
                break;
            }

            case INS_sve_cmphi:
            case INS_sve_cmphs:
            case INS_sve_cmplo:
            case INS_sve_cmpls:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isPredicateRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm(imm, 7));
                fmt = IF_SVE_CY_3B;
                break;
            }

            case INS_sve_sdot:
            case INS_sve_udot:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_EG_3A;
                }
                else if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_EY_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 1));
                    opt = INS_OPTS_SCALABLE_H;
                    fmt = IF_SVE_EY_3B;
                }
                break;
            }

            case INS_sve_usdot:
            case INS_sve_sudot:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                assert(isValidUimm(imm, 2));
                fmt = IF_SVE_EZ_3A;
                break;
            }

            case INS_sve_mul:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                switch (opt)
                {
                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimm(imm, 3));
                        assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                        fmt = IF_SVE_FD_3A;
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimm(imm, 2));
                        assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                        fmt = IF_SVE_FD_3B;
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimm(imm, 1));
                        fmt = IF_SVE_FD_3C;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case INS_sve_cdot:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidRot(emitDecodeRotationImm0_to_270(imm)));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EJ_3A;
                break;
            }

            case INS_sve_cmla:
            case INS_sve_sqrdcmlah:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidRot(emitDecodeRotationImm0_to_270(imm)));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EK_3A;
                break;
            }

            case INS_sve_ld1d:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalable(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    if (opt == INS_OPTS_SCALABLE_Q)
                    {
                        fmt = IF_SVE_IH_3A_A;
                    }
                    else
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        fmt = IF_SVE_IH_3A;
                    }
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm_MultipleOf(imm, 5, 8));
                    fmt = IF_SVE_IV_3A;
                }
                break;
            }

            case INS_sve_ldff1d:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 5, 8));
                fmt = IF_SVE_IV_3A;
                break;
            }

            case INS_sve_ld1w:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWordsOrQuadwords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    fmt = IF_SVE_IH_3A_F;
                }
                else
                {
                    assert(insOptsScalableWords(opt));
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm_MultipleOf(imm, 5, 4));
                    fmt = IF_SVE_HX_3A_E;
                }
                break;
            }

            case INS_sve_ld1sw:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    fmt = IF_SVE_IJ_3A;
                }
                else
                {
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm_MultipleOf(imm, 5, 4));
                    fmt = IF_SVE_IV_3A;
                }
                break;
            }

            case INS_sve_ldff1sw:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 5, 4));
                fmt = IF_SVE_IV_3A;
                break;
            }

            case INS_sve_ld1sb:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isGeneralRegister(reg3));
                    assert(isValidSimm(imm, 4));
                    fmt = IF_SVE_IJ_3A_D;
                }
                else
                {
                    assert(insOptsScalableWords(opt));
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm(imm, 5));
                    fmt = IF_SVE_HX_3A_B;
                }
                break;
            }

            case INS_sve_ld1b:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    fmt = IF_SVE_IJ_3A_E;
                }
                else
                {
                    assert(insOptsScalableWords(opt));
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm(imm, 5));
                    fmt = IF_SVE_HX_3A_B;
                }
                break;
            }

            case INS_sve_ldff1b:
            case INS_sve_ldff1sb:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm(imm, 5));
                fmt = IF_SVE_HX_3A_B;
                break;
            }

            case INS_sve_ld1sh:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    fmt = IF_SVE_IJ_3A_F;
                }
                else
                {
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm_MultipleOf(imm, 5, 2));
                    fmt = IF_SVE_HX_3A_E;
                }
                break;
            }

            case INS_sve_ld1h:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    fmt = IF_SVE_IJ_3A_G;
                }
                else
                {
                    assert(isVectorRegister(reg3));
                    assert(isValidUimm_MultipleOf(imm, 5, 2));
                    fmt = IF_SVE_HX_3A_E;
                }
                break;
            }

            case INS_sve_ldff1h:
            case INS_sve_ldff1sh:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 5, 2));
                fmt = IF_SVE_HX_3A_E;
                break;
            }

            case INS_sve_ldff1w:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 5, 4));
                fmt = IF_SVE_HX_3A_E;
                break;
            }

            case INS_sve_ldnf1sw:
            case INS_sve_ldnf1d:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidSimm(imm, 4));
                fmt = IF_SVE_IL_3A;
                break;
            }

            case INS_sve_ldnf1sh:
            case INS_sve_ldnf1w:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidSimm(imm, 4));
                fmt = IF_SVE_IL_3A_A;
                break;
            }

            case INS_sve_ldnf1h:
            case INS_sve_ldnf1sb:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidSimm(imm, 4));
                fmt = IF_SVE_IL_3A_B;
                break;
            }

            case INS_sve_ldnf1b:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidSimm(imm, 4));
                fmt = IF_SVE_IL_3A_C;
                break;
            }

            case INS_sve_ldnt1b:
            case INS_sve_ldnt1h:
            case INS_sve_ldnt1w:
            case INS_sve_ldnt1d:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidSimm(imm, 4));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_ldnt1b:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        break;
                    }

                    case INS_sve_ldnt1h:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        break;
                    }

                    case INS_sve_ldnt1w:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        break;
                    }

                    case INS_sve_ldnt1d:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_IM_3A;
                break;
            }

            case INS_sve_ld1rqb:
            case INS_sve_ld1rob:
            case INS_sve_ld1rqh:
            case INS_sve_ld1roh:
            case INS_sve_ld1rqw:
            case INS_sve_ld1row:
            case INS_sve_ld1rqd:
            case INS_sve_ld1rod:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_ld1rqb:
                    case INS_sve_ld1rqd:
                    case INS_sve_ld1rqh:
                    case INS_sve_ld1rqw:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 16));
                        break;
                    }

                    case INS_sve_ld1rob:
                    case INS_sve_ld1rod:
                    case INS_sve_ld1roh:
                    case INS_sve_ld1row:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 32));
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
                switch (ins)
                {
                    case INS_sve_ld1rqb:
                    case INS_sve_ld1rob:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        break;
                    }

                    case INS_sve_ld1rqh:
                    case INS_sve_ld1roh:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        break;
                    }

                    case INS_sve_ld1rqw:
                    case INS_sve_ld1row:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        break;
                    }

                    case INS_sve_ld1rqd:
                    case INS_sve_ld1rod:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_IO_3A;
                break;
            }

            case INS_sve_ld2q:
            case INS_sve_ld3q:
            case INS_sve_ld4q:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_Q);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_ld2q:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 2));
                        break;
                    }

                    case INS_sve_ld3q:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 3));
                        break;
                    }

                    case INS_sve_ld4q:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 4));
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_IQ_3A;
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
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_ld2b:
                    case INS_sve_ld2h:
                    case INS_sve_ld2w:
                    case INS_sve_ld2d:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 2));
                        break;
                    }

                    case INS_sve_ld3b:
                    case INS_sve_ld3h:
                    case INS_sve_ld3w:
                    case INS_sve_ld3d:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 3));
                        break;
                    }

                    case INS_sve_ld4b:
                    case INS_sve_ld4h:
                    case INS_sve_ld4w:
                    case INS_sve_ld4d:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 4));
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
                switch (ins)
                {
                    case INS_sve_ld2b:
                    case INS_sve_ld3b:
                    case INS_sve_ld4b:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        break;
                    }

                    case INS_sve_ld2h:
                    case INS_sve_ld3h:
                    case INS_sve_ld4h:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        break;
                    }

                    case INS_sve_ld2w:
                    case INS_sve_ld3w:
                    case INS_sve_ld4w:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        break;
                    }

                    case INS_sve_ld2d:
                    case INS_sve_ld3d:
                    case INS_sve_ld4d:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_IS_3A;
                break;
            }

            case INS_sve_st2q:
            case INS_sve_st3q:
            case INS_sve_st4q:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_Q);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_st2q:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 2));
                        break;
                    }

                    case INS_sve_st3q:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 3));
                        break;
                    }

                    case INS_sve_st4q:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 4));
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_JE_3A;
                break;
            }

            case INS_sve_stnt1b:
            case INS_sve_stnt1h:
            case INS_sve_stnt1w:
            case INS_sve_stnt1d:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidSimm(imm, 4));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_stnt1b:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        break;
                    }

                    case INS_sve_stnt1h:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        break;
                    }

                    case INS_sve_stnt1w:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        break;
                    }

                    case INS_sve_stnt1d:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_JM_3A;
                break;
            }

            case INS_sve_st1w:
            case INS_sve_st1d:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    if ((opt == INS_OPTS_SCALABLE_Q) && (ins == INS_sve_st1d))
                    {
                        fmt = IF_SVE_JN_3C_D;
                    }
                    else
                    {
                        if ((ins == INS_sve_st1w) && insOptsScalableWords(opt))
                        {
                            fmt = IF_SVE_JN_3B;
                        }
                        else
                        {
#if DEBUG
                            if (ins == INS_sve_st1w)
                            {
                                assert(opt == INS_OPTS_SCALABLE_Q);
                            }
                            else
                            {
                                assert(opt == INS_OPTS_SCALABLE_D);
                            }
#endif
                            fmt = IF_SVE_JN_3C;
                        }
                    }
                }
                else
                {
                    assert(isVectorRegister(reg3));
                    if ((ins == INS_sve_st1w) && insOptsScalableWords(opt))
                    {
                        assert(isValidUimm_MultipleOf(imm, 5, 4));
                        fmt = IF_SVE_JI_3A_A;
                    }
                    else
                    {
                        assert(ins == INS_sve_st1d);
                        assert(isValidUimm_MultipleOf(imm, 5, 8));
                        fmt = IF_SVE_JL_3A;
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
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
#if DEBUG
                switch (ins)
                {
                    case INS_sve_st2b:
                    case INS_sve_st2h:
                    case INS_sve_st2w:
                    case INS_sve_st2d:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 2));
                        break;
                    }

                    case INS_sve_st3b:
                    case INS_sve_st3h:
                    case INS_sve_st3w:
                    case INS_sve_st3d:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 3));
                        break;
                    }

                    case INS_sve_st4b:
                    case INS_sve_st4h:
                    case INS_sve_st4w:
                    case INS_sve_st4d:
                    {
                        assert(isValidSimm_MultipleOf(imm, 4, 4));
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
                switch (ins)
                {
                    case INS_sve_st2b:
                    case INS_sve_st3b:
                    case INS_sve_st4b:
                    {
                        assert(opt == INS_OPTS_SCALABLE_B);
                        break;
                    }

                    case INS_sve_st2h:
                    case INS_sve_st3h:
                    case INS_sve_st4h:
                    {
                        assert(opt == INS_OPTS_SCALABLE_H);
                        break;
                    }

                    case INS_sve_st2w:
                    case INS_sve_st3w:
                    case INS_sve_st4w:
                    {
                        assert(opt == INS_OPTS_SCALABLE_S);
                        break;
                    }

                    case INS_sve_st2d:
                    case INS_sve_st3d:
                    case INS_sve_st4d:
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        break;
                    }

                    default:
                    {
                        assert(false, "Invalid instruction");
                        break;
                    }
                }
#endif
                fmt = IF_SVE_JO_3A;
                break;
            }

            case INS_sve_st1b:
            case INS_sve_st1h:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                if (isGeneralRegister(reg3))
                {
                    assert(isValidSimm(imm, 4));
                    // st1h is reserved for scalable B.
                    assert(ins == INS_sve_st1h ? insOptsScalableAtLeastHalf(opt) : insOptsScalableStandard(opt));
                    fmt = IF_SVE_JN_3A;
                }
                else
                {
                    assert(insOptsScalableWords(opt));
                    assert(isVectorRegister(reg3));
#if DEBUG
                    switch (ins)
                    {
                        case INS_sve_st1b:
                        {
                            assert(isValidUimm(imm, 5));
                            break;
                        }

                        case INS_sve_st1h:
                        {
                            assert(isValidUimm_MultipleOf(imm, 5, 2));
                            break;
                        }

                        default:
                        {
                            assert(false, "Invalid instruction");
                            break;
                        }
                    }
#endif
                    fmt = IF_SVE_JI_3A_A;
                }
                break;
            }

            case INS_sve_fmla:
            case INS_sve_fmls:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_GU_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_GU_3B;
                }
                break;
            }

            case INS_sve_bfmla:
            case INS_sve_bfmls:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                assert(isValidUimm(imm, 3));
                fmt = IF_SVE_GU_3C;
                break;
            }

            case INS_sve_fmul:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_GX_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_GX_3B;
                }
                break;
            }

            case INS_sve_bfmul:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                assert(isValidUimm(imm, 3));
                fmt = IF_SVE_GX_3C;
                break;
            }

            case INS_sve_fdot:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                assert(isValidUimm(imm, 2));
                if (opt == INS_OPTS_SCALABLE_B)
                {
                    unreached(); // TODO-SVE: Not yet supported.
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_GY_3B_D;
                }
                else if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_GY_3B;
                }
                else
                {
                    unreached(); // TODO-SVE: Not yet supported.
                    assert(insOptsNone(opt));
                    assert(isValidUimm(imm, 3));
                    // Simplify emitDispInsHelp logic by setting insOpt.
                    opt = INS_OPTS_SCALABLE_B;
                    fmt = IF_SVE_GY_3A;
                }
                break;
            }

            case INS_sve_bfdot:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                assert(isValidUimm(imm, 2));
                fmt = IF_SVE_GY_3B;
                break;
            }

            case INS_sve_mla:
            case INS_sve_mls:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FF_3A;
                }
                else if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FF_3B;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_FF_3C;
                }
                break;
            }

            case INS_sve_smullb:
            case INS_sve_smullt:
            case INS_sve_umullb:
            case INS_sve_umullt:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FE_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FE_3B;
                }
                break;
            }

            case INS_sve_smlalb:
            case INS_sve_smlalt:
            case INS_sve_umlalb:
            case INS_sve_umlalt:
            case INS_sve_smlslb:
            case INS_sve_smlslt:
            case INS_sve_umlslb:
            case INS_sve_umlslt:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FG_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FG_3B;
                }
                break;
            }

            case INS_sve_sqdmullb:
            case INS_sve_sqdmullt:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FH_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FH_3B;
                }
                break;
            }

            case INS_sve_sqdmulh:
            case INS_sve_sqrdmulh:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FI_3A;
                }
                else if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FI_3B;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_FI_3C;
                }
                break;
            }

            case INS_sve_sqdmlalb:
            case INS_sve_sqdmlalt:
            case INS_sve_sqdmlslb:
            case INS_sve_sqdmlslt:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FJ_3A;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FJ_3B;
                }
                break;
            }

            case INS_sve_sqrdmlah:
            case INS_sve_sqrdmlsh:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isLowVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_FK_3A;
                }
                else if (opt == INS_OPTS_SCALABLE_S)
                {
                    assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_FK_3B;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_FK_3C;
                }
                break;
            }

            case INS_sve_fcadd:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isScalableVectorSize(size));
                assert(emitIsValidEncodedRotationImm90_or_270(imm));
                fmt = IF_SVE_GP_3A;
                break;
            }

            case INS_sve_ld1rd:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 6, 8));
                fmt = IF_SVE_IC_3A;
                break;
            }

            case INS_sve_ld1rsw:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 6, 4));
                fmt = IF_SVE_IC_3A;
                break;
            }

            case INS_sve_ld1rsh:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 6, 2));
                fmt = IF_SVE_IC_3A_A;
                break;
            }

            case INS_sve_ld1rw:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableWords(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 6, 4));
                fmt = IF_SVE_IC_3A_A;
                break;
            }

            case INS_sve_ld1rh:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm_MultipleOf(imm, 6, 2));
                fmt = IF_SVE_IC_3A_B;
                break;
            }

            case INS_sve_ld1rsb:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm(imm, 6));
                fmt = IF_SVE_IC_3A_B;
                break;
            }

            case INS_sve_ld1rb:
            {
                assert(insScalableOptsNone(sopt));
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidUimm(imm, 6));
                fmt = IF_SVE_IC_3A_C;
                break;
            }

            case INS_sve_fmlalb:
            case INS_sve_fmlalt:
            case INS_sve_fmlslb:
            case INS_sve_fmlslt:
            case INS_sve_bfmlalb:
            case INS_sve_bfmlalt:
            case INS_sve_bfmlslb:
            case INS_sve_bfmlslt:
            {
                assert(insScalableOptsNone(sopt));
                assert(opt == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert((REG_V0 <= reg3) && (reg3 <= REG_V7));
                assert(isValidUimm(imm, 3));
                fmt = IF_SVE_GZ_3A;
                break;
            }

            case INS_sve_luti2:
            {
                assert(insScalableOptsNone(sopt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert(isValidUimm(imm, 3));
                    fmt = IF_SVE_GG_3B;
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                    assert(isValidUimm(imm, 2));
                    fmt = IF_SVE_GG_3A;
                }
                unreached();
                break;
            }

            case INS_sve_luti4:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_SCALABLE_H)
                {
                    assert(isValidUimm(imm, 2));
                    if (sopt == INS_SCALABLE_OPTS_WITH_VECTOR_PAIR)
                    {
                        fmt = IF_SVE_GH_3B;
                    }
                    else
                    {
                        assert(insScalableOptsNone(sopt));
                        fmt = IF_SVE_GH_3B_B;
                    }
                }
                else
                {
                    assert(opt == INS_OPTS_SCALABLE_B);
                    assert(insScalableOptsNone(sopt));
                    assert(isValidUimm(imm, 1));
                    fmt = IF_SVE_GH_3A;
                }
                unreached();
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

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
