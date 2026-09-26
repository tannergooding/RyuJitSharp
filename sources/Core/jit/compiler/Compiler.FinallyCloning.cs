// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgehopt.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgCloneFinally()
    {
        assert(!fgFuncletsCreated);
        assert(fgPredsComputed);

        var enableCloning = true;
#if DEBUG
        enableCloning = JitConfig.JitEnableFinallyCloning == 1;
#endif
        if (!enableCloning)
        {
            JITDUMP("Finally cloning disabled.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (compHndBBtabCount == 0)
        {
            JITDUMP("No EH in this method, no cloning.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.MinOpts)
        {
            JITDUMP("Method compiled with MinOpts, no cloning.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.compDbgCode)
        {
            JITDUMP("Method compiled with debug codegen, no cloning.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (verbose)
        {
            fgDispBasicBlocks();
            fgDispHandlerTab();
            jitprintf("\n");
        }
        fgDebugCheckTryFinallyExitsCore();
#endif

        var cloneCount = 0;
        for (ushort xtnum = 0; xtnum < compHndBBtabCount; xtnum++)
        {
            ref var clause = ref compHndBBtab[xtnum];
            if (!clause.HasFinallyHandler)
            {
                JITDUMP($"EH#{xtnum} is not a try-finally; skipping.\n");
                continue;
            }

            var enclosingHandler = ehGetEnclosingHndIndex(xtnum);
            if (enclosingHandler != EHblkDsc.NO_ENCLOSING_INDEX)
            {
                JITDUMP($"EH#{xtnum} is enclosed by handler EH#{enclosingHandler}; skipping.\n");
                continue;
            }

            var containsEH = false;
            ushort enclosedHandler = 0;
            for (ushort index = 0; index < xtnum; index++)
            {
                if (ehGetEnclosingHndIndex(index) == xtnum)
                {
                    enclosedHandler = index;
                    containsEH = true;
                    break;
                }
            }

            if (containsEH)
            {
                JITDUMP($"Finally for EH#{xtnum} encloses handler EH#{enclosedHandler}; skipping.\n");
                continue;
            }

            var firstBlock = clause.ebdHndBeg;
            var lastBlock = clause.ebdHndLast;
            var nextBlock = lastBlock.Next;
            var regionBBCount = 0;
            var regionStmtCount = 0;
            var hasFinallyRet = false;
            var hasSwitch = false;

            for (var block = firstBlock; ; block = block.Next
                     ?? throw new InvalidOperationException("Finally handler range ended prematurely."))
            {
                if (block.Kind is BBJ_SWITCH)
                {
                    hasSwitch = true;
                    break;
                }

                regionBBCount++;
                foreach (var _ in block.Statements)
                {
                    regionStmtCount++;
                }

                hasFinallyRet |= block.Kind is BBJ_EHFINALLYRET;
                if (block == lastBlock)
                {
                    break;
                }
            }

            if (hasSwitch)
            {
                JITDUMP($"Finally in EH#{xtnum} has a switch; skipping.\n");
                continue;
            }

            if (!hasFinallyRet)
            {
                JITDUMP($"Finally in EH#{xtnum} does not return; skipping.\n");
                continue;
            }

            const int stmtCountLimit = 15;
            if (regionStmtCount > stmtCountLimit)
            {
                JITDUMP($"Finally in EH#{xtnum} has {regionStmtCount} statements, " +
                    $"limit is {stmtCountLimit}; skipping.\n");
                continue;
            }

            JITDUMP($"EH#{xtnum} is a candidate for finally cloning: " +
                $"{regionBBCount} blocks, {regionStmtCount} statements\n");

            var firstTry = clause.ebdTryBeg;
            var lastTry = clause.ebdTryLast;
            assert(firstTry.TryIndex == xtnum);
            assert(bbInTryRegions(xtnum, lastTry));
            BasicBlock? normalCallFinally = null;
            BasicBlock? normalContinuation = null;
            var cloneInsertAfter = lastTry;
            var usingProfileWeights = fgIsUsingProfileWeights;
            var currentWeight = BB_ZERO_WEIGHT;

            for (var block = lastTry; ; block = block.Prev
                     ?? throw new InvalidOperationException("Try region ended prematurely."))
            {
                var jumpDest = block.Kind is BBJ_ALWAYS or BBJ_CALLFINALLYRET ? block.Target : null;
                if ((jumpDest is null) || !jumpDest.isBBCallFinallyPair || (jumpDest.Target != firstBlock))
                {
                    if (block == firstTry)
                    {
                        break;
                    }
                    continue;
                }

                var leave = jumpDest.Next
                    ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
                var continuation = fgGetFinallyContinuation(leave);
                var isUpdate = false;
                if (normalCallFinally is null)
                {
                    normalCallFinally = jumpDest;
                    normalContinuation = continuation;
                    if (usingProfileWeights)
                    {
                        if (block.hasProfileWeight)
                        {
                            JITDUMP($"Found profiled {FMT_BB(block.bbNum)} " +
                                $"with weight {FMT_WT(block.bbWeight)}\n");
                            currentWeight = block.bbWeight;
                        }
                        else
                        {
                            JITDUMP($"Found unprofiled {FMT_BB(block.bbNum)}\n");
                        }
                    }
                }
                else
                {
                    assert(usingProfileWeights);
                    if (!block.hasProfileWeight)
                    {
                        JITDUMP($"Skipping past unprofiled {FMT_BB(block.bbNum)}\n");
                        if (block == firstTry)
                        {
                            break;
                        }
                        continue;
                    }

                    if (block.bbWeight <= currentWeight)
                    {
                        JITDUMP($"Skipping past {FMT_BB(block.bbNum)} " +
                            $"with weight {FMT_WT(block.bbWeight)}\n");
                        if (block == firstTry)
                        {
                            break;
                        }
                        continue;
                    }

                    JITDUMP($"Preferring {FMT_BB(block.bbNum)} since " +
                        $"{FMT_WT(block.bbWeight)} >  {FMT_WT(currentWeight)}\n");
                    normalCallFinally = jumpDest;
                    normalContinuation = continuation;
                    currentWeight = block.bbWeight;
                    isUpdate = true;
                }

                assert(!jumpDest.hasHndIndex);
                cloneInsertAfter = leave;
                JITDUMP($"{(isUpdate ? "Updating" : "Choosing")} path to clone: " +
                    $"try block {FMT_BB(block.bbNum)} jumps to callfinally at {FMT_BB(jumpDest.bbNum)}; " +
                    $"the call returns to {FMT_BB(leave.bbNum)} which jumps to " +
                    $"{FMT_BB(continuation.bbNum)}\n");
                if (!usingProfileWeights)
                {
                    break;
                }

                if (block == firstTry)
                {
                    break;
                }
            }

            if (normalCallFinally is null || normalContinuation is null)
            {
                JITDUMP($"EH#{xtnum}: no calls from the try to the finally, skipping.\n");
                continue;
            }

            JITDUMP($"Will update callfinally block {FMT_BB(normalCallFinally.bbNum)} to jump to the clone; " +
                $"clone will jump to {FMT_BB(normalContinuation.bbNum)}\n");
            ehGetCallFinallyBlockRange(xtnum, out var firstCallFinally, out var lastCallFinally);

            var finallyTryIndex = firstBlock.bbTryIndex;
            BasicBlock? insertAfter = null;
            var blockMap = new Dictionary<BasicBlock, BasicBlock>();
            var cloneBBCount = 0;
            var originalWeight = BB_ZERO_WEIGHT;
            if (firstBlock.hasProfileWeight)
            {
                originalWeight = firstBlock.bbWeight;
                foreach (var pred in firstBlock.PredBlocks)
                {
                    if (pred.Kind is not BBJ_CALLFINALLY)
                    {
                        originalWeight = Math.Max(BB_ZERO_WEIGHT, originalWeight - pred.bbWeight);
                    }
                }
            }

            for (var block = firstBlock; ; block = block.Next
                     ?? throw new InvalidOperationException("Finally handler range ended prematurely."))
            {
                BasicBlock newBlock;
                if (block == firstBlock)
                {
                    newBlock = fgNewBBinRegion(BBJ_ALWAYS, finallyTryIndex, 0, cloneInsertAfter);
                    if (newBlock.Next == nextBlock)
                    {
                        assert(newBlock.Prev == lastBlock);
                    }
                }
                else
                {
                    newBlock = fgNewBBafter(BBJ_ALWAYS, insertAfter
                        ?? throw new InvalidOperationException("A subsequent clone needs its predecessor."),
                        extendRegion: true);
                }

                cloneBBCount++;
                assert(cloneBBCount <= regionBBCount);
                insertAfter = newBlock;
                blockMap.Add(block, newBlock);
                BasicBlock.CloneBlockState(this, newBlock, block);

                if (block == firstBlock)
                {
                    newBlock.SetFlags(BBF_CLONED_FINALLY_BEGIN);
                }

                if (block == lastBlock)
                {
                    newBlock.SetFlags(BBF_CLONED_FINALLY_END);
                }

                newBlock.RemoveFlags(BBF_DONT_REMOVE);
                assert(newBlock.bbTryIndex == finallyTryIndex);
                newBlock.clearHndIndex();
                assert(newBlock.Kind is BBJ_ALWAYS);
                assert(!newBlock.HasInitializedTarget);

                if (block == lastBlock)
                {
                    break;
                }
            }

            assert(cloneBBCount == regionBBCount);
            JITDUMP($"Cloned finally blocks are: {FMT_BB(blockMap[firstBlock].bbNum)} " +
                $"... {FMT_BB(blockMap[lastBlock].bbNum)}\n");

            for (var block = firstBlock; ; block = block.Next
                     ?? throw new InvalidOperationException("Finally handler range ended prematurely."))
            {
                var clone = blockMap[block];
                assert(clone.Kind is BBJ_ALWAYS);
                assert(!clone.HasInitializedTarget);
                if (block.Kind is BBJ_EHFINALLYRET)
                {
                    var ret = clone.LastStmt
                        ?? throw new InvalidOperationException("A finally return requires a statement.");
                    assert(ret.RootNode.Oper is GT_RETFILT);
                    fgRemoveStmt(clone, ret);
                    var edge = fgAddRefPred(normalContinuation, clone);
                    clone.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
                }
                else
                {
                    optSetMappedBlockTargets(block, clone, blockMap);
                }

                if (block == lastBlock)
                {
                    break;
                }
            }

            var firstClone = blockMap[firstBlock];
            var retargetedAllCalls = true;
            var retargetedWeight = BB_ZERO_WEIGHT;
            var call = firstCallFinally;
            var endCallFinally = lastCallFinally.Next;
            while (call != endCallFinally)
            {
                var next = call.Next;
                if (call.isBBCallFinallyPair && (call.Target == firstBlock))
                {
                    var leave = call.Next
                        ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
                    var continuation = fgGetFinallyContinuation(leave);
                    if (continuation == normalContinuation)
                    {
                        JITDUMP($"Retargeting callfinally {FMT_BB(call.bbNum)} " +
                            $"to clone entry {FMT_BB(firstClone.bbNum)}\n");
                        next = leave.Next;
                        fgPrepareCallFinallyRetForRemoval(leave);
                        _ = fgRemoveBlock(leave, unreachable: true);
                        fgRedirectEdge(ref call.TargetEdgeRef, firstClone);
                        call.RemoveFlags(BBF_RETLESS_CALL);
                        call.Kind = BBJ_ALWAYS;
                        assert(leave != endCallFinally);
                        if (call.hasProfileWeight)
                        {
                            retargetedWeight += call.bbWeight;
                        }
                    }
                    else
                    {
                        JITDUMP($"Can't retarget callfinally in {FMT_BB(call.bbNum)} " +
                            $"as it jumps to {FMT_BB(continuation.bbNum)}, " +
                            $"not {FMT_BB(normalContinuation.bbNum)}\n");
                        retargetedAllCalls = false;
                    }
                }

                if (next == endCallFinally)
                {
                    break;
                }
                call = next ?? throw new InvalidOperationException("Call-finally range ended prematurely.");
            }

            if (retargetedAllCalls)
            {
                JITDUMP("All callfinallys retargeted; changing finally to fault.\n");
                clause.ebdHandlerType = EH_HANDLER_FAULT_WAS_FINALLY;
                firstBlock.CatchType = bbCatchType.BBCT_FAULT;
                for (var block = firstBlock; ; block = block.Next
                         ?? throw new InvalidOperationException("Finally handler range ended prematurely."))
                {
                    if (block.Kind is BBJ_EHFINALLYRET)
                    {
                        assert(block.EhfTargets?.Succs.Length == 0);
                        block.Kind = BBJ_EHFAULTRET;
                    }

                    if (block == lastBlock)
                    {
                        break;
                    }
                }
            }
            else
            {
                JITDUMP("Some callfinallys *not* retargeted, so region must remain as a finally.\n");
            }

            firstClone.CatchType = bbCatchType.BBCT_NONE;
            if (usingProfileWeights && (originalWeight > BB_ZERO_WEIGHT))
            {
                var clonedScale = retargetedWeight < originalWeight
                    ? retargetedWeight / originalWeight : 1.0;
                var originalScale = 1.0 - clonedScale;
                JITDUMP($"Profile scale factor ({FMT_WT(retargetedWeight)}/{FMT_WT(originalWeight)}) " +
                    $"=> clone {FMT_WT(clonedScale)} / original {FMT_WT(originalScale)}\n");
                for (var block = firstBlock; ; block = block.Next
                         ?? throw new InvalidOperationException("Finally handler range ended prematurely."))
                {
                    var weight = block.bbWeight;
                    block.setBBProfileWeight(weight * originalScale);
                    JITDUMP($"Set weight of {FMT_BB(block.bbNum)} to {FMT_WT(block.bbWeight)}\n");
                    var clone = blockMap[block];
                    clone.setBBProfileWeight(weight * clonedScale);
                    JITDUMP($"Set weight of {FMT_BB(clone.bbNum)} to {FMT_WT(clone.bbWeight)}\n");

                    if (block == lastBlock)
                    {
                        break;
                    }
                }

                if (!retargetedAllCalls)
                {
                    JITDUMP($"Reduced flow out of EH{xtnum} needs to be propagated to " +
                        $"continuation block(s). Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                    fgPgoConsistent = false;
                }
            }

            if (normalContinuation.hasProfileWeight)
            {
                normalContinuation.setBBProfileWeight(normalContinuation.computeIncomingWeight());
            }

            JITDUMP($"\nDone with EH#{xtnum}\n\n");
            cloneCount++;
        }

        if (cloneCount > 0)
        {
            JITDUMP($"fgCloneFinally() cloned {cloneCount} finally handlers\n");
#if DEBUG
            if (verbose)
            {
                fgDispBasicBlocks();
                fgDispHandlerTab();
                jitprintf("\n");
            }
            fgDebugCheckTryFinallyExitsCore();
#endif
        }

        return cloneCount > 0 ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
