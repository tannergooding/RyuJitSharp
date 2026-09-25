// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgCanCompactBlock(BasicBlock block)
    {
        assert(block is not null);

        if ((block.Kind is not BBJ_ALWAYS) || block.HasFlag(BBF_KEEP_BBJ_ALWAYS))
        {
            return false;
        }

        var target = block.Target;

        if (block == target)
        {
            return false;
        }

        if (target.IsFirst || (target == fgEntryBB) || (target == fgOSREntryBB))
        {
            return false;
        }

        // Don't bother compacting a call-finally pair if it doesn't succeed block.
        if (target.isBBCallFinallyPair && (block.Next != target))
        {
            return false;
        }

        // If target has multiple incoming edges, we can still compact if block is empty.
        // However, not if it is the beginning of a handler.
        if ((target.CountOfInEdges != 1) && (!block.isEmpty() || (block.CatchType is not BBCT_NONE)))
        {
            return false;
        }

        if (target.HasFlag(BBF_DONT_REMOVE))
        {
            return false;
        }

        // Ensure we leave a valid init BB around.
        if ((block == fgFirstBB) && !fgCanCompactInitBlock())
        {
            return false;
        }

        if (!BasicBlock.sameEHRegion(block, target))
        {
            return false;
        }

        return true;
    }

    public bool fgCanCompactInitBlock()
    {
        assert(fgFirstBB is not null);
        assert(fgFirstBB.Kind is BBJ_ALWAYS);
        var target = fgFirstBB.Target;

        if (target.hasTryIndex)
        {
            return false;
        }

        assert(target.bbPreds is not null);
        if (target.bbPreds.NextPredEdge is not null)
        {
            return false;
        }

        if (opts.compDbgCode && !target.HasFlag(BBF_INTERNAL))
        {
            // Avoid conflating JIT-inserted code with user code in debug methods.
            return false;
        }

        return true;
    }

    /// <summary>Compact an eligible BBJ_ALWAYS block and its target into the first block.</summary>
    public void fgCompactBlock(BasicBlock block)
    {
        assert(fgCanCompactBlock(block));

        // We shouldn't churn the flowgraph after doing hot/cold splitting.
        assert(fgFirstColdBlock is null);

        var target = block.Target;
        JITDUMP($"\nCompacting {FMT_BB(target.bbNum)} into {FMT_BB(block.bbNum)}:\n");
        fgRemoveRefPred(block.TargetEdge);

        var targetHadOtherPreds = target.CountOfInEdges > 0;
        if (targetHadOtherPreds)
        {
            JITDUMP($"Second block has {target.CountOfInEdges} other incoming edges\n");
            assert(block.isEmpty());

            foreach (var predBlock in target.PredBlocksEditing)
            {
                fgReplaceJumpTarget(predBlock, target, block);
            }
        }

        assert(target.CountOfInEdges == 0);
        assert(target.bbPreds is null);

        // First move any phi definitions of the second block after the phi defs of the first.
        // TODO-CQ: This may be the wrong thing to do. If we're compacting blocks, it's because a
        // control-flow choice was constant-folded away. So probably phi's need to go away,
        // as well, in favor of one of the incoming branches. Or at least be modified.
        assert(block.IsLIR == target.IsLIR);
        if (block.IsLIR)
        {
            var targetNode = target.FirstNode;
            if (targetNode is not null)
            {
                assert(target.LastNode is not null);
                var targetNodes = target.RemoveAndGetRange(targetNode, target.LastNode);
                block.InsertAtEnd(targetNodes);
            }
        }
        else
        {
            var blkNonPhi1 = block.GetFirstNonPhiDef();
            var targetNonPhi1 = target.GetFirstNonPhiDef();
            var blkFirst = block.FirstStmt;
            var targetFirst = target.FirstStmt;

            if ((targetFirst is not null) && (targetFirst != targetNonPhi1))
            {
                var targetLast = targetFirst.PrevStmt;
                assert(targetLast is not null);
                assert(targetLast.NextStmt is null);

                if (blkNonPhi1 != blkFirst)
                {
                    assert(blkFirst is not null);

                    // Insert target's phis after the last phi of block.
                    var blkLastPhi = (blkNonPhi1 is not null) ? blkNonPhi1.PrevStmt : blkFirst.PrevStmt;
                    assert(blkLastPhi is not null);
                    blkLastPhi.NextStmt = targetFirst;
                    targetFirst.PrevStmt = blkLastPhi;

                    // Now, rest of block after last phi of target.
                    var targetLastPhi = (targetNonPhi1 is not null) ? targetNonPhi1.PrevStmt : targetFirst.PrevStmt;
                    assert(targetLastPhi is not null);
                    targetLastPhi.NextStmt = blkNonPhi1;

                    if (blkNonPhi1 is not null)
                    {
                        blkNonPhi1.PrevStmt = targetLastPhi;
                    }
                    else
                    {
                        // Block has no non phis, so make the last statement be the last added phi.
                        blkFirst.PrevStmt = targetLastPhi;
                    }

                    target.FirstStmt = targetNonPhi1;
                    targetNonPhi1?.PrevStmt = targetLast;
                }
                else if (blkFirst is not null)
                {
                    // If block has no statements, fusion will work fine.
                    var blkLast = blkFirst.PrevStmt;
                    block.FirstStmt = targetFirst;
                    var targetLastPhi = (targetNonPhi1 is not null) ? targetNonPhi1.PrevStmt : targetFirst.PrevStmt;
                    assert(targetLastPhi is not null);

                    targetFirst.PrevStmt = blkLast;
                    targetLastPhi.NextStmt = blkFirst;
                    blkFirst.PrevStmt = targetLastPhi;
                    target.FirstStmt = targetNonPhi1;
                    targetNonPhi1?.PrevStmt = targetLast;
                }
            }

            var stmtList1 = block.FirstStmt;
            var stmtList2 = target.FirstStmt;

            if (stmtList1 is not null)
            {
                var stmtLast1 = block.LastStmt;
                assert(stmtLast1 is not null);

                // The second block may have an empty statement list.
                if (stmtList2 is not null)
                {
                    var stmtLast2 = target.LastStmt;
                    stmtLast1.NextStmt = stmtList2;
                    stmtList2.PrevStmt = stmtLast1;
                    stmtList1.PrevStmt = stmtLast2;
                }
            }
            else
            {
                block.FirstStmt = stmtList2;
            }
        }

        // Target's weight includes block's weight plus the weights of target's other
        // predecessors, which now flow into block.
        var hasProfileWeight = block.hasProfileWeight;
        block.inheritWeight(target);
        if (hasProfileWeight)
        {
            block.SetFlags(BBF_PROF_WEIGHT);
        }

        // Retargeting other incoming edges may expose accumulated rounding from
        // prior weight adjustments during fgUpdateFlowGraph.
        if (targetHadOtherPreds && block.hasProfileWeight && fgPgoConsistent)
        {
            weight_t incomingLikelyWeight = 0;
            foreach (var predEdge in block.PredEdges)
            {
                incomingLikelyWeight += predEdge.LikelyWeight;
            }

            if (!fgProfileWeightsConsistentOrSmall(block.bbWeight, incomingLikelyWeight))
            {
                JITDUMP($"fgCompactBlock: {FMT_BB(block.bbNum)} weight {FMT_WT(block.bbWeight)} " +
                    $"inconsistent with incoming {FMT_WT(incomingLikelyWeight)} after compaction. " +
                    $"Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                fgPgoConsistent = false;
            }
        }

        VarSetOps.AssignAllowUninitRhs(this, ref block.bbLiveOut, target.bbLiveOut);

        // Merge the IL ranges, ignoring unknown offsets. If both are unknown,
        // preserve that fact. TODO: probably base this on the statements within.
        if (block.bbCodeOffs == BAD_IL_OFFSET)
        {
            block.bbCodeOffs = target.bbCodeOffs;
        }
        else if (target.bbCodeOffs != BAD_IL_OFFSET)
        {
            // Native IL_OFFSET is unsigned; the managed alias retains its bits in an int.
            if (unchecked((uint)block.bbCodeOffs) > unchecked((uint)target.bbCodeOffs))
            {
                block.bbCodeOffs = target.bbCodeOffs;
            }
        }

        if (block.bbCodeOffsEnd == BAD_IL_OFFSET)
        {
            block.bbCodeOffsEnd = target.bbCodeOffsEnd;
        }
        else if (target.bbCodeOffsEnd != BAD_IL_OFFSET)
        {
            if (unchecked((uint)block.bbCodeOffsEnd) < unchecked((uint)target.bbCodeOffsEnd))
            {
                block.bbCodeOffsEnd = target.bbCodeOffsEnd;
            }
        }

        if (block.HasFlag(BBF_INTERNAL) && !target.HasFlag(BBF_INTERNAL))
        {
            block.RemoveFlags(BBF_INTERNAL);
            block.SetFlags(BBF_IMPORTED);
        }

        block.CopyFlags(target, BBF_COMPACT_UPD);
        block.CopyFlags(target, BBF_ASYNC_RESUMPTION);
        target.SetFlags(BBF_REMOVED);

        fgUnlinkRange(target, target);
        fgBBcount--;
        ehUpdateForDeletedBlock(target);

        switch (target.Kind)
        {
            case BBJ_CALLFINALLY:
            {
                block.CopyFlags(target, BBF_RETLESS_CALL);
                goto case BBJ_ALWAYS;
            }

            case BBJ_ALWAYS:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            {
                var targetEdge = target.TargetEdge;
                fgReplacePred(targetEdge, block);
                block.SetKindAndTargetEdge(target.Kind, targetEdge);
                break;
            }

            case BBJ_COND:
            {
                var trueEdge = target.TrueEdge;
                var falseEdge = target.FalseEdge;
                fgReplacePred(trueEdge, block);
                if (trueEdge != falseEdge)
                {
                    fgReplacePred(falseEdge, block);
                }

                block.SetCond(trueEdge, falseEdge);
                break;
            }

            case BBJ_EHFINALLYRET:
            {
                var targets = target.EhfTargets;
                assert(targets is not null);
                block.SetEhf(targets);
                fgChangeEhfBlock(target, block);
                break;
            }

            case BBJ_EHFAULTRET:
            case BBJ_THROW:
            case BBJ_RETURN:
            {
                block.Kind = target.Kind;
                break;
            }

            case BBJ_SWITCH:
            {
                block.SwitchTargets = target.SwitchTargets;
                fgChangeSwitchBlock(target, block);
                break;
            }

            default:
            {
                noway_assert(false, "Unexpected bbKind");
                break;
            }
        }

        assert(block.Kind == target.Kind);
    }

    public void fgChangeEhfBlock(BasicBlock oldBlock, BasicBlock newBlock)
    {
        assert(oldBlock is not null);
        assert(newBlock is not null);
        assert(oldBlock.Kind is BBJ_EHFINALLYRET);
        assert(fgPredsComputed);

        var targets = oldBlock.EhfTargets;
        assert(targets is not null);
        foreach (var succEdge in targets.Succs)
        {
            fgReplacePred(succEdge, newBlock);
        }
    }
}
