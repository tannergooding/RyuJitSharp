// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public partial class Globals
{
    public static bool isPow2<T>(T i)
        where T : IBinaryInteger<T>
    {
        return (i > T.Zero) && (((i - T.One) & i) == T.Zero);
    }

    public static uint CountDigits(uint num, uint @base = 10)
    {
        assert((2 <= @base) && (@base <= 16), "2 <= base && base <= 16");
        var count = 1U;

        while (num >= @base)
        {
            num /= @base;
            ++count;
        }

        return count;
    }
}
