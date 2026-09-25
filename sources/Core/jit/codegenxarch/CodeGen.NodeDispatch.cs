// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCodeForTreeNode(GenTree tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Node instruction generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var targetReg = tree.RegNum;
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
                throw new FatalJitException("FIELD_LIST nodes must be contained during code generation.");
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

            case GT_CCMP:
            {
                genCodeForCCMP(tree.AsCCMP());
                break;
            }

            default:
            {
                throw new FatalJitException($"Unimplemented node type {tree.Oper} in code generation.");
            }
        }
#endif
    }
}
