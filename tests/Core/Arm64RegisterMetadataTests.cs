// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

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
        Assume.That(Configuration.TARGET_ARM64);

        Assert.Multiple(() => {
            Assert.That(REG_INT_FIRST, Is.EqualTo(REG_ARM64_R0));
            Assert.That(REG_INT_LAST_ARM64, Is.EqualTo(REG_ARM64_ZR));
            Assert.That(REG_INT_COUNT_ARM64, Is.EqualTo(32));
            Assert.That(REG_FP_FIRST, Is.EqualTo(REG_ARM64_V0));
            Assert.That(REG_FP_LAST, Is.EqualTo(REG_ARM64_V31));
            Assert.That(REG_MASK_FIRST, Is.EqualTo(REG_ARM64_P0));
            Assert.That(REG_MASK_LAST, Is.EqualTo(REG_ARM64_P15));
            Assert.That(REG_PREDICATE_HIGH_FIRST, Is.EqualTo(REG_ARM64_P8));
            Assert.That(REG_NEXT(REG_ARM64_R0), Is.EqualTo(REG_ARM64_R1));
            Assert.That(REG_PREV(REG_ARM64_R1), Is.EqualTo(REG_ARM64_R0));
            Assert.That(REGNUM_BITS_ARM64, Is.EqualTo(7));
            Assert.That(REGSIZE_BYTES, Is.EqualTo(8));
            Assert.That(FPSAVE_REGSIZE_BYTES_ARM64, Is.EqualTo(8));
            Assert.That(CODE_ALIGN_ARM64, Is.EqualTo(4));
            Assert.That(STACK_ALIGN_ARM64, Is.EqualTo(16));
        });
    }

    [Test]
    public static void RegisterMasksRetainSeparatePredicateBank()
    {
        Assume.That(Configuration.TARGET_ARM64);

        Assert.Multiple(() => {
            Assert.That(SRBM_INT_CALLEE_SAVED & SRBM_ARM64_R19, Is.EqualTo(SRBM_ARM64_R19));
            Assert.That(SRBM_INT_CALLEE_SAVED & SRBM_ARM64_R18, Is.EqualTo(SRBM_NONE));
            Assert.That(SRBM_INT_CALLEE_TRASH & SRBM_ARM64_LR, Is.EqualTo(SRBM_ARM64_LR));
            Assert.That(SRBM_INT_CALLEE_TRASH & SRBM_ARM64_FP, Is.EqualTo(SRBM_NONE));
            Assert.That(SRBM_FLT_CALLEE_SAVED & SRBM_ARM64_V8, Is.EqualTo(SRBM_ARM64_V8));
            Assert.That(SRBM_FLT_CALLEE_TRASH & SRBM_ARM64_V16, Is.EqualTo(SRBM_ARM64_V16));
            Assert.That(SRBM_MSK_CALLEE_TRASH_ARM64, Is.EqualTo(SRBM_ALLMASK));
            Assert.That(SRBM_CALLEE_TRASH_ARM64.Lower,
                Is.EqualTo(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH));
            Assert.That(SRBM_CALLEE_TRASH_ARM64.Upper, Is.EqualTo(SRBM_ALLMASK));
            Assert.That(SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM64 & SRBM_ARM64_R15, Is.EqualTo(SRBM_NONE));
            Assert.That(SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM64 & SRBM_ARM64_R9, Is.EqualTo(SRBM_ARM64_R9));
        });
    }

    [Test]
    public static void RegisterAllocationOrderAndCallingConventionMatchNative()
    {
        Assume.That(Configuration.TARGET_ARM64);

        Assert.Multiple(() => {
            Assert.That(REG_VAR_ORDER.Length, Is.EqualTo(29));
            Assert.That(REG_VAR_ORDER[0], Is.EqualTo(REG_ARM64_R0));
            Assert.That(REG_VAR_ORDER[14], Is.EqualTo(REG_ARM64_R12));
            Assert.That(REG_VAR_ORDER[^1], Is.EqualTo(REG_ARM64_LR));
            Assert.That(REG_VAR_ORDER_FLT.Length, Is.EqualTo(32));
            Assert.That(REG_VAR_ORDER_FLT[0], Is.EqualTo(REG_ARM64_V16));
            Assert.That(REG_VAR_ORDER_FLT[^1], Is.EqualTo(REG_ARM64_V0));
            Assert.That(SRBM_ARG_REGS, Is.EqualTo(
                SRBM_ARM64_R0 | SRBM_ARM64_R1 | SRBM_ARM64_R2 | SRBM_ARM64_R3 |
                SRBM_ARM64_R4 | SRBM_ARM64_R5 | SRBM_ARM64_R6 | SRBM_ARM64_R7));
            Assert.That(REG_INTRET_1_ARM64, Is.EqualTo(REG_ARM64_R1));
            Assert.That(REG_FLOATRET, Is.EqualTo(REG_ARM64_V0));
            Assert.That(CNT_CALLEE_SAVED, Is.EqualTo(11));
            Assert.That(CNT_CALLEE_TRASH, Is.EqualTo(17));
            Assert.That(CNT_CALLEE_TRASH_FLOAT, Is.EqualTo(24));
            Assert.That(CNT_CALLEE_TRASH_MASK, Is.EqualTo(8));
        });
    }
}
