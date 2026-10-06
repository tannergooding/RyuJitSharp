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
    public void emitIns_R_D(instruction ins, emitAttr attr, uint offs, regNumber reg)
    {
        assert(_compiler is not null);
        noway_assert((ins == INS_movw) || (ins == INS_movt));

        var format = IF_T2_N2;
        var id = emitNewInstrSC(attr, unchecked((nint)offs));
        var size = emitInsSize(format);

        id.idIns(ins);
        id.idReg1(reg);
        id.idInsFmt(format);
        id.idInsSize(size);

        if (_compiler.opts.compReloc)
        {
            id.idSetRelocFlags(attr);
        }

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
