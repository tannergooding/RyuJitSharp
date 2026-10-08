// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_Call(in EmitCallParams parameters)
    {
#if TARGET_XARCH
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(_compiler is not null);
        var callType = parameters.callType;
        assert(callType < EC_COUNT);
        if (!_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            assert((callType is not EC_FUNC_TOKEN and not EC_FUNC_TOKEN_INDIR) ||
                ((parameters.addr != null) && (parameters.ireg == REG_NA) && (parameters.xreg == REG_NA) &&
                    (parameters.xmul == 0) && (parameters.disp == 0)));
        }
        assert((callType != EC_INDIR_R) || ((parameters.addr == null) && (parameters.ireg < REG_COUNT) &&
            (parameters.xreg == REG_NA) && (parameters.xmul == 0) && (parameters.disp == 0)));
        assert((callType != EC_INDIR_ARD) || (parameters.addr == null));

#if DEBUG
        var argSize = unchecked((int)parameters.argSize);
        var absArgSize = argSize < 0 ? unchecked((uint)-argSize) : (uint)argSize;
        assert(absArgSize <= codeGen.getCurrentStackLevel());
#endif
        var savedSet = emitGetGCRegsSavedOrModified(parameters.methHnd);
        var gcrefRegs = parameters.gcrefRegs & savedSet;
        var byrefRegs = parameters.byrefRegs & savedSet;

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"\t\t\t\t\t\t\tCall: GCvars={VarSetOps.ToString(_compiler, parameters.ptrVars)} ");
            dumpConvertedVarSet(_compiler, parameters.ptrVars);
            jitprintf(", gcrefRegs=");
            printRegMaskInt(gcrefRegs);
            emitDispRegSet(gcrefRegs);
            jitprintf(", byrefRegs=");
            printRegMaskInt(byrefRegs);
            emitDispRegSet(byrefRegs);
            jitprintf("\n");
        }
#endif
        assert((parameters.argSize % REGSIZE_BYTES) == 0);
        var argCnt = unchecked((int)(parameters.argSize / REGSIZE_BYTES));
        var useInd = callType is EC_INDIR_R or EC_INDIR_ARD;

        var id = useInd
            ? emitNewInstrCallInd(argCnt, parameters.disp, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize,
#if UNIX_AMD64_ABI
                parameters.secondRetSize,
#endif
                parameters.hasAsyncRet)
            : emitNewInstrCallDir(argCnt, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize,
#if UNIX_AMD64_ABI
                parameters.secondRetSize,
#endif
                parameters.hasAsyncRet);

        if (parameters.retSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET);
        }
        else if (parameters.retSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET);
        }
#if UNIX_AMD64_ABI
        if (parameters.secondRetSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_RDX);
        }
        else if (parameters.secondRetSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_RDX);
        }
#endif

        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;

        instruction ins;
        if (parameters.isJump)
        {
            ins = callType == EC_FUNC_TOKEN ? INS_l_jmp : INS_tail_i_jmp;
        }
        else
        {
            ins = INS_call;
        }
        id.idIns(ins);
        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));

        uint size;
        if (useInd)
        {
            if (callType == EC_INDIR_R)
            {
                id.idSetIsCallRegPtr();
            }
            id.idInsFmt(emitInsModeFormat(ins, IF_ARD));
            id.idAddr().iiaAddrMode.amBaseReg = parameters.ireg;
            id.idAddr().iiaAddrMode.amIndxReg = parameters.xreg;
            id.idAddr().iiaAddrMode.amScale = (uint)(parameters.xmul != 0
                ? emitEncodeScale(parameters.xmul) : opSize.OPSZ1);

            ulong code = insCodeMR(ins);
            if (ins == INS_tail_i_jmp)
            {
                // The unwinder recognizes the REX.W-prefixed indirect tail jump as an epilog.
#if TARGET_X86
                if (!hasEvexPrefix(code) && !hasVexPrefix(code))
                {
                    assert(false);
                }
                else
#endif
                {
                    code = AddRexWPrefix(id, code);
                }
            }
            size = emitInsSizeAM(id, code);

            if ((parameters.ireg == REG_NA) && (parameters.xreg == REG_NA))
            {
                if (codeGen.genCodeIndirAddrNeedsReloc(unchecked((nuint)parameters.disp)))
                {
                    id.idSetIsDspReloc();
                }
#if TARGET_AMD64
                else
                {
                    noway_assert(unchecked((nuint)(nint)(int)(nint)parameters.addr) == (nuint)parameters.addr);
                    size++;
                }
#endif
            }
        }
        else if (callType == EC_FUNC_TOKEN_INDIR)
        {
            assert(parameters.addr != null);
            id.idInsFmt(IF_METHPTR);
            id.idAddr().iiaAddr = (byte*)parameters.addr;
            size = 6;

            if (TakesRex2Prefix(id))
            {
                size += 2;
            }
            if (codeGen.genCodeIndirAddrNeedsReloc((nuint)parameters.addr))
            {
                id.idSetIsDspReloc();
            }
#if TARGET_AMD64
            else
            {
                noway_assert(unchecked((nuint)(nint)(int)(nint)parameters.addr) == (nuint)parameters.addr);
                size++;
            }
#endif
        }
        else
        {
            assert(callType == EC_FUNC_TOKEN);
            assert((parameters.addr != null) || _compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI));
            id.idInsFmt(IF_METHOD);
            id.idAddr().iiaAddr = (byte*)parameters.addr;
            size = 5;

            if (codeGen.genCodeAddrNeedsReloc((nuint)parameters.addr))
            {
                id.idSetIsDspReloc();
                if ((nuint)parameters.methHnd == 1)
                {
                    id.idSetTlsGD();
                    size++;
                }
            }
        }

        if (_debugInfoSize > 0)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
#if DEBUG
            debugInfo.idCallSig = parameters.sigInfo;
#endif
            debugInfo.idMemCookie = (nint)parameters.methHnd;
        }

#if LATE_DISASM
        if (parameters.addr != null)
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }
#endif
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);

#if !FEATURE_FIXED_OUT_ARGS
        if (emitCntStackDepth != 0 && parameters.argSize > 0)
        {
            noway_assert(unchecked((nint)(uint)emitCurStackLvl) >= parameters.argSize);
            emitCurStackLvl = unchecked(emitCurStackLvl - (int)parameters.argSize);
            assert(emitCurStackLvl >= 0);
        }
#endif
#elif TARGET_WASM
        assert(parameters.wasmSignature != null);

        instruction wasmInstruction;
        instrDesc id;

        switch (parameters.callType)
        {
            case EC_FUNC_TOKEN:
            {
                wasmInstruction = parameters.isJump ? INS_return_call : INS_call;
                id = emitNewInstrSC(EA_HANDLE_CNS_RELOC, unchecked((nint)parameters.addr));
                id.idInsFmt(IF_FUNCIDX);
                break;
            }

            case EC_INDIR_R:
            {
                wasmInstruction = parameters.isJump ? INS_return_call_indirect : INS_call_indirect;
                id = emitNewInstrSC(EA_HANDLE_CNS_RELOC, unchecked((nint)parameters.wasmSignature));
                id.idInsFmt(IF_CALL_INDIRECT);
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR,
                    $"Unexpected Wasm call type {parameters.callType}.");
            }
        }

        id.idIns(wasmInstruction);

        if ((_debugInfoSize > 0) && (id.idDebugOnlyInfo() is { } debugInfo))
        {
#if DEBUG
            debugInfo.idCallSig = parameters.sigInfo;
#endif
            debugInfo.idMemCookie = unchecked((nint)parameters.methHnd);
            debugInfo.idFlags = GTF_ICON_METHOD_HDL;
        }

        dispIns(id);
        appendToCurIG(id);
#elif TARGET_ARM
        emitIns_CallArm32(in parameters);
#elif TARGET_ARM64
        emitIns_CallArm64(in parameters);
#elif TARGET_RISCV64
        emitIns_CallRiscV64(in parameters);
#elif TARGET_LOONGARCH64
        emitIns_CallLoongArch64(in parameters);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Call instruction recording requires xarch.");
#endif
    }

#if TARGET_AMD64
    private static ulong AddRexWPrefix(instrDesc id, ulong code)
    {
        if (hasEvexPrefix(code))
        {
            return code | 0x0000800000000000UL;
        }
        else if (hasVexPrefix(code))
        {
            return code | 0x00008000000000UL;
        }
        else if (hasRex2Prefix(code))
        {
            return code | 0x000800000000UL;
        }

        return code | 0x4800000000UL;
    }
#endif
}
