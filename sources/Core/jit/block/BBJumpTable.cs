// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public class BBJumpTable
{
    private FlowEdge[] _succs;
    private int _succCount;

    public BBJumpTable(FlowEdge[] succs)
    {
        _succs = succs;
        _succCount = succs.Length;
    }

    public BBJumpTable(BBJumpTable other)
    {
        _succs = [..other.Succs];
        _succCount = other._succCount;
    }

    public Span<FlowEdge> Succs => _succs.AsSpan(0, _succCount);

    public void RemoveSucc(int index)
    {
        var succs = Succs;

        if ((index + 1) < succs.Length)
        {
            succs[(index + 1)..].CopyTo(succs[index..]);
        }
        _succCount--;

        succs[^1] = null!;
    }

    public void SetSuccs(FlowEdge[] succs)
    {
        _succs = succs;
        _succCount = succs.Length;
    }

    public void SetSuccCount(int succCount)
    {
        _succCount = succCount;
    }
}
