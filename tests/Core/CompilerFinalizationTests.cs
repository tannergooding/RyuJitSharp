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
using static RyuJitSharp.Compiler.MeasurementData.MethodCounts;
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

    [Test]
    public static void AddCodeDscNumbersAreCompilerLocalAndDebugOnly()
    {
        WithCompiler(compiler => {
            var source = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
            var first = CreateAddCodeDsc(compiler, source, SpecialCodeKind.SCK_FAIL_FAST);
            var second = CreateAddCodeDsc(compiler, source, SpecialCodeKind.SCK_NULL_CHECK);

            if (TestConfiguration.IsDebug)
            {
                Assert.That(second.acdNum, Is.EqualTo(unchecked(first.acdNum + 1u)));
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = first.acdNum);
                _ = Assert.Throws<InvalidOperationException>(() => first.acdNum = 0);
            }
        });
    }

    [Test]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "The test reflects over private Debug-only assertion operand context.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The test reflects over private Debug-only assertion operand context.")]
    public static void AssertionOperandCompilerReferencesAreDebugOnly()
    {
        WithCompiler(compiler => {
            var op1 = new Compiler.AssertionDsc.AssertionDscOp1(compiler, Compiler.optOp1Kind.O1K_VN, 1);
            var op2 = new Compiler.AssertionDsc.AssertionDscOp2(compiler, Compiler.optOp2Kind.O2K_VN_ADD_CNS, ValueNumStore.NoVN);
            var expectedCompiler = TestConfiguration.IsDebug ? compiler : null;

            var op1Compiler = typeof(Compiler.AssertionDsc.AssertionDscOp1).GetField(
                "_compiler", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The AssertionDscOp1 compiler field is missing.");
            var op2Compiler = typeof(Compiler.AssertionDsc.AssertionDscOp2).GetField(
                "_compiler", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The AssertionDscOp2 compiler field is missing.");

            Assert.That(op1Compiler.GetValue(op1), Is.SameAs(expectedCompiler));
            Assert.That(op2Compiler.GetValue(op2), Is.SameAs(expectedCompiler));
        });
    }

    [Test]
    public static void CseHeuristicCounterIsDebugOnly()
    {
        WithCompiler(compiler => {
            var previous = Compiler.DebugData.CseHeuristicData.s_totalCseCount;
            try
            {
                if (TestConfiguration.IsDebug)
                {
                    _ = compiler.optConfigDisableCSE2();
                    Assert.That(Compiler.DebugData.CseHeuristicData.s_totalCseCount, Is.EqualTo(unchecked(previous + 1u)));
                }
                else
                {
                    _ = Assert.Throws<InvalidOperationException>(() => compiler.optConfigDisableCSE2());
                    Assert.That(Compiler.DebugData.CseHeuristicData.s_totalCseCount, Is.EqualTo(previous));
                }
            }
            finally
            {
                Compiler.DebugData.CseHeuristicData.s_totalCseCount = previous;
            }
        });
    }

    [Test]
    public static void CompilerMethodHashIsDebugOnly()
    {
        WithCompiler(compiler => {
            if (TestConfiguration.IsDebug)
            {
                _ = compiler.info.compMethodHash();
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => compiler.info.compMethodHash());
            }
        });
    }

    [Test]
    public static void ImporterInlinedCodeSizeIsDebugOnly()
    {
        WithCompiler(compiler => {
            if (TestConfiguration.IsDebug)
            {
                compiler.impInlinedCodeSize = 11;
                compiler.AddInlinedCodeSize(5);
                Assert.That(compiler.impInlinedCodeSize, Is.EqualTo(16));
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = compiler.impInlinedCodeSize);
                _ = Assert.Throws<InvalidOperationException>(() => compiler.impInlinedCodeSize = 11);
                _ = Assert.Throws<InvalidOperationException>(() => compiler.AddInlinedCodeSize(5));
            }
        });
    }

    [Test]
    public static void TrackedVariableSetIsDebugOnlySidecarState()
    {
        WithCompiler(compiler => {
            if (TestConfiguration.IsDebug)
            {
                compiler.SetLocalTrackedVars([]);
                Assert.That(compiler.GetLocalTrackedVars(), Is.Empty);
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => compiler.GetLocalTrackedVars());
            }
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
            var xmlState = typeof(Compiler.DebugData.InlineXmlDumpData);
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
            var before = genMethodCnt;

            var text = Capture(compiler.compCompileFinish);

            Assert.That(text, Does.StartWith("Final metrics:\nActualCodeBytes"));
            Assert.That(text, Does.Contain("PerfScore" + new string(' ', 33) + ": " + expected + "\n"));
            Assert.That(text, Does.EndWith("\n****** DONE compiling Finalization\n"));
            Assert.That(genMethodCnt, Is.EqualTo(unchecked(before + 1)));
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
            compiler.dspOrder = true;
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
            Compiler.DebugData.EnregisterStatsData.s_enregisterStats.Dump(stats);
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
            TestConfiguration.SetVerbose(compiler, true);
            TestConfiguration.SetVerboseTrees(compiler, true);
            TestConfiguration.SetVerboseSsa(compiler, true);
            compiler.opts.compDbgEnC = true;
            compiler.NeedsGSSecurityCookie = true;
            Assert.That(compiler.verbose, Is.True);
            Assert.That(compiler.verboseTrees, Is.True);
            Assert.That(compiler.verboseSsa, Is.True);
            Assert.That(compiler.compGSSecurityCheckBlocker, Is.EqualTo("incompatible with EnC"));
            var blocker = compiler.compGSSecurityCheckBlocker;
            AllocationStart(compiler) = GC.GetAllocatedBytesForCurrentThread();
            var before = compiler.ManagedBytesAllocated;
            compiler.BeginLoopHoistStats();
            var afterLoopStats = compiler.ManagedBytesAllocated;
            SetGSSecurityCheckBlocker(compiler, blocker);
            var afterGsBlocker = compiler.ManagedBytesAllocated;
            TestConfiguration.SetVerbose(compiler, false);
            TestConfiguration.SetVerboseTrees(compiler, false);
            TestConfiguration.SetVerboseSsa(compiler, false);
            var verbose = compiler.verbose;
            var verboseTrees = compiler.verboseTrees;
            var verboseSsa = compiler.verboseSsa;
            var after = compiler.ManagedBytesAllocated;
            Assert.That(verbose, Is.False);
            Assert.That(verboseTrees, Is.False);
            Assert.That(verboseSsa, Is.False);
            Assert.That(blocker, Is.EqualTo("incompatible with EnC"));
            Assert.That(after, Is.EqualTo(before),
                $"Loop stats: {afterLoopStats - before}, GS blocker: {afterGsBlocker - afterLoopStats}, " +
                $"verbose state: {after - afterGsBlocker}");
        });
    }

    [Test]
    public static void LoopCounterAccumulationPreservesUnsignedWrapAndRatios()
    {
        Assume.That(TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            Compiler.DebugData.LoopHoistStatsData.s_loopsConsidered = int.MaxValue;
            Compiler.DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions = int.MaxValue;
            Compiler.DebugData.LoopHoistStatsData.s_totalHoistedExpressions = int.MaxValue;
            compiler.BeginLoopHoistStats();
            compiler.RecordLoopHoistedExpression();

            AddLoopHoistStats(compiler);

            Assert.That(Compiler.DebugData.LoopHoistStatsData.s_loopsConsidered, Is.EqualTo(int.MinValue));
            using var writer = new StringWriter();
            PrintAggregateLoopHoistStats(writer);
            Assert.That(writer.ToString(), Does.Contain("(100.00%)."));
            Assert.That(writer.ToString(), Does.Contain(" 1.00 per loop-with-hoisted-expr."));
        });
    }

    [Test]
    public static void ReleaseCompilerDoesNotStoreDebugCompilerState()
    {
        Assume.That(!TestConfiguration.IsDebug);
        WithCompiler(compiler => {
            var fields = typeof(Compiler).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_loopsConsidered"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_curLoopHasHoistedExpression"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_loopsWithHoistedExpressions"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("_totalHoistedExpressions"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("verbose"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("verboseTrees"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("verboseSsa"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("compGSSecurityCheckBlocker"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgSafeBasicBlockCreation"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgSafeFlowEdgeCreation"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgBBOrder"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgImplicitByRefLclFldsStale"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgPrintInlinedMethods"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgPgoDeferredInconsistency"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("fgNormalizeEHDone"));
            Assert.That(fields.Select(field => field.Name), Does.Not.Contain("impInlinedCodeSize"));
            var staticFields = typeof(Compiler).GetFields(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(staticFields.Select(field => field.Name), Does.Not.Contain("s_localAddrPropagationRange"));
            Assert.That(compiler.verbose, Is.False);
            Assert.That(compiler.verboseTrees, Is.False);
            Assert.That(compiler.verboseSsa, Is.False);
            Assert.That(compiler.compGSSecurityCheckBlocker, Is.Null);
            Assert.That(compiler.fgPrintInlinedMethods, Is.False);
            Assert.That(compiler.fgNormalizeEHDone, Is.False);
            _ = Assert.Throws<InvalidOperationException>(() => _ = compiler.fgBBOrder);
            _ = Assert.Throws<InvalidOperationException>(() => compiler.fgPrintInlinedMethods = true);
            _ = Assert.Throws<InvalidOperationException>(() => compiler.fgNormalizeEHDone = true);
            _ = Assert.Throws<InvalidOperationException>(() => _ = compiler.impInlinedCodeSize);
        });
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
            Compiler.DebugData.EnregisterStatsData.s_enregisterStats.RecordLocal(
                in descriptor,
                reason,
                AddressExposedReason.NONE);
            using var writer = new StringWriter();
            Compiler.DebugData.EnregisterStatsData.s_enregisterStats.Dump(writer);
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
            Compiler.DebugData.EnregisterStatsData.s_enregisterStats.RecordLocal(
                in descriptor,
                DoNotEnregisterReason.AddrExposed,
                reason);
            using var writer = new StringWriter();
            Compiler.DebugData.EnregisterStatsData.s_enregisterStats.Dump(writer);
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
            Compiler.DebugData.EnregisterStatsData.s_enregisterStats.Dump(locals);
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
            var previousEnregisterStats = default(Compiler.EnregisterStats);
            var previousLoopsConsidered = 0;
            var previousLoopsWithHoistedExpressions = 0;
            var previousTotalHoistedExpressions = 0;
            var previousHeader = false;
            if (isDebug)
            {
                InlineSize(ref JitConfig) = 100;
                InlineDepth(ref JitConfig) = 20;
                ForceDepth(ref JitConfig) = 20;
                compiler._inlineStrategy = new InlineStrategy(compiler);
                compiler.info.compFullName = "Finalization";
                previousEnregisterStats = Compiler.DebugData.EnregisterStatsData.s_enregisterStats;
                Compiler.DebugData.EnregisterStatsData.s_enregisterStats = default;
                previousLoopsConsidered = Compiler.DebugData.LoopHoistStatsData.s_loopsConsidered;
                Compiler.DebugData.LoopHoistStatsData.s_loopsConsidered = 0;
                previousLoopsWithHoistedExpressions = Compiler.DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions;
                Compiler.DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions = 0;
                previousTotalHoistedExpressions = Compiler.DebugData.LoopHoistStatsData.s_totalHoistedExpressions;
                Compiler.DebugData.LoopHoistStatsData.s_totalHoistedExpressions = 0;
                previousHeader = Compiler.DebugData.s_compOrderHeaderPrinted;
                Compiler.DebugData.s_compOrderHeaderPrinted = false;
            }
            try
            {
                action(compiler);
            }
            finally
            {
                if (isDebug)
                {
                    Compiler.DebugData.EnregisterStatsData.s_enregisterStats = previousEnregisterStats;
                    Compiler.DebugData.LoopHoistStatsData.s_loopsConsidered = previousLoopsConsidered;
                    Compiler.DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions = previousLoopsWithHoistedExpressions;
                    Compiler.DebugData.LoopHoistStatsData.s_totalHoistedExpressions = previousTotalHoistedExpressions;
                    Compiler.DebugData.s_compOrderHeaderPrinted = previousHeader;
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

    private static void AddLoopHoistStats(Compiler compiler)
    {
        compiler.AddLoopHoistStats();
    }

    private static void PrintAggregateLoopHoistStats(TextWriter writer)
    {
        Compiler.PrintAggregateLoopHoistStats(writer);
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_compGSSecurityCheckBlocker")]
    private static extern void SetGSSecurityCheckBlocker(Compiler compiler, string? value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitReportMetrics")]
    private static extern ref int ReportMetrics(ref JitConfigValues values);

    private static ref int InlineXml(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitInlineDumpXml;

    private static ref byte* InlineXmlFile(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitInlineDumpXmlFile;

    private static ref int InlineSize(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitInlineSize;

    private static ref int InlineDepth(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitInlineDepth;

    private static ref int ForceDepth(ref JitConfigValues values)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref values)._jitForceInlineDepth;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgCreateAddCodeDsc")]
    private static extern Compiler.AddCodeDsc CreateAddCodeDsc(Compiler compiler, BasicBlock source, SpecialCodeKind kind);
}
