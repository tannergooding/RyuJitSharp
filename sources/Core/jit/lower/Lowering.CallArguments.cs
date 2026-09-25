// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerArg(GenTreeCall call, CallArg callArg)
    {
#if TARGET_XARCH
        ref var argSlot = ref callArg.NodeRef;
        var arg = argSlot;
        assert(arg is not null);
        JITDUMP("Lowering arg: \n");
        DISPTREERANGE(BlockRange(), arg);
        assert(arg.IsValue);
        assert(!arg.Oper.IsPutArg);

        ref var abiInfo = ref callArg.AbiInfo;
        JITDUMP("Passed in ");
#if DEBUG
        if (CompilerInstance.verbose)
        {
            abiInfo.Dump();
        }
#endif

#if TARGET_X86
        if (CompilerInstance.opts.compUseSoftFP && (arg.Type is TYP_DOUBLE))
        {
            // Doubles remain primitive until lowering, unlike decomposed integer longs.
            var argLclNum = CompilerInstance.lvaGrabTemp(false, "double arg on softFP");
            var store = CompilerInstance.gtNewTempStore(argLclNum, arg);
            var low = CompilerInstance.gtNewLclFldNode(TYP_INT, argLclNum, 0);
            var high = CompilerInstance.gtNewLclFldNode(TYP_INT, argLclNum, 4);
            var longNode = new GenTreeOp(GT_LONG, TYP_LONG, low, high);
            BlockRange().InsertAfter(arg, store, low, high, longNode);
            argSlot = arg = longNode;
            CompilerInstance.lvaSetVarDoNotEnregister(argLclNum, DoNotEnregisterReason.LocalField);
            JITDUMP("Transformed double-typed arg on softFP to LONG node\n");
        }

        if (varTypeIsLong(arg.Type))
        {
            noway_assert(arg.Oper is GT_LONG);
            var fieldList = new GenTreeFieldList();
            fieldList.AddFieldLIR(CompilerInstance, arg.AsOp().Op1, 0, TYP_INT);
            fieldList.AddFieldLIR(CompilerInstance, arg.AsOp().Op2, 4, TYP_INT);
            BlockRange().InsertBefore(arg, fieldList);
            BlockRange().Remove(arg);
            argSlot = arg = fieldList;
            JITDUMP("Transformed long arg on 32-bit to FIELD_LIST node\n");
        }
#endif

        if (abiInfo.HasAnyRegisterSegment)
        {
            if ((arg.Oper is GT_FIELD_LIST) || (abiInfo.NumSegments > 1))
            {
                if (arg.Oper is not GT_FIELD_LIST)
                {
                    // Some ABIs split primitive values across multiple registers.
                    var fieldList = new GenTreeFieldList();
                    fieldList.AddFieldLIR(CompilerInstance, arg, 0, arg.Type.ActualType);
                    BlockRange().InsertAfter(arg, fieldList);
                    argSlot = arg = fieldList;
                }

                LowerArgFieldList(callArg, arg.AsFieldList());
                arg = argSlot;
            }
            else
            {
                assert(abiInfo.HasExactlyOneRegisterSegment);
                InsertPutArgReg(ref argSlot, in abiInfo.Segments[0]);
                arg = argSlot;
            }
        }
        else
        {
            assert(abiInfo.NumSegments == 1);
            ref readonly var stackSeg = ref abiInfo.Segments[0];
            var putArg = new GenTreePutArgStk(TYP_VOID, arg, call, stackSeg.StackOffset,
                stackSeg.StackSize, call.IsFastTailCall);
            BlockRange().InsertAfter(arg, putArg);
            argSlot = arg = putArg;
        }

        if (arg.Oper.IsPutArgStk)
        {
            LowerPutArgStk(arg.AsPutArgStk());
        }
        DISPTREERANGE(BlockRange(), arg);
#else
        throw new NotImplementedException("Non-xarch call argument lowering, including split arguments, is not ported.");
#endif
    }

    private void LowerArgsForCall(GenTreeCall call)
    {
        JITDUMP("Args:\n======\n");
        foreach (var arg in call.Args.EarlyArgs)
        {
            LowerArg(call, arg);
        }

        JITDUMP("\nLate args:\n======\n");
        foreach (var arg in call.Args.LateArgs)
        {
            LowerArg(call, arg);
        }

#if TARGET_X86 && FEATURE_IJW
        throw new NotImplementedException("X86 IJW special-copy argument lowering is not ported.");
#else
        LegalizeArgPlacement(call);
        AfterLowerArgsForCall(call);
#endif
    }

    private static void AfterLowerArgsForCall(GenTreeCall call)
    {
#if TARGET_WASM
        throw new NotImplementedException("Wasm call argument post-processing is not ported.");
#endif
        // Native has no post-processing on non-Wasm targets.
    }
}
