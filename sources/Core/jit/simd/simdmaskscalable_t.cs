// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public struct simdmaskscalable_t : IEquatable<simdmaskscalable_t>
{
    public var_types BaseType;
    public byte Index;

    public static simdmaskscalable_t AllBitsSet => new() { BaseType = TYP_BYTE, Index = 1 };

    public readonly bool IsZero => Index == 0;

    public readonly bool IsAllBitsSet(var_types baseType) => (Index == 1) && (baseType.Size == BaseType.Size);

    public static bool operator ==(in simdmaskscalable_t left, in simdmaskscalable_t right) =>
        (left.IsZero && right.IsZero) || ((left.BaseType == right.BaseType) && (left.Index == right.Index));

    public static bool operator !=(in simdmaskscalable_t left, in simdmaskscalable_t right) => !(left == right);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj is simdmaskscalable_t other && Equals(other);

    public readonly bool Equals(simdmaskscalable_t other) => this == other;

    public override readonly int GetHashCode() => IsZero ? 0 : (int)BaseType ^ Index;
}
#endif
