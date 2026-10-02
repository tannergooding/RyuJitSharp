// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class FloatingPointUtils
{
    // IEEE 754:2019 maximum propagates NaNs and treats +0 as greater than -0.
    public static double maximum(double val1, double val2)
    {
        if (val1 != val2)
        {
            if (!isNaN(val1))
            {
                return val2 < val1 ? val1 : val2;
            }

            return val1;
        }

        return isNegative(val2) ? val1 : val2;
    }

    // Magnitude comparisons select an original operand, preserving its sign and NaN payload.
    public static double maximumMagnitude(double x, double y)
    {
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);

        if ((ax > ay) || isNaN(ax))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? y : x;
        }

        return y;
    }

    // Number variants prefer the numeric operand when exactly one operand is NaN.
    public static double maximumMagnitudeNumber(double x, double y)
    {
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);

        if ((ax > ay) || isNaN(ay))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? y : x;
        }

        return y;
    }

    public static double maximumNumber(double x, double y)
    {
        if (x != y)
        {
            if (!isNaN(y))
            {
                return y < x ? x : y;
            }

            return x;
        }

        return isNegative(y) ? x : y;
    }

    public static float maximum(float val1, float val2)
    {
        if (val1 != val2)
        {
            if (!isNaN(val1))
            {
                return val2 < val1 ? val1 : val2;
            }

            return val1;
        }

        return isNegative(val2) ? val1 : val2;
    }

    public static float maximumMagnitude(float x, float y)
    {
        var ax = MathF.Abs(x);
        var ay = MathF.Abs(y);

        if ((ax > ay) || isNaN(ax))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? y : x;
        }

        return y;
    }

    public static float maximumMagnitudeNumber(float x, float y)
    {
        var ax = MathF.Abs(x);
        var ay = MathF.Abs(y);

        if ((ax > ay) || isNaN(ay))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? y : x;
        }

        return y;
    }

    public static float maximumNumber(float x, float y)
    {
        if (x != y)
        {
            if (!isNaN(y))
            {
                return y < x ? x : y;
            }

            return x;
        }

        return isNegative(y) ? x : y;
    }

    // IEEE 754:2019 minimum propagates NaNs and treats -0 as less than +0.
    public static double minimum(double val1, double val2)
    {
        if (val1 != val2)
        {
            if (!isNaN(val1))
            {
                return val1 < val2 ? val1 : val2;
            }

            return val1;
        }

        return isNegative(val1) ? val1 : val2;
    }

    public static double minimumMagnitude(double x, double y)
    {
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);

        if ((ax < ay) || isNaN(ax))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? x : y;
        }

        return y;
    }

    public static double minimumMagnitudeNumber(double x, double y)
    {
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);

        if ((ax < ay) || isNaN(ay))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? x : y;
        }

        return y;
    }

    public static double minimumNumber(double x, double y)
    {
        if (x != y)
        {
            if (!isNaN(y))
            {
                return x < y ? x : y;
            }

            return x;
        }

        return isNegative(x) ? x : y;
    }

    public static float minimum(float val1, float val2)
    {
        if (val1 != val2)
        {
            if (!isNaN(val1))
            {
                return val1 < val2 ? val1 : val2;
            }

            return val1;
        }

        return isNegative(val1) ? val1 : val2;
    }

    public static float minimumMagnitude(float x, float y)
    {
        var ax = MathF.Abs(x);
        var ay = MathF.Abs(y);

        if ((ax < ay) || isNaN(ax))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? x : y;
        }

        return y;
    }

    public static float minimumMagnitudeNumber(float x, float y)
    {
        var ax = MathF.Abs(x);
        var ay = MathF.Abs(y);

        if ((ax < ay) || isNaN(ay))
        {
            return x;
        }

        if (ax == ay)
        {
            return isNegative(x) ? x : y;
        }

        return y;
    }

    public static float minimumNumber(float x, float y)
    {
        if (x != y)
        {
            if (!isNaN(y))
            {
                return x < y ? x : y;
            }

            return x;
        }

        return isNegative(x) ? x : y;
    }
}
