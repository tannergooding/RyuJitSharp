// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.ARM32CallRecordingIsOnlySupportedOnARM32);
            return;
        }

        assert(_compiler is not null);
        assert(parameters.callType is EC_FUNC_TOKEN or EC_INDIR_R);
        assert((parameters.callType != EC_FUNC_TOKEN) ||
            ((parameters.addr != null) && (parameters.ireg == REG_ARM32_NA)));
        assert((parameters.callType != EC_INDIR_R) ||
            ((parameters.addr == null) && (parameters.ireg < REG_ARM32_COUNT)));
        assert((parameters.xreg == REG_ARM32_NA) && (parameters.xmul == 0) && (parameters.disp == 0));

        if (Configuration.DEBUG)
        {
            var argSize = unchecked((int)parameters.argSize);
            var absArgSize = argSize < 0 ? unchecked((uint)-argSize) : (uint)argSize;
            assert(absArgSize <= codeGen.getCurrentStackLevel());
        }

        var savedSet = emitGetGCRegsSavedOrModified(parameters.methHnd);
        var gcrefRegs = parameters.gcrefRegs & savedSet;
        var byrefRegs = parameters.byrefRegs & savedSet;

        if (Configuration.DEBUG && _compiler.verbose)
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

        assert((parameters.argSize % Configuration.REGSIZE_BYTES) == 0);
        var argCount = unchecked((int)(parameters.argSize / Configuration.REGSIZE_BYTES));
        var isIndirect = parameters.callType == EC_INDIR_R;
        var id = isIndirect
            ? emitNewInstrCallInd(argCount, 0, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.secondRetSize, parameters.hasAsyncRet)
            : emitNewInstrCallDir(argCount, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.secondRetSize, parameters.hasAsyncRet);

        if (parameters.retSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_ARM32_R0);
        }
        else if (parameters.retSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_ARM32_R0);
        }

        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;
        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));

        if (isIndirect)
        {
            id.idIns(parameters.isJump ? INS_bx : INS_blx);
            id.idInsFmt(IF_ARM32_T1_D2);
            id.idInsSize(emitInsSize(IF_ARM32_T1_D2));
            id.idReg3(parameters.ireg);
            assert(parameters.xreg == REG_ARM32_NA);
        }
        else
        {
            assert(parameters.callType == EC_FUNC_TOKEN);
            assert((parameters.addr == null) || codeGen.validImmForBL((nint)parameters.addr));

            id.idIns(parameters.isJump ? INS_b : INS_bl);
            id.idInsFmt(IF_ARM32_T2_J3);
            id.idInsSize(emitInsSize(IF_ARM32_T2_J3));
            id.idAddr().iiaAddr = (byte*)parameters.addr;

            if (_compiler.opts.compReloc)
            {
                id.idSetIsDspReloc();
            }
        }

        if (Configuration.DEBUG && _compiler.verbose && id.idIsLargeCall())
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            jitprintf($"[{debugInfo!.idNum:D2}] Rec call GC vars = " +
                $"{VarSetOps.ToString(_compiler, ((instrDescCGCA)id).idcGCvars)}\n");
        }

        if (_debugInfoSize > 0)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            if (Configuration.DEBUG)
            {
                debugInfo.idCallSig = parameters.sigInfo;
            }
            debugInfo.idMemCookie = (nint)parameters.methHnd;
        }

        if (Configuration.LATE_DISASM && (parameters.addr != null))
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }

        dispIns(id);
        appendToCurIG(id);
        emitLastMemBarrier = null;
    }
}
