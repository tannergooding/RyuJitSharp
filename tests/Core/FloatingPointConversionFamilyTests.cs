// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class FloatingPointConversionFamilyTests
{
    [TestCase(0UL, 0x00000000U, 0x0000000000000000UL)]
    [TestCase(1UL, 0x3F800000U, 0x3FF0000000000000UL)]
    [TestCase(2UL, 0x40000000U, 0x4000000000000000UL)]
    [TestCase(0xFFFFFFUL, 0x4B7FFFFFU, 0x416FFFFFE0000000UL)]
    [TestCase(0x1000000UL, 0x4B800000U, 0x4170000000000000UL)]
    [TestCase(0x1000001UL, 0x4B800000U, 0x4170000010000000UL)]
    [TestCase(0x1000003UL, 0x4B800002U, 0x4170000030000000UL)]
    [TestCase(0x1FFFFFFFFFFFFFUL, 0x5A000000U, 0x433FFFFFFFFFFFFFUL)]
    [TestCase(0x20000000000000UL, 0x5A000000U, 0x4340000000000000UL)]
    [TestCase(0x20000000000001UL, 0x5A000000U, 0x4340000000000000UL)]
    [TestCase(0x20000000000003UL, 0x5A000000U, 0x4340000000000002UL)]
    [TestCase(0x7FFFFFFFFFFFFFFFUL, 0x5F000000U, 0x43E0000000000000UL)]
    [TestCase(0x8000000000000000UL, 0x5F000000U, 0x43E0000000000000UL)]
    [TestCase(0xFFFFFFFFFFFFFFFFUL, 0x5F800000U, 0x43F0000000000000UL)]
    public static void UnsignedConversionsRoundAtEachPrecision(ulong value, uint singleBits, ulong doubleBits)
    {
        Assert.That(SingleToUInt32Bits(FloatingPointUtils.convertUInt64ToFloat(value)), Is.EqualTo(singleBits));
        Assert.That(DoubleToUInt64Bits(FloatingPointUtils.convertUInt64ToDouble(value)), Is.EqualTo(doubleBits));
    }

    // Native floating-to-integer casts only define results when the truncated value is representable.
    [TestCase(0x0000000000000000UL, 0UL)]
    [TestCase(0x8000000000000000UL, 0UL)]
    [TestCase(0x3FE0000000000000UL, 0UL)]
    [TestCase(0xBFE0000000000000UL, 0UL)]
    [TestCase(0x3FF8000000000000UL, 1UL)]
    [TestCase(0x41EFFFFFFFF00000UL, 0xFFFFFFFFUL)]
    [TestCase(0x41F0000000000000UL, 0x100000000UL)]
    [TestCase(0x4340000000000000UL, 0x20000000000000UL)]
    [TestCase(0x43E0000000000000UL, 0x8000000000000000UL)]
    [TestCase(0x43E0000000000001UL, 0x8000000000000800UL)]
    [TestCase(0x43EFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFF800UL)]
    public static void DoubleToUnsignedTruncatesWithinNativeDefinedRange(ulong bits, ulong expected)
    {
        var value = UInt64BitsToDouble(bits);

        Assert.That(FloatingPointUtils.convertDoubleToUInt64(value), Is.EqualTo(expected));
    }

    [TestCase(0x00000000U, 0x0000000000000000UL)]
    [TestCase(0x00000001U, 0x36A0000000000000UL)]
    [TestCase(0x00800000U, 0x3810000000000000UL)]
    [TestCase(0x3F000000U, 0x3FE0000000000000UL)]
    [TestCase(0x3F800000U, 0x3FF0000000000000UL)]
    [TestCase(0x3F800001U, 0x3FF0000020000000UL)]
    [TestCase(0x7F7FFFFFU, 0x47EFFFFFE0000000UL)]
    [TestCase(0x7F800000U, 0x7FF0000000000000UL)]
    [TestCase(0x7FC12345U, 0x7FF82468A0000000UL)]
    [TestCase(0x7F812345U, 0x7FF82468A0000000UL)]
    [TestCase(0x7FFFFFFFU, 0x7FFFFFFFE0000000UL)]
    public static void WideningPreservesSignAndPayload(uint inputBits, ulong expectedBits)
    {
        foreach (var sign in new uint[] { 0, 0x80000000U })
        {
            var input = UInt32BitsToSingle(inputBits | sign);
            var result = FloatingPointUtils.convertToDouble(input);
            var expected = expectedBits | ((ulong)sign << 32);

            Assert.That(DoubleToUInt64Bits(result), Is.EqualTo(expected));
            Assert.That(SingleToUInt32Bits(input), Is.EqualTo(inputBits | sign));
        }
    }
}
