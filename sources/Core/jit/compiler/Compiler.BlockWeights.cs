// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgComputeReturnBlocks()
    {
        fgReturnBlocks = null;

        foreach (var block in Blocks)
        {
            if (block.Kind is BBJ_RETURN)
            {
                fgReturnBlocks = new BasicBlockList(block, fgReturnBlocks);
            }
        }

#if DEBUG
        if (verbose)
        {
            jitprintf("Return blocks:");
            if (fgReturnBlocks is null)
            {
                jitprintf(" NONE");
            }
            else
            {
                for (var returnBlock = fgReturnBlocks; returnBlock is not null; returnBlock = returnBlock.Next)
                {
                    jitprintf($" {FMT_BB(returnBlock.Block.bbNum)}");
                }
            }
            jitprintf("\n");
        }
#endif
    }

    public PhaseStatus optSetBlockWeights()
    {
        noway_assert(opts.OptimizationEnabled);
        var dfsTree = _dfsTree ?? throw new InvalidOperationException("Block weights require a DFS tree.");

        fgHasLoops = dfsTree.HasCycle;
        if (fgIsUsingProfileWeights)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var madeChanges = false;
        var domTree = _domTree ??= FlowGraphDominatorTree.Build(dfsTree);
        var reachabilitySets = _reachabilitySets ??= BlockReachabilitySets.Build(dfsTree);
        var loops = _loops ?? throw new InvalidOperationException("Block weights require natural loops.");

        foreach (var loop in loops.InReversePostOrder())
        {
            optScaleLoopBlocks(loop);
            madeChanges = true;
        }

        var firstBBDominatesAllReturns = true;
        fgComputeReturnBlocks();

        // The old regular-flow dominator heuristic did not count returns reachable from handlers.
        foreach (var clause in new EHClauses(this))
        {
            var flowBlock = clause.ExFlowBlock;

            for (var returns = fgReturnBlocks; returns is not null; returns = returns.Next)
            {
                if (dfsTree.Contains(flowBlock) && reachabilitySets.CanReach(flowBlock, returns.Block))
                {
                    firstBBDominatesAllReturns = false;
                    break;
                }
            }

            if (!firstBBDominatesAllReturns)
            {
                break;
            }
        }

        var firstBlock = fgFirstBB ?? throw new InvalidOperationException("Block weights require an entry block.");
        foreach (var block in Blocks)
        {
            if (!reachabilitySets.CanReach(firstBlock, block) && !block.isRunRarely && !block.hasProfileWeight)
            {
                madeChanges = true;
                block.bbSetRunRarely();
            }

            if (firstBBDominatesAllReturns && (block.bbWeight != BB_ZERO_WEIGHT))
            {
                var blockDominatesAllReturns = true;

                for (var returns = fgReturnBlocks; returns is not null; returns = returns.Next)
                {
                    if (!dfsTree.Contains(returns.Block) || !domTree.Dominates(block, returns.Block))
                    {
                        blockDominatesAllReturns = false;
                        break;
                    }
                }

                if (block == firstBlock)
                {
                    firstBBDominatesAllReturns = blockDominatesAllReturns;
                }
                else if (!blockDominatesAllReturns)
                {
                    madeChanges = true;
                    // Native uses percentage inheritance: direct scaling changes rounding and equality.
                    block.inheritWeightPercentage(block, 50);
                }
            }
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private void optScaleLoopBlocks(FlowGraphNaturalLoop loop)
    {
        var domTree = _domTree ?? throw new InvalidOperationException("Loop weights require dominators.");
        _ = loop.VisitLoopBlocks(curBlk => {
#if DEBUG
            void ReportBlockWeight(string message)
            {
                if (verbose)
                {
                    jitprintf($"\n    {FMT_BB(curBlk.bbNum)}(wt={FMT_WT(curBlk.getBBWeight(this))}){message}");
                }
            }
#endif
            if (curBlk.hasProfileWeight && fgHaveProfileWeights)
            {
#if DEBUG
                ReportBlockWeight("; unchanged: has profile weight");
#endif
                return BasicBlockVisit.Continue;
            }

            if (curBlk.isRunRarely)
            {
#if DEBUG
                ReportBlockWeight("; unchanged: run rarely");
#endif
                return BasicBlockVisit.Continue;
            }

            var dominates = false;
            foreach (var backEdge in loop.BackEdges)
            {
                dominates |= domTree.Dominates(curBlk, backEdge.SourceBlock);
                if (dominates)
                {
                    break;
                }
            }

            var scale = BB_LOOP_WEIGHT_SCALE;
            if (!dominates)
            {
                // Other paths can reach the back edge, so this block gets half the loop weight.
                scale /= 2;
            }

            curBlk.scaleBBWeight(scale);
#if DEBUG
            ReportBlockWeight("");
#endif
            return BasicBlockVisit.Continue;
        });
    }
}
