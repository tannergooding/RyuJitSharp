// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class FloatingPointRoundingTests
{
    [TestCase(0x00000000U, 0x00000000U)]
    [TestCase(0x00000001U, 0x00000000U)]
    [TestCase(0x007FFFFFU, 0x00000000U)]
    [TestCase(0x00800000U, 0x00000000U)]
    [TestCase(0x3E800000U, 0x00000000U)]
    [TestCase(0x3EFFFFFFU, 0x00000000U)]
    [TestCase(0x3F000000U, 0x00000000U)]
    [TestCase(0x3F000001U, 0x3F800000U)]
    [TestCase(0x3F800000U, 0x3F800000U)]
    [TestCase(0x3FBFFFFFU, 0x3F800000U)]
    [TestCase(0x3FC00000U, 0x40000000U)]
    [TestCase(0x3FC00001U, 0x40000000U)]
    [TestCase(0x401FFFFFU, 0x40000000U)]
    [TestCase(0x40200000U, 0x40000000U)]
    [TestCase(0x40200001U, 0x40400000U)]
    [TestCase(0x40600000U, 0x40800000U)]
    [TestCase(0x4AFFFFFFU, 0x4B000000U)]
    [TestCase(0x4B000000U, 0x4B000000U)]
    [TestCase(0x4B000001U, 0x4B000001U)]
    [TestCase(0x7F7FFFFFU, 0x7F7FFFFFU)]
    [TestCase(0x7F800000U, 0x7F800000U)]
    [TestCase(0x7FC12345U, 0x7FC12345U)]
    [TestCase(0x7F812345U, 0x7FC12345U)]
    public static void SingleRoundingPreservesPrecisionAndSign(uint inputBits, uint expectedBits)
    {
        foreach (var sign in new uint[] { 0, 0x80000000U })
        {
            var input = UInt32BitsToSingle(inputBits | sign);
            var actual = FloatingPointUtils.round(input);

            Assert.That(SingleToUInt32Bits(actual), Is.EqualTo(expectedBits | sign));
            Assert.That(SingleToUInt32Bits(input), Is.EqualTo(inputBits | sign));
        }
    }

    [TestCase(0x0000000000000000UL, 0x0000000000000000UL)]
    [TestCase(0x0000000000000001UL, 0x0000000000000000UL)]
    [TestCase(0x000FFFFFFFFFFFFFUL, 0x0000000000000000UL)]
    [TestCase(0x0010000000000000UL, 0x0000000000000000UL)]
    [TestCase(0x3FD0000000000000UL, 0x0000000000000000UL)]
    [TestCase(0x3FDFFFFFFFFFFFFFUL, 0x0000000000000000UL)]
    [TestCase(0x3FE0000000000000UL, 0x0000000000000000UL)]
    [TestCase(0x3FE0000000000001UL, 0x3FF0000000000000UL)]
    [TestCase(0x3FF0000000000000UL, 0x3FF0000000000000UL)]
    [TestCase(0x3FF7FFFFFFFFFFFFUL, 0x3FF0000000000000UL)]
    [TestCase(0x3FF8000000000000UL, 0x4000000000000000UL)]
    [TestCase(0x3FF8000000000001UL, 0x4000000000000000UL)]
    [TestCase(0x4003FFFFFFFFFFFFUL, 0x4000000000000000UL)]
    [TestCase(0x4004000000000000UL, 0x4000000000000000UL)]
    [TestCase(0x4004000000000001UL, 0x4008000000000000UL)]
    [TestCase(0x400C000000000000UL, 0x4010000000000000UL)]
    [TestCase(0x432FFFFFFFFFFFFFUL, 0x4330000000000000UL)]
    [TestCase(0x4330000000000000UL, 0x4330000000000000UL)]
    [TestCase(0x4330000000000001UL, 0x4330000000000001UL)]
    [TestCase(0x7FEFFFFFFFFFFFFFUL, 0x7FEFFFFFFFFFFFFFUL)]
    [TestCase(0x7FF0000000000000UL, 0x7FF0000000000000UL)]
    [TestCase(0x7FF8123456789ABCUL, 0x7FF8123456789ABCUL)]
    [TestCase(0x7FF0123456789ABCUL, 0x7FF8123456789ABCUL)]
    public static void DoubleRoundingPreservesPrecisionAndSign(ulong inputBits, ulong expectedBits)
    {
        foreach (var sign in new ulong[] { 0, 0x8000000000000000UL })
        {
            var input = UInt64BitsToDouble(inputBits | sign);
            var actual = FloatingPointUtils.round(input);

            Assert.That(DoubleToUInt64Bits(actual), Is.EqualTo(expectedBits | sign));
            Assert.That(DoubleToUInt64Bits(input), Is.EqualTo(inputBits | sign));
        }
    }

    [Test]
    public static void InfinityHelpersReturnPositiveInfinityEncoding()
    {
        Assert.That(SingleToUInt32Bits(FloatingPointUtils.infinite_float()), Is.EqualTo(0x7F800000U));
        Assert.That(DoubleToUInt64Bits(FloatingPointUtils.infinite_double()), Is.EqualTo(0x7FF0000000000000UL));
    }

    [Test]
    public static void AllBitsSetRequiresEveryBitIncludingSignAndQuietBit()
    {
        Assert.That(FloatingPointUtils.isAllBitsSet(UInt32BitsToSingle(uint.MaxValue)), Is.True);
        Assert.That(FloatingPointUtils.isAllBitsSet(UInt64BitsToDouble(ulong.MaxValue)), Is.True);

        for (var bit = 0; bit < 32; bit++)
        {
            var bits = uint.MaxValue ^ (1U << bit);
            Assert.That(FloatingPointUtils.isAllBitsSet(UInt32BitsToSingle(bits)), Is.False, $"Bit {bit}");
        }

        for (var bit = 0; bit < 64; bit++)
        {
            var bits = ulong.MaxValue ^ (1UL << bit);
            Assert.That(FloatingPointUtils.isAllBitsSet(UInt64BitsToDouble(bits)), Is.False, $"Bit {bit}");
        }
    }
}
