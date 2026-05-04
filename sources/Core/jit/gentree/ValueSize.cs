// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

/// <summary>A representation of the size of a variable, that allows for symbolic representations of sizes that may be unknown to the compiler at the time of compilation, such as the length of a hardware vector on ARM64.</summary>
public readonly partial struct ValueSize : IEquatable<ValueSize>
{
    private readonly Kind _kind;

    /// <summary>The size field is used when the kind is Exact, otherwise the size field is zero.</summary>
    private readonly uint _size;

    public ValueSize(uint size)
    {
        _size = size;
    }

    private ValueSize(Kind kind)
    {
        _kind = kind;
    }

    public static ValueSize Mask => new ValueSize(Kind.Mask);

    public static ValueSize Unknown => new ValueSize(Kind.Unknown);

    public static ValueSize Vector => new ValueSize(Kind.Vector);

    public uint ExactSize
    {
        get
        {
            assert(Debugger.IsAttached || IsExact);
            return _size;
        }
    }

    public bool IsExact => _kind is Kind.Exact;

    public bool IsMask => _kind is Kind.Mask;

    public bool IsNull => (_kind is Kind.Exact) && (_size is 0);

    public bool IsUnknown => _kind is Kind.Unknown;

    public bool IsVector => _kind is Kind.Vector;

    public static bool operator ==(ValueSize left, ValueSize right) => (left._kind == right._kind)
                                                                    && (left._size == right._size);

    public static bool operator !=(ValueSize left, ValueSize right) => !(left == right);

    public override bool Equals([NotNullWhen(true)] object? obj) => (obj is ValueType other) && Equals(other);

    public bool Equals(ValueSize other) => this == other;

    public override int GetHashCode() => HashCode.Combine(_kind, _size);

    public static ValueSize FromJitType(var_types type)
    {
        assert(type.Size is not 0);

        return type switch {
#if TARGET_ARM64
            TYP_SIMD => ValueSize.Vector,
            TYP_MASK => ValueSize.Mask,
#endif
            _ => new ValueSize(type.Size),
        };
    }
}
