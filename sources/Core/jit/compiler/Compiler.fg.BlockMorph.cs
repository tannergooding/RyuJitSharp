// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgMorphBlocks()
    {
        fgGlobalMorph = true;
        if (fgPgoConsistent)
        {
            Metrics.ProfileConsistentBeforeMorph = 1;
        }

        if (opts.OptimizationEnabled)
        {
            optAssertionInit(isLocalProp: true);
            assert(apTraits is not null);
            apLocal = BitVecOps.MakeEmpty(apTraits);
            apLocalPostorder = BitVecOps.MakeEmpty(apTraits);
        }
        else
        {
            optLocalAssertionProp = false;
            optCrossBlockLocalAssertionProp = false;
        }

        if (!compEnregLocals)
        {
            lvSetMinOptsDoNotEnreg();
        }

        if (!optLocalAssertionProp)
        {
            foreach (var block in Blocks)
            {
                fgMorphBlock(block);
            }
        }
        else
        {
#if DEBUG
            fgSafeBasicBlockCreation = false;
            fgSafeFlowEdgeCreation = false;
#endif
            var unreachableInfo = new MorphUnreachableInfo(this);
            genReturnBB?.SetFlags(BBF_CAN_ADD_PRED);
            BasicBlock? firstILBlock = null;
            if (MethodHasRecursiveTailCall)
            {
                firstILBlock = opts.IsOSR ? fgEntryBB : fgGetFirstILBlock();
                assert(firstILBlock is not null);
                firstILBlock.SetFlags(BBF_CAN_ADD_PRED);
            }

            var maximumBlockNumber = fgBBNumMax;
            assert(_dfsTree is not null);
            for (var index = _dfsTree.PostOrderCount; index != 0; index--)
            {
                fgMorphBlock(_dfsTree.GetPostOrder(index - 1), unreachableInfo);
            }
            assert(maximumBlockNumber == fgBBNumMax);

#if DEBUG
            fgSafeBasicBlockCreation = true;
            fgSafeFlowEdgeCreation = true;
#endif
            genReturnBB?.RemoveFlags(BBF_CAN_ADD_PRED);
            firstILBlock?.RemoveFlags(BBF_CAN_ADD_PRED);
        }

        if (opts.IsOSR && (fgEntryBB is not null))
        {
            JITDUMP($"OSR: un-protecting original method entry {FMT_BB(fgEntryBB.bbNum)}\n");
            assert(fgEntryBBExtraRefs == 1);
            assert(fgEntryBB.bbRefs >= 1);
            fgEntryBB.bbRefs--;
            fgEntryBBExtraRefs = 0;
            if (fgEntryBB.hasProfileWeight && !fgProfileWeightsConsistent(fgEntryBB.computeIncomingWeight(), fgEntryBB.bbWeight))
            {
                JITDUMP($"OSR: Original method entry {FMT_BB(fgEntryBB.bbNum)} has inconsistent weight. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                fgPgoConsistent = false;
            }
            fgEntryBB = null;
        }

        genReturnBB?.RemoveFlags(BBF_DONT_REMOVE);
        genReturnBB = null;
        fgInvalidateDfsTree();
        fgGlobalMorph = false;
        fgGlobalMorphDone = true;
        compCurBB = null;

        if (optLocalAssertionProp)
        {
#if DEBUG
            JITDUMP($"morph assertion stats: {optMaxAssertionCount} table size, {optAssertionCount} assertions, {optAssertionOverflow} dropped\n");
#endif
            Metrics.LocalAssertionCount = optAssertionCount;
            Metrics.LocalAssertionOverflow = optAssertionOverflow;
            Metrics.MorphTrackedLocals = lvaTrackedCount;
            Metrics.MorphLocals = lvaCount;
            optLocalAssertionProp = false;
            optCrossBlockLocalAssertionProp = false;
        }

        _ = fgCanonicalizeFirstBB();
        if (fgPgoConsistent)
        {
            Metrics.ProfileConsistentAfterMorph = 1;
        }
#if DEBUG
        fgPostGlobalMorphChecks();
#endif
        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private void fgMorphBlock(BasicBlock block, MorphUnreachableInfo? unreachableInfo = null)
    {
        JITDUMP($"\nMorphing {FMT_BB(block.bbNum)}\n");
        if (optLocalAssertionProp)
        {
            assert(apTraits is not null);
            if (!optCrossBlockLocalAssertionProp)
            {
                optAssertionReset();
                BitVecOps.ClearD(apTraits, apLocal);
                BitVecOps.ClearD(apTraits, apLocalPostorder);
            }
            else
            {
                var canUsePredecessorAssertions = !block.HasFlag(BBF_CAN_ADD_PRED) && !bbIsHandlerBeg(block);
                if (!canUsePredecessorAssertions)
                {
                    JITDUMP($"{FMT_BB(block.bbNum)} ineligible for cross-block\n");
                }
                else
                {
                    assert(unreachableInfo is not null);
                    assert(_dfsTree is not null);
                    var hasPredecessorAssertions = false;
                    var isReachable = (block == fgFirstBB) || (block == genReturnBB) || (opts.IsOSR && (block == fgEntryBB));
                    foreach (var predecessor in block.PredBlocks)
                    {
                        assert(_dfsTree.Contains(predecessor));
                        if (predecessor.bbPostorderNum <= block.bbPostorderNum)
                        {
                            JITDUMP($"{FMT_BB(block.bbNum)} pred {FMT_BB(predecessor.bbNum)} not processed; clearing assertions in\n");
                            hasPredecessorAssertions = false;
                            isReachable = true;
                            break;
                        }

                        if (unreachableInfo.IsUnreachable(predecessor))
                        {
                            JITDUMP($"Pred {FMT_BB(predecessor.bbNum)} is no longer reachable\n");
                            continue;
                        }

                        isReachable = true;
                        ASSERT_TP? assertionsOut;
                        if ((predecessor.Kind is BBJ_COND) && (predecessor.NumSucc == 2))
                        {
                            if (block == predecessor.TrueTarget)
                            {
                                JITDUMP($"Using `if true` assertions from pred {FMT_BB(predecessor.bbNum)}\n");
                                assertionsOut = predecessor.bbAssertionOutIfTrue;
                            }
                            else
                            {
                                assert(block == predecessor.FalseTarget);
                                JITDUMP($"Using `if false` assertions from pred {FMT_BB(predecessor.bbNum)}\n");
                                assertionsOut = predecessor.bbAssertionOutIfFalse;
                            }
                        }
                        else
                        {
                            assertionsOut = predecessor.bbAssertionOut;
                        }

                        if (!hasPredecessorAssertions)
                        {
                            apLocal = predecessor.NumSucc == 1 ? assertionsOut : BitVecOps.MakeCopy(apTraits, assertionsOut);
                            hasPredecessorAssertions = true;
                        }
                        else
                        {
                            BitVecOps.IntersectionD(apTraits, apLocal, assertionsOut);
                        }
                    }

                    if (!hasPredecessorAssertions)
                    {
                        canUsePredecessorAssertions = false;
                    }

                    if (!isReachable)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} has no reachable preds, marking as unreachable\n");
                        unreachableInfo.SetUnreachable(block);
                        if (block.Kind is not BBJ_CALLFINALLY and not BBJ_CALLFINALLYRET)
                        {
                            fgUnreachableBlock(block);
                            block.RemoveFlags(BBF_REMOVED);
                            block.SetKindAndTargetEdge(BBJ_THROW, null);
                            return;
                        }
                    }
                }

                if (!canUsePredecessorAssertions)
                {
                    apLocal = BitVecOps.MakeEmpty(apTraits);
                }
                assert(apLocalPostorder is not null);
                BitVecOps.Assign(apTraits, ref apLocalPostorder, apLocal);
#if DEBUG
                if (verbose)
                {
                    assert(apLocal is not null);
                    optDumpAssertionIndices("Assertions in: ", apLocal);
                }
#endif
            }
        }

        compCurBB = block;
        fgMorphStmts(block);
        if ((block.Kind is BBJ_RETURN) && !block.HasFlag(BBF_HAS_JMP) && (genReturnBB is not null) && (genReturnBB != block))
        {
            fgMergeBlockReturn(block);
        }

        if (optCrossBlockLocalAssertionProp && (block.NumSucc > 0))
        {
            assert(optLocalAssertionProp);
            assert(apTraits is not null);
            if (block.Kind is BBJ_COND)
            {
                block.bbAssertionOutIfTrue = apLocalIfTrue;
                block.bbAssertionOutIfFalse = BitVecOps.MakeCopy(apTraits, apLocal);
            }
            else
            {
                block.bbAssertionOut = BitVecOps.MakeCopy(apTraits, apLocal);
            }
        }
        compCurBB = null;
    }

    private void fgMorphStmts(BasicBlock block)
    {
        fgRemoveRestOfBlock = false;
        fgHasNoReturnCall = false;
        foreach (var statement in block.Statements)
        {
            if (fgRemoveRestOfBlock)
            {
                fgRemoveStmt(block, statement);
                continue;
            }

            fgMorphStmt = statement;
            compCurStmt = statement;
            var oldTree = statement.RootNode;
            if (optLocalAssertionProp)
            {
                assert(apTraits is not null);
                assert(apLocalPostorder is not null);
                BitVecOps.Assign(apTraits, ref apLocalPostorder, apLocal);
            }
#if DEBUG
            var oldHash = verbose ? gtHashValue(oldTree) : 0;
            if (verbose)
            {
                jitprintf($"\nfgMorphTree {FMT_BB(block.bbNum)}, {FMT_STMT(statement.Id)} (before)\n");
                gtDispTree(oldTree);
            }
#endif
            var morphedTree = fgMorphTree(oldTree);
            if ((statement.RootNode != oldTree) || (block != compCurBB))
            {
                if (statement.RootNode != oldTree)
                {
                    morphedTree = statement.RootNode;
                }
                noway_assert(compTailCallUsed);
                noway_assert(morphedTree.Oper is GT_CALL);
                assert(compCurBB is not null);
                var call = morphedTree.AsCall();
                noway_assert((call.IsFastTailCall && (compCurBB.Kind is BBJ_RETURN) && compCurBB.HasFlag(BBF_HAS_JMP)) ||
                    (call.IsTailCallViaJitHelper && (compCurBB.Kind is BBJ_THROW)) ||
                    (!call.IsTailCall && (compCurBB.Kind is BBJ_RETURN)));
            }
#if DEBUG
            if (compStressCompile(STRESS_CLONE_EXPR, 30))
            {
                if (verbose)
                {
                    jitprintf("\nfgMorphTree (stressClone from):\n");
                    gtDispTree(morphedTree);
                }
                morphedTree = gtCloneExpr(morphedTree);
                morphedTree.SetMorphed(this, doChilren: true);
                if (verbose)
                {
                    jitprintf("\nfgMorphTree (stressClone to):\n");
                    gtDispTree(morphedTree);
                }
            }
            if (verbose && (gtHashValue(morphedTree) != oldHash))
            {
                jitprintf($"\nfgMorphTree {FMT_BB(block.bbNum)}, {FMT_STMT(statement.Id)} (after)\n");
                gtDispTree(morphedTree);
            }
#endif
            if (fgIsCommaThrow(morphedTree, forFolding: true))
            {
                morphedTree = morphedTree.AsOp().Op1;
                noway_assert(morphedTree.Oper is GT_CALL);
                noway_assert((morphedTree.Flags & GTF_COLON_COND) == 0);
                fgRemoveRestOfBlock = true;
            }
            statement.RootNode = morphedTree;

            if (fgHasNoReturnCall)
            {
                fgHasNoReturnCall = false;
                if ((fgGetTopLevelQmark(statement.RootNode, out _) is null) && gtRemoveTreesAfterNoReturnCall(block, statement))
                {
                    fgRemoveRestOfBlock = true;
                    morphedTree = statement.RootNode;
                }
            }
            if (fgRemoveRestOfBlock || fgCheckRemoveStmt(block, statement))
            {
                continue;
            }
            if (fgFoldConditional(block) != FoldResult.FOLD_DID_NOTHING)
            {
                continue;
            }
            if (ehBlockHasExnFlowDsc(block))
            {
                continue;
            }
        }

        if (fgRemoveRestOfBlock)
        {
            if (block.Kind is BBJ_COND or BBJ_SWITCH)
            {
                noway_assert(block.FirstStmt is not null);
                var lastStatement = block.LastStmt;
                noway_assert((lastStatement is not null) && (lastStatement.NextStmt is null));
                var last = lastStatement.RootNode;
                if (((block.Kind is BBJ_COND) && (last.Oper is GT_JTRUE)) ||
                    ((block.Kind is BBJ_SWITCH) && (last.Oper is GT_SWITCH)))
                {
                    var operand = last.AsUnOp().Op1;
                    if (operand.Oper.IsCompare)
                    {
                        operand.Flags &= ~GTF_RELOP_JMP_USED;
                    }
                    lastStatement.RootNode = fgMorphTree(operand);
                }
            }
            fgConvertBBToThrowBB(block);
        }

#if FEATURE_FASTTAILCALL
        if (block.EndsWithTailCall(this, fastTailCallsOnly: false, tailCallsConvertibleToLoopOnly: true, out var recursiveTailCall))
        {
            assert(recursiveTailCall is not null);
            fgMorphRecursiveFastTailCallIntoLoop(block, recursiveTailCall);
        }
#endif
        fgRemoveRestOfBlock = false;
    }

    private bool fgCheckRemoveStmt(BasicBlock block, Statement statement)
    {
        if (opts.compDbgCode)
        {
            return false;
        }

        var tree = statement.RootNode;
        if (tree.Oper is GT_JTRUE or GT_JCMP or GT_JTEST or GT_JCC or GT_SWITCH or GT_LABEL or
            GT_CALL or GT_JMP or GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET or GT_RETURN_SUSPEND or
            GT_PATCHPOINT or GT_PATCHPOINT_FORCED or GT_NONLOCAL_JMP or GT_WASM_JEXCEPT or GT_NO_OP)
        {
            return false;
        }
        if ((tree.Flags & GTF_SIDE_EFFECT) != 0)
        {
            return false;
        }

        fgRemoveStmt(block, statement);
        return true;
    }

    private enum FoldResult
    {
        FOLD_DID_NOTHING,
        FOLD_CHANGED_CONTROL_FLOW,
        FOLD_REMOVED_LAST_STMT,
        FOLD_ALTERED_LAST_STMT,
    }

    private FoldResult fgFoldConditional(BasicBlock block)
    {
        var result = FoldResult.FOLD_DID_NOTHING;
        if (opts.OptimizationDisabled || (block.Kind is not BBJ_COND and not BBJ_SWITCH))
        {
            return result;
        }

        noway_assert((block.FirstStmt is not null) && (block.FirstStmt.PrevStmt is not null));
        var lastStatement = block.LastStmt;
        noway_assert((lastStatement is not null) && (lastStatement.NextStmt is null));
        if (lastStatement.RootNode.Oper is GT_CALL)
        {
            noway_assert(fgRemoveRestOfBlock);
            fgConvertBBToThrowBB(block);
            JITDUMP($"\nConditional folded at {FMT_BB(block.bbNum)}\n");
            JITDUMP($"{FMT_BB(block.bbNum)} becomes a BBJ_THROW\n");
            return FoldResult.FOLD_CHANGED_CONTROL_FLOW;
        }

        var isConditional = block.Kind is BBJ_COND;
        noway_assert(lastStatement.RootNode.Oper == (isConditional ? GT_JTRUE : GT_SWITCH));
        var conditionTree = lastStatement.RootNode.AsUnOp().Op1;
        var condition = conditionTree.EffectiveVal;
        if (!condition.Oper.IsConst)
        {
            return result;
        }

        noway_assert(condition.Oper is GT_CNS_INT);
        if (isConditional)
        {
            noway_assert((block.FalseTarget.CountOfInEdges > 0) && (block.TrueTarget.CountOfInEdges > 0));
        }
        if (conditionTree != condition)
        {
            assert(conditionTree.Oper is GT_COMMA);
            lastStatement.RootNode = conditionTree;
            result = FoldResult.FOLD_ALTERED_LAST_STMT;
        }
        else
        {
            fgRemoveStmt(block, lastStatement);
            result = FoldResult.FOLD_REMOVED_LAST_STMT;
        }

        if (isConditional)
        {
            var isTrue = condition.AsIntCon().IconValue != 0;
            var retainedEdge = isTrue ? block.TrueEdge : block.FalseEdge;
            var removedEdge = isTrue ? block.FalseEdge : block.TrueEdge;
            fgRemoveRefPred(removedEdge);
            block.SetKindAndTargetEdge(BBJ_ALWAYS, retainedEdge);
            fgRepairProfileCondToUncond(block, retainedEdge, removedEdge);
        }
        else
        {
            var switchValue = unchecked((nuint)condition.AsIntCon().IconValue);
            var cases = block.SwitchTargets.Cases;
            var foundValue = false;
            var profileInconsistent = false;
            for (var index = 0; index < cases.Length; index++)
            {
                var edge = cases[index];
                var target = edge.DestinationBlock;
                assert(target.CountOfInEdges > 0);
                if (block.hasProfileWeight && target.hasProfileWeight)
                {
                    target.decreaseBBProfileWeight(edge.LikelyWeight);
                    profileInconsistent |= target.NumSucc > 0;
                }
                if (((nuint)index == switchValue) || (!foundValue && (index == cases.Length - 1)))
                {
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
                    foundValue = true;
                    if (block.hasProfileWeight && target.hasProfileWeight)
                    {
                        target.increaseBBProfileWeight(block.bbWeight);
                        profileInconsistent |= target.NumSucc > 0;
                    }
                }
                else
                {
                    fgRemoveRefPred(edge);
                }
            }

            if (profileInconsistent)
            {
                JITDUMP($"Flow change out of {FMT_BB(block.bbNum)} needs to be propagated. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                fgPgoConsistent = false;
            }
            assert(foundValue);
        }

        JITDUMP($"\nConditional folded at {FMT_BB(block.bbNum)}\n");
        JITDUMP($"{FMT_BB(block.bbNum)} becomes a BBJ_ALWAYS to {FMT_BB(block.Target.bbNum)}\n");
        return result;
    }

    private void fgMergeBlockReturn(BasicBlock block)
    {
        assert((block.Kind is BBJ_RETURN) && !block.HasFlag(BBF_HAS_JMP));
        assert((genReturnBB is not null) && (genReturnBB != block));
        var lastStatement = block.LastStmt;
        var ret = lastStatement?.RootNode;
        if ((ret is not null) && (ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET) && ((ret.Flags & GTF_RET_MERGED) != 0))
        {
            return;
        }

#if !TARGET_X86
        if ((info.compFlags & CORINFO_FLG_SYNCH) != 0)
        {
            fgConvertSyncReturnToLeave(block);
        }
        else
#endif
        {
            var edge = fgAddRefPred(genReturnBB, block);
            block.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
            fgReturnCount--;
        }

#if SWIFT_SUPPORT
        if ((ret is not null) && (ret.Oper is GT_SWIFT_ERROR_RET))
        {
            assert(genReturnErrorLocal != BAD_VAR_NUM);
            assert(lastStatement is not null);
            var errorStore = gtNewTempStore(genReturnErrorLocal, ret.AsOp().Op1);
            errorStore.SetMorphed(this);
            fgInsertStmtBefore(block, lastStatement, gtNewStmt(errorStore, lastStatement.DebugInfo));
        }
#endif
        if (genReturnLocal != BAD_VAR_NUM)
        {
            noway_assert(compMethodHasRetVal);
            noway_assert((lastStatement is not null) && (lastStatement.NextStmt is null));
            noway_assert(ret is not null);
            var returnValue = ret.Oper is GT_SWIFT_ERROR_RET ? ret.AsOp().Op2 : ret.AsUnOp().Op1;
            var afterStatement = lastStatement;
            var debugInfo = lastStatement.DebugInfo;
            var tree = gtNewTempStore(genReturnLocal, returnValue, ref afterStatement, CHECK_SPILL_NONE, debugInfo, block);
            tree.SetMorphed(this);
            if (tree.IsCopyBlkOp)
            {
                tree = fgMorphCopyBlock(tree);
            }
            else if (tree.IsInitBlkOp)
            {
                tree = fgMorphInitBlock(tree);
            }
            if (afterStatement == lastStatement)
            {
                lastStatement.RootNode = tree;
            }
            else
            {
                fgRemoveStmt(block, lastStatement);
                var newStatement = gtNewStmt(tree, debugInfo);
                fgInsertStmtAfter(block, afterStatement, newStatement);
                lastStatement = newStatement;
            }
        }
        else if ((ret is not null) && (ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET))
        {
            noway_assert((lastStatement is not null) && (lastStatement.NextStmt is null));
            noway_assert(ret.Type is TYP_VOID);
            assert((ret.Oper is GT_SWIFT_ERROR_RET ? ret.AsOp().Op2 : ret.AsUnOp().Op1) is null);
            if (opts.compDbgCode && lastStatement.DebugInfo.IsValid)
            {
                ret.BashToNOP();
            }
            else
            {
                fgRemoveStmt(block, lastStatement);
            }
        }

        JITDUMP($"\nUpdate {FMT_BB(block.bbNum)} to jump to common return block.\n");
        DISPBLOCK(block);
        if (block.hasProfileWeight)
        {
            var oldWeight = genReturnBB.hasProfileWeight ? genReturnBB.bbWeight : BB_ZERO_WEIGHT;
            JITDUMP($"merging profile weight {FMT_WT(block.bbWeight)} from {FMT_BB(block.bbNum)} to common return {FMT_BB(genReturnBB.bbNum)}\n");
            genReturnBB.setBBProfileWeight(oldWeight + block.bbWeight);
            DISPBLOCK(genReturnBB);
        }
    }
}
