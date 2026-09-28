// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Diagnostics.CodeAnalysis;
using static RyuJitSharp.SimdScalableKind;

namespace RyuJitSharp;

public struct simdscalable_t : IEquatable<simdscalable_t>
{
    public var_types BaseType;
    public SimdScalableKind Kind;
    public simd8_t Index;
    public simd8_t Step;

    public static simdscalable_t AllBitsSet
    {
        get
        {
            var result = Zero;
            result.Index.u64[0] = 0xFF;

            return result;
        }
    }

    public static simdscalable_t Zero => new() {
        BaseType = TYP_BYTE,
        Kind = SimdScalableRepeated,
    };

    public readonly bool IsZero => Index.u64[0] == 0 &&
        (Kind != SimdScalableSequence || Step.u64[0] == 0);

    public readonly bool IsAllBitsSet
    {
        get
        {
            if (Kind != SimdScalableRepeated)
            {
                return false;
            }

            var elementBitSize = BaseType.Size * 8;
            var mask = elementBitSize == 64 ? ulong.MaxValue : (1UL << elementBitSize) - 1;

            return Index.u64[0] == mask;
        }
    }

    public static bool operator ==(in simdscalable_t left, in simdscalable_t right)
    {
        if (left.IsZero && right.IsZero)
        {
            return true;
        }

        return left.BaseType == right.BaseType && left.Kind == right.Kind &&
            left.Index == right.Index && left.Step == right.Step;
    }

    public static bool operator !=(in simdscalable_t left, in simdscalable_t right) => !(left == right);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj is simdscalable_t other && Equals(other);

    public readonly bool Equals(simdscalable_t other) => this == other;

    public override readonly int GetHashCode() => IsZero ? 0 : HashCode.Combine(BaseType, Kind, Index, Step);
}
#endif
