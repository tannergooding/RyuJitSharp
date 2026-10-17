// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class X86EmitterInstructionDispatchTests
{
    [Test]
    public static void ThreeRegisterAvxOutputIsNotSupported()
    {
        Assume.That(Configuration.TARGET_X86);
        WithEmitter((_, emitter) =>
            Assert.That(emitter.IsThreeOperandAVXInstruction(INS_addps), Is.False));
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
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
            emitter.emitBegFN(true);
            action(compiler, emitter);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
