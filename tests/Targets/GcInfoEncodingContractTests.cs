// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_AMD64
using System;
#endif
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class GcInfoEncodingContractTests
{
    [Test]
    public static void InterruptibleRangeCapabilityMatchesNativeTargetFormat()
    {
#if TARGET_WASM && !TARGET_64BIT
        Assert.That(GcInfoEncoder.HAS_INTERRUPTIBLE_RANGES, Is.False);
#else
        Assert.That(GcInfoEncoder.HAS_INTERRUPTIBLE_RANGES, Is.True);
#endif
    }

#if !TARGET_AMD64
    [Test]
    public static unsafe void FormatCapabilityDoesNotEnableUnimplementedEncoder()
    {
#if TARGET_ARM || TARGET_ARMARCH
        var exception = Assert.Throws<FatalJitException>(() =>
#else
        _ = Assert.Throws<PlatformNotSupportedException>(() =>
#endif
        {
            ICorJitInfo jitInfo = default;
            CORINFO_METHOD_INFO methodInfo = default;
            using var encoder = new GcInfoEncoder(&jitInfo, &methodInfo);
        });
#if TARGET_ARM || TARGET_ARMARCH
        var failure = exception ?? throw new AssertionException("Missing GC-info encoder failure.");
        Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#endif
    }
#endif
}
