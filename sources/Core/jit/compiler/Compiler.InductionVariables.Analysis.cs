// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockVisit;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optCanSinkWidenedIV(int lclNum, FlowGraphNaturalLoop loop)
    {
        assert(lvaGetDesc(lclNum).lvInSsa);
        var result = loop.VisitRegularExitBlocks(exit => {
            if (!optLocalIsLiveIntoBlock(lclNum, exit))
            {
                JITDUMP($"  Exit {FMT_BB(exit.bbNum)} does not need a sink; V{lclNum:D2} is not live-in\n");
                return Continue;
            }

            foreach (var predecessor in exit.PredBlocks)
            {
                if (!loop.ContainsBlock(predecessor))
                {
                    JITDUMP($"  Cannot safely sink widened version of V{lclNum:D2} into exit " +
                        $"{FMT_BB(exit.bbNum)} of L{loop.Index:D2}; it has a non-loop pred " +
                        $"{FMT_BB(predecessor.bbNum)}\n");
                    return Abort;
                }
            }

            return Continue;
        });

#if DEBUG
        _ = loop.VisitLoopBlocks(block => {
            _ = block.VisitAllSuccs(this, successor => {
                if (!loop.ContainsBlock(successor) && bbIsHandlerBeg(successor))
                {
                    assert(!optLocalIsLiveIntoBlock(lclNum, successor));
                }

                return Continue;
            });
            return Continue;
        });
#endif
        return result is not Abort;
    }

    private void optVisitBoundingExitingCondBlocks(FlowGraphNaturalLoop loop, Action<BasicBlock> visitor)
    {
        BasicBlock? dominates = null;
        var domTree = _domTree ?? throw new FatalJitException("IV optimization requires dominators.");
        foreach (var backEdge in loop.BackEdges)
        {
            dominates = dominates is null
                ? backEdge.SourceBlock
                : domTree.Intersect(dominates, backEdge.SourceBlock);
        }

        while ((dominates is not null) && loop.ContainsBlock(dominates))
        {
            if ((dominates.Kind is BBJ_COND) &&
                (!loop.ContainsBlock(dominates.TrueTarget) ||
                    !loop.ContainsBlock(dominates.FalseTarget)))
            {
                visitor(dominates);
            }
            dominates = dominates.bbIDom;
        }
    }

    private bool optCanAndShouldChangeExitTest(GenTree cond, bool dump)
    {
        if ((cond.Flags & GTF_SIDE_EFFECT) != 0)
        {
            if (dump)
            {
                JITDUMP("  No; exit node has side effects\n");
            }
            return false;
        }

        var checkProfitability = !compStressCompile(STRESS_DOWNWARDS_COUNTED_LOOPS, 50);
        if (checkProfitability && cond.Oper.IsCompare &&
            (cond.AsOp().Op1.IsIntegralConst(0) || cond.AsOp().Op2.IsIntegralConst(0)))
        {
            if (dump)
            {
#if DEBUG
                JITDUMP($"  No; operand of condition [{cond.TreeId:D6}] is already 0\n");
#endif
            }
            return false;
        }

        return true;
    }

    private bool optLocalHasNonLoopUses(int lclNum, FlowGraphNaturalLoop loop, PerLoopInfo loopInfo)
    {
        ref var descriptor = ref lvaGetDesc(lclNum);
        if (descriptor.lvIsStructField && loopInfo.HasAnyOccurrences(loop, descriptor.lvParentLcl))
        {
            return true;
        }
        if (!descriptor.lvTracked && !descriptor.lvInSsa)
        {
            return true;
        }
        if (descriptor.lvTracked && descriptor.IsLiveInOutOfHandler)
        {
            return true;
        }

        var result = loop.VisitRegularExitBlocks(block =>
            optLocalIsLiveIntoBlock(lclNum, block) ? Abort : Continue);
        if (result is Abort)
        {
            return true;
        }

#if DEBUG
        _ = loop.VisitLoopBlocks(block => {
            _ = block.VisitAllSuccs(this, successor => {
                if (!loop.ContainsBlock(successor) && bbIsHandlerBeg(successor))
                {
                    assert(!optLocalIsLiveIntoBlock(lclNum, successor));
                }
                return Continue;
            });
            return Continue;
        });
#endif
        return false;
    }

    private bool optLocalIsLiveIntoBlock(int lclNum, BasicBlock block)
    {
        ref var descriptor = ref lvaGetDesc(lclNum);
        if (descriptor.lvTracked)
        {
            return VarSetOps.IsMember(this, block.bbLiveIn, descriptor._varIndex);
        }

        assert(descriptor.lvInSsa);
        return IsInsertedSsaLiveIn(block, lclNum);
    }

    private bool optIsUpdateOfIVWithoutSideEffects(GenTree tree, int lclNum)
    {
        if (!tree.Oper.IsLocalStore)
        {
            return false;
        }
        var store = tree.AsLclVarCommon();
        if (store.LclNum != lclNum)
        {
            return false;
        }
        return (store.Data.Flags & GTF_SIDE_EFFECT) == 0;
    }

    private bool optRemoveUnusedIVs(FlowGraphNaturalLoop loop, PerLoopInfo loopInfo)
    {
        JITDUMP("  Now looking for unnecessary primary IVs\n");
        var removed = 0;
        for (var stmt = loop.Header.FirstStmt; (stmt is not null) && stmt.IsPhiDefnStmt; stmt = stmt.NextStmt)
        {
            var lclNum = stmt.RootNode.AsLclVarCommon().LclNum;
            JITDUMP($"  V{lclNum:D2}");
            if (optLocalHasNonLoopUses(lclNum, loop, loopInfo))
            {
                JITDUMP(" has non-loop uses, cannot remove\n");
                continue;
            }

            if (!loopInfo.VisitStatementsWithOccurrences(loop, lclNum,
                (_, occurrenceStmt) => optIsUpdateOfIVWithoutSideEffects(occurrenceStmt.RootNode, lclNum)))
            {
                JITDUMP(" has essential uses, cannot remove\n");
                continue;
            }

            JITDUMP(" has no essential uses and will be removed\n");
            _ = loopInfo.VisitStatementsWithOccurrences(loop, lclNum, (block, occurrenceStmt) => {
#if DEBUG
                JITDUMP($"  Removing {FMT_STMT(occurrenceStmt.Id)}\n");
#endif
                fgRemoveStmt(block, occurrenceStmt);
                return true;
            });
            removed++;
            loopInfo.Invalidate(loop);
        }

        Metrics.UnusedIVsRemoved += removed;
        return removed > 0;
    }
}
