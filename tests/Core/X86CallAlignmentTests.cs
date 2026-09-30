// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class X86CallAlignmentTests
{
#if UNIX_X86_ABI
    [TestCase(0u, 0u)]
    [TestCase(4u, 12u)]
    [TestCase(12u, 4u)]
    [TestCase(16u, 0u)]
    [TestCase(uint.MaxValue, 1u)]
    public static void CallAlignmentRetainsUnsignedPadAndStackArgumentSize(uint level, uint expectedPad)
    {
        CallArgs args = default;
        args.SetStkSizeBytes(4);
        args.ComputeStackAlignment(level);

        Assert.That(args.GetStkSizeBytes(), Is.EqualTo(4u));
        Assert.That(args.GetStkAlign(), Is.EqualTo(expectedPad));
        Assert.That(args.IsStkAlignmentDone, Is.False);
    }
#else
    [Test]
    public static void WindowsCallAlignmentDoesNotChangeTheStackOrEmitInstructions()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            var call = new GenTreeCall(TYP_VOID);
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            StackLevel(codeGen) = 12;

            Align(codeGen, call);
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genAlignStackBeforeCall")]
    private static extern void Align(CodeGen codeGen, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRemoveAlignmentAfterCall")]
    private static extern void Remove(CodeGen codeGen, GenTreeCall call, uint bias);
#endif
}
#endif
