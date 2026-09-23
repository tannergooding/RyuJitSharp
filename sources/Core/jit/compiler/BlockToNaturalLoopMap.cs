// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class BlockToNaturalLoopMap
{
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
            _ = loop.VisitLoopBlocks(block => {
                indices[block.bbPostorderNum] = loop.Index;
                return BasicBlockVisit.Continue;
            });
        }

        return new(loops, indices);
    }

#if DEBUG
    public void Dump()
    {
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
#endif
}
