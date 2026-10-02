// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    internal void fgVisitBlocksInTryAwareLoopAwareRPO(
        FlowGraphDfsTree dfsTree, FlowGraphTryRegions tryRegions, FlowGraphNaturalLoops loops, Action<BasicBlock> func)
    {
        assert(dfsTree is not null);
        assert(loops is not null);
        assert(tryRegions is not null);

        if (tryRegions.NumTryCatchRegions() == 0)
        {
            fgVisitBlocksInLoopAwareRPO(dfsTree, loops, func);
            return;
        }

        var traits = dfsTree.PostOrderTraits();
        var visitedBlocks = BitVecOps.MakeEmpty(traits);

        void VisitBlock(BasicBlock block)
        {
            if (!BitVecOps.TryAddElemD(traits, visitedBlocks, block.bbPostorderNum))
            {
                return;
            }

            func(block);

            var tryRegion = tryRegions.GetTryRegionByHeader(block);
            if ((tryRegion is not null) && tryRegion.HasCatchHandler())
            {
                _ = tryRegion.VisitTryRegionBlocksReversePostOrder(tryBlock => {
                    VisitBlock(tryBlock);
                    return BasicBlockVisit.Continue;
                });
            }

            var loop = loops.GetLoopByHeader(block);
            if (loop is not null)
            {
                _ = loop.VisitLoopBlocksReversePostOrder(loopBlock => {
                    VisitBlock(loopBlock);
                    return BasicBlockVisit.Continue;
                });
            }
        }

        for (var index = dfsTree.PostOrderCount; index != 0; index--)
        {
            VisitBlock(dfsTree.GetPostOrder(index - 1));
        }
    }
}
