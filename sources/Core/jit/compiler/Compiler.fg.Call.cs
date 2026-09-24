// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    private unsafe GenTree? fgMorphPotentialTailCall(GenTreeCall call)
    {
        assert(call.IsTailPrefixedCall ^ call.IsImplicitTailCall);
        assert(!call.IsInlineCandidate);

        void FailTailCall(string reason, int localNumber = BAD_VAR_NUM)
        {
#if DEBUG
            if (verbose)
            {
                jitprintf("\nRejecting tail call in morph for call ");
                printTreeId(call);
                jitprintf($": {reason}");
                if (localNumber != BAD_VAR_NUM)
                {
                    jitprintf($" V{localNumber:D2}");
                }
                jitprintf("\n");
            }
#endif
            var reasonUtf8 = Encoding.UTF8.GetBytes(reason + '\0');
            fixed (byte* pointer = reasonUtf8)
            {
                info.compCompHnd->reportTailCallDecision(null,
                    call._callType is CT_USER_FUNC ? call._callMethHnd : null,
                    call.IsTailPrefixedCall, TAILCALL_FAIL, pointer);
            }

            call._callMoreFlags &= ~GTF_CALL_M_EXPLICIT_TAILCALL;
#if FEATURE_TAILCALL_OPT
            call._callMoreFlags &= ~GTF_CALL_M_IMPLICIT_TAILCALL;
#endif
        }

        if (call.IsSpecialIntrinsic())
        {
            FailTailCall("Might turn into an intrinsic");
            return null;
        }

        if (call.IsNoReturn && !call.IsTailPrefixedCall)
        {
            FailTailCall("Never returns");
            return null;
        }

#if DEBUG
        if (opts.compGcChecks && (info.compRetType is TYP_REF))
        {
            FailTailCall("DOTNET_JitGCChecks or stress might have interposed a call to CORINFO_HELP_CHECK_OBJ, invalidating tailcall opportunity");
            return null;
        }
#endif
        if (compIsAsync != call.IsAsync)
        {
            FailTailCall("Caller and callee do not agree on async-ness");
            return null;
        }

        if (info.compRetBuffArg != BAD_VAR_NUM)
        {
            noway_assert(call.Type is TYP_VOID);
            noway_assert(call.Args.HasRetBuffer);
            var returnBuffer = call.Args.RetBufferArg;
            assert(returnBuffer is not null);
            var value = returnBuffer.Node;
            if ((value.Oper is not GT_LCL_VAR) || (value.AsLclVarCommon().LclNum != info.compRetBuffArg))
            {
                FailTailCall("Need to copy return buffer");
                return null;
            }
        }

        var isImplicitOrStressTailCall = call.IsImplicitTailCall || call.IsStressTailCall;
        if (isImplicitOrStressTailCall && compLocallocUsed)
        {
            FailTailCall("Localloc used");
            return null;
        }

#if DEBUG
        if (isImplicitOrStressTailCall && compPoisoningAnyImplicitByrefs)
        {
            FailTailCall("STRESS_POISON_IMPLICIT_BYREFS has introduced IR after tailcall opportunity, invalidating");
            return null;
        }
#endif
        var hasStructParam = false;
        for (var localNumber = 0; localNumber < lvaCount; localNumber++)
        {
            ref var descriptor = ref lvaTable[localNumber];
            if (isImplicitOrStressTailCall)
            {
                if (descriptor.IsAddressExposed)
                {
                    if (lvaIsImplicitByRefLocal(localNumber))
                    {
                        // Taking an implicit-byref's address is a non-address use of its pointer.
                    }
                    else if (descriptor.lvIsStructField && lvaIsImplicitByRefLocal(descriptor.lvParentLcl))
                    {
                        // The same applies to a field of an implicit-byref parameter.
                    }
                    else if (descriptor.lvPromoted && (lvaTable[descriptor.lvFieldLclStart].lvParentLcl != localNumber))
                    {
                        // Promotion bookkeeping temps are demoted before their address is used.
                        assert(lvaIsImplicitByRefLocal(lvaTable[descriptor.lvFieldLclStart].lvParentLcl));
                        assert(fgGlobalMorph);
                    }
                    else if (descriptor.IsStackAllocatedObject)
                    {
                        // Stack objects cannot currently be passed to callees.
                    }
#if FEATURE_FIXED_OUT_ARGS
                    else if (localNumber == lvaOutgoingArgSpaceVar)
                    {
                        // Only callees expose the outgoing argument area.
                    }
#endif
                    else
                    {
                        FailTailCall("Local address taken", localNumber);
                        return null;
                    }
                }

                if (descriptor.lvPinned)
                {
                    FailTailCall("Has Pinned Vars", localNumber);
                    return null;
                }
            }

            if (varTypeIsStruct(descriptor.Type) && descriptor.lvIsParam)
            {
                hasStructParam = true;
            }
        }

        var canFastTailCall = fgCanFastTailCall(call, out var failReason);
        CORINFO_TAILCALL_HELPERS tailCallHelpers = default;
        var tailCallViaJitHelper = false;
        if (!canFastTailCall)
        {
            if (call.IsImplicitTailCall)
            {
                assert(failReason is not null);
                FailTailCall(failReason);
                return null;
            }

            assert(call.IsTailPrefixedCall);
            if (!call.IsVirtualStub && (call.HasNonStandardAddedArgs(this)
                || (call.Args.FindWellKnownArg(WellKnownArg.SecretStubParam) is not null)))
            {
                FailTailCall("Method with non-standard args passed in callee trash register cannot be tail called via helper");
                return null;
            }

            if (fgCanTailCallViaJitHelper(call))
            {
                tailCallViaJitHelper = true;
            }
            else
            {
                ref var tailInfo = ref call._tailCallInfo;
                ref var token = ref Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>();
                CORINFO_GET_TAILCALL_HELPERS_FLAGS flags = 0;
                if (!tailInfo.IsCalli)
                {
                    token = ref tailInfo.Token;
                    if (tailInfo.IsCallvirt)
                    {
                        flags |= CORINFO_TAILCALL_IS_CALLVIRT;
                    }
                }

                if (call.Args.HasThisPointer)
                {
                    var thisArgument = call.Args.ThisArg;
                    assert(thisArgument is not null);
                    if (thisArgument.Node.Type is not TYP_REF)
                    {
                        flags |= CORINFO_TAILCALL_THIS_ARG_IS_BYREF;
                    }
                }

                fixed (CORINFO_RESOLVED_TOKEN* tokenPointer = &token)
                fixed (CORINFO_SIG_INFO* signaturePointer = &tailInfo.Sig)
                {
                    if (!info.compCompHnd->getTailCallHelpers(tokenPointer, signaturePointer, flags, &tailCallHelpers))
                    {
                        FailTailCall("Tail call help not available");
                        return null;
                    }
                }
            }
        }

        var fastTailCallToLoop = false;
#if FEATURE_TAILCALL_OPT
        // Generic context updates and register-passed struct parameters/results
        // are not yet supported by the recursive-call-to-loop transformation.
        if (opts.compTailCallLoopOpt && canFastTailCall && !opts.IsOSR && gtIsRecursiveCall(call)
            && !lvaReportParamTypeArg() && !lvaKeepAliveAndReportThis() && !call.IsVirtual
            && !hasStructParam && !varTypeIsStruct(call.Type))
        {
            fastTailCallToLoop = true;
        }
#endif
        var tailCallResult = fastTailCallToLoop ? TAILCALL_RECURSIVE
            : canFastTailCall ? TAILCALL_OPTIMIZED : TAILCALL_HELPER;
        info.compCompHnd->reportTailCallDecision(null,
            call._callType is CT_USER_FUNC ? call._callMethHnd : null,
            call.IsTailPrefixedCall, tailCallResult, null);

        if (call.IsExpandedEarly && call.IsVirtualVtable && (call.ControlExpr is null))
        {
            assert(call.Args.HasThisPointer);
            if (tailCallResult is TAILCALL_HELPER)
            {
                call.IsExpandedEarly = false;
            }
            else if (tailCallResult is TAILCALL_OPTIMIZED)
            {
                var thisArgument = call.Args.ThisArg;
                assert(thisArgument is not null);
                if ((thisArgument.Node.Flags & GTF_SIDE_EFFECT) != 0)
                {
                    call.IsExpandedEarly = false;
                }
            }
        }

        compTailCallUsed = true;
        call._callMoreFlags |= GTF_CALL_M_TAILCALL;
        if (tailCallViaJitHelper)
        {
            call._callMoreFlags |= GTF_CALL_M_TAILCALL_VIA_JIT_HELPER;
        }
#if FEATURE_TAILCALL_OPT
        if (fastTailCallToLoop)
        {
            call._callMoreFlags |= GTF_CALL_M_TAILCALL_TO_LOOP;
        }
#endif
        // Clear the pending state before recursively morphing the accepted call.
        call._callMoreFlags &= ~GTF_CALL_M_EXPLICIT_TAILCALL;
#if FEATURE_TAILCALL_OPT
        call._callMoreFlags &= ~GTF_CALL_M_IMPLICIT_TAILCALL;
#endif
#if DEBUG
        if (verbose)
        {
            jitprintf("\nGTF_CALL_M_TAILCALL bit set for call ");
            printTreeId(call);
            jitprintf("\n");
            if (fastTailCallToLoop)
            {
                jitprintf("\nGTF_CALL_M_TAILCALL_TO_LOOP bit set for call ");
                printTreeId(call);
                jitprintf("\n");
            }
        }
#endif
        if (call.IsR2RRelativeIndir && canFastTailCall && !fastTailCallToLoop && !call.IsDelegateInvoke)
        {
            fixed (CORINFO_CONST_LOOKUP* entryPoint = &call._entryPoint)
            {
                info.compCompHnd->updateEntryPointForTailCall(entryPoint);
            }
#if TARGET_XARCH
            call.Args.ResetFinalArgsAndAbiInfo();
#endif
        }

        fgValidateIRForTailCall(call);
        assert(compCurBB is not null);
        if (compCurBB.Kind is BBJ_ALWAYS)
        {
            var currentBlock = compCurBB;
            var targetBlock = currentBlock.Target;
            fgRemoveRefPred(currentBlock.TargetEdge);
            if (currentBlock.hasProfileWeight && targetBlock.hasProfileWeight)
            {
                targetBlock.decreaseBBProfileWeight(currentBlock.bbWeight);
                if (targetBlock.NumSucc > 0)
                {
                    JITDUMP($"Flow removal out of {FMT_BB(currentBlock.bbNum)} needs to be propagated. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                    fgPgoConsistent = false;
                }
            }
        }
        else
        {
            assert(compCurBB.Kind is BBJ_RETURN);
        }

#if !FEATURE_TAILCALL_OPT_SHARED_RETURN
        if (gtIsRecursiveCall(call))
#endif
        {
            compCurBB.SetKindAndTargetEdge(BBJ_RETURN, null);
        }

        assert(fgMorphStmt is not null);
#if DEBUG
        var statementExpression = fgMorphStmt.RootNode;
        if (statementExpression.Oper is GT_CALL)
        {
            assert(statementExpression == call);
        }
        else
        {
            assert(statementExpression.Oper is GT_RETURN or GT_STORE_LCL_VAR or GT_COMMA);
            if (statementExpression.Oper is GT_COMMA)
            {
                assert(statementExpression.AsOp().Op2.IsNothingNode);
            }
            var treeWithCall = statementExpression.Oper is GT_COMMA
                ? statementExpression.AsOp().Op1
                : statementExpression.AsUnOp().Op1;
            while (treeWithCall.Oper is GT_CAST)
            {
                assert(!treeWithCall.HasOverflowCheck);
                treeWithCall = treeWithCall.AsCast().CastOp;
            }
            assert(treeWithCall == call);
        }
#endif
        var originalCallType = call.Type;
        GenTree result;
        if (!canFastTailCall && !tailCallViaJitHelper)
        {
            result = fgMorphTailCallViaHelpers(call, tailCallHelpers);
        }
        else
        {
            var nextStatement = fgMorphStmt.NextStmt;
            JITDUMP("Remove all stmts after the call.\n");
            while (nextStatement is not null)
            {
                var statementToRemove = nextStatement;
                nextStatement = statementToRemove.NextStmt;
                fgRemoveStmt(compCurBB, statementToRemove);
            }

            var root = fgMorphStmt.RootNode;
            var isRootReplaced = root != call;
            if (isRootReplaced)
            {
#if DEBUG
                JITDUMP($"Replace root node [{root.TreeId:D6}] with [{call.TreeId:D6}] tail call node.\n");
#endif
                fgMorphStmt.RootNode = call;
            }

            call.Type = TYP_VOID;
            if (call.IsVirtualStub)
            {
                call.Flags |= GTF_CALL_NULLCHECK;
            }
            if (tailCallViaJitHelper)
            {
                fgMorphTailCallViaJitHelper(call);
                call.Args.ResetFinalArgsAndAbiInfo();
            }

            assert(fgFirstBB is not null);
            if (!canFastTailCall && !fgFirstBB.HasFlag(BBF_GC_SAFE_POINT) && !compCurBB.HasFlag(BBF_GC_SAFE_POINT))
            {
                JITDUMP($"Marking {FMT_BB(compCurBB.bbNum)} as needs gc poll\n");
                compCurBB.SetFlags(BBF_NEEDS_GCPOLL);
                optMethodFlags |= OMF_NEEDS_GCPOLLS;
            }

            _ = fgMorphCall(call);
            noway_assert(compCurBB.Kind is BBJ_RETURN);
            if (canFastTailCall)
            {
                compCurBB.SetFlags(BBF_HAS_JMP);
            }
            else
            {
                compCurBB.SetKindAndTargetEdge(BBJ_THROW, null);
            }

            if (isRootReplaced)
            {
                call.SetMorphed(this);
                // Unwind the abandoned ancestors without remorphing the new root.
                var zeroType = originalCallType is TYP_STRUCT ? TYP_INT : originalCallType.ActualType;
                result = fgMorphTree(gtNewZeroConNode(zeroType));
            }
            else
            {
                result = call;
            }
        }

        return result;
    }

    private unsafe GenTree fgMorphTailCallViaHelpers(GenTreeCall call, in CORINFO_TAILCALL_HELPERS help)
    {
        assert(!IsAot);
        JITDUMP("fgMorphTailCallViaHelpers (before):\n");
        DISPTREE(call);
        assert(!call.IsHelperCall());
        assert(!call.IsImplicitTailCall);

        call.Args.ResetFinalArgsAndAbiInfo();
        var dispatcherAndResult = fgCreateCallDispatcherAndGetResult(call, help.hCallTarget, help.hDispatcher);
        if (call.Args.HasRetBuffer)
        {
            JITDUMP("Removing retbuf");
            var returnBuffer = call.Args.RetBufferArg;
            assert(returnBuffer is not null);
            call.Args.Remove(returnBuffer);
            call._callMoreFlags &= ~GTF_CALL_M_RETBUFFARG;
        }

        var stubNeedsTargetPointer = (help.flags & CORINFO_TAILCALL_STORE_TARGET) != 0;
        GenTree? beforeStoreArgsStub = null;
        GenTree? thisPointerStubArgument = null;
        if (call.Args.HasThisPointer)
        {
            JITDUMP("Moving this pointer into arg list\n");
            var thisArgument = call.Args.ThisArg;
            assert(thisArgument is not null);
            var instance = thisArgument.Node;
            GenTree? thisPointer = null;
            var needsNullCheck = call.NeedsNullCheck;
            var stubNeedsThisPointer = stubNeedsTargetPointer && call.IsVirtual;
            if (needsNullCheck || stubNeedsThisPointer)
            {
                if ((instance.Flags & GTF_SIDE_EFFECT) == 0)
                {
                    thisPointer = gtClone(instance, complexOK: true);
                }

                if (thisPointer is null)
                {
                    var temporary = lvaGrabTemp(shortLifetime: true, "tail call thisptr");
                    beforeStoreArgsStub = gtNewTempStore(temporary, instance);
                    if (needsNullCheck)
                    {
                        var load = gtNewLclvNode(instance.Type, temporary);
                        beforeStoreArgsStub = gtNewCommaNode(TYP_VOID, beforeStoreArgsStub, gtNewNullCheck(load));
                    }
                    thisPointer = gtNewLclvNode(instance.Type, temporary);
                    if (stubNeedsThisPointer)
                    {
                        thisPointerStubArgument = gtNewLclvNode(instance.Type, temporary);
                    }
                }
                else if (needsNullCheck)
                {
                    beforeStoreArgsStub = gtNewNullCheck(instance);
                    if (stubNeedsThisPointer)
                    {
                        thisPointerStubArgument = gtClone(instance, complexOK: true);
                    }
                }
                else
                {
                    assert(stubNeedsThisPointer);
                    thisPointerStubArgument = instance;
                }

                call.Flags &= ~GTF_CALL_NULLCHECK;
                assert((thisPointerStubArgument is not null) == stubNeedsThisPointer);
            }
            else
            {
                thisPointer = instance;
            }

            // Keep the spill/null check ahead of the regular stub arguments.
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(thisPointer, thisArgument.SignatureType));
            call.Args.Remove(thisArgument);
        }

        if (stubNeedsTargetPointer)
        {
            JITDUMP("Adding target since VM requested it\n");
            GenTree target;
            if (!call.IsVirtual)
            {
                if (call._callType is CT_INDIRECT)
                {
                    noway_assert(call.ControlExpr is not null);
                    target = call.ControlExpr;
                }
                else
                {
                    CORINFO_CONST_LOOKUP lookup;
                    info.compCompHnd->getFunctionEntryPoint(call._callMethHnd, &lookup);
                    target = gtNewIconEmbHndNode(lookup, GTF_ICON_FTN_ADDR, call._callMethHnd);
                }
            }
            else
            {
                ref var tailInfo = ref call._tailCallInfo;
                assert(!tailInfo.Sig.hasTypeArg());
                var flags = CORINFO_CALLINFO_LDFTN;
                if (tailInfo.IsCallvirt)
                {
                    flags |= CORINFO_CALLINFO_CALLVIRT;
                }
                eeGetCallInfo(tailInfo.Token, Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), flags, out _);
                assert(thisPointerStubArgument is not null);
                target = getVirtMethodPointerTree(thisPointerStubArgument, tailInfo.Token);
            }

            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(target));
        }

        call._callType = CT_USER_FUNC;
        call.ControlExpr = null;
        call._callMethHnd = help.hStoreArgs;
        call.Flags &= ~GTF_CALL_VIRT_KIND_MASK;
        call._callMoreFlags &= ~(GTF_CALL_M_TAILCALL | GTF_CALL_M_DELEGATE_INV);
        call._retClsHnd = null;
        call.Type = TYP_VOID;
        call._returnType = TYP_VOID;

        GenTree storeArgsStub = call;
        if (beforeStoreArgsStub is not null)
        {
            storeArgsStub = gtNewCommaNode(TYP_VOID, beforeStoreArgsStub, storeArgsStub);
        }
        var result = fgMorphTree(gtNewCommaNode(dispatcherAndResult.Type, storeArgsStub, dispatcherAndResult));
        JITDUMP("fgMorphTailCallViaHelpers (after):\n");
        DISPTREE(result);

        return result;
    }

    private unsafe GenTree fgMorphCall(GenTreeCall call)
    {
        if (call.CanTailCall)
        {
            var replacement = fgMorphPotentialTailCall(call);
            if (replacement is not null)
            {
                return replacement;
            }

            assert(!call.CanTailCall);

#if FEATURE_MULTIREG_RET
            if (fgGlobalMorph && call.HasMultiRegRetVal && varTypeIsStruct(call.Type))
            {
                // Finish the multi-register return spill deferred for a possible tail call.
                call.Args.ResetFinalArgsAndAbiInfo();
                var temp = lvaGrabTemp(shortLifetime: false, "Return value temp for multi-reg return (rejected tail call).");
                lvaTable[temp].lvIsMultiRegRet = true;
                var structHandle = call.RetClsHnd;
                assert(structHandle != NO_CLASS_HANDLE);
                lvaSetStruct(temp, structHandle, unsafeValueClsCheck: false);
                var store = fgMorphTree(gtNewStoreLclVarNode(temp, call));
                assert(compCurStmt is not null);
                assert(compCurBB is not null);
                var statement = gtNewStmt(store, compCurStmt.DebugInfo);
                fgInsertStmtBefore(compCurBB, compCurStmt, statement);

                var result = gtNewLclvNode(lvaTable[temp].Type, temp);
                result.Flags |= GTF_DONT_CSE;
                compCurBB.SetFlags(BBF_HAS_CALL);
                JITDUMP("\nInserting store of a multi-reg call result to a temp:\n");
                DISPSTMT(statement);
                result.SetMorphed(this);
                return result;
            }
#endif
        }

        if (call.IsSpecialIntrinsic() &&
            (lookupNamedIntrinsic(call._callMethHnd) == NI_System_Text_UTF8Encoding_UTF8EncodingSealed_ReadUtf8))
        {
            MethodHasSpecialIntrinsics = true;
        }

        if (((call._callMoreFlags & (GTF_CALL_M_SPECIAL_INTRINSIC | GTF_CALL_M_LDVIRTFTN_INTERFACE)) == 0) &&
            (call.IsHelperCall(CORINFO_HELP_VIRTUAL_FUNC_PTR)
#if FEATURE_READYTORUN
                || call.IsHelperCall(CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR)
#endif
            ))
        {
            assert(fgMorphStmt is not null);
            if (call == fgMorphStmt.RootNode)
            {
                // The ignored virtual-function-pointer result still requires a null check.
                assert(call.Args.CountUserArgs() >= 1);
                var argument = call.Args.GetUserArgByIndex(0);
                assert(argument is not null);
                return fgMorphTree(gtNewNullCheck(argument.Node));
            }
        }

        noway_assert(call.Oper is GT_CALL);

        if (fgGlobalMorph)
        {
            if (call._callType is CT_INDIRECT)
            {
                optCallCount++;
                optIndirectCallCount++;
                if (call.IsFastTailCall)
                {
                    optFastTailCallCount++;
                    optIndirectFastTailCallCount++;
                }
            }
            else if (call._callType is CT_USER_FUNC)
            {
                optCallCount++;
                if (call.IsVirtual)
                {
                    optIndirectCallCount++;
                }

                if (call.IsFastTailCall)
                {
                    optFastTailCallCount++;
                    if (call.IsVirtual)
                    {
                        optIndirectFastTailCallCount++;
                    }
                }
            }
        }

        assert(compCurBB is not null);
        if (IsGcSafePoint(call))
        {
            compCurBB.SetFlags(BBF_GC_SAFE_POINT);
        }

        if (fgGlobalMorph && call.IsUnmanaged && call.IsSuppressGCTransition)
        {
            compCurBB.SetFlags(BBF_HAS_SUPPRESSGC_CALL | BBF_GC_SAFE_POINT);
            optMethodFlags |= OMF_NEEDS_GCPOLLS;
        }

        if (fgGlobalMorph)
        {
            if (IsStaticHelperEligibleForExpansion(call))
            {
                MethodHasStaticInit = true;
            }
            else if ((call._callMoreFlags & GTF_CALL_M_CAST_CAN_BE_EXPANDED) != 0)
            {
                MethodHasExpandableCasts = true;
            }
        }

        // Fold these intrinsics before argument morphing changes their shape.
        if (!call.Args.AreArgsComplete && call.IsSpecialIntrinsic())
        {
            var folded = gtFoldExprCall(call);
            if (folded != call)
            {
                return fgMorphTree(folded);
            }
        }

        compCurBB.SetFlags(BBF_HAS_CALL);
        using var sharedTemps = new SharedTempsScope(this);
        call = fgMorphArgs(call);
        noway_assert(call.Oper is GT_CALL);

        if (gtIsTypeHandleToRuntimeTypeHelper(call))
        {
            var argument = call.Args.GetUserArgByIndex(0);
            assert(argument is not null);
            var classHandle = gtGetHelperArgClassHandle(argument.Node);
            if (classHandle != NO_CLASS_HANDLE)
            {
                var pointer = info.compCompHnd->getRuntimeTypePointer(classHandle);
                if (pointer is not null)
                {
                    return fgMorphTree(gtNewIconEmbObjHndNode(pointer));
                }
            }
        }

        fgAssignSetVarDef(call);
        if (call.RequiresAsgFlag)
        {
            call.Flags |= GTF_ASG;
        }

        if (call.IsExpandedEarly && call.IsVirtualVtable && fgGlobalMorph && (call.ControlExpr is null))
        {
            call.ControlExpr = fgExpandVirtualVtableCallTarget(call);
        }

        if (call.ControlExpr is GenTree control)
        {
            call.ControlExpr = fgMorphTree(control);
            call.Flags |= call.ControlExpr.Flags & GTF_ALL_EFFECT;
        }

        if (opts.OptimizationEnabled && call.IsHelperCallOrUserEquivalent(this, CORINFO_HELP_ARRADDR_ST))
        {
            assert(call.Args.CountUserArgs() == 3);
            var arrayArgument = call.Args.GetUserArgByIndex(0);
            var indexArgument = call.Args.GetUserArgByIndex(1);
            var valueArgument = call.Args.GetUserArgByIndex(2);
            assert(arrayArgument is not null);
            assert(indexArgument is not null);
            assert(valueArgument is not null);
            var array = arrayArgument.Node;
            var index = indexArgument.Node;
            var value = valueArgument.Node;

            if (!call.IsHelperCall())
            {
                call._callMethHnd = eeFindHelper(CORINFO_HELP_ARRADDR_ST);
                call._callType = CT_HELPER;
            }

            if (gtCanSkipCovariantStoreCheck(value, array))
            {
                GenTree? argumentSetup = null;
                foreach (var argument in call.Args.EarlyArgs)
                {
                    if (argument.LateNode is null)
                    {
                        continue;
                    }

                    var setup = argument.EarlyNode;
                    assert(setup is not null);
                    assert((setup != array) && (setup != index));

                    if (argumentSetup is null)
                    {
                        argumentSetup = setup;
                    }
                    else
                    {
                        argumentSetup = new GenTreeOp(GT_COMMA, TYP_VOID, argumentSetup, setup);
                        argumentSetup.SetMorphed(this);
                    }
                }

                var address = gtNewArrayIndexAddr(array, index, TYP_REF, NO_CLASS_HANDLE);
                var result = fgMorphTree(gtNewStoreIndNode(TYP_REF, address, value));

                if (argumentSetup is not null)
                {
                    result = new GenTreeOp(GT_COMMA, TYP_VOID, argumentSetup, result);
                    result.SetMorphed(this);
                }

                return result;
            }
        }

        // Tail calls must retain their return block so their epilog is emitted.
        if (call.IsNoReturn && !call.IsTailCall)
        {
            fgHasNoReturnCall = true;
        }

        return call;
    }
}
