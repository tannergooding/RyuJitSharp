// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitInsPairSanityCheck(instrDesc? previousId, instrDesc id)
    {
        if (previousId is null || JitConfig.JitEmitUnitTestsSections is not null)
        {
            return;
        }
        if (previousId.idIns() != INS_sve_movprfx)
        {
            return;
        }

        var movprfxIsPredicated = previousId.idInsFmt() == IF_SVE_AH_3A;
        if (!movprfxIsPredicated)
        {
            assert(previousId.idInsFmt() == IF_SVE_BI_2A);
        }

        switch (id.idInsFmt())
        {
            case IF_SVE_BN_1A:
            case IF_SVE_BP_1A:
            case IF_SVE_CC_2A:
            case IF_SVE_CD_2A:
            case IF_SVE_DN_2A:
            case IF_SVE_DP_2A:
            case IF_SVE_BS_1A:
            case IF_SVE_EC_1A:
            case IF_SVE_ED_1A:
            case IF_SVE_EE_1A:
            {
                assert(!movprfxIsPredicated);
                break;
            }

            case IF_SVE_BU_2A:
            case IF_SVE_BV_2A_A:
            case IF_SVE_BV_2A_J:
            case IF_SVE_BV_2B:
            case IF_SVE_CQ_3A:
            case IF_SVE_AM_2A:
            case IF_SVE_HM_2A:
            {
                break;
            }

            case IF_SVE_FU_2A:
            case IF_SVE_AW_2A:
            case IF_SVE_BY_2A:
            case IF_SVE_FV_2A:
            case IF_SVE_HN_2A:
            {
                assert(!movprfxIsPredicated);
                assert(id.idReg1() != id.idReg2());
                break;
            }

            case IF_SVE_AP_3A:
            case IF_SVE_AQ_3A:
            case IF_SVE_CP_3A:
            case IF_SVE_CT_3A:
            case IF_SVE_CU_3A:
            case IF_SVE_ES_3A:
            case IF_SVE_EQ_3A:
            case IF_SVE_HO_3A:
            case IF_SVE_HO_3B:
            case IF_SVE_HO_3C:
            case IF_SVE_HP_3A:
            case IF_SVE_HQ_3A:
            case IF_SVE_HR_3A:
            case IF_SVE_HS_3A:
            case IF_SVE_HP_3B:
            case IF_SVE_AA_3A:
            case IF_SVE_AB_3B:
            case IF_SVE_AC_3A:
            case IF_SVE_AO_3A:
            case IF_SVE_CM_3A:
            case IF_SVE_CV_3B:
            case IF_SVE_GP_3A:
            case IF_SVE_GR_3A:
            case IF_SVE_HL_3A:
            case IF_SVE_HL_3B:
            {
                assert(id.idReg1() != id.idReg3());
                break;
            }

            case IF_SVE_EF_3A:
            case IF_SVE_EG_3A:
            case IF_SVE_EH_3A:
            case IF_SVE_EI_3A:
            case IF_SVE_EJ_3A:
            case IF_SVE_EK_3A:
            case IF_SVE_EL_3A:
            case IF_SVE_EM_3A:
            case IF_SVE_EW_3A:
            case IF_SVE_EW_3B:
            case IF_SVE_EY_3A:
            case IF_SVE_EY_3B:
            case IF_SVE_EZ_3A:
            case IF_SVE_FA_3A:
            case IF_SVE_FA_3B:
            case IF_SVE_FB_3A:
            case IF_SVE_FB_3B:
            case IF_SVE_FC_3A:
            case IF_SVE_FC_3B:
            case IF_SVE_FF_3A:
            case IF_SVE_FF_3B:
            case IF_SVE_FF_3C:
            case IF_SVE_FG_3A:
            case IF_SVE_FG_3B:
            case IF_SVE_FJ_3A:
            case IF_SVE_FJ_3B:
            case IF_SVE_FK_3A:
            case IF_SVE_FK_3B:
            case IF_SVE_FK_3C:
            case IF_SVE_FO_3A:
            case IF_SVE_FW_3A:
            case IF_SVE_FY_3A:
            case IF_SVE_GM_3A:
            case IF_SVE_GN_3A:
            case IF_SVE_GO_3A:
            case IF_SVE_GU_3A:
            case IF_SVE_GU_3B:
            case IF_SVE_GU_3C:
            case IF_SVE_GV_3A:
            case IF_SVE_GW_3B:
            case IF_SVE_GY_3A:
            case IF_SVE_GY_3B:
            case IF_SVE_GY_3B_D:
            case IF_SVE_GZ_3A:
            case IF_SVE_HA_3A:
            case IF_SVE_HA_3A_E:
            case IF_SVE_HA_3A_F:
            case IF_SVE_HB_3A:
            case IF_SVE_HC_3A:
            case IF_SVE_HD_3A:
            case IF_SVE_HD_3A_A:
            case IF_SVE_AV_3A:
            {
                assert(!movprfxIsPredicated);
                assert(id.idReg1() != id.idReg2());
                assert(id.idReg1() != id.idReg3());
                break;
            }

            case IF_SVE_AR_4A:
            case IF_SVE_AS_4A:
            case IF_SVE_GT_4A:
            case IF_SVE_HU_4A:
            case IF_SVE_HU_4B:
            case IF_SVE_HV_4A:
            {
                assert(id.idReg1() != id.idReg3());
                assert(id.idReg1() != id.idReg4());
                break;
            }

            case IF_SVE_AT_3A:
            {
                switch (id.idIns())
                {
                    case INS_sve_sclamp:
                    case INS_sve_uclamp:
                    case INS_sve_eorbt:
                    case INS_sve_eortb:
                    case INS_sve_fclamp:
                    {
                        break;
                    }
                    default:
                    {
                        assert(false, "!\"Got unexpected instruction format within group after MOVPRFX\"");
                        break;
                    }
                }
                assert(!movprfxIsPredicated);
                assert(id.idReg1() != id.idReg2());
                assert(id.idReg1() != id.idReg3());
                break;
            }

            default:
            {
                assert(false, "!\"Got unexpected instruction format after MOVPRFX\"");
                break;
            }
        }

        assert(previousId.idReg1() == id.idReg1());
        if (movprfxIsPredicated)
        {
            assert(isPredicateRegister(previousId.idReg2()));
            assert(isPredicateRegister(id.idReg2()));
            assert(previousId.idReg2() == id.idReg2());

            var movprfxElementSize = optGetSveElemsize(previousId.idInsOpt());
            var instructionElementSize = insOptsScalableStandard(id.idInsOpt())
                ? optGetSveElemsize(id.idInsOpt())
                : optGetDstsize(id.idInsOpt());
            assert(movprfxElementSize == instructionElementSize);
        }

        if (isPredicatedMovprfxForbidden(id.idIns(), id.idInsFmt()))
        {
            assert(!movprfxIsPredicated);
        }
    }

    private static bool isPredicatedMovprfxForbidden(instruction ins, insFormat fmt)
    {
        return (ins, fmt) switch
        {
            (INS_sve_sqdecd or INS_sve_sqdech or INS_sve_sqdecw or INS_sve_sqincd or INS_sve_sqinch or
                INS_sve_sqincw or INS_sve_uqdecd or INS_sve_uqdech or INS_sve_uqdecw or INS_sve_uqincd or
                INS_sve_uqinch or INS_sve_uqincw, IF_SVE_BP_1A) => true,
            (INS_sve_smlalb or INS_sve_smlalt or INS_sve_smlslb or INS_sve_smlslt or INS_sve_umlalb or
                INS_sve_umlalt or INS_sve_umlslb or INS_sve_umlslt, IF_SVE_EL_3A or IF_SVE_FG_3A or IF_SVE_FG_3B) => true,
            (INS_sve_add or INS_sve_sqadd or INS_sve_sqsub or INS_sve_sub or INS_sve_subr or INS_sve_uqadd or
                INS_sve_uqsub, IF_SVE_EC_1A) => true,
            (INS_sve_and or INS_sve_bic or INS_sve_eon or INS_sve_eor or INS_sve_orn or INS_sve_orr, IF_SVE_BS_1A) => true,
            (INS_sve_bcax or INS_sve_bsl or INS_sve_bsl1n or INS_sve_bsl2n or INS_sve_eor3 or INS_sve_nbsl,
                IF_SVE_AV_3A) => true,
            (INS_sve_bfmlalb or INS_sve_bfmlalt or INS_sve_bfmlslb or INS_sve_bfmlslt or INS_sve_fmlslb or
                INS_sve_fmlslt, IF_SVE_GZ_3A or IF_SVE_HB_3A) => true,
            (INS_sve_decd or INS_sve_dech or INS_sve_decw or INS_sve_incd or INS_sve_inch or INS_sve_incw,
                IF_SVE_BN_1A) => true,
            (INS_sve_sabalb or INS_sve_sabalt or INS_sve_sqdmlalbt or INS_sve_sqdmlslbt or INS_sve_uabalb or
                INS_sve_uabalt, IF_SVE_EL_3A) => true,
            (INS_sve_addp or INS_sve_smaxp or INS_sve_sminp or INS_sve_umaxp or INS_sve_uminp, IF_SVE_AA_3A) => true,
            (INS_sve_eorbt or INS_sve_eortb or INS_sve_fclamp or INS_sve_sclamp or INS_sve_uclamp, IF_SVE_AT_3A) => true,
            (INS_sve_faddp or INS_sve_fmaxnmp or INS_sve_fmaxp or INS_sve_fminnmp or INS_sve_fminp, IF_SVE_GR_3A) => true,
            (INS_sve_adclb or INS_sve_adclt or INS_sve_sbclb or INS_sve_sbclt, IF_SVE_FY_3A) => true,
            (INS_sve_fmlallbb or INS_sve_fmlallbt or INS_sve_fmlalltb or INS_sve_fmlalltt,
                IF_SVE_GO_3A or IF_SVE_HC_3A) => true,
            (INS_sve_smax or INS_sve_smin or INS_sve_umax or INS_sve_umin, IF_SVE_ED_1A) => true,
            (INS_sve_sqdecp or INS_sve_sqincp or INS_sve_uqdecp or INS_sve_uqincp, IF_SVE_DP_2A) => true,
            (INS_sve_sqdmlalb or INS_sve_sqdmlalt or INS_sve_sqdmlslb or INS_sve_sqdmlslt,
                IF_SVE_EL_3A or IF_SVE_FJ_3A or IF_SVE_FJ_3B) => true,
            (INS_sve_srsra or INS_sve_ssra or INS_sve_ursra or INS_sve_usra, IF_SVE_FU_2A) => true,
            (INS_sve_smmla or INS_sve_ummla or INS_sve_usmmla, IF_SVE_FO_3A) => true,
            (INS_sve_bfmla or INS_sve_bfmls, IF_SVE_GU_3C) => true,
            (INS_sve_cadd or INS_sve_sqcadd, IF_SVE_FV_2A) => true,
            (INS_sve_clasta or INS_sve_clastb, IF_SVE_CM_3A) => true,
            (INS_sve_decp or INS_sve_incp, IF_SVE_DN_2A) => true,
            (INS_sve_fmla or INS_sve_fmls, IF_SVE_GU_3A or IF_SVE_GU_3B) => true,
            (INS_sve_fmlalb or INS_sve_fmlalt, IF_SVE_GM_3A or IF_SVE_GN_3A or IF_SVE_GZ_3A or IF_SVE_HB_3A) => true,
            (INS_sve_mla or INS_sve_mls, IF_SVE_FF_3A or IF_SVE_FF_3B or IF_SVE_FF_3C) => true,
            (INS_sve_saba or INS_sve_uaba, IF_SVE_FW_3A) => true,
            (INS_sve_sdot or INS_sve_udot, IF_SVE_EF_3A or IF_SVE_EG_3A or IF_SVE_EH_3A or IF_SVE_EY_3A or
                IF_SVE_EY_3B) => true,
            (INS_sve_sqrdmlah or INS_sve_sqrdmlsh, IF_SVE_EM_3A or IF_SVE_FK_3A or IF_SVE_FK_3B or IF_SVE_FK_3C) => true,
            (INS_sve_bfclamp, IF_SVE_GW_3B) => true,
            (INS_sve_bfdot, IF_SVE_GY_3B or IF_SVE_HA_3A) => true,
            (INS_sve_bfmmla, IF_SVE_HD_3A) => true,
            (INS_sve_cdot, IF_SVE_EJ_3A or IF_SVE_FA_3A or IF_SVE_FA_3B) => true,
            (INS_sve_cmla, IF_SVE_EK_3A or IF_SVE_FB_3A or IF_SVE_FB_3B) => true,
            (INS_sve_extq, IF_SVE_BY_2A) => true,
            (INS_sve_fcmla, IF_SVE_GV_3A) => true,
            (INS_sve_fdot, IF_SVE_GY_3A or IF_SVE_GY_3B or IF_SVE_GY_3B_D or IF_SVE_HA_3A or IF_SVE_HA_3A_E or
                IF_SVE_HA_3A_F) => true,
            (INS_sve_fmmla, IF_SVE_HD_3A_A) => true,
            (INS_sve_ftmad, IF_SVE_HN_2A) => true,
            (INS_sve_insr, IF_SVE_CC_2A or IF_SVE_CD_2A) => true,
            (INS_sve_madpt, IF_SVE_EW_3B) => true,
            (INS_sve_mlapt, IF_SVE_EW_3A) => true,
            (INS_sve_mul, IF_SVE_EE_1A) => true,
            (INS_sve_revd, IF_SVE_CT_3A) => true,
            (INS_sve_sqrdcmlah, IF_SVE_EK_3A or IF_SVE_FC_3A or IF_SVE_FC_3B) => true,
            (INS_sve_sudot, IF_SVE_EZ_3A) => true,
            (INS_sve_usdot, IF_SVE_EI_3A or IF_SVE_EZ_3A) => true,
            (INS_sve_xar, IF_SVE_AW_2A) => true,
            _ => false,
        };
    }
}
#endif
