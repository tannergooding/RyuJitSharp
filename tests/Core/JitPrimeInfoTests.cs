// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitPrimeInfoTests
{
    [Test]
    public static void DefaultConstructorClearsMetadata()
    {
        var info = new JitPrimeInfo();
        Assert.That(info.prime, Is.Zero);
        Assert.That(info.magic, Is.Zero);
        Assert.That(info.shift, Is.Zero);
    }

    [Test]
    public static void PrimeTableHasPinnedLength()
    {
        Assert.That(Globals.jitPrimeInfo.Length, Is.EqualTo(27));
    }

    [TestCase(0, 9u, 0x38e38e39u, 1u)]
    [TestCase(1, 23u, 0xb21642c9u, 4u)]
    [TestCase(2, 59u, 0x22b63cbfu, 3u)]
    [TestCase(3, 131u, 0xfa232cf3u, 7u)]
    [TestCase(4, 239u, 0x891ac73bu, 7u)]
    [TestCase(5, 433u, 0x975a751u, 4u)]
    [TestCase(6, 761u, 0x561e46a5u, 8u)]
    [TestCase(7, 1399u, 0xbb612aa3u, 10u)]
    [TestCase(8, 2473u, 0x6a009f01u, 10u)]
    [TestCase(9, 4327u, 0xf2555049u, 12u)]
    [TestCase(10, 7499u, 0x45ea155fu, 11u)]
    [TestCase(11, 12973u, 0x1434f6d3u, 10u)]
    [TestCase(12, 22433u, 0x2ebe18dbu, 12u)]
    [TestCase(13, 46559u, 0xb42bebd5u, 15u)]
    [TestCase(14, 96581u, 0xadb61b1bu, 16u)]
    [TestCase(15, 200341u, 0x29df2461u, 15u)]
    [TestCase(16, 415517u, 0xa181c46du, 18u)]
    [TestCase(17, 861719u, 0x4de0bde5u, 18u)]
    [TestCase(18, 1787021u, 0x9636c46fu, 20u)]
    [TestCase(19, 3705617u, 0x4870adc1u, 20u)]
    [TestCase(20, 7684087u, 0x8bbc5b83u, 22u)]
    [TestCase(21, 15933877u, 0x86c65361u, 23u)]
    [TestCase(22, 33040633u, 0x40fec79bu, 23u)]
    [TestCase(23, 68513161u, 0x7d605cd1u, 25u)]
    [TestCase(24, 142069021u, 0xf1da390bu, 27u)]
    [TestCase(25, 294594427u, 0x74a2507du, 27u)]
    [TestCase(26, 733045421u, 0x5dbec447u, 28u)]
    public static void PinnedMetadataDividesAtUnsignedBoundaries(int index, uint prime, uint magic, uint shift)
    {
        var info = Globals.jitPrimeInfo[index];
        Assert.That(info.prime, Is.EqualTo(prime));
        Assert.That(info.magic, Is.EqualTo(magic));
        Assert.That(info.shift, Is.EqualTo(shift));

        uint largestMultiple = (uint.MaxValue / prime) * prime;
        uint[] numerators = [
            0, 1, prime - 1, prime, prime + 1,
            largestMultiple - 1, largestMultiple, largestMultiple + 1,
            0x7fffffff, 0x80000000, uint.MaxValue - 1, uint.MaxValue,
        ];

        foreach (uint numerator in numerators)
        {
            Assert.That(info.magicNumberDivide(numerator), Is.EqualTo(numerator / prime));
            Assert.That(info.magicNumberRem(numerator), Is.EqualTo(numerator % prime));
        }
    }
}
