// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64AndUnixEmitterSimpleRecordingTests
{
    [TestCase(127, 2u)]
    [TestCase(128, 5u)]
    public static void ImmediateOnlyRecordingUsesNativeConstantWidths(int value, uint expectedSize)
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

            emitter.emitIns_I(INS_push, EA_PTRSIZE, value);
            var descriptor = LastInstruction(emitter) ?? throw new AssertionException("Missing push descriptor.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_push));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_CNS));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(expectedSize));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);
}
#endif
