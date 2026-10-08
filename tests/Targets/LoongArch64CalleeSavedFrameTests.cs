// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static class LoongArch64CalleeSavedFrameTests
{
    [Test]
    public static void CalleeSavedMaskIsRetainedForFuncletCapture()
    {
        var calleeSavedMask = new regMaskTP(SRBM_S0 | SRBM_FP | SRBM_RA);
        var regSet = default(RegSet);

        regSet.rsSetCalleeSavedRegsMask(calleeSavedMask);

        Assert.That(regSet.rsGetCalleeSavedRegsMask().Lower, Is.EqualTo(calleeSavedMask.Lower));
    }
}
#endif
