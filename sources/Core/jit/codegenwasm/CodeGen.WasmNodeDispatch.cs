// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Runtime.CompilerServices;
#endif

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Native Wasm codegen currently builds without WASM_THREAD_SUPPORT.
    private const bool WasmThreadSupport = false;

    private unsafe void genCodeForTreeNodeWasm(GenTree treeNode)
    {
#if DEBUG
        lastConsumedNode = null;
        if (_compiler.verbose)
        {
            _compiler.gtDispLIRNode(treeNode, "Generating: ");
        }
#endif

        assert(!treeNode.IsReuseRegVal);
        if (treeNode.IsContained)
        {
            return;
        }

#if FEATURE_HW_INTRINSICS
        if (treeNode.Oper.IsHWIntrinsic)
        {
            genHWIntrinsic(treeNode.AsHWIntrinsic());
            return;
        }
#endif

        switch (treeNode.Oper)
        {
            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            case GT_OR:
            case GT_XOR:
            case GT_AND:
            {
                genCodeForBinary(treeNode.AsOp());
                break;
            }

            case GT_DIV:
            case GT_MOD:
            case GT_UDIV:
            case GT_UMOD:
            {
                genCodeForDivMod(treeNode.AsOp());
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
            {
                genCodeForShift(treeNode);
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            {
                genCodeForCompare(treeNode.AsOp());
                break;
            }

            case GT_LCL_ADDR:
            {
                genCodeForLclAddr(treeNode.AsLclFld());
                break;
            }

            case GT_LCL_FLD:
            {
                genCodeForLclFld(treeNode.AsLclFld());
                break;
            }

            case GT_LCL_VAR:
            {
                genCodeForLclVar(treeNode.AsLclVar());
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                genCodeForStoreLclVar(treeNode.AsLclVar());
                break;
            }

            case GT_PHYSREG:
            {
                genCodeForPhysReg(treeNode.AsPhysReg());
                break;
            }

            case GT_FRAME_SIZE:
            {
                genCodeForFrameSize(treeNode);
                break;
            }

            case GT_JTRUE:
            {
                genCodeForJTrue(treeNode.AsUnOp());
                break;
            }

            case GT_SWITCH:
            {
                genTableBasedSwitch(treeNode);
                break;
            }

            case GT_RETURN:
            case GT_RETFILT:
            {
                genReturn(treeNode);
                break;
            }

            case GT_IL_OFFSET:
            {
                // Debug-info marker; it emits no code.
                break;
            }

            case GT_NOP:
            {
                break;
            }

            case GT_NO_OP:
            {
                GetEmitter().emitIns(INS_nop);
                break;
            }

            case GT_CNS_INT:
            case GT_CNS_LNG:
            case GT_CNS_DBL:
            {
                genCodeForConstant(treeNode);
                break;
            }

            case GT_CAST:
            {
                genCodeForCast(treeNode.AsCast());
                break;
            }

            case GT_BITCAST:
            {
                genCodeForBitCast(treeNode.AsUnOp());
                break;
            }

            case GT_NEG:
            case GT_NOT:
            {
                genCodeForNegNot(treeNode.AsUnOp());
                break;
            }

            case GT_IND:
            {
                genCodeForIndir(treeNode.AsIndir());
                break;
            }

            case GT_STOREIND:
            {
                genCodeForStoreInd(treeNode.AsStoreInd());
                break;
            }

            case GT_CALL:
            {
                genCall(treeNode.AsCall());
                break;
            }

            case GT_NULLCHECK:
            {
                genCodeForNullCheck(treeNode.AsIndir());
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                genRangeCheck(treeNode);
                break;
            }

            case GT_KEEPALIVE:
            {
                // Remove KEEPALIVE after GC info generation is implemented.
                genConsumeRegs(treeNode.AsOp().Op1);
                GetEmitter().emitIns(INS_drop);
                break;
            }

            case GT_LCLHEAP:
            {
                genLclHeap(treeNode);
                break;
            }

            case GT_INDEX_ADDR:
            {
                genCodeForIndexAddr(treeNode.AsIndexAddr());
                break;
            }

            case GT_LEA:
            {
                genLeaInstruction(treeNode.AsAddrMode());
                break;
            }

            case GT_STORE_BLK:
            {
                genCodeForStoreBlk(treeNode.AsBlk());
                break;
            }

            case GT_MEMORYBARRIER:
            {
                // Wasm codegen is single-threaded, so this is a no-op.
                assert(!WasmThreadSupport);
                JITDUMP("Ignoring GT_MEMORYBARRIER; single-threaded codegen\n");
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsic(treeNode.AsIntrinsic());
                break;
            }

            case GT_WASM_JEXCEPT:
            {
                // The marker does not emit code.
                break;
            }

            case GT_WASM_THROW_REF:
            {
                // Reload and rethrow the exnref stashed at the catch_ref landing.
                var exnRefIndex = _compiler.funCurrentFunc().funWasmExnRefLocalIndex;
                assert(exnRefIndex != uint.MaxValue);
                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)exnRefIndex));
                GetEmitter().emitIns(INS_throw_ref);
                break;
            }

            case GT_CATCH_ARG:
            {
                genCatchArg(treeNode);
                break;
            }

            case GT_CKFINITE:
            {
                genCkfinite(treeNode);
                break;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                genCodeForVectorConstant(treeNode);
                break;
            }
#endif

            case GT_ASYNC_CONTINUATION:
            {
                genCodeForAsyncContinuation(treeNode);
                break;
            }

            case GT_RETURN_SUSPEND:
            {
                genReturnSuspend(treeNode.AsUnOp());
                break;
            }

            case GT_ASYNC_RESUME_INFO:
            {
                genAsyncResumeInfo(treeNode.AsVal());
                break;
            }

            case GT_RECORD_ASYNC_RESUME:
            {
                genRecordAsyncResume(treeNode.AsVal());
                break;
            }

            default:
            {
#if DEBUG
                if (JitConfig.JitWasmNyiToR2RUnsupported > 0)
                {
                    NYI_WASM("Opcode not implemented");
                }
                else
                {
                    NYIRAW(treeNode.Oper.ToString());
                }
#else
                NYI_WASM("Opcode not implemented");
#endif
                break;
            }
        }
    }

#if FEATURE_HW_INTRINSICS
    private void genHWIntrinsic(GenTreeHWIntrinsic node)
    {
        WasmCodegenDependencyNotPorted(node, nameof(genHWIntrinsic));
    }
#endif

    private void genCodeForCompare(GenTreeOp tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForCompare));
    }

    private void genCodeForLclAddr(GenTreeLclFld tree)
    {
        assert(tree.OperIs(GT_LCL_ADDR));

        var lclNum = tree.LclNum;
        var lclOffset = tree.LclOffs;

        // This matches the Wasm-only LIR::Flags::FoldedAddr bit in src/coreclr/jit/lir.h.
        const LIR.Flags WasmFoldedAddr = (LIR.Flags)0x10;

        GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));

        if (((tree._lirFlags & WasmFoldedAddr) == LIR.Flags.None) &&
            ((lclOffset != 0) || (_compiler.lvaFrameAddress(lclNum, out _) != 0)))
        {
            GetEmitter().emitIns_S(INS_I_const, EA_PTRSIZE, lclNum, lclOffset);
            GetEmitter().emitIns(INS_I_add);
        }

        WasmProduceReg(tree);
    }

    private void genCodeForLclFld(GenTreeLclFld tree)
    {
        assert(tree.OperIs(GT_LCL_FLD));
        _ = _compiler.lvaGetDesc(tree.LclNum);

        var type = tree.Type;
        if (type is TYP_SIMD12)
        {
            genLoadLclTypeSimd12(tree);
        }
        else
        {
            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
            GetEmitter().emitIns_S(ins_Load(type), type.EmitSize, tree.LclNum, tree.LclOffs);
        }

        WasmProduceReg(tree);
    }

    private void genLoadLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genLoadLclTypeSimd12));
    }

    private void genCodeForLclVar(GenTreeLclVar tree)
    {
        assert(tree.OperIs(GT_LCL_VAR) && !tree.IsMultiReg);
        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);

        // Wasm cannot reload at the point of use without inserting into an emitted instruction group.
        // Lowering orders nodes to obey the value-stack constraints, so only non-candidates need WasmProduceReg.
        if (!varDsc.lvIsRegCandidate)
        {
            var type = varDsc.GetRegisterType(tree);

            if (type is TYP_SIMD12)
            {
                genLoadLclTypeSimd12(tree);
            }
            else
            {
                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
                GetEmitter().emitIns_S(ins_Load(type), type.EmitSize, tree.LclNum, 0);
            }

            WasmProduceReg(tree);
        }
        else
        {
            assert(genIsValidReg(varDsc.RegNum));
            var type = varDsc.GetRegisterType(tree);
            var wasmLclIndex = regNumberExtensions.WasmRegToIndex(varDsc.RegNum);

            GetEmitter().emitIns_I(INS_local_get, type.EmitSize, unchecked((nint)wasmLclIndex));

            // A register local may have a different type than the tree, so truncate when needed.
            if (tree.Type is TYP_INT && varDsc.Type is TYP_LONG)
            {
                GetEmitter().emitIns(INS_i32_wrap_i64);
            }
        }
    }

    private void genCodeForStoreLclVar(GenTreeLclVar tree)
    {
        assert(tree.Oper is GT_STORE_LCL_VAR);
        var op1 = tree.Op1;
        assert(!op1.IsMultiRegNode);
        genConsumeRegs(op1);

        // Stack stores are rewritten to STOREIND because their address must be first on the Wasm operand stack.
        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);
        var targetReg = tree.RegNum;
        var type = varDsc.GetRegisterType(tree);
        assert(genIsValidReg(targetReg) && varDsc.lvIsRegCandidate);

        var wasmLclIndex = regNumberExtensions.WasmRegToIndex(targetReg);
        GetEmitter().emitIns_I(INS_local_set, type.EmitSize, unchecked((nint)wasmLclIndex));
        genUpdateLifeStore(tree, targetReg, ref varDsc);
    }

    private void genUpdateLifeStore(GenTree tree, regNumber targetReg, ref LclVarDsc varDsc)
    {
        if (targetReg != REG_NA)
        {
            genProduceReg(tree);
        }
        else
        {
            genUpdateLife(tree);
            varDsc.RegNum = REG_STK;
        }
    }

    private void genCodeForPhysReg(GenTreePhysReg tree)
    {
        assert(genIsValidReg(tree.SrcReg));
        var wasmLclIndex = regNumberExtensions.WasmRegToIndex(tree.SrcReg);
        GetEmitter().emitIns_I(INS_local_get, tree.Type.EmitActualSize, unchecked((nint)wasmLclIndex));
        WasmProduceReg(tree);
    }

    private void genCodeForFrameSize(GenTree tree)
    {
        assert(tree.Oper is GT_FRAME_SIZE);
        GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)_compiler.compLclFrameSize));
        WasmProduceReg(tree);
    }

    private void genCodeForIndir(GenTreeIndir tree)
    {
        assert(tree.Oper is GT_IND);
        var type = tree.Type;
        var addr = tree.Addr;

        genConsumeAddress(addr);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            // The base is the address itself unless this is a contained address mode, which is never materialized.
            var baseNode = tree.Base
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "GT_IND base is unavailable for null-check codegen.");
            genEmitNullCheck(GetMultiUseOperandReg(baseNode));
        }

        // TODO-WASM: Memory barriers

        if (addr.IsContained && addr.Oper is not GT_LEA)
        {
            assert(addr.IsIconHandle() && type is not TYP_SIMD12);
            assert(addr.AsIntConCommon().ImmedValNeedsReloc(_compiler));
            WasmCodegenDependencyNotPorted(tree, "Emitter.emitImageBase and Emitter.emitIns_MemargAddress");
        }
        else if (type is TYP_SIMD12)
        {
            genLoadIndTypeSimd12(tree);
        }
        else
        {
            GetEmitter().emitIns_I(ins_Load(type), type.EmitActualSize, genWasmMemargOffset(addr));
        }

        WasmProduceReg(tree);
    }

    private void genLoadIndTypeSimd12(GenTreeIndir tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genLoadIndTypeSimd12));
    }

    private nint genWasmMemargOffset(GenTree addr)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED,
            $"Wasm {nameof(genWasmMemargOffset)} is not ported for {addr.Oper}.");
    }

    private void genCodeForStoreInd(GenTreeStoreInd tree)
    {
        var data = tree.Data;
        var addr = tree.Addr;

        assert(!addr.IsContained || addr.Oper is GT_LEA);
        var offset = genWasmMemargOffset(addr);

        // Consume the address before the data to update liveness in execution order.
        genConsumeAddress(addr);
        genConsumeRegs(data);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            // The base is the address itself unless this is a contained address mode, which is never materialized.
            var baseNode = tree.Base
                ?? throw new FatalJitException(
                    CORJIT_INTERNALERROR,
                    "GT_STOREIND base is unavailable for null-check codegen.");
            genEmitNullCheck(GetMultiUseOperandReg(baseNode));
        }

        var writeBarrierForm = GCInfo.gcIsWriteBarrierCandidate(tree);
        if (writeBarrierForm is not GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            genGCWriteBarrierWasm(writeBarrierForm);
        }
        else // A normal store, not a write-barrier store
        {
            var type = tree.Type;

            // TODO-WASM: Memory barriers
            if (type is TYP_SIMD8)
            {
                // The stack is [address, value]; store the low 8 bytes.
                WasmCodegenDependencyNotPorted(tree, "Emitter.emitIns_MemargLane");
            }
            else if (type is TYP_SIMD12)
            {
                genStoreIndTypeSimd12(tree);
            }
            else
            {
                GetEmitter().emitIns_I(ins_Store(type), type.EmitActualSize, offset);
            }
        }

        genUpdateLife(tree);
    }

    private void genStoreIndTypeSimd12(GenTreeStoreInd tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genStoreIndTypeSimd12));
    }

    private void genCall(GenTreeCall call)
    {
        var thisReg = REG_NA;

        if (call.NeedsNullCheck)
        {
            var thisArg = call.Args.ThisArg
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Call requiring a null check has no this argument.");
            thisReg = GetMultiUseOperandReg(thisArg.Node);
        }

        foreach (var arg in call.Args.EarlyArgs)
        {
            var earlyNode = arg.EarlyNode
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Early call argument has no node.");
            genConsumeRegs(earlyNode);
        }

        foreach (var arg in call.Args.LateArgs)
        {
            var lateNode = arg.LateNode
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Late call argument has no node.");
            genConsumeRegs(lateNode);
        }

        if (call.NeedsNullCheck)
        {
            genEmitNullCheck(thisReg);
        }

        genCallInstruction(call);
        WasmProduceReg(call);
    }

    private unsafe void genCallInstruction(GenTreeCall call)
    {
        ensureCurrentFuncIsUnwindable();

        var parameters = new EmitCallParams
        {
            isJump = call.IsFastTailCall,
            hasAsyncRet = call.IsAsync,
            returnValueCall = call,
        };

#if DEBUG
        if (!call.IsHelperCall())
        {
            parameters.sigInfo = new StrongBox<CORINFO_SIG_INFO>(call._callSig);
        }
#endif

        var target = getCallTarget(call, out parameters.methHnd);
        var typeStack = new ArrayStack<CorInfoWasmType>();

        // Fast tailcalls overwrite the node type with TYP_VOID, but the return_call_indirect
        // signature must still match the current function's return type.
        var callRetType = call.IsFastTailCall ? call._returnType : genActualType(call);
        if (call.ShouldHaveRetBufArg || (callRetType is TYP_VOID))
        {
            typeStack.Push(CORINFO_WASM_TYPE_VOID);
        }
        else if (callRetType is TYP_STRUCT)
        {
            var retWasmType = _compiler.info.compCompHnd->getWasmLowering(call.RetClsHnd);
            // A wider struct is returned through a hidden buffer and must take the branch above.
            assert(retWasmType is not CORINFO_WASM_TYPE_VOID);
            assert(_compiler.info.compCompHnd->getClassSize(call.RetClsHnd) <=
                genTypeSize(WasmClassifier.ToJitType(retWasmType)));
            typeStack.Push(retWasmType);
        }
        else
        {
            // Normalize small integer return types.
            typeStack.Push(WasmValueTypeToCorInfoWasmType(
                regNumberExtensions.ActualTypeToWasmValueType(callRetType)));
        }

        foreach (var arg in call.Args.Args)
        {
            foreach (ref readonly var segment in arg.AbiInfo.Segments)
            {
                assert(segment.IsPassedInRegister);
                var wasmType = regNumberExtensions.WasmRegToType(segment.Register);
                assert(wasmType < WasmValueType.Count);
                typeStack.Push(WasmValueTypeToCorInfoWasmType(wasmType));
            }
        }

        // Report managed call signatures to R2R so it can generate Wasm thunks.
        if (!call.IsHelperCall() && !call.IsUnmanaged)
        {
            CORINFO_SIG_INFO sigInfoLocal = default;
            CORINFO_SIG_INFO* sigInfoCall = null;

            if ((call._callSig.pSig is not null) || (call._callSig.methodSignature is not null))
            {
                sigInfoLocal = call._callSig;
                sigInfoCall = &sigInfoLocal;
            }

            if ((sigInfoCall is null) &&
                (parameters.methHnd != NO_METHOD_HANDLE) &&
                (Compiler.eeGetHelperNum(parameters.methHnd) is CORINFO_HELP_UNDEF))
            {
                _compiler.eeGetMethodSig(parameters.methHnd, out sigInfoLocal);
                sigInfoCall = &sigInfoLocal;

                if ((callRetType is TYP_REF) &&
                    (sigInfoLocal.retType is CORINFO_TYPE_VOID) &&
                    (sigInfoLocal.callConv is CORINFO_CALLCONV_HASTHIS))
                {
                    var methodFlags = _compiler.info.compCompHnd->getMethodAttribs(parameters.methHnd);
                    var stringClass = _compiler.info.compCompHnd->getBuiltinClass(CLASSID_STRING);

                    if (((methodFlags & CORINFO_FLG_CONSTRUCTOR) != 0) &&
                        (stringClass != NO_CLASS_HANDLE) &&
                        (_compiler.info.compCompHnd->getMethodClass(parameters.methHnd) == stringClass))
                    {
                        // String constructors are emitted as static string-returning calls.
                        sigInfoLocal.retType = CORINFO_TYPE_CLASS;
                        sigInfoLocal.callConv = CORINFO_CALLCONV_DEFAULT;
                    }
                }
            }

            if (sigInfoCall is not null)
            {
                _compiler.info.compCompHnd->recordWasmManagedCallSig(sigInfoCall);
            }
        }

        CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature;
        fixed (CorInfoWasmType* types = typeStack.Data())
        {
            wasmSignature = _compiler.info.compCompHnd->getWasmTypeSymbol(types, typeStack.Height());
        }

        // SuppressGCTransition skips the P/Invoke prolog that normally publishes the shadow SP.
        // Publish it here so the native callee allocates below this frame.
        if (call.IsUnmanaged && call.IsSuppressGCTransition)
        {
            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((int)GetStackPointerRegIndex()));
            var stackPointer = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().stackPointer);
            GetEmitter().emitIns_I(INS_global_set, EA_HANDLE_CNS_RELOC, stackPointer);
        }

        if (target is not null)
        {
            // Wasm targets are table indices and must be consumed for call_indirect.
            genConsumeReg(target);

            parameters.callType = EC_INDIR_R;
            genEmitWasmCallWithCurrentGC(call, wasmSignature, ref parameters);
        }
        else
        {
            assert(call.IsHelperCall() || (call._callType is CT_USER_FUNC));

            if (call.IsHelperCall())
            {
                assert(!call.IsFastTailCall);

                if (call._directCallAddress is not null)
                {
                    parameters.addr = call._directCallAddress;
                }
                else
                {
                    var helperNum = Compiler.eeGetHelperNum(parameters.methHnd);
                    noway_assert(helperNum is not CORINFO_HELP_UNDEF);
                    var helperLookup = _compiler.compGetHelperFtn(helperNum);
                    assert(helperLookup.accessType is IAT_VALUE);
                    parameters.addr = helperLookup.addr;
                }
            }
            else
            {
                parameters.addr = call._directCallAddress;
            }

            parameters.callType = EC_FUNC_TOKEN;
            genEmitWasmCallWithCurrentGC(call, wasmSignature, ref parameters);
        }
    }

    private unsafe void genEmitWasmCallWithCurrentGC(
        GenTreeCall call, CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature, ref EmitCallParams parameters)
    {
        parameters.ptrVars = GCInfo.gcVarPtrSetCur;
        parameters.gcrefRegs = GCInfo.gcRegGCrefSetCur;
        parameters.byrefRegs = GCInfo.gcRegByrefSetCur;
        _ = wasmSignature;
        // The managed Wasm emitter has no call-instruction entry point yet.
        WasmCodegenDependencyNotPorted(call, "Emitter.emitIns_Call");
    }

    private unsafe void genEmitWasmCallWithCurrentGC(
        CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature, ref EmitCallParams parameters)
    {
        parameters.ptrVars = GCInfo.gcVarPtrSetCur;
        parameters.gcrefRegs = GCInfo.gcRegGCrefSetCur;
        parameters.byrefRegs = GCInfo.gcRegByrefSetCur;
        _ = wasmSignature;
        WasmCodegenDependencyNotPorted("Emitter.emitIns_Call");
    }

    // Keep Wasm helper signatures and target setup separate from the common helper-call NYI.
    private unsafe void genEmitHelperCallWasm(
        CorInfoHelpFunc helper, int argSize, emitAttr retSize, regNumber callTargetReg = REG_NA)
    {
        ensureCurrentFuncIsUnwindable();

        var parameters = new EmitCallParams();
        var helperFunction = _compiler.compGetHelperFtn(helper);
        parameters.ireg = callTargetReg;

        if (helperFunction.accessType is IAT_VALUE)
        {
            parameters.callType = EC_FUNC_TOKEN;
            parameters.addr = helperFunction.addr;
        }
        else
        {
            assert(helperFunction.accessType is IAT_PVALUE);
            parameters.addr = null;
            parameters.callType = EC_INDIR_R;
        }

        parameters.methHnd = Compiler.eeFindHelper(helper);
        parameters.argSize = argSize;
        parameters.retSize = retSize;

        CorInfoWasmType* types = stackalloc CorInfoWasmType[4];
        nint typeCount = 0;
        var helperIsManaged = false;

#if TARGET_64BIT
        const CorInfoWasmType wasmPointerType = CORINFO_WASM_TYPE_I64;
#else
        const CorInfoWasmType wasmPointerType = CORINFO_WASM_TYPE_I32;
#endif

        switch (helper)
        {
            // Managed throw helpers have no explicit arguments; their stack and PEP arguments follow the result.
            case CORINFO_HELP_RNGCHKFAIL:
            case CORINFO_HELP_OVERFLOW:
            case CORINFO_HELP_THROWDIVZERO:
            case CORINFO_HELP_THROWNULLREF:
            case CORINFO_HELP_THROW_ARGUMENTEXCEPTION:
            case CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION:
            case CORINFO_HELP_THROW_NOT_IMPLEMENTED:
            case CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED:
            case CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED:
            {
                types[0] = CORINFO_WASM_TYPE_VOID;
                types[1] = wasmPointerType;
                types[2] = wasmPointerType;
                typeCount = 3;
                helperIsManaged = true;
                break;
            }

            // RhpAssignRef and RhpCheckedAssignRef.
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                types[0] = CORINFO_WASM_TYPE_VOID;
                types[1] = wasmPointerType;
                types[2] = wasmPointerType;
                typeCount = 3;
                break;
            }

            // RhBulkMoveWithWriteBarrier helpers.
            case CORINFO_HELP_BULK_WRITEBARRIER:
            case CORINFO_HELP_BULK_WRITEBARRIER_SMALL:
            {
                types[0] = CORINFO_WASM_TYPE_VOID;
                types[1] = wasmPointerType;
                types[2] = wasmPointerType;
                types[3] = wasmPointerType;
                typeCount = 4;
                break;
            }

            default:
            {
                JITDUMP(
                    $"Helper '{_compiler.eeGetMethodFullName(parameters.methHnd)}' has no hard-coded signature\n");
                throw new FatalJitException(
                    CORJIT_INTERNALERROR, $"Wasm helper {helper} has no hard-coded signature.");
            }
        }

        // The managed helper signature includes PEP as its last parameter.
        var helperUsesPep = helperIsManaged &&
            _compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_PORTABLE_ENTRY_POINTS);
        if (helperIsManaged && !helperUsesPep)
        {
            typeCount--;
        }

        var wasmSignature = _compiler.info.compCompHnd->getWasmTypeSymbol(types, typeCount);

        if (helperUsesPep)
        {
            // Push the PEP value from the helper's indirection cell.
            assert(helperFunction.accessType is IAT_PVALUE);
            GetEmitter().emitAddressConstant(unchecked((nint)helperFunction.addr));
            GetEmitter().emitIns_I(INS_I_load, EA_PTRSIZE, 0);
        }

        if (parameters.callType is EC_INDIR_R)
        {
            // Push the call target by dereferencing the cell and then the PEP address.
            assert(helperFunction.accessType is IAT_PVALUE);
            GetEmitter().emitAddressConstant(unchecked((nint)helperFunction.addr));
            GetEmitter().emitIns_I(INS_I_load, EA_PTRSIZE, 0);
            GetEmitter().emitIns_I(INS_I_load, EA_PTRSIZE, 0);
        }

        genEmitWasmCallWithCurrentGC(wasmSignature, ref parameters);
    }

    private void genGCWriteBarrierWasm(GCInfo.WriteBarrierForm writeBarrierForm)
    {
        var helper = genWriteBarrierHelperForWriteBarrierForm(writeBarrierForm);
        genEmitHelperCallWasm(helper, 0, EA_PTRSIZE);
    }

    private static CorInfoWasmType WasmValueTypeToCorInfoWasmType(WasmValueType type)
    {
        return type switch
        {
            WasmValueType.I32 => CORINFO_WASM_TYPE_I32,
            WasmValueType.I64 => CORINFO_WASM_TYPE_I64,
            WasmValueType.F32 => CORINFO_WASM_TYPE_F32,
            WasmValueType.F64 => CORINFO_WASM_TYPE_F64,
            WasmValueType.V128 => CORINFO_WASM_TYPE_V128,
            // ExnRef is a valid Wasm type code but is not named in CorInfoWasmType.
            WasmValueType.ExnRef => unchecked((CorInfoWasmType)0x69),
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, "Invalid WebAssembly value type."),
        };
    }

    private void genCodeForNullCheck(GenTreeIndir tree)
    {
        genConsumeAddress(tree.Addr);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            genEmitNullCheck(REG_NA);
        }
        else
        {
            GetEmitter().emitIns(INS_drop);
        }
    }

    private void genEmitNullCheck(regNumber reg)
    {
        WasmCodegenDependencyNotPorted(nameof(genEmitNullCheck));
    }

    private void genRangeCheck(GenTree tree)
    {
        assert(tree.Oper is GT_BOUNDS_CHECK);
        var boundsCheck = tree.AsBoundsChk();

        // Incoming stack operands are index, then length (top of stack).
        genConsumeOperands(boundsCheck);
#if FEATURE_SIMD
        if (varTypeIsSimd(boundsCheck.Index.Type))
        {
            GetEmitter().emitIns(INS_i8x16_splat);
            GetEmitter().emitIns(INS_i8x16_ge_u);
            GetEmitter().emitIns(INS_v128_any_true);
        }
        else
#endif
        {
            GetEmitter().emitIns(INS_I_ge_u);
        }

        genJumpToThrowHlpBlk(boundsCheck.ThrowKind);
    }

    private void genLclHeap(GenTree tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genLclHeap));
    }

    private void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        genConsumeOperands(tree);

        var baseAddress = tree.Arr;
        var index = tree.Index;

        assert(varTypeIsIntegral(index.Type));
        var indexType = genActualType(index.Type);

        // Generate the bounds check if necessary.
        if (tree.IsBoundsChecked)
        {
            var baseReg = GetMultiUseOperandReg(baseAddress);
            var indexReg = GetMultiUseOperandReg(index);

            // Fetch the index, then the array length.
            genEmitLocalGet(indexReg, index.Type);
            genEmitLocalGet(baseReg, WasmValueType.I);
            GetEmitter().emitIns_I(ins_Load(TYP_INT), EA_4BYTE, tree.LenOffset);

            // If the index type is long, extend the array length.
            if (indexType == TYP_LONG)
            {
                GetEmitter().emitIns(INS_i64_extend_u_i32);
            }

            GetEmitter().emitIns(indexType == TYP_LONG ? INS_i64_ge_u : INS_i32_ge_u);
            genJumpToThrowHlpBlk(SCK_RNGCHK_FAIL);
        }

        // Zero extend the index if necessary.
        if (indexType != TYP_I_IMPL)
        {
            GetEmitter().emitIns(INS_i64_extend_u_i32);
        }

        // The result is the address of the array element.
        var scale = tree.ElemSize;
        if (scale > 1)
        {
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, scale);
            GetEmitter().emitIns(INS_I_mul);
        }

        GetEmitter().emitIns(INS_I_add);
        GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, tree.ElemOffset);
        GetEmitter().emitIns(INS_I_add);
        WasmProduceReg(tree);
    }

    private void genLeaInstruction(GenTreeAddrMode lea)
    {
        genConsumeOperands(lea);
        assert(lea.HasIndex || lea.HasBaseAddress);

        if (lea.HasIndex)
        {
            var scale = lea.Scale;

            if (scale > 1)
            {
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, scale);
                GetEmitter().emitIns(INS_I_mul);
            }

            if (lea.HasBaseAddress)
            {
                GetEmitter().emitIns(INS_I_add);
            }
        }

        var offset = lea.Offset;
        if (offset != 0)
        {
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, offset);
            GetEmitter().emitIns(INS_I_add);
        }

        WasmProduceReg(lea);
    }

    private void genCodeForStoreBlk(GenTreeBlk node)
    {
        WasmCodegenDependencyNotPorted(node, nameof(genCodeForStoreBlk));
    }

    private static uint PackIntrinsicAndType(NamedIntrinsic intrinsic, var_types type)
    {
        if (type is TYP_BYREF or TYP_REF)
        {
            type = TYP_I_IMPL;
        }

        // Reserve enough low bits for every var_types value, matching ConstLog2<TYP_COUNT>::value + 1.
        var shift = System.Numerics.BitOperations.Log2((uint)TYP_COUNT) + 1;
        return unchecked(((uint)intrinsic << shift) | (uint)type);
    }

    private void genIntrinsic(GenTreeIntrinsic tree)
    {
        genConsumeOperands(tree);

        var ins = INS_invalid;
        var canHaveMixedTypes = false;
        var intrinsicAndType = PackIntrinsicAndType(tree.IntrinsicName, tree.Type);

        // Native case labels use constexpr calls; guarded patterns preserve those packed-key comparisons.
        switch (intrinsicAndType)
        {
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Abs, TYP_FLOAT):
            {
                ins = INS_f32_abs;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Abs, TYP_DOUBLE):
            {
                ins = INS_f64_abs;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Ceiling, TYP_FLOAT):
            {
                ins = INS_f32_ceil;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Ceiling, TYP_DOUBLE):
            {
                ins = INS_f64_ceil;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Floor, TYP_FLOAT):
            {
                ins = INS_f32_floor;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Floor, TYP_DOUBLE):
            {
                ins = INS_f64_floor;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Max, TYP_FLOAT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MaxNative, TYP_FLOAT):
            {
                ins = INS_f32_max;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Max, TYP_DOUBLE) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MaxNative, TYP_DOUBLE):
            {
                ins = INS_f64_max;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Min, TYP_FLOAT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MinNative, TYP_FLOAT):
            {
                ins = INS_f32_min;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Min, TYP_DOUBLE) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MinNative, TYP_DOUBLE):
            {
                ins = INS_f64_min;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Round, TYP_FLOAT):
            {
                ins = INS_f32_nearest;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Round, TYP_DOUBLE):
            {
                ins = INS_f64_nearest;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Sqrt, TYP_FLOAT):
            {
                ins = INS_f32_sqrt;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Sqrt, TYP_DOUBLE):
            {
                ins = INS_f64_sqrt;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Truncate, TYP_FLOAT):
            {
                ins = INS_f32_trunc;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Truncate, TYP_DOUBLE):
            {
                ins = INS_f64_trunc;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_INT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_INT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_INT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_LONG):
            {
                canHaveMixedTypes = true;
                break;
            }
            default:
            {
                assert(false, "genIntrinsic: Unsupported intrinsic");
                unreached();
                break;
            }
        }

        var needsTruncation = false;
        var needsExtension = false;

        if (canHaveMixedTypes)
        {
            var treeType = tree.Type;
            var operandType = genActualType(tree.Op1.Type);

            needsTruncation = (operandType == TYP_LONG) && (treeType == TYP_INT);
            needsExtension = (operandType == TYP_INT) && (treeType == TYP_LONG);

            var operandIntrinsicAndType = PackIntrinsicAndType(tree.IntrinsicName, operandType);
            switch (operandIntrinsicAndType)
            {
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_INT):
                {
                    ins = INS_i32_clz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG):
                {
                    ins = INS_i64_clz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_INT):
                {
                    ins = INS_i32_ctz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG):
                {
                    ins = INS_i64_ctz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_INT):
                {
                    ins = INS_i32_popcnt;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_LONG):
                {
                    ins = INS_i64_popcnt;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        GetEmitter().emitIns(ins);

        if (needsTruncation)
        {
            GetEmitter().emitIns(INS_i32_wrap_i64);
        }
        else if (needsExtension)
        {
            GetEmitter().emitIns(INS_i64_extend_u_i32);
        }

        WasmProduceReg(tree);
    }

    private void genCodeForAsyncContinuation(GenTree tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForAsyncContinuation));
    }

    private void genReturnSuspend(GenTreeUnOp tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genReturnSuspend));
    }

    private static void WasmCodegenDependencyNotPorted(GenTree tree, string dependency)
    {
        throw new FatalJitException(CORJIT_SKIPPED, $"Wasm {dependency} is not ported for {tree.Oper}.");
    }

    private static void WasmCodegenDependencyNotPorted(string dependency)
    {
        throw new FatalJitException(CORJIT_SKIPPED, $"Wasm {dependency} is not ported.");
    }
}
#endif
