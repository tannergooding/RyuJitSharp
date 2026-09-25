// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionSizeTests
{
    [TestCase(false, 0, 0u, EA_4BYTE, REG_RAX, 3u)]
    [TestCase(true, 0, 0u, EA_4BYTE, REG_RAX, 3u)]
    [TestCase(false, -128, 0u, EA_4BYTE, REG_RAX, 4u)]
    [TestCase(false, 127, 0u, EA_4BYTE, REG_RAX, 4u)]
    [TestCase(false, 128, 0u, EA_4BYTE, REG_RAX, 7u)]
    [TestCase(true, -129, 0u, EA_4BYTE, REG_RAX, 6u)]
    [TestCase(true, -129, 1u, EA_4BYTE, REG_RAX, 3u)]
    [TestCase(false, -1, 1u, EA_4BYTE, REG_RAX, 3u)]
    [TestCase(true, 0, 0u, EA_8BYTE, REG_RAX, 4u)]
    [TestCase(true, 0, 0u, EA_2BYTE, REG_RAX, 4u)]
    [TestCase(true, 0, 0u, EA_1BYTE, REG_RSI, 4u)]
    [TestCase(true, 0, 0u, EA_4BYTE, REG_R8, 4u)]
    public static void StackStoreSizeUsesFrameBaseDisplacementOperandWidthAndRegisters(
        bool fpBased, int offset, uint fieldOffset, emitAttr attr, regNumber reg, uint expected)
    {
        var emitter = CreateEmitter(fpBased, offset);
        var id = CreateDescriptor(INS_mov, attr, IF_SWR_RRD);
        id.idReg1(reg);
        id.idAddr().iiaLclVar.initLclVarAddr(0, fieldOffset);

        Assert.That(emitter.emitInsSizeSV(id, insCodeMR(INS_mov), 0, 123456), Is.EqualTo(expected));
    }

    [TestCase(INS_add, EA_8BYTE, -128, 5u)]
    [TestCase(INS_add, EA_8BYTE, 127, 5u)]
    [TestCase(INS_add, EA_8BYTE, 128, 8u)]
    [TestCase(INS_mov, EA_8BYTE, 1, 8u)]
    [TestCase(INS_test, EA_4BYTE, 1, 7u)]
    [TestCase(INS_add, EA_2BYTE, 128, 6u)]
    public static void StackImmediatesUseSignedByteEncodingOnlyWhereTheInstructionAllows(
        instruction ins, emitAttr attr, int value, uint expected)
    {
        var emitter = CreateEmitter(true, 0);
        var id = CreateDescriptor(ins, attr, IF_SRW_CNS);
        id.idAddr().iiaLclVar.initLclVarAddr(0, 0);

        Assert.That(emitter.emitInsSizeSV(id, insCodeMI(ins), 0, 0, value), Is.EqualTo(expected));
    }

    [TestCase(INS_addps, EA_64BYTE, IF_RWR_SRD, 64)]
    [TestCase(INS_cvtps2pd, EA_64BYTE, IF_RWR_SRD, 32)]
    [TestCase(INS_pshufb, EA_64BYTE, IF_RWR_SRD, 64)]
    [TestCase(INS_movss, EA_4BYTE, IF_RWR_SRD, 4)]
    [TestCase(INS_cvtsd2si64, EA_8BYTE, IF_RWR_SRD, 8)]
    [TestCase(INS_vbroadcastf32x2, EA_64BYTE, IF_RWR_SRD, 8)]
    [TestCase(INS_vbroadcastf32x4, EA_64BYTE, IF_RWR_SRD, 16)]
    [TestCase(INS_vbroadcastf32x8, EA_64BYTE, IF_RWR_SRD, 32)]
    [TestCase(INS_vcvtps2ph, EA_64BYTE, IF_SWR_RRD_CNS, 32)]
    [TestCase(INS_vpmovdb, EA_64BYTE, IF_SWR_RRD, 16)]
    [TestCase(INS_vpmovqb, EA_64BYTE, IF_SWR_RRD, 8)]
    [TestCase(INS_pslld, EA_64BYTE, IF_RWR_RRD_SRD, 16)]
    [TestCase(INS_pslld, EA_64BYTE, IF_RWR_SRD_CNS, 64)]
    [TestCase(INS_psllw, EA_64BYTE, IF_RWR_RRD_SRD, 16)]
    [TestCase(INS_psllw, EA_64BYTE, IF_RWR_SRD_CNS, 64)]
    [TestCase(INS_movddup, EA_16BYTE, IF_RWR_SRD, 8)]
    [TestCase(INS_movddup, EA_32BYTE, IF_RWR_SRD, 32)]
    public static void EveryEvexTupleScalesExactSignedByteBoundaries(
        instruction ins, emitAttr attr, Emitter.insFormat format, int scale)
    {
        var emitter = CreateEmitter(false, 0);
        var id = CreateDescriptor(ins, attr, format);
        id.idSetEvexCompressedDisplacementBit();

        foreach (var value in new[] { -129, -128, -1, 0, 1, 127, 128 })
        {
            nint displacement = value * scale;
            var expected = value is >= -128 and <= 127;
            var compressed = emitter.TryEvexCompressDisp8Byte(id, displacement, out var result, out var fits);

            Assert.That(compressed, Is.EqualTo(expected), $"instruction {ins}, displacement {displacement}");
            Assert.That(fits, Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(expected ? (nint)value : displacement));
        }

        Assert.That(emitter.TryEvexCompressDisp8Byte(id, scale + 1, out var unscaled, out var fitsUnscaled), Is.False);
        Assert.That(unscaled, Is.EqualTo((nint)(scale + 1)));
        Assert.That(fitsUnscaled, Is.False);
    }

    [TestCase(-128, false, true)]
    [TestCase(127, false, true)]
    [TestCase(128, true, true)]
    [TestCase(129, false, false)]
    public static void CompressionPrefersVexWhenAnUnscaledDisplacementAlreadyFits(
        int displacement, bool compressed, bool fits)
    {
        var emitter = CreateEmitter(false, 0);
        var id = CreateDescriptor(INS_addps, EA_16BYTE, IF_RWR_SRD);

        Assert.That(emitter.TryEvexCompressDisp8Byte(id, displacement, out var result, out var actualFits),
            Is.EqualTo(compressed));
        Assert.That(result, Is.EqualTo(compressed ? (nint)(displacement / 16) : displacement));
        Assert.That(actualFits, Is.EqualTo(fits));
    }

    [TestCase(-128, true)]
    [TestCase(127, true)]
    [TestCase(128, false)]
    public static void ApxWithoutTupleInformationKeepsUnitDisplacementScale(int displacement, bool fits)
    {
        var emitter = CreateEmitter(false, 0);
        emitter.UsePromotedEvexEncodings = true;
        var id = CreateDescriptor(INS_add, EA_8BYTE, IF_SRW_RRD);

        Assert.That(emitter.TryEvexCompressDisp8Byte(id, displacement, out var result, out var actualFits),
            Is.EqualTo(fits));
        Assert.That(result, Is.EqualTo((nint)displacement));
        Assert.That(actualFits, Is.EqualTo(fits));
    }

    [TestCase(INS_addps, 4)]
    [TestCase(INS_addpd, 8)]
    [TestCase(INS_cvtps2pd, 4)]
    public static void EmbeddedBroadcastUsesInputWidthInsteadOfVectorWidth(instruction ins, int inputWidth)
    {
        var emitter = CreateEmitter(false, 0);
        var id = CreateDescriptor(ins, EA_64BYTE, IF_RWR_SRD);
        id.idSetEvexBroadcastBit();
        var displacement = 127 * inputWidth;

        Assert.That(emitter.TryEvexCompressDisp8Byte(id, displacement, out var result, out var fits), Is.True);
        Assert.That(result, Is.EqualTo((nint)127));
        Assert.That(fits, Is.True);
        Assert.That(emitter.TryEvexCompressDisp8Byte(id, displacement + 1, out result, out fits), Is.False);
        Assert.That(result, Is.EqualTo((nint)(displacement + 1)));
        Assert.That(fits, Is.False);
    }

    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    [TestCase(-8193L)]
    [TestCase(8192L)]
    public static void OutOfRangeDisplacementsAreNotTruncatedBeforeCompression(long displacement)
    {
        var emitter = CreateEmitter(false, 0);
        var id = CreateDescriptor(INS_addps, EA_64BYTE, IF_RWR_SRD);

        Assert.That(emitter.TryEvexCompressDisp8Byte(id, (nint)displacement, out var result, out var fits), Is.False);
        Assert.That(result, Is.EqualTo((nint)displacement));
        Assert.That(fits, Is.False);
    }

    [TestCase(INS_mov, false, 5u)]
    [TestCase(INS_movzx, false, 5u)]
    [TestCase(INS_add, true, 7u)]
    public static void ApxPrefixesAbsorbLegacyEscapeBytesWithoutCountingRexTwice(
        instruction ins, bool ndd, uint expected)
    {
        var emitter = CreateEmitter(true, 0);
        emitter.UseRex2Encodings = true;
        emitter.UsePromotedEvexEncodings = true;
        var id = CreateDescriptor(ins, EA_8BYTE, ndd ? IF_RWR_RRD_SRD : IF_RWR_SRD);
        id.idReg1(REG_R16);
        id.idAddr().iiaLclVar.initLclVarAddr(0, 0);

        if (ndd)
        {
            id.idSetEvexNdContext();
        }

        Assert.That(emitter.emitInsSizeSV(id, insCodeRM(ins), 0, 0), Is.EqualTo(expected));
    }

    [Test]
    public static void StackSizingPromotesToEvexForACompressibleVectorDisplacement()
    {
        var emitter = CreateEmitter(false, 256);
        var id = CreateDescriptor(INS_movups, EA_16BYTE, IF_SWR_RRD);
        id.idReg1(REG_XMM0);
        id.idAddr().iiaLclVar.initLclVarAddr(0, 0);

        Assert.That(TakesEvexPrefix(emitter, id), Is.False);
        Assert.That(emitter.emitInsSizeSV(id, insCodeMR(INS_movups), 0, 0), Is.EqualTo(8u));
        Assert.That(TakesEvexPrefix(emitter, id), Is.True);
        Assert.That(emitter.TryEvexCompressDisp8Byte(id, 256, out var compressed, out var fits), Is.True);
        Assert.That(compressed, Is.EqualTo((nint)16));
        Assert.That(fits, Is.True);
    }

    private static Emitter.instrDesc CreateDescriptor(instruction ins, emitAttr attr, Emitter.insFormat format)
    {
        var type = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing basic instruction descriptor.");
        var id = (Emitter.instrDesc)(Activator.CreateInstance(type, nonPublic: true)
            ?? throw new AssertionException("Could not create a basic instruction descriptor."));
        id.idIns(ins);
        id.idOpSize(attr);
        id.idInsFmt(format);
        id.idReg1(REG_RAX);
        id.idReg2(REG_RAX);
        return id;
    }

    private static Emitter CreateEmitter(bool fpBased, int offset)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.codeGen = new CodeGen(compiler) { IsFramePointerUsed = fpBased };
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc
        {
            Type = TYP_LONG,
            lvOnFrame = true,
            lvFramePointerBased = fpBased,
            StackOffset = offset,
        }];
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.compCalleeRegsPushed = 0;
        var emitter = compiler.codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        return emitter;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TakesEvexPrefix")]
    private static extern bool TakesEvexPrefix(Emitter emitter, Emitter.instrDesc id);
}
