// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionDisplayFormatTests
{
    [Test]
    public static void StackRegisterConstantKeepsNativeOperandText()
    {
        var emitter = CreateEmitter();
        var id = DescriptorView.Create();
        id.idIns(INS_pshufd);
        id.idInsFmt(IF_SRD_RRD_CNS);
        id.idOpSize(EA_16BYTE);
        id.idCodeSize(1);
        id.idReg1(REG_XMM0);
        id.idSmallCns(7);
        id.idAddr().iiaLclVar.initLclVarAddr(0, 0);

        var output = Capture(() => emitter.emitDispIns(id, true, false, false));
        Assert.That(output, Is.AnyOf(
            "       pshufd   xmmword ptr [V00], xmm0, 7\r\n",
            "       pshufd   xmmword ptr [V00], xmm0, 7\n"));
    }

    [Test]
    public static void ConstantFormatDoesNotApplyBmiOperandSwap()
    {
        var emitter = CreateEmitter();
        var id = DescriptorView.Create();
        id.idIns(INS_bextr);
        id.idInsFmt(IF_RWR_RRD_SRD_CNS);
        id.idOpSize(EA_4BYTE);
        id.idCodeSize(1);
        id.idReg1(REG_EAX);
        id.idReg2(REG_ECX);
        id.idSmallCns(7);
        id.idAddr().iiaLclVar.initLclVarAddr(0, 0);

        var output = Capture(() => emitter.emitDispIns(id, true, false, false));
        Assert.That(output, Is.AnyOf(
            "       bextr    eax, ecx, dword ptr [V00], 7\r\n",
            "       bextr    eax, ecx, dword ptr [V00], 7\n"));
    }

#if DEBUG
    [TestCase(INS_pmovmskb, EA_16BYTE, REG_EAX, REG_XMM0)]
    [TestCase(INS_cvtsi2ss32, EA_4BYTE, REG_XMM0, REG_EAX)]
    [TestCase(INS_cvttsd2si32, EA_4BYTE, REG_EAX, REG_XMM0)]
    public static void RegisterPairRejectsUnexpectedEvexMask(
        instruction ins, emitAttr size, regNumber destination, regNumber source)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = CreateEmitter();
        var id = DescriptorView.Create();
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD);
        id.idOpSize(size);
        id.idCodeSize(1);
        id.idReg1(destination);
        id.idReg2(source);
        id.idSetEvexAaaContext(4);

        _ = Capture(() => emitter.emitDispIns(id, true, false, false));

        Assert.That(context.Assertions, Is.EqualTo(1));
    }

    [TestCase(INS_cvtdq2pd)]
    [TestCase(INS_pmovsxbd)]
    [TestCase(INS_pmovsxbq)]
    public static void RegisterPairTupleInputSizeMatchesMaskRatio(instruction ins)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = CreateEmitter();
        var id = DescriptorView.Create();
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD);
        id.idOpSize(EA_16BYTE);
        id.idCodeSize(1);
        id.idReg1(REG_XMM0);
        id.idReg2(REG_XMM1);

        var output = Capture(() => emitter.emitDispIns(id, true, false, false));

        Assert.That(output, Does.Contain("xmm0, xmm1"));
        Assert.That(context.Assertions, Is.Zero);
    }

    [TestCase(IF_SRD_RRD_CNS, INS_mov)]
    [TestCase(IF_RWR_RRD_SHF, INS_add)]
    public static void SpecializedFormatsKeepTheirNativeInstructionAssertions(
        Emitter.insFormat format, instruction ins)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
        using var tls = new JitTls(&context.JitInfo);
        var emitter = CreateEmitter();
        var id = DescriptorView.Create();
        id.idIns(ins);
        id.idInsFmt(format);
        id.idOpSize(EA_4BYTE);
        id.idCodeSize(1);
        id.idReg1(REG_EAX);
        if (format == IF_RWR_RRD_SHF)
        {
            id.idReg2(REG_ECX);
        }
        id.idSmallCns(7);
        id.idAddr().iiaLclVar.initLclVarAddr(0, 0);

        _ = Capture(() => emitter.emitDispIns(id, true, false, false));

        Assert.That(context.Assertions, Is.EqualTo(1));
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        ((AssertionContext*)self)->Assertions++;
        return 0;
    }
#endif

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        return emitter;
    }

    private abstract class DescriptorView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Create() => new instrDescBasic();
    }

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
