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
public struct simd8_t : IEquatable<simd8_t>
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

    public static simd8_t AllBitsSet
    {
        get
        {
            Unsafe.SkipInit<simd8_t>(out var result);

            result.u32[0] = uint.MaxValue;
            result.u32[1] = uint.MaxValue;

            return result;
        }
    }

    public static simd8_t Zero => default;

    public readonly bool IsAllBitsSet => this == AllBitsSet;

    public readonly bool IsZero => this == Zero;

    public static bool operator ==(in simd8_t left, in simd8_t right) => left.u64[0] == right.u64[0];

    public static bool operator !=(in simd8_t left, in simd8_t right) => !(left == right);

    [UnscopedRef]
    public unsafe Span<T> AsSpan<T>()
        where T : unmanaged
    {
        assert(Vector64<T>.IsSupported);
        var elementCount = sizeof(simd8_t) / sizeof(T);
        return MemoryMarshal.CreateSpan(ref Unsafe.As<simd8_t, T>(ref this), elementCount);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is simd8_t other) && Equals(other);

    public readonly bool Equals(simd8_t other) => (this == other);

    public override readonly int GetHashCode() => u64[0].GetHashCode();
}
