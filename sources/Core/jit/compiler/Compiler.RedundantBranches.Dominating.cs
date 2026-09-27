// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private static BasicBlock optSkipSideEffectFreeBlocks(BasicBlock block)
    {
        var visited = new HashSet<BasicBlock>();
        while (!optRboBlockHasSideEffects(block) && (block.Kind is BBJ_ALWAYS))
        {
            block = block.Target;
            if (!visited.Add(block))
            {
                break;
            }
        }

        return block;
    }

    private bool optRedundantDominatingBranch(BasicBlock block)
    {
        if ((block.Kind is not BBJ_COND) || optRboBlockHasSideEffects(block))
        {
            return false;
        }

        var statement = block.LastStmt;
        if ((statement is null) || (statement.RootNode.Oper is not GT_JTRUE))
        {
            return false;
        }

        var jumpTree = statement.RootNode;
        var tree = jumpTree.AsUnOp().Op1;
        if (!tree.Oper.IsCompare)
        {
            return false;
        }

        assert(vnStore is not null);
        var treeNormVN = vnStore.VNNormalValue(tree._vnPair.Liberal);
        if (vnStore.IsVNConstant(treeNormVN))
        {
            return false;
        }

        var treeApp = new VNFuncApp();
        if (!vnStore.GetVNFunc(treeNormVN, ref treeApp) ||
            !ValueNumStore.VNFuncIsComparison(treeApp.Func) ||
            varTypeIsFloating(vnStore.TypeOfVN(treeApp.GetArg(0))))
        {
            return false;
        }

        var blockTrueSucc = optSkipSideEffectFreeBlocks(block.TrueTarget);
        var blockFalseSucc = optSkipSideEffectFreeBlocks(block.FalseTarget);
        var currentBlock = block;
        var domBlockProbe = fgGetDomSpeculatively(block);
        var madeChanges = false;
        var searchCount = 0;

        JITDUMP($"Checking {FMT_BB(block.bbNum)} for redundant dominating branches\n");
        if (domBlockProbe is null)
        {
            JITDUMP("failed -- no dominator\n");
        }

        while (domBlockProbe is not null)
        {
            if (++searchCount > 8)
            {
                JITDUMP("stopping, hit search limit\n");
                break;
            }

            while ((domBlockProbe is not null) && (domBlockProbe.Kind is BBJ_ALWAYS))
            {
                if ((domBlockProbe.Target != currentBlock) || optRboBlockHasSideEffects(domBlockProbe))
                {
                    domBlockProbe = null;
                    break;
                }

                currentBlock = domBlockProbe;
                domBlockProbe = fgGetDomSpeculatively(domBlockProbe);
            }

            if (domBlockProbe is null)
            {
                JITDUMP("failed -- no dominator\n");
                break;
            }

            if ((domBlockProbe.Kind is not BBJ_COND) || domBlockProbe.HasFlag(BBF_STALE_PREDICATE))
            {
                JITDUMP($"failed -- dominator {FMT_BB(domBlockProbe.bbNum)} is not a usable BBJ_COND\n");
                break;
            }

            currentBlock = optSkipSideEffectFreeBlocks(currentBlock);
            var domTrueSucc = optSkipSideEffectFreeBlocks(domBlockProbe.TrueTarget);
            var domFalseSucc = optSkipSideEffectFreeBlocks(domBlockProbe.FalseTarget);
            var currentIsDomTrueSucc = domTrueSucc == currentBlock;
            var currentIsDomFalseSucc = domFalseSucc == currentBlock;
            if (currentIsDomTrueSucc == currentIsDomFalseSucc)
            {
                JITDUMP($"failed -- {FMT_BB(domBlockProbe.bbNum)} is degnerate\n");
                break;
            }

            var sharedSuccessor = currentIsDomTrueSucc ? domFalseSucc : domTrueSucc;
            ValueNum blockPathVN;
            if (sharedSuccessor == blockFalseSucc)
            {
                blockPathVN = treeNormVN;
            }
            else if (sharedSuccessor == blockTrueSucc)
            {
                blockPathVN = vnStore.GetRelatedRelop(treeNormVN, ValueNumStore.VN_RELATION_KIND.VRK_Reverse);
            }
            else
            {
                JITDUMP($"failed -- {FMT_BB(domBlockProbe.bbNum)} does not share a successor with {FMT_BB(block.bbNum)}\n");
                break;
            }

            if (blockPathVN == ValueNumStore.NoVN)
            {
                break;
            }

            var domStatement = domBlockProbe.LastStmt;
            assert(domStatement is not null && domStatement.RootNode.Oper is GT_JTRUE);
            var domJumpTree = domStatement.RootNode;
            var domTree = domJumpTree.AsUnOp().Op1;
            if (!domTree.Oper.IsCompare)
            {
                break;
            }

            var domNormVN = vnStore.VNNormalValue(domTree._vnPair.Liberal);
            if (vnStore.IsVNConstant(domNormVN))
            {
                break;
            }

            var domPathVN = currentIsDomFalseSucc
                ? vnStore.GetRelatedRelop(domNormVN, ValueNumStore.VN_RELATION_KIND.VRK_Reverse)
                : domNormVN;
            if (domPathVN == ValueNumStore.NoVN)
            {
                break;
            }

            var info = new RelopImplicationInfo {
                TreeNormVN = domPathVN,
                DomCmpNormVN = blockPathVN,
            };
            optRelopImpliesRelop(ref info);
            var canOptimize = info.CanInfer && info.CanInferFromTrue && !info.ReverseSense;
            var newRelop = GT_NONE;
            var isUnsigned = false;

            if (!canOptimize)
            {
                var andVN = vnStore.VNForFunc(TYP_INT, VNF_AND, blockPathVN, domPathVN);
                var andApp = new VNFuncApp();
                var pathApp = new VNFuncApp();
                var newRelopFunc = VNF_NONE;
                if (vnStore.IsVNRelop(andVN, ref andApp) && vnStore.GetVNFunc(blockPathVN, ref pathApp))
                {
                    if ((andApp.GetArg(0) == pathApp.GetArg(0)) && (andApp.GetArg(1) == pathApp.GetArg(1)))
                    {
                        newRelopFunc = andApp.Func;
                    }
                    else if ((andApp.GetArg(0) == pathApp.GetArg(1)) &&
                             (andApp.GetArg(1) == pathApp.GetArg(0)))
                    {
                        andVN = vnStore.GetRelatedRelop(andVN, ValueNumStore.VN_RELATION_KIND.VRK_Swap);
                        if (vnStore.GetVNFunc(andVN, ref andApp))
                        {
                            newRelopFunc = andApp.Func;
                        }
                    }
                }

                if (newRelopFunc != VNF_NONE)
                {
                    // A liberal VN may look through a materialized predicate. Rewriting the tree
                    // is valid only when its operands are the operands of the VN relop.
                    var treeOp1VN = vnStore.VNNormalValue(tree.AsOp().Op1._vnPair.Liberal);
                    var treeOp2VN = vnStore.VNNormalValue(tree.AsOp().Op2._vnPair.Liberal);
                    if ((pathApp.GetArg(0) != treeOp1VN) || (pathApp.GetArg(1) != treeOp2VN))
                    {
                        JITDUMP("; relop operands do not match tree operands, cannot simplify\n");
                        break;
                    }

                    newRelop = vnStore.VNRelopToGenTreeOp(newRelopFunc, out isUnsigned);
                    if (newRelop != GT_NONE)
                    {
                        canOptimize = true;
                    }
                }
            }

            if (!canOptimize)
            {
                JITDUMP($"failed -- Dominated VN {blockPathVN} does not imply dominating VN {domPathVN}\n");
                break;
            }

            var domRelopValue = currentIsDomTrueSucc ? 1 : 0;
            var domMayHaveSideEffects = (domTree.Flags & GTF_SIDE_EFFECT) != 0;
            if (domMayHaveSideEffects)
            {
                domJumpTree.AsUnOp().Op1 = gtNewCommaNode(TYP_INT, domTree,
                    gtNewIconNode(TYP_INT, domRelopValue));
            }
            else
            {
                var constant = new GenTreeIntCon(TYP_INT, domRelopValue, null,
                    domTree, NodeThreading.AllTrees);
                fgUpdateConstTreeValueNumber(constant);
                constant.Flags &= ~GTF_ALL_EFFECT;
                domJumpTree.AsUnOp().Op1 = constant;
            }

            _ = fgMorphBlockStmt(domBlockProbe, domStatement, allowFGChange: true,
                invalidateDFSTreeOnFGChange: false, message: nameof(optRedundantDominatingBranch));
            Metrics.RedundantBranchesEliminated++;

            if (newRelop != GT_NONE)
            {
                if (sharedSuccessor == blockTrueSucc)
                {
                    newRelop = newRelop.ReverseRelop;
                }

                tree.SetOper(newRelop);
                if (isUnsigned)
                {
                    tree.Flags |= GTF_UNSIGNED;
                }
                else
                {
                    tree.Flags &= ~GTF_UNSIGNED;
                }

                fgValueNumberTree(tree);
                treeNormVN = vnStore.VNNormalValue(tree._vnPair.Liberal);
            }

            madeChanges = true;
            domMayHaveSideEffects |= optRboBlockHasSideEffects(domBlockProbe);
            if (domMayHaveSideEffects)
            {
                JITDUMP("stopping -- side effects seen along path to block\n");
                break;
            }

            currentBlock = domBlockProbe;
            domBlockProbe = fgGetDomSpeculatively(domBlockProbe);
        }

        return madeChanges;
    }
}
