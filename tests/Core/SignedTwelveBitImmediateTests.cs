// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class SignedTwelveBitImmediateTests
{
    [TestCase(long.MinValue, false)]
    [TestCase(-2049L, false)]
    [TestCase(-2048L, true)]
    [TestCase(0L, true)]
    [TestCase(2047L, true)]
    [TestCase(2048L, false)]
    [TestCase(long.MaxValue, false)]
    public static void SignedImmediateUsesInclusiveLowerAndExclusiveUpperBound(long value, bool expected)
    {
        Assert.That(Emitter.isValidSimm12(unchecked((nint)value)), Is.EqualTo(expected));
    }
}
#endif
