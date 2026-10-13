// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitInsCodeSve(instruction ins, insFormat fmt)
    {
        RequireArm64Target();
        var code = BAD_CODE;
        var insFmt = emitInsFormatArm64(ins);
        var encoding_found = false;
        var index = -1;

        ReadOnlySpan<insFormat> formats = insFmt switch
        {
            IF_ARM64_SVE_13A => [
                IF_ARM64_SVE_AU_3A, IF_ARM64_SVE_BT_1A, IF_ARM64_SVE_BV_2A, IF_ARM64_SVE_BV_2A_J,
                IF_ARM64_SVE_BW_2A, IF_ARM64_SVE_CB_2A, IF_ARM64_SVE_CP_3A, IF_ARM64_SVE_CQ_3A,
                IF_ARM64_SVE_CW_4A, IF_ARM64_SVE_CZ_4A, IF_ARM64_SVE_CZ_4A_K, IF_ARM64_SVE_CZ_4A_L,
                IF_ARM64_SVE_EB_1A,
            ],
            IF_ARM64_SVE_11A => [
                IF_ARM64_SVE_JD_4B, IF_ARM64_SVE_JD_4C, IF_ARM64_SVE_JI_3A_A, IF_ARM64_SVE_JJ_4A,
                IF_ARM64_SVE_JJ_4A_B, IF_ARM64_SVE_JJ_4A_C, IF_ARM64_SVE_JJ_4A_D, IF_ARM64_SVE_JJ_4B,
                IF_ARM64_SVE_JJ_4B_E, IF_ARM64_SVE_JN_3B, IF_ARM64_SVE_JN_3C,
            ],
            IF_ARM64_SVE_9A => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4A_B, IF_ARM64_SVE_HW_4A_C,
                IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HW_4B_D, IF_ARM64_SVE_HX_3A_E, IF_ARM64_SVE_IJ_3A_F, IF_ARM64_SVE_IK_4A_G,
            ],
            IF_ARM64_SVE_9B => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4A_B, IF_ARM64_SVE_HW_4A_C,
                IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HW_4B_D, IF_ARM64_SVE_HX_3A_E, IF_ARM64_SVE_IJ_3A_G, IF_ARM64_SVE_IK_4A_I,
            ],
            IF_ARM64_SVE_9C => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4A_B, IF_ARM64_SVE_HW_4A_C,
                IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HW_4B_D, IF_ARM64_SVE_HX_3A_E, IF_ARM64_SVE_IH_3A_F, IF_ARM64_SVE_II_4A_H,
            ],
            IF_ARM64_SVE_9D => [
                IF_ARM64_SVE_IH_3A, IF_ARM64_SVE_IH_3A_A, IF_ARM64_SVE_II_4A, IF_ARM64_SVE_II_4A_B,
                IF_ARM64_SVE_IU_4A, IF_ARM64_SVE_IU_4A_C, IF_ARM64_SVE_IU_4B, IF_ARM64_SVE_IU_4B_D, IF_ARM64_SVE_IV_3A,
            ],
            IF_ARM64_SVE_9E => [
                IF_ARM64_SVE_JD_4A, IF_ARM64_SVE_JI_3A_A, IF_ARM64_SVE_JJ_4A, IF_ARM64_SVE_JJ_4A_B,
                IF_ARM64_SVE_JJ_4A_C, IF_ARM64_SVE_JJ_4A_D, IF_ARM64_SVE_JJ_4B, IF_ARM64_SVE_JJ_4B_E, IF_ARM64_SVE_JN_3A,
            ],
            IF_ARM64_SVE_9F => [
                IF_ARM64_SVE_JD_4C, IF_ARM64_SVE_JD_4C_A, IF_ARM64_SVE_JJ_4A, IF_ARM64_SVE_JJ_4A_B,
                IF_ARM64_SVE_JJ_4B, IF_ARM64_SVE_JJ_4B_C, IF_ARM64_SVE_JL_3A, IF_ARM64_SVE_JN_3C, IF_ARM64_SVE_JN_3C_D,
            ],
            IF_ARM64_SVE_8A => [
                IF_ARM64_SVE_CE_2A, IF_ARM64_SVE_CE_2B, IF_ARM64_SVE_CE_2C, IF_ARM64_SVE_CE_2D,
                IF_ARM64_SVE_CF_2A, IF_ARM64_SVE_CF_2B, IF_ARM64_SVE_CF_2C, IF_ARM64_SVE_CF_2D,
            ],
            IF_ARM64_SVE_8B => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4A_B, IF_ARM64_SVE_HW_4A_C,
                IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HW_4B_D, IF_ARM64_SVE_HX_3A_E, IF_ARM64_SVE_IG_4A_F,
            ],
            IF_ARM64_SVE_8C => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4A_B, IF_ARM64_SVE_HW_4A_C,
                IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HW_4B_D, IF_ARM64_SVE_HX_3A_E, IF_ARM64_SVE_IG_4A_G,
            ],
            IF_ARM64_SVE_7A => [
                IF_ARM64_SVE_IJ_3A, IF_ARM64_SVE_IK_4A, IF_ARM64_SVE_IU_4A, IF_ARM64_SVE_IU_4A_A,
                IF_ARM64_SVE_IU_4B, IF_ARM64_SVE_IU_4B_B, IF_ARM64_SVE_IV_3A,
            ],
            IF_ARM64_SVE_6A => [
                IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_EE_1A, IF_ARM64_SVE_FD_3A, IF_ARM64_SVE_FD_3B, IF_ARM64_SVE_FD_3C,
            ],
            IF_ARM64_SVE_6B => [
                IF_ARM64_SVE_GY_3A, IF_ARM64_SVE_GY_3B, IF_ARM64_SVE_GY_3B_D, IF_ARM64_SVE_HA_3A, IF_ARM64_SVE_HA_3A_E, IF_ARM64_SVE_HA_3A_F,
            ],
            IF_ARM64_SVE_6C => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HX_3A_B, IF_ARM64_SVE_IJ_3A_D, IF_ARM64_SVE_IK_4A_F,
            ],
            IF_ARM64_SVE_6D => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HX_3A_B, IF_ARM64_SVE_IJ_3A_E, IF_ARM64_SVE_IK_4A_H,
            ],
            IF_ARM64_SVE_6E => [
                IF_ARM64_SVE_HY_3A, IF_ARM64_SVE_HY_3A_A, IF_ARM64_SVE_HY_3B, IF_ARM64_SVE_HZ_2A_B, IF_ARM64_SVE_IA_2A, IF_ARM64_SVE_IB_3A,
            ],
            IF_ARM64_SVE_6F => [
                IF_ARM64_SVE_IG_4A, IF_ARM64_SVE_IU_4A, IF_ARM64_SVE_IU_4A_A, IF_ARM64_SVE_IU_4B, IF_ARM64_SVE_IU_4B_B, IF_ARM64_SVE_IV_3A,
            ],
            IF_ARM64_SVE_6G => [
                IF_ARM64_SVE_JD_4A, IF_ARM64_SVE_JI_3A_A, IF_ARM64_SVE_JK_4A, IF_ARM64_SVE_JK_4A_B, IF_ARM64_SVE_JK_4B, IF_ARM64_SVE_JN_3A,
            ],
            IF_ARM64_SVE_5A => [
                IF_ARM64_SVE_AM_2A, IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_AO_3A, IF_ARM64_SVE_BF_2A, IF_ARM64_SVE_BG_3A,
            ],
            IF_ARM64_SVE_5B => [
                IF_ARM64_SVE_GX_3A, IF_ARM64_SVE_GX_3B, IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_HL_3A, IF_ARM64_SVE_HM_2A,
            ],
            IF_ARM64_SVE_5C => [
                IF_ARM64_SVE_EF_3A, IF_ARM64_SVE_EG_3A, IF_ARM64_SVE_EH_3A, IF_ARM64_SVE_EY_3A, IF_ARM64_SVE_EY_3B,
            ],
            IF_ARM64_SVE_5D => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HX_3A_B, IF_ARM64_SVE_IG_4A_D,
            ],
            IF_ARM64_SVE_5E => [
                IF_ARM64_SVE_HW_4A, IF_ARM64_SVE_HW_4A_A, IF_ARM64_SVE_HW_4B, IF_ARM64_SVE_HX_3A_B, IF_ARM64_SVE_IG_4A_E,
            ],
            IF_ARM64_SVE_4A => [IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_AU_3A, IF_ARM64_SVE_BS_1A, IF_ARM64_SVE_CZ_4A],
            IF_ARM64_SVE_4B => [IF_ARM64_SVE_BU_2A, IF_ARM64_SVE_BV_2B, IF_ARM64_SVE_EA_1A, IF_ARM64_SVE_EB_1B],
            IF_ARM64_SVE_4E => [IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_FI_3A, IF_ARM64_SVE_FI_3B, IF_ARM64_SVE_FI_3C],
            IF_ARM64_SVE_4F => [IF_ARM64_SVE_EM_3A, IF_ARM64_SVE_FK_3A, IF_ARM64_SVE_FK_3B, IF_ARM64_SVE_FK_3C],
            IF_ARM64_SVE_4G => [IF_ARM64_SVE_AR_4A, IF_ARM64_SVE_FF_3A, IF_ARM64_SVE_FF_3B, IF_ARM64_SVE_FF_3C],
            IF_ARM64_SVE_4H => [IF_ARM64_SVE_GM_3A, IF_ARM64_SVE_GN_3A, IF_ARM64_SVE_GZ_3A, IF_ARM64_SVE_HB_3A],
            IF_ARM64_SVE_4I => [IF_ARM64_SVE_AX_1A, IF_ARM64_SVE_AY_2A, IF_ARM64_SVE_AZ_2A, IF_ARM64_SVE_BA_3A],
            IF_ARM64_SVE_4J => [IF_ARM64_SVE_BV_2A, IF_ARM64_SVE_BV_2A_A, IF_ARM64_SVE_CP_3A, IF_ARM64_SVE_CQ_3A],
            IF_ARM64_SVE_4K => [IF_ARM64_SVE_IF_4A, IF_ARM64_SVE_IF_4A_A, IF_ARM64_SVE_IM_3A, IF_ARM64_SVE_IN_4A],
            IF_ARM64_SVE_4L => [IF_ARM64_SVE_IZ_4A, IF_ARM64_SVE_IZ_4A_A, IF_ARM64_SVE_JB_4A, IF_ARM64_SVE_JM_3A],
            IF_ARM64_SVE_3A => [IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_EC_1A],
            IF_ARM64_SVE_3B => [IF_ARM64_SVE_BH_3A, IF_ARM64_SVE_BH_3B, IF_ARM64_SVE_BH_3B_A],
            IF_ARM64_SVE_3C => [IF_ARM64_SVE_BW_2A, IF_ARM64_SVE_CB_2A, IF_ARM64_SVE_EB_1A],
            IF_ARM64_SVE_3D => [IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_BR_3B, IF_ARM64_SVE_CI_3A],
            IF_ARM64_SVE_3E => [IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_EC_1A, IF_ARM64_SVE_AA_3A],
            IF_ARM64_SVE_3F => [IF_ARM64_SVE_GU_3A, IF_ARM64_SVE_GU_3B, IF_ARM64_SVE_HU_4A],
            IF_ARM64_SVE_3G => [IF_ARM64_SVE_GH_3A, IF_ARM64_SVE_GH_3B, IF_ARM64_SVE_GH_3B_B],
            IF_ARM64_SVE_3H => [IF_ARM64_SVE_AT_3A, IF_ARM64_SVE_HL_3A, IF_ARM64_SVE_HM_2A],
            IF_ARM64_SVE_3I => [IF_ARM64_SVE_CM_3A, IF_ARM64_SVE_CN_3A, IF_ARM64_SVE_CO_3A],
            IF_ARM64_SVE_3J => [IF_ARM64_SVE_CX_4A, IF_ARM64_SVE_CX_4A_A, IF_ARM64_SVE_CY_3A],
            IF_ARM64_SVE_3K => [IF_ARM64_SVE_CX_4A, IF_ARM64_SVE_CX_4A_A, IF_ARM64_SVE_CY_3B],
            IF_ARM64_SVE_3L => [IF_ARM64_SVE_DT_3A, IF_ARM64_SVE_DX_3A, IF_ARM64_SVE_DY_3A],
            IF_ARM64_SVE_3M => [IF_ARM64_SVE_EJ_3A, IF_ARM64_SVE_FA_3A, IF_ARM64_SVE_FA_3B],
            IF_ARM64_SVE_3N => [IF_ARM64_SVE_EK_3A, IF_ARM64_SVE_FB_3A, IF_ARM64_SVE_FB_3B],
            IF_ARM64_SVE_3O => [IF_ARM64_SVE_EK_3A, IF_ARM64_SVE_FC_3A, IF_ARM64_SVE_FC_3B],
            IF_ARM64_SVE_3P => [IF_ARM64_SVE_EL_3A, IF_ARM64_SVE_FG_3A, IF_ARM64_SVE_FG_3B],
            IF_ARM64_SVE_3Q => [IF_ARM64_SVE_EL_3A, IF_ARM64_SVE_FJ_3A, IF_ARM64_SVE_FJ_3B],
            IF_ARM64_SVE_3R => [IF_ARM64_SVE_FE_3A, IF_ARM64_SVE_FE_3B, IF_ARM64_SVE_FL_3A],
            IF_ARM64_SVE_3S => [IF_ARM64_SVE_FH_3A, IF_ARM64_SVE_FH_3B, IF_ARM64_SVE_FL_3A],
            IF_ARM64_SVE_3T => [IF_ARM64_SVE_GX_3C, IF_ARM64_SVE_HK_3B, IF_ARM64_SVE_HL_3B],
            IF_ARM64_SVE_3U => [IF_ARM64_SVE_IM_3A, IF_ARM64_SVE_IN_4A, IF_ARM64_SVE_IX_4A],
            IF_ARM64_SVE_3V => [IF_ARM64_SVE_JA_4A, IF_ARM64_SVE_JB_4A, IF_ARM64_SVE_JM_3A],
            IF_ARM64_SVE_2AA => [IF_ARM64_SVE_ID_2A, IF_ARM64_SVE_IE_2A],
            IF_ARM64_SVE_2AB => [IF_ARM64_SVE_JG_2A, IF_ARM64_SVE_JH_2A],
            IF_ARM64_SVE_2AC => [IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_ED_1A],
            IF_ARM64_SVE_2AD => [IF_ARM64_SVE_AB_3B, IF_ARM64_SVE_AT_3B],
            IF_ARM64_SVE_2AE => [IF_ARM64_SVE_CG_2A, IF_ARM64_SVE_CJ_2A],
            IF_ARM64_SVE_2AF => [IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_AT_3A],
            IF_ARM64_SVE_2AG => [IF_ARM64_SVE_BS_1A, IF_ARM64_SVE_CZ_4A],
            IF_ARM64_SVE_2AH => [IF_ARM64_SVE_BQ_2A, IF_ARM64_SVE_BQ_2B],
            IF_ARM64_SVE_2AI => [IF_ARM64_SVE_AM_2A, IF_ARM64_SVE_AA_3A],
            IF_ARM64_SVE_2AJ => [IF_ARM64_SVE_HI_3A, IF_ARM64_SVE_HT_4A],
            IF_ARM64_SVE_2AK => [IF_ARM64_SVE_BZ_3A, IF_ARM64_SVE_BZ_3A_A],
            IF_ARM64_SVE_2AL => [IF_ARM64_SVE_GG_3A, IF_ARM64_SVE_GG_3B],
            IF_ARM64_SVE_2AM => [IF_ARM64_SVE_HL_3A, IF_ARM64_SVE_HM_2A],
            IF_ARM64_SVE_2AN => [IF_ARM64_SVE_EI_3A, IF_ARM64_SVE_EZ_3A],
            IF_ARM64_SVE_2AO => [IF_ARM64_SVE_GT_4A, IF_ARM64_SVE_GV_3A],
            IF_ARM64_SVE_2AP => [IF_ARM64_SVE_GY_3B, IF_ARM64_SVE_HA_3A],
            IF_ARM64_SVE_2AQ => [IF_ARM64_SVE_GO_3A, IF_ARM64_SVE_HC_3A],
            IF_ARM64_SVE_2AR => [IF_ARM64_SVE_AP_3A, IF_ARM64_SVE_CZ_4A],
            IF_ARM64_SVE_2AT => [IF_ARM64_SVE_AA_3A, IF_ARM64_SVE_EC_1A],
            IF_ARM64_SVE_2AU => [IF_ARM64_SVE_AH_3A, IF_ARM64_SVE_BI_2A],
            IF_ARM64_SVE_2AV => [IF_ARM64_SVE_BM_1A, IF_ARM64_SVE_BN_1A],
            IF_ARM64_SVE_2AW => [IF_ARM64_SVE_BO_1A, IF_ARM64_SVE_BP_1A],
            IF_ARM64_SVE_2AX => [IF_ARM64_SVE_CC_2A, IF_ARM64_SVE_CD_2A],
            IF_ARM64_SVE_2AY => [IF_ARM64_SVE_CR_3A, IF_ARM64_SVE_CS_3A],
            IF_ARM64_SVE_2AZ => [IF_ARM64_SVE_CV_3A, IF_ARM64_SVE_CV_3B],
            IF_ARM64_SVE_2BA => [IF_ARM64_SVE_CW_4A, IF_ARM64_SVE_CZ_4A],
            IF_ARM64_SVE_2BB => [IF_ARM64_SVE_CZ_4A, IF_ARM64_SVE_CZ_4A_A],
            IF_ARM64_SVE_2BC => [IF_ARM64_SVE_DE_1A, IF_ARM64_SVE_DZ_1A],
            IF_ARM64_SVE_2BD => [IF_ARM64_SVE_DG_2A, IF_ARM64_SVE_DH_1A],
            IF_ARM64_SVE_2BE => [IF_ARM64_SVE_DK_3A, IF_ARM64_SVE_DL_2A],
            IF_ARM64_SVE_2BF => [IF_ARM64_SVE_DM_2A, IF_ARM64_SVE_DN_2A],
            IF_ARM64_SVE_2BG => [IF_ARM64_SVE_DO_2A, IF_ARM64_SVE_DP_2A],
            IF_ARM64_SVE_2BH => [IF_ARM64_SVE_DW_2A, IF_ARM64_SVE_DW_2B],
            IF_ARM64_SVE_2BI => [IF_ARM64_SVE_FL_3A, IF_ARM64_SVE_FN_3B],
            IF_ARM64_SVE_2BJ => [IF_ARM64_SVE_GQ_3A, IF_ARM64_SVE_HG_2A],
            IF_ARM64_SVE_2BK => [IF_ARM64_SVE_GU_3C, IF_ARM64_SVE_HU_4B],
            IF_ARM64_SVE_2BL => [IF_ARM64_SVE_GZ_3A, IF_ARM64_SVE_HB_3A],
            IF_ARM64_SVE_2BM => [IF_ARM64_SVE_HK_3B, IF_ARM64_SVE_HL_3B],
            IF_ARM64_SVE_2BN => [IF_ARM64_SVE_IF_4A, IF_ARM64_SVE_IF_4A_A],
            IF_ARM64_SVE_2BO => [IF_ARM64_SVE_IO_3A, IF_ARM64_SVE_IP_4A],
            IF_ARM64_SVE_2BP => [IF_ARM64_SVE_IQ_3A, IF_ARM64_SVE_IR_4A],
            IF_ARM64_SVE_2BQ => [IF_ARM64_SVE_IS_3A, IF_ARM64_SVE_IT_4A],
            IF_ARM64_SVE_2BR => [IF_ARM64_SVE_JC_4A, IF_ARM64_SVE_JO_3A],
            IF_ARM64_SVE_2BS => [IF_ARM64_SVE_JE_3A, IF_ARM64_SVE_JF_4A],
            _ => [],
        };

        if (!formats.IsEmpty)
        {
            for (index = 0; index < formats.Length; index++)
            {
                if (fmt == formats[index])
                {
                    encoding_found = true;
                    break;
                }
            }
        }
        else if (fmt == insFmt)
        {
            encoding_found = true;
            index = 0;
        }

        assert(encoding_found);
        var sve_ins_offset = unchecked((uint)ins - (uint)INS_sve_invalid);

        switch (index)
        {
            case 0:
            {
                assert(sve_ins_offset < (uint)insCodes1_ARM64.Length, "sve_ins_offset < ArrLen(insCodes1_ARM64)");
                code = insCodes1_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 1:
            {
                assert(sve_ins_offset < (uint)insCodes2_ARM64.Length, "sve_ins_offset < ArrLen(insCodes2_ARM64)");
                code = insCodes2_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 2:
            {
                assert(sve_ins_offset < (uint)insCodes3_ARM64.Length, "sve_ins_offset < ArrLen(insCodes3_ARM64)");
                code = insCodes3_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 3:
            {
                assert(sve_ins_offset < (uint)insCodes4_ARM64.Length, "sve_ins_offset < ArrLen(insCodes4_ARM64)");
                code = insCodes4_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 4:
            {
                assert(sve_ins_offset < (uint)insCodes5_ARM64.Length, "sve_ins_offset < ArrLen(insCodes5_ARM64)");
                code = insCodes5_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 5:
            {
                assert(sve_ins_offset < (uint)insCodes6_ARM64.Length, "sve_ins_offset < ArrLen(insCodes6_ARM64)");
                code = insCodes6_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 6:
            {
                assert(sve_ins_offset < (uint)insCodes7_ARM64.Length, "sve_ins_offset < ArrLen(insCodes7_ARM64)");
                code = insCodes7_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 7:
            {
                assert(sve_ins_offset < (uint)insCodes8_ARM64.Length, "sve_ins_offset < ArrLen(insCodes8_ARM64)");
                code = insCodes8_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 8:
            {
                assert(sve_ins_offset < (uint)insCodes9_ARM64.Length, "sve_ins_offset < ArrLen(insCodes9_ARM64)");
                code = insCodes9_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 9:
            {
                assert(sve_ins_offset < (uint)insCodes10_ARM64.Length, "sve_ins_offset < ArrLen(insCodes10_ARM64)");
                code = insCodes10_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 10:
            {
                assert(sve_ins_offset < (uint)insCodes11_ARM64.Length, "sve_ins_offset < ArrLen(insCodes11_ARM64)");
                code = insCodes11_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 11:
            {
                assert(sve_ins_offset < (uint)insCodes12_ARM64.Length, "sve_ins_offset < ArrLen(insCodes12_ARM64)");
                code = insCodes12_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }

            case 12:
            {
                assert(sve_ins_offset < (uint)insCodes13_ARM64.Length, "sve_ins_offset < ArrLen(insCodes13_ARM64)");
                code = insCodes13_ARM64[unchecked((int)sve_ins_offset)];
                break;
            }
        }

        assert(code != BAD_CODE, "(code != BAD_CODE)");

        return code;
    }
}
