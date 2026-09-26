// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgehopt.cpp.

using System;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgRemoveEmptyTry()
    {
        JITDUMP("\n*************** In fgRemoveEmptyTry()\n");
        assert(!fgFuncletsCreated);
        assert(fgPredsComputed);

        var enabled = true;
#if DEBUG
        enabled = JitConfig.JitEnableRemoveEmptyTry == 1;
#endif
        if (!enabled)
        {
            JITDUMP("Empty try removal disabled.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (compHndBBtabCount == 0)
        {
            JITDUMP("No EH in this method, nothing to remove.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.MinOpts)
        {
            JITDUMP("Method compiled with MinOpts, no removal.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (opts.compDbgCode)
        {
            JITDUMP("Method compiled with debug codegen, no removal.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf("\n*************** Before fgRemoveEmptyTry()\n");
            fgDispBasicBlocks();
            fgDispHandlerTab();
            jitprintf("\n");
        }
#endif

        var emptyCount = 0;
        ushort xtnum = 0;
        while (xtnum < compHndBBtabCount)
        {
            ref var clause = ref compHndBBtab[xtnum];
            if (!clause.HasFinallyHandler)
            {
                JITDUMP($"EH#{xtnum} is not a try-finally; skipping.\n");
                xtnum++;
                continue;
            }

            var firstTry = clause.ebdTryBeg;
            var lastTry = clause.ebdTryLast;
            var firstHandler = clause.ebdHndBeg;
            var lastHandler = clause.ebdHndLast;
            assert(firstTry.TryIndex == xtnum);

            var canThrow = false;
            if (!firstTry.IsEmpty)
            {
                foreach (var stmt in firstTry.Statements)
                {
                    if ((stmt.RootNode.Flags & (GTF_EXCEPT | GTF_CALL)) != 0)
                    {
                        canThrow = true;
                        break;
                    }
                }
            }

            if (canThrow)
            {
                JITDUMP($"EH#{xtnum} first try block {FMT_BB(firstTry.bbNum)} " +
                    "can throw exception; skipping.\n");
                xtnum++;
                continue;
            }

            if (firstTry.Kind is not BBJ_ALWAYS)
            {
                JITDUMP($"EH#{xtnum} first try block {FMT_BB(firstTry.bbNum)} " +
                    "not jump to a callfinally; skipping.\n");
                xtnum++;
                continue;
            }

            var callFinally = firstTry.Target;
            if (!callFinally.isBBCallFinallyPair || (callFinally.Target != firstHandler))
            {
                JITDUMP($"EH#{xtnum} first try block {FMT_BB(firstTry.bbNum)} " +
                    "always jumps but not to a callfinally; skipping.\n");
                xtnum++;
                continue;
            }

            if (firstTry != lastTry)
            {
                JITDUMP($"EH#{xtnum} first try block {FMT_BB(firstTry.bbNum)} " +
                    "not only block in try; skipping.\n");
                xtnum++;
                continue;
            }

            JITDUMP($"EH#{xtnum} has empty try, removing the try region and promoting the finally.\n");
            ehGetCallFinallyBlockRange(xtnum, out var firstCallFinally, out var lastCallFinally);
            var singleCallFinally = true;
            for (var block = firstCallFinally; ; block = block.Next
                     ?? throw new InvalidOperationException("Call-finally range ended prematurely."))
            {
                if ((block.Kind is BBJ_CALLFINALLY) && (block.Target == firstHandler))
                {
                    assert(block.isBBCallFinallyPair);
                    if (block != callFinally)
                    {
                        JITDUMP($"EH#{xtnum} found unexpected (likely unreachable) " +
                            $"callfinally {FMT_BB(block.bbNum)}; skipping.\n");
                        singleCallFinally = false;
                        break;
                    }
                }

                if (block == lastCallFinally)
                {
                    break;
                }
            }

            if (!singleCallFinally)
            {
                JITDUMP($"EH#{xtnum} -- unexpectedly -- has multiple callfinallys; skipping.\n");
                xtnum++;
                continue;
            }

            var leave = callFinally.Next
                ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
            var continuation = fgGetFinallyContinuation(leave);
            for (var block = firstTry; ; block = block.Next
                     ?? throw new InvalidOperationException("Try region ended prematurely."))
            {
                if (block.TryIndex == xtnum)
                {
                    if (firstHandler.hasTryIndex)
                    {
                        block.TryIndex = firstHandler.TryIndex;
                    }
                    else
                    {
                        block.clearTryIndex();
                    }
                }

                if (block == lastTry)
                {
                    break;
                }
            }

            fgPrepareCallFinallyRetForRemoval(leave);
            _ = fgRemoveBlock(leave, unreachable: true);
            assert(callFinally.HasInitializedTarget);
            callFinally.Kind = BBJ_ALWAYS;
            callFinally.RemoveFlags(BBF_RETLESS_CALL);

            for (var block = firstHandler; ; block = block.Next
                     ?? throw new InvalidOperationException("Handler region ended prematurely."))
            {
                if (block == firstHandler)
                {
                    block.CatchType = BBCT_NONE;
                }

                if (block.HndIndex == xtnum)
                {
                    if (firstTry.hasHndIndex)
                    {
                        block.HndIndex = firstTry.HndIndex;
                    }
                    else
                    {
                        block.clearHndIndex();
                    }

                    if (block.Kind is BBJ_EHFINALLYRET)
                    {
                        var ret = block.LastStmt
                            ?? throw new InvalidOperationException("Finally return requires a statement.");
                        assert(ret.RootNode.Oper is GT_RETFILT);
                        fgRemoveStmt(block, ret);
                        var edge = fgAddRefPred(continuation, block);
                        block.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
                        if (continuation.hasProfileWeight)
                        {
                            continuation.increaseBBProfileWeight(block.bbWeight);
                        }
                    }
                }

                if (block == lastHandler)
                {
                    break;
                }
            }

            fgUpdateACDsBeforeEHTableEntryRemoval(xtnum);
            fgRemoveEHTableEntry(xtnum);
            assert(firstHandler.bbRefs >= 2);
            firstHandler.bbRefs--;
            firstTry.RemoveFlags(BBF_DONT_REMOVE);
            assert(!bbIsHandlerBeg(firstHandler));
            firstHandler.RemoveFlags(BBF_DONT_REMOVE);
            emptyCount++;
        }

        if (emptyCount > 0)
        {
            JITDUMP($"fgRemoveEmptyTry() optimized {emptyCount} empty-try try-finally clauses\n");
            fgInvalidateDfsTree();
            return PhaseStatus.MODIFIED_EVERYTHING;
        }

        return PhaseStatus.MODIFIED_NOTHING;
    }
}
