// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterMethodInitializationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void StartupCreatesThePrologAndPreparedBodyGroups(bool hasFramePointer)
    {
        var emitter = CreateEmitter();
        emitter.emitCurStackLvl = 32;
        emitter.emitMaxStackDepth = 32;
        emitter.emitCntStackDepth = 0;
        Begin(emitter, hasFramePointer);
        var prolog = FirstGroup(emitter)!;
        var body = emitter.emitCurIG!;

        Assert.That(prolog.GetDisplayId(), Is.EqualTo(1u));
        Assert.That(body.GetDisplayId(), Is.EqualTo(2u));
        Assert.That(prolog.igFlags, Is.EqualTo(InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead));
        Assert.That(body.igFlags, Is.EqualTo(InsGroupFlags.None));
        Assert.That(prolog.igPrev, Is.Null);
        Assert.That(prolog.igNext, Is.SameAs(body));
        Assert.That(body.igPrev, Is.SameAs(prolog));
        Assert.That(body.igNext, Is.Null);
        Assert.That(LastGroup(emitter), Is.SameAs(body));
        Assert.That(prolog.igOffs, Is.Zero);
        Assert.That(body.igOffs, Is.Zero);
        Assert.That(emitter.emitHasFramePtr, Is.EqualTo(hasFramePointer));
        Assert.That(emitter.emitCurStackLvl, Is.Zero);
        Assert.That(emitter.emitMaxStackDepth, Is.Zero);
        Assert.That(emitter.emitCntStackDepth, Is.EqualTo(sizeof(int)));
        Assert.That(body.igStkLvl, Is.Zero);
        Assert.That(LastInstruction(emitter), Is.Null);
#if DEBUG
        Assert.That(emitter.emitChkAlign, Is.True);
        Assert.That(prolog.igWeight, Is.EqualTo(100.0));
        Assert.That(body.igWeight, Is.EqualTo(100.0));
#endif
    }

    [TestCase("emitFwdJumps", true, false)]
    [TestCase("emitNoGCRequestCount", 3, 0)]
    [TestCase("emitNoGCIG", true, false)]
    [TestCase("emitLastSavedIGWasNoGC", true, false)]
    [TestCase("emitForceNewIG", true, false)]
    [TestCase("emitContainsRemovableJmpCandidates", true, false)]
    [TestCase("emitForceStoreGCState", true, false)]
    [TestCase("emitAddedLabel", true, false)]
    [TestCase("emitEpilogSize", 13, 0)]
    [TestCase("emitEpilogCnt", 2, 0)]
    [TestCase("emitExitSeqSize", 9, int.MaxValue)]
    [TestCase("emitCurCodeOffset", 16, 0)]
    [TestCase("emitTotalCodeSize", 32, 0)]
    [TestCase("emitInsCount", 22, 0)]
    [TestCase("emitNxtIGnum", 33, 3)]
    [TestCase("emitGCrFrameOffsMin", -8, 0)]
    [TestCase("emitGCrFrameOffsMax", 88, 0)]
    [TestCase("emitGCrFrameOffsCnt", 17, 0)]
    [TestCase("emitPrevGCrefRegs", SRBM_RAX, SRBM_NONE)]
    [TestCase("emitInitGCrefRegs", SRBM_RAX, SRBM_NONE)]
    [TestCase("emitThisGCrefRegs", SRBM_RAX, SRBM_NONE)]
    [TestCase("emitPrevByrefRegs", SRBM_RCX, SRBM_NONE)]
    [TestCase("emitInitByrefRegs", SRBM_RCX, SRBM_NONE)]
    [TestCase("emitThisByrefRegs", SRBM_RCX, SRBM_NONE)]
    public static void StartupResetsNativeScalarState(string name, object initialValue, object expected)
    {
        var emitter = CreateEmitter();
        var field = typeof(Emitter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(emitter, initialValue);

        Begin(emitter, false);

        Assert.That(field.GetValue(emitter), Is.EqualTo(expected));
    }

    [Test]
    public static void StartupReplacesPriorGroupsAndInvalidatesSavedLocations()
    {
        var emitter = CreateEmitter();
        Begin(emitter, false);
        var oldProlog = FirstGroup(emitter)!;
        PrologEnd(emitter) = new emitLocation(oldProlog);
        ExitStart(emitter) = new emitLocation(oldProlog);
        CodePosition(ref PrologEnd(emitter)) = 25;
        CodePosition(ref ExitStart(emitter)) = 17;
        FirstPlaceholder(emitter) = oldProlog;
        LastPlaceholder(emitter) = oldProlog;
        FirstColdGroup(emitter) = oldProlog;

        emitter.Init();
        Begin(emitter, true);

        Assert.That(FirstGroup(emitter), Is.Not.SameAs(oldProlog));
        Assert.That(FirstGroup(emitter)!.GetDisplayId(), Is.EqualTo(1u));
        Assert.That(PrologEnd(emitter).Valid(), Is.False);
        Assert.That(PrologEnd(emitter).IsOffsetZero(), Is.True);
        Assert.That(ExitStart(emitter).Valid(), Is.False);
        Assert.That(ExitStart(emitter).IsOffsetZero(), Is.True);
        Assert.That(FirstPlaceholder(emitter), Is.Null);
        Assert.That(LastPlaceholder(emitter), Is.Null);
        Assert.That(FirstColdGroup(emitter), Is.Null);
    }

    [Test]
    public static void LocationInitializationRestoresTheNativeDefault()
    {
        var group = new insGroup();
        var location = new emitLocation(group);
        CodePosition(ref location) = 42;
        Assert.That(location.Valid(), Is.True);
        Assert.That(location.GetIG(), Is.SameAs(group));
        Assert.That(location.IsOffsetZero(), Is.False);

        location.Init();

        Assert.That(location.Valid(), Is.False);
        Assert.That(location.GetIG(), Is.Null);
        Assert.That(location.IsOffsetZero(), Is.True);
    }

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();

        return emitter;
    }

    private static void Begin(Emitter emitter, bool hasFramePointer)
    {
        emitter.emitBegFN(hasFramePointer
#if DEBUG
            , true
#endif
            );
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlast")]
    private static extern ref insGroup? LastGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPrologEndPos")]
    private static extern ref emitLocation PrologEnd(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitExitSeqBegLoc")]
    private static extern ref emitLocation ExitStart(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "codePos")]
    private static extern ref uint CodePosition(ref emitLocation location);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderList")]
    private static extern ref insGroup? FirstPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderLast")]
    private static extern ref insGroup? LastPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFirstColdIG")]
    private static extern ref insGroup? FirstColdGroup(Emitter emitter);
}
