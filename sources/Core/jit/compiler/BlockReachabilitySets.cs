// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class BlockReachabilitySets
{
    private readonly FlowGraphDfsTree _dfsTree;
    private readonly BitVec[] _reachabilitySets;

    private BlockReachabilitySets(FlowGraphDfsTree dfsTree, BitVec[] reachabilitySets)
    {
        _dfsTree = dfsTree;
        _reachabilitySets = reachabilitySets;
    }

    public FlowGraphDfsTree GetDfsTree() => _dfsTree;

    public static BlockReachabilitySets Build(FlowGraphDfsTree dfsTree)
    {
        // Dense postorder bitsets retain the native regular-flow, O(n^2) reachability representation.
        var postOrderTraits = dfsTree.PostOrderTraits();
        var count = dfsTree.PostOrderCount;
        var sets = new BitVec[count];

        for (var i = 0; i < count; i++)
        {
            sets[i] = BitVecOps.MakeSingleton(postOrderTraits, i);
        }

        bool change;
        do
        {
            change = false;

            for (var i = count; i != 0; i--)
            {
                var block = dfsTree.GetPostOrder(i - 1);

                foreach (var predBlock in block.PredBlocks)
                {
                    change |= BitVecOps.UnionDChanged(postOrderTraits,
                        sets[block.bbPostorderNum], sets[predBlock.bbPostorderNum]);
                }
            }
        } while (change);

        var reachabilitySets = new BlockReachabilitySets(dfsTree, sets);

#if DEBUG
        if (dfsTree.GetCompiler().verbose)
        {
            jitprintf("\nAfter computing reachability sets:\n");
            reachabilitySets.Dump();
        }
#endif

        return reachabilitySets;
    }

    public bool CanReach(BasicBlock from, BasicBlock to)
    {
        assert(_dfsTree.Contains(from));

        if (!_dfsTree.Contains(to))
        {
            return false;
        }

        var postOrderTraits = _dfsTree.PostOrderTraits();
        return BitVecOps.IsMember(postOrderTraits, _reachabilitySets[to.bbPostorderNum], from.bbPostorderNum);
    }

#if DEBUG
    public void Dump()
    {
        jitprintf("------------------------------------------------\n");
        jitprintf("BBnum  Reachable by \n");
        jitprintf("------------------------------------------------\n");

        var compiler = _dfsTree.GetCompiler();
        var postOrderTraits = _dfsTree.PostOrderTraits();

        foreach (var block in compiler.Blocks)
        {
            jitprintf($"{FMT_BB(block.bbNum)} : ");
            if (_dfsTree.Contains(block))
            {
                var separator = "";
                _ = BitVecOps.VisitBits(postOrderTraits, _reachabilitySets[block.bbPostorderNum], poNum => {
                    jitprintf($"{separator}{FMT_BB(_dfsTree.GetPostOrder(poNum).bbNum)}");
                    separator = " ";
                    return true;
                });
            }
            else
            {
                jitprintf("[unreachable]");
            }
            jitprintf("\n");
        }
    }
#endif
}
