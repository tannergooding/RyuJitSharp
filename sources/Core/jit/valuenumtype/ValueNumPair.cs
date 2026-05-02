// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public struct ValueNumPair : IEquatable<ValueNumPair>
{
    private ValueNum _conservative;

    private ValueNum _liberal;

    /// <summary>Initializes both elements to "NoVN".</summary>
    public ValueNumPair() : this(ValueNumStore.NoVN, ValueNumStore.NoVN)
    {
    }

    public ValueNumPair(ValueNum liberal, ValueNum conservative)
    {
        _conservative = conservative;
        _liberal = liberal;
    }

    public ValueNum Conservative
    {
        readonly get
        {
            return _conservative;
        }

        set
        {
            _conservative = value;
        }
    }

    [UnscopedRef]
    public ref ValueNum ConservativeAddr => ref _conservative;

    public ValueNum Liberal
    {
        readonly get
        {
            return _liberal;
        }

        set
        {
            _liberal = value;
        }
    }

    [UnscopedRef]
    public ref ValueNum LiberalAddr => ref _liberal;

    public ValueNum this[ValueNumKind vnk]
    {
        readonly get
        {
            if (vnk == VNK_Liberal)
            {
                return _liberal;
            }
            else
            {
                assert(vnk == VNK_Conservative);
                return _conservative;
            }
        }

        set
        {
            if (vnk == VNK_Liberal)
            {
                _liberal = value;
            }
            else
            {
                assert(vnk == VNK_Conservative);
                _conservative = value;
            }
        }
    }

    public static bool operator ==(ValueNumPair left, ValueNumPair right) => (left._liberal == right._liberal)
                                                                          && (left._conservative == right._conservative);

    public static bool operator !=(ValueNumPair left, ValueNumPair right) => !(left == right);

    /// <summary>True iff neither element is "NoVN".</summary>
    /// <returns></returns>
    public readonly bool BothDefined()
    {
        return (_liberal != ValueNumStore.NoVN) && (_conservative != ValueNumStore.NoVN);
    }

    public readonly bool BothEqual()
    {
        return _liberal == _conservative;
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is ValueNumPair other) && Equals(other);

    public readonly bool Equals(ValueNumPair other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(_liberal, _conservative);

    public void SetBoth(ValueNum vn)
    {
        _liberal = vn;
        _conservative = vn;
    }
}
