// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class FloatingPointSelectionTests
{
    // Bits 0..3 select the left operand for maximum, maximumNumber, minimum, and minimumNumber.
    // A clear bit selects the right operand. These are explicit operand-selection expectations,
    // including same-sign equality where the returned encoding alone cannot distinguish operands.
    [TestCase(0x3F800000U, 0x40000000U, 0xC)]
    [TestCase(0x40000000U, 0x3F800000U, 0x3)]
    [TestCase(0xBF800000U, 0xC0000000U, 0x3)]
    [TestCase(0xC0000000U, 0xBF800000U, 0xC)]
    [TestCase(0x3F800000U, 0xBF800000U, 0x3)]
    [TestCase(0xBF800000U, 0x3F800000U, 0xC)]
    [TestCase(0xC0000000U, 0x3F800000U, 0xC)]
    [TestCase(0x3F800000U, 0xC0000000U, 0x3)]
    [TestCase(0x40000000U, 0xBF800000U, 0x3)]
    [TestCase(0xBF800000U, 0x40000000U, 0xC)]
    [TestCase(0x3F800000U, 0x3F800000U, 0x0)]
    [TestCase(0xBF800000U, 0xBF800000U, 0xF)]
    [TestCase(0x00000000U, 0x00000000U, 0x0)]
    [TestCase(0x80000000U, 0x80000000U, 0xF)]
    [TestCase(0x00000000U, 0x80000000U, 0x3)]
    [TestCase(0x80000000U, 0x00000000U, 0xC)]
    [TestCase(0x7F800000U, 0x3F800000U, 0x3)]
    [TestCase(0x3F800000U, 0x7F800000U, 0xC)]
    [TestCase(0xFF800000U, 0xBF800000U, 0xC)]
    [TestCase(0xBF800000U, 0xFF800000U, 0x3)]
    [TestCase(0x7F800000U, 0xFF800000U, 0x3)]
    [TestCase(0xFF800000U, 0x7F800000U, 0xC)]
    [TestCase(0x7F800000U, 0x7F800000U, 0x0)]
    [TestCase(0xFF800000U, 0xFF800000U, 0xF)]
    [TestCase(0x00000001U, 0x00000000U, 0x3)]
    [TestCase(0x00000000U, 0x00000001U, 0xC)]
    [TestCase(0x80000001U, 0x00000000U, 0xC)]
    [TestCase(0x00000000U, 0x80000001U, 0x3)]
    [TestCase(0x00000001U, 0x80000000U, 0x3)]
    [TestCase(0x80000000U, 0x00000001U, 0xC)]
    [TestCase(0x80000001U, 0x80000000U, 0xC)]
    [TestCase(0x80000000U, 0x80000001U, 0x3)]
    [TestCase(0x7FC12345U, 0x3F800000U, 0x5)]
    [TestCase(0x3F800000U, 0x7FC12345U, 0xA)]
    [TestCase(0xFFC12345U, 0xBF800000U, 0x5)]
    [TestCase(0xBF800000U, 0xFFC12345U, 0xA)]
    [TestCase(0x7F812345U, 0x3F800000U, 0x5)]
    [TestCase(0x3F800000U, 0x7F812345U, 0xA)]
    [TestCase(0xFF812345U, 0xBF800000U, 0x5)]
    [TestCase(0xBF800000U, 0xFF812345U, 0xA)]
    [TestCase(0x7FC12345U, 0xFFC54321U, 0xF)]
    [TestCase(0xFFC54321U, 0x7FC12345U, 0xF)]
    [TestCase(0x7F812345U, 0xFF854321U, 0xF)]
    [TestCase(0xFF854321U, 0x7F812345U, 0xF)]
    [TestCase(0x7FC12345U, 0xFF854321U, 0xF)]
    [TestCase(0xFF854321U, 0x7FC12345U, 0xF)]
    [TestCase(0x7F812345U, 0xFFC54321U, 0xF)]
    [TestCase(0xFFC54321U, 0x7F812345U, 0xF)]
    public static void SingleSelectionPreservesOperandEncoding(uint leftBits, uint rightBits, int chooseLeftMask)
    {
        var left = UInt32BitsToSingle(leftBits);
        var right = UInt32BitsToSingle(rightBits);

        uint[] results =
        [
            SingleToUInt32Bits(FloatingPointUtils.maximum(left, right)),
            SingleToUInt32Bits(FloatingPointUtils.maximumNumber(left, right)),
            SingleToUInt32Bits(FloatingPointUtils.minimum(left, right)),
            SingleToUInt32Bits(FloatingPointUtils.minimumNumber(left, right)),
        ];

        for (var operation = 0; operation < results.Length; operation++)
        {
            var expectedBits = (chooseLeftMask & (1 << operation)) != 0 ? leftBits : rightBits;
            Assert.That(results[operation], Is.EqualTo(expectedBits), $"Operation {operation}");
        }

        Assert.That(SingleToUInt32Bits(left), Is.EqualTo(leftBits));
        Assert.That(SingleToUInt32Bits(right), Is.EqualTo(rightBits));
    }

    [TestCase(0x3FF0000000000000UL, 0x4000000000000000UL, 0xC)]
    [TestCase(0x4000000000000000UL, 0x3FF0000000000000UL, 0x3)]
    [TestCase(0xBFF0000000000000UL, 0xC000000000000000UL, 0x3)]
    [TestCase(0xC000000000000000UL, 0xBFF0000000000000UL, 0xC)]
    [TestCase(0x3FF0000000000000UL, 0xBFF0000000000000UL, 0x3)]
    [TestCase(0xBFF0000000000000UL, 0x3FF0000000000000UL, 0xC)]
    [TestCase(0xC000000000000000UL, 0x3FF0000000000000UL, 0xC)]
    [TestCase(0x3FF0000000000000UL, 0xC000000000000000UL, 0x3)]
    [TestCase(0x4000000000000000UL, 0xBFF0000000000000UL, 0x3)]
    [TestCase(0xBFF0000000000000UL, 0x4000000000000000UL, 0xC)]
    [TestCase(0x3FF0000000000000UL, 0x3FF0000000000000UL, 0x0)]
    [TestCase(0xBFF0000000000000UL, 0xBFF0000000000000UL, 0xF)]
    [TestCase(0x0000000000000000UL, 0x0000000000000000UL, 0x0)]
    [TestCase(0x8000000000000000UL, 0x8000000000000000UL, 0xF)]
    [TestCase(0x0000000000000000UL, 0x8000000000000000UL, 0x3)]
    [TestCase(0x8000000000000000UL, 0x0000000000000000UL, 0xC)]
    [TestCase(0x7FF0000000000000UL, 0x3FF0000000000000UL, 0x3)]
    [TestCase(0x3FF0000000000000UL, 0x7FF0000000000000UL, 0xC)]
    [TestCase(0xFFF0000000000000UL, 0xBFF0000000000000UL, 0xC)]
    [TestCase(0xBFF0000000000000UL, 0xFFF0000000000000UL, 0x3)]
    [TestCase(0x7FF0000000000000UL, 0xFFF0000000000000UL, 0x3)]
    [TestCase(0xFFF0000000000000UL, 0x7FF0000000000000UL, 0xC)]
    [TestCase(0x7FF0000000000000UL, 0x7FF0000000000000UL, 0x0)]
    [TestCase(0xFFF0000000000000UL, 0xFFF0000000000000UL, 0xF)]
    [TestCase(0x0000000000000001UL, 0x0000000000000000UL, 0x3)]
    [TestCase(0x0000000000000000UL, 0x0000000000000001UL, 0xC)]
    [TestCase(0x8000000000000001UL, 0x0000000000000000UL, 0xC)]
    [TestCase(0x0000000000000000UL, 0x8000000000000001UL, 0x3)]
    [TestCase(0x0000000000000001UL, 0x8000000000000000UL, 0x3)]
    [TestCase(0x8000000000000000UL, 0x0000000000000001UL, 0xC)]
    [TestCase(0x8000000000000001UL, 0x8000000000000000UL, 0xC)]
    [TestCase(0x8000000000000000UL, 0x8000000000000001UL, 0x3)]
    [TestCase(0x7FF8123456789ABCUL, 0x3FF0000000000000UL, 0x5)]
    [TestCase(0x3FF0000000000000UL, 0x7FF8123456789ABCUL, 0xA)]
    [TestCase(0xFFF8123456789ABCUL, 0xBFF0000000000000UL, 0x5)]
    [TestCase(0xBFF0000000000000UL, 0xFFF8123456789ABCUL, 0xA)]
    [TestCase(0x7FF0123456789ABCUL, 0x3FF0000000000000UL, 0x5)]
    [TestCase(0x3FF0000000000000UL, 0x7FF0123456789ABCUL, 0xA)]
    [TestCase(0xFFF0123456789ABCUL, 0xBFF0000000000000UL, 0x5)]
    [TestCase(0xBFF0000000000000UL, 0xFFF0123456789ABCUL, 0xA)]
    [TestCase(0x7FF8123456789ABCUL, 0xFFF8ABCDEF123456UL, 0xF)]
    [TestCase(0xFFF8ABCDEF123456UL, 0x7FF8123456789ABCUL, 0xF)]
    [TestCase(0x7FF0123456789ABCUL, 0xFFF0ABCDEF123456UL, 0xF)]
    [TestCase(0xFFF0ABCDEF123456UL, 0x7FF0123456789ABCUL, 0xF)]
    [TestCase(0x7FF8123456789ABCUL, 0xFFF0ABCDEF123456UL, 0xF)]
    [TestCase(0xFFF0ABCDEF123456UL, 0x7FF8123456789ABCUL, 0xF)]
    [TestCase(0x7FF0123456789ABCUL, 0xFFF8ABCDEF123456UL, 0xF)]
    [TestCase(0xFFF8ABCDEF123456UL, 0x7FF0123456789ABCUL, 0xF)]
    public static void DoubleSelectionPreservesOperandEncoding(ulong leftBits, ulong rightBits, int chooseLeftMask)
    {
        var left = UInt64BitsToDouble(leftBits);
        var right = UInt64BitsToDouble(rightBits);

        ulong[] results =
        [
            DoubleToUInt64Bits(FloatingPointUtils.maximum(left, right)),
            DoubleToUInt64Bits(FloatingPointUtils.maximumNumber(left, right)),
            DoubleToUInt64Bits(FloatingPointUtils.minimum(left, right)),
            DoubleToUInt64Bits(FloatingPointUtils.minimumNumber(left, right)),
        ];

        for (var operation = 0; operation < results.Length; operation++)
        {
            var expectedBits = (chooseLeftMask & (1 << operation)) != 0 ? leftBits : rightBits;
            Assert.That(results[operation], Is.EqualTo(expectedBits), $"Operation {operation}");
        }

        Assert.That(DoubleToUInt64Bits(left), Is.EqualTo(leftBits));
        Assert.That(DoubleToUInt64Bits(right), Is.EqualTo(rightBits));
    }
}
