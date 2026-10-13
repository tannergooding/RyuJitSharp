// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal static class RegSetCalleeSavedRegisterTrackingExtensions
{
    internal static void rsSetCalleeSavedRegsMask(this ref RegSet regSet, regMaskTP mask)
    {
        if (!Configuration.TARGET_ARMARCH && !Configuration.TARGET_LOONGARCH64 &&
            !Configuration.TARGET_RISCV64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Callee-saved register masks are unavailable for this target.");
        }

        regSet.CalleeSavedRegsMask = mask;
    }

    internal static regMaskTP rsGetCalleeSavedRegsMask(this ref RegSet regSet)
    {
        if (!Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Callee-saved register masks are unavailable for this target.");
        }

        return regSet.CalleeSavedRegsMask;
    }
}
