// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if DEBUG
    public string emitOffsetToLabel(uint offs)
    {
        var nextOffs = 0u;
        for (var group = emitIGlist; group is not null; group = group.igNext)
        {
            assert((nextOffs == group.igOffs) || (group == emitFirstColdIG));

            if (group.igOffs == offs)
            {
                return emitLabelString(group);
            }
            if (group.igOffs > offs)
            {
                return "UNKNOWN";
            }

            nextOffs = unchecked(group.igOffs + group.igSize);
        }

        return nextOffs == offs ? "END" : "UNKNOWN";
    }
#endif
}
