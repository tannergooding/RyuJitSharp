// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optRedundantRelop(BasicBlock block)
    {
        var statement = block.LastStmt;
        if ((statement is null) || (statement == block.FirstStmt) ||
            (statement.RootNode.Oper is not GT_JTRUE))
        {
            return false;
        }

        var jumpTree = statement.RootNode;
        var tree = jumpTree.AsUnOp().Op1;
        if (!tree.Oper.IsCompare || ((tree.Flags & GTF_SIDE_EFFECT) is not 0 and not GTF_EXCEPT))
        {
            return false;
        }

        assert(vnStore is not null);
        var treeVN = vnStore.VNNormalValue(tree._vnPair.Liberal);
        if (vnStore.IsVNConstant(treeVN))
        {
            JITDUMP(" -- no, jump tree cond is constant\n");
            return false;
        }

        var treeExcVN = vnStore.VNExceptionSet(tree._vnPair.Liberal);
        JITDUMP($"\noptRedundantRelop in {FMT_BB(block.bbNum)}; jump tree is\n");
#if DEBUG
        DISPTREE(jumpTree);
#endif

        var previous = statement;
        GenTree? candidateTree = null;
        Statement? candidateStatement = null;
        var candidateRelation = ValueNumStore.VN_RELATION_KIND.VRK_Same;
        var sideEffect = false;
        const int definedLocalsSize = 10;
        var definedLocals = new int[definedLocalsSize];
        var definedLocalsCount = 0;

        while (true)
        {
            if (sideEffect)
            {
                break;
            }

            previous = previous.PrevStmt;
            assert(previous is not null);
            if (previous == statement)
            {
                break;
            }

            var previousTree = previous.RootNode;
            JITDUMP(" ... checking previous tree\n");
#if DEBUG
            DISPTREE(previousTree);
#endif
            if (previousTree.Oper is GT_NOP)
            {
                continue;
            }

            if (previousTree.Oper is not GT_STORE_LCL_VAR)
            {
                JITDUMP(" -- prev tree not STORE_LCL_VAR\n");
                break;
            }

            var previousValue = previousTree.AsLclVar().Data;
            if (((previousTree.Flags & (GTF_CALL | GTF_ORDER_SIDEEFF)) != 0) ||
                ((previousValue.Flags & GTF_ASG) != 0))
            {
                if (previous.NextStmt != statement)
                {
                    JITDUMP(" -- prev tree has side effects and is not next to jumpTree\n");
                    break;
                }

                JITDUMP(" -- prev tree has side effects, allowing as prev tree is immediately before jumpTree\n");
                sideEffect = true;
            }

            if (previousValue.Oper is GT_PHI)
            {
                JITDUMP(" -- prev tree is a phi\n");
                break;
            }

            var previousLocal = previousTree.AsLclVarCommon().LclNum;
            ref var previousDescriptor = ref lvaGetDesc(previousLocal);
            if (!previousDescriptor.lvTracked)
            {
                JITDUMP($" -- prev tree defs untracked V{previousLocal:D2}\n");
                break;
            }

            if (definedLocalsCount >= definedLocalsSize)
            {
                JITDUMP(" -- ran out of space for tracking kills\n");
                break;
            }

            definedLocals[definedLocalsCount++] = previousLocal;
            var domCmpVN = vnStore.VNNormalValue(previousValue._vnPair.Liberal);
            var matched = false;
            var relationMatch = ValueNumStore.VN_RELATION_KIND.VRK_Same;
            foreach (var relation in s_vnRelations)
            {
                var relatedVN = vnStore.GetRelatedRelop(domCmpVN, relation);
                if ((relatedVN != ValueNumStore.NoVN) && (relatedVN == treeVN))
                {
                    relationMatch = relation;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                JITDUMP(" -- prev tree VN is not related\n");
                continue;
            }

            JITDUMP($"  -- prev tree has relop with {ValueNumStore.VNRelationString(relationMatch)} liberal VN\n");

            if ((treeExcVN != ValueNumStore.VNForEmptyExcSet()) &&
                !vnStore.VNExcIsSubset(vnStore.VNExceptionSet(previousValue._vnPair.Liberal), treeExcVN))
            {
                JITDUMP(" -- prev tree does not anticipate all jump tree exceptions\n");
                break;
            }

            var interferes = false;
            for (var i = 0; i < definedLocalsCount; i++)
            {
                if (gtTreeHasLocalRead(previousValue, definedLocals[i]))
                {
                    JITDUMP($" -- prev tree ref to V{definedLocals[i]:D2} interferes\n");
                    interferes = true;
                    break;
                }
            }

            if (interferes)
            {
                break;
            }

            if (gtMayHaveStoreInterference(previousValue, tree))
            {
#if DEBUG
                JITDUMP($" -- prev tree has an embedded store that interferes with [{tree.TreeId:D6}]\n");
#endif
                break;
            }

            if (!previousValue.Oper.IsCompare)
            {
                JITDUMP(" -- prev tree is not relop\n");
                continue;
            }

            if (VarSetOps.IsMember(this, block.bbLiveOut, previousDescriptor._varIndex))
            {
                JITDUMP($" -- prev tree lcl V{previousLocal:D2} is live-out\n");
                continue;
            }

            if ((previousValue.Flags & GTF_GLOB_REF) != 0)
            {
                // A duplicated global read is safe only if replacing the jump kills
                // every remaining use of the original local.
                var extraUses = false;
                for (var between = previous.NextStmt; between != statement; between = between.NextStmt)
                {
                    assert(between is not null);
                    if (gtTreeHasLocalRead(between.RootNode, previousLocal))
                    {
#if DEBUG
                        JITDUMP($"-- prev tree has GTF_GLOB_REF and {FMT_STMT(between.Id)} has an interfering use\n");
#endif
                        extraUses = true;
                        break;
                    }
                }

                if (extraUses)
                {
                    continue;
                }
            }

            candidateTree = previousValue;
            candidateStatement = previous;
            candidateRelation = relationMatch;
            JITDUMP(" -- prev tree is viable candidate for relop fwd sub!\n");
        }

        if ((candidateTree is null) || (candidateStatement is null))
        {
            return false;
        }

        var usedCopy = candidateStatement.NextStmt != statement;
        // Move an adjacent expression; clone one that must remain in its original store.
        var substitute = usedCopy ? gtCloneExpr(candidateTree) : candidateTree;
        if (candidateRelation is ValueNumStore.VN_RELATION_KIND.VRK_Reverse or
            ValueNumStore.VN_RELATION_KIND.VRK_SwapReverse)
        {
            var original = substitute._vnPair;
            substitute.SetOper(substitute.Oper.ReverseRelop);
            var conservative = vnStore.GetRelatedRelop(
                vnStore.VNNormalValue(original.Conservative), ValueNumStore.VN_RELATION_KIND.VRK_Reverse);
            var liberal = vnStore.GetRelatedRelop(
                vnStore.VNNormalValue(original.Liberal), ValueNumStore.VN_RELATION_KIND.VRK_Reverse);
            substitute._vnPair = vnStore.VNPWithExc(
                new ValueNumPair(liberal, conservative), vnStore.VNPExceptionSet(original));
        }

        substitute.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
        jumpTree.AsUnOp().Op1 = substitute;
        fgSetStmtSeq(statement);
        gtUpdateStmtSideEffects(statement);

        if (!usedCopy)
        {
            fgRemoveStmt(block, candidateStatement);
            var root = candidateStatement.RootNode.AsLclVarCommon();
            ref var def = ref lvaGetDesc(root.LclNum).GetPerSsaData(root.SsaNum);
            assert(def.DefNode == root);
            def.DefNode = null;
        }
        else
        {
            optRecordSsaUses(substitute, block);
        }

        JITDUMP(" -- done! new jump tree is\n");
#if DEBUG
        DISPTREE(jumpTree);
#endif
        return true;
    }
}
