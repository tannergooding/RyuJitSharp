// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterXarchDiagnosticHelpersTests
{
#if TARGET_AMD64
    [TestCase(regNumber.REG_EAX, emitAttr.EA_1BYTE, "al")]
    [TestCase(regNumber.REG_EBP, emitAttr.EA_1BYTE, "bpl")]
    [TestCase(regNumber.REG_R8, emitAttr.EA_1BYTE, "r8b")]
    [TestCase(regNumber.REG_R8, emitAttr.EA_2BYTE, "r8w")]
    [TestCase(regNumber.REG_R8, emitAttr.EA_4BYTE, "r8d")]
    [TestCase(regNumber.REG_R8, emitAttr.EA_8BYTE, "r8")]
#endif
    [TestCase(regNumber.REG_XMM0, emitAttr.EA_16BYTE, "xmm0")]
    [TestCase(regNumber.REG_XMM0, emitAttr.EA_32BYTE, "ymm0")]
    [TestCase(regNumber.REG_XMM0, emitAttr.EA_64BYTE, "zmm0")]
    public static void RegisterNamesUseNativeOperandWidths(regNumber reg, emitAttr attr, string expected)
    {
        Assert.That(CreateEmitter().emitRegName(reg, attr, varName: false), Is.EqualTo(expected));
    }

    [Test]
    public static void SeparateVectorNamesUseNativeTable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Emitter.emitXMMregName(regNumber.REG_XMM1), Is.EqualTo("xmm1"));
            Assert.That(Emitter.emitYMMregName(regNumber.REG_XMM1), Is.EqualTo("ymm1"));
            Assert.That(Emitter.emitZMMregName(regNumber.REG_XMM1), Is.EqualTo("zmm1"));
        });
    }

    [TestCase(0, "[0x0000]")]
    [TestCase(0x1234, "[0x1234]")]
    [TestCase(-0x1000000, "[0xFF000000]")]
    public static void AddressModePreservesNativeDisplacementWidths(int displacement, string expected)
    {
        var emitter = CreateEmitter();
        var id = displacement < -0xFFFFFF
            ? Descriptor(largeDisplacement: displacement)
            : Descriptor();
        id.idAddr().iiaAddrMode.amBaseReg = regNumber.REG_NA;
        id.idAddr().iiaAddrMode.amIndxReg = regNumber.REG_NA;
        id.idAddr().iiaAddrMode.amDisp = displacement;

        Assert.That(Capture(() => emitter.emitDispAddrMode(id)), Is.EqualTo(expected));
    }

    [Test]
    public static void RelocationTextRespectsDiffableAssembly()
    {
        var compiler = CreateCompiler();
        var emitter = CreateEmitter(compiler);

        Assert.That(Capture(() => emitter.emitDispReloc(0x1234)), Is.EqualTo("(reloc 0x1234)"));

        compiler.opts.disAsm = true;
        compiler.opts.disDiffable = true;
        Assert.That(Capture(() => emitter.emitDispReloc(0x1234)), Is.EqualTo("(reloc)"));
    }

    [TestCase(-3, -16, "[TEMP_03-0x10]")]
    [TestCase(7, 32, "[V07+0x20]")]
    public static void FrameReferencePreservesTemporaryAndLocalText(int variable, int displacement, string expected)
    {
        var compiler = CreateCompiler();
        compiler.lvaDoneFrameLayout = Compiler.NO_FRAME_LAYOUT;
        var emitter = CreateEmitter(compiler);

        Assert.That(Capture(() => DisplayFrameRef(emitter, variable, displacement, 0, false)),
            Is.EqualTo(expected));
    }

    [Test]
    public static void SpecialStaticFieldUsesSegmentAndFourDigitOffset()
    {
        var emitter = CreateEmitter();

        Assert.That(Capture(() => DisplayClsVar(emitter, FLD_GLOBAL_FS, 0x42, false)),
            Is.EqualTo("FS:[0x0042]"));
    }

    [TestCase(instruction.INS_shl_1, 0, ", 1")]
    [TestCase(instruction.INS_shl, 0, ", cl")]
    [TestCase(instruction.INS_shl_N, 13, ", 13")]
    [TestCase(instruction.INS_mov, 0, "")]
    public static void ShiftDisplayPreservesOperandKind(instruction ins, int count, string expected)
    {
        var emitter = CreateEmitter();

        Assert.That(Capture(() => emitter.emitDispShift(ins, count)), Is.EqualTo(expected));
    }

    [Test]
    public static void HexBytesPadToTargetInstructionColumn()
    {
        var compiler = CreateCompiler();
        compiler.opts.disCodeBytes = true;
        var emitter = CreateEmitter(compiler);
        var bytes = stackalloc byte[] { 0x0F, 0xAB };
        var pointer = (byte*)Unsafe.AsPointer(ref bytes[0]);
#if TARGET_AMD64
        const int digits = 10;
#else
        const int digits = 6;
#endif
        Assert.That(Capture(() => emitter.emitDispInsHex(Descriptor(), pointer, 2)),
            Is.EqualTo(" 0FAB" + new string(' ', (digits - 2) * 2)));

        compiler.opts.disDiffable = true;
        Assert.That(Capture(() => emitter.emitDispInsHex(Descriptor(), pointer, 2)), Is.Empty);
    }

    [Test]
    public static void EmbeddedRoundingAndMaskPreserveNativeSuffixes()
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        var rounded = Descriptor();
        rounded.idSetEvexbContext(1);
        Assert.That(Capture(() => emitter.emitDispEmbRounding(rounded)), Is.EqualTo(" {rd-sae}"));

        var masked = Descriptor(instruction.INS_addps);
        masked.idSetEvexAaaContext(2 << 2);
        masked.idSetEvexZContext();
        Assert.That(Capture(() => emitter.emitDispEmbMasking(masked)), Is.EqualTo(" {k2}{z}"));
    }

    [Test]
    public static void EmbeddedBroadcastDisplaysVectorToElementCount()
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = true;
        emitter.UseEvexEncodings = true;
        var id = Descriptor(instruction.INS_addps);
        id.idInsFmt(Emitter.insFormat.IF_RWR_ARD);
        id.idOpSize(emitAttr.EA_16BYTE);
        id.idSetEvexBroadcastBit();

        Assert.That(Capture(() => emitter.emitDispEmbBroadcastCount(id)), Is.EqualTo(" {1to4}"));
    }

    [Test]
    public static void ConstantDisplayKeepsDecimalHexAndSeparator()
    {
        var emitter = CreateEmitter();

        Assert.That(Capture(() => emitter.emitDispConstant(Descriptor(constant: 42))),
            Is.EqualTo(", 42"));
        Assert.That(Capture(() => emitter.emitDispConstant(Descriptor(constant: -0x10000), skipComma: true)),
            Is.EqualTo("-0x10000"));
    }

    private static Compiler CreateCompiler() =>
        (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

    private static Emitter CreateEmitter(Compiler? compiler = null)
    {
        compiler ??= CreateCompiler();
        var emitter = new CodeGen(compiler)
        {
            IsFramePointerUsed = false,
        }.Emitter;
        emitter.emitBegCG(compiler, default);
        return emitter;
    }

    private static Emitter.instrDesc Descriptor(instruction ins = instruction.INS_mov, int constant = 0,
        int? largeDisplacement = null)
    {
        var id = new DescriptorView(constant, largeDisplacement).Value;
        id.idIns(ins);
        id.idInsFmt(Emitter.insFormat.IF_CNS);
        id.idOpSize(emitAttr.EA_4BYTE);
        id.idCodeSize(1);
        return id;
    }

    private sealed class DescriptorView : Emitter
    {
        public instrDesc Value { get; }

        public DescriptorView(int constant, int? largeDisplacement) : base(new CodeGen(CreateCompiler()))
        {
            if (largeDisplacement is int displacement)
            {
                var amd = new instrDescAmd { idaAmdVal = displacement };
                amd.idSetIsLargeDsp();
                Value = amd;
            }
            else if (instrDesc.fitsInSmallCns(constant))
            {
                var basic = new instrDescBasic();
                basic.idSmallCns(constant);
                Value = basic;
            }
            else
            {
                var large = new instrDescCns { idcCnsVal = constant };
                large.idSetIsLargeCns();
                Value = large;
            }
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispFrameRef")]
    private static extern void DisplayFrameRef(Emitter emitter, int variable, int displacement, uint offset, bool assembly);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispClsVar")]
    private static extern void DisplayClsVar(Emitter emitter, CORINFO_FIELD_STRUCT_* field, nint offset, bool reloc);

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
#endif
