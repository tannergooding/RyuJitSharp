// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static class MagicDivide
{
    public static uint GetUnsigned32Magic(uint divisor, out bool increment, out int preShift, out int postShift,
        uint bits = 32)
    {
        assert(bits <= 32);
        return GetUnsignedMagic(divisor, out increment, out preShift, out postShift, bits);
    }

    public static ulong GetUnsigned64Magic(ulong divisor, out bool increment, out int preShift, out int postShift,
        uint bits = 64)
    {
        assert(bits <= 64);
        return GetUnsignedMagic(divisor, out increment, out preShift, out postShift, bits);
    }

    // Matches utils.cpp's round-up/round-down selection from "Faster Unsigned Division by Constants":
    // https://ridiculousfish.com/files/faster_unsigned_division_by_constants.pdf
    // An increment requests saturating increment of the numerator before the high multiply.
    private static T GetUnsignedMagic<T>(T divisor, out bool increment, out int preShift, out int postShift, uint bits)
        where T : unmanaged, IBinaryInteger<T>, IUnsignedNumber<T>
    {
        var width = (uint)(Unsafe.SizeOf<T>() * 8);
        assert((divisor >= T.CreateTruncating(3)) && !T.IsPow2(divisor));
        assert((bits > 0) && (bits <= width));

        if (bits == width)
        {
            if (TryGetUnsignedMagic(divisor, out var magic, out increment, out postShift))
            {
                preShift = 0;
                return magic;
            }
        }

        var extraShift = width - bits;
        var initialPowerOfTwo = T.One << (int)(width - 1);
        var quotient = initialPowerOfTwo / divisor;
        var remainder = initialPowerOfTwo % divisor;
        var downMultiplier = T.Zero;
        uint downExponent = 0;
        var hasMagicDown = false;

        uint ceilLog2Divisor = 0;
        for (var value = divisor; value > T.Zero; value >>= 1)
        {
            ceilLog2Divisor++;
        }

        uint exponent;
        for (exponent = 0; ; exponent++)
        {
            if (remainder >= divisor - remainder)
            {
                quotient = unchecked(quotient * T.CreateTruncating(2) + T.One);
                remainder = unchecked(remainder * T.CreateTruncating(2) - divisor);
            }
            else
            {
                quotient = unchecked(quotient * T.CreateTruncating(2));
                remainder = unchecked(remainder * T.CreateTruncating(2));
            }

            // Test the bound first: the exponent can exceed the supported shift width.
            if ((exponent + extraShift >= ceilLog2Divisor) ||
                (divisor - remainder) <= (T.One << (int)(exponent + extraShift)))
            {
                break;
            }

            if (!hasMagicDown && remainder <= (T.One << (int)(exponent + extraShift)))
            {
                hasMagicDown = true;
                downMultiplier = quotient;
                downExponent = exponent;
            }
        }

        if (exponent < ceilLog2Divisor)
        {
            increment = false;
            preShift = 0;
            postShift = (int)exponent;
            return unchecked(quotient + T.One);
        }

        if ((divisor & T.One) != T.Zero)
        {
            assert(hasMagicDown);
            increment = true;
            preShift = 0;
            postShift = (int)downExponent;
            return downMultiplier;
        }

        uint shift = 0;
        var shiftedDivisor = divisor;
        while ((shiftedDivisor & T.One) == T.Zero)
        {
            shiftedDivisor >>= 1;
            shift++;
        }

        var result = GetUnsignedMagic(shiftedDivisor, out increment, out preShift, out postShift, bits - shift);
        assert(!increment && (preShift == 0));
        preShift = (int)shift;
        return result;
    }

    private static bool TryGetUnsignedMagic<T>(T divisor, out T magic, out bool increment, out int postShift)
        where T : unmanaged, IBinaryInteger<T>, IUnsignedNumber<T>
    {
        var value = ulong.CreateTruncating(divisor);
        var wide = Unsafe.SizeOf<T>() == sizeof(ulong);
        ulong tableMagic = value switch
        {
            3 => wide ? 0xAAAAAAAAAAAAAAABul : 0xAAAAAAABul,
            5 => wide ? 0xCCCCCCCCCCCCCCCDul : 0xCCCCCCCDul,
            6 => wide ? 0xAAAAAAAAAAAAAAABul : 0xAAAAAAABul,
            7 => wide ? 0x9249249249249249ul : 0x49249249ul,
            9 => wide ? 0xE38E38E38E38E38Ful : 0x38E38E39ul,
            10 => wide ? 0xCCCCCCCCCCCCCCCDul : 0xCCCCCCCDul,
            11 => wide ? 0x2E8BA2E8BA2E8BA3ul : 0xBA2E8BA3ul,
            12 => wide ? 0xAAAAAAAAAAAAAAABul : 0xAAAAAAABul,
            _ => 0,
        };
        magic = T.CreateTruncating(tableMagic);
        increment = value == 7;
        postShift = value switch
        {
            3 => 1,
            9 => wide ? 3 : 1,
            5 or 6 => 2,
            7 => wide ? 2 : 1,
            10 or 12 => 3,
            11 => wide ? 1 : 3,
            _ => 0,
        };
        return tableMagic != 0;
    }

    // The signed recurrences follow utils.cpp's GetSignedMagic and
    // The PowerPC Compiler Writer's Guide, pp. 57-58. All unsigned arithmetic
    // wraps at the operand width while searching for the multiplier and shift.
    public static int GetSigned32Magic(int divisor, out int shift)
    {
        if (TryGetSignedMagic(divisor, wide: false, out var magic, out shift))
        {
            return unchecked((int)magic);
        }

        const int width = 32;
        const uint signBit = 1u << (width - 1);
        var absDivisor = unchecked((uint)Math.Abs((long)divisor));
        var t = unchecked(signBit + (unchecked((uint)divisor) >> (width - 1)));
        var absNc = unchecked(t - 1 - (t % absDivisor));
        var p = width - 1;
        var q1 = signBit / absNc;
        var r1 = unchecked(signBit - q1 * absNc);
        var q2 = signBit / absDivisor;
        var r2 = unchecked(signBit - q2 * absDivisor);
        uint delta;

        do
        {
            p++;
            q1 = unchecked(q1 * 2);
            r1 = unchecked(r1 * 2);
            if (r1 >= absNc)
            {
                q1 = unchecked(q1 + 1);
                r1 = unchecked(r1 - absNc);
            }
            q2 = unchecked(q2 * 2);
            r2 = unchecked(r2 * 2);
            if (r2 >= absDivisor)
            {
                q2 = unchecked(q2 + 1);
                r2 = unchecked(r2 - absDivisor);
            }
            delta = unchecked(absDivisor - r2);
        } while (q1 < delta || (q1 == delta && r1 == 0));

        shift = p - width;
        var result = unchecked((int)(q2 + 1));
        return divisor < 0 ? unchecked(-result) : result;
    }

    public static long GetSigned64Magic(long divisor, out int shift)
    {
        if (TryGetSignedMagic(divisor, wide: true, out var magic, out shift))
        {
            return unchecked((long)magic);
        }

        const int width = 64;
        const ulong signBit = 1ul << (width - 1);
        var absDivisor = divisor == long.MinValue ? signBit : unchecked((ulong)Math.Abs(divisor));
        var t = unchecked(signBit + (unchecked((ulong)divisor) >> (width - 1)));
        var absNc = unchecked(t - 1 - (t % absDivisor));
        var p = width - 1;
        var q1 = signBit / absNc;
        var r1 = unchecked(signBit - q1 * absNc);
        var q2 = signBit / absDivisor;
        var r2 = unchecked(signBit - q2 * absDivisor);
        ulong delta;

        do
        {
            p++;
            q1 = unchecked(q1 * 2);
            r1 = unchecked(r1 * 2);
            if (r1 >= absNc)
            {
                q1 = unchecked(q1 + 1);
                r1 = unchecked(r1 - absNc);
            }
            q2 = unchecked(q2 * 2);
            r2 = unchecked(r2 * 2);
            if (r2 >= absDivisor)
            {
                q2 = unchecked(q2 + 1);
                r2 = unchecked(r2 - absDivisor);
            }
            delta = unchecked(absDivisor - r2);
        } while (q1 < delta || (q1 == delta && r1 == 0));

        shift = p - width;
        var result = unchecked((long)(q2 + 1));
        return divisor < 0 ? unchecked(-result) : result;
    }

    private static bool TryGetSignedMagic(long divisor, bool wide, out ulong magic, out int shift)
    {
        magic = divisor switch
        {
            3 => wide ? 0x5555555555555556ul : 0x55555556ul,
            5 => wide ? 0x6666666666666667ul : 0x66666667ul,
            6 => wide ? 0x2AAAAAAAAAAAAAABul : 0x2AAAAAABul,
            7 => wide ? 0x4924924924924925ul : 0x92492493ul,
            9 => wide ? 0x1C71C71C71C71C72ul : 0x38E38E39ul,
            10 => wide ? 0x6666666666666667ul : 0x66666667ul,
            11 => wide ? 0x2E8BA2E8BA2E8BA3ul : 0x2E8BA2E9ul,
            12 => wide ? 0x2AAAAAAAAAAAAAABul : 0x2AAAAAABul,
            _ => 0,
        };
        shift = divisor switch
        {
            5 or 11 => 1,
            7 => wide ? 1 : 2,
            9 => wide ? 0 : 1,
            10 => 2,
            12 => 1,
            _ => 0,
        };
        return magic != 0;
    }
}
