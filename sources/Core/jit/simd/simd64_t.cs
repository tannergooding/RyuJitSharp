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
public struct simd64_t : IEquatable<simd64_t>
{
    [FieldOffset(0)]
    public InlineArray16<float> f32;

    [FieldOffset(0)]
    public InlineArray8<double> f64;

    [FieldOffset(0)]
    public InlineArray64<sbyte> i8;

    [FieldOffset(0)]
    public InlineArray32<short> i16;

    [FieldOffset(0)]
    public InlineArray16<int> i32;

    [FieldOffset(0)]
    public InlineArray8<long> i64;

    [FieldOffset(0)]
    public InlineArray64<byte> u8;

    [FieldOffset(0)]
    public InlineArray32<ushort> u16;

    [FieldOffset(0)]
    public InlineArray16<uint> u32;

    [FieldOffset(0)]
    public InlineArray8<ulong> u64;

    [FieldOffset(0)]
    public InlineArray8<simd8_t> v64;

    [FieldOffset(0)]
    public InlineArray4<simd16_t> v128;

    [FieldOffset(0)]
    public InlineArray2<simd32_t> v256;

    public static simd64_t AllBitsSet
    {
        get
        {
            Unsafe.SkipInit<simd64_t>(out var result);

            result.v256[0] = simd32_t.AllBitsSet;
            result.v256[1] = simd32_t.AllBitsSet;

            return result;
        }
    }

    public static simd64_t Zero => default;

    public readonly bool IsAllBitsSet => this == AllBitsSet;

    public readonly bool IsZero => this == Zero;

    public static bool operator ==(in simd64_t left, in simd64_t right) => (left.v256[0] == right.v256[0]) && (left.v256[1] == right.v256[1]);

    public static bool operator !=(in simd64_t left, in simd64_t right) => !(left == right);

    [UnscopedRef]
    public unsafe Span<T> AsSpan<T>()
        where T : unmanaged
    {
        assert(Vector512<T>.IsSupported || (typeof(T) == typeof(simd8_t)) || (typeof(T) == typeof(simd16_t)) || (typeof(T) == typeof(simd32_t)));
        var elementCount = sizeof(simd64_t) / sizeof(T);
        return MemoryMarshal.CreateSpan(ref Unsafe.As<simd64_t, T>(ref this), elementCount);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is simd64_t other) && Equals(other);

    public readonly bool Equals(simd64_t other) => (this == other);

    public override readonly int GetHashCode() => HashCode.Combine(v256[0], v256[1]);
}
