// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.insBarrier;

namespace RyuJitSharp;

public partial class Emitter
{
    internal bool emitTryCoalesceLastMemBarrier(insBarrier barrier)
    {
        assert(barrier == INS_BARRIER_SY);

        var lastMemBarrier = emitLastMemBarrier;
        if (lastMemBarrier is null)
        {
            return false;
        }

        assert((insBarrier)lastMemBarrier.idSmallCns() == INS_BARRIER_SY);
        return true;
    }
}
#endif
