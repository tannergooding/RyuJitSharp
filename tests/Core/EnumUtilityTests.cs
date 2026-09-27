// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class EnumUtilityTests
{
    [TestCase((SignedByte)0, (SignedByte)1, true)]
    [TestCase((SignedByte)(-1), (SignedByte)0, true)]
    [TestCase((SignedByte)127, (SignedByte)(-128), false)]
    [TestCase((UnsignedByte)0, (UnsignedByte)1, true)]
    [TestCase((UnsignedByte)255, (UnsignedByte)0, false)]
    [TestCase((SignedShort)0, (SignedShort)1, true)]
    [TestCase((SignedShort)(-1), (SignedShort)0, true)]
    [TestCase((SignedShort)32767, (SignedShort)(-32768), false)]
    [TestCase((UnsignedShort)0, (UnsignedShort)1, true)]
    [TestCase((UnsignedShort)32767, (UnsignedShort)32768, true)]
    [TestCase((UnsignedShort)65535, (UnsignedShort)0, false)]
    [TestCase((SignedInt)0, (SignedInt)1, true)]
    [TestCase((SignedInt)(-1), (SignedInt)0, true)]
    [TestCase((UnsignedInt)0, (UnsignedInt)1, true)]
    [TestCase((UnsignedInt)int.MaxValue, (UnsignedInt)((uint)int.MaxValue + 1), true)]
    [TestCase((UnsignedInt)uint.MaxValue, (UnsignedInt)0, true)]
    [TestCase((SignedLong)0, (SignedLong)1, true)]
    [TestCase((SignedLong)(-1), (SignedLong)0, true)]
    [TestCase((UnsignedLong)0, (UnsignedLong)1, true)]
    [TestCase((UnsignedLong)long.MaxValue, (UnsignedLong)((ulong)long.MaxValue + 1), true)]
    [TestCase((UnsignedLong)ulong.MaxValue, (UnsignedLong)0, true)]
    public static void ContiguityPreservesUnderlyingTypeArithmetic<TEnum>(TEnum first, TEnum second, bool expected)
        where TEnum : unmanaged, Enum
    {
        Assert.That(AreContiguous(first, second), Is.EqualTo(expected));
    }

    [Test]
    public static void SequencesCheckEveryAdjacentPair()
    {
        Assert.That(AreContiguous<UnsignedShort>([]), Is.True);
        Assert.That(AreContiguous((UnsignedShort)7), Is.True);
        Assert.That(AreContiguous((UnsignedShort)7, (UnsignedShort)8, (UnsignedShort)9), Is.True);
        Assert.That(AreContiguous((UnsignedShort)7, (UnsignedShort)8, (UnsignedShort)10), Is.False);
        Assert.That(AreContiguous((UnsignedShort)7, (UnsignedShort)7), Is.False);
    }

    private enum SignedByte : sbyte
    {
    }

    private enum UnsignedByte : byte
    {
    }

    private enum SignedShort : short
    {
    }

    private enum UnsignedShort : ushort
    {
    }

    private enum SignedInt : int
    {
    }

    private enum UnsignedInt : uint
    {
    }

    private enum SignedLong : long
    {
    }

    private enum UnsignedLong : ulong
    {
    }
}
