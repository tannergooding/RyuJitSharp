// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TRACK_LSRA_STATS
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanAllocationStatisticsTests
{
    [Test]
    public static void TextDumpWeightsBlockZeroAndRegularBlocksButSkipsResolutionBlocks()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 3);
            compiler.info.compFullName = "Stats::Method";
            compiler.lvaTrackedCount = 2;
            _ = StartBlockSequence(allocator);
            var blockInfo = BlockInfo(allocator);
            blockInfo[0].weight = 1.25;
            var entryStats = blockInfo[0].stats
                ?? throw new FatalJitException("The entry block must have LSRA stats.");
            var firstBlockStats = blockInfo[blocks[0].bbNum].stats
                ?? throw new FatalJitException("Sequenced blocks must have LSRA stats.");
            var resolutionStats = blockInfo[blocks[1].bbNum].stats
                ?? throw new FatalJitException("Sequenced blocks must have LSRA stats.");
            entryStats[(int)LsraStat.STAT_SPILL] = 2;
            entryStats[(int)LsraStat.STAT_FREE] = 1;
            firstBlockStats[(int)LsraStat.STAT_COPY_REG] = 3;
            firstBlockStats[(int)LsraStat.STAT_OWN_PREFERENCE] = 4;
            resolutionStats[(int)LsraStat.STAT_SPLIT_EDGE] = 7;
            blocks[0].bbWeight = 2.5;
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;
            MaxSpill(allocator)[(int)TYP_INT] = 2;
            MaxSpill(allocator)[(int)TYP_FLOAT] = 5;
            allocator.intervals.Add(new Interval(TYP_INT, SRBM_ALLINT_INIT) { isLocalVar = true });
            allocator.intervals.Add(new Interval(TYP_INT, SRBM_ALLINT_INIT));
            allocator.refPositions.Add(new RefPosition(1, 0, null, RefType.RefTypeBB));
            allocator.refPositions.Add(new RefPosition(1, 1, null, RefType.RefTypeBB));
            var newline = Environment.NewLine;

            var output = Capture(allocator);

            Assert.That(output, Is.EqualTo(
                $"----------{newline}" +
                $"LSRA Stats : Stats::Method{newline}" +
                $"----------{newline}" +
                $"Register selection order: ABCDEFGHIJKLMNOPQ{newline}" +
                $"Total Tracked Vars:  2{newline}" +
                $"Total Reg Cand Vars: 1{newline}" +
                $"Total number of Intervals: 1{newline}" +
                $"Total number of RefPositions: 1{newline}" +
                $"Total Number of spill temps created: 7{newline}" +
                $"..........{newline}" +
                $"{FMT_BB(0)} [    1.25]: SpillCount = 2, FREE = 1{newline}" +
                $"{FMT_BB(blocks[0].bbNum)} [    2.50]: CopyReg = 3, OWN_PREFERENCE = 4{newline}" +
                $"..........{newline}" +
                $"Total SpillCount : 2   Weighted: 2.500000{newline}" +
                $"Total CopyReg : 3   Weighted: 7.500000{newline}" +
                $"Total ResolutionMovs : 0   Weighted: 0.000000{newline}" +
                $"Total SplitEdges : 0   Weighted: 0.000000{newline}" +
                $"..........{newline}" +
                $"Total FREE [# 1] : 1   Weighted: 1.250000{newline}" +
                $"Total OWN_PREFERENCE [# 5] : 4   Weighted: 10.000000{newline}" +
                newline));
        });
    }

    [Test]
    public static void EmptyCountersPrintOnlyMandatoryTotalsAndNativeReferenceCount()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            compiler.info.compFullName = "Stats::Empty";
            _ = StartBlockSequence(allocator);
            var newline = Environment.NewLine;

            var output = Capture(allocator);

            Assert.That(output, Is.EqualTo(
                $"----------{newline}" +
                $"LSRA Stats : Stats::Empty{newline}" +
                $"----------{newline}" +
                $"Register selection order: ABCDEFGHIJKLMNOPQ{newline}" +
                $"Total Tracked Vars:  0{newline}" +
                $"Total Reg Cand Vars: 0{newline}" +
                $"Total number of Intervals: 0{newline}" +
                $"Total number of RefPositions: -1{newline}" +
                $"Total Number of spill temps created: 0{newline}" +
                $"..........{newline}" +
                $"..........{newline}" +
                $"Total SpillCount : 0   Weighted: 0.000000{newline}" +
                $"Total CopyReg : 0   Weighted: 0.000000{newline}" +
                $"Total ResolutionMovs : 0   Weighted: 0.000000{newline}" +
                $"Total SplitEdges : 0   Weighted: 0.000000{newline}" +
                $"..........{newline}" +
                newline));
        });
    }

    [Test]
    public static void VerboseHeaderOmitsTheMethodNameAndFinalNewlineGoesToJitStdout()
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            compiler.info.compFullName = "Stats::Verbose";
            _ = StartBlockSequence(allocator);
            compiler.verbose = true;

            using var summaryStream = new MemoryStream();
            using var summaryWriter = new JitTextWriter(summaryStream, leaveOpen: true);
            using var stdoutStream = new MemoryStream();
            using var stdoutWriter = new JitTextWriter(stdoutStream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = stdoutWriter;
                DumpLsraStats(allocator, summaryWriter);
                summaryWriter.Flush();
                stdoutWriter.Flush();

                var summary = Encoding.UTF8.GetString(summaryStream.ToArray());
                var stdout = Encoding.UTF8.GetString(stdoutStream.ToArray());
                Assert.That(summary, Does.StartWith(
                    $"----------{Environment.NewLine}LSRA Stats{Environment.NewLine}----------{Environment.NewLine}"));
                Assert.That(summary, Does.Not.Contain("Stats::Verbose"));
                Assert.That(summary, Does.EndWith($"..........{Environment.NewLine}"));
                Assert.That(stdout, Is.EqualTo(Environment.NewLine));
            }
            finally
            {
                s_jitstdout = previous;
            }
        });
    }

    [TestCase(0x80000000u, "-2147483648", "2147483648.000000")]
    [TestCase(uint.MaxValue, "-1", "4294967295.000000")]
    [SetCulture("fr-FR")]
    public static void TextCountersPreserveNativeSignedBitsAndUnsignedWeights(
        uint count, string signedCount, string weightedCount)
    {
        WithAllocator((compiler, allocator) => {
            _ = CreateBlocks(compiler, 1);
            compiler.info.compFullName = "Stats::Signed";
            _ = StartBlockSequence(allocator);
            var blockInfo = BlockInfo(allocator);
            blockInfo[0].weight = 1;
            var stats = blockInfo[0].stats
                ?? throw new AssertionException("Missing entry statistics.");
            stats[(int)LsraStat.STAT_SPILL] = count;
            MaxSpill(allocator)[(int)TYP_INT] = count;

            var output = Capture(allocator);

            Assert.That(output, Does.Contain(
                $"Total Number of spill temps created: {signedCount}{Environment.NewLine}"));
            Assert.That(output, Does.Contain(
                $"{FMT_BB(0)} [    1.00]: SpillCount = {signedCount}{Environment.NewLine}"));
            Assert.That(output, Does.Contain(
                $"Total SpillCount : {signedCount}   Weighted: {weightedCount}{Environment.NewLine}"));

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(
                stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);
            allocator.dumpLsraStatsCsv(writer);
            writer.Flush();
            var expectedStats = new uint[(int)LsraStat.COUNT];
            expectedStats[(int)LsraStat.STAT_SPILL] = count;
            var expectedRow = $"\"Stats::Signed\",{string.Join(',', expectedStats)},0.00";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Does.EndWith(expectedRow + Environment.NewLine));
        });
    }

    [Test]
    [SetCulture("fr-FR")]
    public static void CsvCountsOnlyLivePreResolutionBlocksAndWritesTheHeaderOnce()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 3);
            var removed = blocks[1];
            blocks[0].Next = blocks[2];
            blocks[2].Prev = blocks[0];
            removed.Next = null;
            removed.Prev = null;
            compiler.fgBBcount = 2;
            compiler.info.compFullName = "Stats::Csv";
            compiler.Metrics.PerfScore = 7.25;
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)removed.bbNum;
            var blockInfo = BlockInfo(allocator);
            var entryStats = blockInfo[0].stats
                ?? throw new AssertionException("Missing entry statistics.");
            var firstStats = blockInfo[blocks[0].bbNum].stats
                ?? throw new AssertionException("Missing block statistics.");
            var resolutionStats = blockInfo[blocks[2].bbNum].stats
                ?? throw new AssertionException("Missing resolution statistics.");
            var removedStats = new uint[(int)LsraStat.COUNT];
            blockInfo[removed.bbNum].stats = removedStats;
            entryStats[(int)LsraStat.STAT_SPILL] = 2;
            firstStats[(int)LsraStat.STAT_SPILL] = 3;
            removedStats[(int)LsraStat.STAT_SPILL] = 101;
            resolutionStats[(int)LsraStat.STAT_SPILL] = 103;

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(
                stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);
            allocator.dumpLsraStatsCsv(writer);
            allocator.dumpLsraStatsCsv(writer);
            writer.Flush();
            var lines = Encoding.UTF8.GetString(stream.ToArray())
                .Split(Environment.NewLine, StringSplitOptions.None);
            var expectedStats = new uint[(int)LsraStat.COUNT];
            expectedStats[(int)LsraStat.STAT_SPILL] = 5;
            var expectedRow = $"\"Stats::Csv\",{string.Join(',', expectedStats)},7.25";

            Assert.That(lines, Has.Length.EqualTo(4));
            Assert.That(lines[0], Does.StartWith("\"Method Name\",\"SpillCount\""));
            Assert.That(lines[0], Does.EndWith(",\"PerfScore\""));
            Assert.That(lines[1], Is.EqualTo(expectedRow));
            Assert.That(lines[2], Is.EqualTo(expectedRow));
            Assert.That(lines[3], Is.Empty);
        });
    }

    [Test]
    public static void CounterAndTotalsWrapWithoutWrappingWeightedAggregation()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, 2);
            compiler.info.compFullName = "Stats::Overflow";
            _ = StartBlockSequence(allocator);
            BbNumMaxBeforeResolution(allocator) = (uint)blocks[0].bbNum;
            var blockInfo = BlockInfo(allocator);
            blockInfo[0].weight = 1;
            blocks[0].bbWeight = 1;
            var entryStats = blockInfo[0].stats
                ?? throw new AssertionException("Missing entry statistics.");
            var firstStats = blockInfo[blocks[0].bbNum].stats
                ?? throw new AssertionException("Missing block statistics.");
            var resolutionStats = blockInfo[blocks[1].bbNum].stats
                ?? throw new AssertionException("Missing resolution statistics.");
            entryStats[(int)LsraStat.STAT_SPILL] = uint.MaxValue;
            entryStats[(int)LsraStat.STAT_COPY_REG] = uint.MaxValue;
            firstStats[(int)LsraStat.STAT_SPILL] = 1;

            UpdateLsraStat(allocator, LsraStat.STAT_COPY_REG, 0);
            UpdateLsraStat(allocator, LsraStat.STAT_SPILL, (uint)blocks[1].bbNum);

            Assert.That(entryStats[(int)LsraStat.STAT_COPY_REG], Is.Zero);
            Assert.That(resolutionStats[(int)LsraStat.STAT_SPILL], Is.Zero);
            var output = Capture(allocator);
            Assert.That(output, Does.Contain(
                $"Total SpillCount : 0   Weighted: 4294967296.000000{Environment.NewLine}"));

            using var summaryStream = new MemoryStream();
            using var summaryWriter = new StreamWriter(
                summaryStream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);
            allocator.dumpLsraStatsSummary(summaryWriter);
            summaryWriter.Flush();
            Assert.That(Encoding.UTF8.GetString(summaryStream.ToArray()), Is.EqualTo(
                ", SpillCount 0 SpillCountWt 4294967296.000000" +
                ", CopyReg 0 CopyRegWt 0.000000" +
                ", ResolutionMovs 0 ResolutionMovsWt 0.000000" +
                ", SplitEdges 0 SplitEdgesWt 0.000000"));

            using var csvStream = new MemoryStream();
            using var csvWriter = new StreamWriter(
                csvStream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);
            allocator.dumpLsraStatsCsv(csvWriter);
            csvWriter.Flush();
            var expectedRow = $"\"Stats::Overflow\",{string.Join(',', new uint[(int)LsraStat.COUNT])},0.00";
            Assert.That(Encoding.UTF8.GetString(csvStream.ToArray()),
                Does.EndWith(expectedRow + Environment.NewLine));
        });
    }

    [Test]
    public static void ScoreStatisticsMapEveryNativeHeuristicAndDefaultToFree()
    {
        var method = typeof(LinearScan).GetMethod(
            "getLsraStatFromScore", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing score-to-statistic mapping.");
        var scoreType = method.GetParameters()[0].ParameterType;
        foreach (var name in Enum.GetNames(scoreType))
        {
            if (name == "NONE")
            {
                continue;
            }

            var score = Enum.Parse(scoreType, name);
            var expected = Enum.Parse<LsraStat>("STAT_" + name);
            Assert.That(method.Invoke(null, [score]), Is.EqualTo(expected));
        }

        foreach (var score in new[] { 0, -1, 0x10001 })
        {
            Assert.That(method.Invoke(null, [Enum.ToObject(scoreType, score)]),
                Is.EqualTo(LsraStat.STAT_FREE));
        }
    }

    private static string Capture(LinearScan allocator)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            DumpLsraStats(allocator, writer);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index != 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = count;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgPredsComputed = true;
        return blocks;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpLsraStats")]
    private static extern void DumpLsraStats(LinearScan allocator, StreamWriter writer);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateLsraStat")]
    private static extern void UpdateLsraStat(LinearScan allocator, LsraStat stat, uint blockNumber);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "startBlockSequence")]
    private static extern BasicBlock StartBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[] BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_maxSpill")]
    private static extern ref uint[] MaxSpill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
