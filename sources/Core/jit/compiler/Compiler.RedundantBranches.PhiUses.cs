// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private struct JumpThreadPhiUseVisitor(
        int localNumber, int ssaNumber, BasicBlock block, List<JumpThreadInfo.PhiUse> uses)
        : IGenTreeVisitor<JumpThreadPhiUseVisitor>
    {
        public static bool ComputeStack => true;
        public static bool DoPreOrder => true;
        public static bool DoLclVarsOnly => true;

        private readonly GenTreeStack _ancestors = [];

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.Oper.IsLocalRead)
            {
                var local = use.AsLclVarCommon();
                if ((local.LclNum == localNumber) && (local.SsaNum == ssaNumber))
                {
                    var phiUse = new JumpThreadInfo.PhiUse(block, local);
                    GenTree? child = null;
                    foreach (var ancestor in _ancestors)
                    {
                        if (child is null)
                        {
                            child = ancestor;
                        }
                        else if ((ancestor.Oper is GT_COMMA) && (ancestor.AsOp().Op2 == child))
                        {
                            phiUse.CommaParents.Add(ancestor.AsOp());
                            child = ancestor;
                        }
                        else
                        {
                            break;
                        }
                    }

                    uses.Add(phiUse);
                }
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<JumpThreadPhiUseVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private static bool optGetThreadedSsaNumForBlock(
        JumpThreadInfo info, GenTreeLclVar phiDef, out int replacementSsaNum)
    {
        assert(info.NumAmbiguousPreds != 0);
        var covered = new HashSet<BasicBlock>();
        replacementSsaNum = SsaConfig.RESERVED_SSA_NUM;
        var found = false;
        foreach (var use in phiDef.Data.AsPhi().Uses)
        {
            var arg = use.Node.AsPhiArg();
            if (!info.AmbiguousPreds.Contains(arg.PredBB))
            {
                continue;
            }

            _ = covered.Add(arg.PredBB);
            if (!found)
            {
                replacementSsaNum = arg.SsaNum;
                found = true;
            }
            else if (replacementSsaNum != arg.SsaNum)
            {
                return false;
            }
        }

        return found && covered.SetEquals(info.AmbiguousPreds);
    }

    private static bool optGetThreadedSsaNumForSuccessor(
        JumpThreadInfo info, GenTreeLclVar phiDef, BasicBlock successor,
        out bool hasThreadedPreds, out int replacementSsaNum)
    {
        hasThreadedPreds = false;
        replacementSsaNum = SsaConfig.RESERVED_SSA_NUM;
        var expected = new HashSet<BasicBlock>(info.AmbiguousPreds);
        foreach (var predecessor in info.Block.PredBlocks)
        {
            if (info.AmbiguousPreds.Contains(predecessor))
            {
                continue;
            }

            var target = info.TruePreds.Contains(predecessor) ? info.TrueTarget : info.FalseTarget;
            if (target == successor)
            {
                _ = expected.Add(predecessor);
                hasThreadedPreds = true;
            }
        }

        var covered = new HashSet<BasicBlock>();
        var found = false;
        foreach (var use in phiDef.Data.AsPhi().Uses)
        {
            var arg = use.Node.AsPhiArg();
            if (!expected.Contains(arg.PredBB))
            {
                continue;
            }

            _ = covered.Add(arg.PredBB);
            if (!found)
            {
                replacementSsaNum = arg.SsaNum;
                found = true;
            }
            else if (replacementSsaNum != arg.SsaNum)
            {
                return false;
            }
        }

        return found && covered.SetEquals(expected);
    }

    private bool optFindPhiUsesInBlockAndSuccessors(
        BasicBlock block, GenTreeLclVar phiDef, JumpThreadInfo info)
    {
        ref var ssaDef = ref lvaGetDesc(phiDef.LclNum).GetPerSsaData(phiDef.SsaNum);
        if (ssaDef.NumUses == ushort.MaxValue)
        {
            return false;
        }

        var initialCount = info.PhiUses.Count;
        var blockVisitor = new JumpThreadPhiUseVisitor(
            phiDef.LclNum, phiDef.SsaNum, block, info.PhiUses);
        foreach (var statement in block.Statements)
        {
            _ = blockVisitor.WalkTree(ref statement.RootNodeRef, null);
        }

        foreach (var successor in block.Succs)
        {
            if (successor == block)
            {
                continue;
            }

            var successorVisitor = new JumpThreadPhiUseVisitor(
                phiDef.LclNum, phiDef.SsaNum, successor, info.PhiUses);
            foreach (var statement in successor.Statements)
            {
                _ = successorVisitor.WalkTree(ref statement.RootNodeRef, null);
            }
        }

        return info.PhiUses.Count - initialCount == ssaDef.NumUses;
    }

    private bool optCanRewritePhiUses(JumpThreadInfo info)
    {
        foreach (var statement in info.Block.Statements)
        {
            if (!statement.IsPhiDefnStmt)
            {
                break;
            }

            var phiDef = statement.RootNode.AsLclVar();
            if (!lvaGetDesc(phiDef.LclNum).GetPerSsaData(phiDef.SsaNum).HasGlobalUse)
            {
                continue;
            }

            if (!optFindPhiUsesInBlockAndSuccessors(info.Block, phiDef, info))
            {
                return false;
            }

            var hasBlockUse = false;
            foreach (var use in info.PhiUses)
            {
                if ((use.Use.LclNum == phiDef.LclNum) && (use.Use.SsaNum == phiDef.SsaNum) &&
                    (use.Block == info.Block))
                {
                    hasBlockUse = true;
                    break;
                }
            }

            var removePhiDef = false;
            if (hasBlockUse && (info.NumAmbiguousPreds != 0))
            {
                if (!optGetThreadedSsaNumForBlock(info, phiDef, out var replacement))
                {
                    return false;
                }

                foreach (var use in info.PhiUses)
                {
                    if ((use.Use.LclNum == phiDef.LclNum) && (use.Use.SsaNum == phiDef.SsaNum) &&
                        (use.Block == info.Block))
                    {
                        use.ReplacementSsaNum = replacement;
                    }
                }

                info.PhiDefsToRemove.Add(statement);
                removePhiDef = true;
            }

            foreach (var successor in info.Block.Succs)
            {
                var hasSuccUse = false;
                foreach (var use in info.PhiUses)
                {
                    if ((use.Use.LclNum == phiDef.LclNum) && (use.Use.SsaNum == phiDef.SsaNum) &&
                        (use.Block == successor))
                    {
                        hasSuccUse = true;
                        break;
                    }
                }

                if (!hasSuccUse)
                {
                    continue;
                }

                if ((successor.GetUniquePred(this) != info.Block) ||
                    !optGetThreadedSsaNumForSuccessor(info, phiDef, successor, out var threaded, out var replacement))
                {
                    return false;
                }

                if (!threaded && !removePhiDef)
                {
                    continue;
                }

                foreach (var use in info.PhiUses)
                {
                    if ((use.Use.LclNum == phiDef.LclNum) && (use.Use.SsaNum == phiDef.SsaNum) &&
                        (use.Block == successor))
                    {
                        use.ReplacementSsaNum = replacement;
                    }
                }
            }
        }

        return true;
    }
}
