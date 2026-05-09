// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct PredEdgeList : IEnumerable<FlowEdge>
{
    private readonly FlowEdge? m_begin;
    private readonly bool m_allowEdits;

    public PredEdgeList(FlowEdge? pred, bool allowEdits)
    {
        m_begin = pred;
        m_allowEdits = allowEdits;
    }

    public Enumerator GetEnumerator() => new Enumerator(m_begin, m_allowEdits);

    IEnumerator<FlowEdge> IEnumerable<FlowEdge>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
