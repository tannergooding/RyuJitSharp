// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgehopt.cpp.

using System;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgRemoveEmptyTryCatchOrTryFault()
    {
        JITDUMP("\n*************** In fgRemoveEmptyTryCatchOrTryFault()\n");
        assert(!fgFuncletsCreated);

        var enabled = true;
#if DEBUG
        enabled = JitConfig.JitEnableRemoveEmptyTryCatchOrTryFault == 1;
#endif
        if (!enabled)
        {
            JITDUMP("Empty try/catch/fault removal disabled.\n");
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
            jitprintf("\n*************** Before fgRemoveEmptyTryCatchOrTryFault()\n");
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
            if (clause.HasFinallyHandler)
            {
                JITDUMP($"EH#{xtnum} is not a try-catch or try-fault; skipping.\n");
                xtnum++;
                continue;
            }

            var firstTry = clause.ebdTryBeg;
            var lastTry = clause.ebdTryLast;
            var canThrow = false;
            for (var block = firstTry; ; block = block.Next
                     ?? throw new InvalidOperationException("Try region ended prematurely."))
            {
                if (block.TryIndex != xtnum)
                {
                    JITDUMP($"EH#{xtnum} try block {FMT_BB(block.bbNum)} " +
                        "is nested try entry; skipping.\n");
                    canThrow = true;
                    break;
                }

                foreach (var stmt in block.Statements)
                {
                    if ((stmt.RootNode.Flags & (GTF_EXCEPT | GTF_CALL)) != 0)
                    {
#if DEBUG
                        JITDUMP($"EH#{xtnum} {FMT_STMT(stmt.Id)} in {FMT_BB(block.bbNum)} " +
                            "can throw; skipping.\n");
#endif
                        canThrow = true;
                        break;
                    }
                }

                if (canThrow || (block == lastTry))
                {
                    break;
                }
            }

            if (canThrow)
            {
                xtnum++;
                continue;
            }

            JITDUMP($"EH#{xtnum} try has no statements that can throw\n");
            assert((firstTry.TryIndex == xtnum) && (lastTry.TryIndex == xtnum));

            var firstHandler = clause.ebdHndBeg;
            var lastHandler = clause.ebdHndLast;
            var handlerEnclosesTry = false;
            for (var block = firstHandler; ; block = block.Next
                     ?? throw new InvalidOperationException("Handler region ended prematurely."))
            {
                if (bbIsTryBeg(block))
                {
                    JITDUMP($"EH#{xtnum} handler block {FMT_BB(block.bbNum)} " +
                        "is nested try entry; skipping.\n");
                    handlerEnclosesTry = true;
                    break;
                }

                if (block == lastHandler)
                {
                    break;
                }
            }

            if (handlerEnclosesTry)
            {
                xtnum++;
                continue;
            }

            var enclosingTry = clause.ebdEnclosingTryIndex;
            for (var block = firstTry; ; block = block.Next
                     ?? throw new InvalidOperationException("Try region ended prematurely."))
            {
                if (block.TryIndex == xtnum)
                {
                    if (enclosingTry == EHblkDsc.NO_ENCLOSING_INDEX)
                    {
                        block.clearTryIndex();
                    }
                    else
                    {
                        block.TryIndex = enclosingTry;
                    }
                }

                if (block == lastTry)
                {
                    break;
                }
            }

            if (clause.HasFilter)
            {
                var firstFilter = clause.ebdFilter;
                var lastFilter = clause.BBFilterLast;
                assert(firstFilter.bbRefs >= 1);
                firstFilter.bbRefs--;
                var afterFilter = lastFilter.Next;
                for (var block = (BasicBlock?)firstFilter; block != afterFilter; block = block.Next)
                {
                    var filter = block
                        ?? throw new InvalidOperationException("Filter region ended prematurely.");
                    fgRemoveBlockAsPred(filter);
                    filter.Kind = BBJ_THROW;
                }

                for (var block = (BasicBlock?)firstFilter; block != afterFilter; block = block.Next)
                {
                    var filter = block
                        ?? throw new InvalidOperationException("Filter region ended prematurely.");
                    filter.RemoveFlags(BBF_DONT_REMOVE);
                    _ = fgRemoveBlock(filter, unreachable: true);
                }
            }

            assert(firstHandler.bbRefs >= 1);
            firstHandler.bbRefs--;
            var afterHandler = lastHandler.Next;
            for (var block = (BasicBlock?)firstHandler; block != afterHandler; block = block.Next)
            {
                var handler = block
                    ?? throw new InvalidOperationException("Handler region ended prematurely.");
                assert(!bbIsTryBeg(handler));
                if (handler.isBBCallFinallyPair)
                {
                    var tail = handler.Next
                        ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
                    fgPrepareCallFinallyRetForRemoval(tail);
                }

                fgRemoveBlockAsPred(handler);
                handler.Kind = BBJ_THROW;
            }

            for (var block = (BasicBlock?)firstHandler; block != afterHandler; block = block.Next)
            {
                var handler = block
                    ?? throw new InvalidOperationException("Handler region ended prematurely.");
                assert(!bbIsTryBeg(handler));
                handler.RemoveFlags(BBF_DONT_REMOVE);
                _ = fgRemoveBlock(handler, unreachable: true);
            }

            fgUpdateACDsBeforeEHTableEntryRemoval(xtnum);
            fgRemoveEHTableEntry(xtnum);
            if (!bbIsTryBeg(firstTry))
            {
                firstTry.RemoveFlags(BBF_DONT_REMOVE);
            }

            emptyCount++;
        }

        if (emptyCount > 0)
        {
            JITDUMP($"fgRemoveEmptyTryCatchOrTryFault() optimized {emptyCount} " +
                "empty-try catch/fault clauses\n");
            fgInvalidateDfsTree();
            return PhaseStatus.MODIFIED_EVERYTHING;
        }

        return PhaseStatus.MODIFIED_NOTHING;
    }
}
