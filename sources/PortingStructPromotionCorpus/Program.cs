// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal readonly record struct IntPair(int First, int Second);
internal readonly record struct IntTriple(int First, int Second, int Third);
internal readonly record struct DoublePair(double First, double Second);
internal readonly record struct IntQuad(int First, int Second, int Third, int Fourth);

internal static class StructPromotionCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IntPair MakePair(int value)
    {
        return new IntPair(value, value + 4);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IntTriple MakeTriple(int value)
    {
        return new IntTriple(value, value + 1, value + 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DoublePair MakeDoublePair(double value)
    {
        return new DoublePair(value, value + 0.5);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IntQuad MakeQuad(int value)
    {
        return new IntQuad(value, value + 1, value + 2, value + 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SumQuad(IntQuad value)
    {
        return value.First + value.Second + value.Third + value.Fourth;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int TwoFields(int value)
    {
        var pair = MakePair(value);
        return (pair.First * 3) + pair.Second;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ThreeFields(int value)
    {
        var triple = MakeTriple(value);
        return triple.First + (triple.Second * 2) + (triple.Third * 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double FloatingFields(double value)
    {
        var pair = MakeDoublePair(value);
        return (pair.First * 2) + pair.Second;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FourFieldCopy(int value)
    {
        var quad = MakeQuad(value);
        return SumQuad(quad);
    }
}

internal static class Program
{
    public static int Main()
    {
        if (StructPromotionCases.TwoFields(3) != 16 ||
            StructPromotionCases.TwoFields(-2) != -4 ||
            StructPromotionCases.ThreeFields(7) != 50 ||
            StructPromotionCases.ThreeFields(-3) != -10 ||
            StructPromotionCases.FloatingFields(4) != 12.5 ||
            StructPromotionCases.FloatingFields(-2) != -5.5 ||
            StructPromotionCases.FourFieldCopy(10) != 46 ||
            StructPromotionCases.FourFieldCopy(0) != 6)
        {
            return 1;
        }

#pragma warning disable CA1303 // Fixed output identifies successful oracle execution.
        Console.WriteLine("Struct promotion corpus: integer, floating, and copy results verified");
#pragma warning restore CA1303
        return 0;
    }
}
