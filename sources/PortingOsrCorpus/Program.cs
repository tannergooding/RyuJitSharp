// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal static class OsrCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Touch(ref long value)
    {
        value += 17;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long HotLoop(int iterations)
    {
        var result = 0L;
        Touch(ref result);

        for (var i = 0; i < iterations; i++)
        {
            result += i;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ContextLoop<T>(int iterations) where T : class
    {
        var type = typeof(T);
        var result = 0;

        for (var i = 0; i < iterations; i++)
        {
            if (type == typeof(string))
            {
                result += i & 7;
            }
        }

        return result + 1;
    }
}

internal static class Program
{
    public static int Main()
    {
        if (OsrCases.HotLoop(1_000_000) != 499_999_500_017 ||
            OsrCases.ContextLoop<string>(1_000_000) != 3_500_001)
        {
            return 1;
        }

#pragma warning disable CA1303 // Fixed output identifies the successful native oracle run.
        Console.WriteLine("OSR corpus: hot loop and generic context verified");
#pragma warning restore CA1303
        return 0;
    }
}
