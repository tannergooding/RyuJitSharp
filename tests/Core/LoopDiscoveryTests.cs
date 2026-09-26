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
internal static unsafe class LoopDiscoveryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void DiscoveryRecordsCyclesAndPhaseState(bool hasLoop)
    {
        WithCompiler(compiler => {
            int[][] successors = hasLoop ? [[1], [2, 3], [1], []] : [[1], [2], []];
            var expectedLoops = hasLoop ? 1 : 0;
            var blocks = CreateGraph(compiler, successors);
            compiler._dfsTree = compiler.fgComputeDfs();
            var oldTree = compiler._dfsTree;

            Assert.That(compiler.optFindLoopsPhase(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler._loops!.NumLoops, Is.EqualTo(expectedLoops));
            Assert.That(compiler.Metrics.LoopsFoundDuringOpts, Is.EqualTo(expectedLoops));
            Assert.That(compiler.optLoopsCanonical, Is.True);
            Assert.That(compiler.fgMightHaveNaturalLoops, Is.EqualTo(expectedLoops != 0));
            if (expectedLoops == 0)
            {
                Assert.That(compiler._dfsTree, Is.SameAs(oldTree));
            }
            if (expectedLoops != 0)
            {
                var loop = compiler._loops.GetLoopByHeader(blocks[1]);
                Assert.That(loop, Is.Not.Null);
                Assert.That(loop!.GetPreheader(), Is.Not.Null);
                Assert.That(loop.GetPreheader()!.Target, Is.SameAs(blocks[1]));
                Assert.That(loop.BackEdges.Length, Is.EqualTo(1));
            }
        });
    }

    [Test]
    public static void ExistingUnconditionalPreheaderDoesNotChangeDfsTree()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [2], [3, 4], [2], []]);
            compiler._dfsTree = compiler.fgComputeDfs();
            var originalTree = compiler._dfsTree;

            _ = compiler.optFindLoopsPhase();
            Assert.That(compiler._dfsTree, Is.SameAs(originalTree));
            Assert.That(compiler._loops!.GetLoopByHeader(blocks[2])!.GetPreheader(), Is.SameAs(blocks[1]));
            Assert.That(compiler.Metrics.LoopsFoundDuringOpts, Is.EqualTo(1));
        });
    }

    [Test]
    public static void MultipleBackedgesAcquireOneLatch()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [2, 4], [3, 1], [1], []]);
            compiler._dfsTree = compiler.fgComputeDfs();
            Assert.That(FlowGraphNaturalLoops.Find(compiler._dfsTree).GetLoopByHeader(blocks[1])!.BackEdges.Length, Is.EqualTo(2));

            _ = compiler.optFindLoopsPhase();
            var loop = compiler._loops!.GetLoopByHeader(blocks[1])!;
            Assert.That(loop.BackEdges.Length, Is.EqualTo(1));
            var latch = loop.BackEdge(0).SourceBlock;
            Assert.That(latch.HasFlag(BBF_INTERNAL), Is.True);
            Assert.That(blocks[2].FalseTarget, Is.SameAs(latch));
            Assert.That(blocks[3].Target, Is.SameAs(latch));
            Assert.That(latch.Target, Is.SameAs(blocks[1]));
        });
    }

    [Test]
    public static void MixedPredecessorExitIsSplit()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1, 4], [2, 3], [1], [], [3]]);
            compiler._dfsTree = compiler.fgComputeDfs();
            _ = compiler.optFindLoopsPhase();

            var loop = compiler._loops!.GetLoopByHeader(blocks[1])!;
            var splitExit = blocks[1].FalseTarget;
            Assert.That(splitExit, Is.Not.SameAs(blocks[3]));
            Assert.That(splitExit.Target, Is.SameAs(blocks[3]));
            Assert.That(splitExit.HasFlag(BBF_INTERNAL), Is.True);
            Assert.That(blocks[4].Target, Is.SameAs(blocks[3]));
            Assert.That(loop.ContainsBlock(splitExit), Is.False);
            var predecessorCount = 0;
            foreach (var pred in splitExit.PredBlocks)
            {
                Assert.That(pred, Is.SameAs(blocks[1]));
                predecessorCount++;
            }

            Assert.That(predecessorCount, Is.EqualTo(1));
        });
    }

    [Test]
    public static void LexicalInterloperMovesAfterLoopWithoutChangingDfs()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [3, 5], [5], [4], [1], []]);
            compiler._dfsTree = compiler.fgComputeDfs();
            var originalTree = compiler._dfsTree;
            var originalLoop = FlowGraphNaturalLoops.Find(originalTree).GetLoopByHeader(blocks[1]);
            Assert.That(originalLoop, Is.Not.Null);
            Assert.That(originalLoop!.ContainsBlock(blocks[2]), Is.False);

            _ = compiler.optFindLoopsPhase();
            var positions = new Dictionary<BasicBlock, int>();
            var index = 0;
            foreach (var block in compiler.Blocks)
            {
                positions.Add(block, index++);
            }

            Assert.That(positions[blocks[2]], Is.GreaterThan(positions[blocks[4]]));
        });
    }

    [Test]
    public static void EnclosingTrySkipsMutuallyProtectingClauses()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], []]);
            compiler.fgImportDone = true;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdEnclosingTryIndex = 1 },
                new EHblkDsc { ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX },
            ];
            compiler.compHndBBtabCount = 2;
            Assert.That(compiler.ehTrueEnclosingTryIndex(0), Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
        });
    }

    [Test]
    public static void TryEntryMovesToNewPreheaderWhenBackedgeStaysInsideTry()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [2, 3], [1], [], []]);
            blocks[1].TryIndex = 0;
            blocks[2].TryIndex = 0;
            blocks[4].HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = blocks[1],
                    ebdTryLast = blocks[2],
                    ebdHndBeg = blocks[4],
                    ebdHndLast = blocks[4],
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            compiler.fgImportDone = true;
            compiler._dfsTree = compiler.fgComputeDfs();

            _ = compiler.optFindLoopsPhase();

            var preheader = compiler._loops!.GetLoopByHeader(blocks[1])!.GetPreheader()!;
            Assert.That(preheader, Is.Not.SameAs(blocks[0]));
            Assert.That(preheader.TryIndex, Is.Zero);
            Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(preheader));
            Assert.That(blocks[1].TryIndex, Is.Zero);
            Assert.That(compiler.bbIsTryBeg(blocks[1]), Is.False);
        });
    }

    [Test]
    public static void NewPreheaderAndExitInheritLikelyProfileWeights()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1, 4], [2, 3], [1], [], [3]]);
            for (var i = 0; i < blocks.Length; i++)
            {
                blocks[i].SetFlags(BBF_PROF_WEIGHT);
                blocks[i].bbWeight = 10 * (i + 1);
            }

            compiler._dfsTree = compiler.fgComputeDfs();
            _ = compiler.optFindLoopsPhase();

            var preheader = compiler._loops!.GetLoopByHeader(blocks[1])!.GetPreheader()!;
            var newExit = blocks[1].FalseTarget;
            Assert.That(preheader.bbWeight, Is.EqualTo(5));
            Assert.That(preheader.hasProfileWeight, Is.True);
            Assert.That(newExit.bbWeight, Is.EqualTo(10));
            Assert.That(newExit.hasProfileWeight, Is.True);
        });
    }

    [Test]
    public static void TryRegionEndInsertionUpdatesEnclosingTryEnds()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [2], []]);
            blocks[1].TryIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = blocks[1],
                    ebdTryLast = blocks[1],
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;

            var inserted = compiler.fgNewBBatTryRegionEnd(BBJ_ALWAYS, 0);

            Assert.That(inserted.Prev, Is.SameAs(blocks[1]));
            Assert.That(inserted.Next, Is.SameAs(blocks[2]));
            Assert.That(inserted.TryIndex, Is.Zero);
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(inserted));
        });
    }

    [Test]
    public static void BackedgeOutsideTrySplitsTryEntryHeader()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1], [2], [3], [1], []]);
            blocks[1].TryIndex = 0;
            blocks[2].TryIndex = 0;
            blocks[4].HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = blocks[1],
                    ebdTryLast = blocks[2],
                    ebdHndBeg = blocks[4],
                    ebdHndLast = blocks[4],
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            compiler.fgImportDone = true;
            compiler._dfsTree = compiler.fgComputeDfs();

            _ = compiler.optFindLoopsPhase();

            var newTryEntry = compiler.compHndBBtab[0].ebdTryBeg;
            Assert.That(newTryEntry, Is.Not.SameAs(blocks[1]));
            Assert.That(blocks[1].hasTryIndex, Is.False);
            Assert.That(newTryEntry.TryIndex, Is.Zero);
            Assert.That(compiler.bbIsTryBeg(newTryEntry), Is.True);
            Assert.That(compiler._loops!.GetLoopByHeader(blocks[1])!.GetPreheader(), Is.SameAs(blocks[0]));
        });
    }

    [Test]
    public static void OutOfRegionBackedgePlacesPreheaderOutsideTryBeforeSplittingHeader()
    {
        WithCompiler(compiler => {
            var blocks = CreateGraph(compiler, [[1, 5], [2], [3], [1], [], []]);
            blocks[1].TryIndex = 0;
            blocks[2].TryIndex = 0;
            blocks[4].HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = blocks[1],
                    ebdTryLast = blocks[2],
                    ebdHndBeg = blocks[4],
                    ebdHndLast = blocks[4],
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            compiler.fgImportDone = true;
            compiler._dfsTree = compiler.fgComputeDfs();

            _ = compiler.optFindLoopsPhase();

            var preheader = compiler._loops!.GetLoopByHeader(blocks[1])!.GetPreheader()!;
            Assert.That(preheader, Is.Not.SameAs(blocks[0]));
            Assert.That(preheader.hasTryIndex, Is.False);
            Assert.That(blocks[0].TrueTarget, Is.SameAs(preheader));
            Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.Not.SameAs(blocks[1]));
            Assert.That(blocks[1].hasTryIndex, Is.False);
        });
    }

    [TestCase(false, NodeThreading.None)]
    [TestCase(false, NodeThreading.AllLocals)]
    [TestCase(false, NodeThreading.AllTrees)]
    [TestCase(true, NodeThreading.None)]
    [TestCase(true, NodeThreading.AllLocals)]
    [TestCase(true, NodeThreading.AllTrees)]
    public static void InversionDuplicatesTopTestButRetainsBottomTestedLoops(bool bottomTested, NodeThreading threading)
    {
        WithCompiler(compiler => {
#if OPT_CONFIG
            InversionEnabled(ref JitConfig) = 1;
#endif
            InversionSizeLimit(ref JitConfig) = 100;
            var blocks = CreateGraph(compiler, bottomTested
                ? [[1], [2], [1, 3], []]
                : [[1], [2, 3], [1], []]);
            compiler.lvaCount = 1;
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            var conditionBlock = blocks[bottomTested ? 2 : 1];
            var comparison = new GenTreeOp(genTreeOps.GT_LT, var_types.TYP_INT,
                compiler.gtNewLclvNode(var_types.TYP_INT, 0),
                compiler.gtNewIconNode(var_types.TYP_INT, 10));
            var jump = new GenTreeUnOp(genTreeOps.GT_JTRUE, var_types.TYP_VOID, comparison);
            compiler.fgNodeThreading = threading;
            compiler.fgInsertStmtAtEnd(conditionBlock, compiler.fgNewStmtFromTree(jump));
            compiler._dfsTree = compiler.fgComputeDfs();
            _ = compiler.optFindLoopsPhase();
            var oldTree = compiler._dfsTree;

            var status = compiler.optInvertLoops();

            Assert.That(status, Is.EqualTo(bottomTested
                ? PhaseStatus.MODIFIED_NOTHING : PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.LoopsInverted, Is.EqualTo(bottomTested ? 0 : 1));
            Assert.That(compiler._loops!.NumLoops, Is.EqualTo(1));
            if (!bottomTested)
            {
                Assert.That(compiler._dfsTree, Is.Not.SameAs(oldTree));
                Assert.That(blocks[0].Kind, Is.EqualTo(BBJ_COND));
                var clonedStatement = blocks[0].LastStmt!;
                Assert.That(clonedStatement.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(genTreeOps.GT_GE));
                Assert.That(clonedStatement.TreeListBegin, Is.Null);
                Assert.That(blocks[0].TrueEdge.Likelihood, Is.EqualTo(0.5));
                Assert.That(blocks[0].FalseEdge.Likelihood, Is.EqualTo(0.5));
            }
        });
    }

#if OPT_CONFIG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDoLoopInversion")]
    private static extern ref int InversionEnabled(ref JitConfigValues config);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitLoopInversionSizeLimit")]
    private static extern ref int InversionSizeLimit(ref JitConfigValues config);

#if FEATURE_LOOP_ALIGN
    [TestCase(7, false, false, false)]
    [TestCase(8, false, false, true)]
    [TestCase(9, false, false, true)]
    [TestCase(9, true, false, false)]
    [TestCase(9, false, true, false)]
    public static void AlignmentRetainsWeightThresholdAndCallAndColdExclusions(
        int weight, bool hasCall, bool cold, bool expected)
    {
        WithCompiler(compiler => {
            var blocks = PrepareAlignment(compiler, [[1], [2, 3], [1], []]);
            compiler.opts.compJitAlignLoopMinBlockWeight = 8;
            blocks[1].bbWeight = weight * BB_UNITY_WEIGHT;
            if (cold)
            {
                blocks[1].SetFlags(BBF_COLD);
            }
            if (hasCall)
            {
                blocks[2].InsertAtEnd(new GenTreeCall(var_types.TYP_VOID));
            }

            var status = compiler.placeLoopAlignInstructions();

            Assert.That(status, Is.EqualTo(expected
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(blocks[1].HasFlag(BBF_LOOP_ALIGN), Is.EqualTo(expected));
            Assert.That(blocks[0].HasFlag(BBF_HAS_ALIGN), Is.EqualTo(expected));
            Assert.That(compiler.Metrics.LoopAlignmentCandidates, Is.EqualTo(expected ? 1 : 0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AlignmentPrefersRetainedJumpEvenWhenPreheaderIsColder(bool hide)
    {
        WithCompiler(compiler => {
            var blocks = PrepareAlignment(compiler, [[1, 2], [3, 4], [4], [4], [5], [6, 7], [5], []]);
            compiler.opts.compJitHideAlignBehindJmp = hide;
            blocks[2].bbWeight = 100 * BB_UNITY_WEIGHT;
            blocks[4].bbWeight = BB_UNITY_WEIGHT;

            Assert.That(compiler.placeLoopAlignInstructions(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(blocks[5].HasFlag(BBF_LOOP_ALIGN), Is.True);
            Assert.That(blocks[2].HasFlag(BBF_HAS_ALIGN), Is.EqualTo(hide));
            Assert.That(blocks[4].HasFlag(BBF_HAS_ALIGN), Is.EqualTo(!hide));
            Assert.That(compiler.Metrics.LoopAlignmentCandidates, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AlignmentExcludesMethodEntryAndNonInnermostLoops(bool nested)
    {
        WithCompiler(compiler => {
            var blocks = PrepareAlignment(compiler, nested
                ? [[1], [2, 5], [3, 4], [2], [1], []]
                : [[1, 2], [0], []]);

            var status = compiler.placeLoopAlignInstructions();

            Assert.That(status, Is.EqualTo(nested
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(blocks[nested ? 1 : 0].HasFlag(BBF_LOOP_ALIGN), Is.False);
            if (nested)
            {
                Assert.That(blocks[2].HasFlag(BBF_LOOP_ALIGN), Is.True);
                Assert.That(blocks[1].HasFlag(BBF_HAS_ALIGN), Is.True);
            }
            Assert.That(compiler.Metrics.LoopAlignmentCandidates, Is.EqualTo(nested ? 1 : 0));
        });
    }

    private static BasicBlock[] PrepareAlignment(Compiler compiler, int[][] successors)
    {
        var blocks = CreateGraph(compiler, successors);
        compiler.codeGen = new CodeGen(compiler) {
            ShouldAlignLoops = true,
        };
        compiler.fgMightHaveNaturalLoops = true;
        compiler.fgCalledCount = BB_UNITY_WEIGHT;
        foreach (var block in blocks)
        {
            block.SetFlags(BBF_IS_LIR);
            block.bbWeight = 10 * BB_UNITY_WEIGHT;
        }

        return blocks;
    }
#endif

    private static BasicBlock[] CreateGraph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[i].bbRefs = i == 0 ? 1 : 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgPredsComputed = true;
        for (var i = blocks.Length - 1; i >= 0; i--)
        {
            var edges = new List<FlowEdge>();
            foreach (var destination in successors[i])
            {
                var target = blocks[destination];
                var edge = new FlowEdge(blocks[i], target, target.bbPreds);
                target.bbPreds = edge;
                edge.incrementDupCount();
                target.bbRefs++;
                edges.Add(edge);
            }

            if (edges.Count == 1)
            {
                blocks[i].SetKindAndTargetEdge(BBJ_ALWAYS, edges[0]);
            }
            else if (edges.Count == 2)
            {
                blocks[i].SetCond(edges[0], edges[1]);
                edges[0].Likelihood = 0.5;
                edges[1].Likelihood = 0.5;
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
        compiler.fgSafeFlowEdgeCreation = true;
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
