// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
