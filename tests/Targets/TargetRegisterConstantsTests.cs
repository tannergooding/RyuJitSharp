// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

#if TARGET_X86 || TARGET_WASM || TARGET_ARM || TARGET_ARM64
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
        Assert.That(Globals.SRBM_SPBASE_X86, Is.EqualTo(regMask.SRBM_X86_ESP));
#elif TARGET_WASM
        Assert.That(Globals.REG_OPT_RSVD, Is.EqualTo(Globals.REG_NA));
        Assert.That(Globals.SRBM_CALLEE_SAVED, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(Globals.SRBM_OPT_RSVD, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(Globals.SRBM_FPBASE, Is.EqualTo(regMask.SRBM_NONE));
        Assert.That(Globals.SRBM_SPBASE, Is.EqualTo(regMask.SRBM_NONE));
#elif TARGET_ARM
        Assert.That(Globals.MAX_REG_ARG, Is.EqualTo(4));
        Assert.That(Globals.REG_SHIFT_LNG, Is.EqualTo(REG_ARM32_R2));
        Assert.That(Globals.REG_WRITE_BARRIER_DST, Is.EqualTo(REG_ARM32_R0));
        Assert.That(Globals.REG_WRITE_BARRIER_SRC, Is.EqualTo(REG_ARM32_R1));
        Assert.That(Globals.REG_PINVOKE_SCRATCH, Is.EqualTo(REG_ARM32_R6));
        Assert.That(Globals.MAX_HFA_RET_SLOTS, Is.EqualTo(8));
        Assert.That(Globals.LBL_DIST_SMALL_MAX_POS, Is.EqualTo(1020));
        Assert.That(Globals.CALL_DIST_MAX_NEG, Is.EqualTo(-16777216));
        Assert.That(Globals.JCC_DIST_MEDIUM_MAX_POS, Is.EqualTo(1048574));
        Assert.That(Globals.SRBM_STACK_PROBE_HELPER_TRASH,
            Is.EqualTo(regMask.SRBM_ARM32_R5 | regMask.SRBM_ARM32_LR));
#elif TARGET_ARM64
        Assert.That(Globals.MAX_REG_ARG, Is.EqualTo(8));
        Assert.That(Globals.REG_PROFILER_ENTER_ARG_FUNC_ID_ARM64, Is.EqualTo(REG_ARM64_R10));
        Assert.That(Globals.REG_PROFILER_ENTER_ARG_CALLER_SP_ARM64, Is.EqualTo(REG_ARM64_R11));
        Assert.That(Globals.REG_ZERO_INIT_FRAME_REG1, Is.EqualTo(REG_ARM64_R9));
        Assert.That(Globals.REG_ZERO_INIT_FRAME_REG2, Is.EqualTo(REG_ARM64_R10));
        Assert.That(Globals.REG_ZERO_INIT_FRAME_SIMD, Is.EqualTo(REG_ARM64_V16));
        Assert.That(Globals.SRBM_ASYNC_CONTINUATION_RET_ARM64, Is.EqualTo(regMask.SRBM_ARM64_R2));
        Assert.That(Globals.SRBM_SVE_INDEXED_S_ELEMENT_ALLOWED_REGS,
            Is.EqualTo(regMask.SRBM_ARM64_V0 | regMask.SRBM_ARM64_V1 | regMask.SRBM_ARM64_V2 | regMask.SRBM_ARM64_V3 |
                regMask.SRBM_ARM64_V4 | regMask.SRBM_ARM64_V5 | regMask.SRBM_ARM64_V6 | regMask.SRBM_ARM64_V7));
        Assert.That(Globals.SRBM_SVE_INDEXED_D_ELEMENT_ALLOWED_REGS,
            Is.EqualTo(Globals.SRBM_ASIMD_INDEXED_H_ELEMENT_ALLOWED_REGS));
        Assert.That(Globals.LBL_DIST_SMALL_MAX_NEG_ARM64, Is.EqualTo(-1048576));
        Assert.That(Globals.JCC_SIZE_SMALL, Is.EqualTo(4));
        Assert.That(Globals.STACK_PROBE_BOUNDARY_THRESHOLD_BYTES, Is.EqualTo(512));
#endif
    }
}
#endif
