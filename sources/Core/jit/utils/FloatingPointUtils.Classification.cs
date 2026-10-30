// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class FloatingPointUtils
{
    public static bool isNormal(double x)
        => double.IsNormal(x);

    public static bool isNormal(float x)
        => float.IsNormal(x);

    public static bool isFinite(float val) => float.IsFinite(val);

    public static bool isFinite(double val) => double.IsFinite(val);

    public static bool isNegative(float val) => float.IsNegative(val);

    public static bool isNegative(double val) => double.IsNegative(val);

    public static bool isNaN(float val) => float.IsNaN(val);

    public static bool isNaN(double val) => double.IsNaN(val);

    public static bool isNegativeZero(double val)
    {
        var bits = DoubleToUInt64Bits(val);

        return bits == 0x8000000000000000UL;
    }

    public static double normalize(double value)
    {
        if (!Configuration.HOST_X86 || !isNaN(value))
        {
            return value;
        }

        // Bit 51 is the double-precision NaN quiet bit.
        var bits = DoubleToUInt64Bits(value);
        bits |= 1UL << 51;
        value = UInt64BitsToDouble(bits);

        return value;
    }
}
