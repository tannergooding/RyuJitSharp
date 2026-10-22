// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation
    {
        private unsafe BasicBlock? CreateInlinedFrameSuspensionTail(BasicBlock callBlock,
            GenTreeCall call, AsyncContinuationLayout layout, GenTree? frameResumed)
        {
            var enclosing = 0;
            foreach (var arg in call.Args.Args)
            {
                if (arg.WellKnownArg is WellKnownArg.AsyncResumedUse)
                {
                    enclosing++;
                }
            }

            if (enclosing == 0)
            {
                return null;
            }

            global::System.Collections.Generic.List<global::RyuJitSharp.ContinuationContextHandling> __jitExceptionResult28;
            var __jitExceptionCoalesceLeft29 = call.GetAsyncInfo().InlineFrameContextHandling;
            if (__jitExceptionCoalesceLeft29 != null)
            {
                __jitExceptionResult28 = __jitExceptionCoalesceLeft29;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlinedAsyncFrameHasNoContextHandling);
                __jitExceptionResult28 = default;
            }
            var handling = __jitExceptionResult28;
            if ((frameResumed is null) || (handling.Count != enclosing))
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlinedAsyncFrameContextChainIsInconsistent);
            }

            JITDUMP($"    Call [{call.TreeId:D6}] is inside an inlined async frame; {enclosing + 1} frames in chain\n");
            var values = new List<GenTree>(enclosing * 3);
            for (var i = 0; i < enclosing; i++)
            {
                WellKnownArg[] kinds = [
                    WellKnownArg.AsyncResumedUse,
                    WellKnownArg.AsyncExecutionContext,
                    WellKnownArg.AsyncSynchronizationContext,
                ];

                foreach (var kind in kinds)
                {
                    global::RyuJitSharp.CallArg __jitExceptionResult30;
                    var __jitExceptionCoalesceLeft31 = call.Args.FindWellKnownArg(kind);
                    if (__jitExceptionCoalesceLeft31 != null)
                    {
                        __jitExceptionResult30 = __jitExceptionCoalesceLeft31;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlinedAsyncFrameIsMissingAContextArgument);
                        __jitExceptionResult30 = default;
                    }
                    var argument = __jitExceptionResult30;
                    values.Add(TakeAsyncArgument(callBlock, call, argument));
                }
            }

            if (_sharedReturnBB is null)
            {
                CreateSharedReturnBB();
            }

            global::RyuJitSharp.BasicBlock __jitExceptionResult32;
            var __jitExceptionCoalesceLeft33 = _sharedReturnBB;
            if (__jitExceptionCoalesceLeft33 != null)
            {
                __jitExceptionResult32 = __jitExceptionCoalesceLeft33;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlinedFrameTailRequiresAReturnBlock);
                __jitExceptionResult32 = default;
            }
            var sharedReturn = __jitExceptionResult32;
            var tail = _compiler.fgNewBBbefore(BBJ_ALWAYS, sharedReturn, false);
            tail.bbSetRunRarely();
            tail.clearTryIndex();
            tail.clearHndIndex();
            tail.SetKindAndTargetEdge(BBJ_ALWAYS, _compiler.fgAddRefPred(sharedReturn, tail));
            if (_compiler.fgIsUsingProfileWeights)
            {
                tail.SetFlags(BBF_PROF_WEIGHT);
            }

            JITDUMP($"    Created inlined frame suspension tail {FMT_BB(tail.bbNum)} for {enclosing + 1} frames\n");
            // Every outer frame transitions only on the first suspension beneath it.
            var anyResumedIsSet = IsKnownResumed(frameResumed);
            var anyResumedLocal = BAD_VAR_NUM;
            if (!anyResumedIsSet)
            {
                anyResumedLocal = _compiler.lvaGrabTemp(false, "Async any inlined frame resumed");
                ref var descriptor = ref _compiler.lvaGetDesc(anyResumedLocal);
                descriptor.Type = TYP_INT;
                descriptor.lvOnlyUsedOnSynchronousPath = true;
                var init = _compiler.gtNewStoreLclVarNode(
                    anyResumedLocal, _compiler.gtCloneExpr(frameResumed));
                tail.InsertAtEnd(LIR.SeqTree(_compiler, init));
            }
            else
            {
                JITDUMP("    Frame is known to have resumed; skipping all frame transition handling\n");
            }

            var asyncInfo = _compiler.eeGetAsyncInfo();
            for (var i = 0; i < enclosing; i++)
            {
                var outerResumed = values[i * 3];
                var outerExec = values[i * 3 + 1];
                var outerSync = values[i * 3 + 2];
                var depth = enclosing - i;

                if (!_compiler.TryGetContinuationMemberIndex(
                    ContinuationMember.InlineFrameFlags(depth), out var index))
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InlineFrameTailMemberWasNeverRegistered);
                }

                var membersAreLive = layout.ContinuationMemberOffsets[index] >= 0;
                if (anyResumedIsSet)
                {
                    JITDUMP($"    Skipping no-op frame transition handling for inline frame depth {depth}\n");
                    continue;
                }

                if (membersAreLive)
                {
                    var frameHandling = handling[i];
                    var captureContext = frameHandling is
                        ContinuationContextHandling.ContinueOnCapturedContext;
                    global::RyuJitSharp.CORINFO_METHOD_STRUCT_* __jitExceptionResult103;
                    switch (frameHandling)
                    {
                        case ContinuationContextHandling.ContinueOnCapturedContext:
                            __jitExceptionResult103 = asyncInfo.captureInlinedFrameTransitionWithContinuationContextMethHnd;
                            break;
                        case ContinuationContextHandling.ContinueOnThreadPool:
                            __jitExceptionResult103 = asyncInfo.captureInlinedFrameTransitionContinueOnThreadPoolMethHnd;
                            break;
                        case ContinuationContextHandling.None:
                            __jitExceptionResult103 = asyncInfo.captureInlinedFrameTransitionNoContinuationContextMethHnd;
                            break;
                        default:
                            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnrecognizedInlineFrameContextHandling);
                            __jitExceptionResult103 = default;
                            break;
                    }
                    var captureMethod = __jitExceptionResult103;
                    var capture = _compiler.gtNewUserCallNode(TYP_VOID, captureMethod);
                    _ = capture.Args.PushFront(NewCallArg.CreateForPrimitive(
                        ContinuationMemberAddress(layout,
                            ContinuationMember.InlineFrameExecutionContext(depth))));
                    _ = capture.Args.PushFront(NewCallArg.CreateForPrimitive(
                        ContinuationMemberAddress(layout, ContinuationMember.InlineFrameFlags(depth))));
                    if (captureContext)
                    {
                        _ = capture.Args.PushFront(NewCallArg.CreateForPrimitive(
                            ContinuationMemberAddress(layout,
                                ContinuationMember.InlineFrameContinuationContext(depth))));
                    }

                    _ = capture.Args.PushFront(NewCallArg.CreateForPrimitive(
                        _compiler.gtNewLclvNode(TYP_INT, anyResumedLocal)));
                    _compiler.compCurBB = tail;
                    _ = _compiler.fgMorphTree(capture);
                    tail.InsertAtEnd(LIR.SeqTree(_compiler, capture));
                }
                else
                {
                    JITDUMP($"    No reads of inline frame depth {depth} members survived; skipping its capture\n");
                }

                if (IsKnownResumed(outerResumed))
                {
                    JITDUMP($"    Inline frame depth {depth} is known to have resumed; skipping the rest of the tail\n");
                    anyResumedIsSet = true;
                    continue;
                }

                var accumulated = _compiler.gtNewBinaryNode(GT_OR, TYP_INT,
                    _compiler.gtNewLclvNode(TYP_INT, anyResumedLocal),
                    _compiler.gtCloneExpr(outerResumed));
                var update = _compiler.gtNewStoreLclVarNode(anyResumedLocal, accumulated);
                tail.InsertAtEnd(LIR.SeqTree(_compiler, update));

                var restore = _compiler.gtNewUserCallNode(TYP_VOID,
                    asyncInfo.restoreContextsOnSuspensionMethHnd);
                _ = restore.Args.PushFront(NewCallArg.CreateForPrimitive(_compiler.gtCloneExpr(outerSync)));
                _ = restore.Args.PushFront(NewCallArg.CreateForPrimitive(_compiler.gtCloneExpr(outerExec)));
                _ = restore.Args.PushFront(NewCallArg.CreateForPrimitive(
                    _compiler.gtNewLclvNode(TYP_INT, anyResumedLocal)));
                _compiler.compCurBB = tail;
                _ = _compiler.fgMorphTree(restore);
                tail.InsertAtEnd(LIR.SeqTree(_compiler, restore));
            }

            DISPRANGE(tail);
            return tail;
        }
    }
}
