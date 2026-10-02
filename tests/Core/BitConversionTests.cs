// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class BitConversionTests
{
    [TestCase(0U)]
    [TestCase(0x80000000U)]
    [TestCase(0x3F800000U)]
    [TestCase(0xBF800000U)]
    [TestCase(1U)]
    [TestCase(0x007FFFFFU)]
    [TestCase(0x00800000U)]
    [TestCase(0x7F7FFFFFU)]
    [TestCase(0x7F800000U)]
    [TestCase(0xFF800000U)]
    [TestCase(0x7FC12345U)]
    [TestCase(0xFFC12345U)]
    [TestCase(0x7F812345U)]
    [TestCase(0xFF812345U)]
    [TestCase(uint.MaxValue)]
    public static void SingleCopiesPreserveTheEntireEncoding(uint bits)
    {
        AssertSingleBits(bits);
    }

    [TestCase(0UL)]
    [TestCase(0x8000000000000000UL)]
    [TestCase(0x3FF0000000000000UL)]
    [TestCase(0xBFF0000000000000UL)]
    [TestCase(1UL)]
    [TestCase(0x000FFFFFFFFFFFFFUL)]
    [TestCase(0x0010000000000000UL)]
    [TestCase(0x7FEFFFFFFFFFFFFFUL)]
    [TestCase(0x7FF0000000000000UL)]
    [TestCase(0xFFF0000000000000UL)]
    [TestCase(0x7FF8123456789ABCUL)]
    [TestCase(0xFFF8123456789ABCUL)]
    [TestCase(0x7FF0123456789ABCUL)]
    [TestCase(0xFFF0123456789ABCUL)]
    [TestCase(ulong.MaxValue)]
    public static void DoubleCopiesPreserveTheEntireEncoding(ulong bits)
    {
        AssertDoubleBits(bits);
    }

    [Test]
    public static void SingleCopiesPreserveEveryBitPosition()
    {
        for (var bit = 0; bit < 32; bit++)
        {
            AssertSingleBits(1U << bit);
        }
    }

    [Test]
    public static void DoubleCopiesPreserveEveryBitPosition()
    {
        for (var bit = 0; bit < 64; bit++)
        {
            AssertDoubleBits(1UL << bit);
        }
    }

    private static void AssertSingleBits(uint bits)
    {
        var input = Unsafe.BitCast<uint, float>(bits);
        Assert.That(SingleToUInt32Bits(input), Is.EqualTo(bits));

        var output = UInt32BitsToSingle(bits);
        Assert.That(Unsafe.BitCast<float, uint>(output), Is.EqualTo(bits));
    }

    private static void AssertDoubleBits(ulong bits)
    {
        var input = Unsafe.BitCast<ulong, double>(bits);
        Assert.That(DoubleToUInt64Bits(input), Is.EqualTo(bits));

        var output = UInt64BitsToDouble(bits);
        Assert.That(Unsafe.BitCast<double, ulong>(output), Is.EqualTo(bits));
    }
}
