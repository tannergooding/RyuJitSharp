// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genReturnSuspend(GenTreeUnOp tree)
    {
#if TARGET_WASM
        assert(tree.Oper is GT_RETURN_SUSPEND);

        var op = tree.Op1;
        assert(op.Type is TYP_REF);
        genConsumeReg(op);
        genStoreAsyncContinuationGlobal();

        var retNativeType = _compiler.info.compRetNativeType;
        if ((retNativeType is not TYP_VOID) && (_compiler.info.compRetBuffArg == BAD_VAR_NUM))
        {
            // Keep the Wasm return stack well-typed after transferring the continuation to its global.
            var emit = GetEmitter();
            switch (genActualType(retNativeType))
            {
                case TYP_INT:
                case TYP_REF:
                case TYP_BYREF:
                {
                    emit.emitIns_I(INS_i32_const, retNativeType.EmitActualSize, unchecked((nint)0));
                    break;
                }

                case TYP_LONG:
                {
                    emit.emitIns_I(INS_i64_const, EA_8BYTE, unchecked((nint)0));
                    break;
                }

                case TYP_FLOAT:
                {
                    emit.emitIns_I(INS_f32_const, EA_4BYTE, unchecked((nint)0));
                    break;
                }

                case TYP_DOUBLE:
                {
                    emit.emitIns_I(INS_f64_const, EA_8BYTE, unchecked((nint)0));
                    break;
                }

#if FEATURE_SIMD
                case TYP_SIMD16:
                {
                    var zero = new byte[16];
                    emit.emitIns_V128Imm(INS_v128_const, zero);
                    break;
                }
#endif

                default:
                {
                    unreached();
                    break;
                }
            }
        }
#else
        Emitter.RequireSupportedInstructionRecording();
        var op = tree.Op1;
        assert(op.Type == TYP_REF);
        var reg = genConsumeReg(op);
        inst_Mov(TYP_REF, REG_ASYNC_CONTINUATION_RET, reg, canSkip: true);
        _gcInfo.gcMarkRegPtrVal(REG_ASYNC_CONTINUATION_RET, TYP_REF);

        var descriptor = _compiler.compRetTypeDesc;
        var count = descriptor.ReturnRegCount;
        for (byte i = 0; i < count; i++)
        {
            if (varTypeIsGC(descriptor.GetReturnRegType(i)))
            {
                var returnReg = descriptor.GetAbiReturnReg(i, _compiler.info.compCallConv);
                instGen_Set_Reg_To_Zero(EA_PTRSIZE, returnReg);
            }
        }
        genMarkReturnGCInfo();
#endif
    }

    public void genCodeForAsyncContinuation(GenTree tree)
    {
#if TARGET_WASM
        assert(tree.Oper is GT_ASYNC_CONTINUATION);
        assert(tree.Type is TYP_REF);

        var asyncContinuation = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().asyncContinuation);
        GetEmitter().emitIns_I(INS_global_get, EA_SET_FLG(EA_GCREF, EA_CNS_RELOC_FLG), asyncContinuation);
        WasmProduceReg(tree);
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_ASYNC_CONTINUATION);
        inst_Mov(tree.Type, tree.RegNum, REG_ASYNC_CONTINUATION_RET, canSkip: true);
        genTransferRegGCState(tree.RegNum, REG_ASYNC_CONTINUATION_RET);
        genProduceReg(tree);
#endif
    }

    public void genNonLocalJmp(GenTreeUnOp tree)
    {
#if TARGET_ARM
        NYI_ARM("GT_NONLOCAL_JMP is not supported on arm32");
        throw new FatalJitException(CORJIT_SKIPPED);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Nonlocal jump generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        genConsumeOperands(tree);
        inst_TT(INS_i_jmp, EA_PTRSIZE, tree.Op1);
#endif
    }

    public void genFtnEntry(GenTree tree)
    {
#if TARGET_ARM
        NYI_ARM("GT_FTN_ENTRY is not supported on arm32");
        throw new FatalJitException(CORJIT_SKIPPED);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Function-entry address generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, Emitter.emitGetFirstPrologIG(), tree.RegNum);
        genProduceReg(tree);
#endif
    }

    public void genPatchpoint(GenTreeUnOp tree)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Patchpoint generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_PATCHPOINT or GT_PATCHPOINT_FORCED);
        genConsumeOperands(tree);
        genCopyRegIfNeeded(tree.Op1, REG_ARG_0);
        if (tree.Oper == GT_PATCHPOINT)
        {
            genCopyRegIfNeeded(tree.AsOp().Op2, REG_ARG_1);
        }
        var helper = tree.Oper == GT_PATCHPOINT ? CORINFO_HELP_PATCHPOINT : CORINFO_HELP_PATCHPOINT_FORCED;
        genEmitHelperCall(helper, 0, EA_UNKNOWN);

#if !TARGET_XARCH
        // After the transfer, the return-address slot may have moved. GC metadata
        // must disable hijacking so unhijacking cannot write to the old slot.
        HasTailCalls = true;
#endif

#if TARGET_XARCH
        // A tail-jump prefix would falsely tell the Windows unwinder that the
        // epilog has restored RSP and callee-saved registers.
        Emitter.emitIns_R(INS_i_jmp, EA_PTRSIZE, REG_INTRET);
#elif TARGET_ARM64
        Emitter.emitIns_R(INS_br, EA_PTRSIZE, REG_INTRET);
#elif TARGET_ARM
        Emitter.emitIns_R(INS_bx, EA_PTRSIZE, REG_INTRET);
#elif TARGET_LOONGARCH64
        Emitter.emitIns_R_R_I(INS_jirl, EA_PTRSIZE, REG_R0, REG_INTRET, (nint)0);
#elif TARGET_RISCV64
        Emitter.emitIns_R_R_I(INS_jalr, EA_PTRSIZE, REG_R0, REG_INTRET, (nint)0);
#else
#error Unsupported target architecture for GT_PATCHPOINT
#endif
#endif
    }
}
