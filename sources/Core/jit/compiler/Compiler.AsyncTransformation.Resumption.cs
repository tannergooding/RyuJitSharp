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
        private void RestoreFromDataOnResumption(AsyncContinuationLayout layout,
            AsyncContinuationLayoutBuilder subLayout, BasicBlock block)
        {
            foreach (var local in layout.Locals)
            {
                if (!subLayout.ContainsLocal(local.LclNum))
                {
                    continue;
                }

                ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
                var continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var offset = OFFSETOF__CORINFO_Continuation__data + local.Offset;
                var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, offset);
                var address = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, continuation, offsetNode);
                var flags = GTF_IND_NONFAULTING |
                    (local.HeapAlignment < local.Alignment ? GTF_IND_UNALIGNED : GTF_EMPTY);
                GenTree value;
                if ((descriptor.Type is TYP_STRUCT) || descriptor.IsImplicitByRef)
                {
                    value = _compiler.gtNewLoadValueNode(address,
                        descriptor.Layout ?? throw new InvalidOperationException("Struct local has no layout"),
                        flags);
                }
                else
                {
                    value = _compiler.gtNewIndir(descriptor.Type, address, flags);
                }

                GenTree store;
                if (descriptor.IsImplicitByRef)
                {
                    var baseAddress = _compiler.gtNewLclvNode(descriptor.Type, local.LclNum);
                    store = _compiler.gtNewStoreValueNode(baseAddress, value,
                        descriptor.Layout ?? throw new InvalidOperationException("Implicit-byref local has no layout"),
                        GTF_IND_NONFAULTING | GTF_IND_TGT_NOT_HEAP);
                }
                else
                {
                    store = _compiler.gtNewStoreLclVarNode(local.LclNum, value);
                }

                block.InsertAtEnd(LIR.SeqTree(_compiler, store));
            }

            if (subLayout.NeedsKeepAlive)
            {
                var continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var keepAlive = _compiler.gtNewKeepAliveNode(continuation);
                block.InsertAtEnd(LIR.SeqTree(_compiler, keepAlive));
            }
        }

        private void StoreResumedDef(BasicBlock callBlock, GenTreeCall call, BasicBlock resumeBlock)
        {
            var argument = call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedDef);
            if (argument is null)
            {
                return;
            }

            var definition = _compiler.gtCallGetDefinedAsyncResumedLclAddr(call) ??
                throw new InvalidOperationException("Async resumed-def argument must be a local address");
            assert(ReferenceEquals(argument.Node, definition));
            StoreResumedDef(definition, resumeBlock);
            callBlock.Remove(definition);
            call.Args.RemoveUnsafe(argument);
        }

        private void StoreResumedDef(GenTreeLclVarCommon definition, BasicBlock block)
        {
#if DEBUG
            JITDUMP($"  Have resume def [{definition.TreeId:D6}] to store to\n");
#endif
            ref var descriptor = ref _compiler.lvaGetDesc(definition.LclNum);
            var one = _compiler.gtNewIconNode(TYP_I_IMPL, 1);
            GenTree store;
            if ((definition.LclOffs == 0) && (descriptor.Type is TYP_I_IMPL))
            {
                store = _compiler.gtNewStoreLclVarNode(definition.LclNum, one);
            }
            else
            {
                store = _compiler.gtNewStoreLclFldNode(TYP_I_IMPL,
                    definition.LclNum, definition.LclOffs, one);
                _compiler.lvaSetVarDoNotEnregister(definition.LclNum, DoNotEnregisterReason.LocalField);
            }

            if (block.HasTerminator)
            {
                block.InsertBefore(block.LastNode ??
                    throw new InvalidOperationException("Terminator block is empty"),
                    LIR.SeqTree(_compiler, store));
            }
            else
            {
                block.InsertAtEnd(LIR.SeqTree(_compiler, store));
            }

#if DEBUG
            JITDUMP($"  Created store [{store.TreeId:D6}] to set resumed def to 1\n");
#endif
        }

        private BasicBlock RethrowExceptionOnResumption(
            BasicBlock callBlock, AsyncContinuationLayout layout, BasicBlock resumeBlock)
        {
            JITDUMP("  We need to rethrow an exception\n");
            var rethrowBlock = _compiler.fgNewBBafter(BBJ_THROW, callBlock, true);
            JITDUMP($"  Created {FMT_BB(rethrowBlock.bbNum)} to rethrow exception on resumption\n");
            if (!callBlock.HasFlag(BBF_INTERNAL))
            {
                rethrowBlock.RemoveFlags(BBF_INTERNAL);
                rethrowBlock.SetFlags(BBF_IMPORTED);
            }

            var resultBlock = _compiler.fgNewBBafter(BBJ_ALWAYS, resumeBlock, true);
            JITDUMP($"  Created {FMT_BB(resultBlock.bbNum)} to store result when resuming with no exception\n");
            var rethrowEdge = _compiler.fgAddRefPred(rethrowBlock, resumeBlock);
            var resultEdge = _compiler.fgAddRefPred(resultBlock, resumeBlock);

            assert(resumeBlock.Kind is BBJ_ALWAYS);
            var remainder = resumeBlock.Target;
            _compiler.fgRemoveRefPred(resumeBlock.TargetEdge);

            resumeBlock.SetCond(rethrowEdge, resultEdge);
            rethrowEdge.Likelihood = 0;
            resultEdge.Likelihood = 1;
            rethrowBlock.inheritWeightPercentage(resumeBlock, 0);
            resultBlock.inheritWeightPercentage(resumeBlock, 100);
            JITDUMP($"  Resumption {FMT_BB(resumeBlock.bbNum)} becomes BBJ_COND to check for non-null exception\n");
            resultBlock.SetKindAndTargetEdge(BBJ_ALWAYS, _compiler.fgAddRefPred(remainder, resultBlock));
            _lastResumptionBB = resultBlock;

            var exceptionLocal = GetExceptionVar();
            var continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
            var offset = OFFSETOF__CORINFO_Continuation__data + layout.ExceptionOffset;
            var load = LoadFromOffset(continuation, offset, TYP_REF);
            var storeException = _compiler.gtNewStoreLclVarNode(exceptionLocal, load);
            resumeBlock.InsertAtEnd(LIR.SeqTree(_compiler, storeException));

            continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
            var clear = StoreAtOffset(continuation, offset, _compiler.gtNewNull(), TYP_REF);
            resumeBlock.InsertAtEnd(LIR.SeqTree(_compiler, clear));

            var exception = _compiler.gtNewLclvNode(TYP_REF, exceptionLocal);
            var nullValue = _compiler.gtNewNull();
            var nonNull = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, exception, nullValue);
            var branch = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, nonNull);
            resumeBlock.InsertAtEnd(exception);
            resumeBlock.InsertAtEnd(nullValue);
            resumeBlock.InsertAtEnd(nonNull);
            resumeBlock.InsertAtEnd(branch);

            exception = _compiler.gtNewLclvNode(TYP_REF, exceptionLocal);
            var throwCall = _compiler.gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_THROWEXACT, exception);
            _compiler.compCurBB = rethrowBlock;
            _ = _compiler.fgMorphTree(throwCall);
            rethrowBlock.InsertAtEnd(LIR.SeqTree(_compiler, throwCall));
            resultBlock.SetFlags(BBF_ASYNC_RESUMPTION);
            JITDUMP($"  Added {FMT_BB(rethrowBlock.bbNum)} to rethrow exception at suspension point\n");
            return resultBlock;
        }

        private unsafe void CopyReturnValueOnResumption(GenTreeCall call,
            AsyncCallDefinitionInfo definition, AsyncContinuationLayout layout, BasicBlock block)
        {
            var resultInfo = layout.FindReturn(_compiler, call);
            GenTree resultBase = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
            var offset = OFFSETOF__CORINFO_Continuation__data + resultInfo.Offset;
            var target = definition.DefinitionNode ??
                throw new InvalidOperationException("Async result requires a local definition");
            ref var destination = ref _compiler.lvaGetDesc(target.LclNum);
            var flags = GTF_IND_NONFAULTING |
                (resultInfo.HeapAlignment < resultInfo.Alignment ? GTF_IND_UNALIGNED : GTF_EMPTY);

            if (call._returnType is TYP_STRUCT)
            {
                if (_compiler.lvaGetPromotionType(in destination) is not PROMOTION_TYPE_INDEPENDENT)
                {
                    var structLayout = resultInfo.Type.ReturnLayout ??
                        throw new InvalidOperationException("Struct return has no layout");
                    var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, offset);
                    var address = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, resultBase, offsetNode);
                    var value = _compiler.gtNewLoadValueNode(address, structLayout, flags);
                    GenTree store;
                    if ((target.LclOffs == 0) &&
                        ClassLayout.AreCompatible(destination.Layout, structLayout))
                    {
                        store = _compiler.gtNewStoreLclVarNode(target.LclNum, value);
                    }
                    else
                    {
                        store = _compiler.gtNewStoreLclFldNode(TYP_STRUCT,
                            target.LclNum, target.LclOffs, value, structLayout);
                    }

                    block.InsertAtEnd(LIR.SeqTree(_compiler, store));
                }
                else
                {
                    assert(call.Args.RetBufferArg is null);
                    if ((destination.lvFieldCnt > 1) && !resultBase.Oper.IsAnyLocal)
                    {
                        var resultLocal = GetResultBaseVar();
                        var storeBase = _compiler.gtNewStoreLclVarNode(resultLocal, resultBase);
                        block.InsertAtEnd(LIR.SeqTree(_compiler, storeBase));
                        resultBase = _compiler.gtNewLclvNode(TYP_REF, resultLocal);
                    }

                    assert(target.Oper is GT_STORE_LCL_VAR);
                    ref var resultDescriptor = ref _compiler.lvaGetDesc(target.LclNum);
                    var count = resultDescriptor.lvFieldCnt;
                    var firstField = resultDescriptor.lvFieldLclStart;
                    for (var i = 0; i < count; i++)
                    {
                        ref var field = ref _compiler.lvaGetDesc(firstField + i);
                        var value = LoadFromOffset(resultBase, offset + field.lvFldOffset,
                            field.Type, flags);
                        var store = _compiler.gtNewStoreLclVarNode(firstField + i, value);
                        block.InsertAtEnd(LIR.SeqTree(_compiler, store));

                        if (i + 1 != count)
                        {
                            resultBase = _compiler.gtCloneExpr(resultBase);
                        }
                    }
                }
            }
            else
            {
                var value = LoadFromOffset(resultBase, offset, call._returnType, flags);
                GenTree store = target.Oper is GT_STORE_LCL_VAR
                    ? _compiler.gtNewStoreLclVarNode(target.LclNum, value)
                    : _compiler.gtNewStoreLclFldNode(target.Type, target.LclNum, target.LclOffs, value);
                block.InsertAtEnd(LIR.SeqTree(_compiler, store));
            }

            ClearReturnValueOnResumption(resultInfo, offset, block);
        }

        private void ClearReturnValueOnResumption(
            AsyncReturnInfo result, int resultOffset, BasicBlock block)
        {
            void ClearReference(int offset, var_types type)
            {
                var continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                var zero = _compiler.gtNewZeroConNode(type);
                var clear = StoreAtOffset(continuation, offset, zero, type);
                block.InsertAtEnd(LIR.SeqTree(_compiler, clear));
            }

            if (result.Type.ReturnType is TYP_STRUCT)
            {
                var structLayout = result.Type.ReturnLayout ??
                    throw new InvalidOperationException("Struct return has no layout");
                if (structLayout.GCPtrCount == 0)
                {
                    return;
                }

                var firstSlot = 0;
                while (!structLayout.IsGCPtr(firstSlot))
                {
                    firstSlot++;
                }

                var lastSlot = structLayout.SlotCount - 1;
                while (!structLayout.IsGCPtr(lastSlot))
                {
                    lastSlot--;
                }

                var slotCount = lastSlot - firstSlot + 1;
                if ((structLayout.GCPtrCount <= 4) && (structLayout.GCPtrCount * 2 <= slotCount))
                {
                    for (var i = firstSlot; i <= lastSlot; i++)
                    {
                        if (structLayout.IsGCPtr(i))
                        {
                            ClearReference(resultOffset + (i * TARGET_POINTER_SIZE),
                                structLayout.GetGCPtrType(i));
                        }
                    }
                }
                else
                {
                    var sliceOffset = firstSlot * TARGET_POINTER_SIZE;
                    var sliceLayout = structLayout.SliceLayout(_compiler,
                        sliceOffset, slotCount * TARGET_POINTER_SIZE);
                    var continuation = _compiler.gtNewLclvNode(TYP_REF, _compiler.lvaAsyncContinuationArg);
                    var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, resultOffset + sliceOffset);
                    var address = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                        continuation, offsetNode);
                    var flags = GTF_IND_NONFAULTING |
                        (result.HeapAlignment < result.Alignment ? GTF_IND_UNALIGNED : GTF_EMPTY);
                    var zero = _compiler.gtNewIconNode(TYP_INT, 0);
                    var store = _compiler.gtNewStoreValueNode(address, zero, sliceLayout, flags);
                    block.InsertAtEnd(LIR.SeqTree(_compiler, store));
                }
            }
            else if (result.Type.ReturnType is TYP_REF or TYP_BYREF)
            {
                ClearReference(resultOffset, result.Type.ReturnType);
            }
        }
    }
}
