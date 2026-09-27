// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, compiler.hpp and fgopt.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgSearchImprovedLayout()
    {
#if DEBUG
        if (verbose)
        {
            jitprintf("*************** In fgSearchImprovedLayout()\n");
            jitprintf("\nInitial BasicBlocks");
            fgDispBasicBlocks(verboseTrees);
            jitprintf("\n");
        }
#endif

        if (_dfsTree is null)
        {
            _dfsTree = fgComputeDfs(useProfile: true);
            _loops = FlowGraphNaturalLoops.Find(_dfsTree);
        }
        assert(_loops is not null);

        var initialLayout = new BasicBlock[_dfsTree.PostOrderCount];
        var numHotBlocks = 0;
        void AddToSequence(BasicBlock block)
        {
            if (!block.hasHndIndex && (!block.isBBWeightCold(this) || block.IsFirst))
            {
                block.bbPreorderNum = numHotBlocks;
                initialLayout[numHotBlocks++] = block;
            }
        }

        if (compStressCompile(STRESS_THREE_OPT_LAYOUT, 10))
        {
            for (var index = 0; index < _dfsTree.PostOrderCount; index++)
            {
                AddToSequence(_dfsTree.GetPostOrder(index));
            }

            (initialLayout[0], initialLayout[numHotBlocks - 1]) = (initialLayout[numHotBlocks - 1], initialLayout[0]);
            (initialLayout[0].bbPreorderNum, initialLayout[numHotBlocks - 1].bbPreorderNum) =
                (initialLayout[numHotBlocks - 1].bbPreorderNum, initialLayout[0].bbPreorderNum);
        }
        else
        {
            fgVisitBlocksInLoopAwareRPO(_dfsTree, _loops, AddToSequence);
        }

        var modified = false;
        if (numHotBlocks == 0)
        {
            JITDUMP("No hot blocks found. Skipping reordering.\n");
        }
        else
        {
            var layoutRunner = new ThreeOptLayout(this, initialLayout, numHotBlocks, compHndBBtabCount != 0);
            modified = layoutRunner.Run();
        }

        // Layout overwrites traversal ordinals even when the linked block order is unchanged.
        fgInvalidateDfsTree();
        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    internal void fgFindTryRegionEnds()
    {
        assert(compHndBBtabCount != 0);
        var unsetTryEnds = 0;
        // Native nulls the end pointers; keep managed descriptors non-null while tracking the same state.
        var unset = new bool[compHndBBtabCount];
        for (var index = 0; index < compHndBBtabCount; index++)
        {
            if (!compHndBBtab[index].ebdTryLast.hasHndIndex)
            {
                unset[index] = true;
                unsetTryEnds++;
            }
        }

        for (var block = fgLastBBInMainFunction(); (unsetTryEnds != 0) && (block is not null); block = block.Prev)
        {
            if (!block.hasTryIndex)
            {
                continue;
            }

            for (var tryIndex = block.TryIndex; tryIndex != EHblkDsc.NO_ENCLOSING_INDEX;
                tryIndex = ehGetEnclosingTryIndex(tryIndex))
            {
                if (!unset[tryIndex])
                {
                    break;
                }

                assert(unsetTryEnds != 0);
                compHndBBtab[tryIndex].ebdTryLast = block;
                unset[tryIndex] = false;
                unsetTryEnds--;
            }
        }

        assert(unsetTryEnds == 0);
    }

    internal void fgVisitBlocksInLoopAwareRPO(FlowGraphDfsTree dfsTree, FlowGraphNaturalLoops loops, Action<BasicBlock> func)
    {
        if (loops.NumLoops == 0)
        {
            for (var index = dfsTree.PostOrderCount; index != 0; index--)
            {
                func(dfsTree.GetPostOrder(index - 1));
            }
        }
        else
        {
            var traits = dfsTree.PostOrderTraits();
            var visitedBlocks = BitVecOps.MakeEmpty(traits);

            // Visit each loop body immediately after its header, including nested loops.
            void VisitBlock(BasicBlock block)
            {
                if (!BitVecOps.TryAddElemD(traits, visitedBlocks, block.bbPostorderNum))
                {
                    return;
                }

                func(block);
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
}
