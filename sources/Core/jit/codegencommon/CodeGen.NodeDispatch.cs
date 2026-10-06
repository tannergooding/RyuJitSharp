// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCodeForTreeNode(GenTree tree)
    {
#if !TARGET_XARCH
#if TARGET_LOONGARCH64
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var emit = GetEmitter();

#if DEBUG
        lastConsumedNode = null;
        if (_compiler.verbose)
        {
            _compiler.gtDispLIRNode(tree, "Generating: ");
        }
#endif

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
                _gcInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_INT_CALLEE_SAVED));
                genDefineTempLabel(genCreateTempLabel());
                break;
            }

            case GT_PROF_HOOK:
            {
                noway_assert(_compiler.compIsProfilerHookNeeded);
#if PROFILING_SUPPORTED
                genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
#endif
                break;
            }

            case GT_LCLHEAP:
            {
                genLclHeap(tree);
                break;
            }

            case GT_CNS_INT:
            {
                if (targetType is TYP_DOUBLE or TYP_FLOAT)
                {
                    tree._oper = GT_CNS_DBL;
                }
                goto case GT_CNS_DBL;
            }

            case GT_CNS_DBL:
            {
                genSetRegToConst(targetReg, targetType, tree);
                genProduceReg(tree);
                break;
            }

            case GT_NOT:
            case GT_NEG:
            {
                genCodeForNegNot(tree.AsUnOp());
                break;
            }

            case GT_BSWAP:
            case GT_BSWAP16:
            {
                genCodeForBswap(tree);
                break;
            }

            case GT_MOD:
            case GT_UMOD:
            case GT_DIV:
            case GT_UDIV:
            {
                genCodeForDivMod(tree.AsOp());
                break;
            }

            case GT_OR:
            case GT_XOR:
            case GT_AND:
            case GT_AND_NOT:
            {
                assert(varTypeIsIntegralOrI(tree.Type));
                goto case GT_ADD;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            {
                genConsumeOperands(tree.AsOp());
                genCodeForBinary(tree.AsOp());
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROR:
            {
                genCodeForShift(tree);
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
                genCodeForLclFld(tree.AsLclFld());
                break;
            }

            case GT_LCL_VAR:
            {
                genCodeForLclVar(tree.AsLclVar());
                break;
            }

            case GT_STORE_LCL_FLD:
            {
                genCodeForStoreLclFld(tree.AsLclFld());
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                genCodeForStoreLclVar(tree.AsLclVar());
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
                genLeaInstruction(tree.AsAddrMode());
                break;
            }

            case GT_INDEX_ADDR:
            {
                genCodeForIndexAddr(tree.AsIndexAddr());
                break;
            }

            case GT_IND:
            {
                genCodeForIndir(tree.AsIndir());
                break;
            }

            case GT_INC_SATURATE:
            {
                genCodeForIncSaturate(tree);
                break;
            }

            case GT_MULHI:
            {
                genCodeForMulHi(tree.AsOp());
                break;
            }

            case GT_SWAP:
            {
                genCodeForSwap(tree.AsOp());
                break;
            }

            case GT_JMP:
            {
                genJmpPlaceArgs(tree);
                break;
            }

            case GT_CKFINITE:
            {
                genCkfinite(tree);
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsic(tree.AsIntrinsic());
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                genHWIntrinsic(tree.AsHWIntrinsic());
                break;
            }
#endif

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            {
                genConsumeOperands(tree.AsOp());
                genCodeForCompare(tree.AsOp());
                break;
            }

            case GT_JCC:
            {
                var currentBlock = _compiler.compCurBB;
                assert(currentBlock is not null);

                var block = currentBlock!;
                var targetBlock = block.Kind is BBJ_COND ? block.TrueTarget : block.Target;
#if !FEATURE_FIXED_OUT_ARGS
                assert((unchecked((uint)targetBlock.bbTgtStkDepth * sizeof(int)) == genStackLevel)
                    || IsFramePointerUsed);
#endif

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

            case GT_JCMP:
            {
                genCodeForJumpCompare(tree.AsOpCC());
                break;
            }

            case GT_RETURNTRAP:
            {
                genCodeForReturnTrap(tree.AsUnOp());
                break;
            }

            case GT_STOREIND:
            {
                genCodeForStoreInd(tree.AsStoreInd());
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
            {
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
                    genConsumeReg(operand);
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
                genRangeCheck(tree);
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
                emit.emitIns_R_L(INS_ld_d, EA_PTRSIZE, genPendingCallLabel, targetReg);
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
                genJumpTable(tree);
                break;
            }

            case GT_SWITCH_TABLE:
            {
                genTableBasedSwitch(tree);
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

            default:
            {
#if DEBUG
                NYIRAW($"NYI: Unimplemented node type {tree.Oper}\n");
#else
                NYI("unimplemented node");
#endif
                break;
            }
        }
#else
#if TARGET_ARM64
        if (tree.Oper is GT_DIV or GT_UDIV or GT_MOD or GT_UMOD)
        {
            genCodeForDivMod(tree.AsOp());
            return;
        }

        switch (tree.Oper)
        {
            case GT_ASYNC_RESUME_INFO:
            {
                genAsyncResumeInfo(tree.AsVal());
                return;
            }

            case GT_RECORD_ASYNC_RESUME:
            {
                genRecordAsyncResume(tree.AsVal());
                return;
            }

            case GT_FTN_ENTRY:
            {
                genFtnEntry(tree);
                return;
            }

            case GT_NONLOCAL_JMP:
            {
                genNonLocalJmp(tree.AsUnOp());
                return;
            }

            case GT_JCMP:
            case GT_JTEST:
            {
                genCodeForJumpCompare(tree.AsOpCC());
                return;
            }

            case GT_XCHG:
            case GT_XADD:
            case GT_XORR:
            case GT_XAND:
            {
                genLockedInstructions(tree.AsOp());
                return;
            }

            case GT_CMPXCHG:
            {
                genCodeForCmpXchg(tree.AsCmpXchg());
                return;
            }

            case GT_JMPTABLE:
            {
                genJumpTable(tree);
                return;
            }

            case GT_SWITCH_TABLE:
            {
                genTableBasedSwitch(tree);
                return;
            }

            case GT_MEMORYBARRIER:
            {
                var barrierKind = (tree.Flags & GTF_MEMORYBARRIER_LOAD) != 0
                    ? BarrierKind.BARRIER_LOAD_ONLY
                    : (tree.Flags & GTF_MEMORYBARRIER_STORE) != 0
                        ? BarrierKind.BARRIER_STORE_ONLY
                        : BarrierKind.BARRIER_FULL;
                instGen_MemoryBarrier(barrierKind);
                return;
            }

            case GT_IND:
            {
                genCodeForIndir(tree.AsIndir());
                return;
            }
        }
#endif
#if TARGET_LOONGARCH64 || TARGET_RISCV64
        switch (tree.Oper)
        {
            case GT_ASYNC_RESUME_INFO:
            {
                genAsyncResumeInfo(tree.AsVal());
                return;
            }
            case GT_FTN_ENTRY:
            {
                genFtnEntry(tree);
                return;
            }
            case GT_NONLOCAL_JMP:
            {
                genNonLocalJmp(tree.AsUnOp());
                return;
            }
            case GT_XCHG:
            case GT_XADD:
#if TARGET_RISCV64
            case GT_XORR:
            case GT_XAND:
#endif
            {
                genLockedInstructions(tree.AsOp());
                return;
            }
            case GT_CMPXCHG:
            {
                genCodeForCmpXchg(tree.AsCmpXchg());
                return;
            }
            case GT_RETURNTRAP:
            {
                genCodeForReturnTrap(tree.AsUnOp());
                return;
            }
            case GT_CKFINITE:
            {
                genCkfinite(tree);
                return;
            }
#if TARGET_LOONGARCH64
            case GT_JCMP:
            {
                genCodeForJumpCompare(tree.AsOpCC());
                return;
            }
#endif
#if TARGET_RISCV64
            case GT_SELECT:
            {
                genCodeForSelect(tree.AsConditional());
                return;
            }
            case GT_JCMP:
            {
                genCodeForJumpCompare(tree.AsOpCC());
                return;
            }
#endif
            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            {
                genConsumeOperands(tree.AsOp());
                genCodeForCompare(tree.AsOp());
                return;
            }
            case GT_JMPTABLE:
            {
                genJumpTable(tree);
                return;
            }
            case GT_SWITCH_TABLE:
            {
                genTableBasedSwitch(tree);
                return;
            }
        }
        throw new FatalJitException(CORJIT_SKIPPED, "Node instruction generation requires xarch.");
#elif TARGET_ARM
        switch (tree.Oper)
        {
            case GT_IND:
            {
                genCodeForIndir(tree.AsIndir());
                return;
            }

            case GT_STOREIND:
            {
                genCodeForStoreInd(tree.AsStoreInd());
                return;
            }
        }
        throw new FatalJitException(CORJIT_SKIPPED, "Node instruction generation requires xarch.");
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Node instruction generation requires xarch.");
#endif
#endif
#else
#if !TARGET_64BIT
        var targetReg = tree.Type == TYP_LONG ? REG_NA : tree.RegNum;
#else
        var targetReg = tree.RegNum;
#endif
        var targetType = tree.Type;
#if DEBUG
        // Operand-use ordering is local to each generated node.
        lastConsumedNode = null;
        if (_compiler.verbose)
        {
            _compiler.gtDispLIRNode(tree, "Generating: ");
        }
#endif
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
                _gcInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_INT_CALLEE_SAVED));
                genDefineTempLabel(genCreateTempLabel());
                break;
            }

            case GT_PROF_HOOK:
            {
#if PROFILING_SUPPORTED
                noway_assert(_compiler.compIsProfilerHookNeeded);
                // This node currently represents only the tail-call profiler hook.
                genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
#endif
                break;
            }

            case GT_LCLHEAP:
            {
                genLclHeap(tree);
                break;
            }

            case GT_CNS_INT:
#if TARGET_X86
            {
                assert(!tree.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL));
                goto case GT_CNS_DBL;
            }
#endif
            case GT_CNS_DBL:
#if FEATURE_SIMD
            case GT_CNS_VEC:
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
#endif
            {
                genSetRegToConst(targetReg, targetType, tree);
                genProduceReg(tree);
                break;
            }

            case GT_NOT:
            case GT_NEG:
            {
                genCodeForNegNot(tree.AsUnOp());
                break;
            }

            case GT_BSWAP:
            case GT_BSWAP16:
            {
                genCodeForBswap(tree);
                break;
            }

            case GT_DIV:
            {
                if (varTypeIsFloating(targetType))
                {
                    genCodeForBinary(tree.AsOp());
                    break;
                }
                goto case GT_MOD;
            }

            case GT_MOD:
            case GT_UMOD:
            case GT_UDIV:
            {
                genCodeForDivMod(tree.AsOp());
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
#if !TARGET_64BIT
            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
#endif
            {
                genCodeForBinary(tree.AsOp());
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
                    genCodeForBinary(tree.AsOp());
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
                genCodeForShift(tree);
                break;
            }

#if !TARGET_64BIT
            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                genCodeForShiftLong(tree);
                break;
            }
#endif

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
                genCodeForLclFld(tree.AsLclFld());
                break;
            }

            case GT_LCL_VAR:
            {
                genCodeForLclVar(tree.AsLclVar());
                break;
            }

            case GT_STORE_LCL_FLD:
            {
                genCodeForStoreLclFld(tree.AsLclFld());
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                genCodeForStoreLclVar(tree.AsLclVar());
                break;
            }

            case GT_RETFILT:
            case GT_RETURN:
            {
                genReturn(tree);
                break;
            }

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR_RET:
            {
                genSwiftErrorReturn(tree);
                break;
            }
#endif

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
                genPatchpoint(tree.AsUnOp());
                break;
            }

            case GT_LEA:
            {
                genLeaInstruction(tree.AsAddrMode());
                break;
            }

            case GT_INDEX_ADDR:
            {
                genCodeForIndexAddr(tree.AsIndexAddr());
                break;
            }

            case GT_IND:
            {
                genCodeForIndir(tree.AsIndir());
                break;
            }

            case GT_INC_SATURATE:
            {
                genCodeForIncSaturate(tree);
                break;
            }

            case GT_MULHI:
#if TARGET_X86
            case GT_MUL_LONG:
#endif
            {
                genCodeForMulHi(tree.AsOp());
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsic(tree.AsIntrinsic());
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                genHWIntrinsic(tree.AsHWIntrinsic());
                break;
            }
#endif
            case GT_CKFINITE:
            {
                genCkfinite(tree);
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
                genCodeForCompare(tree.AsOp());
                break;
            }

            case GT_JTRUE:
            {
                genCodeForJTrue(tree.AsUnOp());
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
                genCodeForSelect(tree.AsConditional());
                break;
            }

            case GT_SELECTCC:
            {
                genCodeForSelect(tree.AsOp());
                break;
            }

            case GT_RETURNTRAP:
            {
                genCodeForReturnTrap(tree.AsUnOp());
                break;
            }

            case GT_STOREIND:
            {
                genCodeForStoreInd(tree.AsStoreInd());
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
                genCodeForSwap(tree.AsOp());
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

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR:
            {
                genCodeForSwiftErrorReg(tree);
                break;
            }
#endif

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
                genRangeCheck(tree);
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
                genJumpTable(tree);
                break;
            }

            case GT_SWITCH_TABLE:
            {
                genTableBasedSwitch(tree);
                break;
            }

#if !TARGET_64BIT
            case GT_LONG:
            {
                assert(tree.IsUsedFromReg);
                genConsumeRegs(tree);
                break;
            }
#endif

#if TARGET_AMD64
            case GT_CCMP:
            {
                genCodeForCCMP(tree.AsCCMP());
                break;
            }
#endif

            default:
            {
#if DEBUG
                // Native OpName formats the complete GT_ operator name.
                NYIRAW($"NYI: Unimplemented node type {tree.Oper}\n");
#endif
                assert(false, "!\"Unknown node in codegen\"");
                break;
            }
        }
#endif
    }

#if !TARGET_WASM && SWIFT_SUPPORT
    public void genCodeForSwiftErrorReg(GenTree tree)
    {
        assert(tree.Oper == GT_SWIFT_ERROR);
        var targetType = tree.Type;
        var targetReg = tree.RegNum;

        // LSRA also assigns the ABI error register as the destination (see LinearScan::BuildNode).
        assert(targetReg == REG_SWIFT_ERROR);

        inst_Mov(targetType, targetReg, REG_SWIFT_ERROR, canSkip: true);
        genTransferRegGCState(targetReg, REG_SWIFT_ERROR);
        genProduceReg(tree);
    }
#endif
}
