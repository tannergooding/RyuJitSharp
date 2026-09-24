// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeCC : GenTree
{
    private GenCondition _condition;

    public GenTreeCC(genTreeOps oper, var_types type, GenCondition condition)
        : base(oper, type)
    {
        _condition = condition;
        assert(oper.IsCC);
    }

    internal GenTreeCC(genTreeOps oper, var_types type, GenCondition condition, GenTree source, NodeThreading threading)
        : base(oper, type, source, threading)
    {
        _condition = condition;
        assert(oper.IsCC);
    }

    public GenCondition Condition
    {
        get
        {
            return _condition;
        }

        set
        {
            _condition = value;
        }
    }
}
