// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    private static ReadOnlySpan<int> PowersOf10 => [
        1,
        10,
        100,
        1_000,
        10_000,
        100_000,
        1_000_000,
        10_000_000,
        100_000_000,
        1_000_000_000,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool AreContiguous<TEnum>(TEnum value1, TEnum value2)
        where TEnum : unmanaged, Enum
    {
        if (sizeof(TEnum) == sizeof(byte))
        {
            return ((byte)(object)(value1) + 1) == (byte)(object)(value2);
        }
        else if (sizeof(TEnum) == sizeof(short))
        {
            return ((short)(object)(value1) + 1) == (short)(object)(value2);
        }
        else if (sizeof(TEnum) == sizeof(int))
        {
            return ((int)(object)(value1) + 1) == (int)(object)(value2);
        }
        else
        {
            return ((long)(object)(value1) + 1) == (long)(object)(value2);
        }
    }

    public static bool AreContiguous<TEnum>(params ReadOnlySpan<TEnum> values)
        where TEnum : struct, Enum
    {
        var areContiguous = true;

        if (values.Length >= 2)
        {
            var previousValue = values[0];

            for (var i = 1; i < values.Length; i++)
            {
                var value = values[i];

                if (!AreContiguous(value, previousValue))
                {
                    areContiguous = false;
                    break;
                }

                previousValue = value;
            }
        }

        return areContiguous;
    }

    public static int CountDigits(int value)
    {
        // Use Log2 to get approximate Log10 via the relationship:
        // log10(x) ≈ (log2(x) + 1) * 1233 >> 12
        // Then correct with a powers-of-10 lookup table.
        // http://graphics.stanford.edu/~seander/bithacks.html#IntegerLog10

        value = (value < 0) ? -value : value | 1;
        value = (value < 0) ? int.MaxValue : value;

        var approx = ((int.Log2(value) + 1) * 1233) >>> 12;
        return (value < PowersOf10[approx]) ? approx : approx + 1;
    }

    public static int CountDigits(double value)
    {
        var approx = double.Log10(value);
        return (int)(double.Ceiling(approx)) + 1;
    }
}
