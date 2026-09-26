// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockWeightTests
{
    [TestCase("diamond", new double[] { 1, 0.5, 0.5, 1 })]
    [TestCase("loop", new double[] { 1, 8, 4, 1 })]
    [TestCase("split-loop", new double[] { 1, 8, 2, 2, 8, 1 })]
    [TestCase("nested-loop", new double[] { 1, 8, 32, 16, 16, 4, 1 })]
    public static void HeuristicWeightsRespectReturnsAndBackEdgeDominance(string shape, double[] expected)
    {
        WithCompiler(compiler => {
            int[][] successors = shape switch {
                "diamond" => [[1, 2], [3], [3], []],
                "loop" => [[1], [2, 3], [1], []],
                "split-loop" => [[1], [2, 3], [4], [4], [1, 5], []],
                "nested-loop" => [[1], [2, 6], [3, 5], [4], [2], [1], []],
                _ => throw new ArgumentException("Unknown graph.", nameof(shape)),
            };
            var blocks = CreateGraph(compiler, successors);
            PrepareGraph(compiler);

            Assert.That(compiler.optSetBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Array.ConvertAll(blocks, block => (double)block.bbWeight),
                Is.EqualTo(Array.ConvertAll(expected, weight => weight * BB_UNITY_WEIGHT)));
            Assert.That(compiler.fgHasLoops, Is.EqualTo(shape != "diamond"));
            Assert.That(compiler._domTree, Is.Not.Null);
            Assert.That(compiler._reachabilitySets, Is.Not.Null);
            Assert.That(compiler.fgReturnBlocks!.Block, Is.SameAs(blocks[^1]));
            Assert.That(compiler.fgReturnBlocks.Next, Is.Null);
        });
    }

    [Test]
    public static void ProfileWeightsLeaveGraphsUntouchedAfterRecordingCycles()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [1, 2], []]);
            PrepareGraph(compiler);
            compiler.fgPgoHaveWeights = true;
            var previousReturns = compiler.fgReturnBlocks = new BasicBlockList(blocks[0]);
            blocks[1].bbWeight = 7;
            blocks[2].bbWeight = 13;

            Assert.That(compiler.optSetBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgHasLoops, Is.True);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(7));
            Assert.That(blocks[2].bbWeight, Is.EqualTo(13));
            Assert.That(compiler.fgReturnBlocks, Is.SameAs(previousReturns));
            Assert.That(compiler._domTree, Is.Null);
            Assert.That(compiler._reachabilitySets, Is.Null);
        });
    }

    [Test]
    public static void SingleReturnWithoutLoopsOrRareBlocksChangesNothing()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], []]);
            PrepareGraph(compiler);

            Assert.That(compiler.optSetBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(blocks[0].bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
            Assert.That(compiler.fgReturnBlocks!.Block, Is.SameAs(blocks[1]));
            Assert.That(compiler.fgHasLoops, Is.False);
        });
    }

    [Test]
    public static void HandlerPathToReturnSuppressesHeuristicScaling()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], [], [3]]);
            blocks[1].TryIndex = 0;
            blocks[4].HndIndex = 0;
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                ebdTryBeg = blocks[1],
                ebdTryLast = blocks[1],
                ebdHndBeg = blocks[4],
                ebdHndLast = blocks[4],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            PrepareGraph(compiler);

            Assert.That(compiler._dfsTree!.Contains(blocks[4]), Is.True);
            var reachability = BlockReachabilitySets.Build(compiler._dfsTree);
            Assert.That(reachability.CanReach(blocks[4], blocks[3]), Is.True);
            Assert.That(reachability.CanReach(blocks[0], blocks[4]), Is.False);
            Assert.That(compiler.optSetBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
            Assert.That(blocks[2].bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
            Assert.That(blocks[4].bbWeight, Is.Zero);
            Assert.That(compiler.fgReturnBlocks!.Block, Is.SameAs(blocks[3]));
        });
    }

    [Test]
    public static void UnreachableBlocksBecomeRareUnlessProfileWeighted()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [], [], []]);
            blocks[2].SetKindAndTargetEdge(BBJ_THROW, null);
            // An unreachable return suppresses the return-dominance heuristic in native.
            blocks[3].SetFlags(BBF_PROF_WEIGHT);
            blocks[3].bbWeight = 3;
            PrepareGraph(compiler);

            Assert.That(compiler.optSetBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[2].bbWeight, Is.Zero);
            Assert.That(blocks[3].bbWeight, Is.EqualTo(3));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
            Assert.That(compiler.fgReturnBlocks!.Block, Is.SameAs(blocks[3]));
        });
    }

    [Test]
    public static void ReturnBlocksAreRebuiltInReverseLexicalOrder()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1, 2], [], []]);
            compiler.fgReturnBlocks = new BasicBlockList(blocks[0]);
            compiler.fgComputeReturnBlocks();

            Assert.That(compiler.fgReturnBlocks!.Block, Is.SameAs(blocks[2]));
            Assert.That(compiler.fgReturnBlocks.Next!.Block, Is.SameAs(blocks[1]));
            Assert.That(compiler.fgReturnBlocks.Next.Next, Is.Null);
        });
    }

    [Test]
    public static void NoReturnBlocksPreserveWeightsAndClearPreviousList()
    {
        WithCompiler(compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            block.SetKindAndTargetEdge(BBJ_THROW, null);
            compiler.fgReturnBlocks = new BasicBlockList(block);
            PrepareGraph(compiler);

            Assert.That(compiler.optSetBlockWeights(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgReturnBlocks, Is.Null);
            Assert.That(block.bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
        });
    }

    [Test]
    public static void ReachabilityUsesRegularPredecessorsAndTransitiveCycles()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], [1], []]);
            var dfs = compiler.fgComputeDfs();
            var sets = BlockReachabilitySets.Build(dfs);

            Assert.That(sets.GetDfsTree(), Is.SameAs(dfs));
            Assert.That(sets.CanReach(blocks[0], blocks[3]), Is.True);
            Assert.That(sets.CanReach(blocks[3], blocks[1]), Is.True);
            Assert.That(sets.CanReach(blocks[1], blocks[1]), Is.True);
            Assert.That(sets.CanReach(blocks[1], blocks[2]), Is.False);
            Assert.That(sets.CanReach(blocks[2], blocks[1]), Is.True);
            Assert.That(sets.CanReach(blocks[0], blocks[4]), Is.False);
        });
    }

    [Test]
    public static void ReachabilityPropagatesAcrossBitsetWords()
    {
        WithCompiler(compiler => {
            const int count = 130;
            var successors = new int[count][];
            for (var i = 0; i < count - 1; i++)
            {
                successors[i] = [i + 1];
            }
            successors[^1] = [];

            var blocks = CreateGraph(compiler, successors);
            var reachability = BlockReachabilitySets.Build(compiler.fgComputeDfs());
            Assert.That(reachability.CanReach(blocks[0], blocks[^1]), Is.True);
            Assert.That(reachability.CanReach(blocks[^1], blocks[0]), Is.False);
            Assert.That(reachability.CanReach(blocks[64], blocks[129]), Is.True);
            Assert.That(reachability.CanReach(blocks[129], blocks[64]), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void DiagnosticsDistinguishUnreachableBlocksAndListReturns()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [], []]);
            var dfs = compiler.fgComputeDfs();
            compiler.verbose = true;

            var output = CodeGenLifeTransitionTests.Capture(() => {
                _ = BlockReachabilitySets.Build(dfs);
                compiler.fgComputeReturnBlocks();
            });
            Assert.That(output, Does.Contain("After computing reachability sets:"));
            Assert.That(output, Does.Contain($"{FMT_BB(blocks[2].bbNum)} : [unreachable]"));
            Assert.That(output, Does.Contain("Return blocks:"));
            Assert.That(output, Does.Contain($"{FMT_BB(blocks[2].bbNum)} {FMT_BB(blocks[1].bbNum)}"));
        });
    }
#endif

    private static void PrepareGraph(Compiler compiler)
    {
        compiler._dfsTree = compiler.fgComputeDfs();
        compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
    }

    private static BasicBlock[] CreateGraph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, BBJ_RETURN);
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        for (var i = 0; i < blocks.Length; i++)
        {
            var edges = new List<FlowEdge>();
            foreach (var destination in successors[i])
            {
                var target = blocks[destination];
                var edge = new FlowEdge(blocks[i], target, target.bbPreds);
                target.bbPreds = edge;
                edges.Add(edge);
            }

            if (edges.Count == 1)
            {
                blocks[i].SetKindAndTargetEdge(BBJ_ALWAYS, edges[0]);
            }
            else if (edges.Count == 2)
            {
                blocks[i].SetCond(edges[0], edges[1]);
            }
        }
        return blocks;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        using var tls = new JitTls(null);
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitTls.Compiler = compiler;
        JitConfig = default;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }
}
