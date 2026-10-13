// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe void InsertPInvokeMethodProlog()
    {
        var compiler = CompilerInstance;
        noway_assert(compiler.info.compUnmanagedCallCountWithGCTransition != 0);
        noway_assert(compiler.lvaInlinedPInvokeFrameVar != BAD_VAR_NUM);

        var hasMDContextArg = compiler.info.compIsVarArgs && compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB);
        if (!hasMDContextArg && compiler.opts.ShouldUsePInvokeHelpers)
        {
            return;
        }

        JITDUMP("======= Inserting PInvoke method prolog\n");

        var firstBlockRange = compiler.fgFirstBB;
        assert(firstBlockRange is not null);
        ref var frameInfo = ref compiler.eeGetEEInfo().inlinedCallFrameInfo;
        assert(compiler.lvaGetDesc(compiler.lvaInlinedPInvokeFrameVar).IsAddressExposed);
        var insertionPoint = firstBlockRange.FirstNonCatchArgNode();

        if (hasMDContextArg)
        {
            assert(compiler.compHasSecretStubArgument());
            var value = compiler.gtNewLclvNode(TYP_I_IMPL, compiler.lvaGetSecretStubArgumentVar());
            var secretArg = compiler.gtNewStoreLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar,
                checked((ushort)frameInfo.offsetOfSecretStubArg), value);
            firstBlockRange.InsertBefore(insertionPoint, LIR.SeqTree(compiler, secretArg));
            DISPTREERANGE(firstBlockRange, secretArg);
        }

        if (compiler.opts.ShouldUsePInvokeHelpers)
        {
            return;
        }

        var frameAddress = compiler.gtNewLclVarAddrNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar);
        var helper = compiler.gtNewHelperCallNode(TYP_I_IMPL, CORINFO_HELP_INIT_PINVOKE_FRAME);
        _ = helper.Args.PushBack(NewCallArg.CreateForPrimitive(frameAddress).WithWellKnownArg(WellKnownArg.PInvokeFrame));

        var rootLocal = compiler.info.compLvFrameListRoot;
        var rootDesc = compiler.lvaGetDesc(rootLocal);
        noway_assert(!rootDesc.lvIsParam);
        noway_assert((rootDesc.Type == TYP_I_IMPL));

        var rootStore = compiler.gtNewStoreLclVarNode(rootLocal, helper);
        var morphedStore = compiler.fgMorphTree(rootStore);
        firstBlockRange.InsertBefore(insertionPoint, LIR.SeqTree(compiler, morphedStore));
        DISPTREERANGE(firstBlockRange, morphedStore);

        if (!Configuration.TARGET_X86 && !Configuration.TARGET_ARM)
        {
            var stackPointer = compiler.gtNewPhysRegNode(
                TargetRuntimeMetadata.StackPointerBaseRegister, TYP_I_IMPL);
            var stackPointerStore = compiler.gtNewStoreLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar,
                checked((ushort)frameInfo.offsetOfCallSiteSP), stackPointer);
            firstBlockRange.InsertBefore(insertionPoint, LIR.SeqTree(compiler, stackPointerStore));
            DISPTREERANGE(firstBlockRange, stackPointerStore);
        }

        if (!Configuration.TARGET_ARM)
        {
            var framePointer = compiler.gtNewPhysRegNode(
                TargetRuntimeMetadata.FramePointerBaseRegister, TYP_I_IMPL);
            var framePointerStore = compiler.gtNewStoreLclFldNode(TYP_I_IMPL, compiler.lvaInlinedPInvokeFrameVar,
                checked((ushort)frameInfo.offsetOfCalleeSavedFP), framePointer);
            firstBlockRange.InsertBefore(insertionPoint, LIR.SeqTree(compiler, framePointerStore));
            DISPTREERANGE(firstBlockRange, framePointerStore);
        }

        if (Configuration.TARGET_64BIT)
        {
            if (!Configuration.USE_PER_FRAME_PINVOKE_INIT ||
                compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB))
            {
                var link = CreateFrameLinkUpdate(FrameLinkAction.PushFrame);
                firstBlockRange.InsertBefore(insertionPoint, LIR.SeqTree(compiler, link));
                ContainCheckStoreIndir(link);
                DISPTREERANGE(firstBlockRange, link);
            }
        }
    }

    private unsafe void InsertPInvokeMethodEpilog(BasicBlock returnBlock, GenTree? lastExpr)
    {
        var compiler = CompilerInstance;
        assert(returnBlock is not null);
        assert(compiler.info.compUnmanagedCallCountWithGCTransition != 0);

        if (compiler.opts.ShouldUsePInvokeHelpers)
        {
            return;
        }

        JITDUMP("======= Inserting PInvoke method epilog\n");
        assert((returnBlock.Kind is BBJ_RETURN) || returnBlock.EndsWithTailCallOrJmp(compiler));

        var insertionPoint = returnBlock.LastNode;
        assert(insertionPoint == lastExpr);

        if (!Configuration.USE_PER_FRAME_PINVOKE_INIT ||
            compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB))
        {
            var link = CreateFrameLinkUpdate(FrameLinkAction.PopFrame);
            returnBlock.InsertBefore(insertionPoint, LIR.SeqTree(compiler, link));
            ContainCheckStoreIndir(link);
        }
    }
}
