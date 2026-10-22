// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genGeneratePrologsAndEpilogs()
    {
        if (Configuration.UNIX_AMD64_ABI)
        {
            RequireSupportedRootPrologAbi();
        }

        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** Before prolog / epilog generation\n");
            Emitter.emitDispIGlist(displayInstructions: false);
        }
        assert(_compiler.RegisterAllocator is not null);
        assert(_compiler.fgFirstBB is not null);
        _compiler.RegisterAllocator.recordVarLocationsAtStartOfBB(_compiler.fgFirstBB);
        Emitter.emitStartPrologEpilogGeneration();
        _compiler.compCurBB = _compiler.fgFirstBB;
        GCInfo.gcResetForBB();
        genFnProlog();
        genCaptureFuncletPrologEpilogInfo();
        Emitter.emitGeneratePrologEpilog();
        Emitter.emitFinishPrologEpilogGeneration();
        _compiler.compCurBB = null;
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** After prolog / epilog generation\n");
            Emitter.emitDispIGlist(displayInstructions: false);
        }
    }

    public void genFnProlog()
    {
        if (Configuration.UNIX_AMD64_ABI)
        {
            RequireSupportedRootPrologAbi();
        }

        _compiler.funSetCurrentFunc(0);
        JITDUMP("*************** In genFnProlog()\n");
        if (Configuration.DEBUG)
        {
            _genInterruptibleUsed = true;
        }
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
        Emitter.emitBegProlog();
        _compiler.unwindBegProlog();
        genIPmappingAddToFront(IPmappingDscKind.Prolog, default, true);
        if (Configuration.DEBUG && _compiler.opts.dspCode)
        {
            jitprintf("\n__prolog:\n");
        }
        if (_compiler.opts.compScopeInfo && (_compiler.info.compVarScopesCount > 0))
        {
            psiBegProlog();
        }
        genBeginFnProlog();

        if (Configuration.DEBUG && _compiler.compJitHaltMethod())
        {
            // Debuggers may replace the first instruction with INT3.
            instGen(INS_nop);
            instGen(INS_BREAKPOINT);
            if (Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                _compiler.unwindPadding();
            }
        }
        var untrLclLo = int.MaxValue;
        var untrLclHi = -int.MaxValue;
        var hasUntrLcl = false;
        var gcRefLo = int.MaxValue;
        var gcRefHi = -int.MaxValue;
        var hasGCRef = false;
        var initRegs = RBM_NONE;
        var initFltRegs = RBM_NONE;
        var initDblRegs = RBM_NONE;

        if (Configuration.TARGET_WASM)
        {
            for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
            {
                ref var local = ref _compiler.lvaGetDesc(varNum);
                if (local.lvIsParam && !local.lvIsRegArg)
                {
                    continue;
                }
                if (!local.lvOnFrame || !local.lvMustInit)
                {
                    continue;
                }
                if (_compiler.lvaIsUnknownSizeLocal(varNum))
                {
                    continue;
                }
                // The frame-header slots hold funclet state written by frame allocation.
                if ((varNum == _compiler.lvaWasmFunctionIndex) || (varNum == _compiler.lvaWasmVirtualIP)
                    || (varNum == _compiler.lvaWasmResumeIP))
                {
                    continue;
                }

                var loOffs = local.StackOffset;
                var hiOffs = unchecked(loOffs + _compiler.lvaLclStackHomeSize(varNum));
                hasUntrLcl = true;
                if (loOffs < untrLclLo)
                {
                    untrLclLo = loOffs;
                }
                if (hiOffs > untrLclHi)
                {
                    untrLclHi = hiOffs;
                }
            }

            assert(_regSet.tmpGetAllFree());
            if (Configuration.DEBUG)
            {
                assert(_regSet.tmpListBeg() is null);
            }
        }
        else
        {
            for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
            {
                ref var local = ref _compiler.lvaGetDesc(varNum);
                if (local.lvIsParam && !local.lvIsRegArg)
                {
                    continue;
                }
                if (!local.lvIsInReg && !local.lvOnFrame)
                {
                    noway_assert(local.lvRefCnt() == 0);
                    continue;
                }
                if (_compiler.lvaLocalIsOnUnknownSizeFrame(varNum))
                {
                    continue;
                }

                var loOffs = local.StackOffset;
                var hiOffs = unchecked(loOffs + _compiler.lvaLclStackHomeSize(varNum));
                if (local.HasGCPtr && local.lvTrackedNonStruct && local.lvOnFrame
                    && !_compiler.lvaIsFieldOfDependentlyPromotedStruct(in local))
                {
                    hasGCRef = true;
                    if (loOffs < gcRefLo)
                    {
                        gcRefLo = loOffs;
                    }
                    if (hiOffs > gcRefHi)
                    {
                        gcRefHi = hiOffs;
                    }
                }
                if (!local.lvMustInit)
                {
                    continue;
                }

                var isInReg = local.lvIsInReg;
                var isInMemory = !isInReg || local.IsLiveInOutOfHandler;
                // Handler-live locals may have a register assigned without being live at entry.
                if (isInReg)
                {
                    assert(_compiler.fgFirstBB is not null);
                    if (_compiler.lvaEnregEHVars && local.IsLiveInOutOfHandler)
                    {
                        isInReg = VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, local._varIndex);
                    }
                    else
                    {
                        assert(VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, local._varIndex));
                    }
                }
                if (isInReg)
                {
                    var reg = local.RegNum;
                    var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
                    if (!genIsValidFloatReg(reg))
                    {
                        initRegs |= mask;
                        if (varTypeIsMultiReg(local.Type))
                        {
                            if (local.OtherReg != REG_STK)
                            {
                                initRegs |= regMaskTP.CreateFromRegNum(local.OtherReg, local.OtherReg.SingleTypeMask);
                            }
                            else
                            {
                                loOffs += sizeof(int);
                                isInMemory = true;
                            }
                        }
                    }
                    else if (local.Type == TYP_DOUBLE)
                    {
                        initDblRegs |= mask;
                    }
                    else
                    {
                        initFltRegs |= mask;
                    }
                }
                if (isInMemory)
                {
                    hasUntrLcl = true;
                    if (loOffs < untrLclLo)
                    {
                        untrLclLo = loOffs;
                    }
                    if (hiOffs > untrLclHi)
                    {
                        untrLclHi = hiOffs;
                    }
                }
            }

            assert(_regSet.tmpGetAllFree());
            for (var temp = _regSet.tmpListBeg(); temp is not null; temp = _regSet.tmpListNxt(temp))
            {
                if (!varTypeIsGC(temp.tdTempType))
                {
                    continue;
                }

                var loOffs = temp.tdTempOffs;
                var hiOffs = unchecked(loOffs + Configuration.TARGET_POINTER_SIZE);
                if (!Configuration.TARGET_AMD64)
                {
                    noway_assert(!IsFramePointerUsed || (loOffs != 0));
                }
                hasUntrLcl = true;
                if (loOffs < untrLclLo)
                {
                    untrLclLo = loOffs;
                }
                if (hiOffs > untrLclHi)
                {
                    untrLclHi = hiOffs;
                }
            }
        }
        assert(_compiler.opts.IsOSR || ((InitStkLclCnt > 0) == hasUntrLcl));
        if (InitStkLclCnt > 0)
        {
            JITDUMP($"Found {InitStkLclCnt} lvMustInit int-sized stack slots, frame offsets {unchecked(-untrLclLo)} through {unchecked(-untrLclHi)}\n");
        }

        var initReg = REG_NA;
        var initRegZeroed = false;
        var inheritsCalleeSaves = false;
        uint extraFrameSize = 0;

        if (!Configuration.TARGET_WASM)
        {
            if (Configuration.TARGET_ARM)
            {
                _calleeRegArgMaskLiveIn &= ~genPrespilledUnmappedRegs();
            }

            initReg = TargetRuntimeMetadata.ScratchRegister;
            var excludeMask = _calleeRegArgMaskLiveIn;
            if (Configuration.TARGET_AMD64 && !_compiler.canUseEvexEncoding())
            {
                excludeMask |= new regMaskTP(
                    SRBM_X64_R16 | SRBM_X64_R17 | SRBM_X64_R18 | SRBM_X64_R19 |
                    SRBM_X64_R20 | SRBM_X64_R21 | SRBM_X64_R22 | SRBM_X64_R23 |
                    SRBM_X64_R24 | SRBM_X64_R25 | SRBM_X64_R26 | SRBM_X64_R27 |
                    SRBM_X64_R28 | SRBM_X64_R29 | SRBM_X64_R30 | SRBM_X64_R31);
            }
            if (Configuration.TARGET_ARM && _compiler.compLocallocUsed)
            {
                excludeMask |= new regMaskTP(SRBM_ARM32_R9);
            }

            var isRoot = _compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT;
            inheritsCalleeSaves = isRoot && _compiler.opts.IsOSR;
            var allInt = new regMaskTP(SRBM_ALLINT);
            var tempMask = initRegs & allInt & ~excludeMask & ~_regSet.rsMaskResvd;
            if (tempMask.IsNonEmpty)
            {
                initReg = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)tempMask.IntRegSet));
            }
            else
            {
                tempMask = _regSet.rsGetModifiedRegsMask() & allInt & ~excludeMask & ~_regSet.rsMaskResvd;
                if (tempMask.IsNonEmpty)
                {
                    initReg = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)tempMask.IntRegSet));
                }
            }
            if (inheritsCalleeSaves)
            {
                // Deferred callee saves cannot be used as scratch registers yet.
                initReg = TargetRuntimeMetadata.ScratchRegister;
                if (Configuration.TARGET_ARM64)
                {
                    initReg = REG_ARM64_IP1;
                }
            }
            if (Configuration.TARGET_AMD64 && _compiler.info.compIsVarArgs && !_compiler.opts.IsOSR)
            {
                Emitter.spillIntArgRegsToShadowSlots();
            }
            if (Configuration.TARGET_ARM)
            {
                var preSpillMask = _regSet.rsMaskPreSpillRegs(true);
                if (preSpillMask.IsNonEmpty)
                {
                    inst_IV(INS_push, unchecked((int)preSpillMask.Lower));
                    _compiler.unwindPushMaskInt(preSpillMask);
                }
            }
        }

        if (inheritsCalleeSaves)
        {
            genOSRHandleTier0CalleeSavedRegistersAndFrame();
            if (Configuration.TARGET_AMD64)
            {
                extraFrameSize = unchecked((uint)(_compiler.compCalleeRegsPushed * Configuration.REGSIZE_BYTES));
                // Reproduce the return-address misalignment of an ordinary method entry.
                Emitter.emitIns_R(INS_push, EA_PTRSIZE, REG_X64_RAX);
                _compiler.unwindAllocStack((uint)Configuration.REGSIZE_BYTES);
            }
        }

        if (Configuration.TARGET_XARCH)
        {
            if (doubleAlignOrFramePointerUsed())
            {
                if (inheritsCalleeSaves && IsFramePointerUsed)
                {
                    Emitter.emitIns_R_AR(INS_mov, EA_8BYTE, initReg, TargetRuntimeMetadata.FramePointerBaseRegister, 0);
                    inst_RV(INS_push, initReg, TYP_REF);
                    initRegZeroed = false;
                    _compiler.unwindAllocStack((uint)Configuration.REGSIZE_BYTES);
                }
                else
                {
                    inst_RV(INS_push, TargetRuntimeMetadata.FramePointerBaseRegister, TYP_REF);
                    _compiler.unwindPush(TargetRuntimeMetadata.FramePointerBaseRegister);
                }
                if (Configuration.TARGET_X86)
                {
                    genEstablishFramePointer(0, reportUnwindData: true);
                }
                if (Configuration.DOUBLE_ALIGN && _compiler.genDoubleAlign)
                {
                    noway_assert(!IsFramePointerUsed);
                    var fpBaseMask = Configuration.TARGET_X86 ? SRBM_X86_EBP : SRBM_X64_RBP;
                    noway_assert(!_regSet.rsRegsModified(new regMaskTP(fpBaseMask)));
                    inst_RV_IV(INS_and, TargetRuntimeMetadata.StackPointerBaseRegister, -8, EA_PTRSIZE);
                }
            }
        }
        var pushesCalleeSaves = true;
        if (Configuration.TARGET_AMD64)
        {
            pushesCalleeSaves = !inheritsCalleeSaves;
        }
        if (pushesCalleeSaves)
        {
            genPushCalleeSavedRegisters(initReg, ref initRegZeroed);
        }

        var needToEstablishFP = false;
        var afterFrameSPtoFPdelta = 0;
        if (Configuration.TARGET_ARM && doubleAlignOrFramePointerUsed())
        {
            needToEstablishFP = true;
            // Defer small-frame FP establishment past the OS-reported prolog to reduce unwind data.
            var spToFPdelta = unchecked((_compiler.compCalleeRegsPushed - 2) * Configuration.REGSIZE_BYTES);
            afterFrameSPtoFPdelta = unchecked(spToFPdelta + _compiler.compLclFrameSize);
            if (!Emitter.emitIns_valid_imm_for_add_sp(afterFrameSPtoFPdelta))
            {
                genEstablishFramePointer(spToFPdelta, reportUnwindData: true);
                needToEstablishFP = false;
            }
        }
        var stackAllocMask = RBM_NONE;
        if (!Configuration.TARGET_ARM64 && !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            if (Configuration.TARGET_ARM)
            {
                stackAllocMask = genStackAllocRegisterMask(
                    unchecked((uint)_compiler.compLclFrameSize + extraFrameSize),
                    _regSet.rsGetModifiedFltCalleeSavedRegsMask());
            }
            if (stackAllocMask.IsEmpty)
            {
                genAllocLclFrame(unchecked((uint)_compiler.compLclFrameSize + extraFrameSize),
                    initReg, ref initRegZeroed, _calleeRegArgMaskLiveIn);
            }
        }
        if (Configuration.TARGET_AMD64 && inheritsCalleeSaves)
        {
            genOSRSaveRemainingCalleeSavedRegisters();
        }
        if (Configuration.TARGET_ARM && _compiler.compLocallocUsed)
        {
            _ = Emitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_ARM32_R9, TargetRuntimeMetadata.StackPointerBaseRegister, canSkip: false);
            _regSet.verifyRegUsed(REG_ARM32_R9);
            _compiler.unwindSetFrameReg(REG_ARM32_R9, 0);
        }
        if (Configuration.TARGET_XARCH)
        {
            genClearAvxStateInProlog();
            genPreserveCalleeSavedFltRegs();
        }
        if (Configuration.TARGET_AMD64 && doubleAlignOrFramePointerUsed())
        {
            var reportUnwindData = _compiler.compLocallocUsed || _compiler.opts.compDbgEnC;
            genEstablishFramePointer(genSPtoFPdelta, reportUnwindData);
        }
        _compiler.unwindEndProlog();

        if (Configuration.TARGET_ARM64 && _compiler.compUsesUnknownSizeFrame)
        {
            genUnknownSizeFrame();
        }
        if (Configuration.TARGET_ARM && needToEstablishFP)
        {
            genEstablishFramePointer(afterFrameSPtoFPdelta, reportUnwindData: false);
        }
        genZeroInitFrame(untrLclHi, untrLclLo, initReg, ref initRegZeroed);
        genReportGenericContextArg(initReg, ref initRegZeroed);
        if (!Configuration.TARGET_WASM)
        {
            if (Configuration.JIT32_GCENCODER && (_compiler.lvaLocAllocSPvar != BAD_VAR_NUM))
            {
                Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, _compiler.lvaLocAllocSPvar, 0);
            }
            genSetGSSecurityCookie(initReg, ref initRegZeroed);
            if (Configuration.PROFILING_SUPPORTED && !_compiler.opts.IsOSR)
            {
                genProfilingEnterCallback(initReg, ref initRegZeroed);
            }
        }
        if (_compiler.opts.IsOSR && (Emitter.emitGetCurrentCodeOffsetFrom(null) == 0)
            && (_compiler.lvaReportParamTypeArg() || _compiler.lvaKeepAliveAndReportThis()))
        {
            JITDUMP("OSR: prolog was zero length and has generic context to report: adding nop to pad prolog.\n");
            instGen(INS_nop);
        }
        if (!Interruptible)
        {
            Emitter.emitMarkPrologEnd();
        }

        if (Configuration.UNIX_AMD64_ABI && Configuration.FEATURE_SIMD)
        {
            genClearStackVec3ArgUpperBits();
        }
        if (Configuration.SWIFT_SUPPORT && (_compiler.info.compCallConv == CorInfoCallConvExtension.Swift)
            && (_compiler.lvaSwiftErrorArg != BAD_VAR_NUM))
        {
            _calleeRegArgMaskLiveIn &= ~new regMaskTP(TargetRuntimeMetadata.SwiftErrorMask);
        }
        if (_compiler.opts.IsOSR)
        {
            genEnregisterOSRArgsAndLocals(initReg, ref initRegZeroed);
            assert((_compiler._paramRegLocalMappings is null) || (_compiler._paramRegLocalMappings.Count == 0));
            _compiler.lvaUpdateArgsWithInitialReg();
        }
        else
        {
            _compiler.lvaUpdateArgsWithInitialReg();
            genHomeStackPartOfSplitParameter(initReg, ref initRegZeroed);
            genHomeRegisterParams(initReg, ref initRegZeroed);
            genEnregisterIncomingStackArgs();
        }

        if (!Configuration.TARGET_WASM)
        {
            if (initRegs.IsNonEmpty)
            {
                for (var reg = TargetRuntimeMetadata.IntegerRegisterFirst; reg <= TargetRuntimeMetadata.IntegerRegisterLast; reg++)
                {
                    var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
                    if ((mask & initRegs).IsNonEmpty)
                    {
                        if ((reg == initReg) && initRegZeroed)
                        {
                            continue;
                        }
                        instGen_Set_Reg_To_Zero(EA_PTRSIZE, reg);
                        if (reg == initReg)
                        {
                            initRegZeroed = true;
                        }
                    }
                }
            }
            if ((initFltRegs | initDblRegs).IsNonEmpty)
            {
                if ((regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask) & initRegs).IsEmpty)
                {
                    initReg = TargetRuntimeMetadata.ScratchRegister;
                    initRegZeroed = false;
                }
                if (Configuration.TARGET_ARM && !initRegZeroed)
                {
                    instGen_Set_Reg_To_Zero(EA_PTRSIZE, initReg);
                }
                genZeroInitFltRegs(initFltRegs, initDblRegs, initReg);
            }
        }

        if (Interruptible)
        {
            Emitter.emitMarkPrologEnd();
        }
        if (_compiler.opts.compScopeInfo && (_compiler.info.compVarScopesCount > 0))
        {
            psiEndProlog();
        }
        if (hasGCRef)
        {
            Emitter.emitSetFrameRangeGCRs(gcRefLo, gcRefHi);
        }
        else
        {
            noway_assert(gcRefLo == int.MaxValue);
            noway_assert(gcRefHi == -int.MaxValue);
        }
        if (Configuration.DEBUG && _compiler.opts.dspCode)
        {
            jitprintf("\n");
        }
        if (Configuration.TARGET_X86)
        {
            var argsStartVar = _compiler.lvaVarargsBaseOfStkArgs;
            if (_compiler.info.compIsVarArgs && (_compiler.lvaGetDesc(argsStartVar).lvRefCnt() > 0))
            {
                ref var local = ref _compiler.lvaGetDesc(argsStartVar);
                noway_assert(_compiler.info.compArgsCount > 0);
                assert(_compiler.lvaVarargsHandleArg == _compiler.info.compArgsCount - 1);
                Emitter.emitIns_R_S(ins_Load(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.ScratchRegister, _compiler.lvaVarargsHandleArg, 0);
                _regSet.verifyRegUsed(TargetRuntimeMetadata.ScratchRegister);
                Emitter.emitIns_R_AR(ins_Load(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.ScratchRegister, TargetRuntimeMetadata.ScratchRegister, 0);

                ref readonly var lastArg = ref _compiler.lvaGetDesc(_compiler.lvaVarargsHandleArg);
                noway_assert(!lastArg.lvRegister);
                var offset = lastArg.StackOffset;
                if (Configuration.DEBUG)
                {
                    assert(offset != BAD_STK_OFFS);
                }
                noway_assert(lastArg.lvFramePointerBased);
                Emitter.emitIns_R_ARR(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.ScratchRegister, genFramePointerReg(), TargetRuntimeMetadata.ScratchRegister, offset);
                if (local.lvIsInReg)
                {
                    _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, local.RegNum, TargetRuntimeMetadata.ScratchRegister, canSkip: true);
                    _regSet.verifyRegUsed(local.RegNum);
                }
                else
                {
                    Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.ScratchRegister, argsStartVar, 0);
                }
            }
        }
        if (Configuration.DEBUG && Configuration.TARGET_XARCH && _compiler.compStackCheckOnRet)
        {
            assert(_compiler.lvaReturnSpCheck != BAD_VAR_NUM);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvDoNotEnregister);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvOnFrame);
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, _compiler.lvaReturnSpCheck, 0);
        }
        Emitter.emitEndProlog();
    }

    private void genBeginFnProlog()
    {
        if (Configuration.TARGET_WASM)
        {
            genBeginFnPrologWasm();
        }
    }

    private void RequireSupportedRootPrologAbi()
    {
        if (_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI))
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnixNativeAOTPrologsRequireCFIUnwindSupport);
            return;
        }

        if (_compiler.info.compIsVarArgs)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnixAMD64VarargsAreUnsupported);
            return;
        }
    }
}
