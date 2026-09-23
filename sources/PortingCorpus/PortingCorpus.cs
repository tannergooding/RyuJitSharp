// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal static class PortingCorpus
{
    public static int Main()
    {
        if (Add(4, 7) != 11)
        {
            return 1;
        }

        if ((Branch(-2) != 2) || (Branch(3) != 4))
        {
            return 2;
        }

        if (Locals(5) != 36)
        {
            return 3;
        }

        if (Call(6) != 13)
        {
            return 4;
        }

        if (InlineCaller(6) != 13)
        {
            return 5;
        }

        if (IndirectCall(6) != 13)
        {
            return 6;
        }

        if (FoldConstants() != 27)
        {
            return 7;
        }

        if (BitConverter.DoubleToInt64Bits(FoldFloating(-0.0)) != long.MinValue)
        {
            return 8;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Add(int left, int right) => left + right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Branch(int value) => value < 0 ? -value : value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Locals(int value)
    {
        var first = value + 1;
        var second = value - 2;

        return (first * first) + second - 3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Call(int value) => Add(value, value + 1);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int InlineCaller(int value) => InlineCandidate(value) + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe int IndirectCall(int value)
    {
        delegate* managed<int, int, int> target = &Add;
        return target(value, value + 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long FoldConstants()
    {
        // BitConverter keeps these constants in IL until the JIT imports the intrinsics.
        return ((long)BitConverter.Int32BitsToSingle(0x40F00000) + (int)BitConverter.Int64BitsToDouble(0x4004000000000000)) * 3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double FoldFloating(double value) => (((value + -0.0) * 1.0) - 0.0) / 1.0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InlineCandidate(int value) => value * 2;
}
