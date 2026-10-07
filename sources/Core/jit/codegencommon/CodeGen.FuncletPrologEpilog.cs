// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private struct FuncletPrologEpilogInfo
    {
        public regMaskTP fiSaveRegs;
        public int fiFrameType;
        public int fiSP_to_FPLR_save_delta;
        public int fiSP_to_CalleeSave_delta;
        public int fiSpDelta1;
        public int fiSpDelta2;
        public uint fiSpDelta;
    }

    private FuncletPrologEpilogInfo genFuncletInfo;

    public unsafe void genFuncletProlog(BasicBlock block)
    {
#if TARGET_WASM
        genFuncletPrologWasm(block);
#elif TARGET_ARM
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif
        assert(block is not null);
        assert(_compiler.bbIsFuncletBeg(block));

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

        var maskPushRegsFloat = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_ALLFLOAT);
        var maskPushRegsInt = genFuncletInfo.fiSaveRegs & ~maskPushRegsFloat;
        var maskStackAlloc = genStackAllocRegisterMask(genFuncletInfo.fiSpDelta, maskPushRegsFloat);
        maskPushRegsInt |= maskStackAlloc;

        assert(unchecked((ulong)maskPushRegsInt.Lower) <= int.MaxValue);
        inst_IV(INS_push, unchecked((int)maskPushRegsInt.Lower));
        _compiler.unwindPushMaskInt(maskPushRegsInt);

        if (maskPushRegsFloat.IsNonEmpty)
        {
            genPushFltRegsArmCore(maskPushRegsFloat);
            _compiler.unwindPushMaskFloat(maskPushRegsFloat);
        }

        var liveIn = block.CatchType switch
        {
            BBCT_FILTER => new regMaskTP(SRBM_R0 | SRBM_R1),
            BBCT_FINALLY or BBCT_FAULT => RBM_NONE,
            _ => new regMaskTP(SRBM_R0),
        };
        var initRegZeroed = false;
        if (maskStackAlloc.IsEmpty)
        {
            // R3 is not live on funclet entry and is available when a large frame needs a stack-probe scratch register.
            genAllocLclFrame(genFuncletInfo.fiSpDelta, REG_R3, ref initRegZeroed, liveIn);
        }

        _compiler.unwindEndProlog();
#elif TARGET_ARM64
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif
        assert(block is not null);
        assert(_compiler.bbIsFuncletBeg(block));
        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

        if (JitConfig.JitPacEnabled != 0)
        {
            Emitter.emitPacInProlog();
        }

        var maskSaveRegsFloat = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_ALLFLOAT);
        var maskSaveRegsInt = genFuncletInfo.fiSaveRegs & ~maskSaveRegsFloat;
        assert((maskSaveRegsInt & new regMaskTP(SRBM_LR)).IsNonEmpty);
        assert((maskSaveRegsInt & new regMaskTP(SRBM_FP)).IsNonEmpty);

        var liveIn = block.CatchType switch
        {
            BBCT_FILTER => new regMaskTP(SRBM_R0 | SRBM_R1),
            BBCT_FINALLY or BBCT_FAULT => RBM_NONE,
            _ => new regMaskTP(SRBM_R0),
        };

        if (genFuncletInfo.fiFrameType == 1)
        {
            if (_compiler.opts.IsOSR)
            {
                var scratchRegIsZero = false;
                genAllocLclFrame(unchecked((uint)-genFuncletInfo.fiSpDelta1), REG_SCRATCH,
                    ref scratchRegIsZero, liveIn);
                genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta1, REG_SCRATCH, null,
                    reportUnwindData: true);
                Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE, 0);
                _compiler.unwindSaveRegPair(REG_FP, REG_LR, 0);
            }
            else
            {
                Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                    genFuncletInfo.fiSpDelta1, INS_OPTS_PRE_INDEX);
                _compiler.unwindSaveRegPairPreindexed(REG_FP, REG_LR, genFuncletInfo.fiSpDelta1);
            }

            maskSaveRegsInt &= ~new regMaskTP(SRBM_LR | SRBM_FP);
            assert(genFuncletInfo.fiSpDelta2 == 0);
            assert(genFuncletInfo.fiSP_to_FPLR_save_delta == 0);
        }
        else if (genFuncletInfo.fiFrameType == 2)
        {
            assert(genFuncletInfo.fiSpDelta1 < 0);
            assert(genFuncletInfo.fiSpDelta1 >= -512);
            genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta1, REG_NA, null, reportUnwindData: true);
            assert(genFuncletInfo.fiSpDelta2 == 0);

            Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                genFuncletInfo.fiSP_to_FPLR_save_delta);
            _compiler.unwindSaveRegPair(REG_FP, REG_LR, genFuncletInfo.fiSP_to_FPLR_save_delta);
            maskSaveRegsInt &= ~new regMaskTP(SRBM_LR | SRBM_FP);
        }
        else if (genFuncletInfo.fiFrameType == 3)
        {
            if (_compiler.opts.IsOSR)
            {
                var scratchRegIsZero = false;
                genAllocLclFrame(unchecked((uint)-genFuncletInfo.fiSpDelta1), REG_SCRATCH,
                    ref scratchRegIsZero, liveIn);
                genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta1, REG_SCRATCH, null,
                    reportUnwindData: true);
                Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE, 0);
                _compiler.unwindSaveRegPair(REG_FP, REG_LR, 0);
            }
            else
            {
                Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                    genFuncletInfo.fiSpDelta1, INS_OPTS_PRE_INDEX);
                _compiler.unwindSaveRegPairPreindexed(REG_FP, REG_LR, genFuncletInfo.fiSpDelta1);
            }

            maskSaveRegsInt &= ~new regMaskTP(SRBM_LR | SRBM_FP);
        }
        else if (genFuncletInfo.fiFrameType == 4)
        {
            assert(genFuncletInfo.fiSpDelta1 < 0);
            assert(genFuncletInfo.fiSpDelta1 >= -512);
            genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta1, REG_NA, null, reportUnwindData: true);
            assert(genFuncletInfo.fiSpDelta2 == 0);
        }
        else
        {
            assert(genFuncletInfo.fiFrameType == 5);
            if (_compiler.opts.IsOSR)
            {
                var scratchRegIsZero = false;
                genAllocLclFrame(unchecked((uint)-genFuncletInfo.fiSpDelta1), REG_SCRATCH,
                    ref scratchRegIsZero, liveIn);
                genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta1, REG_SCRATCH, null,
                    reportUnwindData: true);
            }
            else
            {
                assert(genFuncletInfo.fiSpDelta1 < 0);
                assert(genFuncletInfo.fiSpDelta1 >= -240);
                genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta1, REG_NA, null, reportUnwindData: true);
            }
        }

        var lowestCalleeSavedOffset = genFuncletInfo.fiSP_to_CalleeSave_delta + genFuncletInfo.fiSpDelta2;
        genSaveCalleeSavedRegistersHelpArm64(maskSaveRegsInt | maskSaveRegsFloat, lowestCalleeSavedOffset, 0);

        if (genFuncletInfo.fiFrameType is 3 or 5)
        {
            assert(genFuncletInfo.fiSpDelta2 <= 0);
            if (genFuncletInfo.fiSpDelta2 < 0)
            {
                genStackPointerAdjustmentArm64(genFuncletInfo.fiSpDelta2, REG_R2, null, reportUnwindData: true);
            }
            else
            {
                assert(_compiler.opts.IsOSR);
            }
        }

        _compiler.unwindEndProlog();
#elif TARGET_RISCV64
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif
        // TODO-RISCV64: Implement varargs.
        assert(block is not null);
        assert(_compiler.bbIsFuncletBeg(block));

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

        var frameSize = unchecked((int)genFuncletInfo.fiSpDelta);
        assert(frameSize < 0);

        var maskSaveRegs = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_CALLEE_SAVED);
        var fpOffset = genFuncletInfo.fiSP_to_CalleeSave_delta;

        // 2040 is the largest 8-byte-aligned signed-12-bit load/store offset; reserve 16 bytes for FP/RA.
        if ((fpOffset + (unchecked((int)genCountBits(maskSaveRegs)) * REGSIZE_BYTES)) <=
            (2040 - 2 * REGSIZE_BYTES))
        {
            genStackPointerAdjustment(frameSize, REG_SCRATCH, null, reportUnwindData: true);

            Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
            _compiler.unwindSaveReg(REG_FP, fpOffset);

            Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_RA, REG_SPBASE, fpOffset + 8);
            _compiler.unwindSaveReg(REG_RA, fpOffset + 8);

            genSaveCalleeSavedRegistersHelp(maskSaveRegs, fpOffset + 16);
        }
        else
        {
            assert(frameSize < -2040);

            genStackPointerAdjustment(frameSize + (fpOffset & -16), REG_SCRATCH, null,
                reportUnwindData: true);

            frameSize = -(fpOffset & -16);
            fpOffset &= 0xf;

            Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
            _compiler.unwindSaveReg(REG_FP, fpOffset);

            Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_RA, REG_SPBASE, fpOffset + 8);
            _compiler.unwindSaveReg(REG_RA, fpOffset + 8);

            genSaveCalleeSavedRegistersHelp(maskSaveRegs, fpOffset + 16);

            genStackPointerAdjustment(frameSize, REG_SCRATCH, null, reportUnwindData: true);
        }

        _compiler.unwindEndProlog();
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet prologs require xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif
#if TARGET_AMD64
        assert(!_regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)));
        assert(_compiler.bbIsFuncletBeg(block));
        assert(IsFramePointerUsed);
#endif

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

#if TARGET_AMD64
        var liveIn = block.CatchType is BBCT_FINALLY or BBCT_FAULT
            ? new regMaskTP(SRBM_ARG_0)
            : new regMaskTP(SRBM_ARG_0 | SRBM_ARG_2);
        var initRegZeroed = false;
        genAllocLclFrame(genFuncletInfo.fiSpDelta, REG_NA, ref initRegZeroed, liveIn);
#endif

        _compiler.unwindEndProlog();
#if TARGET_X86
#if UNIX_X86_ABI
        inst_RV_IV(INS_sub, REG_SPBASE, 12, EA_PTRSIZE);
#else
        if (!_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            instGen(INS_nop);
        }
#endif
#endif
        genClearAvxStateInProlog();
#endif
    }

    public unsafe void genFuncletEpilog(BasicBlock block)
    {
#if TARGET_WASM
        genFuncletEpilogWasm(block);
#elif TARGET_ARM
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }
#endif
        var unwindStarted = false;
        assert((genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_LR)).IsNonEmpty);

        var maskPopRegsFloat = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_ALLFLOAT);
        var maskPopRegsInt = genFuncletInfo.fiSaveRegs & ~maskPopRegsFloat;
        var maskStackAlloc = genStackAllocRegisterMask(genFuncletInfo.fiSpDelta, maskPopRegsFloat);
        maskPopRegsInt |= maskStackAlloc;

        if (maskStackAlloc.IsEmpty)
        {
            genFreeLclFrame(genFuncletInfo.fiSpDelta, ref unwindStarted);
        }

        if (!unwindStarted)
        {
            _compiler.unwindBegEpilog();
            unwindStarted = true;
        }

        maskPopRegsInt &= ~new regMaskTP(SRBM_LR);
        maskPopRegsInt |= new regMaskTP(SRBM_PC);

        if (maskPopRegsFloat.IsNonEmpty)
        {
            genPopFltRegsArmCore(maskPopRegsFloat);
            _compiler.unwindPopMaskFloat(maskPopRegsFloat);
        }

        assert(unchecked((ulong)maskPopRegsInt.Lower) <= int.MaxValue);
        inst_IV(INS_pop, unchecked((int)maskPopRegsInt.Lower));
        _compiler.unwindPopMaskInt(maskPopRegsInt);
        _compiler.unwindEndEpilog();
#elif TARGET_ARM64
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }
#endif
        _compiler.unwindBegEpilog();

        var maskRestoreRegsFloat = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_ALLFLOAT);
        var maskRestoreRegsInt = genFuncletInfo.fiSaveRegs & ~maskRestoreRegsFloat;
        assert((maskRestoreRegsInt & new regMaskTP(SRBM_LR)).IsNonEmpty);
        assert((maskRestoreRegsInt & new regMaskTP(SRBM_FP)).IsNonEmpty);

        if (genFuncletInfo.fiFrameType is 3 or 5)
        {
            assert(genFuncletInfo.fiSpDelta2 <= 0);
            if (genFuncletInfo.fiSpDelta2 < 0)
            {
                genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta2, REG_R2, null,
                    reportUnwindData: true);
            }
            else
            {
                assert(_compiler.opts.IsOSR);
            }
        }

        var regsToRestoreMask = maskRestoreRegsInt | maskRestoreRegsFloat;
        if (genFuncletInfo.fiFrameType is 1 or 2 or 3)
        {
            regsToRestoreMask &= ~new regMaskTP(SRBM_LR | SRBM_FP);
        }
        var lowestCalleeSavedOffset = genFuncletInfo.fiSP_to_CalleeSave_delta + genFuncletInfo.fiSpDelta2;
        genRestoreCalleeSavedRegistersHelpArm64(regsToRestoreMask, lowestCalleeSavedOffset, 0);

        if (genFuncletInfo.fiFrameType == 1)
        {
            if (_compiler.opts.IsOSR)
            {
                Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE, 0);
                _compiler.unwindSaveRegPair(REG_FP, REG_LR, 0);
                genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta1, REG_SCRATCH, null,
                    reportUnwindData: true);
            }
            else
            {
                Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                    -genFuncletInfo.fiSpDelta1, INS_OPTS_POST_INDEX);
                _compiler.unwindSaveRegPairPreindexed(REG_FP, REG_LR, genFuncletInfo.fiSpDelta1);
            }

            assert(genFuncletInfo.fiSpDelta2 == 0);
            assert(genFuncletInfo.fiSP_to_FPLR_save_delta == 0);
        }
        else if (genFuncletInfo.fiFrameType == 2)
        {
            Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                genFuncletInfo.fiSP_to_FPLR_save_delta);
            _compiler.unwindSaveRegPair(REG_FP, REG_LR, genFuncletInfo.fiSP_to_FPLR_save_delta);
            assert(genFuncletInfo.fiSpDelta1 < 0);
            assert(genFuncletInfo.fiSpDelta1 >= -512);
            genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta1, REG_NA, null,
                reportUnwindData: true);
            assert(genFuncletInfo.fiSpDelta2 == 0);
        }
        else if (genFuncletInfo.fiFrameType == 3)
        {
            if (_compiler.opts.IsOSR)
            {
                Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE, 0);
                _compiler.unwindSaveRegPair(REG_FP, REG_LR, 0);
                genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta1, REG_SCRATCH, null,
                    reportUnwindData: true);
            }
            else
            {
                Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                    -genFuncletInfo.fiSpDelta1, INS_OPTS_POST_INDEX);
                _compiler.unwindSaveRegPairPreindexed(REG_FP, REG_LR, genFuncletInfo.fiSpDelta1);
            }
        }
        else if (genFuncletInfo.fiFrameType == 4)
        {
            assert(genFuncletInfo.fiSpDelta1 < 0);
            assert(genFuncletInfo.fiSpDelta1 >= -512);
            genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta1, REG_NA, null,
                reportUnwindData: true);
            assert(genFuncletInfo.fiSpDelta2 == 0);
        }
        else
        {
            assert(genFuncletInfo.fiFrameType == 5);
            assert(genFuncletInfo.fiSpDelta1 < 0);
            if (_compiler.opts.IsOSR)
            {
                genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta1, REG_SCRATCH, null,
                    reportUnwindData: true);
            }
            else
            {
                assert(genFuncletInfo.fiSpDelta1 >= -240);
                genStackPointerAdjustmentArm64(-genFuncletInfo.fiSpDelta1, REG_NA, null,
                    reportUnwindData: true);
            }
        }

        if (JitConfig.JitPacEnabled != 0)
        {
            Emitter.emitPacInEpilog();
        }

        inst_RV(INS_ret, REG_LR, TYP_I_IMPL);
        _compiler.unwindReturn(REG_LR);
        _compiler.unwindEndEpilog();
#elif TARGET_RISCV64
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }
#endif
        _compiler.unwindBegEpilog();

        var frameSize = unchecked((int)genFuncletInfo.fiSpDelta);
        assert(frameSize < 0);

        var maskSaveRegs = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_CALLEE_SAVED);
        var fpOffset = genFuncletInfo.fiSP_to_CalleeSave_delta;

        // The prolog reserves the final 16 bytes of the signed-12-bit offset range for FP/RA.
        if ((fpOffset + (unchecked((int)genCountBits(maskSaveRegs)) * REGSIZE_BYTES)) >
            (2040 - 2 * REGSIZE_BYTES))
        {
            assert(frameSize < -2040);

            genStackPointerAdjustment(fpOffset & -16, REG_SCRATCH, null, reportUnwindData: true);

            frameSize += fpOffset & -16;
            fpOffset &= 0xf;
        }

        genRestoreCalleeSavedRegistersHelp(maskSaveRegs, REG_SPBASE, fpOffset + 16,
            reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_ld, EA_PTRSIZE, REG_RA, REG_SPBASE, fpOffset + 8);
        _compiler.unwindSaveReg(REG_RA, fpOffset + 8);

        Emitter.emitIns_R_R_I(INS_ld, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
        _compiler.unwindSaveReg(REG_FP, fpOffset);

        genStackPointerAdjustment(-frameSize, REG_SCRATCH, null, reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_jalr, EA_PTRSIZE, REG_R0, REG_RA, 0);
        _compiler.unwindReturn(REG_RA);
        _compiler.unwindEndEpilog();
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet epilogs require xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }
#endif
        genClearAvxStateInEpilog();
#if TARGET_AMD64
        inst_RV_IV(INS_add, REG_SPBASE, (nint)genFuncletInfo.fiSpDelta, EA_PTRSIZE);
#elif UNIX_X86_ABI
        inst_RV_IV(INS_add, REG_SPBASE, 12, EA_PTRSIZE);
#endif
        instGen_Return(0);
#endif
    }

    public void genCaptureFuncletPrologEpilogInfo()
    {
#if TARGET_WASM
        // Wasm has no funclet stack delta to capture.
#elif TARGET_ARM
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);

        var preSpillRegArgSize = unchecked((int)genCountBits(_regSet.rsMaskPreSpillRegs(true))) * REGSIZE_BYTES;
        var saveRegs = _regSet.rsGetModifiedCalleeSavedRegsMask() | new regMaskTP(SRBM_FPBASE | SRBM_LR);
        assert((saveRegs & new regMaskTP(SRBM_R12 | SRBM_SP)).IsEmpty);
        var saveRegsSize = unchecked((int)genCountBits(saveRegs)) * REGSIZE_BYTES;
        var saveSizeWithPsp = saveRegsSize + REGSIZE_BYTES;
        if (_compiler.lvaMonAcquired != BAD_VAR_NUM)
        {
            saveSizeWithPsp += TARGET_POINTER_SIZE;
        }
        if (_compiler.lvaResumedIndicator != BAD_VAR_NUM)
        {
            saveSizeWithPsp += TARGET_POINTER_SIZE;
        }
        if (_compiler.lvaAsyncThreadObjectVar != BAD_VAR_NUM)
        {
            saveSizeWithPsp += TARGET_POINTER_SIZE;
        }
        if (_compiler.lvaAsyncExecutionContextVar != BAD_VAR_NUM)
        {
            saveSizeWithPsp += TARGET_POINTER_SIZE;
        }
        if (_compiler.lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
        {
            saveSizeWithPsp += TARGET_POINTER_SIZE;
        }

        var outgoingArgSpaceSize = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        assert((outgoingArgSpaceSize % REGSIZE_BYTES) == 0);
        var funcletFrameSize = unchecked((uint)preSpillRegArgSize + (uint)saveSizeWithPsp + outgoingArgSpaceSize);
        var funcletFrameSizeAligned = roundUp(funcletFrameSize, STACK_ALIGN);
        var spDelta = funcletFrameSizeAligned - (uint)saveRegsSize;

        genFuncletInfo.fiSaveRegs = saveRegs;
        genFuncletInfo.fiSpDelta = spDelta;

#if DEBUG
        if (_verbose)
        {
            jitprintf("\nFunclet prolog / epilog info\n");
            jitprintf("                        Save regs: ");
            dspRegMask(saveRegs);
            jitprintf($"\n                         SP delta: {genFuncletInfo.fiSpDelta}\n");
        }
#endif
#elif TARGET_ARM64
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);

        var saveRegs = _regSet.rsGetModifiedCalleeSavedRegsMask() | new regMaskTP(SRBM_LR | SRBM_FP);
        assert((saveRegs & new regMaskTP(SRBM_LR)).IsNonEmpty);
        assert((saveRegs & new regMaskTP(SRBM_FP)).IsNonEmpty);
        var saveRegsSize = unchecked((uint)genCountBits(saveRegs) * REGSIZE_BYTES);

        if (_compiler.info.compIsVarArgs)
        {
            saveRegsSize += MAX_REG_ARG * REGSIZE_BYTES;
        }

        if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            saveRegsSize += unchecked((uint)_compiler.lvaLclStackHomeSize(_compiler.lvaMonAcquired));
        }
        if ((_compiler.lvaResumedIndicator != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            saveRegsSize += unchecked((uint)_compiler.lvaLclStackHomeSize(_compiler.lvaResumedIndicator));
        }
        if ((_compiler.lvaAsyncThreadObjectVar != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            saveRegsSize += unchecked((uint)_compiler.lvaLclStackHomeSize(_compiler.lvaAsyncThreadObjectVar));
        }
        if ((_compiler.lvaAsyncExecutionContextVar != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            saveRegsSize += unchecked((uint)_compiler.lvaLclStackHomeSize(_compiler.lvaAsyncExecutionContextVar));
        }
        if ((_compiler.lvaAsyncSynchronizationContextVar != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            saveRegsSize += unchecked((uint)_compiler.lvaLclStackHomeSize(_compiler.lvaAsyncSynchronizationContextVar));
        }

        var saveRegsSizeAligned = roundUp(saveRegsSize, (uint)STACK_ALIGN);
        var outgoingArgSpaceSize = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        assert((outgoingArgSpaceSize % REGSIZE_BYTES) == 0);
        var outgoingArgSpaceAligned = roundUp(outgoingArgSpaceSize, (uint)STACK_ALIGN);
        var twoSpAdjustmentFrameSizeAligned = saveRegsSizeAligned + outgoingArgSpaceAligned;
        assert((twoSpAdjustmentFrameSizeAligned % STACK_ALIGN) == 0);

        int spToFplrSaveDelta;
        int spToCalleeSaveDelta;
        // Types 4-5 place FP/LR at the highest addresses to match main frames used with GS cookies or localloc.
        // Types 3 and 5 split large outgoing areas into two aligned SP adjustments to keep save offsets encodable.
        var useFrameType5 = genSaveFpLrWithAllCalleeSavedRegisters && genForceFuncletFrameType5 &&
            (outgoingArgSpaceSize > 0);

        if ((twoSpAdjustmentFrameSizeAligned <= 512) && !useFrameType5)
        {
            var oneSpAdjustmentFrameSize = saveRegsSize + outgoingArgSpaceSize;
            var oneSpAdjustmentFrameSizeAligned = roundUp(oneSpAdjustmentFrameSize, (uint)STACK_ALIGN);
            assert(oneSpAdjustmentFrameSizeAligned <= twoSpAdjustmentFrameSizeAligned);

            var alignmentPad = oneSpAdjustmentFrameSizeAligned - oneSpAdjustmentFrameSize;
            assert((alignmentPad == 0) || (alignmentPad == REGSIZE_BYTES));
            if (genSaveFpLrWithAllCalleeSavedRegisters)
            {
                spToFplrSaveDelta = unchecked((int)oneSpAdjustmentFrameSizeAligned - 2 * REGSIZE_BYTES);
                if (_compiler.info.compIsVarArgs)
                {
                    spToFplrSaveDelta -= MAX_REG_ARG * REGSIZE_BYTES;
                }

                spToCalleeSaveDelta = unchecked((int)outgoingArgSpaceSize + (int)alignmentPad);
                genFuncletInfo.fiFrameType = 4;
            }
            else
            {
                spToFplrSaveDelta = unchecked((int)outgoingArgSpaceSize);
                spToCalleeSaveDelta = spToFplrSaveDelta + 2 * REGSIZE_BYTES + unchecked((int)alignmentPad);
                genFuncletInfo.fiFrameType = outgoingArgSpaceSize == 0 ? 1 : 2;
            }

            genFuncletInfo.fiSpDelta1 = -unchecked((int)oneSpAdjustmentFrameSizeAligned);
            genFuncletInfo.fiSpDelta2 = 0;
            assert(genFuncletInfo.fiSpDelta1 + genFuncletInfo.fiSpDelta2 ==
                -unchecked((int)oneSpAdjustmentFrameSizeAligned));
        }
        else
        {
            var saveRegsAlignmentPad = saveRegsSizeAligned - saveRegsSize;
            assert((saveRegsAlignmentPad == 0) || (saveRegsAlignmentPad == REGSIZE_BYTES));
            if (genSaveFpLrWithAllCalleeSavedRegisters)
            {
                spToFplrSaveDelta = unchecked((int)twoSpAdjustmentFrameSizeAligned - 2 * REGSIZE_BYTES);
                if (_compiler.info.compIsVarArgs)
                {
                    spToFplrSaveDelta -= MAX_REG_ARG * REGSIZE_BYTES;
                }

                spToCalleeSaveDelta = unchecked((int)outgoingArgSpaceAligned + (int)saveRegsAlignmentPad);
                genFuncletInfo.fiFrameType = 5;
            }
            else
            {
                spToFplrSaveDelta = unchecked((int)outgoingArgSpaceAligned);
                spToCalleeSaveDelta = spToFplrSaveDelta + 2 * REGSIZE_BYTES +
                    unchecked((int)saveRegsAlignmentPad);
                genFuncletInfo.fiFrameType = 3;
            }

            genFuncletInfo.fiSpDelta1 = -unchecked((int)saveRegsSizeAligned);
            genFuncletInfo.fiSpDelta2 = -unchecked((int)outgoingArgSpaceAligned);
            assert(genFuncletInfo.fiSpDelta1 + genFuncletInfo.fiSpDelta2 ==
                -unchecked((int)twoSpAdjustmentFrameSizeAligned));
        }

        genFuncletInfo.fiSaveRegs = saveRegs;
        genFuncletInfo.fiSP_to_FPLR_save_delta = spToFplrSaveDelta;
        genFuncletInfo.fiSP_to_CalleeSave_delta = spToCalleeSaveDelta;
#if DEBUG
        if (_verbose)
        {
            jitprintf("\nFunclet prolog / epilog info\n");
            jitprintf("                        Save regs: ");
            dspRegMask(saveRegs);
            jitprintf($"\n  SP to FP/LR save location delta: {spToFplrSaveDelta}\n");
            jitprintf($"    SP to callee-saved area delta: {spToCalleeSaveDelta}\n");
            jitprintf($"                       Frame type: {genFuncletInfo.fiFrameType}\n");
            jitprintf($"                       SP delta 1: {genFuncletInfo.fiSpDelta1}\n");
            jitprintf($"                       SP delta 2: {genFuncletInfo.fiSpDelta2}\n");
        }

        assert(spToFplrSaveDelta >= 0);
        assert(spToCalleeSaveDelta >= 0);
#endif
#elif TARGET_RISCV64
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);

        var saveRegs = _regSet.rsGetCalleeSavedRegsMask();
        assert((saveRegs & new regMaskTP(SRBM_RA)).IsNonEmpty);
        assert((saveRegs & new regMaskTP(SRBM_FP)).IsNonEmpty);

        var frameSize = _compiler.lvaOutgoingArgSpaceSize.Value;
        genFuncletInfo.fiSP_to_CalleeSave_delta = frameSize;
        frameSize = unchecked(frameSize + (unchecked((int)genCountBits(saveRegs)) * REGSIZE_BYTES));

        var pspDelta = -TARGET_POINTER_SIZE;
        if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            pspDelta -= TARGET_POINTER_SIZE;
        }

        frameSize = unchecked(frameSize - pspDelta);
        var alignedFrameSize = roundUp(unchecked((uint)frameSize), (uint)STACK_ALIGN);
        genFuncletInfo.fiSpDelta = unchecked((uint)-unchecked((int)alignedFrameSize));
        genFuncletInfo.fiSaveRegs = saveRegs;

#if DEBUG
        if (_verbose)
        {
            jitprintf("\nFunclet prolog / epilog info\n");
            jitprintf("                        Save regs: ");
            dspRegMask(saveRegs);
            jitprintf($"\n  SP to CalleeSaved location delta: {genFuncletInfo.fiSP_to_CalleeSave_delta}\n");
            jitprintf($"                       SP delta: {unchecked((int)genFuncletInfo.fiSpDelta)}\n");
        }

        assert(genFuncletInfo.fiSP_to_CalleeSave_delta >= 0);
#endif
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet frame capture requires xarch.");
#else
        // Native ehAnyFunclets() is compHndBBtabCount > 0.
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

#if TARGET_AMD64
        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);
        var outgoing = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        assert(outgoing % REGSIZE_BYTES == 0);
#if WINDOWS_AMD64_ABI
        assert(outgoing == 0 || outgoing >= 4 * REGSIZE_BYTES);
#endif

        var totalFrameSize = REGSIZE_BYTES + outgoing;
        var padding = (16 - (totalFrameSize % 16)) % 16;
        genFuncletInfo.fiSpDelta = padding + outgoing;

#if DEBUG
        if (_verbose)
        {
            jitprintf($"\nFunclet prolog / epilog info\n                         SP delta: {genFuncletInfo.fiSpDelta}\n");
        }
#endif
#endif
#endif
    }
}
