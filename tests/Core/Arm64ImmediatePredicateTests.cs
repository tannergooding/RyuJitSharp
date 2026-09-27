// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;

namespace RyuJitSharp.UnitTests;

internal static class Arm64ImmediatePredicateTests
{
    [TestCase(0L, true)]
    [TestCase(4095L, true)]
    [TestCase(-4095L, true)]
    [TestCase(4096L, true)]
    [TestCase(-4096L, true)]
    [TestCase(0xFFF000L, true)]
    [TestCase(-0xFFF000L, true)]
    [TestCase(4097L, false)]
    [TestCase(-4097L, false)]
    [TestCase(0x1000000L, false)]
    [TestCase(long.MinValue, false)]
    [TestCase(long.MaxValue, false)]
    public static void AddImmediateUsesTwelveBitsOrTwelveBitShift(long value, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_add(value), Is.EqualTo(expected));
        Assert.That(Emitter.emitIns_valid_imm_for_add(value, EA_4BYTE), Is.EqualTo(expected));
    }

    [TestCase(0L, EA_4BYTE, true)]
    [TestCase(-1L, EA_4BYTE, true)]
    [TestCase(0xFFFFFFFFL, EA_4BYTE, true)]
    [TestCase(0xFFFF0000L, EA_4BYTE, true)]
    [TestCase(0x80000001L, EA_4BYTE, true)]
    [TestCase(0x80000001L, EA_8BYTE, false)]
    [TestCase(0xFF00FF00L, EA_4BYTE, true)]
    [TestCase(0xFF00FF00L, EA_8BYTE, false)]
    [TestCase(0x12345678L, EA_4BYTE, false)]
    [TestCase(0x100000000L, EA_8BYTE, true)]
    [TestCase(0x00FF00FF00FF00FFL, EA_8BYTE, true)]
    [TestCase(long.MinValue + 1, EA_8BYTE, true)]
    [TestCase(0x0123456789ABCDEFL, EA_8BYTE, false)]
    public static void MovImmediatePreservesWidthAndLogicalBitmaskEncoding(
        long value, emitAttr size, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_mov(value, size), Is.EqualTo(expected));
    }

    [TestCase(0.125, true)]
    [TestCase(-0.125, true)]
    [TestCase(0.1328125, true)]
    [TestCase(1.0, true)]
    [TestCase(1.0625, true)]
    [TestCase(31.0, true)]
    [TestCase(-31.0, true)]
    [TestCase(0.0, false)]
    [TestCase(0.0625, false)]
    [TestCase(32.0, false)]
    [TestCase(1.1, false)]
    [TestCase(double.Epsilon, false)]
    [TestCase(double.NaN, false)]
    [TestCase(double.PositiveInfinity, false)]
    [TestCase(double.NegativeInfinity, false)]
    public static void FmovImmediateUsesExactEightBitFloatEncoding(double value, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_fmov(value), Is.EqualTo(expected));
    }

    [Test]
    public static void FmovDoesNotEncodeNegativeZeroOrNonRepresentableNeighbors()
    {
        var negativeZero = BitConverter.Int64BitsToDouble(long.MinValue);
        Assert.Multiple(() => {
            Assert.That(Emitter.emitIns_valid_imm_for_fmov(negativeZero), Is.False);
            Assert.That(Emitter.emitIns_valid_imm_for_fmov(Math.BitIncrement(1.0)), Is.False);
            Assert.That(Emitter.emitIns_valid_imm_for_fmov(Math.BitDecrement(1.0)), Is.False);
        });
    }
}
#endif
