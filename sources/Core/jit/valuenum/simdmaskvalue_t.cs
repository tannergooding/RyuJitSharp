// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_MASKED_HW_INTRINSICS
using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public readonly struct simdmaskvalue_t : IEquatable<simdmaskvalue_t>
{
#if TARGET_ARM64
    public bool IsScalable { get; private init; }

    public simdmaskscalable_t Scalable { get; private init; }
#endif
    public simdmask_t Fixed { get; private init; }

    public static simdmaskvalue_t FromFixed(in simdmask_t value) => new() { Fixed = value };

#if TARGET_ARM64
    public static simdmaskvalue_t FromScalable(in simdmaskscalable_t value) =>
        new() { IsScalable = true, Scalable = value };
#endif

    public static bool operator ==(in simdmaskvalue_t left, in simdmaskvalue_t right)
    {
#if TARGET_ARM64
        if (left.IsScalable != right.IsScalable)
        {
            return false;
        }
        if (left.IsScalable)
        {
            return left.Scalable == right.Scalable;
        }
#endif

        return left.Fixed == right.Fixed;
    }

    public static bool operator !=(in simdmaskvalue_t left, in simdmaskvalue_t right) => !(left == right);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is simdmaskvalue_t other && Equals(other);

    public bool Equals(simdmaskvalue_t other) => this == other;

    public override int GetHashCode()
    {
#if TARGET_ARM64
        if (IsScalable)
        {
            return 1 ^ Scalable.GetHashCode();
        }
#endif

        return Fixed.GetHashCode();
    }
}
#endif
