// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genStackPointerAdjustment(nint spDelta, regNumber tmpReg,
        bool* pTmpRegIsZero, bool reportUnwindData)
    {
        var wasTempRegisterUsedForImmediate =
            !genInstrWithConstant(INS_addi_d, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, spDelta, tmpReg,
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
        var maskSaveRegisters = unchecked((ulong)registersToSave.Lower) >> (int)REG_S0;
        var registerNumber = (int)REG_S0;
        do
        {
            if ((maskSaveRegisters & 1) != 0)
            {
                Emitter.emitIns_R_R_I(INS_st_d, EA_8BYTE, (regNumber)registerNumber, REG_SP,
                    lowestCalleeSavedOffset);
                _compiler.unwindSaveReg((regNumber)registerNumber, unchecked((uint)lowestCalleeSavedOffset));
                lowestCalleeSavedOffset += REGSIZE_BYTES;
            }

            maskSaveRegisters >>= 1;
            registerNumber++;
        } while (maskSaveRegisters != 0);

        var floatMask = new regMaskTP(SRBM_FLT_CALLEE_SAVED);
        registersToSave = regsToSaveMask & floatMask;
        maskSaveRegisters = unchecked((ulong)registersToSave.Lower) >> (int)REG_F24;
        registerNumber = (int)REG_F24;
        do
        {
            if ((maskSaveRegisters & 1) != 0)
            {
                Emitter.emitIns_R_R_I(INS_fst_d, EA_8BYTE, (regNumber)registerNumber, REG_SP,
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
        var maskSaveRegisters = unchecked((long)registersToRestore.Lower) << (63 - (int)REG_F31);
        var registerNumber = (int)REG_F31;
        do
        {
            if (maskSaveRegisters < 0)
            {
                highestCalleeSavedOffset -= REGSIZE_BYTES;
                Emitter.emitIns_R_R_I(INS_fld_d, EA_8BYTE, (regNumber)registerNumber, baseReg,
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
        maskSaveRegisters = unchecked((long)registersToRestore.Lower) << (63 - (int)REG_S8);
        registerNumber = (int)REG_S8;
        do
        {
            if (maskSaveRegisters < 0)
            {
                highestCalleeSavedOffset -= REGSIZE_BYTES;
                Emitter.emitIns_R_R_I(INS_ld_d, EA_8BYTE, (regNumber)registerNumber, baseReg,
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
