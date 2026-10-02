// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class ScalarNarrowingTests
{
    public static IEnumerable<TestCaseData> FloatCases()
    {
        (ulong Source, uint Result)[] cases = [
            (0x0000000000000000UL, 0x00000000U),
            (0x3FF0000000000000UL, 0x3F800000U),
            (0x3FF0000010000000UL, 0x3F800000U),
            (0x3FF0000020000000UL, 0x3F800001U),
            (0x3FF0000030000000UL, 0x3F800002U),
            (0x3690000000000000UL, 0x00000000U),
            (0x3690000000000001UL, 0x00000001U),
            (0x36A0000000000000UL, 0x00000001U),
            (0x3810000000000000UL, 0x00800000U),
            (0x47EFFFFFE0000000UL, 0x7F7FFFFFU),
            (0x7FEFFFFFFFFFFFFFUL, 0x7F800000U),
            (0x7FF0000000000000UL, 0x7F800000U),
        ];

        foreach (var (source, result) in cases)
        {
            yield return new TestCaseData(source, result);
            yield return new TestCaseData(source | 0x8000000000000000UL, result | 0x80000000U);
        }
    }

    [TestCaseSource(nameof(FloatCases))]
    public static void FloatNarrowingMaterializesTheExpectedWidth(ulong source, uint expected)
    {
        var result = forceCastToFloat(BitConverter.UInt64BitsToDouble(source));

        Assert.That(BitConverter.SingleToUInt32Bits(result), Is.EqualTo(expected));
    }

    [TestCase(0x7FF8000020000000UL)]
    [TestCase(0xFFF8000020000000UL)]
    [TestCase(0x7FF0000020000000UL)]
    [TestCase(0xFFF0000020000000UL)]
    public static void NaNNarrowingRemainsNaN(ulong source)
    {
        Assert.That(float.IsNaN(forceCastToFloat(BitConverter.UInt64BitsToDouble(source))), Is.True);
    }

    [TestCase(-0.9999999999999999, 0U)]
    [TestCase(-0.0, 0U)]
    [TestCase(0.0, 0U)]
    [TestCase(0.9999999999999999, 0U)]
    [TestCase(1.0, 1U)]
    [TestCase(1.9, 1U)]
    [TestCase(2147483647.0, 0x7FFFFFFFU)]
    [TestCase(2147483648.0, 0x80000000U)]
    [TestCase(4294967295.0, uint.MaxValue)]
    [TestCase(4294967295.9999995, uint.MaxValue)]
    public static void UInt32NarrowingTruncatesWithinTheNativeDefinedRange(double value, uint expected)
    {
        Assert.That(forceCastToUInt32(value), Is.EqualTo(expected));
    }

    public static IEnumerable<TestCaseData> WordCases()
    {
        yield return new TestCaseData(0UL, 0U, 0U);
        yield return new TestCaseData(ulong.MaxValue, uint.MaxValue, uint.MaxValue);
        yield return new TestCaseData(0x0123456789ABCDEFUL, 0x89ABCDEFU, 0x01234567U);
        yield return new TestCaseData(0xFEDCBA9876543210UL, 0x76543210U, 0xFEDCBA98U);
        yield return new TestCaseData(0xAAAAAAAA55555555UL, 0x55555555U, 0xAAAAAAAAU);
        yield return new TestCaseData(0x55555555AAAAAAAAUL, 0xAAAAAAAAU, 0x55555555U);

        for (var bit = 0; bit < 64; bit++)
        {
            yield return new TestCaseData(1UL << bit,
                bit < 32 ? 1U << bit : 0U,
                bit >= 32 ? 1U << (bit - 32) : 0U);
        }
    }

    [TestCaseSource(nameof(WordCases))]
    public static void WordExtractionPreservesEachUnsignedHalf(ulong value, uint low, uint high)
    {
        Assert.That(ulo32(value), Is.EqualTo(low));
        Assert.That(uhi32(value), Is.EqualTo(high));
        Assert.That(((ulong)uhi32(value) << 32) | ulo32(value), Is.EqualTo(value));
    }
}
