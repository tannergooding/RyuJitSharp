// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private BasicBlock? findPredBlockForLiveIn(BasicBlock block, BasicBlock? previousBlock,
        ref bool predecessorAllocated)
    {
        assert(!predecessorAllocated);
        assert(_blockInfo is not null);
        if (_blockInfo[block.bbNum].hasEHBoundaryIn)
        {
            JITDUMP("\n\nIncoming EH boundary; ");
            return null;
        }

        if (block == _compiler.fgFirstBB)
        {
            return null;
        }

        if (block.bbPreds is null)
        {
            JITDUMP("\n\nNo predecessor; ");
            // A throw block cannot inherit registers from the prior layout block.
            // Other unreachable blocks may do so to avoid unnecessary resolution.
            if (block.Kind is BBJ_THROW)
            {
                JITDUMP(" - throw block; ");
                return null;
            }

            return previousBlock;
        }

        BasicBlock? predecessor = null;
#if DEBUG
        if ((_lsraStressMask & 0x300) == 0x100)
        {
            predecessor = previousBlock;
        }
        else
#endif
        {
            predecessor = block.GetUniquePred(_compiler);
            if (predecessor is not null)
            {
                assert(!predecessor.hasEHBoundaryOut);
                if (isBlockVisited(predecessor))
                {
                    if (predecessor.Kind is BBJ_COND)
                    {
                        var otherBlock = predecessor.FalseTarget == block
                            ? predecessor.TrueTarget
                            : predecessor.FalseTarget;
                        if (isBlockVisited(otherBlock) && !_blockInfo[otherBlock.bbNum].hasEHBoundaryIn)
                        {
                            // Match the visited target's incoming locations across a
                            // conditional backedge to avoid split-edge register moves.
                            foreach (var otherPredecessor in otherBlock.PredBlocks)
                            {
                                if (otherPredecessor.bbNum == _blockInfo[otherBlock.bbNum].predBBNum)
                                {
                                    predecessor = otherPredecessor;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    predecessor = null;
                }
            }
            else
            {
                foreach (var candidate in block.PredBlocks)
                {
                    if (isBlockVisited(candidate) &&
                        ((predecessor is null) || (predecessor.bbWeight < candidate.bbWeight)))
                    {
                        predecessor = candidate;
                        predecessorAllocated = true;
                    }
                }
            }

            if (predecessor is null)
            {
                predecessor = previousBlock;
                assert(predecessor is not null);
                JITDUMP("\n\nNo allocated predecessor; ");
            }
        }

        return predecessor;
    }
}
