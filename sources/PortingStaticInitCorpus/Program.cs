// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Explicit class constructors keep the first access observable at JIT time.

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

#pragma warning disable CA1810 // Explicit constructors prevent beforefieldinit from hiding the first-access helper.

internal static class ProbeState
{
    internal static int Initializations;
}

internal static class IntegerStatics
{
    internal static readonly int Value;

    static IntegerStatics()
    {
        ProbeState.Initializations++;
        Value = 42;
    }
}

internal static class ObjectStatics
{
    internal static readonly object Value;

    static ObjectStatics()
    {
        ProbeState.Initializations++;
        Value = new object();
    }
}

internal static class FirstStatics
{
    internal static readonly int Value;

    static FirstStatics()
    {
        ProbeState.Initializations++;
        Value = 17;
    }
}

internal static class SecondStatics
{
    internal static readonly int Value;

    static SecondStatics()
    {
        ProbeState.Initializations++;
        Value = 23;
    }
}

internal static class StaticInitCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ReadInt() => IntegerStatics.Value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object ReadObject() => ObjectStatics.Value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ReadPair() => FirstStatics.Value + SecondStatics.Value;
}

internal static class Program
{
#pragma warning disable CA1508 // The analyzer does not track writes from triggered class constructors.
#pragma warning disable CA1303 // Fixed output is compared with the native oracle.
    public static int Main()
    {
        if (ProbeState.Initializations != 0)
        {
            return 1;
        }

        var integer = StaticInitCases.ReadInt();
        var reference = StaticInitCases.ReadObject();
        var pair = StaticInitCases.ReadPair();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        if (integer != 42 || pair != 40 || ProbeState.Initializations != 4 ||
            !ReferenceEquals(reference, StaticInitCases.ReadObject()) ||
            StaticInitCases.ReadInt() != integer || StaticInitCases.ReadPair() != pair ||
            ProbeState.Initializations != 4)
        {
            return 2;
        }

        Console.WriteLine("42,40,4; object preserved");
        return 0;
    }
#pragma warning restore CA1303
#pragma warning restore CA1508
}
