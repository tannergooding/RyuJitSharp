// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BitScanReverseTests
{
    [TestCase(1U, 0U)]
    [TestCase(2U, 1U)]
    [TestCase(0x7FFFFFFFU, 30U)]
    [TestCase(0x80000000U, 31U)]
    [TestCase(0x80000001U, 31U)]
    [TestCase(uint.MaxValue, 31U)]
    [TestCase(0xAAAAAAAAU, 31U)]
    [TestCase(0x40000001U, 30U)]
    public static void Scan32FindsTheHighestSetBitWithoutSignedConversion(uint value, uint expected)
    {
        Assert.That(BitScanReverse(value), Is.EqualTo(expected));
    }

    [TestCase(1UL, 0U)]
    [TestCase(2UL, 1U)]
    [TestCase(0x7FFFFFFFUL, 30U)]
    [TestCase(0x80000000UL, 31U)]
    [TestCase(0xFFFFFFFFUL, 31U)]
    [TestCase(0x100000000UL, 32U)]
    [TestCase(0x100000001UL, 32U)]
    [TestCase(0x180000000UL, 32U)]
    [TestCase(0x7FFFFFFFFFFFFFFFUL, 62U)]
    [TestCase(0x8000000000000000UL, 63U)]
    [TestCase(0x8000000000000001UL, 63U)]
    [TestCase(ulong.MaxValue, 63U)]
    public static void Scan64FindsTheHighestSetBitAcrossThe32BitBoundary(ulong value, uint expected)
    {
        Assert.That(BitScanReverse(value), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(OneHot32Cases))]
    public static void Scan32ReturnsEveryOneHotPosition(uint value, uint expected)
    {
        Assert.That(BitScanReverse(value), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(OneHot64Cases))]
    public static void Scan64ReturnsEveryOneHotPosition(ulong value, uint expected)
    {
        Assert.That(BitScanReverse(value), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> OneHot32Cases()
    {
        for (var bit = 0; bit < 32; bit++)
        {
            yield return new TestCaseData(1U << bit, (uint)bit);
        }
    }

    private static IEnumerable<TestCaseData> OneHot64Cases()
    {
        for (var bit = 0; bit < 64; bit++)
        {
            yield return new TestCaseData(1UL << bit, (uint)bit);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroScanRetainsTheNativeAssertionCountAndText(bool wide)
    {
        Assume.That(TestConfiguration.IsDebug);

        var (_, assertions) = TestConfiguration.CaptureDebugAssertions(() => {
            if (wide)
            {
                _ = BitScanReverse(0UL);
            }
            else
            {
                _ = BitScanReverse(0U);
            }
        });

        var expectedCount = wide && OperatingSystem.IsWindows() && !Environment.Is64BitProcess ? 2 : 1;
        Assert.That(assertions, Has.Length.EqualTo(expectedCount));
        Assert.That(assertions, Is.All.EqualTo("value != 0"));
    }
}
