// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    internal static unsafe byte encodeUnsigned(byte* dest, uint value)
    {
        byte size = 1;
        var tmp = value;
        while (tmp > 0x7F)
        {
            tmp >>= 7;
            assert(size < 6);
            size++;
        }

        if (dest != null)
        {
            var p = dest + size;
            byte cont = 0;
            while (value > 0x7F)
            {
                *--p = (byte)(cont | (value & 0x7F));
                value >>= 7;
                cont = 0x80;
            }

            *--p = (byte)(cont | (byte)value);
            assert(p == dest);
        }

        return size;
    }

    internal static unsafe byte encodeUDelta(byte* dest, uint value, uint lastValue)
    {
        assert(value >= lastValue);
        return encodeUnsigned(dest, unchecked(value - lastValue));
    }

    internal static unsafe byte encodeSigned(byte* dest, int val)
    {
        byte size = 1;
        var negative = val < 0;
        // Native signed negation overflows for int.MinValue; unsigned subtraction preserves its Windows x64 wraparound bits.
        var value = negative ? unchecked(0u - (uint)val) : (uint)val;
        var tmp = value;

        while (tmp > 0x3F)
        {
            tmp >>= 7;
            assert(size < 16);
            size++;
        }

        if (dest != null)
        {
            var p = dest + size;
            byte cont = 0;
            while (value > 0x3F)
            {
                *--p = (byte)(cont | (value & 0x7F));
                value >>= 7;
                cont = 0x80;
            }

            var neg = negative ? 0x40 : 0;
            *--p = (byte)(neg | cont | (byte)value);
            assert(p == dest);
        }

        return size;
    }
}
