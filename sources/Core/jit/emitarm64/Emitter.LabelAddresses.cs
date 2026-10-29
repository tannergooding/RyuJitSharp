// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

using static RyuJitSharp.Emitter.EmitterStatistics;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitIns_R_DArm64(instruction ins, emitAttr attr, uint offs, regNumber reg)
    {
        NYI("emitIns_R_D");
    }

    private void emitIns_R_LArm64(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        assert(dst.HasFlag(BBF_HAS_LABEL));

        insFormat format;
        switch (ins)
        {
            case INS_adr:
            {
                format = IF_ARM64_LARGEADR;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.UnexpectedARM64LabelAddressInstructionArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? ins : default)
    );
                return;
            }
        }

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(format);
        id.idjShort = false;
        id.idjTarget = dst;
        id.idOpSize(EA_PTRSIZE);
        id.idReg1(reg);

        var compiler = _compiler;
        if (compiler is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
        }
        assert(compiler.compCurBB is not null);
        if (Configuration.DEBUG && (compiler.compCurBB.Kind == BBJ_EHCATCHRET))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idCatchRet = true;
        }

        id.idjKeepLong = compiler.fgInDifferentRegions(compiler.compCurBB, dst);
        if (Configuration.DEBUG && compiler.compLongAddress)
        {
            id.idjKeepLong = true;
        }
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
        }
        dispIns(id);
        appendToCurIG(id);
    }

    private void emitIns_R_LArm64(instruction ins, emitAttr attr, insGroup dst, regNumber reg)
    {
        assert(dst is not null);

        insFormat format;
        switch (ins)
        {
            case INS_adr:
            {
                format = IF_ARM64_LARGEADR;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.UnexpectedARM64LabelAddressInstructionArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? ins : default)
    );
                return;
            }
        }

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(format);
        id.idjShort = false;
        id.idjTargetIG = dst;
        id.idSetIsBound();
        id.idReg1(reg);
        id.idOpSize(EA_PTRSIZE);
        id.idjKeepLong = false;
        if (Configuration.DEBUG)
        {
            assert(_compiler is not null);
        }
        if (Configuration.DEBUG && _compiler!.compLongAddress)
        {
            id.idjKeepLong = true;
        }
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
        }
        dispIns(id);
        appendToCurIG(id);
    }
}
