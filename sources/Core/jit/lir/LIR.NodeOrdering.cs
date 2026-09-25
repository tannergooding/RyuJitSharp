// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class LIR
{
    public static GenTree LastNode(GenTree node1, GenTree node2)
    {
        assert(node1 is not null);
        assert(node2 is not null);

        if (node1 == node2)
        {
            return node1;
        }

        var cursor1 = node1.Next;
        var cursor2 = node2.Next;

        while (true)
        {
            if ((cursor1 == node2) || (cursor2 is null))
            {
                return node2;
            }

            if ((cursor2 == node1) || (cursor1 is null))
            {
                return node1;
            }

            cursor1 = cursor1.Next;
            cursor2 = cursor2.Next;
        }
    }

    public static GenTree LastNode(ReadOnlySpan<GenTree> nodes)
    {
        assert(nodes.Length > 0);
        var lastNode = nodes[0];
        for (var i = 1; i < nodes.Length; i++)
        {
            lastNode = LastNode(lastNode, nodes[i]);
        }

        return lastNode;
    }
}
