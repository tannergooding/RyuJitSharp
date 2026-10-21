// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCodeForTreeNode(GenTree tree)
    {
        if (!Configuration.TARGET_XARCH)
        {
            if (Configuration.TARGET_ARMARCH)
            {
                genCodeForTreeNodeArmArch(tree);
                return;
            }
            if (!Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Node instruction generation requires xarch.");
                return;
            }

            var targetRegNonXarch = tree.RegNum;
            var targetTypeNonXarch = tree.Type;
            var emit = GetEmitter();

            genPrepareNodeConsumption(tree);

            if (tree.IsReuseRegVal)
            {
                assert(tree.Oper is GT_CNS_INT or GT_CNS_DBL);
                JITDUMP("  TreeNode is marked ReuseReg\n");
                return;
            }

            if (tree.IsContained)
            {
                return;
            }

            switch (tree.Oper)
            {
                case GT_START_NONGC:
                {
                    emit.emitDisableGC();
                    break;
                }

                case GT_START_PREEMPTGC:
                {
                    _gcInfo.gcMarkRegSetNpt(new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask));
                    genDefineTempLabel(genCreateTempLabel());
                    break;
                }

                case GT_PROF_HOOK:
                {
                    noway_assert(_compiler.compIsProfilerHookNeeded);
                    if (Configuration.PROFILING_SUPPORTED)
                    {
                        genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
                    }
                    break;
                }

                case GT_LCLHEAP:
                {
                    genLclHeap(tree);
                    break;
                }

                case GT_CNS_INT:
                {
                    if (targetTypeNonXarch is TYP_DOUBLE or TYP_FLOAT)
                    {
                        tree._oper = GT_CNS_DBL;
                    }
                    goto case GT_CNS_DBL;
                }

                case GT_CNS_DBL:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genSetRegToConstRiscV64(targetRegNonXarch, targetTypeNonXarch, tree);
                    }
                    else
                    {
                        genSetRegToConstLoongArch64(targetRegNonXarch, targetTypeNonXarch, tree);
                    }
                    genProduceReg(tree);
                    break;
                }

                case GT_NOT:
                case GT_NEG:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForNegNotRiscV64(tree.AsUnOp());
                    }
                    else
                    {
                        genCodeForNegNotLoongArch64(tree.AsUnOp());
                    }
                    break;
                }

                case GT_BSWAP:
                case GT_BSWAP16:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForBswapRiscV64(tree);
                    }
                    else
                    {
                        genCodeForBswapLoongArch64(tree);
                    }
                    break;
                }

                case GT_MOD:
                case GT_UMOD:
                case GT_DIV:
                case GT_UDIV:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForDivModRiscV64(tree.AsOp());
                    }
                    else
                    {
                        genCodeForDivModLoongArch64(tree.AsOp());
                    }
                    break;
                }

                case GT_OR:
                case GT_XOR:
                case GT_AND:
                case GT_AND_NOT:
                case GT_OR_NOT:
                case GT_XOR_NOT:
                case GT_BIT_SET:
                case GT_BIT_CLEAR:
                case GT_BIT_INVERT:
                {
                    if (!Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V-only node reached non-RISC-V code generation.");
                        return;
                    }
                    assert(varTypeIsIntegralOrI(tree.Type));
                    goto case GT_ADD;
                }

                case GT_ADD:
                case GT_SUB:
                case GT_MUL:
                {
                    genConsumeOperands(tree.AsOp());
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForBinaryRiscV64(tree.AsOp());
                    }
                    else
                    {
                        genCodeForBinaryLoongArch64(tree.AsOp());
                    }
                    break;
                }

                case GT_LSH:
                case GT_RSH:
                case GT_RSZ:
                case GT_ROR:
                case GT_ROL:
                {
                    if (!Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V rotate node reached non-RISC-V code generation.");
                        return;
                    }
                    genCodeForShiftRiscV64(tree);
                    break;
                }

                case GT_CAST:
                {
                    genCodeForCast(tree.AsCast());
                    break;
                }

                case GT_BITCAST:
                {
                    genCodeForBitCast(tree.AsUnOp());
                    break;
                }

                case GT_LCL_ADDR:
                {
                    genCodeForLclAddr(tree.AsLclFld());
                    break;
                }

                case GT_LCL_FLD:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForLclFldRiscV64(tree.AsLclFld());
                    }
                    else
                    {
                        genCodeForLclFldLoongArch64(tree.AsLclFld());
                    }
                    break;
                }

                case GT_LCL_VAR:
                {
                    genCodeForLclVar(tree.AsLclVar());
                    break;
                }

                case GT_STORE_LCL_FLD:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForStoreLclFldRiscV64(tree.AsLclFld());
                    }
                    else
                    {
                        genCodeForStoreLclFldLoongArch64(tree.AsLclFld());
                    }
                    break;
                }

                case GT_STORE_LCL_VAR:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForStoreLclVarRiscV64(tree.AsLclVar());
                    }
                    else
                    {
                        genCodeForStoreLclVarLoongArch64(tree.AsLclVar());
                    }
                    break;
                }

                case GT_RETFILT:
                case GT_RETURN:
                {
                    genReturn(tree);
                    break;
                }

                case GT_LEA:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genLeaInstructionRiscV64(tree.AsAddrMode());
                    }
                    else
                    {
                        genLeaInstructionLoongArch64(tree.AsAddrMode());
                    }
                    break;
                }

                case GT_INDEX_ADDR:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForIndexAddrRiscV64(tree.AsIndexAddr());
                    }
                    else
                    {
                        genCodeForIndexAddrLoongArch64(tree.AsIndexAddr());
                    }
                    break;
                }

                case GT_IND:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForIndirRiscV64(tree.AsIndir());
                    }
                    else
                    {
                        genCodeForIndirLoongArch64(tree.AsIndir());
                    }
                    break;
                }

                case GT_INC_SATURATE:
                {
                    genCodeForIncSaturate(tree);
                    break;
                }

                case GT_MULHI:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForMulHiRiscV64(tree.AsOp());
                    }
                    else
                    {
                        genCodeForMulHiLoongArch64(tree.AsOp());
                    }
                    break;
                }

                case GT_SWAP:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForSwapRiscV64(tree.AsOp());
                    }
                    else
                    {
                        genCodeForSwapLoongArch64(tree.AsOp());
                    }
                    break;
                }

                case GT_JMP:
                {
                    genJmpPlaceArgs(tree);
                    break;
                }

                case GT_CKFINITE:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCkfiniteRiscV64(tree);
                    }
                    else
                    {
                        genCkfiniteLoongArch64(tree);
                    }
                    break;
                }

                case GT_INTRINSIC:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genIntrinsicRiscV64(tree.AsIntrinsic());
                    }
                    else
                    {
                        genIntrinsicLoongArch64(tree.AsIntrinsic());
                    }
                    break;
                }

                case GT_HWINTRINSIC:
                {
                    if (!Configuration.FEATURE_HW_INTRINSICS)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Hardware intrinsics are unavailable for this target.");
                        return;
                    }
                    genHWIntrinsic(tree.AsHWIntrinsic());
                    break;
                }

                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_LE:
                case GT_GE:
                case GT_GT:
                {
                    genConsumeOperands(tree.AsOp());
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForCompareRiscV64(tree.AsOp());
                    }
                    else
                    {
                        genCodeForCompareLoongArch64(tree.AsOp());
                    }
                    break;
                }

                case GT_JCC:
                {
                    if (!Configuration.TARGET_LOONGARCH64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "LoongArch conditional branch reached another target.");
                        return;
                    }
                    var currentBlock = _compiler.compCurBB;
                    assert(currentBlock is not null);

                    var block = currentBlock!;
                    var targetBlock = block.Kind is BBJ_COND ? block.TrueTarget : block.Target;
                    if (Configuration.DEBUG && !Configuration.FEATURE_FIXED_OUT_ARGS)
                    {
                        assert((unchecked((uint)targetBlock.bbTgtStkDepth * sizeof(int)) == genStackLevel)
                            || IsFramePointerUsed);
                    }

                    var jcc = tree.AsCC();
                    assert(jcc.Condition.Code is GenCondition.EQ or GenCondition.NE);
                    var ins = jcc.Condition.Code is GenCondition.EQ ? INS_bceqz : INS_bcnez;
                    emit.emitIns_J(ins, targetBlock, 1);

                    if (block.Kind is BBJ_COND)
                    {
                        var falseTarget = block.FalseTarget;
                        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
                        {
                            inst_JMP(EJ_jmp, falseTarget);
                        }
                    }
                    break;
                }

                case GT_SELECT:
                {
                    if (!Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V select node reached another target.");
                        return;
                    }
                    genCodeForSelectRiscV64(tree.AsConditional());
                    break;
                }

                case GT_JCMP:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForJumpCompareRiscV64(tree.AsOpCC());
                    }
                    else
                    {
                        genCodeForJumpCompareLoongArch64(tree.AsOpCC());
                    }
                    break;
                }

                case GT_RETURNTRAP:
                {
                    genCodeForReturnTrap(tree.AsUnOp());
                    break;
                }

                case GT_STOREIND:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genCodeForStoreIndRiscV64(tree.AsStoreInd());
                    }
                    else
                    {
                        genCodeForStoreIndLoongArch64(tree.AsStoreInd());
                    }
                    break;
                }

                case GT_COPY:
                {
                    break;
                }

                case GT_FIELD_LIST:
                {
                    assert(false, "LIST, FIELD_LIST nodes should always be marked contained.");
                    break;
                }

                case GT_PUTARG_STK:
                {
                    genPutArgStk(tree.AsPutArgStk());
                    break;
                }

                case GT_PUTARG_REG:
                {
                    genPutArgReg(tree.AsUnOp());
                    break;
                }

                case GT_CALL:
                {
                    genCall(tree.AsCall());
                    break;
                }

                case GT_MEMORYBARRIER:
                {
                    var barrierKind = (tree.Flags & GTF_MEMORYBARRIER_LOAD) != 0
                        ? BarrierKind.BARRIER_LOAD_ONLY
                        : (tree.Flags & GTF_MEMORYBARRIER_STORE) != 0
                            ? BarrierKind.BARRIER_STORE_ONLY
                            : BarrierKind.BARRIER_FULL;
                    instGen_MemoryBarrier(barrierKind);
                    break;
                }

                case GT_XCHG:
                case GT_XADD:
                case GT_XORR:
                case GT_XAND:
                {
                    if ((tree.Oper is GT_XORR or GT_XAND) && !Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V atomic node reached another target.");
                        return;
                    }
                    genLockedInstructions(tree.AsOp());
                    break;
                }

                case GT_CMPXCHG:
                {
                    genCodeForCmpXchg(tree.AsCmpXchg());
                    break;
                }

                case GT_RELOAD:
                {
                    break;
                }

                case GT_NOP:
                {
                    break;
                }

                case GT_KEEPALIVE:
                {
                    var operand = tree.AsOp().Op1;
                    if (operand.IsContained)
                    {
                        genUpdateLife(operand);
                    }
                    else
                    {
                        _ = genConsumeReg(operand);
                    }
                    break;
                }

                case GT_NO_OP:
                {
                    instGen(INS_nop);
                    break;
                }

                case GT_BOUNDS_CHECK:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genRangeCheckRiscV64(tree);
                    }
                    else
                    {
                        genRangeCheckLoongArch64(tree);
                    }
                    break;
                }

                case GT_PHYSREG:
                {
                    genCodeForPhysReg(tree.AsPhysReg());
                    break;
                }

                case GT_NULLCHECK:
                {
                    genCodeForNullCheck(tree.AsIndir());
                    break;
                }

                case GT_CATCH_ARG:
                {
                    genCodeForCatchArg(tree);
                    break;
                }

                case GT_LABEL:
                {
                    genPendingCallLabel = genCreateTempLabel();
                    var loadInstruction = Configuration.TARGET_RISCV64 ? INS_ld : INS_ld_d;
                    emit.emitIns_R_L(loadInstruction, EA_PTRSIZE, genPendingCallLabel, targetRegNonXarch);
                    break;
                }

                case GT_RETURN_SUSPEND:
                {
                    genReturnSuspend(tree.AsUnOp());
                    break;
                }

                case GT_ASYNC_CONTINUATION:
                {
                    genCodeForAsyncContinuation(tree);
                    break;
                }

                case GT_ASYNC_RESUME_INFO:
                {
                    genAsyncResumeInfo(tree.AsVal());
                    break;
                }

                case GT_RECORD_ASYNC_RESUME:
                {
                    genRecordAsyncResume(tree.AsVal());
                    break;
                }

                case GT_FTN_ENTRY:
                {
                    genFtnEntry(tree);
                    break;
                }

                case GT_NONLOCAL_JMP:
                {
                    genNonLocalJmp(tree.AsUnOp());
                    break;
                }

                case GT_STORE_BLK:
                {
                    genCodeForStoreBlk(tree.AsBlk());
                    break;
                }

                case GT_JMPTABLE:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genJumpTableRiscV64(tree);
                    }
                    else
                    {
                        genJumpTableLoongArch64(tree);
                    }
                    break;
                }

                case GT_SWITCH_TABLE:
                {
                    if (Configuration.TARGET_RISCV64)
                    {
                        genTableBasedSwitchRiscV64(tree);
                    }
                    else
                    {
                        genTableBasedSwitchLoongArch64(tree);
                    }
                    break;
                }

                case GT_IL_OFFSET:
                {
                    break;
                }

                case GT_PATCHPOINT:
                case GT_PATCHPOINT_FORCED:
                {
                    genPatchpoint(tree.AsUnOp());
                    break;
                }

                case GT_SH1ADD:
                case GT_SH1ADD_UW:
                case GT_SH2ADD:
                case GT_SH2ADD_UW:
                case GT_SH3ADD:
                case GT_SH3ADD_UW:
                {
                    if (!Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V arithmetic node reached another target.");
                        return;
                    }
                    genCodeForShxadd(tree.AsOp());
                    break;
                }

                case GT_ADD_UW:
                {
                    if (!Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V arithmetic node reached another target.");
                        return;
                    }
                    genCodeForAddUw(tree.AsOp());
                    break;
                }

                case GT_SLLI_UW:
                {
                    if (!Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "RISC-V arithmetic node reached another target.");
                        return;
                    }
                    genCodeForSlliUw(tree.AsOp());
                    break;
                }

                default:
                {
                    if (Configuration.DEBUG)
                    {
                        var message = Configuration.TARGET_RISCV64
                            ? $"NYI: Unimplemented node type {tree.Oper}"
                            : $"NYI: Unimplemented node type {tree.Oper}\n";
                        NYIRAW(message);
                    }
                    else
                    {
                        if (Configuration.TARGET_RISCV64)
                        {
                            NYI_RISCV64("some node type in genCodeForTreeNode-----unimplemented/unused on RISCV64 yet----");
                        }
                        else
                        {
                            NYI("unimplemented node");
                        }
                    }
                    if (Configuration.TARGET_RISCV64)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Unimplemented node type in code generation.");
                        return;
                    }
                    break;
                }
            }
            return;
        }

        var targetReg = !Configuration.TARGET_64BIT && tree.Type == TYP_LONG ? REG_NA : tree.RegNum;
        var targetType = tree.Type;
        // Operand-use ordering is local to each generated node.
        genPrepareNodeConsumption(tree);
        if (tree.IsReuseRegVal)
        {
            genCodeForReuseVal(tree);
            return;
        }
        if (tree.IsContained)
        {
            return;
        }

        switch (tree.Oper)
        {
            case GT_START_NONGC:
            {
                Emitter.emitDisableGC();
                break;
            }

            case GT_START_PREEMPTGC:
            {
                // Kill callee-saved GC registers and propagate the state to the emitter.
                _gcInfo.gcMarkRegSetNpt(new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask));
                genDefineTempLabel(genCreateTempLabel());
                break;
            }

            case GT_PROF_HOOK:
            {
                if (Configuration.PROFILING_SUPPORTED)
                {
                    noway_assert(_compiler.compIsProfilerHookNeeded);
                    // This node currently represents only the tail-call profiler hook.
                    genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
                }
                break;
            }

            case GT_LCLHEAP:
            {
                genLclHeap(tree);
                break;
            }

            case GT_CNS_INT:
            {
                if (Configuration.TARGET_X86)
                {
                    assert(!tree.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL));
                }
                goto case GT_CNS_DBL;
            }
            case GT_CNS_DBL:
            case GT_CNS_VEC:
            case GT_CNS_MSK:
            {
                if ((tree.Oper is GT_CNS_VEC) && !Configuration.FEATURE_SIMD)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "SIMD constants are unavailable for this target.");
                    return;
                }
                if ((tree.Oper is GT_CNS_MSK) &&
                    (!Configuration.FEATURE_SIMD || !Configuration.FEATURE_MASKED_HW_INTRINSICS))
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Masked constants are unavailable for this target.");
                    return;
                }
                genSetRegToConstXarch(targetReg, targetType, tree);
                genProduceReg(tree);
                break;
            }

            case GT_NOT:
            case GT_NEG:
            {
                genCodeForNegNotXarch(tree.AsUnOp());
                break;
            }

            case GT_BSWAP:
            case GT_BSWAP16:
            {
                genCodeForBswapXarch(tree);
                break;
            }

            case GT_DIV:
            {
                if (varTypeIsFloating(targetType))
                {
                    genCodeForBinaryXarch(tree.AsOp());
                    break;
                }
                goto case GT_MOD;
            }

            case GT_MOD:
            case GT_UMOD:
            case GT_UDIV:
            {
                genCodeForDivModXarch(tree.AsOp());
                break;
            }

            case GT_OR:
            case GT_XOR:
            case GT_AND:
            {
                assert(varTypeIsIntegralOrI(targetType));
                goto case GT_ADD;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
            {
                genCodeForBinaryXarch(tree.AsOp());
                break;
            }

            case GT_BIT_SET:
            case GT_BIT_CLEAR:
            case GT_BIT_INVERT:
            {
                genCodeForBitOp(tree.AsOp());
                break;
            }

            case GT_MUL:
            {
                if (varTypeIsFloating(targetType))
                {
                    genCodeForBinaryXarch(tree.AsOp());
                    break;
                }
                genCodeForMul(tree.AsOp());
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
            {
                genCodeForShiftXarch(tree);
                break;
            }

            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                if (Configuration.TARGET_64BIT)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_SKIPPED,
                        FatalJitErrorCode.DirectFailureMessage087
                    );
                    return;
                }
                genCodeForShiftLongXarch(tree);
                break;
            }

            case GT_CAST:
            {
                genCodeForCast(tree.AsCast());
                break;
            }

            case GT_BITCAST:
            {
                genCodeForBitCast(tree.AsUnOp());
                break;
            }

            case GT_LCL_ADDR:
            {
                genCodeForLclAddr(tree.AsLclFld());
                break;
            }

            case GT_LCL_FLD:
            {
                genCodeForLclFldXarch(tree.AsLclFld());
                break;
            }

            case GT_LCL_VAR:
            {
                genCodeForLclVarXarch(tree.AsLclVar());
                break;
            }

            case GT_STORE_LCL_FLD:
            {
                genCodeForStoreLclFldXarch(tree.AsLclFld());
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                genCodeForStoreLclVarXarch(tree.AsLclVar());
                break;
            }

            case GT_RETFILT:
            case GT_RETURN:
            {
                genReturn(tree);
                break;
            }

            case GT_SWIFT_ERROR_RET:
            {
                if (!Configuration.SWIFT_SUPPORT)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_SKIPPED,
                        FatalJitErrorCode.DirectFailureMessage088
                    );
                    return;
                }
                genSwiftErrorReturn(tree);
                break;
            }

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
                genPatchpoint(tree.AsUnOp());
                break;
            }

            case GT_LEA:
            {
                genLeaInstructionXarch(tree.AsAddrMode());
                break;
            }

            case GT_INDEX_ADDR:
            {
                genCodeForIndexAddrXarch(tree.AsIndexAddr());
                break;
            }

            case GT_IND:
            {
                genCodeForIndirXarch(tree.AsIndir());
                break;
            }

            case GT_INC_SATURATE:
            {
                genCodeForIncSaturate(tree);
                break;
            }

            case GT_MULHI:
            case GT_MUL_LONG:
            {
                if ((tree.Oper is GT_MUL_LONG) && !Configuration.TARGET_X86)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_SKIPPED,
                        FatalJitErrorCode.DirectFailureMessage089
                    );
                    return;
                }
                genCodeForMulHiXarch(tree.AsOp());
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsicXarch(tree.AsIntrinsic());
                break;
            }

            case GT_HWINTRINSIC:
            {
                if (!Configuration.FEATURE_HW_INTRINSICS)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Hardware intrinsics are unavailable for this target.");
                    return;
                }
                genHWIntrinsic(tree.AsHWIntrinsic());
                break;
            }
            case GT_CKFINITE:
            {
                genCkfiniteXarch(tree);
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_TEST_EQ:
            case GT_TEST_NE:
            case GT_BITTEST_EQ:
            case GT_BITTEST_NE:
            case GT_CMP:
            case GT_TEST:
            case GT_BT:
            {
                genConsumeOperands(tree.AsOp());
                genCodeForCompareXarch(tree.AsOp());
                break;
            }

            case GT_JTRUE:
            {
                genCodeForJTrueXarch(tree.AsUnOp());
                break;
            }

            case GT_JCC:
            {
                genCodeForJcc(tree.AsCC());
                break;
            }

            case GT_SETCC:
            {
                genCodeForSetcc(tree.AsCC());
                break;
            }

            case GT_SELECT:
            {
                genCodeForSelectXarch(tree.AsConditional());
                break;
            }

            case GT_SELECTCC:
            {
                genCodeForSelectXarch(tree.AsOp());
                break;
            }

            case GT_RETURNTRAP:
            {
                genCodeForReturnTrap(tree.AsUnOp());
                break;
            }

            case GT_STOREIND:
            {
                genCodeForStoreIndXarch(tree.AsStoreInd());
                break;
            }

            case GT_COPY:
            case GT_RELOAD:
            {
                // The consuming parent performs the copy or reload.
                break;
            }

            case GT_FIELD_LIST:
            {
                assert(false, "!\"LIST, FIELD_LIST nodes should always be marked contained.\"");
                break;
            }

            case GT_SWAP:
            {
                genCodeForSwapXarch(tree.AsOp());
                break;
            }

            case GT_PUTARG_STK:
            {
                genPutArgStk(tree.AsPutArgStk());
                break;
            }

            case GT_PUTARG_REG:
            {
                genPutArgReg(tree.AsUnOp());
                break;
            }

            case GT_CALL:
            {
                genCall(tree.AsCall());
                break;
            }

            case GT_JMP:
            {
                genJmpPlaceArgs(tree);
                break;
            }

            case GT_LOCKADD:
            {
                genCodeForLockAdd(tree.AsOp());
                break;
            }

            case GT_XCHG:
            case GT_XADD:
            case GT_XORR:
            case GT_XAND:
            {
                genLockedInstructions(tree.AsOp());
                break;
            }

            case GT_MEMORYBARRIER:
            {
                var kind = (tree.Flags & GTF_MEMORYBARRIER_LOAD) != 0
                    ? BarrierKind.BARRIER_LOAD_ONLY
                    : (tree.Flags & GTF_MEMORYBARRIER_STORE) != 0
                        ? BarrierKind.BARRIER_STORE_ONLY : BarrierKind.BARRIER_FULL;
                instGen_MemoryBarrier(kind);
                break;
            }

            case GT_CMPXCHG:
            {
                genCodeForCmpXchg(tree.AsCmpXchg());
                break;
            }

            case GT_NOP:
            case GT_IL_OFFSET:
            {
                // These nodes are markers, not instructions.
                break;
            }

            case GT_SWIFT_ERROR:
            {
                if (!Configuration.SWIFT_SUPPORT)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_SKIPPED,
                        FatalJitErrorCode.DirectFailureMessage090
                    );
                    return;
                }
                genCodeForSwiftErrorReg(tree);
                break;
            }

            case GT_KEEPALIVE:
            {
                genConsumeRegs(tree.AsUnOp().Op1);
                break;
            }

            case GT_NO_OP:
            {
                Emitter.emitIns_Nop(1);
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                genRangeCheckXarch(tree);
                break;
            }

            case GT_PHYSREG:
            {
                genCodeForPhysReg(tree.AsPhysReg());
                break;
            }

            case GT_NULLCHECK:
            {
                genCodeForNullCheckXarch(tree.AsIndir());
                break;
            }

            case GT_CATCH_ARG:
            {
                genCodeForCatchArg(tree);
                break;
            }

            case GT_LABEL:
            {
                genPendingCallLabel = genCreateTempLabel();
                Emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, genPendingCallLabel, targetReg);
                break;
            }

            case GT_RETURN_SUSPEND:
            {
                genReturnSuspend(tree.AsUnOp());
                break;
            }

            case GT_ASYNC_CONTINUATION:
            {
                genCodeForAsyncContinuation(tree);
                break;
            }

            case GT_ASYNC_RESUME_INFO:
            {
                genAsyncResumeInfo(tree.AsVal());
                break;
            }

            case GT_RECORD_ASYNC_RESUME:
            {
                genRecordAsyncResume(tree.AsVal());
                break;
            }

            case GT_FTN_ENTRY:
            {
                genFtnEntry(tree);
                break;
            }

            case GT_NONLOCAL_JMP:
            {
                genNonLocalJmp(tree.AsUnOp());
                break;
            }

            case GT_STORE_BLK:
            {
                genCodeForStoreBlk(tree.AsBlk());
                break;
            }

            case GT_JMPTABLE:
            {
                genJumpTableXarch(tree);
                break;
            }

            case GT_SWITCH_TABLE:
            {
                genTableBasedSwitchXarch(tree);
                break;
            }

            case GT_LONG:
            {
                if (Configuration.TARGET_64BIT)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_SKIPPED,
                        FatalJitErrorCode.DirectFailureMessage091
                    );
                    return;
                }
                assert(tree.IsUsedFromReg);
                genConsumeRegs(tree);
                break;
            }

            case GT_CCMP:
            {
                if (!Configuration.TARGET_AMD64)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "AMD64 conditional compare reached another target.");
                    return;
                }
                genCodeForCCMP(tree.AsCCMP());
                break;
            }

            default:
            {
                if (Configuration.DEBUG)
                {
                    // Native OpName formats the complete GT_ operator name.
                    NYIRAW($"NYI: Unimplemented node type {tree.Oper}\n");
                }
                assert(false, "!\"Unknown node in codegen\"");
                break;
            }
        }
    }

    public void genCodeForSwiftErrorReg(GenTree tree)
    {
        if (!Configuration.SWIFT_SUPPORT || Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Swift error registers are unavailable for this target.");
            return;
        }

        assert(tree.Oper == GT_SWIFT_ERROR);
        var targetType = tree.Type;
        var targetReg = tree.RegNum;

        // LSRA also assigns the ABI error register as the destination (see LinearScan::BuildNode).
        assert(targetReg == TargetRuntimeMetadata.SwiftErrorRegister);
        inst_Mov(targetType, targetReg, TargetRuntimeMetadata.SwiftErrorRegister, canSkip: true);
        genTransferRegGCState(targetReg, TargetRuntimeMetadata.SwiftErrorRegister);
        genProduceReg(tree);
    }
}
