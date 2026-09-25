// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class GcInfoBitStreamWriterTests
{
    [Test]
    public static void FixedWidthBitsArePackedLeastSignificantFirst()
    {
        using var writer = new BitStreamWriter();

        writer.Write(0xAB, 0);
        Assert.That(writer.GetBitCount(), Is.EqualTo((nuint)0));
        Assert.That(writer.GetByteCount(), Is.EqualTo((nuint)0));

        writer.Write(0b101, 3);
        writer.Write(0b111101, 4);
        writer.Write(0, 1);
        writer.Write(unchecked((nuint)0x0123456789ABCDEFUL), 64);

        Assert.That(writer.GetBitCount(), Is.EqualTo((nuint)72));
        Assert.That(writer.GetByteCount(), Is.EqualTo((nuint)9));

        var bytes = new byte[9];
        writer.CopyTo(bytes);
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x6D, 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 }));
    }

    [TestCase(63)]
    [TestCase(64)]
    [TestCase(65)]
    [TestCase(1023)]
    [TestCase(1024)]
    [TestCase(1025)]
    [TestCase(2049)]
    public static void WritesAcrossWordsAndMemoryBlocksPreserveAllBits(int prefixBits)
    {
        using var writer = new BitStreamWriter();
        var reference = new ReferenceBits();
        const ulong pattern = 0x9ABCDEF012345678;

        while (prefixBits >= 64)
        {
            writer.Write(unchecked((nuint)pattern), 64);
            reference.Write(pattern, 64);
            prefixBits -= 64;
        }

        writer.Write(unchecked((nuint)pattern), (uint)prefixBits);
        reference.Write(pattern, prefixBits);
        writer.Write(0xE71A5C, 24);
        reference.Write(0xE71A5C, 24);
        writer.Write(nuint.MaxValue, 64);
        reference.Write(0xFFFFFFFFFFFFFFFF, 64);
        AssertStream(writer, reference);
    }

    [TestCase(0UL, 1u)]
    [TestCase(1UL, 1u)]
    [TestCase(2UL, 1u)]
    [TestCase(3UL, 2u)]
    [TestCase(4UL, 2u)]
    [TestCase(127UL, 7u)]
    [TestCase(128UL, 7u)]
    [TestCase(0x3FFFFFFFUL, 3u)]
    [TestCase((ulong)int.MaxValue, 1u)]
    [TestCase((ulong)int.MaxValue, 31u)]
    [TestCase((ulong)int.MaxValue, 63u)]
    public static void UnsignedEncodingMatchesReferenceAndSize(ulong value, uint @base)
    {
        using var writer = new BitStreamWriter();
        var reference = new ReferenceBits();

        writer.Write(0b10101, 5);
        reference.Write(0b10101, 5);

        var bits = writer.EncodeVarLengthUnsigned((nuint)value, @base);
        var expected = reference.WriteUnsigned(value, (int)@base);
        Assert.That(bits, Is.EqualTo(expected));
        Assert.That(BitStreamWriter.SizeofVarLengthUnsigned((nuint)value, @base), Is.EqualTo(expected));
        AssertStream(writer, reference);
    }

    [TestCase(0L, 1u)]
    [TestCase(1L, 2u)]
    [TestCase(2L, 2u)]
    [TestCase(-1L, 1u)]
    [TestCase(-2L, 2u)]
    [TestCase(-3L, 2u)]
    [TestCase(63L, 7u)]
    [TestCase(64L, 7u)]
    [TestCase(-64L, 7u)]
    [TestCase(-65L, 7u)]
    [TestCase(long.MaxValue, 1u)]
    [TestCase(long.MinValue, 1u)]
    [TestCase(long.MaxValue, 31u)]
    [TestCase(long.MinValue, 31u)]
    [TestCase(long.MaxValue, 63u)]
    [TestCase(long.MinValue, 63u)]
    public static void SignedEncodingMatchesReferenceAtSignAndNativeWordBoundaries(long value, uint @base)
    {
        using var writer = new BitStreamWriter();
        var reference = new ReferenceBits();

        writer.Write(0b1101, 4);
        reference.Write(0b1101, 4);

        var bits = writer.EncodeVarLengthSigned((nint)value, @base);
        Assert.That(bits, Is.EqualTo(reference.WriteSigned(value, (int)@base)));
        AssertStream(writer, reference);
    }

    [Test]
    public static void VariableLengthChunksCrossBlockBoundariesWithoutPadding()
    {
        using var writer = new BitStreamWriter();
        var reference = new ReferenceBits();

        for (var i = 0; i < 145; i++)
        {
            var unsignedBits = writer.EncodeVarLengthUnsigned((nuint)(i * 47), 2);
            var expectedUnsigned = reference.WriteUnsigned((ulong)(i * 47), 2);
            var signedBits = writer.EncodeVarLengthSigned(i % 2 == 0 ? -i * 121 : i * 121, 3);
            var expectedSigned = reference.WriteSigned(i % 2 == 0 ? -i * 121 : i * 121, 3);

            Assert.That(unsignedBits, Is.EqualTo(expectedUnsigned));
            Assert.That(signedBits, Is.EqualTo(expectedSigned));
        }

        Assert.That(writer.GetBitCount(), Is.GreaterThan((nuint)2048));
        AssertStream(writer, reference);
    }

    [Test]
    public static void InvalidWidthsAndDestinationsFailWithoutChangingTheStream()
    {
        using var writer = new BitStreamWriter();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => writer.Write(1, 65));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => writer.EncodeVarLengthUnsigned(1, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => writer.EncodeVarLengthSigned(-1, 64));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BitStreamWriter.SizeofVarLengthUnsigned(1, 0));
        Assert.That(writer.GetBitCount(), Is.EqualTo((nuint)0));

        writer.Write(0b101, 3);
        _ = Assert.Throws<ArgumentException>(() => writer.CopyTo([]));

        var bytes = new byte[1];
        writer.CopyTo(bytes);
        Assert.That(bytes[0], Is.EqualTo(0b101));
    }

    private static void AssertStream(BitStreamWriter writer, ReferenceBits reference)
    {
        Assert.That(writer.GetBitCount(), Is.EqualTo((nuint)reference.Count));
        var expected = reference.ToArray();
        Assert.That(writer.GetByteCount(), Is.EqualTo((nuint)expected.Length));

        var actual = new byte[expected.Length];
        writer.CopyTo(actual);
        Assert.That(actual, Is.EqualTo(expected));
    }

    private sealed class ReferenceBits
    {
        private readonly List<bool> m_Bits = [];

        public int Count => m_Bits.Count;

        public void Write(ulong data, int count)
        {
            for (var bit = 0; bit < count; bit++)
            {
                m_Bits.Add(((data >> bit) & 1) != 0);
            }
        }

        public int WriteUnsigned(ulong value, int @base)
        {
            var limit = 1UL << @base;
            var start = Count;

            while (value >= limit)
            {
                Write(value % limit, @base);
                Write(1, 1);
                value /= limit;
            }

            Write(value, @base);
            Write(0, 1);
            return Count - start;
        }

        public int WriteSigned(long value, int @base)
        {
            var remaining = (BigInteger)value;
            var mask = (BigInteger.One << @base) - 1;
            var start = Count;

            while (true)
            {
                var chunk = (ulong)(remaining & mask);
                remaining >>= @base;
                var negative = (chunk & (1UL << (@base - 1))) != 0;
                var done = (negative && remaining == -1) || (!negative && remaining.IsZero);

                Write(chunk, @base);
                Write(done ? 0UL : 1UL, 1);

                if (done)
                {
                    return Count - start;
                }
            }
        }

        public byte[] ToArray()
        {
            var bytes = new byte[(Count + 7) / 8];

            for (var i = 0; i < Count; i++)
            {
                if (m_Bits[i])
                {
                    bytes[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return bytes;
        }
    }
}
