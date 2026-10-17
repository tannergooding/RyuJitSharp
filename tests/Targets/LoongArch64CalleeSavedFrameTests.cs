// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static class LoongArch64CalleeSavedFrameTests
{
    [Test]
    public static void CalleeSavedMaskIsRetainedForFuncletCapture()
    {
        Assume.That(Configuration.TARGET_LOONGARCH64);

        var calleeSavedMask = new regMaskTP(SRBM_LOONGARCH64_S0 | SRBM_LOONGARCH64_FP | SRBM_LOONGARCH64_RA);
        var regSet = default(RegSet);

        regSet.rsSetCalleeSavedRegsMask(calleeSavedMask);

        Assert.That(regSet.rsGetCalleeSavedRegsMask().Lower, Is.EqualTo(calleeSavedMask.Lower));
    }
}
