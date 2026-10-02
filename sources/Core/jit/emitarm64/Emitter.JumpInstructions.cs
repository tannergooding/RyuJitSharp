// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitIns_JArm64(instruction ins, BasicBlock dst, bool keepShort)
    {
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_bl_local:
            case INS_b:
            {
                // Unconditional jumps have one form; assume long when crossing hot/cold sections.
                fmt = IF_BI_0A;
                break;
            }

            case INS_beq:
            case INS_bne:
            case INS_bhs:
            case INS_blo:
            case INS_bmi:
            case INS_bpl:
            case INS_bvs:
            case INS_bvc:
            case INS_bhi:
            case INS_bls:
            case INS_bge:
            case INS_blt:
            case INS_bgt:
            case INS_ble:
            {
                // Assume conditional jumps are long.
                fmt = IF_LARGEJMP;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idjTarget = dst;

        if (keepShort)
        {
            id.idjKeepLong = false;
            emitSetShortJump(id);
        }
        else
        {
            id.idjShort = false;
            assert(_compiler is not null);
            assert(_compiler.compCurBB is not null);
            id.idjKeepLong = (ins == INS_bl) || _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
#if DEBUG
            if (_compiler.opts.compLongAddress)
            {
                id.idjKeepLong = true;
            }
#endif
        }

#if DEBUG
        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        if ((ins == INS_bl_local) && (_compiler.compCurBB.Kind == BBJ_CALLFINALLY))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idFinallyCall = true;
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
