// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitIns_CallLoongArch64(in EmitCallParams parameters)
    {
        var compiler = _compiler
            ?? throw new FatalJitException("LoongArch64 call recording requires an active compiler.");

        assert(parameters.callType < EC_COUNT);
        assert(parameters.xreg == REG_NA && parameters.xmul == 0 && parameters.disp == 0);
        assert(parameters.callType == EC_INDIR_R || parameters.callType == EC_FUNC_TOKEN);
        assert(parameters.callType != EC_FUNC_TOKEN ||
            (parameters.ireg == REG_NA && parameters.addr != null));
        assert(parameters.callType != EC_INDIR_R ||
            (parameters.ireg < REG_COUNT && parameters.addr == null));

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

        assert((parameters.argSize % REGSIZE_BYTES) == 0);
        var argCount = unchecked((int)(parameters.argSize / REGSIZE_BYTES));
        instrDesc id;
        if (parameters.callType == EC_INDIR_R)
        {
            id = emitNewInstrCallInd(argCount, parameters.disp, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize,
#if MULTIREG_HAS_SECOND_GC_RET
                parameters.secondRetSize,
#endif
                parameters.hasAsyncRet);
        }
        else
        {
            id = emitNewInstrCallDir(argCount, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize,
#if MULTIREG_HAS_SECOND_GC_RET
                parameters.secondRetSize,
#endif
                parameters.hasAsyncRet);
        }

        if (parameters.retSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET);
        }
        else if (parameters.retSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET);
        }

#if MULTIREG_HAS_SECOND_GC_RET
        if (parameters.secondRetSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET_1);
        }
        else if (parameters.secondRetSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET_1);
        }
#endif

        VarSetOps.Assign(compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;

        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));
        id.idIns(INS_jirl);
        id.idInsOpt(INS_OPTS_C);

        if (parameters.callType == EC_INDIR_R)
        {
            id.idSetIsCallRegPtr();
            id.idReg4(parameters.isJump ? REG_R0 : REG_RA);
            id.idReg3(parameters.ireg);
            id.idCodeSize(4);
        }
        else
        {
            assert(parameters.addr != null);
            assert((unchecked((nuint)parameters.addr) & 3) == 0);

            id.idAddr().iiaAddr = (byte*)parameters.addr + (parameters.isJump ? 0 : 1);
            if (compiler.opts.compReloc)
            {
                id.idSetIsDspReloc();
                id.idCodeSize(8);
            }
            else
            {
                id.idCodeSize(16);
            }
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
            debugInfo.idMemCookie = unchecked((nint)parameters.methHnd);
        }

#if LATE_DISASM
        if (parameters.addr != null)
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }
#endif

        appendToCurIG(id);
    }
}
#endif
