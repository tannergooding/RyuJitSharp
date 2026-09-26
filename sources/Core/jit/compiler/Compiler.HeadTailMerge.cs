// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgopt.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private static ConfigMethodRange s_jitEnableHeadTailMergeRange;
#endif

    private readonly record struct HeadTailMergePredInfo(BasicBlock Block, Statement Stmt);

    public unsafe PhaseStatus fgHeadTailMerge(bool early)
    {
        const int mergeLimit = 50;
        var madeChanges = false;

        if (JitConfig.JitEnableHeadTailMerge <= 0)
        {
            JITDUMP("Head and tail merge disabled by JitEnableHeadTailMerge\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        s_jitEnableHeadTailMergeRange.EnsureInit(JitConfig.JitEnableHeadTailMergeRange);
        if (!s_jitEnableHeadTailMergeRange.Contains(info.compMethodHash()))
        {
            JITDUMP("Tail merge disabled by JitEnableHeadTailMergeRange\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        var predInfo = new List<HeadTailMergePredInfo>();
        var matchedPredInfo = new List<HeadTailMergePredInfo>();
        var retryBlocks = new Stack<BasicBlock>();

        bool SameEHRegionForTailMerge(BasicBlock first, BasicBlock second)
        {
            if (!BasicBlock.sameEHRegion(first, second))
            {
                return false;
            }

            if (!first.hasHndIndex)
            {
                assert(!second.hasHndIndex);
                return true;
            }

            assert(second.hasHndIndex);
            ref var handler = ref ehGetDsc(first.HndIndex);
            if (!handler.HasFilter)
            {
                return true;
            }

            return handler.InFilterRegionBBRange(first) == handler.InFilterRegionBBRange(second);
        }

        bool TryRemoveAndFixFlow(BasicBlock emptyBlock, BasicBlock target)
        {
            assert(emptyBlock.isEmpty());
            assert(emptyBlock.Kind is BBJ_RETURN or BBJ_THROW or BBJ_ALWAYS);

            var canRemove = !emptyBlock.HasFlag(BBF_DONT_REMOVE) && (emptyBlock != fgFirstBB) &&
                (emptyBlock != fgOSREntryBB) && (!opts.IsOSR || (emptyBlock != fgEntryBB));
            if (canRemove)
            {
                foreach (var pred in emptyBlock.PredBlocksEditing)
                {
                    fgReplaceJumpTarget(pred, emptyBlock, target);
                }

                emptyBlock.bbWeight = BB_ZERO_WEIGHT;
                _ = fgRemoveBlock(emptyBlock, true);
            }

            return canRemove;
        }

        bool TailMergePreds(BasicBlock? commonSuccessor)
        {
            if (predInfo.Count < 2)
            {
                return false;
            }

            var effectiveLimit = commonSuccessor is null ? 4 * mergeLimit : mergeLimit;
            if (predInfo.Count > effectiveLimit)
            {
                return false;
            }

            // ArrayStack::TopRef(0) addresses its last insertion.
            for (var i = predInfo.Count - 1; i > 0; i--)
            {
                matchedPredInfo.Clear();
                var first = predInfo[i];
                matchedPredInfo.Add(first);

                for (var j = i - 1; j >= 0; j--)
                {
                    var other = predInfo[j];
                    if (!SameEHRegionForTailMerge(first.Block, other.Block))
                    {
                        continue;
                    }

                    if (GenTree.Compare(first.Stmt.RootNode, other.Stmt.RootNode))
                    {
                        matchedPredInfo.Add(other);
                    }
                }

                if (matchedPredInfo.Count < 2)
                {
                    continue;
                }

                var sameRegionAsSuccessor = (commonSuccessor is not null) &&
                    SameEHRegionForTailMerge(first.Block, commonSuccessor);
                var allPredsMatch = (commonSuccessor is not null) &&
                    (matchedPredInfo.Count == commonSuccessor.CountOfInEdges);
                if (sameRegionAsSuccessor && allPredsMatch)
                {
                    JITDUMP($"All {matchedPredInfo.Count} preds of {FMT_BB(commonSuccessor!.bbNum)} end with the same tree, moving\n");
#if DEBUG
                    if (verbose)
                    {
                        gtDispStmt(matchedPredInfo[^1].Stmt);
                    }
#endif
                    for (var j = 0; j < matchedPredInfo.Count; j++)
                    {
                        var (pred, stmt) = matchedPredInfo[matchedPredInfo.Count - 1 - j];
                        fgUnlinkStmt(pred, stmt);
                        if (j == 0)
                        {
                            fgInsertStmtAtBeg(commonSuccessor, stmt);
                            commonSuccessor.CopyFlags(pred, BBF_COPY_PROPAGATE);
                        }

                        if (pred.isEmpty())
                        {
                            _ = TryRemoveAndFixFlow(pred, commonSuccessor);
                        }

                        madeChanges = true;
                    }

                    return true;
                }

                if (sameRegionAsSuccessor)
                {
                    JITDUMP($"A subset of {matchedPredInfo.Count} preds of {FMT_BB(commonSuccessor!.bbNum)} end with the same tree\n");
                }
                else if (commonSuccessor is not null)
                {
                    JITDUMP($"{(allPredsMatch ? "All" : "A subset of")} {matchedPredInfo.Count} preds of {FMT_BB(commonSuccessor.bbNum)} end with the same tree but are in a different EH region\n");
                }
                else
                {
                    JITDUMP($"A set of {matchedPredInfo.Count} return blocks end with the same tree\n");
                }
#if DEBUG
                if (verbose)
                {
                    gtDispStmt(matchedPredInfo[^1].Stmt);
                }
#endif

                BasicBlock? victim = null;
                Statement? victimStmt = null;
                var bestRank = int.MaxValue;
                for (var j = matchedPredInfo.Count - 1; j >= 0; j--)
                {
                    var (pred, stmt) = matchedPredInfo[j];
                    if (pred == fgFirstBB)
                    {
                        continue;
                    }

                    var noSplit = stmt == pred.FirstStmt;
                    var fallThrough = (pred.Kind is BBJ_ALWAYS) && pred.JumpsToNext;
                    var rank = pred == commonSuccessor ? 0 :
                        noSplit && fallThrough ? 1 :
                        noSplit ? 2 :
                        fallThrough ? 3 : 4;
                    if ((rank < bestRank) || ((rank == bestRank) && (victim is not null) && (pred.bbID < victim.bbID)))
                    {
                        victim = pred;
                        victimStmt = stmt;
                        bestRank = rank;
                    }
                }

                assert(victim is not null);
                assert(victimStmt is not null);
                var target = victim;
                if (victimStmt == target.FirstStmt)
                {
                    JITDUMP($"Will cross-jump to {FMT_BB(target.bbNum)}\n");
                }
                else
                {
                    target = fgSplitBlockAfterStatement(victim, victimStmt.PrevStmt);
                    JITDUMP($"Will cross-jump to newly split off {FMT_BB(target.bbNum)}\n");
                }

                assert(!target.isEmpty());
                for (var j = matchedPredInfo.Count - 1; j >= 0; j--)
                {
                    var (pred, stmt) = matchedPredInfo[j];
                    if (pred == victim)
                    {
                        continue;
                    }

                    fgUnlinkStmt(pred, stmt);
                    if (target.hasProfileWeight)
                    {
                        target.increaseBBProfileWeight(pred.bbWeight);
                    }

                    if (!(pred.isEmpty() && TryRemoveAndFixFlow(pred, target)))
                    {
                        if (commonSuccessor is not null)
                        {
                            assert(pred.Kind is BBJ_ALWAYS);
                            fgRedirectEdge(ref pred.TargetEdgeRef, target);
                        }
                        else
                        {
                            var edge = fgAddRefPred(target, pred);
                            pred.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
                        }
                    }
                }

                madeChanges = true;
                retryBlocks.Push(target);
                return true;
            }

            return false;
        }

        bool TailMerge(BasicBlock block)
        {
            if (block.CountOfInEdges < 2)
            {
                return false;
            }

            predInfo.Clear();
            foreach (var pred in block.PredBlocks)
            {
                if ((pred.UniqueSucc != block) || pred.isEmpty())
                {
                    continue;
                }

                var stmt = pred.LastStmt;
                while ((stmt is not null) && (stmt.RootNode.Oper is GT_NOP))
                {
                    stmt = stmt == pred.FirstStmt ? null : stmt.PrevStmt;
                }

                if (stmt is not null)
                {
                    assert(!stmt.IsPhiDefnStmt);
                    predInfo.Add(new(pred, stmt));
                }
            }

            return TailMergePreds(block);
        }

        void IterateTailMerge(BasicBlock block)
        {
            var count = 0;
            while (TailMerge(block))
            {
                count++;
            }

            if (count > 0)
            {
                JITDUMP($"Did {count} tail merges in {FMT_BB(block.bbNum)}\n");
            }
        }

        var terminalBlocks = new List<BasicBlock>();
        foreach (var block in Blocks)
        {
            IterateTailMerge(block);
            if (block.isEmpty())
            {
                continue;
            }

            if (block.Kind is BBJ_THROW)
            {
                terminalBlocks.Add(block);
            }
            else if ((block.Kind is BBJ_RETURN) && (block != genReturnBB))
            {
                if (block.FirstStmt != block.LastStmt)
                {
                    var precedingTree = block.LastStmt!.PrevStmt!.RootNode;
                    if (precedingTree.Oper.IsCall && precedingTree.AsCall().CanTailCall)
                    {
                        continue;
                    }
                }

                terminalBlocks.Add(block);
            }
        }

        JITDUMP("Trying tail merge of return and throw blocks\n");
        do
        {
            predInfo.Clear();
            foreach (var block in terminalBlocks)
            {
                if (((block.Kind is BBJ_RETURN or BBJ_THROW) && !block.isEmpty()))
                {
                    predInfo.Add(new(block, block.LastStmt!));
                }
            }
        }
        while (TailMergePreds(null));

        while (retryBlocks.Count > 0)
        {
            IterateTailMerge(retryBlocks.Pop());
        }

        foreach (var block in Blocks)
        {
            madeChanges |= fgHeadMergeCore(block, early);
        }

        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
