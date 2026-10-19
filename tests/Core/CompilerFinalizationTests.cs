// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerFinalizationTests
{
    private static readonly Dictionary<string, byte[]> s_metadata = [];

    [TestCase(false)]
    [TestCase(true)]
    public static void FinalizationReportsMetricsOnlyWhenRequested(bool enabled)
    {
        WithCompiler(compiler => {
            ReportMetrics(ref JitConfig) = enabled ? 1 : 0;
            compiler.Metrics.ActualCodeBytes = 43;
            compiler.Metrics.PerfScore = 12.25;
            compiler.Metrics.BytesAllocated = 91;
            var start = GC.GetAllocatedBytesForCurrentThread();
            AllocationStart(compiler) = start;
            var allocation = new byte[4096];

            var text = Capture(compiler.compCompileFinish);
            GC.KeepAlive(allocation);
            Assert.That(text, Is.Empty);
            if (enabled)
            {
                Assert.That(s_metadata.Keys, Is.EqualTo(typeof(JitMetrics).GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .OrderBy(field => field.MetadataToken).Select(field => field.Name)));
                Assert.That(BitConverter.ToInt32(s_metadata[nameof(JitMetrics.ActualCodeBytes)]), Is.EqualTo(43));
                Assert.That(BitConverter.ToDouble(s_metadata[nameof(JitMetrics.PerfScore)]), Is.EqualTo(12.25));
                Assert.That(BitConverter.ToInt64(s_metadata[nameof(JitMetrics.BytesAllocated)]),
                    Is.InRange(4096, GC.GetAllocatedBytesForCurrentThread() - start));
                Assert.That(s_metadata.Values.All(bytes => bytes.Length is 4 or 8), Is.True);
            }
            else
            {
                Assert.That(s_metadata, Is.Empty);
                Assert.That(compiler.Metrics.BytesAllocated, Is.EqualTo(91));
            }
        });
    }

    [Test]
    public static void PublicFinalizationEmitsCompletionMarker()
    {
        Assume.That(TestConfiguration.IsDebug);
        SsaLivenessTests.WithCompiler(0, compiler => {
            TestConfiguration.EnableVerbose(compiler);
            compiler.info.compFullName = "Finalization";
            compiler._inlineStrategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));

            Assert.That(Capture(compiler.compCompileFinish), Does.EndWith("\n****** DONE compiling Finalization\n"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "The test intentionally reflects over private Debug-only inline XML dump state.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The test intentionally reflects over private Debug-only inline XML dump state.")]
    public static void InlineXmlAppendsOrFallsBackToStdout(bool append)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            var path = append ? Path.GetTempFileName() : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "inlines.xml");
            var xmlState = typeof(InlineStrategy).GetNestedType("XmlDumpState", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The inline XML dump state is missing.");
            var header = xmlState.GetField("HasDumpedXmlHeader", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The inline XML header state is missing.");
            var previous = header.GetValue(null);
            header.SetValue(null, false);
            try
            {
                if (append)
                {
                    File.WriteAllText(path, "existing\n");
                }

                TestConfiguration.EnableVerbose(compiler);
                compiler.info.compMethodInfo->ILCodeSize = 10;
                InlineXml(ref JitConfig) = 1;
                fixed (byte* file = Encoding.UTF8.GetBytes(path + "\0"))
                {
                    InlineXmlFile(ref JitConfig) = file;
                    var output = Capture(compiler.compCompileFinish);
                    if (append)
                    {
                        Assert.That(File.ReadAllText(path), Does.StartWith("existing\n<?xml version=\"1.0\"?>"));
                        Assert.That(output, Does.Not.Contain("<InlineForest>"));
                    }
                    else
                    {
                        Assert.That(output, Does.StartWith("<?xml version=\"1.0\"?>\n<InlineForest>\n"));
                        Assert.That(File.Exists(path), Is.False);
                    }
                    Assert.That(output, Does.EndWith("\n****** DONE compiling Finalization\n"));
                }
            }
            finally
            {
                header.SetValue(null, previous);
                if (append)
                {
                    File.Delete(path);
                }
            }
        });
    }

    [TestCase(18.625, "18.625000")]
    [TestCase(0.0, "0.000000")]
    [TestCase(-0.0, "-0.000000")]
    [TestCase(double.PositiveInfinity, "inf")]
    public static void VerboseCompletionUsesNativeMetricFormatting(double score, string expected)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            TestConfiguration.EnableVerbose(compiler);
            compiler.Metrics.PerfScore = score;
            var before = Compiler.genMethodCnt;

            var text = Capture(compiler.compCompileFinish);

            Assert.That(text, Does.StartWith("Final metrics:\nActualCodeBytes"));
            Assert.That(text, Does.Contain("PerfScore" + new string(' ', 33) + ": " + expected + "\n"));
            Assert.That(text, Does.EndWith("\n****** DONE compiling Finalization\n"));
            Assert.That(Compiler.genMethodCnt, Is.EqualTo(unchecked(before + 1)));
            Assert.That(s_metadata, Is.Empty);
        });
    }

    [TestCase(999.0, " 999 | ")]
    [TestCase(1000.0, "  1K | ")]
    [TestCase(999999.0, "1000K | ")]
    [TestCase(1000000.0, "  1M | ")]
    public static void OrderedSummaryUsesNativeProfileBucketsAndSingleHeader(double count, string expected)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            compiler.opts.dspOrder = true;
            compiler.fgPgoHaveWeights = true;
            compiler.fgCalledCount = count;
            compiler.Metrics.PerfScore = 9999.995;
            compiler.info.compMethodInfo->ILCodeSize = 17;
            compiler.info.compTotalHotCodeSize = 123;
            compiler.info.compTotalColdCodeSize = 7;

            var first = Capture(compiler.compCompileFinish);
            var second = Capture(compiler.compCompileFinish);

            Assert.That(first, Does.StartWith("         |  Profiled   |"));
            Assert.That(first, Does.Contain("06001234 | " + expected));
            Assert.That(second, Does.StartWith("06001234 | " + expected));
            Assert.That(second, Does.Contain("   10000 |   17 |   123 |   7 | CORINFO_HELP_THROW\n"));
            Assert.That(second, Does.Not.Contain("mdToken"));
        });
    }

    [Test]
    public static void CompletionAggregatesOnlyReferencedLocalsAndAllLoopCounters()
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            compiler.lvaTable[1].setLvRefCnt(0);
            compiler.BeginLoopHoistStats();
            compiler.RecordLoopHoistedExpression();
            compiler.RecordLoopHoistedExpression();
            compiler.BeginLoopHoistStats();
            compiler.RecordLoopHoistedExpression();
            compiler.BeginLoopHoistStats();
            compiler.BeginLoopHoistStats();

            compiler.compCompileFinish();

            using var stats = new StringWriter();
            DumpEnregisterStats(stats);
            Assert.That(stats.ToString(), Is.EqualTo(
                "\nLocals enregistration statistics:\n" +
                "total number of locals: 2, number of enregistered: 2, notEnreg: 0, ratio: 1.00\n" +
                "All locals are enregistered.\n"));
            using var loops = new StringWriter();
            PrintAggregateLoopHoistStats(loops);
            Assert.That(loops.ToString(), Does.Contain("Considered 4 loops.  Of these, we hoisted expressions out of 2 ( 50.00%).\n"));
            Assert.That(loops.ToString(), Does.EndWith("  A total of 3 expressions were hoisted, an average of  1.50 per loop-with-hoisted-expr.\n"));
        });
    }

    [Test]
    public static void LoopHoistCountersReuseExistingDebugSidecarWithoutAllocation()
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            compiler.SetLocalTrackedVars([]);
            AllocationStart(compiler) = GC.GetAllocatedBytesForCurrentThread();
            var before = compiler.ManagedBytesAllocated;
            compiler.BeginLoopHoistStats();
            Assert.That(compiler.ManagedBytesAllocated, Is.EqualTo(before));
        });
    }

    [Test]
    public static void LoopCounterAccumulationPreservesUnsignedWrapAndRatios()
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            SetStaticLoopCounter("s_loopsConsidered", int.MaxValue);
            SetStaticLoopCounter("s_loopsWithHoistedExpressions", int.MaxValue);
            SetStaticLoopCounter("s_totalHoistedExpressions", int.MaxValue);
            compiler.BeginLoopHoistStats();
            compiler.RecordLoopHoistedExpression();

            AddLoopHoistStats(compiler);

            Assert.That(GetStaticLoopCounter("s_loopsConsidered"), Is.EqualTo(int.MinValue));
            using var writer = new StringWriter();
            PrintAggregateLoopHoistStats(writer);
            Assert.That(writer.ToString(), Does.Contain("(100.00%)."));
            Assert.That(writer.ToString(), Does.Contain(" 1.00 per loop-with-hoisted-expr."));
        });
    }

    [Test]
    public static void ReleaseCompilerDoesNotStoreDebugLoopHoistCounters()
    {
        Assume.That(!TestConfiguration.IsDebug);
        var fields = typeof(Compiler).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_loopsConsidered"));
        Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_curLoopHasHoistedExpression"));
        Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_loopsWithHoistedExpressions"));
        Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_totalHoistedExpressions"));
    }

    [TestCase(DoNotEnregisterReason.HiddenBufferStructArg, "m_hiddenStructArg")]
    [TestCase(DoNotEnregisterReason.DontEnregStructs, "m_dontEnregStructs")]
    [TestCase(DoNotEnregisterReason.NotRegSizeStruct, "m_notRegSizeStruct")]
    [TestCase(DoNotEnregisterReason.LocalField, "m_localField")]
    [TestCase(DoNotEnregisterReason.VMNeedsStackAddr, "m_VMNeedsStackAddr")]
    [TestCase(DoNotEnregisterReason.LiveInOutOfHandler, "m_liveInOutHndlr")]
    [TestCase(DoNotEnregisterReason.BlockOp, "m_blockOp")]
    [TestCase(DoNotEnregisterReason.IsStructArg, "m_structArg")]
    [TestCase(DoNotEnregisterReason.DepField, "m_depField")]
    [TestCase(DoNotEnregisterReason.NoRegVars, "m_noRegVars")]
    [TestCase(DoNotEnregisterReason.PinningRef, "m_PinningRef")]
    [TestCase(DoNotEnregisterReason.LclAddrNode, "m_lclAddrNode")]
    [TestCase(DoNotEnregisterReason.CastTakesAddr, "m_castTakesAddr")]
    [TestCase(DoNotEnregisterReason.StoreBlkSrc, "m_storeBlkSrc")]
    [TestCase(DoNotEnregisterReason.SwizzleArg, "m_swizzleArg")]
    [TestCase(DoNotEnregisterReason.BlockOpRet, "m_blockOpRet")]
    [TestCase(DoNotEnregisterReason.ReturnSpCheck, "m_returnSpCheck")]
    [TestCase(DoNotEnregisterReason.CallSpCheck, "m_callSpCheck")]
    [TestCase(DoNotEnregisterReason.simdUserForcesDep, "m_simdUserForcesDep")]
    [TestCase(DoNotEnregisterReason.WasmGCVisibility, "m_wasmGcVisibility")]
    public static void EnregistrationReasonCountersHaveNativeNames(DoNotEnregisterReason reason, string expected)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            var descriptor = new LclVarDsc { Type = TYP_INT, lvDoNotEnregister = true };
            RecordEnregisterLocal(in descriptor, reason, AddressExposedReason.NONE);
            using var writer = new StringWriter();
            DumpEnregisterStats(writer);
            Assert.That(writer.ToString(), Does.Contain(expected + " 1, ratio: 1.00\n"));
            Assert.That(writer.ToString(), Does.EndWith("\nAddr exposed details:\n\nNo address exposed locals to report.\n"));
        });
    }

    [TestCase(AddressExposedReason.PARENT_EXPOSED, "m_parentExposed")]
    [TestCase(AddressExposedReason.TOO_CONSERVATIVE, "m_tooConservative")]
    [TestCase(AddressExposedReason.ESCAPE_ADDRESS, "m_escapeAddress")]
    [TestCase(AddressExposedReason.WIDE_INDIR, "m_wideIndir")]
    [TestCase(AddressExposedReason.OSR_EXPOSED, "m_osrExposed")]
    [TestCase(AddressExposedReason.STRESS_LCL_FLD, "m_stressLclFld")]
    [TestCase(AddressExposedReason.DISPATCH_RET_BUF, "m_dispatchRetBuf")]
    [TestCase(AddressExposedReason.STRESS_POISON_IMPLICIT_BYREFS, "m_stressPoisonImplicitByrefs")]
    [TestCase(AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY, "m_externallyVisibleImplicitly")]
    [TestCase(AddressExposedReason.SMALL_TYPE_PARTIAL_DEF, "m_smallTypePartialDef")]
    public static void AddressExposureCountersIncludeClearedExposure(AddressExposedReason reason, string expected)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            var descriptor = new LclVarDsc { Type = TYP_STRUCT, lvDoNotEnregister = true };
            RecordEnregisterLocal(in descriptor, DoNotEnregisterReason.AddrExposed, reason);
            using var writer = new StringWriter();
            DumpEnregisterStats(writer);
            Assert.That(writer.ToString(), Does.Contain("total number of struct locals: 1, number of enregistered: 0, notEnreg: 1, ratio: 0.00\n"));
            Assert.That(writer.ToString(), Does.Contain("m_addrExposed 1, ratio: 1.00\n"));
            Assert.That(writer.ToString(), Does.EndWith(expected + " 1, ratio: 1.00\n"));
        });
    }

    [Test]
    public static void EmptyAggregatesAvoidDividingByZero()
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            using var locals = new StringWriter();
            DumpEnregisterStats(locals);
            Assert.That(locals.ToString(), Is.EqualTo("\nLocals enregistration statistics:\nNo locals to report.\n"));
            using var loops = new StringWriter();
            PrintAggregateLoopHoistStats(loops);
            Assert.That(loops.ToString(), Does.Contain("(  0.00%)."));
            Assert.That(loops.ToString(), Does.EndWith(" 0.00 per loop-with-hoisted-expr.\n"));
        });
    }
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "The test resets a Debug-only statistics object through its private runtime type.")]
    private static void WithCompiler(Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(3, compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> table = default;
            table.Base.Base.reportMetadata = &ReportMetadata;
            table.Base.Base.getMethodDefFromMethod = &MethodToken;
            ICorJitInfo jitInfo = new() { lpVtbl = &table };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMethodHnd = Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_THROW);
            s_metadata.Clear();
            var isDebug = TestConfiguration.IsDebug;
            FieldInfo? enregisterStats = null;
            object? previousLocals = null;
            FieldInfo? loopsConsidered = null;
            object? previousLoopsConsidered = null;
            FieldInfo? loopsWithHoistedExpressions = null;
            object? previousLoopsWithHoistedExpressions = null;
            FieldInfo? totalHoistedExpressions = null;
            object? previousTotalHoistedExpressions = null;
            FieldInfo? headerField = null;
            object? previousHeader = null;
            if (isDebug)
            {
                InlineSize(ref JitConfig) = 100;
                InlineDepth(ref JitConfig) = 20;
                ForceDepth(ref JitConfig) = 20;
                compiler._inlineStrategy = new InlineStrategy(compiler);
                compiler.info.compFullName = "Finalization";
                enregisterStats = GetEnregisterStatsField();
                previousLocals = enregisterStats.GetValue(null);
                enregisterStats.SetValue(null, Activator.CreateInstance(enregisterStats.FieldType));
                loopsConsidered = GetCompilerField("s_loopsConsidered", isStatic: true);
                previousLoopsConsidered = loopsConsidered.GetValue(null);
                loopsConsidered.SetValue(null, 0);
                loopsWithHoistedExpressions = GetCompilerField("s_loopsWithHoistedExpressions", isStatic: true);
                previousLoopsWithHoistedExpressions = loopsWithHoistedExpressions.GetValue(null);
                loopsWithHoistedExpressions.SetValue(null, 0);
                totalHoistedExpressions = GetCompilerField("s_totalHoistedExpressions", isStatic: true);
                previousTotalHoistedExpressions = totalHoistedExpressions.GetValue(null);
                totalHoistedExpressions.SetValue(null, 0);
                headerField = typeof(Compiler).GetField("s_compOrderHeaderPrinted",
                    BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The ordered-summary header state is missing.");
                previousHeader = headerField.GetValue(null);
                headerField.SetValue(null, false);
            }
            try
            {
                action(compiler);
            }
            finally
            {
                if (isDebug)
                {
                    enregisterStats!.SetValue(null, previousLocals);
                    loopsConsidered!.SetValue(null, previousLoopsConsidered);
                    loopsWithHoistedExpressions!.SetValue(null, previousLoopsWithHoistedExpressions);
                    totalHoistedExpressions!.SetValue(null, previousTotalHoistedExpressions);
                    headerField!.SetValue(null, previousHeader);
                }
            }
        });
    }

    private static string Capture(Action action)
    {
        var previous = s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    private static FieldInfo GetCompilerField(string name, bool isStatic)
    {
        var flags = isStatic ? BindingFlags.Static : BindingFlags.Instance;
        return typeof(Compiler).GetField(name, flags | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"The compiler field {name} is missing.");
    }

    private static FieldInfo GetEnregisterStatsField()
    {
        return GetCompilerField("s_enregisterStats", isStatic: true);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The test intentionally reflects over Debug-only statistics methods retained in the test assembly.")]
    private static void RecordEnregisterLocal(
        in LclVarDsc descriptor,
        DoNotEnregisterReason doNotEnregisterReason,
        AddressExposedReason addressExposedReason)
    {
        var field = GetEnregisterStatsField();
        var stats = field.GetValue(null) ?? throw new InvalidOperationException("Missing enregistration statistics.");
        var method = stats.GetType().GetMethod("RecordLocal", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("The enregistration RecordLocal method is missing.");
        _ = method.Invoke(stats, [descriptor, doNotEnregisterReason, addressExposedReason]);
        field.SetValue(null, stats);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The test intentionally reflects over Debug-only statistics methods retained in the test assembly.")]
    private static void DumpEnregisterStats(TextWriter writer)
    {
        var stats = GetEnregisterStatsField().GetValue(null)
            ?? throw new InvalidOperationException("Missing enregistration statistics.");
        var method = stats.GetType().GetMethod("Dump", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("The enregistration Dump method is missing.");
        _ = method.Invoke(stats, [writer]);
    }

    private static void SetStaticLoopCounter(string name, int value)
    {
        GetCompilerField(name, isStatic: true).SetValue(null, value);
    }

    private static int GetStaticLoopCounter(string name)
    {
        return (int)(GetCompilerField(name, isStatic: true).GetValue(null)
            ?? throw new InvalidOperationException($"The compiler field {name} is uninitialized."));
    }

    private static void AddLoopHoistStats(Compiler compiler)
    {
        var method = typeof(Compiler).GetMethod("AddLoopHoistStats", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The loop-hoist statistics method is missing.");
        _ = method.Invoke(compiler, null);
    }

    private static void PrintAggregateLoopHoistStats(TextWriter writer)
    {
        var method = typeof(Compiler).GetMethod("PrintAggregateLoopHoistStats",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The aggregate loop-hoist statistics method is missing.");
        _ = method.Invoke(null, [writer]);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ReportMetadata(ICorJitInfo* info, byte* key, void* value, nint length)
    {
        var name = Marshal.PtrToStringUTF8((nint)key);
        if ((name is not null) && (length is 4 or 8))
        {
            s_metadata.Add(name, new ReadOnlySpan<byte>(value, (int)length).ToArray());
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int MethodToken(ICorJitInfo* info, CORINFO_METHOD_STRUCT_* method) => 0x06001234;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocatedBytesAtStart")]
    private static extern ref long AllocationStart(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitReportMetrics")]
    private static extern ref int ReportMetrics(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineDumpXml")]
    private static extern ref int InlineXml(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineDumpXmlFile")]
    private static extern ref byte* InlineXmlFile(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineSize")]
    private static extern ref int InlineSize(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineDepth")]
    private static extern ref int InlineDepth(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitForceInlineDepth")]
    private static extern ref int ForceDepth(ref JitConfigValues values);
}
