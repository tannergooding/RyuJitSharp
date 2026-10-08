// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genPushCalleeSavedRegistersLoongArch64(regNumber initReg, ref bool initRegZeroed)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        var registersToSaveMask = _regSet.rsGetModifiedCalleeSavedRegsMask();

#if ETW_EBP_FRAMED
        if (!IsFramePointerUsed && _regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)))
        {
            noway_assert(false);
        }
#endif

        assert(IsFramePointerUsed);
        var allSavedRegistersMask = registersToSaveMask | new regMaskTP(SRBM_FP | SRBM_RA);
        _regSet.rsSetCalleeSavedRegsMask(allSavedRegistersMask);

#if DEBUG
        JITDUMP($"Frame info. #outsz={unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value)}; " +
            $"#framesz={genTotalFrameSize}; LclFrameSize={_compiler.compLclFrameSize}\n");

        if (_compiler.compCalleeRegsPushed != genCountBits(allSavedRegistersMask))
        {
            jitprintf($"Error: unexpected number of callee-saved registers to push. Expected: " +
                $"{_compiler.compCalleeRegsPushed}. Got: {genCountBits(allSavedRegistersMask)} ");
            dspRegMask(allSavedRegistersMask);
            jitprintf("\n");
            assert(_compiler.compCalleeRegsPushed == genCountBits(allSavedRegistersMask));
        }

        if (_verbose)
        {
            var floatMask = registersToSaveMask & new regMaskTP(SRBM_ALLFLOAT);
            var intMask = registersToSaveMask & ~floatMask;
            jitprintf("Save float regs: ");
            dspRegMask(floatMask);
            jitprintf("\nSave int   regs: ");
            dspRegMask(intMask);
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

#if DEBUG
        if (_compiler.opts.disAsm)
        {
            jitprintf($"Frame info. #outsz={unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value)}; " +
                $"#framesz={genTotalFrameSize}; lcl={localFrameSize}\n");
        }
#endif

        var fpOffset = localFrameSize;
        if (totalFrameSize <= 2040)
        {
            Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, -totalFrameSize);
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

            fixed (bool* initRegZeroedPointer = &initRegZeroed)
            {
                genStackPointerAdjustment(-totalFrameSize, initReg, initRegZeroedPointer, reportUnwindData: true);
            }
        }

        Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
        _compiler.unwindSaveReg(REG_FP, unchecked((uint)fpOffset));

        Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_RA, REG_SPBASE, fpOffset + 8);
        _compiler.unwindSaveReg(REG_RA, unchecked((uint)(fpOffset + 8)));

        genSaveCalleeSavedRegistersHelp(registersToSaveMask, fpOffset + 16);
        JITDUMP($"    offsetSpToSavedFp={fpOffset}\n");
        genEstablishFramePointerLoongArch64(fpOffset, reportUnwindData: true);

        if (_compiler.info.compIsVarArgs)
        {
            JITDUMP("    compIsVarArgs=true\n");
            NYI("genPushCalleeSavedRegisters unsupports compIsVarArgs");
            throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 varargs are not supported.");
        }

        if (leftFrameSize != 0)
        {
            fixed (bool* initRegZeroedPointer = &initRegZeroed)
            {
                genStackPointerAdjustment(-leftFrameSize, initReg, initRegZeroedPointer, reportUnwindData: false);
            }
        }
    }

    private unsafe void genPopCalleeSavedRegistersLoongArch64(bool jumpEpilog = false)
    {
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

        var registersToRestoreMask = _regSet.rsGetModifiedCalleeSavedRegsMask();
        assert(IsFramePointerUsed);

        var totalFrameSize = genTotalFrameSize;
        var localFrameSize = _compiler.compLclFrameSize;
        if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
        {
            localFrameSize -= TARGET_POINTER_SIZE;
        }

        JITDUMP($"Frame type. #outsz={unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value)}; " +
            $"#framesz={totalFrameSize}; #calleeSaveRegsPushed:{_compiler.compCalleeRegsPushed}; " +
            $"localloc? {dspBool(_compiler.compLocallocUsed)}\n");

        var fpOffset = localFrameSize;
        var remainingStackSize = totalFrameSize;
        if (totalFrameSize <= 2040)
        {
            if (_compiler.compLocallocUsed)
            {
                var spToFpDelta = genSPtoFPdelta;
                Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, -spToFpDelta);
                _compiler.unwindSetFrameReg(REG_FPBASE, unchecked((uint)spToFpDelta));
            }
        }
        else
        {
            if (_compiler.compLocallocUsed)
            {
                var spToFpDelta = genSPtoFPdelta;
                if (Emitter.isValidSimm12(spToFpDelta))
                {
                    Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, -spToFpDelta);
                }
                else
                {
                    Emitter.emitIns_I_la(EA_PTRSIZE, REG_RA, spToFpDelta);
                    Emitter.emitIns_R_R_R(INS_sub_d, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, REG_RA);
                }
            }

            if ((localFrameSize + (_compiler.compCalleeRegsPushed << 3)) > 2040)
            {
                remainingStackSize = localFrameSize & -16;
                genStackPointerAdjustment(remainingStackSize, REG_RA, null, reportUnwindData: false);

                remainingStackSize = totalFrameSize - remainingStackSize;
                fpOffset = localFrameSize & 0xf;
            }

            _compiler.unwindSetFrameReg(REG_FPBASE, unchecked((uint)fpOffset));
        }

        JITDUMP($"    calleeSaveSPOffset={fpOffset + 16}\n");
        genRestoreCalleeSavedRegistersHelp(
            registersToRestoreMask,
            REG_SPBASE,
            fpOffset + 16,
            reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_ld_d, EA_PTRSIZE, REG_RA, REG_SPBASE, fpOffset + 8);
        _compiler.unwindSaveReg(REG_RA, unchecked((uint)(fpOffset + 8)));

        Emitter.emitIns_R_R_I(INS_ld_d, EA_PTRSIZE, REG_FP, REG_SPBASE, fpOffset);
        _compiler.unwindSaveReg(REG_FP, unchecked((uint)fpOffset));

        if (Emitter.isValidUimm11(remainingStackSize))
        {
            Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, remainingStackSize);
        }
        else
        {
            Emitter.emitIns_I_la(EA_PTRSIZE, REG_R21, remainingStackSize);
            Emitter.emitIns_R_R_R(INS_add_d, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, REG_R21);
        }

        _compiler.unwindAllocStack(unchecked((uint)remainingStackSize));

        if (_compiler.opts.IsOSR)
        {
            var tier0FrameSize = _compiler.info.compPatchpointInfo->TotalFrameSize;
            JITDUMP($"Extra SP adjust for OSR to pop off Tier0 frame: {tier0FrameSize} bytes\n");

            if (Emitter.isValidUimm11(tier0FrameSize))
            {
                Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, tier0FrameSize);
            }
            else
            {
                Emitter.emitIns_I_la(EA_PTRSIZE, REG_R21, tier0FrameSize);
                Emitter.emitIns_R_R_R(INS_add_d, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, REG_R21);
            }

            _compiler.unwindAllocStack(unchecked((uint)tier0FrameSize));
        }
    }
}
#endif
