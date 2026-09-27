// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RyuJitSharp;

internal static class ValueProfileCases
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Copy(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        source.CopyTo(destination);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Equal(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return left.SequenceEqual(right);
    }
}

internal static class Program
{
    public static int Main()
    {
        var source = new byte[64];
        var destination = new byte[64];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)(3 * i);
        }

        foreach (var length in new[] { 0, 1, 13, 64 })
        {
            destination.AsSpan().Clear();
            ValueProfileCases.Copy(source.AsSpan(0, length), destination);
            if (!ValueProfileCases.Equal(source.AsSpan(0, length), destination.AsSpan(0, length)))
            {
                return 1;
            }

            if (length > 0)
            {
                destination[length - 1] ^= 1;
                if (ValueProfileCases.Equal(source.AsSpan(0, length), destination.AsSpan(0, length)))
                {
                    return 2;
                }
            }
        }

        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < 30_000; i++)
            {
                var length = (i & 63) + 1;
                ValueProfileCases.Copy(source.AsSpan(0, length), destination);
                if (!ValueProfileCases.Equal(source.AsSpan(0, length), destination.AsSpan(0, length)))
                {
                    return 3;
                }
            }

            Thread.Sleep(500);
        }

#pragma warning disable CA1303 // Fixed output identifies the verified corpus.
        Console.WriteLine("Value profile corpus: copy lengths and equality results verified");
#pragma warning restore CA1303
        return 0;
    }
}
