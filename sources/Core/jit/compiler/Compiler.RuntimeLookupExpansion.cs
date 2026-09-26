// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
#if DEBUG
using static RyuJitSharp.GenTreeCallDebugFlags;
#endif
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgExpandRuntimeLookups()
    {
        if (!MethodHasExpRuntimeLookup)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        return fgExpandHelper(fgExpandRuntimeLookupsForCall);
    }

    private unsafe bool fgExpandRuntimeLookupsForCall(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        if (!call.IsHelperCall() || (call.HelperNum is not (CORINFO_HELP_RUNTIMEHANDLE_METHOD or CORINFO_HELP_RUNTIMEHANDLE_CLASS)))
        {
            return false;
        }

#if DEBUG
        call._callDebugFlags |= GTF_CALL_MD_RUNTIME_LOOKUP_EXPANDED;
#endif
        if (call.IsTailCall)
        {
            return false;
        }

        assert(call.Args.CountUserArgs() == 2);
        var signatureNode = call.Args.GetUserArgByIndex(1)?.Node
            ?? throw new FatalJitException("A runtime lookup helper requires a signature argument.");
        if (!signatureNode.Oper.IsCnsIntOrI)
        {
            assert(false, "can't restore signature argument value");
            return false;
        }

        var signature = (void*)signatureNode.AsIntCon().IconValue;
#if DEBUG
        JITDUMP($"Expanding runtime lookup for [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}:\n");
        DISPTREE(call);
        JITDUMP("\n");
#endif
        if (!SignatureToLookupInfoMap.TryGetValue(signature, out var runtimeLookup))
        {
            throw new FatalJitException("Runtime lookup signature is not registered.");
        }

        var needsSizeCheck = runtimeLookup.sizeOffset != CORINFO_NO_SIZE_CHECK;
        if (needsSizeCheck)
        {
            JITDUMP("dynamic expansion, needs size check.\n");
        }

        var debugInfo = stmt.DebugInfo;
        if ((runtimeLookup.indirections is 0 or > CORINFO_MAXINDIRECTIONS) || !runtimeLookup.testForNull)
        {
            throw new FatalJitException("An expandable runtime lookup requires indirections and a null check.");
        }

        var prevBb = block;
        ref var callUse = ref fgSplitBlockBeforeTree(block, stmt, call, out var newFirstStmt, out block);
        while ((newFirstStmt is not null) && (newFirstStmt != stmt))
        {
            fgMorphStmtBlockOps(block, newFirstStmt);
            newFirstStmt = newFirstStmt.NextStmt;
        }

        GenTreeLclVar rtLookupLcl;
        if ((stmt.RootNode is GenTreeLclVar store) && (store.Oper is GT_STORE_LCL_VAR) && (store.Data == callUse))
        {
            rtLookupLcl = gtNewLclVarNode(call.Type, store.LclNum);
            fgRemoveStmt(block, stmt);
        }
        else
        {
            var rtLookupLclNum = lvaGrabTemp(shortLifetime: true, "runtime lookup");
            lvaTable[rtLookupLclNum].Type = TYP_I_IMPL;
            rtLookupLcl = gtNewLclvNode(call.Type, rtLookupLclNum);
            callUse = gtCloneExpr(rtLookupLcl)
                ?? throw new FatalJitException("Cannot clone runtime lookup result local.");
            fgMorphStmtBlockOps(block, stmt);
            gtUpdateStmtSideEffects(stmt);
        }

        var ctxTree = call.Args.GetUserArgByIndex(0)?.Node
            ?? throw new FatalJitException("A runtime lookup helper requires a context argument.");
        var slotPtrTree = gtCloneExpr(ctxTree)
            ?? throw new FatalJitException("Cannot clone runtime lookup context.");
        GenTree? indOffTree = null;
        GenTree? lastIndOfTree = null;

        for (var i = 0; i < runtimeLookup.indirections; i++)
        {
            var indirectOffset = ((i == 1) && runtimeLookup.indirectFirstOffset)
                || ((i == 2) && runtimeLookup.indirectSecondOffset);
            if (indirectOffset)
            {
                indOffTree = SpillExpression(slotPtrTree, prevBb, debugInfo);
                slotPtrTree = gtCloneExpr(indOffTree)
                    ?? throw new FatalJitException("Cannot clone indirect runtime lookup offset.");
            }

            var lastWithSizeCheck = (i == runtimeLookup.indirections - 1) && needsSizeCheck;
            if (i != 0)
            {
                var indirFlags = GTF_IND_NONFAULTING;
                if (!lastWithSizeCheck)
                {
                    indirFlags |= GTF_IND_INVARIANT;
                }

                slotPtrTree = gtNewIndir(TYP_I_IMPL, slotPtrTree, indirFlags);
            }

            if (indirectOffset)
            {
                slotPtrTree = gtNewBinaryNode(GT_ADD, TYP_I_IMPL,
                    indOffTree ?? throw new FatalJitException("Missing indirect runtime lookup offset."),
                    slotPtrTree);
            }

            if (runtimeLookup.offsets[i] != 0)
            {
                if (lastWithSizeCheck)
                {
                    lastIndOfTree = SpillExpression(slotPtrTree, prevBb, debugInfo);
                    slotPtrTree = gtCloneExpr(lastIndOfTree)
                        ?? throw new FatalJitException("Cannot clone dictionary size-check base.");
                }

                slotPtrTree = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, slotPtrTree,
                    gtNewIconNode(TYP_I_IMPL, runtimeLookup.offsets[i]));
            }
        }

        GenTree fastPathValue = gtNewIndir(TYP_I_IMPL,
            gtCloneExpr(slotPtrTree) ?? throw new FatalJitException("Cannot clone runtime lookup slot."),
            GTF_IND_NONFAULTING);
        var fastPathValueClone = fgMakeMultiUse(ref fastPathValue);
        var nullcheckOp = gtNewBinaryNode(GT_EQ, TYP_INT, fastPathValue, gtNewIconNode(TYP_I_IMPL, 0));
        nullcheckOp.Flags |= GTF_RELOP_JMP_USED;
        var nullcheckBb = fgNewBBFromTreeAfter(BBJ_COND, prevBb,
            gtNewUnaryNode(GT_JTRUE, TYP_VOID, nullcheckOp), debugInfo);

        var fallbackValueDef = gtNewStoreLclVarNode(rtLookupLcl.LclNum, call);
        var fallbackBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, nullcheckBb, fallbackValueDef, debugInfo, true);
        var fastPathValueDef = gtNewStoreLclVarNode(rtLookupLcl.LclNum, fastPathValueClone);
        var fastPathBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, nullcheckBb, fastPathValueDef, debugInfo);

        BasicBlock? sizeCheckBb = null;
        if (needsSizeCheck)
        {
            if (lastIndOfTree is null)
            {
                throw new FatalJitException("A dynamic runtime lookup requires a nonzero final offset.");
            }

            var sizeValueOffset = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, lastIndOfTree,
                gtNewIconNode(TYP_I_IMPL, runtimeLookup.sizeOffset));
            var sizeValue = gtNewIndir(TYP_I_IMPL, sizeValueOffset, GTF_IND_NONFAULTING);
            var offsetValue = gtNewIconNode(TYP_I_IMPL, runtimeLookup.offsets[runtimeLookup.indirections - 1]);
            var sizeCheck = gtNewBinaryNode(GT_LE, TYP_INT, sizeValue, offsetValue);
            sizeCheck.Flags |= GTF_RELOP_JMP_USED;
            sizeCheckBb = fgNewBBFromTreeAfter(BBJ_COND, prevBb,
                gtNewUnaryNode(GT_JTRUE, TYP_VOID, sizeCheck), debugInfo);
        }

        assert(prevBb.Kind is BBJ_ALWAYS);
        fastPathBb.TargetEdge = fgAddRefPred(block, fastPathBb);
        fallbackBb.TargetEdge = fgAddRefPred(block, fallbackBb);
        assert(fallbackBb.JumpsToNext);

        if (sizeCheckBb is not null)
        {
            fgRedirectEdge(ref prevBb.TargetEdgeRef, sizeCheckBb);
            var sizeTrueEdge = fgAddRefPred(fallbackBb, sizeCheckBb);
            var sizeFalseEdge = fgAddRefPred(nullcheckBb, sizeCheckBb);
            sizeCheckBb.SetCond(sizeTrueEdge, sizeFalseEdge);
            sizeTrueEdge.Likelihood = 0.2;
            sizeFalseEdge.Likelihood = 0.8;
        }
        else
        {
            fgRedirectEdge(ref prevBb.TargetEdgeRef, nullcheckBb);
        }

        var trueEdge = fgAddRefPred(fallbackBb, nullcheckBb);
        var falseEdge = fgAddRefPred(fastPathBb, nullcheckBb);
        nullcheckBb.SetCond(trueEdge, falseEdge);
        trueEdge.Likelihood = 0.2;
        falseEdge.Likelihood = 0.8;

        block.inheritWeight(prevBb);
        if (sizeCheckBb is not null)
        {
            sizeCheckBb.inheritWeight(prevBb);
            nullcheckBb.inheritWeightPercentage(sizeCheckBb, 80);
            fastPathBb.inheritWeightPercentage(nullcheckBb, 80);
            fallbackBb.inheritWeightPercentage(sizeCheckBb, 36);
        }
        else
        {
            nullcheckBb.inheritWeight(prevBb);
            fastPathBb.inheritWeightPercentage(nullcheckBb, 80);
            fallbackBb.inheritWeightPercentage(nullcheckBb, 20);
        }

        InheritFlags(nullcheckBb, prevBb);
        InheritFlags(fastPathBb, prevBb);
        InheritFlags(fallbackBb, prevBb);
        InheritFlags(block, prevBb);
        if (sizeCheckBb is not null)
        {
            InheritFlags(sizeCheckBb, prevBb);
        }

        assert(BasicBlock.sameEHRegion(prevBb, block));
        assert(BasicBlock.sameEHRegion(prevBb, nullcheckBb));
        assert(BasicBlock.sameEHRegion(prevBb, fastPathBb));
        if (sizeCheckBb is not null)
        {
            assert(BasicBlock.sameEHRegion(prevBb, sizeCheckBb));
        }

        return true;
    }
}
