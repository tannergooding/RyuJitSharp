// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitIns_CallArm64(in EmitCallParams parameters)
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "ARM64 call recording is only supported on ARM64.");
        }

        assert(_compiler is not null);
        assert(parameters.callType is EC_FUNC_TOKEN or EC_INDIR_R);
        assert((parameters.callType != EC_FUNC_TOKEN) ||
            ((parameters.addr != null) && (parameters.ireg == REG_ARM64_NA)));
        assert((parameters.callType != EC_INDIR_R) ||
            ((parameters.addr == null) && (parameters.ireg < REG_ARM64_COUNT)));
        assert((parameters.xreg == REG_ARM64_NA) && (parameters.xmul == 0) && (parameters.disp == 0));

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

        assert((parameters.argSize % Configuration.REGSIZE_BYTES) == 0);
        var argCount = unchecked((int)(parameters.argSize / Configuration.REGSIZE_BYTES));
        var isIndirect = parameters.callType == EC_INDIR_R;
        var id = isIndirect
            ? emitNewInstrCallInd(argCount, 0, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.secondRetSize,
                parameters.hasAsyncRet)
            : emitNewInstrCallDir(argCount, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.secondRetSize,
                parameters.hasAsyncRet);

        if (parameters.retSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET_ARM64);
        }
        else if (parameters.retSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET_ARM64);
        }

        if (Configuration.MULTIREG_HAS_SECOND_GC_RET && parameters.secondRetSize == EA_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET_1_ARM64);
        }
        else if (Configuration.MULTIREG_HAS_SECOND_GC_RET && parameters.secondRetSize == EA_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET_1_ARM64);
        }

        VarSetOps.Assign(_compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;
        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));

        if (isIndirect)
        {
            id.idIns(parameters.isJump ? INS_br_tail : INS_blr);
            id.idInsFmt(IF_ARM64_BR_1B);
            if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && EA_IS_CNS_TLSGD_RELOC(parameters.retSize))
            {
                assert(parameters.ireg == REG_ARM64_R2);
                id.idSetTlsGD();
                id.idAddr().iiaAddr = (byte*)parameters.methHnd;
            }
            else
            {
                id.idReg3(parameters.ireg);
            }
        }
        else
        {
            assert(parameters.callType == EC_FUNC_TOKEN);
            assert(parameters.addr != null);

            id.idIns(parameters.isJump ? INS_b_tail : INS_bl);
            id.idInsFmt(IF_ARM64_BI_0C);
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

        if (Configuration.LATE_DISASM && (parameters.addr != null))
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }

        dispIns(id);
        appendToCurIG(id);
        emitLastMemBarrier = null;
    }
}
