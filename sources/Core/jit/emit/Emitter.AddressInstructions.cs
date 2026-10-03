// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_Data16()
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "DATA16 recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        var id = emitNewInstrSmall(EA_1BYTE);
        id.idIns(INS_data16);
        id.idInsFmt(IF_NONE);
        id.idCodeSize(1);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + 1);
#endif
    }
}
