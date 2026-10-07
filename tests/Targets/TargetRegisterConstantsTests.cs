// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

#if TARGET_X86 || TARGET_WASM
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class TargetRegisterConstantsTests
{
    [Test]
    public static void RetainedTargetRegisterConstantsMatchNativeValues()
    {
#if TARGET_X86
        Assert.That(Globals.SRBM_CALLEE_SAVED, Is.EqualTo(
            Globals.SRBM_INT_CALLEE_SAVED | Globals.SRBM_FLT_CALLEE_SAVED | Globals.SRBM_MSK_CALLEE_SAVED));
        Assert.That(Globals.SRBM_SPBASE, Is.EqualTo(regMask.SRBM_ESP));
#elif TARGET_WASM
        Assert.That(Globals.REG_OPT_RSVD, Is.EqualTo(REG_NA));
        Assert.That(Globals.SRBM_CALLEE_SAVED, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(Globals.SRBM_OPT_RSVD, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(Globals.SRBM_FPBASE, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(Globals.SRBM_SPBASE, Is.EqualTo(regMask.SRBM_NONE));
#endif
    }
}
#endif
