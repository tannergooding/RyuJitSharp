// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class BlockToNaturalLoopMap
{
    private readonly struct AssignLoopIndexVisitor : IBasicBlockVisitor<AssignLoopIndexVisitor>
    {
        private readonly int[] _indices;
        private readonly int _loopIndex;

        public AssignLoopIndexVisitor(int[] indices, int loopIndex)
        {
            _indices = indices;
            _loopIndex = loopIndex;
        }

        public readonly BasicBlockVisit Visit(BasicBlock block)
        {
            _indices[block.bbPostorderNum] = _loopIndex;
            return BasicBlockVisit.Continue;
        }
    }

    private readonly FlowGraphNaturalLoops _loops;
    private readonly int[] _indices;

    private BlockToNaturalLoopMap(FlowGraphNaturalLoops loops, int[] indices)
    {
        _loops = loops;
        _indices = indices;
    }

    public FlowGraphNaturalLoop? GetLoop(BasicBlock block)
    {
        if (!_loops.DfsTree.Contains(block))
        {
            return null;
        }

        var index = _indices[block.bbPostorderNum];
        return index == -1 ? null : _loops.GetLoopByIndex(index);
    }

    public static BlockToNaturalLoopMap Build(FlowGraphNaturalLoops loops)
    {
        var indices = new int[loops.DfsTree.PostOrderCount];
        indices.AsSpan().Fill(-1);

        // Reverse postorder visits inner loops last, overwriting enclosing-loop indices.
        foreach (var loop in loops.InReversePostOrder())
        {
            var visitor = new AssignLoopIndexVisitor(indices, loop.Index);
            _ = loop.VisitLoopBlocks(ref visitor);
        }

        return new(loops, indices);
    }

    public void Dump()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        var dfs = _loops.DfsTree;
        var count = dfs.PostOrderCount;
        jitprintf($"Block -> natural loop map: {count} blocks\n");
        if (count > 0)
        {
            jitprintf("block : loop index\n");
            for (var index = 0; index < count; index++)
            {
                jitprintf($"{FMT_BB(dfs.GetPostOrder(index).bbNum)} : ");
                if (_indices[index] != -1)
                {
                    jitprintf($"L{_indices[index]:D2}");
                }

                jitprintf("\n");
            }
        }
    }
}
