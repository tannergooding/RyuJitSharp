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
internal static unsafe class Arm64SveMultioperandRecordingTests
{
    [TestCase(INS_sve_cdot, INS_OPTS_SCALABLE_B, 3, IF_SVE_FA_3A)]
    [TestCase(INS_sve_cdot, INS_OPTS_SCALABLE_H, 1, IF_SVE_FA_3B)]
    [TestCase(INS_sve_cmla, INS_OPTS_SCALABLE_H, 3, IF_SVE_FB_3A)]
    [TestCase(INS_sve_cmla, INS_OPTS_SCALABLE_S, 1, IF_SVE_FB_3B)]
    [TestCase(INS_sve_sqrdcmlah, INS_OPTS_SCALABLE_H, 3, IF_SVE_FC_3A)]
    [TestCase(INS_sve_sqrdcmlah, INS_OPTS_SCALABLE_S, 1, IF_SVE_FC_3B)]
    [TestCase(INS_sve_fcmla, INS_OPTS_SCALABLE_S, 1, IF_SVE_GV_3A)]
    public static void IndexedRotationFormsPackEveryRotationWithoutChangingOptions(
        instruction ins, insOpts opt, int maximumIndex, Emitter.insFormat format)
    {
        foreach (var index in new[] { 0, maximumIndex })
        {
            for (var rotation = 0; rotation < 4; rotation++)
            {
                WithEmitter(emitter =>
                {
                    var id = Record(emitter, () => emitter.emitIns_R_R_R_I_I(
                        ins, EA_SCALABLE, REG_V0, REG_V31, REG_V7, index, rotation, opt));

                    AssertDescriptor(id, ins, format, opt, REG_V0, REG_V31, REG_V7);
                    Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)((index << 2) | rotation)));
                    Assert.That(id.idIsSmallDsc(), Is.False);
                    Assert.That(id.idIsLargeCns(), Is.EqualTo(
                        !Emitter.instrDesc.fitsInSmallCns((index << 2) | rotation)));
                });
            }
        }
    }

    [TestCase(INS_sve_cmple, INS_sve_cmpge, false)]
    [TestCase(INS_sve_cmplo, INS_sve_cmphi, false)]
    [TestCase(INS_sve_cmpls, INS_sve_cmphs, false)]
    [TestCase(INS_sve_cmplt, INS_sve_cmpgt, false)]
    [TestCase(INS_sve_facle, INS_sve_facge, true)]
    [TestCase(INS_sve_faclt, INS_sve_facgt, true)]
    [TestCase(INS_sve_fcmle, INS_sve_fcmge, true)]
    [TestCase(INS_sve_fcmlt, INS_sve_fcmgt, true)]
    public static void ComparisonAliasesExchangeOnlyTheComparedOperands(
        instruction ins, instruction expected, bool floating)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_R_R(
                ins, EA_SCALABLE, REG_P15, REG_P7, REG_V0, REG_V31, INS_OPTS_SCALABLE_S));

            AssertDescriptor(id, expected, floating ? IF_SVE_HT_4A : IF_SVE_CX_4A,
                INS_OPTS_SCALABLE_S, REG_P15, REG_P7, REG_V31, REG_V0);
            Assert.That(id.idIsSmallDsc(), Is.False);
        });
    }

    [TestCase(INS_sve_cmpeq, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_WIDE, IF_SVE_CX_4A_A)]
    [TestCase(INS_sve_cmpne, INS_OPTS_SCALABLE_D, INS_SCALABLE_OPTS_NONE, IF_SVE_CX_4A)]
    [TestCase(INS_sve_fcmeq, INS_OPTS_SCALABLE_H, INS_SCALABLE_OPTS_NONE, IF_SVE_HT_4A)]
    [TestCase(INS_sve_fcmuo, INS_OPTS_SCALABLE_D, INS_SCALABLE_OPTS_NONE, IF_SVE_HT_4A)]
    [TestCase(INS_sve_match, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_NONE, IF_SVE_GE_4A)]
    [TestCase(INS_sve_nmatch, INS_OPTS_SCALABLE_H, INS_SCALABLE_OPTS_NONE, IF_SVE_GE_4A)]
    public static void PredicateDestinationFormsRetainWideAndFloatingFormats(
        instruction ins, insOpts opt, insScalableOpts sopt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_P15, REG_P7, REG_V0, REG_V31, opt, sopt));

            AssertDescriptor(id, ins, format, opt, REG_P15, REG_P7, REG_V0, REG_V31);
        });
    }

    [TestCase(INS_sve_and, IF_SVE_CZ_4A)]
    [TestCase(INS_sve_bic, IF_SVE_CZ_4A)]
    [TestCase(INS_sve_orr, IF_SVE_CZ_4A)]
    [TestCase(INS_sve_nands, IF_SVE_CZ_4A)]
    [TestCase(INS_sve_orns, IF_SVE_CZ_4A)]
    [TestCase(INS_sve_brkpa, IF_SVE_DA_4A)]
    [TestCase(INS_sve_brkpbs, IF_SVE_DA_4A)]
    public static void PredicateLogicalFormsRetainAllFourRegisters(instruction ins, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_P15, REG_P0, REG_P7, REG_P8, INS_OPTS_SCALABLE_B));

            AssertDescriptor(id, ins, format, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, REG_P7, REG_P8);
        });
    }

    [TestCase(INS_sve_mla, INS_OPTS_SCALABLE_B, IF_SVE_AR_4A)]
    [TestCase(INS_sve_mls, INS_OPTS_SCALABLE_D, IF_SVE_AR_4A)]
    [TestCase(INS_sve_mad, INS_OPTS_SCALABLE_H, IF_SVE_AS_4A)]
    [TestCase(INS_sve_msb, INS_OPTS_SCALABLE_S, IF_SVE_AS_4A)]
    [TestCase(INS_sve_histcnt, INS_OPTS_SCALABLE_S, IF_SVE_GI_4A)]
    [TestCase(INS_sve_fmla, INS_OPTS_SCALABLE_H, IF_SVE_HU_4A)]
    [TestCase(INS_sve_fnmls, INS_OPTS_SCALABLE_D, IF_SVE_HU_4A)]
    [TestCase(INS_sve_bfmls, INS_OPTS_SCALABLE_H, IF_SVE_HU_4B)]
    [TestCase(INS_sve_fnmsb, INS_OPTS_SCALABLE_S, IF_SVE_HV_4A)]
    public static void AccumulatingFormsRetainOperandOrder(instruction ins, insOpts opt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31, opt));

            AssertDescriptor(id, ins, format, opt, REG_V0, REG_P7, REG_V1, REG_V31);
        });
    }

    [Test]
    public static void VectorSelectUsesMovOnlyWhenDestinationMatchesFinalSource()
    {
        foreach (var aliases in new[] { false, true })
        {
            WithEmitter(emitter =>
            {
                var fourth = aliases ? REG_V0 : REG_V31;
                var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                    INS_sve_sel, EA_SCALABLE, REG_V0, REG_P15, REG_V1, fourth, INS_OPTS_SCALABLE_H));

                AssertDescriptor(id, aliases ? INS_sve_mov : INS_sve_sel, IF_SVE_CW_4A,
                    INS_OPTS_SCALABLE_H, REG_V0, REG_P15, REG_V1);
                if (!aliases)
                {
                    Assert.That(id.idReg4(), Is.EqualTo(fourth));
                }
            });
        }
    }

    [TestCase(INS_OPTS_SCALABLE_B)]
    [TestCase(INS_OPTS_SCALABLE_H)]
    [TestCase(INS_OPTS_SCALABLE_S)]
    [TestCase(INS_OPTS_SCALABLE_D)]
    [TestCase(INS_OPTS_SCALABLE_Q)]
    public static void PredicateSelectForcesByteEncodingWithoutChangingRegisters(insOpts opt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                INS_sve_sel, EA_SCALABLE, REG_P15, REG_P0, REG_P7, REG_P8, opt));

            AssertDescriptor(id, INS_sve_sel, IF_SVE_CZ_4A, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, REG_P7, REG_P8);
        });
    }

    [TestCase(INS_sve_st1b, INS_OPTS_SCALABLE_B, REG_R30, INS_SCALABLE_OPTS_NONE, IF_SVE_JD_4A)]
    [TestCase(INS_sve_st1b, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JK_4B)]
    [TestCase(INS_sve_st1b, INS_OPTS_SCALABLE_S_SXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JK_4A_B)]
    [TestCase(INS_sve_st1b, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JK_4A)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_H, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JD_4A)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JJ_4B)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JJ_4B_E)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_S_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JJ_4A_D)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_S_SXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_JJ_4A)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JJ_4A_C)]
    [TestCase(INS_sve_st1h, INS_OPTS_SCALABLE_D_SXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_JJ_4A_B)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_S, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JD_4B)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_Q, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JD_4C)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JJ_4B_E)]
    [TestCase(INS_sve_st1w, INS_OPTS_SCALABLE_S_SXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_JJ_4A)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_Q, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JD_4C_A)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_D, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JD_4C)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JJ_4B_C)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_D_SXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_JJ_4A)]
    [TestCase(INS_sve_st1d, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_JJ_4A_B)]
    [TestCase(INS_sve_ld1b, INS_OPTS_SCALABLE_B, REG_R30, INS_SCALABLE_OPTS_NONE, IF_SVE_IK_4A_H)]
    [TestCase(INS_sve_ldff1b, INS_OPTS_SCALABLE_D, REG_ZR, INS_SCALABLE_OPTS_NONE, IF_SVE_IG_4A_E)]
    [TestCase(INS_sve_ld1sb, INS_OPTS_SCALABLE_H, REG_R30, INS_SCALABLE_OPTS_NONE, IF_SVE_IK_4A_F)]
    [TestCase(INS_sve_ldff1sb, INS_OPTS_SCALABLE_S, REG_ZR, INS_SCALABLE_OPTS_NONE, IF_SVE_IG_4A_D)]
    [TestCase(INS_sve_ld1b, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_HW_4A)]
    [TestCase(INS_sve_ld1b, INS_OPTS_SCALABLE_S_SXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_HW_4A_A)]
    [TestCase(INS_sve_ld1sb, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_HW_4B)]
    [TestCase(INS_sve_ld1h, INS_OPTS_SCALABLE_H, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IK_4A_I)]
    [TestCase(INS_sve_ld1sh, INS_OPTS_SCALABLE_D, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IK_4A_G)]
    [TestCase(INS_sve_ld1w, INS_OPTS_SCALABLE_Q, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_II_4A_H)]
    [TestCase(INS_sve_ldff1h, INS_OPTS_SCALABLE_H, REG_ZR, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IG_4A_G)]
    [TestCase(INS_sve_ldff1w, INS_OPTS_SCALABLE_S, REG_ZR, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IG_4A_F)]
    [TestCase(INS_sve_ld1h, INS_OPTS_SCALABLE_D_SXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_HW_4A_A)]
    [TestCase(INS_sve_ld1h, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_HW_4A_B)]
    [TestCase(INS_sve_ld1w, INS_OPTS_SCALABLE_S_UXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_HW_4A)]
    [TestCase(INS_sve_ld1w, INS_OPTS_SCALABLE_S_SXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_HW_4A_C)]
    [TestCase(INS_sve_ld1sh, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_LSL_N, IF_SVE_HW_4B)]
    [TestCase(INS_sve_ld1w, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_HW_4B_D)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_Q, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_II_4A_B)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_II_4A)]
    [TestCase(INS_sve_ld1sw, INS_OPTS_SCALABLE_D, REG_R30, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IK_4A)]
    [TestCase(INS_sve_ldff1d, INS_OPTS_SCALABLE_D, REG_ZR, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IG_4A)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_MOD_N, IF_SVE_IU_4A)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D_SXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_IU_4A_C)]
    [TestCase(INS_sve_ld1sw, INS_OPTS_SCALABLE_D_UXTW, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_IU_4A_A)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IU_4B)]
    [TestCase(INS_sve_ld1d, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_IU_4B_D)]
    [TestCase(INS_sve_ld1sw, INS_OPTS_SCALABLE_D, REG_V31, INS_SCALABLE_OPTS_NONE, IF_SVE_IU_4B_B)]
    public static void MemoryAddressFormsRetainRegisterBanksExtensionsAndScaling(
        instruction ins, insOpts opt, regNumber fourth, insScalableOpts sopt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_P7, REG_R0, fourth, opt, sopt));

            AssertDescriptor(id, ins, format, opt, REG_V0, REG_P7, REG_R0, fourth);
            Assert.That(id.idIsSmallDsc(), Is.False);
        });
    }

    [TestCase(INS_sve_ldnt1b, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_NONE, IF_SVE_IN_4A)]
    [TestCase(INS_sve_ldnt1h, INS_OPTS_SCALABLE_H, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IN_4A)]
    [TestCase(INS_sve_ld1rqb, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_NONE, IF_SVE_IP_4A)]
    [TestCase(INS_sve_ld1rod, INS_OPTS_SCALABLE_D, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IP_4A)]
    [TestCase(INS_sve_ld3q, INS_OPTS_SCALABLE_Q, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IR_4A)]
    [TestCase(INS_sve_ld4b, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_NONE, IF_SVE_IT_4A)]
    [TestCase(INS_sve_ld2d, INS_OPTS_SCALABLE_D, INS_SCALABLE_OPTS_LSL_N, IF_SVE_IT_4A)]
    [TestCase(INS_sve_stnt1b, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_NONE, IF_SVE_JB_4A)]
    [TestCase(INS_sve_stnt1w, INS_OPTS_SCALABLE_S, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JB_4A)]
    [TestCase(INS_sve_st3h, INS_OPTS_SCALABLE_H, INS_SCALABLE_OPTS_LSL_N, IF_SVE_JC_4A)]
    [TestCase(INS_sve_st4q, INS_OPTS_SCALABLE_Q, INS_SCALABLE_OPTS_NONE, IF_SVE_JF_4A)]
    public static void StructureAndNonTemporalScalarAddressesKeepFourthRegister(
        instruction ins, insOpts opt, insScalableOpts sopt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_P15, REG_R0, REG_R30, opt, sopt));

            AssertDescriptor(id, ins, format, opt, REG_V0, REG_P15, REG_R0, REG_R30);
        });
    }

    [TestCase(INS_sve_ld1q, INS_OPTS_SCALABLE_Q, IF_SVE_IW_4A)]
    [TestCase(INS_sve_st1q, INS_OPTS_SCALABLE_Q, IF_SVE_IY_4A)]
    [TestCase(INS_sve_ldnt1sw, INS_OPTS_SCALABLE_D, IF_SVE_IX_4A)]
    [TestCase(INS_sve_ldnt1b, INS_OPTS_SCALABLE_S, IF_SVE_IF_4A)]
    [TestCase(INS_sve_ldnt1sh, INS_OPTS_SCALABLE_D, IF_SVE_IF_4A_A)]
    [TestCase(INS_sve_stnt1b, INS_OPTS_SCALABLE_S, IF_SVE_IZ_4A)]
    [TestCase(INS_sve_stnt1h, INS_OPTS_SCALABLE_D, IF_SVE_IZ_4A_A)]
    [TestCase(INS_sve_stnt1d, INS_OPTS_SCALABLE_D, IF_SVE_JA_4A)]
    public static void VectorBaseMemoryFormsPreserveZeroRegister(
        instruction ins, insOpts opt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_P15, REG_V31, REG_ZR, opt));

            AssertDescriptor(id, ins, format, opt, REG_V0, REG_P15, REG_V31, REG_ZR);
        });
    }

    [TestCase(INS_sve_add, INS_OPTS_SCALABLE_B, IF_SVE_AA_3A)]
    [TestCase(INS_sve_and, INS_OPTS_SCALABLE_D, IF_SVE_AA_3A)]
    [TestCase(INS_sve_abs, INS_OPTS_SCALABLE_H, IF_SVE_AQ_3A)]
    [TestCase(INS_sve_frintn, INS_OPTS_SCALABLE_S, IF_SVE_HQ_3A)]
    [TestCase(INS_sve_fdiv, INS_OPTS_SCALABLE_D, IF_SVE_HL_3A)]
    [TestCase(INS_sve_faddp, INS_OPTS_SCALABLE_H, IF_SVE_GR_3A)]
    [TestCase(INS_sve_splice, INS_OPTS_SCALABLE_B, IF_SVE_CV_3B)]
    [TestCase(INS_sve_fcvt, INS_OPTS_S_TO_D, IF_SVE_HO_3B)]
    [TestCase(INS_sve_fcvtnt, INS_OPTS_S_TO_H, IF_SVE_GQ_3A)]
    public static void EmbeddedRmwElidesPrefixButRetainsRealOperandRecorder(
        instruction ins, insOpts opt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_P7, REG_V0, REG_V31, opt));

            AssertDescriptor(id, ins, format, opt, REG_V0, REG_P7, REG_V31);
        });
    }

    [TestCase(INS_SVE_MOV_OPTS_ZEROING)]
    [TestCase(INS_SVE_MOV_OPTS_MERGING)]
    public static void PredicatedRmwRecordsPrefixBeforeArithmetic(insSveMovOpts mopt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                INS_sve_add, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31,
                INS_OPTS_SCALABLE_S, mopt: mopt), 8);

            AssertDescriptor(id, INS_sve_add, IF_SVE_AA_3A, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31);
        });
    }

    [TestCase(INS_SVE_MOV_OPTS_ZEROING)]
    [TestCase(INS_SVE_MOV_OPTS_MERGING)]
    public static void EmbeddedImmediateRmwRecordsPrefixAndIgnoresScalableOptions(insSveMovOpts mopt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R_I(
                INS_sve_fcadd, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31, 1,
                INS_OPTS_SCALABLE_S, INS_SCALABLE_OPTS_WIDE, mopt), 8);

            AssertDescriptor(id, INS_sve_fcadd, IF_SVE_GP_3A, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)1));
        });
    }

    [TestCase(INS_sve_sdot, INS_OPTS_SCALABLE_S, IF_SVE_EH_3A)]
    [TestCase(INS_sve_sbclt, INS_OPTS_SCALABLE_D, IF_SVE_FY_3A)]
    [TestCase(INS_sve_bsl, INS_OPTS_SCALABLE_D, IF_SVE_AV_3A)]
    public static void UnmaskedRmwUsesSecondRegisterForElisionAndFinalTwoSources(
        instruction ins, insOpts opt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                ins, EA_SCALABLE, REG_V0, REG_V0, REG_V1, REG_V31, opt));

            AssertDescriptor(id, ins, format, opt, REG_V0, REG_V1, REG_V31);
        });
    }

    [Test]
    public static void CarryAddUsesFourthRegisterForElisionAndPreservesMiddleSources()
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                INS_sve_adclt, EA_SCALABLE, REG_V0, REG_V1, REG_V31, REG_V0, INS_OPTS_SCALABLE_D));

            AssertDescriptor(id, INS_sve_adclt, IF_SVE_FY_3A, INS_OPTS_SCALABLE_D, REG_V0, REG_V1, REG_V31);
        });
    }

    [Test]
    public static void PredicateBreakUsesFourthRegisterAsRmwSource()
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                INS_sve_brkn, EA_SCALABLE, REG_P15, REG_P0, REG_P7, REG_P15, INS_OPTS_SCALABLE_B));

            AssertDescriptor(id, INS_sve_brkn, IF_SVE_DC_3A, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, REG_P7);
        });
    }

    [TestCase(INS_OPTS_SCALABLE_H, 0)]
    [TestCase(INS_OPTS_SCALABLE_S, 3)]
    [TestCase(INS_OPTS_SCALABLE_D, 2)]
    public static void FourRegisterComplexMultiplyRetainsEncodedRotation(insOpts opt, int rotation)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_R_R_I(
                INS_sve_fcmla, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31, rotation, opt));

            AssertDescriptor(id, INS_sve_fcmla, IF_SVE_GT_4A, opt, REG_V0, REG_P7, REG_V1, REG_V31);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)rotation));
            Assert.That(id.idIsSmallDsc(), Is.False);
        });
    }

    [TestCase(INS_sve_fmul, INS_OPTS_SCALABLE_D, 1, IF_SVE_GX_3B)]
    [TestCase(INS_sve_fmla, INS_OPTS_SCALABLE_S, 3, IF_SVE_GU_3A)]
    [TestCase(INS_sve_mla, INS_OPTS_SCALABLE_H, 7, IF_SVE_FF_3A)]
    [TestCase(INS_sve_sdot, INS_OPTS_SCALABLE_D, 1, IF_SVE_EY_3B)]
    public static void IndexedRmwRetainsImmediateAndDelegatedOptionMutation(
        instruction ins, insOpts opt, int immediate, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R_I(
                ins, EA_SCALABLE, REG_V0, REG_V0, REG_V31, REG_V7, immediate, opt));

            var expectedOpt = ins == INS_sve_sdot ? INS_OPTS_SCALABLE_H : opt;
            AssertDescriptor(id, ins, format, expectedOpt, REG_V0, REG_V31, REG_V7);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)immediate));
        });
    }

    [Test]
    public static void EmbeddedComplexAddDelegatesImmediateAfterElidingPrefix()
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R_I(
                INS_sve_fcadd, EA_SCALABLE, REG_V0, REG_P7, REG_V0, REG_V31, 1, INS_OPTS_SCALABLE_S));

            AssertDescriptor(id, INS_sve_fcadd, IF_SVE_GP_3A, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31);
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)1));
        });
    }

    [TestCase(INS_sve_tbx)]
    [TestCase(INS_sve_addhnt)]
    public static void UnportedMoveDependencyTerminatesWithoutDroppingRmwBranch(instruction ins)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                emitter.emitInsSve_R_R_R_R(
                    ins, EA_SCALABLE, REG_V0, REG_V0, REG_V1, REG_V31, INS_OPTS_SCALABLE_D);
            }
#if DEBUG
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
                () => Assert.Throws<FatalJitException>(() => RecordOperands()));
            Assert.That(assertions, Is.Empty);
#else
            Assert.Throws<FatalJitException>(() => RecordOperands());
#endif
            Assert.That(GroupSize(emitter), Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RequiredUnpredicatedPrefixPrecedesRealOperandRecorder(bool immediate)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                if (immediate)
                {
                    emitter.emitInsSve_R_R_R_R_I(INS_sve_sdot, EA_SCALABLE,
                        REG_V0, REG_V1, REG_V31, REG_V7, 1, INS_OPTS_SCALABLE_D);
                }
                else
                {
                    emitter.emitInsSve_R_R_R_R(INS_sve_add, EA_SCALABLE,
                        REG_V0, REG_P7, REG_V1, REG_V31, INS_OPTS_SCALABLE_S);
                }
            }

            var id = Record(emitter, RecordOperands, 8);
            var instructions = CurrentInstructions(emitter)
                ?? throw new AssertionException("No descriptor buffer was prepared.");
            Assert.That(instructions.Count, Is.EqualTo(2));
            Assert.That(instructions[1], Is.SameAs(id));
            var prefix = instructions[0];
            Assert.That(prefix.idIns(), Is.EqualTo(INS_sve_movprfx));
            Assert.That(prefix.idInsFmt(), Is.EqualTo(IF_SVE_BI_2A));
            Assert.That(prefix.idInsOpt(), Is.EqualTo(INS_OPTS_NONE));
            Assert.That(prefix.idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(prefix.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(prefix.idReg2(), Is.EqualTo(REG_V1));
            Assert.That(prefix.idCodeSize(), Is.EqualTo(4u));
            Assert.That(prefix.idIsSmallDsc(), Is.True);
            Assert.That(id.idIsSmallDsc(), Is.False);
            if (immediate)
            {
                AssertDescriptor(id, INS_sve_sdot, IF_SVE_EY_3B, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, REG_V7);
                Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)1));
            }
            else
            {
                AssertDescriptor(id, INS_sve_add, IF_SVE_AA_3A, INS_OPTS_SCALABLE_S, REG_V0, REG_P7, REG_V31);
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void UnsupportedDispatchersTerminateBeforeDescriptorAllocation(int recorder)
    {
        WithEmitter(emitter =>
        {
            void RecordOperands()
            {
                switch (recorder)
                {
                    case 0:
                    {
                        emitter.emitInsSve_R_R_R_I_I(INS_nop, EA_SCALABLE, REG_V0, REG_V1, REG_V7, 0, 0,
                            INS_OPTS_SCALABLE_S);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitInsSve_R_R_R_R(INS_nop, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31,
                            INS_OPTS_SCALABLE_S);
                        break;
                    }

                    default:
                    {
                        emitter.emitInsSve_R_R_R_R_I(INS_sve_psel, EA_SCALABLE, REG_P0, REG_P1, REG_P2, REG_R12,
                            0, INS_OPTS_SCALABLE_B);
                        break;
                    }
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

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void DisplayFailureOccursAfterDescriptorFieldsButBeforeAppend(int recorder)
    {
        WithEmitter(emitter =>
        {
            var compiler = JitTls.Compiler ?? throw new AssertionException("No test compiler is installed.");
            compiler.opts.dspCode = true;
            void RecordOperands()
            {
                switch (recorder)
                {
                    case 0:
                    {
                        emitter.emitInsSve_R_R_R_I_I(INS_sve_cdot, EA_SCALABLE,
                            REG_V0, REG_V31, REG_V7, 3, 2, INS_OPTS_SCALABLE_B);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitInsSve_R_R_R_R(INS_sve_cmple, EA_SCALABLE,
                            REG_P15, REG_P7, REG_V0, REG_V31, INS_OPTS_SCALABLE_S);
                        break;
                    }

                    default:
                    {
                        emitter.emitInsSve_R_R_R_R_I(INS_sve_fcmla, EA_SCALABLE,
                            REG_V0, REG_P7, REG_V1, REG_V31, 3, INS_OPTS_SCALABLE_D);
                        break;
                    }
                }
            }
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
                () => Assert.Throws<FatalJitException>(() => RecordOperands()));
            Assert.That(assertions, Is.Empty);
            Assert.That(GroupSize(emitter), Is.Zero);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
            switch (recorder)
            {
                case 0:
                {
                    AssertDescriptor(id, INS_sve_cdot, IF_SVE_FA_3A, INS_OPTS_SCALABLE_B, REG_V0, REG_V31, REG_V7);
                    Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)14));
                    break;
                }

                case 1:
                {
                    AssertDescriptor(id, INS_sve_cmpge, IF_SVE_CX_4A, INS_OPTS_SCALABLE_S,
                        REG_P15, REG_P7, REG_V31, REG_V0);
                    break;
                }

                default:
                {
                    AssertDescriptor(id, INS_sve_fcmla, IF_SVE_GT_4A, INS_OPTS_SCALABLE_D,
                        REG_V0, REG_P7, REG_V1, REG_V31);
                    Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)3));
                    break;
                }
            }
        });
    }

    [Test]
    public static void ContinuingEeRetainsRecorderThenDescriptorImmediateAssertions()
    {
        WithEmitter(emitter =>
        {
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() => emitter.emitInsSve_R_R_R_I_I(
                INS_sve_cdot, EA_SCALABLE, REG_V0, REG_V31, REG_V7, 4, 0, INS_OPTS_SCALABLE_B));

            Assert.That(assertions, Is.EqualTo<string[]>([
                "isValidUimm(imm1, 2)",
                "isValidUimm(emitGetInsSC(id), 4)"
            ]));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
            Assert.That(Emitter.emitGetInsSC(LastInstruction(emitter)
                ?? throw new AssertionException("No descriptor was prepared.")), Is.EqualTo((nint)16));
        });
    }

    [Test]
    public static void ContinuingEePreservesNativeScatterOptionSanityMismatch()
    {
        WithEmitter(emitter =>
        {
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() => emitter.emitInsSve_R_R_R_R(
                INS_sve_st1b, EA_SCALABLE, REG_V0, REG_P7, REG_R0, REG_V31, INS_OPTS_SCALABLE_B));

            Assert.That(assertions, Is.EqualTo<string[]>(["id.idInsOpt() == INS_OPTS_SCALABLE_D"]));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
            var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
            AssertDescriptor(id, INS_sve_st1b, IF_SVE_JK_4B, INS_OPTS_SCALABLE_B, REG_V0, REG_P7, REG_R0, REG_V31);
        });
    }

    [TestCase(INS_OPTS_SCALABLE_D, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_OPTS_H_TO_D, INS_OPTS_SCALABLE_D)]
    [TestCase(INS_OPTS_D_TO_S, INS_OPTS_SCALABLE_S)]
    public static void ConversionPrefixUsesDestinationElementSizeBeforeDisplayFailure(insOpts opt, insOpts prefixOpt)
    {
        WithEmitter(emitter =>
        {
            var compiler = JitTls.Compiler ?? throw new AssertionException("No test compiler is installed.");
            compiler.opts.dspCode = true;
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() =>
                Assert.Throws<FatalJitException>(() => emitter.emitInsSve_R_R_R_R(
                    INS_sve_fcvt, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31,
                    opt, mopt: INS_SVE_MOV_OPTS_MERGING)));

            Assert.That(assertions, Is.Empty);
            Assert.That(GroupSize(emitter), Is.Zero);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No prefix descriptor was prepared.");
            AssertDescriptor(id, INS_sve_movprfx, IF_SVE_AH_3A, prefixOpt, REG_V0, REG_P7, REG_V1);
            Assert.That(id.idPredicateReg2Merge(), Is.True);
        });
    }
#endif

    private static Emitter.instrDesc Record(Emitter emitter, Action record, int expectedSize = 4)
    {
#if DEBUG
        var (output, assertions) = Arm64SveInstructionSanityTests.Capture(record);
        Assert.That(assertions, Is.Empty);
        Assert.That(output, Is.Empty);
#else
        record();
#endif
        Assert.That(GroupSize(emitter), Is.EqualTo(expectedSize));
        var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
        Assert.That(id.idCodeSize(), Is.EqualTo(4u));

        return id;
    }

    private static void AssertDescriptor(Emitter.instrDesc id, instruction ins, Emitter.insFormat format,
        insOpts opt, regNumber first, regNumber second, regNumber third, regNumber? fourth = null)
    {
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idInsOpt(), Is.EqualTo(opt));
        Assert.That(id.idOpSize(), Is.EqualTo(EA_SCALABLE));
        Assert.That(id.idReg1(), Is.EqualTo(first));
        Assert.That(id.idReg2(), Is.EqualTo(second));
        Assert.That(id.idReg3(), Is.EqualTo(third));
        if (fourth.HasValue)
        {
            Assert.That(id.idReg4(), Is.EqualTo(fourth.Value));
        }
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
}
#endif
