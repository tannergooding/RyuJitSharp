// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class Arm64RegisterMetadataTests
{
    [Test]
    public static void RegisterClassBoundsAndSizesMatchNative()
    {
        Assert.Multiple(() => {
            Assert.That(REG_INT_FIRST, Is.EqualTo(REG_R0));
            Assert.That(REG_INT_LAST, Is.EqualTo(REG_ZR));
            Assert.That(REG_INT_COUNT, Is.EqualTo(32));
            Assert.That(REG_FP_FIRST, Is.EqualTo(REG_V0));
            Assert.That(REG_FP_LAST, Is.EqualTo(REG_V31));
            Assert.That(REG_MASK_FIRST, Is.EqualTo(REG_P0));
            Assert.That(REG_MASK_LAST, Is.EqualTo(REG_P15));
            Assert.That(REG_PREDICATE_HIGH_FIRST, Is.EqualTo(REG_P8));
            Assert.That(REG_NEXT(REG_R0), Is.EqualTo(REG_R1));
            Assert.That(REG_PREV(REG_R1), Is.EqualTo(REG_R0));
            Assert.That(REGNUM_BITS, Is.EqualTo(7));
            Assert.That(REGSIZE_BYTES, Is.EqualTo(8));
            Assert.That(FPSAVE_REGSIZE_BYTES, Is.EqualTo(8));
            Assert.That(CODE_ALIGN, Is.EqualTo(4));
            Assert.That(STACK_ALIGN, Is.EqualTo(16));
        });
    }

    [Test]
    public static void RegisterMasksRetainSeparatePredicateBank()
    {
        Assert.Multiple(() => {
            Assert.That(SRBM_INT_CALLEE_SAVED & SRBM_R19, Is.EqualTo(SRBM_R19));
            Assert.That(SRBM_INT_CALLEE_SAVED & SRBM_R18, Is.EqualTo(SRBM_NONE));
            Assert.That(SRBM_INT_CALLEE_TRASH & SRBM_LR, Is.EqualTo(SRBM_LR));
            Assert.That(SRBM_INT_CALLEE_TRASH & SRBM_FP, Is.EqualTo(SRBM_NONE));
            Assert.That(SRBM_FLT_CALLEE_SAVED & SRBM_V8, Is.EqualTo(SRBM_V8));
            Assert.That(SRBM_FLT_CALLEE_TRASH & SRBM_V16, Is.EqualTo(SRBM_V16));
            Assert.That(SRBM_MSK_CALLEE_TRASH, Is.EqualTo(SRBM_ALLMASK));
            Assert.That(SRBM_CALLEE_TRASH.Lower,
                Is.EqualTo(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH));
            Assert.That(SRBM_CALLEE_TRASH.Upper, Is.EqualTo(SRBM_ALLMASK));
            Assert.That(SRBM_VALIDATE_INDIRECT_CALL_TRASH & SRBM_R15, Is.EqualTo(SRBM_NONE));
            Assert.That(SRBM_VALIDATE_INDIRECT_CALL_TRASH & SRBM_R9, Is.EqualTo(SRBM_R9));
        });
    }

    [Test]
    public static void RegisterAllocationOrderAndCallingConventionMatchNative()
    {
        Assert.Multiple(() => {
            Assert.That(REG_VAR_ORDER.Length, Is.EqualTo(29));
            Assert.That(REG_VAR_ORDER[0], Is.EqualTo(REG_R0));
            Assert.That(REG_VAR_ORDER[14], Is.EqualTo(REG_R12));
            Assert.That(REG_VAR_ORDER[^1], Is.EqualTo(REG_LR));
            Assert.That(REG_VAR_ORDER_FLT.Length, Is.EqualTo(32));
            Assert.That(REG_VAR_ORDER_FLT[0], Is.EqualTo(REG_V16));
            Assert.That(REG_VAR_ORDER_FLT[^1], Is.EqualTo(REG_V0));
            Assert.That(SRBM_ARG_REGS, Is.EqualTo(
                SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 |
                SRBM_R4 | SRBM_R5 | SRBM_R6 | SRBM_R7));
            Assert.That(REG_INTRET_1, Is.EqualTo(REG_R1));
            Assert.That(REG_FLOATRET, Is.EqualTo(REG_V0));
            Assert.That(CNT_CALLEE_SAVED, Is.EqualTo(11));
            Assert.That(CNT_CALLEE_TRASH, Is.EqualTo(17));
            Assert.That(CNT_CALLEE_TRASH_FLOAT, Is.EqualTo(24));
            Assert.That(CNT_CALLEE_TRASH_MASK, Is.EqualTo(8));
        });
    }
}
#endif
