// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System.Collections.Generic;
using static RyuJitSharp.BasicBlockVisit;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optMakeLoopDownwardsCounted(ScalarEvolutionContext scevContext,
        FlowGraphNaturalLoop loop, PerLoopInfo loopInfo)
    {
        JITDUMP($"Checking if we should make L{loop.Index:D2} downwards counted\n");
        var changed = false;
        optVisitBoundingExitingCondBlocks(loop, exiting => {
            JITDUMP($"  Considering exiting block {FMT_BB(exiting.bbNum)}\n");
            changed |= optMakeExitTestDownwardsCounted(scevContext, loop, exiting, loopInfo);
        });
        return changed;
    }

    private bool optMakeExitTestDownwardsCounted(ScalarEvolutionContext scevContext,
        FlowGraphNaturalLoop loop, BasicBlock exiting, PerLoopInfo loopInfo)
    {
        assert(exiting.Kind is BBJ_COND);
        var jtrueStmt = exiting.LastStmt
            ?? throw new FatalJitException("A conditional loop exit must have a terminal statement.");
        var jtrue = jtrueStmt.RootNode;
        assert(jtrue.Oper is GT_JTRUE);
        var cond = jtrue.AsUnOp().Op1;
        if (!optCanAndShouldChangeExitTest(cond, dump: true))
        {
            return false;
        }

        var removableLocals = new List<int>();
        for (var stmt = loop.Header.FirstStmt; (stmt is not null) && stmt.IsPhiDefnStmt; stmt = stmt.NextStmt)
        {
            var candidateLclNum = stmt.RootNode.AsLclVarCommon().LclNum;
            if (optLocalHasNonLoopUses(candidateLclNum, loop, loopInfo))
            {
                continue;
            }

            var hasUseInTest = false;
            if (!loopInfo.VisitStatementsWithOccurrences(loop, candidateLclNum, (_, occurrenceStmt) => {
                if (occurrenceStmt == jtrueStmt)
                {
                    hasUseInTest = true;
                    return true;
                }
                return optIsUpdateOfIVWithoutSideEffects(occurrenceStmt.RootNode, candidateLclNum);
            }))
            {
                continue;
            }
            if (!hasUseInTest)
            {
                continue;
            }

            JITDUMP($"  Expecting to be able to remove V{candidateLclNum:D2} " +
                "by making this loop reverse counted\n");
            removableLocals.Add(candidateLclNum);
        }

        var checkProfitability = !compStressCompile(STRESS_DOWNWARDS_COUNTED_LOOPS, 50);
        if (checkProfitability && (removableLocals.Count == 0))
        {
            JITDUMP("  Found no potentially removable locals when making this loop downwards counted\n");
            return false;
        }
        if (loop.MayExecuteBlockMultipleTimesPerIteration(exiting))
        {
            JITDUMP("  Exiting block may be executed multiple times per iteration; " +
                "cannot place decrement in it\n");
            return false;
        }

        var backedgeCount = scevContext.ComputeExitNotTakenCount(exiting);
        if (backedgeCount is null)
        {
            JITDUMP("  Could not compute backedge count -- not a counted loop\n");
            return false;
        }

        var preheader = loop.GetPreheader()
            ?? throw new FatalJitException("The counted loop must have a preheader.");
        // Adding one to the backedge count gives the trip count even if it wraps.
        var tripCount = scevContext.Simplify(scevContext.NewBinop(ScevOper.Add,
            backedgeCount, scevContext.NewConstant(backedgeCount.Type, 1)));
        var tripCountNode = scevContext.Materialize(tripCount);
        if (tripCountNode is null)
        {
            JITDUMP("  Could not materialize trip count into IR\n");
            return false;
        }

        JITDUMP($"  Converting L{loop.Index:D2} into a downwards loop\n");
        var tripCountLcl = lvaGrabTemp(false, "Trip count IV");
        var store = gtNewTempStore(tripCountLcl, tripCountNode);
        var newStmt = fgNewStmtFromTree(store);
        fgInsertStmtAtEnd(preheader, newStmt);
        JITDUMP("  Inserted initialization of tripcount local\n\n");
        DISPSTMT(newStmt);

        var exitOp = loop.ContainsBlock(exiting.TrueTarget) ? GT_NE : GT_EQ;
        var negOne = tripCount.Type is TYP_LONG
            ? (GenTree)gtNewLconNode(-1)
            : gtNewIconNode(tripCount.Type, -1);
        var decremented = gtNewBinaryNode(GT_ADD, tripCount.Type,
            gtNewLclVarNode(tripCount.Type, tripCountLcl), negOne);
        store = gtNewTempStore(tripCountLcl, decremented);
        newStmt = fgNewStmtFromTree(store);
        fgInsertStmtNearEnd(exiting, newStmt);
        JITDUMP("\n  Inserted decrement of tripcount local\n\n");
        DISPSTMT(newStmt);

        cond.SetOper(exitOp);
        cond.AsOp().Op1 = gtNewLclVarNode(tripCount.Type, tripCountLcl);
        cond.AsOp().Op2 = gtNewZeroConNode(tripCount.Type);
        gtSetStmtInfo(jtrueStmt);
        fgSetStmtSeq(jtrueStmt);
        JITDUMP("\n  Updated exit test:\n");
        DISPSTMT(jtrueStmt);
        JITDUMP("\n");

        loopInfo.Invalidate(loop);
        return true;
    }
}
