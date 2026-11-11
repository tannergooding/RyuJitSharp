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
        public uint fiSpDelta;
    }

    private FuncletPrologEpilogInfo genFuncletInfo;

    public unsafe void genFuncletProlog(BasicBlock block)
    {
        if (Configuration.TARGET_WASM)
        {
            genFuncletPrologWasm(block);
        }
        else if (Configuration.TARGET_ARM)
        {
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletProlog()\n");
            }
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

            var liveIn = block.CatchType switch {
                BBCT_FILTER => new regMaskTP(SRBM_ARM32_R0 | SRBM_ARM32_R1),
                BBCT_FINALLY or BBCT_FAULT => RBM_NONE,
                _ => new regMaskTP(SRBM_ARM32_R0),
            };
            var initRegZeroed = false;
            if (maskStackAlloc.IsEmpty)
            {
                // R3 is not live on funclet entry and is available when a large frame needs a stack-probe scratch register.
                genAllocLclFrame(genFuncletInfo.fiSpDelta, REG_ARM32_R3, ref initRegZeroed, liveIn);
            }

            _compiler.unwindEndProlog();
        }
        else if (Configuration.TARGET_ARM64)
        {
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletProlog()\n");
            }
            assert(block is not null);
            assert(_compiler.bbIsFuncletBeg(block));
            GCInfo.gcResetForBB();
            _compiler.unwindBegProlog();

            if (JitConfig.JitPacEnabled != 0)
            {
                Emitter.emitPacInProlog();
            }

            Emitter.emitIns_R_R_I(
                INS_str,
                EA_PTRSIZE,
                REG_ARM64_LR,
                TargetRuntimeMetadata.StackPointerBaseRegister,
                -Configuration.STACK_ALIGN,
                INS_OPTS_PRE_INDEX);
            _compiler.unwindSaveRegPreindexed(REG_ARM64_LR, -Configuration.STACK_ALIGN);

            if (genFuncletInfo.fiSpDelta > 0)
            {
                genStackPointerAdjustmentArm64(
                    unchecked(-unchecked((int)genFuncletInfo.fiSpDelta)),
                    REG_ARM64_R2,
                    null,
                    reportUnwindData: true);
            }

            _compiler.unwindEndProlog();
        }
        else if (Configuration.TARGET_RISCV64)
        {
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletProlog()\n");
            }
            assert(block is not null);
            assert(_compiler.bbIsFuncletBeg(block));

            GCInfo.gcResetForBB();
            _compiler.unwindBegProlog();

            genStackPointerAdjustmentRiscV64(
                -Configuration.STACK_ALIGN,
                TargetRuntimeMetadata.ScratchRegister,
                null,
                reportUnwindData: true);

            Emitter.emitIns_R_R_I(
                INS_sd,
                EA_PTRSIZE,
                REG_RISCV64_RA,
                TargetRuntimeMetadata.StackPointerBaseRegister,
                Configuration.REGSIZE_BYTES);
            _compiler.unwindSaveReg(REG_RISCV64_RA, Configuration.REGSIZE_BYTES);

            if (genFuncletInfo.fiSpDelta > 0)
            {
                genStackPointerAdjustmentRiscV64(
                    unchecked(-unchecked((int)genFuncletInfo.fiSpDelta)),
                    TargetRuntimeMetadata.ScratchRegister,
                    null,
                    reportUnwindData: true);
            }

            _compiler.unwindEndProlog();
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genFuncletPrologLoongArch64(block);
        }
        else if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.FuncletPrologsRequireXarch);
            return;
        }
        else
        {
            if (Configuration.TARGET_AMD64)
            {
                Emitter.RequireSupportedInstructionRecording();
            }
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletProlog()\n");
            }
            if (Configuration.TARGET_AMD64)
            {
                assert(!_regSet.rsRegsModified(new regMaskTP(SRBM_X64_RBP)));
                assert(_compiler.bbIsFuncletBeg(block));
                assert(IsFramePointerUsed);
            }

            GCInfo.gcResetForBB();
            _compiler.unwindBegProlog();

            if (Configuration.TARGET_AMD64)
            {
                var arg0Mask = Configuration.WINDOWS_AMD64_ABI ? SRBM_X64_RCX : SRBM_X64_RDI;
                var arg2Mask = Configuration.WINDOWS_AMD64_ABI ? SRBM_X64_R8 : SRBM_X64_RDX;
                var liveIn = block.CatchType is BBCT_FINALLY or BBCT_FAULT
                    ? new regMaskTP(arg0Mask)
                    : new regMaskTP(arg0Mask | arg2Mask);
                var initRegZeroed = false;
                genAllocLclFrame(genFuncletInfo.fiSpDelta, REG_NA, ref initRegZeroed, liveIn);
            }

            _compiler.unwindEndProlog();
            if (Configuration.TARGET_X86)
            {
                if (!_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
                {
                    instGen(INS_nop);
                }
            }
            genClearAvxStateInProlog();
        }
    }

    public unsafe void genFuncletEpilog(BasicBlock block)
    {
        if (Configuration.TARGET_WASM)
        {
            genFuncletEpilogWasm(block);
        }
        else if (Configuration.TARGET_ARM)
        {
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletEpilog()\n");
            }
            var unwindStarted = false;
            assert((genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_ARM32_LR)).IsNonEmpty);

            var maskPopRegsFloat = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_ALLFLOAT);
            var maskPopRegsInt = genFuncletInfo.fiSaveRegs & ~maskPopRegsFloat;
            var maskStackAlloc = genStackAllocRegisterMask(genFuncletInfo.fiSpDelta, maskPopRegsFloat);
            maskPopRegsInt |= maskStackAlloc;

            if (maskStackAlloc.IsEmpty)
            {
                genFreeLclFrameArm(genFuncletInfo.fiSpDelta, ref unwindStarted);
            }

            if (!unwindStarted)
            {
                _compiler.unwindBegEpilog();
                unwindStarted = true;
            }

            maskPopRegsInt &= ~new regMaskTP(SRBM_ARM32_LR);
            maskPopRegsInt |= new regMaskTP(SRBM_ARM32_PC);

            if (maskPopRegsFloat.IsNonEmpty)
            {
                genPopFltRegsArmCore(maskPopRegsFloat);
                _compiler.unwindPopMaskFloat(maskPopRegsFloat);
            }

            assert(unchecked((ulong)maskPopRegsInt.Lower) <= int.MaxValue);
            inst_IV(INS_pop, unchecked((int)maskPopRegsInt.Lower));
            _compiler.unwindPopMaskInt(maskPopRegsInt);
            _compiler.unwindEndEpilog();
        }
        else if (Configuration.TARGET_ARM64)
        {
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletEpilog()\n");
            }
            _compiler.unwindBegEpilog();

            if (genFuncletInfo.fiSpDelta > 0)
            {
                genStackPointerAdjustmentArm64(
                    unchecked((int)genFuncletInfo.fiSpDelta),
                    REG_ARM64_R2,
                    null,
                    reportUnwindData: true);
            }

            Emitter.emitIns_R_R_I(
                INS_ldr,
                EA_PTRSIZE,
                REG_ARM64_LR,
                TargetRuntimeMetadata.StackPointerBaseRegister,
                Configuration.STACK_ALIGN,
                INS_OPTS_POST_INDEX);
            _compiler.unwindSaveRegPreindexed(REG_ARM64_LR, -Configuration.STACK_ALIGN);

            if (JitConfig.JitPacEnabled != 0)
            {
                Emitter.emitPacInEpilog();
            }

            inst_RV(INS_ret, REG_ARM64_LR, TYP_I_IMPL);
            _compiler.unwindReturn(REG_ARM64_LR);
            _compiler.unwindEndEpilog();
        }
        else if (Configuration.TARGET_RISCV64)
        {
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletEpilog()\n");
            }
            _compiler.unwindBegEpilog();

            if (genFuncletInfo.fiSpDelta > 0)
            {
                genStackPointerAdjustmentRiscV64(
                    unchecked((int)genFuncletInfo.fiSpDelta),
                    TargetRuntimeMetadata.ScratchRegister,
                    null,
                    reportUnwindData: true);
            }

            Emitter.emitIns_R_R_I(
                INS_ld,
                EA_PTRSIZE,
                REG_RISCV64_RA,
                TargetRuntimeMetadata.StackPointerBaseRegister,
                Configuration.REGSIZE_BYTES);
            _compiler.unwindSaveReg(REG_RISCV64_RA, Configuration.REGSIZE_BYTES);

            genStackPointerAdjustmentRiscV64(
                Configuration.STACK_ALIGN,
                TargetRuntimeMetadata.ScratchRegister,
                null,
                reportUnwindData: true);

            Emitter.emitIns_R_R_I(INS_jalr, EA_PTRSIZE, REG_RISCV64_R0, REG_RISCV64_RA, 0);
            _compiler.unwindReturn(REG_RISCV64_RA);
            _compiler.unwindEndEpilog();
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genFuncletEpilogLoongArch64();
        }
        else if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.FuncletEpilogsRequireXarch);
            return;
        }
        else
        {
            if (Configuration.TARGET_AMD64)
            {
                Emitter.RequireSupportedInstructionRecording();
            }
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("*************** In genFuncletEpilog()\n");
            }
            genClearAvxStateInEpilog();
            if (Configuration.TARGET_AMD64)
            {
                inst_RV_IV(INS_add, TargetRuntimeMetadata.StackPointerBaseRegister, (nint)genFuncletInfo.fiSpDelta, EA_PTRSIZE);
            }
            instGen_Return(0);
        }
    }

    public void genCaptureFuncletPrologEpilogInfo()
    {
        if (Configuration.TARGET_WASM)
        {
            // Wasm has no funclet stack delta to capture.
        }
        else if (Configuration.TARGET_ARM)
        {
            if (_compiler.compHndBBtabCount == 0)
            {
                return;
            }

            assert(IsFramePointerUsed);
            assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);

            var preSpillRegArgSize = unchecked((int)genCountBits(_regSet.rsMaskPreSpillRegs(true))) * Configuration.REGSIZE_BYTES;
            var saveRegs = _regSet.rsGetModifiedCalleeSavedRegsMask() |
                new regMaskTP(SRBM_ARM32_FP | SRBM_ARM32_LR);
            assert((saveRegs & new regMaskTP(SRBM_ARM32_R12 | SRBM_ARM32_SP)).IsEmpty);
            var saveRegsSize = unchecked((int)genCountBits(saveRegs)) * Configuration.REGSIZE_BYTES;
            var saveSizeWithPsp = saveRegsSize + Configuration.REGSIZE_BYTES;
            if (_compiler.lvaMonAcquired != BAD_VAR_NUM)
            {
                saveSizeWithPsp += Configuration.TARGET_POINTER_SIZE;
            }
            if (_compiler.lvaResumedIndicator != BAD_VAR_NUM)
            {
                saveSizeWithPsp += Configuration.TARGET_POINTER_SIZE;
            }
            if (_compiler.lvaAsyncThreadObjectVar != BAD_VAR_NUM)
            {
                saveSizeWithPsp += Configuration.TARGET_POINTER_SIZE;
            }
            if (_compiler.lvaAsyncExecutionContextVar != BAD_VAR_NUM)
            {
                saveSizeWithPsp += Configuration.TARGET_POINTER_SIZE;
            }
            if (_compiler.lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
            {
                saveSizeWithPsp += Configuration.TARGET_POINTER_SIZE;
            }

            var outgoingArgSpaceSize = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
            assert((outgoingArgSpaceSize % Configuration.REGSIZE_BYTES) == 0);
            var funcletFrameSize = unchecked((uint)preSpillRegArgSize + (uint)saveSizeWithPsp + outgoingArgSpaceSize);
            var funcletFrameSizeAligned = roundUp(funcletFrameSize, unchecked((uint)Configuration.STACK_ALIGN));
            var spDelta = funcletFrameSizeAligned - (uint)saveRegsSize;

            genFuncletInfo.fiSaveRegs = saveRegs;
            genFuncletInfo.fiSpDelta = spDelta;

            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("\nFunclet prolog / epilog info\n");
                jitprintf("                        Save regs: ");
                dspRegMask(saveRegs);
                jitprintf($"\n                         SP delta: {genFuncletInfo.fiSpDelta}\n");
            }
        }
        else if (Configuration.TARGET_ARM64)
        {
            if (_compiler.compHndBBtabCount == 0)
            {
                return;
            }

            assert(IsFramePointerUsed);
            assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
            noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);

            var outgoingArgSpaceSize = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
            assert((outgoingArgSpaceSize % Configuration.REGSIZE_BYTES) == 0);
            genFuncletInfo.fiSpDelta = roundUp(outgoingArgSpaceSize, (uint)Configuration.STACK_ALIGN);
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("\nFunclet prolog / epilog info\n");
                jitprintf($"              Outgoing arg space: {genFuncletInfo.fiSpDelta}\n");
            }
        }
        else if (Configuration.TARGET_RISCV64)
        {
            if (_compiler.compHndBBtabCount == 0)
            {
                return;
            }

            assert(IsFramePointerUsed);
            assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);

            noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);
            var outgoingArgSpaceSize = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
            assert((outgoingArgSpaceSize % Configuration.REGSIZE_BYTES) == 0);
            genFuncletInfo.fiSpDelta = roundUp(outgoingArgSpaceSize, (uint)Configuration.STACK_ALIGN);

            if (Configuration.DEBUG && _verbose)
            {
                jitprintf("\nFunclet prolog / epilog info\n");
                jitprintf($"              Outgoing arg space: {genFuncletInfo.fiSpDelta}\n");
            }
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genCaptureFuncletPrologEpilogInfoLoongArch64();
        }
        else if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.FuncletFrameCaptureRequiresXarch);
            return;
        }
        else if (Configuration.TARGET_AMD64)
        {
            // Native ehAnyFunclets() is compHndBBtabCount > 0.
            if (_compiler.compHndBBtabCount == 0)
            {
                return;
            }

            assert(IsFramePointerUsed);
            assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
            noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);
            var outgoing = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
            assert(outgoing % Configuration.REGSIZE_BYTES == 0);
            if (Configuration.WINDOWS_AMD64_ABI)
            {
                assert(outgoing == 0 || outgoing >= 4 * Configuration.REGSIZE_BYTES);
            }

            var totalFrameSize = Configuration.REGSIZE_BYTES + outgoing;
            var padding = (16 - (totalFrameSize % 16)) % 16;
            genFuncletInfo.fiSpDelta = unchecked((uint)padding + outgoing);
            if (Configuration.DEBUG && _verbose)
            {
                jitprintf($"\nFunclet prolog / epilog info\n                         SP delta: {genFuncletInfo.fiSpDelta}\n");
            }
        }
    }
}
