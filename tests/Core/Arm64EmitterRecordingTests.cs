// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
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

    private static RecordingEmitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
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
