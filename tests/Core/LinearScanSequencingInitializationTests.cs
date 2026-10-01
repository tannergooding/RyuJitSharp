// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 || TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Collections.Generic;
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
#if TARGET_AMD64
using static RyuJitSharp.regMask;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanSequencingInitializationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void RestartClearsVisitationAndPeekDoesNotAdvance(bool minOpts)
    {
        WithAllocator(minOpts, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, blocks[0], blocks[1]));

            Assert.That(StartBlockSequence(allocator), Is.SameAs(blocks[0]));
            MarkBlockVisited(allocator, blocks[1]);
            var metadata = BlockInfo(allocator);

            Assert.That(StartBlockSequence(allocator), Is.SameAs(blocks[0]));
            Assert.That(BlockInfo(allocator), Is.SameAs(metadata));
            Assert.That(IsBlockVisited(allocator, blocks[0]), Is.True);
            Assert.That(IsBlockVisited(allocator, blocks[1]), Is.False);
            Assert.That(CurrentBlockNumber(allocator), Is.EqualTo((uint)blocks[0].bbNum));
            Assert.That(CurrentSequenceNumber(allocator), Is.Zero);

            Assert.That(GetNextBlock(allocator), Is.SameAs(blocks[1]));
            Assert.That(GetNextBlock(allocator), Is.SameAs(blocks[1]));
            Assert.That(CurrentSequenceNumber(allocator), Is.Zero);
            Assert.That(CurrentBlockNumber(allocator), Is.EqualTo((uint)blocks[0].bbNum));

            Assert.That(MoveToNextBlock(allocator), Is.SameAs(blocks[1]));
            Assert.That(CurrentSequenceNumber(allocator), Is.EqualTo(1));
            Assert.That(CurrentBlockNumber(allocator), Is.EqualTo((uint)blocks[1].bbNum));
            Assert.That(IsBlockVisited(allocator, blocks[1]), Is.False);
            Assert.That(MoveToNextBlock(allocator), Is.Null);
            Assert.That(CurrentSequenceNumber(allocator), Is.EqualTo(2));
            Assert.That(CurrentBlockNumber(allocator), Is.EqualTo((uint)blocks[1].bbNum));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EntryBackedgeIncludesImplicitPrologPredecessor(bool minOpts)
    {
        WithAllocator(minOpts, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_RETURN);
            blocks[0].SetCond(
                Connect(compiler, blocks[0], blocks[0], 0.5),
                Connect(compiler, blocks[0], blocks[1], 0.5));

            _ = StartBlockSequence(allocator);

            var metadata = BlockInfo(allocator)!;
            Assert.That(metadata[blocks[0].bbNum].hasCriticalInEdge, Is.True);
            Assert.That(metadata[blocks[0].bbNum].hasCriticalOutEdge, Is.True);
            Assert.That(metadata[blocks[1].bbNum].hasCriticalInEdge, Is.False);
        });
    }

    [Test]
    public static void CallFinallyTailAndUniqueSuccessorRetainExceptionalBoundaries()
    {
        WithAllocator(true, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_CALLFINALLY, BBJ_CALLFINALLYRET, BBJ_RETURN, BBJ_EHFINALLYRET);
            blocks[0].SetKindAndTargetEdge(
                BBJ_CALLFINALLY, Connect(compiler, blocks[0], blocks[3]));
            blocks[1].SetKindAndTargetEdge(
                BBJ_CALLFINALLYRET, Connect(compiler, blocks[1], blocks[2]));

            _ = StartBlockSequence(allocator);

            var metadata = BlockInfo(allocator)!;
            Assert.That(metadata[blocks[1].bbNum].hasEHBoundaryIn, Is.True);
            Assert.That(metadata[blocks[1].bbNum].hasEHBoundaryOut, Is.True);
            Assert.That(metadata[blocks[1].bbNum].hasEHPred, Is.False);
            Assert.That(metadata[blocks[2].bbNum].hasEHBoundaryIn, Is.True);
            Assert.That(metadata[blocks[2].bbNum].hasEHPred, Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void UnreachableMetadataIsVisitedBeforeItsSequenceEntryIsPublished()
    {
        WithAllocator(false, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, blocks[0], blocks[3]));
            compiler.verbose = true;
            using var stream = new SequenceObservationStream(allocator);
            using var writer = new JitTextWriter(stream, leaveOpen: true) { AutoFlush = true };
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                _ = StartBlockSequence(allocator);
            }
            finally
            {
                s_jitstdout = previous;
            }

            Assert.That(stream.SequenceCounts, Is.EqualTo((int[])[2, 2, 2, 3]));
            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].weight,
                Is.EqualTo(blocks[1].getBBWeight(compiler)));
            Assert.That(BlockInfo(allocator)![blocks[2].bbNum].weight,
                Is.EqualTo(blocks[2].getBBWeight(compiler)));
        });
    }

    private sealed class SequenceObservationStream(LinearScan allocator) : MemoryStream
    {
        internal List<int> SequenceCounts { get; } = [];

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Encoding.UTF8.GetString(buffer).Contains("Current block:", StringComparison.Ordinal))
            {
                SequenceCounts.Add(SequenceCount(allocator));
            }

            base.Write(buffer);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequenceCount")]
    private static extern ref int SequenceCount(LinearScan allocator);
#endif

    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, kinds[index]);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index != 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = blocks.Length;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgPredsComputed = true;

        return blocks;
    }

    private static FlowEdge Connect(Compiler compiler, BasicBlock source, BasicBlock target, double likelihood = 1)
    {
        var edge = compiler.fgAddRefPred(target, source);
        edge.Likelihood = likelihood;

        return edge;
    }

    private static void WithAllocator(bool minOpts, Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compFlags = minOpts ? CLFLG_MINOPT : 0;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
#if TARGET_AMD64
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.CopyRegisterInfo();
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "startBlockSequence")]
    private static extern BasicBlock StartBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "moveToNextBlock")]
    private static extern BasicBlock? MoveToNextBlock(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getNextBlock")]
    private static extern BasicBlock? GetNextBlock(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "markBlockVisited")]
    private static extern void MarkBlockVisited(LinearScan allocator, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "isBlockVisited")]
    private static extern bool IsBlockVisited(LinearScan allocator, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockSequenceNumber")]
    private static extern ref int CurrentSequenceNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

#if TARGET_AMD64
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
#endif
}
#endif
