// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class GcInfoVariableEncodingTests
{
    [TestCase(0U, "00")]
    [TestCase(1U, "01")]
    [TestCase(0x7FU, "7F")]
    [TestCase(0x80U, "8100")]
    [TestCase(0x3FFFU, "FF7F")]
    [TestCase(0x4000U, "818000")]
    [TestCase(uint.MaxValue, "8FFFFFFF7F")]
    public static void EncodeUnsignedPreservesBoundaryBytesAndSizingMode(uint value, string expectedHex)
    {
        var expected = Convert.FromHexString(expectedHex);
        Assert.That(GCInfo.encodeUnsigned(null, value), Is.EqualTo((byte)expected.Length));

        var output = stackalloc byte[5];
        Assert.That(GCInfo.encodeUnsigned(output, value), Is.EqualTo((byte)expected.Length));
        Assert.That(new ReadOnlySpan<byte>(output, expected.Length).ToArray(), Is.EqualTo(expected));
    }

    [TestCase(0U, 0U, "00")]
    [TestCase(0x7FU, 0U, "7F")]
    [TestCase(0x80U, 0U, "8100")]
    [TestCase(0x100U, 0x7FU, "8101")]
    [TestCase(uint.MaxValue, 0U, "8FFFFFFF7F")]
    [TestCase(uint.MaxValue, 0xFFFFFF80U, "7F")]
    public static void EncodeUnsignedDeltaPreservesBoundariesAndSizingMode(uint value, uint lastValue, string expectedHex)
    {
        var expected = Convert.FromHexString(expectedHex);
        Assert.That(GCInfo.encodeUDelta(null, value, lastValue), Is.EqualTo((byte)expected.Length));

        var output = stackalloc byte[5];
        Assert.That(GCInfo.encodeUDelta(output, value, lastValue), Is.EqualTo((byte)expected.Length));
        Assert.That(new ReadOnlySpan<byte>(output, expected.Length).ToArray(), Is.EqualTo(expected));
    }

    [TestCase(0, "00")]
    [TestCase(1, "01")]
    [TestCase(63, "3F")]
    [TestCase(64, "8040")]
    [TestCase(65, "8041")]
    [TestCase(-1, "41")]
    [TestCase(-63, "7F")]
    [TestCase(-64, "C040")]
    [TestCase(-65, "C041")]
    [TestCase(int.MaxValue, "87FFFFFF7F")]
    [TestCase(int.MinValue, "C880808000")]
    public static void EncodeSignedPreservesBoundaryBytesAndSizingMode(int value, string expectedHex)
    {
        var expected = Convert.FromHexString(expectedHex);
        Assert.That(GCInfo.encodeSigned(null, value), Is.EqualTo((byte)expected.Length));

        var output = stackalloc byte[5];
        Assert.That(GCInfo.encodeSigned(output, value), Is.EqualTo((byte)expected.Length));
        Assert.That(new ReadOnlySpan<byte>(output, expected.Length).ToArray(), Is.EqualTo(expected));
    }
}
