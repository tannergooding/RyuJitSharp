// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.EmitCallType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitIns_CallRiscV64(in EmitCallParams parameters)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "RISC-V call recording is only supported on RISC-V64.");
        }

        var compiler = _compiler
            ?? throw new FatalJitException("RISC-V call recording requires an active compiler.");

        if (parameters.callType is not EC_FUNC_TOKEN and not EC_INDIR_R)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                $"RISC-V call recording does not support call kind {parameters.callType}.");
        }

        assert(isGeneralRegister(parameters.ireg));
        assert((parameters.callType is EC_FUNC_TOKEN) || (parameters.addr == null));
        assert(parameters.xreg == REG_NA && parameters.xmul == 0 && parameters.disp == 0);

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

        nint jalrOffset = 0;
        if ((parameters.callType is EC_FUNC_TOKEN) && !IsAddressInRange(parameters.addr))
        {
            assert(parameters.addr is not null);
            var imm = (nint)parameters.addr;
            jalrOffset = unchecked((imm << (64 - 12)) >> (64 - 12));
            imm = unchecked(imm - jalrOffset);
            _ = emitLoadImmediate(true, EA_PTRSIZE, parameters.ireg, imm);
        }

        assert((parameters.argSize % Configuration.REGSIZE_BYTES) == 0);
        var argCnt = unchecked((int)(parameters.argSize / Configuration.REGSIZE_BYTES));
        instrDesc id;
        if (parameters.callType is EC_INDIR_R)
        {
            id = emitNewInstrCallInd(argCnt, parameters.disp, parameters.ptrVars, gcrefRegs, byrefRegs,
                parameters.retSize, parameters.secondRetSize,
                parameters.hasAsyncRet);
        }
        else
        {
            assert(parameters.callType is EC_FUNC_TOKEN);
            id = emitNewInstrCallDir(argCnt, parameters.ptrVars, gcrefRegs, byrefRegs, parameters.retSize,
                parameters.secondRetSize,
                parameters.hasAsyncRet);
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
        id.idIns(INS_jalr);
        id.idInsOpt(INS_OPTS_RISCV64_C);

        if ((parameters.callType is EC_INDIR_R) ||
            ((parameters.callType is EC_FUNC_TOKEN) && !IsAddressInRange(parameters.addr)))
        {
            id.idSetIsCallRegPtr();
            id.idReg4(parameters.isJump ? REG_RISCV64_R0 : REG_RISCV64_RA);
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

        if (Configuration.LATE_DISASM && (parameters.addr is not null))
        {
            codeGen.Disassembler.disSetMethod((nuint)parameters.addr, parameters.methHnd);
        }

        dispIns(id);
        appendToCurIG(id);
    }
}
