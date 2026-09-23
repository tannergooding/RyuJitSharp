// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.Numerics;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CheckedCastTests
{
    [TestCase(TYP_BYTE, -128L, 127UL)]
    [TestCase(TYP_UBYTE, 0L, 255UL)]
    [TestCase(TYP_SHORT, -32768L, 32767UL)]
    [TestCase(TYP_USHORT, 0L, 65535UL)]
    [TestCase(TYP_INT, int.MinValue, (ulong)int.MaxValue)]
    [TestCase(TYP_UINT, 0L, (ulong)uint.MaxValue)]
    [TestCase(TYP_LONG, long.MinValue, (ulong)long.MaxValue)]
    [TestCase(TYP_ULONG, 0L, ulong.MaxValue)]
    public static void IntegralBoundaries(var_types type, long minimum, ulong maximum)
    {
        var min = new BigInteger(minimum);
        var max = new BigInteger(maximum);
        BigInteger[] values = [
            min - 1, min, min + 1, max - 1, max, max + 1, -1, 0, 1,
            int.MinValue, int.MaxValue, uint.MaxValue, long.MinValue, long.MaxValue, ulong.MaxValue,
        ];

        foreach (var value in values)
        {
            var intValue = int.CreateTruncating(value);
            var uintValue = unchecked((uint)intValue);
            var longValue = long.CreateTruncating(value);
            var ulongValue = unchecked((ulong)longValue);
            Assert.That(CheckedOps.CastFromIntOverflows(intValue, type, false), Is.EqualTo(Overflows(intValue, min, max)));
            Assert.That(CheckedOps.CastFromIntOverflows(intValue, type, true), Is.EqualTo(Overflows(uintValue, min, max)));
            Assert.That(CheckedOps.CastFromLongOverflows(longValue, type, false), Is.EqualTo(Overflows(longValue, min, max)));
            Assert.That(CheckedOps.CastFromLongOverflows(longValue, type, true), Is.EqualTo(Overflows(ulongValue, min, max)));

            CheckFitsIn(type, intValue, min, max);
            CheckFitsIn(type, uintValue, min, max);
            CheckFitsIn(type, longValue, min, max);
            CheckFitsIn(type, ulongValue, min, max);
            CheckFitsIn(type, nint.CreateTruncating(value), min, max);
            CheckFitsIn(type, nuint.CreateTruncating(value), min, max);
        }
    }

    [TestCase(TYP_BYTE, -128L, 127UL)]
    [TestCase(TYP_UBYTE, 0L, 255UL)]
    [TestCase(TYP_SHORT, -32768L, 32767UL)]
    [TestCase(TYP_USHORT, 0L, 65535UL)]
    [TestCase(TYP_INT, int.MinValue, (ulong)int.MaxValue)]
    [TestCase(TYP_UINT, 0L, (ulong)uint.MaxValue)]
    [TestCase(TYP_LONG, long.MinValue, (ulong)long.MaxValue)]
    [TestCase(TYP_ULONG, 0L, ulong.MaxValue)]
    public static void FloatingBoundaries(var_types type, long minimum, ulong maximum)
    {
        var min = new BigInteger(minimum);
        var max = new BigInteger(maximum);
        var lower = (double)minimum - 1;
        var upper = (double)maximum + 1;
        double[] doubles = [
            double.BitDecrement(lower), lower, double.BitIncrement(lower),
            double.BitDecrement(upper), upper, double.BitIncrement(upper),
            -1.0, -0.5, -0.0, 0.0, 0.5, 1.0,
            double.Epsilon, -double.Epsilon, double.MaxValue, double.MinValue,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity,
        ];

        foreach (var value in doubles)
        {
            var expected = !double.IsFinite(value) || Overflows(new BigInteger(value), min, max);
            Assert.That(CheckedOps.CastFromDoubleOverflows(value, type), Is.EqualTo(expected), $"Source: {value:R}");
        }

        var lowerFloat = (float)lower;
        var upperFloat = (float)upper;
        float[] floats = [
            float.BitDecrement(lowerFloat), lowerFloat, float.BitIncrement(lowerFloat),
            float.BitDecrement(upperFloat), upperFloat, float.BitIncrement(upperFloat),
            -1.0f, -0.5f, -0.0f, 0.0f, 0.5f, 1.0f,
            float.Epsilon, -float.Epsilon, float.MaxValue, float.MinValue,
            float.NaN, float.PositiveInfinity, float.NegativeInfinity,
        ];

        foreach (var value in floats)
        {
            var expected = !float.IsFinite(value) || Overflows(new BigInteger(value), min, max);
            Assert.That(CheckedOps.CastFromFloatOverflows(value, type), Is.EqualTo(expected), $"Source: {value:R}");
        }
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void FloatingDestinationsNeverOverflow(var_types type)
    {
        long[] integers = [long.MinValue, int.MinValue, -1, 0, 1, int.MaxValue, long.MaxValue];

        foreach (var value in integers)
        {
            Assert.That(CheckedOps.CastFromIntOverflows(unchecked((int)value), type, false), Is.False);
            Assert.That(CheckedOps.CastFromIntOverflows(unchecked((int)value), type, true), Is.False);
            Assert.That(CheckedOps.CastFromLongOverflows(value, type, false), Is.False);
            Assert.That(CheckedOps.CastFromLongOverflows(value, type, true), Is.False);
        }

        double[] values = [double.MinValue, -0.0, 0.0, double.MaxValue, double.NaN, double.NegativeInfinity, double.PositiveInfinity];

        foreach (var value in values)
        {
            Assert.That(CheckedOps.CastFromFloatOverflows((float)value, type), Is.False);
            Assert.That(CheckedOps.CastFromDoubleOverflows(value, type), Is.False);
        }
    }

    [TestCase(TYP_UNDEF)]
    [TestCase(TYP_VOID)]
    [TestCase(TYP_REF)]
    [TestCase(TYP_STRUCT)]
    public static void InvalidDestinationsAreRejected(var_types type)
    {
        _ = Assert.Throws<UnreachableException>(() => Globals.FitsIn(type, 0));
        _ = Assert.Throws<UnreachableException>(() => CheckedOps.CastFromIntOverflows(0, type, false));
        _ = Assert.Throws<UnreachableException>(() => CheckedOps.CastFromLongOverflows(0, type, false));
        _ = Assert.Throws<UnreachableException>(() => CheckedOps.CastFromFloatOverflows(0, type));
        _ = Assert.Throws<UnreachableException>(() => CheckedOps.CastFromDoubleOverflows(0, type));
    }

    private static void CheckFitsIn<T>(var_types type, T value, BigInteger minimum, BigInteger maximum)
        where T : IBinaryInteger<T>
    {
        var expected = !Overflows(BigInteger.CreateChecked(value), minimum, maximum);
        Assert.That(Globals.FitsIn(type, value), Is.EqualTo(expected), $"Source: {value} ({typeof(T)})");
    }

    private static bool Overflows(BigInteger value, BigInteger minimum, BigInteger maximum) => value < minimum || value > maximum;
}
