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

        if (Configuration.DEBUG && verbose)
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
    }

    public PhaseStatus optSetBlockWeights()
    {
        noway_assert(opts.OptimizationEnabled);
        global::RyuJitSharp.FlowGraphDfsTree __jitExceptionResult116;
        var __jitExceptionCoalesceLeft117 = _dfsTree;
        if (__jitExceptionCoalesceLeft117 != null)
        {
            __jitExceptionResult116 = __jitExceptionCoalesceLeft117;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.BlockWeightsRequireADFSTree);
            __jitExceptionResult116 = default;
        }
        var dfsTree = __jitExceptionResult116;

        fgHasLoops = dfsTree.HasCycle;
        if (fgIsUsingProfileWeights)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var madeChanges = false;
        var domTree = _domTree ??= FlowGraphDominatorTree.Build(dfsTree);
        var reachabilitySets = _reachabilitySets ??= BlockReachabilitySets.Build(dfsTree);
        global::RyuJitSharp.FlowGraphNaturalLoops __jitExceptionResult118;
        var __jitExceptionCoalesceLeft119 = _loops;
        if (__jitExceptionCoalesceLeft119 != null)
        {
            __jitExceptionResult118 = __jitExceptionCoalesceLeft119;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.BlockWeightsRequireNaturalLoops);
            __jitExceptionResult118 = default;
        }
        var loops = __jitExceptionResult118;

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

        global::RyuJitSharp.BasicBlock __jitExceptionResult120;
        var __jitExceptionCoalesceLeft121 = fgFirstBB;
        if (__jitExceptionCoalesceLeft121 != null)
        {
            __jitExceptionResult120 = __jitExceptionCoalesceLeft121;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.BlockWeightsRequireAnEntryBlock);
            __jitExceptionResult120 = default;
        }
        var firstBlock = __jitExceptionResult120;
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
        global::RyuJitSharp.FlowGraphDominatorTree __jitExceptionResult122;
        var __jitExceptionCoalesceLeft123 = _domTree;
        if (__jitExceptionCoalesceLeft123 != null)
        {
            __jitExceptionResult122 = __jitExceptionCoalesceLeft123;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopWeightsRequireDominators);
            __jitExceptionResult122 = default;
        }
        var domTree = __jitExceptionResult122;
        _ = loop.VisitLoopBlocks(curBlk => {
            void ReportBlockWeight(string message)
            {
                jitprintf($"\n    {FMT_BB(curBlk.bbNum)}(wt={FMT_WT(curBlk.getBBWeight(this))}){message}");
            }
            if (curBlk.hasProfileWeight && fgHaveProfileWeights)
            {
                if (Configuration.DEBUG && verbose)
                {
                    ReportBlockWeight("; unchanged: has profile weight");
                }
                return BasicBlockVisit.Continue;
            }

            if (curBlk.isRunRarely)
            {
                if (Configuration.DEBUG && verbose)
                {
                    ReportBlockWeight("; unchanged: run rarely");
                }
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
            if (Configuration.DEBUG && verbose)
            {
                ReportBlockWeight("");
            }
            return BasicBlockVisit.Continue;
        });
    }
}
