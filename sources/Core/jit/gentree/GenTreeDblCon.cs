// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class GenTreeDblCon : GenTree
{
    private double _dconVal;

    public GenTreeDblCon(var_types type, double val)
        : base(GT_CNS_DBL, type)
    {
        assert(varTypeIsFloating(type));
        _dconVal = val;
    }

    public double DconVal
    {
        get
        {
            return _dconVal;
        }

        set
        {
            _dconVal = value;
        }
    }

    public bool IsAllBitsSet
    {
        get
        {
            if (Type is TYP_FLOAT)
            {
                return BitConverter.SingleToInt32Bits((float)_dconVal) == -1;

            }
            else
            {
                assert(Type is TYP_DOUBLE);
                return BitConverter.DoubleToInt64Bits(_dconVal) == -1;
            }
        }
    }

    public bool IsNaN => double.IsNaN(_dconVal);

    public bool IsNegativeZero => (_dconVal == 0.0) && double.IsNegative(_dconVal);

    public bool IsPositiveZero => (_dconVal == 0.0) && double.IsPositive(_dconVal);

    public bool IsBitwiseEqual(GenTreeDblCon other)
    {
        var otherBits = BitConverter.DoubleToInt64Bits(other._dconVal);
        return IsBitwiseEqual(otherBits);
    }

    public bool IsBitwiseEqual(long otherBits)
    {
        var bits = BitConverter.DoubleToInt64Bits(_dconVal);
        return bits == otherBits;
    }
}
