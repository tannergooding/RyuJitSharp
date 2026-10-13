// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsRiscV64(instruction ins)
    {
        RequireRiscV64Target();
        var id = emitNewInstr(EA_8BYTE);
        id.idIns(ins);
        id.idAddr().iiaInstrEncode = emitInsCodeRiscV64(ins);
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }
}
