// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal class CastBase;
internal sealed class CastDerived : CastBase;

internal static class LateCastCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object? CastClass(object? value) => (CastBase?)value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object? CastString(object? value) => (string?)value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object? CastArray(object? value) => (string[]?)value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object? IsString(object? value) => value as string;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T? CastGeneric<T>(object? value) where T : class => (T?)value;
}

internal static class Program
{
#pragma warning disable CA1303 // Fixed output is compared with the native oracle.
    public static int Main()
    {
        var parent = new CastBase();
        var child = new CastDerived();
        var text = "late cast";
        string[] array = [text];
        if (!ReferenceEquals(LateCastCases.CastClass(parent), parent) ||
            !ReferenceEquals(LateCastCases.CastClass(child), child) ||
            !ReferenceEquals(LateCastCases.CastString(text), text) ||
            !ReferenceEquals(LateCastCases.CastArray(array), array) ||
            !ReferenceEquals(LateCastCases.CastGeneric<CastBase>(child), child) ||
            !ReferenceEquals(LateCastCases.IsString(text), text) ||
            LateCastCases.IsString(child) is not null ||
            LateCastCases.CastClass(null) is not null ||
            LateCastCases.CastString(null) is not null ||
            LateCastCases.CastArray(null) is not null ||
            LateCastCases.CastGeneric<CastBase>(null) is not null ||
            LateCastCases.IsString(null) is not null)
        {
            return 1;
        }

        var exceptions = 0;
        foreach (var cast in new Func<object?, object?>[] {
            LateCastCases.CastClass, LateCastCases.CastString, LateCastCases.CastArray, LateCastCases.CastGeneric<CastBase> })
        {
            try
            {
                _ = cast(new object());
            }
            catch (InvalidCastException)
            {
                exceptions++;
            }
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        if (exceptions != 4 || !ReferenceEquals(LateCastCases.CastClass(child), child) ||
            !ReferenceEquals(LateCastCases.CastArray(array), array))
        {
            return 2;
        }

        Console.WriteLine("casts/nulls/identity preserved; 4 InvalidCastExceptions");
        return 0;
    }
#pragma warning restore CA1303
}
