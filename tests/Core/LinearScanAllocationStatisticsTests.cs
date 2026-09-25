// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.IO;
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
