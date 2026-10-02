// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitInsCodeSve(instruction ins, insFormat fmt)
    {
        var code = BAD_CODE;
        var insFmt = emitInsFormat(ins);
        var encoding_found = false;
        var index = -1;

        ReadOnlySpan<insFormat> formats = insFmt switch
        {
            IF_SVE_13A => [
                IF_SVE_AU_3A, IF_SVE_BT_1A, IF_SVE_BV_2A, IF_SVE_BV_2A_J,
                IF_SVE_BW_2A, IF_SVE_CB_2A, IF_SVE_CP_3A, IF_SVE_CQ_3A,
                IF_SVE_CW_4A, IF_SVE_CZ_4A, IF_SVE_CZ_4A_K, IF_SVE_CZ_4A_L,
                IF_SVE_EB_1A,
            ],
            IF_SVE_11A => [
                IF_SVE_JD_4B, IF_SVE_JD_4C, IF_SVE_JI_3A_A, IF_SVE_JJ_4A,
                IF_SVE_JJ_4A_B, IF_SVE_JJ_4A_C, IF_SVE_JJ_4A_D, IF_SVE_JJ_4B,
                IF_SVE_JJ_4B_E, IF_SVE_JN_3B, IF_SVE_JN_3C,
            ],
            IF_SVE_9A => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4A_B, IF_SVE_HW_4A_C,
                IF_SVE_HW_4B, IF_SVE_HW_4B_D, IF_SVE_HX_3A_E, IF_SVE_IJ_3A_F, IF_SVE_IK_4A_G,
            ],
            IF_SVE_9B => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4A_B, IF_SVE_HW_4A_C,
                IF_SVE_HW_4B, IF_SVE_HW_4B_D, IF_SVE_HX_3A_E, IF_SVE_IJ_3A_G, IF_SVE_IK_4A_I,
            ],
            IF_SVE_9C => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4A_B, IF_SVE_HW_4A_C,
                IF_SVE_HW_4B, IF_SVE_HW_4B_D, IF_SVE_HX_3A_E, IF_SVE_IH_3A_F, IF_SVE_II_4A_H,
            ],
            IF_SVE_9D => [
                IF_SVE_IH_3A, IF_SVE_IH_3A_A, IF_SVE_II_4A, IF_SVE_II_4A_B,
                IF_SVE_IU_4A, IF_SVE_IU_4A_C, IF_SVE_IU_4B, IF_SVE_IU_4B_D, IF_SVE_IV_3A,
            ],
            IF_SVE_9E => [
                IF_SVE_JD_4A, IF_SVE_JI_3A_A, IF_SVE_JJ_4A, IF_SVE_JJ_4A_B,
                IF_SVE_JJ_4A_C, IF_SVE_JJ_4A_D, IF_SVE_JJ_4B, IF_SVE_JJ_4B_E, IF_SVE_JN_3A,
            ],
            IF_SVE_9F => [
                IF_SVE_JD_4C, IF_SVE_JD_4C_A, IF_SVE_JJ_4A, IF_SVE_JJ_4A_B,
                IF_SVE_JJ_4B, IF_SVE_JJ_4B_C, IF_SVE_JL_3A, IF_SVE_JN_3C, IF_SVE_JN_3C_D,
            ],
            IF_SVE_8A => [
                IF_SVE_CE_2A, IF_SVE_CE_2B, IF_SVE_CE_2C, IF_SVE_CE_2D,
                IF_SVE_CF_2A, IF_SVE_CF_2B, IF_SVE_CF_2C, IF_SVE_CF_2D,
            ],
            IF_SVE_8B => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4A_B, IF_SVE_HW_4A_C,
                IF_SVE_HW_4B, IF_SVE_HW_4B_D, IF_SVE_HX_3A_E, IF_SVE_IG_4A_F,
            ],
            IF_SVE_8C => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4A_B, IF_SVE_HW_4A_C,
                IF_SVE_HW_4B, IF_SVE_HW_4B_D, IF_SVE_HX_3A_E, IF_SVE_IG_4A_G,
            ],
            IF_SVE_7A => [
                IF_SVE_IJ_3A, IF_SVE_IK_4A, IF_SVE_IU_4A, IF_SVE_IU_4A_A,
                IF_SVE_IU_4B, IF_SVE_IU_4B_B, IF_SVE_IV_3A,
            ],
            IF_SVE_6A => [
                IF_SVE_AA_3A, IF_SVE_AT_3A, IF_SVE_EE_1A, IF_SVE_FD_3A, IF_SVE_FD_3B, IF_SVE_FD_3C,
            ],
            IF_SVE_6B => [
                IF_SVE_GY_3A, IF_SVE_GY_3B, IF_SVE_GY_3B_D, IF_SVE_HA_3A, IF_SVE_HA_3A_E, IF_SVE_HA_3A_F,
            ],
            IF_SVE_6C => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4B, IF_SVE_HX_3A_B, IF_SVE_IJ_3A_D, IF_SVE_IK_4A_F,
            ],
            IF_SVE_6D => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4B, IF_SVE_HX_3A_B, IF_SVE_IJ_3A_E, IF_SVE_IK_4A_H,
            ],
            IF_SVE_6E => [
                IF_SVE_HY_3A, IF_SVE_HY_3A_A, IF_SVE_HY_3B, IF_SVE_HZ_2A_B, IF_SVE_IA_2A, IF_SVE_IB_3A,
            ],
            IF_SVE_6F => [
                IF_SVE_IG_4A, IF_SVE_IU_4A, IF_SVE_IU_4A_A, IF_SVE_IU_4B, IF_SVE_IU_4B_B, IF_SVE_IV_3A,
            ],
            IF_SVE_6G => [
                IF_SVE_JD_4A, IF_SVE_JI_3A_A, IF_SVE_JK_4A, IF_SVE_JK_4A_B, IF_SVE_JK_4B, IF_SVE_JN_3A,
            ],
            IF_SVE_5A => [
                IF_SVE_AM_2A, IF_SVE_AA_3A, IF_SVE_AO_3A, IF_SVE_BF_2A, IF_SVE_BG_3A,
            ],
            IF_SVE_5B => [
                IF_SVE_GX_3A, IF_SVE_GX_3B, IF_SVE_AT_3A, IF_SVE_HL_3A, IF_SVE_HM_2A,
            ],
            IF_SVE_5C => [
                IF_SVE_EF_3A, IF_SVE_EG_3A, IF_SVE_EH_3A, IF_SVE_EY_3A, IF_SVE_EY_3B,
            ],
            IF_SVE_5D => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4B, IF_SVE_HX_3A_B, IF_SVE_IG_4A_D,
            ],
            IF_SVE_5E => [
                IF_SVE_HW_4A, IF_SVE_HW_4A_A, IF_SVE_HW_4B, IF_SVE_HX_3A_B, IF_SVE_IG_4A_E,
            ],
            IF_SVE_4A => [IF_SVE_AA_3A, IF_SVE_AU_3A, IF_SVE_BS_1A, IF_SVE_CZ_4A],
            IF_SVE_4B => [IF_SVE_BU_2A, IF_SVE_BV_2B, IF_SVE_EA_1A, IF_SVE_EB_1B],
            IF_SVE_4E => [IF_SVE_AT_3A, IF_SVE_FI_3A, IF_SVE_FI_3B, IF_SVE_FI_3C],
            IF_SVE_4F => [IF_SVE_EM_3A, IF_SVE_FK_3A, IF_SVE_FK_3B, IF_SVE_FK_3C],
            IF_SVE_4G => [IF_SVE_AR_4A, IF_SVE_FF_3A, IF_SVE_FF_3B, IF_SVE_FF_3C],
            IF_SVE_4H => [IF_SVE_GM_3A, IF_SVE_GN_3A, IF_SVE_GZ_3A, IF_SVE_HB_3A],
            IF_SVE_4I => [IF_SVE_AX_1A, IF_SVE_AY_2A, IF_SVE_AZ_2A, IF_SVE_BA_3A],
            IF_SVE_4J => [IF_SVE_BV_2A, IF_SVE_BV_2A_A, IF_SVE_CP_3A, IF_SVE_CQ_3A],
            IF_SVE_4K => [IF_SVE_IF_4A, IF_SVE_IF_4A_A, IF_SVE_IM_3A, IF_SVE_IN_4A],
            IF_SVE_4L => [IF_SVE_IZ_4A, IF_SVE_IZ_4A_A, IF_SVE_JB_4A, IF_SVE_JM_3A],
            IF_SVE_3A => [IF_SVE_AA_3A, IF_SVE_AT_3A, IF_SVE_EC_1A],
            IF_SVE_3B => [IF_SVE_BH_3A, IF_SVE_BH_3B, IF_SVE_BH_3B_A],
            IF_SVE_3C => [IF_SVE_BW_2A, IF_SVE_CB_2A, IF_SVE_EB_1A],
            IF_SVE_3D => [IF_SVE_AT_3A, IF_SVE_BR_3B, IF_SVE_CI_3A],
            IF_SVE_3E => [IF_SVE_AT_3A, IF_SVE_EC_1A, IF_SVE_AA_3A],
            IF_SVE_3F => [IF_SVE_GU_3A, IF_SVE_GU_3B, IF_SVE_HU_4A],
            IF_SVE_3G => [IF_SVE_GH_3A, IF_SVE_GH_3B, IF_SVE_GH_3B_B],
            IF_SVE_3H => [IF_SVE_AT_3A, IF_SVE_HL_3A, IF_SVE_HM_2A],
            IF_SVE_3I => [IF_SVE_CM_3A, IF_SVE_CN_3A, IF_SVE_CO_3A],
            IF_SVE_3J => [IF_SVE_CX_4A, IF_SVE_CX_4A_A, IF_SVE_CY_3A],
            IF_SVE_3K => [IF_SVE_CX_4A, IF_SVE_CX_4A_A, IF_SVE_CY_3B],
            IF_SVE_3L => [IF_SVE_DT_3A, IF_SVE_DX_3A, IF_SVE_DY_3A],
            IF_SVE_3M => [IF_SVE_EJ_3A, IF_SVE_FA_3A, IF_SVE_FA_3B],
            IF_SVE_3N => [IF_SVE_EK_3A, IF_SVE_FB_3A, IF_SVE_FB_3B],
            IF_SVE_3O => [IF_SVE_EK_3A, IF_SVE_FC_3A, IF_SVE_FC_3B],
            IF_SVE_3P => [IF_SVE_EL_3A, IF_SVE_FG_3A, IF_SVE_FG_3B],
            IF_SVE_3Q => [IF_SVE_EL_3A, IF_SVE_FJ_3A, IF_SVE_FJ_3B],
            IF_SVE_3R => [IF_SVE_FE_3A, IF_SVE_FE_3B, IF_SVE_FL_3A],
            IF_SVE_3S => [IF_SVE_FH_3A, IF_SVE_FH_3B, IF_SVE_FL_3A],
            IF_SVE_3T => [IF_SVE_GX_3C, IF_SVE_HK_3B, IF_SVE_HL_3B],
            IF_SVE_3U => [IF_SVE_IM_3A, IF_SVE_IN_4A, IF_SVE_IX_4A],
            IF_SVE_3V => [IF_SVE_JA_4A, IF_SVE_JB_4A, IF_SVE_JM_3A],
            IF_SVE_2AA => [IF_SVE_ID_2A, IF_SVE_IE_2A],
            IF_SVE_2AB => [IF_SVE_JG_2A, IF_SVE_JH_2A],
            IF_SVE_2AC => [IF_SVE_AA_3A, IF_SVE_ED_1A],
            IF_SVE_2AD => [IF_SVE_AB_3B, IF_SVE_AT_3B],
            IF_SVE_2AE => [IF_SVE_CG_2A, IF_SVE_CJ_2A],
            IF_SVE_2AF => [IF_SVE_AA_3A, IF_SVE_AT_3A],
            IF_SVE_2AG => [IF_SVE_BS_1A, IF_SVE_CZ_4A],
            IF_SVE_2AH => [IF_SVE_BQ_2A, IF_SVE_BQ_2B],
            IF_SVE_2AI => [IF_SVE_AM_2A, IF_SVE_AA_3A],
            IF_SVE_2AJ => [IF_SVE_HI_3A, IF_SVE_HT_4A],
            IF_SVE_2AK => [IF_SVE_BZ_3A, IF_SVE_BZ_3A_A],
            IF_SVE_2AL => [IF_SVE_GG_3A, IF_SVE_GG_3B],
            IF_SVE_2AM => [IF_SVE_HL_3A, IF_SVE_HM_2A],
            IF_SVE_2AN => [IF_SVE_EI_3A, IF_SVE_EZ_3A],
            IF_SVE_2AO => [IF_SVE_GT_4A, IF_SVE_GV_3A],
            IF_SVE_2AP => [IF_SVE_GY_3B, IF_SVE_HA_3A],
            IF_SVE_2AQ => [IF_SVE_GO_3A, IF_SVE_HC_3A],
            IF_SVE_2AR => [IF_SVE_AP_3A, IF_SVE_CZ_4A],
            IF_SVE_2AT => [IF_SVE_AA_3A, IF_SVE_EC_1A],
            IF_SVE_2AU => [IF_SVE_AH_3A, IF_SVE_BI_2A],
            IF_SVE_2AV => [IF_SVE_BM_1A, IF_SVE_BN_1A],
            IF_SVE_2AW => [IF_SVE_BO_1A, IF_SVE_BP_1A],
            IF_SVE_2AX => [IF_SVE_CC_2A, IF_SVE_CD_2A],
            IF_SVE_2AY => [IF_SVE_CR_3A, IF_SVE_CS_3A],
            IF_SVE_2AZ => [IF_SVE_CV_3A, IF_SVE_CV_3B],
            IF_SVE_2BA => [IF_SVE_CW_4A, IF_SVE_CZ_4A],
            IF_SVE_2BB => [IF_SVE_CZ_4A, IF_SVE_CZ_4A_A],
            IF_SVE_2BC => [IF_SVE_DE_1A, IF_SVE_DZ_1A],
            IF_SVE_2BD => [IF_SVE_DG_2A, IF_SVE_DH_1A],
            IF_SVE_2BE => [IF_SVE_DK_3A, IF_SVE_DL_2A],
            IF_SVE_2BF => [IF_SVE_DM_2A, IF_SVE_DN_2A],
            IF_SVE_2BG => [IF_SVE_DO_2A, IF_SVE_DP_2A],
            IF_SVE_2BH => [IF_SVE_DW_2A, IF_SVE_DW_2B],
            IF_SVE_2BI => [IF_SVE_FL_3A, IF_SVE_FN_3B],
            IF_SVE_2BJ => [IF_SVE_GQ_3A, IF_SVE_HG_2A],
            IF_SVE_2BK => [IF_SVE_GU_3C, IF_SVE_HU_4B],
            IF_SVE_2BL => [IF_SVE_GZ_3A, IF_SVE_HB_3A],
            IF_SVE_2BM => [IF_SVE_HK_3B, IF_SVE_HL_3B],
            IF_SVE_2BN => [IF_SVE_IF_4A, IF_SVE_IF_4A_A],
            IF_SVE_2BO => [IF_SVE_IO_3A, IF_SVE_IP_4A],
            IF_SVE_2BP => [IF_SVE_IQ_3A, IF_SVE_IR_4A],
            IF_SVE_2BQ => [IF_SVE_IS_3A, IF_SVE_IT_4A],
            IF_SVE_2BR => [IF_SVE_JC_4A, IF_SVE_JO_3A],
            IF_SVE_2BS => [IF_SVE_JE_3A, IF_SVE_JF_4A],
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
                assert(sve_ins_offset < (uint)insCodes1.Length, "sve_ins_offset < ArrLen(insCodes1)");
                code = insCodes1[unchecked((int)sve_ins_offset)];
                break;
            }

            case 1:
            {
                assert(sve_ins_offset < (uint)insCodes2.Length, "sve_ins_offset < ArrLen(insCodes2)");
                code = insCodes2[unchecked((int)sve_ins_offset)];
                break;
            }

            case 2:
            {
                assert(sve_ins_offset < (uint)insCodes3.Length, "sve_ins_offset < ArrLen(insCodes3)");
                code = insCodes3[unchecked((int)sve_ins_offset)];
                break;
            }

            case 3:
            {
                assert(sve_ins_offset < (uint)insCodes4.Length, "sve_ins_offset < ArrLen(insCodes4)");
                code = insCodes4[unchecked((int)sve_ins_offset)];
                break;
            }

            case 4:
            {
                assert(sve_ins_offset < (uint)insCodes5.Length, "sve_ins_offset < ArrLen(insCodes5)");
                code = insCodes5[unchecked((int)sve_ins_offset)];
                break;
            }

            case 5:
            {
                assert(sve_ins_offset < (uint)insCodes6.Length, "sve_ins_offset < ArrLen(insCodes6)");
                code = insCodes6[unchecked((int)sve_ins_offset)];
                break;
            }

            case 6:
            {
                assert(sve_ins_offset < (uint)insCodes7.Length, "sve_ins_offset < ArrLen(insCodes7)");
                code = insCodes7[unchecked((int)sve_ins_offset)];
                break;
            }

            case 7:
            {
                assert(sve_ins_offset < (uint)insCodes8.Length, "sve_ins_offset < ArrLen(insCodes8)");
                code = insCodes8[unchecked((int)sve_ins_offset)];
                break;
            }

            case 8:
            {
                assert(sve_ins_offset < (uint)insCodes9.Length, "sve_ins_offset < ArrLen(insCodes9)");
                code = insCodes9[unchecked((int)sve_ins_offset)];
                break;
            }

            case 9:
            {
                assert(sve_ins_offset < (uint)insCodes10.Length, "sve_ins_offset < ArrLen(insCodes10)");
                code = insCodes10[unchecked((int)sve_ins_offset)];
                break;
            }

            case 10:
            {
                assert(sve_ins_offset < (uint)insCodes11.Length, "sve_ins_offset < ArrLen(insCodes11)");
                code = insCodes11[unchecked((int)sve_ins_offset)];
                break;
            }

            case 11:
            {
                assert(sve_ins_offset < (uint)insCodes12.Length, "sve_ins_offset < ArrLen(insCodes12)");
                code = insCodes12[unchecked((int)sve_ins_offset)];
                break;
            }

            case 12:
            {
                assert(sve_ins_offset < (uint)insCodes13.Length, "sve_ins_offset < ArrLen(insCodes13)");
                code = insCodes13[unchecked((int)sve_ins_offset)];
                break;
            }
        }

        assert(code != BAD_CODE, "(code != BAD_CODE)");

        return code;
    }
}
#endif
