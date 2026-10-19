// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerShutdownFamilyTests
{
    [Test]
    public static void OperatorCountStorageIncludesSentinelAndSupportsWrites()
    {
        if (!Configuration.COUNT_AST_OPERS)
        {
            Assert.Ignore("Operator-count storage is disabled for this build.");
        }

        var nodeCounts = GetNodeCounts();
        var previousCount = nodeCounts[(int)GT_ADD];
        var updatedCount = unchecked(previousCount + 1);

        try
        {
            nodeCounts[(int)GT_ADD] = updatedCount;
            Assert.That(nodeCounts[(int)GT_ADD], Is.EqualTo(updatedCount));
        }
        finally
        {
            nodeCounts[(int)GT_ADD] = previousCount;
        }

        Assert.That(nodeCounts[(int)GT_COUNT], Is.Zero);
    }

    [Test]
    public static void NodeConstructionIncrementsOperatorCountWhenEnabled()
    {
        if (!Configuration.COUNT_AST_OPERS)
        {
            Assert.Ignore("Operator-count storage is disabled for this build.");
        }

        var nodeCounts = GetNodeCounts();
        SsaLivenessTests.WithCompiler(2, _ => {
            var previousCount = nodeCounts[(int)GT_CNS_INT];

            try
            {
                var node = new GenTreeIntCon(TYP_INT, 1);
                Assert.That(node.Oper, Is.EqualTo(GT_CNS_INT));
                Assert.That(nodeCounts[(int)GT_CNS_INT], Is.EqualTo(unchecked(previousCount + 1)));
            }
            finally
            {
                nodeCounts[(int)GT_CNS_INT] = previousCount;
            }
        });
    }

    [Test]
    public static void NodeBashStorageMatchesFeatureConfiguration()
    {
        var nodeBash = GetRequiredStaticField(typeof(GenTree), "BashHash") as GenTree.BashHashDsc[] ??
            throw new InvalidOperationException("GenTree node-bashing storage is not available.");

        Assert.That(nodeBash.Length > 0, Is.EqualTo(Configuration.NODEBASH_STATS));
    }

    [Test]
    public static void NodeBashReportRequiresEnabledStatistics()
    {
        if (Configuration.NODEBASH_STATS)
        {
            Assert.Ignore("Node-bashing statistics are enabled for this build.");
        }

        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        var exception = Assert.Throws<InvalidOperationException>(() => GenTree.ReportOperBashing(writer));

        Assert.That(exception?.Message, Is.EqualTo("Node-bashing statistics are not enabled."));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public static void SetOperRecordsNodeBashingWhenStatisticsAreEnabled()
    {
        if (!Configuration.NODEBASH_STATS)
        {
            Assert.Ignore("Node-bashing statistics are disabled for this build.");
        }

        var nodeBash = GetRequiredStaticField(typeof(GenTree), "BashHash") as GenTree.BashHashDsc[] ??
            throw new InvalidOperationException("GenTree node-bashing storage is not available.");
        var snapshot = (GenTree.BashHashDsc[])nodeBash.Clone();
        var previousCount = GetNodeBashCount(nodeBash);

        try
        {
            SsaLivenessTests.WithCompiler(2, _ => {
                var node = new GenTreeIntCon(TYP_INT, 1);
                node.SetOper(GT_CNS_INT);
            });

            var updatedCount = GetNodeBashCount(nodeBash);

            Assert.That(updatedCount, Is.EqualTo(unchecked(previousCount + 1)));
        }
        finally
        {
            Array.Copy(snapshot, nodeBash, nodeBash.Length);
        }
    }

    [Test]
    public static void FlowNodeStatisticsStorageSupportsReadsAndWrites()
    {
        if (!Configuration.MEASURE_BLOCK_SIZE)
        {
            Assert.Ignore("Flow-node statistics are disabled for this build.");
        }

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

    [Test]
    public static void MeasurementStructureNamesRemainAvailableWithoutDebug()
    {
        if (!TestConfiguration.IsDebug && !Configuration.MEASURE_NODE_SIZE)
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
            Assert.That(text, Does.Not.Contain("Fatal errors stats"));
        });
    }

    [Test]
    public static void DisabledEmitterStatisticsDoNotAllocateStorage()
    {
        if (Configuration.EMITTER_STATS)
        {
            Assert.Ignore("Emitter statistics are enabled for this build.");
        }

        foreach (var fieldName in new[] { "emitSmallCns", "emitIFcounts", "emitSizeTable", "GCrefsTable", "stkDepthTable" })
        {
            var field = typeof(Emitter).GetField(fieldName, StaticMemberBindingFlags) ??
                throw new InvalidOperationException($"{typeof(Emitter).FullName}.{fieldName} is not available.");

            Assert.That(field.GetValue(null), Is.Null, fieldName);
        }
    }

    [Test]
    public static void ZeroMethodGuardPrecedesEveryOptionalStatisticsDependency()
    {
        if (!TestConfiguration.IsDebug &&
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
            if (TestConfiguration.IsDebug)
            {
                SetOptionalStaticField(typeof(Compiler), "s_pJitDisasmIncludeAssembliesList",
                    (AssemblyNamesList2)RuntimeHelpers.GetUninitializedObject(typeof(AssemblyNamesList2)));
            }
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvOutput = new MemoryStream();
            using var csv = new StreamWriter(csvOutput, leaveOpen: true);
            s_jitstdout = writer;
            CsvFile(null) = csv;

            Compiler.compShutdown();
            writer.Flush();

            Assert.That(AltAssemblies(null), Is.Null);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(GetOptionalStaticField(typeof(Compiler), "s_pJitDisasmIncludeAssembliesList"), Is.Null);
            }
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

        if (Configuration.MEASURE_FATAL)
        {
            Assert.Ignore("Fatal counter reporting is covered by CompilerShutdownTests.");
        }

        if (Configuration.MEASURE_NODE_SIZE &&
            (Configuration.NODEBASH_STATS ||
                Configuration.COUNT_BASIC_BLOCKS ||
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
            if (Configuration.MEASURE_BLOCK_SIZE)
            {
                BasicBlock.s_Count = 9;
                BasicBlock.s_Size = -1;
                Compiler.genMethodCnt = 3;
            }

            var onlyNodeBash =
                Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.COUNT_AST_OPERS &&
                !Configuration.CALL_ARG_STATS &&
                !Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.MEASURE_PTRTAB_SIZE &&
                !Configuration.EMITTER_STATS;
            var onlyEmitterStats =
                Configuration.EMITTER_STATS &&
                (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_ARM) &&
                !Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.COUNT_AST_OPERS &&
                !Configuration.CALL_ARG_STATS &&
                !Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.MEASURE_PTRTAB_SIZE;
            var onlyCallArgumentStats =
                Configuration.CALL_ARG_STATS &&
                !Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.COUNT_AST_OPERS &&
                !Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.MEASURE_PTRTAB_SIZE &&
                !Configuration.EMITTER_STATS;
            var onlyOperatorCounts =
                Configuration.COUNT_AST_OPERS &&
                !Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.CALL_ARG_STATS &&
                !Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.MEASURE_PTRTAB_SIZE &&
                !Configuration.EMITTER_STATS;
            var onlyBlockSize =
                Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.COUNT_AST_OPERS &&
                !Configuration.CALL_ARG_STATS &&
                !Configuration.MEASURE_PTRTAB_SIZE &&
                !Configuration.EMITTER_STATS;
            var onlyPointerTableStats =
                Configuration.MEASURE_PTRTAB_SIZE &&
                !Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.COUNT_AST_OPERS &&
                !Configuration.CALL_ARG_STATS &&
                !Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.EMITTER_STATS;
            var onlyBasicBlockCounts =
                Configuration.COUNT_BASIC_BLOCKS &&
                !Configuration.NODEBASH_STATS &&
                !Configuration.COUNT_AST_OPERS &&
                !Configuration.CALL_ARG_STATS &&
                !Configuration.MEASURE_NODE_SIZE &&
                !Configuration.MEASURE_BLOCK_SIZE &&
                !Configuration.MEASURE_PTRTAB_SIZE &&
                !Configuration.EMITTER_STATS &&
                !Configuration.DISPLAY_SIZES;

            if (onlyNodeBash || onlyEmitterStats || onlyBlockSize || onlyPointerTableStats || onlyBasicBlockCounts)
            {
                Compiler.compShutdown();
            }
            else if (onlyCallArgumentStats)
            {
                SetRequiredStaticField(typeof(Compiler), "argTotalCalls", 0U);
                Compiler.compShutdown();
            }
            else if (onlyOperatorCounts)
            {
                var nodeCounts = GetNodeCounts();
                var previousCount = nodeCounts[(int)GT_ADD];

                try
                {
                    nodeCounts[(int)GT_ADD] = unchecked(previousCount + 1);
                    exception = Assert.Throws<FatalJitException>(Compiler.compShutdown);
                    Assert.That(exception?.Message,
                        Is.EqualTo("GenTree::s_gtTrueSizes native logical sizes are not ported."));
                }
                finally
                {
                    nodeCounts[(int)GT_ADD] = previousCount;
                }
            }
            else
            {
                exception = Assert.Throws<FatalJitException>(Compiler.compShutdown);
                Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            }

            writer.Flush();
            var text = Encoding.UTF8.GetString(output.ToArray());

            if (Configuration.NODEBASH_STATS)
            {
                Assert.That(text, Does.Contain("Bashed gtOper stats:\n"));
                Assert.That(text, Does.Contain("Total bashings: 0\n"));
                Assert.That(csvOutput.CanWrite, Is.True);
            }

            if (!Configuration.NODEBASH_STATS)
            {
                Assert.That(CsvFile(null), Is.Null);
            }

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
                if (Configuration.COUNT_AST_OPERS)
                {
                    Assert.That(text, Does.Not.Contain("GenTree operator counts"));
                }
                else if (Configuration.CALL_ARG_STATS &&
                    !Configuration.NODEBASH_STATS &&
                    !Configuration.MEASURE_BLOCK_SIZE &&
                    !Configuration.MEASURE_PTRTAB_SIZE &&
                    !Configuration.EMITTER_STATS)
                {
                    Assert.That(text, Does.Not.Contain("Call stats"));
                    Assert.That(text, Does.Not.Contain("Basic block count frequency table"));
                }
                else if (Configuration.MEASURE_BLOCK_SIZE)
                {
                    var average = unchecked((uint)(nuint.MaxValue / 3));
                    Assert.That(text, Does.EndWith(
                        "\n---------------------------------------------------\n" +
                        "BasicBlock and FlowEdge/BasicBlockList allocation stats\n" +
                        "---------------------------------------------------\n" +
                        $"Allocated      9 basic blocks (4294967295 bytes total, avg {average,4} bytes per method)\n" +
                        "Allocated      0 flow nodes (      0 bytes total, avg    0 bytes per method)\n\n" +
                        "A total of      3 methods compiled.\n"));
                }
                else if (Configuration.MEASURE_PTRTAB_SIZE)
                {
                    Assert.That(text, Does.Contain("\nGC pointer table stats\n"));
                    Assert.That(text, Does.Contain("Reg pointer descriptor size (internal):        0 (avg    0 per method)\n"));
                    Assert.That(text, Does.Contain("Total pointer table size:        0 (avg    0 per method)\n"));
                }
                else if (Configuration.COUNT_BASIC_BLOCKS)
                {
                    Assert.That(text, Does.Contain("Basic block count frequency table:"));
                    Assert.That(text, Does.Contain("IL method size frequency table for methods with a single basic block:"));
                    Assert.That(text, Does.Contain("fgComputeReachabilitySets `while (change)` iterations:"));
                }
                else if (Configuration.EMITTER_STATS)
                {
                    if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_ARM)
                    {
                        Assert.That(text, Does.Contain("\nInstruction format frequency table:\n"));
                        Assert.That(text, Does.Contain("Descriptor size distribution:\n"));
                        Assert.That(text, Does.EndWith(" bytes allocated in the emitter\n"));
                    }
                    else
                    {
                        Assert.That(exception?.Message,
                            Is.EqualTo("emitterStats outside xarch, arm, and arm64 is not ported."));
                    }
                }
            }
            Assert.That(text, Does.Not.Contain("Fatal errors stats"));
        });
    }

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

    public static IEnumerable<TestCaseData> StatisticsDependencyCases()
    {
        if (Configuration.NODEBASH_STATS)
        {
            yield return new TestCaseData("OperBashing");
        }

        if (Configuration.COUNT_AST_OPERS)
        {
            yield return new TestCaseData("SmallNodeSize");
        }

        yield return new TestCaseData("TrueNodeSize");
        yield return new TestCaseData("NodeStatistics");
        yield return new TestCaseData("PerMethodNodeStatistics");
        yield return new TestCaseData("NodeStatisticsInit");
        yield return new TestCaseData("NodeCountHistogram");
        yield return new TestCaseData("NodeSizeHistogram");
        yield return new TestCaseData("NodeSizeDump");
        yield return new TestCaseData("StaticNodeSizeReport");

        if (Configuration.CALL_ARG_STATS)
        {
            yield return new TestCaseData("CallArguments");
        }

        if (Configuration.MEASURE_BLOCK_SIZE)
        {
            yield return new TestCaseData("BasicBlockSize");
            yield return new TestCaseData("FlowEdgeSize");
        }

        if (Configuration.MEASURE_PTRTAB_SIZE)
        {
            yield return new TestCaseData("RegisterPointerSize");
            yield return new TestCaseData("TotalPointerSize");
        }

        if (Configuration.EMITTER_STATS)
        {
            yield return new TestCaseData("EmitterStatistics");
        }
    }

    [TestCaseSource(nameof(StatisticsDependencyCases))]
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

        if (Configuration.NODEBASH_STATS && dependency == "OperBashing")
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

        if (Configuration.CALL_ARG_STATS && dependency == "CallArguments")
        {
            var totalCalls = GetRequiredStaticField(typeof(Compiler), "argTotalCalls");
            SetRequiredStaticField(typeof(Compiler), "argTotalCalls", 0U);

            try
            {
                AccessDependency(dependency, writer);
                writer.Flush();
                Assert.That(output.Length, Is.Zero);
            }
            finally
            {
                SetRequiredStaticField(typeof(Compiler), "argTotalCalls", totalCalls);
            }

            return;
        }

        if (Configuration.MEASURE_PTRTAB_SIZE &&
            (dependency == "RegisterPointerSize" || dependency == "TotalPointerSize"))
        {
            AccessDependency(dependency, writer);
            writer.Flush();
            Assert.That(output.Length, Is.Zero);
            return;
        }

        if (Configuration.EMITTER_STATS && dependency == "EmitterStatistics" &&
            (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_ARM))
        {
            AccessDependency(dependency, writer);
            writer.Flush();
            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text, Does.Contain("\nInstruction format frequency table:\n"));
            Assert.That(text, Does.Contain("Descriptor size distribution:\n"));
            return;
        }

        var exception = Assert.Throws<FatalJitException>(() => AccessDependency(dependency, writer));
        Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        writer.Flush();
        Assert.That(output.Length, Is.Zero);
    }

    private static void AccessDependency(string dependency, StreamWriter writer)
    {
        switch (dependency)
        {
            case "OperBashing":
            {
                _ = InvokeStaticMethod(typeof(GenTree), "ReportOperBashing", writer);
                break;
            }
            case "SmallNodeSize":
            {
                _ = TREE_NODE_SZ_SMALL;
                break;
            }
            case "TrueNodeSize":
            {
                _ = GenTree.GetTrueSize(0);
                break;
            }
            case "CallArguments":
            {
                _ = InvokeStaticMethod(typeof(Compiler), "compDispCallArgStats", writer);
                break;
            }
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
                _ = InvokeStaticMethod(typeof(Compiler), "compDisplayStaticSizes");
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
            case "RegisterPointerSize":
            {
                _ = GetRequiredStaticField(typeof(GCInfo), "s_gcRegPtrDscSize");
                break;
            }
            case "TotalPointerSize":
            {
                _ = GetRequiredStaticField(typeof(GCInfo), "s_gcTotalPtrTabSize");
                break;
            }
            case "EmitterStatistics":
            {
                _ = InvokeStaticMethod(typeof(Emitter), "emitterStats", writer);
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(dependency));
            }
        }
    }

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

    [Test]
    public static void OperatorSortReversesEqualCountsInNativeInsertionSort()
    {
        if (TestConfiguration.IsDebug)
        {
            Assert.Ignore("Native insertion-sort tie ordering is only exercised in Release.");
        }

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

    private const BindingFlags StaticMemberBindingFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static uint[] GetNodeCounts()
    {
        return GetRequiredStaticField(typeof(GenTree), "s_gtNodeCounts") as uint[] ??
            throw new InvalidOperationException("GenTree node-count storage is not available.");
    }

    private static int GetNodeBashCount(GenTree.BashHashDsc[] nodeBash)
    {
        foreach (var entry in nodeBash)
        {
            if ((entry.bhOperOld == GT_CNS_INT) && (entry.bhOperNew == GT_CNS_INT))
            {
                return entry.bhCount;
            }
        }

        return 0;
    }

    private static object? GetOptionalStaticField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type declaringType,
        string fieldName)
    {
        return declaringType.GetField(fieldName, StaticMemberBindingFlags)?.GetValue(null);
    }

    private static object GetRequiredStaticField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type declaringType,
        string fieldName)
    {
        var field = declaringType.GetField(fieldName, StaticMemberBindingFlags) ??
            throw new InvalidOperationException($"{declaringType.FullName}.{fieldName} is not available.");
        return field.GetValue(null) ??
            throw new InvalidOperationException($"{declaringType.FullName}.{fieldName} has no value.");
    }

    private static void SetOptionalStaticField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type declaringType,
        string fieldName,
        object? value)
    {
        declaringType.GetField(fieldName, StaticMemberBindingFlags)?.SetValue(null, value);
    }

    private static void SetRequiredStaticField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type declaringType,
        string fieldName,
        object value)
    {
        var field = declaringType.GetField(fieldName, StaticMemberBindingFlags) ??
            throw new InvalidOperationException($"{declaringType.FullName}.{fieldName} is not available.");
        field.SetValue(null, value);
    }

    private static object? InvokeStaticMethod(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type declaringType,
        string methodName,
        params object?[] arguments)
    {
        var method = declaringType.GetMethod(methodName, StaticMemberBindingFlags) ??
            throw new InvalidOperationException($"{declaringType.FullName}.{methodName} is not available.");

        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void WithShutdownState(Action action)
    {
        var config = JitConfig;
        var stdout = s_jitstdout;
        var csv = CsvFile(null);
        var timeFile = TimeLogFile(null);
        var summary = CompTimeSummaryInfo.s_compTimeSummary;
        var alt = AltAssemblies(null);
        var noway = GetOptionalStaticField(typeof(Compiler), "s_nowayAssertMap");
        SetOptionalStaticField(typeof(Compiler), "s_nowayAssertMap", null);
        var dump = GetOptionalStaticField(typeof(Compiler), "s_pJitDisasmIncludeAssembliesList");
        var methods = Compiler.genMethodCnt;
        Compiler.genMethodCnt = 1;
        var grossVmSize = GetOptionalStaticField(typeof(Globals), "grossVMsize");
        var grossNativeCodeSize = GetOptionalStaticField(typeof(Globals), "grossNCsize");
        var totalNativeCodeSize = GetOptionalStaticField(typeof(Globals), "totalNCsize");
        SetOptionalStaticField(typeof(Globals), "grossVMsize", (nuint)0);
        SetOptionalStaticField(typeof(Globals), "grossNCsize", (nuint)0);
        SetOptionalStaticField(typeof(Globals), "totalNCsize", (nuint)0);
        var blocks = (BasicBlock.s_Count, BasicBlock.s_Size);
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
            SetOptionalStaticField(typeof(Compiler), "s_nowayAssertMap", noway);
            SetOptionalStaticField(typeof(Compiler), "s_pJitDisasmIncludeAssembliesList", dump);
            Compiler.genMethodCnt = methods;
            SetOptionalStaticField(typeof(Globals), "grossVMsize", grossVmSize);
            SetOptionalStaticField(typeof(Globals), "grossNCsize", grossNativeCodeSize);
            SetOptionalStaticField(typeof(Globals), "totalNCsize", totalNativeCodeSize);
            (BasicBlock.s_Count, BasicBlock.s_Size) = blocks;
            Compiler.s_dspMemStats = displayMemory;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_csvFile")]
    private static extern ref StreamWriter? CsvFile(JitTimer? timer);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "compJitTimeLogFilename")]
    private static extern ref nint TimeLogFile(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pAltJitExcludeAssembliesList")]
    private static extern ref AssemblyNamesList2? AltAssemblies(Compiler? compiler);

}
