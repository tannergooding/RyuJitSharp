// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public static void CheckDoublyLinkedList(GenTree? first)
    {
        // (1) ensure there are no circularities, (2) ensure the prev list is
        // precisely the inverse of the gtNext list.
        //
        // To detect circularity, use the "tortoise and hare" 2-pointer algorithm.

        if (first is null)
        {
            return;
        }

        var slowNode = first;
        var prevSlowNode = null as GenTree;

        var fastNode1 = slowNode.Next;
        var fastNode2 = fastNode1?.Next;

        while ((fastNode1 is not null) && (fastNode2 is not null))
        {
            if ((slowNode == fastNode1) || (slowNode == fastNode2))
            {
                NO_WAY("Circularity detected");
            }

            assert(slowNode.Prev == prevSlowNode, "Invalid prev link");
            prevSlowNode = slowNode;

            // the fastNodes would have gone null first.
            slowNode = slowNode.Next;
            assert(slowNode is not null);

            fastNode1 = fastNode2.Next;
            fastNode2 = fastNode1?.Next;
        }
        // If we get here, the list had no circularities, so either fastNode1 or fastNode2 must be nullptr.
        assert((fastNode1 is null) || (fastNode2 is null));

        // Need to check the rest of the gtPrev links.
        while (slowNode != null)
        {
            assert(slowNode.Prev == prevSlowNode, "Invalid prev link");
            prevSlowNode = slowNode;
            slowNode = slowNode.Next;
        }
    }
}
