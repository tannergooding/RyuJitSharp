// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private enum JumpThreadCheckResult
    {
        CannotThread,
        CanThread,
        NeedsPhiUseResolution,
    }

    private sealed class JumpThreadInfo(BasicBlock block)
    {
        public readonly BasicBlock Block = block;
        public readonly BasicBlock TrueTarget = block.TrueTarget;
        public readonly BasicBlock FalseTarget = block.FalseTarget;
        public readonly HashSet<BasicBlock> TruePreds = [];
        public readonly HashSet<BasicBlock> AmbiguousPreds = [];
        public readonly List<PhiUse> PhiUses = [];
        public readonly List<Statement> PhiDefsToRemove = [];
        public BasicBlock? AmbiguousVNBlock;
        public ValueNum AmbiguousVN = ValueNumStore.NoVN;
        public int NumPreds;
        public int NumAmbiguousPreds;
        public int NumTruePreds;
        public int NumFalsePreds;
        public bool IsPhiBased;

        public sealed class PhiUse(BasicBlock block, GenTreeLclVarCommon use)
        {
            public readonly BasicBlock Block = block;
            public readonly GenTreeLclVarCommon Use = use;
            public readonly List<GenTreeOp> CommaParents = [];
            public int ReplacementSsaNum = SsaConfig.RESERVED_SSA_NUM;
        }
    }

    private JumpThreadCheckResult optJumpThreadCheck(BasicBlock block, BasicBlock? domBlock)
    {
        if (bbIsTryBeg(block))
        {
            JITDUMP($"{FMT_BB(block.bbNum)} is first block of try-region; no threading\n");
            return JumpThreadCheckResult.CannotThread;
        }

        if (domBlock is not null)
        {
            assert(_dfsTree is not null && _domTree is not null);
            foreach (var predecessor in block.PredBlocks)
            {
                if (_dfsTree.Contains(predecessor) && !_domTree.Dominates(domBlock, predecessor))
                {
                    JITDUMP($"Dom {FMT_BB(domBlock.bbNum)} is stale; no threading\n");
                    return JumpThreadCheckResult.CannotThread;
                }
            }
        }

        var lastStatement = block.LastStmt;
        var isPhiRbo = domBlock is null;
        var hasGlobalPhiUses = false;
        foreach (var statement in block.Statements)
        {
            var root = statement.RootNode;
            if (statement.IsPhiDefnStmt)
            {
                if (isPhiRbo)
                {
                    var phiDef = root.AsLclVarCommon();
                    ref var descriptor = ref lvaGetDesc(phiDef.LclNum);
                    // Promoted fields can have implicit uses that SSA does not record.
                    if (descriptor.lvIsStructField)
                    {
                        return JumpThreadCheckResult.CannotThread;
                    }

                    if (descriptor.GetPerSsaData(phiDef.SsaNum).HasGlobalUse)
                    {
                        hasGlobalPhiUses = true;
                    }
                }

                continue;
            }

            if ((root.Flags & GTF_SIDE_EFFECT) != 0)
            {
                if ((statement == lastStatement) && (root.Oper is GT_JTRUE) &&
                    ((root.Flags & GTF_SIDE_EFFECT) == GTF_EXCEPT) && (domBlock is not null) &&
                    BasicBlock.sameEHRegion(block, domBlock))
                {
                    continue;
                }

                JITDUMP($"{FMT_BB(block.bbNum)} has side effects; no threading\n");
                return JumpThreadCheckResult.CannotThread;
            }
        }

        return hasGlobalPhiUses ? JumpThreadCheckResult.NeedsPhiUseResolution : JumpThreadCheckResult.CanThread;
    }

    private bool optJumpThreadDom(
        BasicBlock block, BasicBlock domBlock, bool domIsSameRelop, ValueNum domCmpExcVN, ValueNum treeExcVN)
    {
        assert(vnStore is not null);
        if (!vnStore.VNExcIsSubset(domCmpExcVN, treeExcVN))
        {
            JITDUMP("Dominating compare does not anticipate all current relop exceptions\n");
            return false;
        }

        if (domBlock != block.bbIDom)
        {
            var immediate = block.bbIDom;
            while ((immediate is not null) && (immediate != domBlock))
            {
                if (immediate.Kind is BBJ_COND)
                {
                    return false;
                }

                immediate = immediate.bbIDom;
            }

            assert(immediate == domBlock);
        }

        var check = optJumpThreadCheck(block, domBlock);
        if (check is not JumpThreadCheckResult.CanThread)
        {
            return false;
        }

        var domTrueSuccessor = domIsSameRelop ? domBlock.TrueTarget : domBlock.FalseTarget;
        var domFalseSuccessor = domIsSameRelop ? domBlock.FalseTarget : domBlock.TrueTarget;
        var info = new JumpThreadInfo(block);
        foreach (var predecessor in block.PredBlocks)
        {
            info.NumPreds++;
            if (predecessor.Kind is BBJ_SWITCH)
            {
                _ = info.AmbiguousPreds.Add(predecessor);
                info.NumAmbiguousPreds++;
                continue;
            }

            var isTruePred = (predecessor == domBlock)
                ? domTrueSuccessor == block : optReachable(domTrueSuccessor, predecessor, domBlock);
            var isFalsePred = (predecessor == domBlock)
                ? domFalseSuccessor == block : optReachable(domFalseSuccessor, predecessor, domBlock);
            if (isTruePred == isFalsePred)
            {
                _ = info.AmbiguousPreds.Add(predecessor);
                info.NumAmbiguousPreds++;
                continue;
            }

            if (isTruePred)
            {
                if (!BasicBlock.sameEHRegion(predecessor, info.TrueTarget))
                {
                    _ = info.AmbiguousPreds.Add(predecessor);
                    info.NumAmbiguousPreds++;
                    continue;
                }

                _ = info.TruePreds.Add(predecessor);
                info.NumTruePreds++;
            }
            else
            {
                if (!BasicBlock.sameEHRegion(predecessor, info.FalseTarget))
                {
                    _ = info.AmbiguousPreds.Add(predecessor);
                    info.NumAmbiguousPreds++;
                    continue;
                }

                info.NumFalsePreds++;
            }
        }

        return optJumpThreadCore(info);
    }

    private bool optJumpThreadPhi(BasicBlock block, GenTree tree, ValueNum treeNormVN)
    {
        var check = optJumpThreadCheck(block, null);
        if (check is JumpThreadCheckResult.CannotThread)
        {
            return false;
        }

        assert(vnStore is not null);
        var treeApp = new VNFuncApp();
        if (!vnStore.GetVNFunc(treeNormVN, ref treeApp) || (treeApp.Arity != 2) ||
            bbIsHandlerBeg(block))
        {
            return false;
        }

        var phiLocals = new int[] { BAD_VAR_NUM, BAD_VAR_NUM };
        var phiDefs = new GenTreeLclVar?[2];
        var foundPhiDef = false;
        for (var index = 0; index < 2; index++)
        {
            var phiVN = new VNPhiDef();
            if (!vnStore.GetPhiDef(treeApp.GetArg(index), ref phiVN))
            {
                continue;
            }

            foreach (var statement in block.Statements)
            {
                if (!statement.IsPhiDefnStmt)
                {
                    break;
                }

                var def = statement.RootNode.AsLclVar();
                if (def.LclNum == phiVN.LclNum)
                {
                    if (def.SsaNum == phiVN.SsaDef)
                    {
                        phiLocals[index] = phiVN.LclNum;
                        phiDefs[index] = def;
                        foundPhiDef = true;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        if (!foundPhiDef)
        {
            return false;
        }

        var info = new JumpThreadInfo(block) { IsPhiBased = true };
        foreach (var predecessor in block.PredBlocks)
        {
            info.NumPreds++;
            var newArgs = new[] { treeApp.GetArg(0), treeApp.GetArg(1) };
            var updatedArg = false;
            for (var index = 0; index < 2; index++)
            {
                if (phiLocals[index] == BAD_VAR_NUM)
                {
                    continue;
                }

                var phiDef = phiDefs[index] ?? throw new InvalidOperationException(
                    "The mapped phi operand has no definition.");
                foreach (var use in phiDef.Data.AsPhi().Uses)
                {
                    var arg = use.Node.AsPhiArg();
                    assert(arg.LclNum == phiLocals[index]);
                    if ((arg.PredBB == predecessor) && (arg._vnPair.Liberal != ValueNumStore.NoVN))
                    {
                        newArgs[index] = arg._vnPair.Liberal;
                        updatedArg = true;
                        break;
                    }
                }
            }

            if (!updatedArg)
            {
                _ = info.AmbiguousPreds.Add(predecessor);
                info.NumAmbiguousPreds++;
                continue;
            }

            var substVN = vnStore.VNForFunc(tree.Type, treeApp.Func, newArgs[0], newArgs[1]);
            if (vnStore.IsVNConstant(substVN))
            {
                var isTrue = substVN != vnStore.VNZeroForType(TYP_INT);
                var target = isTrue ? info.TrueTarget : info.FalseTarget;
                if (!BasicBlock.sameEHRegion(predecessor, target))
                {
                    _ = info.AmbiguousPreds.Add(predecessor);
                    info.NumAmbiguousPreds++;
                    continue;
                }

                if (isTrue)
                {
                    _ = info.TruePreds.Add(predecessor);
                    info.NumTruePreds++;
                }
                else
                {
                    info.NumFalsePreds++;
                }
            }
            else
            {
                _ = info.AmbiguousPreds.Add(predecessor);
                info.NumAmbiguousPreds++;
                if ((info.NumAmbiguousPreds == 1) && (substVN != treeNormVN))
                {
                    info.AmbiguousVN = substVN;
                    info.AmbiguousVNBlock = predecessor;
                }
            }
        }

        if ((check is JumpThreadCheckResult.NeedsPhiUseResolution) && !optCanRewritePhiUses(info))
        {
            return false;
        }

        return optJumpThreadCore(info);
    }
}
