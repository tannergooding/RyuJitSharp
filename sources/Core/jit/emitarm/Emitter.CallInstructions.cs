// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitIns_CallArm32(in EmitCallParams parameters)
    {
        assert(_compiler is not null);
        assert(parameters.callType < EC_COUNT);
        assert((parameters.callType != EC_FUNC_TOKEN) ||
            ((parameters.addr != null) && (parameters.ireg == REG_NA)));
        assert((parameters.callType != EC_INDIR_R) ||
            ((parameters.addr == null) && (parameters.ireg < REG_COUNT)));
        assert((parameters.xreg == REG_NA) && (parameters.xmul == 0) && (parameters.disp == 0));

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
        var argCount = unchecked((int)(parameters.argSize / REGSIZE_BYTES));
        var isIndirect = parameters.callType == EC_INDIR_R;
        var id = isIndirect
            ? emitNewInstrCallInd(argCount, 0, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.hasAsyncRet)
            : emitNewInstrCallDir(argCount, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.hasAsyncRet);

        if (parameters.retSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_R0);
        }
        else if (parameters.retSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_R0);
        }

        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;
        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));

        if (isIndirect)
        {
            id.idIns(parameters.isJump ? INS_bx : INS_blx);
            id.idInsFmt(IF_T1_D2);
            id.idInsSize(emitInsSize(IF_T1_D2));
            id.idReg3(parameters.ireg);
            assert(parameters.xreg == REG_NA);
        }
        else
        {
            assert(parameters.callType == EC_FUNC_TOKEN);
            assert((parameters.addr == null) || codeGen.validImmForBL((nint)parameters.addr));

            id.idIns(parameters.isJump ? INS_b : INS_bl);
            id.idInsFmt(IF_T2_J3);
            id.idInsSize(emitInsSize(IF_T2_J3));
            id.idAddr().iiaAddr = (byte*)parameters.addr;

            if (_compiler.opts.compReloc)
            {
                id.idSetIsDspReloc();
            }
        }

#if DEBUG
        if (_compiler.verbose && id.idIsLargeCall())
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            jitprintf($"[{debugInfo.idNum:D2}] Rec call GC vars = " +
                $"{VarSetOps.ToString(_compiler, ((instrDescCGCA)id).idcGCvars)}\n");
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
        if (parameters.addr != null)
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }
#endif

        dispIns(id);
        appendToCurIG(id);
        emitLastMemBarrier = null;
    }
}
#endif
