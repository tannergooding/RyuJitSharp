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
