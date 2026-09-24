// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class MagicDivideTests
{
    [TestCase(3u)]
    [TestCase(7u)]
    [TestCase(12u)]
    [TestCase(19u)]
    [TestCase(0x80000001u)]
    [TestCase(uint.MaxValue)]
    public static void Unsigned32MagicMatchesQuotients(uint divisor)
    {
        var magic = MagicDivide.GetUnsigned32Magic(divisor, out var increment, out var preShift, out var postShift);
        uint[] numerators = [0, 1, divisor - 1, divisor, unchecked(divisor + 1), 1234567890, uint.MaxValue - 1, uint.MaxValue];

        foreach (var numerator in numerators)
        {
            var adjusted = increment && numerator != uint.MaxValue ? numerator + 1 : numerator;
            adjusted >>= preShift;
            var actual = (uint)(uint.BigMul(adjusted, magic) >> 32 >> postShift);
            Assert.That(actual, Is.EqualTo(numerator / divisor), $"numerator={numerator}, divisor={divisor}");
            Assert.That(numerator - (actual * divisor), Is.EqualTo(numerator % divisor));
        }
    }

    [TestCase(3ul)]
    [TestCase(7ul)]
    [TestCase(12ul)]
    [TestCase(19ul)]
    [TestCase(0x8000000000000001ul)]
    [TestCase(ulong.MaxValue)]
    public static void Unsigned64MagicMatchesQuotients(ulong divisor)
    {
        var magic = MagicDivide.GetUnsigned64Magic(divisor, out var increment, out var preShift, out var postShift);
        ulong[] numerators = [0, 1, divisor - 1, divisor, unchecked(divisor + 1), 12345678901234567890, ulong.MaxValue - 1, ulong.MaxValue];

        foreach (var numerator in numerators)
        {
            var adjusted = increment && numerator != ulong.MaxValue ? numerator + 1 : numerator;
            adjusted >>= preShift;
            var actual = Math.BigMul(adjusted, magic, out _) >> postShift;
            Assert.That(actual, Is.EqualTo(numerator / divisor), $"numerator={numerator}, divisor={divisor}");
            Assert.That(numerator - (actual * divisor), Is.EqualTo(numerator % divisor));
        }
    }

    [TestCase(3)]
    [TestCase(-3)]
    [TestCase(7)]
    [TestCase(-7)]
    [TestCase(13)]
    [TestCase(-19)]
    public static void Signed32MagicMatchesQuotients(int divisor)
    {
        var magic = MagicDivide.GetSigned32Magic(divisor, out var shift);
        int[] numerators = [int.MinValue, -123456789, -1, 0, 1, 123456789, int.MaxValue];
        foreach (var numerator in numerators)
        {
            var high = (int)(int.BigMul(numerator, magic) >> 32);
            var adjusted = Math.Sign(divisor) != Math.Sign(magic)
                ? divisor > 0 ? unchecked(high + numerator) : unchecked(high - numerator)
                : high;
            var actual = unchecked((adjusted >> shift) + (int)((uint)adjusted >> 31));
            Assert.That(actual, Is.EqualTo(numerator / divisor), $"numerator={numerator}, divisor={divisor}");
            Assert.That(numerator - (actual * divisor), Is.EqualTo(numerator % divisor));
        }
    }

    [TestCase(3L)]
    [TestCase(-3L)]
    [TestCase(7L)]
    [TestCase(-7L)]
    [TestCase(13L)]
    [TestCase(-19L)]
    public static void Signed64MagicMatchesQuotients(long divisor)
    {
        var magic = MagicDivide.GetSigned64Magic(divisor, out var shift);
        long[] numerators = [long.MinValue, -12345678901234567, -1, 0, 1, 12345678901234567, long.MaxValue];
        foreach (var numerator in numerators)
        {
            var high = Math.BigMul(numerator, magic, out _);
            var adjusted = Math.Sign(divisor) != Math.Sign(magic)
                ? divisor > 0 ? unchecked(high + numerator) : unchecked(high - numerator)
                : high;
            var actual = unchecked((adjusted >> shift) + (long)((ulong)adjusted >> 63));
            Assert.That(actual, Is.EqualTo(numerator / divisor), $"numerator={numerator}, divisor={divisor}");
            Assert.That(numerator - (actual * divisor), Is.EqualTo(numerator % divisor));
        }
    }

    [Test]
    public static void NonTableMagicMatchesRepresentativeQuotients()
    {
        for (var divisor = 13; divisor <= 73; divisor++)
        {
            if ((divisor & (divisor - 1)) == 0)
            {
                continue;
            }

            Unsigned32MagicMatchesQuotients((uint)divisor);
            Unsigned64MagicMatchesQuotients((ulong)divisor);
            Signed32MagicMatchesQuotients(divisor);
            Signed64MagicMatchesQuotients(divisor);
        }
    }

    [TestCase(3u, 8u)]
    [TestCase(7u, 8u)]
    [TestCase(11u, 12u)]
    [TestCase(46u, 16u)]
    public static void ReducedNumeratorWidthRetainsCorrectUnsignedQuotients(uint divisor, uint bits)
    {
        var magic = MagicDivide.GetUnsigned32Magic(divisor, out var increment, out var preShift, out var postShift, bits);
        var maxNumerator = (1u << (int)bits) - 1;

        for (uint numerator = 0; numerator <= maxNumerator; numerator += Math.Max(1u, maxNumerator / 97))
        {
            var adjusted = increment && numerator != uint.MaxValue ? numerator + 1 : numerator;
            adjusted >>= preShift;
            var quotient = (uint)(uint.BigMul(adjusted, magic) >> 32 >> postShift);
            Assert.That(quotient, Is.EqualTo(numerator / divisor), $"numerator={numerator}, divisor={divisor}, bits={bits}");
        }
    }

    [TestCase(7ul, 12u)]
    [TestCase(46ul, 16u)]
    [TestCase(0x100000007ul, 40u)]
    public static void ReducedNativeWidthRetainsCorrectUnsignedQuotients(ulong divisor, uint bits)
    {
        var magic = MagicDivide.GetUnsigned64Magic(divisor, out var increment, out var preShift, out var postShift, bits);
        var maximum = (1ul << (int)bits) - 1;
        for (ulong numerator = 0; numerator <= maximum; numerator += Math.Max(1ul, maximum / 113))
        {
            var adjusted = increment && numerator != ulong.MaxValue ? numerator + 1 : numerator;
            adjusted >>= preShift;
            var quotient = Math.BigMul(adjusted, magic, out _) >> postShift;
            Assert.That(quotient, Is.EqualTo(numerator / divisor),
                $"numerator={numerator}, divisor={divisor}, bits={bits}");
        }
    }
}
