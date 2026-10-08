// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    internal static int insGetSveReg1ListSize(instruction ins)
    {
        return ins switch
        {
            INS_sve_ld1d or INS_sve_ld1w or INS_sve_ld1sw or INS_sve_ld1sb or INS_sve_ld1b or
            INS_sve_ld1sh or INS_sve_ld1h or INS_sve_ldnf1d or INS_sve_ldnf1sw or INS_sve_ldnf1sh or
            INS_sve_ldnf1w or INS_sve_ldnf1h or INS_sve_ldnf1sb or INS_sve_ldnf1b or INS_sve_ldnt1b or
            INS_sve_ldnt1d or INS_sve_ldnt1h or INS_sve_ldnt1w or INS_sve_ld1rob or INS_sve_ld1rod or
            INS_sve_ld1roh or INS_sve_ld1row or INS_sve_ld1rqb or INS_sve_ld1rqd or INS_sve_ld1rqh or
            INS_sve_ld1rqw or INS_sve_stnt1b or INS_sve_stnt1d or INS_sve_stnt1h or INS_sve_stnt1w or
            INS_sve_st1d or INS_sve_st1w or INS_sve_ldff1sh or INS_sve_ldff1w or INS_sve_ldff1h or
            INS_sve_ldff1d or INS_sve_ldff1sw or INS_sve_st1b or INS_sve_st1h or INS_sve_ldff1sb or
            INS_sve_ldff1b or INS_sve_ldnt1sb or INS_sve_ldnt1sh or INS_sve_ld1rd or INS_sve_ld1rsw or
            INS_sve_ld1rh or INS_sve_ld1rsb or INS_sve_ld1rsh or INS_sve_ld1rw or INS_sve_ld1q or
            INS_sve_ldnt1sw or INS_sve_st1q or INS_sve_ld1rb => 1,

            INS_sve_ld2b or INS_sve_ld2h or INS_sve_ld2w or INS_sve_ld2d or INS_sve_ld2q or
            INS_sve_splice or INS_sve_st2b or INS_sve_st2h or INS_sve_st2w or INS_sve_st2d or
            INS_sve_st2q or INS_sve_whilege or INS_sve_whilegt or INS_sve_whilehi or INS_sve_whilehs or
            INS_sve_whilele or INS_sve_whilels or INS_sve_whilelt or INS_sve_pext => 2,

            INS_sve_ld3b or INS_sve_ld3h or INS_sve_ld3w or INS_sve_ld3d or INS_sve_ld3q or
            INS_sve_st3b or INS_sve_st3h or INS_sve_st3w or INS_sve_st3d or INS_sve_st3q => 3,

            INS_sve_ld4b or INS_sve_ld4h or INS_sve_ld4w or INS_sve_ld4d or INS_sve_ld4q or
            INS_sve_st4b or INS_sve_st4h or INS_sve_st4w or INS_sve_st4d or INS_sve_st4q => 4,

            _ => InvalidSveRegisterListSize(),
        };
    }

    private static int InvalidSveRegisterListSize()
    {
        assert(false, "!\"Unexpected instruction\"");
        return 1;
    }

    private static PredicateType insGetPredicateType(insFormat fmt, int regpos = 0)
    {
        switch (fmt)
        {
            case IF_SVE_BV_2A:
            case IF_SVE_HW_4A:
            case IF_SVE_HW_4A_A:
            case IF_SVE_HW_4A_B:
            case IF_SVE_HW_4A_C:
            case IF_SVE_HW_4B:
            case IF_SVE_HW_4B_D:
            case IF_SVE_HX_3A_E:
            case IF_SVE_IJ_3A_D:
            case IF_SVE_IJ_3A_E:
            case IF_SVE_IJ_3A_F:
            case IF_SVE_IK_4A_G:
            case IF_SVE_IJ_3A_G:
            case IF_SVE_IK_4A_I:
            case IF_SVE_IH_3A_F:
            case IF_SVE_II_4A_H:
            case IF_SVE_IH_3A:
            case IF_SVE_IH_3A_A:
            case IF_SVE_II_4A:
            case IF_SVE_II_4A_B:
            case IF_SVE_IU_4A:
            case IF_SVE_IU_4A_C:
            case IF_SVE_IU_4B:
            case IF_SVE_IU_4B_D:
            case IF_SVE_IV_3A:
            case IF_SVE_IG_4A_F:
            case IF_SVE_IG_4A_G:
            case IF_SVE_IJ_3A:
            case IF_SVE_IK_4A:
            case IF_SVE_IK_4A_F:
            case IF_SVE_IK_4A_H:
            case IF_SVE_IU_4A_A:
            case IF_SVE_IU_4B_B:
            case IF_SVE_HX_3A_B:
            case IF_SVE_IG_4A:
            case IF_SVE_IG_4A_D:
            case IF_SVE_IG_4A_E:
            case IF_SVE_IF_4A:
            case IF_SVE_IF_4A_A:
            case IF_SVE_IM_3A:
            case IF_SVE_IN_4A:
            case IF_SVE_IX_4A:
            case IF_SVE_IO_3A:
            case IF_SVE_IP_4A:
            case IF_SVE_IQ_3A:
            case IF_SVE_IR_4A:
            case IF_SVE_IS_3A:
            case IF_SVE_IT_4A:
            case IF_SVE_GI_4A:
            case IF_SVE_IC_3A_C:
            case IF_SVE_IC_3A:
            case IF_SVE_IC_3A_B:
            case IF_SVE_IC_3A_A:
            case IF_SVE_IL_3A_C:
            case IF_SVE_IL_3A:
            case IF_SVE_IL_3A_B:
            case IF_SVE_IL_3A_A:
            case IF_SVE_IW_4A:
            {
                return PredicateType.PREDICATE_ZERO;
            }

            case IF_SVE_BV_2A_J:
            case IF_SVE_CP_3A:
            case IF_SVE_CQ_3A:
            case IF_SVE_AM_2A:
            case IF_SVE_AO_3A:
            case IF_SVE_HL_3A:
            case IF_SVE_HM_2A:
            case IF_SVE_AA_3A:
            case IF_SVE_BU_2A:
            case IF_SVE_BV_2B:
            case IF_SVE_HS_3A:
            case IF_SVE_HP_3A:
            case IF_SVE_HP_3B:
            case IF_SVE_AR_4A:
            case IF_SVE_BV_2A_A:
            case IF_SVE_HU_4A:
            case IF_SVE_HL_3B:
            case IF_SVE_AB_3B:
            case IF_SVE_GT_4A:
            case IF_SVE_AP_3A:
            case IF_SVE_HO_3A:
            case IF_SVE_HO_3B:
            case IF_SVE_HO_3C:
            case IF_SVE_GQ_3A:
            case IF_SVE_HU_4B:
            case IF_SVE_AQ_3A:
            case IF_SVE_CU_3A:
            case IF_SVE_AC_3A:
            case IF_SVE_GR_3A:
            case IF_SVE_ES_3A:
            case IF_SVE_HR_3A:
            case IF_SVE_GP_3A:
            case IF_SVE_EQ_3A:
            case IF_SVE_HQ_3A:
            case IF_SVE_AS_4A:
            case IF_SVE_CT_3A:
            case IF_SVE_HV_4A:
            {
                return PredicateType.PREDICATE_MERGE;
            }

            case IF_SVE_CZ_4A_A:
            case IF_SVE_CZ_4A_L:
            case IF_SVE_CE_2A:
            case IF_SVE_CE_2B:
            case IF_SVE_CE_2C:
            case IF_SVE_CE_2D:
            case IF_SVE_CF_2A:
            case IF_SVE_CF_2B:
            case IF_SVE_CF_2C:
            case IF_SVE_CF_2D:
            case IF_SVE_CI_3A:
            case IF_SVE_CJ_2A:
            case IF_SVE_DE_1A:
            case IF_SVE_DH_1A:
            case IF_SVE_DJ_1A:
            case IF_SVE_DM_2A:
            case IF_SVE_DN_2A:
            case IF_SVE_DO_2A:
            case IF_SVE_DP_2A:
            case IF_SVE_DR_1A:
            case IF_SVE_DT_3A:
            case IF_SVE_DU_3A:
            case IF_SVE_CK_2A:
            {
                return PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DB_3A:
            {
                assert(regpos != 2);
                return PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DL_2A:
            case IF_SVE_DY_3A:
            case IF_SVE_DZ_1A:
            {
                return PredicateType.PREDICATE_N_SIZED;
            }

            case IF_SVE_AH_3A:
            {
                assert(false, "!\"TODO: Handle ambiguous predicate types\"");
                break;
            }

            case IF_SVE_JD_4B:
            case IF_SVE_JD_4C:
            case IF_SVE_JI_3A_A:
            case IF_SVE_JJ_4A:
            case IF_SVE_JJ_4A_B:
            case IF_SVE_JJ_4A_C:
            case IF_SVE_JJ_4A_D:
            case IF_SVE_JJ_4B:
            case IF_SVE_JJ_4B_E:
            case IF_SVE_JN_3B:
            case IF_SVE_JN_3C:
            case IF_SVE_JD_4A:
            case IF_SVE_JN_3A:
            case IF_SVE_JD_4C_A:
            case IF_SVE_JJ_4B_C:
            case IF_SVE_JL_3A:
            case IF_SVE_JN_3C_D:
            case IF_SVE_HY_3A:
            case IF_SVE_HY_3A_A:
            case IF_SVE_HY_3B:
            case IF_SVE_HZ_2A_B:
            case IF_SVE_IA_2A:
            case IF_SVE_IB_3A:
            case IF_SVE_JK_4A:
            case IF_SVE_JK_4A_B:
            case IF_SVE_JK_4B:
            case IF_SVE_IZ_4A:
            case IF_SVE_IZ_4A_A:
            case IF_SVE_JB_4A:
            case IF_SVE_JM_3A:
            case IF_SVE_CM_3A:
            case IF_SVE_CN_3A:
            case IF_SVE_CO_3A:
            case IF_SVE_JA_4A:
            case IF_SVE_CR_3A:
            case IF_SVE_CS_3A:
            case IF_SVE_CV_3A:
            case IF_SVE_CV_3B:
            case IF_SVE_DW_2A:
            case IF_SVE_DW_2B:
            case IF_SVE_JC_4A:
            case IF_SVE_JO_3A:
            case IF_SVE_JE_3A:
            case IF_SVE_JF_4A:
            case IF_SVE_AK_3A:
            case IF_SVE_HE_3A:
            case IF_SVE_AF_3A:
            case IF_SVE_AG_3A:
            case IF_SVE_AI_3A:
            case IF_SVE_AJ_3A:
            case IF_SVE_AL_3A:
            case IF_SVE_CL_3A:
            case IF_SVE_GS_3A:
            case IF_SVE_HJ_3A:
            case IF_SVE_IY_4A:
            {
                return PredicateType.PREDICATE_NONE;
            }

            case IF_SVE_CX_4A:
            case IF_SVE_CX_4A_A:
            case IF_SVE_CY_3A:
            case IF_SVE_CY_3B:
            case IF_SVE_GE_4A:
            case IF_SVE_HT_4A:
            {
                assert((regpos == 1) || (regpos == 2));
                return regpos == 2 ? PredicateType.PREDICATE_ZERO : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_CZ_4A:
            case IF_SVE_DA_4A:
            case IF_SVE_DB_3B:
            case IF_SVE_DC_3A:
            {
                assert((regpos >= 1) && (regpos <= 4));
                return regpos == 2 ? PredicateType.PREDICATE_ZERO : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_CZ_4A_K:
            {
                assert((regpos >= 1) && (regpos <= 3));
                return regpos == 2 ? PredicateType.PREDICATE_MERGE : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DD_2A:
            case IF_SVE_DF_2A:
            {
                assert((regpos >= 1) && (regpos <= 3));
                return regpos == 2 ? PredicateType.PREDICATE_NONE : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DG_2A:
            {
                return regpos == 2 ? PredicateType.PREDICATE_ZERO : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DI_2A:
            {
                return regpos == 1 ? PredicateType.PREDICATE_NONE : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DK_3A:
            {
                assert((regpos == 2) || (regpos == 3));
                return regpos == 2 ? PredicateType.PREDICATE_NONE : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_HI_3A:
            {
                assert((regpos == 1) || (regpos == 2));
                return regpos == 2 ? PredicateType.PREDICATE_ZERO : PredicateType.PREDICATE_SIZED;
            }

            case IF_SVE_DV_4A:
            {
                assert((regpos >= 1) && (regpos <= 3));
                return regpos == 3 ? PredicateType.PREDICATE_SIZED : PredicateType.PREDICATE_NONE;
            }

            case IF_SVE_ID_2A:
            case IF_SVE_JG_2A:
            {
                return PredicateType.PREDICATE_NONE;
            }

            default:
            {
                break;
            }
        }

        assert(false, "!\"Unexpected instruction format\"");
        return PredicateType.PREDICATE_NONE;
    }

    private static bool insSveIsLslN(instruction ins, insFormat fmt)
    {
        return (fmt, ins) switch
        {
            (IF_SVE_JD_4A, INS_sve_st1h) or
            (IF_SVE_JD_4B, INS_sve_st1w) or
            (IF_SVE_HW_4B, INS_sve_ld1h or INS_sve_ld1sh or INS_sve_ldff1h or INS_sve_ldff1sh or INS_sve_ld1w or INS_sve_ldff1w) or
            (IF_SVE_IG_4A, INS_sve_ldff1d or INS_sve_ldff1sw) or
            (IF_SVE_IG_4A_F, INS_sve_ldff1sh or INS_sve_ldff1w) or
            (IF_SVE_IG_4A_G, INS_sve_ldff1h) or
            (IF_SVE_II_4A or IF_SVE_II_4A_B, INS_sve_ld1d) or
            (IF_SVE_II_4A_H, INS_sve_ld1w) or
            (IF_SVE_IK_4A, INS_sve_ld1sw) or
            (IF_SVE_IK_4A_G, INS_sve_ld1sh) or
            (IF_SVE_IK_4A_I, INS_sve_ld1h) or
            (IF_SVE_IN_4A, INS_sve_ldnt1d or INS_sve_ldnt1h or INS_sve_ldnt1w) or
            (IF_SVE_IP_4A, INS_sve_ld1roh or INS_sve_ld1row or INS_sve_ld1rod or INS_sve_ld1rqh or INS_sve_ld1rqw or INS_sve_ld1rqd) or
            (IF_SVE_IR_4A, INS_sve_ld2q or INS_sve_ld3q or INS_sve_ld4q) or
            (IF_SVE_IT_4A, INS_sve_ld2h or INS_sve_ld2w or INS_sve_ld2d or INS_sve_ld3h or INS_sve_ld3w or INS_sve_ld3d or INS_sve_ld4h or INS_sve_ld4w or INS_sve_ld4d) or
            (IF_SVE_IU_4B, INS_sve_ld1sw or INS_sve_ldff1sw or INS_sve_ld1d or INS_sve_ldff1d) or
            (IF_SVE_JB_4A, INS_sve_stnt1h or INS_sve_stnt1w or INS_sve_stnt1d) or
            (IF_SVE_JC_4A, INS_sve_st2h or INS_sve_st2w or INS_sve_st2d or INS_sve_st3h or INS_sve_st3w or INS_sve_st3d or INS_sve_st4h or INS_sve_st4w or INS_sve_st4d) or
            (IF_SVE_JD_4C, INS_sve_st1w or INS_sve_st1d) or
            (IF_SVE_JD_4C_A, INS_sve_st1d) or
            (IF_SVE_JF_4A, INS_sve_st2q or INS_sve_st3q or INS_sve_st4q) or
            (IF_SVE_JJ_4B, INS_sve_st1h or INS_sve_st1w or INS_sve_st1d) or
            (IF_SVE_HY_3B or IF_SVE_IB_3A, INS_sve_prfh or INS_sve_prfw or INS_sve_prfd) => true,
            _ => false,
        };
    }

    private static bool insSveIsModN(instruction ins, insFormat fmt)
    {
        return (fmt, ins) switch
        {
            (IF_SVE_JJ_4A or IF_SVE_JJ_4A_B, INS_sve_st1d or INS_sve_st1h or INS_sve_st1w) or
            (IF_SVE_JJ_4A_C or IF_SVE_JJ_4A_D, INS_sve_st1h or INS_sve_st1w) or
            (IF_SVE_JK_4A or IF_SVE_JK_4A_B, INS_sve_st1b) or
            (IF_SVE_HW_4A or IF_SVE_HW_4A_A, INS_sve_ld1b or INS_sve_ld1h or INS_sve_ld1sb or INS_sve_ld1sh or INS_sve_ld1w or INS_sve_ldff1b or INS_sve_ldff1h or INS_sve_ldff1sb or INS_sve_ldff1sh or INS_sve_ldff1w) or
            (IF_SVE_HW_4A_B or IF_SVE_HW_4A_C, INS_sve_ld1h or INS_sve_ld1sh or INS_sve_ld1w or INS_sve_ldff1h or INS_sve_ldff1sh or INS_sve_ldff1w) or
            (IF_SVE_IU_4A, INS_sve_ld1d or INS_sve_ld1sw or INS_sve_ldff1d or INS_sve_ldff1sw) or
            (IF_SVE_IU_4A_A, INS_sve_ld1sw or INS_sve_ldff1d or INS_sve_ldff1sw) or
            (IF_SVE_IU_4A_C, INS_sve_ld1d) or
            (IF_SVE_HY_3A or IF_SVE_HY_3A_A, INS_sve_prfb or INS_sve_prfh or INS_sve_prfw or INS_sve_prfd) => true,
            _ => false,
        };
    }

    private static int insSveGetLslOrModN(instruction ins, insFormat fmt)
    {
        if (insSveIsLslN(ins, fmt))
        {
            return (fmt, ins) switch
            {
                (IF_SVE_JD_4A, INS_sve_st1h) => 1,
                (IF_SVE_JD_4B, INS_sve_st1w) => 2,
                (IF_SVE_HW_4B, INS_sve_ld1h or INS_sve_ld1sh or INS_sve_ldff1h or INS_sve_ldff1sh) => 1,
                (IF_SVE_HW_4B, INS_sve_ld1w or INS_sve_ldff1w) => 2,
                (IF_SVE_IG_4A, INS_sve_ldff1sw) => 2,
                (IF_SVE_IG_4A, INS_sve_ldff1d) => 3,
                (IF_SVE_IG_4A_F, INS_sve_ldff1sh) => 1,
                (IF_SVE_IG_4A_F, INS_sve_ldff1w) => 2,
                (IF_SVE_IG_4A_G, INS_sve_ldff1h) => 1,
                (IF_SVE_II_4A or IF_SVE_II_4A_B, INS_sve_ld1d) => 3,
                (IF_SVE_II_4A_H, INS_sve_ld1w) => 2,
                (IF_SVE_IK_4A, INS_sve_ld1sw) => 2,
                (IF_SVE_IK_4A_G, INS_sve_ld1sh) => 1,
                (IF_SVE_IK_4A_I, INS_sve_ld1h) => 1,
                (IF_SVE_IN_4A, INS_sve_ldnt1h) => 1,
                (IF_SVE_IN_4A, INS_sve_ldnt1w) => 2,
                (IF_SVE_IN_4A, INS_sve_ldnt1d) => 3,
                (IF_SVE_IP_4A, INS_sve_ld1roh or INS_sve_ld1rqh) => 1,
                (IF_SVE_IP_4A, INS_sve_ld1row or INS_sve_ld1rqw) => 2,
                (IF_SVE_IP_4A, INS_sve_ld1rod or INS_sve_ld1rqd) => 3,
                (IF_SVE_IR_4A, INS_sve_ld2q or INS_sve_ld3q or INS_sve_ld4q) => 4,
                (IF_SVE_IT_4A, INS_sve_ld2h or INS_sve_ld3h or INS_sve_ld4h) => 1,
                (IF_SVE_IT_4A, INS_sve_ld2w or INS_sve_ld3w or INS_sve_ld4w) => 2,
                (IF_SVE_IT_4A, INS_sve_ld2d or INS_sve_ld3d or INS_sve_ld4d) => 3,
                (IF_SVE_IU_4B, INS_sve_ld1sw or INS_sve_ldff1sw) => 2,
                (IF_SVE_IU_4B, INS_sve_ld1d or INS_sve_ldff1d) => 3,
                (IF_SVE_JB_4A, INS_sve_stnt1h) => 1,
                (IF_SVE_JB_4A, INS_sve_stnt1w) => 2,
                (IF_SVE_JB_4A, INS_sve_stnt1d) => 3,
                (IF_SVE_JC_4A, INS_sve_st2h or INS_sve_st3h or INS_sve_st4h) => 1,
                (IF_SVE_JC_4A, INS_sve_st2w or INS_sve_st3w or INS_sve_st4w) => 2,
                (IF_SVE_JC_4A, INS_sve_st2d or INS_sve_st3d or INS_sve_st4d) => 3,
                (IF_SVE_JD_4C, INS_sve_st1w) => 2,
                (IF_SVE_JD_4C, INS_sve_st1d) => 3,
                (IF_SVE_JD_4C_A, INS_sve_st1d) => 3,
                (IF_SVE_JF_4A, INS_sve_st2q or INS_sve_st3q or INS_sve_st4q) => 4,
                (IF_SVE_JJ_4B, INS_sve_st1h) => 1,
                (IF_SVE_JJ_4B, INS_sve_st1w) => 2,
                (IF_SVE_JJ_4B, INS_sve_st1d) => 3,
                (IF_SVE_HY_3B or IF_SVE_IB_3A, INS_sve_prfh) => 1,
                (IF_SVE_HY_3B or IF_SVE_IB_3A, INS_sve_prfw) => 2,
                (IF_SVE_HY_3B or IF_SVE_IB_3A, INS_sve_prfd) => 3,
                _ => InvalidSveModN(),
            };
        }

        assert(insSveIsModN(ins, fmt));
        return (fmt, ins) switch
        {
            (IF_SVE_HW_4A or IF_SVE_HW_4A_A, INS_sve_ld1h or INS_sve_ld1sh or INS_sve_ldff1h or INS_sve_ldff1sh) => 1,
            (IF_SVE_HW_4A or IF_SVE_HW_4A_A or IF_SVE_IU_4A, INS_sve_ld1w or INS_sve_ldff1w or INS_sve_ld1sw or INS_sve_ldff1sw) => 2,
            (IF_SVE_IU_4A, INS_sve_ld1d or INS_sve_ldff1d) => 3,
            (IF_SVE_JJ_4A_C or IF_SVE_JJ_4A_D, INS_sve_st1h or INS_sve_st1w) => 0,
            (IF_SVE_JJ_4A_B, INS_sve_st1d) => 0,
            (IF_SVE_JJ_4A or IF_SVE_JJ_4A_B or IF_SVE_JJ_4A_C or IF_SVE_JJ_4A_D, INS_sve_st1h) => 1,
            (IF_SVE_JJ_4A or IF_SVE_JJ_4A_B or IF_SVE_JJ_4A_C or IF_SVE_JJ_4A_D, INS_sve_st1w) => 2,
            (IF_SVE_JJ_4A or IF_SVE_JJ_4A_B, INS_sve_st1d) => 3,
            (IF_SVE_JK_4A or IF_SVE_JK_4A_B, INS_sve_st1b) => 0,
            (IF_SVE_HW_4A or IF_SVE_HW_4A_A or IF_SVE_HW_4A_B or IF_SVE_HW_4A_C, INS_sve_ld1b or INS_sve_ld1sb or INS_sve_ldff1b or INS_sve_ldff1sb) => 0,
            (IF_SVE_HW_4A or IF_SVE_HW_4A_A or IF_SVE_HW_4A_B or IF_SVE_HW_4A_C, INS_sve_ld1h or INS_sve_ld1sh or INS_sve_ldff1h or INS_sve_ldff1sh) => 0,
            (IF_SVE_HW_4A or IF_SVE_HW_4A_A or IF_SVE_HW_4A_B or IF_SVE_HW_4A_C, INS_sve_ld1w or INS_sve_ldff1w) => 0,
            (IF_SVE_IU_4A_A, INS_sve_ld1sw or INS_sve_ldff1sw or INS_sve_ldff1d) => 0,
            (IF_SVE_IU_4A_C, INS_sve_ld1d) => 0,
            (IF_SVE_HY_3A or IF_SVE_HY_3A_A, INS_sve_prfb) => 0,
            (IF_SVE_HY_3A or IF_SVE_HY_3A_A, INS_sve_prfh) => 1,
            (IF_SVE_HY_3A or IF_SVE_HY_3A_A, INS_sve_prfw) => 2,
            (IF_SVE_HY_3A or IF_SVE_HY_3A_A, INS_sve_prfd) => 3,
            _ => 0,
        };
    }

    private static int InvalidSveModN()
    {
        assert(false, "!\"Unexpected instruction format\"");
        return 0;
    }
}
#endif
