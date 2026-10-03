// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public void emitMarkStackLvl(uint stackLevel)
    {
        assert(unchecked((int)stackLevel) >= 0);
        assert(emitCurStackLvl == 0);
        var group = emitCurIG ?? throw new FatalJitException("A current instruction group is required.");
        assert(group.igStkLvl == 0);
        assert(emitCurIGfreeNext == 0);
        assert(emitCurIGfreeBase is not null && emitCurIGfreeBase.Count == 0);
        assert(stackLevel != 0 && stackLevel % sizeof(int) == 0);

        emitCurStackLvl = unchecked((int)stackLevel);
        group.igStkLvl = stackLevel;

        if (emitMaxStackDepth < emitCurStackLvl)
        {
            JITDUMP($"Upping emitMaxStackDepth from {emitMaxStackDepth} to {emitCurStackLvl}\n");
            emitMaxStackDepth = emitCurStackLvl;
        }
    }
}
#endif
