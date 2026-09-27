// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, fgopt.cpp.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;

namespace RyuJitSharp.UnitTests;

internal static class ThreeOptLayoutTests
{
    [Test]
    public static void CountsOnlyLostFallthroughInPartitionCost()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 70);
            var target = Block(compiler, 50);
            Link(compiler, entry, middle, target);
            Jump(compiler, entry, middle);
            Jump(compiler, middle, target);

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, target], 3, hasEH: false);
            Assert.Multiple(() => {
                Assert.That(GetCost(layout, entry, middle), Is.Zero);
                Assert.That(GetCost(layout, entry, target), Is.EqualTo(100));
                Assert.That(GetPartitionCostDelta(layout, 1, 2, 2, 2), Is.EqualTo(170));
            });

            entry.bbWeight = double.NaN;
            Assert.That(GetCost(layout, entry, middle), Is.Zero);
        });
    }

    [Test]
    public static void LeavesNonImprovingFallthroughIntact()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 100);
            var target = Block(compiler, 100);
            Link(compiler, entry, middle, target);
            Conditional(compiler, entry, target, middle, 0.5);
            Jump(compiler, middle, target);

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, target], 3, hasEH: false);
            Assert.That(RunGreedyThreeOptPass(layout, 0, 2), Is.False);
            Assert.That(layout.Run(), Is.False);
            Assert.That(entry.Next, Is.SameAs(middle));
            Assert.That(middle.Next, Is.SameAs(target));
        });
    }

    [Test]
    public static void SwapsProfitablePartitionsAndRelinksBlocks()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 100);
            var target = Block(compiler, 100);
            Link(compiler, entry, middle, target);
            Conditional(compiler, entry, target, middle, 0.5);
            Jump(compiler, middle, entry);
            Jump(compiler, target, middle);

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, target], 3, hasEH: false);
            Assert.That(GetPartitionCostDelta(layout, 1, 2, 2, 2), Is.LessThan(0));
            Assert.That(RunGreedyThreeOptPass(layout, 0, 2), Is.True);
            Assert.That(target.bbPreorderNum, Is.EqualTo(1));
            Assert.That(middle.bbPreorderNum, Is.EqualTo(2));
            Assert.That(layout.Run(), Is.True);
            Assert.That(entry.Next, Is.SameAs(target));
            Assert.That(target.Next, Is.SameAs(middle));
        });
    }

    [Test]
    public static void PriorityQueueBreaksEqualWeightTiesBySourceThenTargetId()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 100);
            var target = Block(compiler, 100);
            Link(compiler, entry, middle, target);
            var low = compiler.fgAddRefPred(middle, entry);
            var high = compiler.fgAddRefPred(target, entry);
            var highest = compiler.fgAddRefPred(target, middle);
            low.Likelihood = 0.5;
            high.Likelihood = 0.5;
            highest.Likelihood = 0.5;

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, target], 3, hasEH: false);
            PushCutPoint(layout, low);
            PushCutPoint(layout, highest);
            PushCutPoint(layout, high);
            Assert.That(PopCutPoint(layout), Is.SameAs(highest));
            Assert.That(PopCutPoint(layout), Is.SameAs(high));
            Assert.That(PopCutPoint(layout), Is.SameAs(low));
        });
    }

    [Test]
    public static void PriorityQueuePrefersWeightOverBlockIds()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 100);
            var target = Block(compiler, 100);
            Link(compiler, entry, middle, target);
            var hotter = compiler.fgAddRefPred(target, entry);
            var colder = compiler.fgAddRefPred(target, middle);
            hotter.Likelihood = 0.8;
            colder.Likelihood = 0.2;

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, target], 3, hasEH: false);
            PushCutPoint(layout, colder);
            PushCutPoint(layout, hotter);
            Assert.That(PopCutPoint(layout), Is.SameAs(hotter));
            Assert.That(PopCutPoint(layout), Is.SameAs(colder));
        });
    }

    [Test]
    public static void PrevisitedEdgeIsSkippedThenMarkedAndClearedOnPop()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 100);
            var target = Block(compiler, 100);
            Link(compiler, entry, middle, target);
            Jump(compiler, entry, target);
            var edge = entry.TargetEdge;
            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, target], 3, hasEH: false);

            edge.Visited = true;
            Assert.That(ConsiderEdge(layout, edge, false), Is.True);
            Assert.That(RunGreedyThreeOptPass(layout, 0, 2), Is.False);
            Assert.That(edge.Visited, Is.True);

            edge.Visited = false;
            Assert.That(ConsiderEdge(layout, edge, true), Is.True);
            Assert.That(edge.Visited, Is.True);
            Assert.That(ConsiderEdge(layout, edge, true), Is.False);
            Assert.That(PopCutPoint(layout), Is.SameAs(edge));
            edge.Visited = false;

            Assert.That(RunGreedyThreeOptPass(layout, 0, 2), Is.True);
            Assert.That(edge.Visited, Is.False);
            Assert.That(compiler._dfsTree?.GetPostOrder()[1], Is.SameAs(target));
        });
    }

    [Test]
    public static void RelinksHotBlocksAheadOfColdBlocks()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var cold = Block(compiler, 0);
            var hot = Block(compiler, 100);
            Link(compiler, entry, cold, hot);
            hot.bbPreorderNum = 1;

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, hot], 2, hasEH: false);
            Assert.That(layout.Run(), Is.True);
            Assert.That(entry.Next, Is.SameAs(hot));
            Assert.That(hot.Next, Is.SameAs(cold));
        });
    }

    [Test]
    public static void ExcludesEdgesAcrossOrIntoTryEntries()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var tryEntry = Block(compiler, 100);
            var tryBody = Block(compiler, 100);
            Link(compiler, entry, tryEntry, tryBody);
            tryEntry.TryIndex = 0;
            tryBody.TryIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = tryEntry, ebdTryLast = tryBody,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX }
            ];
            compiler.compHndBBtabCount = 1;
            var crossRegion = compiler.fgAddRefPred(tryBody, entry);
            var tryEntryEdge = compiler.fgAddRefPred(tryEntry, tryBody);
            var layout = new Compiler.ThreeOptLayout(compiler, [entry, tryEntry, tryBody], 3, hasEH: true);

            Assert.That(ConsiderEdge(layout, crossRegion, false), Is.False);
            Assert.That(ConsiderEdge(layout, tryEntryEdge, false), Is.False);
        });
    }

    [Test]
    public static void MovesCompleteTryRegionBehindItsChosenPredecessor()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var after = Block(compiler, 100);
            var tryEntry = Block(compiler, 100);
            var tryEnd = Block(compiler, 100);
            var handler = Block(compiler, 0);
            Link(compiler, entry, after, tryEntry, tryEnd, handler);
            tryEntry.TryIndex = 0;
            tryEnd.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = tryEntry, ebdTryLast = tryEnd, ebdHndBeg = handler,
                    ebdHndLast = handler, ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX }
            ];
            compiler.compHndBBtabCount = 1;
            tryEntry.bbPreorderNum = 1;
            after.bbPreorderNum = 2;

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, tryEntry, after, tryEnd], 4, hasEH: true);
            Assert.That(layout.Run(), Is.True);
            Assert.That(entry.Next, Is.SameAs(tryEntry));
            Assert.That(tryEntry.Next, Is.SameAs(tryEnd));
            Assert.That(tryEnd.Next, Is.SameAs(after));
            Assert.That(after.Next, Is.SameAs(handler));
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(tryEnd));
        });
    }

    [Test]
    public static void MovesCallFinallyHeadAndTailTogether()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, 100);
            var middle = Block(compiler, 100);
            var head = Block(compiler, 100, BBJ_CALLFINALLY);
            var tail = Block(compiler, 100, BBJ_CALLFINALLYRET);
            var exit = Block(compiler, 100);
            var handler = Block(compiler, 0);
            Link(compiler, entry, middle, head, tail, exit, handler);
            exit.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = exit, ebdTryLast = exit, ebdHndBeg = handler, ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX }
            ];
            compiler.compHndBBtabCount = 1;
            Jump(compiler, entry, head);
            Jump(compiler, head, handler);
            Jump(compiler, tail, exit);

            var layout = new Compiler.ThreeOptLayout(compiler, [entry, middle, head, tail], 4, hasEH: true);
            Assert.That(head.isBBCallFinallyPair, Is.True);
            Assert.That(layout.Run(), Is.True);
            Assert.That(entry.Next, Is.SameAs(head));
            Assert.That(head.Next, Is.SameAs(tail));
            Assert.That(tail.Next, Is.SameAs(middle));
        });
    }

    private static BasicBlock Block(Compiler compiler, double weight, BBKinds kind = BBJ_RETURN)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        block.setBBProfileWeight(weight);
        return block;
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler._dfsTree = new FlowGraphDfsTree(compiler, new BasicBlock[blocks.Length], blocks.Length,
            hasCycle: false, profileAware: true);
        blocks[0].bbRefs++;
        for (var i = 1; i < blocks.Length; i++)
        {
            blocks[i - 1].Next = blocks[i];
            blocks[i].bbPreorderNum = i;
        }
    }

    private static void Jump(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(source.Kind is BBJ_CALLFINALLY or BBJ_CALLFINALLYRET
            ? source.Kind : BBJ_ALWAYS, compiler.fgAddRefPred(target, source));
        source.TargetEdge.Likelihood = 1.0;
    }

    private static void Conditional(Compiler compiler, BasicBlock source, BasicBlock taken, BasicBlock other,
        double likelihood)
    {
        source.SetCond(compiler.fgAddRefPred(taken, source), compiler.fgAddRefPred(other, source));
        source.TrueEdge.Likelihood = likelihood;
        source.FalseEdge.Likelihood = 1.0 - likelihood;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetCost")]
    private static extern double GetCost(Compiler.ThreeOptLayout layout, BasicBlock block, BasicBlock next);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetPartitionCostDelta")]
    private static extern double GetPartitionCostDelta(Compiler.ThreeOptLayout layout, int s2Start, int s3Start,
        int s3End, int s4End);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RunGreedyThreeOptPass")]
    private static extern bool RunGreedyThreeOptPass(Compiler.ThreeOptLayout layout, int startPos, int endPos);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PushCutPoint")]
    private static extern void PushCutPoint(Compiler.ThreeOptLayout layout, FlowEdge edge);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PopCutPoint")]
    private static extern FlowEdge PopCutPoint(Compiler.ThreeOptLayout layout);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ConsiderEdge")]
    private static extern bool ConsiderEdge(Compiler.ThreeOptLayout layout, FlowEdge edge, bool addToQueue);
}
