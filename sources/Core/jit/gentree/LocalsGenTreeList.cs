// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct LocalsGenTreeList : IEnumerable<GenTreeLclVarCommon>
{
    private readonly Statement _stmt;

    public LocalsGenTreeList(Statement stmt)
    {
        _stmt = stmt;
    }

    public void Remove(GenTreeLclVarCommon node)
    {
        var previous = node.Prev;
        var next = node.Next;

        if (previous is null)
        {
            assert(_stmt.TreeListBegin == node);
            _stmt.TreeListBegin = next;
        }
        else
        {
            assert(previous.Next == node);
            previous.Next = next;
        }

        if (next is null)
        {
            assert(_stmt.TreeListEnd == node);
            _stmt.TreeListEnd = previous;
        }
        else
        {
            assert(next.Prev == node);
            next.Prev = previous;
        }
    }

    public void Replace(GenTreeLclVarCommon firstNode, GenTreeLclVarCommon lastNode,
        GenTreeLclVarCommon newFirstNode, GenTreeLclVarCommon newLastNode)
    {
        assert((newFirstNode is not null) && (newLastNode is not null));

        var previous = firstNode.Prev;
        var next = lastNode.Next;

        if (previous is null)
        {
            assert(_stmt.TreeListBegin == firstNode);
            _stmt.TreeListBegin = newFirstNode;
        }
        else
        {
            assert(previous.Next == firstNode);
            previous.Next = newFirstNode;
        }

        if (next is null)
        {
            assert(_stmt.TreeListEnd == lastNode);
            _stmt.TreeListEnd = newLastNode;
        }
        else
        {
            assert(next.Prev == lastNode);
            next.Prev = newLastNode;
        }

        newFirstNode.Prev = previous;
        newLastNode.Next = next;
    }

    public Enumerator GetEnumerator()
    {
        var first = _stmt.TreeListBegin;
        assert((first is null) || (first.Oper.IsAnyLocal));
        return new Enumerator(first?.AsLclVarCommon());
    }

    IEnumerator<GenTreeLclVarCommon> IEnumerable<GenTreeLclVarCommon>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
