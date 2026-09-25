// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public insGroup emitGetFirstPrologIG()
    {
        assert(emitIGlist is not null);
        assert((emitIGlist.igFlags & InsGroupFlags.Prolog) != 0);

        return emitIGlist;
    }

    public void emitIns_R_L(instruction ins, emitAttr attr, insGroup dst, regNumber reg)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction-group address recording requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(ins == INS_lea);
        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idReg1(reg);
        id.idInsFmt(IF_RWR_LABEL);
        id.idOpSize(EA_SIZE(attr));
        id.idjTargetIG = dst;
        id.idSetIsBound();
        id.idjShort = false;
        id.idjKeepLong = true;
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
#if DEBUG
        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        if (_compiler.compCurBB.Kind == BBJ_EHCATCHRET)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idCatchRet = true;
        }
#endif
#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif
        id.idSetRelocFlags(attr);
        var size = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }
}
