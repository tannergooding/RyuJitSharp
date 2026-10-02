// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LittleEndianReaderTests
{
    [Test, Combinatorial]
    public static void IntegerReadsPreserveWidthSignAndByteOrder(
        [Values(0UL, ulong.MaxValue, 0x8000000000000000UL, 0x7FFFFFFFFFFFFFFFUL,
            0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL, 0x80000000UL, 0x8000UL, 0x80UL)] ulong bits,
        [Values(0, 1, 2, 3, 4, 5, 6, 7)] int offset)
    {
        var buffer = stackalloc byte[16];
        WriteBytes(buffer, offset, bits, sizeof(ulong));
        var ptr = buffer + offset;

        Assert.That(getU1LittleEndian(ptr), Is.EqualTo(unchecked((byte)bits)));
        Assert.That(getU2LittleEndian(ptr), Is.EqualTo(unchecked((ushort)bits)));
        Assert.That(getU4LittleEndian(ptr), Is.EqualTo(unchecked((uint)bits)));
        Assert.That(getI1LittleEndian(ptr), Is.EqualTo(unchecked((sbyte)bits)));
        Assert.That(getI2LittleEndian(ptr), Is.EqualTo(unchecked((short)bits)));
        Assert.That(getI4LittleEndian(ptr), Is.EqualTo(unchecked((int)bits)));
        Assert.That(getI8LittleEndian(ptr), Is.EqualTo(unchecked((long)bits)));
        AssertBytesUnchanged(buffer, offset, bits, sizeof(ulong));
    }

    [Test, Combinatorial]
    public static void SingleReadsPreserveRawZerosInfinitiesAndNaNPayloads(
        [Values(0U, 0x80000000U, 0x3F800000U, 0xBF800000U, 0x7F7FFFFFU, 0xFF7FFFFFU,
            1U, 0x80000001U, 0x7F800000U, 0xFF800000U, 0x7F800001U, 0xFFC12345U)] uint bits,
        [Values(0, 1, 2, 3, 4, 5, 6, 7)] int offset)
    {
        var buffer = stackalloc byte[16];
        WriteBytes(buffer, offset, bits, sizeof(float));

        var actual = getR4LittleEndian(buffer + offset);

        Assert.That(BitConverter.SingleToUInt32Bits(actual), Is.EqualTo(bits));
        AssertBytesUnchanged(buffer, offset, bits, sizeof(float));
    }

    [Test, Combinatorial]
    public static void DoubleReadsPreserveRawZerosInfinitiesAndNaNPayloads(
        [Values(0UL, 0x8000000000000000UL, 0x3FF0000000000000UL, 0xBFF0000000000000UL,
            0x7FEFFFFFFFFFFFFFUL, 0xFFEFFFFFFFFFFFFFUL, 1UL, 0x8000000000000001UL,
            0x7FF0000000000000UL, 0xFFF0000000000000UL, 0x7FF0000000000001UL, 0xFFF8123456789ABCUL)] ulong bits,
        [Values(0, 1, 2, 3, 4, 5, 6, 7)] int offset)
    {
        var buffer = stackalloc byte[16];
        WriteBytes(buffer, offset, bits, sizeof(double));

        var actual = getR8LittleEndian(buffer + offset);

        Assert.That(BitConverter.DoubleToUInt64Bits(actual), Is.EqualTo(bits));
        AssertBytesUnchanged(buffer, offset, bits, sizeof(double));
    }

    private static void WriteBytes(byte* buffer, int offset, ulong bits, int size)
    {
        new Span<byte>(buffer, 16).Fill(0xA5);

        for (var index = 0; index < size; index++)
        {
            buffer[offset + index] = unchecked((byte)(bits >> (index * 8)));
        }
    }

    private static void AssertBytesUnchanged(byte* buffer, int offset, ulong bits, int size)
    {
        for (var index = 0; index < 16; index++)
        {
            var expected = ((index >= offset) && (index < offset + size))
                ? unchecked((byte)(bits >> ((index - offset) * 8))) : (byte)0xA5;

            Assert.That(buffer[index], Is.EqualTo(expected));
        }
    }
}
