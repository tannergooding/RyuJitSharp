// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgehopt.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgMergeFinallyChains()
    {
        assert(!fgFuncletsCreated);
        assert(fgPredsComputed);

        if (compHndBBtabCount == 0)
        {
            JITDUMP("No EH in this method, nothing to merge.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.MinOpts)
        {
            JITDUMP("Method compiled with MinOpts, no merging.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.compDbgCode)
        {
            JITDUMP("Method compiled with debug codegen, no merging.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (verbose)
        {
            fgDispBasicBlocks();
            fgDispHandlerTab();
            jitprintf("\n");
        }
#endif

        var hasFinally = false;
        for (var index = 0; index < compHndBBtabCount; index++)
        {
            if (compHndBBtab[index].HasFinallyHandler)
            {
                hasFinally = true;
                break;
            }
        }

        if (!hasFinally)
        {
            JITDUMP("Method does not have any try-finallys; no merging.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        // Process outside in so outer canonicalizations enable inner merges.
        var canMerge = false;
        var didMerge = false;
        var continuationMap = new Dictionary<BasicBlock, BasicBlock>();
        for (var xtnum = (int)compHndBBtabCount - 1; xtnum >= 0; xtnum--)
        {
            ref var clause = ref compHndBBtab[xtnum];
            if (!clause.HasFinallyHandler)
            {
                continue;
            }

            JITDUMP($"Examining callfinallys for EH#{xtnum}.\n");
            ehGetCallFinallyBlockRange((ushort)xtnum, out var firstCallFinally, out var lastCallFinally);
            continuationMap.Clear();
            var callFinallyCount = 0;
            var beginHandler = clause.ebdHndBeg;

            for (var block = firstCallFinally; ; block = block.Next
                     ?? throw new InvalidOperationException("Call-finally range ended prematurely."))
            {
                if (block.isBBCallFinallyPair && (block.Target == beginHandler))
                {
                    assert(block.isEmpty());
                    callFinallyCount++;
                    var leave = block.Next
                        ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
                    var continuation = fgGetFinallyContinuation(leave);
                    continuationMap.TryAdd(continuation, block);
                }

                if (block == lastCallFinally)
                {
                    break;
                }
            }

            JITDUMP($"EH#{xtnum} has {callFinallyCount} callfinallys, {continuationMap.Count} continuations\n");
            if (callFinallyCount <= continuationMap.Count)
            {
                JITDUMP($"EH#{xtnum} does not have any mergeable callfinallys\n");
                continue;
            }

            canMerge = true;
            for (var block = firstCallFinally; ; block = block.Next
                     ?? throw new InvalidOperationException("Call-finally range ended prematurely."))
            {
                var merged = fgRetargetBranchesToCanonicalCallFinallyCore(block, beginHandler, continuationMap);
                didMerge = didMerge || merged;

                if (block == lastCallFinally)
                {
                    break;
                }
            }
        }

        if (!canMerge)
        {
            JITDUMP("Method had try-finallys, but did not have any mergeable finally chains.\n");
        }
        else if (didMerge)
        {
            JITDUMP("Method had mergeable try-finallys and some callfinally merges were performed.\n");
#if DEBUG
            if (verbose)
            {
                jitprintf("\n*************** After fgMergeFinallyChains()\n");
                fgDispBasicBlocks();
                fgDispHandlerTab();
                jitprintf("\n");
            }
#endif
        }
        else
        {
            JITDUMP("Method had mergeable try-finallys but no callfinally merges were performed,\n" +
                "likely the non-canonical callfinallys were unreachable\n");
        }

        return didMerge ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool fgRetargetBranchesToCanonicalCallFinallyCore(
        BasicBlock block, BasicBlock handler, Dictionary<BasicBlock, BasicBlock> continuationMap)
    {
        if (block.Kind is not (BBJ_ALWAYS or BBJ_CALLFINALLYRET))
        {
            return false;
        }

        var callFinally = block.Target;
        if (!callFinally.isBBCallFinallyPair || (callFinally.Target != handler))
        {
            return false;
        }

        var leave = callFinally.Next
            ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
        var continuation = fgGetFinallyContinuation(leave);
        var canonical = continuationMap[continuation];
        assert(canonical is not null);

        if (block.Target == canonical)
        {
            JITDUMP($"{FMT_BB(block.bbNum)} already canonical\n");
            return false;
        }

        JITDUMP($"Redirecting branch in {FMT_BB(block.bbNum)} from {FMT_BB(callFinally.bbNum)} " +
            $"to {FMT_BB(canonical.bbNum)}.\n");
        assert(callFinally.bbRefs > 0);
        fgRedirectEdge(ref block.TargetEdgeRef, canonical);

        if (block.hasProfileWeight)
        {
            canonical.increaseBBProfileWeight(block.bbWeight);
            callFinally.decreaseBBProfileWeight(block.bbWeight);
        }

        return true;
    }
}
