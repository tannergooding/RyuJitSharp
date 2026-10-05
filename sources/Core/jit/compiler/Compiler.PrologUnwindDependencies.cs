// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH || TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindPadding()
    {
#if TARGET_ARMARCH
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            return;
        }
#endif

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        var currentLocation = unwindInfo.GetCurrentEmitterLocation();
        noway_assert(currentLocation.HasValue);
        GetEmitter().emitUnwindNopPadding(currentLocation.GetValueOrDefault(), this);
#elif TARGET_LOONGARCH64
#if FEATURE_CFI_SUPPORT
        if (generateCFIUnwindCodes())
        {
            return;
        }
#endif

        var unwindInfo = funCurrentFunc().GetUnwindInfo();
        var currentLocation = unwindInfo.GetCurrentEmitterLocation();
        noway_assert(currentLocation.HasValue);
        GetEmitter().emitUnwindNopPadding(currentLocation.GetValueOrDefault(), this);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Target prolog unwind padding is not ported.");
#endif
    }
}
#endif
