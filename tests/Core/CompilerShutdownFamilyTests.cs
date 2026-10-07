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
#if MEASURE_NODE_SIZE
    [Test]
    public static void MeasurementStructureNamesRemainAvailableWithoutDebug()
    {
        Assert.That(genTreeOps.GT_ADD.StructName, Is.EqualTo(nameof(GenTreeOp)));
        Assert.That(genTreeOps.GT_CALL.StructName, Is.EqualTo(nameof(GenTreeCall)));
    }
#endif

#if !NODEBASH_STATS && !COUNT_AST_OPERS && !CALL_ARG_STATS && !MEASURE_NODE_SIZE && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
    [Test]
    public static void DisabledStatisticsPreserveCleanupAndTimingShutdown()
    {
        WithShutdownState(() => {
            AltAssemblies(null) = (AssemblyNamesList2)RuntimeHelpers.GetUninitializedObject(typeof(AssemblyNamesList2));
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvOutput = new MemoryStream();
            using var csv = new StreamWriter(csvOutput, leaveOpen: true);
            s_jitstdout = writer;
            CsvFile(null) = csv;
#if MEASURE_MEM_ALLOC
            Compiler.s_dspMemStats = false;
#endif

            Compiler.compShutdown();
            writer.Flush();

            Assert.That(AltAssemblies(null), Is.Null);
#if FEATURE_JIT_METHOD_PERF
            Assert.That(CsvFile(null), Is.Null);
#else
            Assert.That(CsvFile(null), Is.SameAs(csv));
#endif
            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Does.Not.Contain("GenTree operator counts"));
            Assert.That(text, Does.Not.Contain("All allocations:"));
            Assert.That(text, Does.Not.Contain("Largest method:"));
            Assert.That(text, Does.Not.Contain("GC pointer table stats"));
        });
    }
#endif

#if DEBUG || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || DISPLAY_SIZES || CALL_ARG_STATS
    [Test]
    public static void ZeroMethodGuardPrecedesEveryOptionalStatisticsDependency()
    {
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
#endif

#if NODEBASH_STATS || COUNT_AST_OPERS || CALL_ARG_STATS || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || MEASURE_PTRTAB_SIZE || EMITTER_STATS
    [Test]
    public static void ShutdownPreservesStatisticsCallOrderAndUnportedDependencies()
    {
        WithShutdownState(() => {
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvOutput = new MemoryStream();
            using var csv = new StreamWriter(csvOutput, leaveOpen: true);
            s_jitstdout = writer;
            CsvFile(null) = csv;
#if MEASURE_MEM_ALLOC
            Compiler.s_dspMemStats = true;
#endif
#if MEASURE_BLOCK_SIZE
            BasicBlock.s_Count = 9;
            BasicBlock.s_Size = -1;
            Compiler.genMethodCnt = 3;
#endif
#if EMITTER_STATS && (TARGET_XARCH || TARGET_ARM64) && !NODEBASH_STATS && !COUNT_AST_OPERS && !CALL_ARG_STATS && !MEASURE_NODE_SIZE && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE
            Compiler.compShutdown();
#elif CALL_ARG_STATS && !NODEBASH_STATS && !COUNT_AST_OPERS && !MEASURE_NODE_SIZE && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
            Compiler.argTotalCalls = 0;
            Compiler.compShutdown();
#else
            var exception = Assert.Throws<FatalJitException>(Compiler.compShutdown);
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#endif
            writer.Flush();
            var text = Encoding.UTF8.GetString(output.ToArray());

#if NODEBASH_STATS
            Assert.That(exception?.Message, Is.EqualTo("GenTree::ReportOperBashing is not ported."));
            Assert.That(CsvFile(null), Is.SameAs(csv));
            Assert.That(csvOutput.CanWrite, Is.True);
            Assert.That(text, Is.Empty);
#else
#if FEATURE_JIT_METHOD_PERF
            Assert.That(CsvFile(null), Is.Null);
#endif
#if COUNT_AST_OPERS
            Assert.That(exception?.Message, Is.EqualTo("GenTree::s_gtNodeCounts storage is not ported."));
            Assert.That(text, Does.Not.Contain("GenTree operator counts"));
#elif CALL_ARG_STATS && !NODEBASH_STATS && !MEASURE_NODE_SIZE && !MEASURE_BLOCK_SIZE && !MEASURE_PTRTAB_SIZE && !EMITTER_STATS
            Assert.That(text, Does.Not.Contain("Call stats"));
            Assert.That(text, Does.Not.Contain("Basic block count frequency table"));
#elif MEASURE_NODE_SIZE
            Assert.That(exception?.Message, Is.EqualTo("genNodeSizeStats collection is not ported."));
            Assert.That(text, Does.EndWith(
                "\n---------------------------------------------------\n" +
                "GenTree node allocation stats\n" +
                "---------------------------------------------------\n"));
#elif MEASURE_BLOCK_SIZE
            Assert.That(exception?.Message, Is.EqualTo("genFlowNodeCnt collection is not ported."));
            var average = unchecked((uint)(nuint.MaxValue / 3));
            Assert.That(text, Does.EndWith(
                "\n---------------------------------------------------\n" +
                "BasicBlock and FlowEdge/BasicBlockList allocation stats\n" +
                "---------------------------------------------------\n" +
                $"Allocated      9 basic blocks (4294967295 bytes total, avg {average,4} bytes per method)\n"));
#elif MEASURE_PTRTAB_SIZE
            Assert.That(exception?.Message, Is.EqualTo("GCInfo::s_gcRegPtrDscSize collection is not ported."));
            Assert.That(text, Does.EndWith(
                "\n---------------------------------------------------\n" +
                "GC pointer table stats\n" +
                "---------------------------------------------------\n"));
#elif EMITTER_STATS
#if TARGET_XARCH || TARGET_ARM64
            Assert.That(text, Does.Contain("\nInstruction format frequency table:\n"));
            Assert.That(text, Does.Contain("Descriptor size distribution:\n"));
            Assert.That(text, Does.EndWith(" bytes allocated in the emitter\n"));
#else
            Assert.That(exception?.Message, Is.EqualTo("emitterStats outside xarch and arm64 is not ported."));
#endif
#endif
#endif
            Assert.That(text, Does.Not.Contain("Fatal errors stats"));
        });
    }
#endif

#if MEASURE_MEM_ALLOC
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
#endif

#if NODEBASH_STATS || COUNT_AST_OPERS || CALL_ARG_STATS || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || MEASURE_PTRTAB_SIZE || EMITTER_STATS
#if NODEBASH_STATS
    [TestCase("OperBashing")]
#endif
#if COUNT_AST_OPERS
    [TestCase("NodeCountRead")]
    [TestCase("NodeCountWrite")]
    [TestCase("SmallNodeSize")]
#endif
#if NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS
    [TestCase("TrueNodeSize")]
#endif
#if CALL_ARG_STATS
    [TestCase("CallArguments")]
#endif
#if MEASURE_NODE_SIZE
    [TestCase("NodeStatistics")]
    [TestCase("PerMethodNodeStatistics")]
    [TestCase("NodeStatisticsInit")]
    [TestCase("NodeCountHistogram")]
    [TestCase("NodeSizeHistogram")]
    [TestCase("NodeSizeDump")]
    [TestCase("StaticNodeSizeReport")]
#endif
#if MEASURE_BLOCK_SIZE
    [TestCase("FlowCountRead")]
    [TestCase("FlowCountWrite")]
    [TestCase("FlowSizeRead")]
    [TestCase("FlowSizeWrite")]
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
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

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
            case "NodeCountRead":
            {
                _ = GenTree.GetNodeCount(0);
                break;
            }
            case "NodeCountWrite":
            {
                GenTree.GetNodeCount(0) = 1;
                break;
            }
            case "SmallNodeSize":
            {
                _ = TREE_NODE_SZ_SMALL;
                break;
            }
#endif
#if NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS
            case "TrueNodeSize":
            {
                _ = GenTree.GetTrueSize(0);
                break;
            }
#endif
#if CALL_ARG_STATS
            case "CallArguments":
            {
                Compiler.compDispCallArgStats(writer);
                break;
            }
#endif
#if MEASURE_NODE_SIZE
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
#endif
#if MEASURE_BLOCK_SIZE
            case "FlowCountRead":
            {
                _ = genFlowNodeCnt;
                break;
            }
            case "FlowCountWrite":
            {
                genFlowNodeCnt = 1;
                break;
            }
            case "FlowSizeRead":
            {
                _ = genFlowNodeSize;
                break;
            }
            case "FlowSizeWrite":
            {
                genFlowNodeSize = 1;
                break;
            }
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
#endif

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
#if DEBUG || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || DISPLAY_SIZES || CALL_ARG_STATS
        var methods = Compiler.genMethodCnt;
        Compiler.genMethodCnt = 1;
#endif
#if DISPLAY_SIZES
        var sizes = (grossVMsize, grossNCsize, totalNCsize);
        grossVMsize = grossNCsize = totalNCsize = 0;
#endif
#if MEASURE_BLOCK_SIZE
        var blocks = (BasicBlock.s_Count, BasicBlock.s_Size);
#endif
#if MEASURE_MEM_ALLOC
        var displayMemory = Compiler.s_dspMemStats;
#endif
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
#if DEBUG || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || DISPLAY_SIZES || CALL_ARG_STATS
            Compiler.genMethodCnt = methods;
#endif
#if DISPLAY_SIZES
            (grossVMsize, grossNCsize, totalNCsize) = sizes;
#endif
#if MEASURE_BLOCK_SIZE
            (BasicBlock.s_Count, BasicBlock.s_Size) = blocks;
#endif
#if MEASURE_MEM_ALLOC
            Compiler.s_dspMemStats = displayMemory;
#endif
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
