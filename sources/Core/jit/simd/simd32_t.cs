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
public struct simd32_t : IEquatable<simd32_t>
{
    [FieldOffset(0)]
    public InlineArray8<float> f32;

    [FieldOffset(0)]
    public InlineArray4<double> f64;

    [FieldOffset(0)]
    public InlineArray32<sbyte> i8;

    [FieldOffset(0)]
    public InlineArray16<short> i16;

    [FieldOffset(0)]
    public InlineArray8<int> i32;

    [FieldOffset(0)]
    public InlineArray4<long> i64;

    [FieldOffset(0)]
    public InlineArray32<byte> u8;

    [FieldOffset(0)]
    public InlineArray16<ushort> u16;

    [FieldOffset(0)]
    public InlineArray8<uint> u32;

    [FieldOffset(0)]
    public InlineArray4<ulong> u64;

    [FieldOffset(0)]
    public InlineArray4<simd8_t> v64;

    [FieldOffset(0)]
    public InlineArray2<simd16_t> v128;

    public static simd32_t AllBitsSet
    {
        get
        {
            Unsafe.SkipInit<simd32_t>(out var result);

            result.v128[0] = simd16_t.AllBitsSet;
            result.v128[1] = simd16_t.AllBitsSet;

            return result;
        }
    }

    public static simd32_t Zero => default;

    public readonly bool IsAllBitsSet => this == AllBitsSet;

    public readonly bool IsZero => this == Zero;

    public static bool operator ==(in simd32_t left, in simd32_t right) => (left.v128[0] == right.v128[0]) && (left.v128[1] == right.v128[1]);

    public static bool operator !=(in simd32_t left, in simd32_t right) => !(left == right);

    [UnscopedRef]
    public unsafe Span<T> AsSpan<T>()
        where T : unmanaged
    {
        assert(Vector256<T>.IsSupported || (typeof(T) == typeof(simd8_t)) || (typeof(T) == typeof(simd16_t)));
        var elementCount = sizeof(simd32_t) / sizeof(T);
        return MemoryMarshal.CreateSpan(ref Unsafe.As<simd32_t, T>(ref this), elementCount);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is simd32_t other) && Equals(other);

    public readonly bool Equals(simd32_t other) => (this == other);

    public override readonly int GetHashCode() => HashCode.Combine(v128[0], v128[1]);
}
