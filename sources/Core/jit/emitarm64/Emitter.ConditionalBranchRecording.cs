// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitIns_J_RArm64(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        assert(ins is INS_cbz or INS_cbnz);
        assert(dst is not null);
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(IF_ARM64_LARGEJMP);
        id.idReg1(reg);
        id.idjShort = false;
        id.idOpSize(EA_SIZE(attr));
        id.idjTarget = dst;

        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        id.idjKeepLong = _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
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

    public void emitIns_J_R_I(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg, int imm)
    {
        assert(ins is INS_tbz or INS_tbnz);
        assert(dst is not null);
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var size = EA_SIZE(attr);
        assert((size == EA_4BYTE) || (size == EA_8BYTE));
        assert(imm < (size == EA_4BYTE ? 32 : 64));

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(IF_ARM64_LARGEJMP);
        id.idReg1(reg);
        id.idjShort = false;
        id.idSmallCns(imm);
        id.idOpSize(size);
        id.idjTarget = dst;

        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        id.idjKeepLong = _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
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
