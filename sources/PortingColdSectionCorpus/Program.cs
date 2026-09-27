// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal static class ColdSectionCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ColdThrow(int value)
    {
        if (value < 0)
        {
            throw new InvalidOperationException("Negative value");
        }

        if (value == 0)
        {
            throw new InvalidOperationException("Zero value");
        }

        return value + 17;
    }
}

internal static class Program
{
    public static int Main()
    {
        if (ColdSectionCases.ColdThrow(19) != 36)
        {
            return 1;
        }

        try
        {
            return ColdSectionCases.ColdThrow(-1) == 0 ? 2 : 3;
        }
        catch (InvalidOperationException exception)
        {
            if (exception.Message != "Negative value")
            {
                return 3;
            }
        }

        try
        {
            return ColdSectionCases.ColdThrow(0) == 0 ? 4 : 5;
        }
        catch (InvalidOperationException exception)
        {
            if (exception.Message != "Zero value")
            {
                return 6;
            }
        }

#pragma warning disable CA1303 // Fixed output identifies the successful native oracle run.
        Console.WriteLine("Cold section corpus: hot result and cold exception verified");
#pragma warning restore CA1303
        return 0;
    }
}
