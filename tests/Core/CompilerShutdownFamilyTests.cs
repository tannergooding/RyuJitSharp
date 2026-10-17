// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
#if COUNT_AST_OPERS
using static RyuJitSharp.genTreeOps;
#endif
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerShutdownFamilyTests
{
#if COUNT_AST_OPERS
    [Test]
    public static void OperatorCountStorageIncludesSentinelAndSupportsWrites()
    {
        var previousCount = GenTree.GetNodeCount((uint)GT_ADD);
        var updatedCount = unchecked(previousCount + 1);

        try
        {
            GenTree.GetNodeCount((uint)GT_ADD) = updatedCount;
            Assert.That(GenTree.GetNodeCount((uint)GT_ADD), Is.EqualTo(updatedCount));
        }
        finally
        {
            GenTree.GetNodeCount((uint)GT_ADD) = previousCount;
        }

        Assert.That(GenTree.GetNodeCount((uint)GT_COUNT), Is.Zero);
    }

#if MEASURE_BLOCK_SIZE
    [Test]
    public static void FlowNodeStatisticsStorageSupportsReadsAndWrites()
    {
        var previousCount = genFlowNodeCnt;
        var previousSize = genFlowNodeSize;
        var updatedCount = unchecked(previousCount + 1);
        var updatedSize = unchecked(previousSize + 1);

        try
        {
            genFlowNodeCnt = updatedCount;
            genFlowNodeSize = updatedSize;

            Assert.That(genFlowNodeCnt, Is.EqualTo(updatedCount));
            Assert.That(genFlowNodeSize, Is.EqualTo(updatedSize));
        }
        finally
        {
            genFlowNodeCnt = previousCount;
            genFlowNodeSize = previousSize;
        }
    }
#endif

    [Test]
    public static void MeasurementStructureNamesRemainAvailableWithoutDebug()
    {
        if (!Configuration.DEBUG && !Configuration.MEASURE_NODE_SIZE)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _ = genTreeOps.GT_ADD.StructName);
            Assert.That(exception?.Message,
                Is.EqualTo("Operator struct type metadata is only available in Debug or node-size measurement builds."));
            return;
        }

        Assert.That(genTreeOps.GT_ADD.StructName, Is.EqualTo(nameof(GenTreeOp)));
        Assert.That(genTreeOps.GT_CALL.StructName, Is.EqualTo(nameof(GenTreeCall)));
    }

    [Test]
    public static void DisabledStatisticsPreserveCleanupAndTimingShutdown()
    {
        if (Configuration.OPTIONAL_STATISTICS)
        {
            Assert.Ignore("Optional statistics are enabled for this build.");
        }

        WithShutdownState(() => {
            AltAssemblies(null) = (AssemblyNamesList2)RuntimeHelpers.GetUninitializedObject(typeof(AssemblyNamesList2));
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvOutput = new MemoryStream();
            using var csv = new StreamWriter(csvOutput, leaveOpen: true);
            s_jitstdout = writer;
            CsvFile(null) = csv;
            Compiler.s_dspMemStats = false;

            Compiler.compShutdown();
            writer.Flush();

            Assert.That(AltAssemblies(null), Is.Null);
            Assert.That(CsvFile(null), Is.Null);
            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Does.Not.Contain("GenTree operator counts"));
            Assert.That(text, Does.Not.Contain("All allocations:"));
            Assert.That(text, Does.Not.Contain("Largest method:"));
            Assert.That(text, Does.Not.Contain("GC pointer table stats"));
        });
    }

    [Test]
    public static void ZeroMethodGuardPrecedesEveryOptionalStatisticsDependency()
    {
        if (!Configuration.DEBUG &&
            !Configuration.MEASURE_NODE_SIZE &&
            !Configuration.MEASURE_BLOCK_SIZE &&
            !Configuration.DISPLAY_SIZES &&
            !Configuration.CALL_ARG_STATS)
        {
            Assert.Ignore("No zero-method statistics mode is enabled for this build.");
        }

        WithShutdownState(() => {
            Compiler.genMethodCnt = 0;
            AltAssemblies(null) = (AssemblyNamesList2)RuntimeHelpers.GetUninitializedObject(typeof(AssemblyNamesList2));
#if DEBUG
            DumpAssemblies(null) = (AssemblyNamesList2)RuntimeHelpers.GetUninitializedObject(typeof(AssemblyNamesList2));
#endif
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvOutput = new MemoryStream();
            using var csv = new StreamWriter(csvOutput, leaveOpen: true);
            s_jitstdout = writer;
            CsvFile(null) = csv;

            Compiler.compShutdown();
            writer.Flush();

            Assert.That(AltAssemblies(null), Is.Null);
#if DEBUG
            Assert.That(DumpAssemblies(null), Is.Null);
#endif
            Assert.That(CsvFile(null), Is.SameAs(csv));
            Assert.That(csvOutput.CanWrite, Is.True);
            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.Empty);
        });
    }

    [Test]
    public static void ShutdownPreservesStatisticsCallOrderAndUnportedDependencies()
    {
        if (!Configuration.OPTIONAL_STATISTICS)
        {
            Assert.Ignore("Optional statistics are disabled for this build.");
        }

        if (Configuration.MEASURE_NODE_SIZE &&
            (Configuration.NODEBASH_STATS ||
                Configuration.COUNT_AST_OPERS ||
                Configuration.CALL_ARG_STATS ||
                Configuration.MEASURE_BLOCK_SIZE ||
                Configuration.MEASURE_PTRTAB_SIZE ||
                Configuration.EMITTER_STATS ||
                Configuration.DISPLAY_SIZES))
        {
            Assert.Ignore("The shutdown statistics test covers one optional statistics mode at a time.");
        }

        WithShutdownState(() => {
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvOutput = new MemoryStream();
            using var csv = new StreamWriter(csvOutput, leaveOpen: true);
            s_jitstdout = writer;
            CsvFile(null) = csv;
            Compiler.s_dspMemStats = JitConfig.DisplayMemStats != 0;
            FatalJitException? exception = null;
#if MEASURE_BLOCK_SIZE
            BasicBlock.s_Count = 9;
            BasicBlock.s_Size = -1;
            Compiler.genMethodCnt = 3;
#endif
#if NODEBASH_STATS && !COUNT_AST_OPERS && !CALL_ARG_STATS && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
            Compiler.compShutdown();
#elif EMITTER_STATS && (TARGET_XARCH || TARGET_ARM64) && !NODEBASH_STATS && !COUNT_AST_OPERS && !CALL_ARG_STATS && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE
            Compiler.compShutdown();
#elif CALL_ARG_STATS && !NODEBASH_STATS && !COUNT_AST_OPERS && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
            Compiler.argTotalCalls = 0;
            Compiler.compShutdown();
#elif COUNT_AST_OPERS && !NODEBASH_STATS && !CALL_ARG_STATS && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
            var previousCount = GenTree.GetNodeCount((uint)GT_ADD);
            try
            {
                GenTree.GetNodeCount((uint)GT_ADD) = unchecked(previousCount + 1);
                exception = Assert.Throws<FatalJitException>(Compiler.compShutdown);
                Assert.That(exception?.Message,
                    Is.EqualTo("GenTree::s_gtTrueSizes native logical sizes are not ported."));
            }
            finally
            {
                GenTree.GetNodeCount((uint)GT_ADD) = previousCount;
            }
#elif MEASURE_BLOCK_SIZE && !NODEBASH_STATS && !COUNT_AST_OPERS && !CALL_ARG_STATS && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
            Compiler.compShutdown();
#else
            exception = Assert.Throws<FatalJitException>(Compiler.compShutdown);
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#endif
            writer.Flush();
            var text = Encoding.UTF8.GetString(output.ToArray());

#if NODEBASH_STATS
            Assert.That(text, Does.Contain("Bashed gtOper stats:\n"));
            Assert.That(text, Does.Contain("Total bashings: 0\n"));
            Assert.That(csvOutput.CanWrite, Is.True);
#endif
#if !NODEBASH_STATS
            Assert.That(CsvFile(null), Is.Null);
#endif
            if (Configuration.MEASURE_NODE_SIZE)
            {
                Assert.That(exception?.Message, Is.EqualTo("genNodeSizeStats collection is not ported."));
                Assert.That(text, Does.EndWith(
                    "\n---------------------------------------------------\n" +
                    "GenTree node allocation stats\n" +
                    "---------------------------------------------------\n"));
            }
            else
            {
#if COUNT_AST_OPERS
                Assert.That(text, Does.Not.Contain("GenTree operator counts"));
#elif CALL_ARG_STATS && !NODEBASH_STATS && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
                Assert.That(text, Does.Not.Contain("Call stats"));
                Assert.That(text, Does.Not.Contain("Basic block count frequency table"));
#elif MEASURE_BLOCK_SIZE
                var average = unchecked((uint)(nuint.MaxValue / 3));
                Assert.That(text, Does.EndWith(
                    "\n---------------------------------------------------\n" +
                    "BasicBlock and FlowEdge/BasicBlockList allocation stats\n" +
                    "---------------------------------------------------\n" +
                    $"Allocated      9 basic blocks (4294967295 bytes total, avg {average,4} bytes per method)\n" +
                    "Allocated      0 flow nodes (      0 bytes total, avg    0 bytes per method)\n\n" +
                    "A total of      3 methods compiled.\n"));
#elif MEASURE_PTRTAB_SIZE
                Assert.That(exception?.Message, Is.EqualTo("GCInfo::s_gcRegPtrDscSize collection is not ported."));
                Assert.That(text, Does.EndWith(
                    "\n---------------------------------------------------\n" +
                    "GC pointer table stats\n" +
                    "---------------------------------------------------\n"));
#elif EMITTER_STATS
#if TARGET_XARCH || TARGET_ARM64 || TARGET_ARM
                Assert.That(text, Does.Contain("\nInstruction format frequency table:\n"));
                Assert.That(text, Does.Contain("Descriptor size distribution:\n"));
                Assert.That(text, Does.EndWith(" bytes allocated in the emitter\n"));
#else
                Assert.That(exception?.Message, Is.EqualTo("emitterStats outside xarch and arm64 is not ported."));
#endif
#endif
            }
            Assert.That(text, Does.Not.Contain("Fatal errors stats"));
        });
    }
#endif

    [Test]
    public static void ManagedAllocationStatsAggregateConcurrentCompilationsAndTrackTheMaximum()
    {
        var stats = new ManagedAllocationStats();
        _ = System.Threading.Tasks.Parallel.For(1, 101, bytes => stats.Add(bytes));

        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

        stats.dumpAggregateMemStats(writer);
        stats.dumpMaxMemStats(writer);
        writer.Flush();

        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.That(text, Does.Contain("100 method compilations"));
        Assert.That(text, Does.Contain("managed bytes allocated:"));
        Assert.That(text, Does.Contain("5050"));
        Assert.That(text, Does.Contain("50 per compilation"));
        Assert.That(text, Does.Contain("maximum managed allocation: 100 bytes"));
    }

    [Test]
    public static void ShutdownReportsManagedAllocationStatsWithoutInventingArenaUsage()
    {
        if (Configuration.MEASURE_NODE_SIZE)
        {
            Assert.Ignore("Node-size statistics terminate at the explicit unported collection dependency.");
        }

        WithShutdownState(() => {
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            s_jitstdout = writer;
            Compiler.s_dspMemStats = true;

            Compiler.compShutdown();
            writer.Flush();

            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Does.Contain("\nAll allocations:\n"));
            Assert.That(text, Does.Contain("Distribution of total managed memory allocated per method (in KB):"));
            Assert.That(text, Does.Contain("Native arena memory used per method is unavailable for managed allocations."));
        });
    }

#if NODEBASH_STATS
    [TestCase("OperBashing")]
#endif
#if COUNT_AST_OPERS
    [TestCase("SmallNodeSize")]
#endif
    [TestCase("TrueNodeSize")]
#if CALL_ARG_STATS
    [TestCase("CallArguments")]
#endif
    [TestCase("NodeStatistics")]
    [TestCase("PerMethodNodeStatistics")]
    [TestCase("NodeStatisticsInit")]
    [TestCase("NodeCountHistogram")]
    [TestCase("NodeSizeHistogram")]
    [TestCase("NodeSizeDump")]
    [TestCase("StaticNodeSizeReport")]
#if MEASURE_BLOCK_SIZE
    [TestCase("BasicBlockSize")]
    [TestCase("FlowEdgeSize")]
#endif
#if MEASURE_PTRTAB_SIZE
    [TestCase("RegisterPointerSize")]
    [TestCase("TotalPointerSize")]
#endif
#if EMITTER_STATS
    [TestCase("EmitterStatistics")]
#endif
    public static void StatisticsDependenciesTerminateOrReportWithoutInventingData(string dependency)
    {
        if (Configuration.MEASURE_NODE_SIZE &&
            (Configuration.NODEBASH_STATS ||
                Configuration.COUNT_AST_OPERS ||
                Configuration.CALL_ARG_STATS ||
                Configuration.MEASURE_BLOCK_SIZE ||
                Configuration.MEASURE_PTRTAB_SIZE ||
                Configuration.EMITTER_STATS))
        {
            Assert.Ignore("The dependency test covers one optional statistics mode at a time.");
        }

        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

#if NODEBASH_STATS
        if (dependency == "OperBashing")
        {
            AccessDependency(dependency, writer);
            writer.Flush();

            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Is.EqualTo(
                "\nBashed gtOper stats:\n" +
                "\n" +
                "    Old operator        New operator     #bytes old->new      Count\n" +
                "    ---------------------------------------------------------------\n" +
                "\n" +
                "Total bashings: 0\n" +
                "\n"));
            return;
        }
#endif

        if (dependency == "NodeStatisticsInit")
        {
            NodeSizeStats statistics = default;
            statistics.Init();

            Assert.That(statistics.genTreeNodeCnt, Is.Zero);
            Assert.That(statistics.genTreeNodeSize, Is.Zero);
            Assert.That(statistics.genTreeNodeActualSize, Is.Zero);
            return;
        }

        if (dependency == "NodeSizeDump")
        {
            var previousWriter = s_jitstdout;
            s_jitstdout = writer;

            try
            {
                var nodeSizeException = Assert.Throws<FatalJitException>(() => AccessDependency(dependency, writer));
                Assert.That(nodeSizeException?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(nodeSizeException?.Message, Is.EqualTo(
                    "Native sizeof(GenTreeLclFld) is not ported."));
                writer.Flush();
                Assert.That(output.Length, Is.Zero);
            }
            finally
            {
                s_jitstdout = previousWriter;
            }

            return;
        }

        if (dependency == "StaticNodeSizeReport" && !Configuration.MEASURE_NODE_SIZE)
        {
            AccessDependency(dependency, writer);
            writer.Flush();
            Assert.That(output.Length, Is.Zero);
            return;
        }

#if CALL_ARG_STATS
        if (dependency == "CallArguments")
        {
            var totalCalls = Compiler.argTotalCalls;
            Compiler.argTotalCalls = 0;

            try
            {
                AccessDependency(dependency, writer);
                writer.Flush();
                Assert.That(output.Length, Is.Zero);
            }
            finally
            {
                Compiler.argTotalCalls = totalCalls;
            }

            return;
        }
#endif

#if EMITTER_STATS && (TARGET_XARCH || TARGET_ARM64)
        AccessDependency(dependency, writer);
        writer.Flush();
        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.That(text, Does.Contain("\nInstruction format frequency table:\n"));
        Assert.That(text, Does.Contain("Descriptor size distribution:\n"));
#else
        var exception = Assert.Throws<FatalJitException>(() => AccessDependency(dependency, writer));
        Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        writer.Flush();
        Assert.That(output.Length, Is.Zero);
#endif
    }

    private static void AccessDependency(string dependency, StreamWriter writer)
    {
        switch (dependency)
        {
#if NODEBASH_STATS
            case "OperBashing":
            {
                GenTree.ReportOperBashing(writer);
                break;
            }
#endif
#if COUNT_AST_OPERS
            case "SmallNodeSize":
            {
                _ = TREE_NODE_SZ_SMALL;
                break;
            }
#endif
            case "TrueNodeSize":
            {
                _ = GenTree.GetTrueSize(0);
                break;
            }
#if CALL_ARG_STATS
            case "CallArguments":
            {
                Compiler.compDispCallArgStats(writer);
                break;
            }
#endif
            case "NodeStatistics":
            {
                _ = genNodeSizeStats.genTreeNodeCnt;
                break;
            }
            case "PerMethodNodeStatistics":
            {
                _ = genNodeSizeStatsPerFunc.genTreeNodeCnt;
                break;
            }
            case "NodeStatisticsInit":
            {
                NodeSizeStats statistics = default;
                statistics.Init();
                break;
            }
            case "NodeCountHistogram":
            {
                _ = genTreeNcntHist;
                break;
            }
            case "NodeSizeHistogram":
            {
                _ = genTreeNsizHist;
                break;
            }
            case "NodeSizeDump":
            {
                GenTree.DumpNodeSizes();
                break;
            }
            case "StaticNodeSizeReport":
            {
                Compiler.compDisplayStaticSizes();
                break;
            }
#if MEASURE_BLOCK_SIZE
            case "BasicBlockSize":
            {
                _ = NativeBasicBlockSize;
                break;
            }
            case "FlowEdgeSize":
            {
                _ = NativeFlowEdgeSize;
                break;
            }
#endif
#if MEASURE_PTRTAB_SIZE
            case "RegisterPointerSize":
            {
                _ = GCInfo.s_gcRegPtrDscSize;
                break;
            }
            case "TotalPointerSize":
            {
                _ = GCInfo.s_gcTotalPtrTabSize;
                break;
            }
#endif
#if EMITTER_STATS
            case "EmitterStatistics":
            {
                emitterStats(writer);
                break;
            }
#endif
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(dependency));
            }
        }
    }

#if COUNT_AST_OPERS
    [TestCase(0U, 0U, true)]
    [TestCase(1U, 0U, true)]
    [TestCase(0U, 1U, false)]
    [TestCase(uint.MaxValue, uint.MaxValue, true)]
    [TestCase(uint.MaxValue, 0U, true)]
    [TestCase(0U, uint.MaxValue, false)]
    public static void OperatorSortPreservesNativeNonStrictUnsignedPredicate(uint first, uint second, bool expected)
    {
        var less = new Compiler.OperInfoLess();
        Assert.That(less.Less(new Compiler.OperInfo(first, 0, GT_NONE), new Compiler.OperInfo(second, 0, GT_NONE)),
            Is.EqualTo(expected));
    }

    [Test]
    public static void OperatorSortUsesNativeQuickSortForDescendingCounts()
    {
        Span<Compiler.OperInfo> operators = stackalloc Compiler.OperInfo[9];
        for (var index = 0; index < operators.Length; index++)
        {
            operators[index] = new Compiler.OperInfo((uint)(index + 1), 0, (genTreeOps)index);
        }

        SortNative(operators, new Compiler.OperInfoLess());

        for (var index = 0; index < operators.Length; index++)
        {
            Assert.That(operators[index].Count, Is.EqualTo((uint)(operators.Length - index)));
            Assert.That(operators[index].Oper, Is.EqualTo((genTreeOps)(operators.Length - index - 1)));
        }
    }

#if !DEBUG
    [Test]
    public static void OperatorSortReversesEqualCountsInNativeInsertionSort()
    {
        Span<Compiler.OperInfo> operators = stackalloc Compiler.OperInfo[8];
        for (var index = 0; index < operators.Length; index++)
        {
            operators[index] = new Compiler.OperInfo(1, 0, (genTreeOps)index);
        }

        SortNative(operators, new Compiler.OperInfoLess());

        for (var index = 0; index < operators.Length; index++)
        {
            Assert.That(operators[index].Oper, Is.EqualTo((genTreeOps)(operators.Length - index - 1)));
        }
    }
#endif
#endif

    private static void WithShutdownState(Action action)
    {
        var config = JitConfig;
        var stdout = s_jitstdout;
        var csv = CsvFile(null);
        var timeFile = TimeLogFile(null);
        var summary = CompTimeSummaryInfo.s_compTimeSummary;
        var alt = AltAssemblies(null);
#if MEASURE_NOWAY
        var noway = NowayMap(null);
        NowayMap(null) = null;
#endif
#if DEBUG
        var dump = DumpAssemblies(null);
#endif
        var methods = Compiler.genMethodCnt;
        Compiler.genMethodCnt = 1;
#if DISPLAY_SIZES
        var sizes = (grossVMsize, grossNCsize, totalNCsize);
        grossVMsize = grossNCsize = totalNCsize = 0;
#endif
#if MEASURE_BLOCK_SIZE
        var blocks = (BasicBlock.s_Count, BasicBlock.s_Size);
#endif
        var displayMemory = Compiler.s_dspMemStats;
        JitConfig = new JitConfigValues();
        CsvFile(null) = null;
        TimeLogFile(null) = 0;
        CompTimeSummaryInfo.s_compTimeSummary = default;
        try
        {
            action();
        }
        finally
        {
            JitConfig = config;
            s_jitstdout = stdout;
            CsvFile(null) = csv;
            TimeLogFile(null) = timeFile;
            CompTimeSummaryInfo.s_compTimeSummary = summary;
            AltAssemblies(null) = alt;
#if MEASURE_NOWAY
            NowayMap(null) = noway;
#endif
#if DEBUG
            DumpAssemblies(null) = dump;
#endif
            Compiler.genMethodCnt = methods;
#if DISPLAY_SIZES
            (grossVMsize, grossNCsize, totalNCsize) = sizes;
#endif
#if MEASURE_BLOCK_SIZE
            (BasicBlock.s_Count, BasicBlock.s_Size) = blocks;
#endif
            Compiler.s_dspMemStats = displayMemory;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_csvFile")]
    private static extern ref StreamWriter? CsvFile(JitTimer? timer);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "compJitTimeLogFilename")]
    private static extern ref nint TimeLogFile(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pAltJitExcludeAssembliesList")]
    private static extern ref AssemblyNamesList2? AltAssemblies(Compiler? compiler);

#if MEASURE_NOWAY
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_nowayAssertMap")]
    private static extern ref Compiler.NowayAssertMap? NowayMap(Compiler? compiler);
#endif

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pJitDisasmIncludeAssembliesList")]
    private static extern ref AssemblyNamesList2? DumpAssemblies(Compiler? compiler);
#endif
}
