// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    internal static bool emitIns_valid_imm_for_ccmp(long immediate)
    {
        if (Configuration.TARGET_AMD64)
        {
            return FitsInI32(immediate);
        }

        if (Configuration.TARGET_ARM64)
        {
            return (immediate & 0x1f) == immediate;
        }

        FatalJitException.ThrowFatalJitException(
            CORJIT_INTERNALERROR,
        "Conditional compare immediates are only supported for x64 and ARM64.");
        return default!;
    }
}
