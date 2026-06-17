// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public readonly struct regMaskTP : IEquatable<regMaskTP>
{
    private readonly regMask _lower;

#if HAS_MORE_THAN_64_REGISTERS
    private readonly regMask _upper;
#endif

    public regMaskTP(regMask lower)
    {
        _lower = lower;
    }

#if HAS_MORE_THAN_64_REGISTERS
    public regMaskTP(regMask lower, regMask upper)
    {
        _lower = lower;
        _upper = upper;
    }
#endif

    public regMask IntRegSet => _lower;

    public regMask FltRegSet => _lower;

#if HAS_MORE_THAN_64_REGISTERS
    public regMask MskRegSet => _upper;
#else
    public regMask MskRegSet => _lower;
#endif

#if HAS_MORE_THAN_64_REGISTERS
    public bool IsEmpty => (_lower | _upper) == SRBM_NONE;
#else
    public bool IsEmpty => _lower == SRBM_NONE;
#endif

    public bool IsNonEmpty => !IsEmpty;

    public regMask Lower => _lower;

#if HAS_MORE_THAN_64_REGISTERS
    public regMask Upper => _upper;
#endif

#if HAS_MORE_THAN_64_REGISTERS
    public static regMaskTP CreateFromRegNum(regNumber reg, regMask mask) => ((int)(reg) < 64) ? new regMaskTP(mask) : new regMaskTP(SRBM_NONE, mask);
#else
    public static regMaskTP CreateFromRegNum(regNumber reg, regMask mask) => new regMaskTP(mask);
#endif

    public static explicit operator regMask(regMaskTP mask)
    {
#if HAS_MORE_THAN_64_REGISTERS
        assert(mask._upper == SRBM_NONE);
#endif
        return mask._lower;
    }

#if HAS_MORE_THAN_64_REGISTERS
    public static bool operator ==(regMaskTP left, regMaskTP right) => (left._lower == right._lower) && (left._upper == right._upper);

    public static bool operator !=(regMaskTP left, regMaskTP right) => (left._lower != right._lower) || (left._upper != right._upper);

    public static regMaskTP operator &(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower & right._lower, left._upper & right._upper);

    public static regMaskTP operator |(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower | right._lower, left._upper | right._upper);

    public static regMaskTP operator ^(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower ^ right._lower, left._upper ^ right._upper);
#else
    public static bool operator ==(regMaskTP left, regMaskTP right) => left._lower == right._lower;

    public static bool operator !=(regMaskTP left, regMaskTP right) => left._lower != right._lower;

    public static regMaskTP operator &(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower & right._lower);

    public static regMaskTP operator |(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower | right._lower);

    public static regMaskTP operator ^(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower ^ right._lower);
#endif

    public readonly bool IsSet(regNumber regNum)
    {
        var mask = ((int)(regNum) < 64) ? _lower : _upper;
        return ((long)(mask) & (1L << (int)(regNum))) is not 0;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => (obj is regMaskTP other) && (this == other);

    public bool Equals(regMaskTP other) => this == other;

#if HAS_MORE_THAN_64_REGISTERS
    public override int GetHashCode() => HashCode.Combine(_lower, _upper);

    public override string ToString() => $"{{Lower = {_lower}, Upper = {_upper}}}";
#else
    public override int GetHashCode() => _lower.GetHashCode();

    public override string ToString() => _lower.ToString();
#endif
}
