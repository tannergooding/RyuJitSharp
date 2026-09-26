// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, earlyprop.cpp.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optFoldNullCheck(GenTree tree, Dictionary<int, GenTree> nullCheckMap)
    {
        var nullCheckTree = optFindNullCheckToFold(tree, nullCheckMap);
        var folded = false;
        if ((nullCheckTree is not null) &&
            optIsNullCheckFoldingLegal(tree, nullCheckTree, out var nullCheckParent, out var nullCheckStmt))
        {
            JITDUMP("optEarlyProp Marking a null check for removal\n");
            DISPTREE(nullCheckTree);
            JITDUMP("\n");

            nullCheckTree.Flags &= ~(GTF_EXCEPT | GTF_DONT_CSE);
            nullCheckTree.HasOrderingSideEffect = true;
            nullCheckTree.Flags |= GTF_IND_NONFAULTING;
            tree.Flags &= ~GTF_IND_NONFAULTING;

            if (nullCheckParent is GenTree parent)
            {
                parent.Flags &= ~GTF_DONT_CSE;
            }

            _ = nullCheckMap.Remove(nullCheckTree.AsIndir().Addr.AsLclVarCommon().LclNum);
            var curStmt = compCurStmt;
            assert(compCurBB is not null);
            _ = fgMorphBlockStmt(compCurBB, nullCheckStmt, allowFGChange: false, message: nameof(optFoldNullCheck));
            optRecordSsaUses(nullCheckStmt.RootNode, compCurBB);
            compCurStmt = curStmt;
            folded = true;
        }

        if ((tree.Oper is GT_NULLCHECK) && (tree.AsIndir().Addr.Oper is GT_LCL_VAR))
        {
            nullCheckMap[tree.AsIndir().Addr.AsLclVarCommon().LclNum] = tree;
        }

        return folded;
    }

    private GenTree? optFindNullCheckToFold(GenTree tree, Dictionary<int, GenTree> nullCheckMap)
    {
        assert(tree.Oper.IsIndirOrArrMetaData);
        var addr = tree.IndirOrArrMetaDataAddr.EffectiveVal;
        nint offset = 0;

        if ((addr.Oper is GT_ADD) && addr.AsOp().Op2.Oper.IsCnsIntOrI)
        {
            offset = unchecked(offset + addr.AsOp().Op2.AsIntConCommon().IconValue);
            addr = addr.AsOp().Op1;
        }

        if (addr.Oper is not GT_LCL_VAR)
        {
            return null;
        }

        var local = addr.AsLclVarCommon();
        if (local.SsaNum == SsaConfig.RESERVED_SSA_NUM)
        {
            return null;
        }

        var lclNum = local.LclNum;
        _ = nullCheckMap.TryGetValue(lclNum, out var nullCheckTree);
        if (nullCheckTree is not null)
        {
            var checkedAddr = nullCheckTree.AsIndir().Addr;
            if ((checkedAddr.Oper is not GT_LCL_VAR) || (checkedAddr.AsLclVarCommon().SsaNum != local.SsaNum))
            {
                nullCheckTree = null;
            }
        }

        if (nullCheckTree is null)
        {
            var def = lvaGetDesc(lclNum).GetPerSsaData(local.SsaNum);
            if (compCurBB != def.Block)
            {
                return null;
            }

            var defNode = def.DefNode;
            if ((defNode is null) || (defNode.Oper is not GT_STORE_LCL_VAR) || (defNode.LclNum != lclNum))
            {
                return null;
            }

            var defValue = defNode.Data;
            if (defValue.Oper is not GT_COMMA)
            {
                return null;
            }

            var checkedValue = defValue.AsOp().Op1.EffectiveVal;
            if (checkedValue.Oper is not GT_NULLCHECK)
            {
                return null;
            }

            var checkedAddress = checkedValue.AsIndir().Addr;
            if ((checkedAddress.Oper is not GT_LCL_VAR) || (defValue.AsOp().Op2.Oper is not GT_ADD))
            {
                return null;
            }

            var addition = defValue.AsOp().Op2.AsOp();
            if ((addition.Op1.Oper is GT_LCL_VAR) &&
                (addition.Op1.AsLclVarCommon().LclNum == checkedAddress.AsLclVarCommon().LclNum) &&
                addition.Op2.Oper.IsCnsIntOrI)
            {
                offset = unchecked(offset + addition.Op2.AsIntConCommon().IconValue);
                nullCheckTree = checkedValue;
            }
        }

        return fgIsBigOffset(offset) ? null : nullCheckTree;
    }

    private bool optIsNullCheckFoldingLegal(GenTree tree, GenTree nullCheckTree,
        out GenTree? nullCheckParent, out Statement nullCheckStmt)
    {
        assert(compCurBB is not null && compCurStmt is not null);
        nullCheckParent = null;
        nullCheckStmt = compCurStmt;
        var isInsideTryOrFilter = compCurBB.HasPotentialEHSuccs(this);
        var canRemoveNullCheck = true;
        const int maxNodesWalked = 50;
        var nodesWalked = 0;

        var previousTree = nullCheckTree;
        var currentTree = nullCheckTree.Next;
        assert(fgNodeThreading is NodeThreading.AllTrees);
        while (canRemoveNullCheck && (currentTree != tree) && (currentTree is not null))
        {
            if (nullCheckParent is null)
            {
                foreach (var operand in currentTree.Operands)
                {
                    if (operand == nullCheckTree)
                    {
                        nullCheckParent = currentTree;
                        break;
                    }
                }
            }

            if ((nodesWalked++ > maxNodesWalked) ||
                !optCanMoveNullCheckPastTree(currentTree, isInsideTryOrFilter, checkSideEffectSummary: false))
            {
                canRemoveNullCheck = false;
            }
            else
            {
                previousTree = currentTree;
                currentTree = currentTree.Next;
            }
        }

        if (currentTree == tree)
        {
            nullCheckStmt = compCurStmt;
        }
        else
        {
            var nullCheckStatementRoot = previousTree;
            currentTree = tree.Prev;
            while (canRemoveNullCheck && (currentTree is not null))
            {
                if ((nodesWalked++ > maxNodesWalked) ||
                    !optCanMoveNullCheckPastTree(currentTree, isInsideTryOrFilter, checkSideEffectSummary: false))
                {
                    canRemoveNullCheck = false;
                }
                else
                {
                    currentTree = currentTree.Prev;
                }
            }

            var curStmt = compCurStmt.PrevStmt;
            assert(curStmt is not null);
            currentTree = curStmt.RootNode;
            while (canRemoveNullCheck && (currentTree != nullCheckStatementRoot))
            {
                if ((nodesWalked++ > maxNodesWalked) ||
                    !optCanMoveNullCheckPastTree(currentTree, isInsideTryOrFilter, checkSideEffectSummary: true))
                {
                    canRemoveNullCheck = false;
                }
                else
                {
                    curStmt = curStmt.PrevStmt;
                    assert(curStmt is not null);
                    currentTree = curStmt.RootNode;
                }
            }
            nullCheckStmt = curStmt;
        }

        if (canRemoveNullCheck && (nullCheckParent is null))
        {
            nullCheckParent = gtFindLink(nullCheckStmt, nullCheckTree).parent;
        }

        return canRemoveNullCheck;
    }

    private bool optCanMoveNullCheckPastTree(GenTree tree, bool isInsideTryOrFilter, bool checkSideEffectSummary)
    {
        var result = true;
        if ((tree.Flags & GTF_CALL) != 0)
        {
            result = !checkSideEffectSummary && !tree.RequiresCallFlag(this);
        }

        if (result && ((tree.Flags & GTF_EXCEPT) != 0))
        {
            result = !checkSideEffectSummary && !tree.MayThrow(this);
        }

        if (result && ((tree.Flags & GTF_ASG) != 0))
        {
            if (tree.Oper.IsStore)
            {
                if (checkSideEffectSummary && ((tree.Data.Flags & GTF_ASG) != 0))
                {
                    result = false;
                }
                else if (isInsideTryOrFilter)
                {
                    result = false;
                    if (tree.Oper is GT_STORE_LCL_VAR)
                    {
                        var descriptor = lvaGetDesc(tree.AsLclVarCommon().LclNum);
                        result = descriptor.lvTracked && !descriptor.IsLiveInOutOfHandler;
                    }
                }
                else
                {
                    result = tree.Oper.IsLocalStore && !lvaGetDesc(tree.AsLclVarCommon().LclNum).IsAddressExposed;
                    if ((tree.Flags & GTF_GLOB_REF) == 0)
                    {
                        result = true;
                    }
                }
            }
            else if (checkSideEffectSummary)
            {
                result = !isInsideTryOrFilter && ((tree.Flags & GTF_GLOB_REF) == 0);
            }
            else
            {
                result = !isInsideTryOrFilter &&
                    (!tree.RequiresAsgFlag || ((tree.Flags & GTF_GLOB_REF) == 0));
            }
        }

        return result;
    }
}
