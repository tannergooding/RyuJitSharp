// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class ConfigIntegerReinterpretationTests
{
    [TestCase(0, 0)]
    [TestCase(0x12345678, 12345678)]
    [TestCase(int.MinValue, 80000000)]
    [TestCase(unchecked((int)0x99999999U), 99999999)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public static void ReinterpretHexDigitsAsDecimalPreservesTheUnsignedNibbles(
        int value, int expected)
    {
        Assert.That(Globals.ReinterpretHexAsDecimal(value), Is.EqualTo(expected));
    }
}
