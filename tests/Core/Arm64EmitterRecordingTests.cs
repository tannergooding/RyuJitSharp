// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
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
internal static unsafe class Arm64EmitterRecordingTests
{
    [Test]
    public static void GeneratedMetadataMatchesPinnedNativeTables()
    {
        var formats = Formats(null);
        var flags = Flags(null);
        Assert.That((int)IF_COUNT, Is.EqualTo(599));
        Assert.That(formats.Length, Is.EqualTo(1158));
        Assert.That(flags.Length, Is.EqualTo(formats.Length));
        Assert.That((int)INS_lea, Is.EqualTo(formats.Length));
        var rows = new StringBuilder();
        for (var index = 0; index < (int)IF_COUNT; index++)
        {
            _ = rows.Append(FormattableString.Invariant($"F {index} {(Emitter.insFormat)index}\n"));
        }
        for (var index = 0; index < formats.Length; index++)
        {
            _ = rows.Append(FormattableString.Invariant($"I {index} {(instruction)index} {(uint)formats[index]} {flags[index]}\n"));
        }

        // Native macro expansion of emitfmtsarm64{,sve}.h and instrsarm64{,sve}.h
        // at 33baf8ee337b20dd0f184b69a6f09be92850bf9e, with FEATURE_LOOP_ALIGN.
        Assert.That(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))),
            Is.EqualTo("bcb51ded8b656135816235dc66868ac85a812c2cee5ec70239b7fef278ce6f35"));
    }

    [TestCase(INS_nop, IF_SN_0A)]
    [TestCase(INS_brk, IF_SI_0A)]
    [TestCase(INS_autia1716, IF_PC_0A)]
    [TestCase(INS_autiasp, IF_PC_0A)]
    [TestCase(INS_autiaz, IF_PC_0A)]
    [TestCase(INS_autib1716, IF_PC_0A)]
    [TestCase(INS_autibsp, IF_PC_0A)]
    [TestCase(INS_autibz, IF_PC_0A)]
    [TestCase(INS_pacia1716, IF_PC_0A)]
    [TestCase(INS_paciasp, IF_PC_0A)]
    [TestCase(INS_paciaz, IF_PC_0A)]
    [TestCase(INS_pacib1716, IF_PC_0A)]
    [TestCase(INS_pacibsp, IF_PC_0A)]
    [TestCase(INS_pacibz, IF_PC_0A)]
    [TestCase(INS_xpaclri, IF_PC_0A)]
    [TestCase(INS_retaa, IF_BR_0A)]
    [TestCase(INS_retab, IF_BR_0A)]
    public static void ZeroOperandRecordingPreservesFormatsAndDiagnosticOrdering(instruction ins, Emitter.insFormat format)
    {
        var emitter = CreateEmitter();
        emitter.emitIns(ins);
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_8BYTE));
        Assert.That(descriptor.idIsSmallDsc(), Is.True);
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
    }

    [TestCase(0)]
    [TestCase(65535)]
    public static void ImmediateBreakpointRecordingPreservesTheConstant(int immediate)
    {
        var emitter = CreateEmitter();
        emitter.emitIns_I(INS_brk, EA_8BYTE, immediate);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_SI_0A));
        Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)immediate));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_br, REG_R0, IF_BR_1A)]
    [TestCase(INS_ret, REG_LR, IF_BR_1A)]
    [TestCase(INS_dczva, REG_R2, IF_SR_1A)]
    [TestCase(INS_autiza, REG_R3, IF_PC_1A)]
    [TestCase(INS_autizb, REG_R4, IF_PC_1A)]
    [TestCase(INS_paciza, REG_R5, IF_PC_1A)]
    [TestCase(INS_pacizb, REG_R6, IF_PC_1A)]
    [TestCase(INS_xpacd, REG_R7, IF_PC_1A)]
    [TestCase(INS_xpaci, REG_R8, IF_PC_1A)]
    [TestCase(INS_mrs_tpid0, REG_R9, IF_SR_1A)]
    public static void UnaryRecordingPreservesNativeFormats(instruction ins, regNumber reg, Emitter.insFormat format)
    {
        var emitter = CreateEmitter();
        emitter.emitIns_R(ins, EA_8BYTE, reg);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
        Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        Assert.That(descriptor.idIsSmallDsc(), Is.True);
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_movk, EA_8BYTE, 65535L, INS_movk, IF_DI_1B, 65535L, INS_OPTS_NONE)]
    [TestCase(INS_movn, EA_4BYTE, 1L, INS_movn, IF_DI_1B, 1L, INS_OPTS_NONE)]
    [TestCase(INS_movz, EA_8BYTE, 0L, INS_movz, IF_DI_1B, 0L, INS_OPTS_NONE)]
    [TestCase(INS_mov, EA_8BYTE, 0x12340000L, INS_mov, IF_DI_1B, 0x11234L, INS_OPTS_NONE)]
    [TestCase(INS_mov, EA_8BYTE, -65536L, INS_movn, IF_DI_1B, 65535L, INS_OPTS_NONE)]
    [TestCase(INS_mov, EA_8BYTE, 0x00ff00ff00ff00ffL, INS_mov, IF_DI_1D, 39L, INS_OPTS_NONE)]
    [TestCase(INS_tst, EA_8BYTE, 255L, INS_tst, IF_DI_1C, 4103L, INS_OPTS_NONE)]
    [TestCase(INS_tst, EA_4BYTE, 0x00ff00ffL, INS_tst, IF_DI_1C, 39L, INS_OPTS_NONE)]
    [TestCase(INS_tst, EA_8BYTE, unchecked((long)0xff00ff00ff00ff00UL), INS_tst, IF_DI_1C, 551L, INS_OPTS_NONE)]
    [TestCase(INS_cmp, EA_8BYTE, -4095L, INS_cmn, IF_DI_1A, 4095L, INS_OPTS_NONE)]
    [TestCase(INS_cmn, EA_4BYTE, -4096L, INS_cmp, IF_DI_1A, 1L, INS_OPTS_LSL12)]
    [TestCase(INS_cmp, EA_8BYTE, 0xfff000L, INS_cmp, IF_DI_1A, 4095L, INS_OPTS_LSL12)]
    public static void RegisterImmediateRecordingUsesNativeEncodingPreference(
        instruction ins, emitAttr size, long immediate, instruction expectedIns,
        Emitter.insFormat format, long encoded, insOpts option)
    {
        var emitter = CreateEmitter();
        emitter.emitIns_R_I(ins, size, REG_R0, (nint)immediate);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(descriptor.idIns(), Is.EqualTo(expectedIns));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
        Assert.That(descriptor.idInsOpt(), Is.EqualTo(option));
        Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)encoded));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_movi, EA_8BYTE, INS_OPTS_NONE, unchecked((long)0xff00ff00ff00ff00UL), INS_movi, 170L, INS_OPTS_1D)]
    [TestCase(INS_movi, EA_16BYTE, INS_OPTS_4S, 0x1200L, INS_movi, 274L, INS_OPTS_4S)]
    [TestCase(INS_movi, EA_16BYTE, INS_OPTS_4S, 0x12ffL, INS_movi, 1298L, INS_OPTS_4S)]
    [TestCase(INS_movi, EA_16BYTE, INS_OPTS_4S, -0x1201L, INS_mvni, 274L, INS_OPTS_4S)]
    [TestCase(INS_orr, EA_16BYTE, INS_OPTS_4S, 0x1200L, INS_orr, 274L, INS_OPTS_4S)]
    [TestCase(INS_bic, EA_16BYTE, INS_OPTS_8H, 0x1200L, INS_bic, 274L, INS_OPTS_8H)]
    [TestCase(INS_mvni, EA_16BYTE, INS_OPTS_4S, 0x12ffffL, INS_mvni, 1554L, INS_OPTS_4S)]
    public static void VectorImmediateRecordingPreservesShiftAndComplementSelection(
        instruction ins, emitAttr size, insOpts option, long immediate, instruction expectedIns,
        long encoded, insOpts expectedOption)
    {
        var emitter = CreateEmitter();
        emitter.emitIns_R_I(ins, size, REG_V0, (nint)immediate, option);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(descriptor.idIns(), Is.EqualTo(expectedIns));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_DV_1B));
        Assert.That(descriptor.idInsOpt(), Is.EqualTo(expectedOption));
        Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)encoded));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void UnsupportedFormsReachTheirSeparateSveRecorders(int form)
    {
        var previousConfig = Globals.JitConfig;
        Globals.JitConfig = new JitConfigValues();
        try
        {
            var emitter = CreateEmitter();
            var error = Assert.Throws<FatalJitException>(() =>
            {
                switch (form)
                {
                    case 0:
                    {
                        emitter.emitIns_I(INS_nop, EA_8BYTE, 0);
                        break;
                    }
                    case 1:
                    {
                        emitter.emitIns_R(INS_nop, EA_8BYTE, REG_R0);
                        break;
                    }
                    default:
                    {
                        emitter.emitIns_R_I(INS_nop, EA_8BYTE, REG_R0, 0);
                        break;
                    }
                }
            });
            Assert.That(error, Has.Property(nameof(FatalJitException.Result))
                .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            Assert.That(GroupSize(emitter), Is.Zero);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [TestCase(EA_4BYTE, INS_OPTS_NONE, IF_DV_1A)]
    [TestCase(EA_8BYTE, INS_OPTS_NONE, IF_DV_1A)]
    [TestCase(EA_8BYTE, INS_OPTS_2S, IF_DV_1B)]
    [TestCase(EA_16BYTE, INS_OPTS_4S, IF_DV_1B)]
    [TestCase(EA_16BYTE, INS_OPTS_2D, IF_DV_1B)]
    public static void FloatingImmediateRecordingCoversEveryEncoding(
        emitAttr size, insOpts option, Emitter.insFormat format)
    {
        for (var encoded = 0; encoded <= 255; encoded++)
        {
            // ARM64 imm8 has sign:exponent:mantissa fields of widths 1:3:4.
            // The exponent toggles its top bit; the significand is 16..31.
            var exponent = ((encoded >> 4) & 7) ^ 4;
            var value = Math.ScaleB(16 + (encoded & 15), exponent - 7);
            if ((encoded & 128) != 0)
            {
                value = -value;
            }

            var emitter = CreateEmitter();
            RecordFloat(emitter, INS_fmov, size, REG_V0, value, option);
            var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(option));
            Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)encoded));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
        }
    }

    [TestCase(INS_fcmp, 0UL)]
    [TestCase(INS_fcmp, 0x8000000000000000UL)]
    [TestCase(INS_fcmpe, 0UL)]
    [TestCase(INS_fcmpe, 0x8000000000000000UL)]
    public static void FloatingCompareRecordingAcceptsBothZeroSigns(instruction ins, ulong bits)
    {
        var emitter = CreateEmitter();
        RecordFloat(emitter, ins, EA_8BYTE, REG_V0, BitConverter.UInt64BitsToDouble(bits), INS_OPTS_NONE);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_DV_1C));
        Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)0));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [Test]
    public static void FloatingFallbackRetainsItsSeparateSveRecorder()
    {
        var previousConfig = Globals.JitConfig;
        Globals.JitConfig = new JitConfigValues();
        try
        {
            var emitter = CreateEmitter();
            var error = Assert.Throws<FatalJitException>(() =>
                RecordFloat(emitter, INS_nop, EA_8BYTE, REG_V0, 1.0, INS_OPTS_NONE));
            Assert.That(error, Has.Property(nameof(FatalJitException.Result))
                .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            Assert.That(GroupSize(emitter), Is.Zero);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_F")]
    private static extern void RecordFloat(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg, double immDbl, insOpts opt);

    [TestCase(INS_mov, EA_4BYTE)]
    [TestCase(INS_mov, EA_8BYTE)]
    [TestCase(INS_movz, EA_4BYTE)]
    [TestCase(INS_movz, EA_8BYTE)]
    [TestCase(INS_movn, EA_4BYTE)]
    [TestCase(INS_movn, EA_8BYTE)]
    [TestCase(INS_movk, EA_4BYTE)]
    [TestCase(INS_movk, EA_8BYTE)]
    public static void ShiftedHalfwordRecordingPreservesEveryLegalPosition(instruction ins, emitAttr size)
    {
        for (var shift = 0; shift < (int)size * 8; shift += 16)
        {
            foreach (var immediate in new[] { 0, 1, 32768, 65535 })
            {
                var emitter = CreateEmitter();
                RecordShifted(emitter, ins, size, REG_R19, immediate, shift, INS_OPTS_LSL);
                var descriptor = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
                Assert.That(descriptor.idIns(), Is.EqualTo(ins == INS_mov ? INS_movz : ins));
                Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_DI_1B));
                Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
                Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_LSL));
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R19));
                Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)(immediate | ((shift / 16) << 16))));
                Assert.That(GroupSize(emitter), Is.EqualTo(4));
            }
        }
    }

    [Test]
    public static void ShiftedFallbackRetainsItsSeparateSveRecorder()
    {
        var emitter = CreateEmitter();
        var error = Assert.Throws<FatalJitException>(() =>
            RecordShifted(emitter, INS_nop, EA_8BYTE, REG_R0, 1, 16, INS_OPTS_LSL));
        Assert.That(error, Has.Message.EqualTo("ARM64 SVE register/two-immediate instruction recording is not ported."));
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_I_I")]
    private static extern void RecordShifted(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg, nint imm1, nint imm2, insOpts opt
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GenTreeFlags.GTF_EMPTY
#endif
        );

    [TestCase(INS_lsl, EA_8BYTE, 63, INS_OPTS_NONE, IF_DI_2D)]
    [TestCase(INS_lsr, EA_4BYTE, 31, INS_OPTS_NONE, IF_DI_2D)]
    [TestCase(INS_asr, EA_8BYTE, 0, INS_OPTS_NONE, IF_DI_2D)]
    [TestCase(INS_ror, EA_8BYTE, 63, INS_OPTS_NONE, IF_DI_2B)]
    [TestCase(INS_mvn, EA_8BYTE, 0, INS_OPTS_NONE, IF_DR_2E)]
    [TestCase(INS_neg, EA_8BYTE, 0, INS_OPTS_NONE, IF_DR_2E)]
    [TestCase(INS_negs, EA_8BYTE, 3, INS_OPTS_LSL, IF_DR_2F)]
    [TestCase(INS_tst, EA_8BYTE, 0, INS_OPTS_NONE, IF_DR_2A)]
    [TestCase(INS_tst, EA_8BYTE, 3, INS_OPTS_LSR, IF_DR_2B)]
    [TestCase(INS_cmp, EA_8BYTE, 0, INS_OPTS_NONE, IF_DR_2A)]
    [TestCase(INS_cmn, EA_8BYTE, 3, INS_OPTS_LSL, IF_DR_2B)]
    [TestCase(INS_cmp, EA_8BYTE, 4, INS_OPTS_UXTW, IF_DR_2C)]
    public static void PairImmediateScalarRecording(instruction ins, emitAttr size, int imm,
        insOpts opt, Emitter.insFormat format)
    {
        CheckPair(ins, size, REG_R19, REG_R20, imm, opt, format, imm, ins);
    }

    [TestCase(INS_add, 1, INS_add, 1, INS_OPTS_NONE)]
    [TestCase(INS_add, -4095, INS_sub, 4095, INS_OPTS_NONE)]
    [TestCase(INS_sub, -1, INS_add, 1, INS_OPTS_NONE)]
    [TestCase(INS_sub, 4096, INS_sub, 1, INS_OPTS_LSL12)]
    [TestCase(INS_adds, 0, INS_adds, 0, INS_OPTS_NONE)]
    [TestCase(INS_adds, -16773120, INS_subs, 4095, INS_OPTS_LSL12)]
    [TestCase(INS_subs, -4096, INS_adds, 1, INS_OPTS_LSL12)]
    [TestCase(INS_subs, 4095, INS_subs, 4095, INS_OPTS_NONE)]
    public static void PairImmediateArithmeticRecording(instruction ins, int imm,
        instruction expectedIns, int expectedImm, insOpts expectedOpt)
    {
        var emitter = CreateEmitter();
        RecordPair(emitter, ins, EA_8BYTE, REG_R19, REG_R20, imm, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(expectedIns));
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_DI_2A));
        Assert.That(id.idInsOpt(), Is.EqualTo(expectedOpt));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)expectedImm));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_and, EA_4BYTE, 7)]
    [TestCase(INS_and, EA_8BYTE, 4103)]
    [TestCase(INS_ands, EA_4BYTE, 7)]
    [TestCase(INS_ands, EA_8BYTE, 4103)]
    [TestCase(INS_eor, EA_4BYTE, 7)]
    [TestCase(INS_eor, EA_8BYTE, 4103)]
    [TestCase(INS_orr, EA_4BYTE, 7)]
    [TestCase(INS_orr, EA_8BYTE, 4103)]
    public static void PairImmediateLogicalRecording(instruction ins, emitAttr size, int encoded)
    {
        CheckPair(ins, size, REG_R19, REG_R20, 255, INS_OPTS_NONE, IF_DI_2C, encoded, ins);
    }

    [TestCase(INS_mov, EA_4BYTE, REG_V19, REG_R20, 3, INS_OPTS_NONE, IF_DV_2C)]
    [TestCase(INS_mov, EA_4BYTE, REG_V19, REG_V20, 3, INS_OPTS_NONE, IF_DV_2E)]
    [TestCase(INS_mov, EA_4BYTE, REG_R19, REG_V20, 3, INS_OPTS_NONE, IF_DV_2B)]
    [TestCase(INS_dup, EA_4BYTE, REG_V19, REG_V20, 3, INS_OPTS_NONE, IF_DV_2E)]
    [TestCase(INS_dup, EA_8BYTE, REG_V19, REG_V20, 3, INS_OPTS_2S, IF_DV_2D)]
    [TestCase(INS_ins, EA_1BYTE, REG_V19, REG_ZR, 15, INS_OPTS_NONE, IF_DV_2C)]
    [TestCase(INS_umov, EA_8BYTE, REG_R19, REG_V20, 1, INS_OPTS_NONE, IF_DV_2B)]
    [TestCase(INS_smov, EA_2BYTE, REG_R19, REG_V20, 7, INS_OPTS_NONE, IF_DV_2B)]
    [TestCase(INS_shl, EA_16BYTE, REG_V19, REG_V20, 31, INS_OPTS_4S, IF_DV_2O)]
    [TestCase(INS_sshr, EA_8BYTE, REG_V19, REG_V20, 64, INS_OPTS_NONE, IF_DV_2N)]
    [TestCase(INS_sqshl, EA_2BYTE, REG_V19, REG_V20, 15, INS_OPTS_NONE, IF_DV_2N)]
    [TestCase(INS_uqshl, EA_16BYTE, REG_V19, REG_V20, 15, INS_OPTS_8H, IF_DV_2O)]
    [TestCase(INS_sqrshrn, EA_2BYTE, REG_V19, REG_V20, 16, INS_OPTS_NONE, IF_DV_2N)]
    [TestCase(INS_sqshrun, EA_8BYTE, REG_V19, REG_V20, 16, INS_OPTS_4H, IF_DV_2O)]
    [TestCase(INS_shrn, EA_8BYTE, REG_V19, REG_V20, 16, INS_OPTS_4H, IF_DV_2O)]
    [TestCase(INS_sxtl, EA_8BYTE, REG_V19, REG_V20, 0, INS_OPTS_4H, IF_DV_2O)]
    [TestCase(INS_uxtl2, EA_16BYTE, REG_V19, REG_V20, 0, INS_OPTS_8H, IF_DV_2O)]
    [TestCase(INS_shrn2, EA_16BYTE, REG_V19, REG_V20, 16, INS_OPTS_8H, IF_DV_2O)]
    public static void PairImmediateVectorRecording(instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, int imm, insOpts opt, Emitter.insFormat format)
    {
        CheckPair(ins, size, reg1, reg2, imm, opt, format, imm, ins);
    }

    [TestCase(INS_mvn, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_ROR, IF_DR_2F,
        "insOptsNone(id.idInsOpt()) || insOptsAluShift(id.idInsOpt())")]
    [TestCase(INS_dup, EA_4BYTE, REG_V19, REG_R20, INS_OPTS_NONE, IF_DV_2C,
        "isValidVectorDatasize(datasize)")]
    public static void PairImmediateNativeDiagnosticRestrictions(instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, insOpts opt, Emitter.insFormat format, string condition)
    {
#if DEBUG
        var emitter = CreateEmitter();
        var error = Assert.Catch(() =>
            RecordPair(emitter, ins, size, reg1, reg2, 3, opt, INS_SCALABLE_OPTS_NONE));
        Assert.That(error, Has.Message.Contains(condition));
        Assert.That(GroupSize(emitter), Is.Zero);
#else
        CheckPair(ins, size, reg1, reg2, 3, opt, format, 3, ins);
#endif
    }

    [TestCase(INS_ldrsb, EA_8BYTE, REG_R19, 255, INS_OPTS_NONE, IF_LS_2B, 255)]
    [TestCase(INS_ldursb, EA_4BYTE, REG_R19, -256, INS_OPTS_NONE, IF_LS_2C, -256)]
    [TestCase(INS_ldrsh, EA_8BYTE, REG_R19, 8190, INS_OPTS_NONE, IF_LS_2B, 4095)]
    [TestCase(INS_ldursh, EA_8BYTE, REG_R19, 3, INS_OPTS_NONE, IF_LS_2C, 3)]
    [TestCase(INS_ldrsw, EA_8BYTE, REG_R19, 16380, INS_OPTS_NONE, IF_LS_2B, 4095)]
    [TestCase(INS_ldursw, EA_8BYTE, REG_R19, -4, INS_OPTS_NONE, IF_LS_2C, -4)]
    [TestCase(INS_ldrb, EA_1BYTE, REG_R19, 0, INS_OPTS_NONE, IF_LS_2A, 0)]
    [TestCase(INS_strh, EA_2BYTE, REG_R19, 3, INS_OPTS_NONE, IF_LS_2C, 3)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, 32760, INS_OPTS_NONE, IF_LS_2B, 4095)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, 8, INS_OPTS_PRE_INDEX, IF_LS_2C, 8)]
    [TestCase(INS_str, EA_8BYTE, REG_R19, -8, INS_OPTS_POST_INDEX, IF_LS_2C, -8)]
    [TestCase(INS_ldr, EA_16BYTE, REG_V19, 65520, INS_OPTS_NONE, IF_LS_2B, 4095)]
    [TestCase(INS_str, EA_1BYTE, REG_V19, 255, INS_OPTS_NONE, IF_LS_2B, 255)]
    [TestCase(INS_ldurb, EA_1BYTE, REG_R19, -1, INS_OPTS_NONE, IF_LS_2C, -1)]
    [TestCase(INS_stlurh, EA_2BYTE, REG_R19, -2, INS_OPTS_NONE, IF_LS_2C, -2)]
    [TestCase(INS_ldapur, EA_8BYTE, REG_R19, -8, INS_OPTS_NONE, IF_LS_2C, -8)]
    [TestCase(INS_stur, EA_8BYTE, REG_R19, -8, INS_OPTS_NONE, IF_LS_2C, -8)]
    public static void PairImmediateMemoryRecording(instruction ins, emitAttr size, regNumber reg,
        int imm, insOpts opt, Emitter.insFormat format, int expectedImm)
    {
        CheckPair(ins, size, reg, REG_SPBASE, imm, opt, format, expectedImm, ins);
    }

    [TestCase(INS_ld1, EA_4BYTE, INS_OPTS_NONE, 3, IF_LS_2F)]
    [TestCase(INS_st2, EA_2BYTE, INS_OPTS_NONE, 7, IF_LS_2F)]
    [TestCase(INS_ld1, EA_8BYTE, INS_OPTS_1D, 8, IF_LS_2E)]
    [TestCase(INS_st1_2regs, EA_16BYTE, INS_OPTS_4S, 32, IF_LS_2E)]
    [TestCase(INS_ld3, EA_16BYTE, INS_OPTS_16B, 48, IF_LS_2E)]
    [TestCase(INS_st4, EA_8BYTE, INS_OPTS_4H, 32, IF_LS_2E)]
    [TestCase(INS_ld1r, EA_16BYTE, INS_OPTS_4S, 4, IF_LS_2E)]
    [TestCase(INS_ld2r, EA_16BYTE, INS_OPTS_4S, 8, IF_LS_2E)]
    [TestCase(INS_ld3r, EA_16BYTE, INS_OPTS_4S, 12, IF_LS_2E)]
    [TestCase(INS_ld4r, EA_16BYTE, INS_OPTS_4S, 16, IF_LS_2E)]
    public static void PairImmediateStructureRecording(instruction ins, emitAttr size, insOpts opt,
        int imm, Emitter.insFormat format)
    {
        CheckPair(ins, size, REG_V19, REG_SPBASE, imm, opt, format, imm, ins);
    }

    [Test]
    public static void PairImmediateTlsRecordingPreservesUnscaledToken()
    {
        var emitter = CreateEmitter();
        RecordPair(emitter, INS_ldr, EA_8BYTE | EA_CNS_TLSGD_RELOC, REG_R19, REG_SPBASE,
            0x12345, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_2A));
        Assert.That(id.idIsTlsGD(), Is.True);
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)0x12345));
    }

    [TestCase(false, REG_R19, REG_R21)]
    [TestCase(true, REG_R21, REG_R19)]
    public static void AdjacentLoadsCombineInAddressOrder(bool descending, regNumber firstExpected, regNumber secondExpected)
    {
        var emitter = CreateEmitter(optimized: true);
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = EmitterCompiler(emitter);
#endif

        RecordPair(emitter, INS_ldr, EA_4BYTE, REG_R19, REG_R20, descending ? 4 : 0,
            INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        RecordPair(emitter, INS_ldr, EA_4BYTE, REG_R21, REG_R20, descending ? 0 : 4,
            INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);

        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(1));
        var pair = instructions[0];
        Assert.That(pair.idIns(), Is.EqualTo(INS_ldp));
        Assert.That(pair.idInsFmt(), Is.EqualTo(IF_LS_3B));
        Assert.That(pair.idReg1(), Is.EqualTo(firstExpected));
        Assert.That(pair.idReg2(), Is.EqualTo(secondExpected));
        Assert.That(pair.idReg3(), Is.EqualTo(REG_R20));
        Assert.That(Emitter.emitGetInsSC(pair), Is.Zero);
    }

    [Test]
    public static void RepeatedLoadFromSameAddressBecomesMove()
    {
        var emitter = CreateEmitter(optimized: true);
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = EmitterCompiler(emitter);
#endif

        RecordPair(emitter, INS_ldr, EA_8BYTE, REG_R19, REG_R20, 0,
            INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        RecordPair(emitter, INS_ldr, EA_8BYTE, REG_R21, REG_R20, 0,
            INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);

        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(2));
        Assert.That(instructions[0].idIns(), Is.EqualTo(INS_ldr));
        Assert.That(instructions[1].idIns(), Is.EqualTo(INS_mov));
        Assert.That(instructions[1].idReg1(), Is.EqualTo(REG_R21));
        Assert.That(instructions[1].idReg2(), Is.EqualTo(REG_R19));
        Assert.That(instructions[1].idOpSize(), Is.EqualTo(EA_8BYTE));
    }

    [TestCase(INS_add, 8, 8)]
    [TestCase(INS_sub, 8, -8)]
    public static void StackPointerAdjustmentCombinesWithPreviousLoad(instruction ins, int immediate, int expectedOffset)
    {
        var emitter = CreateEmitter(optimized: true);
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = EmitterCompiler(emitter);
#endif

        RecordPair(emitter, INS_ldr, EA_8BYTE, REG_R19, REG_R20, 0,
            INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        RecordPair(emitter, ins, EA_8BYTE, REG_R20, REG_R20, immediate,
            INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);

        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(1));
        var postIndexed = instructions[0];
        Assert.That(postIndexed.idIns(), Is.EqualTo(INS_ldr));
        Assert.That(postIndexed.idInsFmt(), Is.EqualTo(IF_LS_2C));
        Assert.That(postIndexed.idInsOpt(), Is.EqualTo(INS_OPTS_POST_INDEX));
        Assert.That(postIndexed.idReg1(), Is.EqualTo(REG_R19));
        Assert.That(postIndexed.idReg2(), Is.EqualTo(REG_R20));
        Assert.That(Emitter.emitGetInsSC(postIndexed), Is.EqualTo((nint)expectedOffset));
    }

    [TestCase(INS_nop, 1, false, false, REG_R20, "SVE two-register/immediate")]
    [TestCase(INS_ldr, 0, false, true, REG_R20, "relocatable page-offset load folding")]
    public static void PairImmediateDependenciesRemainExplicit(instruction ins, int imm,
        bool optimized, bool reloc, regNumber reg2, string dependency)
    {
        var emitter = CreateEmitter(optimized, reloc);
        var error = Assert.Throws<FatalJitException>(() =>
            RecordPair(emitter, ins, EA_8BYTE, REG_R19, reg2, imm, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE));
        Assert.That(error, Has.Message.Contains(dependency));
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    private static void CheckPair(instruction ins, emitAttr size, regNumber reg1, regNumber reg2,
        int imm, insOpts opt, Emitter.insFormat format, int expectedImm, instruction expectedIns)
    {
        var emitter = CreateEmitter();
        RecordPair(emitter, ins, size, reg1, reg2, imm, opt, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(expectedIns));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idInsOpt(), Is.EqualTo(opt));
        Assert.That(id.idReg1(), Is.EqualTo(reg1 == REG_SPBASE ? REG_ZR : reg1));
        Assert.That(id.idReg2(), Is.EqualTo(reg2 == REG_SPBASE ? REG_ZR : reg2));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)expectedImm));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_I")]
    private static extern void RecordPair(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, nint imm, insOpts opt, insScalableOpts sopt);

    [TestCase(INS_mov, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2E, INS_OPTS_NONE)]
    [TestCase(INS_mov, EA_4BYTE, REG_R19, REG_ZR, INS_OPTS_NONE, IF_DR_2E, INS_OPTS_NONE)]
    [TestCase(INS_mov, EA_8BYTE, REG_SPBASE, REG_R20, INS_OPTS_NONE, IF_DR_2G, INS_OPTS_NONE)]
    [TestCase(INS_mov, EA_8BYTE, REG_R19, REG_SPBASE, INS_OPTS_NONE, IF_DR_2G, INS_OPTS_NONE)]
    [TestCase(INS_sxtw, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2H, INS_OPTS_NONE)]
    [TestCase(INS_sxtb, EA_4BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2H, INS_OPTS_NONE)]
    [TestCase(INS_sxth, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2H, INS_OPTS_NONE)]
    [TestCase(INS_uxtb, EA_4BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2H, INS_OPTS_NONE)]
    [TestCase(INS_uxth, EA_4BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2H, INS_OPTS_NONE)]
    [TestCase(INS_fmov, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_fmov, EA_4BYTE, REG_V0, REG_R20, INS_OPTS_NONE, IF_DV_2I, INS_OPTS_4BYTE_TO_S)]
    [TestCase(INS_fmov, EA_8BYTE, REG_V0, REG_R20, INS_OPTS_NONE, IF_DV_2I, INS_OPTS_8BYTE_TO_D)]
    [TestCase(INS_fmov, EA_4BYTE, REG_R19, REG_V1, INS_OPTS_NONE, IF_DV_2H, INS_OPTS_S_TO_4BYTE)]
    [TestCase(INS_fmov, EA_8BYTE, REG_R19, REG_V1, INS_OPTS_NONE, IF_DV_2H, INS_OPTS_D_TO_8BYTE)]
    [TestCase(INS_fmov, EA_4BYTE, REG_R19, REG_V1, INS_OPTS_D_TO_4BYTE, IF_DV_2H, INS_OPTS_D_TO_4BYTE)]
    [TestCase(INS_mov, EA_8BYTE, REG_R19, REG_V1, INS_OPTS_NONE, IF_DV_2B, INS_OPTS_NONE)]
    public static void MoveRecordingPreservesNativeFormats(instruction ins, emitAttr size,
        regNumber dst, regNumber src, insOpts opt, Emitter.insFormat format, insOpts expectedOpt)
    {
        var emitter = CreateEmitter();
        RecordMove(emitter, ins, size, dst, src, false, opt);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No move was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idInsOpt(), Is.EqualTo(expectedOpt));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idReg1(), Is.EqualTo(dst == REG_SPBASE ? REG_ZR : dst));
        Assert.That(id.idReg2(), Is.EqualTo(src == REG_SPBASE ? REG_ZR : src));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_dup, EA_16BYTE, REG_V0, REG_R20, INS_OPTS_4S, IF_DV_2C, INS_OPTS_4S)]
    [TestCase(INS_abs, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2L, INS_OPTS_NONE)]
    [TestCase(INS_abs, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_4S)]
    [TestCase(INS_not, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_16B)]
    [TestCase(INS_mvn, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_2S, IF_DV_2M, INS_OPTS_8B)]
    [TestCase(INS_neg, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2L, INS_OPTS_NONE)]
    [TestCase(INS_neg, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2E, INS_OPTS_NONE)]
    [TestCase(INS_mvn, EA_4BYTE, REG_R19, REG_ZR, INS_OPTS_NONE, IF_DR_2E, INS_OPTS_NONE)]
    [TestCase(INS_negs, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2E, INS_OPTS_NONE)]
    [TestCase(INS_sxtl, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_8B, IF_DV_2O, INS_OPTS_8B)]
    [TestCase(INS_cls, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_4S)]
    [TestCase(INS_clz, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2G, INS_OPTS_NONE)]
    [TestCase(INS_rev32, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_DR_2G, INS_OPTS_NONE)]
    [TestCase(INS_rev32, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_8H, IF_DV_2M, INS_OPTS_8H)]
    [TestCase(INS_rbit, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_16B, IF_DV_2M, INS_OPTS_16B)]
    [TestCase(INS_cnt, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_16B, IF_DV_2M, INS_OPTS_16B)]
    [TestCase(INS_addv, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2T, INS_OPTS_4S)]
    [TestCase(INS_rev64, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_4S)]
    [TestCase(INS_sqxtn, EA_4BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2L, INS_OPTS_NONE)]
    [TestCase(INS_xtn, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_2S, IF_DV_2M, INS_OPTS_2S)]
    [TestCase(INS_sqxtn2, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_4S)]
    [TestCase(INS_ldar, EA_8BYTE, REG_R19, REG_SPBASE, INS_OPTS_NONE, IF_LS_2A, INS_OPTS_NONE)]
    [TestCase(INS_ldarb, EA_1BYTE, REG_R19, REG_R20, INS_OPTS_NONE, IF_LS_2A, INS_OPTS_NONE)]
    [TestCase(INS_stlrh, EA_2BYTE, REG_ZR, REG_SPBASE, INS_OPTS_NONE, IF_LS_2A, INS_OPTS_NONE)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, REG_SPBASE, INS_OPTS_NONE, IF_LS_2A, INS_OPTS_NONE)]
    [TestCase(INS_fcmp, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2K, INS_OPTS_NONE)]
    [TestCase(INS_fcvtzs, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_fcvtzs, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_fcvtzs, EA_8BYTE, REG_R19, REG_V1, INS_OPTS_D_TO_8BYTE, IF_DV_2H, INS_OPTS_D_TO_8BYTE)]
    [TestCase(INS_fcvtl, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_2S, IF_DV_2A, INS_OPTS_2S)]
    [TestCase(INS_fcvtn2, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_fcvtxn, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_2S, IF_DV_2A, INS_OPTS_2S)]
    [TestCase(INS_fcvtxn, EA_4BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_fcvtxn2, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_scvtf, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_scvtf, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_scvtf, EA_8BYTE, REG_V0, REG_R20, INS_OPTS_8BYTE_TO_D, IF_DV_2I, INS_OPTS_8BYTE_TO_D)]
    [TestCase(INS_fsqrt, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_fsqrt, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_faddp, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_2D, IF_DV_2Q, INS_OPTS_2D)]
    [TestCase(INS_fmaxv, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2R, INS_OPTS_4S)]
    [TestCase(INS_addp, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_2D, IF_DV_2S, INS_OPTS_2D)]
    [TestCase(INS_fcvt, EA_4BYTE, REG_V0, REG_V1, INS_OPTS_D_TO_S, IF_DV_2J, INS_OPTS_D_TO_S)]
    [TestCase(INS_cmeq, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_4S)]
    [TestCase(INS_cmeq, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2L, INS_OPTS_NONE)]
    [TestCase(INS_frecpe, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_frecpe, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_aesd, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_16B, IF_DV_2P, INS_OPTS_16B)]
    [TestCase(INS_sha1h, EA_4BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2U, INS_OPTS_NONE)]
    [TestCase(INS_sha256su0, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2U, INS_OPTS_4S)]
    [TestCase(INS_sha512su0, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_2D, IF_DV_2V, INS_OPTS_2D)]
    [TestCase(INS_sm4e, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2V, INS_OPTS_4S)]
    [TestCase(INS_ld2, EA_16BYTE, REG_V0, REG_SPBASE, INS_OPTS_4S, IF_LS_2D, INS_OPTS_4S)]
    [TestCase(INS_st1, EA_8BYTE, REG_V0, REG_R20, INS_OPTS_1D, IF_LS_2D, INS_OPTS_1D)]
    [TestCase(INS_ld4r, EA_16BYTE, REG_V0, REG_R20, INS_OPTS_4S, IF_LS_2D, INS_OPTS_4S)]
    [TestCase(INS_urecpe, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2A, INS_OPTS_4S)]
    [TestCase(INS_frecpx, EA_8BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2G, INS_OPTS_NONE)]
    [TestCase(INS_sadalp, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2T, INS_OPTS_4S)]
    [TestCase(INS_sqabs, EA_16BYTE, REG_V0, REG_V1, INS_OPTS_4S, IF_DV_2M, INS_OPTS_4S)]
    [TestCase(INS_sqabs, EA_2BYTE, REG_V0, REG_V1, INS_OPTS_NONE, IF_DV_2L, INS_OPTS_NONE)]
    [TestCase(INS_pacia, EA_8BYTE, REG_R19, REG_SPBASE, INS_OPTS_NONE, IF_PC_2A, INS_OPTS_NONE)]
    public static void RegisterPairRecordingPreservesNativeFormats(instruction ins, emitAttr size,
        regNumber dst, regNumber src, insOpts opt, Emitter.insFormat format, insOpts expectedOpt)
    {
        var emitter = CreateEmitter();
        RecordRegisters(emitter, ins, size, dst, src, opt, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No register pair was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idInsOpt(), Is.EqualTo(expectedOpt));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idReg1(), Is.EqualTo(dst));
        Assert.That(id.idReg2(), Is.EqualTo(src == REG_SPBASE ? REG_ZR : src));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(false, false, EA_8BYTE, REG_R19, 4)]
    [TestCase(false, true, EA_4BYTE, REG_R19, 0)]
    [TestCase(true, false, EA_8BYTE, REG_R19, 0)]
    [TestCase(true, false, EA_4BYTE, REG_R19, 4)]
    [TestCase(true, false, EA_16BYTE, REG_V0, 0)]
    public static void MoveElisionPreservesOptimizationAndClearingRules(
        bool optimized, bool canSkip, emitAttr size, regNumber reg, int expectedSize)
    {
        var emitter = CreateEmitter(optimized);
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = EmitterCompiler(emitter);
#endif
        RecordMove(emitter, INS_mov, size, reg, reg, canSkip, INS_OPTS_NONE);
        Assert.That(GroupSize(emitter), Is.EqualTo(expectedSize));
    }

    [TestCase(INS_sxtb)]
    [TestCase(INS_sxth)]
    [TestCase(INS_sxtw)]
    [TestCase(INS_uxtb)]
    [TestCase(INS_uxth)]
    [TestCase(INS_fmov)]
    public static void MoveElisionHonorsExplicitExtensionAndFloatPermission(instruction ins)
    {
        var emitter = CreateEmitter();
        var reg = ins == INS_fmov ? REG_V0 : REG_R19;
        RecordMove(emitter, ins, EA_8BYTE, reg, reg, true, INS_OPTS_NONE);
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    [TestCase(EA_8BYTE, false, 4)]
    [TestCase(EA_4BYTE, false, 4)]
    [TestCase(EA_8BYTE, true, 4)]
    [TestCase(EA_4BYTE, true, 8)]
    public static void MoveElisionPreservesRepeatedAndOppositeMoveRules(
        emitAttr size, bool opposite, int expectedSize)
    {
        var emitter = CreateEmitter(optimized: true);
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = EmitterCompiler(emitter);
#endif
        RecordMove(emitter, INS_mov, size, REG_R19, REG_R20, false, INS_OPTS_NONE);
        RecordMove(emitter, INS_mov, size, opposite ? REG_R20 : REG_R19,
            opposite ? REG_R19 : REG_R20, false, INS_OPTS_NONE);
        Assert.That(GroupSize(emitter), Is.EqualTo(expectedSize));
    }

    [TestCase(INS_ldr, EA_4BYTE, 4)]
    [TestCase(INS_ldrh, EA_4BYTE, 4)]
    [TestCase(INS_ldrb, EA_4BYTE, 4)]
    [TestCase(INS_ldr, EA_8BYTE, 8)]
    public static void MoveElisionRecognizesPriorZeroExtension(instruction ins, emitAttr size, int expectedSize)
    {
        var emitter = CreateEmitter(optimized: true);
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing emitter compiler.");
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = compiler;
#endif
        compiler.opts.compMinOpts = true;
        compiler.opts.canUseAllOpts = false;
        RecordRegisters(emitter, ins, size, REG_R19, REG_R20, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        compiler.opts.compMinOpts = false;
        compiler.opts.canUseAllOpts = true;
        RecordMove(emitter, INS_mov, EA_4BYTE, REG_R19, REG_R19, false, INS_OPTS_NONE);
        Assert.That(GroupSize(emitter), Is.EqualTo(expectedSize));
    }

    [Test]
    public static void MoveElisionRespectsGroupBoundariesAndMovprfx()
    {
        var emitter = CreateEmitter(optimized: true);
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = EmitterCompiler(emitter);
#endif
        RecordMove(emitter, INS_mov, EA_8BYTE, REG_R19, REG_R20, false, INS_OPTS_NONE);
        ForceNewGroup(emitter) = true;
        Assert.That(RedundantMove(emitter, INS_mov, EA_8BYTE, REG_R19, REG_R20, false), Is.False);
        Assert.That(RedundantMove(emitter, INS_sve_movprfx, EA_16BYTE, REG_V0, REG_V0, false), Is.False);
        Assert.That(RedundantMove(emitter, INS_sve_movprfx, EA_16BYTE, REG_V0, REG_V0, true), Is.True);
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [Test]
    public static void MoveAliasesPreserveZeroAddAndSeparateDependencies()
    {
        var emitter = CreateEmitter();
        RecordPair(emitter, INS_add, EA_8BYTE, REG_R19, REG_R20, 0, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No zero-add alias was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(INS_mov));
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_DR_2E));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));

        var sve = CreateEmitter();
        var moveError = Assert.Throws<FatalJitException>(() =>
            RecordMove(sve, INS_sve_movprfx, EA_8BYTE, REG_V0, REG_V1, false, INS_OPTS_NONE));
        Assert.That(moveError, Has.Message.Contains("SVE move recording"));
        var pairError = Assert.Throws<FatalJitException>(() =>
            RecordRegisters(sve, INS_nop, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE));
        Assert.That(pairError, Has.Message.Contains("SVE two-register recording"));
        Assert.That(GroupSize(sve), Is.Zero);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_Mov")]
    private static extern void RecordMove(Emitter emitter, instruction ins, emitAttr attr,
        regNumber dst, regNumber src, bool canSkip, insOpts opt);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R")]
    private static extern void RecordRegisters(Emitter emitter, instruction ins, emitAttr attr,
        regNumber dst, regNumber src, insOpts opt, insScalableOpts sopt);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "IsRedundantMov")]
    private static extern bool RedundantMove(Emitter emitter, instruction ins, emitAttr size,
        regNumber dst, regNumber src, bool canSkip);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_compiler")]
    private static extern ref Compiler? EmitterCompiler(Emitter emitter);

    [TestCase(INS_mul, EA_8BYTE, false, INS_OPTS_NONE, IF_DR_3A, INS_OPTS_NONE)]
    [TestCase(INS_adc, EA_4BYTE, false, INS_OPTS_NONE, IF_DR_3A, INS_OPTS_NONE)]
    [TestCase(INS_smulh, EA_8BYTE, false, INS_OPTS_NONE, IF_DR_3A, INS_OPTS_NONE)]
    [TestCase(INS_crc32b, EA_4BYTE, false, INS_OPTS_NONE, IF_DR_3A, INS_OPTS_NONE)]
    [TestCase(INS_mul, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_add, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_add, EA_8BYTE, true, INS_OPTS_NONE, IF_DV_3E, INS_OPTS_NONE)]
    [TestCase(INS_cmhi, EA_8BYTE, true, INS_OPTS_NONE, IF_DV_3E, INS_OPTS_NONE)]
    [TestCase(INS_cmhi, EA_16BYTE, true, INS_OPTS_2D, IF_DV_3A, INS_OPTS_2D)]
    [TestCase(INS_sqadd, EA_1BYTE, true, INS_OPTS_NONE, IF_DV_3E, INS_OPTS_NONE)]
    [TestCase(INS_sqadd, EA_16BYTE, true, INS_OPTS_8H, IF_DV_3A, INS_OPTS_8H)]
    [TestCase(INS_fcmeq, EA_8BYTE, true, INS_OPTS_NONE, IF_DV_3D, INS_OPTS_NONE)]
    [TestCase(INS_fcmeq, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3B, INS_OPTS_4S)]
    [TestCase(INS_mla, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_zip1, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_mov, EA_16BYTE, true, INS_OPTS_NONE, IF_DV_3C, INS_OPTS_16B)]
    [TestCase(INS_and, EA_8BYTE, true, INS_OPTS_NONE, IF_DV_3C, INS_OPTS_8B)]
    [TestCase(INS_tbl_4regs, EA_16BYTE, true, INS_OPTS_16B, IF_DV_3C, INS_OPTS_16B)]
    [TestCase(INS_bsl, EA_16BYTE, true, INS_OPTS_NONE, IF_DV_3C, INS_OPTS_16B)]
    [TestCase(INS_fadd, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3B, INS_OPTS_4S)]
    [TestCase(INS_fadd, EA_8BYTE, true, INS_OPTS_NONE, IF_DV_3D, INS_OPTS_NONE)]
    [TestCase(INS_fnmul, EA_4BYTE, true, INS_OPTS_NONE, IF_DV_3D, INS_OPTS_NONE)]
    [TestCase(INS_fmla, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3B, INS_OPTS_4S)]
    [TestCase(INS_stxr, EA_8BYTE, false, INS_OPTS_NONE, IF_LS_3D, INS_OPTS_NONE)]
    [TestCase(INS_cas, EA_8BYTE, false, INS_OPTS_NONE, IF_LS_3E, INS_OPTS_NONE)]
    [TestCase(INS_ldaddb, EA_1BYTE, false, INS_OPTS_NONE, IF_LS_3E, INS_OPTS_NONE)]
    [TestCase(INS_swpalh, EA_2BYTE, false, INS_OPTS_NONE, IF_LS_3E, INS_OPTS_NONE)]
    [TestCase(INS_sha256h, EA_16BYTE, true, INS_OPTS_NONE, IF_DV_3F, INS_OPTS_4S)]
    [TestCase(INS_addhn, EA_8BYTE, true, INS_OPTS_2S, IF_DV_3A, INS_OPTS_2S)]
    [TestCase(INS_addhn2, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_sabal, EA_8BYTE, true, INS_OPTS_2S, IF_DV_3A, INS_OPTS_2S)]
    [TestCase(INS_sabal2, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_sqdmlal, EA_8BYTE, true, INS_OPTS_2S, IF_DV_3A, INS_OPTS_2S)]
    [TestCase(INS_sqdmlal, EA_4BYTE, true, INS_OPTS_NONE, IF_DV_3E, INS_OPTS_NONE)]
    [TestCase(INS_sqdmulh, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_sqdmulh, EA_4BYTE, true, INS_OPTS_NONE, IF_DV_3E, INS_OPTS_NONE)]
    [TestCase(INS_sqdmlal2, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_pmul, EA_8BYTE, true, INS_OPTS_8B, IF_DV_3A, INS_OPTS_8B)]
    [TestCase(INS_pmull, EA_8BYTE, true, INS_OPTS_1D, IF_DV_3A, INS_OPTS_1D)]
    [TestCase(INS_pmull2, EA_16BYTE, true, INS_OPTS_2D, IF_DV_3A, INS_OPTS_2D)]
    [TestCase(INS_sdot, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3A, INS_OPTS_4S)]
    [TestCase(INS_rax1, EA_16BYTE, true, INS_OPTS_2D, IF_DV_3H, INS_OPTS_2D)]
    [TestCase(INS_sm3partw1, EA_16BYTE, true, INS_OPTS_4S, IF_DV_3H, INS_OPTS_4S)]
    public static void ThreeRegisterRecordingPreservesNativeFormats(instruction ins, emitAttr size,
        bool vector, insOpts opt, Emitter.insFormat format, insOpts expectedOpt)
    {
        var emitter = CreateEmitter();
        var reg1 = vector ? REG_V19 : REG_R19;
        var reg2 = vector ? REG_V20 : REG_R20;
        var reg3 = ins == INS_mov ? reg2 : vector ? REG_V21 : REG_R21;
        RecordThree(emitter, ins, size, reg1, reg2, reg3, opt, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No three-register instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idInsOpt(), Is.EqualTo(expectedOpt));
        Assert.That(id.idReg1(), Is.EqualTo(reg1));
        Assert.That(id.idReg2(), Is.EqualTo(reg2));
        Assert.That(id.idReg3(), Is.EqualTo(reg3));
        Assert.That(id.idIsSmallDsc(), Is.False);
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_aesd, INS_OPTS_16B, false)]
    [TestCase(INS_aesd, INS_OPTS_16B, true)]
    [TestCase(INS_fcvtn2, INS_OPTS_4S, false)]
    [TestCase(INS_sadalp, INS_OPTS_4S, false)]
    [TestCase(INS_sqxtn2, INS_OPTS_4S, false)]
    public static void ThreeRegisterRmwPreservesCopyBeforeUpdate(instruction ins, insOpts opt, bool sameSource)
    {
        var emitter = CreateEmitter();
        RecordThree(emitter, ins, EA_16BYTE, REG_V19, sameSource ? REG_V19 : REG_V20,
            REG_V21, opt, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No RMW operation was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idReg1(), Is.EqualTo(REG_V19));
        Assert.That(id.idReg2(), Is.EqualTo(REG_V21));
        Assert.That(GroupSize(emitter), Is.EqualTo(sameSource ? 4 : 8));
    }

    [TestCase(INS_ld2, INS_OPTS_2S)]
    [TestCase(INS_st1, INS_OPTS_1D)]
    [TestCase(INS_ld4r, INS_OPTS_2S)]
    public static void ThreeRegisterStructuresPreservePostIndexAndSpEncoding(instruction ins, insOpts opt)
    {
        var emitter = CreateEmitter();
        RecordThree(emitter, ins, EA_8BYTE, REG_V19, REG_SPBASE, REG_R20, opt, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No post-indexed structure was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_3F));
        Assert.That(id.idReg1(), Is.EqualTo(REG_V19));
        Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(id.idReg3(), Is.EqualTo(REG_R20));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_nop, "SVE three-register")]
    public static void ThreeRegisterDependenciesRemainExplicit(instruction ins, string dependency)
    {
        var emitter = CreateEmitter();
        var error = Assert.Throws<FatalJitException>(() =>
            RecordThree(emitter, ins, EA_8BYTE, REG_R19, REG_R20, REG_R21, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE));
        Assert.That(error, Has.Message.Contains(dependency));
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    [Test]
    public static void ThreeRegisterForwardingPreservesStoreAliasAndIgnoredFlags()
    {
        var emitter = CreateEmitter();
        RecordRegisters(emitter, INS_stadd, EA_8BYTE, REG_R19, REG_R20, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        var atomic = LastInstruction(emitter) ?? throw new AssertionException("No store-add alias was recorded.");
        Assert.That(atomic.idIns(), Is.EqualTo(INS_ldadd));
        Assert.That(atomic.idReg1(), Is.EqualTo(REG_R19));
        Assert.That(atomic.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(atomic.idReg3(), Is.EqualTo(REG_R20));

        RecordRegistersWithFlags(emitter, INS_neg, EA_8BYTE, REG_R19, REG_R20, insFlags.INS_FLAGS_DONT_CARE);
        var unary = LastInstruction(emitter) ?? throw new AssertionException("No flags-forwarded pair was recorded.");
        Assert.That(unary.idInsFmt(), Is.EqualTo(IF_DR_2E));
        Assert.That(GroupSize(emitter), Is.EqualTo(8));
    }

    [TestCase(INS_ldrb, EA_1BYTE, REG_R19, INS_OPTS_NONE, -1, true)]
    [TestCase(INS_ldrb, EA_1BYTE, REG_R19, INS_OPTS_NONE, 0, true)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, INS_OPTS_NONE, -1, false)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, INS_OPTS_LSL, -1, true)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, INS_OPTS_UXTW, 0, false)]
    [TestCase(INS_ldr, EA_8BYTE, REG_R19, INS_OPTS_SXTW, 3, true)]
    [TestCase(INS_ldrh, EA_2BYTE, REG_R19, INS_OPTS_NONE, -1, false)]
    [TestCase(INS_ldrh, EA_2BYTE, REG_R19, INS_OPTS_UXTW, 1, true)]
    [TestCase(INS_ldrsw, EA_8BYTE, REG_R19, INS_OPTS_SXTW, 2, true)]
    [TestCase(INS_ldrsb, EA_8BYTE, REG_R19, INS_OPTS_LSL, -1, true)]
    [TestCase(INS_ldr, EA_16BYTE, REG_V19, INS_OPTS_LSL, -1, true)]
    [TestCase(INS_str, EA_4BYTE, REG_V19, INS_OPTS_UXTX, 0, false)]
    [TestCase(INS_str, EA_16BYTE, REG_V19, INS_OPTS_SXTX, 4, true)]
    public static void ThreeRegisterExtendedMemoryPreservesNaturalAndExplicitScale(instruction ins,
        emitAttr size, regNumber reg, insOpts opt, int shift, bool scaled)
    {
        var emitter = CreateEmitter();
        RecordExtended(emitter, ins, size, reg, REG_SPBASE, REG_R20, opt, shift);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No extended-register memory operand was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_3A));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idInsOpt(), Is.EqualTo(opt));
        Assert.That(id.idReg1(), Is.EqualTo(reg));
        Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(id.idReg3(), Is.EqualTo(REG_R20));
        Assert.That(id.idReg3Scaled(), Is.EqualTo(scaled));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [Test]
    public static void ThreeRegisterMemoryDelegatesToExtendedRecording()
    {
        var emitter = CreateEmitter();
        RecordThree(emitter, INS_ldr, EA_8BYTE, REG_R19, REG_SPBASE, REG_R20,
            INS_OPTS_LSL, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No three-register memory load was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_3A));
        Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(id.idReg3Scaled(), Is.True);
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_Ext")]
    private static extern void RecordExtended(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, insOpts opt, int shift);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R")]
    private static extern void RecordThree(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, insOpts opt, insScalableOpts sopt);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R")]
    private static extern void RecordRegistersWithFlags(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, insFlags flags);

    [TestCase(INS_extr, EA_8BYTE, false, 63, INS_OPTS_NONE, IF_DR_3E)]
    [TestCase(INS_and, EA_8BYTE, false, 0, INS_OPTS_NONE, IF_DR_3A)]
    [TestCase(INS_orr, EA_4BYTE, false, 31, INS_OPTS_ROR, IF_DR_3B)]
    [TestCase(INS_fmul, EA_16BYTE, true, 3, INS_OPTS_4S, IF_DV_3BI)]
    [TestCase(INS_fmla, EA_8BYTE, true, 1, INS_OPTS_NONE, IF_DV_3DI)]
    [TestCase(INS_fmls, EA_4BYTE, true, 3, INS_OPTS_NONE, IF_DV_3DI)]
    [TestCase(INS_fmulx, EA_16BYTE, true, 1, INS_OPTS_2D, IF_DV_3BI)]
    [TestCase(INS_mul, EA_16BYTE, true, 7, INS_OPTS_8H, IF_DV_3AI)]
    [TestCase(INS_mla, EA_8BYTE, true, 3, INS_OPTS_2S, IF_DV_3AI)]
    [TestCase(INS_mls, EA_16BYTE, true, 3, INS_OPTS_4S, IF_DV_3AI)]
    [TestCase(INS_add, EA_8BYTE, false, 4, INS_OPTS_UXTW, IF_DR_3C)]
    [TestCase(INS_sub, EA_8BYTE, false, 63, INS_OPTS_LSL, IF_DR_3B)]
    [TestCase(INS_adds, EA_4BYTE, false, 0, INS_OPTS_NONE, IF_DR_3A)]
    [TestCase(INS_subs, EA_8BYTE, false, 4, INS_OPTS_SXTX, IF_DR_3C)]
    [TestCase(INS_ext, EA_16BYTE, true, 15, INS_OPTS_16B, IF_DV_3G)]
    [TestCase(INS_smlal, EA_8BYTE, true, 7, INS_OPTS_4H, IF_DV_3AI)]
    [TestCase(INS_umull, EA_8BYTE, true, 3, INS_OPTS_2S, IF_DV_3AI)]
    [TestCase(INS_sqdmlal, EA_8BYTE, true, 7, INS_OPTS_4H, IF_DV_3AI)]
    [TestCase(INS_sqdmull, EA_2BYTE, true, 7, INS_OPTS_NONE, IF_DV_3EI)]
    [TestCase(INS_sqdmulh, EA_16BYTE, true, 7, INS_OPTS_8H, IF_DV_3AI)]
    [TestCase(INS_sqrdmlah, EA_4BYTE, true, 3, INS_OPTS_NONE, IF_DV_3EI)]
    [TestCase(INS_smlal2, EA_16BYTE, true, 7, INS_OPTS_8H, IF_DV_3AI)]
    [TestCase(INS_sqdmull2, EA_16BYTE, true, 3, INS_OPTS_4S, IF_DV_3AI)]
    [TestCase(INS_sdot, EA_8BYTE, true, 3, INS_OPTS_2S, IF_DV_3AI)]
    [TestCase(INS_udot, EA_16BYTE, true, 3, INS_OPTS_4S, IF_DV_3AI)]
    [TestCase(INS_xar, EA_16BYTE, true, 63, INS_OPTS_2D, IF_DV_3I)]
    public static void ThreeImmediateRecordingPreservesNativeFormats(instruction ins, emitAttr size,
        bool vector, int imm, insOpts opt, Emitter.insFormat format)
    {
        var emitter = CreateEmitter();
        var reg1 = vector ? REG_V19 : REG_R19;
        var reg2 = vector ? REG_V20 : REG_R20;
        var reg3 = vector ? (opt is INS_OPTS_8H or INS_OPTS_4H || size == EA_2BYTE ? REG_V15 : REG_V31) : REG_R21;
        RecordThreeImmediate(emitter, ins, size, reg1, reg2, reg3, imm, opt, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No three-register/immediate instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idInsOpt(), Is.EqualTo(opt));
        Assert.That(id.idReg1(), Is.EqualTo(reg1));
        Assert.That(id.idReg2(), Is.EqualTo(reg2));
        Assert.That(id.idReg3(), Is.EqualTo(reg3));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)imm));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_ldp, EA_8BYTE, false, -512, INS_OPTS_PRE_INDEX, -64)]
    [TestCase(INS_stp, EA_8BYTE, false, 504, INS_OPTS_POST_INDEX, 63)]
    [TestCase(INS_ldnp, EA_4BYTE, false, 252, INS_OPTS_NONE, 63)]
    [TestCase(INS_stnp, EA_4BYTE, false, -256, INS_OPTS_NONE, -64)]
    [TestCase(INS_ldpsw, EA_8BYTE, false, -256, INS_OPTS_NONE, -64)]
    [TestCase(INS_ldp, EA_16BYTE, true, 1008, INS_OPTS_NONE, 63)]
    [TestCase(INS_stp, EA_16BYTE, true, -1024, INS_OPTS_NONE, -64)]
    [TestCase(INS_ldp, EA_8BYTE, false, 0, INS_OPTS_NONE, 0)]
    public static void ThreeImmediatePairsPreserveScaleAndSecondGcType(instruction ins, emitAttr size,
        bool vector, int imm, insOpts opt, int scaled)
    {
        var emitter = CreateEmitter();
        RecordThreeImmediate(emitter, ins, size, vector ? REG_V19 : REG_R19,
            vector ? REG_V20 : REG_R20, REG_SPBASE, imm, opt, vector ? EA_UNKNOWN : EA_BYREF, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No load/store pair was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(imm == 0 ? IF_LS_3B : IF_LS_3C));
        Assert.That(id.idReg3(), Is.EqualTo(REG_ZR));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)scaled));
        Assert.That(id.idGCrefReg2(), Is.EqualTo(vector ? GCInfo.GCtype.GCT_NONE : GCInfo.GCtype.GCT_BYREF));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_add)]
    [TestCase(INS_sub)]
    [TestCase(INS_adds)]
    [TestCase(INS_subs)]
    public static void ThreeImmediateSpSourceSelectsExtendedEncoding(instruction ins)
    {
        var emitter = CreateEmitter();
        RecordThreeImmediate(emitter, ins, EA_8BYTE, REG_R19, REG_SPBASE, REG_R20, 0,
            INS_OPTS_NONE, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No SP arithmetic was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_DR_3C));
        Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_LSL));
        Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
    }

    [TestCase(INS_ld1)]
    [TestCase(INS_st4)]
    public static void ThreeImmediateStructuresPreservePostIndex(instruction ins)
    {
        var emitter = CreateEmitter();
        RecordThreeImmediate(emitter, ins, EA_2BYTE, REG_V19, REG_SPBASE, REG_R20, 7,
            INS_OPTS_POST_INDEX, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No indexed structure was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_3G));
        Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)7));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ThreeImmediateRmwPreservesCopyBeforeShift(bool sameSource)
    {
        var emitter = CreateEmitter();
        RecordThreeImmediate(emitter, INS_sli, EA_16BYTE, REG_V19, sameSource ? REG_V19 : REG_V20,
            REG_V21, 7, INS_OPTS_16B, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No RMW shift was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(INS_sli));
        Assert.That(id.idReg1(), Is.EqualTo(REG_V19));
        Assert.That(id.idReg2(), Is.EqualTo(REG_V21));
        Assert.That(GroupSize(emitter), Is.EqualTo(sameSource ? 4 : 8));
    }

    [TestCase(INS_add, IF_DR_3A)]
    [TestCase(INS_and, IF_DR_3A)]
    [TestCase(INS_ldp, IF_LS_3B)]
    public static void ThreeImmediateDependenciesNowRecord(instruction ins, Emitter.insFormat format)
    {
        var emitter = CreateEmitter();
        RecordThree(emitter, ins, EA_8BYTE, REG_R19, REG_R20, REG_R21, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No forwarded instruction was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(3, 4, EA_GCREF)]
    [TestCase(3, -1, EA_BYREF)]
    [TestCase(-1, 4, EA_8BYTE)]
    [TestCase(-1, -1, EA_8BYTE)]
    public static void LocalPairRecordingPreservesAddressesAndGc(int first, int second, emitAttr attr2)
    {
        var emitter = CreateEmitter();
        RecordLocalPair(emitter, INS_ldp, EA_GCREF, attr2, REG_R19, REG_R20, REG_SPBASE,
            16, first, second, 24, 40
#if DEBUG
            , 101, 202
#endif
            );
        var id = LastInstruction(emitter) ?? throw new AssertionException("No local pair was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_3C));
        Assert.That(id.idReg3(), Is.EqualTo(REG_ZR));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)2));
        Assert.That(id.idIsLclVarPair(), Is.EqualTo(first != -1 && second != -1));
        Assert.That(id.idIsLclVar(), Is.EqualTo(first != -1 || second != -1));
        Assert.That(id.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
        Assert.That(id.idGCrefReg2(), Is.EqualTo(attr2 == EA_GCREF ? GCInfo.GCtype.GCT_GCREF :
            attr2 == EA_BYREF ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
        if (first != -1 || second != -1)
        {
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(first != -1 ? first : second));
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(first != -1 ? 24 : 40));
        }
        if (first != -1 && second != -1)
        {
            ref var address = ref SecondLocal(null, id);
            Assert.That(address.lvaVarNum(), Is.EqualTo(second));
            Assert.That(address.lvaOffset(), Is.EqualTo(40));
        }
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing local-reference metadata.");
        Assert.That(debugInfo.idVarRefOffs, Is.EqualTo(101));
        Assert.That(debugInfo.idVarRefOffs2, Is.EqualTo(202));
#endif
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(0L)]
    [TestCase(-1L)]
    [TestCase(long.MaxValue)]
    [TestCase(long.MinValue)]
    public static void LocalPairAllocationPreservesConstantAndSecondLocal(long constant)
    {
        var emitter = CreateEmitter();
        var id = AllocateLocalPair(emitter, EA_8BYTE, (nint)constant);
        Assert.That(id.idIsLclVarPair(), Is.True);
        Assert.That(id.idIsLargeCns(), Is.EqualTo(!Emitter.instrDesc.fitsInSmallCns((nint)constant)));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)constant));
        ref var address = ref SecondLocal(null, id);
        address.initLclVarAddr(40000, 128);
        Assert.That(SecondLocal(null, id).lvaVarNum(), Is.EqualTo(40000));
        Assert.That(SecondLocal(null, id).lvaOffset(), Is.EqualTo(128));
        Assert.That(LastInstruction(emitter), Is.SameAs(id));
    }

    [TestCase(0, 0)]
    [TestCase(-1024, -64)]
    public static void LocalPairRecordingPreservesVectorScale(int imm, int scaled)
    {
        var emitter = CreateEmitter();
        RecordLocalPair(emitter, INS_stp, EA_16BYTE, EA_16BYTE, REG_V19, REG_V20, REG_SPBASE,
            imm, 3, 4, 0, 16
#if DEBUG
            , 0, 16
#endif
            );
        var id = LastInstruction(emitter) ?? throw new AssertionException("No vector local pair was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(imm == 0 ? IF_LS_3B : IF_LS_3C));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)scaled));
        Assert.That(id.idReg1(), Is.EqualTo(REG_V19));
        Assert.That(id.idReg2(), Is.EqualTo(REG_V20));
        Assert.That(id.idGCrefReg2(), Is.EqualTo(GCInfo.GCtype.GCT_NONE));
        Assert.That(id.idIsLclVarPair(), Is.True);
    }

#if DEBUG
    [TestCase(INS_mul, EA_16BYTE, INS_OPTS_8H)]
    [TestCase(INS_smlal, EA_8BYTE, INS_OPTS_4H)]
    [TestCase(INS_sqdmull, EA_2BYTE, INS_OPTS_NONE)]
    [TestCase(INS_sqdmulh, EA_16BYTE, INS_OPTS_8H)]
    [TestCase(INS_smlal2, EA_16BYTE, INS_OPTS_8H)]
    public static void IndexedHalfwordRejectsUnencodableRegister(instruction ins, emitAttr size, insOpts opt)
    {
        var emitter = CreateEmitter();
        var error = Assert.Catch(() => RecordThreeImmediate(emitter, ins, size,
            REG_V19, REG_V20, REG_V16, 0, opt, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE));
        if (ins == INS_mul)
        {
            Assert.That(error, Is.TypeOf<FatalJitException>());
        }
        else
        {
            Assert.That(error, Has.Message.Contains("Invalid reg3"));
        }
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    [TestCase(INS_ldp, REG_R19, REG_R19, REG_R21, 8, INS_OPTS_NONE, "reg1 != reg2")]
    [TestCase(INS_stp, REG_R19, REG_R20, REG_R19, 8, INS_OPTS_PRE_INDEX, "reg1 != reg3")]
    [TestCase(INS_ldpsw, REG_R19, REG_R20, REG_R21, 256, INS_OPTS_NONE, "Instruction cannot be encoded")]
    public static void ImmediatePairRejectsReservedEncoding(instruction ins, regNumber reg1,
        regNumber reg2, regNumber reg3, int imm, insOpts opt, string condition)
    {
        var emitter = CreateEmitter();
        var error = Assert.Catch(() => RecordThreeImmediate(emitter, ins, EA_8BYTE,
            reg1, reg2, reg3, imm, opt, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE));
        Assert.That(error, Has.Message.Contains(condition));
        Assert.That(GroupSize(emitter), Is.Zero);
    }
#endif

    [Test]
    public static void ImmediateSveDependencyRemainsExplicit()
    {
        var emitter = CreateEmitter();
        var error = Assert.Throws<FatalJitException>(() => RecordThreeImmediate(emitter, INS_nop,
            EA_8BYTE, REG_R19, REG_R20, REG_R21, 0, INS_OPTS_NONE, EA_UNKNOWN, INS_SCALABLE_OPTS_NONE));
        Assert.That(error, Has.Message.Contains("SVE three-register/immediate"));
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_I")]
    private static extern void RecordThreeImmediate(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, nint imm, insOpts opt, emitAttr attrReg2, insScalableOpts sopt);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_I_LdStPair")]
    private static extern void RecordLocalPair(Emitter emitter, instruction ins, emitAttr size, emitAttr size2,
        regNumber reg1, regNumber reg2, regNumber reg3, nint imm, int var1, int var2, int offs1, int offs2
#if DEBUG
        , uint var1RefsOffs, uint var2RefsOffs
#endif
        );

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrLclVarPair")]
    private static extern Emitter.instrDesc AllocateLocalPair(Emitter emitter, emitAttr attr, nint cns);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitGetLclVarPairLclVar2")]
    private static extern ref emitLclVarAddr SecondLocal(Emitter? emitter, Emitter.instrDesc id);

    [TestCase(INS_bfm, EA_8BYTE, 63, 0, IF_DI_2D, 8128)]
    [TestCase(INS_sbfm, EA_4BYTE, 0, 31, IF_DI_2D, 31)]
    [TestCase(INS_ubfm, EA_8BYTE, 3, 63, IF_DI_2D, 4351)]
    [TestCase(INS_bfi, EA_8BYTE, 1, 63, IF_DI_2D, 8190)]
    [TestCase(INS_sbfiz, EA_4BYTE, 31, 1, IF_DI_2D, 64)]
    [TestCase(INS_ubfiz, EA_8BYTE, 63, 1, IF_DI_2D, 4160)]
    [TestCase(INS_bfxil, EA_8BYTE, 32, 32, IF_DI_2D, 6207)]
    [TestCase(INS_sbfx, EA_4BYTE, 1, 31, IF_DI_2D, 95)]
    [TestCase(INS_ubfx, EA_8BYTE, 63, 1, IF_DI_2D, 8191)]
    [TestCase(INS_mov, EA_1BYTE, 15, 14, IF_DV_2F, 254)]
    [TestCase(INS_ins, EA_8BYTE, 1, 0, IF_DV_2F, 16)]
    [TestCase(INS_ld1, EA_1BYTE, 15, 1, IF_LS_2G, 15)]
    [TestCase(INS_ld2, EA_2BYTE, 7, 4, IF_LS_2G, 7)]
    [TestCase(INS_st3, EA_4BYTE, 3, 12, IF_LS_2G, 3)]
    [TestCase(INS_st4, EA_8BYTE, 1, 32, IF_LS_2G, 1)]
    public static void MultioperandTwoImmediatesPreserveEncoding(instruction ins, emitAttr size,
        int imm1, int imm2, Emitter.insFormat format, int encoded)
    {
        var emitter = CreateEmitter();
        var vector = format != IF_DI_2D;
        var memory = format == IF_LS_2G;
        var reg1 = vector ? REG_V19 : REG_R19;
        var reg2 = memory ? REG_SPBASE : vector ? REG_V20 : REG_R20;
        var opt = memory ? INS_OPTS_POST_INDEX : INS_OPTS_NONE;
        RecordTwoImmediates(emitter, ins, size, reg1, reg2, imm1, imm2, opt);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No two-immediate instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idReg1(), Is.EqualTo(reg1));
        Assert.That(id.idReg2(), Is.EqualTo(memory ? REG_ZR : reg2));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)encoded));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultioperandInsertCopiesBeforeUpdating(bool sameSource)
    {
        var emitter = CreateEmitter();
        RecordThreeTwoImmediates(emitter, INS_ins, EA_8BYTE, REG_V19, sameSource ? REG_V19 : REG_V20,
            REG_V21, 1, 0, INS_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No element insertion was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_DV_2F));
        Assert.That(id.idReg2(), Is.EqualTo(REG_V21));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)16));
        Assert.That(GroupSize(emitter), Is.EqualTo(sameSource ? 4 : 8));
    }

    [TestCase(INS_madd, EA_4BYTE, INS_OPTS_NONE, IF_DR_4A)]
    [TestCase(INS_msub, EA_8BYTE, INS_OPTS_NONE, IF_DR_4A)]
    [TestCase(INS_smaddl, EA_8BYTE, INS_OPTS_NONE, IF_DR_4A)]
    [TestCase(INS_umaddl, EA_8BYTE, INS_OPTS_NONE, IF_DR_4A)]
    [TestCase(INS_fmadd, EA_4BYTE, INS_OPTS_NONE, IF_DV_4A)]
    [TestCase(INS_fnmsub, EA_8BYTE, INS_OPTS_NONE, IF_DV_4A)]
    [TestCase(INS_eor3, EA_16BYTE, INS_OPTS_16B, IF_DV_4B)]
    [TestCase(INS_bcax, EA_16BYTE, INS_OPTS_16B, IF_DV_4B)]
    [TestCase(INS_sm3ss1, EA_16BYTE, INS_OPTS_4S, IF_DV_4B)]
    public static void MultioperandFourRegistersPreserveFormats(instruction ins, emitAttr size,
        insOpts opt, Emitter.insFormat format)
    {
        var emitter = CreateEmitter();
        var vector = format != IF_DR_4A;
        var reg1 = vector ? REG_V19 : REG_R19;
        var reg2 = vector ? REG_V20 : REG_R20;
        var reg3 = vector ? REG_V21 : REG_R21;
        var reg4 = vector ? REG_V22 : REG_R22;
        RecordFour(emitter, ins, size, reg1, reg2, reg3, reg4, opt, INS_SCALABLE_OPTS_NONE);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No four-register instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idReg1(), Is.EqualTo(reg1));
        Assert.That(id.idReg2(), Is.EqualTo(reg2));
        Assert.That(id.idReg3(), Is.EqualTo(reg3));
        Assert.That(id.idReg4(), Is.EqualTo(reg4));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_sqdmlal, EA_2BYTE, INS_OPTS_NONE, EA_8BYTE, false, false)]
    [TestCase(INS_sqdmlal, EA_2BYTE, INS_OPTS_NONE, EA_8BYTE, false, true)]
    [TestCase(INS_smlal, EA_8BYTE, INS_OPTS_4H, EA_16BYTE, false, false)]
    [TestCase(INS_fmla, EA_16BYTE, INS_OPTS_4S, EA_16BYTE, false, false)]
    [TestCase(INS_sqdmlal, EA_2BYTE, INS_OPTS_NONE, EA_8BYTE, true, false)]
    [TestCase(INS_smlal, EA_8BYTE, INS_OPTS_4H, EA_16BYTE, true, false)]
    [TestCase(INS_fmla, EA_16BYTE, INS_OPTS_4S, EA_16BYTE, true, false)]
    public static void MultioperandRmwPreservesDestinationCopyWidth(instruction ins, emitAttr size,
        insOpts opt, emitAttr copySize, bool immediate, bool sameSource)
    {
        var emitter = CreateEmitter();
        var reg2 = sameSource ? REG_V19 : REG_V20;
        if (immediate)
        {
            RecordFourImmediate(emitter, ins, size, REG_V19, reg2, REG_V21, REG_V15,
                size == EA_2BYTE || opt == INS_OPTS_4H ? 7 : 3, opt);
        }
        else
        {
            RecordFour(emitter, ins, size, REG_V19, reg2, REG_V21, REG_V15, opt, INS_SCALABLE_OPTS_NONE);
        }
        var id = LastInstruction(emitter) ?? throw new AssertionException("No four-operand RMW operation was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idReg1(), Is.EqualTo(REG_V19));
        Assert.That(id.idReg2(), Is.EqualTo(REG_V21));
        Assert.That(id.idReg3(), Is.EqualTo(REG_V15));
        Assert.That(GroupSize(emitter), Is.EqualTo(sameSource ? 4 : 8));
        if (!sameSource)
        {
            var copy = CurrentInstructions(emitter)[0];
            Assert.That(copy.idIns(), Is.EqualTo(INS_mov));
            Assert.That(copy.idOpSize(), Is.EqualTo(copySize));
            Assert.That(copy.idReg1(), Is.EqualTo(REG_V19));
            Assert.That(copy.idReg2(), Is.EqualTo(reg2));
        }
    }

    [TestCase(INS_cset, 1, IF_DR_1D)]
    [TestCase(INS_csetm, 1, IF_DR_1D)]
    [TestCase(INS_cinc, 2, IF_DR_2D)]
    [TestCase(INS_cinv, 2, IF_DR_2D)]
    [TestCase(INS_cneg, 2, IF_DR_2D)]
    [TestCase(INS_csel, 3, IF_DR_3D)]
    [TestCase(INS_csinc, 3, IF_DR_3D)]
    [TestCase(INS_csinv, 3, IF_DR_3D)]
    [TestCase(INS_csneg, 3, IF_DR_3D)]
    public static void ConditionalRecordingPreservesEveryCondition(instruction ins, int registers,
        Emitter.insFormat format)
    {
        foreach (var condition in Enum.GetValues<insCond>())
        {
            var emitter = CreateEmitter();
            if (registers == 1)
            {
                RecordCondition(emitter, ins, EA_8BYTE, REG_R19, condition);
            }
            else if (registers == 2)
            {
                RecordPairCondition(emitter, ins, EA_8BYTE, REG_R19, REG_ZR, condition);
            }
            else
            {
                RecordThreeCondition(emitter, ins, EA_8BYTE, REG_R19, REG_ZR, REG_R20, condition);
            }
            var id = LastInstruction(emitter) ?? throw new AssertionException("No conditional instruction was recorded.");
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)condition));
            Assert.That(id.idIsSmallDsc(), Is.EqualTo(registers != 3));
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
        }
    }

    [TestCase(INS_ccmp)]
    [TestCase(INS_ccmn)]
    public static void ConditionalRecordingPreservesAndTruncatesFlagBits(instruction ins)
    {
        foreach (var condition in Enum.GetValues<insCond>())
        {
            for (uint flags = 0; flags < 32; flags++)
            {
                var emitter = CreateEmitter();
                RecordFlagsCondition(emitter, ins, EA_8BYTE, REG_R19, REG_R20, (insCFlags)flags, condition);
                var id = LastInstruction(emitter) ?? throw new AssertionException("No conditional register comparison was recorded.");
                Assert.That(id.idInsFmt(), Is.EqualTo(IF_DR_2I));
                Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)(((flags & 15) << 4) | (uint)condition)));
            }
        }
    }

    [TestCase(INS_ccmp, 0, INS_ccmp)]
    [TestCase(INS_ccmp, 31, INS_ccmp)]
    [TestCase(INS_ccmp, -31, INS_ccmn)]
    [TestCase(INS_ccmn, 0, INS_ccmn)]
    [TestCase(INS_ccmn, 31, INS_ccmn)]
    [TestCase(INS_ccmn, -31, INS_ccmp)]
    public static void ConditionalRecordingImmediateReversesNegativeOperands(instruction ins, int imm,
        instruction expected)
    {
        foreach (var condition in Enum.GetValues<insCond>())
        {
            var emitter = CreateEmitter();
            RecordImmediateCondition(emitter, ins, EA_8BYTE, REG_R19, imm, insCFlags.INS_FLAGS_NZCV, condition);
            var id = LastInstruction(emitter) ?? throw new AssertionException("No conditional immediate comparison was recorded.");
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_DI_1F));
            Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)((Math.Abs(imm) << 8) | 0xF0 | (int)condition)));
        }
    }

    [TestCase(INS_dmb, 1)]
    [TestCase(INS_dmb, 2)]
    [TestCase(INS_dmb, 3)]
    [TestCase(INS_dmb, 5)]
    [TestCase(INS_dmb, 6)]
    [TestCase(INS_dmb, 7)]
    [TestCase(INS_dmb, 9)]
    [TestCase(INS_dmb, 10)]
    [TestCase(INS_dmb, 11)]
    [TestCase(INS_dmb, 13)]
    [TestCase(INS_dmb, 14)]
    [TestCase(INS_dmb, 15)]
    [TestCase(INS_dsb, 3)]
    [TestCase(INS_isb, 15)]
    public static void BarrierRecordingPreservesImmediate(instruction ins, int barrier)
    {
        var emitter = CreateEmitter();
        RecordBarrier(emitter, ins, (insBarrier)barrier);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No barrier was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_SI_0B));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)barrier));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(0, "three-register/two-immediate")]
    [TestCase(1, "four-register recording")]
    [TestCase(2, "four-register/immediate")]
    public static void SveMultioperandFallbackTerminates(int form, string dependency)
    {
        var emitter = CreateEmitter();
        var error = Assert.Throws<FatalJitException>(() =>
        {
            switch (form)
            {
                case 0:
                {
                    RecordThreeTwoImmediates(emitter, INS_nop, EA_8BYTE,
                        REG_V19, REG_V20, REG_V21, 0, 0, INS_OPTS_NONE);
                    break;
                }
                case 1:
                {
                    RecordFour(emitter, INS_nop, EA_8BYTE,
                        REG_V19, REG_V20, REG_V21, REG_V22, INS_OPTS_NONE, INS_SCALABLE_OPTS_NONE);
                    break;
                }
                default:
                {
                    RecordFourImmediate(emitter, INS_nop, EA_8BYTE,
                        REG_V19, REG_V20, REG_V21, REG_V22, 0, INS_OPTS_NONE);
                    break;
                }
            }
        });
        Assert.That(error, Has.Message.Contains(dependency));
        Assert.That(GroupSize(emitter), Is.Zero);
    }

#if DEBUG
    [TestCase(32)]
    [TestCase(-32)]
    public static void ConditionalImmediateRejectsWideOperands(int imm)
    {
        var emitter = CreateEmitter();
        var error = Assert.Catch(() => RecordImmediateCondition(emitter, INS_ccmp, EA_8BYTE,
            REG_R19, imm, insCFlags.INS_FLAGS_NONE, insCond.INS_COND_EQ));
        Assert.That(error, Has.Message.Contains("ccmp/ccmn imm5"));
        Assert.That(GroupSize(emitter), Is.Zero);
    }

    [TestCase(14)]
    [TestCase(15)]
    public static void ConditionalCodeRejectsReservedValues(int condition)
    {
        var emitter = CreateEmitter();
        var error = Assert.Catch(() => RecordCondition(emitter, INS_cset, EA_8BYTE, REG_R19, (insCond)condition));
        Assert.That(error, Has.Message.Contains("isValidImmCond"));
        Assert.That(GroupSize(emitter), Is.Zero);
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_I_I")]
    private static extern void RecordTwoImmediates(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, nint imm1, nint imm2, insOpts opt);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_I_I")]
    private static extern void RecordThreeTwoImmediates(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, nint imm1, nint imm2, insOpts opt);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_R")]
    private static extern void RecordFour(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, regNumber reg4, insOpts opt, insScalableOpts sopt);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_R_I")]
    private static extern void RecordFourImmediate(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, regNumber reg4, nint imm, insOpts opt);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc> CurrentInstructions(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_COND")]
    private static extern void RecordCondition(Emitter emitter, instruction ins, emitAttr size, regNumber reg, insCond cond);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_COND")]
    private static extern void RecordPairCondition(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, insCond cond);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_R_COND")]
    private static extern void RecordThreeCondition(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, regNumber reg3, insCond cond);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_FLAGS_COND")]
    private static extern void RecordFlagsCondition(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg1, regNumber reg2, insCFlags flags, insCond cond);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_I_FLAGS_COND")]
    private static extern void RecordImmediateCondition(Emitter emitter, instruction ins, emitAttr size,
        regNumber reg, nint imm, insCFlags flags, insCond cond);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_BARR")]
    private static extern void RecordBarrier(Emitter emitter, instruction ins, insBarrier barrier);

    [TestCase(0L, 1, true)]
    [TestCase(-1L, 1, true)]
    [TestCase(1L, 1, false)]
    [TestCase(-2L, 1, false)]
    [TestCase(-256L, 9, true)]
    [TestCase(255L, 9, true)]
    [TestCase(-257L, 9, false)]
    [TestCase(256L, 9, false)]
    [TestCase(-2147483648L, 32, true)]
    [TestCase(2147483648L, 32, false)]
    [TestCase(-4611686018427387904L, 63, true)]
    [TestCase(4611686018427387904L, 63, false)]
    [TestCase(long.MinValue, 64, true)]
    [TestCase(long.MaxValue, 64, true)]
    public static void SignedImmediateBoundsIncludeFullNativeWidth(long value, int bits, bool expected)
    {
        Assert.That(ValidSignedImmediate(null, (nint)value, bits), Is.EqualTo(expected));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidSimm")]
    private static extern bool ValidSignedImmediate(Emitter? emitter, nint value, int bits);

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void ExtraRegisterAccessReadsNativeWord(bool fourth)
    {
        var descriptor = RecordingEmitter.Basic(INS_nop, IF_DR_4A);
        Unsafe.As<Emitter.instrDesc.idAddrUnion, ulong>(ref descriptor.idAddr()) =
            ((ulong)REG_R19 << 35) | ((ulong)REG_R28 << 42);
        Assert.That(fourth ? descriptor.idReg4() : descriptor.idReg3(), Is.EqualTo(fourth ? REG_R28 : REG_R19));
    }

    [TestCase(IF_DR_3A, REG_R2, REG_R3)]
    [TestCase(IF_DR_3B, REG_LR, REG_R0)]
    [TestCase(IF_DR_4A, REG_R0, REG_LR)]
    public static void SanityChecksUseThirdAndFourthRegisters(
        Emitter.insFormat format, regNumber third, regNumber fourth)
    {
        var descriptor = RecordingEmitter.Basic(INS_nop, format);
        descriptor.idOpSize(EA_8BYTE);
        descriptor.idReg1(REG_R0);
        descriptor.idReg2(REG_R1);
        Unsafe.As<Emitter.instrDesc.idAddrUnion, ulong>(ref descriptor.idAddr()) =
            ((ulong)third << 35) | ((ulong)fourth << 42);
        CheckSanity(CreateEmitter(), descriptor);
    }

    [TestCase(IF_BI_1A, EA_4BYTE, REG_LR, REG_R0)]
    [TestCase(IF_BR_1A, EA_8BYTE, REG_R0, REG_R0)]
    [TestCase(IF_DR_2A, EA_8BYTE, REG_ZR, REG_R0)]
    [TestCase(IF_DR_2G, EA_8BYTE, REG_ZR, REG_ZR)]
    [TestCase(IF_DV_1C, EA_4BYTE, REG_V31, REG_R0)]
    [TestCase(IF_DV_2G, EA_2BYTE, REG_V0, REG_V31)]
    [TestCase(IF_PC_2A, EA_8BYTE, REG_LR, REG_ZR)]
    [TestCase(IF_SR_1A, EA_8BYTE, REG_R0, REG_R0)]
    public static void SanityChecksAcceptNativeRegisterClasses(
        Emitter.insFormat format, emitAttr size, regNumber reg1, regNumber reg2)
    {
        var descriptor = RecordingEmitter.Basic(INS_nop, format);
        descriptor.idOpSize(size);
        Register1(descriptor) = reg1;
        Register2(descriptor) = reg2;
        CheckSanity(CreateEmitter(), descriptor);
    }

    [Test]
    public static void SveFormatsRetainTheirSeparateSanityDependency()
    {
        var descriptor = RecordingEmitter.Basic(INS_ldr, IF_EN5A);
        var result = Arm64SveInstructionSanityTests.Capture(() => CheckSanity(CreateEmitter(), descriptor));
        Arm64SveInstructionSanityTests.AssertUnexpectedFormat(result);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsSanityCheck")]
    private static extern void CheckSanity(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_idReg2")]
    private static extern ref regNumber Register2(Emitter.instrDesc descriptor);
#endif

    [TestCase(-1, EA_4BYTE, false)]
    [TestCase(0, EA_4BYTE, true)]
    [TestCase(31, EA_4BYTE, true)]
    [TestCase(32, EA_4BYTE, false)]
    [TestCase(63, EA_8BYTE, true)]
    [TestCase(64, EA_8BYTE, false)]
    public static void ImmediateShiftBoundsUseOperandWidth(int shift, emitAttr size, bool expected)
    {
        Assert.That(IsValidImmShift(null, shift, size), Is.EqualTo(expected));
    }

    [TestCase(-1, true, false)]
    [TestCase(0, true, true)]
    [TestCase(63, true, true)]
    [TestCase(64, true, true)]
    [TestCase(65, true, false)]
    [TestCase(0, false, true)]
    [TestCase(63, false, true)]
    [TestCase(64, false, false)]
    public static void VectorShiftsPreserveNativeInclusiveRightBound(int shift, bool right, bool expected)
    {
        Assert.That(IsValidVectorShift(null, shift, EA_8BYTE, right), Is.EqualTo(expected));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidImmShift")]
    private static extern bool IsValidImmShift(Emitter? emitter, nint shift, emitAttr size);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidVectorShiftAmount")]
    private static extern bool IsValidVectorShift(Emitter? emitter, nint shift, emitAttr size, bool right);

    [TestCase(IF_LARGEADR, REG_R0, 8u)]
    [TestCase(IF_LARGEJMP, REG_R0, 8u)]
    [TestCase(IF_LARGELDC, REG_R0, 8u)]
    [TestCase(IF_LARGELDC, REG_V0, 12u)]
    [TestCase(IF_LARGELDC, REG_V31, 12u)]
    [TestCase(IF_SN_0A, REG_R0, 4u)]
    public static void DescriptorCodeSizesFollowFormatAndRegisterClass(Emitter.insFormat format, regNumber reg, uint size)
    {
        var descriptor = RecordingEmitter.Basic(INS_nop, format);
        Register1(descriptor) = reg;
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(size));
    }

    [Test]
    public static void DescriptorFieldsPreserveArm64Widths()
    {
        var format = IF_COUNT - 1;
        var descriptor = RecordingEmitter.Basic(INS_nop, format);
        descriptor.idInsOpt((insOpts)63);
        descriptor.idOpSize(EA_16BYTE);
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
        Assert.That(descriptor.idInsOpt(), Is.EqualTo((insOpts)63));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_16BYTE));
    }

    [Test]
    public static void EmptyAlignmentConsumesNoCodeBytes()
    {
        var descriptor = RecordingEmitter.Basic(INS_align, IF_SN_0A);
        descriptor.idInsOpt(INS_OPTS_NONE);
        Assert.That(descriptor.idIsEmptyAlign(), Is.True);
        Assert.That(descriptor.idCodeSize(), Is.Zero);
        descriptor.idInsOpt((insOpts)1);
        Assert.That(descriptor.idIsEmptyAlign(), Is.False);
        Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
    }

    [TestCase(INS_ldr, true)]
    [TestCase(INS_str, true)]
    [TestCase(INS_nop, false)]
    [TestCase(INS_lea, false)]
    public static void MemoryClassificationIncludesSyntheticInstructionBoundary(instruction ins, bool expected)
    {
        Assert.That(IsMemory(CreateEmitter(), ins), Is.EqualTo(expected));
    }

    [Test]
    public static void AppendingPreservesBarriersUntilMemoryIsAccessed()
    {
        var emitter = CreateEmitter();
        var barrier = RecordingEmitter.Basic(INS_dmb, IF_SI_0B);
        Append(emitter, barrier);
        Assert.That(LastBarrier(emitter), Is.SameAs(barrier));
        Append(emitter, RecordingEmitter.Basic(INS_nop, IF_SN_0A));
        Assert.That(LastBarrier(emitter), Is.SameAs(barrier));
        Append(emitter, RecordingEmitter.Basic(INS_ldr, IF_EN5A));
        Assert.That(LastBarrier(emitter), Is.Null);
        Assert.That(GroupSize(emitter), Is.EqualTo(12));
    }

#if EMITTER_STATS
    [Test]
    public static void RecordingIncrementsTheInstructionFormatCounter()
    {
        var counts = FormatCounts(null);
        var before = counts[(int)IF_SN_0A];
        try
        {
            CreateEmitter().emitIns(INS_nop);
            Assert.That(counts[(int)IF_SN_0A], Is.EqualTo(unchecked(before + 1)));
        }
        finally
        {
            counts[(int)IF_SN_0A] = before;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitIFcounts")]
    private static extern ref uint[] FormatCounts(Emitter? emitter);
#endif

    [TestCase(false, INS_ldr, EA_8BYTE, REG_R4, 0, 0, false, IF_LS_2A, 0)]
    [TestCase(false, INS_ldrb, EA_4BYTE, REG_R4, 4094, 1, false, IF_LS_2B, 4095)]
    [TestCase(false, INS_ldrsb, EA_8BYTE, REG_R4, -256, 0, true, IF_LS_2C, -256)]
    [TestCase(false, INS_ldrh, EA_4BYTE, REG_R4, 8190, 0, false, IF_LS_2B, 4095)]
    [TestCase(false, INS_ldrsh, EA_8BYTE, REG_R4, 3, 0, false, IF_LS_2C, 3)]
    [TestCase(false, INS_ldrsw, EA_8BYTE, REG_R4, 16380, 0, false, IF_LS_2B, 4095)]
    [TestCase(false, INS_ldr, EA_GCREF, REG_R4, 32760, 0, false, IF_LS_2B, 4095)]
    [TestCase(false, INS_ldr, EA_16BYTE, REG_V4, 65520, 0, false, IF_LS_2B, 4095)]
    [TestCase(false, INS_lea, EA_8BYTE, REG_R4, -4095, 0, true, IF_DI_2A, 4095)]
    [TestCase(false, INS_lea, EA_8BYTE, REG_R4, 4090, 5, false, IF_DI_2A, 4095)]
    [TestCase(false, INS_str, EA_8BYTE, REG_R4, 8, 0, false, IF_LS_2B, 1)]
    [TestCase(true, INS_strb, EA_4BYTE, REG_R4, 0, 0, false, IF_LS_2A, 0)]
    [TestCase(true, INS_strh, EA_4BYTE, REG_R4, -256, 0, true, IF_LS_2C, -256)]
    [TestCase(true, INS_str, EA_4BYTE, REG_R4, 16380, 0, false, IF_LS_2B, 4095)]
    [TestCase(true, INS_str, EA_BYREF, REG_R4, 32760, 0, false, IF_LS_2B, 4095)]
    [TestCase(true, INS_str, EA_16BYTE, REG_V4, 65520, 0, false, IF_LS_2B, 4095)]
    [TestCase(true, INS_str, EA_16BYTE, REG_V4, 255, 0, false, IF_LS_2C, 255)]
    public static void LocalStackRecordingPreservesFrameAndMetadata(bool store, instruction ins, emitAttr attr,
        regNumber reg, int frameOffset, int localOffset, bool fpBased, Emitter.insFormat format, int immediate)
    {
        var emitter = CreateLocalStackEmitter(frameOffset, fpBased);
        if (store)
        {
            RecordLocalStore(emitter, ins, attr, reg, 0, localOffset);
        }
        else
        {
            RecordLocalLoad(emitter, ins, attr, reg, 0, localOffset);
        }

        var id = LastInstruction(emitter) ?? throw new AssertionException("No local access was recorded.");
        var expectedIns = ins == INS_lea ? frameOffset < 0 ? INS_sub : INS_add : ins;
        Assert.That(id.idIns(), Is.EqualTo(expectedIns));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idReg1(), Is.EqualTo(reg));
        Assert.That(id.idReg2(), Is.EqualTo(fpBased ? REG_FPBASE : REG_ZR));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)immediate));
        Assert.That(id.idGCref(), Is.EqualTo(attr == EA_GCREF ? GCInfo.GCtype.GCT_GCREF :
            attr == EA_BYREF ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
        Assert.That(id.idIsLclVar(), Is.True);
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(localOffset));
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing local-reference metadata.");
        Assert.That(debugInfo.idVarRefOffs, Is.EqualTo(123));
#endif
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_ldp, -512, false, EA_GCREF, IF_LS_3C, -64, false)]
    [TestCase(INS_ldnp, 504, false, EA_BYREF, IF_LS_3C, 63, false)]
    [TestCase(INS_ldp, 0, true, EA_8BYTE, IF_LS_3B, 0, false)]
    [TestCase(INS_ldnp, 512, false, EA_BYREF, IF_LS_3B, 0, true)]
    [TestCase(INS_stp, -512, false, EA_GCREF, IF_LS_3C, -64, false)]
    [TestCase(INS_stnp, 504, false, EA_BYREF, IF_LS_3C, 63, false)]
    [TestCase(INS_stp, 0, true, EA_8BYTE, IF_LS_3B, 0, false)]
    [TestCase(INS_stnp, 7, false, EA_GCREF, IF_LS_3B, 0, true)]
    public static void LocalStackRecordingPairsPreserveBoundsAndGc(instruction ins, int offset, bool fpBased,
        emitAttr secondAttr, Emitter.insFormat format, int immediate, bool reserved)
    {
        var emitter = CreateLocalStackEmitter(offset, fpBased);
        if (ins is INS_ldp or INS_ldnp)
        {
            RecordLocalLoadPair(emitter, ins, EA_GCREF, secondAttr, REG_R4, REG_R5, 0, 0);
        }
        else
        {
            RecordLocalStorePair(emitter, ins, EA_GCREF, secondAttr, REG_R4, REG_R5, 0, 0);
        }

        var id = LastInstruction(emitter) ?? throw new AssertionException("No local pair was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idReg1(), Is.EqualTo(REG_R4));
        Assert.That(id.idReg2(), Is.EqualTo(REG_R5));
        Assert.That(id.idReg3(), Is.EqualTo(reserved ? REG_OPT_RSVD : fpBased ? REG_FPBASE : REG_ZR));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)immediate));
        Assert.That(id.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
        Assert.That(id.idGCrefReg2(), Is.EqualTo(secondAttr == EA_GCREF ? GCInfo.GCtype.GCT_GCREF :
            secondAttr == EA_BYREF ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
        Assert.That(id.idIsLclVar(), Is.True);
        Assert.That(id.idIsLclVarPair(), Is.False);
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        Assert.That(GroupSize(emitter), Is.EqualTo(reserved ? 8 : 4));
        if (reserved)
        {
            var address = CurrentInstructions(emitter)[0];
            Assert.That(address.idIns(), Is.EqualTo(INS_add));
            Assert.That(address.idReg1(), Is.EqualTo(REG_OPT_RSVD));
            Assert.That(Emitter.emitGetInsSC(address), Is.EqualTo((nint)offset));
        }
    }

    [TestCase(false, false, REG_V4, IF_SVE_IE_2A)]
    [TestCase(false, true, REG_P1, IF_SVE_ID_2A)]
    [TestCase(true, false, REG_P1, IF_SVE_JG_2A)]
    [TestCase(true, true, REG_V4, IF_SVE_JH_2A)]
    public static void LocalStackRecordingScalableLocalsAndTemps(bool store, bool temp,
        regNumber reg, Emitter.insFormat format)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.unkSizeFrame.nMask = 9;
        compiler.unkSizeFrame.nVector = 3;
        compiler.unkSizeFrame.FinalizeLayout();
        var varx = 0;
        if (temp)
        {
            ref var regSet = ref EmitterCodeGen(emitter).RegSet;
            regSet.tmpBeginPreAllocateTemps();
            regSet.tmpPreAllocateTemps(var_types.TYP_SIMD, 1);
            regSet.tmpGetNum(-1).tdTempOffs = 2;
            varx = -1;
        }
        else
        {
            compiler.lvaTable[0].Type = var_types.TYP_SIMD;
            compiler.lvaTable[0].UnknownSizeFrameIndex = 2;
        }

        void Record()
        {
            if (store)
            {
                RecordLocalStore(emitter, INS_sve_str, EA_SCALABLE, reg, varx, 0);
            }
            else
            {
                RecordLocalLoad(emitter, INS_sve_ldr, EA_SCALABLE, reg, varx, 0);
            }
        }

#if DEBUG
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(Record);
        Assert.That(assertions, Is.Empty);
#else
        Record();
#endif
        var id = LastInstruction(emitter) ?? throw new AssertionException("No scalable access was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(store ? INS_sve_str : INS_sve_ldr));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idOpSize(), Is.EqualTo(EA_SCALABLE));
        Assert.That(id.idReg1(), Is.EqualTo(reg));
        Assert.That(id.idReg2(), Is.EqualTo(REG_UNKBASE));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)(-5)));
        Assert.That(id.idIsLclVar(), Is.True);
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(varx));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(INS_add, 4095)]
    [TestCase(INS_adds, 4096)]
    [TestCase(INS_sub, 8)]
    [TestCase(INS_subs, 16)]
    [TestCase(INS_and, 255)]
    [TestCase(INS_ands, 255)]
    [TestCase(INS_eor, 255)]
    [TestCase(INS_orr, 255)]
    public static void LocalStackRecordingImmediateWrapper(instruction ins, int immediate)
    {
        var emitter = CreateEmitter();
        RecordFlexibleImmediate(emitter, ins, EA_8BYTE, REG_R4, REG_R5, immediate);
        var id = LastInstruction(emitter) ?? throw new AssertionException("No immediate instruction was recorded.");
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idReg1(), Is.EqualTo(REG_R4));
        Assert.That(id.idReg2(), Is.EqualTo(REG_R5));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalStackRecordingOrdinaryTemps(bool store)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        var codeGen = EmitterCodeGen(emitter);
        codeGen.IsFramePointerUsed = false;
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        ref var regSet = ref codeGen.RegSet;
        regSet.tmpBeginPreAllocateTemps();
        regSet.tmpPreAllocateTemps(var_types.TYP_LONG, 1);
        regSet.tmpGetNum(-1).tdTempOffs = 16;
        if (store)
        {
            RecordLocalStore(emitter, INS_str, EA_8BYTE, REG_R4, -1, 8);
        }
        else
        {
            RecordLocalLoad(emitter, INS_ldr, EA_8BYTE, REG_R4, -1, 8);
        }

        var id = LastInstruction(emitter) ?? throw new AssertionException("No spill-temp access was recorded.");
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_LS_2B));
        Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(Emitter.emitGetInsSC(id), Is.EqualTo((nint)3));
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(-1));
        Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8));
    }

    [TestCase(false, INS_ldr)]
    [TestCase(true, INS_str)]
    public static void LocalStackLoadStoreRecordsWithoutAnOptimizationCandidate(bool store, instruction expected)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.opts.compMinOpts = false;
        compiler.opts.canUseAllOpts = true;
        if (store)
        {
            RecordLocalStore(emitter, expected, EA_8BYTE, REG_R4, 0, 0);
        }
        else
        {
            RecordLocalLoad(emitter, expected, EA_8BYTE, REG_R4, 0, 0);
        }

        var instruction = LastInstruction(emitter) ?? throw new AssertionException("No local stack access was recorded.");
        Assert.That(instruction.idIns(), Is.EqualTo(expected));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [TestCase(31, "SVE", 0)]
    [TestCase(32, "SVE", 4)]
    public static void LocalStackScalableAddressDependenciesPreserveSignedSixBitBoundary(
        int index, string dependency, int recordedSize)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.lvaTable[0].Type = var_types.TYP_SIMD;
        compiler.lvaTable[0].UnknownSizeFrameIndex = index;
        compiler.unkSizeFrame.nVector = (uint)(index + 1);
        compiler.unkSizeFrame.FinalizeLayout();
        Assert.That(() => RecordLocalLoad(emitter, INS_lea, EA_8BYTE, REG_R4, 0, 0),
            Throws.TypeOf<FatalJitException>().With.Message.Contains(dependency));
        Assert.That(GroupSize(emitter), Is.EqualTo(recordedSize));
    }

    [TestCase(false, INS_ldr, -257, false, IF_LS_3A)]
    [TestCase(true, INS_str, 32768, false, IF_LS_3A)]
    [TestCase(false, INS_lea, 4096, false, IF_DR_3C)]
    [TestCase(false, INS_lea, -4096, true, IF_DR_3A)]
    public static void MaterializationStackOffsetsRecordThroughReservedRegister(bool store, instruction ins,
        int offset, bool fpBased, Emitter.insFormat format)
    {
        var emitter = CreateLocalStackEmitter(offset, fpBased);
        if (store)
        {
            RecordLocalStore(emitter, ins, EA_8BYTE, REG_R4, 0, 0);
        }
        else
        {
            RecordLocalLoad(emitter, ins, EA_8BYTE, REG_R4, 0, 0);
        }

        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(2));
        Assert.That(instructions[0].idReg1(), Is.EqualTo(REG_OPT_RSVD));
        Assert.That(instructions[1].idInsFmt(), Is.EqualTo(format));
        Assert.That(instructions[1].idReg1(), Is.EqualTo(REG_R4));
        Assert.That(instructions[1].idReg2(), Is.EqualTo(fpBased ? REG_FPBASE : REG_ZR));
        Assert.That(instructions[1].idIsLclVar(), Is.True);
        Assert.That(GroupSize(emitter), Is.EqualTo(8));
    }

    [TestCase(false, REG_V4, IF_SVE_IE_2A)]
    [TestCase(false, REG_P1, IF_SVE_ID_2A)]
    [TestCase(true, REG_V4, IF_SVE_JH_2A)]
    [TestCase(true, REG_P1, IF_SVE_JG_2A)]
    public static void MaterializationScalableAccessUsesFixedFrameAddress(bool store,
        regNumber reg, Emitter.insFormat format)
    {
        var emitter = CreateLocalStackEmitter(24, false);
        void Record()
        {
            if (store)
            {
                RecordLocalStore(emitter, INS_sve_str, EA_8BYTE, reg, 0, 0);
            }
            else
            {
                RecordLocalLoad(emitter, INS_sve_ldr, EA_8BYTE, reg, 0, 0);
            }
        }

#if DEBUG
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(Record);
        Assert.That(assertions, Is.Empty);
#else
        Record();
#endif
        Assert.That(GroupSize(emitter), Is.EqualTo(8));
        Assert.That(CurrentInstructions(emitter), Has.Count.EqualTo(2));
        var access = LastInstruction(emitter) ?? throw new AssertionException("Missing scalable access.");
        Assert.That(access.idIns(), Is.EqualTo(store ? INS_sve_str : INS_sve_ldr));
        Assert.That(access.idInsFmt(), Is.EqualTo(format));
        Assert.That(access.idOpSize(), Is.EqualTo(EA_SCALABLE));
        Assert.That(access.idReg1(), Is.EqualTo(reg));
        Assert.That(access.idReg2(), Is.EqualTo(REG_OPT_RSVD));
        Assert.That(Emitter.emitGetInsSC(access), Is.EqualTo((nint)0));
        Assert.That(access.idIsLclVar(), Is.True);
        Assert.That(access.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        var address = CurrentInstructions(emitter)[0];
        Assert.That(address.idIns(), Is.EqualTo(INS_add));
        Assert.That(address.idReg1(), Is.EqualTo(REG_OPT_RSVD));
        Assert.That(address.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(Emitter.emitGetInsSC(address), Is.EqualTo((nint)24));
    }

    [TestCase(0L, EA_8BYTE, 1, INS_mov)]
    [TestCase(1L, EA_8BYTE, 1, INS_mov)]
    [TestCase(-1L, EA_8BYTE, 1, INS_movn)]
    [TestCase(0x12345678L, EA_4BYTE, 2, INS_movz)]
    [TestCase(0x89abcdefL, EA_4BYTE, 2, INS_movz)]
    [TestCase(0x1234000056780000L, EA_8BYTE, 2, INS_movz)]
    [TestCase(0x123456789abcdef0L, EA_8BYTE, 4, INS_movz)]
    [TestCase(unchecked((long)0xffff1234ffff5678UL), EA_8BYTE, 2, INS_movn)]
    [TestCase(unchecked((long)0xffff000012345678UL), EA_8BYTE, 3, INS_movz)]
    public static void MaterializationPreservesHalfwordSelection(long value, emitAttr size, int count,
        instruction first)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        var codeGen = EmitterCodeGen(emitter);
        codeGen.instGen_Set_Reg_To_Imm(size, REG_R4, unchecked((nint)value));
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(count));
        Assert.That(instructions[0].idIns(), Is.EqualTo(first));
        ulong reconstructed = 0;
        for (var index = 0; index < instructions.Count; index++)
        {
            var id = instructions[index];
            Assert.That(id.idReg1(), Is.EqualTo(REG_R4));
            if (value == 0)
            {
                Assert.That(id.idReg2(), Is.EqualTo(REG_ZR));
                continue;
            }

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_DI_1B));
            var encoded = unchecked((uint)Emitter.emitGetInsSC(id));
            var shift = (int)((encoded >> 16) & 3) * 16;
            var halfword = (ulong)(encoded & 0xffff) << shift;
            if (index == 0)
            {
                reconstructed = first == INS_movn ? ~halfword : halfword;
            }
            else
            {
                Assert.That(id.idIns(), Is.EqualTo(INS_movk));
                reconstructed = (reconstructed & ~(0xffffUL << shift)) | halfword;
            }
        }

        var mask = size == EA_8BYTE ? ulong.MaxValue : uint.MaxValue;
        Assert.That(reconstructed & mask, Is.EqualTo(unchecked((ulong)value) & mask));
        Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(REG_R4.SingleTypeMask)));
        Assert.That(GroupSize(emitter), Is.EqualTo(count * 4));
    }

    [TestCase(0, REG_R5, 1)]
    [TestCase(4095, REG_SPBASE, 1)]
    [TestCase(0x12345678, REG_R5, 3)]
    [TestCase(0x12345678, REG_SPBASE, 3)]
    public static void MaterializationBasePlusImmediatePreservesSpOperand(int immediate, regNumber baseReg, int count)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        EmitterCodeGen(emitter).instGen_Set_Reg_To_Base_Plus_Imm(EA_8BYTE, REG_R4, baseReg, immediate);
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(count));
        var add = instructions[^1];
        Assert.That(add.idIns(), Is.EqualTo(immediate == 0 ? INS_mov : INS_add));
        Assert.That(add.idReg1(), Is.EqualTo(REG_R4));
        Assert.That(add.idReg2(), Is.EqualTo(baseReg == REG_SPBASE ? REG_ZR : baseReg));
        if (count > 1)
        {
            Assert.That(add.idReg3(), Is.EqualTo(REG_R4));
        }
    }

    [TestCase(false, 0L)]
    [TestCase(false, 0x1234L)]
    [TestCase(true, 0L)]
    [TestCase(true, 0x12345678L)]
    public static void MaterializationRelocationsPrecedeZeroSelection(bool reloc, long address)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.opts.compReloc = reloc;
        EmitterCodeGen(emitter).instGen_Set_Reg_To_Imm(EA_8BYTE | EA_CNS_RELOC_FLG, REG_R4, (nint)address
#if DEBUG
            , targetHandle: 0x4321, gtFlags: GTF_ICON_CLASS_HDL
#endif
            );
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(reloc ? 2 : 1));
        var first = instructions[0];
        Assert.That(first.idIns(), Is.EqualTo(reloc ? INS_adrp : INS_mov));
        Assert.That(first.idIsDspReloc(), Is.EqualTo(reloc));
        if (reloc)
        {
            Assert.That((nint)first.idAddr().iiaAddr, Is.EqualTo((nint)address));
            Assert.That(instructions[1].idIns(), Is.EqualTo(INS_add));
            Assert.That(instructions[1].idReg2(), Is.EqualTo(REG_R4));
            Assert.That(instructions[1].idIsCnsReloc(), Is.True);
            Assert.That((nint)instructions[1].idAddr().iiaAddr, Is.EqualTo((nint)address));
#if DEBUG
            var debugInfo = first.idDebugOnlyInfo() ?? throw new AssertionException("Missing relocation metadata.");
            Assert.That(debugInfo.idMemCookie, Is.EqualTo((nint)0x4321));
            Assert.That(debugInfo.idFlags, Is.EqualTo(GTF_ICON_CLASS_HDL));
#endif
        }
    }

    [Test]
    public static void MaterializationAdrDoesNotAddAPageOffsetInstruction()
    {
        var emitter = CreateLocalStackEmitter(0, false);
        emitter.emitIns_R_AI(INS_adr, EA_8BYTE | EA_DSP_RELOC_FLG, REG_R4, 0x12345678);
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(1));
        Assert.That(instructions[0].idInsFmt(), Is.EqualTo(IF_DI_1E));
        Assert.That(instructions[0].idIsDspReloc(), Is.True);
        Assert.That((nint)instructions[0].idAddr().iiaAddr, Is.EqualTo((nint)0x12345678));
    }

    [Test]
    public static void MaterializationBitmaskUsesASingleMove()
    {
        var emitter = CreateLocalStackEmitter(0, false);
        EmitterCodeGen(emitter).instGen_Set_Reg_To_Imm(EA_8BYTE, REG_R4, unchecked((nint)0x00ff00ff00ff00ff));
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(1));
        Assert.That(instructions[0].idIns(), Is.EqualTo(INS_mov));
        Assert.That(instructions[0].idInsFmt(), Is.EqualTo(IF_DI_1D));
    }

    [Test]
    public static void MaterializationZeroPreservesNativeFlagHandling()
    {
        var emitter = CreateLocalStackEmitter(0, false);
        EmitterCodeGen(emitter).instGen_Set_Reg_To_Imm(EA_8BYTE, REG_R4, 0, insFlags.INS_FLAGS_SET);
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(1));
        Assert.That(instructions[0].idIns(), Is.EqualTo(INS_mov));
        Assert.That(instructions[0].idReg2(), Is.EqualTo(REG_ZR));
    }

#if DEBUG
    [TestCase(0x12345678L)]
    [TestCase(0x1234000056780000L)]
    public static void MaterializationHalfwordCookiesBelongOnlyToTheLowHalfword(long value)
    {
        var emitter = CreateLocalStackEmitter(0, false);
        EmitterCodeGen(emitter).instGen_Set_Reg_To_Imm(EA_8BYTE, REG_R4, (nint)value,
            targetHandle: 0x4321, gtFlags: GTF_ICON_CLASS_HDL);
        foreach (var id in CurrentInstructions(emitter))
        {
            var lowHalfword = ((uint)Emitter.emitGetInsSC(id) >> 16) == 0;
            var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing immediate metadata.");
            Assert.That(info.idMemCookie, Is.EqualTo(lowHalfword ? (nint)0x4321 : 0));
            Assert.That(info.idFlags, Is.EqualTo(lowHalfword ? GTF_ICON_CLASS_HDL : GenTreeFlags.GTF_EMPTY));
        }
    }

    [Test]
    public static void NonzeroImmediateFlagRequestPreservesNativeZeroMaskAssertion()
    {
        var emitter = CreateLocalStackEmitter(0, false);
        Assert.That(() => EmitterCodeGen(emitter).instGen_Set_Reg_To_Imm(
            EA_8BYTE, REG_R4, 1, insFlags.INS_FLAGS_SET),
            Throws.Exception.With.Message.Contains("canEncode"));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }
#endif

    [TestCase(var_types.TYP_FLOAT, 0UL, INS_movi)]
    [TestCase(var_types.TYP_DOUBLE, 0UL, INS_movi)]
    [TestCase(var_types.TYP_FLOAT, 0x3FF8000000000000UL, INS_fmov)]
    [TestCase(var_types.TYP_DOUBLE, 0x3FF8000000000000UL, INS_fmov)]
    [TestCase(var_types.TYP_FLOAT, 0x8000000000000000UL, INS_ldr)]
    [TestCase(var_types.TYP_DOUBLE, 0x8000000000000000UL, INS_ldr)]
    [TestCase(var_types.TYP_FLOAT, 0x7FF8123400000000UL, INS_ldr)]
    [TestCase(var_types.TYP_DOUBLE, 0x7FF8123400000000UL, INS_ldr)]
    public static void ConstantNodeFloatingPreservesBitsAndInstructionSelection(
        var_types type, ulong bits, instruction expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        var value = BitConverter.UInt64BitsToDouble(bits);
        var tree = compiler.gtNewDconNode(type, value);
        var codeGen = EmitterCodeGen(emitter);
        codeGen.InternalRegisters.Add(tree, new regMaskTP(REG_R3.SingleTypeMask));
        compiler.opts.compCodeOpt = Compiler.SMALL_CODE;
        RecordConstantNode(codeGen, REG_V2, type, tree);

        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing constant.");
        Assert.That(descriptor.idIns(), Is.EqualTo(expected));
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_V2));
        if (expected == INS_ldr)
        {
            var section = emitter.emitConsDsc.dsdList ?? throw new AssertionException("Missing constant data.");
            Assert.That(section.dsSize, Is.EqualTo(type.Size));
            Assert.That(section.dsAlignment, Is.EqualTo(type.Size));
            Assert.That(section.dsDataType, Is.EqualTo(type));
            Assert.That(section.Data, Is.EqualTo(type == var_types.TYP_FLOAT
                ? BitConverter.GetBytes((float)value) : BitConverter.GetBytes(value)));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_LARGELDC));
        }
        else
        {
            Assert.That(emitter.emitConsDsc.dsdList, Is.Null);
        }
    }

    [TestCase(var_types.TYP_SIMD8, 0u, INS_movi, INS_OPTS_2S)]
    [TestCase(var_types.TYP_SIMD12, 0u, INS_movi, INS_OPTS_4S)]
    [TestCase(var_types.TYP_SIMD16, uint.MaxValue, INS_mvni, INS_OPTS_4S)]
    [TestCase(var_types.TYP_SIMD8, uint.MaxValue, INS_mvni, INS_OPTS_2S)]
    [TestCase(var_types.TYP_SIMD16, 0x12000000u, INS_movi, INS_OPTS_4S)]
    [TestCase(var_types.TYP_SIMD16, 0x12001200u, INS_movi, INS_OPTS_8H)]
    [TestCase(var_types.TYP_SIMD16, 0x12121212u, INS_movi, INS_OPTS_16B)]
    [TestCase(var_types.TYP_SIMD8, 0x12000000u, INS_movi, INS_OPTS_2S)]
    [TestCase(var_types.TYP_SIMD8, 0x12001200u, INS_movi, INS_OPTS_4H)]
    [TestCase(var_types.TYP_SIMD8, 0x12121212u, INS_movi, INS_OPTS_8B)]
    public static void ConstantNodeVectorsPreserveBroadcastOrder(
        var_types type, uint bits, instruction expected, insOpts option)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var tree = new GenTreeVecCon(type) { RegNum = REG_V3 };
        tree.SimdVal.AsSpan<uint>().Fill(bits);
        var targetType = type == var_types.TYP_SIMD12 ? var_types.TYP_SIMD16 : type;
        RecordConstantNode(EmitterCodeGen(emitter), REG_V2, targetType, tree);

        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing vector constant.");
        Assert.That(descriptor.idIns(), Is.EqualTo(expected));
        Assert.That(descriptor.idInsOpt(), Is.EqualTo(option));
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_V2));
        Assert.That(emitter.emitConsDsc.dsdList, Is.Null);
    }

    [TestCase(var_types.TYP_SIMD8)]
    [TestCase(var_types.TYP_SIMD12)]
    [TestCase(var_types.TYP_SIMD16)]
    public static void ConstantNodeVectorPoolPreservesThePhysicalPayload(var_types type)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var tree = new GenTreeVecCon(type);
        tree.SimdVal.AsSpan<uint>().Fill(0x12);
        tree.SimdVal.u32[type == var_types.TYP_SIMD8 ? 1 : 3] = 0x13;
        var codeGen = EmitterCodeGen(emitter);
        codeGen.InternalRegisters.Add(tree, new regMaskTP(REG_R3.SingleTypeMask));
        var targetType = type == var_types.TYP_SIMD8 ? type : var_types.TYP_SIMD16;
        RecordConstantNode(codeGen, REG_V2, targetType, tree);

        var section = emitter.emitConsDsc.dsdList ?? throw new AssertionException("Missing vector data.");
        Assert.That(section.Data, Is.EqualTo(tree.SimdVal.AsSpan<byte>()[..(int)targetType.Size].ToArray()));
        Assert.That(section.dsAlignment, Is.EqualTo(targetType.Size));
        Assert.That(section.dsDataType, Is.EqualTo(targetType));
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing vector load.");
        Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_V2));
        Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R3));
    }

    [TestCase(var_types.TYP_INT, 0L, false, GCInfo.GCtype.GCT_NONE)]
    [TestCase(var_types.TYP_LONG, 0x12345678L, false, GCInfo.GCtype.GCT_NONE)]
    [TestCase(var_types.TYP_BYREF, 0x12345678L, false, GCInfo.GCtype.GCT_NONE)]
    [TestCase(var_types.TYP_REF, 0L, false, GCInfo.GCtype.GCT_NONE)]
    [TestCase(var_types.TYP_BYREF, 0x12345678L, true, GCInfo.GCtype.GCT_BYREF)]
    [TestCase(var_types.TYP_REF, 0L, true, GCInfo.GCtype.GCT_GCREF)]
    public static void ConstantNodeIntegersPreserveTypeAndModifiedRegister(
        var_types type, long value, bool reloc, GCInfo.GCtype gcType)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.opts.compReloc = reloc;
        var tree = new GenTreeIntCon(type, (nint)value);
        var codeGen = EmitterCodeGen(emitter);
        RecordConstantNode(codeGen, REG_R4, type, tree);
        var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing integer constant.");
        Assert.That(descriptor.idGCref(), Is.EqualTo(gcType));
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R4));
        Assert.That((codeGen.RegSet.rsGetModifiedRegsMask() & new regMaskTP(REG_R4.SingleTypeMask)) != RBM_NONE, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConstantNodeTlsDeferralRequiresRelocation(bool reloc)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.opts.compReloc = reloc;
        var tree = new GenTreeIntCon(TYP_I_IMPL, 0x1234) {
            Flags = GTF_ICON_TLS_HDL | GTF_ICON_TLSGD_OFFSET,
        };
        RecordConstantNode(EmitterCodeGen(emitter), REG_R4, TYP_I_IMPL, tree);
        Assert.That(CurrentInstructions(emitter), Has.Count.EqualTo(reloc ? 0 : 1));
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
#if DEBUG
    [TestCase(false, false, true)]
#endif
    public static void ConstantNodeAddressRecordingPreservesBindingAndJumpLinks(
        bool cold, bool reloc, bool forceLong)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        var block = compiler.compCurBB ?? throw new AssertionException("Missing block.");
        if (cold)
        {
            compiler.fgFirstColdBlock = block;
            block.SetFlags(BasicBlockFlags.BBF_COLD);
        }
        if (reloc)
        {
            compiler.compCurBB = null;
        }
#if DEBUG
        compiler.opts.compLongAddress = forceLong;
#endif
        emitter.emitIns(INS_nop);
        var handle = Compiler.eeFindJitDataOffs(64);
        RecordConstantAddress(emitter, INS_ldr, EA_8BYTE | (reloc ? EA_DSP_RELOC_FLG : 0),
            REG_R4, REG_R4, handle, 4);
        var keepLong = cold || reloc || forceLong;
        RecordingEmitter.CheckConstantAddress(emitter, keepLong, reloc, (nint)handle);
    }

    [TestCase(SimdScalableKind.SimdScalableRepeated, 0, 0, 0, "register-immediate")]
    [TestCase(SimdScalableKind.SimdScalableRepeated, -1, 0, 0, "register-immediate")]
    [TestCase(SimdScalableKind.SimdScalableRepeated, 127, 0, 0, "register-immediate")]
    [TestCase(SimdScalableKind.SimdScalableRepeated, 0x12345678, 0, 1, "two-register")]
    [TestCase(SimdScalableKind.SimdScalableSequence, 1, 2, 0, "two-immediate")]
    [TestCase(SimdScalableKind.SimdScalableSequence, 1, 64, 1, "two-register/immediate")]
    [TestCase(SimdScalableKind.SimdScalableSequence, 32, 2, 1, "two-register/immediate")]
    [TestCase(SimdScalableKind.SimdScalableSequence, 32, 64, 2, "three-register")]
    [TestCase(SimdScalableKind.SimdScalableScalar, 32, 0, 0, "register-immediate")]
    public static void ConstantNodeScalablePreservesLoadsBeforeItsExplicitDependency(
        SimdScalableKind kind, int index, int step, int loads, string dependency)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        var tree = new GenTreeVecCon(var_types.TYP_SIMD);
        tree.SimdScalableVal.BaseType = var_types.TYP_INT;
        tree.SimdScalableVal.Kind = kind;
        tree.SimdScalableVal.Index.i32[0] = index;
        tree.SimdScalableVal.Step.i32[0] = step;
        var codeGen = EmitterCodeGen(emitter);
        codeGen.InternalRegisters.Add(tree, new regMaskTP(REG_R3.SingleTypeMask | REG_R5.SingleTypeMask));

        Assert.That(() => RecordConstantNode(codeGen, REG_V2, var_types.TYP_SIMD, tree),
            Throws.TypeOf<FatalJitException>().With.Message.Contains(dependency));
        var instructions = CurrentInstructions(emitter);
        Assert.That(instructions, Has.Count.EqualTo(loads));
        var section = emitter.emitConsDsc.dsdList;
        for (var i = 0; i < loads; i++)
        {
            Assert.That(section, Is.Not.Null);
            var data = section ?? throw new AssertionException("Missing scalable constant data.");
            var expected = kind == SimdScalableKind.SimdScalableRepeated || index > 15 ? index : step;
            if (i != 0)
            {
                expected = step;
            }
            Assert.That(BitConverter.ToInt64(data.Data), Is.EqualTo((long)expected));
            Assert.That(data.dsSize, Is.EqualTo(8));
            Assert.That(data.dsDataType, Is.EqualTo(var_types.TYP_LONG));
            Assert.That(instructions[i].idReg1(), Is.EqualTo(i == 0 ? REG_R3 : REG_R5));
            Assert.That(instructions[i].idReg2(), Is.EqualTo(instructions[i].idReg1()));
            section = data.dsNext;
        }
    }

    [TestCase(0UL, "single-register")]
    [TestCase(1UL, "register-pattern")]
    [TestCase(0xFFFFUL, "register-pattern")]
    public static void ConstantNodeMasksReachTheirExplicitRecordingDependency(ulong bits, string dependency)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var emitter = CreateConstantEmitter();
        simdmask_t value = default;
        value.u64[0] = bits;
        var tree = new GenTreeMskCon(value);
        Assert.That(() => RecordConstantNode(EmitterCodeGen(emitter), REG_P1, var_types.TYP_MASK, tree),
            Throws.TypeOf<FatalJitException>().With.Message.Contains(dependency));
        Assert.That(CurrentInstructions(emitter), Is.Empty);
    }

    [TestCase(EA_1BYTE, INS_OPTS_SCALABLE_B)]
    [TestCase(EA_2BYTE, INS_OPTS_SCALABLE_H)]
    [TestCase(EA_4BYTE, INS_OPTS_SCALABLE_S)]
    [TestCase(EA_8BYTE, INS_OPTS_SCALABLE_D)]
    [TestCase(EA_16BYTE, INS_OPTS_SCALABLE_Q)]
    public static void ConstantNodeScalableOptionsPreserveElementWidths(emitAttr size, insOpts expected)
    {
        Assert.That(ScalableOption(null, size), Is.EqualTo(expected));
    }

    private static RecordingEmitter CreateConstantEmitter()
    {
        var emitter = CreateEmitter();
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
#if DEBUG
        JitTls.Compiler = compiler;
#endif
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.compCurBB = new BasicBlock(null, null);
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genSetRegToConst")]
    private static extern void RecordConstantNode(CodeGen codeGen, regNumber reg, var_types type, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_C")]
    private static extern void RecordConstantAddress(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg, regNumber addrReg, CORINFO_FIELD_STRUCT_* handle, int offset);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "optGetSveInsOpt")]
    private static extern insOpts ScalableOption(Emitter? emitter, emitAttr size);

    private static RecordingEmitter CreateLocalStackEmitter(int offset, bool fpBased)
    {
        var emitter = CreateEmitter();
        var compiler = EmitterCompiler(emitter) ?? throw new AssertionException("Missing compiler.");
        compiler.codeGen = EmitterCodeGen(emitter);
        compiler.codeGen.IsFramePointerRequired = fpBased;
        compiler.codeGen.RegSet.rsMaskResvd = new regMaskTP(SRBM_OPT_RSVD);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc {
            Type = var_types.TYP_LONG,
            lvOnFrame = true,
            lvFramePointerBased = fpBased,
            StackOffset = offset,
        }];
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
#if DEBUG
        LocalReferenceOffset(emitter) = 123;
#endif
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_S")]
    private static extern void RecordLocalLoad(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg, int varx, int offs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_S_R")]
    private static extern void RecordLocalStore(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg, int varx, int offs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_S_S")]
    private static extern void RecordLocalLoadPair(Emitter emitter, instruction ins, emitAttr attr1, emitAttr attr2,
        regNumber reg1, regNumber reg2, int varx, int offs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_S_S_R_R")]
    private static extern void RecordLocalStorePair(Emitter emitter, instruction ins, emitAttr attr1, emitAttr attr2,
        regNumber reg1, regNumber reg2, int varx, int offs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitIns_R_R_Imm")]
    private static extern void RecordFlexibleImmediate(Emitter emitter, instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, nint immediate);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "codeGen")]
    private static extern ref CodeGen EmitterCodeGen(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cgEmitter")]
    private static extern ref Emitter CodeGenEmitter(CodeGen codeGen);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitVarRefOffs")]
    private static extern ref int LocalReferenceOffset(Emitter emitter);
#endif

    private static RecordingEmitter CreateEmitter(bool optimized = false, bool reloc = false)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.opts.compMinOptsIsSet = true;
        compiler.opts.compMinOpts = !optimized;
        compiler.opts.canUseAllOpts = optimized;
        compiler.opts.compReloc = reloc;
        var codeGen = new CodeGen(compiler);
        codeGen.RegSet.rsClearRegsModified();
        var emitter = new RecordingEmitter(codeGen);
        CodeGenEmitter(codeGen) = emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
        return emitter;
    }

    private sealed class RecordingEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static void CheckConstantAddress(Emitter emitter, bool keepLong, bool reloc, nint handle)
        {
            var descriptor = (instrDescJmp)(LastInstruction(emitter) ??
                throw new AssertionException("Missing constant address."));
            Assert.That(descriptor.idIsBound(), Is.True);
            Assert.That(descriptor.idjKeepLong, Is.EqualTo(keepLong));
            Assert.That(descriptor.idjShort, Is.False);
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(4));
            Assert.That((nint)descriptor.idAddr().iiaFieldHnd, Is.EqualTo(handle));
            Assert.That(descriptor.idIsDspReloc(), Is.EqualTo(reloc));
            if (keepLong)
            {
                Assert.That(CurrentJumpList(emitter), Is.Null);
            }
            else
            {
                Assert.That(CurrentJumpList(emitter), Is.SameAs(descriptor));
                Assert.That(descriptor.idjIG, Is.Not.Null);
                Assert.That(descriptor.idjOffs, Is.EqualTo(4u));
            }
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGjmpList")]
        private static extern ref instrDescJmp? CurrentJumpList(Emitter emitter);

        public static instrDesc Basic(instruction ins, insFormat format)
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(ins);
            descriptor.idInsFmt(format);
            return descriptor;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_idReg1")]
    private static extern ref regNumber Register1(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastMemBarrier")]
    private static extern ref Emitter.instrDesc? LastBarrier(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "appendToCurIG")]
    private static extern void Append(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsIsLoadOrStore")]
    private static extern bool IsMemory(Emitter emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_s_instructionFormats")]
    private static extern ReadOnlySpan<Emitter.insFormat> Formats(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_instInfo")]
    private static extern ReadOnlySpan<byte> Flags(CodeGen? codeGen);
}
#endif
