// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genPopCalleeSavedRegisters(bool jmpEpilog = false)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save restoration requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

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
            genPopCalleeSavedRegistersFromMask(tier0 & ~RBM_RBP);
            return;
        }

        var normalRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
        var count = _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPP2 != 0)
            ? genPopCalleeSavedRegistersFromMaskAPX(normalRegs)
            : genPopCalleeSavedRegistersFromMask(normalRegs);
        noway_assert(_compiler.compCalleeRegsPushed == count);
#endif
    }

    public uint genPopCalleeSavedRegistersFromMask(regMaskTP popRegs)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save restoration requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var count = 0u;
        var options = _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPPHint != 0)
            ? INS_OPTS_APX_ppx : INS_OPTS_NONE;

        if ((popRegs & RBM_RBX).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_RBX, options);
        }
        if ((popRegs & RBM_RBP).IsNonEmpty)
        {
            assert(!IsFramePointerUsed);
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_RBP, options);
        }
        if ((popRegs & RBM_RSI).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_RSI, options);
        }
        if ((popRegs & RBM_RDI).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_RDI, options);
        }

        var highRegs = popRegs & (RBM_R12 | RBM_R13 | RBM_R14 | RBM_R15);
        while (highRegs.IsNonEmpty)
        {
            var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)highRegs.IntRegSet);
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, reg, options);
            highRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }

        return count;
#endif
    }

    public uint genPopCalleeSavedRegistersFromMaskAPX(regMaskTP popRegs)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "APX callee-save restoration requires Windows AMD64.");
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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Root epilog generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
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

        var removeEbpFrame = IsFramePointerUsed &&
            (_compiler.compLocallocUsed || _compiler.opts.compDbgEnC);
        if (!removeEbpFrame)
        {
            noway_assert(!_compiler.compLocallocUsed);
            noway_assert(_compiler.compLclFrameSize >= 0);
            var frameSize = unchecked((uint)_compiler.compLclFrameSize);
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

            if (frameSize > 0)
            {
                inst_RV_IV(INS_add, REG_SPBASE, (nint)frameSize, EA_PTRSIZE);
            }
            genPopCalleeSavedRegisters();
            if (IsFramePointerUsed || _compiler.opts.IsOSR)
            {
                inst_RV(INS_pop, REG_RBP, TYP_I_IMPL);
            }
        }
        else
        {
            noway_assert(IsFramePointerUsed);
            assert(!_compiler.opts.IsOSR);
            var needLea = _compiler.compLocallocUsed
                || (_compiler.compLclFrameSize != 0);
            if (needLea)
            {
                var offset = genSPtoFPdelta - _compiler.compLclFrameSize;
                if (!_compiler.compLocallocUsed)
                {
                    noway_assert(offset < byte.MaxValue);
                }
                Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_SPBASE, REG_RBP, -offset);
            }
            genPopCalleeSavedRegisters();
            inst_RV(INS_pop, REG_RBP, TYP_I_IMPL);
        }

        Emitter.emitStartExitSeq();

        if (jmpEpilog)
        {
            noway_assert(block.Kind == BBJ_RETURN);
            var last = block.GetLastNode() ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Missing JMP epilog node.");
            if (last.Oper == GT_JMP)
            {
                noway_assert(last.Next is null);
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
                noway_assert(last.Oper == GT_CALL && last.AsCall().IsFastTailCall);
                genCallInstruction(last.AsCall());
            }
        }
        else
        {
            instGen_Return(0);
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
