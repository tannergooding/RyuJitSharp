// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal static class PortingObjectAllocationCorpus
{
    private static object? s_escaped;

    public static int Main()
    {
        if (LocalClassFields(7) != 15)
        {
            return 1;
        }

        if (FixedValueArray(3) != 9)
        {
            return 2;
        }

        if (ReferenceLifetime(5) != 12)
        {
            return 3;
        }

        if (BoxRoundTrip(9) != 20)
        {
            return 4;
        }

        if (EscapingControl(4) != 9)
        {
            return 5;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LocalClassFields(int value)
    {
        var instance = new SmallClass(value, value + 1);
        return instance.First + instance.Second;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FixedValueArray(int value)
    {
        var items = new int[4];
        items[0] = value;
        items[1] = value + 1;
        items[2] = items[0] + items[1];
        items[3] = 2;
        return items[2] + items[3];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ReferenceLifetime(int value)
    {
        var leaf = new Leaf(value + 2);
        var holder = new Holder(leaf, value);
        var result = holder.Item.Value + holder.Extra;
        GC.KeepAlive(holder);
        GC.KeepAlive(leaf);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int BoxRoundTrip(int value)
    {
        object boxed = new Pair { First = value, Second = value + 2 };
        var unboxed = (Pair)boxed;
        GC.KeepAlive(boxed);
        return unboxed.First + unboxed.Second;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int EscapingControl(int value)
    {
        var instance = new SmallClass(value, value + 1);
        s_escaped = instance;
        return instance.First + instance.Second;
    }

    private sealed class SmallClass(int first, int second)
    {
        public readonly int First = first;
        public readonly int Second = second;
    }

    private sealed class Leaf(int value)
    {
        public readonly int Value = value;
    }

    private sealed class Holder(Leaf item, int extra)
    {
        public readonly Leaf Item = item;
        public readonly int Extra = extra;
    }

    private struct Pair
    {
        public int First;
        public int Second;
    }
}
