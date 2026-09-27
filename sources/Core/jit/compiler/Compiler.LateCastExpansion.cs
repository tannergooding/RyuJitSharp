// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoFlag;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private enum TypeCheckFailedAction
    {
        Unknown,
        ReturnNull,
        CallHelper,
        CallHelper_Specialized,
        CallHelper_AlwaysThrows,
    }

    private enum TypeCheckPassedAction
    {
        Unknown,
        ReturnObj,
        ReturnNull,
        CallHelper_AlwaysThrows,
    }

    public PhaseStatus fgLateCastExpansion()
    {
        if (!MethodHasExpandableCasts || opts.OptimizationDisabled)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        return fgExpandHelper(fgLateCastExpansionForCall, skipRarelyRunBlocks: true);
    }

    private unsafe int PickCandidatesForTypeCheck(GenTreeCall castHelper, Span<nint> candidates,
        out CORINFO_CLASS_HANDLE commonCls, Span<int> likelihoods,
        out TypeCheckFailedAction typeCheckFailed, out TypeCheckPassedAction typeCheckPassed)
    {
        commonCls = NO_CLASS_HANDLE;
        typeCheckFailed = TypeCheckFailedAction.Unknown;
        typeCheckPassed = TypeCheckPassedAction.Unknown;

        if (!castHelper.IsHelperCall() || ((castHelper._callMoreFlags & GTF_CALL_M_CAST_CAN_BE_EXPANDED) == 0))
        {
            return 0;
        }

        assert(!castHelper.IsTailCall);
        bool isCastClass;
        var helper = castHelper.HelperNum;

        switch (helper)
        {
            case CORINFO_HELP_CHKCASTARRAY:
            case CORINFO_HELP_CHKCASTANY:
            case CORINFO_HELP_CHKCASTINTERFACE:
            case CORINFO_HELP_CHKCASTCLASS:
            {
                isCastClass = true;
                break;
            }

            case CORINFO_HELP_ISINSTANCEOFARRAY:
            case CORINFO_HELP_ISINSTANCEOFCLASS:
            case CORINFO_HELP_ISINSTANCEOFANY:
            case CORINFO_HELP_ISINSTANCEOFINTERFACE:
            {
                isCastClass = false;
                break;
            }

            default:
            {
                return 0;
            }
        }

        var clsArgument = castHelper.Args.GetUserArgByIndex(0);
        var objArgument = castHelper.Args.GetUserArgByIndex(1);
        assert(clsArgument is not null && objArgument is not null);
        var clsArg = clsArgument.Node;
        var objArg = objArgument.Node;
        var castToCls = gtGetHelperArgClassHandle(clsArg);

        if (castToCls == NO_CLASS_HANDLE)
        {
            switch (helper)
            {
                case CORINFO_HELP_CHKCASTCLASS:
                case CORINFO_HELP_CHKCASTARRAY:
                case CORINFO_HELP_CHKCASTANY:
                {
                    typeCheckFailed = helper is CORINFO_HELP_CHKCASTCLASS
                        ? TypeCheckFailedAction.CallHelper_Specialized : TypeCheckFailedAction.CallHelper;
                    likelihoods[0] = 50;
                    candidates[0] = 0;
                    return 1;
                }

                default:
                {
                    return 0;
                }
            }
        }

        if (((objArg.Flags & GTF_ALL_EFFECT) != 0) && lvaHaveManyLocals())
        {
            JITDUMP("lvaHaveManyLocals() is true and objArg has side effects - bail out.");
            return 0;
        }

        typeCheckFailed = TypeCheckFailedAction.CallHelper;
        typeCheckPassed = TypeCheckPassedAction.ReturnObj;
        commonCls = castToCls;
        const CorInfoFlag isAbstractFlags = CORINFO_FLG_INTERFACE | CORINFO_FLG_ABSTRACT;

        var fromClass = gtGetClassHandle(objArg, out var fromClassIsExact, out var fromClassIsNonNull);

        if ((fromClass != NO_CLASS_HANDLE) && fromClassIsExact)
        {
            if (fromClassIsNonNull)
            {
                castHelper._callMoreFlags |= GTF_CALL_M_CAST_OBJ_NONNULL;
            }

            var castResult = info.compCompHnd->compareTypesForCast(fromClass, castToCls);
            if (isCastClass && (castResult is TypeCompareState.MustNot))
            {
                typeCheckPassed = TypeCheckPassedAction.CallHelper_AlwaysThrows;
                return 0;
            }
        }

        var isCastToExact = info.compCompHnd->isExactType(castToCls);
        if (isCastToExact && (helper is CORINFO_HELP_CHKCASTCLASS or CORINFO_HELP_CHKCASTARRAY))
        {
            // Exactness excludes array covariance and primitive-array interchange.
            typeCheckFailed = TypeCheckFailedAction.CallHelper_AlwaysThrows;
            likelihoods[0] = 100;
            commonCls = castToCls;
            candidates[0] = (nint)castToCls;
            return 1;
        }

        if (isCastToExact && (helper is CORINFO_HELP_ISINSTANCEOFARRAY or CORINFO_HELP_ISINSTANCEOFCLASS))
        {
            typeCheckFailed = TypeCheckFailedAction.ReturnNull;
            candidates[0] = (nint)castToCls;
            likelihoods[0] = 50;
            return 1;
        }

        var maxTypeChecks = int.Min(GetGdvMaxTypeChecks(), MAX_GDV_TYPE_CHECKS);
        Span<nint> exactClasses = stackalloc nint[MAX_GDV_TYPE_CHECKS];
        exactClasses.Clear();
        int numExactClasses;

        fixed (nint* exactClassesPtr = exactClasses)
        {
            numExactClasses = info.compCompHnd->getExactClasses(castToCls, maxTypeChecks,
                (CORINFO_CLASS_HANDLE*)exactClassesPtr);
        }

        var allTrulyExact = true;
        for (var i = 0; i < numExactClasses; i++)
        {
            if (!info.compCompHnd->isExactType((CORINFO_CLASS_HANDLE)exactClasses[i]))
            {
                allTrulyExact = false;
                break;
            }
        }

        if ((numExactClasses > 0) && allTrulyExact)
        {
            // Preserve native integer rounding: three candidates leave a 1% throwing fallback.
            for (var i = 0; i < numExactClasses; i++)
            {
                likelihoods[i] = 100 / (numExactClasses + 1);
            }

            exactClasses[..numExactClasses].CopyTo(candidates);
            if (helper is CORINFO_HELP_CHKCASTINTERFACE or CORINFO_HELP_CHKCASTCLASS)
            {
                typeCheckFailed = TypeCheckFailedAction.CallHelper_AlwaysThrows;
                for (var i = 0; i < numExactClasses; i++)
                {
                    likelihoods[i] = 100 / numExactClasses;
                }

                if (numExactClasses == 1)
                {
                    commonCls = (CORINFO_CLASS_HANDLE)exactClasses[0];
                }
            }
            else if (helper is CORINFO_HELP_ISINSTANCEOFINTERFACE or CORINFO_HELP_ISINSTANCEOFCLASS)
            {
                typeCheckFailed = TypeCheckFailedAction.ReturnNull;
                if (numExactClasses == 1)
                {
                    commonCls = (CORINFO_CLASS_HANDLE)exactClasses[0];
                }
            }

            return numExactClasses;
        }

        Span<nint> likelyClasses = stackalloc nint[MAX_GDV_TYPE_CHECKS];
        Span<int> likelyLikelihoods = stackalloc int[MAX_GDV_TYPE_CHECKS];
        likelyClasses.Clear();
        likelyLikelihoods.Clear();
        pickGDV(castHelper, castHelper._castHelperILOffset, false, likelyClasses, [],
            out var likelyClassCount, likelyLikelihoods);

        if (likelyClassCount > 0)
        {
            // Native deliberately uses only the first profile candidate.
            likelihoods[0] = likelyLikelihoods[0];
            candidates[0] = likelyClasses[0];

            if ((likelyClasses[0] == 0) ||
                ((info.compCompHnd->getClassAttribs((CORINFO_CLASS_HANDLE)likelyClasses[0]) & isAbstractFlags) != 0))
            {
                return 0;
            }

            var castResult = info.compCompHnd->compareTypesForCast((CORINFO_CLASS_HANDLE)candidates[0], castToCls);
            if (castResult is TypeCompareState.May)
            {
                JITDUMP("compareTypesForCast returned May for this candidate\n");
                return 0;
            }

            if (castResult is TypeCompareState.Must)
            {
                // The specialized helper skips exact-target checks, so a subclass guard is insufficient.
                if ((helper is CORINFO_HELP_CHKCASTCLASS) && (candidates[0] == (nint)castToCls))
                {
                    typeCheckFailed = TypeCheckFailedAction.CallHelper_Specialized;
                }

                typeCheckPassed = TypeCheckPassedAction.ReturnObj;
                return 1;
            }

            if (castResult is TypeCompareState.MustNot)
            {
                if (!isCastClass)
                {
                    typeCheckPassed = TypeCheckPassedAction.ReturnNull;
                    return 1;
                }

                return 0;
            }
        }

        switch (helper)
        {
            case CORINFO_HELP_CHKCASTARRAY:
            case CORINFO_HELP_CHKCASTCLASS:
            case CORINFO_HELP_CHKCASTANY:
            {
                if ((info.compCompHnd->getClassAttribs(castToCls) & isAbstractFlags) != 0)
                {
                    return 0;
                }

                candidates[0] = (nint)castToCls;
                likelihoods[0] = 50;
                if (helper is CORINFO_HELP_CHKCASTCLASS)
                {
                    typeCheckFailed = TypeCheckFailedAction.CallHelper_Specialized;
                }

                return 1;
            }

            case CORINFO_HELP_CHKCASTINTERFACE:
            case CORINFO_HELP_ISINSTANCEOFINTERFACE:
            case CORINFO_HELP_ISINSTANCEOFARRAY:
            case CORINFO_HELP_ISINSTANCEOFCLASS:
            case CORINFO_HELP_ISINSTANCEOFANY:
            {
                return 0;
            }

            default:
            {
                unreached();
                return 0;
            }
        }
    }

    private unsafe bool fgLateCastExpansionForCall(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        Span<nint> expectedExactClasses = stackalloc nint[MAX_GDV_TYPE_CHECKS];
        Span<int> likelihoods = stackalloc int[MAX_GDV_TYPE_CHECKS];
        expectedExactClasses.Clear();
        likelihoods.Clear();

        var numOfCandidates = PickCandidatesForTypeCheck(call, expectedExactClasses, out _, likelihoods,
            out var typeCheckFailedAction, out var typeCheckPassedAction);
        if ((numOfCandidates == 0) && (typeCheckPassedAction is not TypeCheckPassedAction.CallHelper_AlwaysThrows))
        {
            return false;
        }

        JITDUMP($"Expanding cast helper call in {FMT_BB(block.bbNum)}...\n");
        DISPTREE(call);
        JITDUMP("\n");

        var debugInfo = stmt.DebugInfo;
        var tmpNum = SplitAtTreeAndReplaceItWithLocal(block, stmt, call, out var firstBb, out var lastBb);
        // The result initially holds the uncast object, so commonCls cannot describe this temp yet.
        var tmpNode = gtNewLclvNode(call.Type, tmpNum);
        block = lastBb;

        var nullcheckOp = gtNewBinaryNode(GT_EQ, TYP_INT, tmpNode, gtNewNull());
        nullcheckOp.Flags |= GTF_RELOP_JMP_USED;
        var nullcheckBb = fgNewBBFromTreeAfter(BBJ_COND, firstBb,
            gtNewUnaryNode(GT_JTRUE, TYP_VOID, nullcheckOp), debugInfo, true);

        var clsArgument = call.Args.GetUserArgByIndex(0);
        var objArgument = call.Args.GetUserArgByIndex(1);
        assert(clsArgument is not null && objArgument is not null);
        var originalObj = gtCloneExpr(objArgument.Node);
        var assignTmp = gtNewStmt(gtNewTempStore(tmpNum, originalObj), debugInfo);
        fgInsertStmtAtBeg(nullcheckBb, assignTmp);
        gtSetStmtInfo(assignTmp);
        fgSetStmtSeq(assignTmp);

        var typeChecksBbs = new BasicBlock[numOfCandidates];
        var lastTypeCheckBb = nullcheckBb;
        for (var candidateId = 0; candidateId < numOfCandidates; candidateId++)
        {
            var expectedCls = (CORINFO_CLASS_HANDLE)expectedExactClasses[candidateId];
            var expectedClsNode = expectedCls != NO_CLASS_HANDLE
                ? gtNewIconEmbClsHndNode(expectedCls) : gtCloneExpr(clsArgument.Node);

            GenTree? storeCseVal = null;
            if (candidateId == 0)
            {
                ref var castArg = ref clsArgument.NodeRef;
                if (GenTree.Compare(castArg, expectedClsNode))
                {
                    var clsTmp = lvaGrabTemp(true, "CSE for expectedClsNode");
                    storeCseVal = gtNewTempStore(clsTmp, expectedClsNode);
                    expectedClsNode = gtNewLclvNode(TYP_I_IMPL, clsTmp);
                    castArg = gtNewLclvNode(TYP_I_IMPL, clsTmp);
                }
            }

            var mtCheck = gtNewBinaryNode(GT_EQ, TYP_INT, gtNewMethodTableLookup(gtCloneExpr(tmpNode)), expectedClsNode);
            mtCheck.Flags |= GTF_RELOP_JMP_USED;
            var jtrue = gtNewUnaryNode(GT_JTRUE, TYP_VOID, mtCheck);
            typeChecksBbs[candidateId] = fgNewBBFromTreeAfter(BBJ_COND, lastTypeCheckBb, jtrue, debugInfo, true);
            lastTypeCheckBb = typeChecksBbs[candidateId];

            if (storeCseVal is not null)
            {
                var clsStmt = gtNewStmt(storeCseVal, debugInfo);
                fgInsertStmtAtBeg(typeChecksBbs[0], clsStmt);
                gtSetStmtInfo(clsStmt);
                fgSetStmtSeq(clsStmt);
            }
        }

        var typeCheckNotNeeded = numOfCandidates == 0;
        BasicBlock fallbackBb;
        if (typeCheckNotNeeded || (typeCheckFailedAction is TypeCheckFailedAction.CallHelper_AlwaysThrows))
        {
            setCallDoesNotReturn(call);
            fallbackBb = fgNewBBFromTreeAfter(BBJ_THROW, lastTypeCheckBb, call, debugInfo, true);
        }
        else if (typeCheckFailedAction is TypeCheckFailedAction.ReturnNull)
        {
            var fallbackTree = gtNewTempStore(tmpNum, gtNewNull());
            fallbackBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, lastTypeCheckBb, fallbackTree, debugInfo, true);
        }
        else
        {
            if (typeCheckFailedAction is TypeCheckFailedAction.CallHelper_Specialized)
            {
                call._callMethHnd = eeFindHelper(CORINFO_HELP_CHKCASTCLASS_SPECIAL);
            }

            var fallbackTree = gtNewTempStore(tmpNum, call);
            fallbackBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, lastTypeCheckBb, fallbackTree, debugInfo, true);
        }

        var typeCheckSucceedTree = typeCheckPassedAction is TypeCheckPassedAction.ReturnNull
            ? gtNewTempStore(tmpNum, gtNewNull()) : gtNewNothingNode();

        const double nullcheckTrueLikelihood = 0.5;
        const double nullcheckFalseLikelihood = 0.5;
        BasicBlock? typeCheckSucceedBb;
        var nullTrueEdge = fgAddRefPred(lastBb, nullcheckBb);
        nullcheckBb.TrueEdge = nullTrueEdge;
        nullTrueEdge.Likelihood = nullcheckTrueLikelihood;
        nullcheckBb.inheritWeight(firstBb);

        if (typeCheckNotNeeded)
        {
            var falseEdge = fgAddRefPred(fallbackBb, nullcheckBb);
            nullcheckBb.FalseEdge = falseEdge;
            falseEdge.Likelihood = nullcheckFalseLikelihood;
            fallbackBb.inheritWeight(nullcheckBb);
            fallbackBb.scaleBBWeight(nullcheckFalseLikelihood);
            lastBb.inheritWeight(nullcheckBb);
            lastBb.scaleBBWeight(nullcheckTrueLikelihood);
            typeCheckSucceedBb = null;
        }
        else
        {
            var falseEdge = fgAddRefPred(typeChecksBbs[0], nullcheckBb);
            nullcheckBb.FalseEdge = falseEdge;
            falseEdge.Likelihood = nullcheckFalseLikelihood;
            typeCheckSucceedBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, fallbackBb, typeCheckSucceedTree, debugInfo);
            typeCheckSucceedBb.TargetEdge = fgAddRefPred(lastBb, typeCheckSucceedBb);
        }

        for (var candidateId = 0; candidateId < numOfCandidates; candidateId++)
        {
            assert(typeCheckSucceedBb is not null);
            var curTypeCheckBb = typeChecksBbs[candidateId];
            curTypeCheckBb.TrueEdge = fgAddRefPred(typeCheckSucceedBb, curTypeCheckBb);
            var falseTarget = candidateId == numOfCandidates - 1 ? fallbackBb : typeChecksBbs[candidateId + 1];
            curTypeCheckBb.FalseEdge = fgAddRefPred(falseTarget, curTypeCheckBb);
        }

        fgRedirectEdge(ref firstBb.TargetEdgeRef, nullcheckBb);

        // Condition each guess on all earlier failures: p(current) / (1 - sum(previous)).
        // Keep this order and double arithmetic identical to native multi-guess GDV.
        double sumOfPreviousLikelihood = 0;
        for (var candidateId = 0; candidateId < numOfCandidates; candidateId++)
        {
            var curTypeCheckBb = typeChecksBbs[candidateId];
            var predecessor = candidateId == 0 ? nullcheckBb : typeChecksBbs[candidateId - 1];
            curTypeCheckBb.inheritWeight(predecessor);
            curTypeCheckBb.scaleBBWeight(predecessor.FalseEdge.Likelihood);

            var likelihood = (double)likelihoods[candidateId] / 100;
            var relLikelihood = likelihood / (1.0 - sumOfPreviousLikelihood);
            JITDUMP($"Candidate {candidateId}: likelihood {FMT_WT(likelihood)} relative likelihood {FMT_WT(relLikelihood)}\n");

            curTypeCheckBb.TrueEdge.Likelihood = relLikelihood;
            curTypeCheckBb.FalseEdge.Likelihood = 1.0 - relLikelihood;
            sumOfPreviousLikelihood += likelihood;
        }

        fallbackBb.inheritWeight(lastTypeCheckBb);
        fallbackBb.scaleBBWeight(lastTypeCheckBb.FalseEdge.Likelihood);
        if (fallbackBb.Kind is BBJ_ALWAYS)
        {
            fallbackBb.TargetEdge = fgAddRefPred(lastBb, fallbackBb);
        }

        if (!typeCheckNotNeeded)
        {
            assert(typeCheckSucceedBb is not null);
            typeCheckSucceedBb.inheritWeight(typeChecksBbs[0]);
            typeCheckSucceedBb.scaleBBWeight(sumOfPreviousLikelihood);
            lastBb.inheritWeight(firstBb);
        }

        assert(BasicBlock.sameEHRegion(firstBb, lastBb));
        assert(BasicBlock.sameEHRegion(firstBb, nullcheckBb));
        assert(BasicBlock.sameEHRegion(firstBb, fallbackBb));

        if ((call._callMoreFlags & GTF_CALL_M_CAST_OBJ_NONNULL) != 0)
        {
            var nullcheckStmt = nullcheckBb.LastStmt;
            assert(nullcheckStmt is not null);
            fgRemoveStmt(nullcheckBb, nullcheckStmt);
            var removedEdge = nullcheckBb.TrueEdge;
            fgRemoveRefPred(removedEdge);
            nullcheckBb.SetKindAndTargetEdge(BBJ_ALWAYS, nullcheckBb.FalseEdge);

            if (nullcheckBb.hasProfileWeight)
            {
                var removedTarget = removedEdge.DestinationBlock;
                if (removedTarget.bbPreds is null)
                {
                    JITDUMP($"fgLateCastExpansionForCall: {FMT_BB(removedTarget.bbNum)} is unreachable, " +
                        $"and will be removed later. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                    fgPgoConsistent = false;
                }

                assert(nullcheckBb.Next is not null);
                foreach (var successor in new BasicBlockRangeList(nullcheckBb.Next, lastBb))
                {
                    successor.setBBProfileWeight(successor.computeIncomingWeight());
                }
            }
        }

        if ((fallbackBb.Kind is BBJ_THROW) && (fallbackBb.bbWeight != BB_ZERO_WEIGHT))
        {
            JITDUMP($"fgLateCastExpansionForCall: fallback {FMT_BB(fallbackBb.bbNum)} throws and has flow into it. " +
                $"Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            fgPgoConsistent = false;
        }

        if (fgCanCompactBlock(firstBb))
        {
            fgCompactBlock(firstBb);
        }

        return true;
    }
}
