// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed class FlowGraphNaturalLoops
{
    private readonly FlowGraphDfsTree _dfsTree;
    private readonly List<FlowGraphNaturalLoop> _loops = [];
    private int _improperLoopHeaders;

    private FlowGraphNaturalLoops(FlowGraphDfsTree dfsTree)
    {
        _dfsTree = dfsTree;
    }

    public FlowGraphDfsTree DfsTree => _dfsTree;
    public int NumLoops => _loops.Count;
    public int ImproperLoopHeaders => _improperLoopHeaders;
    public bool IsForWasm => _dfsTree.IsForWasm;

    public FlowGraphNaturalLoop GetLoopByIndex(int index) => _loops[index];

    public FlowGraphNaturalLoop? GetLoopByHeader(BasicBlock block)
    {
        if (!_dfsTree.Contains(block))
        {
            return null;
        }

        var min = 0;
        var max = NumLoops;
        while (min < max)
        {
            var mid = min + ((max - min) / 2);
            var loop = _loops[mid];
            var header = loop.Header;
            if (header == block)
            {
                return loop;
            }
            if (header.bbPostorderNum < block.bbPostorderNum)
            {
                max = mid;
            }
            else
            {
                assert(header.bbPostorderNum > block.bbPostorderNum);
                min = mid + 1;
            }
        }

        return null;
    }

    public bool IsLoopBackEdge(FlowEdge edge)
    {
        foreach (var loop in _loops)
        {
            foreach (var backEdge in loop.BackEdges)
            {
                if (backEdge == edge)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsLoopExitEdge(FlowEdge edge)
    {
        foreach (var loop in _loops)
        {
            foreach (var exitEdge in loop.ExitEdges)
            {
                if (exitEdge == edge)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public ReadOnlySpan<FlowGraphNaturalLoop> InReversePostOrder() => CollectionsMarshal.AsSpan(_loops);

    public IEnumerable<FlowGraphNaturalLoop> InPostOrder()
    {
        for (var i = _loops.Count - 1; i >= 0; i--)
        {
            yield return _loops[i];
        }
    }

    public static FlowGraphNaturalLoops Find(FlowGraphDfsTree dfsTree)
    {
        var comp = dfsTree.GetCompiler();
        comp._blockToEHPreds = null;
#if DEBUG
        JITDUMP("Identifying loops in DFS tree with following reverse post order:\n");
        JITDUMP("RPO -> BB [pre, post]\n");
        for (var i = dfsTree.PostOrderCount; i != 0; i--)
        {
            var block = dfsTree.GetPostOrder(i - 1);
            JITDUMP($"{dfsTree.PostOrderCount - i:D2} -> {FMT_BB(block.bbNum)}[{block.bbPreorderNum}, {block.bbPostorderNum}]\n");
        }
#endif
        var loops = new FlowGraphNaturalLoops(dfsTree);
        if (!dfsTree.HasCycle)
        {
            JITDUMP("Flow graph has no cycles; skipping identification of natural loops\n");
            return loops;
        }

        var worklist = new Stack<BasicBlock>();
        for (var i = dfsTree.PostOrderCount; i != 0; i--)
        {
            var header = dfsTree.GetPostOrder(i - 1);
            FlowGraphNaturalLoop? loop = null;
            foreach (var predEdge in header.PredEdges)
            {
                var predBlock = predEdge.SourceBlock;
                if (dfsTree.Contains(predBlock) && dfsTree.IsAncestor(header, predBlock))
                {
                    if (loop is null)
                    {
                        loop = new FlowGraphNaturalLoop(dfsTree, header);
                        JITDUMP("\n");
                    }
                    JITDUMP($"{FMT_BB(predBlock.bbNum)} -> {FMT_BB(header.bbNum)} is a backedge\n");
                    loop._backEdges.Add(predEdge);
                }
            }
            if (loop is null)
            {
                continue;
            }

            JITDUMP($"{FMT_BB(header.bbNum)} is the header of a DFS loop with {loop._backEdges.Count} back edges\n");
            loop._blocksSize = header.bbPostorderNum + 1;
            var loopTraits = loop.LoopBlockTraits();
            loop._blocks = BitVecOps.MakeEmpty(loopTraits);
            if (!loops.FindNaturalLoopBlocks(loop, worklist) || !IsLoopCanonicalizable(loop))
            {
                loops._improperLoopHeaders++;
                foreach (var otherLoop in loops.InPostOrder())
                {
                    if (otherLoop.ContainsBlock(header))
                    {
                        JITDUMP($"Noting that L{loop.Index:D2} contains an improper loop header\n");
                        otherLoop._containsImproperHeader = true;
                    }
                }
                continue;
            }

            JITDUMP($"Loop has {BitVecOps.Count(loopTraits, loop._blocks)} blocks\n");
            _ = loop.VisitLoopBlocksReversePostOrder(loopBlock => {
                _ = loopBlock.VisitRegularSuccs(comp, succBlock => {
                    if (!loop.ContainsBlock(succBlock))
                    {
                        var exitEdge = comp.fgGetPredForBlock(succBlock, loopBlock);
                        assert(exitEdge is not null);
                        JITDUMP($"{FMT_BB(loopBlock.bbNum)} -> {FMT_BB(succBlock.bbNum)} is an exit edge\n");
                        loop._exitEdges.Add(exitEdge);
                    }
                    return BasicBlockVisit.Continue;
                });
                return BasicBlockVisit.Continue;
            });

            foreach (var predEdge in header.PredEdges)
            {
                var predBlock = predEdge.SourceBlock;
                if (dfsTree.Contains(predBlock) && !dfsTree.IsAncestor(header, predBlock))
                {
                    JITDUMP($"{FMT_BB(predBlock.bbNum)} -> {FMT_BB(header.bbNum)} is an entry edge\n");
                    loop._entryEdges.Add(predEdge);
                }
            }

            // Outer loops precede inner loops; the most recent containing loop is the parent.
            foreach (var otherLoop in loops.InPostOrder())
            {
                if (otherLoop.ContainsBlock(header))
                {
                    loop._parent = otherLoop;
                    JITDUMP($"Nested within loop starting at {FMT_BB(otherLoop.Header.bbNum)}\n");
                    break;
                }
            }
#if DEBUG
            foreach (var otherLoop in loops.InPostOrder())
            {
                var containsHeader = otherLoop.ContainsBlock(header);
                _ = loop.VisitLoopBlocks(loopBlock => {
                    assert(otherLoop.ContainsBlock(loopBlock) == containsHeader);
                    return BasicBlockVisit.Continue;
                });
            }
#endif
            loop._index = loops._loops.Count;
            loops._loops.Add(loop);
            JITDUMP($"Added loop L{loop.Index:D2} with header {FMT_BB(loop.Header.bbNum)}\n");
        }

        // Prepending in postorder leaves siblings in reverse postorder.
        foreach (var loop in loops.InPostOrder())
        {
            if (loop.Parent is FlowGraphNaturalLoop parent)
            {
                loop._sibling = parent._child;
                parent._child = loop;
            }
        }
#if DEBUG
        if (loops.NumLoops > 0)
        {
            JITDUMP($"\nFound {loops.NumLoops} loops\n");
        }
        if (loops._improperLoopHeaders > 0)
        {
            JITDUMP($"Rejected {loops._improperLoopHeaders} loop headers\n");
        }
        if (comp.verbose)
        {
            Dump(loops);
        }
#endif
        return loops;
    }

    private bool FindNaturalLoopBlocks(FlowGraphNaturalLoop loop, Stack<BasicBlock> worklist)
    {
        var dfsTree = loop.DfsTree;
        var comp = dfsTree.GetCompiler();
        var traits = loop.LoopBlockTraits();
        BitVecOps.AddElemD(traits, loop._blocks, 0);
        worklist.Clear();
        foreach (var backEdge in loop.BackEdges)
        {
            var source = backEdge.SourceBlock;
            if (source == loop.Header)
            {
                continue;
            }
            assert(!BitVecOps.IsMember(traits, loop._blocks, loop.LoopBlockBitVecIndex(source)));
            worklist.Push(source);
            BitVecOps.AddElemD(traits, loop._blocks, loop.LoopBlockBitVecIndex(source));
        }

        while (worklist.Count > 0)
        {
            var loopBlock = worklist.Pop();
            if (IsForWasm && loopBlock.isBBCallFinallyPairTail)
            {
                var callfinally = loopBlock.Prev;
                assert(callfinally is not null);
                if (BitVecOps.TryAddElemD(traits, loop._blocks, loop.LoopBlockBitVecIndex(callfinally)))
                {
                    worklist.Push(callfinally);
                }
                continue;
            }

            for (var predEdge = comp.BlockPredsWithEH(loopBlock); predEdge is not null; predEdge = predEdge.NextPredEdge)
            {
                var pred = predEdge.SourceBlock;
                if (!dfsTree.Contains(pred))
                {
                    continue;
                }
                if (IsForWasm && (pred.Kind is BBJ_EHFINALLYRET or BBJ_EHFAULTRET or BBJ_EHFILTERRET or BBJ_EHCATCHRET))
                {
                    continue;
                }
                if (!dfsTree.IsAncestor(loop.Header, pred))
                {
                    JITDUMP($"Loop is not natural; witness {FMT_BB(pred.bbNum)} -> {FMT_BB(loopBlock.bbNum)}\n");
                    return false;
                }
                if (BitVecOps.TryAddElemD(traits, loop._blocks, loop.LoopBlockBitVecIndex(pred)))
                {
                    worklist.Push(pred);
                }
            }
        }

        return true;
    }

    private static bool IsLoopCanonicalizable(FlowGraphNaturalLoop loop)
    {
        var comp = loop.DfsTree.GetCompiler();
        if (!comp.bbIsHandlerBeg(loop.Header))
        {
            return true;
        }
        foreach (var backEdge in loop.BackEdges)
        {
            if (backEdge.SourceBlock.Kind is BBJ_CALLFINALLY)
            {
                // A callfinally backedge cannot be redirected through a preheader.
                return false;
            }
        }

        return true;
    }

#if DEBUG
    public static void Dump(FlowGraphNaturalLoops? loops)
    {
        jitprintf("\n***************  Natural loop graph\n");
        if (loops is null)
        {
            jitprintf("loops is nullptr\n");
        }
        else if (loops.NumLoops == 0)
        {
            jitprintf("No loops\n");
        }
        else
        {
            foreach (var loop in loops.InReversePostOrder())
            {
                FlowGraphNaturalLoop.Dump(loop);
            }
        }
        jitprintf("\n");
    }
#endif
}
