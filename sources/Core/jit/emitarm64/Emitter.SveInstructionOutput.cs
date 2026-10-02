// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Numerics;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe byte* emitOutput_InstrSve(byte* dst, instrDesc id)
    {
        unchecked
        {
            uint code;
            var ins = id.idIns();
            var fmt = id.idInsFmt();
            nint imm;

            switch (fmt)
            {
                // Scalable: element size, predicate, and two vector registers.
                case IF_SVE_AA_3A:
                case IF_SVE_AC_3A:
                case IF_SVE_AF_3A:
                case IF_SVE_AG_3A:
                case IF_SVE_AI_3A:
                case IF_SVE_AJ_3A:
                case IF_SVE_AK_3A:
                case IF_SVE_AL_3A:
                case IF_SVE_AO_3A:
                case IF_SVE_AP_3A:
                case IF_SVE_AQ_3A:
                case IF_SVE_CL_3A:
                case IF_SVE_CM_3A:
                case IF_SVE_CN_3A:
                case IF_SVE_CP_3A:
                case IF_SVE_CR_3A:
                case IF_SVE_CU_3A:
                case IF_SVE_EQ_3A:
                case IF_SVE_ES_3A:
                case IF_SVE_GR_3A:
                case IF_SVE_GS_3A:
                case IF_SVE_HE_3A:
                case IF_SVE_HJ_3A:
                case IF_SVE_HL_3A:
                case IF_SVE_HQ_3A:
                case IF_SVE_HR_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AB_3B:
                case IF_SVE_HL_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AH_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodePredQualifier_16(id.idPredicateReg2Merge());
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AM_2A:
                {
                    var isRightShift = emitInsIsVectorRightShift(ins);
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeSveShift_23_to_22_9_to_0(
                        optGetSveElemsize(id.idInsOpt()), isRightShift, (nuint)imm);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                // Four registers: the fourth source occupies the m field.
                case IF_SVE_AR_4A:
                case IF_SVE_GI_4A:
                case IF_SVE_HU_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                // The multiplicand-writing form puts the fourth source in the a field.
                case IF_SVE_AS_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeReg_V(id.idReg4(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AT_3A:
                case IF_SVE_BG_3A:
                case IF_SVE_BZ_3A:
                case IF_SVE_BZ_3A_A:
                case IF_SVE_EH_3A:
                case IF_SVE_EL_3A:
                case IF_SVE_EM_3A:
                case IF_SVE_EX_3A:
                case IF_SVE_FL_3A:
                case IF_SVE_FM_3A:
                case IF_SVE_FW_3A:
                case IF_SVE_GF_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GC_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeNarrowingSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BA_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BH_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeUimm(emitGetInsSC(id), 11, 10);
                    code |= insEncodeUimm(id.idInsOpt() == INS_OPTS_SCALABLE_D ? 1 : 0, 22, 22);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BH_3B:
                case IF_SVE_BH_3B_A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeUimm(emitGetInsSC(id), 11, 10);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BL_1A:
                case IF_SVE_BM_1A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeSvePattern(id.idSvePattern());
                    code |= insEncodeUimm(imm - 1, 19, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BO_1A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeSvePattern(id.idSvePattern());
                    code |= insEncodeUimm(imm - 1, 19, 16);
                    code |= insEncodeSveElemsize_sz_20(id.idOpSize());
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BQ_2A:
                case IF_SVE_BQ_2B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(imm & 0b111, 12, 10);
                    code |= insEncodeUimm(imm >> 3, 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BN_1A:
                case IF_SVE_BP_1A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeSvePattern(id.idSvePattern());
                    code |= insEncodeUimm(imm - 1, 19, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BS_1A:
                case IF_SVE_BT_1A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= (uint)(imm << 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BU_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeImm8_12_to_5(imm);
                    code |= insEncodeReg_P(id.idReg2(), 19, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BV_2A:
                case IF_SVE_BV_2A_J:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 19, 16);
                    code |= insEncodeImm8_12_to_5(imm);
                    code |= id.idHasShift() ? 0x2000u : 0;
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BV_2B:
                {
                    // MOV is the preferred disassembly, but only FMOV owns this encoding.
                    code = emitInsCodeSve(INS_sve_fmov, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 19, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BW_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSveBroadcastIndex(optGetSveElemsize(id.idInsOpt()), imm);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CE_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CE_2B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSplitUimm(emitGetInsSC(id), 22, 22, 18, 17);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CE_2C:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 17, 17);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CE_2D:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 18, 17);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CF_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CF_2B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeSplitUimm(emitGetInsSC(id), 22, 22, 18, 17);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CF_2C:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 17, 17);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CF_2D:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 18, 17);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CC_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CD_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CI_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeReg_P(id.idReg3(), 19, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CJ_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CK_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GQ_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    if (ins == INS_sve_fcvtnt && id.idInsOpt() == INS_OPTS_D_TO_S)
                    {
                        code |= (1u << 22) | (1u << 17);
                    }
                    else if (ins == INS_sve_fcvtlt && id.idInsOpt() == INS_OPTS_S_TO_D)
                    {
                        code |= (1u << 22) | (1u << 17);
                    }

                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CO_3A:
                case IF_SVE_CS_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CQ_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CT_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CV_3A:
                case IF_SVE_CV_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CW_4A:
                {
                    var reg4 = ins == INS_sve_mov ? id.idReg1() : id.idReg4();
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(reg4, 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CX_4A:
                case IF_SVE_CX_4A_A:
                case IF_SVE_GE_4A:
                case IF_SVE_HT_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CY_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSimm(imm, 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CY_3B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeUimm(imm, 20, 14);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EW_3A:
                case IF_SVE_BR_3B:
                case IF_SVE_FN_3B:
                case IF_SVE_FO_3A:
                case IF_SVE_AT_3B:
                case IF_SVE_BD_3B:
                case IF_SVE_EF_3A:
                case IF_SVE_EI_3A:
                case IF_SVE_GJ_3A:
                case IF_SVE_GN_3A:
                case IF_SVE_GO_3A:
                case IF_SVE_GW_3B:
                case IF_SVE_HA_3A:
                case IF_SVE_HA_3A_E:
                case IF_SVE_HA_3A_F:
                case IF_SVE_HB_3A:
                case IF_SVE_HD_3A:
                case IF_SVE_HD_3A_A:
                case IF_SVE_HK_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AU_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    if (id.idIns() != INS_sve_mov)
                    {
                        code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    }
                    else
                    {
                        code |= insEncodeReg_V(id.idReg2(), 20, 16);
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AV_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 20, 16);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AW_2A:
                {
                    imm = insSveGetImmDiff(emitGetInsSC(id), id.idInsOpt());
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(imm & 0b11111, 20, 16);
                    code |= insEncodeUimm(imm >> 5, 22, 22);
                    code |= insEncodeSveElemsize_tszh_23_tszl_20_to_19(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AX_1A:
                {
                    nint imm1;
                    nint imm2;
                    insSveDecodeTwoSimm5(emitGetInsSC(id), &imm1, &imm2);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeSimm(imm1, 9, 5);
                    code |= insEncodeSimm(imm2, 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AY_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeSimm(emitGetInsSC(id), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_AZ_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeSimm(emitGetInsSC(id), 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BB_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeSimm(emitGetInsSC(id), 10, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BC_1A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeSimm(emitGetInsSC(id), 10, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EW_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg2(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EG_3A:
                case IF_SVE_EY_3A:
                case IF_SVE_EZ_3A:
                case IF_SVE_FD_3B:
                case IF_SVE_FF_3B:
                case IF_SVE_FI_3B:
                case IF_SVE_GU_3A:
                case IF_SVE_GX_3A:
                case IF_SVE_GY_3B:
                case IF_SVE_GY_3B_D:
                case IF_SVE_FK_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 18, 16);
                    code |= insEncodeUimm(emitGetInsSC(id), 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FD_3A:
                case IF_SVE_FF_3A:
                case IF_SVE_FI_3A:
                case IF_SVE_GU_3C:
                case IF_SVE_GX_3C:
                case IF_SVE_FK_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 18, 16);
                    code |= insEncodeUimm(imm & 0b11, 20, 19);
                    code |= insEncodeUimm(imm >> 2, 22, 22);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FE_3A:
                case IF_SVE_FG_3A:
                case IF_SVE_FH_3A:
                case IF_SVE_FJ_3A:
                case IF_SVE_GY_3A:
                case IF_SVE_GZ_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(imm & 1, 11, 11);
                    code |= insEncodeReg_V(id.idReg3(), 18, 16);
                    code |= insEncodeUimm(imm >> 1, 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FE_3B:
                case IF_SVE_FG_3B:
                case IF_SVE_FH_3B:
                case IF_SVE_FJ_3B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(imm & 1, 11, 11);
                    code |= insEncodeReg_V(id.idReg3(), 19, 16);
                    code |= insEncodeUimm(imm & 0b10, 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EY_3B:
                case IF_SVE_FD_3C:
                case IF_SVE_FF_3C:
                case IF_SVE_FI_3C:
                case IF_SVE_GU_3B:
                case IF_SVE_GX_3B:
                case IF_SVE_FK_3C:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 19, 16);
                    // The index occupies bit 20, leaving the register bit at 19 intact.
                    code |= insEncodeUimm(emitGetInsSC(id) << 1, 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CZ_4A:
                case IF_SVE_DA_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg3(), 8, 5);

                    regNumber regm;
                    switch (ins)
                    {
                        case INS_sve_mov:
                        case INS_sve_movs:
                        {
                            regm = id.idReg3();
                            break;
                        }

                        case INS_sve_not:
                        case INS_sve_nots:
                        {
                            regm = id.idReg2();
                            break;
                        }

                        default:
                        {
                            regm = id.idReg4();
                            break;
                        }
                    }

                    code |= insEncodeReg_P(regm, 19, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CZ_4A_A:
                case IF_SVE_CZ_4A_L:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeReg_P(id.idReg2(), 19, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CZ_4A_K:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg3(), 8, 5);
                    code |= insEncodeReg_P(id.idReg1(), 19, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DB_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg3(), 8, 5);
                    code |= insEncodePredQualifier_4(id.idPredicateReg2Merge());
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DB_3B:
                case IF_SVE_DC_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg3(), 8, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DD_2A:
                case IF_SVE_DG_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DE_1A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeSvePattern(id.idSvePattern());
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DF_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DH_1A:
                case IF_SVE_DJ_1A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DI_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 13, 10);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DK_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg3(), 8, 5);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GA_2A:
                {
                    imm = emitGetInsSC(id);
                    assert(id.idInsOpt() == INS_OPTS_SCALABLE_H);
                    assert(emitInsIsVectorRightShift(id.idIns()));
                    assert(isValidVectorShiftAmount(imm, EA_4BYTE, true));
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeVectorShift(EA_4BYTE, true, imm);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V_9_to_6_Times_Two(id.idReg2());
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DL_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeVectorLengthSpecifier(id);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DM_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DN_2A:
                case IF_SVE_DP_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DO_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 8, 5);
                    code |= insEncodeVLSElemsize(id.idOpSize());
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DQ_0A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DR_1A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 8, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DS_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_R(id.idReg1(), 9, 5);
                    code |= insEncodeReg_R(id.idReg2(), 20, 16);
                    code |= insEncodeSveElemsize_R_22(id.idOpSize());
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FZ_2A:
                case IF_SVE_HG_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V_9_to_6_Times_Two(id.idReg2());
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GD_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    // Wide options leave bit 23 clear in the split element-size encoding.
                    assert(insOptsScalableWide(id.idInsOpt()));
                    code |= insEncodeSveElemsize_tszh_23_tszl_20_to_19(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FR_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 20, 16);
                    assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                    code |= insEncodeSplitUimm((nint)optGetSveElemsize(id.idInsOpt()) / 2, 22, 22, 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GB_2A:
                {
                    // Neither the element size nor the immediate difference may use D width.
                    assert(insOptsScalableWide(id.idInsOpt()));
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(insSveGetImmDiff(emitGetInsSC(id), id.idInsOpt()), 20, 16);
                    code |= insEncodeSveElemsize_tszh_23_tszl_20_to_19(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FV_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 10, 10);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FY_3A:
                {
                    nint sizeEncoding = id.idInsOpt() == INS_OPTS_SCALABLE_D ? 1 : 0;
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeUimm(sizeEncoding, 22, 22);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GK_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GL_1A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DT_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= id.idOpSize() == EA_8BYTE ? 1u << 12 : 0;
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DW_2A:
                case IF_SVE_DW_2B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 7, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 9, 8);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DX_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 1);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DY_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeVectorLengthSpecifier(id);
                    code |= insEncodeReg_P(id.idReg1(), 2, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DZ_1A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 2, 0);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EA_1A:
                case IF_SVE_ED_1A:
                case IF_SVE_EE_1A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeImm8_12_to_5(imm);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FA_3A:
                case IF_SVE_FB_3A:
                case IF_SVE_FC_3A:
                {
                    var packedImm = emitGetInsSC(id);
                    var rot = packedImm & 0b11;
                    var index = packedImm >> 2;
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(rot, 11, 10);
                    code |= insEncodeReg_V(id.idReg3(), 18, 16);
                    code |= insEncodeUimm(index, 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EJ_3A:
                case IF_SVE_EK_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(emitGetInsSC(id), 11, 10);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_FA_3B:
                case IF_SVE_FB_3B:
                case IF_SVE_FC_3B:
                case IF_SVE_GV_3A:
                {
                    var packedImm = emitGetInsSC(id);
                    var rot = packedImm & 0b11;
                    var index = packedImm >> 2;
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 19, 16);
                    code |= insEncodeUimm(rot, 11, 10);
                    // The index occupies bit 20, leaving the register bit at 19 intact.
                    code |= insEncodeUimm(index << 1, 20, 19);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EB_1A:
                case IF_SVE_EC_1A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    code |= insEncodeImm8_12_to_5(imm);
                    code |= id.idHasShift() ? 0x2000u : 0;
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_EB_1B:
                {
                    // MOV is the preferred disassembly, but only FMOV owns this encoding.
                    code = emitInsCodeSve(INS_sve_fmov, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DU_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_DV_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 13, 10);
                    code |= insEncodeReg_P(id.idReg3(), 8, 5);
                    code |= insEncodeReg_R(id.idReg4(), 17, 16);
                    code |= insEncodeSveElemsize_tszh_tszl_and_imm(id.idInsOpt(), emitGetInsSC(id));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HO_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HO_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    switch (id.idInsOpt())
                    {
                        case INS_OPTS_H_TO_S:
                        {
                            code |= 1u << 16;
                            break;
                        }

                        case INS_OPTS_H_TO_D:
                        {
                            code |= (1u << 22) | (1u << 16);
                            break;
                        }

                        case INS_OPTS_S_TO_H:
                        {
                            break;
                        }

                        case INS_OPTS_S_TO_D:
                        {
                            code |= (1u << 22) | (3u << 16);
                            break;
                        }

                        case INS_OPTS_D_TO_H:
                        {
                            code |= 1u << 22;
                            break;
                        }

                        case INS_OPTS_D_TO_S:
                        {
                            code |= (1u << 22) | (1u << 17);
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HO_3C:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HP_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);

                    switch (id.idInsOpt())
                    {
                        case INS_OPTS_SCALABLE_H:
                        {
                            code |= (1u << 22) | (1u << 17);
                            break;
                        }

                        case INS_OPTS_H_TO_S:
                        {
                            code |= (1u << 22) | (1u << 18);
                            break;
                        }

                        case INS_OPTS_H_TO_D:
                        {
                            code |= (1u << 22) | (3u << 17);
                            break;
                        }

                        case INS_OPTS_SCALABLE_S:
                        {
                            code |= (1u << 23) | (1u << 18);
                            break;
                        }

                        case INS_OPTS_S_TO_D:
                        {
                            code |= (3u << 22) | (1u << 18);
                            break;
                        }

                        case INS_OPTS_D_TO_S:
                        {
                            code |= 3u << 22;
                            break;
                        }

                        case INS_OPTS_SCALABLE_D:
                        {
                            code |= (3u << 22) | (3u << 17);
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HS_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);

                    switch (id.idInsOpt())
                    {
                        case INS_OPTS_SCALABLE_H:
                        {
                            code |= (1u << 22) | (1u << 17);
                            break;
                        }

                        case INS_OPTS_S_TO_H:
                        {
                            code |= (1u << 22) | (1u << 18);
                            break;
                        }

                        case INS_OPTS_SCALABLE_S:
                        {
                            code |= (1u << 23) | (1u << 18);
                            break;
                        }

                        case INS_OPTS_S_TO_D:
                        {
                            code |= (1u << 23) | (1u << 22);
                            break;
                        }

                        case INS_OPTS_D_TO_H:
                        {
                            code |= (1u << 22) | (3u << 17);
                            break;
                        }

                        case INS_OPTS_D_TO_S:
                        {
                            code |= (3u << 22) | (1u << 18);
                            break;
                        }

                        case INS_OPTS_SCALABLE_D:
                        {
                            code |= (3u << 22) | (3u << 17);
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IH_3A:
                case IF_SVE_IH_3A_A:
                case IF_SVE_IH_3A_F:
                case IF_SVE_IJ_3A:
                case IF_SVE_IJ_3A_D:
                case IF_SVE_IJ_3A_E:
                case IF_SVE_IJ_3A_F:
                case IF_SVE_IJ_3A_G:
                case IF_SVE_IL_3A:
                case IF_SVE_IL_3A_A:
                case IF_SVE_IL_3A_B:
                case IF_SVE_IL_3A_C:
                case IF_SVE_IM_3A:
                case IF_SVE_IO_3A:
                case IF_SVE_IQ_3A:
                case IF_SVE_IS_3A:
                case IF_SVE_JE_3A:
                case IF_SVE_JM_3A:
                case IF_SVE_JN_3C:
                case IF_SVE_JN_3C_D:
                case IF_SVE_JO_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);

                    switch (ins)
                    {
                        case INS_sve_ld2b:
                        case INS_sve_ld2h:
                        case INS_sve_ld2w:
                        case INS_sve_ld2d:
                        case INS_sve_ld2q:
                        case INS_sve_st2b:
                        case INS_sve_st2h:
                        case INS_sve_st2w:
                        case INS_sve_st2d:
                        case INS_sve_st2q:
                        {
                            code |= insEncodeSimm_MultipleOf(imm, 19, 16, 2);
                            break;
                        }

                        case INS_sve_ld3b:
                        case INS_sve_ld3h:
                        case INS_sve_ld3w:
                        case INS_sve_ld3d:
                        case INS_sve_ld3q:
                        case INS_sve_st3b:
                        case INS_sve_st3h:
                        case INS_sve_st3w:
                        case INS_sve_st3d:
                        case INS_sve_st3q:
                        {
                            code |= insEncodeSimm_MultipleOf(imm, 19, 16, 3);
                            break;
                        }

                        case INS_sve_ld4b:
                        case INS_sve_ld4h:
                        case INS_sve_ld4w:
                        case INS_sve_ld4d:
                        case INS_sve_ld4q:
                        case INS_sve_st4b:
                        case INS_sve_st4h:
                        case INS_sve_st4w:
                        case INS_sve_st4d:
                        case INS_sve_st4q:
                        {
                            code |= insEncodeSimm_MultipleOf(imm, 19, 16, 4);
                            break;
                        }

                        case INS_sve_ld1rqb:
                        case INS_sve_ld1rqd:
                        case INS_sve_ld1rqh:
                        case INS_sve_ld1rqw:
                        {
                            code |= insEncodeSimm_MultipleOf(imm, 19, 16, 16);
                            break;
                        }

                        case INS_sve_ld1rob:
                        case INS_sve_ld1rod:
                        case INS_sve_ld1roh:
                        case INS_sve_ld1row:
                        {
                            code |= insEncodeSimm_MultipleOf(imm, 19, 16, 32);
                            break;
                        }

                        default:
                        {
                            code |= insEncodeSimm(imm, 19, 16);
                            break;
                        }
                    }

                    if (canEncodeSveElemsize_dtype(ins))
                    {
                        if (ins == INS_sve_ld1w)
                        {
                            code = insEncodeSveElemsize_dtype_ld1w(ins, fmt, optGetSveElemsize(id.idInsOpt()), code);
                        }
                        else
                        {
                            code = insEncodeSveElemsize_dtype(ins, optGetSveElemsize(id.idInsOpt()), code);
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JD_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_R(id.idReg4(), 20, 16);
                    code |= insEncodeSveElemsize_22_to_21(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JD_4B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_R(id.idReg4(), 20, 16);
                    code |= insEncodeSveElemsize_sz_21(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JJ_4A:
                case IF_SVE_JJ_4A_B:
                case IF_SVE_JJ_4A_C:
                case IF_SVE_JJ_4A_D:
                case IF_SVE_JK_4A:
                case IF_SVE_JK_4A_B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    switch (id.idInsOpt())
                    {
                        case INS_OPTS_SCALABLE_S_SXTW:
                        case INS_OPTS_SCALABLE_D_SXTW:
                        {
                            code |= 1u << 14;
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JN_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeSimm(imm, 19, 16);
                    code |= insEncodeSveElemsize_22_to_21(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JN_3B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeSimm(imm, 19, 16);
                    code |= insEncodeSveElemsize_sz_21(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HW_4A:
                case IF_SVE_HW_4A_A:
                case IF_SVE_HW_4A_B:
                case IF_SVE_HW_4A_C:
                case IF_SVE_IU_4A:
                case IF_SVE_IU_4A_A:
                case IF_SVE_IU_4A_C:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    switch (id.idInsOpt())
                    {
                        case INS_OPTS_SCALABLE_S_SXTW:
                        case INS_OPTS_SCALABLE_D_SXTW:
                        {
                            code |= 1u << 22;
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HW_4B:
                case IF_SVE_HW_4B_D:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IF_4A:
                case IF_SVE_IF_4A_A:
                case IF_SVE_IW_4A:
                case IF_SVE_IX_4A:
                case IF_SVE_IY_4A:
                case IF_SVE_IZ_4A:
                case IF_SVE_IZ_4A_A:
                case IF_SVE_JA_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_R(id.idReg4(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IG_4A_D:
                case IF_SVE_IG_4A_E:
                case IF_SVE_IG_4A_F:
                case IF_SVE_IG_4A_G:
                case IF_SVE_II_4A_H:
                case IF_SVE_IK_4A_F:
                case IF_SVE_IK_4A_G:
                case IF_SVE_IK_4A_H:
                case IF_SVE_IK_4A_I:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_R(id.idReg4(), 20, 16);
                    if (canEncodeSveElemsize_dtype(ins))
                    {
                        if (ins == INS_sve_ld1w)
                        {
                            code = insEncodeSveElemsize_dtype_ld1w(ins, fmt, optGetSveElemsize(id.idInsOpt()), code);
                        }
                        else
                        {
                            code = insEncodeSveElemsize_dtype(ins, optGetSveElemsize(id.idInsOpt()), code);
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IG_4A:
                case IF_SVE_II_4A:
                case IF_SVE_II_4A_B:
                case IF_SVE_IK_4A:
                case IF_SVE_IN_4A:
                case IF_SVE_IP_4A:
                case IF_SVE_IR_4A:
                case IF_SVE_IT_4A:
                case IF_SVE_JB_4A:
                case IF_SVE_JC_4A:
                case IF_SVE_JD_4C:
                case IF_SVE_JD_4C_A:
                case IF_SVE_JF_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_R(id.idReg4(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IU_4B:
                case IF_SVE_IU_4B_B:
                case IF_SVE_IU_4B_D:
                case IF_SVE_JJ_4B:
                case IF_SVE_JJ_4B_C:
                case IF_SVE_JJ_4B_E:
                case IF_SVE_JK_4B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GP_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveImm90_or_270_rot(imm);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GT_4A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    code |= insEncodeSveImm0_to_270_rot(imm);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HI_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HM_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeSveSmallFloatImm(imm);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HN_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(imm, 18, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HP_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize_18_to_17(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HU_4B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HV_4A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeReg_V(id.idReg4(), 20, 16);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_ID_2A:
                case IF_SVE_JG_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 3, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeSimm9h9l_21_to_16_and_12_to_10(imm);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IE_2A:
                case IF_SVE_JH_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeSimm9h9l_21_to_16_and_12_to_10(imm);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GG_3A:
                case IF_SVE_GH_3B:
                case IF_SVE_GH_3B_B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeUimm(imm, 23, 22);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GG_3B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeUimm3h3l_23_to_22_and_12(imm);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_GH_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= insEncodeUimm(imm, 23, 23);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HY_3A:
                case IF_SVE_HY_3A_A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 12, 10);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= (uint)id.idSvePrfop();
                    switch (id.idInsOpt())
                    {
                        case INS_OPTS_SCALABLE_S_SXTW:
                        case INS_OPTS_SCALABLE_D_SXTW:
                        {
                            code |= 1u << 22;
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HY_3B:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 12, 10);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_V(id.idReg3(), 20, 16);
                    code |= (uint)id.idSvePrfop();
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IB_3A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 12, 10);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeReg_R(id.idReg3(), 20, 16);
                    code |= (uint)id.idSvePrfop();
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HZ_2A_B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 12, 10);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= (uint)id.idSvePrfop();

                    if (id.idInsOpt() == INS_OPTS_SCALABLE_D)
                    {
                        code |= 1u << 30;
                    }

                    switch (ins)
                    {
                        case INS_sve_prfh:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 2);
                            break;
                        }

                        case INS_sve_prfw:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 4);
                            break;
                        }

                        case INS_sve_prfd:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 8);
                            break;
                        }

                        default:
                        {
                            // Native PRFB has no immediate encoding in this branch.
                            assert(ins == INS_sve_prfb);
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HX_3A_B:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeUimm(imm, 20, 16);
                    code |= insEncodeSveElemsize_30_or_21(fmt, optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_HX_3A_E:
                case IF_SVE_IV_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize_30_or_21(fmt, optGetSveElemsize(id.idInsOpt()));

                    switch (ins)
                    {
                        case INS_sve_ld1d:
                        case INS_sve_ldff1d:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 8);
                            break;
                        }

                        case INS_sve_ld1w:
                        case INS_sve_ld1sw:
                        case INS_sve_ldff1w:
                        case INS_sve_ldff1sw:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 4);
                            break;
                        }

                        default:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 2);
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JL_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeUimm_MultipleOf(imm, 20, 16, 8);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_JI_3A_A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_V(id.idReg3(), 9, 5);
                    code |= insEncodeSveElemsize_30_or_21(fmt, optGetSveElemsize(id.idInsOpt()));
                    switch (ins)
                    {
                        case INS_sve_st1h:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 2);
                            break;
                        }

                        case INS_sve_st1w:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 20, 16, 4);
                            break;
                        }

                        default:
                        {
                            assert(ins == INS_sve_st1b);
                            code |= insEncodeUimm(imm, 20, 16);
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IA_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_P(id.idReg1(), 12, 10);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= (uint)id.idSvePrfop();
                    code |= insEncodeSimm(imm, 21, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IC_3A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    switch (ins)
                    {
                        case INS_sve_ld1rd:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 21, 16, 8);
                            break;
                        }

                        default:
                        {
                            assert(ins == INS_sve_ld1rsw);
                            code |= insEncodeUimm_MultipleOf(imm, 21, 16, 4);
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_IC_3A_A:
                case IF_SVE_IC_3A_B:
                case IF_SVE_IC_3A_C:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_P(id.idReg2(), 12, 10);
                    code |= insEncodeReg_R(id.idReg3(), 9, 5);
                    code = insEncodeSveElemsize_dtypeh_dtypel(ins, fmt, optGetSveElemsize(id.idInsOpt()), code);
                    switch (ins)
                    {
                        case INS_sve_ld1rw:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 21, 16, 4);
                            break;
                        }

                        case INS_sve_ld1rh:
                        case INS_sve_ld1rsh:
                        {
                            code |= insEncodeUimm_MultipleOf(imm, 21, 16, 2);
                            break;
                        }

                        default:
                        {
                            code |= insEncodeUimm(imm, 21, 16);
                            break;
                        }
                    }

                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BI_2A:
                case IF_SVE_HH_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CB_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_R(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BJ_2A:
                case IF_SVE_CG_2A:
                case IF_SVE_HF_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize(id.idInsOpt()));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_CH_2A:
                {
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsize(optGetSveElemsize((insOpts)((uint)id.idInsOpt() + 1)));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BF_2A:
                case IF_SVE_FT_2A:
                case IF_SVE_FU_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsizeWithShift_tszh_tszl_imm3(
                        id.idInsOpt(), imm, emitInsIsVectorRightShift(ins));
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BX_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeSveElemsizeWithImmediate_i1_tsz(id.idInsOpt(), imm);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                case IF_SVE_BY_2A:
                {
                    imm = emitGetInsSC(id);
                    code = emitInsCodeSve(ins, fmt);
                    code |= insEncodeReg_V(id.idReg1(), 4, 0);
                    code |= insEncodeReg_V(id.idReg2(), 9, 5);
                    code |= insEncodeUimm(imm, 19, 16);
                    dst += emitOutputLong(dst, code);
                    break;
                }

                default:
                {
                    assert(false, "!\"Unexpected format\"");
                    break;
                }
            }

            return dst;
        }
    }

    // Native template bounds are constants at every callsite. Their static assertions
    // constrain field positions, not the runtime immediate/register assertions below.
    private static uint insEncodeReg_V(regNumber reg, int hi, int lo)
    {
        assert(isVectorRegister(reg));
        var ureg = unchecked((uint)reg - (uint)REG_V0);
        var bits = hi - lo + 1;
        var mask = (1u << bits) - 1;

        return (ureg & mask) << lo;
    }

    private static uint insEncodeReg_P(regNumber reg, int hi, int lo)
    {
        assert(isPredicateRegister(reg));
        var ureg = unchecked((uint)reg - (uint)REG_P0);
        var bits = hi - lo + 1;
        var mask = (1u << bits) - 1;

        return (ureg & mask) << lo;
    }

    private static uint insEncodeReg_R(regNumber reg, int hi, int lo)
    {
        assert(isIntegerRegister(reg));
        var ureg = unchecked((uint)reg);
        var bits = hi - lo + 1;
        var mask = (1u << bits) - 1;

        return (ureg & mask) << lo;
    }

    private static uint insEncodeUimm(nint imm, int hi, int lo)
    {
        // The native size_t parameter receives signed immediates by modulo conversion.
        var uimm = unchecked((nuint)imm);
        var imm_bits = hi - lo + 1;
        var imm_max = (nuint)1 << imm_bits;
        assert(uimm < imm_max, "imm < imm_max");
        var result = unchecked((uint)(uimm << lo));
        assert((nuint)(result >> lo) == uimm, "(result >> lo) == imm");

        return result;
    }

    private static uint insEncodeSplitUimm(nint imm, int hi1, int lo1, int hi2, int lo2)
    {
        var uimm = unchecked((nuint)imm);
        var hi_bits = hi1 - lo1 + 1;
        var lo_bits = hi2 - lo2 + 1;
        var imm_max = (nuint)1 << (hi_bits + lo_bits);
        assert(uimm < imm_max, "imm < imm_max");
        var hi_max = (nuint)1 << hi_bits;
        var lo_max = (nuint)1 << lo_bits;
        var immhi = (uimm >> lo_bits) & (hi_max - 1);
        var immlo = uimm & (lo_max - 1);
        var result = insEncodeUimm(unchecked((nint)immhi), hi1, lo1)
            | insEncodeUimm(unchecked((nint)immlo), hi2, lo2);

        var between_bits = lo1 - hi2 - 1;
        var between_mask = ((1u << between_bits) - 1) << (hi2 + 1);
        assert((result & between_mask) == 0);

        return result;
    }

    private static uint insEncodeSimm(nint imm, int hi, int lo)
    {
        var imm_bits = hi - lo + 1;
        var imm_max = (nint)1 << (imm_bits - 1);
        var imm_min = -imm_max;
        assert(imm_min <= imm && imm < imm_max);
        var result = unchecked((uint)((nuint)imm & (((nuint)1 << imm_bits) - 1)));

        return result << lo;
    }

    private static uint insEncodeUimm_MultipleOf(nint imm, int hi, int lo, nint mul)
    {
        var bits = hi - lo + 1;
        assert(isValidUimm_MultipleOf(imm, bits, unchecked((nuint)mul)));

        return insEncodeUimm(imm / mul, hi, lo);
    }

    private static uint insEncodeSimm_MultipleOf(nint imm, int hi, int lo, nint mul)
    {
        var bits = hi - lo + 1;
        assert(isValidSimm_MultipleOf(imm, bits, mul));

        return insEncodeSimm(imm / mul, hi, lo);
    }

    private static uint insEncodeReg_V_9_to_6_Times_Two(regNumber reg)
    {
        assert(isVectorRegister(reg));
        var ureg = unchecked((uint)reg - (uint)REG_V0);
        assert(ureg % 2 == 0);
        ureg /= 2;
        assert((ureg >= 0) && (ureg <= 31));

        return ureg << 6;
    }

    private static uint insEncodeShiftImmediate(emitAttr size, bool isRightShift, nint shiftAmount)
    {
        if (isRightShift)
        {
            assert((shiftAmount > 0) && (shiftAmount <= (nint)getBitWidth(size)),
                "(shiftAmount > 0) && (shiftAmount <= getBitWidth(size))");
            return unchecked((uint)((nint)(2 * getBitWidth(size)) - shiftAmount));
        }
        else
        {
            // Native does not assert a nonnegative left shift here.
            assert(shiftAmount < (nint)getBitWidth(size), "shiftAmount < getBitWidth(size)");
            return unchecked((uint)((nint)getBitWidth(size) + shiftAmount));
        }
    }

    private static uint insEncodeVectorShift(emitAttr size, bool isRightShift, nint shiftAmount)
    {
        return insEncodeShiftImmediate(size, isRightShift, shiftAmount) << 16;
    }

    private static uint insEncodeElemsize(emitAttr size)
    {
        if (size == EA_8BYTE)
        {
            return 0x00C00000;
        }
        else if (size == EA_4BYTE)
        {
            return 0x00800000;
        }
        else if (size == EA_2BYTE)
        {
            return 0x00400000;
        }

        assert(size == EA_1BYTE);

        return 0;
    }

    private static uint insEncodeVLSElemsize(emitAttr size)
    {
        uint result = 0;
        switch (size)
        {
            case EA_1BYTE:
            {
                result |= 0;
                break;
            }

            case EA_2BYTE:
            {
                result |= 0x0400;
                break;
            }

            case EA_4BYTE:
            {
                result |= 0x0800;
                break;
            }

            case EA_8BYTE:
            {
                result |= 0x0C00;
                break;
            }

            default:
            {
                assert(false, "!\"Invalid element size\"");
                break;
            }
        }

        return result;
    }

    private static uint insEncodeSimm9h9l_21_to_16_and_12_to_10(nint imm)
    {
        assert(isValidSimm(imm, 9));
        if (imm < 0)
        {
            imm &= 0x1FF;
        }

        var h = unchecked((uint)(imm & 0x1F8)) << 13;
        var l = unchecked((uint)((imm & ~0x1F8) & 0x7)) << 10;

        return h | l;
    }

    private static uint insEncodeUimm3h3l_23_to_22_and_12(nint imm)
    {
        assert(isValidUimm(imm, 3));
        var h = unchecked((uint)(imm & 0x6)) << 21;
        var l = unchecked((uint)(imm & 0x1)) << 12;

        return h | l;
    }

    private static uint insEncodeImm8_12_to_5(nint imm)
    {
        assert(isValidSimm(imm, 8) || isValidUimm(imm, 8));

        return unchecked((uint)((imm & 0xFF) << 5));
    }

    private static uint insEncodeSveElemsize(emitAttr size)
    {
        switch (size)
        {
            case EA_1BYTE:
            {
                return 0;
            }

            case EA_2BYTE:
            {
                return 0x00400000;
            }

            case EA_4BYTE:
            {
                return 0x00800000;
            }

            case EA_8BYTE:
            {
                return 0x00C00000;
            }

            default:
            {
                assert(false, "!\"Invalid insOpt for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeNarrowingSveElemsize(emitAttr size)
    {
        switch (size)
        {
            case EA_1BYTE:
            {
                return 0x00400000;
            }

            case EA_2BYTE:
            {
                return 0x00800000;
            }

            case EA_4BYTE:
            {
                return 0x00C00000;
            }

            default:
            {
                assert(false, "!\"Invalid insOpt for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeSveElemsize_22_to_21(emitAttr size)
    {
        switch (size)
        {
            case EA_1BYTE:
            {
                return 0;
            }

            case EA_2BYTE:
            {
                return 1u << 21;
            }

            case EA_4BYTE:
            {
                return 1u << 22;
            }

            case EA_8BYTE:
            {
                return (1u << 22) | (1u << 21);
            }

            default:
            {
                assert(false, "!\"Invalid insOpt for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeSveElemsize_18_to_17(emitAttr size)
    {
        switch (size)
        {
            case EA_1BYTE:
            {
                return 0;
            }

            case EA_2BYTE:
            {
                return 1u << 17;
            }

            case EA_4BYTE:
            {
                return 1u << 18;
            }

            case EA_8BYTE:
            {
                return (1u << 18) | (1u << 17);
            }

            default:
            {
                assert(false, "!\"Invalid insOpt for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeSveElemsize_sz_20(emitAttr size)
    {
        switch (size)
        {
            case EA_4BYTE:
            {
                return 0;
            }

            case EA_8BYTE:
            {
                return 1u << 20;
            }

            default:
            {
                assert(false, "!\"Invalid insOpt for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeSveElemsize_sz_21(emitAttr size)
    {
        switch (size)
        {
            case EA_4BYTE:
            {
                return 0;
            }

            case EA_8BYTE:
            {
                return 1u << 21;
            }

            default:
            {
                assert(false, "!\"Invalid insOpt for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeSveElemsize_tszh_23_tszl_20_to_19(emitAttr size)
    {
        switch (size)
        {
            case EA_1BYTE:
            {
                return 0x080000;
            }

            case EA_2BYTE:
            {
                return 0x100000;
            }

            case EA_4BYTE:
            {
                return 0x400000;
            }

            case EA_8BYTE:
            {
                return 0x800000;
            }

            default:
            {
                assert(false, "!\"Invalid size for vector register\"");
                break;
            }
        }

        return 0;
    }

    private static uint insEncodeSveElemsize_30_or_21(insFormat fmt, emitAttr size)
    {
        switch (fmt)
        {
            case IF_SVE_HX_3A_B:
            case IF_SVE_HX_3A_E:
            {
                switch (size)
                {
                    case EA_4BYTE:
                    {
                        return 0;
                    }

                    case EA_8BYTE:
                    {
                        return 1u << 30;
                    }

                    default:
                    {
                        break;
                    }
                }

                assert(false, "!\"Invalid size for vector register\"");
                return 0;
            }

            case IF_SVE_IV_3A:
            {
                assert(size == EA_8BYTE);
                return 0;
            }

            case IF_SVE_JI_3A_A:
            {
                switch (size)
                {
                    case EA_4BYTE:
                    {
                        return 1u << 21;
                    }

                    case EA_8BYTE:
                    {
                        return 0;
                    }

                    default:
                    {
                        break;
                    }
                }

                assert(false, "!\"Invalid size for vector register\"");
                return 0;
            }

            default:
            {
                break;
            }
        }

        assert(false, "!\"Unexpected instruction format\"");

        return 0;
    }

    private static uint insEncodeSveElemsize_tszh_tszl_and_imm(insOpts opt, nint imm)
    {
        uint encoding = 0;
        unchecked
        {
            switch (opt)
            {
                case INS_OPTS_SCALABLE_B:
                {
                    assert(isValidUimm(imm, 4));
                    encoding = 0x040000;
                    encoding |= (uint)((imm & 0b1100) << 22);
                    encoding |= (uint)((imm & 0b11) << 19);
                    break;
                }

                case INS_OPTS_SCALABLE_H:
                {
                    assert(isValidUimm(imm, 3));
                    encoding = 0x080000;
                    encoding |= (uint)((imm & 0b110) << 22);
                    encoding |= (uint)((imm & 1) << 20);
                    break;
                }

                case INS_OPTS_SCALABLE_S:
                {
                    assert(isValidUimm(imm, 2));
                    encoding = 0x100000;
                    encoding |= (uint)(imm << 22);
                    break;
                }

                case INS_OPTS_SCALABLE_D:
                {
                    assert(isValidUimm(imm, 1));
                    encoding = 0x400000;
                    encoding |= (uint)(imm << 23);
                    break;
                }

                default:
                {
                    assert(false, "!\"Invalid size for vector register\"");
                    break;
                }
            }
        }

        return encoding;
    }

    private static uint insEncodeSveElemsizeWithShift_tszh_tszl_imm3(insOpts opt, nint imm, bool isRightShift)
    {
        uint encoding = 0;
        imm = (nint)insEncodeShiftImmediate(optGetSveElemsize(opt), isRightShift, imm);
        switch (opt)
        {
            case INS_OPTS_SCALABLE_B:
            {
                imm &= 0b111;
                encoding |= 1u << 19;
                break;
            }

            case INS_OPTS_SCALABLE_H:
            {
                imm &= 0b1111;
                encoding |= 1u << 20;
                break;
            }

            case INS_OPTS_SCALABLE_S:
            {
                imm &= 0b11111;
                encoding |= 1u << 22;
                break;
            }

            case INS_OPTS_SCALABLE_D:
            {
                encoding |= unchecked((uint)((imm >> 5) << 22));
                imm &= 0b11111;
                encoding |= 1u << 23;
                break;
            }

            default:
            {
                assert(false, "!\"Invalid size for vector register\"");
                break;
            }
        }

        return encoding | unchecked((uint)(imm << 16));
    }

    private static uint insEncodeSveElemsizeWithImmediate_i1_tsz(insOpts opt, nint imm)
    {
        uint encoding = 0;
        unchecked
        {
            switch (opt)
            {
                case INS_OPTS_SCALABLE_B:
                {
                    assert(isValidUimm(imm, 4));
                    encoding |= 1u << 16;
                    encoding |= (uint)(imm << 17);
                    break;
                }

                case INS_OPTS_SCALABLE_H:
                {
                    assert(isValidUimm(imm, 3));
                    encoding |= 1u << 17;
                    encoding |= (uint)(imm << 18);
                    break;
                }

                case INS_OPTS_SCALABLE_S:
                {
                    assert(isValidUimm(imm, 2));
                    encoding |= 1u << 18;
                    encoding |= (uint)(imm << 19);
                    break;
                }

                case INS_OPTS_SCALABLE_D:
                {
                    assert(isValidUimm(imm, 1));
                    encoding |= 1u << 19;
                    encoding |= (uint)(imm << 20);
                    break;
                }

                default:
                {
                    assert(false, "!\"Invalid size for vector register\"");
                    break;
                }
            }
        }

        return encoding;
    }

    private static uint insEncodeSveShift_23_to_22_9_to_0(emitAttr size, bool isRightShift, nuint imm)
    {
        uint encodedSize = 0;
        switch (size)
        {
            case EA_1BYTE:
            {
                encodedSize = 0x100;
                break;
            }

            case EA_2BYTE:
            {
                encodedSize = 0x200;
                break;
            }

            case EA_4BYTE:
            {
                encodedSize = 0x400000;
                break;
            }

            case EA_8BYTE:
            {
                encodedSize = 0x800000;
                break;
            }

            default:
            {
                assert(false, "!\"Invalid esize for vector register\"");
                break;
            }
        }

        var encodedImm = insEncodeShiftImmediate(size, isRightShift, unchecked((nint)imm));
        var imm3High = (encodedImm & 0x60) << 17;
        var imm3Low = (encodedImm & 0x1F) << 5;

        return encodedSize | imm3High | imm3Low;
    }

    private static uint insEncodeSveImm90_or_270_rot(nint imm)
    {
        assert(emitIsValidEncodedRotationImm90_or_270(imm));

        return unchecked((uint)(imm << 16));
    }

    private static uint insEncodeSveImm0_to_270_rot(nint imm)
    {
        assert(emitIsValidEncodedRotationImm0_to_270(imm));

        return unchecked((uint)(imm << 13));
    }

    private static uint insEncodeSveSmallFloatImm(nint imm)
    {
        assert(emitIsValidEncodedSmallFloatImm(unchecked((nuint)imm)), "emitIsValidEncodedSmallFloatImm(imm)");

        return unchecked((uint)(imm << 5));
    }

    private static uint insEncodeSveElemsize_R_22(emitAttr size)
    {
        if (size == EA_8BYTE)
        {
            return 0x400000;
        }

        assert(size == EA_4BYTE);

        return 0;
    }

    private static nint insSveGetImmDiff(nint imm, insOpts opt)
    {
        unchecked
        {
            switch (opt)
            {
                case INS_OPTS_SCALABLE_B:
                {
                    assert(isValidUimmFrom1(imm, 3));
                    return 8 - imm;
                }

                case INS_OPTS_SCALABLE_H:
                {
                    assert(isValidUimmFrom1(imm, 4));
                    return 16 - imm;
                }

                case INS_OPTS_SCALABLE_S:
                {
                    assert(isValidUimmFrom1(imm, 5));
                    return 32 - imm;
                }

                case INS_OPTS_SCALABLE_D:
                {
                    assert(isValidUimmFrom1(imm, 6));
                    return 64 - imm;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }

        return 0;
    }

    private static uint insEncodeSvePattern(insSvePattern pattern)
    {
        return unchecked((uint)pattern) << 5;
    }

    private static uint insEncodeSveBroadcastIndex(emitAttr elemsize, nint index)
    {
        // Inline the shared genLog2(unsigned) contract without adding a shared mapping:
        // after a continuing EE assertion it still selects the least significant bit.
        var value = (uint)elemsize;
        assert(BitOperations.IsPow2(value), "genExactlyOneBit(value)");
        assert(value != 0, "value != 0");
        var lane_bytes = BitOperations.TrailingZeroCount(value) + 1;
        var tsz = 1u << (lane_bytes - 1);
        var imm = (unchecked((uint)index) << lane_bytes) | tsz;

        return insEncodeSplitUimm((nint)imm, 23, 22, 20, 16);
    }

    private static uint insEncodeVectorLengthSpecifier(instrDesc id)
    {
        assert(id is not null);
        assert(insOptsScalableStandard(id.idInsOpt()));
        if (id.idVectorLength4x())
        {
            switch (id.idInsFmt())
            {
                case IF_SVE_DL_2A:
                {
                    return 0x400;
                }

                case IF_SVE_DY_3A:
                {
                    return 0x2000;
                }

                default:
                {
                    assert(false, "!\"Unexpected format\"");
                    break;
                }
            }
        }

        return 0;
    }

    private static uint insEncodePredQualifier_16(bool merge)
    {
        return merge ? 1u << 16 : 0;
    }

    private static uint insEncodePredQualifier_4(bool merge)
    {
        return merge ? 1u << 4 : 0;
    }

    private static bool canEncodeSveElemsize_dtype(instruction ins)
    {
        switch (ins)
        {
            case INS_sve_ld1w:
            case INS_sve_ld1sb:
            case INS_sve_ld1b:
            case INS_sve_ld1sh:
            case INS_sve_ld1h:
            case INS_sve_ldnf1sh:
            case INS_sve_ldnf1w:
            case INS_sve_ldnf1h:
            case INS_sve_ldnf1sb:
            case INS_sve_ldnf1b:
            case INS_sve_ldff1b:
            case INS_sve_ldff1sb:
            case INS_sve_ldff1h:
            case INS_sve_ldff1sh:
            case INS_sve_ldff1w:
            {
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    private static uint insEncodeSveElemsize_dtype(instruction ins, emitAttr size, uint code)
    {
        assert(canEncodeSveElemsize_dtype(ins));
        assert(ins != INS_sve_ld1w);
        switch (size)
        {
            case EA_1BYTE:
            {
                switch (ins)
                {
                    case INS_sve_ld1b:
                    case INS_sve_ldnf1b:
                    case INS_sve_ldff1b:
                    {
                        return code;
                    }

                    default:
                    {
                        assert(false, "!\"Invalid instruction for encoding dtype.\"");
                        break;
                    }
                }

                return code;
            }

            case EA_2BYTE:
            {
                switch (ins)
                {
                    case INS_sve_ld1b:
                    case INS_sve_ld1h:
                    case INS_sve_ldnf1b:
                    case INS_sve_ldnf1h:
                    case INS_sve_ldff1b:
                    case INS_sve_ldff1h:
                    {
                        return code | (1u << 21);
                    }

                    case INS_sve_ld1sb:
                    case INS_sve_ldnf1sb:
                    case INS_sve_ldff1sb:
                    {
                        return code | (1u << 22);
                    }

                    default:
                    {
                        assert(false, "!\"Invalid instruction for encoding dtype.\"");
                        break;
                    }
                }

                return code;
            }

            case EA_4BYTE:
            {
                switch (ins)
                {
                    case INS_sve_ldnf1w:
                    case INS_sve_ldff1w:
                    {
                        return code;
                    }

                    case INS_sve_ld1b:
                    case INS_sve_ld1h:
                    case INS_sve_ldnf1b:
                    case INS_sve_ldnf1h:
                    case INS_sve_ldff1b:
                    case INS_sve_ldff1h:
                    {
                        return code | (1u << 22);
                    }

                    case INS_sve_ld1sb:
                    case INS_sve_ld1sh:
                    case INS_sve_ldnf1sb:
                    case INS_sve_ldnf1sh:
                    case INS_sve_ldff1sb:
                    case INS_sve_ldff1sh:
                    {
                        return code | (1u << 21);
                    }

                    default:
                    {
                        assert(false, "!\"Invalid instruction for encoding dtype.\"");
                        break;
                    }
                }

                return code;
            }

            case EA_8BYTE:
            {
                switch (ins)
                {
                    case INS_sve_ldnf1w:
                    case INS_sve_ldff1w:
                    {
                        return code | (1u << 21);
                    }

                    case INS_sve_ld1b:
                    case INS_sve_ld1h:
                    case INS_sve_ldnf1b:
                    case INS_sve_ldnf1h:
                    case INS_sve_ldff1b:
                    case INS_sve_ldff1h:
                    {
                        return (code | (1u << 22)) | (1u << 21);
                    }

                    case INS_sve_ld1sb:
                    case INS_sve_ld1sh:
                    case INS_sve_ldnf1sb:
                    case INS_sve_ldnf1sh:
                    case INS_sve_ldff1sb:
                    case INS_sve_ldff1sh:
                    {
                        return code;
                    }

                    default:
                    {
                        assert(false, "!\"Invalid instruction for encoding dtype.\"");
                        break;
                    }
                }

                return code;
            }

            default:
            {
                assert(false, "!\"Invalid size for encoding dtype.\"");
                break;
            }
        }

        return code;
    }

    private static uint insEncodeSveElemsize_dtype_ld1w(instruction ins, insFormat fmt, emitAttr size, uint code)
    {
        assert(canEncodeSveElemsize_dtype(ins));
        assert(ins == INS_sve_ld1w);
        switch (size)
        {
            case EA_4BYTE:
            {
                switch (fmt)
                {
                    case IF_SVE_IH_3A_F:
                    {
                        // Bit 15 is outside dtype, but is required for S in the immediate form.
                        return (code | (1u << 15)) | (1u << 22);
                    }

                    case IF_SVE_II_4A_H:
                    {
                        // Bit 14 is outside dtype, but is required for S in the register form.
                        return (code | (1u << 14)) | (1u << 22);
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            case EA_8BYTE:
            {
                switch (fmt)
                {
                    case IF_SVE_IH_3A_F:
                    {
                        return ((code | (1u << 15)) | (1u << 22)) | (1u << 21);
                    }

                    case IF_SVE_II_4A_H:
                    {
                        return ((code | (1u << 14)) | (1u << 22)) | (1u << 21);
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            case EA_16BYTE:
            {
                switch (fmt)
                {
                    case IF_SVE_IH_3A_F:
                    {
                        return code | (1u << 20);
                    }

                    case IF_SVE_II_4A_H:
                    {
                        // Bit 15 is outside dtype, but is required for Q in the register form.
                        return code | (1u << 15);
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            default:
            {
                assert(false, "!\"Invalid size for encoding dtype.\"");
                break;
            }
        }

        assert(false, "!\"Invalid instruction format\"");

        return code;
    }

    private static uint insEncodeSveElemsize_dtypeh_dtypel(instruction ins, insFormat fmt, emitAttr size, uint code)
    {
        switch (fmt)
        {
            case IF_SVE_IC_3A_A:
            {
                switch (size)
                {
                    case EA_4BYTE:
                    {
                        switch (ins)
                        {
                            case INS_sve_ld1rsh:
                            {
                                return code | (1u << 13);
                            }

                            case INS_sve_ld1rw:
                            {
                                return code | (1u << 14);
                            }

                            default:
                            {
                                break;
                            }
                        }

                        break;
                    }

                    case EA_8BYTE:
                    {
                        switch (ins)
                        {
                            case INS_sve_ld1rsh:
                            {
                                return code;
                            }

                            case INS_sve_ld1rw:
                            {
                                return code | (1u << 14) | (1u << 13);
                            }

                            default:
                            {
                                break;
                            }
                        }

                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            case IF_SVE_IC_3A_B:
            {
                switch (size)
                {
                    case EA_2BYTE:
                    {
                        switch (ins)
                        {
                            case INS_sve_ld1rh:
                            {
                                return code | (1u << 13);
                            }

                            case INS_sve_ld1rsb:
                            {
                                return code | (1u << 24) | (1u << 14);
                            }

                            default:
                            {
                                break;
                            }
                        }

                        break;
                    }

                    case EA_4BYTE:
                    {
                        switch (ins)
                        {
                            case INS_sve_ld1rh:
                            {
                                return code | (1u << 14);
                            }

                            case INS_sve_ld1rsb:
                            {
                                return code | (1u << 24) | (1u << 13);
                            }

                            default:
                            {
                                break;
                            }
                        }

                        break;
                    }

                    case EA_8BYTE:
                    {
                        switch (ins)
                        {
                            case INS_sve_ld1rh:
                            {
                                return code | (1u << 14) | (1u << 13);
                            }

                            case INS_sve_ld1rsb:
                            {
                                return code | (1u << 24);
                            }

                            default:
                            {
                                break;
                            }
                        }

                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            case IF_SVE_IC_3A_C:
            {
                assert(ins == INS_sve_ld1rb);
                switch (size)
                {
                    case EA_1BYTE:
                    {
                        return code;
                    }

                    case EA_2BYTE:
                    {
                        return code | (1u << 13);
                    }

                    case EA_4BYTE:
                    {
                        return code | (1u << 14);
                    }

                    case EA_8BYTE:
                    {
                        return code | (1u << 14) | (1u << 13);
                    }

                    default:
                    {
                        break;
                    }
                }

                break;
            }

            default:
            {
                break;
            }
        }

        assert(false, "!\"Unexpected instruction format\"");

        return code;
    }
}
#endif
