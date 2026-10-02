// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class FloatingPointClassificationTests
{
    [TestCase(0x00000000U, false, true, false, false)]
    [TestCase(0x80000000U, false, true, true, false)]
    [TestCase(0x3F800000U, true, true, false, false)]
    [TestCase(0xBF800000U, true, true, true, false)]
    [TestCase(0x00000001U, false, true, false, false)]
    [TestCase(0x80000001U, false, true, true, false)]
    [TestCase(0x007FFFFFU, false, true, false, false)]
    [TestCase(0x807FFFFFU, false, true, true, false)]
    [TestCase(0x00800000U, true, true, false, false)]
    [TestCase(0x80800000U, true, true, true, false)]
    [TestCase(0x7F7FFFFFU, true, true, false, false)]
    [TestCase(0xFF7FFFFFU, true, true, true, false)]
    [TestCase(0x7F800000U, false, false, false, false)]
    [TestCase(0xFF800000U, false, false, true, false)]
    [TestCase(0x7FC12345U, false, false, false, true)]
    [TestCase(0xFFC12345U, false, false, true, true)]
    [TestCase(0x7F812345U, false, false, false, true)]
    [TestCase(0xFF812345U, false, false, true, true)]
    [TestCase(0x7F800001U, false, false, false, true)]
    [TestCase(0xFF800001U, false, false, true, true)]
    [TestCase(0x7FFFFFFFU, false, false, false, true)]
    [TestCase(0xFFFFFFFFU, false, false, true, true)]
    public static void SingleClassificationPreservesEncoding(uint bits, bool normal, bool finite, bool negative, bool nan)
    {
        var value = UInt32BitsToSingle(bits);

        Assert.That(FloatingPointUtils.isNormal(value), Is.EqualTo(normal));
        Assert.That(FloatingPointUtils.isFinite(value), Is.EqualTo(finite));
        Assert.That(FloatingPointUtils.isNegative(value), Is.EqualTo(negative));
        Assert.That(FloatingPointUtils.isNaN(value), Is.EqualTo(nan));
        Assert.That(SingleToUInt32Bits(value), Is.EqualTo(bits));
    }

    [TestCase(0x0000000000000000UL, false, true, false, false, false, true)]
    [TestCase(0x8000000000000000UL, false, true, true, false, true, false)]
    [TestCase(0x3FF0000000000000UL, true, true, false, false, false, false)]
    [TestCase(0xBFF0000000000000UL, true, true, true, false, false, false)]
    [TestCase(0x0000000000000001UL, false, true, false, false, false, false)]
    [TestCase(0x8000000000000001UL, false, true, true, false, false, false)]
    [TestCase(0x000FFFFFFFFFFFFFUL, false, true, false, false, false, false)]
    [TestCase(0x800FFFFFFFFFFFFFUL, false, true, true, false, false, false)]
    [TestCase(0x0010000000000000UL, true, true, false, false, false, false)]
    [TestCase(0x8010000000000000UL, true, true, true, false, false, false)]
    [TestCase(0x7FEFFFFFFFFFFFFFUL, true, true, false, false, false, false)]
    [TestCase(0xFFEFFFFFFFFFFFFFUL, true, true, true, false, false, false)]
    [TestCase(0x7FF0000000000000UL, false, false, false, false, false, false)]
    [TestCase(0xFFF0000000000000UL, false, false, true, false, false, false)]
    [TestCase(0x7FF8123456789ABCUL, false, false, false, true, false, false)]
    [TestCase(0xFFF8123456789ABCUL, false, false, true, true, false, false)]
    [TestCase(0x7FF0123456789ABCUL, false, false, false, true, false, false)]
    [TestCase(0xFFF0123456789ABCUL, false, false, true, true, false, false)]
    [TestCase(0x7FF0000000000001UL, false, false, false, true, false, false)]
    [TestCase(0xFFF0000000000001UL, false, false, true, true, false, false)]
    [TestCase(0x7FFFFFFFFFFFFFFFUL, false, false, false, true, false, false)]
    [TestCase(0xFFFFFFFFFFFFFFFFUL, false, false, true, true, false, false)]
    public static void DoubleClassificationAndNormalizationPreserveEncoding(
        ulong bits, bool normal, bool finite, bool negative, bool nan, bool negativeZero, bool positiveZero)
    {
        var value = UInt64BitsToDouble(bits);

        Assert.That(FloatingPointUtils.isNormal(value), Is.EqualTo(normal));
        Assert.That(FloatingPointUtils.isFinite(value), Is.EqualTo(finite));
        Assert.That(FloatingPointUtils.isNegative(value), Is.EqualTo(negative));
        Assert.That(FloatingPointUtils.isNaN(value), Is.EqualTo(nan));
        Assert.That(FloatingPointUtils.isNegativeZero(value), Is.EqualTo(negativeZero));
        Assert.That(FloatingPointUtils.isPositiveZero(value), Is.EqualTo(positiveZero));
        Assert.That(DoubleToUInt64Bits(value), Is.EqualTo(bits));

        var expectedBits = bits;
        if ((RuntimeInformation.ProcessArchitecture == Architecture.X86) && nan)
        {
            expectedBits |= 1UL << 51;
        }

        var normalized = FloatingPointUtils.normalize(value);
        Assert.That(DoubleToUInt64Bits(normalized), Is.EqualTo(expectedBits));
        Assert.That(DoubleToUInt64Bits(value), Is.EqualTo(bits));
    }
}
