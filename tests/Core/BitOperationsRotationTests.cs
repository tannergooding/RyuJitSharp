// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

#if WINDOWS_AMD64_ABI
internal static class BitOperationsRotationTests
{
    private static readonly uint[] s_offsets =
    [
        0, 1, 7, 8, 15, 16, 31, 32, 33, 63, 64, 65, 127, 128, 0x80000000, uint.MaxValue,
    ];

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "EvalOp")]
    private static extern T EvalOpAccessor<T>(ValueNumStore? _, VNFunc func, T left, T right)
        where T : unmanaged, INumber<T>;

    private static IEnumerable<TestCaseData> UInt32Cases()
    {
        foreach (var offset in s_offsets)
        {
            yield return new TestCaseData(0x89ABCDEFu, offset);
        }

        uint[] values =
        [
            0, uint.MaxValue, 1, 0x80000000, 0x80000001, 0xAAAAAAAA, 0x55555555, 0x00010000,
        ];

        foreach (var value in values)
        {
            yield return new TestCaseData(value, 1u);
            yield return new TestCaseData(value, 31u);
        }
    }

    private static IEnumerable<TestCaseData> UInt64Cases()
    {
        foreach (var offset in s_offsets)
        {
            yield return new TestCaseData(0x89ABCDEF01234567UL, offset);
        }

        ulong[] values =
        [
            0, ulong.MaxValue, 1, 0x8000000000000000, 0x8000000000000001,
            0xAAAAAAAAAAAAAAAA, 0x5555555555555555, 0x0000000100000000,
        ];

        foreach (var value in values)
        {
            yield return new TestCaseData(value, 1u);
            yield return new TestCaseData(value, 63u);
        }
    }

    [TestCaseSource(nameof(UInt32Cases))]
    public static void UInt32RotateLeft(uint value, uint offset)
    {
        var expected = (uint)PermuteBits(value, offset, 32, left: true);
        var count = unchecked((int)offset);

        Assert.That(BitOperations.RotateLeft(value, count), Is.EqualTo(expected));
        Assert.That(EvalOpAccessor<int>(null, VNFunc.VNF_ROL, unchecked((int)value), count),
            Is.EqualTo(unchecked((int)expected)));
    }

    [TestCaseSource(nameof(UInt32Cases))]
    public static void UInt32RotateRight(uint value, uint offset)
    {
        var expected = (uint)PermuteBits(value, offset, 32, left: false);
        var count = unchecked((int)offset);

        Assert.That(BitOperations.RotateRight(value, count), Is.EqualTo(expected));
        Assert.That(EvalOpAccessor<int>(null, VNFunc.VNF_ROR, unchecked((int)value), count),
            Is.EqualTo(unchecked((int)expected)));
    }

    [TestCaseSource(nameof(UInt64Cases))]
    public static void UInt64RotateLeft(ulong value, uint offset)
    {
        var expected = PermuteBits(value, offset, 64, left: true);
        var count = unchecked((int)offset);

        Assert.That(BitOperations.RotateLeft(value, count), Is.EqualTo(expected));
        Assert.That(EvalOpAccessor<long>(null, VNFunc.VNF_ROL, unchecked((long)value), offset),
            Is.EqualTo(unchecked((long)expected)));
        Assert.That(EvalOpAccessor<long>(null, VNFunc.VNF_ROL, unchecked((long)value), count),
            Is.EqualTo(unchecked((long)expected)));
    }

    [TestCaseSource(nameof(UInt64Cases))]
    public static void UInt64RotateRight(ulong value, uint offset)
    {
        var expected = PermuteBits(value, offset, 64, left: false);
        var count = unchecked((int)offset);

        Assert.That(BitOperations.RotateRight(value, count), Is.EqualTo(expected));
        Assert.That(EvalOpAccessor<long>(null, VNFunc.VNF_ROR, unchecked((long)value), offset),
            Is.EqualTo(unchecked((long)expected)));
        Assert.That(EvalOpAccessor<long>(null, VNFunc.VNF_ROR, unchecked((long)value), count),
            Is.EqualTo(unchecked((long)expected)));
    }

    private static ulong PermuteBits(ulong value, uint offset, int width, bool left)
    {
        // Place each set bit independently instead of reproducing the native shift pair.
        var displacement = (int)(offset % (uint)width);
        ulong result = 0;

        for (var source = 0; source < width; source++)
        {
            if ((value & (1UL << source)) != 0)
            {
                var destination = left
                    ? (source + displacement) % width
                    : (source + width - displacement) % width;

                result |= 1UL << destination;
            }
        }

        return result;
    }
}
#endif
