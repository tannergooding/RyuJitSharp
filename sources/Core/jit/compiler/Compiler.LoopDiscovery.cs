// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus optFindLoopsPhase()
    {
#if DEBUG
        if (verbose)
        {
            jitprintf("*************** In optFindLoopsPhase()\n");
        }
#endif
        assert(_dfsTree is not null);
        optFindLoops();
        Metrics.LoopsFoundDuringOpts = _loops!.NumLoops;

        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    public void optFindLoops()
    {
        var dfsTree = _dfsTree ?? throw new InvalidOperationException("Loop discovery requires a DFS tree.");
        _loops = FlowGraphNaturalLoops.Find(dfsTree);

        optCompactLoops();

        if (optCanonicalizeLoops())
        {
            fgInvalidateDfsTree();
            _dfsTree = fgComputeDfs();
            _loops = FlowGraphNaturalLoops.Find(_dfsTree);
        }

        optLoopsCanonical = true;

        // Removing edges can turn general cycles into natural loops in later phases.
        fgMightHaveNaturalLoops = _dfsTree.HasCycle;
        assert(fgMightHaveNaturalLoops || (_loops.NumLoops == 0));
    }

    private bool optCanonicalizeLoops()
    {
        var loops = _loops!;
        var changed = false;

        foreach (var loop in loops.InReversePostOrder())
        {
            changed |= optCreatePreheader(loop);
        }

        // Exit splitting can invalidate backedge lists. Canonicalize backedges first.
        foreach (var loop in loops.InReversePostOrder())
        {
            changed |= optCanonicalizeBackedges(loop);
        }

        // New preheaders are not in the old DFS tree; read current successors,
        // not the recorded exit destinations, while processing inner loops first.
        foreach (var loop in loops.InPostOrder())
        {
            changed |= optCanonicalizeExits(loop);
        }

        foreach (var loop in loops.InReversePostOrder())
        {
            changed |= optSplitHeaderIfNecessary(loop);
        }

        return changed;
    }

    private void optCompactLoops()
    {
        foreach (var loop in _loops!.InReversePostOrder())
        {
            optCompactLoop(loop);
        }
    }

    private void optCompactLoop(FlowGraphNaturalLoop loop)
    {
        BasicBlock? insertionPoint = null;
        var top = loop.GetLexicallyTopMostBlock();
        var numLoopBlocks = loop.NumLoopBlocks();
        var cur = (BasicBlock?)top;

        while (numLoopBlocks > 0)
        {
            assert(cur is not null);
            if (loop.ContainsBlock(cur))
            {
                numLoopBlocks--;
                cur = cur.Next;
                continue;
            }

            if (cur.isBBCallFinallyPairTail)
            {
                cur = cur.Next;
                continue;
            }

            var lastNonLoopBlock = cur;
            while (true)
            {
                assert(lastNonLoopBlock.Next is not null);
                if (loop.ContainsBlock(lastNonLoopBlock.Next))
                {
                    break;
                }

                lastNonLoopBlock = lastNonLoopBlock.Next;
            }

            insertionPoint ??= loop.GetLexicallyBottomMostBlock();
            var previous = cur.Prev;
            var nextLoopBlock = lastNonLoopBlock.Next;
            assert(previous is not null);
            if (!BasicBlock.sameEHRegion(previous, nextLoopBlock) ||
                !BasicBlock.sameEHRegion(previous, insertionPoint))
            {
                cur = nextLoopBlock;
                continue;
            }

            fgUnlinkRange(cur, lastNonLoopBlock);
            fgMoveBlocksAfter(cur, lastNonLoopBlock, insertionPoint);
            ehUpdateLastBlocks(insertionPoint, lastNonLoopBlock);
            insertionPoint = lastNonLoopBlock;
            cur = nextLoopBlock;
        }
    }

    private bool optCreatePreheader(FlowGraphNaturalLoop loop)
    {
        var header = loop.Header;
        var preheaderEHRegion = EHblkDsc.NO_ENCLOSING_INDEX;
        var inSameRegionAsHeader = true;
        var headerIsTryEntry = bbIsTryBeg(header);

        if (header.hasTryIndex)
        {
            preheaderEHRegion = header.TryIndex;
            foreach (var backEdge in loop.BackEdges)
            {
                if (!bbInTryRegions(preheaderEHRegion, backEdge.SourceBlock))
                {
                    preheaderEHRegion = ehTrueEnclosingTryIndex(preheaderEHRegion);
                    inSameRegionAsHeader = false;
                    break;
                }
            }
        }

        if (!bbIsHandlerBeg(header) && (loop.EntryEdges.Length == 1))
        {
            var candidate = loop.EntryEdge(0).SourceBlock;
            var candidateEHRegion = candidate.hasTryIndex ? candidate.TryIndex : EHblkDsc.NO_ENCLOSING_INDEX;
            if ((candidate.Kind is BBJ_ALWAYS) && (candidate.Target == header) &&
                (candidateEHRegion == preheaderEHRegion))
            {
                JITDUMP($"Natural loop L{loop.Index:D2} already has preheader {FMT_BB(candidate.bbNum)}\n");
                return false;
            }
        }

        var preheader = fgNewBBbefore(BBJ_ALWAYS, header, false);
        preheader.SetFlags(BBF_INTERNAL);

        if (inSameRegionAsHeader)
        {
            fgExtendEHRegionBefore(header);
            if (headerIsTryEntry)
            {
                assert(!bbIsTryBeg(header));
                header.RemoveFlags(BBF_DONT_REMOVE);
            }
        }
        else
        {
            fgSetEHRegionForNewPreheaderOrExit(preheader);
        }

        preheader.bbCodeOffs = header.bbCodeOffs;
        JITDUMP($"Created new preheader {FMT_BB(preheader.bbNum)} for L{loop.Index:D2}\n");

        var newEdge = fgAddRefPred(header, preheader);
        preheader.TargetEdge = newEdge;

        foreach (var enterEdge in loop.EntryEdges)
        {
            var enterBlock = enterEdge.SourceBlock;
            JITDUMP($"Entry edge {FMT_BB(enterBlock.bbNum)} -> {FMT_BB(header.bbNum)} becomes {FMT_BB(enterBlock.bbNum)} -> {FMT_BB(preheader.bbNum)}\n");
            fgReplaceJumpTarget(enterBlock, header, preheader);
        }

        loop.SetEntryEdge(newEdge);
        optSetWeightForPreheaderOrExit(loop, preheader);
        if (preheader.hasProfileWeight && preheader.hasEHBoundaryIn)
        {
            JITDUMP($"optCreatePreheader: {FMT_BB(preheader.bbNum)} is not reachable via normal flow, so skip checking its entry weight. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            fgPgoConsistent = false;
        }

        return true;
    }

    private bool optSplitHeaderIfNecessary(FlowGraphNaturalLoop loop)
    {
        var header = loop.Header;
        var preheader = loop.GetPreheader();
        assert(preheader is not null);
        if (BasicBlock.sameTryRegion(header, preheader))
        {
            assert(!bbIsTryBeg(header));
            return false;
        }

        assert(bbIsTryBeg(header));
        JITDUMP($"Splitting L{loop.Index:D2} header / try entry {FMT_BB(header.bbNum)}\n");

        var firstStmt = header.FirstStmt;
        BasicBlock newTryEntry;
        if (firstStmt is null)
        {
            newTryEntry = fgSplitBlockAtEnd(header);
        }
        else
        {
            var lastStmt = header.LastStmt;
            var hasTerminator = header.HasTerminator;
            var stopStmt = hasTerminator ? lastStmt : null;
            var splitBefore = firstStmt;
            while ((splitBefore is not null) && (splitBefore != stopStmt) &&
                   ((splitBefore.RootNode.Flags & (GTF_EXCEPT | GTF_CALL)) == 0))
            {
                splitBefore = splitBefore.NextStmt;
            }

            if (splitBefore is null)
            {
                assert(!hasTerminator);
                newTryEntry = fgSplitBlockAtEnd(header);
            }
            else if (splitBefore == firstStmt)
            {
                newTryEntry = fgSplitBlockAtBeginning(header);
            }
            else
            {
                newTryEntry = fgSplitBlockAfterStatement(header, splitBefore.PrevStmt);
            }
        }

        var outermostIndex = EHblkDsc.NO_ENCLOSING_INDEX;
        for (ushort index = 0; index < compHndBBtabCount; index++)
        {
            ref var clause = ref ehGetDsc(index);
            if (clause.ebdTryBeg == header)
            {
                fgSetTryBeg(ref clause, newTryEntry);
                outermostIndex = index;
            }
        }

        assert(outermostIndex != EHblkDsc.NO_ENCLOSING_INDEX);
        assert(!bbIsTryBeg(header));
        header.RemoveFlags(BBF_DONT_REMOVE);

        var enclosingTryIndex = ehGetDsc(outermostIndex).ebdEnclosingTryIndex;
        if (enclosingTryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
        {
            header.clearTryIndex();
        }
        else
        {
            header.TryIndex = enclosingTryIndex;
        }

        assert(!bbIsTryBeg(header));
        return true;
    }

    private bool optCanonicalizeExits(FlowGraphNaturalLoop loop)
    {
        var changed = false;
        foreach (var edge in loop.ExitEdges)
        {
            _ = edge.SourceBlock.VisitRegularSuccs(this, succ => {
                if (!loop.ContainsBlock(succ))
                {
                    changed |= optCanonicalizeExit(loop, succ);
                }

                return BasicBlockVisit.Continue;
            });
        }

        return changed;
    }

    private bool optCanonicalizeExit(FlowGraphNaturalLoop loop, BasicBlock exit)
    {
        assert(!loop.ContainsBlock(exit));
        if (bbIsHandlerBeg(exit))
        {
            return false;
        }

        var allLoopPreds = true;
        foreach (var pred in exit.PredBlocks)
        {
            if (!loop.ContainsBlock(pred))
            {
                allLoopPreds = false;
                break;
            }
        }

        if (allLoopPreds)
        {
            JITDUMP($"All preds of exit {FMT_BB(exit.bbNum)} of L{loop.Index:D2} are already in the loop, no exit canonicalization needed\n");
            return false;
        }

        JITDUMP($"Canonicalize exit {FMT_BB(exit.bbNum)} for L{loop.Index:D2} to have only loop predecessors\n");
        BasicBlock newExit;
        if (exit.Kind is BBJ_CALLFINALLY)
        {
            var finallyBlock = exit.Target;
            assert(finallyBlock.hasHndIndex);
            newExit = fgNewBBatTryRegionEnd(BBJ_ALWAYS, finallyBlock.HndIndex);
        }
        else
        {
            newExit = fgNewBBbefore(BBJ_ALWAYS, exit, false);
            fgSetEHRegionForNewPreheaderOrExit(newExit);
        }

        newExit.SetFlags(BBF_INTERNAL);
        var newEdge = fgAddRefPred(exit, newExit);
        newExit.TargetEdge = newEdge;
        newExit.bbCodeOffs = exit.bbCodeOffs;

        foreach (var pred in exit.PredBlocksEditing)
        {
            if (loop.ContainsBlock(pred))
            {
                fgReplaceJumpTarget(pred, exit, newExit);
            }
        }

        optSetWeightForPreheaderOrExit(loop, newExit);
        JITDUMP($"Created new exit {FMT_BB(newExit.bbNum)} to replace {FMT_BB(exit.bbNum)} exit for L{loop.Index:D2}\n");
        return true;
    }

    private bool optCanonicalizeBackedges(FlowGraphNaturalLoop loop)
    {
        if (loop.BackEdges.Length <= 1)
        {
            return false;
        }

        var header = loop.Header;
        var inSameRegionAsHeader = true;
        if (header.hasTryIndex)
        {
            var latchEHRegion = header.TryIndex;
            foreach (var backEdge in loop.BackEdges)
            {
                if (!bbInTryRegions(latchEHRegion, backEdge.SourceBlock))
                {
                    _ = ehTrueEnclosingTryIndex(latchEHRegion);
                    inSameRegionAsHeader = false;
                    break;
                }
            }
        }

        if (!inSameRegionAsHeader && !bbIsTryBeg(header))
        {
            JITDUMP($"Skip backedge canonicalization for L{loop.Index:D2}: header {FMT_BB(header.bbNum)} is not a try entry but has backedge sources outside its try region\n");
            return false;
        }

        // Redirection mutates the predecessor list containing these edges.
        var sources = new List<BasicBlock>(loop.BackEdges.Length);
        foreach (var edge in loop.BackEdges)
        {
            sources.Add(edge.SourceBlock);
        }

        BasicBlock latch;
        if (inSameRegionAsHeader)
        {
            latch = fgNewBBinRegion(BBJ_ALWAYS, header);
        }
        else
        {
            latch = fgNewBBbefore(BBJ_ALWAYS, header, false);
            fgSetEHRegionForNewPreheaderOrExit(latch);
        }

        latch.SetFlags(BBF_INTERNAL);
        latch.bbCodeOffs = header.bbCodeOffs;
        var newEdge = fgAddRefPred(header, latch);
        latch.TargetEdge = newEdge;
        JITDUMP($"Created new latch {FMT_BB(latch.bbNum)} for L{loop.Index:D2} in {(inSameRegionAsHeader ? "header's" : "header's enclosing")} EH region to merge {sources.Count} backedges\n");

        foreach (var source in sources)
        {
            JITDUMP($"  Backedge {FMT_BB(source.bbNum)} -> {FMT_BB(header.bbNum)} becomes {FMT_BB(source.bbNum)} -> {FMT_BB(latch.bbNum)}\n");
            fgReplaceJumpTarget(source, header, latch);
        }

        optSetWeightForPreheaderOrExit(loop, latch);
        return true;
    }

    private void optSetWeightForPreheaderOrExit(FlowGraphNaturalLoop loop, BasicBlock block)
    {
        var hasProfWeight = true;
        var newWeight = BB_ZERO_WEIGHT;
        foreach (var edge in block.PredEdges)
        {
            newWeight += edge.LikelyWeight;
            hasProfWeight &= edge.SourceBlock.hasProfileWeight;
        }

        block.bbWeight = newWeight;
        if (hasProfWeight)
        {
            block.SetFlags(BBF_PROF_WEIGHT);
        }
        else
        {
            block.RemoveFlags(BBF_PROF_WEIGHT);
        }
    }
}
