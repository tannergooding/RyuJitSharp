// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindPadding()
    {
        if (!Configuration.TARGET_ARMARCH && !Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Target prolog unwind padding is not ported.");
            return;
        }

        if (generateCFIUnwindCodes())
        {
            return;
        }

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        var currentLocation = unwindInfo.GetCurrentEmitterLocation();
        noway_assert(currentLocation.HasValue);
        GetEmitter().emitUnwindNopPadding(currentLocation.GetValueOrDefault(), this);
    }
}
