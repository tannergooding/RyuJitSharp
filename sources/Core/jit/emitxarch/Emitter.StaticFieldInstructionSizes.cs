// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public uint emitInsSizeCV(instrDesc id, ulong code)
    {
        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var attrSize = id.idOpSize();

        // All addresses in an M format are assumed reachable by RIP-relative addressing.
        uint size = sizeof(int);
        size += emitGetAdjustedSize(id, code);
        var includeRexPrefixSize = true;

        if (TakesRexWPrefix(id) || IsExtendedReg(id.idReg1(), attrSize) || IsExtendedReg(id.idReg2(), attrSize))
        {
            size += emitGetRexPrefixSize(id, ins);
            includeRexPrefixSize = false;
        }

        return size + emitInsSize(id, code, includeRexPrefixSize);
    }
#endif
}
