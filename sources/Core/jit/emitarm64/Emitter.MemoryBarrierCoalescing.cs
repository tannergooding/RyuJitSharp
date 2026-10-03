// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insBarrier;

namespace RyuJitSharp;

public partial class Emitter
{
    internal bool emitTryCoalesceLastMemBarrier(insBarrier barrier)
    {
        // The emitter clears this reference on memory accesses and instruction-group changes.
        var lastMemBarrier = emitLastMemBarrier;
        if (lastMemBarrier is null)
        {
            return false;
        }

        var previousBarrier = (insBarrier)lastMemBarrier.idSmallCns();
        if (previousBarrier == INS_BARRIER_ISHLD)
        {
            if (barrier == INS_BARRIER_ISH)
            {
                lastMemBarrier.idSmallCns((nint)(uint)INS_BARRIER_ISH);
            }
        }
        else
        {
            assert(previousBarrier == INS_BARRIER_ISH);
        }

        return true;
    }
}
#endif
