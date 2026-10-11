// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class FeatureConfigurationTests
{
    [Test]
    public static void MaskedHardwareIntrinsicsMatchTheTargetArchitecture()
    {
        Assert.That(Configuration.FEATURE_MASKED_HW_INTRINSICS,
            Is.EqualTo(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64));
    }
}
