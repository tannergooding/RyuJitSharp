// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM || TARGET_ARMARCH
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ArmGcInfoPublicationTests
{
    [Test]
    public static void GcInfoEncoderUsesJitSkipUntilArmEncodingIsPorted()
    {
        var failure = Assert.Throws<FatalJitException>(() =>
            _ = new GcInfoEncoder((ICorJitInfo*)0, (CORINFO_METHOD_INFO*)0));

        Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
    }
}
#endif
