// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genPopCalleeSavedRegistersXarch(bool jmpEpilog = false)
    {
        if (Configuration.TARGET_ARM)
        {
            genPopCalleeSavedRegistersArmCore(jmpEpilog);
            return;
        }
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.CalleeSaveRestorationRequiresXarch);
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

        if (Configuration.TARGET_AMD64)
        {
            var isFunclet = _compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT;
            if (_compiler.opts.IsOSR && !isFunclet)
            {
                var popRegs = _regSet.rsGetModifiedOsrIntCalleeSavedRegsMask();
                var patchpoint = _compiler.info.compPatchpointInfo;
                noway_assert(patchpoint is not null);
                var tier0 = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters)
                    & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
                var additional = popRegs & ~tier0;
                _ = genPopCalleeSavedRegistersFromMaskXarch(additional);
                _ = genPopCalleeSavedRegistersFromMaskXarch(tier0 & ~new regMaskTP(SRBM_X86_EBP));
                return;
            }

            if (_compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPP2 != 0))
            {
                var apxRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
                var apxCount = genPopCalleeSavedRegistersFromMaskAPX(apxRegs);
                noway_assert(_compiler.compCalleeRegsPushed == apxCount);
                return;
            }
        }

        var normalRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
        var count = genPopCalleeSavedRegistersFromMaskXarch(normalRegs);
        noway_assert(_compiler.compCalleeRegsPushed == count);
    }

    public uint genPopCalleeSavedRegistersFromMaskXarch(regMaskTP popRegs)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.CalleeSaveRestorationRequiresXarch);
            return default!;
        }
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var count = 0u;
        var options = _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPPHint != 0)
            ? INS_OPTS_APX_ppx : INS_OPTS_NONE;

        if ((popRegs & RBM_EBX).IsNonEmpty)
        {
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_X86_EBX, options);
        }
        if ((popRegs & new regMaskTP(SRBM_X86_EBP)).IsNonEmpty)
        {
            assert(!doubleAlignOrFramePointerUsed());
            count++;
            Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_X86_EBP, options);
        }
        if (!Configuration.UNIX_AMD64_ABI)
        {
            if ((popRegs & RBM_ESI).IsNonEmpty)
            {
                count++;
                Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_X86_ESI, options);
            }
            if ((popRegs & RBM_EDI).IsNonEmpty)
            {
                count++;
                Emitter.emitIns_R(INS_pop, EA_PTRSIZE, REG_X86_EDI, options);
            }
        }

        if (Configuration.TARGET_AMD64)
        {
            var highRegs = popRegs & (RBM_R12 | RBM_R13 | RBM_R14 | RBM_R15);
            while (highRegs.IsNonEmpty)
            {
                var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)highRegs.IntRegSet);
                count++;
                Emitter.emitIns_R(INS_pop, EA_PTRSIZE, reg, options);
                highRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            }
        }

        return count;
    }

    public unsafe void genFnEpilogXarch(BasicBlock block)
    {
        if (Configuration.TARGET_WASM)
        {
            genFnEpilogWasm(block);
            return;
        }
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RootEpilogGenerationRequiresXarch);
            return;
        }
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genFnEpilog()\n");
        }
        VarSetOps.Assign(_compiler, ref GCInfo.gcVarPtrSetCur, Emitter.InitGCrefVars);
        GCInfo.gcRegGCrefSetCur = Emitter.InitGCrefRegs;
        GCInfo.gcRegByrefSetCur = Emitter.InitByrefRegs;
        noway_assert(!_compiler.opts.MinOpts || IsFramePointerUsed);
        if (Configuration.DEBUG)
        {
            _genInterruptibleUsed = true;
        }
        var jmpEpilog = block.HasFlag(BBF_HAS_JMP);

        if (Configuration.DEBUG && _compiler.opts.dspCode)
        {
            jitprintf("\n__epilog:\n");
        }
        if (Configuration.DEBUG && _verbose)
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
        genClearAvxStateInEpilog();
        genRestoreCalleeSavedFltRegs();

        if (Configuration.JIT32_GCENCODER)
        {
            Emitter.emitStartEpilog();
        }

        var removeEbpFrame = doubleAlignOrFramePointerUsed();
        if (Configuration.TARGET_AMD64 && removeEbpFrame)
        {
            removeEbpFrame = _compiler.compLocallocUsed || _compiler.opts.compDbgEnC;
        }
        if (!removeEbpFrame)
        {
            noway_assert(!_compiler.compLocallocUsed);
            noway_assert(_compiler.compLclFrameSize >= 0);
            var frameSize = unchecked((uint)_compiler.compLclFrameSize);
            if (Configuration.TARGET_AMD64 && _compiler.opts.IsOSR)
            {
                var patchpoint = _compiler.info.compPatchpointInfo;
                noway_assert(patchpoint is not null);
                var tier0Saves = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters);
                var tier0Ints = tier0Saves & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
                var allInts = _regSet.rsGetModifiedOsrIntCalleeSavedRegsMask() | tier0Ints;
                var tier0Frame = unchecked((uint)(patchpoint->TotalFrameSize + Configuration.REGSIZE_BYTES));
                var usedSaves = unchecked((uint)BitOperations.PopCount((ulong)allInts.IntRegSet) *
                    (uint)Configuration.REGSIZE_BYTES);
                var osrSaves = unchecked((uint)_compiler.compCalleeRegsPushed * (uint)Configuration.REGSIZE_BYTES);
                var framePointer = IsFramePointerUsed ? (uint)Configuration.REGSIZE_BYTES : 0;
                var adjustment = unchecked(tier0Frame - usedSaves + osrSaves + framePointer);

                JITDUMP($"OSR epilog adjust factors: tier0 frame {tier0Frame}, tier0 callee saves -{usedSaves}, osr callee saves {osrSaves} framePointer {framePointer}\n");
                JITDUMP($"    OSR frame size {frameSize}; net osr adjust {adjustment}, result {frameSize + adjustment}\n");
                frameSize = unchecked(frameSize + adjustment);
            }
            if (frameSize > 0)
            {
                if (Configuration.TARGET_X86 && (frameSize == Configuration.TARGET_POINTER_SIZE) &&
                    !_compiler.compJmpOpUsed && !_compiler.compIsAsync)
                {
                    inst_RV(INS_pop, REG_X86_ECX, TYP_I_IMPL);
                    _regSet.verifyRegUsed(REG_X86_ECX);
                }
                else
                {
                    if (!Configuration.TARGET_X86 || (frameSize != Configuration.TARGET_POINTER_SIZE) ||
                        _compiler.compJmpOpUsed || _compiler.compIsAsync)
                    {
                        inst_RV_IV(INS_add, REG_X86_ESP, (nint)frameSize, EA_PTRSIZE);
                    }
                }
            }
            genPopCalleeSavedRegistersXarch();
            if (Configuration.TARGET_AMD64 &&
                (doubleAlignOrFramePointerUsed() || _compiler.opts.IsOSR))
            {
                inst_RV(INS_pop, REG_X86_EBP, TYP_I_IMPL);
            }
        }
        else
        {
            noway_assert(doubleAlignOrFramePointerUsed());
            assert(!_compiler.opts.IsOSR);

            var needMovEspEbp = false;
            if (Configuration.DOUBLE_ALIGN && _compiler.genDoubleAlign)
            {
                noway_assert(_compiler.compLclFrameSize != 0);
                inst_RV_IV(INS_add, REG_X86_ESP, _compiler.compLclFrameSize, EA_PTRSIZE);
                needMovEspEbp = true;
            }
            else
            {
                var needLea = false;
                if (_compiler.compLocallocUsed)
                {
                    needLea = true;
                }
                else if (!_regSet.rsRegsModified(
                    new regMaskTP(Configuration.TARGET_AMD64
                        ? SRBM_INT_CALLEE_SAVED_AMD64 | SRBM_FLT_CALLEE_SAVED_AMD64
                        : SRBM_INT_CALLEE_SAVED_X86 | SRBM_FLT_CALLEE_SAVED_X86)))
                {
                    if (_compiler.compLclFrameSize != 0)
                    {
                        if (Configuration.TARGET_AMD64)
                        {
                            needLea = true;
                        }
                        else
                        {
                            needMovEspEbp = true;
                        }
                    }
                }
                else if (_compiler.compLclFrameSize != 0)
                {
                    if (Configuration.TARGET_X86 && (_compiler.compLclFrameSize == Configuration.REGSIZE_BYTES) &&
                        !_compiler.compJmpOpUsed &&
                        !_compiler.compIsAsync)
                    {
                        inst_RV(INS_pop, REG_X86_ECX, TYP_I_IMPL);
                        _regSet.verifyRegUsed(REG_X86_ECX);
                    }
                    {
                        if (!Configuration.TARGET_X86 || (_compiler.compLclFrameSize != Configuration.REGSIZE_BYTES) ||
                            _compiler.compJmpOpUsed || _compiler.compIsAsync)
                        {
                            needLea = true;
                        }
                    }
                }

                if (needLea)
                {
                    int offset;
                    if (Configuration.TARGET_AMD64)
                    {
                        offset = genSPtoFPdelta - _compiler.compLclFrameSize;
                        if (!_compiler.compLocallocUsed)
                        {
                            noway_assert(offset < byte.MaxValue);
                        }
                    }
                    else
                    {
                        offset = _compiler.compCalleeRegsPushed * Configuration.REGSIZE_BYTES;
                        noway_assert(offset < byte.MaxValue);
                    }

                    Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_X86_ESP, REG_X86_EBP, -offset);
                }
            }
            genPopCalleeSavedRegistersXarch();
            if (Configuration.TARGET_AMD64)
            {
                if (_compiler.opts.IsOSR)
                {
                    var patchpoint = _compiler.info.compPatchpointInfo;
                    noway_assert(patchpoint is not null);
                    inst_RV_IV(INS_add, REG_X86_ESP,
                        patchpoint->TotalFrameSize + Configuration.TARGET_POINTER_SIZE, EA_PTRSIZE);
                }
                assert(!needMovEspEbp);
            }
            else if (needMovEspEbp)
            {
                inst_Mov(TYP_I_IMPL, REG_X86_ESP, REG_X86_EBP, canSkip: false);
            }
            inst_RV(INS_pop, REG_X86_EBP, TYP_I_IMPL);
        }

        Emitter.emitStartExitSeq();

        if (jmpEpilog)
        {
            noway_assert(block.Kind == BBJ_RETURN);
            var last = block.GetLastNode();
            if (last is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.MissingJMPEpilogNode);
                return;
            }
            if (!Configuration.FEATURE_FASTTAILCALL)
            {
                noway_assert(last.Oper == GT_JMP);
            }
            else
            {
                noway_assert((last.Oper == GT_JMP) ||
                    ((last.Oper == GT_CALL) && last.AsCall().IsFastTailCall));
            }

            if (last.Oper == GT_JMP)
            {
                if (Configuration.FEATURE_FASTTAILCALL)
                {
                    noway_assert(last.Next is null);
                }

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
                        parameters.ireg = REG_X64_RAX;
                        instGen_Set_Reg_To_ImmXarch(EA_PTRSIZE | EA_CNS_RELOC_FLG, REG_X64_RAX, (nint)lookup.addr);
                        _regSet.verifyRegUsed(REG_X64_RAX);
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
                if (Configuration.FEATURE_FASTTAILCALL)
                {
                    noway_assert(last.Oper == GT_CALL && last.AsCall().IsFastTailCall);
                    genCallInstructionXarch(last.AsCall());
                }
            }
        }
        else
        {
            var stackArgumentSize = 0u;
            if (Configuration.TARGET_X86)
            {
                var calleePop = !_compiler.info.compIsVarArgs && !IsCallerPop(_compiler.info.compCallConv);
                if (calleePop)
                {
                    stackArgumentSize = unchecked((uint)_compiler.lvaParameterStackSize);
                    noway_assert(stackArgumentSize < 0x10000);
                }
                if (Configuration.UNIX_X86_ABI &&
                    (_compiler.info.compCallConv == CorInfoCallConvExtension.C) &&
                    (_compiler.info.compRetBuffArg != BAD_VAR_NUM))
                {
                    stackArgumentSize += (uint)Configuration.TARGET_POINTER_SIZE;
                }
            }

            instGen_ReturnXarch(stackArgumentSize);
        }
    }

    public void instGen_ReturnXarch(uint stackArgumentSize)
    {
        if (Configuration.TARGET_XARCH)
        {
            if (stackArgumentSize == 0)
            {
                instGen(INS_ret);
            }
            else
            {
                inst_IV(INS_ret, unchecked((nint)stackArgumentSize));
            }
        }
        else if (Configuration.TARGET_ARM)
        {
            // ARM emits the return as part of the register-restoring epilog pop.
        }
        else if (Configuration.TARGET_ARM64)
        {
            unreached();
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.InstGenReturnIsNotUsedOnARM64);
            return;
        }
        else
        {
            NYI("instGen_Return");
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ReturnInstructionGenerationIsNotImplementedOnThisTarget
    );
            return;
        }
    }
}
