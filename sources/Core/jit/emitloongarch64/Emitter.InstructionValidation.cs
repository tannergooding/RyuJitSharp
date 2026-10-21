// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private static void EnsureLoongArch64Target()
    {
        if (!Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "LoongArch64 instruction validation is not available for this target.");
            return;
        }
    }

    public static bool isValidVectorDatasize(emitAttr size)
    {
        EnsureLoongArch64Target();
        EnsureLoongArch64Simd();

        return (size == EA_8BYTE) || (size == EA_16BYTE) || (size == EA_32BYTE);
    }

    public static bool isValidVectorElemsize(emitAttr size)
    {
        EnsureLoongArch64Target();
        EnsureLoongArch64Simd();

        return (size == EA_16BYTE) || (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE) ||
               (size == EA_1BYTE);
    }

    private static void EnsureLoongArch64Simd()
    {
        if (!Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "LoongArch64 vector validation is not available without SIMD support.");
            return;
        }
    }
}
