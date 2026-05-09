// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class GenTreeDblCon : GenTree
{
    private double _dconVal;

    public GenTreeDblCon(double val, var_types type = TYP_DOUBLE)
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
