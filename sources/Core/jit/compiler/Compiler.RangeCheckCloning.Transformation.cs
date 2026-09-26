// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, rangecheckcloning.cpp and fgbasic.cpp.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private void RemoveClonedBoundsCheck(GenTreeBoundsChk check, Statement stmt)
    {
        JITDUMP("Before RemoveBoundsChk:\n");
        DISPTREE(check);

        GenTree? sideEffects = null;
        gtExtractSideEffList(check, ref sideEffects, GTF_SIDE_EFFECT, ignoreRoot: true);
        var link = gtFindLink(stmt, check);
        if (Unsafe.IsNullRef(ref link.result))
        {
            throw new FatalJitException("The bounds check to remove is not in its original statement.");
        }
        link.result = sideEffects ?? gtNewNothingNode();

        gtUpdateStmtSideEffects(stmt);
        gtSetStmtInfo(stmt);
        fgSetStmtSeq(stmt);

        JITDUMP("After RemoveBoundsChk:\n");
        DISPTREE(stmt.RootNode);
    }

    private BasicBlock SplitRangeCheckBlockBeforeTree(BasicBlock block, Statement stmt, GenTree splitPoint,
        out Statement? firstNewStmt)
    {
        _ = gtSplitTree(block, stmt, splitPoint, out firstNewStmt, out _);
        var originalFlags = block.FlagsRaw;
        var previous = block;

        if (stmt == block.FirstStmt)
        {
            block = fgSplitBlockAtBeginning(previous);
        }
        else
        {
            var before = stmt.PrevStmt ?? throw new FatalJitException("A non-first statement needs a predecessor.");
            assert(before != block.LastStmt);
#if DEBUG
            JITDUMP($"Splitting {FMT_BB(previous.bbNum)} after statement {FMT_STMT(before.Id)}\n");
#endif
            block = fgSplitBlockAfterStatement(previous, before);
        }

        previous.FlagsRaw = originalFlags & (~(BBF_SPLIT_LOST | BBF_RETLESS_CALL) | BBF_GC_SAFE_POINT);
        block.SetFlags(originalFlags & (BBF_SPLIT_GAINED | BBF_IMPORTED | BBF_GC_SAFE_POINT | BBF_RETLESS_CALL));
        assert(previous.Kind is BBJ_ALWAYS && previous.JumpsToNext && previous.Next == block);

        return block;
    }

    private BasicBlock optRangeCheckCloningDoClone(BasicBlock block, List<BoundsCheckInfo> checks, Statement lastStmt)
    {
        assert(checks.Count > 0);
        var first = checks[0];
        var previous = block;
        var fast = SplitRangeCheckBlockBeforeTree(block, first.Stmt, first.Check, out var newFirstStmt);

        while ((newFirstStmt is not null) && (newFirstStmt != first.Stmt))
        {
            fgMorphStmtBlockOps(fast, newFirstStmt);
            newFirstStmt = newFirstStmt.NextStmt;
        }
        fgMorphStmtBlockOps(fast, first.Stmt);
        gtUpdateStmtSideEffects(first.Stmt);

        var isReturn = (fast.Kind is BBJ_RETURN) && (lastStmt == fast.LastStmt);
        BasicBlock? last = null;
        if (!isReturn)
        {
            last = fgSplitBlockAfterStatement(fast, lastStmt);
        }

        var firstStmt = fast.FirstStmt ?? throw new FatalJitException("The fast path must contain a statement.");
        var debugInfo = firstStmt.DebugInfo;
        var offset = 0;
        for (var i = checks.Count - 1; i >= 0; i--)
        {
            offset = Math.Max(offset, checks[i].Offset);
        }
        assert(offset >= 0);

        var idx = gtCloneExpr(first.Check.Index)
            ?? throw new FatalJitException("The first bounds-check index cannot be cloned.");
        var arrLen = gtCloneExpr(first.Check.ArrayLength)
            ?? throw new FatalJitException("The first bounds-check length cannot be cloned.");
        assert((idx.Flags & GTF_ALL_EFFECT) == 0);
        assert((arrLen.Flags & GTF_ALL_EFFECT) == 0);

        GenTree idxClone;
        if (first.Offset > 0)
        {
            idx = gtNewBinaryNode(GT_ADD, TYP_INT, idx, gtNewIconNode(TYP_INT, -first.Offset));
            idxClone = fgInsertCommaFormTemp(ref idx);
        }
        else
        {
            idxClone = gtCloneExpr(idx)
                ?? throw new FatalJitException("The cloned bounds-check index cannot be cloned again.");
        }

        var lowerTest = gtNewBinaryNode(GT_LT, TYP_INT,
            gtCloneExpr(idx) ?? throw new FatalJitException("The lower-bound index cannot be cloned."),
            gtNewIconNode(TYP_INT, 0));
        lowerTest.Flags |= GTF_RELOP_JMP_USED;
        var lowerJump = gtNewUnaryNode(GT_JTRUE, TYP_VOID, lowerTest);
        var lower = fgNewBBFromTreeAfter(BBJ_COND, previous, lowerJump, debugInfo);
        JITDUMP("\nLower bound check:\n");
        DISPTREE(lowerJump);

        GenTreeOp upperTest;
        if (idx.IsIntegralConst(0))
        {
            upperTest = gtNewBinaryNode(GT_GT, TYP_INT, arrLen, gtNewIconNode(TYP_INT, offset));
        }
        else
        {
            var maxOffset = gtNewIconNode(TYP_INT, -offset);
            var lengthMinusOffset = gtNewBinaryNode(GT_ADD, TYP_INT, arrLen, maxOffset);
            upperTest = gtNewBinaryNode(GT_LT, TYP_INT, idxClone, lengthMinusOffset);
        }
        upperTest.Flags |= GTF_RELOP_JMP_USED;
        var upperJump = gtNewUnaryNode(GT_JTRUE, TYP_VOID, upperTest);
        var upper = fgNewBBFromTreeAfter(BBJ_COND, lower, upperJump, debugInfo);
        JITDUMP("\nUpper bound check:\n");
        DISPTREE(upperJump);

        var fallback = fgNewBBafter(isReturn ? BBJ_RETURN : BBJ_ALWAYS, upper, false);
        BasicBlock.CloneBlockState(this, fallback, fast);

        fgRedirectEdge(ref previous.TargetEdgeRef, lower);
        var lowerToUpper = fgAddRefPred(upper, lower);
        var lowerToFallback = fgAddRefPred(fallback, lower);
        var upperToFast = fgAddRefPred(fast, upper);
        var upperToFallback = fgAddRefPred(fallback, upper);
        if (!isReturn)
        {
            var next = last ?? throw new FatalJitException("The split fast path must have a successor.");
            var fallbackToNext = fgAddRefPred(next, fallback);
            fallback.TargetEdge = fallbackToNext;
            fallbackToNext.Likelihood = 1.0;
        }
        lower.TrueEdge = lowerToFallback;
        lower.FalseEdge = lowerToUpper;
        upper.TrueEdge = upperToFast;
        upper.FalseEdge = upperToFallback;

        lower.inheritWeight(previous);
        upper.inheritWeight(previous);
        fast.inheritWeight(previous);
        fallback.bbSetRunRarely();
        lowerToUpper.Likelihood = 1.0;
        lowerToFallback.Likelihood = 0.0;
        upperToFast.Likelihood = 1.0;
        upperToFallback.Likelihood = 0.0;
        lower.SetFlags(BBF_INTERNAL);
        upper.SetFlags(BBF_INTERNAL);

        for (var i = checks.Count - 1; i >= 0; i--)
        {
            var info = checks[i];
#if DEBUG
            var statementFound = false;
            foreach (var stmt in fast.Statements)
            {
                if (stmt == info.Stmt)
                {
                    statementFound = true;
                    assert(!Unsafe.IsNullRef(ref gtFindLink(stmt, info.Check).result));
                    break;
                }
            }
            assert(statementFound);
#endif
            RemoveClonedBoundsCheck(info.Check, info.Stmt);
        }

        _ = fgMorphBlockStmt(lower, lower.LastStmt
            ?? throw new FatalJitException("The lower-bound block must have a test."), message: "Morph lowerBnd");
        _ = fgMorphBlockStmt(upper, upper.LastStmt
            ?? throw new FatalJitException("The upper-bound block must have a test."), message: "Morph upperBnd");
        if (lower.LastStmt is Statement lowerStmt)
        {
            gtUpdateStmtSideEffects(lowerStmt);
        }
        if (upper.LastStmt is Statement upperStmt)
        {
            gtUpdateStmtSideEffects(upperStmt);
        }

        assert(BasicBlock.sameEHRegion(previous, lower));
        assert(BasicBlock.sameEHRegion(previous, upper));
        assert(BasicBlock.sameEHRegion(previous, fast));
        assert(BasicBlock.sameEHRegion(previous, fallback));
        assert(isReturn || BasicBlock.sameEHRegion(previous, last!));

        return fast;
    }
}
