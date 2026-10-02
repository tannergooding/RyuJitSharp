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

#if !TARGET_64BIT && !TARGET_WASM
        if (Compiler.Options.compUseSoftFP && (arg.Type is TYP_DOUBLE))
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

#if FEATURE_ARG_SPLIT
        if (compFeatureArgSplit() && abiInfo.IsSplitAcrossRegistersAndStack)
        {
            SplitArgumentBetweenRegistersAndStack(call, callArg);
            LowerArg(call, callArg);
            return;
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
        LowerSpecialCopyArgs(call);
#endif
        LegalizeArgPlacement(call);
        AfterLowerArgsForCall(call);
    }

#if TARGET_X86 && FEATURE_IJW
    private unsafe void LowerSpecialCopyArgs(GenTreeCall call)
    {
        var compiler = CompilerInstance;
        if (compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB) &&
            compiler.compMethodRequiresPInvokeFrame && call.IsUnmanaged &&
            compiler.compHasSpecialCopyArgs())
        {
            var argIndex = call.Args.CountUserArgs() - 1;
            assert(call.Args.CountUserArgs() <= compiler.info.compILargsCount);
            var checkForUnmanagedThisArg = call.UnmanagedCallConv is CorInfoCallConvExtension.Thiscall;
            foreach (var arg in call.Args.Args)
            {
                if (!arg.IsUserArg)
                {
                    continue;
                }

                if (checkForUnmanagedThisArg && (argIndex == call.Args.CountUserArgs() - 1))
                {
                    assert(arg.Node.Oper is GT_PUTARG_REG);
                    checkForUnmanagedThisArg = false;
                    continue;
                }

                var paramLocal = compiler.compMapILargNum(argIndex);
                assert(paramLocal < compiler.info.compArgsCount);
                if (compiler.argRequiresSpecialCopy(paramLocal) && (arg.SignatureType is TYP_STRUCT))
                {
                    assert(arg.Node.Oper is GT_PUTARG_STK);
                    InsertSpecialCopyArg(arg.Node.AsPutArgStk(), arg.SignatureClassHandle, paramLocal);
                }

                argIndex--;
            }
        }
    }

    private unsafe void InsertSpecialCopyArg(GenTreePutArgStk putArgStk, CORINFO_CLASS_HANDLE argType, int lclNum)
    {
        assert(putArgStk is not null);
        var compiler = CompilerInstance;
        GenTree destination = new GenTreePhysReg(REG_SPBASE, TYP_I_IMPL);
        var localType = compiler.lvaGetRealType(lclNum);
        GenTree source;
        if (localType is TYP_BYREF or TYP_I_IMPL)
        {
            source = compiler.gtNewLclVarNode(localType, lclNum);
        }
        else
        {
            assert(localType is TYP_STRUCT);
            source = compiler.gtNewLclAddrNode(TYP_I_IMPL, lclNum, 0);
        }

        var destinationPlaceholder = compiler.gtNewZeroConNode(destination.Type);
        var sourcePlaceholder = compiler.gtNewZeroConNode(source.Type);
        var helper = compiler.gtNewUserCallNode(TYP_VOID, compiler.info.compCompHnd->getSpecialCopyHelper(argType));
        _ = helper.Args.PushBack(NewCallArg.CreateForPrimitive(destinationPlaceholder));
        _ = helper.Args.PushBack(NewCallArg.CreateForPrimitive(sourcePlaceholder));
        _ = compiler.fgMorphArgs(helper);

        var helperRange = LIR.SeqTree(compiler, helper);
        var first = helperRange.FirstNode;
        var last = helperRange.LastNode;
        assert(first is not null && last is not null);
        BlockRange().InsertAfter(putArgStk, helperRange);
        BlockRange().InsertAfter(putArgStk, destination);
        BlockRange().InsertAfter(putArgStk, source);

        var foundDestination = BlockRange().TryGetUse(destinationPlaceholder, out var destinationUse);
        var foundSource = BlockRange().TryGetUse(sourcePlaceholder, out var sourceUse);
        assert(foundDestination && foundSource);
        destinationUse.ReplaceWith(destination);
        sourceUse.ReplaceWith(source);
        destinationPlaceholder.IsUnusedValue = true;
        sourcePlaceholder.IsUnusedValue = true;

        LowerRange(first, last);
        MovePutArgNodesUpToCall(helper);

        BlockRange().Remove(destinationPlaceholder);
        BlockRange().Remove(sourcePlaceholder);
    }
#endif

    private static void AfterLowerArgsForCall(GenTreeCall call)
    {
#if TARGET_WASM
        throw new NotImplementedException("Wasm call argument post-processing is not ported.");
#endif
        // Native has no post-processing on non-Wasm targets.
    }
}
