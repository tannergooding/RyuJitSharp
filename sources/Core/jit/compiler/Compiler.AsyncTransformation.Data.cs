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
        private GenTreeIndir LoadFromOffset(GenTree baseNode, int offset, var_types type,
            GenTreeFlags flags = GTF_IND_NONFAULTING)
        {
            assert(baseNode.Type is TYP_REF or TYP_BYREF or TYP_I_IMPL);
            var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, offset);
            var addressType = baseNode.Type is TYP_I_IMPL ? TYP_I_IMPL : TYP_BYREF;
            var address = _compiler.gtNewBinaryNode(GT_ADD, addressType, baseNode, offsetNode);
            return _compiler.gtNewIndir(type, address, flags);
        }

        private GenTreeStoreInd StoreAtOffset(GenTree baseNode, int offset, GenTree value,
            var_types type, GenTreeFlags flags = GTF_IND_NONFAULTING)
        {
            assert(baseNode.Type is TYP_REF or TYP_BYREF or TYP_I_IMPL);
            var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, offset);
            var addressType = baseNode.Type is TYP_I_IMPL ? TYP_I_IMPL : TYP_BYREF;
            var address = _compiler.gtNewBinaryNode(GT_ADD, addressType, baseNode, offsetNode);
            return _compiler.gtNewStoreIndNode(type, address, value, flags);
        }

        private unsafe GenTreeCall CreateAllocContinuationCall(
            bool hasKeepAlive, GenTree previous, AsyncContinuationLayout layout)
        {
            var classHandle = _compiler.gtNewIconEmbClsHndNode(layout.ClassHnd);
            if (hasKeepAlive)
            {
                assert(layout.KeepAliveOffset >= 0);
                var context = _compiler.gtNewLclvNode(TYP_I_IMPL, _compiler.info.compTypeCtxtArg);
                var offset = OFFSETOF__CORINFO_Continuation__data -
                    SIZEOF__CORINFO_Object + layout.KeepAliveOffset;
                var offsetNode = _compiler.gtNewIconNode(TYP_INT, offset);
                var fromClass = (_compiler.info.compMethodInfo->options &
                    CORINFO_GENERICS_CTXT_FROM_METHODTABLE) != 0;
                var helper = fromClass ? CORINFO_HELP_ALLOC_CONTINUATION_CLASS :
                    CORINFO_HELP_ALLOC_CONTINUATION_METHOD;
                return _compiler.gtNewHelperCallNode(TYP_REF, helper,
                    previous, classHandle, offsetNode, context);
            }

            return _compiler.gtNewHelperCallNode(TYP_REF, CORINFO_HELP_ALLOC_CONTINUATION,
                previous, classHandle);
        }

        private void StoreAsyncAwaiter(
            BasicBlock callBlock, GenTreeCall call, BasicBlock suspension, AsyncContinuationLayout layout)
        {
            var argument = call.Args.FindWellKnownArg(WellKnownArg.AsyncAwaiter);
            if (argument is null)
            {
                return;
            }

            var awaiterLayout = argument.SignatureLayout ??
                throw new InvalidOperationException("Async awaiter has no signature layout");
            if (!_compiler.TryGetContinuationMemberIndex(
                ContinuationMember.CustomAwaiterOfLayout(awaiterLayout), out var index) ||
                (layout.ContinuationMemberOffsets[index] < 0))
            {
                throw new InvalidOperationException("Async awaiter has no allocated continuation member");
            }

            var awaiter = argument.Node;
            assert(awaiter.Type is TYP_STRUCT);
            var alignment = awaiterLayout.GetAlignmentRequirement(_compiler);
            var isStructAligned = Math.Min(alignment, TARGET_POINTER_SIZE) == alignment;

            if (awaiter is GenTreeFieldList fieldList)
            {
                foreach (var use in fieldList.Uses)
                {
                    if (!use.Node.IsInvariant && (use.Node.Oper is not GT_LCL_VAR))
                    {
                        var edge = new LIR.Use(callBlock, ref use.NodeRef, fieldList);
                        _ = edge.ReplaceWithLclVar(_compiler);
                    }

                    var field = use.Node;
                    callBlock.Remove(field);
                    var isAligned = isStructAligned && ((use.Offset % use.Type.Size) == 0);
                    var flags = GTF_IND_NONFAULTING | (isAligned ? GTF_EMPTY : GTF_IND_UNALIGNED);
                    var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
                    var offset = OFFSETOF__CORINFO_Continuation__data +
                        layout.ContinuationMemberOffsets[index] + use.Offset;
                    var store = StoreAtOffset(continuation, offset, field, use.Type, flags);
                    suspension.InsertAtEnd(LIR.SeqTree(_compiler, store));
                }

                callBlock.Remove(fieldList);
            }
            else
            {
                if (awaiter.Oper is not GT_LCL_VAR)
                {
                    var use = new LIR.Use(callBlock, ref argument.NodeRef, call);
                    _ = use.ReplaceWithLclVar(_compiler);
                    awaiter = use.Def();
                }

                callBlock.Remove(awaiter);
                var flags = GTF_IND_NONFAULTING |
                    (isStructAligned ? GTF_EMPTY : GTF_IND_UNALIGNED);
                var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
                var offset = OFFSETOF__CORINFO_Continuation__data +
                    layout.ContinuationMemberOffsets[index];
                var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, offset);
                var address = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, continuation, offsetNode);
                var store = _compiler.gtNewStoreValueNode(address, awaiter, awaiterLayout, flags);
                suspension.InsertAtEnd(LIR.SeqTree(_compiler, store));
            }

            call.Args.RemoveUnsafe(argument);
        }

        private void FillInDataOnSuspension(AsyncContinuationLayout layout,
            AsyncContinuationLayoutBuilder subLayout, BasicBlock block,
            VARSET_TP mutatedSinceResumption, AsyncSaveSet saveSet)
        {
            if (saveSet is not AsyncSaveSet.MutatedLocals &&
                (_compiler.MethodHasPatchpoint || _compiler.opts.IsOSR))
            {
                var osrAddress = _compiler.MethodHasPatchpoint
                    ? _compiler.gtNewIconNode(TYP_I_IMPL, 0)
                    : new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);
                assert(layout.OSRAddressOffset == 0);
                var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
                var store = StoreAtOffset(continuation,
                    OFFSETOF__CORINFO_Continuation__data, osrAddress, TYP_I_IMPL);
                block.InsertAtEnd(LIR.SeqTree(_compiler, store));
            }

            foreach (var local in layout.Locals)
            {
                if (!subLayout.ContainsLocal(local.LclNum))
                {
                    continue;
                }

                ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
                if ((saveSet is not AsyncSaveSet.All) &&
                    (GetLocalSaveSet(in descriptor, mutatedSinceResumption) != saveSet))
                {
                    continue;
                }

                var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
                var offset = OFFSETOF__CORINFO_Continuation__data + local.Offset;
                GenTree value;
                if (descriptor.IsImplicitByRef)
                {
                    var address = _compiler.gtNewLclvNode(descriptor.Type, local.LclNum);
                    value = _compiler.gtNewLoadValueNode(address,
                        descriptor.Layout ?? throw new InvalidOperationException("Implicit-byref local has no layout"),
                        GTF_IND_NONFAULTING);
                }
                else
                {
                    value = _compiler.gtNewLclVarNode(descriptor.Type, local.LclNum);
                }

                var flags = GTF_IND_NONFAULTING |
                    (local.HeapAlignment < local.Alignment ? GTF_IND_UNALIGNED : GTF_EMPTY);
                GenTree store;
                if ((descriptor.Type is TYP_STRUCT) || descriptor.IsImplicitByRef)
                {
                    var offsetNode = _compiler.gtNewIconNode(TYP_I_IMPL, offset);
                    var address = _compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                        continuation, offsetNode);
                    store = _compiler.gtNewStoreValueNode(address, value,
                        descriptor.Layout ?? throw new InvalidOperationException("Struct local has no layout"),
                        flags);
                }
                else
                {
                    store = StoreAtOffset(continuation, offset, value, descriptor.Type, flags);
                }

                block.InsertAtEnd(LIR.SeqTree(_compiler, store));
                _compiler.compLongUsed |= descriptor.Type is TYP_LONG;
            }
        }

        private void CreateDebugInfoForSuspensionPoint(
            AsyncContinuationLayout layout, AsyncContinuationLayoutBuilder subLayout)
        {
            var vars = _compiler.compAsyncVars ??
                throw new InvalidOperationException("Async continuation variable list has not been created");
            var points = _compiler.compSuspensionPoints ??
                throw new InvalidOperationException("Async suspension point list has not been created");
            var numLocals = 0;

            foreach (var local in layout.Locals)
            {
                if (!subLayout.ContainsLocal(local.LclNum))
                {
                    continue;
                }

                var ilVarNum = _compiler.compMap2ILvarNum(local.LclNum);
                if (ilVarNum == ICorDebugInfo.UNKNOWN_ILNUM)
                {
                    continue;
                }

                vars.Add(new ICorDebugInfo.AsyncContinuationVarInfo {
                    VarNumber = ilVarNum,
                    Offset = OFFSETOF__CORINFO_Continuation__data + local.Offset,
                });
                numLocals++;
            }

            points.Add(new ICorDebugInfo.AsyncSuspensionPoint {
                DiagnosticNativeOffset = 0,
                NumContinuationVars = numLocals,
            });
        }
    }
}
