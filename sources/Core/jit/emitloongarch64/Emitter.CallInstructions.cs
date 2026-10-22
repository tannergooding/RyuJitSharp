// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        if (!Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64CallRecordingIsOnlySupportedOnLoongArch64
    );
            return;
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64CallRecordingRequiresAnActiveCompiler);
            return;
        }

        assert(parameters.xreg == REG_LOONGARCH64_NA && parameters.xmul == 0 && parameters.disp == 0);
        assert(parameters.callType is EC_INDIR_R or EC_FUNC_TOKEN);
        assert(parameters.callType != EC_FUNC_TOKEN ||
            (parameters.ireg == REG_LOONGARCH64_NA && parameters.addr != null));
        assert(parameters.callType != EC_INDIR_R ||
            (parameters.ireg < REG_LOONGARCH64_COUNT && parameters.addr == null));

        var argSize = parameters.argSize;
        var absArgSize = argSize < 0 ? unchecked((nuint)(-argSize)) : (nuint)argSize;
        assert(absArgSize <= (nuint)codeGen.getCurrentStackLevel());

        var savedSet = emitGetGCRegsSavedOrModified(parameters.methHnd);
        var gcrefRegs = parameters.gcrefRegs & savedSet;
        var byrefRegs = parameters.byrefRegs & savedSet;

        if (Configuration.DEBUG && compiler.verbose)
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

        assert((parameters.argSize % Configuration.REGSIZE_BYTES) == 0);
        var argCount = unchecked((int)(parameters.argSize / Configuration.REGSIZE_BYTES));
        instrDesc id;
        if (parameters.callType == EC_INDIR_R)
        {
            id = emitNewInstrCallInd(argCount, parameters.disp, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize, parameters.secondRetSize, parameters.hasAsyncRet);
        }
        else
        {
            id = emitNewInstrCallDir(argCount, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize, parameters.secondRetSize, parameters.hasAsyncRet);
        }

        if (parameters.retSize == EA_GCREF)
        {
            gcrefRegs |= TargetRuntimeMetadata.IntegerReturnRegisterMask;
        }
        else if (parameters.retSize == EA_BYREF)
        {
            byrefRegs |= TargetRuntimeMetadata.IntegerReturnRegisterMask;
        }

        if (Configuration.MULTIREG_HAS_SECOND_GC_RET && parameters.secondRetSize == EA_GCREF)
        {
            gcrefRegs |= TargetRuntimeMetadata.IntegerReturnRegister1Mask;
        }
        else if (Configuration.MULTIREG_HAS_SECOND_GC_RET && parameters.secondRetSize == EA_BYREF)
        {
            byrefRegs |= TargetRuntimeMetadata.IntegerReturnRegister1Mask;
        }

        VarSetOps.Assign(compiler, ref emitThisGCrefVars, parameters.ptrVars);
        emitThisGCrefRegs = (regMask)gcrefRegs;
        emitThisByrefRegs = (regMask)byrefRegs;

        id.idSetIsNoGC(parameters.isJump || parameters.noSafePoint || emitNoGChelper(parameters.methHnd));
        id.idIns(INS_jirl);
        id.idInsOpt(INS_OPTS_LOONGARCH64_C);

        if (parameters.callType == EC_INDIR_R)
        {
            id.idSetIsCallRegPtr();
            id.idReg4(parameters.isJump ? REG_LOONGARCH64_R0 : REG_LOONGARCH64_RA);
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

        if (Configuration.DEBUG)
        {
            if (compiler.verbose && id.idIsLargeCall())
            {
                var debugInfo = id.idDebugOnlyInfo();
                assert(debugInfo is not null);
                jitprintf($"[{debugInfo.idNum:D2}] Rec call GC vars = " +
                    $"{VarSetOps.ToString(compiler, ((instrDescCGCA)id).idcGCvars)}\n");
            }

            if (_debugInfoSize > 0)
            {
                var debugInfo = id.idDebugOnlyInfo();
                assert(debugInfo is not null);
                debugInfo.idCallSig = parameters.sigInfo;
                debugInfo.idMemCookie = unchecked((nint)parameters.methHnd);
            }

            if (Configuration.LATE_DISASM && (parameters.addr != null))
            {
                codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
            }
        }

        appendToCurIG(id);
    }
}
