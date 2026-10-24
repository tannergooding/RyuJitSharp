// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

internal readonly struct simdmaskvalue_t : IEquatable<simdmaskvalue_t>
{
    public bool IsScalable { get; private init; }

    public simdmaskscalable_t Scalable { get; private init; }

    public simdmask_t Fixed { get; private init; }

    public static simdmaskvalue_t FromFixed(in simdmask_t value) => new() { Fixed = value };

    public static simdmaskvalue_t FromScalable(in simdmaskscalable_t value)
    {
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS || !Configuration.TARGET_ARM64)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ScalableMaskValuesAreOnlySupportedOnARM64WithMaskedIntrinsicsEnabled);
        }

        return new() { IsScalable = true, Scalable = value };
    }

    public static bool operator ==(in simdmaskvalue_t left, in simdmaskvalue_t right)
    {
        if (left.IsScalable != right.IsScalable)
        {
            return false;
        }

        if (left.IsScalable)
        {
            return left.Scalable == right.Scalable;
        }

        return left.Fixed == right.Fixed;
    }

    public static bool operator !=(in simdmaskvalue_t left, in simdmaskvalue_t right) => !(left == right);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is simdmaskvalue_t other && Equals(other);

    public bool Equals(simdmaskvalue_t other) => this == other;

    public override int GetHashCode()
    {
        if (IsScalable)
        {
            return 1 ^ Scalable.GetHashCode();
        }

        return Fixed.GetHashCode();
    }
}
