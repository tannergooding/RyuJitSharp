// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static class RangeOps
{
    private static Range ApplyRangeOp(Range left, Range right, bool unsignedAdd)
    {
        return new(
            left.LowerLimit.IsDependent || right.LowerLimit.IsDependent
                ? new(LimitType.Dependent) : AddLimit(left.LowerLimit, right.LowerLimit, unsignedAdd),
            left.UpperLimit.IsDependent || right.UpperLimit.IsDependent
                ? new(LimitType.Dependent) : AddLimit(left.UpperLimit, right.UpperLimit, unsignedAdd));
    }

    public static Range Add(Range left, Range right, bool unsignedAdd = false)
    {
        if (unsignedAdd)
        {
            var leftStraddlesZero = left.IsConstantRange() &&
                (left.LowerLimit.GetConstant() < 0) && (left.UpperLimit.GetConstant() >= 0);
            var rightStraddlesZero = right.IsConstantRange() &&
                (right.LowerLimit.GetConstant() < 0) && (right.UpperLimit.GetConstant() >= 0);
            if (leftStraddlesZero || rightStraddlesZero)
            {
                return new(new(LimitType.Unknown));
            }
        }

        return ApplyRangeOp(left, right, unsignedAdd);
    }

    private static Limit AddLimit(Limit first, Limit second, bool unsignedAdd)
    {
        if (first.IsConstantOrBinOp && second.IsConstantOrBinOp &&
            !(first.IsBinOpArray && second.IsBinOpArray))
        {
            var lhs = first.GetConstant();
            var rhs = second.GetConstant();
            var valid = unsignedAdd
                ? CheckedOps.TryAddUns(lhs, rhs, out _) && CheckedOps.TryAdd(lhs, rhs, out _)
                : CheckedOps.TryAdd(lhs, rhs, out _);
            if (valid)
            {
                var sum = unchecked(lhs + rhs);
                return first.IsConstant && second.IsConstant
                    ? new(LimitType.Constant, sum)
                    : new(LimitType.BinOpArray, first.IsBinOpArray ? first.VN : second.VN, sum);
            }
        }

        return new(LimitType.Unknown);
    }

    public static Range Subtract(Range left, Range right, bool unsignedSub = false)
        => unsignedSub ? new(new(LimitType.Unknown)) : Add(left, Negate(right));

    public static Range Multiply(Range left, Range right, bool unsignedMul = false)
    {
        if (!left.IsConstantRange() || !right.IsConstantRange())
        {
            return new(
                new(left.LowerLimit.IsDependent || right.LowerLimit.IsDependent
                    ? LimitType.Dependent : LimitType.Unknown),
                new(left.UpperLimit.IsDependent || right.UpperLimit.IsDependent
                    ? LimitType.Dependent : LimitType.Unknown));
        }

        var leftLow = left.LowerLimit.GetConstant();
        var leftHigh = left.UpperLimit.GetConstant();
        var rightLow = right.LowerLimit.GetConstant();
        var rightHigh = right.UpperLimit.GetConstant();
        if (MulOverflows(leftLow, rightLow) || MulOverflows(leftLow, rightHigh) ||
            MulOverflows(leftHigh, rightLow) || MulOverflows(leftHigh, rightHigh))
        {
            return new(new(LimitType.Unknown));
        }

        var a = unchecked(leftLow * rightLow);
        var b = unchecked(leftLow * rightHigh);
        var c = unchecked(leftHigh * rightLow);
        var d = unchecked(leftHigh * rightHigh);
        var minimum = Math.Min(Math.Min(a, b), Math.Min(c, d));
        var maximum = Math.Max(Math.Max(a, b), Math.Max(c, d));
        assert(maximum >= minimum);
        return new(new(LimitType.Constant, minimum), new(LimitType.Constant, maximum));

        bool MulOverflows(int first, int second)
        {
            return unsignedMul ? !CheckedOps.TryMulUns(first, second, out _) :
                !CheckedOps.TryMul(first, second, out _);
        }
    }

    public static Range ShiftRight(Range left, Range right, bool logical)
    {
        var result = new Range(new Limit(LimitType.Unknown));
        if (!right.IsConstantRange() ||
            ((uint)right.LowerLimit.GetConstant() > 31) ||
            ((uint)right.UpperLimit.GetConstant() > 31))
        {
            return result;
        }

        if (left.LowerLimit.IsConstant && (left.LowerLimit.GetConstant() >= 0))
        {
            result.LowerLimit = new(LimitType.Constant,
                left.LowerLimit.GetConstant() >> right.UpperLimit.GetConstant());
        }

        if (left.UpperLimit.IsConstant && (left.UpperLimit.GetConstant() >= 0))
        {
            result.UpperLimit = new(LimitType.Constant,
                left.UpperLimit.GetConstant() >> right.LowerLimit.GetConstant());
        }

        if (logical && (right.LowerLimit.GetConstant() >= 1) &&
            !(left.LowerLimit.IsConstant && (left.LowerLimit.GetConstant() >= 0)))
        {
            result.LowerLimit = new(LimitType.Constant, 0);
            result.UpperLimit = new(LimitType.Constant,
                unchecked((int)(uint.MaxValue >> right.LowerLimit.GetConstant())));
        }

        return result;
    }

    public static Range ShiftLeft(Range left, Range right)
        => Multiply(left, ConvertShiftToMultiply(right));

    public static Range Or(Range left, Range right)
    {
        if (!left.IsConstantRange() || !right.IsConstantRange())
        {
            return new(new(LimitType.Unknown));
        }

        var leftLow = left.LowerLimit.GetConstant();
        var leftHigh = left.UpperLimit.GetConstant();
        var rightLow = right.LowerLimit.GetConstant();
        var rightHigh = right.UpperLimit.GetConstant();
        if ((leftLow < 0) || (rightLow < 0))
        {
            return new(new(LimitType.Unknown));
        }

        var low = Math.Max(leftLow, rightLow);
        var high = Math.Max(leftHigh, rightHigh);
        high |= high >> 1;
        high |= high >> 2;
        high |= high >> 4;
        high |= high >> 8;
        high |= high >> 16;
        return new(new(LimitType.Constant, low), new(LimitType.Constant, high));
    }

    public static Range And(Range left, Range right)
    {
        var leftConstant = left.IsSingleValueConstant(out var leftValue);
        var rightConstant = right.IsSingleValueConstant(out var rightValue);
        if (leftConstant && rightConstant)
        {
            return new(new(LimitType.Constant, leftValue & rightValue));
        }

        if (leftConstant && (leftValue >= 0))
        {
            return new(new(LimitType.Constant, 0), new(LimitType.Constant, leftValue));
        }

        if (rightConstant && (rightValue >= 0))
        {
            return new(new(LimitType.Constant, 0), new(LimitType.Constant, rightValue));
        }

        return new(new(LimitType.Unknown));
    }

    public static Range UnsignedMod(Range left, Range right)
    {
        if (right.IsSingleValueConstant(out var divisor) && (divisor > 0))
        {
            return new(new(LimitType.Constant, 0), new(LimitType.Constant, divisor - 1));
        }

        return new(new(LimitType.Unknown));
    }

    public static Range UnsignedDivide(Range left, Range right)
    {
        if (!left.IsConstantRange() || !right.IsConstantRange())
        {
            return new(new(LimitType.Unknown));
        }

        var numeratorLow = left.LowerLimit.GetConstant();
        var numeratorHigh = left.UpperLimit.GetConstant();
        var divisorLow = right.LowerLimit.GetConstant();
        var divisorHigh = right.UpperLimit.GetConstant();
        if ((numeratorLow < 0) || (divisorLow <= 0))
        {
            return new(new(LimitType.Unknown));
        }

        return new(new(LimitType.Constant, numeratorLow / divisorHigh),
            new(LimitType.Constant, numeratorHigh / divisorLow));
    }

    public static Range Merge(Range left, Range right, bool monIncreasing)
    {
        assert(left.IsValid() && right.IsValid());
        var leftLow = left.LowerLimit;
        var leftHigh = left.UpperLimit;
        var rightLow = right.LowerLimit;
        var rightHigh = right.UpperLimit;
        var result = new Range(new Limit(LimitType.Unknown));

        if (leftLow.IsUnknown || rightLow.IsUnknown)
        {
            result.LowerLimit = new(LimitType.Unknown);
        }
        else if (leftLow.IsUndef)
        {
            result.LowerLimit = rightLow;
        }
        else if (leftLow.IsDependent || rightLow.IsDependent)
        {
            result.LowerLimit = monIncreasing
                ? (leftLow.IsDependent ? rightLow : leftLow) : new(LimitType.Dependent);
        }

        if (leftHigh.IsUnknown || rightHigh.IsUnknown)
        {
            result.UpperLimit = new(LimitType.Unknown);
        }
        else if (leftHigh.IsUndef)
        {
            result.UpperLimit = rightHigh;
        }
        else if (leftHigh.IsDependent || rightHigh.IsDependent)
        {
            result.UpperLimit = new(LimitType.Dependent);
        }

        if (leftLow.IsConstant && rightLow.IsConstant)
        {
            result.LowerLimit = new(LimitType.Constant,
                Math.Min(leftLow.GetConstant(), rightLow.GetConstant()));
        }

        if (leftHigh.IsConstant && rightHigh.IsConstant)
        {
            result.UpperLimit = new(LimitType.Constant,
                Math.Max(leftHigh.GetConstant(), rightHigh.GetConstant()));
        }

        if (rightHigh.Equals(leftHigh))
        {
            result.UpperLimit = rightHigh;
        }

        if (rightLow.Equals(leftLow))
        {
            result.LowerLimit = leftLow;
        }

        if (leftHigh.IsConstant && rightHigh.IsBinOpArray &&
            (rightHigh.GetConstant() >= leftHigh.GetConstant()))
        {
            result.UpperLimit = rightHigh;
        }

        if (rightHigh.IsConstant && leftHigh.IsBinOpArray &&
            (leftHigh.GetConstant() >= rightHigh.GetConstant()))
        {
            result.UpperLimit = leftHigh;
        }

        // Checked bounds are non-negative; adding a positive offset can wrap,
        // but a non-positive offset has a safe lower bound of that offset.
        if (leftLow.IsBinOpArray && rightLow.IsConstant && (leftLow.Constant <= 0))
        {
            result.LowerLimit = new(LimitType.Constant, Math.Min(leftLow.Constant, rightLow.Constant));
        }

        if (rightLow.IsBinOpArray && leftLow.IsConstant && (rightLow.Constant <= 0))
        {
            result.LowerLimit = new(LimitType.Constant, Math.Min(rightLow.Constant, leftLow.Constant));
        }

        if (leftHigh.IsBinOpArray && rightHigh.IsBinOpArray && (leftHigh.VN == rightHigh.VN))
        {
            var high = leftHigh;
            high.Constant = Math.Max(leftHigh.Constant, rightHigh.Constant);
            result.UpperLimit = high;
        }

        if (leftLow.IsBinOpArray && rightLow.IsBinOpArray && (leftLow.VN == rightLow.VN))
        {
            var low = leftLow;
            low.Constant = Math.Min(leftLow.Constant, rightLow.Constant);
            result.LowerLimit = low;
        }

        return result;
    }

    public static Range ConvertShiftToMultiply(Range range)
    {
        if (!range.LowerLimit.IsConstant || !range.UpperLimit.IsConstant)
        {
            return new(new(LimitType.Unknown));
        }

        var low = range.LowerLimit.GetConstant();
        var high = range.UpperLimit.GetConstant();
        if ((low <= 0) || (low > 31) || (high <= 0) || (high > 31))
        {
            return new(new(LimitType.Unknown));
        }

        return new(new(LimitType.Constant, unchecked(1 << low)),
            new(LimitType.Constant, unchecked(1 << high)));
    }

    public static Range Negate(Range range)
    {
        if (!range.IsConstantRange())
        {
            return new(new(LimitType.Unknown));
        }

        var high = range.UpperLimit.GetConstant();
        var low = range.LowerLimit.GetConstant();
        if ((high == int.MinValue) || (low == int.MinValue))
        {
            return new(new(LimitType.Unknown));
        }

        return new(new(LimitType.Constant, -high), new(LimitType.Constant, -low));
    }

    public static Range EvalRelop(genTreeOps oper, bool isUnsigned, Range left, Range right)
    {
        assert(left.IsValid() && right.IsValid());
        var leftLow = left.LowerLimit;
        var leftHigh = left.UpperLimit;
        var rightLow = right.LowerLimit;
        var rightHigh = right.UpperLimit;
        var unknown = new Range(new Limit(LimitType.Constant, 0), new Limit(LimitType.Constant, 1));
        if (isUnsigned && (oper is not GT_EQ and not GT_NE) &&
            (!leftLow.IsConstant || !rightLow.IsConstant ||
                (leftLow.GetConstant() < 0) || (rightLow.GetConstant() < 0)))
        {
            return unknown;
        }

        switch (oper)
        {
            case GT_GE:
            case GT_LT:
            {
                if (leftLow.IsConstant && rightHigh.IsConstant &&
                    (leftLow.GetConstant() >= rightHigh.GetConstant()))
                {
                    return new(new(LimitType.Constant, oper is GT_GE ? 1 : 0));
                }

                if (leftHigh.IsConstant && rightLow.IsConstant &&
                    (leftHigh.GetConstant() < rightLow.GetConstant()))
                {
                    return new(new(LimitType.Constant, oper is GT_GE ? 0 : 1));
                }
                break;
            }

            case GT_GT:
            case GT_LE:
            {
                if (leftLow.IsConstant && rightHigh.IsConstant &&
                    (leftLow.GetConstant() > rightHigh.GetConstant()))
                {
                    return new(new(LimitType.Constant, oper is GT_GT ? 1 : 0));
                }

                if (leftHigh.IsConstant && rightLow.IsConstant &&
                    (leftHigh.GetConstant() <= rightLow.GetConstant()))
                {
                    return new(new(LimitType.Constant, oper is GT_GT ? 0 : 1));
                }
                break;
            }

            case GT_EQ:
            case GT_NE:
            {
                if ((leftLow.IsConstant && rightHigh.IsConstant &&
                    (leftLow.GetConstant() > rightHigh.GetConstant())) ||
                    (leftHigh.IsConstant && rightLow.IsConstant &&
                    (leftHigh.GetConstant() < rightLow.GetConstant())))
                {
                    return new(new(LimitType.Constant, oper is GT_EQ ? 0 : 1));
                }

                if (left.IsSingleValueConstant(out var lhs) &&
                    right.IsSingleValueConstant(out var rhs) && (lhs == rhs))
                {
                    return new(new(LimitType.Constant, oper is GT_EQ ? 1 : 0));
                }
                break;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(nameof(oper));
            }
        }

        return unknown;
    }
}
