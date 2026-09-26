// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed class FlowGraphDominanceFrontiers
{
    private readonly FlowGraphDominatorTree _domTree;
    private readonly Dictionary<BasicBlock, List<BasicBlock>> _map = [];
    private readonly BitVecTraits _poTraits;
    private readonly nint[] _visited;

    private FlowGraphDominanceFrontiers(FlowGraphDominatorTree domTree)
    {
        _domTree = domTree;
        _poTraits = domTree.GetDfsTree().PostOrderTraits();
        _visited = BitVecOps.MakeEmpty(_poTraits);
    }

    public FlowGraphDominatorTree GetDomTree() => _domTree;

    public static FlowGraphDominanceFrontiers Build(FlowGraphDominatorTree domTree)
    {
        var dfsTree = domTree.GetDfsTree();
        var compiler = dfsTree.GetCompiler();
        var result = new FlowGraphDominanceFrontiers(domTree);

        for (var i = 0; i < dfsTree.PostOrderCount; i++)
        {
            var block = dfsTree.GetPostOrder(i);
            var predecessors = compiler.BlockPredsWithEH(block);

            // A sole predecessor dominates the block. Handler entries are the
            // exception: they remain in the frontiers of their enclosed blocks.
            if (!compiler.bbIsHandlerBeg(block) && ((predecessors is null) || (predecessors.NextPredEdge is null)))
            {
                continue;
            }

            for (var edge = predecessors; edge is not null; edge = edge.NextPredEdge)
            {
                var predecessor = edge.SourceBlock;

                if (!dfsTree.Contains(predecessor))
                {
                    continue;
                }

                // Each ancestor of a predecessor, up to but excluding the
                // block's immediate dominator, has this block in its frontier.
                for (var ancestor = predecessor; (ancestor is not null) && (ancestor != block.bbIDom); ancestor = ancestor.bbIDom)
                {
                    if (!result._map.TryGetValue(ancestor, out var frontier))
                    {
                        frontier = [];
                        result._map.Add(ancestor, frontier);
                    }

                    // All contributions of this block are consecutive.
                    if ((frontier.Count == 0) || (frontier[^1] != block))
                    {
                        frontier.Add(block);
                    }
                }
            }
        }

        return result;
    }

    public void ComputeIteratedDominanceFrontier(BasicBlock block, List<BasicBlock> result)
    {
        assert(result.Count == 0);

        if (!_map.TryGetValue(block, out var frontier))
        {
            return;
        }

        _ = result.EnsureCapacity(frontier.Count);
        BitVecOps.ClearD(_poTraits, _visited);

        foreach (var member in frontier)
        {
            BitVecOps.AddElemD(_poTraits, _visited, member.bbPostorderNum);
            result.Add(member);
        }

        // Index the growing vector: newly discovered frontiers themselves
        // induce phi definitions, without needing a separate worklist.
        for (var index = 0; index < result.Count; index++)
        {
            if (!_map.TryGetValue(result[index], out var nextFrontier))
            {
                continue;
            }

            foreach (var member in nextFrontier)
            {
                if (BitVecOps.TryAddElemD(_poTraits, _visited, member.bbPostorderNum))
                {
                    result.Add(member);
                }
            }
        }
    }

#if DEBUG
    public void Dump()
    {
        jitprintf("DF:\n");
        var dfsTree = _domTree.GetDfsTree();

        for (var i = 0; i < dfsTree.PostOrderCount; i++)
        {
            var block = dfsTree.GetPostOrder(i);
            jitprintf($"Block {FMT_BB(block.bbNum)} := {{");

            if (_map.TryGetValue(block, out var frontier))
            {
                for (var index = 0; index < frontier.Count; index++)
                {
                    jitprintf($"{(index == 0 ? "" : ",")}{FMT_BB(frontier[index].bbNum)}");
                }
            }

            jitprintf("}\n");
        }
    }
#endif
}
