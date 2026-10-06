// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_J_R(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var format = IF_NONE;
        switch (ins)
        {
            case INS_cbz:
            case INS_cbnz:
            {
                format = IF_T1_I;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(format == IF_T1_I);
        assert(isLowRegister(reg));

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(IF_T1_I);
        id.idInsSize(emitInsSize(IF_T1_I));
        id.idReg1(reg);
        id.idjShort = true;
        id.idjTarget = dst;
        id.idjKeepLong = false;
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
