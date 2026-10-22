// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    internal bool emitTryCoalesceLastMemBarrier(insBarrier barrier)
    {
        if (Configuration.TARGET_ARM)
        {
            return emitTryCoalesceLastMemBarrierArm32(barrier);
        }

        if (Configuration.TARGET_ARM64)
        {
            return emitTryCoalesceLastMemBarrierArm64(barrier);
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.MemoryBarrierCoalescingIsNotSupportedForThisTarget
    );
        return default!;
    }
}
