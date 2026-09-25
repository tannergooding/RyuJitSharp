// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private enum FrameLinkAction
    {
        PushFrame,
        PopFrame,
    }

    private void InsertTreeBeforeAndContainCheck(GenTree insertionPoint, GenTree tree)
    {
        var range = LIR.SeqTree(CompilerInstance, tree);
        ContainCheckRange(range);
        BlockRange().InsertBefore(insertionPoint, range);
    }

    private unsafe GenTreeUnOp CreateReturnTrapSeq()
    {
        void* indirection = null;
        var address = CompilerInstance.info.compCompHnd->getAddrOfCaptureThreadGlobal(&indirection);
        var test = address is not null ? (GenTree)AddrGen((nint)address) : Ind(AddrGen((nint)indirection));
        return new GenTreeUnOp(GT_RETURNTRAP, TYP_INT, Ind(test, TYP_INT));
    }

    private GenTreeStoreInd SetGCState(int state)
    {
        assert(state is 0 or 1);
        var compiler = CompilerInstance;
        var thread = compiler.gtNewLclvNode(TYP_I_IMPL, compiler.info.compLvFrameListRoot);
        var address = new GenTreeAddrMode(TYP_I_IMPL, thread, null, 1, compiler.eeGetEEInfo().offsetOfGCState);
        var value = new GenTreeIntCon(TYP_BYTE, state);
        return new GenTreeStoreInd(TYP_BYTE, address, value);
    }

    private GenTreeStoreInd CreateFrameLinkUpdate(FrameLinkAction action)
    {
        var compiler = CompilerInstance;
        ref var info = ref compiler.eeGetEEInfo();
        var thread = compiler.gtNewLclvNode(TYP_I_IMPL, compiler.info.compLvFrameListRoot);
        var address = new GenTreeAddrMode(TYP_I_IMPL, thread, null, 1, info.offsetOfThreadFrame);
        assert(action is FrameLinkAction.PushFrame or FrameLinkAction.PopFrame);
        var value = action is FrameLinkAction.PushFrame
            ? compiler.gtNewLclVarAddrNode(TYP_BYREF, compiler.lvaInlinedPInvokeFrameVar)
            : compiler.gtNewLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar,
                checked((ushort)info.inlinedCallFrameInfo.offsetOfFrameLink));
        return compiler.gtNewStoreIndNode(TYP_I_IMPL, address, value);
    }

    private unsafe void InsertPInvokeCallProlog(GenTreeCall call)
    {
        JITDUMP("======= Inserting PInvoke call prolog\n");
        var compiler = CompilerInstance;
        GenTree insertionPoint = call;
        if (call._callType is CT_INDIRECT)
        {
            var control = call._controlExpr;
            assert(control is not null);
            var range = BlockRange().GetTreeRange(control, out var isClosed);
            assert(isClosed);
            insertionPoint = range.FirstNode ?? throw new InvalidOperationException("Indirect P/Invoke target is not in LIR.");
        }

        ref var frameInfo = ref compiler.eeGetEEInfo().inlinedCallFrameInfo;
        noway_assert(compiler.lvaInlinedPInvokeFrameVar != BAD_VAR_NUM);
        if (compiler.opts.ShouldUsePInvokeHelpers)
        {
            var frameAddress = compiler.gtNewLclVarAddrNode(TYP_BYREF, compiler.lvaInlinedPInvokeFrameVar);
            var helper = compiler.gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_JIT_PINVOKE_BEGIN, frameAddress);
            _ = compiler.fgMorphTree(helper);
            BlockRange().InsertBefore(insertionPoint, LIR.SeqTree(compiler, helper));
            _ = LowerNode(helper);
            return;
        }

        GenTree target;
        if (call._callType is CT_INDIRECT)
        {
            target = compiler.gtNewIconNode(TYP_I_IMPL, 0);
        }
        else
        {
            assert(call._callType is CT_USER_FUNC);
            void* indirectHandle = null;
            var embeddedHandle = compiler.info.compCompHnd->embedMethodHandle(call._callMethHnd, &indirectHandle);
#pragma warning disable CA1508 // The native callback can write through indirectHandle.
            noway_assert((embeddedHandle is null) != (indirectHandle is null));
#pragma warning restore CA1508
            target = embeddedHandle is not null
                ? AddrGen((nint)embeddedHandle)
                : Ind(AddrGen((nint)indirectHandle));
        }

        var targetStore = compiler.gtNewStoreLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar,
            checked((ushort)frameInfo.offsetOfCallTarget), target);
        InsertTreeBeforeAndContainCheck(insertionPoint, targetStore);

        var label = new GenTree(GT_LABEL, TYP_I_IMPL);
        var returnAddressStore = compiler.gtNewStoreLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar,
            checked((ushort)frameInfo.offsetOfReturnAddress), label);
        InsertTreeBeforeAndContainCheck(insertionPoint, returnAddressStore);

#if USE_PER_FRAME_PINVOKE_INIT
        if (!compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB))
        {
            var link = CreateFrameLinkUpdate(FrameLinkAction.PushFrame);
            BlockRange().InsertBefore(insertionPoint, LIR.SeqTree(compiler, link));
            ContainCheckStoreIndir(link);
        }
#endif

        var gcState = SetGCState(0);
        BlockRange().InsertBefore(insertionPoint, LIR.SeqTree(compiler, gcState));
        ContainCheckStoreIndir(gcState);
        BlockRange().InsertBefore(insertionPoint, new GenTree(GT_START_PREEMPTGC, TYP_VOID));
    }

    private unsafe void InsertPInvokeCallEpilog(GenTreeCall call)
    {
        JITDUMP("======= Inserting PInvoke call epilog\n");
        var compiler = CompilerInstance;
        if (compiler.opts.ShouldUsePInvokeHelpers)
        {
            noway_assert(compiler.lvaInlinedPInvokeFrameVar != BAD_VAR_NUM);
            var frameAddress = compiler.gtNewLclVarAddrNode(TYP_BYREF, compiler.lvaInlinedPInvokeFrameVar);
#if DEBUG
            assert(compiler.lvaGetDesc(compiler.lvaInlinedPInvokeFrameVar).IsAddressExposed);
#endif
            var helper = compiler.gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_JIT_PINVOKE_END, frameAddress);
            _ = compiler.fgMorphTree(helper);
            BlockRange().InsertAfter(call, LIR.SeqTree(compiler, helper));
            ContainCheckCallOperands(helper);
            return;
        }

        var insertionPoint = call.Next;
        var gcState = SetGCState(1);
        BlockRange().InsertBefore(insertionPoint, LIR.SeqTree(compiler, gcState));
        ContainCheckStoreIndir(gcState);

        var returnTrap = CreateReturnTrapSeq();
        BlockRange().InsertBefore(insertionPoint, LIR.SeqTree(compiler, returnTrap));
        ContainCheckNode(returnTrap);

#if USE_PER_FRAME_PINVOKE_INIT
        if (!compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB))
        {
            var link = CreateFrameLinkUpdate(FrameLinkAction.PopFrame);
            BlockRange().InsertBefore(insertionPoint, LIR.SeqTree(compiler, link));
            ContainCheckStoreIndir(link);
        }
#else
        var zero = compiler.gtNewIconNode(TYP_I_IMPL, 0);
        var offset = checked((ushort)compiler.eeGetEEInfo().inlinedCallFrameInfo.offsetOfReturnAddress);
        var clearCallSite = compiler.gtNewStoreLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar, offset, zero);
        BlockRange().InsertBefore(insertionPoint, zero, clearCallSite);
        ContainCheckStoreLoc(clearCallSite);
#endif
    }

    private unsafe GenTree? LowerNonvirtPinvokeCall(GenTreeCall call)
    {
#if !TARGET_AMD64
        throw new NotImplementedException("Nonvirtual P/Invoke lowering outside AMD64 is not ported.");
#else
        var compiler = CompilerInstance;
        var needsTransition = !call.IsSuppressGCTransition;
        if (needsTransition)
        {
            InsertPInvokeCallProlog(call);
        }

        GenTree? result = null;
        if (call._callType is not CT_INDIRECT)
        {
            noway_assert(call._callType is CT_USER_FUNC);
            CORINFO_CONST_LOOKUP lookup;
            compiler.info.compCompHnd->getAddressOfPInvokeTarget(call._callMethHnd, &lookup);
            var address = lookup.addr;
            switch (lookup.accessType)
            {
                case IAT_VALUE:
                {
                    if (!compiler.IsAot || !IsCallTargetInRange(address))
                    {
                        result = AddrGen((nint)address);
                    }
                    else
                    {
                        call._directCallAddress = address;
#if FEATURE_READYTORUN
                        call._entryPoint.addr = null;
                        call._entryPoint.accessType = IAT_VALUE;
#endif
                    }
                    break;
                }

                case IAT_PVALUE:
                {
                    var cell = AddrGen((nint)address);
#if DEBUG
                    cell.TargetHandle = (nint)call._callMethHnd;
#endif
                    result = Ind(cell);
                    break;
                }

                case IAT_PPVALUE:
                {
                    var cell = AddrGen((nint)address);
#if DEBUG
                    cell.TargetHandle = (nint)call._callMethHnd;
#endif
                    result = Ind(Ind(cell));
                    break;
                }

                case IAT_RELPVALUE:
                {
                    throw new InvalidOperationException("Relative P/Invoke target lookup is unsupported by the native contract.");
                }

                default:
                {
                    throw new InvalidOperationException("Invalid P/Invoke target access type.");
                }
            }
        }

        if (needsTransition)
        {
            InsertPInvokeCallEpilog(call);
        }
        return result;
#endif
    }
}
