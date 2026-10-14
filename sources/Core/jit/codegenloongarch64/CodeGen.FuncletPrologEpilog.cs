// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genFuncletPrologLoongArch64(BasicBlock block)
    {
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }

        assert(block is not null);
        assert(_compiler.bbIsFuncletBeg(block));

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

        var frameSize = unchecked((int)genFuncletInfo.fiSpDelta);
        assert(frameSize < 0);

        var maskSaveRegs = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_CALLEE_SAVED_LOONGARCH64);
        var fpOffset = genFuncletInfo.fiSP_to_CalleeSave_delta;

        if ((fpOffset + (unchecked((int)genCountBits(maskSaveRegs)) << 3)) <= (2040 - 16))
        {
            genStackPointerAdjustment(frameSize, REG_LOONGARCH64_R21, null, reportUnwindData: true);

            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_FP, REG_SPBASE_LOONGARCH64, fpOffset);
            _compiler.unwindSaveReg(REG_LOONGARCH64_FP, fpOffset);

            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_RA, REG_SPBASE_LOONGARCH64, fpOffset + 8);
            _compiler.unwindSaveReg(REG_LOONGARCH64_RA, fpOffset + 8);

            genSaveCalleeSavedRegistersHelp(maskSaveRegs, fpOffset + 16);
        }
        else
        {
            assert(frameSize < -2040);

            genStackPointerAdjustment(frameSize + (fpOffset & -16), REG_LOONGARCH64_R21, null, reportUnwindData: true);

            frameSize = -(fpOffset & -16);
            fpOffset &= 0xf;

            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_FP, REG_SPBASE_LOONGARCH64, fpOffset);
            _compiler.unwindSaveReg(REG_LOONGARCH64_FP, fpOffset);

            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_RA, REG_SPBASE_LOONGARCH64, fpOffset + 8);
            _compiler.unwindSaveReg(REG_LOONGARCH64_RA, fpOffset + 8);

            genSaveCalleeSavedRegistersHelp(maskSaveRegs, fpOffset + 16);

            genStackPointerAdjustment(frameSize, REG_LOONGARCH64_R21, null, reportUnwindData: true);
        }

        _compiler.unwindEndProlog();
    }

    private unsafe void genFuncletEpilogLoongArch64()
    {
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }

        _compiler.unwindBegEpilog();

        var frameSize = unchecked((int)genFuncletInfo.fiSpDelta);
        assert(frameSize < 0);

        var maskSaveRegs = genFuncletInfo.fiSaveRegs & new regMaskTP(SRBM_CALLEE_SAVED_LOONGARCH64);
        var fpOffset = genFuncletInfo.fiSP_to_CalleeSave_delta;

        if ((fpOffset + (unchecked((int)genCountBits(maskSaveRegs)) << 3)) > (2040 - 16))
        {
            assert(frameSize < -2040);

            genStackPointerAdjustment(fpOffset & -16, REG_LOONGARCH64_R21, null, reportUnwindData: true);

            frameSize += fpOffset & -16;
            fpOffset &= 0xf;
        }

        genRestoreCalleeSavedRegistersHelpLoongArch64(maskSaveRegs, REG_SPBASE_LOONGARCH64, fpOffset + 16, reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_ld_d, EA_PTRSIZE, REG_LOONGARCH64_RA, REG_SPBASE_LOONGARCH64, fpOffset + 8);
        _compiler.unwindSaveReg(REG_LOONGARCH64_RA, fpOffset + 8);

        Emitter.emitIns_R_R_I(INS_ld_d, EA_PTRSIZE, REG_LOONGARCH64_FP, REG_SPBASE_LOONGARCH64, fpOffset);
        _compiler.unwindSaveReg(REG_LOONGARCH64_FP, fpOffset);

        genStackPointerAdjustment(-frameSize, REG_LOONGARCH64_R21, null, reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_jirl, TYP_I_IMPL.EmitActualSize, REG_LOONGARCH64_R0, REG_LOONGARCH64_RA, 0);
        _compiler.unwindReturn(REG_LOONGARCH64_RA);

        _compiler.unwindEndEpilog();
    }

    private void genCaptureFuncletPrologEpilogInfoLoongArch64()
    {
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);

        var saveRegs = _regSet.rsGetCalleeSavedRegsMask();
        assert((saveRegs & new regMaskTP(SRBM_LOONGARCH64_RA)).IsNonEmpty);
        assert((saveRegs & new regMaskTP(SRBM_LOONGARCH64_FP)).IsNonEmpty);

        var frameSize = _compiler.lvaOutgoingArgSpaceSize.Value;
        genFuncletInfo.fiSP_to_CalleeSave_delta = frameSize;
        frameSize = unchecked(frameSize + unchecked((int)genCountBits(saveRegs)) * Configuration.REGSIZE_BYTES);

        var deltaPsp = -Configuration.TARGET_POINTER_SIZE;
        if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            deltaPsp -= Configuration.TARGET_POINTER_SIZE;
        }

        frameSize = unchecked(frameSize - deltaPsp);
        frameSize = unchecked((int)roundUp(unchecked((uint)frameSize), (uint)Configuration.STACK_ALIGN));

        genFuncletInfo.fiSpDelta = unchecked((uint)-frameSize);
        genFuncletInfo.fiSaveRegs = saveRegs;

#if DEBUG
        if (_verbose)
        {
            jitprintf("\n");
            jitprintf("Funclet prolog / epilog info\n");
            jitprintf("                        Save regs: ");
            dspRegMask(saveRegs);
            jitprintf("\n");
            jitprintf($"  SP to CalleeSaved location delta: {genFuncletInfo.fiSP_to_CalleeSave_delta}\n");
            jitprintf($"                       SP delta: {unchecked((int)genFuncletInfo.fiSpDelta)}\n");
        }

        assert(genFuncletInfo.fiSP_to_CalleeSave_delta >= 0);
#endif
    }
}
