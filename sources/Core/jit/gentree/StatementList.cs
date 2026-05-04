// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct StatementList : IEnumerable<Statement>
{
    private readonly Statement? _stmts;

    public StatementList(Statement? stmt)
    {
        _stmts = stmt;
    }

    public Enumerator GetEnumerator() => new Enumerator(_stmts);

    IEnumerator<Statement> IEnumerable<Statement>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
