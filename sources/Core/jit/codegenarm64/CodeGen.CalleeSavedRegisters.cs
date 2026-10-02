// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Collections.Generic;
using static RyuJitSharp.regMask;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genPushCalleeSavedRegistersArm64Core(regNumber initReg, ref bool initRegZeroed)
    {
        unchecked
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());

            // Probe before saving registers, using scratch rather than a possibly
            // callee-saved initReg whose incoming value has not yet been saved.
            var ignoreInitRegZeroed = false;
            genAllocLclFrame((uint)_compiler.compLclFrameSize, REG_SCRATCH, ref ignoreInitRegZeroed,
                _calleeRegArgMaskLiveIn);
            var pushRegs = _regSet.rsGetModifiedCalleeSavedRegsMask();
#if ETW_EBP_FRAMED
            if (!IsFramePointerUsed && _regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)))
            {
                noway_assert(false, "Used register RBM_FPBASE as a scratch register!");
            }
#endif
            if (IsFramePointerUsed)
            {
                pushRegs |= new regMaskTP(SRBM_FPBASE);
            }

            // LR must be saved even in leaf methods: partially interruptible GC
            // suspension uses stack return-address hijacking.
            pushRegs |= new regMaskTP(SRBM_LR);
            _regSet.rsSetCalleeSavedRegsMask(pushRegs);
#if DEBUG
            if (_compiler.compCalleeRegsPushed != genCalleeSaveCountArm64(pushRegs))
            {
                jitprintf($"Error: unexpected number of callee-saved registers to push. Expected: {_compiler.compCalleeRegsPushed}. " +
                    $"Got: {genCalleeSaveCountArm64(pushRegs)} ");
                dspRegMask(pushRegs);
                jitprintf("\n");
                assert(_compiler.compCalleeRegsPushed == genCalleeSaveCountArm64(pushRegs));
            }
#endif
            var totalFrameSize = genTotalFrameSize;
            int offset;
            var saveFloat = pushRegs & new regMaskTP(SRBM_ALLFLOAT);
            var saveInt = pushRegs & ~saveFloat;
#if DEBUG
            if (_verbose)
            {
                jitprintf("Save float regs: ");
                dspRegMask(saveFloat);
                jitprintf("\n");
                jitprintf("Save int   regs: ");
                dspRegMask(saveInt);
                jitprintf("\n");
            }
#endif
            if (JitConfig.JitPacEnabled != 0)
            {
                genEmitPacInPrologArm64Dependency();
            }

            var frameType = 0;
            var calleeSaveSpDelta = 0;
            if (IsFramePointerUsed)
            {
                assert((saveInt & new regMaskTP(SRBM_FP)).IsNonEmpty);
                assert((saveInt & new regMaskTP(SRBM_LR)).IsNonEmpty);
                if (_compiler.lvaOutgoingArgSpaceSize.Value == 0 && totalFrameSize <= 504 &&
                    !genSaveFpLrWithAllCalleeSavedRegisters)
                {
                    // STP can pre-index -512, but its matching epilog LDP can
                    // only post-index +504; both sides must use the same frame.
                    JITDUMP($"Frame type 1. #outsz=0; #framesz={totalFrameSize}; LclFrameSize={_compiler.compLclFrameSize}\n");
                    frameType = 1;
                    assert(totalFrameSize <= Arm64StackProbeBoundaryThresholdBytes);
                    Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                        -totalFrameSize, INS_OPTS_PRE_INDEX);
                    genUnwindSaveRegPairPreindexedArm64Dependency(REG_FP, REG_LR, -totalFrameSize);
                    saveInt &= ~new regMaskTP(SRBM_FP | SRBM_LR);
                    offset = _compiler.compLclFrameSize + 2 * REGSIZE_BYTES;
                }
                else if (totalFrameSize <= 512 && !_compiler.opts.compDbgEnC)
                {
                    if (genSaveFpLrWithAllCalleeSavedRegisters)
                    {
                        JITDUMP($"Frame type 4 (save FP/LR at top). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                            $"#framesz={totalFrameSize}; LclFrameSize={_compiler.compLclFrameSize}\n");
                        frameType = 4;
                        calleeSaveSpDelta = totalFrameSize;
                        offset = _compiler.compLclFrameSize;
                    }
                    else
                    {
                        JITDUMP($"Frame type 2 (save FP/LR at bottom). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                            $"#framesz={totalFrameSize}; LclFrameSize={_compiler.compLclFrameSize}\n");
                        frameType = 2;
                        assert((uint)totalFrameSize - (uint)_compiler.lvaOutgoingArgSpaceSize.Value <=
                            Arm64StackProbeBoundaryThresholdBytes);
                        Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, totalFrameSize);
                        _compiler.unwindAllocStack((uint)totalFrameSize);
                        assert((uint)_compiler.lvaOutgoingArgSpaceSize.Value + 2 * REGSIZE_BYTES <= (uint)totalFrameSize);
                        Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                            _compiler.lvaOutgoingArgSpaceSize.Value);
                        genUnwindSaveRegPairArm64Dependency(REG_FP, REG_LR, _compiler.lvaOutgoingArgSpaceSize.Value);
                        saveInt &= ~new regMaskTP(SRBM_FP | SRBM_LR);
                        offset = _compiler.compLclFrameSize + 2 * REGSIZE_BYTES;
                    }
                }
                else
                {
                    var unalignedDelta = totalFrameSize - _compiler.compLclFrameSize;
                    if (genSaveFpLrWithAllCalleeSavedRegisters)
                    {
                        JITDUMP($"Frame type 5 (save FP/LR at top). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                            $"#framesz={totalFrameSize}; LclFrameSize={_compiler.compLclFrameSize}\n");
                        frameType = 5;
                    }
                    else
                    {
                        assert(!_compiler.opts.compDbgEnC);
                        JITDUMP($"Frame type 3 (save FP/LR at bottom). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                            $"#framesz={totalFrameSize}; LclFrameSize={_compiler.compLclFrameSize}\n");
                        frameType = 3;
                        unalignedDelta -= 2 * REGSIZE_BYTES;
                        saveInt &= ~new regMaskTP(SRBM_FP | SRBM_LR);
                    }

                    assert(unalignedDelta >= 0);
                    assert(unalignedDelta % 8 == 0);
                    calleeSaveSpDelta = (int)roundUp((uint)unalignedDelta, STACK_ALIGN);
                    offset = calleeSaveSpDelta - unalignedDelta;
                    JITDUMP($"    calleeSaveSpDelta={calleeSaveSpDelta}, offset={offset}\n");
                    assert(offset == 0 || offset == REGSIZE_BYTES);
                }
            }
            else
            {
                assert((saveInt & new regMaskTP(SRBM_FP)).IsEmpty);
                assert((saveInt & new regMaskTP(SRBM_LR)).IsNonEmpty);
                NYI("Frame without frame pointer");
                offset = 0;
            }

            assert(frameType != 0);
            var calleeSaveSpOffset = offset;
            JITDUMP($"    offset={offset}, calleeSaveSpDelta={calleeSaveSpDelta}\n");
            genSaveCalleeSavedRegistersHelpArm64(saveInt | saveFloat, offset, -calleeSaveSpDelta);
            offset += genCalleeSaveCountArm64(saveInt | saveFloat) * REGSIZE_BYTES;

            if (_compiler.info.compIsVarArgs)
            {
                JITDUMP("    compIsVarArgs=true\n");
                assert(offset % 16 == 0);
                for (var reg1 = REG_ARG_FIRST; reg1 < REG_ARG_LAST; reg1 += 2)
                {
                    var reg2 = reg1 + 1;
                    Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, reg1, reg2, REG_SPBASE, offset);
                    _compiler.unwindNop();
                    offset += 2 * REGSIZE_BYTES;
                }
            }

            var establishFramePointer = true;
            var offsetSpToSavedFp = 0u;
            switch (frameType)
            {
                case 1:
                {
                    assert(!genSaveFpLrWithAllCalleeSavedRegisters);
                    assert(offsetSpToSavedFp == 0);
                    break;
                }

                case 2:
                {
                    assert(!genSaveFpLrWithAllCalleeSavedRegisters);
                    offsetSpToSavedFp = (uint)_compiler.lvaOutgoingArgSpaceSize.Value;
                    break;
                }

                case 3:
                {
                    assert(!genSaveFpLrWithAllCalleeSavedRegisters);
                    var remainingFrameSize = totalFrameSize - calleeSaveSpDelta;
                    assert(remainingFrameSize > 0);
                    assert(remainingFrameSize % 16 == 0);
                    if ((uint)_compiler.lvaOutgoingArgSpaceSize.Value > 504)
                    {
                        assert(remainingFrameSize > _compiler.lvaOutgoingArgSpaceSize.Value);
                        var unalignedAdjustment = remainingFrameSize - _compiler.lvaOutgoingArgSpaceSize.Value;
                        var adjustment2 = (int)roundUp((uint)unalignedAdjustment, STACK_ALIGN);
                        var alignmentAdjustment = adjustment2 - unalignedAdjustment;
                        assert(alignmentAdjustment == 0 || alignmentAdjustment == 8);
                        JITDUMP($"    spAdjustment2={adjustment2}\n");
                        fixed (bool* zeroed = &initRegZeroed)
                        {
                            genPrologSaveRegPairArm64(REG_FP, REG_LR, alignmentAdjustment, -adjustment2,
                                false, initReg, zeroed);
                        }
                        offset += adjustment2;
                        var adjustment3 = _compiler.lvaOutgoingArgSpaceSize.Value - alignmentAdjustment;
                        assert(adjustment3 > 0);
                        assert(adjustment3 % 16 == 0);
                        JITDUMP($"    alignmentAdjustment2={alignmentAdjustment}\n");
                        genEstablishFramePointer(alignmentAdjustment, reportUnwindData: true);
                        establishFramePointer = false;
                        JITDUMP($"    spAdjustment3={adjustment3}\n");
                        fixed (bool* zeroed = &initRegZeroed)
                        {
                            genStackPointerAdjustmentArm64(-adjustment3, initReg, zeroed, reportUnwindData: false);
                        }
                        offset += adjustment3;
                    }
                    else
                    {
                        fixed (bool* zeroed = &initRegZeroed)
                        {
                            genPrologSaveRegPairArm64(REG_FP, REG_LR, _compiler.lvaOutgoingArgSpaceSize.Value,
                                -remainingFrameSize, false, initReg, zeroed);
                        }
                        offset += remainingFrameSize;
                        offsetSpToSavedFp = (uint)_compiler.lvaOutgoingArgSpaceSize.Value;
                    }
                    break;
                }

                case 4:
                {
                    assert(genSaveFpLrWithAllCalleeSavedRegisters);
                    offsetSpToSavedFp = (uint)(calleeSaveSpDelta -
                        (_compiler.info.compIsVarArgs ? MAX_REG_ARG * REGSIZE_BYTES : 0) - 2 * REGSIZE_BYTES);
                    break;
                }

                case 5:
                {
                    assert(genSaveFpLrWithAllCalleeSavedRegisters);
                    offsetSpToSavedFp = (uint)(calleeSaveSpDelta -
                        (_compiler.info.compIsVarArgs ? MAX_REG_ARG * REGSIZE_BYTES : 0) - 2 * REGSIZE_BYTES);
                    JITDUMP($"    offsetSpToSavedFp={offsetSpToSavedFp}\n");
                    genEstablishFramePointer((int)offsetSpToSavedFp, reportUnwindData: true);
                    establishFramePointer = false;
                    var remainingFrameSize = totalFrameSize - calleeSaveSpDelta;
                    if (remainingFrameSize > 0)
                    {
                        assert(remainingFrameSize % 16 == 0);
                        JITDUMP($"    remainingFrameSz={remainingFrameSize}\n");
                        fixed (bool* zeroed = &initRegZeroed)
                        {
                            genStackPointerAdjustmentArm64(-remainingFrameSize, initReg, zeroed, reportUnwindData: false);
                        }
                        offset += remainingFrameSize;
                    }
                    else
                    {
                        assert(_compiler.opts.compDbgEnC);
                    }
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            if (establishFramePointer)
            {
                JITDUMP($"    offsetSpToSavedFp={offsetSpToSavedFp}\n");
                genEstablishFramePointer((int)offsetSpToSavedFp, reportUnwindData: true);
            }
            assert(offset == totalFrameSize);
            _compiler.compFrameInfo.frameType = frameType;
            _compiler.compFrameInfo.calleeSaveSpOffset = calleeSaveSpOffset;
            _compiler.compFrameInfo.calleeSaveSpDelta = calleeSaveSpDelta;
            _compiler.compFrameInfo.offsetSpToSavedFp = (int)offsetSpToSavedFp;
        }
    }

    public unsafe void genPopCalleeSavedRegistersAndFreeLclFrame(bool jmpEpilog)
    {
        genPopCalleeSavedRegistersArm64Core(jmpEpilog);
    }

    private unsafe void genPopCalleeSavedRegistersArm64Core(bool jmpEpilog = false)
    {
        unchecked
        {
            assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());
            var restoreRegs = _regSet.rsGetModifiedCalleeSavedRegsMask();
            if (IsFramePointerUsed)
            {
                restoreRegs |= new regMaskTP(SRBM_FPBASE);
            }
            restoreRegs |= new regMaskTP(SRBM_LR);
            var regsToRestore = restoreRegs;
            var totalFrameSize = genTotalFrameSize;
            var frameType = _compiler.compFrameInfo.frameType;
            var calleeSaveSpOffset = _compiler.compFrameInfo.calleeSaveSpOffset;
            var calleeSaveSpDelta = _compiler.compFrameInfo.calleeSaveSpDelta;
            var offsetSpToSavedFp = _compiler.compFrameInfo.offsetSpToSavedFp;

            switch (frameType)
            {
                case 1:
                {
                    JITDUMP($"Frame type 1. #outsz=0; #framesz={totalFrameSize}; localloc? {dspBool(_compiler.compLocallocUsed)}\n");
                    if (_compiler.compLocallocUsed)
                    {
                        inst_Mov(TYP_I_IMPL, REG_SPBASE, REG_FPBASE, canSkip: false);
                        _compiler.unwindSetFrameReg(REG_FPBASE, 0);
                    }
                    regsToRestore &= ~new regMaskTP(SRBM_FP | SRBM_LR);
                    break;
                }

                case 2:
                {
                    JITDUMP($"Frame type 2 (save FP/LR at bottom). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                        $"#framesz={totalFrameSize}; localloc? {dspBool(_compiler.compLocallocUsed)}\n");
                    assert(!genSaveFpLrWithAllCalleeSavedRegisters);
                    if (_compiler.compLocallocUsed)
                    {
                        var delta = genSPtoFPdelta;
                        Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, delta);
                        _compiler.unwindSetFrameReg(REG_FPBASE, (uint)delta);
                    }
                    regsToRestore &= ~new regMaskTP(SRBM_FP | SRBM_LR);
                    break;
                }

                case 3:
                {
                    JITDUMP($"Frame type 3 (save FP/LR at bottom). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                        $"#framesz={totalFrameSize}; localloc? {dspBool(_compiler.compLocallocUsed)}\n");
                    assert(!genSaveFpLrWithAllCalleeSavedRegisters);
                    JITDUMP($"    calleeSaveSpDelta={calleeSaveSpDelta}\n");
                    regsToRestore &= ~new regMaskTP(SRBM_FP | SRBM_LR);
                    var remainingFrameSize = totalFrameSize - calleeSaveSpDelta;
                    assert(remainingFrameSize > 0);
                    if ((uint)_compiler.lvaOutgoingArgSpaceSize.Value > 504)
                    {
                        assert(remainingFrameSize > _compiler.lvaOutgoingArgSpaceSize.Value);
                        var unalignedAdjustment = remainingFrameSize - _compiler.lvaOutgoingArgSpaceSize.Value;
                        var adjustment2 = (int)roundUp((uint)unalignedAdjustment, STACK_ALIGN);
                        var alignmentAdjustment = adjustment2 - unalignedAdjustment;
                        assert(alignmentAdjustment == 0 || alignmentAdjustment == REGSIZE_BYTES);
                        Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, alignmentAdjustment);
                        _compiler.unwindSetFrameReg(REG_FPBASE, (uint)alignmentAdjustment);
                        JITDUMP($"    alignmentAdjustment2={alignmentAdjustment}\n");
                        genRestoreRegPairArm64(REG_FP, REG_LR, REG_SPBASE, alignmentAdjustment,
                            adjustment2, false, REG_IP1, null, reportUnwindData: true);
                    }
                    else
                    {
                        if (_compiler.compLocallocUsed)
                        {
                            var delta = genSPtoFPdelta;
                            assert(delta == _compiler.lvaOutgoingArgSpaceSize.Value);
                            Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, delta);
                            _compiler.unwindSetFrameReg(REG_FPBASE, (uint)delta);
                        }
                        JITDUMP($"    remainingFrameSz={remainingFrameSize}\n");
                        genRestoreRegPairArm64(REG_FP, REG_LR, REG_SPBASE, _compiler.lvaOutgoingArgSpaceSize.Value,
                            remainingFrameSize, false, REG_IP1, null, reportUnwindData: true);
                    }
                    assert(calleeSaveSpOffset == 0 || calleeSaveSpOffset == REGSIZE_BYTES);
                    break;
                }

                case 4:
                {
                    JITDUMP($"Frame type 4 (save FP/LR at top). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                        $"#framesz={totalFrameSize}; localloc? {dspBool(_compiler.compLocallocUsed)}\n");
                    assert(genSaveFpLrWithAllCalleeSavedRegisters);
                    if (_compiler.compLocallocUsed)
                    {
                        var delta = genSPtoFPdelta;
                        Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, delta);
                        _compiler.unwindSetFrameReg(REG_FPBASE, (uint)delta);
                    }
                    break;
                }

                case 5:
                {
                    JITDUMP($"Frame type 5 (save FP/LR at top). #outsz={(uint)_compiler.lvaOutgoingArgSpaceSize.Value}; " +
                        $"#framesz={totalFrameSize}; localloc? {dspBool(_compiler.compLocallocUsed)}\n");
                    assert(genSaveFpLrWithAllCalleeSavedRegisters);
                    assert(calleeSaveSpOffset == 0 || calleeSaveSpOffset == REGSIZE_BYTES);
                    Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, offsetSpToSavedFp);
                    _compiler.unwindSetFrameReg(REG_FPBASE, (uint)offsetSpToSavedFp);
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            JITDUMP($"    calleeSaveSpOffset={calleeSaveSpOffset}, calleeSaveSpDelta={calleeSaveSpDelta}\n");
            genRestoreCalleeSavedRegistersHelpArm64(regsToRestore, calleeSaveSpOffset, calleeSaveSpDelta);
            switch (frameType)
            {
                case 1:
                {
                    Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                        totalFrameSize, INS_OPTS_POST_INDEX);
                    genUnwindSaveRegPairPreindexedArm64Dependency(REG_FP, REG_LR, -totalFrameSize);
                    break;
                }

                case 2:
                {
                    Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, REG_FP, REG_LR, REG_SPBASE,
                        _compiler.lvaOutgoingArgSpaceSize.Value);
                    genUnwindSaveRegPairArm64Dependency(REG_FP, REG_LR, _compiler.lvaOutgoingArgSpaceSize.Value);
                    Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, totalFrameSize);
                    _compiler.unwindAllocStack((uint)totalFrameSize);
                    break;
                }

                case 3:
                case 4:
                case 5:
                {
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
            if (JitConfig.JitPacEnabled != 0)
            {
                genEmitPacInEpilogArm64Dependency();
            }
            if (_compiler.opts.IsOSR)
            {
                var patchpoint = _compiler.info.compPatchpointInfo;
                var tier0FrameSize = patchpoint->TotalFrameSize;
                JITDUMP($"Extra SP adjust for OSR to pop off Tier0 frame: {tier0FrameSize} bytes\n");
                var adjustment = tier0FrameSize;
                if (!Emitter.emitIns_valid_imm_for_add(tier0FrameSize, EA_PTRSIZE))
                {
                    var lowPart = adjustment & 0xFFF;
                    var highPart = adjustment - lowPart;
                    assert(Emitter.emitIns_valid_imm_for_add(highPart, EA_PTRSIZE));
                    Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, highPart);
                    _compiler.unwindAllocStack((uint)highPart);
                    adjustment = lowPart;
                }
                assert(Emitter.emitIns_valid_imm_for_add(adjustment, EA_PTRSIZE));
                Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, adjustment);
                _compiler.unwindAllocStack((uint)adjustment);
            }
        }
    }

    private unsafe void genStackPointerAdjustmentArm64(nint delta, regNumber tmpReg, bool* tmpRegIsZero,
        bool reportUnwindData)
    {
        var usedTemp = !genInstrWithConstant(INS_add, EA_PTRSIZE, REG_SPBASE, REG_SPBASE, delta, tmpReg, true);
        if (usedTemp && tmpRegIsZero != null)
        {
            *tmpRegIsZero = false;
        }
        if (reportUnwindData)
        {
            var absoluteDelta = delta < 0 ? unchecked(-delta) : delta;
            var unwindDelta = unchecked((uint)absoluteDelta);
            assert((nint)unwindDelta == absoluteDelta);
            _compiler.unwindAllocStack(unwindDelta);
        }
    }

    private unsafe void genPrologSaveRegPairArm64(regNumber reg1, regNumber reg2, int offset, int delta,
        bool useSaveNextPair, regNumber tmpReg, bool* tmpRegIsZero)
    {
        assert(offset >= 0);
        assert(delta <= 0);
        assert(delta % 16 == 0);
        assert(genIsValidFloatReg(reg1) == genIsValidFloatReg(reg2));
        var saveRegs = true;
        if (delta != 0)
        {
            assert(!useSaveNextPair);
            if (offset == 0 && delta >= -512)
            {
                assert(reg1 != REG_LR);
                Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, reg1, reg2, REG_SPBASE, delta, INS_OPTS_PRE_INDEX);
                genUnwindSaveRegPairPreindexedArm64Dependency(reg1, reg2, delta);
                saveRegs = false;
            }
            else
            {
                genStackPointerAdjustmentArm64(delta, tmpReg, tmpRegIsZero, reportUnwindData: true);
            }
        }
        if (saveRegs)
        {
            assert(offset <= 504);
            assert(offset % 8 == 0);
            assert(reg1 != REG_LR);
            Emitter.emitIns_R_R_R_I(INS_stp, EA_PTRSIZE, reg1, reg2, REG_SPBASE, offset);
            if (TargetOS.IsUnix && _compiler.generateCFIUnwindCodes())
            {
                useSaveNextPair = false;
            }
            if (useSaveNextPair)
            {
                genUnwindSaveNextArm64Dependency();
            }
            else
            {
                genUnwindSaveRegPairArm64Dependency(reg1, reg2, offset);
            }
        }
    }

    private unsafe void genPrologSaveRegArm64(regNumber reg, int offset, int delta, regNumber tmpReg,
        bool* tmpRegIsZero)
    {
        assert(offset >= 0);
        assert(delta <= 0);
        assert(delta % 16 == 0);
        var saveRegs = true;
        if (delta != 0)
        {
            if (offset == 0 && delta >= -256)
            {
                Emitter.emitIns_R_R_I(INS_str, EA_PTRSIZE, reg, REG_SPBASE, delta, INS_OPTS_PRE_INDEX);
                genUnwindSaveRegPreindexedArm64Dependency(reg, delta);
                saveRegs = false;
            }
            else
            {
                genStackPointerAdjustmentArm64(delta, tmpReg, tmpRegIsZero, reportUnwindData: true);
            }
        }
        if (saveRegs)
        {
            Emitter.emitIns_R_R_I(INS_str, EA_PTRSIZE, reg, REG_SPBASE, offset);
            _compiler.unwindSaveReg(reg, unchecked((uint)offset));
        }
    }

    private unsafe void genRestoreRegPairArm64(regNumber reg1, regNumber reg2, regNumber baseReg,
        int offset, int delta, bool useSaveNextPair, regNumber tmpReg, bool* tmpRegIsZero, bool reportUnwindData)
    {
        assert(offset >= -512 && offset <= 504);
        assert(delta >= 0);
        assert(delta % 16 == 0);
        assert(genIsValidFloatReg(reg1) == genIsValidFloatReg(reg2));
        assert(reg1 != REG_LR);
        if (delta != 0)
        {
            assert(!useSaveNextPair);
            if (offset == 0 && delta <= 504)
            {
                Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, reg1, reg2, baseReg, delta, INS_OPTS_POST_INDEX);
                if (reportUnwindData)
                {
                    genUnwindSaveRegPairPreindexedArm64Dependency(reg1, reg2, unchecked(-delta));
                }
            }
            else
            {
                Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, reg1, reg2, baseReg, offset);
                if (reportUnwindData)
                {
                    genUnwindSaveRegPairArm64Dependency(reg1, reg2, offset);
                }
                genStackPointerAdjustmentArm64(delta, tmpReg, tmpRegIsZero, reportUnwindData);
            }
        }
        else
        {
            Emitter.emitIns_R_R_R_I(INS_ldp, EA_PTRSIZE, reg1, reg2, baseReg, offset);
            if (reportUnwindData)
            {
                if (TargetOS.IsUnix && _compiler.generateCFIUnwindCodes())
                {
                    useSaveNextPair = false;
                }
                if (useSaveNextPair)
                {
                    genUnwindSaveNextArm64Dependency();
                }
                else
                {
                    genUnwindSaveRegPairArm64Dependency(reg1, reg2, offset);
                }
            }
        }
    }

    private unsafe void genRestoreRegArm64(regNumber reg, regNumber baseReg, int offset, int delta,
        regNumber tmpReg, bool* tmpRegIsZero, bool reportUnwindData)
    {
        assert(delta >= 0);
        assert(delta % 16 == 0);
        if (delta != 0)
        {
            if (offset == 0 && delta <= 255)
            {
                Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, reg, baseReg, delta, INS_OPTS_POST_INDEX);
                if (reportUnwindData)
                {
                    genUnwindSaveRegPreindexedArm64Dependency(reg, unchecked(-delta));
                }
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, reg, baseReg, offset);
                if (reportUnwindData)
                {
                    _compiler.unwindSaveReg(reg, unchecked((uint)offset));
                }
                genStackPointerAdjustmentArm64(delta, tmpReg, tmpRegIsZero, reportUnwindData);
            }
        }
        else
        {
            Emitter.emitIns_R_R_I(INS_ldr, EA_PTRSIZE, reg, baseReg, offset);
            if (reportUnwindData)
            {
                _compiler.unwindSaveReg(reg, unchecked((uint)offset));
            }
        }
    }

    private static int genCalleeSaveCountArm64(regMaskTP mask)
    {
        return (int)PopCount(mask);
    }

    private static void genBuildRegPairsStackArm64(regMaskTP mask,
        List<(regNumber First, regNumber Second, bool UseSaveNextPair)> stack)
    {
        assert(stack is not null);
        assert(stack.Count == 0);
        var count = genCalleeSaveCountArm64(mask);
        while (mask.IsNonEmpty)
        {
            var first = genRegNumFromMaskArm64(genFindLowestBitArm64(mask));
            regMaskTP.RemoveRegNumFromMask(ref mask, first);
            count--;
            var pair = false;
            if (count > 0)
            {
                var second = genRegNumFromMaskArm64(genFindLowestBitArm64(mask));

                // FP/LR use their dedicated unwind pair, so R28 cannot pair with FP.
                if (second == first + 1 && first != REG_R28 &&
                    genIsValidFloatReg(first) == genIsValidFloatReg(second))
                {
                    pair = true;
                    regMaskTP.RemoveRegNumFromMask(ref mask, second);
                    count--;
                    stack.Add((first, second, false));
                }
            }
            if (!pair)
            {
                stack.Add((first, REG_NA, false));
            }
        }
        assert(count == 0 && mask.IsEmpty);
        genSetUseSaveNextPairsArm64(stack);
    }

    private static void genSetUseSaveNextPairsArm64(List<(regNumber First, regNumber Second, bool UseSaveNextPair)> stack)
    {
        for (var index = 1; index < stack.Count; index++)
        {
            var (currentFirst, currentSecond, _) = stack[index];
            var (_, previousSecond, _) = stack[index - 1];
            if (previousSecond == REG_NA || currentSecond == REG_NA)
            {
                continue;
            }
            if (previousSecond + 1 != currentFirst)
            {
                continue;
            }
            if (genIsValidFloatReg(previousSecond) != genIsValidFloatReg(currentFirst))
            {
                continue;
            }
            stack[index] = (currentFirst, currentSecond, true);
        }
    }

    private static int genGetSlotSizeForRegsInMaskArm64(regMaskTP mask)
    {
        assert((mask & new regMaskTP(SRBM_CALLEE_SAVED | SRBM_FP | SRBM_LR)) == mask);
        assert(REGSIZE_BYTES == FPSAVE_REGSIZE_BYTES);

        return REGSIZE_BYTES;
    }

    private unsafe void genSaveCalleeSavedRegisterGroupArm64(regMaskTP mask, int delta, int offset)
    {
        unchecked
        {
            var slotSize = genGetSlotSizeForRegsInMaskArm64(mask);
            List<(regNumber First, regNumber Second, bool UseSaveNextPair)> stack = [];
            genBuildRegPairsStackArm64(mask, stack);
            for (var index = 0; index < stack.Count; index++)
            {
                var (first, second, useSaveNextPair) =
                    genReverseAndPairCalleeSavedRegisters ? stack[stack.Count - 1 - index] : stack[index];
                if (second != REG_NA)
                {
                    if (genReverseAndPairCalleeSavedRegisters)
                    {
                        genPrologSaveRegPairArm64(second, first, offset, delta, false, REG_IP0, null);
                    }
                    else
                    {
                        genPrologSaveRegPairArm64(first, second, offset, delta, useSaveNextPair, REG_IP0, null);
                    }
                    offset += 2 * slotSize;
                }
                else
                {
                    genPrologSaveRegArm64(first, offset, delta, REG_IP0, null);
                    offset += slotSize;
                }
                delta = 0;
            }
        }
    }

    private unsafe void genSaveCalleeSavedRegistersHelpArm64(regMaskTP mask, int offset, int delta)
    {
        unchecked
        {
            assert(delta <= 0);
            assert(-delta <= Arm64StackProbeBoundaryThresholdBytes);
            var count = genCalleeSaveCountArm64(mask);
            if (count == 0)
            {
                if (delta != 0)
                {
                    genStackPointerAdjustmentArm64(delta, REG_NA, null, reportUnwindData: true);
                }

                return;
            }
            assert(delta % 16 == 0);
            assert(count <= genCalleeSaveCountArm64(new regMaskTP(SRBM_CALLEE_SAVED | SRBM_FP | SRBM_LR)));
            var frame = mask & new regMaskTP(SRBM_FP | SRBM_LR);
            var floats = mask & new regMaskTP(SRBM_ALLFLOAT);
            var integers = mask & ~floats & ~frame;
            if (floats.IsNonEmpty)
            {
                genSaveCalleeSavedRegisterGroupArm64(floats, delta, offset);
                delta = 0;
                offset += genCalleeSaveCountArm64(floats) * FPSAVE_REGSIZE_BYTES;
            }
            if (integers.IsNonEmpty)
            {
                genSaveCalleeSavedRegisterGroupArm64(integers, delta, offset);
                delta = 0;
                offset += genCalleeSaveCountArm64(integers) * FPSAVE_REGSIZE_BYTES;
            }
            if (frame.IsNonEmpty)
            {
                genPrologSaveRegPairArm64(REG_FP, REG_LR, offset, delta, false, REG_IP0, null);
            }
        }
    }

    private unsafe void genRestoreCalleeSavedRegisterGroupArm64(regMaskTP mask, regNumber baseReg,
        int delta, int offset, bool reportUnwindData)
    {
        unchecked
        {
            var slotSize = genGetSlotSizeForRegsInMaskArm64(mask);
            List<(regNumber First, regNumber Second, bool UseSaveNextPair)> stack = [];
            genBuildRegPairsStackArm64(mask, stack);
            var stackDelta = 0;
            for (var index = 0; index < stack.Count; index++)
            {
                if (index == stack.Count - 1 && delta != 0)
                {
                    assert(stackDelta == 0);
                    stackDelta = delta;
                }
                var (first, second, useSaveNextPair) =
                    genReverseAndPairCalleeSavedRegisters ? stack[index] : stack[stack.Count - 1 - index];
                if (second != REG_NA)
                {
                    offset -= 2 * slotSize;
                    if (genReverseAndPairCalleeSavedRegisters)
                    {
                        genRestoreRegPairArm64(second, first, baseReg, offset, stackDelta,
                            false, REG_IP1, null, reportUnwindData);
                    }
                    else
                    {
                        genRestoreRegPairArm64(first, second, baseReg, offset, stackDelta,
                            useSaveNextPair, REG_IP1, null, reportUnwindData);
                    }
                }
                else
                {
                    offset -= slotSize;
                    genRestoreRegArm64(first, baseReg, offset, stackDelta, REG_IP1, null, reportUnwindData);
                }
            }
        }
    }

    private unsafe void genRestoreCalleeSavedRegistersHelpArm64(regMaskTP mask, int offset, int delta)
    {
        unchecked
        {
            assert(delta >= 0);
            var count = genCalleeSaveCountArm64(mask);
            if (count == 0)
            {
                if (delta != 0)
                {
                    genStackPointerAdjustmentArm64(delta, REG_NA, null, reportUnwindData: true);
                }

                return;
            }
            assert(delta % 16 == 0);
            assert(count <= genCalleeSaveCountArm64(new regMaskTP(SRBM_CALLEE_SAVED | SRBM_FP | SRBM_LR)));
            assert(REGSIZE_BYTES == FPSAVE_REGSIZE_BYTES);
            var topOffset = offset + count * REGSIZE_BYTES;
            var frame = mask & new regMaskTP(SRBM_FP | SRBM_LR);
            var floats = mask & new regMaskTP(SRBM_ALLFLOAT);
            var integers = mask & ~floats & ~frame;
            if (frame.IsNonEmpty)
            {
                var frameDelta = floats.IsNonEmpty || integers.IsNonEmpty ? 0 : delta;
                topOffset -= 2 * REGSIZE_BYTES;
                genRestoreRegPairArm64(REG_FP, REG_LR, REG_SPBASE, topOffset, frameDelta,
                    false, REG_IP1, null, reportUnwindData: true);
            }
            if (integers.IsNonEmpty)
            {
                var intDelta = floats.IsNonEmpty ? 0 : delta;
                genRestoreCalleeSavedRegisterGroupArm64(integers, REG_SPBASE, intDelta, topOffset, reportUnwindData: true);
                topOffset -= genCalleeSaveCountArm64(integers) * REGSIZE_BYTES;
            }
            if (floats.IsNonEmpty)
            {
                genRestoreCalleeSavedRegisterGroupArm64(floats, REG_SPBASE, delta, topOffset, reportUnwindData: true);
            }
        }
    }

    private static void genUnwindSaveRegPairPreindexedArm64Dependency(regNumber reg1, regNumber reg2, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind pre-indexed register-pair recording is not ported.");
    }

    private static void genUnwindSaveRegPairArm64Dependency(regNumber reg1, regNumber reg2, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind register-pair recording is not ported.");
    }

    private static void genUnwindSaveRegPreindexedArm64Dependency(regNumber reg, int offset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind pre-indexed register recording is not ported.");
    }

    private static void genUnwindSaveNextArm64Dependency()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 unwind save-next recording is not ported.");
    }

    private static void genEmitPacInPrologArm64Dependency()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 prolog pointer authentication is not ported.");
    }

    private static void genEmitPacInEpilogArm64Dependency()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 epilog pointer authentication is not ported.");
    }
}
#endif
