// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, flowgraph.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public enum GCPollType
    {
        GCPOLL_NONE,
        GCPOLL_CALL,
        GCPOLL_INLINE,
    }

    private static bool blockNeedsGCPoll(BasicBlock block)
    {
        var blockMayNeedGCPoll = block.HasFlag(BBF_NEEDS_GCPOLL);
        for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
        {
            if ((stmt.RootNode.Flags & GTF_CALL) == 0)
            {
                continue;
            }

            foreach (var tree in stmt.TreeList)
            {
                if (tree is GenTreeCall call)
                {
                    if (call.IsUnmanaged)
                    {
                        // A regular unmanaged call already polls for the entire block.
                        if (!call.IsSuppressGCTransition)
                        {
                            return false;
                        }

                        blockMayNeedGCPoll = true;
                    }
                }
                else if (tree.Oper is GT_GCPOLL)
                {
                    blockMayNeedGCPoll = true;
                }
            }
        }

        return blockMayNeedGCPoll;
    }

    public PhaseStatus fgInsertGCPolls()
    {
        var result = PhaseStatus.MODIFIED_NOTHING;
        if ((optMethodFlags & OMF_NEEDS_GCPOLLS) == 0)
        {
            return result;
        }

        var createdPollBlocks = false;
        for (var block = fgFirstBB; block is not null; block = block.Next)
        {
            compCurBB = block;
            if (opts.OptimizationDisabled)
            {
                if (block.HasAnyFlag(BBF_HAS_SUPPRESSGC_CALL | BBF_NEEDS_GCPOLL) == 0)
                {
                    continue;
                }
            }
            else if (!blockNeedsGCPoll(block))
            {
                continue;
            }

            result = PhaseStatus.MODIFIED_EVERYTHING;
            assert((block.Kind is BBJ_RETURN or BBJ_ALWAYS or BBJ_COND or BBJ_SWITCH or BBJ_THROW or BBJ_CALLFINALLY) ||
                block.hasEHBoundaryOut);
            var pollType = GCPollType.GCPOLL_INLINE;

            if (opts.OptimizationDisabled)
            {
                JITDUMP($"Selecting CALL poll in block {FMT_BB(block.bbNum)} because of debug/minopts\n");
                pollType = GCPollType.GCPOLL_CALL;
            }
            else if (genReturnBB == block)
            {
                JITDUMP($"Selecting CALL poll in block {FMT_BB(block.bbNum)} because it is the single return block\n");
                pollType = GCPollType.GCPOLL_CALL;
            }
            else if (block.Kind is BBJ_SWITCH)
            {
                JITDUMP($"Selecting CALL poll in block {FMT_BB(block.bbNum)} because it is a SWITCH block\n");
                pollType = GCPollType.GCPOLL_CALL;
            }
            else if (block.hasEHBoundaryOut)
            {
                JITDUMP($"Selecting CALL poll in block {FMT_BB(block.bbNum)} because it ends an EH region\n");
                pollType = GCPollType.GCPOLL_CALL;
            }
            else if (block.HasFlag(BBF_COLD))
            {
                JITDUMP($"Selecting CALL poll in block {FMT_BB(block.bbNum)} because it is a cold block\n");
                pollType = GCPollType.GCPOLL_CALL;
            }

            var curBasicBlock = fgCreateGCPoll(pollType, block);
            createdPollBlocks |= block != curBasicBlock;
            block = curBasicBlock;
        }

        assert(!createdPollBlocks || opts.OptimizationEnabled);
        return result;
    }

    public unsafe BasicBlock fgCreateGCPoll(GCPollType pollType, BasicBlock block)
    {
        void* pAddrOfCaptureThreadGlobal;
        var addrTrap = info.compCompHnd->getAddrOfCaptureThreadGlobal(&pAddrOfCaptureThreadGlobal);
        if ((addrTrap == null) && (pAddrOfCaptureThreadGlobal == null))
        {
            pollType = GCPollType.GCPOLL_CALL;
        }

        var helper = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_POLL_GC);
        var call = fgMorphCall(helper);
        _ = gtSetEvalOrder(call);

        if (pollType is GCPollType.GCPOLL_CALL)
        {
            Statement newStmt;
            if (block.HasFlag(BBF_NEEDS_GCPOLL))
            {
                // Tail-call blocks need the probe before any argument setup.
                newStmt = gtNewStmt(call);
                fgInsertStmtAtBeg(block, newStmt);
            }
            else if (block.Kind is BBJ_ALWAYS or BBJ_CALLFINALLY)
            {
                newStmt = gtNewStmt(call);
                fgInsertStmtAtEnd(block, newStmt);
            }
            else
            {
                newStmt = fgNewStmtNearEnd(block, call);
                // Branches targeting the poll must retain the following statement's sequence point.
                if (newStmt.NextStmt is Statement nextStmt)
                {
                    newStmt.SetDebugInfo(nextStmt.DebugInfo);
                }
            }

            if (fgNodeThreading is not NodeThreading.None)
            {
                gtSetStmtInfo(newStmt);
                fgSetStmtSeq(newStmt);
            }

            block.SetFlags(BBF_GC_SAFE_POINT);
#if DEBUG
            if (verbose)
            {
                jitprintf($"*** creating GC Poll in block {FMT_BB(block.bbNum)}\n");
                gtDispBlockStmts(block);
            }
#endif
            return block;
        }

        assert(pollType is GCPollType.GCPOLL_INLINE);
        var top = block;
        var poll = fgNewBBafter(BBJ_ALWAYS, top, true);
        var bottom = fgNewBBafter(top.Kind, poll, true);
        var originalFlags = top.FlagsRaw | BBF_GC_SAFE_POINT;
        noway_assert((originalFlags & (BBF_SPLIT_NONEXIST & ~BBF_RETLESS_CALL)) == 0);
        top.FlagsRaw = originalFlags & (~(BBF_SPLIT_LOST | BBF_RETLESS_CALL) | BBF_GC_SAFE_POINT);
        bottom.SetFlags(originalFlags & (BBF_SPLIT_GAINED | BBF_IMPORTED | BBF_GC_SAFE_POINT | BBF_RETLESS_CALL));
        bottom.inheritWeight(top);
        poll.SetFlags(originalFlags & (BBF_SPLIT_GAINED | BBF_IMPORTED | BBF_GC_SAFE_POINT));
        poll.bbSetRunRarely();

        var pollStmt = gtNewStmt(call);
        fgInsertStmtAtEnd(poll, pollStmt);
        if (fgNodeThreading is not NodeThreading.None)
        {
            gtSetStmtInfo(pollStmt);
            fgSetStmtSeq(pollStmt);
        }

        if (top.Kind is BBJ_COND or BBJ_RETURN or BBJ_THROW)
        {
            var stmt = top.LastStmt;
            assert(stmt is not null);
            fgUnlinkStmt(top, stmt);
            fgInsertStmtAtEnd(bottom, stmt);
        }

#if ENABLE_FAST_GCPOLL_HELPER
        noway_assert(pAddrOfCaptureThreadGlobal == null);
#endif
        GenTree value;
        if (pAddrOfCaptureThreadGlobal != null)
        {
            var addr = gtNewIndOfIconHandleNode(TYP_I_IMPL, (nint)pAddrOfCaptureThreadGlobal, GTF_ICON_CONST_PTR);
            value = gtNewIndir(TYP_INT, addr, GTF_IND_NONFAULTING);
        }
        else
        {
            value = gtNewIndOfIconHandleNode(TYP_INT, (nint)addrTrap, GTF_ICON_GLOBAL_PTR);
        }

        // This unknown-location read is introduced after the optimizations that could hoist/cache it.
        var trapRelop = gtNewBinaryNode(GT_EQ, TYP_INT, value, gtNewIconNode(TYP_INT, 0));
        trapRelop.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
        var trapCheck = gtNewUnaryNode(GT_JTRUE, TYP_VOID, trapRelop);
        _ = gtSetEvalOrder(trapCheck);
        var trapCheckStmt = gtNewStmt(trapCheck);
        fgInsertStmtAtEnd(top, trapCheckStmt);
        if (fgNodeThreading is not NodeThreading.None)
        {
            gtSetStmtInfo(trapCheckStmt);
            fgSetStmtSeq(trapCheckStmt);
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"Adding trapCheck in {FMT_BB(top.bbNum)}\n");
            gtDispTree(trapCheck);
        }
#endif
        var trueEdge = fgAddRefPred(bottom, top);
        var falseEdge = fgAddRefPred(poll, top);
        trueEdge.Likelihood = 1.0;
        falseEdge.Likelihood = 0.0;
        poll.TargetEdge = fgAddRefPred(bottom, poll);
        assert(poll.JumpsToNext);

        switch (top.Kind)
        {
            case BBJ_RETURN:
            case BBJ_THROW:
            {
                break;
            }

            case BBJ_COND:
            {
                var oldFalseEdge = top.FalseEdge;
                var oldTrueEdge = top.TrueEdge;
                fgReplacePred(oldFalseEdge, bottom);
                if (oldTrueEdge != oldFalseEdge)
                {
                    fgReplacePred(oldTrueEdge, bottom);
                }

                break;
            }

            case BBJ_ALWAYS:
            case BBJ_CALLFINALLY:
            {
                fgReplacePred(top.TargetEdge, bottom);
                break;
            }

            case BBJ_SWITCH:
            {
                throw new FatalJitException("SWITCH should be a call rather than an inlined poll.");
            }

            default:
            {
                throw new FatalJitException("Unknown block type for updating predecessor lists.");
            }
        }

        bottom.TransferTarget(top);
        top.SetCond(trueEdge, falseEdge);
        if (compCurBB == top)
        {
            compCurBB = bottom;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"*** creating inlined GC Poll in top block {FMT_BB(top.bbNum)}\n");
            gtDispBlockStmts(top);
            jitprintf($" poll block is {FMT_BB(poll.bbNum)}\n");
            gtDispBlockStmts(poll);
            jitprintf($" bottom block is {FMT_BB(bottom.bbNum)}\n");
            gtDispBlockStmts(bottom);
            jitprintf("\nAfter this change in fgCreateGCPoll the BB graph is:");
            fgDispBasicBlocks(false);
        }
#endif
        return bottom;
    }
}
