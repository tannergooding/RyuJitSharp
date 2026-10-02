// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class FloatingPointUtils
{
    public static bool isNormal(double x)
    {
        var bits = unchecked((long)DoubleToUInt64Bits(x));
        bits &= 0x7FFFFFFFFFFFFFFF;

        return (bits < 0x7FF0000000000000) && (bits != 0) && ((bits & 0x7FF0000000000000) != 0);
    }

    public static bool isNormal(float x)
    {
        var bits = unchecked((int)SingleToUInt32Bits(x));
        bits &= 0x7FFFFFFF;

        return (bits < 0x7F800000) && (bits != 0) && ((bits & 0x7F800000) != 0);
    }

    public static bool isFinite(float val)
    {
        var bits = SingleToUInt32Bits(val);

        return (~bits & 0x7F800000U) != 0;
    }

    public static bool isFinite(double val)
    {
        var bits = DoubleToUInt64Bits(val);

        return (~bits & 0x7FF0000000000000UL) != 0;
    }

    public static bool isNegative(float val)
    {
        return unchecked((int)SingleToUInt32Bits(val)) < 0;
    }

    public static bool isNegative(double val)
    {
        return unchecked((long)DoubleToUInt64Bits(val)) < 0;
    }

    public static bool isNaN(float val)
    {
        var bits = SingleToUInt32Bits(val);

        return (bits & 0x7FFFFFFFU) > 0x7F800000U;
    }

    public static bool isNaN(double val)
    {
        var bits = DoubleToUInt64Bits(val);

        return (bits & 0x7FFFFFFFFFFFFFFFUL) > 0x7FF0000000000000UL;
    }

    public static bool isNegativeZero(double val)
    {
        var bits = DoubleToUInt64Bits(val);

        return bits == 0x8000000000000000UL;
    }

    public static bool isPositiveZero(double val)
    {
        var bits = DoubleToUInt64Bits(val);

        return bits == 0x0000000000000000UL;
    }

    public static double normalize(double value)
    {
#if HOST_X86
        if (!isNaN(value))
        {
            return value;
        }

        // Bit 51 is the double-precision NaN quiet bit.
        var bits = DoubleToUInt64Bits(value);
        bits |= 1UL << 51;
        value = UInt64BitsToDouble(bits);

        return value;
#else
        return value;
#endif
    }
}
