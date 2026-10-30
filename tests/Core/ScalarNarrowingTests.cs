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
}
