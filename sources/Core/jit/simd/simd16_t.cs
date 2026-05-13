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
public struct simd16_t : IEquatable<simd16_t>
{
    [FieldOffset(0)]
    public InlineArray4<float> f32;

    [FieldOffset(0)]
    public InlineArray2<double> f64;

    [FieldOffset(0)]
    public InlineArray16<sbyte> i8;

    [FieldOffset(0)]
    public InlineArray8<short> i16;

    [FieldOffset(0)]
    public InlineArray4<int> i32;

    [FieldOffset(0)]
    public InlineArray2<long> i64;

    [FieldOffset(0)]
    public InlineArray16<byte> u8;

    [FieldOffset(0)]
    public InlineArray8<ushort> u16;

    [FieldOffset(0)]
    public InlineArray4<uint> u32;

    [FieldOffset(0)]
    public InlineArray2<ulong> u64;

    [FieldOffset(0)]
    public InlineArray2<simd8_t> v64;

    public static simd16_t AllBitsSet
    {
        get
        {
            Unsafe.SkipInit<simd16_t>(out var result);

            result.v64[0] = simd8_t.AllBitsSet;
            result.v64[1] = simd8_t.AllBitsSet;

            return result;
        }
    }

    public static simd16_t Zero => default;

    public readonly bool IsAllBitsSet => this == AllBitsSet;

    public readonly bool IsZero => this == Zero;

    public static bool operator ==(in simd16_t left, in simd16_t right) => (left.v64[0] == right.v64[0]) && (left.v64[1] == right.v64[1]);

    public static bool operator !=(in simd16_t left, in simd16_t right) => !(left == right);

    [UnscopedRef]
    public unsafe Span<T> AsSpan<T>()
        where T : unmanaged
    {
        assert(Vector128<T>.IsSupported || (typeof(T) == typeof(simd8_t)));
        var elementCount = sizeof(simd16_t) / sizeof(T);
        return MemoryMarshal.CreateSpan(ref Unsafe.As<simd16_t, T>(ref this), elementCount);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is simd16_t other) && Equals(other);

    public readonly bool Equals(simd16_t other) => (this == other);

    public override readonly int GetHashCode() => HashCode.Combine(v64[0], v64[1]);
}
