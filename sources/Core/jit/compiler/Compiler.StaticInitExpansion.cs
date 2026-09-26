// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgExpandStaticInit()
    {
        if (!MethodHasStaticInit)
        {
            JITDUMP("Nothing to expand.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var isNativeAot = IsTargetAbi(CORINFO_NATIVEAOT_ABI);

        if (!isNativeAot && opts.OptimizationDisabled)
        {
            JITDUMP("Optimizations aren't allowed - bail out.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        return fgExpandHelper(fgExpandStaticInitForCall, skipRarelyRunBlocks: !isNativeAot);
    }

    private unsafe bool fgExpandStaticInitForCall(ref BasicBlock block, Statement stmt, GenTreeCall call)
    {
        if (!call.IsHelperCall())
        {
            return false;
        }

        if (!IsStaticHelperEligibleForExpansion(call, out var isGc, out var retValKind))
        {
            return false;
        }

        assert(!call.IsTailCall);

        if (call._initClsHnd == NO_CLASS_HANDLE)
        {
            assert(false, "helper call was created without gtInitClsHnd or already visited");
            return false;
        }

        var isInitOffset = 0;
        var flagAddr = default(CORINFO_CONST_LOOKUP);

        if (!info.compCompHnd->getIsClassInitedFlagAddress(call._initClsHnd, &flagAddr, &isInitOffset))
        {
            JITDUMP("getIsClassInitedFlagAddress returned false - bail out.\n");
            return false;
        }

        var staticBaseAddr = default(CORINFO_CONST_LOOKUP);

        if ((retValKind is SHRV_STATIC_BASE_PTR) &&
            !info.compCompHnd->getStaticBaseAddress(call._initClsHnd, isGc, &staticBaseAddr))
        {
            JITDUMP("getStaticBaseAddress returned false - bail out.\n");
            return false;
        }

#if DEBUG
        if (verbose)
        {
            JITDUMP($"Expanding static initialization for '{eeGetClassName(call._initClsHnd)}', call: [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}\n");
        }
#endif

        var debugInfo = stmt.DebugInfo;
        var prevBb = block;
        ref var callUse = ref fgSplitBlockBeforeTree(block, stmt, call, out var newFirstStmt, out block);

        while ((newFirstStmt is not null) && (newFirstStmt != stmt))
        {
            fgMorphStmtBlockOps(block, newFirstStmt);
            newFirstStmt = newFirstStmt.NextStmt;
        }

        assert(flagAddr.accessType is IAT_VALUE);

        GenTree? cachedStaticBase = null;
        GenTree isInitedActualValueNode;
        GenTree isInitedExpectedValue;

        if (IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            GenTree baseAddr = gtNewIconHandleNode((nint)flagAddr.addr, GTF_ICON_GLOBAL_PTR);

            if ((staticBaseAddr.addr == flagAddr.addr) && (staticBaseAddr.accessType == flagAddr.accessType))
            {
                cachedStaticBase = fgInsertCommaFormTemp(ref baseAddr);
            }

            // A relocatable base and its offset must remain separate nodes.
            var offsetNode = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, baseAddr, gtNewIconNode(TYP_I_IMPL, isInitOffset));
            isInitedActualValueNode = gtNewIndir(TYP_I_IMPL, offsetNode, GTF_IND_NONFAULTING | GTF_IND_VOLATILE);
            isInitedExpectedValue = gtNewIconNode(TYP_I_IMPL, 0);
        }
        else
        {
#pragma warning disable CA1508 // The EE writes this value through the pointer passed above.
            assert(isInitOffset == 0);
#pragma warning restore CA1508

            isInitedActualValueNode = gtNewIndOfIconHandleNode(TYP_INT, (nint)flagAddr.addr, GTF_ICON_GLOBAL_PTR);
            isInitedActualValueNode.Flags |= GTF_IND_VOLATILE;
            isInitedActualValueNode.HasOrderingSideEffect = true;
            isInitedActualValueNode = gtNewBinaryNode(GT_AND, TYP_INT, isInitedActualValueNode, gtNewIconNode(TYP_INT, 1));
            isInitedExpectedValue = gtNewIconNode(TYP_INT, 1);
        }

        var isInitedCmp = gtNewBinaryNode(GT_EQ, TYP_INT, isInitedActualValueNode, isInitedExpectedValue);
        isInitedCmp.Flags |= GTF_RELOP_JMP_USED;
        var isInitedBb = fgNewBBFromTreeAfter(BBJ_COND, prevBb, gtNewUnaryNode(GT_JTRUE, TYP_VOID, isInitedCmp), debugInfo);
        var helperCallBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, isInitedBb, call, debugInfo, true);

        GenTree? replacementNode = null;

        if (retValKind is SHRV_STATIC_BASE_PTR)
        {
            assert(staticBaseAddr.addr != null);

            if (cachedStaticBase is not null)
            {
                assert(staticBaseAddr.accessType is IAT_VALUE);
                replacementNode = cachedStaticBase;
            }
            else if (staticBaseAddr.accessType is IAT_VALUE)
            {
                replacementNode = gtNewIconHandleNode((nint)staticBaseAddr.addr, GTF_ICON_STATIC_HDL);
            }
            else
            {
                assert(staticBaseAddr.accessType is IAT_PVALUE);
                replacementNode = gtNewIndOfIconHandleNode(TYP_I_IMPL, (nint)staticBaseAddr.addr, GTF_ICON_GLOBAL_PTR);
            }
        }

        if (replacementNode is null)
        {
            if (callUse is not GenTree use)
            {
                throw new FatalJitException("Static initialization call has no owning use.");
            }

            use.BashToNOP();
        }
        else
        {
            callUse = replacementNode;
        }

        fgMorphStmtBlockOps(block, stmt);
        gtUpdateStmtSideEffects(stmt);

        fgRedirectEdge(ref prevBb.TargetEdgeRef, isInitedBb);
        assert(prevBb.JumpsToNext);

        var newEdge = fgAddRefPred(block, helperCallBb);
        helperCallBb.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);
        assert(helperCallBb.JumpsToNext);

        var trueEdge = fgAddRefPred(block, isInitedBb);
        var falseEdge = fgAddRefPred(helperCallBb, isInitedBb);
        isInitedBb.SetCond(trueEdge, falseEdge);
        trueEdge.Likelihood = 1.0;
        falseEdge.Likelihood = 0.0;

        block.inheritWeight(prevBb);
        isInitedBb.inheritWeight(prevBb);
        helperCallBb.inheritWeightPercentage(isInitedBb, 0);

        InheritFlags(block, prevBb);
        InheritFlags(isInitedBb, prevBb);
        InheritFlags(helperCallBb, prevBb);

        assert(BasicBlock.sameEHRegion(prevBb, block));
        assert(BasicBlock.sameEHRegion(prevBb, isInitedBb));

        if (fgCanCompactBlock(prevBb))
        {
            fgCompactBlock(prevBb);
        }

        call._initClsHnd = NO_CLASS_HANDLE;
        block.bbCodeOffsEnd = BAD_IL_OFFSET;

        return true;
    }
}
