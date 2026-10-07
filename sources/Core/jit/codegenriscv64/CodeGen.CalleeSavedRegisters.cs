// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genPushCalleeSavedRegistersRiscV(regNumber initReg, ref bool initRegZeroed)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        var pushRegs = _regSet.rsGetModifiedCalleeSavedRegsMask();
        var framePointerMask = regMaskTP.CreateFromRegNum(REG_FPBASE, REG_FPBASE.SingleTypeMask);

#if ETW_EBP_FRAMED
        if (!IsFramePointerUsed && _regSet.rsRegsModified(framePointerMask))
        {
            noway_assert(false, "Used register RBM_FPBASE as a scratch register.");
        }
#endif

        assert(IsFramePointerUsed);

        var calleeSavedRegs = pushRegs | framePointerMask | RBM_RA;
        _regSet.rsSetCalleeSavedRegsMask(calleeSavedRegs);

#if DEBUG
        JITDUMP(
            $"Frame info. #outsz={_compiler.lvaOutgoingArgSpaceSize.Value}; #framesz={genTotalFrameSize}; LclFrameSize={_compiler.compLclFrameSize};\n");

        var expectedPushCount = unchecked((int)genCountBits(calleeSavedRegs));
        if (_compiler.compCalleeRegsPushed != expectedPushCount)
        {
            jitprintf(
                $"Error: unexpected number of callee-saved registers to push. Expected: {_compiler.compCalleeRegsPushed}. Got: {expectedPushCount} ");
            dspRegMask(calleeSavedRegs);
            jitprintf("\n");
            assert(_compiler.compCalleeRegsPushed == expectedPushCount);
        }

        if (_verbose)
        {
            var maskSaveRegsFloat = pushRegs & new regMaskTP(SRBM_FLT_CALLEE_SAVED);
            var maskSaveRegsInt = pushRegs & ~maskSaveRegsFloat;
            jitprintf("Save float regs: ");
            dspRegMask(maskSaveRegsFloat);
            jitprintf("\n");
            jitprintf("Save int   regs: ");
            dspRegMask(maskSaveRegsInt);
            jitprintf("\n");
        }
#endif

        var totalFrameSize = genTotalFrameSize;
        var leftFrameSize = 0;
        var localFrameSize = _compiler.compLclFrameSize;
        if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            localFrameSize -= TARGET_POINTER_SIZE;
        }

        if (_compiler.opts.disAsm)
        {
            jitprintf(
                $"Frame info. #outsz={_compiler.lvaOutgoingArgSpaceSize.Value}; #framesz={genTotalFrameSize}; lcl={localFrameSize}\n");
        }

        var fpOffset = localFrameSize;
        if (totalFrameSize <= 2040)
        {
            Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, -totalFrameSize);
            _compiler.unwindAllocStack(unchecked((uint)totalFrameSize));
        }
        else
        {
            if ((localFrameSize + (_compiler.compCalleeRegsPushed << 3)) > 2040)
            {
                leftFrameSize = localFrameSize & -16;
                totalFrameSize -= localFrameSize & -16;
                fpOffset = localFrameSize & 0xf;
            }

            unsafe
            {
                genStackPointerAdjustment(unchecked(-totalFrameSize), REG_SCRATCH, null, reportUnwindData: true);
            }
        }

        Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
        _compiler.unwindSaveReg(REG_FP, unchecked((uint)fpOffset));

        Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_RA, REG_SPBASE, unchecked(fpOffset + 8));
        _compiler.unwindSaveReg(REG_RA, unchecked((uint)(fpOffset + 8)));

        genSaveCalleeSavedRegistersHelp(pushRegs, unchecked(fpOffset + 16));

        JITDUMP($"    offsetSpToSavedFp={fpOffset}\n");
        genEstablishFramePointer(fpOffset, reportUnwindData: true);

        if (_compiler.info.compIsVarArgs)
        {
            JITDUMP("    compIsVarArgs=true\n");
            NYI_RISCV64("genPushCalleeSavedRegisters unsupports compIsVarArgs");
        }

        if (leftFrameSize != 0)
        {
            unsafe
            {
                genStackPointerAdjustment(unchecked(-leftFrameSize), REG_SCRATCH, null, reportUnwindData: false);
            }
        }
    }

    private unsafe void genStackPointerAdjustment(nint spDelta, regNumber tmpReg,
        bool* pTmpRegIsZero, bool reportUnwindData)
    {
        var wasTempRegisterUsedForImmediate =
            !genInstrWithConstant(INS_addi, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, spDelta, tmpReg,
                inUnwindRegion: true);
        if (wasTempRegisterUsedForImmediate && (pTmpRegIsZero != null))
        {
            *pTmpRegIsZero = false;
        }

        if (reportUnwindData)
        {
            var spDeltaAbs = spDelta < 0 ? unchecked(-spDelta) : spDelta;
            var unwindSpDelta = unchecked((uint)spDeltaAbs);
            assert(unchecked((nint)unwindSpDelta) == spDeltaAbs,
                "(ssize_t)unwindSpDelta == spDeltaAbs");

            _compiler.unwindAllocStack(unwindSpDelta);
        }
    }

    private void genSaveCalleeSavedRegistersHelp(regMaskTP regsToSaveMask, int lowestCalleeSavedOffset)
    {
        if (regsToSaveMask.IsEmpty)
        {
            return;
        }

        var calleeSavedMask = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED);
        assert((regsToSaveMask & ~calleeSavedMask).IsEmpty);
        assert(lowestCalleeSavedOffset >= 0);

        var integerMask = new regMaskTP(SRBM_INT_CALLEE_SAVED);
        var registersToSave = regsToSaveMask & integerMask;
        var maskSaveRegisters = unchecked((ulong)registersToSave.Lower) >> (int)REG_S1;
        var registerNumber = (int)REG_S1;
        do
        {
            if ((maskSaveRegisters & 1) != 0)
            {
                Emitter.emitIns_R_R_I(INS_sd, EA_8BYTE, (regNumber)registerNumber, REG_SP,
                    lowestCalleeSavedOffset);
                _compiler.unwindSaveReg((regNumber)registerNumber, unchecked((uint)lowestCalleeSavedOffset));
                lowestCalleeSavedOffset += REGSIZE_BYTES;
            }

            maskSaveRegisters >>= 1;
            registerNumber++;
        } while (maskSaveRegisters != 0);

        var floatMask = new regMaskTP(SRBM_FLT_CALLEE_SAVED);
        registersToSave = regsToSaveMask & floatMask;
        maskSaveRegisters = unchecked((ulong)registersToSave.Lower) >> (int)REG_FS0;
        registerNumber = (int)REG_FS0;
        do
        {
            if ((maskSaveRegisters & 1) != 0)
            {
                Emitter.emitIns_R_R_I(INS_fsd, EA_8BYTE, (regNumber)registerNumber, REG_SP,
                    lowestCalleeSavedOffset);
                _compiler.unwindSaveReg((regNumber)registerNumber, unchecked((uint)lowestCalleeSavedOffset));
                lowestCalleeSavedOffset += REGSIZE_BYTES;
            }

            maskSaveRegisters >>= 1;
            registerNumber++;
        } while (maskSaveRegisters != 0);
    }

    private void genRestoreCalleeSavedRegistersHelp(regMaskTP regsToRestoreMask, regNumber baseReg,
        int lowestCalleeSavedOffset, bool reportUnwindData)
    {
        var calleeSavedMask = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED);
        assert((regsToRestoreMask & ~calleeSavedMask).IsEmpty);
        if (regsToRestoreMask.IsEmpty)
        {
            return;
        }

        var highestCalleeSavedOffset =
            unchecked((int)(genCountBits(regsToRestoreMask) << 3)) + lowestCalleeSavedOffset;
        assert((highestCalleeSavedOffset & 7) == 0);
        assert(highestCalleeSavedOffset >= 16);

        var floatMask = new regMaskTP(SRBM_FLT_CALLEE_SAVED);
        var registersToRestore = regsToRestoreMask & floatMask;
        var maskSaveRegisters = unchecked((long)registersToRestore.Lower) << (63 - (int)REG_FS11);
        var registerNumber = (int)REG_FS11;
        do
        {
            if (maskSaveRegisters < 0)
            {
                highestCalleeSavedOffset -= REGSIZE_BYTES;
                Emitter.emitIns_R_R_I(INS_fld, EA_8BYTE, (regNumber)registerNumber, baseReg,
                    highestCalleeSavedOffset);
                if (reportUnwindData)
                {
                    _compiler.unwindSaveReg((regNumber)registerNumber,
                        unchecked((uint)highestCalleeSavedOffset));
                }
            }

            maskSaveRegisters <<= 1;
            registerNumber--;
        } while (maskSaveRegisters != 0);

        var integerMask = new regMaskTP(SRBM_INT_CALLEE_SAVED);
        registersToRestore = regsToRestoreMask & integerMask;
        maskSaveRegisters = unchecked((long)registersToRestore.Lower) << (63 - (int)REG_S11);
        registerNumber = (int)REG_S11;
        do
        {
            if (maskSaveRegisters < 0)
            {
                highestCalleeSavedOffset -= REGSIZE_BYTES;
                Emitter.emitIns_R_R_I(INS_ld, EA_8BYTE, (regNumber)registerNumber, baseReg,
                    highestCalleeSavedOffset);
                if (reportUnwindData)
                {
                    _compiler.unwindSaveReg((regNumber)registerNumber,
                        unchecked((uint)highestCalleeSavedOffset));
                }
            }

            maskSaveRegisters <<= 1;
            registerNumber--;
        } while (maskSaveRegisters != 0);

        assert(highestCalleeSavedOffset >= 16, "the callee-saved regs always above ra/fp");
    }
}
#endif
