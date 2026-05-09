// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public ref struct FlowEdgeEnumerator : IEnumerator<FlowEdge>
{
    private readonly ReadOnlySpan<FlowEdge> _edgeEntry;
    private int _index;

    public FlowEdgeEnumerator(ReadOnlySpan<FlowEdge> edgeEntry)
    {
        _edgeEntry = edgeEntry;
        _index = -1;
    }

#nullable disable
    public readonly FlowEdge Current => _edgeEntry[_index];
#nullable restore

    [MemberNotNullWhen(true, nameof(Current))]
    public bool MoveNext()
    {
        var index = _index + 1;
        var succeeded = false;

        if (index != _edgeEntry.Length)
        {
            _index = index;
            succeeded = true;
        }
        return succeeded;
    }

    public void Reset()
    {
        _index = -1;
    }

    readonly object IEnumerator.Current => Current;

    readonly void IDisposable.Dispose() { }
}
