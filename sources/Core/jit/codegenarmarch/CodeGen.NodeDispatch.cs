// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genCodeForTreeNodeArmArch(GenTree treeNode)
    {
        if (!Configuration.TARGET_ARMARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM code generation was requested for a non-ARM target.");
            return;
        }

        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;

        genPrepareNodeConsumption(treeNode);

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
                GCInfo.gcMarkRegSetNpt(new regMaskTP(Configuration.TARGET_ARM64
                    ? SRBM_INT_CALLEE_SAVED_ARM64
                    : SRBM_INT_CALLEE_SAVED_ARM));
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
                genLclHeap(treeNode);
                break;
            }

            case GT_CNS_INT:
            case GT_CNS_DBL:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genSetRegToConstArm64(targetReg, targetType, treeNode);
                }
                else
                {
                    genSetRegToConstArm(targetReg, targetType, treeNode);
                }
                genProduceReg(treeNode);
                break;
            }

            case GT_CNS_VEC:
            {
                if (!Configuration.FEATURE_SIMD)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                        "SIMD constants are not supported by this ARM configuration.");
                    return;
                }

                requireArm64Codegen(treeNode);
                genSetRegToConstArm64(targetReg, targetType, treeNode);
                genProduceReg(treeNode);
                break;
            }

            case GT_CNS_MSK:
            {
                if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                        "Masked SIMD constants are not supported by this ARM configuration.");
                    return;
                }

                requireArm64Codegen(treeNode);
                genSetRegToConstArm64(targetReg, targetType, treeNode);
                genProduceReg(treeNode);
                break;
            }

            case GT_NOT:
            case GT_NEG:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForNegNotArm64(treeNode);
                }
                else
                {
                    genCodeForNegNotArm(treeNode);
                }
                break;
            }

            case GT_BSWAP:
            case GT_BSWAP16:
            {
                requireArm64Codegen(treeNode);
                genCodeForBswapArm64(treeNode);
                break;
            }

            case GT_MOD:
            case GT_UMOD:
            case GT_DIV:
            case GT_UDIV:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForDivModArm64(treeNode.AsOp());
                }
                else
                {
                    genCodeForDivModArm(treeNode.AsOp());
                }
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

            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
            {
                requireArm32Codegen(treeNode);
                genConsumeOperands(treeNode.AsOp());
                genCodeForBinaryArm(treeNode.AsOp());
                break;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            {
                genConsumeOperands(treeNode.AsOp());
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForBinaryArm64(treeNode.AsOp());
                }
                else
                {
                    genCodeForBinaryArm(treeNode.AsOp());
                }
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            // case GT_ROL: // ARM has no ROL instruction; lowering converts it to ROR.
            case GT_ROR:
            {
                genCodeForShiftArmArch(treeNode);
                break;
            }

            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                requireArm32Codegen(treeNode);
                genCodeForShiftLongArm(treeNode);
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

            case GT_LCL_ADDR:
            {
                genCodeForLclAddr(treeNode.AsLclFld());
                break;
            }

            case GT_LCL_FLD:
            {
                genCodeForLclFldArmArch(treeNode.AsLclFld());
                break;
            }

            case GT_LCL_VAR:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForLclVarArm64(treeNode.AsLclVar());
                }
                else
                {
                    genCodeForLclVarArm(treeNode.AsLclVar());
                }
                break;
            }

            case GT_STORE_LCL_FLD:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForStoreLclFldArm64(treeNode.AsLclFld());
                }
                else
                {
                    genCodeForStoreLclFldArm(treeNode.AsLclFld());
                }
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForStoreLclVarArm64(treeNode.AsLclVar());
                }
                else
                {
                    genCodeForStoreLclVarArm(treeNode.AsLclVar());
                }
                break;
            }

            case GT_RETFILT:
            case GT_RETURN:
            {
                genReturn(treeNode);
                break;
            }

            case GT_SWIFT_ERROR_RET:
            {
                if (!Configuration.SWIFT_SUPPORT)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                        "Swift error returns are not supported by this ARM configuration.");
                    return;
                }

                genSwiftErrorReturn(treeNode);
                break;
            }

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
                genPatchpoint(treeNode.AsUnOp());
                break;
            }

            case GT_IND:
            {
                genCodeForIndirArmArch(treeNode.AsIndir());
                break;
            }

            case GT_INDEX_ADDR:
            {
                genCodeForIndexAddrArmArch(treeNode.AsIndexAddr());
                break;
            }

            case GT_LEA:
            {
                genLeaInstructionArmArch(treeNode.AsAddrMode());
                break;
            }

            case GT_MUL_LONG:
            {
                genCodeForMulLong(treeNode.AsOp());
                break;
            }

            case GT_INC_SATURATE:
            {
                requireArm64Codegen(treeNode);
                genCodeForIncSaturateArm64(treeNode);
                break;
            }

            case GT_MULHI:
            {
                requireArm64Codegen(treeNode);
                genCodeForMulHiArm64(treeNode.AsOp());
                break;
            }

            case GT_SWAP:
            {
                requireArm64Codegen(treeNode);
                genCodeForSwapArm64(treeNode.AsOp());
                break;
            }

            case GT_BFIZ:
            {
                requireArm64Codegen(treeNode);
                genCodeForBfizArm64(treeNode.AsOp());
                break;
            }

            case GT_BFX:
            {
                requireArm64Codegen(treeNode);
                genCodeForBfxArm64(treeNode.AsBfm());
                break;
            }

            case GT_JMP:
            {
                genJmpPlaceArgs(treeNode);
                break;
            }

            case GT_CKFINITE:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCkfiniteArm64(treeNode);
                }
                else
                {
                    genCkfiniteArm(treeNode);
                }
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsicArmArch(treeNode.AsIntrinsic());
                break;
            }

            case GT_HWINTRINSIC:
            {
                if (!Configuration.FEATURE_HW_INTRINSICS)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                        "Hardware intrinsics are not supported by this ARM configuration.");
                    return;
                }

                requireArm64Codegen(treeNode);
                genHWIntrinsicArm64(treeNode.AsHWIntrinsic());
                break;
            }

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
                if (Configuration.TARGET_ARM64)
                {
                    requireArm64Codegen(treeNode);
                }
                else
                {
                    requireArm32Codegen(treeNode);
                }

                genConsumeOperands(treeNode.AsOp());
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForCompareArm64(treeNode.AsOp());
                }
                else
                {
                    genCodeForCompareArm(treeNode.AsOp());
                }
                break;
            }

            case GT_SELECT_NEG:
            case GT_SELECT_INV:
            case GT_SELECT_INC:
            case GT_SELECT:
            {
                requireArm64Codegen(treeNode);
                genCodeForSelectArm64(treeNode.AsConditional());
                break;
            }

            case GT_SELECT_NEGCC:
            case GT_SELECT_INVCC:
            case GT_SELECT_INCCC:
            case GT_SELECTCC:
            {
                requireArm64Codegen(treeNode);
                genCodeForSelectArm64(treeNode.AsOp());
                break;
            }

            case GT_JCMP:
            case GT_JTEST:
            {
                requireArm64Codegen(treeNode);
                genCodeForJumpCompareArm64(treeNode.AsOpCC());
                break;
            }

            case GT_CCMP:
            {
                requireArm64Codegen(treeNode);
                genCodeForCCMP(treeNode.AsCCMP());
                break;
            }

            case GT_JTRUE:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genCodeForJTrueArm64(treeNode.AsUnOp());
                }
                else
                {
                    genCodeForJTrueArm(treeNode.AsUnOp());
                }
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
                if (Configuration.TARGET_ARM64)
                {
                    requireArm64Codegen(treeNode);
                    genCodeForStoreIndArm64(treeNode.AsStoreInd());
                }
                else
                {
                    requireArm32Codegen(treeNode);
                    genCodeForStoreIndArm(treeNode.AsStoreInd());
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
                genCallArmArch(treeNode.AsCall());
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

            case GT_XCHG:
            case GT_XORR:
            case GT_XAND:
            case GT_XADD:
            {
                requireArm64Codegen(treeNode);
                genLockedInstructions(treeNode.AsOp());
                break;
            }

            case GT_CMPXCHG:
            {
                requireArm64Codegen(treeNode);
                genCodeForCmpXchg(treeNode.AsCmpXchg());
                break;
            }

            case GT_SWIFT_ERROR:
            {
                if (!Configuration.SWIFT_SUPPORT)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                        "Swift error values are not supported by this ARM configuration.");
                    return;
                }

                genCodeForSwiftErrorReg(treeNode);
                break;
            }

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
                genRangeCheckArmArch(treeNode);
                break;
            }

            case GT_PHYSREG:
            {
                genCodeForPhysReg(treeNode.AsPhysReg());
                break;
            }

            case GT_NULLCHECK:
            {
                genCodeForNullCheckArmArch(treeNode.AsIndir());
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
                if (Configuration.TARGET_ARM)
                {
                    genMov32RelocatableDisplacement(genPendingCallLabel, targetReg);
                }
                else
                {
                    Emitter.emitIns_R_L(INS_adr, EA_PTRSIZE, genPendingCallLabel, targetReg);
                }
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
                genCodeForStoreBlkArmArch(treeNode.AsBlk());
                break;
            }

            case GT_JMPTABLE:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genJumpTableArm64(treeNode);
                }
                else
                {
                    genJumpTableArm(treeNode);
                }
                break;
            }

            case GT_SWITCH_TABLE:
            {
                if (Configuration.TARGET_ARM64)
                {
                    genTableBasedSwitchArm64(treeNode);
                }
                else
                {
                    genTableBasedSwitchArm(treeNode);
                }
                break;
            }

            case GT_LONG:
            {
                requireArm32Codegen(treeNode);
                assert(treeNode.IsUsedFromReg);
                genConsumeRegs(treeNode);
                break;
            }

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

    private static void requireArm64Codegen(GenTree treeNode)
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                $"ARM64 code generation for {treeNode.Oper} is not supported on this target.");
            return;
        }
    }

    private static void requireArm32Codegen(GenTree treeNode)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                $"ARM32 code generation for {treeNode.Oper} is not supported on this target.");
            return;
        }
    }
}
