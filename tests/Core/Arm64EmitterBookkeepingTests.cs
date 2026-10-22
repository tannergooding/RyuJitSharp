// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64EmitterBookkeepingTests
{
    [SetUp]
    public static void RequireArm64OrAmd64()
    {
        Assume.That(Configuration.TARGET_ARM64 || Configuration.TARGET_AMD64,
            "This fixture requires ARM64 or AMD64 target semantics.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NestedNoGcRequestsPreserveThePendingGroupBoundary(bool nonempty)
    {
        var (compiler, emitter) = CreateEmitter();
        using var context = new CompilerContext(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        var body = emitter.emitCurIG ?? throw new AssertionException("Missing body group.");
        if (nonempty)
        {
            Descriptor(emitter, body, 4);
        }

        emitter.emitDisableGC();
        var region = emitter.emitCurIG ?? throw new AssertionException("Missing NoGC group.");
        Assert.That(region, nonempty ? Is.Not.SameAs(body) : Is.SameAs(body));
        Assert.That(region.igFlags & InsGroupFlags.NoGCInterrupt, Is.EqualTo(InsGroupFlags.NoGCInterrupt));
        emitter.emitDisableGC();
        emitter.emitEnableGC();
        Assert.That(NoGcRequests(emitter), Is.EqualTo(1));
        Assert.That(ForceNewGroup(emitter), Is.False);
        emitter.emitEnableGC();
        Assert.That(NoGcRequests(emitter), Is.Zero);
        Assert.That(ForceNewGroup(emitter), Is.True);
        Assert.That(emitter.emitLastCodeIsNoGC(), Is.False);
    }

    [Test]
    public static void FrameRangeUsesNativeExclusiveEndAndTargetSpecificDiagnostic()
    {
        var (compiler, emitter) = CreateEmitter();
        emitter.emitBegProlog();
        if (TestConfiguration.IsDebug)
        {
            var previous = Globals.s_jitstdout;
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            try
            {
                Globals.s_jitstdout = writer;
                TestConfiguration.EnableVerbose(compiler);
                emitter.emitSetFrameRangeGCRs(-16, -8);
                var text = Encoding.UTF8.GetString(stream.ToArray());
                if (Configuration.TARGET_AMD64)
                {
                    Assert.That(text, Does.Contain("-0010 ... FFFFFFF8"));
                }
                else
                {
                    Assert.That(text, Does.Contain("-0010 ... -0008"));
                }
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        }
        else
        {
            emitter.emitSetFrameRangeGCRs(-16, -8);
        }

        var predicate = typeof(Emitter).GetMethod("emitIsWithinFrameRangeGCRs")
            ?? throw new AssertionException("Missing native range predicate.");
        Assert.That(predicate.Invoke(emitter, [-16]), Is.EqualTo(true));
        Assert.That(predicate.Invoke(emitter, [-9]), Is.EqualTo(true));
        Assert.That(predicate.Invoke(emitter, [-8]), Is.EqualTo(false));
        emitter.emitEndProlog();
    }

    [Test]
    public static void EpilogCounterHonorsEncoderAvailabilityAndUnsignedBits()
    {
        var (_, emitter) = CreateEmitter();
        if (Configuration.JIT32_GCENCODER)
        {
            EpilogCount(emitter) = -1;
            Assert.That(emitter.emitGetEpilogCnt(), Is.EqualTo(uint.MaxValue));
        }
        else
        {
            var failure = Assert.Throws<FatalJitException>(() => emitter.emitGetEpilogCnt());
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(
                failure?.ErrorCode,
                Is.EqualTo(FatalJitErrorCode.EpilogRecorderIsOnlySupportedByTheX86GCEncoder));
            Assert.That(
                failure?.Message,
                Is.EqualTo(
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "The epilog recorder is only supported by the x86 GC encoder."
                        : "RJS-1331"));
        }
    }

    [TestCase(false, 2)]
    [TestCase(true, 1)]
    public static void NoGcCallbackRetainsUnsignedArgumentsAndEarlyStop(bool skipMain, int expected)
    {
        var (_, emitter) = CreateEmitter();
        var prolog = FirstGroup(emitter) ?? throw new AssertionException("Missing prolog group.");
        var body = emitter.emitCurIG ?? throw new AssertionException("Missing body group.");
        var prologSize = Configuration.TARGET_ARM64 ? 4u : 2u;
        var bodySize = Configuration.TARGET_ARM64 ? 4u : 3u;
        var bodyOffset = Configuration.TARGET_ARM64 ? 8u : 6u;
        Descriptor(emitter, prolog, prologSize);
        Descriptor(emitter, body, bodySize);
        if (Configuration.TARGET_ARM64)
        {
            foreach (var group in new[] { prolog, body })
            {
                var descriptor = group.igData?[0] ?? throw new AssertionException("Missing NoGC descriptor.");
                descriptor.idIns(instruction.INS_nop);
                descriptor.idInsFmt(Emitter.insFormat.IF_ARM64_SN_0A);
            }
        }
        prolog.igFlags |= InsGroupFlags.NoGCInterrupt;
        body.igFlags |= InsGroupFlags.NoGCInterrupt | InsGroupFlags.FuncletProlog;
        prolog.igOffs = 4;
        body.igOffs = bodyOffset;
        var calls = 0;

        var completed = emitter.emitGenNoGCLst((funcIndex, offset, size, firstSize, isFuncletProlog) =>
        {
            calls++;
            Assert.That(funcIndex, Is.Zero);
            var visitingProlog = calls == 1 && !skipMain;
            Assert.That(offset, Is.EqualTo(visitingProlog ? 4u : bodyOffset));
            Assert.That(size, Is.EqualTo(visitingProlog ? prologSize : bodySize));
            Assert.That(firstSize, Is.EqualTo(visitingProlog ? prologSize : bodySize));
            Assert.That(isFuncletProlog, Is.EqualTo(!visitingProlog));
            return calls < expected;
        }, skipMain);

        Assert.That(completed, Is.False);
        Assert.That(calls, Is.EqualTo(expected));
    }

    private static (Compiler Compiler, TestEmitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new TestEmitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false, TestConfiguration.IsDebug);
        return (compiler, emitter);
    }

    private static void Descriptor(TestEmitter emitter, insGroup group, uint size)
    {
        var descriptor = TestEmitter.MakeDescriptor();
        if (Configuration.TARGET_XARCH)
        {
            descriptor.idCodeSize(size);
        }
        group.igData = [descriptor];
        group.igSize = checked((ushort)size);
        group.igInsCnt = 1;
        if (group == emitter.emitCurIG)
        {
            Buffer(emitter).Add(descriptor);
            CurrentCount(emitter) = 1;
            CurrentSize(emitter) = checked((int)size);
            UsedBytes(emitter) = 16;
        }
    }

    private sealed class TestEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc MakeDescriptor()
        {
            return new instrDescBasic();
        }
    }

    private sealed class CompilerContext : IDisposable
    {
        private readonly Compiler? _previous;
        private readonly IDisposable? _scope;

        public CompilerContext(Compiler compiler)
        {
            _scope = TestConfiguration.CreateJitTls();
            _previous = JitTls.Compiler;
            JitTls.Compiler = compiler;
        }

        public void Dispose()
        {
            JitTls.Compiler = _previous;
            _scope?.Dispose();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitEpilogCnt")]
    private static extern ref int EpilogCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc> Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint UsedBytes(Emitter emitter);
}
