// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;

namespace RyuJitSharp;

public sealed partial class WasmRegAlloc
{
    private BasicBlock CurrentRange()
    {
        return _currentBlock ?? throw new InvalidOperationException("Wasm register allocation has no current block.");
    }

    private void IdentifyCandidates()
    {
        InitializeStackPointer();

        var anyFrameLocals = false;
        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            local.RegNum = REG_STK;

            CheckForDNER(localNumber, in local);

            var isRegCandidate = IsRegCandidate(in local);

            // Wasm RA currently does not support EH write-through, so any local live in or out
            // of a handler must be located only on the stack.
            if (local.lvTracked && local.IsLiveInOutOfHandler)
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LiveInOutOfHandler);
                isRegCandidate = false;
            }

            // GC refs cannot live in Wasm locals until they can be spilled to the stack before calls.
            if (varTypeIsGC(local.Type))
            {
                _compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.WasmGCVisibility);
                isRegCandidate = false;
            }

            if (isRegCandidate)
            {
                JITDUMP($"RA candidate: V{localNumber:D2}\n");
                InitializeCandidate(in local);
            }
            else if (local.lvRefCnt() != 0)
            {
                anyFrameLocals = true;
            }
        }

        if (anyFrameLocals || _compiler.compLocallocUsed || (_compiler.compFuncCount() > 1))
        {
            AllocateFramePointer();
        }
    }

    private void InitializeCandidate(in LclVarDsc local)
    {
        var register = AllocateVirtualRegister(local.GetRegisterType());
        ref var localDescriptor = ref _compiler.lvaGetDesc(_compiler.lvaGetLclNum(in local));
        localDescriptor.RegNum = register;
        localDescriptor.lvLRACandidate = true;
    }

    private void InitializeStackPointer()
    {
        ref var stackPointerLocal = ref _compiler.lvaGetDesc(_compiler.lvaWasmSpArg);
        assert(stackPointerLocal.lvRefCnt() != 0);

        stackPointerLocal.lvImplicitlyReferenced = false;
        stackPointerLocal.setLvRefCnt(0);

        AllocateStackPointer();
    }

    private void AllocateStackPointer()
    {
        if (GetFuncletData(ROOT_FUNC_IDX).StackPointerReg == REG_NA)
        {
            var stackPointer = AllocateVirtualRegister(TYP_I_IMPL);

            for (var index = 0; index < _perFuncletData.Length; index++)
            {
                GetFuncletData(index).StackPointerReg = stackPointer;
            }
        }
    }

    private void AllocateFramePointer()
    {
        AllocateStackPointer();

        var stackPointer = GetFuncletData(ROOT_FUNC_IDX).StackPointerReg;
        var needUniqueFramePointer = _compiler.compLocallocUsed || (_compiler.compFuncCount() > 1);
        var framePointer = needUniqueFramePointer ? AllocateVirtualRegister(TYP_I_IMPL) : REG_NA;

        if (_compiler.compLocallocUsed)
        {
            GetFuncletData(ROOT_FUNC_IDX).FramePointerReg = framePointer;
        }
        else
        {
            GetFuncletData(ROOT_FUNC_IDX).FramePointerReg = stackPointer;
        }

        for (var index = 1; index < _perFuncletData.Length; index++)
        {
            GetFuncletData(index).FramePointerReg = framePointer;
        }
    }

    private regNumber AllocateVirtualRegister(var_types type)
    {
        return AllocateVirtualRegister(regNumberExtensions.ActualTypeToWasmValueType(type));
    }

    private regNumber AllocateVirtualRegister(WasmValueType type)
    {
        ref var virtualRegisters = ref _virtualRegs[(int)type];
        if (!virtualRegisters.IsInitialized)
        {
            virtualRegisters = new VirtualRegStack(type);
        }

        return virtualRegisters.Push();
    }

    private regNumber AllocateTemporaryRegister(var_types type)
    {
        var wasmType = regNumberExtensions.ActualTypeToWasmValueType(type);
        var index = _temporaryRegs[(int)wasmType].Push();
        return regNumberExtensions.MakeWasmReg(index, wasmType);
    }

    private regNumber ReleaseTemporaryRegister(var_types type)
    {
        return ReleaseTemporaryRegister(regNumberExtensions.TypeToWasmValueType(type));
    }

    private regNumber ReleaseTemporaryRegister(WasmValueType wasmType)
    {
        var index = _temporaryRegs[(int)wasmType].Pop();
        return regNumberExtensions.MakeWasmReg(index, wasmType);
    }

    private void CollectReferences()
    {
        foreach (var block in _compiler.Blocks)
        {
            CollectReferencesForBlock(block);
        }
    }

    private void CollectReferencesForBlock(BasicBlock block)
    {
        _currentBlock = block;

        if (_compiler.bbIsFuncletBeg(block))
        {
            _currentFunclet = (int)_compiler.funGetFuncIdx(block);
        }

        // Collection can reorder already-visited nodes, but introduces no nodes that need collection.
        var node = block.FirstNode;
        while (node is not null)
        {
            var nextNode = node.Next;
            CollectReferencesForNode(node);
            node = nextNode;
        }

        _currentBlock = null;
    }

    private void CollectReferencesForNode(GenTree node)
    {
        switch (node.Oper)
        {
            case GT_NULLCHECK:
            {
                CollectReferencesForNullCheck(node.AsIndir());
                break;
            }

            case GT_LCL_VAR:
            {
                node = CollectReferencesForLclVar(node.AsLclVar());
                break;
            }

            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                ref var local = ref _compiler.lvaGetDesc(node.AsLclVarCommon().LclNum);
                if (local.lvIsRegCandidate)
                {
                    assert(node.Oper is GT_STORE_LCL_VAR);
                    CollectReference(node.AsLclVarCommon());
                }
                else
                {
                    RewriteLocalStackStore(node.AsLclVarCommon());
                }

                break;
            }

            case GT_DIV:
            case GT_UDIV:
            case GT_MOD:
            case GT_UMOD:
            {
                CollectReferencesForDivMod(node.AsOp());
                break;
            }

            case GT_LCLHEAP:
            {
                CollectReferencesForLclHeap(node.AsOp());
                break;
            }

            case GT_CALL:
            {
                CollectReferencesForCall(node.AsCall());
                break;
            }

            case GT_CAST:
            {
                CollectReferencesForCast(node.AsOp());
                break;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            {
                CollectReferencesForBinop(node.AsOp());
                break;
            }

            case GT_IND:
            case GT_STOREIND:
            {
                CollectReferencesForIndir(node.AsIndir());
                break;
            }

            case GT_STORE_BLK:
            {
                CollectReferencesForBlockStore(node.AsBlk());
                break;
            }

            case GT_INDEX_ADDR:
            {
                CollectReferencesForIndexAddr(node.AsIndexAddr());
                break;
            }

            case GT_CKFINITE:
            {
                ConsumeTemporaryRegForOperand(node.AsUnOp().Op1, "ckfinite finiteness check");
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                CollectReferencesForHardwareIntrinsic(node.AsHWIntrinsic());
                break;
            }
#endif

            default:
            {
                assert(!node.Oper.IsLocalStore);
                break;
            }
        }

        RequestTemporaryRegisterForMultiplyUsedNode(node);
    }

    private void CollectReferencesForDivMod(GenTreeOp divModNode)
    {
        ConsumeTemporaryRegForOperand(divModNode.Op2, "div-by-zero / overflow check");
        ConsumeTemporaryRegForOperand(divModNode.Op1, "div-by-zero / overflow check");
    }

    private void CollectReferencesForLclHeap(GenTreeOp lclHeapNode)
    {
        if (!lclHeapNode.Op1.IsContainedIntOrIImmed)
        {
            var internalRegister = RequestInternalRegister(lclHeapNode, TYP_I_IMPL);
            var releasedRegister = ReleaseTemporaryRegister(regNumberExtensions.WasmRegToType(internalRegister));
            assert(releasedRegister == internalRegister);
        }
    }

    private void CollectReferencesForIndexAddr(GenTreeIndexAddr indexAddrNode)
    {
        ConsumeTemporaryRegForOperand(indexAddrNode.Index, "bounds check");
        ConsumeTemporaryRegForOperand(indexAddrNode.Arr, "bounds check");
    }

    private void CollectReferencesForCall(GenTreeCall callNode)
    {
        var thisArg = callNode.Args.ThisArg;
        if (thisArg is not null)
        {
            ConsumeTemporaryRegForOperand(thisArg.Node, "call this argument");
        }

        if (callNode.IsFastTailCall)
        {
            var stackPointerArg = callNode.Args.FindWellKnownArg(WellKnownArg.WasmShadowStackPointer);
            if (stackPointerArg is not null)
            {
                var physicalRegister = stackPointerArg.Node;
                assert(physicalRegister.Oper is GT_PHYSREG);
                assert(physicalRegister.AsPhysReg().SrcReg == GetFuncletData(_currentFunclet).StackPointerReg);
                assert(_currentFunclet == ROOT_FUNC_IDX);

                var frameSize = new GenTree(GT_FRAME_SIZE, TYP_I_IMPL);
                var stackPointerAdjustment = new GenTreeOp(GT_ADD, TYP_I_IMPL, physicalRegister, frameSize);

                CurrentRange().InsertAfter(physicalRegister, frameSize, stackPointerAdjustment);
                stackPointerArg.NodeRef = stackPointerAdjustment;
            }
        }
    }

    private void CollectReferencesForCast(GenTreeOp castNode)
    {
        ConsumeTemporaryRegForOperand(castNode.Op1, "cast overflow check");
    }

    private void CollectReferencesForBinop(GenTreeOp binopNode)
    {
        var internalRegister = REG_NA;
        if (binopNode.HasOverflowCheck)
        {
            if (binopNode.Oper is GT_ADD or GT_SUB)
            {
                internalRegister = RequestInternalRegister(binopNode, binopNode.Type);
            }
            else if (binopNode.Oper is GT_MUL)
            {
                assert(binopNode.Type is TYP_INT);
                internalRegister = RequestInternalRegister(binopNode, TYP_LONG);
            }
        }

        if (internalRegister != REG_NA)
        {
            var releasedRegister = ReleaseTemporaryRegister(regNumberExtensions.WasmRegToType(internalRegister));
            assert(releasedRegister == internalRegister);
        }

        ConsumeTemporaryRegForOperand(binopNode.Op2, "binop overflow check");
        ConsumeTemporaryRegForOperand(binopNode.Op1, "binop overflow check");
    }

    private void CollectReferencesForNullCheck(GenTreeIndir node)
    {
        var baseNode = node.Base;
        if ((baseNode is not null) &&
            ((baseNode._lirFlags & LIR.Flags.MultiplyUsed) != LIR.Flags.None))
        {
            ConsumeTemporaryRegForOperand(baseNode, "Orphaned GT_NULLCHECK with multiply-used flag");
        }
    }

    private void CollectReferencesForIndir(GenTreeIndir node)
    {
        var baseNode = node.Base
            ?? throw new InvalidOperationException("Wasm indirections require a base address.");
        ConsumeTemporaryRegForOperand(baseNode, "indirection address");

        if (node.Oper is GT_STOREIND && node.Type is TYP_SIMD12)
        {
            var internalRegister = RequestInternalRegister(node, TYP_SIMD16);
            var releasedRegister = ReleaseTemporaryRegister(regNumberExtensions.WasmRegToType(internalRegister));
            assert(releasedRegister == internalRegister);
        }
    }

    private void CollectReferencesForBlockStore(GenTreeBlk node)
    {
        var source = node.Data;
        if (source.Oper is GT_IND)
        {
            source = source.AsIndir().Addr;
        }

        ConsumeTemporaryRegForOperand(source, "block store source");
        ConsumeTemporaryRegForOperand(node.Addr, "block store destination");
    }

    private GenTree CollectReferencesForLclVar(GenTreeLclVar local)
    {
        if (local.LclNum == _compiler.lvaWasmSpArg)
        {
            var physicalRegister = new GenTreePhysReg(
                GetFuncletData(_currentFunclet).StackPointerReg,
                local.Type,
                local,
                NodeThreading.LIR);
            CurrentRange().ReplaceNode(local, physicalRegister);
            CollectReference(physicalRegister);
            return physicalRegister;
        }

        return local;
    }

#if FEATURE_HW_INTRINSICS
    private void CollectReferencesForHardwareIntrinsic(GenTreeHWIntrinsic node)
    {
        if ((node.HWIntrinsicId is NI_PackedSimd_Swizzle) && node.GetOp(2).IsContained)
        {
            ConsumeTemporaryRegForOperand(node.GetOp(1), "i8x16.shuffle source reuse");
            return;
        }

        var needsJumpTableFallback = false;
        if (HWIntrinsicInfo.HasImmediateOperand(node.HWIntrinsicId))
        {
            GenTree? immediateOperand = null;
            foreach (var operand in node.Operands)
            {
                if (HWIntrinsicInfo.isImmOp(node.HWIntrinsicId, operand))
                {
                    immediateOperand = operand;
                    break;
                }
            }

            assert(immediateOperand is not null);
            if (immediateOperand.Oper is not GT_CNS_INT and not GT_CNS_LNG)
            {
                needsJumpTableFallback = true;
            }
        }

        if (needsJumpTableFallback)
        {
            var operandCount = node.Operands.Length;
            for (var index = operandCount; index >= 1; index--)
            {
                ConsumeTemporaryRegForOperand(node.GetOp(index), "hardware intrinsic fallback");
            }
        }
        else if (node.IsMemoryLoad(out var address) || node.IsMemoryStore(out address))
        {
            assert(address is not null);
            ConsumeTemporaryRegForOperand(address, "hardware intrinsic memory address null check");
        }
    }
#endif

    private void RewriteLocalStackStore(GenTreeLclVarCommon localNode)
    {
        var value = localNode.Data;
        var insertionPoint = FirstNodeInOperandOrder(value);

        var storeType = localNode.Type;
        if ((storeType is TYP_STRUCT) && localNode.IsCopyBlkOp)
        {
            ref var local = ref _compiler.lvaGetDesc(localNode.LclNum);
            var localRegisterType = local.GetRegisterType(localNode);
            if (localRegisterType is not TYP_UNDEF)
            {
                storeType = localRegisterType;
            }
        }

        var isStruct = storeType is TYP_STRUCT;
        var offset = localNode.LclOffs;
        var layout = isStruct ? localNode.GetLayout(_compiler) : null;
        assert(!isStruct || layout is not null);

        var range = CurrentRange();
        var nextNode = localNode.Next;
        range.Remove(localNode);

        var localAddress = new GenTreeLclFld(
            GT_LCL_ADDR,
            TYP_I_IMPL,
            localNode.LclNum,
            offset,
            data: null,
            layout: layout,
            source: localNode,
            threading: NodeThreading.None);
        localAddress.CopySsaIdentityFrom(localNode);
        range.InsertBefore(insertionPoint, localAddress);

        GenTree store;
        var indirectFlags = GTF_IND_NONFAULTING | GTF_IND_TGT_NOT_HEAP;
        if (isStruct)
        {
            var structLayout = layout ?? throw new InvalidOperationException("Wasm struct stores require a local layout.");
            store = _compiler.gtNewStoreBlkNode(localAddress, value, structLayout, indirectFlags);
        }
        else
        {
            store = _compiler.gtNewStoreIndNode(storeType, localAddress, value, indirectFlags);
        }

        range.InsertBefore(nextNode, store);

        var storeRange = new LIR.ReadOnlyRange(store, store);
        var lowering = _compiler.LoweringPhase
            ?? throw new InvalidOperationException("Wasm local stack stores require an active lowering phase.");
        lowering.LowerRange(_currentBlock ?? throw new InvalidOperationException("Wasm register allocation has no current block."), storeRange);

        if ((store.Oper is GT_STOREIND) && (store.Type is TYP_SIMD12))
        {
            var internalRegister = RequestInternalRegister(store, TYP_SIMD16);
            var releasedRegister = ReleaseTemporaryRegister(regNumberExtensions.WasmRegToType(internalRegister));
            assert(releasedRegister == internalRegister);
        }
    }

    private static GenTree FirstNodeInOperandOrder(GenTree node)
    {
        var firstNode = node;
        while (firstNode.VisitOperands(operand =>
        {
            firstNode = operand;
            return GenTree.VisitResult.Abort;
        }) is GenTree.VisitResult.Abort)
        {
        }

        return firstNode;
    }

    private void CollectReference(GenTree node)
    {
        var data = GetFuncletData(_currentFunclet);
        var references = data.VirtualRegRefs;

        if (data.LastVirtualRegRefsCount > 0)
        {
            assert(references is not null);
            if (node == references.Nodes[data.LastVirtualRegRefsCount - 1])
            {
                return;
            }
        }

        if (references is null)
        {
            references = new VirtualRegReferences();
            data.VirtualRegRefs = references;
        }
        else if (data.LastVirtualRegRefsCount == references.Nodes.Length)
        {
            references = new VirtualRegReferences
            {
                Previous = data.VirtualRegRefs,
            };
            data.VirtualRegRefs = references;
            data.LastVirtualRegRefsCount = 0;
        }

        assert(data.LastVirtualRegRefsCount < references.Nodes.Length);
        references.Nodes[data.LastVirtualRegRefsCount++] = node;
    }

    private void RequestTemporaryRegisterForMultiplyUsedNode(GenTree node)
    {
        if ((node._lirFlags & LIR.Flags.MultiplyUsed) == LIR.Flags.None)
        {
            return;
        }

        assert(node.IsValue);
        if (node.IsUnusedValue)
        {
            node._lirFlags &= ~LIR.Flags.MultiplyUsed;
            return;
        }

        if ((node.Oper is GT_LCL_VAR) && _compiler.lvaGetDesc(node.AsLclVar().LclNum).lvIsRegCandidate)
        {
            node._lirFlags &= ~LIR.Flags.MultiplyUsed;
            return;
        }

        var register = AllocateTemporaryRegister(node.Type);
        assert(node.RegNum == REG_NA);
        node.RegNum = register;
        CollectReference(node);
    }

    private void ConsumeTemporaryRegForOperand(GenTree operand, string reason)
    {
        if ((operand._lirFlags & LIR.Flags.MultiplyUsed) == LIR.Flags.None)
        {
            return;
        }

        var register = ReleaseTemporaryRegister(genActualType(operand.Type));
        assert(register == operand.RegNum);

        operand._lirFlags &= ~LIR.Flags.MultiplyUsed;
#if DEBUG
        JITDUMP($"Consumed a temporary reg for [{operand.TreeId:D6}]: {reason}\n");
#else
        JITDUMP($"Consumed a temporary reg: {reason}\n");
#endif
    }

    private regNumber RequestInternalRegister(GenTree node, var_types type)
    {
#if DEBUG
        JITDUMP($"Requesting internal {type.Name} register for [{node.TreeId:D6}]\n");
#else
        JITDUMP($"Requesting internal {type.Name} register\n");
#endif

        var register = AllocateTemporaryRegister(type);
        _codeGen.InternalRegisters.Add(node, register);
        _nodesWithInternalRegisters.Add(node);
        CollectReference(node);
        return register;
    }
}
#endif
