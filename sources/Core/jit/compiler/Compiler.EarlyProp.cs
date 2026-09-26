// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, earlyprop.cpp.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private enum OptPropKind
    {
        Invalid,
        ArrayLength,
        NullCheck,
    }

    private bool optDoEarlyPropForFunc()
    {
        var propArrayLen = (optMethodFlags & OMF_HAS_ARRAYREF) != 0;
        var propNullCheck = (optMethodFlags & OMF_HAS_NULLCHECK) != 0;
        return propArrayLen || propNullCheck;
    }

    public PhaseStatus optEarlyProp()
    {
        if (!optDoEarlyPropForFunc())
        {
            JITDUMP("no arrays or null checks in the method\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert(fgSsaPassesCompleted == 1);
        var numChanges = 0;

        foreach (var block in Blocks)
        {
            compCurBB = block;
            var nullCheckMap = new Dictionary<int, GenTree>();

            for (var stmt = block.FirstStmt; stmt is not null;)
            {
                var next = stmt.NextStmt;
                if ((stmt.RootNode is null) || ((stmt.RootNode.Flags & GTF_ALL_EFFECT) == 0))
                {
                    stmt = next;
                    continue;
                }

                compCurStmt = stmt;
                var isRewritten = false;
                for (var tree = stmt.TreeListBegin; tree is not null; tree = tree.Next)
                {
                    var rewrittenTree = optEarlyPropRewriteTree(tree, nullCheckMap);
                    if (rewrittenTree is not null)
                    {
                        gtUpdateSideEffects(stmt, rewrittenTree);
                        isRewritten = true;
                        tree = rewrittenTree;
                    }
                }

                if (isRewritten)
                {
                    gtSetStmtInfo(stmt);
                    fgSetStmtSeq(stmt);
                    numChanges++;
                }

                stmt = next;
            }
        }

        JITDUMP($"\nOptimized {numChanges} trees\n");
        return numChanges > 0 ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private GenTree? optEarlyPropRewriteTree(GenTree tree, Dictionary<int, GenTree> nullCheckMap)
    {
        if (!tree.Oper.IsIndirOrArrMetaData)
        {
            return null;
        }

        var folded = optFoldNullCheck(tree, nullCheckMap);
        if (tree.Oper is not GT_ARR_LENGTH)
        {
            return folded ? tree : null;
        }

        var objectRef = tree.AsArrCommon().ArrRef;
        if (!objectRef.Oper.IsScalarLocal || !lvaGetDesc(objectRef.AsLclVarCommon().LclNum).lvInSsa)
        {
            return folded ? tree : null;
        }

        var local = objectRef.AsLclVarCommon();
        var actualValue = optPropGetValue(local.LclNum, local.SsaNum, OptPropKind.ArrayLength);
        if (actualValue is not null)
        {
            assert(actualValue.Oper.IsCnsIntOrI && !actualValue.IsIconHandle());
            var length = actualValue.AsIntCon().IconValue;
            if ((length < 0) || (length > CORINFO_Array_MaxLength))
            {
                return null;
            }

            if ((tree.Next is GenTreeBoundsChk check) && (check.ArrayLength == tree) &&
                check.Index.Oper.IsCnsIntOrI)
            {
                var index = check.Index.AsIntCon().IconValue;
                if ((index >= 0) && (index < length))
                {
                    assert(compCurStmt is not null);
                    var parent = gtFindLink(compCurStmt, check).parent;
                    assert(((parent is not null) && (parent.Oper is GT_COMMA) &&
                        ((parent.AsOp().Op1 == check) || (parent.Type is TYP_VOID))) ||
                        (check == compCurStmt.RootNode));

                    if (((parent is not null) && (parent.Oper is GT_COMMA) && (parent.AsOp().Op1 == check)) ||
                        (check == compCurStmt.RootNode))
                    {
                        return optRemoveRangeCheck(check, parent, compCurStmt);
                    }
                }
            }

            assert(compCurBB is not null && compCurStmt is not null);
            JITDUMP($"optEarlyProp Rewriting {FMT_BB(compCurBB.bbNum)}\n");
            DISPSTMT(compCurStmt);
            JITDUMP("\n");
        }

        return folded ? tree : null;
    }

    private GenTree? optPropGetValue(int lclNum, int ssaNum, OptPropKind kind)
        => optPropGetValueRec(lclNum, ssaNum, kind, 0);

    private GenTree? optPropGetValueRec(int lclNum, int ssaNum, OptPropKind kind, int walkDepth)
    {
        if ((ssaNum == SsaConfig.RESERVED_SSA_NUM) || (walkDepth > optEarlyPropRecurBound))
        {
            return null;
        }

        var defStore = lvaGetDesc(lclNum).GetPerSsaData(ssaNum).DefNode;
        if (defStore is null)
        {
            return null;
        }

        assert(defStore.Oper.IsLocalStore);
        var defValue = defStore.Data;
        if ((defStore.Oper is GT_STORE_LCL_VAR) && (defStore.LclNum == lclNum) &&
            (defValue.Oper is GT_LCL_VAR))
        {
            var source = defValue.AsLclVarCommon();
            return optPropGetValueRec(source.LclNum, source.SsaNum, kind, walkDepth + 1);
        }

        if (kind is OptPropKind.ArrayLength)
        {
            var length = getArrayLengthFromAllocation(defValue);
            return length is not null && length.Oper.IsCnsIntOrI ? length : null;
        }

        return null;
    }
}
