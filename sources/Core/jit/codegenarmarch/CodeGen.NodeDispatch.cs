// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genCodeForTreeNodeArmArch(GenTree treeNode)
    {
        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;

#if DEBUG
        lastConsumedNode = null;
        if (_compiler.verbose)
        {
            _compiler.gtDispLIRNode(treeNode, "Generating: ");
        }
#endif

        if (treeNode.IsReuseRegVal)
        {
            genCodeForReuseVal(treeNode);
            return;
        }

        if (treeNode.IsContained)
        {
            return;
        }

        switch (treeNode.Oper)
        {
            case GT_START_NONGC:
            {
                Emitter.emitDisableGC();
                break;
            }

            case GT_START_PREEMPTGC:
            {
                GCInfo.gcMarkRegSetNpt(new regMaskTP(SRBM_INT_CALLEE_SAVED));
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
                genLclHeap(treeNode);
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
                genSetRegToConst(targetReg, targetType, treeNode);
                genProduceReg(treeNode);
                break;
            }

            case GT_NOT:
            case GT_NEG:
            {
#if TARGET_ARM64
                genCodeForNegNot(treeNode.AsOp());
#else
                genCodeForNegNot(treeNode.AsUnOp());
#endif
                break;
            }

#if TARGET_ARM64
            case GT_BSWAP:
            case GT_BSWAP16:
            {
                genCodeForBswap(treeNode);
                break;
            }
#endif

            case GT_MOD:
            case GT_UMOD:
            case GT_DIV:
            case GT_UDIV:
            {
                genCodeForDivMod(treeNode.AsOp());
                break;
            }

            case GT_OR:
            case GT_OR_NOT:
            case GT_XOR:
            case GT_XOR_NOT:
            case GT_AND:
            case GT_AND_NOT:
            {
                assert(varTypeIsIntegralOrI(treeNode.Type));
                goto case GT_ADD;
            }

#if !TARGET_64BIT
            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
#endif
            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            {
                genConsumeOperands(treeNode.AsOp());
                genCodeForBinary(treeNode.AsOp());
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            // case GT_ROL: // ARM has no ROL instruction; lowering converts it to ROR.
            case GT_ROR:
            {
                genCodeForShift(treeNode);
                break;
            }

#if !TARGET_64BIT
            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                genCodeForShiftLong(treeNode);
                break;
            }
#endif

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

            case GT_STORE_LCL_FLD:
            {
                genCodeForStoreLclFld(treeNode.AsLclFld());
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                genCodeForStoreLclVar(treeNode.AsLclVar());
                break;
            }

            case GT_RETFILT:
            case GT_RETURN:
            {
                genReturn(treeNode);
                break;
            }

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR_RET:
            {
                genSwiftErrorReturn(treeNode);
                break;
            }
#endif

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
                genPatchpoint(treeNode.AsUnOp());
                break;
            }

            case GT_IND:
            {
                genCodeForIndir(treeNode.AsIndir());
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

            case GT_MUL_LONG:
            {
                genCodeForMulLong(treeNode.AsOp());
                break;
            }

#if TARGET_ARM64
            case GT_INC_SATURATE:
            {
                genCodeForIncSaturate(treeNode);
                break;
            }

            case GT_MULHI:
            {
                genCodeForMulHi(treeNode.AsOp());
                break;
            }

            case GT_SWAP:
            {
                genCodeForSwap(treeNode.AsOp());
                break;
            }

            case GT_BFIZ:
            {
                genCodeForBfiz(treeNode.AsOp());
                break;
            }

            case GT_BFX:
            {
                genCodeForBfx(treeNode.AsBfm());
                break;
            }
#endif

            case GT_JMP:
            {
                genJmpPlaceArgs(treeNode);
                break;
            }

            case GT_CKFINITE:
            {
                genCkfinite(treeNode);
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsic(treeNode.AsIntrinsic());
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                genHWIntrinsic(treeNode.AsHWIntrinsic());
                break;
            }
#endif

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_TEST_NE:
            case GT_TEST_EQ:
            case GT_CMP:
            case GT_TEST:
            {
                genConsumeOperands(treeNode.AsOp());
                genCodeForCompare(treeNode.AsOp());
                break;
            }

#if TARGET_ARM64
            case GT_SELECT_NEG:
            case GT_SELECT_INV:
            case GT_SELECT_INC:
            case GT_SELECT:
            {
                genCodeForSelect(treeNode.AsConditional());
                break;
            }

            case GT_SELECT_NEGCC:
            case GT_SELECT_INVCC:
            case GT_SELECT_INCCC:
            case GT_SELECTCC:
            {
                genCodeForSelect(treeNode.AsOp());
                break;
            }

            case GT_JCMP:
            case GT_JTEST:
            {
                genCodeForJumpCompare(treeNode.AsOpCC());
                break;
            }

            case GT_CCMP:
            {
                genCodeForCCMP(treeNode.AsCCMP());
                break;
            }
#endif

            case GT_JTRUE:
            {
                genCodeForJTrue(treeNode.AsUnOp());
                break;
            }

            case GT_JCC:
            {
                genCodeForJcc(treeNode.AsCC());
                break;
            }

            case GT_SETCC:
            {
                genCodeForSetcc(treeNode.AsCC());
                break;
            }

            case GT_RETURNTRAP:
            {
                genCodeForReturnTrap(treeNode.AsUnOp());
                break;
            }

            case GT_STOREIND:
            {
                genCodeForStoreInd(treeNode.AsStoreInd());
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
                genPutArgStk(treeNode.AsPutArgStk());
                break;
            }

            case GT_PUTARG_REG:
            {
                genPutArgReg(treeNode.AsUnOp());
                break;
            }

            case GT_CALL:
            {
                genCall(treeNode.AsCall());
                break;
            }

            case GT_MEMORYBARRIER:
            {
                var barrierKind = (treeNode.Flags & GTF_MEMORYBARRIER_LOAD) != 0
                    ? BarrierKind.BARRIER_LOAD_ONLY
                    : (treeNode.Flags & GTF_MEMORYBARRIER_STORE) != 0
                        ? BarrierKind.BARRIER_STORE_ONLY
                        : BarrierKind.BARRIER_FULL;
                instGen_MemoryBarrier(barrierKind);
                break;
            }

#if TARGET_ARM64
            case GT_XCHG:
            case GT_XORR:
            case GT_XAND:
            case GT_XADD:
            {
                genLockedInstructions(treeNode.AsOp());
                break;
            }

            case GT_CMPXCHG:
            {
                genCodeForCmpXchg(treeNode.AsCmpXchg());
                break;
            }
#endif

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR:
            {
                genCodeForSwiftErrorReg(treeNode);
                break;
            }
#endif

            case GT_RELOAD:
            case GT_NOP:
            {
                break;
            }

            case GT_KEEPALIVE:
            {
                var operand = treeNode.AsOp().Op1;
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
                genRangeCheck(treeNode);
                break;
            }

            case GT_PHYSREG:
            {
                genCodeForPhysReg(treeNode.AsPhysReg());
                break;
            }

            case GT_NULLCHECK:
            {
                genCodeForNullCheck(treeNode.AsIndir());
                break;
            }

            case GT_CATCH_ARG:
            {
                genCodeForCatchArg(treeNode);
                break;
            }

            case GT_LABEL:
            {
                genPendingCallLabel = genCreateTempLabel();
#if TARGET_ARM
                genMov32RelocatableDisplacement(genPendingCallLabel, targetReg);
#else
                Emitter.emitIns_R_L(INS_adr, EA_PTRSIZE, genPendingCallLabel, targetReg);
#endif
                break;
            }

            case GT_RETURN_SUSPEND:
            {
                genReturnSuspend(treeNode.AsUnOp());
                break;
            }

            case GT_ASYNC_CONTINUATION:
            {
                genCodeForAsyncContinuation(treeNode);
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

            case GT_FTN_ENTRY:
            {
                genFtnEntry(treeNode);
                break;
            }

            case GT_NONLOCAL_JMP:
            {
                genNonLocalJmp(treeNode.AsUnOp());
                break;
            }

            case GT_STORE_BLK:
            {
                genCodeForStoreBlk(treeNode.AsBlk());
                break;
            }

            case GT_JMPTABLE:
            {
                genJumpTable(treeNode);
                break;
            }

            case GT_SWITCH_TABLE:
            {
                genTableBasedSwitch(treeNode);
                break;
            }

#if TARGET_ARM
            case GT_LONG:
            {
                assert(treeNode.IsUsedFromReg);
                genConsumeRegs(treeNode);
                break;
            }
#endif

            case GT_IL_OFFSET:
            {
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
}
#endif
