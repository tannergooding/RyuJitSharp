// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class X86CallAlignmentTests
{
    [Test]
    public static void WindowsCallCleanupWithoutBiasDoesNotChangeTheStackOrEmitInstructions()
    {
        if (!Configuration.TARGET_X86)
        {
            Assert.Ignore("The no-op alignment case requires the Windows x86 target.");
        }

        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            var call = new GenTreeCall(TYP_VOID);
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            StackLevel(codeGen) = 12;

            Remove(codeGen, call, 0);

            Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo(12u));
            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(CurrentCount(emitter), Is.Zero);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRemoveAlignmentAfterCall")]
    private static extern void Remove(CodeGen codeGen, GenTreeCall call, uint bias);
}
