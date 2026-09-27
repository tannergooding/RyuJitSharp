// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal static class OsrCases
{
    public static int TouchCount;
    public static int ContextInitializationCount;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Touch(ref long value)
    {
        TouchCount++;
        value += 17;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type InitializeContext(Type type)
    {
        ContextInitializationCount++;
        return type;
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
        var type = InitializeContext(typeof(T));
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
        var hotResult = OsrCases.HotLoop(1_000_000);
        var contextResult = OsrCases.ContextLoop<string>(1_000_000);

        if (hotResult != 499_999_500_017 ||
            contextResult != 3_500_001 ||
            OsrCases.TouchCount != 1 ||
            OsrCases.ContextInitializationCount != 1)
        {
            Console.Error.WriteLine(FormattableString.Invariant(
                $"OSR corpus mismatch: hot={hotResult}, touch={OsrCases.TouchCount}, context={contextResult}, initialization={OsrCases.ContextInitializationCount}"));
            return 1;
        }

#pragma warning disable CA1303 // Fixed output identifies the successful native oracle run.
        Console.WriteLine("OSR corpus: hot loop and generic context verified");
#pragma warning restore CA1303
        return 0;
    }
}
