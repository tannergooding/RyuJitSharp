// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private List<ContinuationMember>? _asyncContinuationMembers;

    public int GetContinuationMemberIndex(in ContinuationMember member)
    {
        var root = impInlineRoot;

        if (root._asyncContinuationMembers is null)
        {
            root._asyncContinuationMembers = [];
        }
        else
        {
            for (var i = 0; i < root._asyncContinuationMembers.Count; i++)
            {
                if (ContinuationMember.AreCompatible(member, root._asyncContinuationMembers[i]))
                {
                    return i;
                }
            }
        }

        root._asyncContinuationMembers.Add(member);
        return root._asyncContinuationMembers.Count - 1;
    }

    /// <summary>Look up an existing member without growing a table whose continuation layout may already be fixed.</summary>
    public bool TryGetContinuationMemberIndex(in ContinuationMember member, out int index)
    {
        var members = impInlineRoot._asyncContinuationMembers;

        if (members is not null)
        {
            for (var i = 0; i < members.Count; i++)
            {
                if (ContinuationMember.AreCompatible(member, members[i]))
                {
                    index = i;
                    return true;
                }
            }
        }

        index = 0;
        return false;
    }

    public int GetContinuationMemberCount() => impInlineRoot._asyncContinuationMembers?.Count ?? 0;

    public ContinuationMember GetContinuationMember(int index)
    {
        var members = impInlineRoot._asyncContinuationMembers;
        assert(members is not null);
        assert((uint)index < (uint)members.Count);
        return members[index];
    }

    public unsafe PhaseStatus SaveAsyncContexts()
    {
        if ((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) == 0)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert(fgFirstBB is not null);
        assert(fgLastBB is not null);
        assert(compInlineContext is not null);

        lvaResumedIndicator = lvaGrabTemp(false, "Async Resumed");
        lvaAsyncThreadObjectVar = lvaGrabTemp(false, "Async Thread");
        lvaAsyncExecutionContextVar = lvaGrabTemp(false, "Async ExecutionContext");
        lvaAsyncSynchronizationContextVar = lvaGrabTemp(false, "Async SynchronizationContext");

        ref var resumedDsc = ref lvaGetDesc(lvaResumedIndicator);
        ref var threadDsc = ref lvaGetDesc(lvaAsyncThreadObjectVar);
        ref var execCtxDsc = ref lvaGetDesc(lvaAsyncExecutionContextVar);
        ref var syncCtxDsc = ref lvaGetDesc(lvaAsyncSynchronizationContextVar);
        resumedDsc.Type = TYP_I_IMPL;
        threadDsc.Type = TYP_REF;
        execCtxDsc.Type = TYP_REF;
        syncCtxDsc.Type = TYP_REF;

        // These values are never read after resumption, including for inlined frames.
        resumedDsc.lvOnlyUsedOnSynchronousPath = true;
        threadDsc.lvOnlyUsedOnSynchronousPath = true;
        execCtxDsc.lvOnlyUsedOnSynchronousPath = true;
        syncCtxDsc.lvOnlyUsedOnSynchronousPath = true;

        if (opts.IsOSR)
        {
            resumedDsc.lvIsOSRLocal = true;
            threadDsc.lvIsOSRLocal = true;
            execCtxDsc.lvIsOSRLocal = true;
            syncCtxDsc.lvIsOSRLocal = true;
        }

        // Normal exits restore in a merged return block, so EH only needs a fault handler.
        var tryBegBB = fgSplitBlockAtBeginning(fgFirstBB);
        var tryLastBB = fgLastBB;
        var faultBB = fgNewBBafter(BBJ_EHFAULTRET, tryLastBB, extendRegion: false);
        faultBB.bbRefs = 1;
        faultBB.inheritWeightPercentage(tryBegBB, 0);

        var XTnew = compHndBBtabCount;
        var newEntryIndex = fgTryAddEHTableEntries(XTnew);

        if (newEntryIndex < 0)
        {
            IMPL_LIMITATION("too many exception clauses");
        }

        ref var newEntry = ref compHndBBtab[newEntryIndex];
        var root = impInlineRoot;
        asyncContextRestoreEHID = root.compEHID++;
        newEntry.ebdID = asyncContextRestoreEHID;
        newEntry.ebdHandlerType = EH_HANDLER_FAULT;
        root._asyncContextRestoreEHIDs ??= [];
        _ = root._asyncContextRestoreEHIDs.Add(asyncContextRestoreEHID);

        newEntry.ebdTryBeg = tryBegBB;
        newEntry.ebdTryLast = tryLastBB;
        newEntry.ebdHndBeg = faultBB;
        newEntry.ebdHndLast = faultBB;
        newEntry.ebdTyp = 0;
        newEntry.ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX;
        newEntry.ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX;
        newEntry._ebdTryBegOffset = tryBegBB.bbCodeOffs;
        newEntry._ebdTryEndOffset = tryLastBB.bbCodeOffsEnd;
        newEntry._ebdFilterBegOffset = 0;
        newEntry._ebdHndBegOffset = 0;
        newEntry._ebdHndEndOffset = 0;

        tryBegBB.SetFlags(BBF_DONT_REMOVE | BBF_IMPORTED);
        faultBB.SetFlags(BBF_DONT_REMOVE | BBF_IMPORTED);
        faultBB.CatchType = BBCT_FAULT;
        tryBegBB.TryIndex = XTnew;
        tryBegBB.clearHndIndex();
        faultBB.clearTryIndex();
        faultBB.HndIndex = XTnew;

        for (var tmpBB = tryBegBB.Next; tmpBB != faultBB; tmpBB = tmpBB.Next)
        {
            assert(tmpBB is not null);

            if (!tmpBB.hasTryIndex)
            {
                tmpBB.TryIndex = XTnew;
            }
        }

        for (var XTnum = 0; XTnum < XTnew; XTnum++)
        {
            ref var HBtab = ref compHndBBtab[XTnum];

            if (HBtab.ebdEnclosingTryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
            {
                HBtab.ebdEnclosingTryIndex = XTnew;
            }
        }

        JITDUMP($"Created EH descriptor EH#{XTnew} for try/fault wrapping body to save/restore async contexts\n");
#if DEBUG
        fgVerifyHandlerTab();
#endif

        ref var asyncInfo = ref eeGetAsyncInfo();

        // Keep capture outside the try so it does not prevent removal of an otherwise unnecessary region.
        // An OSR entry reuses the contexts captured by tier 0.
        if (!opts.IsOSR)
        {
            var captureCall = gtNewUserCallNode(TYP_VOID, asyncInfo.captureContextsMethHnd);
            _ = captureCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclAddrNode(TYP_BYREF, lvaAsyncSynchronizationContextVar, 0)));
            _ = captureCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclAddrNode(TYP_BYREF, lvaAsyncExecutionContextVar, 0)));
            _ = captureCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclAddrNode(TYP_BYREF, lvaAsyncThreadObjectVar, 0)));
            lvaGetDesc(lvaAsyncThreadObjectVar).lvHasLdAddrOp = true;
            lvaGetDesc(lvaAsyncExecutionContextVar).lvHasLdAddrOp = true;
            lvaGetDesc(lvaAsyncSynchronizationContextVar).lvHasLdAddrOp = true;

            CORINFO_CALL_INFO callInfo = default;
            callInfo.hMethod = captureCall._callMethHnd;
            callInfo.methodFlags = info.compCompHnd->getMethodAttribs(callInfo.hMethod);
            impMarkInlineCandidate(captureCall, MAKE_METHODCONTEXT(callInfo.hMethod), callInfo, compInlineContext);

            var captureStmt = gtNewStmt(captureCall);
            fgInsertStmtAtBeg(fgFirstBB, captureStmt);
            JITDUMP("Inserted capture\n");
            DISPSTMT(captureStmt);

            var containingBlock = compIsForInlining ? impInlineInfo.iciBlock : fgFirstBB;
            assert(containingBlock is not null);
            var inALoop = containingBlock.HasFlag(BBF_BACKWARD_JUMP);
            var isReturn = containingBlock.Kind is BBJ_RETURN;

            if ((inALoop && !isReturn) || !root.info.compInitMem)
            {
                var storeIndicator = gtNewStoreLclVarNode(lvaResumedIndicator, gtNewIconNode(TYP_I_IMPL, 0));
                var storeIndicatorStmt = gtNewStmt(storeIndicator);
                fgInsertStmtAtBeg(fgFirstBB, storeIndicatorStmt);
                JITDUMP("Inserted resumed indicator initialization\n");
                DISPSTMT(storeIndicatorStmt);
            }
            else
            {
                JITDUMP("Skipping zero init of resumed indicator due to compInitMem\n");
            }
        }

        var restoreCall = gtNewUserCallNode(TYP_VOID, asyncInfo.restoreContextsMethHnd);
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_REF, lvaAsyncSynchronizationContextVar)));
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_REF, lvaAsyncExecutionContextVar)));
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_REF, lvaAsyncThreadObjectVar)));
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_INT, lvaResumedIndicator)));
        fgInsertStmtAtEnd(faultBB, gtNewStmt(restoreCall));

        BasicBlock? newReturnBB = null;
        var mergedReturnLcl = BAD_VAR_NUM;

        foreach (var block in Blocks)
        {
            AddContextArgsToAsyncCalls(block);

            if ((block.Kind is not BBJ_RETURN) || (block == newReturnBB))
            {
                continue;
            }

            JITDUMP($"Merging BBJ_RETURN block {FMT_BB(block.bbNum)}\n");

            if (newReturnBB is null)
            {
                newReturnBB = CreateReturnBB(out mergedReturnLcl);
                newReturnBB.inheritWeightPercentage(block, 0);
            }

            // Inline return values are merged during import.
            if (!compIsForInlining)
            {
                var retStmt = block.LastStmt;
                assert((retStmt is not null) && (retStmt.RootNode.Oper is GT_RETURN));

                if (mergedReturnLcl != BAD_VAR_NUM)
                {
                    var retVal = retStmt.RootNode.AsUnOp().Op1;
                    var insertAfter = retStmt;
                    var storeRetVal = gtNewTempStore(mergedReturnLcl, retVal, ref insertAfter, CHECK_SPILL_NONE, retStmt.DebugInfo, block);
                    var storeStmt = gtNewStmt(storeRetVal);
                    fgInsertStmtAtEnd(block, storeStmt);
                    JITDUMP("Inserted store to common return local\n");
                    DISPSTMT(storeStmt);
                }

                retStmt.RootNode.BashToNOP();
            }

            block.SetKindAndTargetEdge(BBJ_ALWAYS, fgAddRefPred(newReturnBB, block));
            fgReturnCount--;
        }

        newReturnBB?.bbWeight = newReturnBB.computeIncomingWeight();
        assert(fgReturnCount <= 1);

        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    private unsafe BasicBlock CreateReturnBB(out int mergedReturnLcl)
    {
        assert(fgLastBB is not null);
        assert(compInlineContext is not null);
        var newReturnBB = fgNewBBafter(BBJ_RETURN, fgLastBB, extendRegion: false);
        newReturnBB.bbTryIndex = 0;
        newReturnBB.bbHndIndex = 0;
        fgReturnCount++;
        JITDUMP($"Created new BBJ_RETURN block {FMT_BB(newReturnBB.bbNum)}\n");

        ref var asyncInfo = ref eeGetAsyncInfo();
        var restoreCall = gtNewUserCallNode(TYP_VOID, asyncInfo.restoreContextsMethHnd);
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_REF, lvaAsyncSynchronizationContextVar)));
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_REF, lvaAsyncExecutionContextVar)));
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_REF, lvaAsyncThreadObjectVar)));
        _ = restoreCall.Args.PushFront(NewCallArg.CreateForPrimitive(gtNewLclVarNode(TYP_INT, lvaResumedIndicator)));

        // Unlike the fault restore, the normal-exit restore is an inline candidate.
        CORINFO_CALL_INFO callInfo = default;
        callInfo.hMethod = restoreCall._callMethHnd;
        callInfo.methodFlags = info.compCompHnd->getMethodAttribs(callInfo.hMethod);
        impMarkInlineCandidate(restoreCall, MAKE_METHODCONTEXT(callInfo.hMethod), callInfo, compInlineContext);

        var restoreStmt = gtNewStmt(restoreCall);
        fgInsertStmtAtEnd(newReturnBB, restoreStmt);
        JITDUMP("Inserted restore statement in return block\n");
        DISPSTMT(restoreStmt);

        mergedReturnLcl = BAD_VAR_NUM;

        if (!compIsForInlining)
        {
            GenTree ret;

            if (compMethodHasRetVal)
            {
                mergedReturnLcl = lvaGrabTemp(false, "Async merged return local");
                var retLclType = compMethodReturnsRetBufAddr ? TYP_BYREF : info.compRetType.ActualType;

                if (varTypeIsStruct(retLclType))
                {
                    lvaSetStruct(mergedReturnLcl, info.compMethodInfo->args.retTypeClass, false);

                    if (compMethodReturnsMultiRegRetType)
                    {
                        lvaGetDesc(mergedReturnLcl).lvIsMultiRegRet = true;
                    }
                }
                else
                {
                    lvaGetDesc(mergedReturnLcl).Type = retLclType;
                }

                var retTemp = gtNewLclVarNode(TYP_UNDEF, mergedReturnLcl);
                ret = new GenTreeUnOp(GT_RETURN, retTemp.Type, retTemp);
            }
            else
            {
                ret = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            }

            var retStmt = gtNewStmt(ret);
            fgInsertStmtAtEnd(newReturnBB, retStmt);
            JITDUMP("Inserted return statement in return block\n");
            DISPSTMT(retStmt);
        }

        return newReturnBB;
    }

    /// <summary>Add uses of the saved contexts to async calls, modelling their restoration on suspension.</summary>
    public void AddContextArgsToAsyncCalls(BasicBlock block)
    {
        var visitor = new AddAsyncContextArgsVisitor(this);

        foreach (var statement in block.Statements)
        {
            _ = visitor.WalkTree(ref statement.RootNodeRef, null);
        }
    }

    private struct AddAsyncContextArgsVisitor : IGenTreeVisitor<AddAsyncContextArgsVisitor>
    {
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;

        public AddAsyncContextArgsVisitor(Compiler compiler)
        {
            _compiler = compiler;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;

            if ((tree.Flags & GTF_CALL) == 0)
            {
                return WALK_SKIP_SUBTREES;
            }

            if (!tree.Oper.IsCall || !tree.AsCall().IsAsync)
            {
                return WALK_CONTINUE;
            }

            var call = tree.AsCall();
            _compiler.compAsyncBodyMaySuspend = true;

            if (call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse) is not null)
            {
                assert(_compiler.compIsForInlining);
                return WALK_CONTINUE;
            }

            if (_compiler.compIsForInlining && !generalAsyncInliningEnabled())
            {
                return WALK_CONTINUE;
            }

            var resumed = _compiler.gtNewLclVarNode(TYP_INT, _compiler.lvaResumedIndicator);
            var resumedAddr = _compiler.gtNewLclAddrNode(TYP_BYREF, _compiler.lvaResumedIndicator, 0);
            var execCtx = _compiler.gtNewLclVarNode(TYP_REF, _compiler.lvaAsyncExecutionContextVar);
            var syncCtx = _compiler.gtNewLclVarNode(TYP_REF, _compiler.lvaAsyncSynchronizationContextVar);
#if DEBUG
            JITDUMP($"Adding resumed use [{resumed.TreeId:D6}], resumed def [{resumedAddr.TreeId:D6}] exec context [{execCtx.TreeId:D6}], sync context [{syncCtx.TreeId:D6}] to async call [{call.TreeId:D6}]\n");
#endif

            var resumedDefArg = NewCallArg.CreateForPrimitive(resumedAddr).WithWellKnownArg(WellKnownArg.AsyncResumedDef);
            var resumedUseArg = NewCallArg.CreateForPrimitive(resumed).WithWellKnownArg(WellKnownArg.AsyncResumedUse);
            var execCtxArg = NewCallArg.CreateForPrimitive(execCtx).WithWellKnownArg(WellKnownArg.AsyncExecutionContext);
            var syncCtxArg = NewCallArg.CreateForPrimitive(syncCtx).WithWellKnownArg(WellKnownArg.AsyncSynchronizationContext);

            // Keep the single def outside the per-frame (resumed, exec, sync) triples.
            var insertAfter = call.Args.PushFront(resumedDefArg);
            insertAfter = call.Args.InsertAfter(insertAfter, resumedUseArg);
            insertAfter = call.Args.InsertAfter(insertAfter, execCtxArg);
            insertAfter = call.Args.InsertAfter(insertAfter, syncCtxArg);
            _compiler.lvaGetDesc(_compiler.lvaResumedIndicator).lvHasLdAddrOp = true;

            if (!_compiler.compIsForInlining)
            {
                return WALK_CONTINUE;
            }

            // The inlining call's values describe every enclosing frame, innermost first.
            // Suspension lowering later uses these pseudo-args to hand contexts back through
            // frames that have not resumed, as though their physical frames had returned.
            var inlCall = _compiler.impInlineInfo.iciCall;
            assert(inlCall is not null);
            var numCopied = 0;

            foreach (var arg in inlCall.Args.Args)
            {
                var kind = arg.WellKnownArg;

                if (kind is not (WellKnownArg.AsyncResumedUse or WellKnownArg.AsyncExecutionContext or WellKnownArg.AsyncSynchronizationContext))
                {
                    continue;
                }

                var newArg = NewCallArg.CreateForPrimitive(_compiler.gtCloneExpr(arg.Node)).WithWellKnownArg(kind);
                insertAfter = call.Args.InsertAfter(insertAfter, newArg);
                numCopied++;
            }

            assert((numCopied % 3) == 0);

            if (numCopied == 0)
            {
#if DEBUG
                JITDUMP($"Inlining call [{inlCall.TreeId:D6}] has no context args; inlinee has no enclosing async frame\n");

#endif
                return WALK_CONTINUE;
            }

            List<ContinuationContextHandling> handling = [inlCall.GetAsyncInfo().ContinuationContextHandling];

            if (inlCall.GetAsyncInfo().InlineFrameContextHandling is List<ContinuationContextHandling> outerHandling)
            {
                foreach (var outer in outerHandling)
                {
                    handling.Add(outer);
                }
            }

            call.GetAsyncInfo().InlineFrameContextHandling = handling;
            assert(handling.Count == (numCopied / 3));

#if DEBUG
            JITDUMP($"Extended async call [{call.TreeId:D6}] to {(numCopied / 3) + 1} frames in chain\n");

#endif
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<AddAsyncContextArgsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
