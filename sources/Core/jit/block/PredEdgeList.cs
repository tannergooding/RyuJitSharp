// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct PredEdgeList : IEnumerable<FlowEdge>
{
    private readonly FlowEdge? _begin;
    private readonly bool _allowEdits;

    public PredEdgeList(FlowEdge? pred, bool allowEdits)
    {
        _begin = pred;
        _allowEdits = allowEdits;
    }

    public Enumerator GetEnumerator() => new Enumerator(_begin, _allowEdits);

    IEnumerator<FlowEdge> IEnumerable<FlowEdge>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
