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

    public static regMaskTP FromIntRegSet(SingleTypeRegSet intRegs) => new regMaskTP(intRegs);

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

    public regMask GetRegSetForType(var_types type)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return varTypeIsMask(type) ? _upper : _lower;
#else
        return _lower;
#endif
    }

    public regMask Lower => _lower;

#if HAS_MORE_THAN_64_REGISTERS
    public regMask Upper => _upper;
#endif

#if HAS_MORE_THAN_64_REGISTERS
    public static regMaskTP CreateFromRegNum(regNumber reg, regMask mask) => ((int)(reg) < 64) ? new regMaskTP(mask) : new regMaskTP(SRBM_NONE, mask);
#else
    public static regMaskTP CreateFromRegNum(regNumber reg, regMask mask) => new regMaskTP(mask);
#endif

    internal static void AddRegNumInMask(ref regMaskTP destination, regNumber reg)
    {
        var value = reg.SingleTypeMask;
        destination |= CreateFromRegNum(reg, value);
    }

#if TARGET_ARM
    internal static void AddRegNumInMask(ref regMaskTP destination, regNumber reg, var_types type)
    {
        var value = genSingleTypeRegMask(reg, type);
        destination |= new regMaskTP(value);
    }

    internal static void RemoveRegNumFromMask(ref regMaskTP destination, regNumber reg, var_types type)
    {
        var value = genSingleTypeRegMask(reg, type);
        destination &= ~new regMaskTP(value);
    }

    internal bool IsRegNumInMask(regNumber reg, var_types type)
    {
        return (_lower & genSingleTypeRegMask(reg, type)) != SRBM_NONE;
    }
#endif

    internal static void AddGprRegs(ref regMaskTP destination, SingleTypeRegSet gprRegs
#if DEBUG
        , regMaskTP availableIntRegs
#endif
    )
    {
#if DEBUG
        assert((gprRegs == SRBM_NONE) || ((gprRegs & availableIntRegs._lower) != SRBM_NONE),
            "(gprRegs == RBM_NONE) || ((gprRegs & availableIntRegs) != RBM_NONE)");
#endif
        destination |= new regMaskTP(gprRegs);
    }

    internal static void AddRegNum(ref regMaskTP destination, regNumber reg, var_types type)
    {
#if TARGET_ARM
        var value = getSingleTypeRegMask(reg, type);
        destination |= new regMaskTP(value);
#else
        AddRegNumInMask(ref destination, reg);
#endif
    }

    internal static void AddRegsetForType(ref regMaskTP destination, SingleTypeRegSet regsToAdd, var_types type)
    {
#if HAS_MORE_THAN_64_REGISTERS
        if (!varTypeIsMask(type))
        {
            destination |= new regMaskTP(regsToAdd);
        }
        else
        {
            destination |= new regMaskTP(SRBM_NONE, regsToAdd);
        }
#else
        destination |= new regMaskTP(regsToAdd);
#endif
    }

    internal bool IsRegNumInMask(regNumber reg)
    {
        var value = reg.SingleTypeMask;
#if HAS_MORE_THAN_64_REGISTERS
        if ((int)reg < 64)
        {
            return (_lower & value) != SRBM_NONE;
        }
        else
        {
            return (_upper & value) != SRBM_NONE;
        }
#else
        return (_lower & value) != SRBM_NONE;
#endif
    }

    internal bool IsRegNumPresent(regNumber reg, var_types type)
    {
#if TARGET_ARM
        return (_lower & getSingleTypeRegMask(reg, type)) != SRBM_NONE;
#else
        return IsRegNumInMask(reg);
#endif
    }

    internal static void RemoveRegNumFromMask(ref regMaskTP destination, regNumber reg)
    {
        var value = reg.SingleTypeMask;
        destination &= ~CreateFromRegNum(reg, value);
    }

    internal static void RemoveRegNum(ref regMaskTP destination, regNumber reg, var_types type)
    {
#if TARGET_ARM
        var value = getSingleTypeRegMask(reg, type);
        destination &= ~new regMaskTP(value);
#else
        RemoveRegNumFromMask(ref destination, reg);
#endif
    }

    internal static void RemoveRegsetForType(ref regMaskTP destination, SingleTypeRegSet regsToRemove, var_types type)
    {
#if HAS_MORE_THAN_64_REGISTERS
        if (!varTypeIsMask(type))
        {
            destination &= ~new regMaskTP(regsToRemove);
        }
        else
        {
            destination &= ~new regMaskTP(SRBM_NONE, regsToRemove);
        }
#else
        destination &= ~new regMaskTP(regsToRemove);
#endif
    }

    public static explicit operator bool(regMaskTP mask) => !mask.IsEmpty;

#if !REGMASK_BITS_32
    public static explicit operator uint(regMaskTP mask) => unchecked((uint)mask._lower);
#endif

    public static explicit operator regMask(regMaskTP mask)
    {
#if HAS_MORE_THAN_64_REGISTERS
        assert(mask._upper == SRBM_NONE);
#endif
        return mask._lower;
    }

    public static regMaskTP operator >>(regMaskTP mask, int count)
    {
#if REGMASK_BITS_32
        var lower = unchecked((uint)mask._lower) >> count;
#else
        var lower = unchecked((ulong)mask._lower) >> count;
#endif
        return new regMaskTP(unchecked((regMask)lower));
    }

    internal static ref regMaskTP ShiftRightAssign(ref regMaskTP destination, int count)
    {
        destination >>= count;

        return ref destination;
    }

#if HAS_MORE_THAN_64_REGISTERS
    public static bool operator ==(regMaskTP left, regMaskTP right) => (left._lower == right._lower) && (left._upper == right._upper);

    public static bool operator !=(regMaskTP left, regMaskTP right) => (left._lower != right._lower) || (left._upper != right._upper);

    public static regMaskTP operator &(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower & right._lower, left._upper & right._upper);

    public static regMaskTP operator |(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower | right._lower, left._upper | right._upper);

    public static regMaskTP operator ^(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower ^ right._lower, left._upper ^ right._upper);

    public static regMaskTP operator ~(regMaskTP mask) => new regMaskTP(~mask._lower, ~mask._upper);
#else
    public static bool operator ==(regMaskTP left, regMaskTP right) => left._lower == right._lower;

    public static bool operator !=(regMaskTP left, regMaskTP right) => left._lower != right._lower;

    public static regMaskTP operator &(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower & right._lower);

    public static regMaskTP operator |(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower | right._lower);

    public static regMaskTP operator ^(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower ^ right._lower);

    public static regMaskTP operator ~(regMaskTP mask) => new regMaskTP(~mask._lower);
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
