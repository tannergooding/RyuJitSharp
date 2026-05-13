// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace RyuJitSharp;

[StructLayout(LayoutKind.Explicit)]
public struct simdmask_t : IEquatable<simdmask_t>
{
    [FieldOffset(0)]
    public InlineArray2<float> f32;

    [FieldOffset(0)]
    public InlineArray1<double> f64;

    [FieldOffset(0)]
    public InlineArray8<sbyte> i8;

    [FieldOffset(0)]
    public InlineArray4<short> i16;

    [FieldOffset(0)]
    public InlineArray2<int> i32;

    [FieldOffset(0)]
    public InlineArray1<long> i64;

    [FieldOffset(0)]
    public InlineArray8<byte> u8;

    [FieldOffset(0)]
    public InlineArray4<ushort> u16;

    [FieldOffset(0)]
    public InlineArray2<uint> u32;

    [FieldOffset(0)]
    public InlineArray1<ulong> u64;

    public static simdmask_t Zero => default;

    public readonly bool IsAllBitsSet => this == AllBitsSet(64);

    public readonly bool IsZero => this == Zero;

    public readonly long RawBits => i64[0];

    public static bool operator ==(in simdmask_t left, in simdmask_t right) => left.u64[0] == right.u64[0];

    public static bool operator !=(in simdmask_t left, in simdmask_t right) => !(left == right);

    public static simdmask_t AllBitsSet(int elementCount)
    {
        Unsafe.SkipInit<simdmask_t>(out var result);
        result.u64[0] = uint.MaxValue;
        return result;
    }

    public static long GetBitMask(int elementCount)
    {
        assert(elementCount is >= 1 && elementCount <= 64);
        return (elementCount is 64) ? -1 : ((1L << elementCount) - 1);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is simdmask_t other) && Equals(other);

    public readonly bool Equals(simdmask_t other) => (this == other);

    public override readonly int GetHashCode() => u64[0].GetHashCode();
}
