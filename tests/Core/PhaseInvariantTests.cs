// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class PhaseInvariantTests
{
    [TestCase("valid")]
    [TestCase("missing")]
    [TestCase("try")]
    [TestCase("predecessor")]
    [TestCase("debug-external")]
    [TestCase("debug-internal")]
    public static void EntryBlockContract(string state)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            if (state == "missing")
            {
                compiler.fgFirstBB = null;
            }
            else if (state == "try")
            {
                block.TryIndex = 0;
            }
            else if (state == "predecessor")
            {
                block.bbPreds = new FlowEdge(block, block, null);
            }
            else if (state.StartsWith("debug-", StringComparison.Ordinal))
            {
                compiler.opts.compDbgCode = true;
                block.RemoveFlags(BBF_INTERNAL);
                if (state == "debug-internal")
                {
                    block.SetFlags(BBF_INTERNAL);
                }
            }

            var valid = state is "valid" or "debug-internal";
            Assert.That(() => compiler.fgDebugCheckInitBB(), valid ? Throws.Nothing : Throws.Exception);
        });
    }

    [TestCase("valid")]
    [TestCase("preorder")]
    [TestCase("postorder")]
    [TestCase("order")]
    [TestCase("count")]
    [TestCase("loop-owner")]
    [TestCase("dominator-owner")]
    [TestCase("frontier-owner")]
    [TestCase("reachability-owner")]
    [TestCase("missing-dfs")]
    public static void GraphAnnotationsAreCheckedWithoutRepair(string state)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var blocks = CreateGraph(compiler, [[1], []]);
            var dfs = compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(dfs);
            var dominators = compiler._domTree = FlowGraphDominatorTree.Build(dfs);
            compiler._domFrontiers = FlowGraphDominanceFrontiers.Build(dominators);
            compiler._reachabilitySets = BlockReachabilitySets.Build(dfs);
            var other = new FlowGraphDfsTree(compiler, dfs.GetPostOrder(), dfs.PostOrderCount, false, false);

            switch (state)
            {
                case "preorder":
                {
                    blocks[0].bbPreorderNum = 42;
                    break;
                }
                case "postorder":
                {
                    blocks[1].bbPostorderNum = 42;
                    break;
                }
                case "order":
                {
                    (dfs.GetPostOrder()[0], dfs.GetPostOrder()[1]) = (dfs.GetPostOrder()[1], dfs.GetPostOrder()[0]);
                    break;
                }
                case "count":
                {
                    compiler._dfsTree = new FlowGraphDfsTree(compiler, dfs.GetPostOrder(), 1, false, false);
                    break;
                }
                case "loop-owner":
                {
                    compiler._loops = FlowGraphNaturalLoops.Find(other);
                    break;
                }
                case "dominator-owner":
                {
                    compiler._domTree = FlowGraphDominatorTree.Build(other);
                    break;
                }
                case "frontier-owner":
                {
                    compiler._domFrontiers = FlowGraphDominanceFrontiers.Build(FlowGraphDominatorTree.Build(other));
                    break;
                }
                case "reachability-owner":
                {
                    compiler._reachabilitySets = BlockReachabilitySets.Build(other);
                    break;
                }
                case "missing-dfs":
                {
                    compiler._dfsTree = null;
                    break;
                }
            }

            var preorder = Array.ConvertAll(blocks, block => block.bbPreorderNum);
            var postorder = Array.ConvertAll(blocks, block => block.bbPostorderNum);
            Assert.That(() => compiler.fgDebugCheckFlowGraphAnnotations(), state == "valid" ? Throws.Nothing : Throws.Exception);
            Assert.That(Array.ConvertAll(blocks, block => block.bbPreorderNum), Is.EqualTo(preorder));
            Assert.That(Array.ConvertAll(blocks, block => block.bbPostorderNum), Is.EqualTo(postorder));
        });
    }

    [Test]
    public static void AnnotationWalkPreservesProfileOrderAndExtraRoots(
        [Values] bool profile, [Values("ordinary", "osr", "return")] string root)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var blocks = CreateGraph(compiler, [[1, 2], [], [], []]);
            blocks[0].TrueEdge.Likelihood = 0.1;
            blocks[0].FalseEdge.Likelihood = 0.9;
            if (root == "osr")
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
                compiler.fgEntryBB = blocks[3];
            }
            else if (root == "return")
            {
                compiler.genReturnBB = blocks[3];
            }

            compiler._dfsTree = compiler.fgComputeDfs(profile);

            compiler.fgDebugCheckFlowGraphAnnotations();

            Assert.That(compiler._dfsTree.PostOrderCount, Is.EqualTo(root == "ordinary" ? 3 : 4));
            Assert.That(compiler._dfsTree.GetPostOrder(0), Is.SameAs(blocks[profile ? 1 : 2]));
        });
    }

    [Test]
    public static void UniquenessCoversEachIrRepresentation(
        [Values("tree", "locals", "threaded", "lir")] string representation, [Values] bool duplicate)
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var second = compiler.gtNewLclvNode(TYP_INT, 1);
            if (representation == "lir")
            {
                block.SetFlags(BBF_IS_LIR);
                block.InsertAtEnd(first);
                block.InsertAtEnd(second);
            }
            else
            {
                var statement = new Statement(new GenTreeOp(GT_ADD, TYP_INT, first, second), 0);
                compiler.fgInsertStmtAtEnd(block, statement);
                compiler.fgNodeThreading = representation switch {
                    "threaded" => NodeThreading.AllTrees,
                    "locals" => NodeThreading.AllLocals,
                    _ => NodeThreading.None,
                };
                if (representation == "threaded")
                {
                    compiler.fgSetStmtSeq(statement);
                }
                else if (representation == "locals")
                {
                    compiler.fgSequenceLocals(statement);
                }
            }

            compiler.fgDebugCheckNodesUniqueness();
            if (duplicate)
            {
                TreeId(second) = first.TreeId;
            }

            compiler.verbose = true;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckNodesUniqueness(), duplicate ? Throws.Exception : Throws.Nothing));
            Assert.That(output, Is.EqualTo(duplicate
                ? $"Duplicate gtTreeID was found: {first.TreeId}{Environment.NewLine}" : ""));
        });
    }

    [Test]
    public static void LinkedLocalsFollowExecutionOrderAndRejectBrokenLists(
        [Values] bool reverse, [Values("valid", "head", "tail", "prev", "order")] string state)
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var block = CreateGraph(compiler, [[]])[0];
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            var expression = new GenTreeOp(GT_ADD, TYP_INT, left, right);
            if (reverse)
            {
                expression.Flags |= GTF_REVERSE_OPS;
            }

            var statement = new Statement(expression, 0);
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.fgNodeThreading = NodeThreading.AllLocals;
            compiler.fgSequenceLocals(statement);
            compiler.fgDebugCheckLinkedLocals();
            var first = statement.TreeListBegin!;
            var last = statement.TreeListEnd!;

            switch (state)
            {
                case "head":
                {
                    statement.TreeListBegin = null;
                    break;
                }
                case "tail":
                {
                    statement.TreeListEnd = first;
                    break;
                }
                case "prev":
                {
                    last.Prev = null;
                    break;
                }
                case "order":
                {
                    statement.TreeListBegin = last;
                    statement.TreeListEnd = first;
                    last.Prev = null;
                    last.Next = first;
                    first.Prev = last;
                    first.Next = null;
                    break;
                }
            }

            Assert.That(() => compiler.fgDebugCheckLinkedLocals(), state == "valid" ? Throws.Nothing : Throws.Exception);
        });
    }

    [TestCase("valid")]
    [TestCase("entry-count")]
    [TestCase("entry-kind")]
    [TestCase("backedges")]
    [TestCase("exit-predecessor")]
    public static void CanonicalLoopsRejectInvalidBoundaries(string state)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            _ = CreateGraph(compiler, [[1], [2, 3], [1], []]);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler.optFindLoops();
            compiler.fgDebugCheckLoops();
            var loop = compiler._loops!.GetLoopByIndex(0);

            switch (state)
            {
                case "entry-count":
                {
                    loop._entryEdges.Add(loop.EntryEdge(0));
                    break;
                }
                case "entry-kind":
                {
                    loop.EntryEdge(0).SourceBlock.SetKindAndTargetEdge(BBJ_RETURN, null);
                    break;
                }
                case "backedges":
                {
                    loop._backEdges.Clear();
                    break;
                }
                case "exit-predecessor":
                {
                    var exit = loop.ExitEdge(0).DestinationBlock;
                    exit.bbPreds = new FlowEdge(loop.EntryEdge(0).SourceBlock, exit, exit.bbPreds);
                    break;
                }
            }

            Assert.That(() => compiler.fgDebugCheckLoops(), state == "valid" ? Throws.Nothing : Throws.Exception);
        });
    }

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
        for (var i = 0; i < blocks.Length; i++)
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_treeId")]
    private static extern ref int TreeId(GenTree tree);
}
#endif
