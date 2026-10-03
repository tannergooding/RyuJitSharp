// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genGeneratePrologsAndEpilogs()
    {
#if UNIX_AMD64_ABI
        RequireSupportedRootPrologAbi();
#endif
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** Before prolog / epilog generation\n");
            Emitter.emitDispIGlist(displayInstructions: false);
        }
#endif
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
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** After prolog / epilog generation\n");
            Emitter.emitDispIGlist(displayInstructions: false);
        }
#endif
    }

    public void genFnProlog()
    {
#if UNIX_AMD64_ABI
        RequireSupportedRootPrologAbi();
#endif
        _compiler.funSetCurrentFunc(0);
        JITDUMP("*************** In genFnProlog()\n");
#if DEBUG
        _genInterruptibleUsed = true;
#endif
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
        Emitter.emitBegProlog();
        _compiler.unwindBegProlog();
        genIPmappingAddToFront(IPmappingDscKind.Prolog, default, true);
#if DEBUG
        if (_compiler.opts.dspCode)
        {
            jitprintf("\n__prolog:\n");
        }
#endif
        if (_compiler.opts.compScopeInfo && (_compiler.info.compVarScopesCount > 0))
        {
            psiBegProlog();
        }
        genBeginFnProlog();

#if DEBUG
        if (_compiler.compJitHaltMethod())
        {
            // Debuggers may replace the first instruction with INT3.
            instGen(INS_nop);
            instGen(INS_BREAKPOINT);
#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
            _compiler.unwindPadding();
#endif
        }
#endif
        var untrLclLo = int.MaxValue;
        var untrLclHi = -int.MaxValue;
        var hasUntrLcl = false;
        var gcRefLo = int.MaxValue;
        var gcRefHi = -int.MaxValue;
        var hasGCRef = false;
#if !TARGET_WASM
        var initRegs = RBM_NONE;
        var initFltRegs = RBM_NONE;
        var initDblRegs = RBM_NONE;
#endif

#if TARGET_WASM
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

#if DEBUG
        assert(_regSet.tmpGetAllFree());
        assert(_regSet.tmpListBeg() is null);
#endif
#else
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
            if (_compiler.lvaIsUnknownSizeLocal(varNum))
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

#if DEBUG
        assert(_regSet.tmpGetAllFree());
#endif
        for (var temp = _regSet.tmpListBeg(); temp is not null; temp = _regSet.tmpListNxt(temp))
        {
            if (!varTypeIsGC(temp.tdTempType))
            {
                continue;
            }

            var loOffs = temp.tdTempOffs;
            var hiOffs = unchecked(loOffs + TARGET_POINTER_SIZE);
#if !TARGET_AMD64
            noway_assert(!IsFramePointerUsed || (loOffs != 0));
#endif
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
#endif
        assert(_compiler.opts.IsOSR || ((InitStkLclCnt > 0) == hasUntrLcl));
        if (InitStkLclCnt > 0)
        {
            JITDUMP($"Found {InitStkLclCnt} lvMustInit int-sized stack slots, frame offsets {unchecked(-untrLclLo)} through {unchecked(-untrLclHi)}\n");
        }

#if !TARGET_WASM
#if TARGET_ARM
        _calleeRegArgMaskLiveIn &= ~genPrespilledUnmappedRegs();
#endif
        var initReg = REG_SCRATCH;
        var initRegZeroed = false;
        var excludeMask = _calleeRegArgMaskLiveIn;
#if TARGET_AMD64
        if (!_compiler.canUseEvexEncoding())
        {
            excludeMask |= new regMaskTP(SRBM_HIGHINT);
        }
#endif
#if TARGET_ARM
        if (_compiler.compLocallocUsed)
        {
            excludeMask |= new regMaskTP(SRBM_SAVED_LOCALLOC_SP);
        }
#endif

        var isRoot = _compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT;
        var inheritsCalleeSaves = isRoot && _compiler.opts.IsOSR;
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
            initReg = REG_SCRATCH;
#if TARGET_ARM64
            initReg = REG_IP1;
#endif
        }
#if TARGET_AMD64
        if (_compiler.info.compIsVarArgs && !_compiler.opts.IsOSR)
        {
            Emitter.spillIntArgRegsToShadowSlots();
        }
#endif
#if TARGET_ARM
        var preSpillMask = _regSet.rsMaskPreSpillRegs(true);
        if (preSpillMask.IsNonEmpty)
        {
            inst_IV(INS_push, unchecked((int)preSpillMask.Lower));
            _compiler.unwindPushMaskInt(preSpillMask);
        }
#endif
#else
        var initReg = REG_NA;
        var initRegZeroed = false;
        var inheritsCalleeSaves = false;
#endif

#if !TARGET_ARM64 && !TARGET_LOONGARCH64 && !TARGET_RISCV64
        uint extraFrameSize = 0;
#endif
        if (inheritsCalleeSaves)
        {
            genOSRHandleTier0CalleeSavedRegistersAndFrame();
#if TARGET_AMD64
            extraFrameSize = unchecked((uint)(_compiler.compCalleeRegsPushed * REGSIZE_BYTES));
            // Reproduce the return-address misalignment of an ordinary method entry.
            Emitter.emitIns_R(INS_push, EA_PTRSIZE, REG_RAX);
            _compiler.unwindAllocStack(REGSIZE_BYTES);
#endif
        }

#if TARGET_XARCH
        if (doubleAlignOrFramePointerUsed())
        {
            if (inheritsCalleeSaves && IsFramePointerUsed)
            {
                Emitter.emitIns_R_AR(INS_mov, EA_8BYTE, initReg, REG_FPBASE, 0);
                inst_RV(INS_push, initReg, TYP_REF);
                initRegZeroed = false;
                _compiler.unwindAllocStack(REGSIZE_BYTES);
            }
            else
            {
                inst_RV(INS_push, REG_FPBASE, TYP_REF);
                _compiler.unwindPush(REG_FPBASE);
            }
#if TARGET_X86
            genEstablishFramePointer(0, reportUnwindData: true);
#endif
#if DOUBLE_ALIGN
            if (_compiler.genDoubleAlign)
            {
                noway_assert(!IsFramePointerUsed);
                noway_assert(!_regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)));
                inst_RV_IV(INS_and, REG_SPBASE, -8, EA_PTRSIZE);
            }
#endif
        }
#endif
        var pushesCalleeSaves = true;
#if TARGET_AMD64
        pushesCalleeSaves = !inheritsCalleeSaves;
#endif
        if (pushesCalleeSaves)
        {
            genPushCalleeSavedRegisters(initReg, ref initRegZeroed);
        }

#if TARGET_ARM
        var needToEstablishFP = false;
        var afterFrameSPtoFPdelta = 0;
        if (doubleAlignOrFramePointerUsed())
        {
            needToEstablishFP = true;
            // Defer small-frame FP establishment past the OS-reported prolog to reduce unwind data.
            var spToFPdelta = unchecked((_compiler.compCalleeRegsPushed - 2) * REGSIZE_BYTES);
            afterFrameSPtoFPdelta = unchecked(spToFPdelta + _compiler.compLclFrameSize);
            if (!arm_Valid_Imm_For_Add_SP(afterFrameSPtoFPdelta))
            {
                genEstablishFramePointer(spToFPdelta, reportUnwindData: true);
                needToEstablishFP = false;
            }
        }
#endif
#if !TARGET_ARM64 && !TARGET_LOONGARCH64 && !TARGET_RISCV64
        var stackAllocMask = RBM_NONE;
#if TARGET_ARM
        stackAllocMask = genStackAllocRegisterMask(unchecked((uint)_compiler.compLclFrameSize + extraFrameSize),
            _regSet.rsGetModifiedFltCalleeSavedRegsMask());
#endif
        if (stackAllocMask.IsEmpty)
        {
            genAllocLclFrame(unchecked((uint)_compiler.compLclFrameSize + extraFrameSize),
                initReg, ref initRegZeroed, _calleeRegArgMaskLiveIn);
        }
#endif
#if TARGET_AMD64
        if (inheritsCalleeSaves)
        {
            genOSRSaveRemainingCalleeSavedRegisters();
        }
#endif
#if TARGET_ARM
        if (_compiler.compLocallocUsed)
        {
            Emitter.emitIns_Mov(INS_mov, EA_4BYTE, REG_SAVED_LOCALLOC_SP, REG_SPBASE, canSkip: false);
            _regSet.verifyRegUsed(REG_SAVED_LOCALLOC_SP);
            _compiler.unwindSetFrameReg(REG_SAVED_LOCALLOC_SP, 0);
        }
#endif
#if TARGET_XARCH
        genClearAvxStateInProlog();
        genPreserveCalleeSavedFltRegs();
#endif
#if TARGET_AMD64
        if (doubleAlignOrFramePointerUsed())
        {
            var reportUnwindData = _compiler.compLocallocUsed || _compiler.opts.compDbgEnC;
            genEstablishFramePointer(genSPtoFPdelta, reportUnwindData);
        }
#endif
        _compiler.unwindEndProlog();

#if TARGET_ARM64
        if (_compiler.compUsesUnknownSizeFrame)
        {
            genUnknownSizeFrame();
        }
#endif
#if TARGET_ARM
        if (needToEstablishFP)
        {
            genEstablishFramePointer(afterFrameSPtoFPdelta, reportUnwindData: false);
        }
#endif
        genZeroInitFrame(untrLclHi, untrLclLo, initReg, ref initRegZeroed);
        genReportGenericContextArg(initReg, ref initRegZeroed);
#if !TARGET_WASM
#if JIT32_GCENCODER
        if (_compiler.lvaLocAllocSPvar != BAD_VAR_NUM)
        {
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SPBASE, _compiler.lvaLocAllocSPvar, 0);
        }
#endif
        genSetGSSecurityCookie(initReg, ref initRegZeroed);
#if PROFILING_SUPPORTED
        if (!_compiler.opts.IsOSR)
        {
            genProfilingEnterCallback(initReg, ref initRegZeroed);
        }
#endif
#endif
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

#if UNIX_AMD64_ABI && FEATURE_SIMD
        genClearStackVec3ArgUpperBits();
#endif
#if SWIFT_SUPPORT
        if ((_compiler.info.compCallConv == CorInfoCallConvExtension.Swift)
            && (_compiler.lvaSwiftErrorArg != BAD_VAR_NUM))
        {
            _calleeRegArgMaskLiveIn &= ~new regMaskTP(SRBM_SWIFT_ERROR);
        }
#endif
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

#if !TARGET_WASM
        if (initRegs.IsNonEmpty)
        {
            for (var reg = REG_INT_FIRST; reg <= REG_INT_LAST; reg++)
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
                initReg = REG_SCRATCH;
                initRegZeroed = false;
            }
#if TARGET_ARM
            if (!initRegZeroed)
            {
                instGen_Set_Reg_To_Zero(EA_PTRSIZE, initReg);
            }
#endif
            genZeroInitFltRegs(initFltRegs, initDblRegs, initReg);
        }
#endif

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
#if DEBUG
        if (_compiler.opts.dspCode)
        {
            jitprintf("\n");
        }
#endif
#if TARGET_X86
        var argsStartVar = _compiler.lvaVarargsBaseOfStkArgs;
        if (_compiler.info.compIsVarArgs && (_compiler.lvaGetDesc(argsStartVar).lvRefCnt() > 0))
        {
            ref var local = ref _compiler.lvaGetDesc(argsStartVar);
            noway_assert(_compiler.info.compArgsCount > 0);
            assert(_compiler.lvaVarargsHandleArg == _compiler.info.compArgsCount - 1);
            Emitter.emitIns_R_S(ins_Load(TYP_I_IMPL), EA_PTRSIZE, REG_SCRATCH, _compiler.lvaVarargsHandleArg, 0);
            _regSet.verifyRegUsed(REG_SCRATCH);
            Emitter.emitIns_R_AR(ins_Load(TYP_I_IMPL), EA_PTRSIZE, REG_SCRATCH, REG_SCRATCH, 0);

            ref readonly var lastArg = ref _compiler.lvaGetDesc(_compiler.lvaVarargsHandleArg);
            noway_assert(!lastArg.lvRegister);
            var offset = lastArg.StackOffset;
            assert(offset != BAD_STK_OFFS);
            noway_assert(lastArg.lvFramePointerBased);
            Emitter.emitIns_R_ARR(INS_lea, EA_PTRSIZE, REG_SCRATCH, genFramePointerReg(), REG_SCRATCH, offset);
            if (local.lvIsInReg)
            {
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, local.RegNum, REG_SCRATCH, canSkip: true);
                _regSet.verifyRegUsed(local.RegNum);
            }
            else
            {
                Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SCRATCH, argsStartVar, 0);
            }
        }
#endif
#if DEBUG && TARGET_XARCH
        if (_compiler.opts.compStackCheckOnRet)
        {
            assert(_compiler.lvaReturnSpCheck != BAD_VAR_NUM);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvDoNotEnregister);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvOnFrame);
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SPBASE, _compiler.lvaReturnSpCheck, 0);
        }
#endif
        Emitter.emitEndProlog();
    }

    private void genBeginFnProlog()
    {
#if TARGET_WASM
        genBeginFnPrologWasm();
#endif
    }

#if UNIX_AMD64_ABI
    private void RequireSupportedRootPrologAbi()
    {
        if (_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI))
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Unix NativeAOT prologs require CFI unwind support.");
        }

        if (_compiler.info.compIsVarArgs)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Unix AMD64 varargs are unsupported.");
        }
    }
#endif
}
