// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;

namespace RyuJitSharp;

public enum LimitType
{
    Undef,
    BinOpArray,
    Constant,
    Dependent,
    Unknown,
}

public struct Limit
{
    public Limit(LimitType type)
    {
        Type = type;
        VN = ValueNumStore.NoVN;
        Constant = 0;
    }

    public Limit(LimitType type, int constant)
    {
        assert(type is LimitType.Constant);
        Type = type;
        VN = ValueNumStore.NoVN;
        Constant = constant;
    }

    public Limit(LimitType type, ValueNum vn, int constant)
    {
        assert(type is LimitType.BinOpArray);
        Type = type;
        VN = vn;
        Constant = constant;
    }

    public LimitType Type { get; }

    public ValueNum VN { get; }

    public int Constant { get; set; }

    public readonly bool IsUndef => Type is LimitType.Undef;

    public readonly bool IsDependent => Type is LimitType.Dependent;

    public readonly bool IsUnknown => Type is LimitType.Unknown;

    public readonly bool IsConstant => Type is LimitType.Constant;

    public readonly bool IsConstantOrBinOp => Type is LimitType.Constant or LimitType.BinOpArray;

    public readonly bool IsBinOpArray => Type is LimitType.BinOpArray;

    public readonly int GetConstant()
    {
        assert(IsConstantOrBinOp);
        return Constant;
    }

    public bool AddConstant(int value)
    {
        switch (Type)
        {
            case LimitType.Dependent:
            {
                return true;
            }

            case LimitType.BinOpArray:
            case LimitType.Constant:
            {
                if (!CheckedOps.TryAdd(Constant, value, out var sum))
                {
                    return false;
                }

                Constant = sum;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    public readonly bool Equals(Limit other)
    {
        if (Type != other.Type)
        {
            return false;
        }

        return Type switch
        {
            LimitType.BinOpArray => (VN == other.VN) && (Constant == other.Constant),
            LimitType.Constant => Constant == other.Constant,
            _ => true,
        };
    }

#if DEBUG
    public override readonly string ToString() => Type switch
    {
        LimitType.Undef => "Undef",
        LimitType.Unknown => "Unknown",
        LimitType.Dependent => "Dependent",
        LimitType.BinOpArray => FormattableString.Invariant($"${VN:x} + {Constant}"),
        LimitType.Constant => Constant.ToString(CultureInfo.InvariantCulture),
        _ => FormattableString.Invariant($"InvalidLimit({(int)Type})"),
    };
#endif
}

public struct Range
{
    public Range(Limit limit)
    {
        LowerLimit = limit;
        UpperLimit = limit;
    }

    public Range(Limit lowerLimit, Limit upperLimit)
    {
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
    }

    public Limit LowerLimit { get; set; }

    public Limit UpperLimit { get; set; }

    public readonly bool IsValid()
    {
        if (LowerLimit.IsConstant && UpperLimit.IsConstant)
        {
            return LowerLimit.GetConstant() <= UpperLimit.GetConstant();
        }

        if (LowerLimit.IsBinOpArray && UpperLimit.IsBinOpArray && (LowerLimit.VN == UpperLimit.VN))
        {
            return LowerLimit.GetConstant() <= UpperLimit.GetConstant();
        }

        if (LowerLimit.IsBinOpArray && UpperLimit.IsConstant)
        {
            return LowerLimit.GetConstant() <= UpperLimit.GetConstant();
        }

        return true;
    }

    public readonly bool IsConstantRange()
        => LowerLimit.IsConstant && UpperLimit.IsConstant && IsValid();

    public readonly bool IsUndef()
        => LowerLimit.IsUndef && UpperLimit.IsUndef;

    public readonly bool IsFullRange()
        => LowerLimit.IsConstant && UpperLimit.IsConstant &&
            (LowerLimit.GetConstant() == int.MinValue) && (UpperLimit.GetConstant() == int.MaxValue);

    public readonly bool IsSingleValueConstant(out int constant)
    {
        if (LowerLimit.IsConstant && UpperLimit.IsConstant &&
            (LowerLimit.GetConstant() == UpperLimit.GetConstant()))
        {
            constant = LowerLimit.GetConstant();
            return true;
        }

        constant = default;
        return false;
    }

#if DEBUG
    public override readonly string ToString() => $"<{LowerLimit}, {UpperLimit}>";
#endif
}
