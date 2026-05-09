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

    public Enumerator GetEnumerator()
    {
        var first = _stmt.TreeListBegin;
        assert((first is null) || (first.Oper.IsAnyLocal));
        return new Enumerator(first?.AsLclVarCommon());
    }

    IEnumerator<GenTreeLclVarCommon> IEnumerable<GenTreeLclVarCommon>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
