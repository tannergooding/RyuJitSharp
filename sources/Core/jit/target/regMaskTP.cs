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
    private readonly regMask _upper;

    public regMaskTP(regMask lower)
    {
        _lower = NormalizeLower(lower);
        _upper = SRBM_NONE;
    }

    public regMaskTP(regMask lower, regMask upper)
    {
        _lower = NormalizeLower(lower);
        _upper = Configuration.HAS_MORE_THAN_64_REGISTERS ? upper : SRBM_NONE;
    }

    private static regMask NormalizeLower(regMask value)
    {
        if (Configuration.TARGET_X86)
        {
            var bits = unchecked((uint)(long)value);

            return unchecked((regMask)(ulong)bits);
        }

        return value;
    }

    public regMask IntRegSet => _lower;

    public static regMaskTP FromIntRegSet(SingleTypeRegSet intRegs) => new regMaskTP(intRegs);

    public regMask FltRegSet => _lower;

    public regMask MskRegSet => Configuration.HAS_MORE_THAN_64_REGISTERS ? _upper : _lower;

    public bool IsEmpty => (_lower | _upper) == SRBM_NONE;

    public bool IsNonEmpty => !IsEmpty;

    public regMask GetRegSetForType(var_types type)
    {
        return Configuration.HAS_MORE_THAN_64_REGISTERS && varTypeIsMask(type) ? _upper : _lower;
    }

    public regMask Lower => _lower;

    public regMask Upper => _upper;

    public static regMaskTP CreateFromRegNum(regNumber reg, regMask mask)
    {
        if (Configuration.HAS_MORE_THAN_64_REGISTERS && ((int)reg >= 64))
        {
            return new regMaskTP(SRBM_NONE, mask);
        }

        return new regMaskTP(mask);
    }

    internal static void AddRegNumInMask(ref regMaskTP destination, regNumber reg)
    {
        var value = reg.SingleTypeMask;
        destination |= CreateFromRegNum(reg, value);
    }

    internal static void AddRegNumInMask(ref regMaskTP destination, regNumber reg, var_types type)
    {
        if (Configuration.TARGET_ARM)
        {
            var value = genSingleTypeRegMask(reg, type);
            destination |= new regMaskTP(value);
        }
        else
        {
            AddRegNumInMask(ref destination, reg);
        }
    }

    internal static void RemoveRegNumFromMask(ref regMaskTP destination, regNumber reg, var_types type)
    {
        if (Configuration.TARGET_ARM)
        {
            var value = genSingleTypeRegMask(reg, type);
            destination &= ~new regMaskTP(value);
        }
        else
        {
            RemoveRegNumFromMask(ref destination, reg);
        }
    }

    internal bool IsRegNumInMask(regNumber reg, var_types type)
    {
        if (Configuration.TARGET_ARM)
        {
            return (_lower & genSingleTypeRegMask(reg, type)) != SRBM_NONE;
        }

        return IsRegNumInMask(reg);
    }

    internal static void AddGprRegs(
        ref regMaskTP destination,
        SingleTypeRegSet gprRegs,
        regMaskTP availableIntRegs = default)
    {
        if (Configuration.DEBUG)
        {
            assert((gprRegs == SRBM_NONE) || ((gprRegs & availableIntRegs._lower) != SRBM_NONE),
                "(gprRegs == RBM_NONE) || ((gprRegs & availableIntRegs) != RBM_NONE)");
        }

        destination |= new regMaskTP(gprRegs);
    }

    internal static void AddRegNum(ref regMaskTP destination, regNumber reg, var_types type)
    {
        if (Configuration.TARGET_ARM)
        {
            var value = getSingleTypeRegMask(reg, type);
            destination |= new regMaskTP(value);
        }
        else
        {
            AddRegNumInMask(ref destination, reg);
        }
    }

    internal static void AddRegsetForType(ref regMaskTP destination, SingleTypeRegSet regsToAdd, var_types type)
    {
        if (!Configuration.HAS_MORE_THAN_64_REGISTERS || !varTypeIsMask(type))
        {
            destination |= new regMaskTP(regsToAdd);
        }
        else
        {
            destination |= new regMaskTP(SRBM_NONE, regsToAdd);
        }
    }

    internal bool IsRegNumInMask(regNumber reg)
    {
        var value = reg.SingleTypeMask;
        if (!Configuration.HAS_MORE_THAN_64_REGISTERS || ((int)reg < 64))
        {
            return (_lower & value) != SRBM_NONE;
        }

        return (_upper & value) != SRBM_NONE;
    }

    internal bool IsRegNumPresent(regNumber reg, var_types type)
    {
        if (Configuration.TARGET_ARM)
        {
            return (_lower & getSingleTypeRegMask(reg, type)) != SRBM_NONE;
        }

        return IsRegNumInMask(reg);
    }

    internal static void RemoveRegNumFromMask(ref regMaskTP destination, regNumber reg)
    {
        var value = reg.SingleTypeMask;
        destination &= ~CreateFromRegNum(reg, value);
    }

    internal static void RemoveRegNum(ref regMaskTP destination, regNumber reg, var_types type)
    {
        if (Configuration.TARGET_ARM)
        {
            var value = getSingleTypeRegMask(reg, type);
            destination &= ~new regMaskTP(value);
        }
        else
        {
            RemoveRegNumFromMask(ref destination, reg);
        }
    }

    internal static void RemoveRegsetForType(ref regMaskTP destination, SingleTypeRegSet regsToRemove, var_types type)
    {
        if (!Configuration.HAS_MORE_THAN_64_REGISTERS || !varTypeIsMask(type))
        {
            destination &= ~new regMaskTP(regsToRemove);
        }
        else
        {
            destination &= ~new regMaskTP(SRBM_NONE, regsToRemove);
        }
    }

    public static explicit operator bool(regMaskTP mask) => !mask.IsEmpty;

    public static explicit operator uint(regMaskTP mask) => unchecked((uint)mask._lower);

    public static explicit operator regMask(regMaskTP mask)
    {
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            assert(mask._upper == SRBM_NONE);
        }

        return mask._lower;
    }

    public static regMaskTP operator >>(regMaskTP mask, int count)
    {
        var lower = Configuration.REGMASK_BITS_32
            ? unchecked((ulong)(uint)mask._lower) >> count
            : unchecked((ulong)mask._lower) >> count;

        return new regMaskTP(unchecked((regMask)lower));
    }

    public static explicit operator int(regMaskTP mask) => unchecked((int)(long)mask._lower);

    public static explicit operator byte(regMaskTP mask) => unchecked((byte)(long)mask._lower);

    public static regMaskTP operator -(regMaskTP first, regMaskTP second)
    {
        var lower = unchecked((ulong)(long)first._lower - (ulong)(long)second._lower);
        return new regMaskTP(unchecked((regMask)(long)lower));
    }

    public static bool operator >(
        regMaskTP first,
        regMaskTP second) =>
        unchecked((ulong)(long)first._lower) > unchecked((ulong)(long)second._lower);

    // C# requires relational operators to be declared in pairs.
    public static bool operator <(
        regMaskTP first,
        regMaskTP second) =>
        unchecked((ulong)(long)first._lower) < unchecked((ulong)(long)second._lower);

    public static regMaskTP operator <<(regMaskTP first, int count)
    {
        var lower = unchecked((ulong)(long)first._lower << count);
        return new regMaskTP(unchecked((regMask)(long)lower));
    }

    internal static ref regMaskTP ShiftRightAssign(ref regMaskTP destination, int count)
    {
        destination >>= count;

        return ref destination;
    }

    public static bool operator ==(regMaskTP left, regMaskTP right) => (left._lower == right._lower) && (left._upper == right._upper);

    public static bool operator !=(regMaskTP left, regMaskTP right) => (left._lower != right._lower) || (left._upper != right._upper);

    public static regMaskTP operator &(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower & right._lower, left._upper & right._upper);

    public static regMaskTP operator |(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower | right._lower, left._upper | right._upper);

    public static regMaskTP operator ^(regMaskTP left, regMaskTP right) => new regMaskTP(left._lower ^ right._lower, left._upper ^ right._upper);

    public static regMaskTP operator ~(regMaskTP mask) => new regMaskTP(~mask._lower, ~mask._upper);

    public readonly bool IsSet(regNumber regNum)
    {
        var mask = Configuration.HAS_MORE_THAN_64_REGISTERS && ((int)regNum >= 64) ? _upper : _lower;
        return ((long)(mask) & (1L << (int)(regNum))) is not 0;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => (obj is regMaskTP other) && (this == other);

    public bool Equals(regMaskTP other) => this == other;

    public override int GetHashCode() => Configuration.HAS_MORE_THAN_64_REGISTERS
        ? HashCode.Combine(_lower, _upper)
        : Configuration.TARGET_X86
            ? unchecked((int)(long)_lower)
            : _lower.GetHashCode();

    public override string ToString() => Configuration.HAS_MORE_THAN_64_REGISTERS
        ? $"{{Lower = {_lower}, Upper = {_upper}}}"
        : _lower.ToString();
}
