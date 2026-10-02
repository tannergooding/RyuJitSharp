// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.EHblkDsc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerTryRegionTraversalTests
{
    [Test]
    public static void NoRegionsOrReachableLoopsVisitOnlyDescendingDfsOrder()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 3);
            Jump(blocks[0], blocks[1]);
            Jump(blocks[2], blocks[2]);
            blocks[2].bbPostorderNum = int.MaxValue;
            BasicBlock[] reachable = [blocks[0], blocks[1]];
            var dfs = Dfs(compiler, reachable, reachable, hasCycle: false);
            SetClauses(compiler, 5);
            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, dfs);

            Assert.That(loops.NumLoops, Is.Zero);
            Assert.That(regions.NumTryRegions(), Is.Zero);
            Assert.That(Visit(compiler, dfs, regions, loops), Is.EqualTo(reachable));
        });
    }

    [TestCase(EH_HANDLER_FINALLY)]
    [TestCase(EH_HANDLER_FAULT)]
    [TestCase(EH_HANDLER_FAULT_WAS_FINALLY)]
    public static void NoCatchRegionsDelegateToTheExistingLoopAwareTraversal(EHHandlerType handlerType)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = ConflictGraph(compiler);
            var dfs = ConflictDfs(compiler, blocks);
            SetClauses(compiler, 8, Clause(7, blocks[1], blocks[2], blocks[5], handlerType));
            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, null);
            Assert.That(regions.NumTryCatchRegions(), Is.Zero);
            Assert.That(loops.NumLoops, Is.EqualTo(1));

            var expected = new List<BasicBlock>();
            compiler.fgVisitBlocksInLoopAwareRPO(dfs, loops, expected.Add);
            var actual = Visit(compiler, dfs, regions, loops);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual, Is.EqualTo<BasicBlock[]>([blocks[0], blocks[1], blocks[3], blocks[2], blocks[4]]));
        });
    }

    [Test]
    public static void EmptyCatchCountSnapshotStillDelegatesAfterTheDescriptorChanges()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = ConflictGraph(compiler);
            var dfs = ConflictDfs(compiler, blocks);
            SetClauses(compiler, 1, Clause(0, blocks[1], blocks[2], blocks[5], EH_HANDLER_FINALLY));
            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, null);
            compiler.compHndBBtab[0].ebdHandlerType = EH_HANDLER_CATCH;

            Assert.That(regions.NumTryCatchRegions(), Is.Zero);
            Assert.That(regions.GetTryRegionByHeader(blocks[1])?.HasCatchHandler(), Is.True);
            Assert.That(Visit(compiler, dfs, regions, loops),
                Is.EqualTo<BasicBlock[]>([blocks[0], blocks[1], blocks[3], blocks[2], blocks[4]]));
        });
    }

    [TestCase(1)]
    [TestCase(65)]
    [TestCase(130)]
    public static void CatchTraversalUsesDfsIndicesAndExcludesUnreachableTryBlocks(int count)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, count + 4);
            var reachable = new BasicBlock[count + 2];
            for (var index = 0; index <= count; index++)
            {
                reachable[index] = blocks[index];
                if (index > 0)
                {
                    blocks[index].TryIndex = 0;
                    Jump(blocks[index - 1], blocks[index]);
                }
            }

            var unreachable = blocks[count + 1];
            unreachable.TryIndex = 0;
            unreachable.bbPostorderNum = int.MaxValue;
            reachable[^1] = blocks[count + 2];
            Jump(blocks[count], reachable[^1]);
            var dfs = Dfs(compiler, reachable, reachable, hasCycle: false);
            SetClauses(compiler, 12, Clause(11, blocks[1], unreachable, blocks[^1], EH_HANDLER_CATCH));
            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            var region = regions.GetTryRegionByHeader(blocks[1])
                ?? throw new AssertionException("Missing catch region at its header.");

            Assert.That(loops.NumLoops, Is.Zero);
            Assert.That(regions.NumTryRegions(), Is.EqualTo(1));
            Assert.That(region.UnreachableBlocks().ToArray(), Is.EqualTo<BasicBlock[]>([unreachable]));
            Assert.That(Visit(compiler, dfs, regions, loops), Is.EqualTo(reachable));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CatchRegionPrecedesTheIntersectingLoopAndReadsCallbackState(bool removeCatchInCallback)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = ConflictGraph(compiler);
            var dfs = ConflictDfs(compiler, blocks);
            SetClauses(compiler, 5, Clause(4, blocks[1], blocks[2], blocks[5], EH_HANDLER_CATCH));
            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            Assert.That(regions.NumTryCatchRegions(), Is.EqualTo(1));
            Assert.That(loops.GetLoopByHeader(blocks[1])?.ContainsBlock(blocks[3]), Is.True);
            Assert.That(loops.GetLoopByHeader(blocks[1])?.ContainsBlock(blocks[2]), Is.False);

            var actual = Visit(compiler, dfs, regions, loops, block =>
            {
                if (removeCatchInCallback && (block == blocks[1]))
                {
                    compiler.compHndBBtab[0].ebdHandlerType = EH_HANDLER_FINALLY;
                }
            });

            BasicBlock[] expected = removeCatchInCallback
                ? [blocks[0], blocks[1], blocks[3], blocks[2], blocks[4]]
                : [blocks[0], blocks[1], blocks[2], blocks[3], blocks[4]];
            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NestedAndMutuallyProtectingRegionsKeepNestedLoopBodiesExactlyOnce(bool mutualProtection)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 10);
            Jump(blocks[0], blocks[1]);
            Branch(blocks[1], blocks[2], blocks[6]);
            Branch(blocks[2], blocks[3], blocks[4]);
            Branch(blocks[3], blocks[2], blocks[5]);
            Jump(blocks[4], blocks[6]);
            Jump(blocks[5], blocks[1]);
            BasicBlock[] preorder = [blocks[0], blocks[1], blocks[2], blocks[3], blocks[5], blocks[4], blocks[6]];
            BasicBlock[] reversePostorder = [blocks[0], blocks[1], blocks[2], blocks[4], blocks[6], blocks[3], blocks[5]];
            var dfs = Dfs(compiler, reversePostorder, preorder, hasCycle: true);
            var outerIndex = mutualProtection ? (ushort)2 : (ushort)1;
            blocks[1].TryIndex = outerIndex;
            blocks[2].TryIndex = 0;
            blocks[3].TryIndex = 0;
            blocks[4].TryIndex = outerIndex;
            if (mutualProtection)
            {
                SetClauses(compiler, 9,
                    Clause(8, blocks[2], blocks[3], blocks[7], EH_HANDLER_CATCH, parent: 1),
                    Clause(3, blocks[2], blocks[3], blocks[8], EH_HANDLER_CATCH, parent: 2),
                    Clause(1, blocks[1], blocks[4], blocks[9], EH_HANDLER_CATCH));
            }
            else
            {
                SetClauses(compiler, 9,
                    Clause(8, blocks[2], blocks[3], blocks[7], EH_HANDLER_CATCH, parent: 1),
                    Clause(1, blocks[1], blocks[4], blocks[9], EH_HANDLER_CATCH));
            }

            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            Assert.That(loops.NumLoops, Is.EqualTo(2));
            Assert.That(loops.GetLoopByHeader(blocks[2])?.Parent, Is.SameAs(loops.GetLoopByHeader(blocks[1])));
            Assert.That(regions.NumTryCatchRegions(), Is.EqualTo(mutualProtection ? 3 : 2));
            Assert.That(Visit(compiler, dfs, regions, loops),
                Is.EqualTo<BasicBlock[]>([blocks[0], blocks[1], blocks[2], blocks[3], blocks[4], blocks[5], blocks[6]]));
        });
    }

    [Test]
    public static void CallbackFailurePropagatesAndTheNextInvocationHasFreshVisitedState()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = ConflictGraph(compiler);
            var dfs = ConflictDfs(compiler, blocks);
            SetClauses(compiler, 1, Clause(0, blocks[1], blocks[2], blocks[5], EH_HANDLER_CATCH));
            var loops = FlowGraphNaturalLoops.Find(dfs);
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            var visited = new List<BasicBlock>();
            var failure = new InvalidOperationException("Callback stopped traversal.");

            var actual = Assert.Throws<InvalidOperationException>(() =>
                compiler.fgVisitBlocksInTryAwareLoopAwareRPO(dfs, regions, loops, block =>
                {
                    visited.Add(block);
                    if (block == blocks[1])
                    {
                        throw failure;
                    }
                }));

            Assert.That(actual, Is.SameAs(failure));
            Assert.That(visited, Is.EqualTo<BasicBlock[]>([blocks[0], blocks[1]]));
            Assert.That(Visit(compiler, dfs, regions, loops),
                Is.EqualTo<BasicBlock[]>([blocks[0], blocks[1], blocks[2], blocks[3], blocks[4]]));
        });
    }

    private static List<BasicBlock> Visit(
        Compiler compiler, FlowGraphDfsTree dfs, FlowGraphTryRegions regions, FlowGraphNaturalLoops loops,
        Action<BasicBlock>? callback = null)
    {
        var visited = new List<BasicBlock>();
        var unique = new HashSet<BasicBlock>();
        compiler.fgVisitBlocksInTryAwareLoopAwareRPO(dfs, regions, loops, block =>
        {
            Assert.That(dfs.Contains(block), Is.True);
            Assert.That(unique.Add(block), Is.True);
            visited.Add(block);
            callback?.Invoke(block);
        });
        Assert.That(visited.Count, Is.EqualTo(dfs.PostOrderCount));
        return visited;
    }

    private static BasicBlock[] ConflictGraph(Compiler compiler)
    {
        var blocks = Blocks(compiler, 6);
        Jump(blocks[0], blocks[1]);
        Branch(blocks[1], blocks[2], blocks[3]);
        Jump(blocks[2], blocks[4]);
        Jump(blocks[3], blocks[1]);
        blocks[1].TryIndex = 0;
        blocks[2].TryIndex = 0;
        return blocks;
    }

    private static FlowGraphDfsTree ConflictDfs(Compiler compiler, BasicBlock[] blocks)
    {
        BasicBlock[] preorder = [blocks[0], blocks[1], blocks[2], blocks[4], blocks[3]];
        BasicBlock[] reversePostorder = [blocks[0], blocks[1], blocks[3], blocks[2], blocks[4]];
        return Dfs(compiler, reversePostorder, preorder, hasCycle: true);
    }

    private static BasicBlock[] Blocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = new BasicBlock(null, null) { bbNum = (index + 1) * 3, bbRefs = index == 0 ? 1 : 0 };
            blocks[index].SetKindAndTargetEdge(BBJ_RETURN, null);
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgBBcount = count;
        return blocks;
    }

    private static FlowEdge Connect(BasicBlock source, BasicBlock target)
    {
        var edge = new FlowEdge(source, target, target.bbPreds);
        edge.incrementDupCount();
        target.bbPreds = edge;
        target.bbRefs++;
        return edge;
    }

    private static void Jump(BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(source, target));
    }

    private static void Branch(BasicBlock source, BasicBlock taken, BasicBlock notTaken)
    {
        source.SetCond(Connect(source, taken), Connect(source, notTaken));
    }

    private static FlowGraphDfsTree Dfs(
        Compiler compiler, BasicBlock[] reversePostorder, BasicBlock[] preorder, bool hasCycle)
    {
        for (var index = 0; index < preorder.Length; index++)
        {
            preorder[index].bbPreorderNum = index;
        }

        var postorder = (BasicBlock[])reversePostorder.Clone();
        Array.Reverse(postorder);
        for (var index = 0; index < postorder.Length; index++)
        {
            postorder[index].bbPostorderNum = index;
        }

        return new FlowGraphDfsTree(compiler, postorder, postorder.Length, hasCycle, profileAware: false);
    }

    private static EHblkDsc Clause(
        ushort id, BasicBlock first, BasicBlock last, BasicBlock handler, EHHandlerType type,
        ushort parent = NO_ENCLOSING_INDEX)
    {
        return new EHblkDsc
        {
            ebdID = id,
            ebdTryBeg = first,
            ebdTryLast = last,
            ebdHndBeg = handler,
            ebdHndLast = handler,
            ebdHandlerType = type,
            ebdEnclosingTryIndex = parent,
            ebdEnclosingHndIndex = NO_ENCLOSING_INDEX,
        };
    }

    private static void SetClauses(Compiler compiler, ushort nextId, params EHblkDsc[] clauses)
    {
        compiler.compHndBBtab = clauses;
        compiler.compHndBBtabCount = (ushort)clauses.Length;
        compiler.compEHID = nextId;
        for (ushort index = 0; index < clauses.Length; index++)
        {
            clauses[index].ebdHndBeg.HndIndex = index;
        }
    }
}
