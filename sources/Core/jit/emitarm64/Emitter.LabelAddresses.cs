// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_D(instruction ins, emitAttr attr, uint offs, regNumber reg)
    {
        NYI("emitIns_R_D");
    }

    private void emitIns_R_LArm64(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var format = ins switch
        {
            INS_adr => IF_LARGEADR,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Unexpected ARM64 label-address instruction {ins}."),
        };

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(format);
        id.idjShort = false;
        id.idjTarget = dst;
        id.idOpSize(EA_PTRSIZE);
        id.idReg1(reg);

        var compiler = _compiler ?? throw new InvalidOperationException("Emitter is not initialized.");
        assert(compiler.compCurBB is not null);
#if DEBUG
        if (compiler.compCurBB.Kind == BBJ_EHCATCHRET)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idCatchRet = true;
        }
#endif

        id.idjKeepLong = compiler.fgInDifferentRegions(compiler.compCurBB, dst);
#if DEBUG
        if (compiler.opts.compLongAddress)
        {
            id.idjKeepLong = true;
        }
#endif
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif
        dispIns(id);
        appendToCurIG(id);
    }

    private void emitIns_R_LArm64(instruction ins, emitAttr attr, insGroup dst, regNumber reg)
    {
        assert(dst is not null);

        var format = ins switch
        {
            INS_adr => IF_LARGEADR,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Unexpected ARM64 label-address instruction {ins}."),
        };

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(format);
        id.idjShort = false;
        id.idjTargetIG = dst;
        id.idSetIsBound();
        id.idReg1(reg);
        id.idOpSize(EA_PTRSIZE);
        id.idjKeepLong = false;
#if DEBUG
        assert(_compiler is not null);
        if (_compiler.opts.compLongAddress)
        {
            id.idjKeepLong = true;
        }
#endif
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif
        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
