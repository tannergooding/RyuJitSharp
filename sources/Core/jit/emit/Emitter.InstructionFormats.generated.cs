// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public enum insFormat : uint
    {
#if TARGET_XARCH
        IF_NONE,
        IF_LABEL,
        IF_RWR_LABEL,
        IF_SWR_LABEL,
        IF_METHOD,
        IF_METHPTR,
        IF_CNS,
        IF_RRD,
        IF_RWR,
        IF_RRW,
        IF_RRD_CNS,
        IF_RWR_CNS,
        IF_RRW_CNS,
        IF_RRW_SHF,
        IF_RRD_RRD,
        IF_RWR_RRD,
        IF_RRW_RRD,
        IF_RRW_RRW,
        IF_RRD_RRD_CNS,
        IF_RWR_RRD_CNS,
        IF_RRW_RRD_CNS,
        IF_RWR_RRD_SHF,
        IF_RRD_RRD_RRD,
        IF_RWR_RRD_RRD,
        IF_RRW_RRD_RRD,
        IF_RWR_RWR_RRD,
        IF_RWR_RRD_RRD_CNS,
        IF_RWR_RRD_RRD_RRD,
        IF_MRD,
        IF_MWR,
        IF_MRW,
        IF_MRD_CNS,
        IF_MWR_CNS,
        IF_MRW_CNS,
        IF_MRW_SHF,
        IF_MRD_RRD,
        IF_MWR_RRD,
        IF_MRW_RRD,
        IF_MRW_RRW,
        IF_MRD_RRD_CNS,
        IF_MWR_RRD_CNS,
        IF_MRW_RRD_CNS,
        IF_MWR_RRD_RRD,
        IF_RRD_MRD,
        IF_RWR_MRD,
        IF_RRW_MRD,
        IF_RRD_MRD_CNS,
        IF_RWR_MRD_CNS,
        IF_RRW_MRD_CNS,
        IF_RRD_MRD_RRD,
        IF_RWR_MRD_RRD,
        IF_RRW_MRD_RRD,
        IF_RRD_RRD_MRD,
        IF_RWR_RRD_MRD,
        IF_RRW_RRD_MRD,
        IF_RWR_RWR_MRD,
        IF_RWR_RRD_MRD_CNS,
        IF_RWR_RRD_MRD_RRD,
        IF_MRD_OFF,
        IF_RWR_MRD_OFF,
        IF_SRD,
        IF_SWR,
        IF_SRW,
        IF_SRD_CNS,
        IF_SWR_CNS,
        IF_SRW_CNS,
        IF_SRW_SHF,
        IF_SRD_RRD,
        IF_SWR_RRD,
        IF_SRW_RRD,
        IF_SRW_RRW,
        IF_SRD_RRD_CNS,
        IF_SWR_RRD_CNS,
        IF_SRW_RRD_CNS,
        IF_SWR_RRD_RRD,
        IF_RRD_SRD,
        IF_RWR_SRD,
        IF_RRW_SRD,
        IF_RRD_SRD_CNS,
        IF_RWR_SRD_CNS,
        IF_RRW_SRD_CNS,
        IF_RRD_SRD_RRD,
        IF_RWR_SRD_RRD,
        IF_RRW_SRD_RRD,
        IF_RRD_RRD_SRD,
        IF_RWR_RRD_SRD,
        IF_RRW_RRD_SRD,
        IF_RWR_RWR_SRD,
        IF_RWR_RRD_SRD_CNS,
        IF_RWR_RRD_SRD_RRD,
        IF_ARD,
        IF_AWR,
        IF_ARW,
        IF_ARD_CNS,
        IF_AWR_CNS,
        IF_ARW_CNS,
        IF_ARW_SHF,
        IF_ARD_RRD,
        IF_AWR_RRD,
        IF_ARW_RRD,
        IF_ARW_RRW,
        IF_ARD_RRD_CNS,
        IF_AWR_RRD_CNS,
        IF_ARW_RRD_CNS,
        IF_AWR_RRD_RRD,
        IF_RRD_ARD,
        IF_RWR_ARD,
        IF_RRW_ARD,
        IF_RRD_ARD_CNS,
        IF_RWR_ARD_CNS,
        IF_RRW_ARD_CNS,
        IF_RRD_ARD_RRD,
        IF_RWR_ARD_RRD,
        IF_RRW_ARD_RRD,
        IF_RRD_RRD_ARD,
        IF_RWR_RRD_ARD,
        IF_RRW_RRD_ARD,
        IF_RWR_RWR_ARD,
        IF_RWR_RRD_ARD_CNS,
        IF_RWR_RRD_ARD_RRD,
#elif TARGET_ARM64
        IF_NONE,
        IF_LABEL,
        IF_LARGEJMP,
        IF_LARGEADR,
        IF_LARGELDC,
        IF_EN9,
        IF_EN6A,
        IF_EN6B,
        IF_EN5A,
        IF_EN5B,
        IF_EN5C,
        IF_EN4A,
        IF_EN4B,
        IF_EN4C,
        IF_EN4D,
        IF_EN4E,
        IF_EN4F,
        IF_EN4G,
        IF_EN4H,
        IF_EN4I,
        IF_EN4J,
        IF_EN4K,
        IF_EN3A,
        IF_EN3B,
        IF_EN3C,
        IF_EN3D,
        IF_EN3E,
        IF_EN3F,
        IF_EN3G,
        IF_EN3H,
        IF_EN3I,
        IF_EN3J,
        IF_EN2A,
        IF_EN2B,
        IF_EN2C,
        IF_EN2D,
        IF_EN2E,
        IF_EN2F,
        IF_EN2G,
        IF_EN2H,
        IF_EN2I,
        IF_EN2J,
        IF_EN2K,
        IF_EN2L,
        IF_EN2M,
        IF_EN2N,
        IF_EN2O,
        IF_EN2P,
        IF_EN2Q,
        IF_BI_0A,
        IF_BI_0B,
        IF_BI_0C,
        IF_BI_1A,
        IF_BI_1B,
        IF_BR_0A,
        IF_BR_1A,
        IF_BR_1B,
        IF_LS_1A,
        IF_LS_2A,
        IF_LS_2B,
        IF_LS_2C,
        IF_LS_2D,
        IF_LS_2E,
        IF_LS_2F,
        IF_LS_2G,
        IF_LS_3A,
        IF_LS_3B,
        IF_LS_3C,
        IF_LS_3D,
        IF_LS_3E,
        IF_LS_3F,
        IF_LS_3G,
        IF_DI_1A,
        IF_DI_1B,
        IF_DI_1C,
        IF_DI_1D,
        IF_DI_1E,
        IF_DI_1F,
        IF_DI_2A,
        IF_DI_2B,
        IF_DI_2C,
        IF_DI_2D,
        IF_DR_1D,
        IF_DR_2A,
        IF_DR_2B,
        IF_DR_2C,
        IF_DR_2D,
        IF_DR_2E,
        IF_DR_2F,
        IF_DR_2G,
        IF_DR_2H,
        IF_DR_2I,
        IF_DR_3A,
        IF_DR_3B,
        IF_DR_3C,
        IF_DR_3D,
        IF_DR_3E,
        IF_DR_4A,
        IF_DV_1A,
        IF_DV_1B,
        IF_DV_1C,
        IF_DV_2A,
        IF_DV_2B,
        IF_DV_2C,
        IF_DV_2D,
        IF_DV_2E,
        IF_DV_2F,
        IF_DV_2G,
        IF_DV_2H,
        IF_DV_2I,
        IF_DV_2J,
        IF_DV_2K,
        IF_DV_2L,
        IF_DV_2M,
        IF_DV_2N,
        IF_DV_2O,
        IF_DV_2P,
        IF_DV_2Q,
        IF_DV_2R,
        IF_DV_2S,
        IF_DV_2T,
        IF_DV_2U,
        IF_DV_2V,
        IF_DV_3A,
        IF_DV_3AI,
        IF_DV_3B,
        IF_DV_3BI,
        IF_DV_3C,
        IF_DV_3D,
        IF_DV_3DI,
        IF_DV_3E,
        IF_DV_3EI,
        IF_DV_3F,
        IF_DV_3G,
        IF_DV_3H,
        IF_DV_3I,
        IF_DV_4A,
        IF_DV_4B,
        IF_SN_0A,
        IF_SI_0A,
        IF_SI_0B,
        IF_PC_0A,
        IF_PC_1A,
        IF_PC_2A,
        IF_SR_1A,
        IF_INVALID,
        IF_SVE_13A,
        IF_SVE_11A,
        IF_SVE_9A,
        IF_SVE_9B,
        IF_SVE_9C,
        IF_SVE_9D,
        IF_SVE_9E,
        IF_SVE_9F,
        IF_SVE_8A,
        IF_SVE_8B,
        IF_SVE_8C,
        IF_SVE_7A,
        IF_SVE_6A,
        IF_SVE_6B,
        IF_SVE_6C,
        IF_SVE_6D,
        IF_SVE_6E,
        IF_SVE_6F,
        IF_SVE_6G,
        IF_SVE_5A,
        IF_SVE_5B,
        IF_SVE_5C,
        IF_SVE_5D,
        IF_SVE_5E,
        IF_SVE_4A,
        IF_SVE_4B,
        IF_SVE_4C,
        IF_SVE_4D,
        IF_SVE_4E,
        IF_SVE_4F,
        IF_SVE_4G,
        IF_SVE_4H,
        IF_SVE_4I,
        IF_SVE_4J,
        IF_SVE_4K,
        IF_SVE_4L,
        IF_SVE_3A,
        IF_SVE_3B,
        IF_SVE_3C,
        IF_SVE_3D,
        IF_SVE_3E,
        IF_SVE_3F,
        IF_SVE_3G,
        IF_SVE_3H,
        IF_SVE_3I,
        IF_SVE_3J,
        IF_SVE_3K,
        IF_SVE_3L,
        IF_SVE_3M,
        IF_SVE_3N,
        IF_SVE_3O,
        IF_SVE_3P,
        IF_SVE_3Q,
        IF_SVE_3R,
        IF_SVE_3S,
        IF_SVE_3T,
        IF_SVE_3U,
        IF_SVE_3V,
        IF_SVE_2AA,
        IF_SVE_2AB,
        IF_SVE_2AC,
        IF_SVE_2AD,
        IF_SVE_2AE,
        IF_SVE_2AF,
        IF_SVE_2AG,
        IF_SVE_2AH,
        IF_SVE_2AI,
        IF_SVE_2AJ,
        IF_SVE_2AK,
        IF_SVE_2AL,
        IF_SVE_2AM,
        IF_SVE_2AN,
        IF_SVE_2AO,
        IF_SVE_2AP,
        IF_SVE_2AQ,
        IF_SVE_2AR,
        IF_SVE_2AS,
        IF_SVE_2AT,
        IF_SVE_2AU,
        IF_SVE_2AV,
        IF_SVE_2AW,
        IF_SVE_2AX,
        IF_SVE_2AY,
        IF_SVE_2AZ,
        IF_SVE_2BA,
        IF_SVE_2BB,
        IF_SVE_2BC,
        IF_SVE_2BD,
        IF_SVE_2BE,
        IF_SVE_2BF,
        IF_SVE_2BG,
        IF_SVE_2BH,
        IF_SVE_2BI,
        IF_SVE_2BJ,
        IF_SVE_2BK,
        IF_SVE_2BL,
        IF_SVE_2BM,
        IF_SVE_2BN,
        IF_SVE_2BO,
        IF_SVE_2BP,
        IF_SVE_2BQ,
        IF_SVE_2BR,
        IF_SVE_2BS,
        IF_SVE_AA_3A,
        IF_SVE_AB_3B,
        IF_SVE_AC_3A,
        IF_SVE_AF_3A,
        IF_SVE_AG_3A,
        IF_SVE_AH_3A,
        IF_SVE_AI_3A,
        IF_SVE_AJ_3A,
        IF_SVE_AK_3A,
        IF_SVE_AL_3A,
        IF_SVE_AM_2A,
        IF_SVE_AO_3A,
        IF_SVE_AP_3A,
        IF_SVE_AQ_3A,
        IF_SVE_AR_4A,
        IF_SVE_AS_4A,
        IF_SVE_AT_3A,
        IF_SVE_AT_3B,
        IF_SVE_AU_3A,
        IF_SVE_AV_3A,
        IF_SVE_AW_2A,
        IF_SVE_AX_1A,
        IF_SVE_AY_2A,
        IF_SVE_AZ_2A,
        IF_SVE_BA_3A,
        IF_SVE_BB_2A,
        IF_SVE_BC_1A,
        IF_SVE_BD_3B,
        IF_SVE_BF_2A,
        IF_SVE_BG_3A,
        IF_SVE_BH_3A,
        IF_SVE_BH_3B,
        IF_SVE_BH_3B_A,
        IF_SVE_BI_2A,
        IF_SVE_BJ_2A,
        IF_SVE_BL_1A,
        IF_SVE_BM_1A,
        IF_SVE_BN_1A,
        IF_SVE_BO_1A,
        IF_SVE_BO_1A_A,
        IF_SVE_BP_1A,
        IF_SVE_BQ_2A,
        IF_SVE_BQ_2B,
        IF_SVE_BR_3B,
        IF_SVE_BS_1A,
        IF_SVE_BT_1A,
        IF_SVE_BU_2A,
        IF_SVE_BV_2A,
        IF_SVE_BV_2A_A,
        IF_SVE_BV_2B,
        IF_SVE_BV_2A_J,
        IF_SVE_BW_2A,
        IF_SVE_BX_2A,
        IF_SVE_BY_2A,
        IF_SVE_BZ_3A,
        IF_SVE_BZ_3A_A,
        IF_SVE_CB_2A,
        IF_SVE_CC_2A,
        IF_SVE_CD_2A,
        IF_SVE_CE_2A,
        IF_SVE_CE_2B,
        IF_SVE_CE_2C,
        IF_SVE_CE_2D,
        IF_SVE_CF_2A,
        IF_SVE_CF_2B,
        IF_SVE_CF_2C,
        IF_SVE_CF_2D,
        IF_SVE_CG_2A,
        IF_SVE_CH_2A,
        IF_SVE_CI_3A,
        IF_SVE_CJ_2A,
        IF_SVE_CK_2A,
        IF_SVE_CL_3A,
        IF_SVE_CM_3A,
        IF_SVE_CN_3A,
        IF_SVE_CO_3A,
        IF_SVE_CP_3A,
        IF_SVE_CQ_3A,
        IF_SVE_CR_3A,
        IF_SVE_CS_3A,
        IF_SVE_CT_3A,
        IF_SVE_CU_3A,
        IF_SVE_CV_3A,
        IF_SVE_CV_3B,
        IF_SVE_CW_4A,
        IF_SVE_CX_4A,
        IF_SVE_CX_4A_A,
        IF_SVE_CY_3A,
        IF_SVE_CY_3B,
        IF_SVE_CZ_4A,
        IF_SVE_CZ_4A_K,
        IF_SVE_CZ_4A_L,
        IF_SVE_CZ_4A_A,
        IF_SVE_DA_4A,
        IF_SVE_DB_3A,
        IF_SVE_DB_3B,
        IF_SVE_DC_3A,
        IF_SVE_DD_2A,
        IF_SVE_DE_1A,
        IF_SVE_DF_2A,
        IF_SVE_DG_2A,
        IF_SVE_DH_1A,
        IF_SVE_DI_2A,
        IF_SVE_DJ_1A,
        IF_SVE_DK_3A,
        IF_SVE_DL_2A,
        IF_SVE_DM_2A,
        IF_SVE_DN_2A,
        IF_SVE_DO_2A,
        IF_SVE_DO_2A_A,
        IF_SVE_DP_2A,
        IF_SVE_DQ_0A,
        IF_SVE_DR_1A,
        IF_SVE_DS_2A,
        IF_SVE_DT_3A,
        IF_SVE_DU_3A,
        IF_SVE_DV_4A,
        IF_SVE_DW_2A,
        IF_SVE_DW_2B,
        IF_SVE_DX_3A,
        IF_SVE_DY_3A,
        IF_SVE_DZ_1A,
        IF_SVE_EA_1A,
        IF_SVE_EB_1A,
        IF_SVE_EB_1B,
        IF_SVE_EC_1A,
        IF_SVE_ED_1A,
        IF_SVE_EE_1A,
        IF_SVE_EF_3A,
        IF_SVE_EG_3A,
        IF_SVE_EH_3A,
        IF_SVE_EI_3A,
        IF_SVE_EJ_3A,
        IF_SVE_EK_3A,
        IF_SVE_EL_3A,
        IF_SVE_EM_3A,
        IF_SVE_EQ_3A,
        IF_SVE_ES_3A,
        IF_SVE_EW_3A,
        IF_SVE_EW_3B,
        IF_SVE_EX_3A,
        IF_SVE_EY_3A,
        IF_SVE_EY_3B,
        IF_SVE_EZ_3A,
        IF_SVE_FA_3A,
        IF_SVE_FA_3B,
        IF_SVE_FB_3A,
        IF_SVE_FB_3B,
        IF_SVE_FC_3A,
        IF_SVE_FC_3B,
        IF_SVE_FD_3A,
        IF_SVE_FD_3B,
        IF_SVE_FD_3C,
        IF_SVE_FE_3A,
        IF_SVE_FE_3B,
        IF_SVE_FF_3A,
        IF_SVE_FF_3B,
        IF_SVE_FF_3C,
        IF_SVE_FG_3A,
        IF_SVE_FG_3B,
        IF_SVE_FH_3A,
        IF_SVE_FH_3B,
        IF_SVE_FI_3A,
        IF_SVE_FI_3B,
        IF_SVE_FI_3C,
        IF_SVE_FJ_3A,
        IF_SVE_FJ_3B,
        IF_SVE_FK_3A,
        IF_SVE_FK_3B,
        IF_SVE_FK_3C,
        IF_SVE_FL_3A,
        IF_SVE_FM_3A,
        IF_SVE_FN_3B,
        IF_SVE_FO_3A,
        IF_SVE_FR_2A,
        IF_SVE_FT_2A,
        IF_SVE_FU_2A,
        IF_SVE_FV_2A,
        IF_SVE_FW_3A,
        IF_SVE_FY_3A,
        IF_SVE_FZ_2A,
        IF_SVE_GA_2A,
        IF_SVE_GB_2A,
        IF_SVE_GC_3A,
        IF_SVE_GD_2A,
        IF_SVE_GE_4A,
        IF_SVE_GF_3A,
        IF_SVE_GG_3A,
        IF_SVE_GG_3B,
        IF_SVE_GH_3A,
        IF_SVE_GH_3B,
        IF_SVE_GH_3B_B,
        IF_SVE_GI_4A,
        IF_SVE_GJ_3A,
        IF_SVE_GK_2A,
        IF_SVE_GL_1A,
        IF_SVE_GM_3A,
        IF_SVE_GN_3A,
        IF_SVE_GO_3A,
        IF_SVE_GP_3A,
        IF_SVE_GQ_3A,
        IF_SVE_GR_3A,
        IF_SVE_GS_3A,
        IF_SVE_GT_4A,
        IF_SVE_GU_3A,
        IF_SVE_GU_3B,
        IF_SVE_GU_3C,
        IF_SVE_GV_3A,
        IF_SVE_GW_3B,
        IF_SVE_GX_3A,
        IF_SVE_GX_3B,
        IF_SVE_GX_3C,
        IF_SVE_GY_3A,
        IF_SVE_GY_3B,
        IF_SVE_GY_3B_D,
        IF_SVE_GZ_3A,
        IF_SVE_HA_3A,
        IF_SVE_HA_3A_E,
        IF_SVE_HA_3A_F,
        IF_SVE_HB_3A,
        IF_SVE_HC_3A,
        IF_SVE_HD_3A,
        IF_SVE_HD_3A_A,
        IF_SVE_HE_3A,
        IF_SVE_HF_2A,
        IF_SVE_HG_2A,
        IF_SVE_HH_2A,
        IF_SVE_HI_3A,
        IF_SVE_HJ_3A,
        IF_SVE_HK_3B,
        IF_SVE_HL_3A,
        IF_SVE_HL_3B,
        IF_SVE_HM_2A,
        IF_SVE_HN_2A,
        IF_SVE_HO_3A,
        IF_SVE_HO_3B,
        IF_SVE_HO_3C,
        IF_SVE_HP_3A,
        IF_SVE_HP_3B,
        IF_SVE_HQ_3A,
        IF_SVE_HR_3A,
        IF_SVE_HS_3A,
        IF_SVE_HT_4A,
        IF_SVE_HU_4A,
        IF_SVE_HU_4B,
        IF_SVE_HV_4A,
        IF_SVE_HW_4A,
        IF_SVE_HW_4A_A,
        IF_SVE_HW_4B,
        IF_SVE_HW_4A_B,
        IF_SVE_HW_4A_C,
        IF_SVE_HW_4B_D,
        IF_SVE_HX_3A,
        IF_SVE_HX_3A_B,
        IF_SVE_HX_3A_E,
        IF_SVE_HY_3A,
        IF_SVE_HY_3A_A,
        IF_SVE_HY_3B,
        IF_SVE_HZ_2A,
        IF_SVE_HZ_2A_B,
        IF_SVE_IA_2A,
        IF_SVE_IB_3A,
        IF_SVE_IC_3A,
        IF_SVE_IC_3A_A,
        IF_SVE_IC_3A_B,
        IF_SVE_IC_3A_C,
        IF_SVE_ID_2A,
        IF_SVE_IE_2A,
        IF_SVE_IF_4A,
        IF_SVE_IF_4A_A,
        IF_SVE_IG_4A,
        IF_SVE_IG_4A_C,
        IF_SVE_IG_4A_D,
        IF_SVE_IG_4A_E,
        IF_SVE_IG_4A_F,
        IF_SVE_IG_4A_G,
        IF_SVE_IH_3A,
        IF_SVE_IH_3A_F,
        IF_SVE_IH_3A_G,
        IF_SVE_IH_3A_A,
        IF_SVE_II_4A,
        IF_SVE_II_4A_H,
        IF_SVE_II_4A_I,
        IF_SVE_II_4A_B,
        IF_SVE_IJ_3A,
        IF_SVE_IJ_3A_C,
        IF_SVE_IJ_3A_D,
        IF_SVE_IJ_3A_E,
        IF_SVE_IJ_3A_F,
        IF_SVE_IJ_3A_G,
        IF_SVE_IK_4A,
        IF_SVE_IK_4A_F,
        IF_SVE_IK_4A_G,
        IF_SVE_IK_4A_H,
        IF_SVE_IK_4A_I,
        IF_SVE_IK_4A_E,
        IF_SVE_IL_3A,
        IF_SVE_IL_3A_A,
        IF_SVE_IL_3A_B,
        IF_SVE_IL_3A_C,
        IF_SVE_IM_3A,
        IF_SVE_IN_4A,
        IF_SVE_IO_3A,
        IF_SVE_IP_4A,
        IF_SVE_IQ_3A,
        IF_SVE_IR_4A,
        IF_SVE_IS_3A,
        IF_SVE_IT_4A,
        IF_SVE_IU_4A,
        IF_SVE_IU_4A_A,
        IF_SVE_IU_4B,
        IF_SVE_IU_4B_B,
        IF_SVE_IU_4A_C,
        IF_SVE_IU_4B_D,
        IF_SVE_IV_3A,
        IF_SVE_IW_4A,
        IF_SVE_IX_4A,
        IF_SVE_IY_4A,
        IF_SVE_IZ_4A,
        IF_SVE_IZ_4A_A,
        IF_SVE_JA_4A,
        IF_SVE_JB_4A,
        IF_SVE_JC_4A,
        IF_SVE_JD_4A,
        IF_SVE_JD_4B,
        IF_SVE_JD_4C,
        IF_SVE_JD_4C_A,
        IF_SVE_JE_3A,
        IF_SVE_JF_4A,
        IF_SVE_JG_2A,
        IF_SVE_JH_2A,
        IF_SVE_JI_3A,
        IF_SVE_JI_3A_A,
        IF_SVE_JJ_4A,
        IF_SVE_JJ_4A_B,
        IF_SVE_JJ_4A_C,
        IF_SVE_JJ_4A_D,
        IF_SVE_JJ_4B,
        IF_SVE_JJ_4B_E,
        IF_SVE_JJ_4B_C,
        IF_SVE_JK_4A,
        IF_SVE_JK_4A_B,
        IF_SVE_JK_4B,
        IF_SVE_JL_3A,
        IF_SVE_JM_3A,
        IF_SVE_JN_3A,
        IF_SVE_JN_3B,
        IF_SVE_JN_3C,
        IF_SVE_JN_3C_D,
        IF_SVE_JO_3A,
#endif
        IF_COUNT,
    }

#if TARGET_XARCH
    internal static ReadOnlySpan<byte> emitFmtToOps => [
        (byte)ID_OP_NONE, // IF_NONE
        (byte)ID_OP_JMP, // IF_LABEL
        (byte)ID_OP_JMP, // IF_RWR_LABEL
        (byte)ID_OP_LBL, // IF_SWR_LABEL
        (byte)ID_OP_CALL, // IF_METHOD
        (byte)ID_OP_CALL, // IF_METHPTR
        (byte)ID_OP_SCNS, // IF_CNS
        (byte)ID_OP_NONE, // IF_RRD
        (byte)ID_OP_NONE, // IF_RWR
        (byte)ID_OP_NONE, // IF_RRW
        (byte)ID_OP_SCNS, // IF_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_CNS
        (byte)ID_OP_SCNS, // IF_RRW_CNS
        (byte)ID_OP_SCNS, // IF_RRW_SHF
        (byte)ID_OP_NONE, // IF_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRW
        (byte)ID_OP_SCNS, // IF_RRD_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RRW_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_SHF
        (byte)ID_OP_NONE, // IF_RRD_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RRD_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RWR_RRD
        (byte)ID_OP_SCNS, // IF_RWR_RRD_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_RRD_RRD
        (byte)ID_OP_SPEC, // IF_MRD
        (byte)ID_OP_DSP, // IF_MWR
        (byte)ID_OP_DSP, // IF_MRW
        (byte)ID_OP_DSP_CNS, // IF_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MWR_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_SHF
        (byte)ID_OP_DSP, // IF_MRD_RRD
        (byte)ID_OP_DSP, // IF_MWR_RRD
        (byte)ID_OP_DSP, // IF_MRW_RRD
        (byte)ID_OP_DSP, // IF_MRW_RRW
        (byte)ID_OP_DSP_CNS, // IF_MRD_RRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MWR_RRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_RRD_CNS
        (byte)ID_OP_DSP, // IF_MWR_RRD_RRD
        (byte)ID_OP_DSP, // IF_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_MRD
        (byte)ID_OP_DSP, // IF_RRW_MRD
        (byte)ID_OP_DSP_CNS, // IF_RRD_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RWR_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RRW_MRD_CNS
        (byte)ID_OP_DSP, // IF_RRD_MRD_RRD
        (byte)ID_OP_DSP, // IF_RWR_MRD_RRD
        (byte)ID_OP_DSP, // IF_RRW_MRD_RRD
        (byte)ID_OP_DSP, // IF_RRD_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_RRD_MRD
        (byte)ID_OP_DSP, // IF_RRW_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_RWR_MRD
        (byte)ID_OP_DSP_CNS, // IF_RWR_RRD_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RWR_RRD_MRD_RRD
        (byte)ID_OP_DSP, // IF_MRD_OFF
        (byte)ID_OP_DSP, // IF_RWR_MRD_OFF
        (byte)ID_OP_SPEC, // IF_SRD
        (byte)ID_OP_NONE, // IF_SWR
        (byte)ID_OP_NONE, // IF_SRW
        (byte)ID_OP_CNS, // IF_SRD_CNS
        (byte)ID_OP_CNS, // IF_SWR_CNS
        (byte)ID_OP_CNS, // IF_SRW_CNS
        (byte)ID_OP_CNS, // IF_SRW_SHF
        (byte)ID_OP_NONE, // IF_SRD_RRD
        (byte)ID_OP_NONE, // IF_SWR_RRD
        (byte)ID_OP_NONE, // IF_SRW_RRD
        (byte)ID_OP_NONE, // IF_SRW_RRW
        (byte)ID_OP_CNS, // IF_SRD_RRD_CNS
        (byte)ID_OP_CNS, // IF_SWR_RRD_CNS
        (byte)ID_OP_CNS, // IF_SRW_RRD_CNS
        (byte)ID_OP_NONE, // IF_SWR_RRD_RRD
        (byte)ID_OP_NONE, // IF_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_SRD
        (byte)ID_OP_NONE, // IF_RRW_SRD
        (byte)ID_OP_CNS, // IF_RRD_SRD_CNS
        (byte)ID_OP_CNS, // IF_RWR_SRD_CNS
        (byte)ID_OP_CNS, // IF_RRW_SRD_CNS
        (byte)ID_OP_NONE, // IF_RRD_SRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_SRD_RRD
        (byte)ID_OP_NONE, // IF_RRW_SRD_RRD
        (byte)ID_OP_NONE, // IF_RRD_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_RRD_SRD
        (byte)ID_OP_NONE, // IF_RRW_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_RWR_SRD
        (byte)ID_OP_CNS, // IF_RWR_RRD_SRD_CNS
        (byte)ID_OP_CNS, // IF_RWR_RRD_SRD_RRD
        (byte)ID_OP_SPEC, // IF_ARD
        (byte)ID_OP_AMD, // IF_AWR
        (byte)ID_OP_AMD, // IF_ARW
        (byte)ID_OP_AMD_CNS, // IF_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_SHF
        (byte)ID_OP_AMD, // IF_ARD_RRD
        (byte)ID_OP_AMD, // IF_AWR_RRD
        (byte)ID_OP_AMD, // IF_ARW_RRD
        (byte)ID_OP_AMD, // IF_ARW_RRW
        (byte)ID_OP_AMD_CNS, // IF_ARD_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_RRD_RRD
        (byte)ID_OP_AMD, // IF_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_ARD
        (byte)ID_OP_AMD, // IF_RRW_ARD
        (byte)ID_OP_AMD_CNS, // IF_RRD_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RWR_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RRW_ARD_CNS
        (byte)ID_OP_AMD, // IF_RRD_ARD_RRD
        (byte)ID_OP_AMD, // IF_RWR_ARD_RRD
        (byte)ID_OP_AMD, // IF_RRW_ARD_RRD
        (byte)ID_OP_AMD, // IF_RRD_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_RRD_ARD
        (byte)ID_OP_AMD, // IF_RRW_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_RWR_ARD
        (byte)ID_OP_AMD_CNS, // IF_RWR_RRD_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RWR_RRD_ARD_RRD
    ];

    private static ReadOnlySpan<IS_INFO> emitFmtToSchedInfo => [
        IS_NONE, // IF_NONE
        IS_NONE, // IF_LABEL
        IS_R1_WR, // IF_RWR_LABEL
        IS_SF_WR, // IF_SWR_LABEL
        IS_NONE, // IF_METHOD
        IS_NONE, // IF_METHPTR
        IS_NONE, // IF_CNS
        IS_R1_RD, // IF_RRD
        IS_R1_WR, // IF_RWR
        IS_R1_RW, // IF_RRW
        IS_R1_RD, // IF_RRD_CNS
        IS_R1_WR, // IF_RWR_CNS
        IS_R1_RW, // IF_RRW_CNS
        IS_R1_RW, // IF_RRW_SHF
        IS_R1_RD | IS_R2_RD, // IF_RRD_RRD
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD
        IS_R1_RW | IS_R2_RD, // IF_RRW_RRD
        IS_R1_RW | IS_R2_RW, // IF_RRW_RRW
        IS_R1_RD | IS_R2_RD, // IF_RRD_RRD_CNS
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD_CNS
        IS_R1_RW | IS_R2_RD, // IF_RRW_RRD_CNS
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD_SHF
        IS_R1_RD | IS_R2_RD | IS_R3_RD, // IF_RRD_RRD_RRD
        IS_R1_WR | IS_R2_RD | IS_R3_RD, // IF_RWR_RRD_RRD
        IS_R1_RW | IS_R2_RD | IS_R3_RD, // IF_RRW_RRD_RRD
        IS_R1_WR | IS_R2_WR | IS_R3_RD, // IF_RWR_RWR_RRD
        IS_R1_WR | IS_R2_RD | IS_R3_RD, // IF_RWR_RRD_RRD_CNS
        IS_R1_WR | IS_R2_RD | IS_R3_RD | IS_R4_RD, // IF_RWR_RRD_RRD_RRD
        IS_GM_RD, // IF_MRD
        IS_GM_WR, // IF_MWR
        IS_GM_RW, // IF_MRW
        IS_GM_RD, // IF_MRD_CNS
        IS_GM_WR, // IF_MWR_CNS
        IS_GM_RW, // IF_MRW_CNS
        IS_GM_RW, // IF_MRW_SHF
        IS_GM_RD | IS_R1_RD, // IF_MRD_RRD
        IS_GM_WR | IS_R1_RD, // IF_MWR_RRD
        IS_GM_RW | IS_R1_RD, // IF_MRW_RRD
        IS_GM_RW | IS_R1_RW, // IF_MRW_RRW
        IS_GM_RD | IS_R1_RD, // IF_MRD_RRD_CNS
        IS_GM_WR | IS_R1_RD, // IF_MWR_RRD_CNS
        IS_GM_RW | IS_R1_RD, // IF_MRW_RRD_CNS
        IS_GM_WR | IS_R1_RD | IS_R2_RD, // IF_MWR_RRD_RRD
        IS_R1_RD | IS_GM_RD, // IF_RRD_MRD
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD
        IS_R1_RW | IS_GM_RD, // IF_RRW_MRD
        IS_R1_RD | IS_GM_RD, // IF_RRD_MRD_CNS
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD_CNS
        IS_R1_RW | IS_GM_RD, // IF_RRW_MRD_CNS
        IS_R1_RD | IS_GM_RD | IS_R2_RD, // IF_RRD_MRD_RRD
        IS_R1_WR | IS_GM_RD | IS_R2_RD, // IF_RWR_MRD_RRD
        IS_R1_RW | IS_GM_RD | IS_R2_RD, // IF_RRW_MRD_RRD
        IS_R1_RD | IS_R2_RD | IS_GM_RD, // IF_RRD_RRD_MRD
        IS_R1_WR | IS_R2_RD | IS_GM_RD, // IF_RWR_RRD_MRD
        IS_R1_RW | IS_R2_RD | IS_GM_RD, // IF_RRW_RRD_MRD
        IS_R1_WR | IS_R2_WR | IS_GM_RD, // IF_RWR_RWR_MRD
        IS_R1_WR | IS_R2_RD | IS_GM_RD, // IF_RWR_RRD_MRD_CNS
        IS_R1_WR | IS_R2_RD | IS_GM_RD | IS_R3_RD, // IF_RWR_RRD_MRD_RRD
        IS_GM_RD, // IF_MRD_OFF
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD_OFF
        IS_SF_RD, // IF_SRD
        IS_SF_WR, // IF_SWR
        IS_SF_RW, // IF_SRW
        IS_SF_RD, // IF_SRD_CNS
        IS_SF_WR, // IF_SWR_CNS
        IS_SF_RW, // IF_SRW_CNS
        IS_SF_RW, // IF_SRW_SHF
        IS_SF_RD | IS_R1_RD, // IF_SRD_RRD
        IS_SF_WR | IS_R1_RD, // IF_SWR_RRD
        IS_SF_RW | IS_R1_RD, // IF_SRW_RRD
        IS_SF_RW | IS_R1_RW, // IF_SRW_RRW
        IS_SF_RD | IS_R1_RD, // IF_SRD_RRD_CNS
        IS_SF_WR | IS_R1_RD, // IF_SWR_RRD_CNS
        IS_SF_RW | IS_R1_RD, // IF_SRW_RRD_CNS
        IS_SF_WR | IS_R1_RD | IS_R2_RD, // IF_SWR_RRD_RRD
        IS_R1_RD | IS_SF_RD, // IF_RRD_SRD
        IS_R1_WR | IS_SF_RD, // IF_RWR_SRD
        IS_R1_RW | IS_SF_RD, // IF_RRW_SRD
        IS_R1_RD | IS_SF_RD, // IF_RRD_SRD_CNS
        IS_R1_WR | IS_SF_RD, // IF_RWR_SRD_CNS
        IS_R1_RW | IS_SF_RD, // IF_RRW_SRD_CNS
        IS_R1_RD | IS_SF_RD | IS_R2_RD, // IF_RRD_SRD_RRD
        IS_R1_WR | IS_SF_RD | IS_R2_RD, // IF_RWR_SRD_RRD
        IS_R1_RW | IS_SF_RD | IS_R2_RD, // IF_RRW_SRD_RRD
        IS_R1_RD | IS_R2_RD | IS_SF_RD, // IF_RRD_RRD_SRD
        IS_R1_WR | IS_R2_RD | IS_SF_RD, // IF_RWR_RRD_SRD
        IS_R1_RW | IS_R2_RD | IS_SF_RD, // IF_RRW_RRD_SRD
        IS_R1_WR | IS_R2_WR | IS_SF_RD, // IF_RWR_RWR_SRD
        IS_R1_WR | IS_R2_RD | IS_SF_RD, // IF_RWR_RRD_SRD_CNS
        IS_R1_WR | IS_R2_RD | IS_SF_RD | IS_R3_RD, // IF_RWR_RRD_SRD_RRD
        IS_AM_RD, // IF_ARD
        IS_AM_WR, // IF_AWR
        IS_AM_RW, // IF_ARW
        IS_AM_RD, // IF_ARD_CNS
        IS_AM_WR, // IF_AWR_CNS
        IS_AM_RW, // IF_ARW_CNS
        IS_AM_RW, // IF_ARW_SHF
        IS_AM_RD | IS_R1_RD, // IF_ARD_RRD
        IS_AM_WR | IS_R1_RD, // IF_AWR_RRD
        IS_AM_RW | IS_R1_RD, // IF_ARW_RRD
        IS_AM_RW | IS_R1_RW, // IF_ARW_RRW
        IS_AM_RD | IS_R1_RD, // IF_ARD_RRD_CNS
        IS_AM_WR | IS_R1_RD, // IF_AWR_RRD_CNS
        IS_AM_RW | IS_R1_RD, // IF_ARW_RRD_CNS
        IS_AM_WR | IS_R1_RD | IS_R2_RD, // IF_AWR_RRD_RRD
        IS_R1_RD | IS_AM_RD, // IF_RRD_ARD
        IS_R1_WR | IS_AM_RD, // IF_RWR_ARD
        IS_R1_RW | IS_AM_RD, // IF_RRW_ARD
        IS_R1_RD | IS_AM_RD, // IF_RRD_ARD_CNS
        IS_R1_WR | IS_AM_RD, // IF_RWR_ARD_CNS
        IS_R1_RW | IS_AM_RD, // IF_RRW_ARD_CNS
        IS_R1_RD | IS_AM_RD | IS_R2_RD, // IF_RRD_ARD_RRD
        IS_R1_WR | IS_AM_RD | IS_R2_RD, // IF_RWR_ARD_RRD
        IS_R1_RW | IS_AM_RD | IS_R2_RD, // IF_RRW_ARD_RRD
        IS_R1_RD | IS_R2_RD | IS_AM_RD, // IF_RRD_RRD_ARD
        IS_R1_WR | IS_R2_RD | IS_AM_RD, // IF_RWR_RRD_ARD
        IS_R1_RW | IS_R2_RD | IS_AM_RD, // IF_RRW_RRD_ARD
        IS_R1_WR | IS_R2_WR | IS_AM_RD, // IF_RWR_RWR_ARD
        IS_R1_WR | IS_R2_RD | IS_AM_RD, // IF_RWR_RRD_ARD_CNS
        IS_R1_WR | IS_R2_RD | IS_AM_RD | IS_R3_RD, // IF_RWR_RRD_ARD_RRD
    ];
#endif
}