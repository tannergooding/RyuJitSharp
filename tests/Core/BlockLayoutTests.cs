// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockLayoutTests
{
    [TestCase(100, 0, true)]
    [TestCase(100, 0.9999, true)]
    [TestCase(100, 1, false)]
    [TestCase(1000, 9.999, true)]
    [TestCase(1000, 10, false)]
    public static void ColdThresholdUsesCalledCountAndStrictComparison(double calledCount, double weight, bool cold)
    {
        WithCompiler(compiler => {
            var block = NewBlocks(compiler, 1)[0];
            compiler.fgCalledCount = calledCount;
            block.setBBProfileWeight(weight);
            Assert.That(block.isBBWeightCold(compiler), Is.EqualTo(cold));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PublicPhaseMovesColdPollBehindHotReturnAndInvalidatesTraversal(bool reuseTraversal)
    {
        WithCompiler(compiler => {
            var blocks = NewBlocks(compiler, 3);
            Cond(compiler, blocks[0], blocks[2], blocks[1], 1);
            Jump(compiler, blocks[1], blocks[2]);
            blocks[0].setBBProfileWeight(100);
            blocks[1].bbSetRunRarely();
            blocks[2].setBBProfileWeight(100);
            if (reuseTraversal)
            {
                compiler._dfsTree = compiler.fgComputeDfs(useProfile: true);
                compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            }

            var trueEdge = blocks[0].TrueEdge;
            var falseEdge = blocks[0].FalseEdge;
            Assert.That(compiler.fgSearchImprovedLayout(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            BasicBlock[] expected = [blocks[0], blocks[2], blocks[1]];
            Assert.That(compiler.Blocks, Is.EqualTo(expected));
            Assert.That(compiler.fgLastBB, Is.SameAs(blocks[1]));
            Assert.That(blocks[0].Prev, Is.Null);
            Assert.That(blocks[2].Prev, Is.SameAs(blocks[0]));
            Assert.That(blocks[1].Prev, Is.SameAs(blocks[2]));
            Assert.That(blocks[1].Next, Is.Null);
            Assert.That(blocks[0].TrueEdge, Is.SameAs(trueEdge));
            Assert.That(blocks[0].FalseEdge, Is.SameAs(falseEdge));
            Assert.That(compiler._dfsTree, Is.Null);
            Assert.That(compiler._loops, Is.Null);
        });
    }

    [Test]
    public static void TryEndRepairUpdatesEnclosingRegionsButLeavesFuncletTriesAlone()
    {
        WithCompiler(compiler => {
            var blocks = NewBlocks(compiler, 4);
            blocks[0].TryIndex = 0;
            blocks[1].TryIndex = 1;
            blocks[3].TryIndex = 2;
            blocks[3].HndIndex = 0;
            compiler.fgFirstFuncletBB = blocks[3];
            compiler.compHndBBtabCount = 3;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdEnclosingTryIndex = 1 },
                new EHblkDsc { ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX },
                new EHblkDsc { ebdTryBeg = blocks[3], ebdTryLast = blocks[3], ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX },
            ];

            compiler.fgFindTryRegionEnds();
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(blocks[0]));
            Assert.That(compiler.compHndBBtab[1].ebdTryLast, Is.SameAs(blocks[1]));
            Assert.That(compiler.compHndBBtab[2].ebdTryLast, Is.SameAs(blocks[3]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LoopAwareTraversalKeepsNestedBodiesBeforeTheirExits(bool nested)
    {
        WithCompiler(compiler => {
            var blocks = NewBlocks(compiler, nested ? 6 : 4);
            Jump(compiler, blocks[0], blocks[1]);
            Cond(compiler, blocks[1], blocks[2], blocks[^1], 0.1);
            if (nested)
            {
                Cond(compiler, blocks[2], blocks[3], blocks[4], 0.1);
                Jump(compiler, blocks[3], blocks[2]);
                Jump(compiler, blocks[4], blocks[1]);
            }
            else
            {
                Jump(compiler, blocks[2], blocks[1]);
            }

            var tree = compiler.fgComputeDfs(useProfile: true);
            var loops = FlowGraphNaturalLoops.Find(tree);
            Assert.That(loops.NumLoops, Is.EqualTo(nested ? 2 : 1));
            var visited = new List<BasicBlock>();
            compiler.fgVisitBlocksInLoopAwareRPO(tree, loops, visited.Add);
            Assert.That(visited, Is.EqualTo(blocks));
            Assert.That(visited.Distinct().Count(), Is.EqualTo(blocks.Length));
        });
    }

    [TestCase(0.1)]
    [TestCase(0.5)]
    [TestCase(0.9)]
    public static void AcyclicTraversalPreservesProfileOrderedRPO(double likelihood)
    {
        WithCompiler(compiler => {
            var blocks = NewBlocks(compiler, 3);
            Cond(compiler, blocks[0], blocks[1], blocks[2], likelihood);
            var tree = compiler.fgComputeDfs(useProfile: true);
            var loops = FlowGraphNaturalLoops.Find(tree);
            Assert.That(loops.NumLoops, Is.Zero);
            var visited = new List<BasicBlock>();
            compiler.fgVisitBlocksInLoopAwareRPO(tree, loops, visited.Add);
            BasicBlock[] expected = likelihood < 0.5
                ? [blocks[0], blocks[2], blocks[1]]
                : [blocks[0], blocks[1], blocks[2]];
            Assert.That(visited, Is.EqualTo(expected));
        });
    }

    private static BasicBlock[] NewBlocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = block;
                block.Prev = blocks[index - 1];
            }
            blocks[index] = block;
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        return blocks;
    }

    private static void Jump(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        var edge = compiler.fgAddRefPred(target, source);
        edge.Likelihood = 1;
        source.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
    }

    private static void Cond(Compiler compiler, BasicBlock source, BasicBlock whenTrue, BasicBlock whenFalse, double likelihood)
    {
        var trueEdge = compiler.fgAddRefPred(whenTrue, source);
        var falseEdge = compiler.fgAddRefPred(whenFalse, source);
        trueEdge.Likelihood = likelihood;
        falseEdge.Likelihood = 1 - likelihood;
        source.SetCond(trueEdge, falseEdge);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        compiler.info.compFullName = nameof(BlockLayoutTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
