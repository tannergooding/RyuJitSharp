// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool doubleAlignOrFramePointerUsed()
    {
#if DOUBLE_ALIGN
        return IsFramePointerUsed || _compiler.genDoubleAlign;
#else
        return IsFramePointerUsed;
#endif
    }

    public unsafe void genPopCalleeSavedRegisters(bool jmpEpilog = false)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save restoration requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

#if TARGET_AMD64
        var isFunclet = _compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT;
        if (_compiler.opts.IsOSR && !isFunclet)
        {
            var popRegs = _regSet.rsGetModifiedOsrIntCalleeSavedRegsMask();
            var patchpoint = _compiler.info.compPatchpointInfo;
            noway_assert(patchpoint is not null);
            var tier0 = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters)
                & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
            var additional = popRegs & ~tier0;
            genPopCalleeSavedRegistersFromMask(additional);
            genPopCalleeSavedRegistersFromMask(tier0 & ~new regMaskTP(SRBM_EBP));
            return;
        }

        if (_compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPP2 != 0))
        {
            var apxRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
            var apxCount = genPopCalleeSavedRegistersFromMaskAPX(apxRegs);
            noway_assert(_compiler.compCalleeRegsPushed == apxCount);
            return;
        }
#endif
        var normalRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
        var count = genPopCalleeSavedRegistersFromMask(normalRegs);
        noway_assert(_compiler.compCalleeRegsPushed == count);
#endif
    }

    public uint genPopCalleeSavedRegistersFromMask(regMaskTP popRegs)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save restoration requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        var count = 0u;
        var options = _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPPHint != 0)
            ? INS_OPTS_APX_ppx : INS_OPTS_NONE;

        if ((popRegs & RBM_EBX).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_EBX, options);
        }
        if ((popRegs & new regMaskTP(SRBM_EBP)).IsNonEmpty)
        {
            assert(!doubleAlignOrFramePointerUsed());
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_FPBASE, options);
        }
#if !UNIX_AMD64_ABI
        if ((popRegs & RBM_ESI).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_ESI, options);
        }
        if ((popRegs & RBM_EDI).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_EDI, options);
        }
#endif

#if TARGET_AMD64
        var highRegs = popRegs & (RBM_R12 | RBM_R13 | RBM_R14 | RBM_R15);
        while (highRegs.IsNonEmpty)
        {
            var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)highRegs.IntRegSet);
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, reg, options);
            highRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }
#endif

        return count;
#endif
    }

    public uint genPopCalleeSavedRegistersFromMaskAPX(regMaskTP popRegs)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "APX callee-save restoration requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert((_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT) && !_compiler.opts.IsOSR);
        assert((popRegs & RBM_RSP).IsEmpty);

        var alignReg = REG_NA;
        if (!IsFramePointerUsed && popRegs.IsNonEmpty)
        {
            alignReg = (popRegs & RBM_RBP).IsNonEmpty
                ? REG_RBP : (regNumber)BitOperations.TrailingZeroCount((ulong)popRegs.IntRegSet);
            popRegs &= ~regMaskTP.CreateFromRegNum(alignReg, alignReg.SingleTypeMask);
        }

        Span<regNumber> registers = stackalloc regNumber[REG_INT_LAST - REG_INT_FIRST + 1];
        var count = 0;
        while (popRegs.IsNonEmpty)
        {
            var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)popRegs.IntRegSet);
            registers[count++] = reg;
            popRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }

        var popped = 0u;
        var index = 0;
        if ((count & 1) != 0)
        {
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, registers[index++], INS_OPTS_APX_ppx);
            popped++;
        }

        while (index < count - 1)
        {
            Emitter.emitIns_R_R(INS_pop2, EA_PTRSIZE, registers[index++], registers[index++],
                INS_OPTS_EVEX_nd | INS_OPTS_APX_ppx);
            popped += 2;
        }
        assert(index == count);

        if (alignReg != REG_NA)
        {
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, alignReg, INS_OPTS_APX_ppx);
            popped++;
        }

        return popped;
#endif
    }

    public unsafe void genFnEpilog(BasicBlock block)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Root epilog generation requires xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFnEpilog()\n");
        }
#endif
        VarSetOps.Assign(_compiler, ref GCInfo.gcVarPtrSetCur, Emitter.InitGCrefVars);
        GCInfo.gcRegGCrefSetCur = Emitter.InitGCrefRegs;
        GCInfo.gcRegByrefSetCur = Emitter.InitByrefRegs;
        noway_assert(!_compiler.opts.MinOpts || IsFramePointerUsed);
#if DEBUG
        _genInterruptibleUsed = true;
#endif
        var jmpEpilog = block.HasFlag(BBF_HAS_JMP);

#if DEBUG
        if (_compiler.opts.dspCode)
        {
            jitprintf("\n__epilog:\n");
        }
        if (_verbose)
        {
            jitprintf($"gcVarPtrSetCur={VarSetOps.ToString(_compiler, GCInfo.gcVarPtrSetCur)} ");
            dumpConvertedVarSet(_compiler, GCInfo.gcVarPtrSetCur);
            jitprintf(", gcRegGCrefSetCur=");
            printRegMaskInt(GCInfo.gcRegGCrefSetCur);
            Emitter.emitDispRegSet(GCInfo.gcRegGCrefSetCur);
            jitprintf(", gcRegByrefSetCur=");
            printRegMaskInt(GCInfo.gcRegByrefSetCur);
            Emitter.emitDispRegSet(GCInfo.gcRegByrefSetCur);
            jitprintf("\n");
        }
#endif
        genClearAvxStateInEpilog();
        genRestoreCalleeSavedFltRegs();

#if JIT32_GCENCODER
        Emitter.emitStartEpilog();
#endif

        var removeEbpFrame = doubleAlignOrFramePointerUsed();
#if TARGET_AMD64
        if (removeEbpFrame)
        {
            removeEbpFrame = _compiler.compLocallocUsed || _compiler.opts.compDbgEnC;
        }
#endif
        if (!removeEbpFrame)
        {
            noway_assert(!_compiler.compLocallocUsed);
            noway_assert(_compiler.compLclFrameSize >= 0);
            var frameSize = unchecked((uint)_compiler.compLclFrameSize);
#if TARGET_AMD64
            if (_compiler.opts.IsOSR)
            {
                var patchpoint = _compiler.info.compPatchpointInfo;
                noway_assert(patchpoint is not null);
                var tier0Saves = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters);
                var tier0Ints = tier0Saves & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
                var allInts = _regSet.rsGetModifiedOsrIntCalleeSavedRegsMask() | tier0Ints;
                var tier0Frame = unchecked((uint)(patchpoint->TotalFrameSize + REGSIZE_BYTES));
                var usedSaves = (uint)BitOperations.PopCount((ulong)allInts.IntRegSet) * REGSIZE_BYTES;
                var osrSaves = unchecked((uint)_compiler.compCalleeRegsPushed * REGSIZE_BYTES);
                var framePointer = IsFramePointerUsed ? REGSIZE_BYTES : 0;
                var adjustment = unchecked(tier0Frame - usedSaves + osrSaves + (uint)framePointer);

                JITDUMP($"OSR epilog adjust factors: tier0 frame {tier0Frame}, tier0 callee saves -{usedSaves}, osr callee saves {osrSaves} framePointer {framePointer}\n");
                JITDUMP($"    OSR frame size {frameSize}; net osr adjust {adjustment}, result {frameSize + adjustment}\n");
                frameSize = unchecked(frameSize + adjustment);
            }
#endif

            if (frameSize > 0)
            {
#if TARGET_X86
                if ((frameSize == TARGET_POINTER_SIZE) && !_compiler.compJmpOpUsed && !_compiler.compIsAsync)
                {
                    inst_RV(INS_pop, REG_ECX, TYP_I_IMPL);
                    _regSet.verifyRegUsed(REG_ECX);
                }
                else
#endif
                {
                    inst_RV_IV(INS_add, REG_SPBASE, (nint)frameSize, EA_PTRSIZE);
                }
            }
            genPopCalleeSavedRegisters();
#if TARGET_AMD64
            if (doubleAlignOrFramePointerUsed() || _compiler.opts.IsOSR)
            {
                inst_RV(INS_pop, REG_FPBASE, TYP_I_IMPL);
            }
#endif
        }
        else
        {
            noway_assert(doubleAlignOrFramePointerUsed());
            assert(!_compiler.opts.IsOSR);

            var needMovEspEbp = false;
#if DOUBLE_ALIGN
            if (_compiler.genDoubleAlign)
            {
                noway_assert(_compiler.compLclFrameSize != 0);
                inst_RV_IV(INS_add, REG_SPBASE, _compiler.compLclFrameSize, EA_PTRSIZE);
                needMovEspEbp = true;
            }
            else
#endif
            {
                var needLea = false;
                if (_compiler.compLocallocUsed)
                {
                    needLea = true;
                }
                else if (!_regSet.rsRegsModified(
                    new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED)))
                {
                    if (_compiler.compLclFrameSize != 0)
                    {
#if TARGET_AMD64
                        needLea = true;
#else
                        needMovEspEbp = true;
#endif
                    }
                }
                else if (_compiler.compLclFrameSize != 0)
                {
#if TARGET_X86
                    if ((_compiler.compLclFrameSize == REGSIZE_BYTES) && !_compiler.compJmpOpUsed &&
                        !_compiler.compIsAsync)
                    {
                        inst_RV(INS_pop, REG_ECX, TYP_I_IMPL);
                        _regSet.verifyRegUsed(REG_ECX);
                    }
                    else
#endif
                    {
                        needLea = true;
                    }
                }

                if (needLea)
                {
#if TARGET_AMD64
                    var offset = genSPtoFPdelta - _compiler.compLclFrameSize;
                    if (!_compiler.compLocallocUsed)
                    {
                        noway_assert(offset < byte.MaxValue);
                    }
#else
                    var offset = _compiler.compCalleeRegsPushed * REGSIZE_BYTES;
                    noway_assert(offset < byte.MaxValue);
#endif
                    Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, -offset);
                }
            }
            genPopCalleeSavedRegisters();
#if TARGET_AMD64
            if (_compiler.opts.IsOSR)
            {
                var patchpoint = _compiler.info.compPatchpointInfo;
                noway_assert(patchpoint is not null);
                inst_RV_IV(INS_add, REG_SPBASE,
                    patchpoint->TotalFrameSize + TARGET_POINTER_SIZE, EA_PTRSIZE);
            }
            assert(!needMovEspEbp);
#else
            if (needMovEspEbp)
            {
                inst_Mov(TYP_I_IMPL, REG_SPBASE, REG_FPBASE, canSkip: false);
            }
#endif
            inst_RV(INS_pop, REG_FPBASE, TYP_I_IMPL);
        }

        Emitter.emitStartExitSeq();

        if (jmpEpilog)
        {
            noway_assert(block.Kind == BBJ_RETURN);
            var last = block.GetLastNode() ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Missing JMP epilog node.");
#if !FEATURE_FASTTAILCALL
            noway_assert(last.Oper == GT_JMP);
#else
            noway_assert((last.Oper == GT_JMP) ||
                ((last.Oper == GT_CALL) && last.AsCall().IsFastTailCall));
#endif
            if (last.Oper == GT_JMP)
            {
#if FEATURE_FASTTAILCALL
                noway_assert(last.Next is null);
#endif
                var method = (CORINFO_METHOD_HANDLE)last.AsVal().Val1;
                CORINFO_CONST_LOOKUP lookup = default;
                _compiler.info.compCompHnd->getFunctionEntryPoint(method, &lookup);
                noway_assert(lookup.accessType is IAT_VALUE or IAT_PVALUE);

                var parameters = new EmitCallParams { methHnd = method, isJump = true };
                if (lookup.accessType == IAT_PVALUE)
                {
                    if (genCodeIndirAddrCanBeEncodedAsPCRelOffset((nuint)lookup.addr))
                    {
                        parameters.callType = EC_FUNC_TOKEN_INDIR;
                        parameters.addr = lookup.addr;
                    }
                    else
                    {
                        parameters.callType = EC_INDIR_ARD;
                        parameters.ireg = REG_RAX;
                        instGen_Set_Reg_To_Imm(EA_PTRSIZE | EA_CNS_RELOC_FLG, REG_RAX, (nint)lookup.addr);
                        _regSet.verifyRegUsed(REG_RAX);
                    }
                }
                else
                {
                    parameters.callType = EC_FUNC_TOKEN;
                    parameters.addr = lookup.addr;
                }
                genEmitCallWithCurrentGC(ref parameters);
            }
            else
            {
#if FEATURE_FASTTAILCALL
                noway_assert(last.Oper == GT_CALL && last.AsCall().IsFastTailCall);
                genCallInstruction(last.AsCall());
#endif
            }
        }
        else
        {
            var stackArgumentSize = 0u;
#if TARGET_X86
            var calleePop = !_compiler.info.compIsVarArgs && !IsCallerPop(_compiler.info.compCallConv);
            if (calleePop)
            {
                stackArgumentSize = unchecked((uint)_compiler.lvaParameterStackSize);
                noway_assert(stackArgumentSize < 0x10000);
            }
#if UNIX_X86_ABI
            if ((_compiler.info.compCallConv == CorInfoCallConvExtension.C) &&
                (_compiler.info.compRetBuffArg != BAD_VAR_NUM))
            {
                stackArgumentSize += TARGET_POINTER_SIZE;
            }
#endif
#endif
            instGen_Return(stackArgumentSize);
        }
#endif
    }

    public void instGen_Return(uint stackArgumentSize)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Return instruction generation requires xarch.");
#else
        if (stackArgumentSize == 0)
        {
            instGen(INS_ret);
        }
        else
        {
            inst_IV(INS_ret, (nint)stackArgumentSize);
        }
#endif
    }
}
