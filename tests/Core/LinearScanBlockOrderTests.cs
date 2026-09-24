// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanBlockOrderTests
{
    [Test]
    public static void MinOptsUsesLinearLayoutAndInitializesBlockMetadata()
    {
        WithAllocator(minOpts: true, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            compiler.fgPredsComputed = true;

            var sequence = EnumerateBlockSequence(allocator);

            Assert.That(sequence, Is.EqualTo(blocks));
            Assert.That(BbNumMaxBeforeResolution(allocator), Is.EqualTo((uint)compiler.fgBBNumMax));
            foreach (var block in blocks)
            {
                var info = BlockInfo(allocator)![block.bbNum];
                Assert.That(info.predBBNum, Is.Zero);
                Assert.That(info.weight, Is.EqualTo(block.getBBWeight(compiler)));
                Assert.That(info.hasCriticalInEdge, Is.False);
                Assert.That(info.hasCriticalOutEdge, Is.False);
            }
        });
    }

    [Test]
    public static void OptimizedSequenceCompactsLoopAndAppendsUnreachableBlocks()
    {
        WithAllocator(minOpts: false, (compiler, allocator) => {
            var blocks = CreateBlocks(
                compiler,
                BBJ_ALWAYS,
                BBJ_COND,
                BBJ_ALWAYS,
                BBJ_ALWAYS,
                BBJ_COND,
                BBJ_RETURN,
                BBJ_ALWAYS,
                BBJ_RETURN);
            var entry = blocks[0];
            var header = blocks[1];
            var exit = blocks[2];
            var body = blocks[3];
            var criticalSource = blocks[4];
            var criticalJoin = blocks[5];
            var criticalOtherPred = blocks[6];
            var unreachable = blocks[7];

            compiler.fgPredsComputed = true;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, entry, header, 1));
            header.SetCond(
                Connect(compiler, header, body, 0.5),
                Connect(compiler, header, exit, 0.5));
            body.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, body, header, 1));
            exit.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, exit, criticalSource, 1));
            criticalSource.SetCond(
                Connect(compiler, criticalSource, criticalJoin, 0.5),
                Connect(compiler, criticalSource, criticalOtherPred, 0.5));
            criticalOtherPred.SetKindAndTargetEdge(
                BBJ_ALWAYS,
                Connect(compiler, criticalOtherPred, criticalJoin, 1));

            var sequence = EnumerateBlockSequence(allocator);

            Assert.That(sequence, Has.Length.EqualTo(blocks.Length));
            Assert.That(sequence[0], Is.SameAs(entry));
            var headerIndex = IndexOf(sequence, header);
            var bodyIndex = IndexOf(sequence, body);
            Assert.That(bodyIndex, Is.EqualTo(headerIndex + 1));
            Assert.That(IndexOf(sequence, exit), Is.GreaterThan(bodyIndex));
            Assert.That(sequence[^1], Is.SameAs(unreachable));
            for (var first = 0; first < sequence.Length; first++)
            {
                for (var second = first + 1; second < sequence.Length; second++)
                {
                    Assert.That(sequence[first], Is.Not.SameAs(sequence[second]));
                }
            }

            var headerInfo = BlockInfo(allocator)![header.bbNum];
            Assert.That(headerInfo.hasCriticalInEdge, Is.False);
            Assert.That(headerInfo.hasCriticalOutEdge, Is.False);
            Assert.That(BlockInfo(allocator)![criticalSource.bbNum].hasCriticalOutEdge, Is.True);
            Assert.That(BlockInfo(allocator)![criticalJoin.bbNum].hasCriticalInEdge, Is.True);
            Assert.That(HasCriticalEdges(allocator), Is.True);
            Assert.That(compiler._loops!.GetLoopByHeader(header), Is.Not.Null);
        });
    }

    [Test]
    public static void OptimizedSequenceScansPastReachableTailForUnreachableBlocks()
    {
        WithAllocator(minOpts: false, (compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            compiler.fgPredsComputed = true;
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, blocks[0], blocks[3], 1));

            var sequence = EnumerateBlockSequence(allocator);

            BasicBlock[] expected = [blocks[0], blocks[3], blocks[2], blocks[1]];
            Assert.That(sequence, Is.EqualTo(expected));
            Assert.That(BlockInfo(allocator)![blocks[1].bbNum].weight, Is.EqualTo(blocks[1].getBBWeight(compiler)));
            Assert.That(BlockInfo(allocator)![blocks[2].bbNum].weight, Is.EqualTo(blocks[2].getBBWeight(compiler)));
        });
    }

    [Test]
    public static void BlockMetadataPreservesExceptionalPredecessorAndBoundaryPolicies()
    {
        WithAllocator(minOpts: true, (compiler, allocator) => {
            var blocks = CreateBlocks(
                compiler,
                BBJ_COND,
                BBJ_EHCATCHRET,
                BBJ_ALWAYS,
                BBJ_RETURN,
                BBJ_EHCATCHRET,
                BBJ_RETURN);
            var entry = blocks[0];
            var ehPredecessor = blocks[1];
            var normalPredecessor = blocks[2];
            var join = blocks[3];
            var uniqueEhPredecessor = blocks[4];
            var uniqueEhTarget = blocks[5];

            compiler.fgPredsComputed = true;
            entry.SetCond(
                Connect(compiler, entry, ehPredecessor, 0.5),
                Connect(compiler, entry, normalPredecessor, 0.5));
            ehPredecessor.SetKindAndTargetEdge(BBJ_EHCATCHRET, Connect(compiler, ehPredecessor, join, 1));
            normalPredecessor.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, normalPredecessor, join, 1));
            uniqueEhPredecessor.SetKindAndTargetEdge(
                BBJ_EHCATCHRET,
                Connect(compiler, uniqueEhPredecessor, uniqueEhTarget, 1));
            _ = EnumerateBlockSequence(allocator);

            var info = BlockInfo(allocator)!;
            Assert.That(info[ehPredecessor.bbNum].hasEHBoundaryOut, Is.True);
            Assert.That(info[join.bbNum].hasEHPred, Is.True);
            Assert.That(info[join.bbNum].hasEHBoundaryIn, Is.False);
            Assert.That(info[uniqueEhTarget.bbNum].hasEHBoundaryIn, Is.True);
        });
    }

    private static BasicBlock[] EnumerateBlockSequence(LinearScan allocator)
    {
        var result = new List<BasicBlock>();
        var block = StartBlockSequence(allocator);
        result.Add(block);

        BasicBlock? nextBlock;
        while ((nextBlock = MoveToNextBlock(allocator)) is not null)
        {
            result.Add(nextBlock);
        }

        return [.. result];
    }

    private static int IndexOf(BasicBlock[] blocks, BasicBlock target)
    {
        for (var index = 0; index < blocks.Length; index++)
        {
            if (ReferenceEquals(blocks[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, kinds[index]);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = blocks.Length;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        return blocks;
    }

    private static FlowEdge Connect(Compiler compiler, BasicBlock source, BasicBlock target, double likelihood)
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
        compiler.compFloatingPointUsed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        ref var registerSet = ref codeGen.RegSet;
        registerSet.rsClearRegsModified();

        try
        {
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hasCriticalEdges")]
    private static extern ref bool HasCriticalEdges(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
