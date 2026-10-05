// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class SafeCvtTests
{
    [Test]
    public static void AssertConversionAcceptsSignedAndUnsignedDestinationBoundaries()
    {
        Assert.That(SafeCvtAssert<sbyte, long>(sbyte.MinValue), Is.EqualTo(sbyte.MinValue));
        Assert.That(SafeCvtAssert<sbyte, long>(sbyte.MaxValue), Is.EqualTo(sbyte.MaxValue));
        Assert.That(SafeCvtAssert<byte, long>(byte.MaxValue), Is.EqualTo(byte.MaxValue));
        Assert.That(SafeCvtAssert<short, long>(short.MinValue), Is.EqualTo(short.MinValue));
        Assert.That(SafeCvtAssert<short, long>(short.MaxValue), Is.EqualTo(short.MaxValue));
        Assert.That(SafeCvtAssert<ushort, long>(ushort.MaxValue), Is.EqualTo(ushort.MaxValue));
        Assert.That(SafeCvtAssert<int, long>(int.MinValue), Is.EqualTo(int.MinValue));
        Assert.That(SafeCvtAssert<int, long>(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(SafeCvtAssert<uint, long>(uint.MaxValue), Is.EqualTo(uint.MaxValue));
        Assert.That(SafeCvtAssert<long, long>(long.MinValue), Is.EqualTo(long.MinValue));
        Assert.That(SafeCvtAssert<long, ulong>(long.MaxValue), Is.EqualTo(long.MaxValue));
        Assert.That(SafeCvtAssert<ulong, ulong>(ulong.MaxValue), Is.EqualTo(ulong.MaxValue));
        Assert.That(SafeCvtAssert<nint, nint>(nint.MinValue), Is.EqualTo(nint.MinValue));
        Assert.That(SafeCvtAssert<nuint, nuint>(nuint.MaxValue), Is.EqualTo(nuint.MaxValue));
    }

    [Test]
    public static void NowayAssertConversionAcceptsSignedAndUnsignedDestinationBoundaries()
    {
        Assert.That(SafeCvtNowayAssert<sbyte, long>(sbyte.MinValue), Is.EqualTo(sbyte.MinValue));
        Assert.That(SafeCvtNowayAssert<sbyte, long>(sbyte.MaxValue), Is.EqualTo(sbyte.MaxValue));
        Assert.That(SafeCvtNowayAssert<byte, long>(byte.MaxValue), Is.EqualTo(byte.MaxValue));
        Assert.That(SafeCvtNowayAssert<short, long>(short.MinValue), Is.EqualTo(short.MinValue));
        Assert.That(SafeCvtNowayAssert<short, long>(short.MaxValue), Is.EqualTo(short.MaxValue));
        Assert.That(SafeCvtNowayAssert<ushort, long>(ushort.MaxValue), Is.EqualTo(ushort.MaxValue));
        Assert.That(SafeCvtNowayAssert<int, long>(int.MinValue), Is.EqualTo(int.MinValue));
        Assert.That(SafeCvtNowayAssert<int, long>(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(SafeCvtNowayAssert<uint, long>(uint.MaxValue), Is.EqualTo(uint.MaxValue));
        Assert.That(SafeCvtNowayAssert<long, long>(long.MinValue), Is.EqualTo(long.MinValue));
        Assert.That(SafeCvtNowayAssert<long, ulong>(long.MaxValue), Is.EqualTo(long.MaxValue));
        Assert.That(SafeCvtNowayAssert<ulong, ulong>(ulong.MaxValue), Is.EqualTo(ulong.MaxValue));
        Assert.That(SafeCvtNowayAssert<nint, nint>(nint.MinValue), Is.EqualTo(nint.MinValue));
        Assert.That(SafeCvtNowayAssert<nuint, nuint>(nuint.MaxValue), Is.EqualTo(nuint.MaxValue));
    }

    [TestCase(-129L, Destination.SByte)]
    [TestCase(128L, Destination.SByte)]
    [TestCase(-1L, Destination.Byte)]
    [TestCase(256L, Destination.Byte)]
    [TestCase(-32769L, Destination.Int16)]
    [TestCase(32768L, Destination.Int16)]
    [TestCase(-1L, Destination.UInt16)]
    [TestCase(65536L, Destination.UInt16)]
    [TestCase(-2147483649L, Destination.Int32)]
    [TestCase(2147483648L, Destination.Int32)]
    [TestCase(-1L, Destination.UInt32)]
    [TestCase(4294967296L, Destination.UInt32)]
    public static void NowayAssertRejectsValuesOutsideDestinationRange(long value, Destination destination)
    {
        switch (destination)
        {
            case Destination.SByte:
                Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<sbyte, long>(value));
                break;
            case Destination.Byte:
                Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<byte, long>(value));
                break;
            case Destination.Int16:
                Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<short, long>(value));
                break;
            case Destination.UInt16:
                Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<ushort, long>(value));
                break;
            case Destination.Int32:
                Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<int, long>(value));
                break;
            case Destination.UInt32:
                Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<uint, long>(value));
                break;
            default:
                Assert.Fail($"Unexpected destination type {destination}.");
                break;
        }
    }

    [Test]
    public static void NowayAssertRejectsUnsignedOverflowIntoSignedDestination()
    {
        Assert.Throws<FatalJitException>(() => SafeCvtNowayAssert<long, ulong>(ulong.MaxValue));
    }

    private enum Destination
    {
        SByte,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
    }
}
