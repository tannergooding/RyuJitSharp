// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal static class NoReturnCases
{
    public static int SideEffects;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Throw()
    {
        throw new InvalidOperationException("Inline guard failed");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InlineGuard(int value)
    {
        if (value < 0)
        {
            Throw();
        }

        return value + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Record(int value)
    {
        SideEffects++;
        return value;
    }

    [DoesNotReturn]
    private static int ThrowComplex(int value)
    {
        // Distinct throw paths expose a no-return inline candidate without making the inline profitable.
        if (value < -5)
        {
            throw new InvalidOperationException("Inline guard failed");
        }

        if (value < 0)
        {
            throw new InvalidOperationException("Inline guard failed");
        }

        if (value == 0)
        {
            throw new InvalidOperationException("Inline guard failed");
        }

        throw new InvalidOperationException("Inline guard failed");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Root(int value)
    {
        return InlineGuard(value) + 3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Nested(int value)
    {
        return Record(value) + InlineGuard(value) + 5;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Conditional(int value)
    {
        return value > 0 ? value : InlineGuard(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int DirectNested(int value)
    {
        return Record(value) + ThrowComplex(value) + 17;
    }
}

internal static class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetSideEffects()
    {
        return NoReturnCases.SideEffects;
    }

    public static int Main()
    {
        if (NoReturnCases.Root(4) != 8 ||
            NoReturnCases.Nested(4) != 14 ||
            GetSideEffects() != 1 ||
            NoReturnCases.Conditional(4) != 4)
        {
            return 1;
        }

        try
        {
            _ = NoReturnCases.Root(-1);
            return 2;
        }
        catch (InvalidOperationException exception) when (exception.Message == "Inline guard failed")
        {
        }

        try
        {
            _ = NoReturnCases.Nested(-1);
            return 3;
        }
        catch (InvalidOperationException exception) when (exception.Message == "Inline guard failed")
        {
        }

        try
        {
            _ = NoReturnCases.Conditional(-1);
            return 4;
        }
        catch (InvalidOperationException exception) when (exception.Message == "Inline guard failed")
        {
        }

        try
        {
            _ = NoReturnCases.DirectNested(-1);
            return 5;
        }
        catch (InvalidOperationException exception) when (exception.Message == "Inline guard failed")
        {
        }

        if (GetSideEffects() != 3)
        {
            return 6;
        }

#pragma warning disable CA1303 // Fixed output identifies the successful native oracle run.
        Console.WriteLine("Post-inline no-return corpus: inline throws and preceding effects verified");
#pragma warning restore CA1303
        return 0;
    }
}
