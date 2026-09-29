// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
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

    [TestCase(0, "ARM64 SVE immediate-only instruction recording is not ported.")]
    [TestCase(1, "ARM64 SVE single-register instruction recording is not ported.")]
    [TestCase(2, "ARM64 SVE register-immediate instruction recording is not ported.")]
    public static void UnsupportedFormsReachTheirSeparateSveRecorders(int form, string message)
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
        Assert.That(error, Has.Message.EqualTo(message));
        Assert.That(GroupSize(emitter), Is.Zero);
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
        var emitter = CreateEmitter();
        var error = Assert.Throws<FatalJitException>(() =>
            RecordFloat(emitter, INS_nop, EA_8BYTE, REG_V0, 1.0, INS_OPTS_NONE));
        Assert.That(error, Has.Message.EqualTo("ARM64 SVE floating-immediate instruction recording is not ported."));
        Assert.That(GroupSize(emitter), Is.Zero);
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

    [TestCase(INS_nop, 1, false, false, REG_R20, "SVE two-register/immediate")]
    [TestCase(INS_ldr, 0, false, true, REG_R20, "relocatable page-offset load folding")]
    [TestCase(INS_ldr, 8, true, false, REG_R20, "load/store instruction optimization")]
    [TestCase(INS_add, 8, true, false, REG_R19, "post-indexed instruction optimization")]
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
        var error = Assert.Throws<FatalJitException>(() => CheckSanity(CreateEmitter(), descriptor));
        Assert.That(error, Has.Message.EqualTo("ARM64 SVE instruction sanity checking is not ported."));
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

    private static RecordingEmitter CreateEmitter(bool optimized = false, bool reloc = false)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.opts.compMinOptsIsSet = true;
        compiler.opts.compMinOpts = !optimized;
        compiler.opts.canUseAllOpts = optimized;
        compiler.opts.compReloc = reloc;
        var emitter = new RecordingEmitter(new CodeGen(compiler));
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
