// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class BitReversalTests
{
    [TestCase(0U, 0U)]
    [TestCase(uint.MaxValue, uint.MaxValue)]
    [TestCase(1U, 0x80000000U)]
    [TestCase(0x80000000U, 1U)]
    [TestCase(0xAAAAAAAAU, 0x55555555U)]
    [TestCase(0x55555555U, 0xAAAAAAAAU)]
    [TestCase(0x01234567U, 0xE6A2C480U)]
    [TestCase(0x80000001U, 0x80000001U)]
    public static void Reverse32PreservesEveryBitAndIsItsOwnInverse(uint value, uint expected)
    {
        Assert.That(ReverseBits(value), Is.EqualTo(expected));
        Assert.That(ReverseBits(ReverseBits(value)), Is.EqualTo(value));
    }

    [TestCase(0UL, 0UL)]
    [TestCase(ulong.MaxValue, ulong.MaxValue)]
    [TestCase(1UL, 0x8000000000000000UL)]
    [TestCase(0x8000000000000000UL, 1UL)]
    [TestCase(0xAAAAAAAAAAAAAAAAUL, 0x5555555555555555UL)]
    [TestCase(0x5555555555555555UL, 0xAAAAAAAAAAAAAAAAUL)]
    [TestCase(0x0123456789ABCDEFUL, 0xF7B3D591E6A2C480UL)]
    [TestCase(0x8000000000000001UL, 0x8000000000000001UL)]
    public static void Reverse64PreservesEveryBitAndIsItsOwnInverse(ulong value, ulong expected)
    {
        Assert.That(ReverseBits(value), Is.EqualTo(expected));
        Assert.That(ReverseBits(ReverseBits(value)), Is.EqualTo(value));
    }

    [Test]
    public static void Reverse32MapsEachPositionToItsMirror()
    {
        for (var bit = 0; bit < 32; bit++)
        {
            Assert.That(ReverseBits(1U << bit), Is.EqualTo(1U << (31 - bit)));
        }
    }

    [Test]
    public static void Reverse64MapsEachPositionToItsMirror()
    {
        for (var bit = 0; bit < 64; bit++)
        {
            Assert.That(ReverseBits(1UL << bit), Is.EqualTo(1UL << (63 - bit)));
        }
    }
}
