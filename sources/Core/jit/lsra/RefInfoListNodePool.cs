// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal sealed class RefInfoListNode
{
    internal RefPosition? refPosition;
    internal GenTree? treeNode;
    internal RefInfoListNode? next;
}

internal sealed class RefInfoListNodePool
{
    private const int DefaultPreallocation = 8;

    private RefInfoListNode? _freeList;

    public RefInfoListNodePool(int preallocate = DefaultPreallocation)
    {
        for (var index = 0; index < preallocate; index++)
        {
            _freeList = new RefInfoListNode { next = _freeList };
        }
    }

    public RefInfoListNode GetNode(RefPosition refPosition, GenTree? treeNode)
    {
        var node = _freeList;
        if (node is null)
        {
            node = new RefInfoListNode();
        }
        else
        {
            _freeList = node.next;
        }

        node.refPosition = refPosition;
        node.treeNode = treeNode;
        node.next = null;
        return node;
    }

    public void ReturnNode(RefInfoListNode node)
    {
        node.next = _freeList;
        _freeList = node;
    }
}
