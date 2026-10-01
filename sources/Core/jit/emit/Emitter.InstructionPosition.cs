// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitFindInsNum(insGroup ig, instrDesc? idMatch)
    {
        var storage = ig.igData;
        var id = storage is not null ? emitFirstInstrDesc(storage) : null;
        if (ReferenceEquals(id, idMatch))
        {
            return 0;
        }

        uint insNum = 0;
        uint insRemaining = ig.igInsCnt;
        while (insRemaining > 0)
        {
            assert(id is not null);
            var size = (nuint)emitSizeOfInsDsc(id);
            emitAdvanceInstrDesc(ref id, size);
            insNum++;
            insRemaining--;
            if (ReferenceEquals(id, idMatch))
            {
                return insNum;
            }
        }

        assert(false, "!\"emitFindInsNum failed\"");
        return uint.MaxValue;
    }
}
