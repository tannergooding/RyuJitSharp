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
        private static int EncodeContinuationIndex(int offset, int firstBit, int numBits)
        {
            assert(numBits < 32);
            assert((offset % TARGET_POINTER_SIZE) == 0);
            var index = 1 + (offset / TARGET_POINTER_SIZE);
            var mask = (1 << numBits) - 1;
            if ((index & mask) != index)
            {
                IMPL_LIMITATION("Cannot encode continuation offset in flags");
            }

            return index << firstBit;
        }

        private unsafe void CreateSuspension(BasicBlock callBlock, GenTreeCall call,
            BasicBlock suspension, int stateNumber, AsyncContinuationLayout layout,
            AsyncContinuationLayoutBuilder subLayout, bool resumeReachable,
            VARSET_TP mutatedSinceResumption)
        {
            var debugOffset = new GenTreeILOffset(call.GetAsyncInfo().CallAsyncDebugInfo);
            suspension.InsertAtEnd(LIR.SeqTree(_compiler, debugOffset));
            var record = new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, stateNumber);
            suspension.InsertAtEnd(record);

            var previous = _compiler.gtNewLclvNode(TYP_REF, GetReturnedContinuationVar());
            var allocation = CreateAllocContinuationCall(subLayout.NeedsKeepAlive, previous, layout);
            _compiler.compCurBB = suspension;
            _ = _compiler.fgMorphTree(allocation);
            suspension.InsertAtEnd(LIR.SeqTree(_compiler, allocation));
            var newContinuationLocal = GetNewContinuationVar();
            var storeContinuation = _compiler.gtNewStoreLclVarNode(newContinuationLocal, allocation);
            suspension.InsertAtEnd(storeContinuation);

            var saveSet = AsyncSaveSet.All;
            if (resumeReachable)
            {
                var allocate = _compiler.fgSplitBlockAfterNode(suspension, record);
                var reuse = _compiler.fgSplitBlockAtEnd(suspension);
                var tail = _compiler.fgSplitBlockAtEnd(allocate);
                _compiler.fgRemoveRefPred(reuse.TargetEdge);
                reuse.SetKindAndTargetEdge(BBJ_ALWAYS, _compiler.fgAddRefPred(tail, reuse));
                var allocateEdge = _compiler.fgAddRefPred(allocate, suspension);
                suspension.SetCond(suspension.TargetEdge, allocateEdge);
                suspension.TrueEdge.Likelihood = 1;
                suspension.FalseEdge.Likelihood = 0;
                JITDUMP($"Continuation reuse is active. Split suspendBB into suspendBB {FMT_BB(suspension.bbNum)} " +
                    $"-> reuseContinuationBlock {FMT_BB(reuse.bbNum)} -> allocNewBlock {FMT_BB(allocate.bbNum)} " +
                    $"-> suspendBBTail {FMT_BB(tail.bbNum)}\n");

                var reusable = _compiler.gtNewLclvNode(TYP_REF, _reuseContinuationVar);
                var storeReusable = _compiler.gtNewStoreLclVarNode(newContinuationLocal, reusable);
                suspension.InsertAtEnd(reusable);
                suspension.InsertAtEnd(storeReusable);

                var candidate = _compiler.gtNewLclvNode(TYP_REF, newContinuationLocal);
                var nullValue = _compiler.gtNewNull();
                var notNull = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, candidate, nullValue);
                var branch = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, notNull);
                suspension.InsertAtEnd(candidate);
                suspension.InsertAtEnd(nullValue);
                suspension.InsertAtEnd(notNull);
                suspension.InsertAtEnd(branch);

                var continuation = _compiler.gtNewLclvNode(TYP_REF, newContinuationLocal);
                var returned = _compiler.gtNewLclvNode(TYP_REF, GetReturnedContinuationVar());
                var offset = _compiler.info.compCompHnd->getFieldOffset(
                    _compiler.eeGetAsyncInfo().continuationNextFldHnd);
                var next = StoreAtOffset(returned, offset, continuation, TYP_REF);
                reuse.InsertAtEnd(LIR.SeqTree(_compiler, next));

                FillInDataOnSuspension(layout, subLayout, allocate,
                    mutatedSinceResumption, AsyncSaveSet.UnmutatedLocals);
                saveSet = AsyncSaveSet.MutatedLocals;
                suspension = tail;
            }

            var asyncInfo = _compiler.eeGetAsyncInfo();
            var newContinuation = _compiler.gtNewLclvNode(TYP_REF, newContinuationLocal);
            var resumeInfoOffset = _compiler.info.compCompHnd->getFieldOffset(
                asyncInfo.continuationResumeInfoFldHnd);
            var resumeInfo = new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, stateNumber);
            var storeResume = StoreAtOffset(newContinuation, resumeInfoOffset, resumeInfo, TYP_I_IMPL);
            suspension.InsertAtEnd(LIR.SeqTree(_compiler, storeResume));

            newContinuation = _compiler.gtNewLclvNode(TYP_REF, newContinuationLocal);
            var stateOffset = _compiler.info.compCompHnd->getFieldOffset(
                asyncInfo.continuationStateFldHnd);
            var stateValue = _compiler.gtNewIconNode(TYP_INT, stateNumber);
            var storeState = StoreAtOffset(newContinuation, stateOffset, stateValue, TYP_INT);
            suspension.InsertAtEnd(LIR.SeqTree(_compiler, storeState));

            var flags = 0;
            if (subLayout.NeedsExecutionContext)
            {
                flags |= EncodeContinuationIndex(layout.ExecutionContextOffset,
                    (int)CORINFO_CONTINUATION_EXECUTION_CONTEXT_INDEX_FIRST_BIT,
                    (int)CORINFO_CONTINUATION_EXECUTION_CONTEXT_INDEX_NUM_BITS);
            }

            if (subLayout.NeedsContinuationContext)
            {
                flags |= EncodeContinuationIndex(layout.ContinuationContextOffset,
                    (int)CORINFO_CONTINUATION_CONTEXT_INDEX_FIRST_BIT,
                    (int)CORINFO_CONTINUATION_CONTEXT_INDEX_NUM_BITS);
            }

            if (subLayout.NeedsException)
            {
                flags |= EncodeContinuationIndex(layout.ExceptionOffset,
                    (int)CORINFO_CONTINUATION_EXCEPTION_INDEX_FIRST_BIT,
                    (int)CORINFO_CONTINUATION_EXCEPTION_INDEX_NUM_BITS);
            }

            if (call._returnType is not TYP_VOID)
            {
                var result = layout.FindReturn(_compiler, call);
                flags |= EncodeContinuationIndex(result.Offset,
                    (int)CORINFO_CONTINUATION_RESULT_INDEX_FIRST_BIT,
                    (int)CORINFO_CONTINUATION_RESULT_INDEX_NUM_BITS);
            }

            var callInfo = call.GetAsyncInfo();
            if (callInfo.ContinuationContextHandling is
                ContinuationContextHandling.ContinueOnThreadPool)
            {
                flags |= (int)CORINFO_CONTINUATION_CONTINUE_ON_THREAD_POOL;
            }

            if (callInfo.IsValueTaskAsTask)
            {
                flags |= (int)CORINFO_CONTINUATION_VALUETASK_ADAPTED_TO_TASK;
            }

            newContinuation = _compiler.gtNewLclvNode(TYP_REF, newContinuationLocal);
            var flagsOffset = _compiler.info.compCompHnd->getFieldOffset(
                asyncInfo.continuationFlagsFldHnd);
            var flagsNode = _compiler.gtNewIconNode(TYP_INT, flags);
            var storeFlags = StoreAtOffset(newContinuation, flagsOffset, flagsNode, TYP_INT);
            suspension.InsertAtEnd(LIR.SeqTree(_compiler, storeFlags));

            FillInDataOnSuspension(layout, subLayout, suspension, mutatedSinceResumption, saveSet);
            StoreAsyncAwaiter(callBlock, call, suspension, layout);
            FinishContextHandlingAndSuspension(callBlock, call, suspension, layout, subLayout);
        }
    }
}
