// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowGraphDominanceTests
{
    [TestCase("single")]
    [TestCase("diamond")]
    [TestCase("loop")]
    [TestCase("irreducible")]
    [TestCase("nested")]
    [TestCase("unreachable")]
    [TestCase("duplicate")]
    public static void DominatorsAndOrderedIteratedFrontiersMatchSetDefinitions(string shape)
    {
        int[][] successors = shape switch
        {
            "single" => [[]],
            "diamond" => [[1, 2], [3], [3], []],
            "loop" => [[1], [2, 3], [1], []],
            "irreducible" => [[1, 2], [3], [3], [1, 2]],
            "nested" => [[1], [2, 5], [3, 4], [2], [1], []],
            "unreachable" => [[1], [2], [], [1]],
            "duplicate" => [[1, 2], [3, 3], [3], []],
            _ => throw new ArgumentException("Unknown graph shape.", nameof(shape)),
        };

        WithCompiler(compiler =>
        {
            var blocks = CreateGraph(compiler, successors);
            var dfs = compiler.fgComputeDfs();
            var reachable = dfs.GetPostOrder().Take(dfs.PostOrderCount).ToArray();
            var expected = ComputeDominators(blocks[0], reachable);
            var tree = FlowGraphDominatorTree.Build(dfs);
            var frontiers = FlowGraphDominanceFrontiers.Build(tree);
            Assert.That(tree.GetDfsTree(), Is.SameAs(dfs));
            Assert.That(frontiers.GetDomTree(), Is.SameAs(tree));
            Assert.That(blocks[0].bbIDom, Is.Null);

            foreach (var block in reachable)
            {
                foreach (var candidate in reachable)
                {
                    Assert.That(tree.Dominates(candidate, block), Is.EqualTo(expected[block].Contains(candidate)),
                        $"Dominance of {candidate.bbNum} over {block.bbNum}");
                    var common = expected[block].Intersect(expected[candidate]).ToArray();
                    var nearest = common.Single(dom => common.All(other => expected[dom].Contains(other)));
                    Assert.That(tree.Intersect(block, candidate), Is.SameAs(nearest));
                }

                if (block != blocks[0])
                {
                    var strict = expected[block].Where(dom => dom != block).ToArray();
                    var parent = strict.Single(dom => strict.All(other => expected[dom].Contains(other)));
                    Assert.That(block.bbIDom, Is.SameAs(parent));
                }

                var actual = new List<BasicBlock>();
                frontiers.ComputeIteratedDominanceFrontier(block, actual);
                Assert.That(actual.Select(member => member.bbNum),
                    Is.EqualTo(ComputeIdf(block, reachable, expected).Select(member => member.bbNum)));

                // Reuse the visited storage after querying a different block.
                var otherResult = new List<BasicBlock>();
                frontiers.ComputeIteratedDominanceFrontier(blocks[0], otherResult);
                actual.Clear();
                frontiers.ComputeIteratedDominanceFrontier(block, actual);
                Assert.That(actual.Select(member => member.bbNum),
                    Is.EqualTo(ComputeIdf(block, reachable, expected).Select(member => member.bbNum)));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HandlerDominanceAddsTryEntryPredecessorsAndRebuildsBothCaches(bool filter)
    {
        WithCompiler(compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], [2], [], [4], [2]]);
            var entry = blocks[0];
            var tryBlock = blocks[1];
            var exit = blocks[2];
            var handler = blocks[4];
            var filterBlock = blocks[3];
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            if (filter)
            {
                filterBlock.HndIndex = 0;
            }
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = filter ? EH_HANDLER_FILTER : EH_HANDLER_CATCH,
                    ebdTryBeg = tryBlock,
                    ebdTryLast = tryBlock,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdFilter = filterBlock,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var target = filter ? filterBlock : handler;
            var ehPreds = compiler.BlockPredsWithEH(target);
            var dominancePreds = compiler.BlockDominancePreds(target);
            Assert.That(dominancePreds!.SourceBlock, Is.SameAs(entry));
            Assert.That(dominancePreds.NextPredEdge, Is.SameAs(ehPreds));
            Assert.That(compiler.BlockDominancePreds(target), Is.SameAs(dominancePreds));
            Assert.That(compiler.BlockDominancePreds(tryBlock), Is.SameAs(tryBlock.bbPreds));

            var ehCache = compiler.GetBlockToEHPreds();
            var dominanceCache = compiler.GetDominancePreds();
            var dfs = compiler.fgComputeDfs();
            var tree = FlowGraphDominatorTree.Build(dfs);
            Assert.That(compiler.GetBlockToEHPreds(), Is.Not.SameAs(ehCache));
            Assert.That(compiler.GetDominancePreds(), Is.Not.SameAs(dominanceCache));
            Assert.That(target.bbIDom, Is.SameAs(entry));
            Assert.That(tree.Dominates(tryBlock, target), Is.False);
            Assert.That(tree.Dominates(entry, exit), Is.True);

            var frontiers = FlowGraphDominanceFrontiers.Build(tree);
            var idf = new List<BasicBlock>();
            frontiers.ComputeIteratedDominanceFrontier(tryBlock, idf);
            Assert.That(idf.Select(member => member.bbNum), Does.Contain(target.bbNum));
            Assert.That(idf.Select(member => member.bbNum), Is.Unique);
        });
    }

    [Test]
    public static void VisitorPreservesReversePostorderSiblingsAndExitCallbacks()
    {
        WithCompiler(compiler =>
        {
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], []]);
            var tree = FlowGraphDominatorTree.Build(compiler.fgComputeDfs());
            var events = new List<string>();
            var visitor = new RecordingVisitor(compiler, events);
            visitor.WalkTree(tree);
            var siblings = blocks.Skip(1).OrderByDescending(block => block.bbPostorderNum);
            var expected = new List<string> { "begin", $"+{blocks[0].bbNum}" };

            foreach (var sibling in siblings)
            {
                expected.Add($"+{sibling.bbNum}");
                expected.Add($"-{sibling.bbNum}");
            }

            expected.Add($"-{blocks[0].bbNum}");
            expected.Add("end");
            Assert.That(events, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void DeepTreeWalkDoesNotUseTheCallStack()
    {
        WithCompiler(compiler =>
        {
            const int count = 20000;
            var blocks = new BasicBlock[count];
            var postOrder = new BasicBlock[count];

            for (var i = 0; i < count; i++)
            {
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                block.bbPostorderNum = count - i - 1;
                blocks[i] = block;
                postOrder[count - i - 1] = block;

                if (i > 0)
                {
                    blocks[i - 1].Next = block;
                    block.bbPreds = new FlowEdge(blocks[i - 1], block, null);
                }
            }

            compiler.fgFirstBB = blocks[0];
            compiler.fgLastBB = blocks[^1];
            var dfs = new FlowGraphDfsTree(compiler, postOrder, count, hasCycle: false, profileAware: false);
            var tree = FlowGraphDominatorTree.Build(dfs);
            var visitor = new CountingVisitor(compiler);
            visitor.WalkTree(tree);
            Assert.That(visitor.PreorderCount, Is.EqualTo(count));
            Assert.That(visitor.PostorderCount, Is.EqualTo(count));
            Assert.That(tree.Dominates(blocks[0], blocks[^1]), Is.True);
            Assert.That(tree.Dominates(blocks[^1], blocks[0]), Is.False);

            visitor = new CountingVisitor(compiler);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            visitor.WalkTree(tree);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(allocated, Is.Zero);
            Assert.That(visitor.PreorderCount, Is.EqualTo(count));
            Assert.That(visitor.PostorderCount, Is.EqualTo(count));
        });
    }

#if DEBUG
    [Test]
    public static void DiagnosticsPreserveNativeOrderAndSpacing()
    {
        WithCompiler(compiler =>
        {
            _ = CreateGraph(compiler, [[1, 2], [3], [3], []]);
            var dfs = compiler.fgComputeDfs();
            compiler.verbose = true;
            var text = CodeGenLifeTransitionTests.Capture(() =>
            {
                var tree = FlowGraphDominatorTree.Build(dfs);
                tree.Dump();
                FlowGraphDominanceFrontiers.Build(tree).Dump();
            });
            var expected = "After computing the dominance tree:\nBB01 : BB02 BB03 BB04\n\n" +
                "BB01 : BB02 BB03 BB04 \n\n" +
                "DF:\nBlock BB04 := {}\nBlock BB03 := {BB04}\nBlock BB02 := {BB04}\nBlock BB01 := {}\n";
            Assert.That(text, Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        });
    }
#endif

    private static Dictionary<BasicBlock, HashSet<BasicBlock>> ComputeDominators(BasicBlock root, BasicBlock[] blocks)
    {
        var result = blocks.ToDictionary(block => block, block => block == root
            ? (HashSet<BasicBlock>)[root] : [.. blocks]);
        bool changed;

        do
        {
            changed = false;

            foreach (var block in blocks.Where(block => block != root))
            {
                var next = new HashSet<BasicBlock>(blocks);

                foreach (var predecessor in block.PredBlocks.Where(result.ContainsKey))
                {
                    next.IntersectWith(result[predecessor]);
                }

                _ = next.Add(block);

                if (!next.SetEquals(result[block]))
                {
                    result[block] = next;
                    changed = true;
                }
            }
        }
        while (changed);

        return result;
    }

    private static List<BasicBlock> ComputeIdf(
        BasicBlock block, BasicBlock[] postOrder, Dictionary<BasicBlock, HashSet<BasicBlock>> dominators)
    {
        BasicBlock[] Frontier(BasicBlock candidate)
        {
            return [.. postOrder.Where(member =>
                ((member == candidate) || !dominators[member].Contains(candidate)) &&
                member.PredBlocks.Any(pred => dominators.TryGetValue(pred, out var doms) && doms.Contains(candidate)))];
        }

        var result = new List<BasicBlock>(Frontier(block));

        for (var index = 0; index < result.Count; index++)
        {
            foreach (var member in Frontier(result[index]))
            {
                if (!result.Contains(member))
                {
                    result.Add(member);
                }
            }
        }

        return result;
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

    private struct RecordingVisitor(Compiler compiler, List<string> events) : IDomTreeVisitor<RecordingVisitor>
    {
        public readonly void Begin() => events.Add("begin");

        public readonly void PreOrderVisit(BasicBlock block) => events.Add($"+{block.bbNum}");

        public readonly void PostOrderVisit(BasicBlock block) => events.Add($"-{block.bbNum}");

        public readonly void End() => events.Add("end");

        public void WalkTree(FlowGraphDominatorTree tree) => IDomTreeVisitor<RecordingVisitor>.WalkTree(ref this, compiler, tree);
    }

    private struct CountingVisitor(Compiler compiler) : IDomTreeVisitor<CountingVisitor>
    {
        public int PreorderCount;
        public int PostorderCount;

        public readonly void Begin()
        {
        }

        public void PreOrderVisit(BasicBlock block) => PreorderCount++;

        public void PostOrderVisit(BasicBlock block) => PostorderCount++;

        public readonly void End()
        {
        }

        public void WalkTree(FlowGraphDominatorTree tree) => IDomTreeVisitor<CountingVisitor>.WalkTree(ref this, compiler, tree);
    }
}
