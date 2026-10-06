// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns(instruction ins)
    {
        var id = emitNewInstrSmall(EA_4BYTE);
        var format = emitInsFormat(ins);
        var size = emitInsSize(format);

        assert((format == IF_T1_A) || (format == IF_T2_A));

        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(size);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
