// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && PROFILING_SUPPORTED
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.InsGroupFlags;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class X86ProfilingCallbackTests
{
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL)]
    public static void UnhookedCallbacksPreserveStackAndDoNotEmitInstructions(CorInfoHelpFunc helper)
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= Prolog;
            StackLevel(codeGen) = 24;
            var zeroed = true;

            codeGen.genProfilingEnterCallback(REG_EAX, ref zeroed);
            codeGen.genProfilingLeaveCallback(helper);

            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(CurrentCount(emitter), Is.Zero);
            Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo(24u));
            Assert.That(zeroed, Is.True);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }

    [Test]
    public static void SinglePushAndStackAdjustmentUseUnsignedNativeWidths()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, _) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            StackLevel(codeGen) = 12;

            SinglePush(codeGen);
            Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo(16u));

            StackLevel(codeGen) = uint.MaxValue;
            AddStackLevel(codeGen, 4);
            Assert.That(codeGen.getCurrentStackLevel(), Is.EqualTo(3u));
        });
    }

#if UNIX_X86_ABI
    [Test]
    public static void NestedAlignmentTracksCurrentAndHighWaterMarkIndependently()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, _) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            Assert.That(NestedAlignment(codeGen), Is.Zero);
            Assert.That(MaxNestedAlignment(codeGen), Is.Zero);

            AddNestedAlignment(codeGen, 12);
            AddNestedAlignment(codeGen, 4);
            Assert.That(NestedAlignment(codeGen), Is.EqualTo(16u));
            Assert.That(MaxNestedAlignment(codeGen), Is.EqualTo(16u));

            SubtractNestedAlignment(codeGen, 16);
            Assert.That(NestedAlignment(codeGen), Is.Zero);
            Assert.That(MaxNestedAlignment(codeGen), Is.EqualTo(16u));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genSinglePush")]
    private static extern void SinglePush(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddStackLevel")]
    private static extern void AddStackLevel(CodeGen codeGen, uint adjustment);

#if UNIX_X86_ABI
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<curNestedAlignment>k__BackingField")]
    private static extern ref uint NestedAlignment(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "maxNestedAlignment")]
    private static extern ref uint MaxNestedAlignment(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddNestedAlignment")]
    private static extern void AddNestedAlignment(CodeGen codeGen, uint adjustment);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SubtractNestedAlignment")]
    private static extern void SubtractNestedAlignment(CodeGen codeGen, uint adjustment);
#endif
}
#endif
