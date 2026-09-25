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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog and epilog materialization requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
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
#endif
    }

    public void genFnProlog()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Root prolog generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
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
            instGen(INS_int3);
        }
#endif
        var untrLclLo = int.MaxValue;
        var untrLclHi = -int.MaxValue;
        var hasUntrLcl = false;
        var gcRefLo = int.MaxValue;
        var gcRefHi = -int.MaxValue;
        var hasGCRef = false;
        var initRegs = RBM_NONE;
        var initFltRegs = RBM_NONE;
        var initDblRegs = RBM_NONE;

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
        assert(_compiler.opts.IsOSR || ((InitStkLclCnt > 0) == hasUntrLcl));
        if (InitStkLclCnt > 0)
        {
            JITDUMP($"Found {InitStkLclCnt} lvMustInit int-sized stack slots, frame offsets {unchecked(-untrLclLo)} through {unchecked(-untrLclHi)}\n");
        }

        var initReg = REG_SCRATCH;
        var initRegZeroed = false;
        var excludeMask = _calleeRegArgMaskLiveIn;
        if (!_compiler.canUseEvexEncoding())
        {
            excludeMask |= new regMaskTP(SRBM_HIGHINT);
        }

        var isRoot = _compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT;
        var inheritsCalleeSaves = isRoot && _compiler.opts.IsOSR;
        var allInt = new regMaskTP(SRBM_ALLINT);
        var tempMask = initRegs & allInt & ~excludeMask & ~_regSet.rsMaskResvd;
        if (tempMask.IsNonEmpty)
        {
            initReg = (regNumber)BitOperations.TrailingZeroCount((ulong)tempMask.IntRegSet);
        }
        else
        {
            tempMask = _regSet.rsGetModifiedRegsMask() & allInt & ~excludeMask & ~_regSet.rsMaskResvd;
            if (tempMask.IsNonEmpty)
            {
                initReg = (regNumber)BitOperations.TrailingZeroCount((ulong)tempMask.IntRegSet);
            }
        }
        if (inheritsCalleeSaves)
        {
            // Deferred callee saves cannot be used as scratch registers yet.
            initReg = REG_SCRATCH;
        }
        if (_compiler.info.compIsVarArgs && !_compiler.opts.IsOSR)
        {
            Emitter.spillIntArgRegsToShadowSlots();
        }

        var extraFrameSize = 0;
        if (inheritsCalleeSaves)
        {
            genOSRHandleTier0CalleeSavedRegistersAndFrame();
            extraFrameSize = _compiler.compCalleeRegsPushed * REGSIZE_BYTES;
            // Reproduce the return-address misalignment of an ordinary method entry.
            Emitter.emitIns_R(INS_push, EA_PTRSIZE, REG_RAX);
            _compiler.unwindAllocStack(REGSIZE_BYTES);
        }

        if (IsFramePointerUsed)
        {
            if (inheritsCalleeSaves)
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
        }
        if (!inheritsCalleeSaves)
        {
            genPushCalleeSavedRegisters(initReg, ref initRegZeroed);
        }

        genAllocLclFrame(unchecked((uint)(_compiler.compLclFrameSize + extraFrameSize)),
            initReg, ref initRegZeroed, _calleeRegArgMaskLiveIn);
        if (inheritsCalleeSaves)
        {
            genOSRSaveRemainingCalleeSavedRegisters();
        }
        genClearAvxStateInProlog();
        genPreserveCalleeSavedFltRegs();
        if (IsFramePointerUsed)
        {
            var reportUnwindData = _compiler.compLocallocUsed || _compiler.opts.compDbgEnC;
            genEstablishFramePointer(genSPtoFPdelta, reportUnwindData);
        }
        _compiler.unwindEndProlog();

        genZeroInitFrame(untrLclHi, untrLclLo, initReg, ref initRegZeroed);
        genReportGenericContextArg(initReg, ref initRegZeroed);
        genSetGSSecurityCookie(initReg, ref initRegZeroed);
#if PROFILING_SUPPORTED
        if (!_compiler.opts.IsOSR)
        {
            genProfilingEnterCallback(initReg, ref initRegZeroed);
        }
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
            genZeroInitFltRegs(initFltRegs, initDblRegs, initReg);
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
#if DEBUG
        if (_compiler.opts.dspCode)
        {
            jitprintf("\n");
        }
        if (_compiler.opts.compStackCheckOnRet)
        {
            assert(_compiler.lvaReturnSpCheck != BAD_VAR_NUM);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvDoNotEnregister);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvOnFrame);
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SPBASE, _compiler.lvaReturnSpCheck, 0);
        }
#endif
        Emitter.emitEndProlog();
#endif
    }

    private void genBeginFnProlog()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "The target-specific prolog hook is only ported for AMD64.");
#endif
    }
}
