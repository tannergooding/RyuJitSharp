// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EHCloneFeasibilityTests
{
    [Test]
    public static void SimpleTryIsCollectedOnceWithoutMutatingGraphOrEhTable()
    {
        WithGraph(3, (compiler, blocks) =>
        {
            var (entry, middle, handler) = (blocks[0], blocks[1], blocks[2]);
            entry.TryIndex = 0;
            middle.TryIndex = 0;
            handler.HndIndex = 0;
            var descriptor = new EHblkDsc
            {
                ebdTryBeg = entry,
                ebdTryLast = middle,
                ebdHndBeg = handler,
                ebdHndLast = handler,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            };
            compiler.compHndBBtab = [descriptor];
            compiler.compHndBBtabCount = 1;
            var collected = new List<BasicBlock>();
            var info = new CloneTryInfo(compiler) { BlocksToClone = collected };
            var originalLinks = Array.ConvertAll(blocks,
                block => (block.Prev, block.Next, block.Kind, block.bbTryIndex, block.bbHndIndex));

            Assert.That(compiler.fgCloneTryRegionFeasibility(entry, info), Is.SameAs(entry));
            BasicBlock[] expected = [entry, middle, handler];
            Assert.That(collected, Is.EqualTo(expected));
            Assert.That(compiler.fgCloneTryRegionFeasibility(entry, info), Is.SameAs(entry));
            Assert.That(collected, Has.Count.EqualTo(3));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtab, Has.Length.EqualTo(1));
            Assert.That(compiler.compHndBBtab[0], Is.EqualTo(descriptor));
            for (var index = 0; index < blocks.Length; index++)
            {
                Assert.That((blocks[index].Prev, blocks[index].Next, blocks[index].Kind,
                    blocks[index].bbTryIndex, blocks[index].bbHndIndex), Is.EqualTo(originalLinks[index]));
                Assert.That(BitVecOps.IsMember(info.Traits, info.Visited, blocks[index].bbID), Is.True);
            }
        });
    }

    [Test]
    public static void NestedTryAndNestedHandlerAreTraversedInStackOrder()
    {
        WithGraph(7, (compiler, blocks) =>
        {
            var (outer, nestedInTry, tail, handler, nestedInHandler, innerHandler, outerTail) =
                (blocks[0], blocks[1], blocks[2], blocks[3], blocks[4], blocks[5], blocks[6]);
            outer.TryIndex = 2;
            nestedInTry.TryIndex = 0;
            tail.TryIndex = 2;
            handler.HndIndex = 2;
            nestedInHandler.TryIndex = 1;
            nestedInHandler.HndIndex = 2;
            innerHandler.HndIndex = 1;
            outerTail.HndIndex = 2;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = nestedInTry, ebdTryLast = nestedInTry,
                    ebdHndBeg = tail, ebdHndLast = tail,
                    ebdEnclosingTryIndex = 2,
                },
                new EHblkDsc
                {
                    ebdTryBeg = nestedInHandler, ebdTryLast = nestedInHandler,
                    ebdHndBeg = innerHandler, ebdHndLast = innerHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdTryBeg = outer, ebdTryLast = tail,
                    ebdHndBeg = handler, ebdHndLast = outerTail,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 3;
            var collected = new List<BasicBlock>();
            var info = new CloneTryInfo(compiler) { BlocksToClone = collected };

            Assert.That(compiler.fgCloneTryRegionFeasibility(outer, info), Is.SameAs(outer));
            Assert.That(collected, Is.EqualTo(blocks));
        });
    }

    [Test]
    public static void MutuallyProtectingHandlersAndFilterAreIncludedOnlyOnce()
    {
        WithGraph(4, (compiler, blocks) =>
        {
            var (entry, filter, firstHandler, secondHandler) =
                (blocks[0], blocks[1], blocks[2], blocks[3]);
            entry.TryIndex = 0;
            filter.HndIndex = 0;
            firstHandler.HndIndex = 0;
            secondHandler.HndIndex = 1;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry,
                    ebdFilter = filter, ebdHndBeg = firstHandler, ebdHndLast = firstHandler,
                    ebdHandlerType = EH_HANDLER_FILTER,
                    ebdEnclosingTryIndex = 1,
                },
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry,
                    ebdHndBeg = secondHandler, ebdHndLast = secondHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 2;
            var collected = new List<BasicBlock>();
            var info = new CloneTryInfo(compiler) { BlocksToClone = collected };

            Assert.That(compiler.fgCloneTryRegionFeasibility(entry, info), Is.SameAs(entry));
            Assert.That(collected, Is.EqualTo(blocks));
        });
    }

    [Test]
    public static void FinallyIncludesMatchingCallAndReturnButNotOtherTargets()
    {
        WithGraph(6, (compiler, blocks) =>
        {
            var (entry, call, ret, otherCall, handler, tail) =
                (blocks[0], blocks[1], blocks[2], blocks[3], blocks[4], blocks[5]);
            entry.TryIndex = 0;
            handler.HndIndex = 0;
            call.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(handler, call));
            ret.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, compiler.fgAddRefPred(tail, ret));
            otherCall.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(tail, otherCall));
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry,
                    ebdHndBeg = handler, ebdHndLast = handler,
                    ebdHandlerType = EH_HANDLER_FINALLY,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var collected = new List<BasicBlock>();
            var info = new CloneTryInfo(compiler) { BlocksToClone = collected };

            Assert.That(compiler.fgCloneTryRegionFeasibility(entry, info), Is.SameAs(entry));
            BasicBlock[] expected = [entry, call, ret, handler];
            Assert.That(collected, Is.EqualTo(expected));
            Assert.That(otherCall.Kind, Is.EqualTo(BBJ_CALLFINALLY));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CapacityCheckDoesNotChangeEhTableOrVisitedBlocks(bool tooManyClauses)
    {
        WithGraph(2, (compiler, blocks) =>
        {
            var entry = blocks[0];
            entry.TryIndex = 0;
            blocks[1].HndIndex = 0;
            var table = new EHblkDsc[tooManyClauses ? MAX_XCPTN_INDEX : 1];
            table[0] = new EHblkDsc
            {
                ebdTryBeg = entry, ebdTryLast = entry,
                ebdHndBeg = blocks[1], ebdHndLast = blocks[1],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            };
            compiler.compHndBBtab = table;
            compiler.compHndBBtabCount = tooManyClauses ? MAX_XCPTN_INDEX : (ushort)1;
            var info = new CloneTryInfo(compiler) { BlocksToClone = [] };
            var originalTable = compiler.compHndBBtab;

            Assert.That(compiler.fgCloneTryRegionFeasibility(entry, info),
                tooManyClauses ? Is.Null : Is.SameAs(entry));
            Assert.That(compiler.compHndBBtab, Is.SameAs(originalTable));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(tooManyClauses ? MAX_XCPTN_INDEX : 1));
            Assert.That(info.BlocksToClone, Is.EqualTo(blocks));
        });
    }

#if DEBUG
    [Test]
    public static void EnclosingHandlerPrecedesTryAndReportsInsertionPosition()
    {
        WithGraph(4, (compiler, blocks) =>
        {
            var (entry, handler, enclosingTry, enclosingHandler) =
                (blocks[0], blocks[1], blocks[2], blocks[3]);
            entry.TryIndex = 0;
            entry.HndIndex = 1;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry,
                    ebdHndBeg = handler, ebdHndLast = handler,
                    ebdEnclosingTryIndex = 2,
                },
                new EHblkDsc
                {
                    ebdTryBeg = enclosingTry, ebdTryLast = enclosingTry,
                    ebdHndBeg = entry, ebdHndLast = entry,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdTryBeg = enclosingTry, ebdTryLast = enclosingTry,
                    ebdHndBeg = enclosingHandler, ebdHndLast = enclosingHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 3;
            compiler.verbose = true;
            var info = new CloneTryInfo(compiler);

            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(compiler.fgCloneTryRegionFeasibility(entry, info), Is.SameAs(entry)));

            Assert.That(output, Does.Contain("Checking if it is possible to clone the try region EH#00"));
            Assert.That(output, Does.Contain("Will need to clone 1 EH regions"));
            Assert.That(output, Does.Contain("Cloned EH clauses will go before enclosing handler region EH#01"));
            Assert.That(output, Does.Contain("fgCloneTryRegion: cloning is possible"));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(3));
        });
    }
#endif

    private static void WithGraph(int blockCount, Action<Compiler, BasicBlock[]> action)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var blocks = new BasicBlock[blockCount];
            for (var index = 0; index < blocks.Length; index++)
            {
                blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
                if (index > 0)
                {
                    blocks[index - 1].Next = blocks[index];
                }
            }

            compiler.fgFirstBB = blocks[0];
            compiler.fgLastBB = blocks[^1];
            action(compiler, blocks);
        });
    }
}
