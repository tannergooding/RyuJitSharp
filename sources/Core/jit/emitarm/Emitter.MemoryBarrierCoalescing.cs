// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.insBarrier;

namespace RyuJitSharp;

public partial class Emitter
{
    private bool emitTryCoalesceLastMemBarrierArm32(insBarrier barrier)
    {
        if (!Configuration.TARGET_ARM)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "ARM32 memory-barrier coalescing is not available for this target.");
        }

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
