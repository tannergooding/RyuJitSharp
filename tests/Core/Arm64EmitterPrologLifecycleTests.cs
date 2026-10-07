// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.insGroupPlaceholderType;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64EmitterPrologLifecycleTests
{
    [Test]
    public static void BeginningAndEndingThePrologReusesThePreallocatedGroup()
    {
        var (compiler, emitter) = CreateEmitter();
        var prolog = FirstGroup(emitter) ?? throw new AssertionException("Missing prolog group.");
        Assert.That(emitter.emitCurIG, Is.Not.SameAs(prolog));

        emitter.emitBegProlog();
        Assert.That(emitter.emitCurIG, Is.SameAs(prolog));
        Assert.That(NoGcRequests(emitter), Is.EqualTo(1));
        Assert.That(VarSetOps.IsEmpty(compiler, emitter.InitGCrefVars), Is.True);
        emitter.emitEndProlog();

        Assert.That(prolog.igInsCnt, Is.Zero);
        Assert.That(NoGcRequests(emitter), Is.Zero);
    }

    [TestCase(IGPT_PROLOG)]
    [TestCase(IGPT_FUNCLET_PROLOG)]
    [TestCase(IGPT_EPILOG)]
    public static void ReservingAndGeneratingPlaceholdersPreservesTheirLifecycle(insGroupPlaceholderType kind)
    {
        var (compiler, emitter) = CreateEmitter();
        compiler.fgFuncletsCreated = true;
        compiler.compFuncInfos = [new() { funKind = FuncKind.FUNC_ROOT }];
        compiler.compFuncInfoCount = 1;
        var block = new BasicBlock(null, null);
        var vars = VarSetOps.MakeEmpty(compiler);

        emitter.emitCreatePlaceholderIG(kind, block, vars, default, default, last: true);
        var placeholder = LastPlaceholder(emitter) ?? throw new AssertionException("Missing placeholder group.");
        Assert.That(placeholder.igFlags & InsGroupFlags.Placeholder, Is.EqualTo(InsGroupFlags.Placeholder));
        Assert.That(placeholder.igSize, Is.EqualTo(0));
        Assert.That(CodeOffset(emitter), Is.EqualTo(256));

        if (kind == IGPT_PROLOG)
        {
            emitter.emitGeneratePrologEpilog();
            Assert.That(placeholder.igPhData, Is.Not.Null);
            return;
        }

        if (kind == IGPT_FUNCLET_PROLOG)
        {
            emitter.emitBegFuncletProlog(placeholder);
            emitter.emitEndFuncletProlog();
        }
        else
        {
            emitter.emitBegFnEpilog(placeholder);
            emitter.emitEndFnEpilog();
        }

        Assert.That(placeholder.igPhData, Is.Null);
        Assert.That(placeholder.igFlags & InsGroupFlags.Placeholder, Is.EqualTo(InsGroupFlags.None));
        Assert.That(compiler.compCurBB, Is.SameAs(block));
        Assert.That(NoGcRequests(emitter), Is.Zero);
    }

#if EMITTER_STATS
    [Test]
    public static void EachReservationIncrementsTheNativePlaceholderCounter()
    {
        var counter = typeof(Emitter).GetField("emitTotalPhIGcnt", BindingFlags.NonPublic | BindingFlags.Static);
        var previous = counter?.GetValue(null);

        try
        {
            var (compiler, emitter) = CreateEmitter();
            var vars = VarSetOps.MakeEmpty(compiler);
            var block = new BasicBlock(null, null);
            emitter.emitCreatePlaceholderIG(IGPT_FUNCLET_PROLOG, block, vars, default, default, last: false);
            var countField = counter ?? throw new AssertionException("Missing native placeholder counter.");
            var firstCount = (uint)(countField.GetValue(null) ?? throw new AssertionException("Missing placeholder count."));
            emitter.emitCreatePlaceholderIG(IGPT_FUNCLET_EPILOG, block, vars, default, default, last: true);
            Assert.That(countField.GetValue(null), Is.EqualTo(unchecked(firstCount + 1)));
        }
        finally
        {
            counter?.SetValue(null, previous);
        }
    }
#endif

    private static (Compiler Compiler, Emitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new Emitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
        return (compiler, emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderLast")]
    private static extern ref insGroup? LastPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurCodeOffset")]
    private static extern ref int CodeOffset(Emitter emitter);
}
#endif
