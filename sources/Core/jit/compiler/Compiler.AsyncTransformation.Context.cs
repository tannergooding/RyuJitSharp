// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation
    {
        private enum SuspensionContextHelper
        {
            None,
            WithContinuationContext,
            WithoutContinuationContext,
        }

        private static bool IsKnownResumed(GenTree node) =>
            node.Oper.IsIntegralConst && !node.IsIntegralConst(0);

        private static SuspensionContextHelper GetSuspensionContextHelper(GenTreeCall call)
        {
            var exec = call.Args.FindWellKnownArg(WellKnownArg.AsyncExecutionContext);
            var sync = call.Args.FindWellKnownArg(WellKnownArg.AsyncSynchronizationContext);
            assert((exec is null) == (sync is null));
            if ((exec is null) || !call.GetAsyncInfo().NeedsToSaveAndRestoreExecutionContext)
            {
                return SuspensionContextHelper.None;
            }

            return call.GetAsyncInfo().ContinuationContextHandling is
                ContinuationContextHandling.ContinueOnCapturedContext
                ? SuspensionContextHelper.WithContinuationContext
                : SuspensionContextHelper.WithoutContinuationContext;
        }

        private GenTree TakeAsyncArgument(BasicBlock block, GenTreeCall call, CallArg argument,
            bool allowInvariant = true)
        {
            var value = argument.Node;
            if ((!allowInvariant || !value.IsInvariant) && (value.Oper is not GT_LCL_VAR))
            {
                var edge = new LIR.Use(block, ref argument.NodeRef, call);
                _ = edge.ReplaceWithLclVar(_compiler);
                value = edge.Def();
            }

            block.Remove(value);
            call.Args.RemoveUnsafe(argument);
            return value;
        }

        private void ReplaceAsyncPlaceholder(BasicBlock block, GenTree placeholder, GenTree value,
            bool sequence = true)
        {
            if (!block.TryGetUse(placeholder, out var use))
            {
                throw new InvalidOperationException("Morphed async helper lost its argument placeholder");
            }

            if (sequence)
            {
                block.InsertBefore(placeholder, LIR.SeqTree(_compiler, value));
            }
            else
            {
                // Existing LIR operands retain their sequence numbers and costs.
                block.InsertBefore(placeholder, value);
            }
            use.ReplaceWith(value);
            block.Remove(placeholder);
        }

        private GenTreeOp ContinuationMemberAddress(AsyncContinuationLayout layout, ContinuationMember member)
        {
            if (!_compiler.TryGetContinuationMemberIndex(member, out var index) ||
                (layout.ContinuationMemberOffsets[index] < 0))
            {
                throw new InvalidOperationException("Unregistered inline-frame continuation member");
            }

            var offset = OFFSETOF__CORINFO_Continuation__data +
                layout.ContinuationMemberOffsets[index];
            var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
            return _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, continuation,
                _compiler.gtNewIconNode(TYP_I_IMPL, offset));
        }

        private unsafe void InsertFinishContextHandlingCall(BasicBlock block,
            AsyncContinuationLayout layout, SuspensionContextHelper helper,
            GenTree resumed, GenTree execContext, GenTree syncContext)
        {
            var asyncInfo = _compiler.eeGetAsyncInfo();
            var method = helper is SuspensionContextHelper.WithContinuationContext
                ? asyncInfo.finishSuspensionWithContinuationContextMethHnd
                : asyncInfo.finishSuspensionNoContinuationContextMethHnd;

            var execAddrPlaceholder = _compiler.gtNewZeroConNode(TYP_BYREF);
            var resumedPlaceholder = _compiler.gtNewIconNode(TYP_INT, 0);
            var execPlaceholder = _compiler.gtNewNull();
            var syncPlaceholder = _compiler.gtNewNull();
            var call = _compiler.gtNewUserCallNode(TYP_VOID, method);
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(syncPlaceholder));
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(execPlaceholder));
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(resumedPlaceholder));
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(execAddrPlaceholder));

            GenTree? contAddrPlaceholder = null;
            GenTree? flagsPlaceholder = null;
            if (helper is SuspensionContextHelper.WithContinuationContext)
            {
                contAddrPlaceholder = _compiler.gtNewZeroConNode(TYP_BYREF);
                flagsPlaceholder = _compiler.gtNewZeroConNode(TYP_BYREF);
                _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(flagsPlaceholder));
                _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(contAddrPlaceholder));
            }

            _compiler.compCurBB = block;
            _ = _compiler.fgMorphTree(call);
            block.InsertAtEnd(LIR.SeqTree(_compiler, call));

            if ((contAddrPlaceholder is not null) && (flagsPlaceholder is not null))
            {
                ReplaceAsyncPlaceholder(block, contAddrPlaceholder,
                    ContinuationAddressAtOffset(layout.ContinuationContextOffset));
                var flagsOffset = _compiler.info.compCompHnd->getFieldOffset(
                    asyncInfo.continuationFlagsFldHnd);
                ReplaceAsyncPlaceholder(block, flagsPlaceholder,
                    ContinuationAddressAtOffset(flagsOffset, fromData: false));
            }

            ReplaceAsyncPlaceholder(block, execAddrPlaceholder,
                ContinuationAddressAtOffset(layout.ExecutionContextOffset));
            ReplaceAsyncPlaceholder(block, resumedPlaceholder, resumed, sequence: false);
            ReplaceAsyncPlaceholder(block, execPlaceholder, execContext, sequence: false);
            ReplaceAsyncPlaceholder(block, syncPlaceholder, syncContext, sequence: false);
            JITDUMP("    Created FinishSuspension call:\n");
            DISPTREERANGE(block, call);
        }

        private GenTreeOp ContinuationAddressAtOffset(int offset, bool fromData = true)
        {
            var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
            var absolute = offset + (fromData ? OFFSETOF__CORINFO_Continuation__data : 0);
            return _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                continuation, _compiler.gtNewIconNode(TYP_I_IMPL, absolute));
        }

        private unsafe GenTree? RestoreContexts(
            BasicBlock callBlock, GenTreeCall call, BasicBlock suspension)
        {
            var resumedArg = call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse);
            var execArg = call.Args.FindWellKnownArg(WellKnownArg.AsyncExecutionContext);
            var syncArg = call.Args.FindWellKnownArg(WellKnownArg.AsyncSynchronizationContext);
            assert((resumedArg is null) == (execArg is null));
            assert((execArg is null) == (syncArg is null));
            if (resumedArg is null)
            {
#if DEBUG
                JITDUMP($"    Call [{call.TreeId:D6}] does not have async contexts; skipping restore on suspension\n");
#endif
                return null;
            }

#if DEBUG
            JITDUMP($"    Call [{call.TreeId:D6}] has async contexts; will restore on suspension\n");
#endif
            var resumed = TakeAsyncArgument(callBlock, call, resumedArg);
            var exec = TakeAsyncArgument(callBlock, call, execArg ??
                throw new InvalidOperationException("Missing async execution context"));
            var sync = TakeAsyncArgument(callBlock, call, syncArg ??
                throw new InvalidOperationException("Missing async synchronization context"));
            if (IsKnownResumed(resumed))
            {
                JITDUMP("    Frame is known to have resumed; skipping no-op restore on suspension\n");
                return resumed;
            }

            var resumedPlaceholder = _compiler.gtNewIconNode(TYP_INT, 0);
            var execPlaceholder = _compiler.gtNewNull();
            var syncPlaceholder = _compiler.gtNewNull();
            var method = _compiler.eeGetAsyncInfo().restoreContextsOnSuspensionMethHnd;
            var restore = _compiler.gtNewUserCallNode(TYP_VOID, method);
            _ = restore.Args.PushFront(NewCallArg.CreateForPrimitive(syncPlaceholder));
            _ = restore.Args.PushFront(NewCallArg.CreateForPrimitive(execPlaceholder));
            _ = restore.Args.PushFront(NewCallArg.CreateForPrimitive(resumedPlaceholder));
            _compiler.compCurBB = suspension;
            _ = _compiler.fgMorphTree(restore);
            suspension.InsertAtEnd(LIR.SeqTree(_compiler, restore));

            ReplaceAsyncPlaceholder(suspension, resumedPlaceholder, resumed, sequence: false);
            ReplaceAsyncPlaceholder(suspension, execPlaceholder, exec, sequence: false);
            ReplaceAsyncPlaceholder(suspension, syncPlaceholder, sync, sequence: false);
            JITDUMP("    Created RestoreContexts call on suspension:\n");
            DISPTREERANGE(suspension, restore);
            return resumed;
        }

        private void FinishSuspensionReturn(BasicBlock suspension, BasicBlock? frameTail)
        {
            if (frameTail is not null)
            {
                suspension.SetKindAndTargetEdge(BBJ_ALWAYS,
                    _compiler.fgAddRefPred(frameTail, suspension));
            }
            else if (_sharedReturnBB is not null)
            {
                suspension.SetKindAndTargetEdge(BBJ_ALWAYS,
                    _compiler.fgAddRefPred(_sharedReturnBB, suspension));
            }
            else
            {
                var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
                var ret = _compiler.gtNewUnaryNode(GT_RETURN_SUSPEND, TYP_VOID, continuation);
                suspension.InsertAtEnd(continuation);
                suspension.InsertAtEnd(ret);
            }
        }

        private unsafe void FinishContextHandlingAndSuspension(
            BasicBlock callBlock, GenTreeCall call, BasicBlock suspension,
            AsyncContinuationLayout layout, AsyncContinuationLayoutBuilder subLayout)
        {
            var helper = GetSuspensionContextHelper(call);
            if (helper is not SuspensionContextHelper.None)
            {
#if DEBUG
                JITDUMP($"    Call [{call.TreeId:D6}] has async context and captured execution context; using finish-suspension helper\n");
#endif
                FinishContextHandlingAndSuspensionWithHelper(
                    callBlock, call, suspension, layout, subLayout, helper);
                return;
            }

            var asyncInfo = _compiler.eeGetAsyncInfo();
            if (subLayout.NeedsContinuationContext)
            {
                var contPlaceholder = _compiler.gtNewZeroConNode(TYP_BYREF);
                var flagsPlaceholder = _compiler.gtNewZeroConNode(TYP_BYREF);
                var capture = _compiler.gtNewUserCallNode(TYP_VOID,
                    asyncInfo.captureContinuationContextMethHnd);
                _ = capture.Args.PushFront(NewCallArg.CreateForPrimitive(flagsPlaceholder));
                _ = capture.Args.PushFront(NewCallArg.CreateForPrimitive(contPlaceholder));
                _compiler.compCurBB = suspension;
                _ = _compiler.fgMorphTree(capture);
                suspension.InsertAtEnd(LIR.SeqTree(_compiler, capture));
                ReplaceAsyncPlaceholder(suspension, contPlaceholder,
                    ContinuationAddressAtOffset(layout.ContinuationContextOffset));
                var flagsOffset = _compiler.info.compCompHnd->getFieldOffset(
                    asyncInfo.continuationFlagsFldHnd);
                ReplaceAsyncPlaceholder(suspension, flagsPlaceholder,
                    ContinuationAddressAtOffset(flagsOffset, fromData: false));
            }

            if (subLayout.NeedsExecutionContext)
            {
                var capture = _compiler.gtNewUserCallNode(TYP_REF,
                    asyncInfo.captureExecutionContextMethHnd);
                _compiler.compCurBB = suspension;
                _ = _compiler.fgMorphTree(capture);
                var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
                var offset = OFFSETOF__CORINFO_Continuation__data +
                    layout.ExecutionContextOffset;
                var store = StoreAtOffset(continuation, offset, capture, TYP_REF);
                suspension.InsertAtEnd(LIR.SeqTree(_compiler, store));
            }

            var resumed = RestoreContexts(callBlock, call, suspension);
            assert(suspension.Kind is BBJ_RETURN);
            var frameTail = CreateInlinedFrameSuspensionTail(callBlock, call, layout, resumed);
            FinishSuspensionReturn(suspension, frameTail);
        }

        private void FinishContextHandlingAndSuspensionWithHelper(
            BasicBlock callBlock, GenTreeCall call, BasicBlock suspension,
            AsyncContinuationLayout layout, AsyncContinuationLayoutBuilder subLayout,
            SuspensionContextHelper helper)
        {
            assert((helper is not SuspensionContextHelper.WithContinuationContext) ||
                subLayout.NeedsContinuationContext);
            var resumedArg = call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse) ??
                throw new InvalidOperationException("Missing async resumed indicator");
            var execArg = call.Args.FindWellKnownArg(WellKnownArg.AsyncExecutionContext) ??
                throw new InvalidOperationException("Missing async execution context");
            var syncArg = call.Args.FindWellKnownArg(WellKnownArg.AsyncSynchronizationContext) ??
                throw new InvalidOperationException("Missing async synchronization context");
            var resumed = TakeAsyncArgument(callBlock, call, resumedArg);
            var exec = TakeAsyncArgument(callBlock, call, execArg, allowInvariant: false);
            var sync = TakeAsyncArgument(callBlock, call, syncArg, allowInvariant: false);
            var frameTail = CreateInlinedFrameSuspensionTail(
                callBlock, call, layout, resumed);
            var shared = helper is SuspensionContextHelper.WithContinuationContext
                ? _sharedFinishContextHandlingWithContinuationContextBB
                : _sharedFinishContextHandlingWithoutContinuationContextBB;

            if ((shared is not null) && (frameTail is null))
            {
                if (_sharedFinishContextHandlingResumedVar != BAD_VAR_NUM)
                {
                    var store = _compiler.gtNewStoreLclVarNode(
                        _sharedFinishContextHandlingResumedVar, resumed);
                    suspension.InsertAtEnd(LIR.SeqTree(_compiler, store));
                }

                if (_sharedFinishContextHandlingExecContextVar != BAD_VAR_NUM)
                {
                    var store = _compiler.gtNewStoreLclVarNode(
                        _sharedFinishContextHandlingExecContextVar, exec);
                    suspension.InsertAtEnd(LIR.SeqTree(_compiler, store));
                }

                if (_sharedFinishContextHandlingSyncContextVar != BAD_VAR_NUM)
                {
                    var store = _compiler.gtNewStoreLclVarNode(
                        _sharedFinishContextHandlingSyncContextVar, sync);
                    suspension.InsertAtEnd(LIR.SeqTree(_compiler, store));
                }

                suspension.SetKindAndTargetEdge(BBJ_ALWAYS,
                    _compiler.fgAddRefPred(shared, suspension));
            }
            else
            {
                InsertFinishContextHandlingCall(
                    suspension, layout, helper, resumed, exec, sync);
                FinishSuspensionReturn(suspension, frameTail);
            }
        }
    }
}
