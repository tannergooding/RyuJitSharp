// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForJTrue(GenTreeUnOp jtrue)
    {
        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block.Kind is BBJ_COND);

        genConsumeOperands(jtrue);

        var trueTarget = block.TrueTarget;
        var falseTarget = block.FalseTarget;
        assert(trueTarget != falseTarget);
        assert(trueTarget != block.Next);

        inst_JMP(EJ_jmpif, trueTarget);

        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }

    private unsafe void genCallFinallyWasm(BasicBlock block)
    {
        assert(block.Kind is BBJ_CALLFINALLY);

        var finallyTarget = block.Target;
        ref var ehDsc = ref _compiler.ehGetBlockHndDsc(finallyTarget);
        assert(ehDsc.ebdHandlerType is EH_HANDLER_FINALLY);
        assert(ehDsc.ebdHndBeg == finallyTarget);

        var funcletIndex = ehDsc.ebdFuncIndex;
        assert((funcletIndex >= 1) && (funcletIndex < _compiler.compFuncCount()));

        var parameters = new EmitCallParams
        {
            callType = EC_INDIR_R,
        };

        // A finally takes SP and FP as arguments, and returns nothing.
        var pointerType = TARGET_POINTER_SIZE == 4 ? CORINFO_WASM_TYPE_I32 : CORINFO_WASM_TYPE_I64;
        var typeStack = new ArrayStack<CorInfoWasmType>();
        typeStack.Push(CORINFO_WASM_TYPE_VOID);
        typeStack.Push(pointerType);
        typeStack.Push(pointerType);

        CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature;
        fixed (CorInfoWasmType* types = typeStack.Data())
        {
            wasmSignature = _compiler.info.compCompHnd->getWasmTypeSymbol(types, typeStack.Height());
        }

        GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
        GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
        GetEmitter().emitFuncletAddressConstant(unchecked((nint)funcletIndex));

        genEmitWasmCallWithCurrentGC(wasmSignature, ref parameters);

        if (block.HasFlag(BBF_RETLESS_CALL))
        {
            GetEmitter().emitIns(INS_unreachable);

            // Every Wasm function body must end with `end`, even when a retless call ends the block.
            if (block.IsLast() || _compiler.bbIsFuncletBeg(block.Next))
            {
                GetEmitter().emitIns(INS_end);
            }

            return;
        }

        assert(block.isBBCallFinallyPair);
        var callFinallyRet = block.Next;
        assert(callFinallyRet.Kind is BBJ_CALLFINALLYRET);

        var continuation = callFinallyRet.Target;
        // Branch to the continuation block if it is not the next block.
        if (continuation != callFinallyRet.Next)
        {
            inst_JMP(EJ_jmp, continuation);
        }
    }

    public void genTableBasedSwitch(GenTree treeNode)
    {
        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block.Kind is BBJ_SWITCH);

        genConsumeOperands(treeNode.AsOp());

        var switchDescriptor = block.SwitchTargets;
        var cases = switchDescriptor.Cases;
        var caseCount = unchecked((uint)cases.Length);
        assert(caseCount > 0);
        assert(switchDescriptor.HasDefaultCase);

        // br_table's immediate is the number of non-default entries; the default target is listed last.
        GetEmitter().emitIns_I(INS_br_table, EA_4BYTE, unchecked((nint)(caseCount - 1)));

        for (var caseNum = 0u; caseNum < caseCount; caseNum++)
        {
            var caseTarget = cases[unchecked((int)caseNum)].DestinationBlock;
            var depth = findTargetDepth(caseTarget);
            GetEmitter().emitIns_J(INS_label, EA_4BYTE, depth, caseTarget);
        }
    }

    private static uint GetWasmBlockIndex(BasicBlock block)
    {
        return unchecked((uint)block.bbPreorderNum);
    }

    private uint findTargetDepth(BasicBlock targetBlock)
    {
        var sourceBlock = _compiler.compCurBB;
        assert(sourceBlock is not null);

        var stack = wasmControlFlowStack;
        assert(stack is not null);
        var height = stack.Height();

        var targetIndex = GetWasmBlockIndex(targetBlock);
        var sourceIndex = GetWasmBlockIndex(sourceBlock);
        var isBackedge = targetIndex <= sourceIndex;

        for (var i = 0; i < height; i++)
        {
            var interval = stack.Top(i);
            uint match;

            if (isBackedge)
            {
                // Loop targets bind to interval starts.
                match = interval.Start();
            }
            else
            {
                // Block targets bind to interval ends. Try and wrapper ends emit additional instructions.
                if (interval.IsTry() || interval.IsExnRefWrapper())
                {
                    continue;
                }

                match = interval.End();
            }

            if ((match == targetIndex) && (isBackedge == interval.IsLoop()))
            {
                return unchecked((uint)i);
            }
        }

#if DEBUG
        jitprintf($"Could not find {FMT_BB(targetBlock.bbNum)}[{targetIndex}]"
            + $"{(isBackedge ? " (backedge)" : "")} in active control stack\n");
        jitprintf("Current stack is\n");

        for (var i = 0; i < height; i++)
        {
            stack.Top(i).Dump();
        }
#endif

        assert(false, conditionExpression: "Can't find target in control stack");
        return uint.MaxValue;
    }

    private void genEmitIf(WasmValueType blockType = WasmValueType.Invalid)
    {
        // These labels are not represented in wasmControlFlowStack, but they still affect branch depths.
        wasmExtraControlFlowDepth = unchecked(wasmExtraControlFlowDepth + 1);

        if (blockType != WasmValueType.Invalid)
        {
            GetEmitter().emitIns_BlockTy(INS_if, blockType);
        }
        else
        {
            GetEmitter().emitIns(INS_if);
        }
    }

    private void genEmitEndIf()
    {
        assert(wasmExtraControlFlowDepth > 0);
        wasmExtraControlFlowDepth = unchecked(wasmExtraControlFlowDepth - 1);
        GetEmitter().emitIns(INS_end);
    }

    private void genEmitBeginBlock(WasmValueType blockType = WasmValueType.Invalid)
    {
        wasmExtraControlFlowDepth = unchecked(wasmExtraControlFlowDepth + 1);

        if (blockType != WasmValueType.Invalid)
        {
            GetEmitter().emitIns_BlockTy(INS_block, blockType);
        }
        else
        {
            GetEmitter().emitIns(INS_block);
        }
    }

    private void genEmitEndBlock()
    {
        assert(wasmExtraControlFlowDepth > 0);
        wasmExtraControlFlowDepth = unchecked(wasmExtraControlFlowDepth - 1);
        GetEmitter().emitIns(INS_end);
    }

    private void genEmitStartBlockWasm(BasicBlock block)
    {
        var cursor = GetWasmBlockIndex(block);
        var stack = wasmControlFlowStack;
        assert(stack is not null);

        // Close intervals that end at this block before opening any new ones.
        while (!stack.Empty() && (stack.Top().End() == cursor))
        {
            var topInterval = stack.Top();
            if (topInterval.IsExnRefWrapper())
            {
                // Normal fall-through cannot provide the [exnref] required by the wrapper's end.
                instGen(INS_unreachable);
            }

            instGen(INS_end);
            var interval = stack.Pop();

            if (interval.IsTry())
            {
                // The try_table has a void result; make the wrapper body polymorphic.
                instGen(INS_unreachable);
            }

            if (interval.IsExnRefWrapper())
            {
                // Save the catch_ref value for later throw_ref emission.
                var exnRefIndex = _compiler.funCurrentFunc().funWasmExnRefLocalIndex;
                assert(exnRefIndex != uint.MaxValue);
                Emitter.emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)exnRefIndex));
            }
        }

        var intervals = _compiler.fgWasmIntervals;
        assert(intervals is not null);
        if (wasmCursor < intervals.Count)
        {
            var interval = intervals[unchecked((int)wasmCursor)];
            var chain = interval.Chain();

            while (chain.Start() <= cursor)
            {
                if (interval.IsLoop())
                {
                    Emitter.emitIns_BlockTy(INS_loop);
                }
                else if (interval.IsTry())
                {
                    var jTrue = block.LastNode;
                    assert(jTrue is not null);
                    assert(jTrue.Oper is GT_WASM_JEXCEPT);

                    // Plain blocks between the wrapper and try_table shift its branch depth.
                    Emitter.emitIns_Ty_I(INS_try_table, WasmValueType.Invalid, 1);

                    var stackHeight = stack.Height();
                    var wrapperDepth = uint.MaxValue;
                    for (var i = 0; i < stackHeight; i++)
                    {
                        if (stack.Top(i).IsExnRefWrapper())
                        {
                            wrapperDepth = unchecked((uint)i);
                            break;
                        }
                    }
                    assert(wrapperDepth != uint.MaxValue);

                    assert(block.FalseTarget == block.Next);
                    var target = block.TrueTarget;
                    Emitter.emitIns_J(INS_catch_ref, EA_4BYTE, wrapperDepth, target);
                }
                else
                {
                    assert(interval.IsBlock());
                    if (interval.IsExnRefWrapper())
                    {
                        Emitter.emitIns_BlockTy(INS_block, WasmValueType.ExnRef);
                    }
                    else
                    {
                        Emitter.emitIns_BlockTy(INS_block);
                    }
                }

                wasmCursor = unchecked(wasmCursor + 1);
                stack.Push(interval);

                if (interval.IsLoop())
                {
                    if (!block.HasFlag(BBF_HAS_LABEL))
                    {
                        block.SetFlags(BBF_HAS_LABEL);
                        genDefineTempLabel(block);
                    }
                }
                else
                {
                    var indexToBlockMap = _compiler.fgIndexToBlockMap;
                    assert(indexToBlockMap is not null);
                    var endBlock = indexToBlockMap[unchecked((int)interval.End())];
                    if (!endBlock.HasFlag(BBF_HAS_LABEL))
                    {
                        endBlock.SetFlags(BBF_HAS_LABEL);
                        genDefineTempLabel(endBlock);
                    }
                }

                if (wasmCursor >= intervals.Count)
                {
                    break;
                }

                interval = intervals[unchecked((int)wasmCursor)];
                chain = interval.Chain();
            }
        }
    }

    private void genEmitFunctionEndWasm(bool emitTerminalUnreachable)
    {
        // Close remaining intervals before terminating the function body.
        var stack = wasmControlFlowStack;
        assert(stack is not null);

        while (!stack.Empty())
        {
            var topInterval = stack.Top();
            if (topInterval.IsExnRefWrapper())
            {
                // Preserve the same fall-through and stack-polymorphism rules as block starts.
                instGen(INS_unreachable);
            }

            instGen(INS_end);
            var interval = stack.Pop();

            if (interval.IsTry())
            {
                instGen(INS_unreachable);
            }

            if (interval.IsExnRefWrapper())
            {
                // Save the catch_ref value for later throw_ref emission.
                var exnRefIndex = _compiler.funCurrentFunc().funWasmExnRefLocalIndex;
                assert(exnRefIndex != uint.MaxValue);
                Emitter.emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)exnRefIndex));
            }
        }

        if (emitTerminalUnreachable)
        {
            instGen(INS_unreachable);
        }

        instGen(INS_end);
    }
}
#endif
