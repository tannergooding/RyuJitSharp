// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgDedupReturnComparison(BasicBlock block)
    {
#if JIT32_GCENCODER
        // The legacy encoder has a hard limit on the number of epilogues.
        return false;
#else
        assert(block.Kind is BBJ_RETURN);
        var statement = block.LastStmt;
        if ((info.compRetType is not TYP_UBYTE) || (block == genReturnBB) || (statement is null))
        {
            return false;
        }

        var rootNode = statement.RootNode;
        if ((rootNode.Oper is not GT_RETURN) || !rootNode.AsUnOp().Op1.Oper.IsCmpCompare)
        {
            return false;
        }

        var cmp = rootNode.AsUnOp().Op1;
        cmp.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
        // Native changes the root in place without resequencing the statement.
        var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, cmp, rootNode, fgNodeThreading)
        {
            Prev = rootNode.Prev,
            Next = rootNode.Next,
        };
        jump.Prev?.Next = jump;
        jump.Next?.Prev = jump;
        if (statement.TreeListBegin == rootNode)
        {
            statement.TreeListBegin = jump;
        }

        if (statement.TreeListEnd == rootNode)
        {
            statement.TreeListEnd = jump;
        }

        statement.RootNode = jump;
        rootNode.Prev = null;
        rootNode.Next = null;

        var retTrue = gtNewUnaryNode(GT_RETURN, TYP_INT, gtNewTrue());
        var retFalse = gtNewUnaryNode(GT_RETURN, TYP_INT, gtNewFalse());

        // Both insertions are after block, so the false return precedes the true return.
        var debugInfo = statement.DebugInfo;
        var retTrueBb = fgNewBBFromTreeAfter(BBJ_RETURN, block, retTrue, debugInfo);
        var retFalseBb = fgNewBBFromTreeAfter(BBJ_RETURN, block, retFalse, debugInfo);
        var trueEdge = fgAddRefPred(retTrueBb, block);
        var falseEdge = fgAddRefPred(retFalseBb, block);
        block.SetCond(trueEdge, falseEdge);

        // Return conditions are not instrumented: use equal likelihoods.
        trueEdge.Likelihood = 0.5;
        falseEdge.Likelihood = 0.5;
        retTrueBb.inheritWeightPercentage(block, 50);
        retFalseBb.inheritWeightPercentage(block, 50);

        return true;
#endif
    }

    public BasicBlock fgNewBBFromTreeAfter(
        BBKinds jumpKind, BasicBlock block, GenTree tree, in DebugInfo debugInfo, bool updateSideEffects = false)
    {
        var newBlock = fgNewBBafter(jumpKind, block, true);
        newBlock.SetFlags(BBF_INTERNAL);
        var stmt = fgNewStmtFromTree(tree, di: debugInfo);
        fgInsertStmtAtEnd(newBlock, stmt);
        newBlock.bbCodeOffs = block.bbCodeOffsEnd;
        newBlock.bbCodeOffsEnd = block.bbCodeOffsEnd;
        if (updateSideEffects)
        {
            gtUpdateStmtSideEffects(stmt);
        }

        return newBlock;
    }

    public unsafe bool fgOptimizeUncondBranchToSimpleCond(BasicBlock block, BasicBlock target)
    {
        JITDUMP($"Considering uncond to cond {FMT_BB(block.bbNum)} -> {FMT_BB(target.bbNum)}\n");
        if (!BasicBlock.sameEHRegion(block, target))
        {
            return false;
        }

        if (!fgBlockIsGoodTailDuplicationCandidate(target, out var lclNum))
        {
            return false;
        }

        assert(target.Kind is BBJ_COND);
        if (!fgBlockEndFavorsTailDuplication(block, lclNum))
        {
            return false;
        }

        // Backend flowgraph updates disable tail duplication.
        assert(!block.IsLIR);
        for (var stmt = target.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
        {
            var clone = gtCloneExpr(stmt.RootNode);
            noway_assert(clone is not null);
            var cloneStmt = gtNewStmt(clone);
            if (fgNodeThreading is not NodeThreading.None)
            {
                gtSetStmtInfo(cloneStmt);
            }

            fgInsertStmtAtEnd(block, cloneStmt);
        }

        // Transfer likelihoods and their provenance along with the duplicated flow.
        fgRedirectEdge(ref block.TargetEdgeRef, target.TrueTarget);
        block.TargetEdge.Likelihood = target.TrueEdge.Likelihood;
        block.TargetEdge.isHeuristicBased = target.TrueEdge.isHeuristicBased;
        var falseEdge = fgAddRefPred(target.FalseTarget, block, target.FalseEdge);
        block.SetCond(block.TargetEdge, falseEdge);

        JITDUMP($"fgOptimizeUncondBranchToSimpleCond(from {FMT_BB(block.bbNum)} to cond {FMT_BB(target.bbNum)}), " +
            $"modified {FMT_BB(block.bbNum)}\n");
        JITDUMP($"   expecting opts to key off V{lclNum:D2} in {FMT_BB(block.bbNum)}\n");

        if (target.hasProfileWeight && block.hasProfileWeight)
        {
            var targetWeight = target.bbWeight;
            target.decreaseBBProfileWeight(block.bbWeight);
            JITDUMP($"Decreased {FMT_BB(target.bbNum)} profile weight from {FMT_WT(targetWeight)} " +
                $"to {FMT_WT(target.bbWeight)}\n");
        }

        return true;
    }

    public unsafe bool fgFoldSimpleCondByForwardSub(BasicBlock block)
    {
        assert(block.Kind is BBJ_COND);
        var lastStmt = block.LastStmt;
        assert(lastStmt is not null);
        var jtrue = lastStmt.RootNode;
        assert(jtrue.Oper is GT_JTRUE);

        var relop = jtrue.AsUnOp().Op1;
        if (!relop.Oper.IsCompare)
        {
            return false;
        }

        var op1 = relop.AsOp().Op1;
        var op2 = relop.AsOp().Op2;
        GenTreeLclVarCommon lcl;
        var useOp1 = false;
        if ((op1.Oper is GT_LCL_VAR) && op2.Oper.IsIntegralConst)
        {
            lcl = op1.AsLclVarCommon();
            useOp1 = true;
        }
        else if ((op2.Oper is GT_LCL_VAR) && op1.Oper.IsIntegralConst)
        {
            lcl = op2.AsLclVarCommon();
        }
        else
        {
            return false;
        }

        var secondLastStmt = lastStmt.PrevStmt;
        if ((secondLastStmt is null) || (secondLastStmt == lastStmt))
        {
            return false;
        }

        var prevTree = secondLastStmt.RootNode;
        if (prevTree.Oper is not GT_STORE_LCL_VAR)
        {
            return false;
        }

        var store = prevTree.AsLclVarCommon();
        if (store.LclNum != lcl.LclNum)
        {
            return false;
        }

        if (!store.Data.Oper.IsIntegralConst)
        {
            return false;
        }

        if ((store.Type.ActualType != store.Data.Type.ActualType) || (store.Type.ActualType != lcl.Type.ActualType))
        {
            return false;
        }

        JITDUMP("Forward substituting local after jump threading. Before:\n");
        DISPSTMT(lastStmt);
        JITDUMP("\nAfter:\n");

        ref var varDsc = ref lvaGetDesc(lcl.LclNum);
        var newData = gtCloneExpr(store.Data);
        assert(newData is not null);
        if (varTypeIsSmall(varDsc.Type) && fgCastNeeded(store.Data, varDsc.Type))
        {
            newData = gtNewCastNode(TYP_INT, newData, false, varDsc.Type);
            newData = gtFoldExpr(newData);
        }

        // The constant folders replace CLR node representations and require unthreaded inputs.
        // Preserve the statement owner, then rebuild its mode-specific links after replacement.
        for (var threaded = lastStmt.TreeListBegin; threaded is not null;)
        {
            var next = threaded.Next;
            threaded.Prev = null;
            threaded.Next = null;
            threaded = next;
        }

        lastStmt.TreeListBegin = null;
        lastStmt.TreeListEnd = null;
        if (useOp1)
        {
            relop.AsOp().Op1 = newData;
        }
        else
        {
            relop.AsOp().Op2 = newData;
        }

        DISPSTMT(lastStmt);
        JITDUMP("\nNow trying to fold...\n");
        jtrue.AsUnOp().Op1 = gtFoldExpr(relop);
        DISPSTMT(lastStmt);

        var result = fgFoldConditional(block);
        if (result is not FoldResult.FOLD_DID_NOTHING)
        {
            assert(block.Kind is BBJ_ALWAYS);
            return true;
        }

        // A removed statement has no remaining owner. Only restore the surviving statement's links.
        if (fgNodeThreading is NodeThreading.AllTrees)
        {
            fgSetStmtSeq(lastStmt);
        }
        else if (fgNodeThreading is NodeThreading.AllLocals)
        {
            fgSequenceLocals(lastStmt);
        }

        return false;
    }

    private bool fgBlockEndFavorsTailDuplication(BasicBlock block, int lclNum)
    {
        if (block.isRunRarely)
        {
            return false;
        }

        if (lvaGetDesc(lclNum).IsAddressExposed)
        {
            return false;
        }

        var lastStmt = block.LastStmt;
        if (lastStmt is null)
        {
            return false;
        }

        // Look at the last two statements for information lost at the upcoming merge.
        const int limit = 2;
        var count = 0;
        var stmt = lastStmt;
        while (count < limit)
        {
            count++;
            var tree = stmt.RootNode;
            if (tree.Oper.IsLocalStore && !tree.IsBlkOp && (tree.AsLclVarCommon().LclNum == lclNum))
            {
                var value = tree.Data;
                if (value.Oper.IsArrLength || value.Oper.IsConst || value.Oper.IsCompare)
                {
                    return true;
                }
            }

            var prevStmt = stmt.PrevStmt;

            // The first statement's prev link wraps to the last statement.
            if (prevStmt == lastStmt)
            {
                break;
            }

            assert(prevStmt is not null);
            stmt = prevStmt;
        }

        return false;
    }

    private bool fgBlockIsGoodTailDuplicationCandidate(BasicBlock target, out int lclNum)
    {
        lclNum = BAD_VAR_NUM;
        if (target.Kind is not BBJ_COND)
        {
            return false;
        }

        // Duplication must remove part of a control-flow join.
        if ((target.bbRefs < 2) || (target.TrueTarget == target) || (target.FalseTarget == target))
        {
            return false;
        }

        var lastStmt = target.LastStmt;
        assert(lastStmt is not null);
        var firstStmt = target.GetFirstNonPhiDef();

        // Allow at most one statement besides the branch, ignoring phi definitions.
        if ((firstStmt != lastStmt) && (firstStmt != lastStmt.PrevStmt))
        {
            return false;
        }

        var lastTree = lastStmt.RootNode;
        if (lastTree.Oper is not GT_JTRUE)
        {
            return false;
        }

        var cond = lastTree.AsUnOp().Op1;
        if (!cond.Oper.IsCompare)
        {
            return false;
        }

        var op1 = cond.AsOp().Op1;
        while (op1.Oper is GT_CAST)
        {
            op1 = op1.AsUnOp().Op1;
        }

        if (!op1.Oper.IsLocal && !op1.Oper.IsConst)
        {
            return false;
        }

        var op2 = cond.AsOp().Op2;
        while (op2.Oper is GT_CAST)
        {
            op2 = op2.AsUnOp().Op1;
        }

        if (!op2.Oper.IsLocal && !op2.Oper.IsConst)
        {
            return false;
        }

        var lcl1 = op1.Oper.IsLocal ? op1.AsLclVarCommon().LclNum : BAD_VAR_NUM;
        var lcl2 = op2.Oper.IsLocal ? op2.AsLclVarCommon().LclNum : BAD_VAR_NUM;
        if ((lcl1 != BAD_VAR_NUM) && op2.Oper.IsConst)
        {
            lclNum = lcl1;
        }
        else if ((lcl2 != BAD_VAR_NUM) && op1.Oper.IsConst)
        {
            lclNum = lcl2;
        }
        else if ((lcl1 != BAD_VAR_NUM) && (lcl1 == lcl2))
        {
            lclNum = lcl1;
        }
        else
        {
            return false;
        }

        if (firstStmt == lastStmt)
        {
            return true;
        }

        assert(firstStmt is not null);
        var firstTree = firstStmt.RootNode;
        if ((firstTree.Oper is not GT_STORE_LCL_VAR) || (firstTree.AsLclVar().LclNum != lclNum))
        {
            return false;
        }

        var data = firstTree.AsLclVar().Data;
        if (!data.Oper.IsBinary)
        {
            return false;
        }

        op1 = data.AsOp().Op1;
        while (op1.Oper is GT_CAST)
        {
            op1 = op1.AsUnOp().Op1;
        }

        if (!op1.Oper.IsLocal && !op1.Oper.IsConst)
        {
            return false;
        }

        // Some operators classified as binary do not actually have a second operand.
        op2 = data.AsOp().Op2;
        if (op2 is null)
        {
            return false;
        }

        while (op2.Oper is GT_CAST)
        {
            op2 = op2.AsUnOp().Op1;
        }

        if (!op2.Oper.IsLocal && !op2.Oper.IsConst)
        {
            return false;
        }

        lcl1 = op1.Oper.IsLocal ? op1.AsLclVarCommon().LclNum : BAD_VAR_NUM;
        lcl2 = op2.Oper.IsLocal ? op2.AsLclVarCommon().LclNum : BAD_VAR_NUM;
        if ((lcl1 != BAD_VAR_NUM) && op2.Oper.IsConst)
        {
            lclNum = lcl1;
        }
        else if ((lcl2 != BAD_VAR_NUM) && op1.Oper.IsConst)
        {
            lclNum = lcl2;
        }
        else if ((lcl1 != BAD_VAR_NUM) && (lcl1 == lcl2))
        {
            lclNum = lcl1;
        }
        else
        {
            return false;
        }

        return true;
    }
}
