// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class X86EmitterStaticOutputTests
{
    [TestCase(false, EA_1BYTE, "A078563412")]
    [TestCase(false, EA_2BYTE, "66A178563412")]
    [TestCase(false, EA_4BYTE, "A178563412")]
    [TestCase(true, EA_4BYTE, "A378563412")]
    public static void AccumulatorStaticMovUsesAbsoluteAddressInsteadOfModRm(bool store, emitAttr size, string hex)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            var id = NewInstructionDisplacement(emitter, size, 0x12345678);
            id.idIns(INS_mov);
            id.idInsFmt(store ? IF_MWR_RRD : IF_RWR_MRD);
            id.idReg1(REG_EAX);
            id.idAddr().iiaFieldHnd = FLD_GLOBAL_DS;
            var code = (ulong)(store ? insCodeMR(INS_mov) : insCodeRM(INS_mov)) | 0x0500;
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, code, null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
        });
    }

    internal static void WithEmitter(Action<Compiler, Emitter> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
            action(compiler, emitter);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrDsp")]
    private static extern Emitter.instrDesc NewInstructionDisplacement(Emitter emitter, emitAttr attr, nint displacement);
}
#endif
