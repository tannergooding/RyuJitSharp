// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

// A strongly connected set of blocks dominated by its header, including exceptional flow.
public sealed class FlowGraphNaturalLoop
{
    internal readonly FlowGraphDfsTree _dfsTree;
    internal readonly BasicBlock _header;
    internal FlowGraphNaturalLoop? _parent;
    internal FlowGraphNaturalLoop? _child;
    internal FlowGraphNaturalLoop? _sibling;
    internal BitVec _blocks = BitVecOps.UninitVal();
    internal int _blocksSize;
    internal readonly List<FlowEdge> _backEdges = [];
    internal readonly List<FlowEdge> _entryEdges = [];
    internal readonly List<FlowEdge> _exitEdges = [];
    internal int _index;
    internal bool _containsImproperHeader;

    internal FlowGraphNaturalLoop(FlowGraphDfsTree dfsTree, BasicBlock header)
    {
        _dfsTree = dfsTree;
        _header = header;
    }

    public BasicBlock Header => _header;
    public FlowGraphDfsTree DfsTree => _dfsTree;
    public FlowGraphNaturalLoop? Parent => _parent;
    public FlowGraphNaturalLoop? Child => _child;
    public FlowGraphNaturalLoop? Sibling => _sibling;
    public int Index => _index;
    public bool ContainsImproperHeader => _containsImproperHeader;
    public ReadOnlySpan<FlowEdge> BackEdges => CollectionsMarshal.AsSpan(_backEdges);
    public ReadOnlySpan<FlowEdge> EntryEdges => CollectionsMarshal.AsSpan(_entryEdges);
    public ReadOnlySpan<FlowEdge> ExitEdges => CollectionsMarshal.AsSpan(_exitEdges);

    public FlowEdge BackEdge(int index) => _backEdges[index];
    public FlowEdge EntryEdge(int index) => _entryEdges[index];
    public FlowEdge ExitEdge(int index) => _exitEdges[index];

    public BasicBlock? GetPreheader()
    {
        if (_entryEdges.Count != 1)
        {
            return null;
        }
        var preheader = _entryEdges[0].SourceBlock;

        return preheader.Kind is BBJ_ALWAYS ? preheader : null;
    }

    public void SetEntryEdge(FlowEdge entryEdge)
    {
        _entryEdges.Clear();
        _entryEdges.Add(entryEdge);
    }

    public int GetDepth()
    {
        var depth = 0;
        for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            depth++;
        }

        return depth;
    }

    internal int LoopBlockBitVecIndex(BasicBlock block)
    {
        assert(_dfsTree.Contains(block));
        // The header dominates the loop, so its reverse-postorder index is the base.
        var index = _header.bbPostorderNum - block.bbPostorderNum;
        assert((uint)index < (uint)_blocksSize);

        return index;
    }

    private bool TryGetLoopBlockBitVecIndex(BasicBlock block, out int index)
    {
        index = _header.bbPostorderNum - block.bbPostorderNum;

        return unchecked((uint)index) < (uint)_blocksSize;
    }

    internal BitVecTraits LoopBlockTraits() => new BitVecTraits(_dfsTree.GetCompiler(), _blocksSize);

    public bool ContainsBlock(BasicBlock block)
    {
        if (!_dfsTree.Contains(block) || !TryGetLoopBlockBitVecIndex(block, out var index))
        {
            return false;
        }

        return BitVecOps.IsMember(LoopBlockTraits(), _blocks, index);
    }

    public bool ContainsLoop(FlowGraphNaturalLoop childLoop) => ContainsBlock(childLoop.Header);

    public int NumLoopBlocks() => (int)BitVecOps.Count(LoopBlockTraits(), _blocks);

    public BasicBlockVisit VisitLoopBlocksReversePostOrder(Func<BasicBlock, BasicBlockVisit> func)
    {
        var result = BitVecOps.VisitBits(LoopBlockTraits(), _blocks, index => {
            var poIndex = _header.bbPostorderNum - index;
            assert((uint)poIndex < (uint)_dfsTree.PostOrderCount);
            return func(_dfsTree.GetPostOrder(poIndex)) is BasicBlockVisit.Continue;
        });

        return result ? BasicBlockVisit.Continue : BasicBlockVisit.Abort;
    }

    public BasicBlockVisit VisitLoopBlocksPostOrder(Func<BasicBlock, BasicBlockVisit> func)
    {
        var result = BitVecOps.VisitBitsReverse(LoopBlockTraits(), _blocks, index => {
            var poIndex = _header.bbPostorderNum - index;
            assert((uint)poIndex < (uint)_dfsTree.PostOrderCount);
            return func(_dfsTree.GetPostOrder(poIndex)) is BasicBlockVisit.Continue;
        });

        return result ? BasicBlockVisit.Continue : BasicBlockVisit.Abort;
    }

    public BasicBlockVisit VisitLoopBlocks(Func<BasicBlock, BasicBlockVisit> func) => VisitLoopBlocksReversePostOrder(func);

    public BasicBlockVisit VisitRegularExitBlocks(Func<BasicBlock, BasicBlockVisit> func)
    {
        var comp = _dfsTree.GetCompiler();
        var traits = _dfsTree.PostOrderTraits();
        var visited = BitVecOps.MakeEmpty(traits);
        foreach (var edge in _exitEdges)
        {
            var exit = edge.DestinationBlock;
            assert(_dfsTree.Contains(exit) && !ContainsBlock(exit));
            if (!comp.bbIsHandlerBeg(exit) && BitVecOps.TryAddElemD(traits, visited, exit.bbPostorderNum) &&
                (func(exit) is BasicBlockVisit.Abort))
            {
                return BasicBlockVisit.Abort;
            }
        }

        return BasicBlockVisit.Continue;
    }

    public BasicBlock GetLexicallyTopMostBlock()
    {
        var top = _dfsTree.GetCompiler().fgFirstBB;
        assert(top is not null);
        while (!ContainsBlock(top))
        {
            top = top.Next;
            assert(top is not null);
        }

        return top;
    }

    public BasicBlock GetLexicallyBottomMostBlock()
    {
        var bottom = _dfsTree.GetCompiler().fgLastBB;
        assert(bottom is not null);
        while (!ContainsBlock(bottom))
        {
            bottom = bottom.Prev;
            assert(bottom is not null);
        }

        return bottom;
    }

#if DEBUG
    public static void Dump(FlowGraphNaturalLoop? loop)
    {
        if (loop is null)
        {
            jitprintf("loop is nullptr");
            return;
        }

        jitprintf($"L{loop.Index:D2} header: {FMT_BB(loop.Header.bbNum)}");
        if (loop.Parent is FlowGraphNaturalLoop parent)
        {
            jitprintf($" parent: L{parent.Index:D2}");
        }
        var numBlocks = loop.NumLoopBlocks();
        jitprintf($"\n  Members ({numBlocks}): ");
        if (numBlocks == 0)
        {
            jitprintf("NONE?");
        }
        else if (numBlocks == 1)
        {
            jitprintf(FMT_BB(loop.Header.bbNum));
        }
        else
        {
            var lexicalTop = loop.GetLexicallyTopMostBlock();
            var lexicalBottom = loop.GetLexicallyBottomMostBlock();
            var lexicalEnd = lexicalBottom.Next;
            var numLexicalBlocks = 0;
            var lexicallyDense = true;
            for (var block = lexicalTop; (block is not null) && (block != lexicalEnd); block = block.Next)
            {
                if (!loop.ContainsBlock(block))
                {
                    lexicallyDense = false;
                }
                else
                {
                    numLexicalBlocks++;
                }
            }

            var lexicalRangeContainsAllLoopBlocks = numBlocks == numLexicalBlocks;
            if (lexicallyDense && lexicalRangeContainsAllLoopBlocks)
            {
                jitprintf($"[{FMT_BB(lexicalTop.bbNum)}..{FMT_BB(lexicalBottom.bbNum)}]");
            }
            else if (lexicalRangeContainsAllLoopBlocks)
            {
                BasicBlock? firstInRange = null;
                BasicBlock? lastInRange = null;
                var first = true;
                void PrintRange()
                {
                    if (firstInRange is null)
                    {
                        return;
                    }
                    if (!first)
                    {
                        jitprintf(";");
                    }
                    if (firstInRange == lastInRange)
                    {
                        jitprintf(FMT_BB(firstInRange.bbNum));
                    }
                    else
                    {
                        assert(lastInRange is not null);
                        jitprintf($"[{FMT_BB(firstInRange.bbNum)}..{FMT_BB(lastInRange.bbNum)}]");
                    }
                    firstInRange = lastInRange = null;
                    first = false;
                }

                for (var block = lexicalTop; block != lexicalEnd; block = block.Next)
                {
                    assert(block is not null);
                    if (!loop.ContainsBlock(block))
                    {
                        PrintRange();
                    }
                    else
                    {
                        firstInRange ??= block;
                        lastInRange = block;
                    }
                }
                PrintRange();
            }
            else
            {
                var first = true;
                _ = loop.VisitLoopBlocksReversePostOrder(block => {
                    jitprintf($"{(first ? "" : ";")}{FMT_BB(block.bbNum)}");
                    first = false;
                    return BasicBlockVisit.Continue;
                });
                jitprintf($"\n  Lexical top: {FMT_BB(lexicalTop.bbNum)}");
                jitprintf($"\n  Lexical bottom: {FMT_BB(lexicalBottom.bbNum)}");
            }
        }

        jitprintf("\n  Entry: ");
        if (loop._entryEdges.Count == 0)
        {
            jitprintf("NONE");
        }
        else
        {
            var first = true;
            foreach (var edge in loop._entryEdges)
            {
                jitprintf($"{(first ? "" : "; ")}{FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(loop.Header.bbNum)}");
                first = false;
            }
        }
        jitprintf("\n  Exit: ");
        if (loop._exitEdges.Count == 0)
        {
            jitprintf("NONE");
        }
        else
        {
            var first = true;
            foreach (var edge in loop._exitEdges)
            {
                jitprintf($"{(first ? "" : "; ")}{FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(edge.DestinationBlock.bbNum)}");
                first = false;
            }
        }
        jitprintf("\n  Back: ");
        if (loop._backEdges.Count == 0)
        {
            jitprintf("NONE");
        }
        else
        {
            var first = true;
            foreach (var edge in loop._backEdges)
            {
                jitprintf($"{(first ? "" : "; ")}{FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(loop.Header.bbNum)}");
                first = false;
            }
        }
        jitprintf("\n");
    }
#endif
}
