// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool fgLeadsToEmptyBlockCycle(BasicBlock block)
    {
        var traits = new BitVecTraits(this, fgBBNumMax + 1);
        var visited = BitVecOps.MakeEmpty(traits);

        while (block.IsEmpty && (block.Kind is BBJ_ALWAYS))
        {
            if (!BitVecOps.TryAddElemD(traits, visited, block.bbNum))
            {
                return true;
            }

            block = block.Target;
        }

        return false;
    }

    private bool fgOptimizeBranchToEmptyUnconditional(BasicBlock block, BasicBlock bDest)
    {
        assert(bDest.IsEmpty);
        assert(bDest.Kind is BBJ_ALWAYS);

        var bDestTarget = bDest.Target;
        var optimizeJump = !fgLeadsToEmptyBlockCycle(bDest);

        if (bDest.hasTryIndex && !BasicBlock.sameTryRegion(block, bDest))
        {
            optimizeJump = false;
        }

        if (bDestTarget.HasFlag(BBF_REMOVED))
        {
            optimizeJump = false;
        }

        if (bDest.HasFlag(BBF_CLONED_FINALLY_BEGIN))
        {
            optimizeJump = false;
        }

        if (bDest.HasFlag(BBF_REMOVED))
        {
            optimizeJump = true;
        }

        if (optimizeJump)
        {
            JITDUMP($"\nOptimizing a jump to an unconditional jump ({FMT_BB(block.bbNum)} -> {FMT_BB(bDest.bbNum)} -> {FMT_BB(bDest.Target.bbNum)})\n");

            weight_t removedWeight;
            switch (block.Kind)
            {
                case BBJ_ALWAYS:
                case BBJ_CALLFINALLYRET:
                {
                    removedWeight = block.bbWeight;
                    fgRedirectEdge(ref block.TargetEdgeRef, bDest.Target);
                    break;
                }

                case BBJ_COND:
                {
                    if (block.TrueTarget == bDest)
                    {
                        assert(block.FalseTarget != bDest);
                        removedWeight = block.TrueEdge.LikelyWeight;
                        fgRedirectEdge(ref block.TrueEdgeRef, bDest.Target);

                        if (block.TrueEdge == block.FalseEdge)
                        {
                            block.TrueEdge.Likelihood = 1.0;
                        }
                    }
                    else
                    {
                        assert(block.FalseTarget == bDest);
                        removedWeight = block.FalseEdge.LikelyWeight;
                        fgRedirectEdge(ref block.FalseEdgeRef, bDest.Target);

                        if (block.TrueEdge == block.FalseEdge)
                        {
                            block.FalseEdge.Likelihood = 1.0;
                        }
                    }

                    break;
                }

                default:
                    unreached();
                    return false;
            }

            block.CopyFlags(bDest, BBF_ASYNC_RESUMPTION);

            if (bDest.hasProfileWeight)
            {
                if (fgPgoConsistent && (bDest.bbWeight < removedWeight))
                {
                    JITDUMP($"Clamping {FMT_BB(bDest.bbNum)} weight in fgOptimizeBranchToEmptyUnconditional\n");
                    fgPgoConsistent = false;
                }

                bDest.decreaseBBProfileWeight(removedWeight);
            }

            return true;
        }

        return false;
    }
}
