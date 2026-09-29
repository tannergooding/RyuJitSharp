// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isInsIGSafeForPeepholeOptimization(insGroup prevInsIG, insGroup curInsIG)
    {
        if (prevInsIG == curInsIG)
        {
            return true;
        }

        return ((curInsIG.igFlags & InsGroupFlags.Extend) != 0) &&
            ((prevInsIG.igFlags & InsGroupFlags.NoGCInterrupt) == (curInsIG.igFlags & InsGroupFlags.NoGCInterrupt));
    }

    private bool emitCanPeepholeLastIns()
    {
        assert(emitHasLastIns() == (emitLastInsIG is not null));

        if (!emitHasLastIns() || emitForceNewIG)
        {
            return false;
        }

        assert(emitLastInsIG is not null);
        assert(emitCurIG is not null);
        return isInsIGSafeForPeepholeOptimization(emitLastInsIG, emitCurIG);
    }
}
