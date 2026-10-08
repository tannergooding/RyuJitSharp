// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSveMovOpts;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64SveThreeRegisterRecordingTests
{
    [TestCase(INS_sve_and, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_AU_3A)]
    [TestCase(INS_sve_bic, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_add, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_AT_3A)]
    [TestCase(INS_sve_subr, INS_OPTS_SCALABLE_S, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_udivr, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AC_3A)]
    [TestCase(INS_sve_smax, INS_OPTS_SCALABLE_B, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_umulh, INS_OPTS_SCALABLE_S, REG_V1, IF_SVE_AT_3A)]
    [TestCase(INS_sve_mul, INS_OPTS_SCALABLE_H, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_pmul, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_BD_3B)]
    [TestCase(INS_sve_andv, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AF_3A)]
    [TestCase(INS_sve_saddv, INS_OPTS_SCALABLE_S, REG_P7, IF_SVE_AI_3A)]
    [TestCase(INS_sve_uaddv, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AI_3A)]
    [TestCase(INS_sve_sminv, INS_OPTS_SCALABLE_H, REG_P7, IF_SVE_AK_3A)]
    [TestCase(INS_sve_asrr, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_asr, INS_OPTS_SCALABLE_S, REG_V1, IF_SVE_BG_3A)]
    [TestCase(INS_sve_lsr, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_zip1, INS_OPTS_SCALABLE_Q, REG_V1, IF_SVE_BR_3B)]
    [TestCase(INS_sve_tbl, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_BZ_3A)]
    [TestCase(INS_sve_sdot, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_EF_3A)]
    [TestCase(INS_sve_udot, INS_OPTS_SCALABLE_D, REG_V1, IF_SVE_EH_3A)]
    [TestCase(INS_sve_usdot, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_EI_3A)]
    [TestCase(INS_sve_umlslt, INS_OPTS_SCALABLE_S, REG_V1, IF_SVE_EL_3A)]
    [TestCase(INS_sve_sqrdmlsh, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_EM_3A)]
    [TestCase(INS_sve_tblq, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_EX_3A)]
    [TestCase(INS_sve_sabdlb, INS_OPTS_SCALABLE_D, REG_V1, IF_SVE_FL_3A)]
    [TestCase(INS_sve_uaddwt, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_FM_3A)]
    [TestCase(INS_sve_pmullt, INS_OPTS_SCALABLE_Q, REG_V1, IF_SVE_FN_3B)]
    [TestCase(INS_sve_smmla, INS_OPTS_SCALABLE_S, REG_V1, IF_SVE_FO_3A)]
    [TestCase(INS_sve_rax1, INS_OPTS_SCALABLE_D, REG_V1, IF_SVE_GJ_3A)]
    [TestCase(INS_sve_fmlalt, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_HB_3A)]
    [TestCase(INS_sve_bfmmla, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_HD_3A)]
    [TestCase(INS_sve_bfclamp, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_GW_3B)]
    [TestCase(INS_sve_bfdot, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_HA_3A)]
    [TestCase(INS_sve_bext, INS_OPTS_SCALABLE_D, REG_V1, IF_SVE_AT_3A)]
    [TestCase(INS_sve_saba, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_FW_3A)]
    [TestCase(INS_sve_rsubhnt, INS_OPTS_SCALABLE_S, REG_V1, IF_SVE_GC_3A)]
    [TestCase(INS_sve_histseg, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_GF_3A)]
    [TestCase(INS_sve_cnot, INS_OPTS_SCALABLE_B, REG_P7, IF_SVE_AP_3A)]
    [TestCase(INS_sve_sxtw, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AQ_3A)]
    [TestCase(INS_sve_compact, INS_OPTS_SCALABLE_S, REG_P7, IF_SVE_CL_3A)]
    [TestCase(INS_sve_revd, INS_OPTS_SCALABLE_Q, REG_P7, IF_SVE_CT_3A)]
    [TestCase(INS_sve_revw, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_CU_3A)]
    [TestCase(INS_sve_splice, INS_OPTS_SCALABLE_B, REG_P7, IF_SVE_CV_3B)]
    [TestCase(INS_sve_sadalp, INS_OPTS_SCALABLE_H, REG_P7, IF_SVE_EQ_3A)]
    [TestCase(INS_sve_sqabs, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_ES_3A)]
    [TestCase(INS_sve_urecpe, INS_OPTS_SCALABLE_S, REG_P7, IF_SVE_ES_3A)]
    [TestCase(INS_sve_sqadd, INS_OPTS_SCALABLE_B, REG_V1, IF_SVE_AT_3A)]
    [TestCase(INS_sve_sqsub, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_AA_3A)]
    [TestCase(INS_sve_fcvtnt, INS_OPTS_S_TO_H, REG_P7, IF_SVE_GQ_3A)]
    [TestCase(INS_sve_faddp, INS_OPTS_SCALABLE_H, REG_P7, IF_SVE_GR_3A)]
    [TestCase(INS_sve_faddv, INS_OPTS_SCALABLE_S, REG_P7, IF_SVE_HE_3A)]
    [TestCase(INS_sve_fadda, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_HJ_3A)]
    [TestCase(INS_sve_frecps, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_AT_3A)]
    [TestCase(INS_sve_fadd, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_HL_3A)]
    [TestCase(INS_sve_bfmul, INS_OPTS_SCALABLE_H, REG_V1, IF_SVE_HK_3B)]
    [TestCase(INS_sve_bfsub, INS_OPTS_SCALABLE_H, REG_P7, IF_SVE_HL_3B)]
    [TestCase(INS_sve_bsl2n, INS_OPTS_SCALABLE_D, REG_V1, IF_SVE_AV_3A)]
    [TestCase(INS_sve_frinti, INS_OPTS_SCALABLE_D, REG_P7, IF_SVE_HQ_3A)]
    [TestCase(INS_sve_bfcvt, INS_OPTS_S_TO_H, REG_P7, IF_SVE_HO_3A)]
    [TestCase(INS_sve_fcvt, INS_OPTS_S_TO_D, REG_P7, IF_SVE_HO_3B)]
    [TestCase(INS_sve_fcvtx, INS_OPTS_D_TO_S, REG_P7, IF_SVE_HO_3C)]
    [TestCase(INS_sve_fcvtzu, INS_OPTS_H_TO_D, REG_P7, IF_SVE_HP_3B)]
    [TestCase(INS_sve_ucvtf, INS_OPTS_D_TO_H, REG_P7, IF_SVE_HS_3A)]
    [TestCase(INS_sve_fsqrt, INS_OPTS_SCALABLE_S, REG_P7, IF_SVE_HR_3A)]
    [TestCase(INS_sve_sbclt, INS_OPTS_SCALABLE_D, REG_V1, IF_SVE_FY_3A)]
    [TestCase(INS_sve_flogb, INS_OPTS_SCALABLE_H, REG_P7, IF_SVE_HP_3A)]
    public static void RegisterFamiliesRetainFormatsRegistersAndNormalDescriptor(
        instruction ins, insOpts opt, regNumber second, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_R(ins, EA_SCALABLE, REG_V0, second, REG_V31, opt));

            AssertDescriptor(id, ins, format, opt, EA_SCALABLE, REG_V0, second, REG_V31);
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idPredicateReg2Merge(), Is.False);
        });
    }

    [TestCase(INS_sve_cpy, EA_SCALABLE, REG_V0, REG_P7, REG_V31, INS_OPTS_SCALABLE_D,
        INS_SCALABLE_OPTS_WITH_SIMD_SCALAR, INS_sve_mov, IF_SVE_CP_3A, REG_V31, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_cpy, EA_8BYTE, REG_V0, REG_P7, REG_SP, INS_OPTS_SCALABLE_D,
        INS_SCALABLE_OPTS_NONE, INS_sve_mov, IF_SVE_CQ_3A, REG_ZR, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_mov, EA_SCALABLE, REG_V0, REG_V1, REG_V31, INS_OPTS_SCALABLE_B,
        INS_SCALABLE_OPTS_NONE, INS_sve_orr, IF_SVE_AU_3A, REG_V31, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_cpy, EA_SCALABLE, REG_P0, REG_P15, REG_P7, INS_OPTS_SCALABLE_B,
        INS_SCALABLE_OPTS_PREDICATE_MERGE, INS_sve_mov, IF_SVE_CZ_4A_K, REG_P7, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_mov, EA_SCALABLE, REG_V0, REG_P15, REG_V31, INS_OPTS_SCALABLE_D,
        INS_SCALABLE_OPTS_PREDICATE_MERGE, INS_sve_mov, IF_SVE_CW_4A, REG_V31, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_brkns, EA_SCALABLE, REG_P15, REG_P0, REG_P7, INS_OPTS_SCALABLE_Q,
        INS_SCALABLE_OPTS_NONE, INS_sve_brkns, IF_SVE_DC_3A, REG_P7, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_zip2, EA_SCALABLE, REG_P15, REG_P0, REG_P7, INS_OPTS_SCALABLE_H,
        INS_SCALABLE_OPTS_NONE, INS_sve_zip2, IF_SVE_CI_3A, REG_P7, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_index, EA_8BYTE, REG_V0, REG_ZR, REG_R30, INS_OPTS_SCALABLE_S,
        INS_SCALABLE_OPTS_NONE, INS_sve_index, IF_SVE_BA_3A, REG_R30, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_clasta, EA_8BYTE, REG_R30, REG_P7, REG_V31, INS_OPTS_SCALABLE_D,
        INS_SCALABLE_OPTS_NONE, INS_sve_clasta, IF_SVE_CO_3A, REG_V31, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_clastb, EA_SCALABLE, REG_V0, REG_P7, REG_V31, INS_OPTS_SCALABLE_S,
        INS_SCALABLE_OPTS_WITH_SIMD_SCALAR, INS_sve_clastb, IF_SVE_CN_3A, REG_V31, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_clastb, EA_SCALABLE, REG_V0, REG_P7, REG_V31, INS_OPTS_SCALABLE_B,
        INS_SCALABLE_OPTS_NONE, INS_sve_clastb, IF_SVE_CM_3A, REG_V31, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_tbl, EA_SCALABLE, REG_V0, REG_V2, REG_V31, INS_OPTS_SCALABLE_D,
        INS_SCALABLE_OPTS_WITH_VECTOR_PAIR, INS_sve_tbl, IF_SVE_BZ_3A_A, REG_V31, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_lsl, EA_SCALABLE, REG_V0, REG_P7, REG_V31, INS_OPTS_SCALABLE_H,
        INS_SCALABLE_OPTS_WIDE, INS_sve_lsl, IF_SVE_AO_3A, REG_V31, INS_OPTS_SCALABLE_H)]
    public static void SpecialBanksAliasesAndAuxiliaryOptionsRetainNativeBinding(
        instruction ins, emitAttr attr, regNumber first, regNumber second, regNumber third,
        insOpts opt, insScalableOpts sopt, instruction expectedIns, Emitter.insFormat format,
        regNumber expectedThird, insOpts expectedOpt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R(ins, attr, first, second, third, opt, sopt));

            AssertDescriptor(id, expectedIns, format, expectedOpt, attr, first, second, expectedThird);
        });
    }

    [TestCase(INS_sve_movprfx, REG_V0, REG_P7, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_AH_3A, false)]
    [TestCase(INS_sve_movprfx, REG_V0, REG_P7, REG_V31, INS_SCALABLE_OPTS_PREDICATE_MERGE, IF_SVE_AH_3A, true)]
    [TestCase(INS_sve_brka, REG_P15, REG_P0, REG_P7, INS_SCALABLE_OPTS_NONE, IF_SVE_DB_3A, false)]
    [TestCase(INS_sve_brkb, REG_P15, REG_P0, REG_P7, INS_SCALABLE_OPTS_PREDICATE_MERGE, IF_SVE_DB_3A, true)]
    public static void PredicateMergeBitUsesNormalDescriptor(
        instruction ins, regNumber first, regNumber second, regNumber third,
        insScalableOpts sopt, Emitter.insFormat format, bool merge)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R(
                ins, EA_SCALABLE, first, second, third, INS_OPTS_SCALABLE_B, sopt));

            AssertDescriptor(id, ins, format, INS_OPTS_SCALABLE_B, EA_SCALABLE, first, second, third);
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idPredicateReg2Merge(), Is.EqualTo(merge));
        });
    }

    [TestCase(REG_P15, EA_4BYTE, INS_SCALABLE_OPTS_NONE, IF_SVE_DT_3A, false)]
    [TestCase(REG_P7, EA_8BYTE, INS_SCALABLE_OPTS_WITH_PREDICATE_PAIR, IF_SVE_DX_3A, false)]
    [TestCase(REG_P8, EA_8BYTE, INS_SCALABLE_OPTS_VL_2X, IF_SVE_DY_3A, false)]
    [TestCase(REG_P15, EA_8BYTE, INS_SCALABLE_OPTS_VL_4X, IF_SVE_DY_3A, true)]
    public static void WhileFormsRetainWidthPredicateBankAndVectorLength(
        regNumber first, emitAttr attr, insScalableOpts sopt, Emitter.insFormat format, bool fourTimes)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R(
                INS_sve_whilelt, attr, first, REG_R0, REG_R30, INS_OPTS_SCALABLE_D, sopt));

            AssertDescriptor(id, INS_sve_whilelt, format, INS_OPTS_SCALABLE_D, attr, first, REG_R0, REG_R30);
            Assert.That(id.idVectorLength4x(), Is.EqualTo(fourTimes));
        });
    }

    [TestCase(INS_sve_cmpeq, INS_OPTS_SCALABLE_B, REG_P15, REG_P7, REG_V31, -16, IF_SVE_CY_3A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_cmple, INS_OPTS_SCALABLE_D, REG_P0, REG_P0, REG_V0, 15, IF_SVE_CY_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_cmphi, INS_OPTS_SCALABLE_H, REG_P15, REG_P7, REG_V31, 127, IF_SVE_CY_3B, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_sdot, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 3, IF_SVE_EG_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_udot, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 3, IF_SVE_EY_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_sdot, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 1, IF_SVE_EY_3B, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_sudot, INS_OPTS_SCALABLE_B, REG_V0, REG_V31, REG_V7, 3, IF_SVE_EZ_3A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_mul, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 7, IF_SVE_FD_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_mul, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 3, IF_SVE_FD_3B, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_mul, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 1, IF_SVE_FD_3C, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_cdot, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V1, 3, IF_SVE_EJ_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_sqrdcmlah, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V1, 2, IF_SVE_EK_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IH_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_Q, REG_V0, REG_P7, REG_R30, 7, IF_SVE_IH_3A_A, INS_OPTS_SCALABLE_Q)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_V31, 248, IF_SVE_IV_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ldff1d, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_V31, 248, IF_SVE_IV_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ld1w, INS_OPTS_SCALABLE_Q, REG_V0, REG_P7, REG_R30, 7, IF_SVE_IH_3A_F, INS_OPTS_SCALABLE_Q)]
    [TestCase(INS_sve_ld1w, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31, 124, IF_SVE_HX_3A_E, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_ld1sw, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IJ_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ldff1sw, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_V31, 124, IF_SVE_IV_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ld1sb, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IJ_3A_D, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_ld1b, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_R30, 7, IF_SVE_IJ_3A_E, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_ldff1sb, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31, 31, IF_SVE_HX_3A_B, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_ld1sh, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IJ_3A_F, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ld1h, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_R30, 7, IF_SVE_IJ_3A_G, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_ldff1h, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31, 62, IF_SVE_HX_3A_E, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_ldnf1d, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IL_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ldnf1w, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_R30, 7, IF_SVE_IL_3A_A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_ldnf1sb, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IL_3A_B, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_ldnf1b, INS_OPTS_SCALABLE_B, REG_V0, REG_P15, REG_R30, 7, IF_SVE_IL_3A_C, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_ldnt1w, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_R30, -8, IF_SVE_IM_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_ld1rqb, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_R30, -128, IF_SVE_IO_3A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_ld1rod, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, 224, IF_SVE_IO_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ld3q, INS_OPTS_SCALABLE_Q, REG_V0, REG_P7, REG_R30, -24, IF_SVE_IQ_3A, INS_OPTS_SCALABLE_Q)]
    [TestCase(INS_sve_ld4h, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_R30, 28, IF_SVE_IS_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_st2q, INS_OPTS_SCALABLE_Q, REG_V0, REG_P7, REG_R30, -16, IF_SVE_JE_3A, INS_OPTS_SCALABLE_Q)]
    [TestCase(INS_sve_stnt1d, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, 7, IF_SVE_JM_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_Q, REG_V0, REG_P7, REG_R30, -8, IF_SVE_JN_3C_D, INS_OPTS_SCALABLE_Q)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_R30, 7, IF_SVE_JN_3B, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_Q, REG_V0, REG_P7, REG_R30, -8, IF_SVE_JN_3C, INS_OPTS_SCALABLE_Q)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_V31, 124, IF_SVE_JI_3A_A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_V31, 248, IF_SVE_JL_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_st3b, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_R30, -24, IF_SVE_JO_3A, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_R30, 7, IF_SVE_JN_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_fmls, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 3, IF_SVE_GU_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_fmla, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 1, IF_SVE_GU_3B, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_bfmla, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 7, IF_SVE_GU_3C, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_fmul, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 3, IF_SVE_GX_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_bfmul, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 7, IF_SVE_GX_3C, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_fdot, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 3, IF_SVE_GY_3B, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_mls, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 7, IF_SVE_FF_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_mla, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 3, IF_SVE_FF_3B, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_mla, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 1, IF_SVE_FF_3C, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_umullt, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 7, IF_SVE_FE_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_umullb, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 3, IF_SVE_FE_3B, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_umlslt, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 3, IF_SVE_FG_3B, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_sqdmullb, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 7, IF_SVE_FH_3A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_sqrdmulh, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 7, IF_SVE_FI_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_sqdmlslt, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, REG_V7, 3, IF_SVE_FJ_3B, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_sqrdmlah, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, REG_V7, 3, IF_SVE_FK_3B, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_fcadd, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_V31, 1, IF_SVE_GP_3A, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_ld1rd, INS_OPTS_SCALABLE_D, REG_V0, REG_P7, REG_R30, 504, IF_SVE_IC_3A, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_sve_ld1rw, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_R30, 252, IF_SVE_IC_3A_A, INS_OPTS_SCALABLE_S)]
    [TestCase(INS_sve_ld1rh, INS_OPTS_SCALABLE_H, REG_V0, REG_P7, REG_R30, 126, IF_SVE_IC_3A_B, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_ld1rb, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_R30, 63, IF_SVE_IC_3A_C, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_bfmlslt, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7, 7, IF_SVE_GZ_3A, INS_OPTS_SCALABLE_H)]
    public static void ImmediateFamiliesRetainRawImmediateAndNativeOptionRewriting(
        instruction ins, insOpts opt, regNumber first, regNumber second, regNumber third,
        int immediate, Emitter.insFormat format, insOpts expectedOpt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_R_I(
                ins, EA_SCALABLE, first, second, third, immediate, opt));

            AssertDescriptor(id, ins, format, expectedOpt, EA_SCALABLE, first, second, third);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)immediate));
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idIsLargeCns(), Is.EqualTo(!Emitter.instrDesc.fitsInSmallCns(immediate)));
        });
    }

    [TestCase(INS_OPTS_SCALABLE_S, INS_SCALABLE_OPTS_LSL_N, IF_SVE_BH_3A)]
    [TestCase(INS_OPTS_SCALABLE_D, INS_SCALABLE_OPTS_LSL_N, IF_SVE_BH_3A)]
    [TestCase(INS_OPTS_SCALABLE_D_SXTW, INS_SCALABLE_OPTS_NONE, IF_SVE_BH_3B)]
    [TestCase(INS_OPTS_SCALABLE_D_UXTW, INS_SCALABLE_OPTS_NONE, IF_SVE_BH_3B_A)]
    public static void AddressFormsRetainExtensionOptionsAndShift(
        insOpts opt, insScalableOpts sopt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_I(
                INS_sve_adr, EA_SCALABLE, REG_V0, REG_V1, REG_V31, 3, opt, sopt));

            AssertDescriptor(id, INS_sve_adr, format, opt, EA_SCALABLE, REG_V0, REG_V1, REG_V31);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)3));
        });
    }

    [TestCase(INS_sve_ld1b, INS_OPTS_SCALABLE_B, IF_SVE_IJ_3A_E)]
    [TestCase(INS_sve_ldnf1d, INS_OPTS_SCALABLE_D, IF_SVE_IL_3A)]
    [TestCase(INS_sve_ldnt1w, INS_OPTS_SCALABLE_S, IF_SVE_IM_3A)]
    [TestCase(INS_sve_ld1rqh, INS_OPTS_SCALABLE_H, IF_SVE_IO_3A)]
    public static void RegisterOnlyLoadsDelegateZeroImmediate(
        instruction ins, insOpts opt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_P7, REG_R30, opt));

            AssertDescriptor(id, ins, format, opt, EA_SCALABLE, REG_V0, REG_P7, REG_R30);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)0));
        });
    }

    [TestCase(INS_sve_asr, INS_SVE_MOV_OPTS_UNPRED, 4)]
    [TestCase(INS_sve_asr, INS_SVE_MOV_OPTS_ZEROING, 8)]
    [TestCase(INS_sve_asr, INS_SVE_MOV_OPTS_MERGING, 8)]
    public static void MaskedRmwPreservesPrefixBeforeRealPairImmediateRecorder(
        instruction ins, insSveMovOpts mopt, int expectedSize)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                emitter.emitInsSve_R_R_R_I(
                    ins, EA_SCALABLE, REG_V0, REG_P7, REG_V0, 1, INS_OPTS_SCALABLE_B, mopt: mopt);
            }
#if DEBUG
            var (output, assertions) = Arm64SveInstructionSanityTests.Capture(RecordOperands);
            Assert.That(assertions, Is.Empty);
            Assert.That(output, Is.Empty);
#else
            RecordOperands();
#endif
            Assert.That(GroupSize(emitter), Is.EqualTo(expectedSize));
            var instructions = CurrentInstructions(emitter)
                ?? throw new AssertionException("No descriptor buffer was prepared.");
            Assert.That(instructions.Count, Is.EqualTo(expectedSize / 4));
            var id = LastInstruction(emitter) ?? throw new AssertionException("No operand descriptor was prepared.");
            Assert.That(instructions[^1], Is.SameAs(id));
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_SVE_AM_2A));
            Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_B));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(id.idReg2(), Is.EqualTo(REG_P7));
            Assert.That(id.idCodeSize(), Is.EqualTo(4u));
            Assert.That(id.idIsSmallDsc(), Is.True);
            Assert.That(id.idHasShift(), Is.False);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)1));
            if (mopt != INS_SVE_MOV_OPTS_UNPRED)
            {
                var prefix = instructions[0];
                AssertDescriptor(prefix, INS_sve_movprfx, IF_SVE_AH_3A, INS_OPTS_SCALABLE_B,
                    EA_SCALABLE, REG_V0, REG_P7, REG_V0);
                Assert.That(prefix.idPredicateReg2Merge(), Is.EqualTo(mopt == INS_SVE_MOV_OPTS_MERGING));
                Assert.That(prefix.idIsSmallDsc(), Is.False);
                Assert.That(prefix.idCodeSize(), Is.EqualTo(4u));
            }
        });
    }

    [TestCase(INS_sve_pfirst, EA_SCALABLE, REG_P0, REG_P7, REG_P0, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_sqincp, EA_SCALABLE, REG_V0, REG_V0, REG_P7, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_insr, EA_SCALABLE, REG_V0, REG_V0, REG_R0, INS_OPTS_SCALABLE_B)]
    [TestCase(INS_sve_sqxtnt, EA_SCALABLE, REG_V0, REG_V0, REG_V31, INS_OPTS_SCALABLE_H)]
    public static void RegisterRmwUsesPairRecorderWhenMoveDependencyIsAvailable(
        instruction ins, emitAttr attr, regNumber first, regNumber second, regNumber third, insOpts opt)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                emitter.emitInsSve_R_R_R(ins, attr, first, second, third, opt);
            }
            if (ins == INS_sve_sqxtnt)
            {
                var rmwId = Record(emitter, RecordOperands);
                Assert.That(rmwId.idIns(), Is.EqualTo(INS_sve_sqxtnt));
                Assert.That(rmwId.idInsFmt(), Is.EqualTo(IF_SVE_GD_2A));
                Assert.That(rmwId.idInsOpt(), Is.EqualTo(opt));
                Assert.That(rmwId.idOpSize(), Is.EqualTo(attr));
                Assert.That(rmwId.idReg1(), Is.EqualTo(first));
                Assert.That(rmwId.idReg2(), Is.EqualTo(third));
                var rmwInstructions = CurrentInstructions(emitter)
                    ?? throw new AssertionException("No descriptor buffer was prepared.");
                Assert.That(rmwInstructions.Count, Is.EqualTo(1));
                Assert.That(rmwInstructions[0], Is.SameAs(rmwId));
                return;
            }

            var id = Record(emitter, RecordOperands);
            var format = ins switch
            {
                INS_sve_pfirst => IF_SVE_DD_2A,
                INS_sve_sqincp => IF_SVE_DP_2A,
                INS_sve_insr => IF_SVE_CD_2A,
                _ => throw new AssertionException("Unexpected recorder control.")
            };
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idInsOpt(), Is.EqualTo(opt));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idReg1(), Is.EqualTo(first));
            Assert.That(id.idReg2(), Is.EqualTo(ins == INS_sve_pfirst ? second : third));
            Assert.That(id.idIsSmallDsc(), Is.True);
            var instructions = CurrentInstructions(emitter)
                ?? throw new AssertionException("No descriptor buffer was prepared.");
            Assert.That(instructions.Count, Is.EqualTo(1));
            Assert.That(instructions[0], Is.SameAs(id));
        });
    }

    [TestCase(INS_sve_ext, INS_OPTS_SCALABLE_D, IF_SVE_BQ_2B, false)]
    [TestCase(INS_sve_ext, INS_OPTS_SCALABLE_B, IF_SVE_BQ_2B, true)]
    [TestCase(INS_sve_sri, INS_OPTS_SCALABLE_D, IF_SVE_FT_2A, true)]
    [TestCase(INS_sve_cadd, INS_OPTS_SCALABLE_D, IF_SVE_FV_2A, true)]
    [TestCase(INS_sve_xar, INS_OPTS_SCALABLE_D, IF_SVE_AW_2A, true)]
    public static void ImmediateRmwPreservesPairFlowAndInvalidExtContinuation(
        instruction ins, insOpts opt, Emitter.insFormat format, bool validOptions)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                emitter.emitInsSve_R_R_R_I(
                    ins, EA_SCALABLE, REG_V0, REG_V0, REG_V31, 1, opt);
            }
#if DEBUG
            var (output, assertions) = Arm64SveInstructionSanityTests.Capture(RecordOperands);
            Assert.That(assertions, Is.EqualTo<string[]>(validOptions ? [] : [
                "opt == INS_OPTS_SCALABLE_B",
                "id.idInsOpt() == INS_OPTS_SCALABLE_B"
            ]));
            Assert.That(output, Is.Empty);
#else
            RecordOperands();
#endif
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
            var id = LastInstruction(emitter) ?? throw new AssertionException("No operand descriptor was prepared.");
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idInsOpt(), Is.EqualTo(opt));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(id.idReg2(), Is.EqualTo(REG_V31));
            Assert.That(id.idCodeSize(), Is.EqualTo(4u));
            Assert.That(id.idIsSmallDsc(), Is.True);
            Assert.That(id.idHasShift(), Is.False);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)1));
            var instructions = CurrentInstructions(emitter)
                ?? throw new AssertionException("No descriptor buffer was prepared.");
            Assert.That(instructions.Count, Is.EqualTo(1));
            Assert.That(instructions[0], Is.SameAs(id));
        });
    }

    [TestCase(INS_sve_addpt, false)]
    [TestCase(INS_sve_fmmla, false)]
    [TestCase(INS_sve_luti2, true)]
    [TestCase(INS_sve_luti4, true)]
    [TestCase(INS_nop, false)]
    [TestCase(INS_nop, true)]
    public static void NativeUnsupportedPathsTerminateBeforeAppending(instruction ins, bool immediate)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                if (immediate)
                {
                    emitter.emitInsSve_R_R_R_I(ins, EA_SCALABLE, REG_V0, REG_V1, REG_V2,
                        0, INS_OPTS_SCALABLE_H);
                }
                else
                {
                    emitter.emitInsSve_R_R_R(ins, EA_SCALABLE, REG_V0, REG_V1, REG_V2, INS_OPTS_SCALABLE_D);
                }
            }
#if DEBUG
            Arm64SveInstructionSanityTests.Capture(() => Assert.Throws<FatalJitException>(() => RecordOperands()));
#else
            Assert.Throws<FatalJitException>(() => RecordOperands());
#endif
            Assert.That(GroupSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }

    [TestCase(-128, 4, 16, true)]
    [TestCase(112, 4, 16, true)]
    [TestCase(-127, 4, 16, false)]
    [TestCase(128, 4, 16, false)]
    public static void SignedMultiplesRetainTruncationAndBounds(int value, int bits, int multiple, bool valid)
    {
        Assert.That(SignedMultiple(null, value, bits, multiple), Is.EqualTo(valid));
    }

    [TestCase(0L, 5, 8, true)]
    [TestCase(248L, 5, 8, true)]
    [TestCase(249L, 5, 8, false)]
    [TestCase(256L, 5, 8, false)]
    [TestCase(-8L, 5, 8, false)]
    [TestCase(long.MinValue, 5, 8, false)]
    public static void UnsignedMultiplesRetainNativeUnsignedDivisorConversion(
        long value, int bits, int multiple, bool valid)
    {
        Assert.That(UnsignedMultiple(null, (nint)value, bits, (nuint)multiple), Is.EqualTo(valid));
    }

    [TestCase(INS_SVE_MOV_OPTS_UNPRED, REG_V0, REG_V0, REG_V0, true)]
    [TestCase(INS_SVE_MOV_OPTS_UNPRED, REG_V0, REG_V1, REG_V0, false)]
    [TestCase(INS_SVE_MOV_OPTS_MERGING, REG_V0, REG_V0, REG_V0, false)]
    [TestCase(INS_SVE_MOV_OPTS_ZEROING, REG_V0, REG_V1, REG_V31, true)]
    public static void PrefixRegisterValidationRetainsElisionExceptionAndOverlapChecks(
        insSveMovOpts mopt, regNumber destination, regNumber source, regNumber operand, bool valid)
    {
        Assert.That(ValidPrefix(null, mopt, destination, source, operand, REG_NA, REG_NA), Is.EqualTo(valid));
    }

    [TestCase(0, 0)]
    [TestCase(1, 90)]
    [TestCase(2, 180)]
    [TestCase(3, 270)]
    public static void RotationDecoderRetainsEncodedRotation(int encoded, int degrees)
    {
        Assert.That(Rotation(null, encoded), Is.EqualTo((nint)degrees));
    }

#if DEBUG
    [Test]
    public static void RecorderAssertionsPrecedeDescriptorSanityAssertions()
    {
        WithEmitter(emitter =>
        {
            var (output, assertions) = Arm64SveInstructionSanityTests.Capture(() => emitter.emitInsSve_R_R_R(
                INS_sve_add, EA_4BYTE, REG_R0, REG_P8, REG_R1, INS_OPTS_NONE));

            Assert.That(assertions, Is.EqualTo<string[]>([
                "isVectorRegister(reg1)",
                "isVectorRegister(reg3)",
                "insOptsScalableStandard(opt)",
                "isLowPredicateRegister(reg2)",
                "insOptsScalableStandard(id.idInsOpt())",
                "isVectorRegister(id.idReg1())",
                "isLowPredicateRegister(id.idReg2())",
                "isVectorRegister(id.idReg3())",
                "isScalableVectorSize(id.idOpSize())"
            ]));
            Assert.That(output, Is.Empty);
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
        });
    }

    [Test]
    public static void DebugDisplayPrintsDescriptorBeforeAppending()
    {
        WithEmitter(emitter =>
        {
            var compiler = JitTls.Compiler ?? throw new AssertionException("No test compiler is installed.");
            compiler.opts.dspCode = true;
            var (output, assertions) = Arm64SveInstructionSanityTests.Capture(() =>
                emitter.emitInsSve_R_R_R(
                    INS_sve_add, EA_SCALABLE, REG_V0, REG_V1, REG_V31, INS_OPTS_SCALABLE_B));

            Assert.That(assertions, Is.Empty);
            Assert.That(output, Does.Contain("z0.b, z1.b, z31.b"));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
            var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
            AssertDescriptor(id, INS_sve_add, IF_SVE_AT_3A, INS_OPTS_SCALABLE_B,
                EA_SCALABLE, REG_V0, REG_V1, REG_V31);
        });
    }
#endif

    private static Emitter.instrDesc Record(Emitter emitter, Action record)
    {
#if DEBUG
        var (output, assertions) = Arm64SveInstructionSanityTests.Capture(record);
        Assert.That(assertions, Is.Empty);
        Assert.That(output, Is.Empty);
#else
        record();
#endif
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
        var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
        Assert.That(id.idCodeSize(), Is.EqualTo(4u));

        return id;
    }

    private static void AssertDescriptor(Emitter.instrDesc id, instruction ins, Emitter.insFormat format,
        insOpts opt, emitAttr attr, regNumber first, regNumber second, regNumber third)
    {
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idInsOpt(), Is.EqualTo(opt));
        Assert.That(id.idOpSize(), Is.EqualTo(attr));
        Assert.That(id.idReg1(), Is.EqualTo(first));
        Assert.That(id.idReg2(), Is.EqualTo(second));
        Assert.That(id.idReg3(), Is.EqualTo(third));
    }

    private static void WithEmitter(Action<Emitter> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            var emitter = new Emitter(codeGen);
            CodeGenEmitter(codeGen) = emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(emitter);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cgEmitter")]
    private static extern ref Emitter CodeGenEmitter(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentInstructions(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidSimm_MultipleOf")]
    private static extern bool SignedMultiple(Emitter? emitter, nint value, int bits, nint multiple);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidUimm_MultipleOf")]
    private static extern bool UnsignedMultiple(Emitter? emitter, nint value, int bits, nuint multiple);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidMovprfxReg")]
    private static extern bool ValidPrefix(Emitter? emitter, insSveMovOpts mopt, regNumber destination,
        regNumber source, regNumber operand2, regNumber operand3, regNumber operand4);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitDecodeRotationImm0_to_270")]
    private static extern nint Rotation(Emitter? emitter, nint encoded);
}
#endif
