// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class BasicBlock : LIR.Range
{
    /// <summary>next BB in ascending PC offset order</summary>
    private BasicBlock? _next;

    private BasicBlock? _prev;

    private BBKinds _kind;

    private Statement? _stmtList;

    /// <summary>The dynamic execution weight of this block</summary>
    public weight_t bbWeight;

    public BasicBlock(GenTree? firstNode, GenTree? lastNode)
        : base(firstNode, lastNode)
    {
    }

    /// <summary>Returns the first statement in the block</summary>
    public Statement? FirstStmt => _stmtList;

    /// <summary>Returns the last statement in the block</summary>
    public Statement? LastStmt
    {
        get
        {
            var result = _stmtList;

            if (result is not null)
            {
                result = result.PrevStmt;
                assert((result is not null) && (result.NextStmt is null));
            }
            return result;
        }
    }

    public BasicBlock? Next
    {
        get
        {
            return _next;
        }

        set
        {
            assert(value is not null);
            _next = value;
            value._prev = this;
        }
    }

    public BasicBlock? Prev
    {
        get
        {
            return _prev;
        }

        set
        {
            assert(value is not null);
            _prev = value;
            value._next = this;
        }
    }

    public StatementList Statements => new StatementList(_stmtList);
}
