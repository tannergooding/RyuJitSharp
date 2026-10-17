// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insFlags;
using TargetDefinition = global::RyuJitSharp.Target;
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
        RequireArm64Target();
        Assert.That((uint)option, Is.EqualTo(expected));
    }

    [Test]
    public static void LoopAlignmentOptionPreservesNativeValue()
    {
        RequireArm64Target();
        Assert.That((uint)INS_OPTS_ALIGN, Is.EqualTo(52u));
    }

    [Test]
    public static void InstructionFlagsPreserveNativeValues()
    {
        RequireArm64Target();
        Assert.Multiple(() => {
            Assert.That((uint)INS_FLAGS_NOT_SET, Is.Zero);
            Assert.That((uint)INS_FLAGS_SET, Is.EqualTo(1u));
            Assert.That((uint)INS_FLAGS_DONT_CARE, Is.EqualTo(2u));
        });
    }

    [Test]
    public static void TargetDeclarationSizesPreserveNativeValues()
    {
        RequireArm64Target();
        Assert.Multiple(() => {
            Assert.That(Configuration.MAX_PASS_SINGLEREG_BYTES, Is.EqualTo(16));
            Assert.That(Configuration.MAX_PASS_MULTIREG_BYTES, Is.EqualTo(64));
            Assert.That(Configuration.MAX_RET_MULTIREG_BYTES, Is.EqualTo(64));
            Assert.That(Configuration.MAX_ARG_REG_COUNT, Is.EqualTo(4));
            Assert.That(Configuration.MAX_RET_REG_COUNT, Is.EqualTo(4));
            Assert.That(Configuration.MAX_MULTIREG_COUNT, Is.EqualTo(4));
            Assert.That(Configuration.FP_REGSIZE_BYTES, Is.EqualTo(16));
            Assert.That(TargetDefinition.ARG_ORDER_R2L, Is.EqualTo(TargetDefinition.ArgOrder.ARG_ORDER_R2L));
            Assert.That(TargetDefinition.ARG_ORDER_L2R, Is.EqualTo(TargetDefinition.ArgOrder.ARG_ORDER_L2R));
            Assert.That(TargetDefinition.TgtCpuName, Is.EqualTo("arm64"));
            Assert.That(TargetDefinition.TgtArgOrder, Is.EqualTo(TargetDefinition.ArgOrder.ARG_ORDER_R2L));
            Assert.That(TargetDefinition.TgtUnmanagedArgOrder, Is.EqualTo(TargetDefinition.ArgOrder.ARG_ORDER_R2L));
        });
    }

    private static void RequireArm64Target()
    {
        if (!Configuration.TARGET_ARM64)
        {
            Assert.Ignore("Declaration checks require the ARM64 target.");
        }
    }
}
