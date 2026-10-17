// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class GcInfoEncodingContractTests
{
    [Test]
    public static void InterruptibleRangeCapabilityMatchesNativeTargetFormat()
    {
        var expected = !(Configuration.TARGET_WASM && !Configuration.TARGET_64BIT);
        Assert.That(GcInfoEncoder.HAS_INTERRUPTIBLE_RANGES, Is.EqualTo(expected));
    }

    [Test]
    public static unsafe void FormatCapabilityDoesNotEnableUnimplementedEncoder()
    {
        Assume.That(!Configuration.TARGET_AMD64, "the AMD64 encoder is implemented");

        static unsafe void CreateEncoder()
        {
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO methodInfo = default;
            using var encoder = new GcInfoEncoder(&jitInfo, &methodInfo);
        }

        if (Configuration.TARGET_ARM || Configuration.TARGET_ARMARCH)
        {
            var exception = Assert.Throws<FatalJitException>(CreateEncoder)
                ?? throw new AssertionException("Missing GC-info encoder failure.");
            Assert.That(exception.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        }
        else
        {
            _ = Assert.Throws<PlatformNotSupportedException>(CreateEncoder);
        }
    }
}
