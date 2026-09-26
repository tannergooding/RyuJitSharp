// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgehopt.cpp.

using System;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    private static BasicBlock fgGetFinallyContinuation(BasicBlock leave)
    {
        assert(leave.Kind is BBJ_CALLFINALLYRET);
        return leave.Target;
    }

    public PhaseStatus fgRemoveEmptyFinally()
    {
        assert(!fgFuncletsCreated);
        assert(fgPredsComputed);

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
            jitprintf("\n*************** Before fgRemoveEmptyFinally()\n");
            fgDispBasicBlocks();
            fgDispHandlerTab();
            jitprintf("\n");
        }
#endif

        var finallyCount = 0;
        var emptyCount = 0;
        ushort xtnum = 0;
        while (xtnum < compHndBBtabCount)
        {
            ref var clause = ref compHndBBtab[xtnum];
            if (!clause.HasFinallyOrFaultHandler)
            {
                JITDUMP($"EH#{xtnum} is not a try-finally or try-fault; skipping.\n");
                xtnum++;
                continue;
            }

            finallyCount++;
            var firstBlock = clause.ebdHndBeg;
            var lastBlock = clause.ebdHndLast;
            if (firstBlock != lastBlock)
            {
                JITDUMP($"EH#{xtnum} handler has multiple basic blocks; skipping.\n");
                xtnum++;
                continue;
            }

            if ((firstBlock.Kind is BBJ_ALWAYS) && (firstBlock.Target == firstBlock))
            {
                JITDUMP($"EH#{xtnum} handler has basic block that jumps to itself; skipping.\n");
                xtnum++;
                continue;
            }

            var isEmpty = true;
            foreach (var stmt in firstBlock.Statements)
            {
                if (stmt.RootNode.Oper is not GT_RETFILT)
                {
                    isEmpty = false;
                    break;
                }
            }

            if (!isEmpty)
            {
                JITDUMP($"EH#{xtnum} handler is not empty; skipping.\n");
                xtnum++;
                continue;
            }

            assert(lastBlock.Kind is BBJ_EHFINALLYRET or BBJ_EHFAULTRET or BBJ_THROW);
            JITDUMP($"EH#{xtnum} has empty handler, removing the region.\n");

            if (clause.HasFinallyHandler)
            {
                ehGetCallFinallyBlockRange(xtnum, out var firstCallFinally, out var lastCallFinally);
                var current = (BasicBlock?)firstCallFinally;
                var end = lastCallFinally.Next;
                while (current != end)
                {
                    var callBlock = current
                        ?? throw new InvalidOperationException("Call-finally range ended prematurely.");
                    var next = callBlock.Next;
                    if ((callBlock.Kind is BBJ_CALLFINALLY) && (callBlock.Target == firstBlock))
                    {
                        noway_assert(callBlock.isBBCallFinallyPair);
                        var leave = callBlock.Next
                            ?? throw new InvalidOperationException("A call-finally pair requires its leave block.");
                        var continuation = fgGetFinallyContinuation(leave);
                        JITDUMP($"Modifying callfinally {FMT_BB(callBlock.bbNum)} leave " +
                            $"{FMT_BB(leave.bbNum)} finally {FMT_BB(firstBlock.bbNum)} " +
                            $"continuation {FMT_BB(continuation.bbNum)}\n");
                        JITDUMP($"so that {FMT_BB(callBlock.bbNum)} jumps to " +
                            $"{FMT_BB(continuation.bbNum)}; then remove {FMT_BB(leave.bbNum)}\n");

                        next = leave.Next;
                        fgPrepareCallFinallyRetForRemoval(leave);
                        _ = fgRemoveBlock(leave, unreachable: true);
                        fgRedirectEdge(ref callBlock.TargetEdgeRef, continuation);
                        callBlock.Kind = BBJ_ALWAYS;
                        callBlock.RemoveFlags(BBF_RETLESS_CALL);

                        if (callBlock.hasProfileWeight)
                        {
                            continuation.increaseBBProfileWeight(callBlock.bbWeight);
                        }

                        assert(leave != end);
                    }

                    current = next;
                }
            }

            JITDUMP($"Remove now-unreachable handler {FMT_BB(firstBlock.bbNum)}\n");
            firstBlock.bbRefs = 0;
            firstBlock.RemoveFlags(BBF_DONT_REMOVE);
            _ = fgRemoveBlock(firstBlock, unreachable: true);

            var firstTry = clause.ebdTryBeg;
            var lastTry = clause.ebdTryLast;
            assert(firstTry.TryIndex == xtnum);
            for (var block = firstTry; ; block = block.Next
                     ?? throw new InvalidOperationException("Try region ended prematurely."))
            {
                if (block.TryIndex == xtnum)
                {
                    if (firstBlock.hasTryIndex)
                    {
                        block.TryIndex = firstBlock.TryIndex;
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

            fgUpdateACDsBeforeEHTableEntryRemoval(xtnum);
            fgRemoveEHTableEntry(xtnum);
            firstTry.RemoveFlags(BBF_DONT_REMOVE);
            emptyCount++;
        }

        if (emptyCount > 0)
        {
            JITDUMP($"fgRemoveEmptyFinally() removed {emptyCount} try-finally/fault clauses " +
                $"from {finallyCount} finally/fault(s)\n");
            fgInvalidateDfsTree();
#if DEBUG
            if (verbose)
            {
                jitprintf("\n*************** After fgRemoveEmptyFinally()\n");
                fgDispBasicBlocks();
                fgDispHandlerTab();
                jitprintf("\n");
            }
#endif
        }

        return emptyCount > 0 ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
