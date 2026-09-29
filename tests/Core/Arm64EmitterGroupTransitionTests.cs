// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 || EMITTER_STATS
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64EmitterGroupTransitionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void PreparingAndTransitioningGroupsRetainsTheNativeCapacity(bool extend)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new Emitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();

        var first = new insGroup { igFlags = InsGroupFlags.Prolog };
        FirstGroup(emitter) = first;
        LastGroup(emitter) = first;
        NextNumber(emitter) = 1;
        Prepare(emitter, first);
        var buffer = Buffer(emitter);
        var capacity = Capacity(emitter);
        Assert.That(emitter.emitCurIGnonEmpty(), Is.False);

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
        var expectedCapacity = 200 * (16 + DebugPrefix(emitter));
#else
        var expectedCapacity = (14 * (8 + DebugPrefix(emitter))) + (50 * (16 + DebugPrefix(emitter)));
#endif
        Assert.That(capacity, Is.EqualTo((nuint)expectedCapacity));

        Count(emitter) = 12;
        CodeSize(emitter) = 28;
#if TARGET_XARCH || EMIT_BACKWARDS_NAVIGATION
        LastSize(emitter) = 20;
#endif
        Prepare(emitter, first);

        Assert.That(Buffer(emitter), Is.SameAs(buffer));
        Assert.That(Count(emitter), Is.Zero);
        Assert.That(CodeSize(emitter), Is.Zero);
#if TARGET_XARCH || EMIT_BACKWARDS_NAVIGATION
        Assert.That(LastSize(emitter), Is.Zero);
#endif

        Transition(emitter, extend);

        var second = emitter.emitCurIG ?? throw new AssertionException("The transition did not create an instruction group.");
        Assert.That(first.igNext, Is.SameAs(second));
        Assert.That(LastGroup(emitter), Is.SameAs(second));
        Assert.That(second.igFlags & InsGroupFlags.Prolog, Is.EqualTo(InsGroupFlags.Prolog));
        Assert.That(second.igFlags & InsGroupFlags.Extend, Is.EqualTo(extend ? InsGroupFlags.Extend : InsGroupFlags.None));
        Assert.That(Buffer(emitter), Is.SameAs(buffer));
        Assert.That(Capacity(emitter), Is.EqualTo(capacity));
        Assert.That(Used(emitter), Is.EqualTo((nuint)0));
        Assert.That(emitter.emitCurIGnonEmpty(), Is.False);
        Assert.That(NextNumber(emitter), Is.EqualTo(2));
    }

#if EMITTER_STATS
    [Test]
    public static void StatisticsCountNativeBufferBytesOnlyOnFirstPreparation()
    {
        var previous = TotalMemory(null);

        try
        {
            TotalMemory(null) = 0;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new Emitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            var group = new insGroup();

            Prepare(emitter, group);
            Assert.That(TotalMemory(null), Is.EqualTo(Capacity(emitter)));

            Prepare(emitter, group);
            Assert.That(TotalMemory(null), Is.EqualTo(Capacity(emitter)));
        }
        finally
        {
            TotalMemory(null) = previous;
        }
    }

    [Test]
    public static void StatisticsCountOnlyExtensionTransitions()
    {
        var previous = TotalExtensions(null);

        try
        {
            TotalExtensions(null) = 0;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var emitter = new Emitter(new CodeGen(compiler));
            emitter.emitBegCG(compiler, default);
            emitter.Init();

            var first = new insGroup { igFlags = InsGroupFlags.Prolog };
            FirstGroup(emitter) = first;
            LastGroup(emitter) = first;
            NextNumber(emitter) = 1;
            Prepare(emitter, first);

            Transition(emitter, true);
            Transition(emitter, false);
            Transition(emitter, true);

            Assert.That(TotalExtensions(null), Is.EqualTo(2u));
        }
        finally
        {
            TotalExtensions(null) = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIGExtend")]
    private static extern ref uint TotalExtensions(Emitter? emitter);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotMemAlloc")]
    private static extern ref nuint TotalMemory(Emitter? emitter);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGenIG")]
    private static extern void Prepare(Emitter emitter, insGroup group);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void Transition(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlast")]
    private static extern ref insGroup? LastGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNxtIGnum")]
    private static extern ref int NextNumber(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_debugInfoSize")]
    private static extern ref int DebugPrefix(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int Count(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CodeSize(Emitter emitter);

#if TARGET_XARCH || EMIT_BACKWARDS_NAVIGATION
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastInsFullSize")]
    private static extern ref int LastSize(Emitter emitter);
#endif
}
#endif
