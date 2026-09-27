// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;

namespace RyuJitSharp.UnitTests;

internal static class Arm64DeclarationTests
{
    [TestCase(INS_OPTS_NONE, 0u)]
    [TestCase(INS_OPTS_LSL12, 3u)]
    [TestCase(INS_OPTS_LSL, 4u)]
    [TestCase(INS_OPTS_UXTB, 8u)]
    [TestCase(INS_OPTS_8B, 16u)]
    [TestCase(INS_OPTS_SCALABLE_B, 24u)]
    [TestCase(INS_OPTS_D_TO_H, 51u)]
    public static void InstructionOptionsPreserveNativeValues(insOpts option, uint expected)
    {
        Assert.That((uint)option, Is.EqualTo(expected));
    }

#if FEATURE_LOOP_ALIGN
    [Test]
    public static void LoopAlignmentOptionPreservesNativeValue()
    {
        Assert.That((uint)INS_OPTS_ALIGN, Is.EqualTo(52u));
    }
#endif

    [Test]
    public static void InstructionFlagsPreserveNativeValues()
    {
        Assert.Multiple(() => {
            Assert.That((uint)INS_FLAGS_NOT_SET, Is.Zero);
            Assert.That((uint)INS_FLAGS_SET, Is.EqualTo(1u));
            Assert.That((uint)INS_FLAGS_DONT_CARE, Is.EqualTo(2u));
        });
    }

    [Test]
    public static void TargetDeclarationSizesPreserveNativeValues()
    {
        Assert.Multiple(() => {
            Assert.That(MAX_PASS_SINGLEREG_BYTES, Is.EqualTo(16));
            Assert.That(MAX_PASS_MULTIREG_BYTES, Is.EqualTo(64));
            Assert.That(MAX_RET_MULTIREG_BYTES, Is.EqualTo(64));
            Assert.That(MAX_ARG_REG_COUNT, Is.EqualTo(4));
            Assert.That(MAX_RET_REG_COUNT, Is.EqualTo(4));
            Assert.That(MAX_MULTIREG_COUNT, Is.EqualTo(4));
            Assert.That(FP_REGSIZE_BYTES, Is.EqualTo(16));
            Assert.That(Target.ARG_ORDER_R2L, Is.EqualTo(Target.ArgOrder.ARG_ORDER_R2L));
            Assert.That(Target.ARG_ORDER_L2R, Is.EqualTo(Target.ArgOrder.ARG_ORDER_L2R));
            Assert.That(Target.TgtCpuName, Is.EqualTo("arm64"));
            Assert.That(Target.TgtArgOrder, Is.EqualTo(Target.ArgOrder.ARG_ORDER_R2L));
            Assert.That(Target.TgtUnmanagedArgOrder, Is.EqualTo(Target.ArgOrder.ARG_ORDER_R2L));
        });
    }
}
#endif
