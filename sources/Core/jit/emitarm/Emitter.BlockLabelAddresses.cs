// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitIns_R_LArm32(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var format = IF_NONE;
        instrDescJmp id;
        switch (ins)
        {
            case INS_adr:
            {
                id = emitNewInstrLbl();
                format = IF_T2_M1;
                break;
            }

            case INS_movt:
            case INS_movw:
            {
                id = emitNewInstrJmp();
                format = IF_T2_N1;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert((format == IF_T2_M1) || (format == IF_T2_N1));
        var size = emitInsSize(format);

        id.idIns(ins);
        id.idReg1(reg);
        id.idInsFmt(format);
        id.idInsSize(size);

#if DEBUG
        if (_compiler.compCurBB.Kind == BBJ_EHCATCHRET)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idCatchRet = true;
        }
#endif

        id.idjTarget = dst;
        id.idjShort = false;

        if (ins == INS_adr)
        {
            id.idReg2(REG_PC);
            id.idjKeepLong = _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
        }
        else
        {
            id.idjKeepLong = true;
        }

        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;

        if (_compiler.opts.compReloc)
        {
            id.idSetRelocFlags(attr);
        }

#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
