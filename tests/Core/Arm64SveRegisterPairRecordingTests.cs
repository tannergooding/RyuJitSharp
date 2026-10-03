// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64SveRegisterPairRecordingTests
{
    [TestCase(INS_sve_mov, INS_OPTS_SCALABLE_B, REG_V0, REG_V31, IF_SVE_AU_3A)]
    [TestCase(INS_sve_mov, INS_OPTS_SCALABLE_Q, REG_V0, REG_V31, IF_SVE_AU_3A)]
    [TestCase(INS_sve_mov, INS_OPTS_SCALABLE_B, REG_P0, REG_P15, IF_SVE_CZ_4A_L)]
    [TestCase(INS_sve_movs, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, IF_SVE_CZ_4A_A)]
    [TestCase(INS_sve_pmov, INS_OPTS_SCALABLE_B, REG_P15, REG_V31, IF_SVE_CE_2A)]
    [TestCase(INS_sve_pmov, INS_OPTS_SCALABLE_B, REG_V31, REG_P15, IF_SVE_CF_2A)]
    [TestCase(INS_sve_insr, INS_OPTS_SCALABLE_D, REG_V0, REG_V31, IF_SVE_CC_2A)]
    [TestCase(INS_sve_insr, INS_OPTS_SCALABLE_B, REG_V0, REG_ZR, IF_SVE_CD_2A)]
    [TestCase(INS_sve_pfirst, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, IF_SVE_DD_2A)]
    [TestCase(INS_sve_pnext, INS_OPTS_SCALABLE_D, REG_P15, REG_P0, IF_SVE_DF_2A)]
    [TestCase(INS_sve_punpkhi, INS_OPTS_NONE, REG_P15, REG_P0, IF_SVE_CK_2A)]
    [TestCase(INS_sve_punpklo, INS_OPTS_NONE, REG_P0, REG_P15, IF_SVE_CK_2A)]
    [TestCase(INS_sve_rdffr, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, IF_SVE_DG_2A)]
    [TestCase(INS_sve_rdffrs, INS_OPTS_SCALABLE_B, REG_P0, REG_P15, IF_SVE_DG_2A)]
    [TestCase(INS_sve_rev, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, IF_SVE_CG_2A)]
    [TestCase(INS_sve_rev, INS_OPTS_SCALABLE_S, REG_P0, REG_P15, IF_SVE_CJ_2A)]
    [TestCase(INS_sve_ptest, INS_OPTS_SCALABLE_B, REG_P15, REG_P0, IF_SVE_DI_2A)]
    [TestCase(INS_sve_incp, INS_OPTS_SCALABLE_H, REG_V0, REG_P15, IF_SVE_DN_2A)]
    [TestCase(INS_sve_decp, INS_OPTS_SCALABLE_D, REG_V31, REG_P0, IF_SVE_DN_2A)]
    [TestCase(INS_sve_sqincp, INS_OPTS_SCALABLE_H, REG_V0, REG_P15, IF_SVE_DP_2A)]
    [TestCase(INS_sve_uqincp, INS_OPTS_SCALABLE_S, REG_V31, REG_P0, IF_SVE_DP_2A)]
    [TestCase(INS_sve_sqdecp, INS_OPTS_SCALABLE_D, REG_V0, REG_P15, IF_SVE_DP_2A)]
    [TestCase(INS_sve_uqdecp, INS_OPTS_SCALABLE_H, REG_V31, REG_P0, IF_SVE_DP_2A)]
    [TestCase(INS_sve_sqcvtn, INS_OPTS_NONE, REG_V31, REG_V0, IF_SVE_FZ_2A)]
    [TestCase(INS_sve_uqcvtn, INS_OPTS_NONE, REG_V0, REG_V30, IF_SVE_FZ_2A)]
    [TestCase(INS_sve_sqcvtun, INS_OPTS_NONE, REG_V31, REG_V30, IF_SVE_FZ_2A)]
    [TestCase(INS_sve_sqxtnb, INS_OPTS_SCALABLE_B, REG_V0, REG_V31, IF_SVE_GD_2A)]
    [TestCase(INS_sve_sqxtnt, INS_OPTS_SCALABLE_H, REG_V31, REG_V0, IF_SVE_GD_2A)]
    [TestCase(INS_sve_uqxtnb, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, IF_SVE_GD_2A)]
    [TestCase(INS_sve_uqxtnt, INS_OPTS_SCALABLE_B, REG_V31, REG_V0, IF_SVE_GD_2A)]
    [TestCase(INS_sve_sqxtunb, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, IF_SVE_GD_2A)]
    [TestCase(INS_sve_sqxtunt, INS_OPTS_SCALABLE_S, REG_V31, REG_V0, IF_SVE_GD_2A)]
    [TestCase(INS_sve_aese, INS_OPTS_SCALABLE_B, REG_V0, REG_V31, IF_SVE_GK_2A)]
    [TestCase(INS_sve_aesd, INS_OPTS_SCALABLE_B, REG_V31, REG_V0, IF_SVE_GK_2A)]
    [TestCase(INS_sve_sm4e, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, IF_SVE_GK_2A)]
    [TestCase(INS_sve_frecpe, INS_OPTS_SCALABLE_H, REG_V0, REG_V31, IF_SVE_HF_2A)]
    [TestCase(INS_sve_frsqrte, INS_OPTS_SCALABLE_D, REG_V31, REG_V0, IF_SVE_HF_2A)]
    [TestCase(INS_sve_sunpkhi, INS_OPTS_SCALABLE_B, REG_V0, REG_V31, IF_SVE_CH_2A)]
    [TestCase(INS_sve_sunpklo, INS_OPTS_SCALABLE_H, REG_V31, REG_V0, IF_SVE_CH_2A)]
    [TestCase(INS_sve_uunpkhi, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, IF_SVE_CH_2A)]
    [TestCase(INS_sve_uunpklo, INS_OPTS_SCALABLE_B, REG_V31, REG_V0, IF_SVE_CH_2A)]
    [TestCase(INS_sve_fexpa, INS_OPTS_SCALABLE_S, REG_V0, REG_V31, IF_SVE_BJ_2A)]
    [TestCase(INS_sve_movprfx, INS_OPTS_NONE, REG_V0, REG_V31, IF_SVE_BI_2A)]
    public static void RegisterFormsPreserveFormatBanksOptionsAndSmallDescriptors(
        instruction ins, insOpts opt, regNumber first, regNumber second, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R(ins, EA_SCALABLE, first, second, opt));
            AssertDescriptor(id, ins, EA_SCALABLE, format, opt, first, second);
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_sve_mov, INS_OPTS_SCALABLE_B, IF_SVE_AU_3A)]
    [TestCase(INS_sve_movprfx, INS_OPTS_NONE, IF_SVE_BI_2A)]
    public static void GenericMoveRecordingDispatchesUnpredicatedSVEForms(
        instruction ins, insOpts opt, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_Mov(ins, EA_SCALABLE, REG_V0, REG_V31, false, opt));
            AssertDescriptor(id, ins, EA_SCALABLE, format, opt, REG_V0, REG_V31);
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_sve_mov, EA_4BYTE, INS_OPTS_SCALABLE_B, REG_R0)]
    [TestCase(INS_sve_mov, EA_4BYTE, INS_OPTS_SCALABLE_H, REG_SP)]
    [TestCase(INS_sve_dup, EA_4BYTE, INS_OPTS_SCALABLE_S, REG_R30)]
    [TestCase(INS_sve_dup, EA_8BYTE, INS_OPTS_SCALABLE_D, REG_SP)]
    public static void GeneralRegisterBroadcastSelectsMovAliasAndEncodesStackPointer(
        instruction ins, emitAttr attr, insOpts opt, regNumber second)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R(ins, attr, REG_V31, second, opt));
            AssertDescriptor(id, INS_sve_mov, attr, IF_SVE_CB_2A, opt, REG_V31,
                second == REG_SP ? REG_ZR : second);
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_sve_incp, EA_8BYTE, IF_SVE_DM_2A)]
    [TestCase(INS_sve_decp, EA_8BYTE, IF_SVE_DM_2A)]
    [TestCase(INS_sve_sqincp, EA_4BYTE, IF_SVE_DO_2A)]
    [TestCase(INS_sve_uqincp, EA_8BYTE, IF_SVE_DO_2A)]
    [TestCase(INS_sve_sqdecp, EA_8BYTE, IF_SVE_DO_2A)]
    [TestCase(INS_sve_uqdecp, EA_4BYTE, IF_SVE_DO_2A)]
    [TestCase(INS_sve_ctermeq, EA_4BYTE, IF_SVE_DS_2A)]
    [TestCase(INS_sve_ctermne, EA_8BYTE, IF_SVE_DS_2A)]
    public static void ScalarRegisterFormsRetainGeneralWidth(instruction ins, emitAttr attr, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var conditional = ins is INS_sve_ctermeq or INS_sve_ctermne;
            var opt = conditional ? INS_OPTS_NONE : INS_OPTS_SCALABLE_H;
            var second = conditional ? REG_R30 : REG_P15;
            var id = Record(emitter, () => emitter.emitInsSve_R_R(ins, attr, REG_R0, second, opt));
            AssertDescriptor(id, ins, attr, format, opt, REG_R0, second);
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_VL_2X, false)]
    [TestCase(INS_OPTS_SCALABLE_H, INS_SCALABLE_OPTS_VL_4X, true)]
    [TestCase(INS_OPTS_SCALABLE_S, INS_SCALABLE_OPTS_VL_2X, false)]
    [TestCase(INS_OPTS_SCALABLE_D, INS_SCALABLE_OPTS_VL_4X, true)]
    public static void VectorLengthCountUsesNormalDescriptorAndRetainsLengthBit(
        insOpts opt, insScalableOpts sopt, bool fourTimes)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R(
                INS_sve_cntp, EA_SCALABLE, REG_R30, REG_P15, opt, sopt));
            AssertDescriptor(id, INS_sve_cntp, EA_SCALABLE, IF_SVE_DL_2A, opt, REG_R30, REG_P15);
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(id.idVectorLength4x(), Is.EqualTo(fourTimes));
        });
    }

    [TestCase(INS_OPTS_SCALABLE_H, true, IF_SVE_CE_2C)]
    [TestCase(INS_OPTS_SCALABLE_S, true, IF_SVE_CE_2D)]
    [TestCase(INS_OPTS_SCALABLE_D, true, IF_SVE_CE_2B)]
    [TestCase(INS_OPTS_SCALABLE_H, false, IF_SVE_CF_2C)]
    [TestCase(INS_OPTS_SCALABLE_S, false, IF_SVE_CF_2D)]
    [TestCase(INS_OPTS_SCALABLE_D, false, IF_SVE_CF_2B)]
    public static void NonBytePredicateMovesDelegateToZeroImmediateForm(
        insOpts opt, bool predicateDestination, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var first = predicateDestination ? REG_P15 : REG_V31;
            var second = predicateDestination ? REG_V31 : REG_P15;
            var id = Record(emitter, () => emitter.emitInsSve_R_R(INS_sve_pmov, EA_SCALABLE, first, second, opt));
            AssertDescriptor(id, INS_sve_pmov, EA_SCALABLE, format, opt, first, second);
            AssertConstant(id, 0, false);
        });
    }

    [TestCase(INS_sve_fmov, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_fmov, INS_OPTS_SCALABLE_D)]
    public static void RegisterFloatingCopyUsesMovAlias(instruction ins, insOpts opt)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R(ins, EA_SCALABLE, REG_V31, REG_P15, opt));
            AssertDescriptor(id, INS_sve_mov, EA_SCALABLE, IF_SVE_BV_2B, opt, REG_V31, REG_P15);
            Assert.That(id.idIsSmallDsc(), Is.True);
        });
    }

    [TestCase(INS_sve_asr, INS_OPTS_SCALABLE_B, REG_V31, 8, IF_SVE_BF_2A)]
    [TestCase(INS_sve_lsl, INS_OPTS_SCALABLE_H, REG_V31, 0, IF_SVE_BF_2A)]
    [TestCase(INS_sve_lsr, INS_OPTS_SCALABLE_S, REG_V31, 32, IF_SVE_BF_2A)]
    [TestCase(INS_sve_srshr, INS_OPTS_SCALABLE_H, REG_P7, 16, IF_SVE_AM_2A)]
    [TestCase(INS_sve_sqshl, INS_OPTS_SCALABLE_B, REG_P7, 7, IF_SVE_AM_2A)]
    [TestCase(INS_sve_urshr, INS_OPTS_SCALABLE_S, REG_P0, 1, IF_SVE_AM_2A)]
    [TestCase(INS_sve_sqshlu, INS_OPTS_SCALABLE_D, REG_P7, 63, IF_SVE_AM_2A)]
    [TestCase(INS_sve_uqshl, INS_OPTS_SCALABLE_H, REG_P0, 0, IF_SVE_AM_2A)]
    [TestCase(INS_sve_asrd, INS_OPTS_SCALABLE_D, REG_P7, 1, IF_SVE_AM_2A)]
    [TestCase(INS_sve_xar, INS_OPTS_SCALABLE_B, REG_V31, 8, IF_SVE_AW_2A)]
    [TestCase(INS_sve_xar, INS_OPTS_SCALABLE_H, REG_V31, 16, IF_SVE_AW_2A)]
    [TestCase(INS_sve_xar, INS_OPTS_SCALABLE_S, REG_V31, 32, IF_SVE_AW_2A)]
    [TestCase(INS_sve_xar, INS_OPTS_SCALABLE_D, REG_V31, 64, IF_SVE_AW_2A)]
    [TestCase(INS_sve_sqrshrn, INS_OPTS_SCALABLE_H, REG_V30, 32, IF_SVE_GA_2A)]
    [TestCase(INS_sve_sqrshrun, INS_OPTS_SCALABLE_H, REG_V0, 1, IF_SVE_GA_2A)]
    [TestCase(INS_sve_uqrshrn, INS_OPTS_SCALABLE_H, REG_V30, 16, IF_SVE_GA_2A)]
    [TestCase(INS_sve_sshllb, INS_OPTS_SCALABLE_H, REG_V31, 7, IF_SVE_FR_2A)]
    [TestCase(INS_sve_sshllt, INS_OPTS_SCALABLE_S, REG_V31, 15, IF_SVE_FR_2A)]
    [TestCase(INS_sve_ushllb, INS_OPTS_SCALABLE_D, REG_V31, 31, IF_SVE_FR_2A)]
    [TestCase(INS_sve_ushllt, INS_OPTS_SCALABLE_H, REG_V31, 0, IF_SVE_FR_2A)]
    [TestCase(INS_sve_sqshrunb, INS_OPTS_SCALABLE_B, REG_V31, 8, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqshrunt, INS_OPTS_SCALABLE_H, REG_V31, 16, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqrshrunb, INS_OPTS_SCALABLE_S, REG_V31, 32, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqrshrunt, INS_OPTS_SCALABLE_B, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_shrnb, INS_OPTS_SCALABLE_H, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_shrnt, INS_OPTS_SCALABLE_S, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_rshrnb, INS_OPTS_SCALABLE_B, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_rshrnt, INS_OPTS_SCALABLE_H, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqshrnb, INS_OPTS_SCALABLE_S, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqshrnt, INS_OPTS_SCALABLE_B, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqrshrnb, INS_OPTS_SCALABLE_H, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_sqrshrnt, INS_OPTS_SCALABLE_S, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_uqshrnb, INS_OPTS_SCALABLE_B, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_uqshrnt, INS_OPTS_SCALABLE_H, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_uqrshrnb, INS_OPTS_SCALABLE_S, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_uqrshrnt, INS_OPTS_SCALABLE_B, REG_V31, 1, IF_SVE_GB_2A)]
    [TestCase(INS_sve_cadd, INS_OPTS_SCALABLE_B, REG_V31, 0, IF_SVE_FV_2A)]
    [TestCase(INS_sve_sqcadd, INS_OPTS_SCALABLE_D, REG_V31, 1, IF_SVE_FV_2A)]
    [TestCase(INS_sve_ftmad, INS_OPTS_SCALABLE_H, REG_V31, 7, IF_SVE_HN_2A)]
    [TestCase(INS_sve_sli, INS_OPTS_SCALABLE_S, REG_V31, 31, IF_SVE_FT_2A)]
    [TestCase(INS_sve_sri, INS_OPTS_SCALABLE_H, REG_V31, 16, IF_SVE_FT_2A)]
    [TestCase(INS_sve_srsra, INS_OPTS_SCALABLE_B, REG_V31, 8, IF_SVE_FU_2A)]
    [TestCase(INS_sve_ssra, INS_OPTS_SCALABLE_H, REG_V31, 16, IF_SVE_FU_2A)]
    [TestCase(INS_sve_ursra, INS_OPTS_SCALABLE_S, REG_V31, 32, IF_SVE_FU_2A)]
    [TestCase(INS_sve_usra, INS_OPTS_SCALABLE_D, REG_V31, 1, IF_SVE_FU_2A)]
    [TestCase(INS_sve_ext, INS_OPTS_SCALABLE_B, REG_V31, 255, IF_SVE_BQ_2B)]
    [TestCase(INS_sve_dupq, INS_OPTS_SCALABLE_B, REG_V31, 15, IF_SVE_BX_2A)]
    [TestCase(INS_sve_dupq, INS_OPTS_SCALABLE_H, REG_V31, 7, IF_SVE_BX_2A)]
    [TestCase(INS_sve_dupq, INS_OPTS_SCALABLE_S, REG_V31, 3, IF_SVE_BX_2A)]
    [TestCase(INS_sve_dupq, INS_OPTS_SCALABLE_D, REG_V31, 1, IF_SVE_BX_2A)]
    [TestCase(INS_sve_extq, INS_OPTS_SCALABLE_B, REG_V31, 15, IF_SVE_BY_2A)]
    public static void ImmediateFormsPreserveNativeImmediateRatherThanPreencodingIt(
        instruction ins, insOpts opt, regNumber second, int immediate, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitIns_R_R_I(
                ins, EA_SCALABLE, REG_V0, second, immediate, opt));
            AssertDescriptor(id, ins, EA_SCALABLE, format, opt, REG_V0, second);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_SCALABLE_OPTS_NONE, -16, IF_SVE_AZ_2A)]
    [TestCase(INS_SCALABLE_OPTS_IMM_FIRST, 15, IF_SVE_AY_2A)]
    public static void IndexOrderSelectsFormatWithoutReorderingDescriptorFields(
        insScalableOpts sopt, int immediate, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(
                INS_sve_index, EA_SCALABLE, REG_V31, REG_R30, immediate, INS_OPTS_SCALABLE_S, sopt));
            AssertDescriptor(id, INS_sve_index, EA_SCALABLE, format, INS_OPTS_SCALABLE_S, REG_V31, REG_R30);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_sve_addvl, -32, REG_SP, REG_R30)]
    [TestCase(INS_sve_addpl, 31, REG_R0, REG_SP)]
    [TestCase(INS_sve_addvl, 0, REG_SP, REG_SP)]
    public static void ScalableAddressAddsEncodeBothStackPointerOperands(
        instruction ins, int immediate, regNumber first, regNumber second)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(ins, EA_8BYTE, first, second, immediate));
            AssertDescriptor(id, ins, EA_8BYTE, IF_SVE_BB_2A, INS_OPTS_NONE,
                first == REG_SP ? REG_ZR : first, second == REG_SP ? REG_ZR : second);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_sve_cpy, -128, false)]
    [TestCase(INS_sve_mov, 127, true)]
    [TestCase(INS_sve_cpy, -32768, true)]
    [TestCase(INS_sve_mov, 32512, false)]
    [TestCase(INS_sve_cpy, 256, false)]
    [TestCase(INS_sve_mov, -256, true)]
    public static void IntegerCopyReducesShiftedConstantsAndKeepsShiftBitOutOfSmallDescriptors(
        instruction ins, int immediate, bool merge)
    {
        WithEmitter(emitter =>
        {
            var shifted = immediate is < -128 or > 127;
            var reduced = shifted ? immediate >> 8 : immediate;
            var sopt = merge ? INS_SCALABLE_OPTS_PREDICATE_MERGE : INS_SCALABLE_OPTS_NONE;
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(
                ins, EA_SCALABLE, REG_V31, REG_P15, immediate, INS_OPTS_SCALABLE_S, sopt));
            AssertDescriptor(id, INS_sve_mov, EA_SCALABLE, merge ? IF_SVE_BV_2A_J : IF_SVE_BV_2A,
                INS_OPTS_SCALABLE_S, REG_V31, REG_P15);
            AssertConstant(id, reduced, shifted);
        });
    }

    [TestCase(INS_sve_dup, INS_OPTS_SCALABLE_B, 63)]
    [TestCase(INS_sve_mov, INS_OPTS_SCALABLE_H, 31)]
    [TestCase(INS_sve_dup, INS_OPTS_SCALABLE_S, 15)]
    [TestCase(INS_sve_mov, INS_OPTS_SCALABLE_D, 7)]
    [TestCase(INS_sve_dup, INS_OPTS_SCALABLE_Q, 3)]
    public static void VectorBroadcastUsesMovAliasWithoutChangingLaneIndex(instruction ins, insOpts opt, int immediate)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(
                ins, EA_SCALABLE, REG_V0, REG_V31, immediate, opt));
            AssertDescriptor(id, INS_sve_mov, EA_SCALABLE, IF_SVE_BW_2A, opt, REG_V0, REG_V31);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_OPTS_SCALABLE_H, true, 1, IF_SVE_CE_2C)]
    [TestCase(INS_OPTS_SCALABLE_S, true, 3, IF_SVE_CE_2D)]
    [TestCase(INS_OPTS_SCALABLE_D, true, 7, IF_SVE_CE_2B)]
    [TestCase(INS_OPTS_SCALABLE_H, false, 1, IF_SVE_CF_2C)]
    [TestCase(INS_OPTS_SCALABLE_S, false, 3, IF_SVE_CF_2D)]
    [TestCase(INS_OPTS_SCALABLE_D, false, 7, IF_SVE_CF_2B)]
    public static void PredicateMoveImmediateKeepsDirectionalWidthFormat(
        insOpts opt, bool predicateDestination, int immediate, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var first = predicateDestination ? REG_P15 : REG_V31;
            var second = predicateDestination ? REG_V31 : REG_P15;
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(
                INS_sve_pmov, EA_SCALABLE, first, second, immediate, opt));
            AssertDescriptor(id, INS_sve_pmov, EA_SCALABLE, format, opt, first, second);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_SCALABLE_OPTS_NONE, 3, IF_SVE_DW_2A)]
    [TestCase(INS_SCALABLE_OPTS_WITH_PREDICATE_PAIR, 1, IF_SVE_DW_2B)]
    public static void PredicateExtractRetainsHighSourceBankAndPairFormat(
        insScalableOpts sopt, int immediate, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(
                INS_sve_pext, EA_SCALABLE, REG_P15, REG_P8, immediate, INS_OPTS_SCALABLE_D, sopt));
            AssertDescriptor(id, INS_sve_pext, EA_SCALABLE, format, INS_OPTS_SCALABLE_D, REG_P15, REG_P8);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_sve_ldr, REG_V31, -256, IF_SVE_IE_2A)]
    [TestCase(INS_sve_ldr, REG_P15, 255, IF_SVE_ID_2A)]
    [TestCase(INS_sve_str, REG_V31, 255, IF_SVE_JH_2A)]
    [TestCase(INS_sve_str, REG_P15, -256, IF_SVE_JG_2A)]
    public static void ScalableLoadsAndStoresKeepSignedOffsetAndStackPointerEncoding(
        instruction ins, regNumber first, int immediate, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_I(ins, EA_SCALABLE, first, REG_SP, immediate));
            AssertDescriptor(id, ins, EA_SCALABLE, format, INS_OPTS_NONE, first, REG_ZR);
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(INS_sve_ldr, REG_V31, IF_SVE_IE_2A)]
    [TestCase(INS_sve_str, REG_P15, IF_SVE_JG_2A)]
    public static void RegisterOnlyMemoryFormsDelegateZeroOffset(
        instruction ins, regNumber first, Emitter.insFormat format)
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R(ins, EA_SCALABLE, first, REG_SP));
            AssertDescriptor(id, ins, EA_SCALABLE, format, INS_OPTS_NONE, first, REG_ZR);
            AssertConstant(id, 0, false);
        });
    }

    [Test]
    public static void UnpredicatedPrefixDependencyNowRecordsBeforeExistingThreeRegisterArithmetic()
    {
        WithEmitter(emitter =>
        {
            var id = Record(emitter, () => emitter.emitInsSve_R_R_R_R(
                INS_sve_add, EA_SCALABLE, REG_V0, REG_P7, REG_V1, REG_V31, INS_OPTS_SCALABLE_S), 8);
            AssertDescriptor(id, INS_sve_add, EA_SCALABLE, IF_SVE_AA_3A, INS_OPTS_SCALABLE_S, REG_V0, REG_P7);
            Assert.That(id.idReg3(), Is.EqualTo(REG_V31));
            Assert.That(id.idIsSmallDsc(), Is.False);
        });
    }

    [TestCase(INS_sve_fcvtn, INS_OPTS_NONE)]
    [TestCase(INS_sve_bfcvtn, INS_OPTS_NONE)]
    [TestCase(INS_sve_fcvtnt, INS_OPTS_NONE)]
    [TestCase(INS_sve_fcvtnb, INS_OPTS_NONE)]
    [TestCase(INS_sve_bf1cvt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_bf1cvtlt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_bf2cvt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_bf2cvtlt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_f1cvt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_f1cvtlt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_f2cvt, INS_OPTS_SCALABLE_H)]
    [TestCase(INS_sve_f2cvtlt, INS_OPTS_SCALABLE_H)]
    public static void NativeUnsupportedRegisterBranchesStillTerminate(instruction ins, insOpts opt)
    {
        WithEmitter(emitter =>
        {
            ExpectFailure(() => emitter.emitInsSve_R_R(ins, EA_SCALABLE, REG_V0, REG_V30, opt));
            Assert.That(GroupSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnknownInstructionsTerminateRatherThanAppendFallbackDescriptor(bool immediate)
    {
        WithEmitter(emitter =>
        {
            ExpectFailure(() =>
            {
                if (immediate)
                {
                    emitter.emitInsSve_R_R_I(INS_nop, EA_SCALABLE, REG_V0, REG_V31, 0);
                }
                else
                {
                    emitter.emitInsSve_R_R(INS_nop, EA_SCALABLE, REG_V0, REG_V31);
                }
            });
            Assert.That(GroupSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }

    [Test]
    public static void UnsupportedVectorPairExtractRetainsNativeTermination()
    {
        WithEmitter(emitter =>
        {
            ExpectFailure(() => emitter.emitInsSve_R_R_I(INS_sve_ext, EA_SCALABLE,
                REG_V0, REG_V31, 0, INS_OPTS_SCALABLE_B, INS_SCALABLE_OPTS_WITH_VECTOR_PAIR));
            Assert.That(GroupSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }

    [TestCase(0, 90)]
    [TestCase(1, 270)]
    [TestCase(2, 0)]
    [TestCase(3, 0)]
    public static void RotationDecoderPreservesNativeReturns(int encoded, int expected)
    {
#if DEBUG
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() =>
            Assert.That(DecodeRotation(null, encoded), Is.EqualTo((nint)expected)));
        Assert.That(assertions, Is.Empty);
#else
        Assert.That(DecodeRotation(null, encoded), Is.EqualTo((nint)expected));
#endif
    }

#if DEBUG
    [TestCase(-1)]
    [TestCase(4)]
    public static void ContinuingEeKeepsDecoderAssertionAndZeroReturn(int encoded)
    {
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() =>
            Assert.That(DecodeRotation(null, encoded), Is.EqualTo((nint)0)));
        Assert.That(assertions, Is.EqualTo<string[]>(["emitIsValidEncodedRotationImm0_to_270(imm)"]));
    }

    [TestCase(129, 0)]
    [TestCase(-129, -1)]
    public static void ContinuingEePreservesFailedMultipleAssertionAndArithmeticReduction(int immediate, int reduced)
    {
        WithEmitter(emitter =>
        {
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() => emitter.emitInsSve_R_R_I(
                INS_sve_cpy, EA_SCALABLE, REG_V0, REG_P15, immediate, INS_OPTS_SCALABLE_S));
            Assert.That(assertions, Is.EqualTo<string[]>(["isValidSimm_MultipleOf(imm, 8, 256)"]));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
            var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
            AssertDescriptor(id, INS_sve_mov, EA_SCALABLE, IF_SVE_BV_2A, INS_OPTS_SCALABLE_S, REG_V0, REG_P15);
            AssertConstant(id, reduced, true);
        });
    }

    [TestCase(INS_sve_sqcvtn, false)]
    [TestCase(INS_sve_sqrshrn, true)]
    public static void ContinuingEeKeepsRecorderThenSanityEvenRegisterAssertions(instruction ins, bool immediate)
    {
        WithEmitter(emitter =>
        {
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() =>
            {
                if (immediate)
                {
                    emitter.emitInsSve_R_R_I(ins, EA_SCALABLE, REG_V0, REG_V31, 4, INS_OPTS_SCALABLE_H);
                }
                else
                {
                    emitter.emitInsSve_R_R(ins, EA_SCALABLE, REG_V0, REG_V31);
                }
            });
            Assert.That(assertions, Is.EqualTo<string[]>(["isEvenRegister(reg2)", "isEvenRegister(id.idReg2())"]));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
        });
    }

    [TestCase(2)]
    [TestCase(3)]
    public static void RotationDecoderAllowsNativeZeroResultButDescriptorSanityRejectsEncoding(int immediate)
    {
        WithEmitter(emitter =>
        {
            var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() => emitter.emitInsSve_R_R_I(
                INS_sve_cadd, EA_SCALABLE, REG_V0, REG_V31, immediate, INS_OPTS_SCALABLE_B));
            Assert.That(assertions, Is.EqualTo<string[]>(["emitIsValidEncodedRotationImm90_or_270(emitGetInsSC(id))"]));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
            var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
            AssertConstant(id, immediate, false);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DisplayFailureLeavesPreparedDescriptorUnappended(bool immediate)
    {
        WithEmitter(emitter =>
        {
            var compiler = JitTls.Compiler ?? throw new AssertionException("No test compiler is installed.");
            compiler.opts.dspCode = true;
            ExpectFailure(() =>
            {
                if (immediate)
                {
                    emitter.emitInsSve_R_R_I(INS_sve_cpy, EA_SCALABLE,
                        REG_V0, REG_P15, -32768, INS_OPTS_SCALABLE_H);
                }
                else
                {
                    emitter.emitInsSve_R_R(INS_sve_dup, EA_8BYTE, REG_V0, REG_SP, INS_OPTS_SCALABLE_D);
                }
            });
            Assert.That(GroupSize(emitter), Is.Zero);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No descriptor was prepared.");
            if (immediate)
            {
                AssertDescriptor(id, INS_sve_mov, EA_SCALABLE, IF_SVE_BV_2A, INS_OPTS_SCALABLE_H, REG_V0, REG_P15);
                AssertConstant(id, -128, true);
            }
            else
            {
                AssertDescriptor(id, INS_sve_mov, EA_8BYTE, IF_SVE_CB_2A, INS_OPTS_SCALABLE_D, REG_V0, REG_ZR);
                Assert.That(id.idIsSmallDsc(), Is.True);
            }
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

    private static void ExpectFailure(Action record)
    {
#if DEBUG
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => Assert.Throws<FatalJitException>(() => record()));
        Assert.That(assertions, Is.Empty);
#else
        Assert.Throws<FatalJitException>(() => record());
#endif
    }

    private static void AssertConstant(Emitter.instrDesc id, nint immediate, bool shifted)
    {
        var smallConstant = Emitter.instrDesc.fitsInSmallCns(immediate);
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo(immediate));
        Assert.That(id.idHasShift(), Is.EqualTo(shifted));
        Assert.That(id.idIsSmallDsc(), Is.EqualTo(smallConstant && !shifted));
        Assert.That(id.idIsLargeCns(), Is.EqualTo(!smallConstant));
    }

    private static void AssertDescriptor(Emitter.instrDesc id, instruction ins, emitAttr attr,
        Emitter.insFormat format, insOpts opt, regNumber first, regNumber second)
    {
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idInsOpt(), Is.EqualTo(opt));
        Assert.That(id.idOpSize(), Is.EqualTo(attr));
        Assert.That(id.idReg1(), Is.EqualTo(first));
        Assert.That(id.idReg2(), Is.EqualTo(second));
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

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitDecodeRotationImm90_or_270")]
    private static extern nint DecodeRotation(Emitter? emitter, nint encoded);
}
#endif
