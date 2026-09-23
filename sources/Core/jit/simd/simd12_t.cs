// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace RyuJitSharp;

[StructLayout(LayoutKind.Explicit, Pack = 4, Size = 12)]
public struct simd12_t : IEquatable<simd12_t>
{
    [FieldOffset(0)]
    public InlineArray3<float> f32;

    [FieldOffset(0)]
    public InlineArray12<sbyte> i8;

    [FieldOffset(0)]
    public InlineArray6<short> i16;

    [FieldOffset(0)]
    public InlineArray3<int> i32;

    [FieldOffset(0)]
    public InlineArray12<byte> u8;

    [FieldOffset(0)]
    public InlineArray6<ushort> u16;

    [FieldOffset(0)]
    public InlineArray3<uint> u32;

    // Native provides these partial views only for templated code.
    [FieldOffset(0)]
    public InlineArray1<double> f64;

    [FieldOffset(0)]
    public InlineArray1<long> i64;

    [FieldOffset(0)]
    public InlineArray1<ulong> u64;

    public static simd12_t AllBitsSet
    {
        get
        {
            simd12_t result = default;
            result.u32[0] = uint.MaxValue;
            result.u32[1] = uint.MaxValue;
            result.u32[2] = uint.MaxValue;
            return result;
        }
    }

    public static simd12_t Zero => default;

    public readonly bool IsAllBitsSet => this == AllBitsSet;

    public readonly bool IsZero => this == Zero;

    public static bool operator ==(in simd12_t left, in simd12_t right)
        => (left.u32[0] == right.u32[0]) && (left.u32[1] == right.u32[1]) && (left.u32[2] == right.u32[2]);

    public static bool operator !=(in simd12_t left, in simd12_t right) => !(left == right);

    [UnscopedRef]
    public unsafe Span<T> AsSpan<T>() where T : unmanaged
    {
        assert(Vector64<T>.IsSupported);
        return MemoryMarshal.CreateSpan(ref Unsafe.As<simd12_t, T>(ref this), sizeof(simd12_t) / sizeof(T));
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is simd12_t other) && Equals(other);

    public readonly bool Equals(simd12_t other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(u32[0], u32[1], u32[2]);
}
