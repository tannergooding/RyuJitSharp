// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Globals
{
    internal static T GetRvaSpanEntry<T>(ReadOnlySpan<T> data, int index)
    {
        Debug.Assert((uint)index < (uint)data.Length);
        return Unsafe.Add(ref MemoryMarshal.GetReference(data), index);
    }

    internal static ReadOnlySpan<T> GetRvaSpanSlice<T>(ReadOnlySpan<T> data, int index, int length)
    {
        Debug.Assert((uint)index <= (uint)data.Length);
        Debug.Assert((uint)length <= (uint)(data.Length - index));

        ref var first = ref MemoryMarshal.GetReference(data);
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, index), length);
    }
}
