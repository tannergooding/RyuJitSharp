// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

#if TARGET_AMD64
using indexType = ulong;
#else
using indexType = uint;
#endif

namespace RyuJitSharp;

public sealed partial class hashBv
{
    public hashBvNode?[] nodeArr = new hashBvNode?[1];
    public ushort log2_hashSize;
    public ushort numNodes;

    public static hashBv Create(Compiler compiler) => new();

    public static void Init(Compiler compiler)
    {
        // Managed allocation needs no compiler-owned arena free lists.
    }

    public int hashtable_size() => 1 << log2_hashSize;

    private static int getHashForIndex(indexType index, int tableSize)
        => (int)((index >> LOG2_BITS_PER_NODE) & (indexType)(tableSize - 1));

    public ref hashBvNode? getInsertionPointForIndex(indexType index)
    {
        var hashIndex = getHashForIndex(index, hashtable_size());
        var baseIndex = index & ~(indexType)(BITS_PER_NODE - 1);
        ref var prev = ref nodeArr[hashIndex];
        var result = prev;

        while (result is not null)
        {
            if (result.baseIndex >= baseIndex)
            {
                return ref prev;
            }

            prev = ref result.next;
            result = result.next;
        }

        return ref prev;
    }

    private hashBvNode? getNodeForIndexHelper(indexType index, bool canAdd)
    {
        index &= ~(indexType)(BITS_PER_NODE - 1);
        ref var prev = ref getInsertionPointForIndex(index);
        var node = prev;

        if ((node is not null) && node.belongsIn(index))
        {
            return node;
        }
        else if (canAdd)
        {
            var temp = new hashBvNode(index) { next = node };
            prev = temp;
            numNodes = unchecked((ushort)(numNodes + 1));
            return temp;
        }
        else
        {
            return null;
        }
    }

    public hashBvNode getOrAddNodeForIndex(indexType index)
    {
        var node = getNodeForIndexHelper(index, true);
        assert(node is not null);
        return node;
    }

    public hashBvNode? getNodeForIndex(indexType index)
    {
        index &= ~(indexType)(BITS_PER_NODE - 1);
        var node = getInsertionPointForIndex(index);
        return ((node is not null) && node.belongsIn(index)) ? node : null;
    }

    private int getNodeCount()
    {
        var result = 0;

        for (var i = 0; i < hashtable_size(); i++)
        {
            var node = nodeArr[i];

            while (node is not null)
            {
                node = node.next;
                result++;
            }
        }

        return result;
    }

    public bool IsValid()
    {
        var size = hashtable_size();
        assert(((size - 1) & size) == 0);

        for (var i = 0; i < size; i++)
        {
            var node = nodeArr[i];
            var lastIndex = -1;

            while (node is not null)
            {
                assert((int)node.baseIndex > lastIndex);
                lastIndex = (int)node.baseIndex;
                assert(i == getHashForIndex(node.baseIndex, size));

                if (node.next is hashBvNode next)
                {
                    assert(next.baseIndex > node.baseIndex);
                }

                node = node.next;
            }
        }

        return true;
    }

    public void Resize() => Resize(numNodes);

    public void Resize(int newSize)
    {
        assert(newSize > 0);
        newSize = 1 << BitOperations.Log2((uint)newSize);
        var oldSize = hashtable_size();

        if (newSize == oldSize)
        {
            return;
        }

        var newNodes = new hashBvNode?[newSize];

        if (newSize > oldSize)
        {
            // Tail references replace native pointers to the destination links.
            var tails = new hashBvNode?[newSize];

            for (var i = 0; i < oldSize; i++)
            {
                var next = nodeArr[i];

                while (next is not null)
                {
                    var curr = next;
                    next = curr.next;
                    var destination = getHashForIndex(curr.baseIndex, newSize);

                    if (tails[destination] is hashBvNode tail)
                    {
                        tail.next = curr;
                    }
                    else
                    {
                        newNodes[destination] = curr;
                    }

                    tails[destination] = curr;
                    curr.next = null;
                }
            }
        }
        else
        {
            for (var i = 0; i < oldSize; i++)
            {
                var next = nodeArr[i];

                if (next is not null)
                {
                    var destination = getHashForIndex(next.baseIndex, newSize);
                    ref var insertionPoint = ref newNodes[destination];

                    do
                    {
                        var curr = next;

                        while ((insertionPoint is hashBvNode existing) && (existing.baseIndex < curr.baseIndex))
                        {
                            insertionPoint = ref existing.next;
                        }

                        next = curr.next;
                        var temp = insertionPoint;
                        insertionPoint = curr;
                        curr.next = temp;
                    }
                    while (next is not null);
                }
            }
        }

        nodeArr = newNodes;
        log2_hashSize = (ushort)BitOperations.Log2((uint)newSize);
        assert(IsValid());
    }

    public void setBit(indexType index)
    {
        assert(numNodes == getNodeCount());

        var baseIndex = index & ~(indexType)(BITS_PER_NODE - 1);
        var result = nodeArr[0];

        // Preserve the native single-node fast path.
        if ((result is not null) && (result.baseIndex == baseIndex))
        {
            result.setBit(index);
            return;
        }

        result = getOrAddNodeForIndex(index);
        result.setBit(index);
        assert(numNodes == getNodeCount());

        if (numNodes > hashtable_size() * 4)
        {
            Resize();
        }
    }

    public void clearBit(indexType index)
    {
        assert(numNodes == getNodeCount());
        var baseIndex = index & ~(indexType)(BITS_PER_NODE - 1);
        var hashIndex = getHashForIndex(index, hashtable_size());
        ref var prev = ref nodeArr[hashIndex];
        var result = prev;

        while (result is not null)
        {
            if (result.baseIndex == baseIndex)
            {
                result.clrBit(index);

                if (!result.anySet())
                {
                    prev = result.next;
                    numNodes = unchecked((ushort)(numNodes - 1));
                }

                return;
            }
            else if (result.baseIndex > baseIndex)
            {
                return;
            }
            else
            {
                prev = ref result.next;
                result = result.next;
            }
        }

        assert(numNodes == getNodeCount());
    }

    public bool testBit(indexType index)
    {
        var baseIndex = index & ~(indexType)(BITS_PER_NODE - 1);

        if ((nodeArr[0] is hashBvNode first) && (first.baseIndex == baseIndex))
        {
            return first.getBit(index);
        }

        var hashIndex = getHashForIndex(baseIndex, hashtable_size());
        var iter = nodeArr[hashIndex];

        while (iter is not null)
        {
            if (iter.baseIndex == baseIndex)
            {
                return iter.getBit(index);
            }

            iter = iter.next;
        }

        return false;
    }
}
