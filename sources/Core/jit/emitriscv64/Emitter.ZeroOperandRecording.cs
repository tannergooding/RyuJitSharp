// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns(instruction ins)
    {
        var id = emitNewInstr(EA_8BYTE);
        id.idIns(ins);
        id.idAddr().iiaInstrEncode = emitInsCode(ins);
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
