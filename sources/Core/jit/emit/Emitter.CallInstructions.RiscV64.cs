// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitIns_CallRiscV64(in EmitCallParams parameters)
    {
        var compiler = _compiler
            ?? throw new FatalJitException("RISC-V call recording requires an active compiler.");

        assert(parameters.callType < EC_COUNT);
        assert(isGeneralRegister(parameters.ireg));
        assert((parameters.callType < EC_INDIR_R) || (parameters.addr == null));
        assert(parameters.xreg == REG_NA && parameters.xmul == 0 && parameters.disp == 0);

        var argSize = parameters.argSize;
        var absArgSize = argSize < 0 ? unchecked((nuint)(-argSize)) : (nuint)argSize;
        assert(absArgSize <= (nuint)codeGen.getCurrentStackLevel());

        var savedSet = emitGetGCRegsSavedOrModified(parameters.methHnd);
        var gcrefRegs = parameters.gcrefRegs & savedSet;
        var byrefRegs = parameters.byrefRegs & savedSet;

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"Call: GCvars={VarSetOps.ToString(compiler, parameters.ptrVars)} ");
            dumpConvertedVarSet(compiler, parameters.ptrVars);
            jitprintf(", gcrefRegs=");
            printRegMaskInt(gcrefRegs);
            emitDispRegSet(gcrefRegs);
            jitprintf(", byrefRegs=");
            printRegMaskInt(byrefRegs);
            emitDispRegSet(byrefRegs);
            jitprintf("\n");
        }
#endif

        nint jalrOffset = 0;
        if ((parameters.callType is EC_FUNC_TOKEN) && !IsAddressInRange(parameters.addr))
        {
            assert(parameters.addr is not null);
            var imm = (nint)parameters.addr;
            jalrOffset = unchecked((imm << (64 - 12)) >> (64 - 12));
            imm = unchecked(imm - jalrOffset);
            _ = emitLoadImmediate(true, EA_PTRSIZE, parameters.ireg, imm);
        }

        assert((parameters.argSize % REGSIZE_BYTES) == 0);
        var argCnt = unchecked((int)(parameters.argSize / REGSIZE_BYTES));
        instrDesc id;
        if (parameters.callType >= EC_INDIR_R)
        {
            assert(parameters.callType is EC_INDIR_R);
            id = emitNewInstrCallInd(argCnt, parameters.disp, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize,
#if MULTIREG_HAS_SECOND_GC_RET
                parameters.secondRetSize,
#endif
                parameters.hasAsyncRet);
        }
        else
        {
            assert(parameters.callType is EC_FUNC_TOKEN);
            id = emitNewInstrCallDir(argCnt, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
#if MULTIREG_HAS_SECOND_GC_RET
                parameters.secondRetSize,
#endif
                parameters.hasAsyncRet);
        }

        if (parameters.retSize is EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET);
        }
        else if (parameters.retSize is EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET);
        }
#if MULTIREG_HAS_SECOND_GC_RET
        if (parameters.secondRetSize is EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET_1);
        }
        else if (parameters.secondRetSize is EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET_1);
        }
#endif

        VarSetOps.Assign(compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;

        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));
        id.idIns(INS_jalr);
        id.idInsOpt(INS_OPTS_C);

        if ((parameters.callType is EC_INDIR_R) ||
            ((parameters.callType is EC_FUNC_TOKEN) && !IsAddressInRange(parameters.addr)))
        {
            id.idSetIsCallRegPtr();
            id.idReg4(parameters.isJump ? REG_R0 : REG_RA);
            id.idReg3(parameters.ireg);
            id.idSmallCns(jalrOffset);
            id.idCodeSize(4);
        }
        else
        {
            assert(parameters.callType is EC_FUNC_TOKEN);
            assert(parameters.addr is not null);
            assert(IsAddressInRange(parameters.addr));

            id.idAddr().iiaAddr = (byte*)parameters.addr + (parameters.isJump ? 0 : 1);
            id.idCodeSize(8);
            id.idSetIsDspReloc();
        }

#if DEBUG
        if (compiler.verbose && id.idIsLargeCall())
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            jitprintf($"[{debugInfo.idNum:D2}] Rec call GC vars = " +
                $"{VarSetOps.ToString(compiler, ((instrDescCGCA)id).idcGCvars)}\n");
        }
#endif

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
        if (parameters.addr is not null)
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }
#endif

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
