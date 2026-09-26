// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RyuJitSharp;

internal static class PortingHardwareCorpus
{
    public static int Main()
    {
        if (VectorAddGetLane(9, -4) != 5)
        {
            return 1;
        }

        if (VectorCreateGetLane(6, 13) != 11)
        {
            return 2;
        }

        if (ImmediateShift(5) != 40)
        {
            return 3;
        }

        if (VariableShift(7, 3) != 56)
        {
            return 4;
        }

        if (ByrefLoadStore(8, 5) != 14)
        {
            return 5;
        }

        if (BmiExtract(0xDEADBEEFu, 4, 8) != 0xEE)
        {
            return 6;
        }

        if (Crc32C(0x12345678u, 0xABCDEF01u) != 0x39BCB2DDu)
        {
            return 7;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int VectorAddGetLane(int left, int right)
    {
        var sum = Vector128.Create(left) + Vector128.Create(right);
        return sum.GetElement(1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int VectorCreateGetLane(int left, int right)
    {
        var vector = Vector128.Create(left, right, left + 3, right - 2);
        return vector.GetElement(3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ImmediateShift(int value)
    {
        if (!Sse2.IsSupported)
        {
            return value << 3;
        }

        var vector = Vector128.Create(value).AsUInt32();
        return Sse2.ShiftLeftLogical(vector, 3).AsInt32().GetElement(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int VariableShift(int value, byte amount)
    {
        if (!Sse2.IsSupported)
        {
            return value << amount;
        }

        var vector = Vector128.Create(value).AsUInt32();
#pragma warning disable CA1857 // This operand must remain nonconstant to exercise the immediate fallback.
        return Sse2.ShiftLeftLogical(vector, amount).AsInt32().GetElement(0);
#pragma warning restore CA1857
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe int ByrefLoadStore(int first, int second)
    {
        if (!Sse2.IsSupported)
        {
            return second + first + 1;
        }

        var source = stackalloc int[4];
        source[0] = first;
        source[1] = second;
        source[2] = first + 1;
        source[3] = second + 1;

        var destination = stackalloc int[4];
        Sse2.Store(destination, Sse2.LoadVector128(source));
        return destination[1] + destination[2];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint BmiExtract(uint value, byte start, byte length)
    {
        if (!Bmi1.IsSupported)
        {
            if (start >= 32)
            {
                return 0;
            }

            var shifted = value >> start;
            return length >= 32 ? shifted : shifted & ((1u << length) - 1);
        }

        var control = (ushort)(start | (length << 8));
        return Bmi1.BitFieldExtract(value, control);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint Crc32C(uint seed, uint value)
    {
        if (Sse42.IsSupported)
        {
            return Sse42.Crc32(seed, value);
        }

        var crc = seed ^ value;
        for (var bit = 0; bit < 32; bit++)
        {
            crc = (crc >> 1) ^ (((crc & 1) != 0) ? 0x82F63B78u : 0);
        }
        return crc;
    }
}
