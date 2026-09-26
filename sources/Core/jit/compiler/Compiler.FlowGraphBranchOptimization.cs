// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgOptimizeBranch(BasicBlock bJump)
    {
        assert(opts.OptimizationEnabled);

        if (bJump.Kind is not BBJ_ALWAYS)
        {
            return false;
        }

        if (bJump.JumpsToNext)
        {
            return false;
        }

        if (bJump.HasFlag(BBF_KEEP_BBJ_ALWAYS))
        {
            return false;
        }

        var bDest = bJump.Target;

        if (bDest.Kind is not BBJ_COND)
        {
            return false;
        }

        if (bJump.Next != bDest.TrueTarget)
        {
            return false;
        }

        // Duplicating a throwing condition requires the same try region.
        if (!BasicBlock.sameTryRegion(bJump, bDest))
        {
            return false;
        }

        assert(!fgCanCompactBlock(bJump));

        var trueTarget = bDest.TrueTarget;
        var falseTarget = bDest.FalseTarget;

        assert(!bJump.IsLIR);
        assert(!bDest.IsLIR);

        uint estDupCostSz = 0;
        foreach (var stmt in bDest.Statements)
        {
            gtSetStmtInfo(stmt);
            if (fgNodeThreading is NodeThreading.AllTrees)
            {
                fgSetStmtSeq(stmt);
            }

            estDupCostSz += stmt.RootNode.CostSz;
        }

        var haveProfileWeights = false;
        var weightJump = bJump.bbWeight;
        var weightDest = bDest.bbWeight;
        var weightNext = trueTarget.bbWeight;
        var rareJump = bJump.isRunRarely;
        var rareDest = bDest.isRunRarely;
        var rareNext = trueTarget.isRunRarely;

        if (fgIsUsingProfileWeights)
        {
            if ((bJump.hasProfileWeight || bJump.isRunRarely) &&
                (bDest.hasProfileWeight || bDest.isRunRarely) &&
                (trueTarget.hasProfileWeight || trueTarget.isRunRarely))
            {
                haveProfileWeights = true;

                if ((weightJump * 100) < weightDest)
                {
                    rareJump = true;
                }

                if ((weightNext * 100) < weightDest)
                {
                    rareNext = true;
                }

                if (((weightDest * 100) < weightJump) && ((weightDest * 100) < weightNext))
                {
                    rareDest = true;
                }
            }
        }

        uint maxDupCostSz = 6;
        if (rareDest != rareJump)
        {
            maxDupCostSz += 6;
        }

        if (rareDest != rareNext)
        {
            maxDupCostSz += 6;
        }

        if (IsAot && rareJump)
        {
            maxDupCostSz *= 2;
        }

        var costIsTooHigh = estDupCostSz > maxDupCostSz;
#if DEBUG
        if (verbose)
        {
            jitprintf($"\nDuplication of the conditional block {FMT_BB(bDest.bbNum)} (always branch from {FMT_BB(bJump.bbNum)}) " +
                      $"{(costIsTooHigh ? "not done" : "performed")}, because the cost of duplication ({estDupCostSz}) is " +
                      $"{(costIsTooHigh ? "greater" : "less or equal")} than {maxDupCostSz}, haveProfileWeights = {dspBool(haveProfileWeights)}\n");
        }
#endif

        // Costing may have reordered trees even when the duplication is rejected.
        if (costIsTooHigh)
        {
            return true;
        }

        Statement? newStmtList = null;
        Statement? newLastStmt = null;

        for (var curStmt = bDest.GetFirstNonPhiDef(); curStmt is not null; curStmt = curStmt.NextStmt)
        {
            var stmt = gtCloneStmt(curStmt);
            if (fgNodeThreading is NodeThreading.AllTrees)
            {
                gtSetStmtInfo(stmt);
                fgSetStmtSeq(stmt);
            }

            if (newStmtList is not null)
            {
                assert(newLastStmt is not null);
                newLastStmt.NextStmt = stmt;
            }
            else
            {
                newStmtList = stmt;
            }

            stmt.PrevStmt = newLastStmt;
            newLastStmt = stmt;
        }

        assert(newLastStmt is not null);
        var condTree = newLastStmt.RootNode;
        noway_assert(condTree.Oper is GT_JTRUE);
        condTree = condTree.AsUnOp().Op1;

        if (!condTree.Oper.IsCompare)
        {
            return true;
        }

        var lastStmt = bJump.LastStmt;
        assert(newStmtList is not null);
        if (lastStmt is not null)
        {
            var firstStmt = bJump.FirstStmt ?? throw new InvalidOperationException("A block with a last statement must have a first statement.");
            firstStmt.PrevStmt = newLastStmt;
            lastStmt.NextStmt = newStmtList;
            newStmtList.PrevStmt = lastStmt;
        }
        else
        {
            bJump.FirstStmt = newStmtList;
            newStmtList.PrevStmt = newLastStmt;
        }

        bJump.CopyFlags(bDest, BBF_COPY_PROPAGATE);

        var falseEdge = bDest.FalseEdge;
        var trueEdge = bDest.TrueEdge;

        fgRedirectEdge(ref bJump.TargetEdgeRef, falseTarget);
        bJump.TargetEdge.Likelihood = falseEdge.Likelihood;
        bJump.TargetEdge.isHeuristicBased = falseEdge.isHeuristicBased;

        var newTrueEdge = fgAddRefPred(trueTarget, bJump, trueEdge);
        bJump.SetCond(newTrueEdge, bJump.TargetEdge);

        if (haveProfileWeights)
        {
            bDest.decreaseBBProfileWeight(bJump.bbWeight);
            trueTarget.setBBProfileWeight(trueTarget.computeIncomingWeight());
            falseTarget.setBBProfileWeight(falseTarget.computeIncomingWeight());

            if ((trueTarget.NumSucc > 0) || (falseTarget.NumSucc > 0))
            {
                JITDUMP($"fgOptimizeBranch: New flow out of {FMT_BB(bJump.bbNum)} needs to be propagated. Data " +
                        $"{(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                fgPgoConsistent = false;
            }
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"\nfgOptimizeBranch added these statements(s) at the end of {FMT_BB(bJump.bbNum)}:\n");
            for (var stmt = newStmtList; stmt is not null; stmt = stmt.NextStmt)
            {
                gtDispStmt(stmt);
            }
            jitprintf($"\nfgOptimizeBranch changed block {FMT_BB(bJump.bbNum)} from BBJ_ALWAYS to BBJ_COND.\n");
            jitprintf("\nAfter this change in fgOptimizeBranch the BB graph is:");
            fgDispBasicBlocks(verboseTrees);
            jitprintf("\n");
        }
#endif

        var uniquePred = bDest.GetUniquePred(this);
        if ((uniquePred is not null) && fgCanCompactBlock(uniquePred))
        {
            JITDUMP($"{FMT_BB(bDest.bbNum)} can now be compacted into its remaining predecessor.\n");
            fgCompactBlock(uniquePred);
        }

        return true;
    }
}
