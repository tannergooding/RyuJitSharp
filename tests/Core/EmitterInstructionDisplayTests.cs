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
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionDisplayTests
{
    [Test]
    public static void FormatNamesFollowNativeTableAndInvalidValueMarker()
    {
        for (uint format = 0; format < (uint)IF_COUNT; format++)
        {
            Assert.That(Emitter.emitIfName(format),
                Is.EqualTo(Enum.GetName((Emitter.insFormat)format)));
        }
        Assert.That(Emitter.emitIfName((uint)IF_COUNT), Is.EqualTo("??120??"));
        Assert.That(Emitter.emitIfName(uint.MaxValue), Is.EqualTo("??4294967295??"));
    }

    [TestCase(instruction.INS_mov, IF_RWR_RRD, emitAttr.EA_8BYTE, regNumber.REG_EAX, regNumber.REG_ECX, "mov      rax, rcx")]
    [TestCase(instruction.INS_movsxd, IF_RWR_RRD, emitAttr.EA_4BYTE, regNumber.REG_EAX, regNumber.REG_ECX, "movsxd   rax, ecx")]
    [TestCase(instruction.INS_shl_1, IF_RRW, emitAttr.EA_4BYTE, regNumber.REG_EAX, regNumber.REG_NA, "shl      eax, 1")]
    public static void DisplayRegisterOperandsInNativeOrder(instruction ins, Emitter.insFormat format,
        emitAttr size, regNumber first, regNumber second, string expected)
    {
        var emitter = CreateEmitter();
        var id = Descriptor(ins, format, size, first, second);

        var text = Capture(() => emitter.emitDispIns(id, isNew: true, doffs: false,
            asmfm: false, offset: 0, code: null, size: 0, ig: null));

        Assert.That(text, Is.EqualTo($"       {expected}{Environment.NewLine}"));
    }

    [TestCase(instruction.INS_l_je, "je")]
    [TestCase(instruction.INS_i_jmp, "jmp")]
    [TestCase(instruction.INS_seto_apx, "setzuo")]
    [TestCase(instruction.INS_r_movsb, "rep movsb")]
    [TestCase(instruction.INS_blendvpd, "blendvpd")]
    [TestCase(instruction.INS_lfence, "lfence")]
    [TestCase(instruction.INS_andn, "andn")]
    [TestCase(instruction.INS_kaddb, "kaddb")]
    public static void DisplayMnemonicUsesNativeTableName(instruction ins, string expected)
    {
        var emitter = CreateEmitter();
        var id = Descriptor(ins, IF_NONE, emitAttr.EA_4BYTE, regNumber.REG_NA, regNumber.REG_NA);

        Assert.That(DisplayName(emitter, id), Is.EqualTo(expected));
    }

    [TestCase(instruction.INS_invalid, "INVALID")]
    [TestCase(instruction.INS_push_hide, "push")]
    [TestCase(instruction.INS_cvttsd2si64, "vcvttsd2si")]
    [TestCase(instruction.INS_vcvttss2usis64, "vcvttss2usis")]
    [TestCase(instruction.INS_movd64, "vmovq")]
    [TestCase(instruction.INS_tail_i_jmp, "tail.jmp")]
    [TestCase(instruction.INS_r_movsq, "rep movsq")]
    [TestCase(instruction.INS_setno_apx, "setzuno")]
    [TestCase(instruction.INS_blendvpd, "blendvpd")]
    [TestCase(instruction.INS_sha256rnds2, "sha256rnds2")]
    public static void InstructionNamesComeFromTheNativeTable(instruction ins, string expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(new CodeGen(compiler).genInsName(ins), Is.EqualTo(expected));
    }

#if TARGET_AMD64
    [TestCase(instruction.INS_cmppd, 1, false, "cmpltpd")]
    [TestCase(instruction.INS_cmppd, 17, true, "vcmplt_oqpd")]
    [TestCase(instruction.INS_vpcmpuw, 6, true, "vpcmpgtuw")]
    [TestCase(instruction.INS_vpcmpuw, 6, false, "pcmpgtuw")]
    [TestCase(instruction.INS_pclmulqdq, 0x10, true, "vpclmullqhqdq")]
    [TestCase(instruction.INS_pclmulqdq, 1, false, "pclmulhqlqdq")]
    public static void PseudoNamesRespectControlAndEncoding(instruction ins, int control,
        bool vex, string expected)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = vex;
        var id = Descriptor(ins, IF_NONE, emitAttr.EA_16BYTE,
            regNumber.REG_NA, regNumber.REG_NA, control);

        Assert.That(DisplayName(emitter, id), Is.EqualTo(expected));
    }

    [TestCase(instruction.INS_roundpd, emitAttr.EA_16BYTE, true, false, "vroundpd")]
    [TestCase(instruction.INS_roundpd, emitAttr.EA_64BYTE, true, true, "vrndscalepd")]
    [TestCase(instruction.INS_vbroadcastf32x4, emitAttr.EA_32BYTE, true, false, "vbroadcastf128")]
    [TestCase(instruction.INS_vbroadcastf32x4, emitAttr.EA_64BYTE, true, true, "vbroadcastf32x4")]
    [TestCase(instruction.INS_roundpd, emitAttr.EA_16BYTE, false, false, "roundpd")]
    public static void EvexNamesUseNativePseudoForms(instruction ins, emitAttr size,
        bool vex, bool evex, string expected)
    {
        var emitter = CreateEmitter();
        emitter.UseVexEncodings = vex;
        emitter.UseEvexEncodings = evex;
        var id = Descriptor(ins, IF_NONE, size, regNumber.REG_NA, regNumber.REG_NA);

        Assert.That(DisplayName(emitter, id), Is.EqualTo(expected));
    }

    [TestCase(false, "push")]
    [TestCase(true, "pushp")]
    public static void ApxPpxContextAppendsSuffix(bool ppx, string expected)
    {
        var emitter = CreateEmitter();
        var id = Descriptor(instruction.INS_push, IF_NONE, emitAttr.EA_4BYTE,
            regNumber.REG_NA, regNumber.REG_NA);
        if (ppx)
        {
            id.idSetApxPpxContext();
        }

        Assert.That(DisplayName(emitter, id), Is.EqualTo(expected));
    }
#endif

    [Test]
    public static void OperandlessInstructionKeepsEmptySizeAndOffsetColumn()
    {
        var emitter = CreateEmitter();
        var id = Descriptor(instruction.INS_nop, IF_NONE, emitAttr.EA_4BYTE,
            regNumber.REG_NA, regNumber.REG_NA);

        var output = Capture(() => emitter.emitDispIns(id, isNew: false, doffs: false,
            asmfm: false, offset: 42, code: null, size: 0, ig: null));

        Assert.That(output, Is.EqualTo($"00002A nop      {Environment.NewLine}"));
    }

    [Test]
    public static void AddressModeKeepsRegisterScaleAndSignedFrameDisplacement()
    {
        var emitter = CreateEmitter();
        var id = Descriptor(instruction.INS_mov, IF_RWR_ARD, emitAttr.EA_8BYTE,
            regNumber.REG_EAX, regNumber.REG_NA);
        id.idAddr().iiaAddrMode.amBaseReg = regNumber.REG_EAX;
        id.idAddr().iiaAddrMode.amIndxReg = regNumber.REG_ECX;
        id.idAddr().iiaAddrMode.amScale = 2;
        id.idAddr().iiaAddrMode.amDisp = -32;

        var output = Capture(() => emitter.emitDispAddrMode(id));

        Assert.That(output, Is.EqualTo("[rax+4*rcx-0x20]"));
    }

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler)
        {
            IsFramePointerUsed = false,
        };
        var emitter = codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        return emitter;
    }

    private static Emitter.instrDesc Descriptor(instruction ins, Emitter.insFormat format,
        emitAttr size, regNumber first, regNumber second, int constant = 0)
    {
        var id = DescriptorView.Create(constant);
        id.idIns(ins);
        id.idInsFmt(format);
        id.idOpSize(size);
        id.idCodeSize(1);
        if (first != regNumber.REG_NA)
        {
            id.idReg1(first);
        }
        if (second != regNumber.REG_NA)
        {
            id.idReg2(second);
        }
        return id;
    }

    private abstract class DescriptorView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Create(int constant)
        {
            if (instrDesc.fitsInSmallCns(constant))
            {
                var descriptor = new instrDescBasic();
                descriptor.idSmallCns(constant);
                return descriptor;
            }

            var large = new instrDescCns
            {
                idcCnsVal = constant,
            };
            large.idSetIsLargeCns();
            return large;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDisplayName")]
    private static extern string DisplayName(Emitter emitter, Emitter.instrDesc id);

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
