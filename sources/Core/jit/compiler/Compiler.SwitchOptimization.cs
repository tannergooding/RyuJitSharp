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
    public bool fgOptimizeSwitchBranches(BasicBlock block)
    {
        assert(block.Kind is BBJ_SWITCH);
        var modified = false;

        for (var i = 0; i < block.SwitchTargets.Succs.Length; i++)
        {
            var edge = block.SwitchTargets.Succs[i];
            var bDest = edge.DestinationBlock;
            var bNewDest = bDest;

            // Bypass empty unconditional branches, but not self jumps or empty cycles.
            if (bDest.isEmpty() && (bDest.Kind is BBJ_ALWAYS) && (bDest.Target != bDest))
            {
                var optimizeJump = !fgLeadsToEmptyBlockCycle(bDest);

                // Jumping out of a try region is allowed, but crossing into a different try is not.
                if (bDest.hasTryIndex && !BasicBlock.sameTryRegion(block, bDest))
                {
                    optimizeJump = false;
                }

                if (optimizeJump)
                {
                    bNewDest = bDest.Target;
                    JITDUMP("\nOptimizing a switch jump to an empty block with an unconditional jump " +
                        $"({FMT_BB(block.bbNum)} -> {FMT_BB(bDest.bbNum)} -> {FMT_BB(bNewDest.bbNum)})\n");
                }
            }

            if (bNewDest != bDest)
            {
                if (bDest.hasProfileWeight)
                {
                    bDest.decreaseBBProfileWeight(edge.LikelyWeight);
                }

                block.CopyFlags(bDest, BBF_ASYNC_RESUMPTION);
                fgReplaceJumpTarget(block, bDest, bNewDest);
                modified = true;

                // The unique successor table may shrink. Try this position again.
                i--;
            }
        }

        if (modified)
        {
            JITDUMP("fgOptimizeSwitchBranches: Optimized switch flow. Profile needs to be re-propagated. " +
                $"Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            fgPgoConsistent = false;
        }

        Statement? switchStmt = null;
        GenTree switchTree;
        if (block.IsLIR)
        {
            assert(block.LastNode is not null);
            switchTree = block.LastNode;
#if TARGET_WASM
            assert(switchTree.Oper is GT_SWITCH);
#else
            assert(switchTree.Oper is GT_SWITCH_TABLE);
#endif
        }
        else
        {
            switchStmt = block.LastStmt;
            assert(switchStmt is not null);
            switchTree = switchStmt.RootNode;
            assert(switchTree.Oper is GT_SWITCH);
        }

        noway_assert(switchTree.Type is TYP_VOID);

        if (block.SwitchTargets.Succs.Length == 1)
        {
            // Use BBJ_ALWAYS when all cases, including the default, have the same target.
            JITDUMP($"\nRemoving a switch jump with a single target ({FMT_BB(block.bbNum)})\n");
            JITDUMP("BEFORE:\n");
#if DEBUG
            if (verbose)
            {
                fgDispBasicBlocks();
            }
#endif
            if (block.IsLIR)
            {
                var switchTreeRange = block.GetTreeRange(switchTree, out var isClosed, out var sideEffects);

                // LowerSwitch constructs a contiguous, side-effect-free tree for the dispatch.
                assert(isClosed);
                assert((sideEffects & GTF_ALL_EFFECT) == 0);
                block.Delete(switchTreeRange);
            }
            else
            {
                assert(switchStmt is not null);
                GenTree? sideEffList = null;
                if ((switchTree.Flags & GTF_SIDE_EFFECT) != 0)
                {
                    gtExtractSideEffList(switchTree, ref sideEffList);
                }

                if (sideEffList is not null)
                {
                    noway_assert((sideEffList.Flags & GTF_SIDE_EFFECT) != 0);
#if DEBUG
                    if (verbose)
                    {
                        jitprintf("\nSwitch expression has side effects! Extracting side effects...\n");
                        gtDispTree(switchTree);
                        jitprintf("\n");
                        gtDispTree(sideEffList);
                        jitprintf("\n");
                    }
#endif
                    noway_assert(sideEffList.Oper is not GT_SWITCH);
                    switchStmt.RootNode = sideEffList;
                    if (fgNodeThreading is not NodeThreading.None)
                    {
                        compCurBB = block;
                        gtSetStmtInfo(switchStmt);
                        fgSetStmtSeq(switchStmt);
                    }
                }
                else
                {
                    fgRemoveStmt(block, switchStmt);
                }
            }

            block.SetKindAndTargetEdge(BBJ_ALWAYS, block.SwitchTargets.Cases[0]);
            var dupCount = block.TargetEdge.DupCount;
            block.TargetEdge.decrementDupCount(dupCount - 1);
            block.Target.bbRefs -= dupCount - 1;

            return true;
        }
        else if (block.SwitchTargets.Cases.Length == 2)
        {
            // A switch with one non-default clause is equivalent to switchVal == 0.
            var switchVal = switchTree.AsUnOp().Op1;
            noway_assert(genActualTypeIsIntOrI(switchVal.Type));
#if !TARGET_WASM
            if (block.IsLIR)
            {
                var jumpTable = switchTree.AsOp().Op2;
                assert(jumpTable.Oper is GT_JMPTABLE);
                block.Remove(jumpTable);
            }
#endif
            JITDUMP($"\nConverting a switch ({FMT_BB(block.bbNum)}) with only one significant clause " +
                "besides a default target to a conditional branch. Before:\n");
            DISPNODE(switchTree);

            var zeroConstNode = gtNewZeroConNode(switchVal.Type.ActualType);
            var condNode = gtNewBinaryNode(GT_EQ, TYP_INT, switchVal, zeroConstNode);

            // CSE must not replace a JTRUE's relop with a COMMA.
            condNode.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
            var jump = new GenTreeUnOp(GT_JTRUE, switchTree.Type, condNode, switchTree,
                block.IsLIR ? NodeThreading.LIR : fgNodeThreading);

            if (block.IsLIR)
            {
                block.ReplaceNode(switchTree, jump);
                block.InsertAfter(switchVal, zeroConstNode, condNode);
                assert(_pLowering is not null);
                _pLowering.LowerRange(block, new LIR.ReadOnlyRange(zeroConstNode, jump));

                // Lowering may replace JTRUE again. Keep the diagnostic alias on its live replacement.
                assert(block.LastNode is not null);
                switchTree = block.LastNode;
            }
            else
            {
                assert(switchStmt is not null);
                switchTree.Prev = null;
                switchTree.Next = null;
                switchStmt.RootNode = jump;
                switchTree = jump;
                if (fgNodeThreading is not NodeThreading.None)
                {
                    gtSetStmtInfo(switchStmt);
                    fgSetStmtSeq(switchStmt);
                }
            }

            var trueEdge = block.SwitchTargets.Cases[0];
            var falseEdge = block.SwitchTargets.Cases[1];
            block.SetCond(trueEdge, falseEdge);
            JITDUMP("After:\n");
            DISPNODE(switchTree);

            return true;
        }
        else if ((block.SwitchTargets.Succs.Length == 2) && block.SwitchTargets.HasDefaultCase &&
            !block.IsLIR && (fgNodeThreading is NodeThreading.AllTrees))
        {
            // All non-default cases sharing one target become an unsigned range test.
            var switchDesc = block.SwitchTargets;
            var defaultEdge = switchDesc.DefaultCase;
            var firstCaseEdge = switchDesc.Cases[0];
            var caseDest = firstCaseEdge.DestinationBlock;

            // Only the default case may target the default destination.
            if (defaultEdge.DupCount != 1)
            {
                return modified;
            }

            JITDUMP($"\nConverting a switch ({FMT_BB(block.bbNum)}) where all non-default cases target " +
                "the same block to a conditional branch. Before:\n");
            DISPNODE(switchTree);

            var switchVal = switchTree.AsUnOp().Op1;
            noway_assert(genActualTypeIsIntOrI(switchVal.Type));
            var caseCount = firstCaseEdge.DupCount;
            var iconNode = gtNewIconNode(switchVal.Type.ActualType, caseCount);
            var condNode = gtNewBinaryNode(GT_LT, TYP_INT, switchVal, iconNode);
            condNode.AsOp().IsUnsigned = true;
            condNode.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;

            var jump = new GenTreeUnOp(GT_JTRUE, switchTree.Type, condNode, switchTree, fgNodeThreading);
            assert(switchStmt is not null);
            switchTree.Prev = null;
            switchTree.Next = null;
            switchStmt.RootNode = jump;
            switchTree = jump;
            gtSetStmtInfo(switchStmt);
            fgSetStmtSeq(switchStmt);

            var caseDupCount = firstCaseEdge.DupCount;
            if (caseDupCount > 1)
            {
                firstCaseEdge.decrementDupCount(caseDupCount - 1);
                caseDest.bbRefs -= caseDupCount - 1;
            }

            block.SetCond(firstCaseEdge, defaultEdge);
            JITDUMP("After:\n");
            DISPNODE(switchTree);

            if (fgFoldCondToReturnBlock(block))
            {
                // Folding can replace the statement root as well as the block kind.
                switchTree = switchStmt.RootNode;
                JITDUMP("Folded conditional return into branchless return. After:\n");
                DISPNODE(switchTree);
            }

            return true;
        }

        return modified;
    }

    public bool fgFoldCondToReturnBlock(BasicBlock block)
    {
        var modified = false;
        assert(block.Kind is BBJ_COND);
#if JIT32_GCENCODER
        // The legacy encoder has a hard limit on the number of epilogues.
        return modified;
#else
        if (info.compRetType is not TYP_UBYTE)
        {
            return modified;
        }

        var retFalseBb = block.FalseTarget;
        var retTrueBb = block.TrueTarget;
        if (fgCanCompactBlock(retTrueBb) && (retTrueBb.Target != block))
        {
            fgCompactBlock(retTrueBb);
            modified = true;
        }

        // Compacting the true successor can remove the false successor or the conditional itself.
        if (!retFalseBb.HasFlag(BBF_REMOVED) && fgCanCompactBlock(retFalseBb) && (retFalseBb.Target != block))
        {
            fgCompactBlock(retFalseBb);
            modified = true;
        }

        if (block.Kind is not BBJ_COND)
        {
            return modified;
        }

        assert(block.TrueTarget == retTrueBb);
        assert(block.FalseTarget == retFalseBb);
        if ((retTrueBb.Kind is not BBJ_RETURN) || (retFalseBb.Kind is not BBJ_RETURN) ||
            !BasicBlock.sameEHRegion(block, retTrueBb) || !BasicBlock.sameEHRegion(block, retFalseBb) ||
            (retTrueBb == genReturnBB) || (retFalseBb == genReturnBB))
        {
            return modified;
        }

        var stmt = block.LastStmt;
        assert(stmt is not null);
        var node = stmt.RootNode;
        var cond = node.AsUnOp().Op1;
        if (!cond.Oper.IsCompare)
        {
            return modified;
        }

        assert(cond.Type is TYP_INT);
        if ((retTrueBb.GetUniquePred(this) is null) && (retFalseBb.GetUniquePred(this) is null))
        {
            // Do not introduce an additional epilogue when both returns remain shared.
            return modified;
        }

        var retTrueFalse = IsReturnBool(retTrueBb, true) && IsReturnBool(retFalseBb, false);
        var retFalseTrue = IsReturnBool(retTrueBb, false) && IsReturnBool(retFalseBb, true);
        if (!retTrueFalse && !retFalseTrue)
        {
            return modified;
        }

        if (retFalseTrue)
        {
            cond = gtReverseCond(cond);
        }

        modified = true;
        if (retTrueBb.hasProfileWeight)
        {
            retTrueBb.decreaseBBProfileWeight(block.TrueEdge.LikelyWeight);
        }

        if (retFalseBb.hasProfileWeight)
        {
            retFalseBb.decreaseBBProfileWeight(block.FalseEdge.LikelyWeight);
        }

        fgRemoveRefPred(block.TrueEdge);
        fgRemoveRefPred(block.FalseEdge);
        block.SetKindAndTargetEdge(BBJ_RETURN, null);
        var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, cond, node, fgNodeThreading);
        node.Prev = null;
        node.Next = null;
        stmt.RootNode = ret;
        cond.Flags &= ~GTF_RELOP_JMP_USED;

        // Native IL_OFFSET is unsigned, including the all-ones unknown-offset sentinel.
        block.bbCodeOffsEnd = unchecked((int)uint.Max((uint)retTrueBb.bbCodeOffsEnd, (uint)retFalseBb.bbCodeOffsEnd));
        gtSetStmtInfo(stmt);
        fgSetStmtSeq(stmt);
        gtUpdateStmtSideEffects(stmt);
        JITDUMP($"fgFoldCondToReturnBlock: folding {FMT_BB(block.bbNum)} from BBJ_COND into BBJ_RETURN:");
        DISPBLOCK(block);

        return modified;
#endif
    }

#if !JIT32_GCENCODER
    private static bool IsReturnBool(BasicBlock block, bool value)
    {
        if ((block.Kind is BBJ_RETURN) && (block.LastStmt is Statement stmt))
        {
            var node = stmt.RootNode;
            if (!((node.Oper is GT_RETURN) && node.AsUnOp().Op1.IsIntegralConst(value ? 1 : 0)))
            {
                return false;
            }

            // Dead local stores left by inlining are allowed; globally visible effects are not.
            foreach (var statement in block.Statements)
            {
                if (GTF_GLOBALLY_VISIBLE_SIDE_EFFECTS(statement.RootNode.Flags))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
#endif
}
