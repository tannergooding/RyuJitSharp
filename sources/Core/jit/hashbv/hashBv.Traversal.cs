// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

#if TARGET_AMD64
using elemType = ulong;
using indexType = ulong;
#else
using elemType = uint;
using indexType = uint;
#endif

namespace RyuJitSharp;

public sealed partial class hashBv
{
    public void InorderTraverse(Action<hashBvNode> action)
    {
        var tableSize = hashtable_size();
        var nodes = new hashBvNode?[tableSize];

        for (var i = 0; i < tableSize; i++)
        {
            nodes[i] = nodeArr[i];
        }

        while (true)
        {
            var lowest = unchecked((indexType)int.MaxValue);
            var lowestIndex = -1;

            for (var i = 0; i < tableSize; i++)
            {
                if ((nodes[i] is hashBvNode node) && (node.baseIndex < lowest))
                {
                    lowest = node.baseIndex;
                    lowestIndex = i;
                }
            }

            if (lowestIndex < 0)
            {
                break;
            }

            var next = nodes[lowestIndex] ?? throw new InvalidOperationException("A selected node is missing.");
            action(next);
            nodes[lowestIndex] = next.next;
        }
    }

    public void InorderTraverseTwo(
        hashBv other,
        Action<hashBv, hashBv, hashBvNode?, hashBvNode?> action)
    {
        var thisSize = hashtable_size();
        var otherSize = other.hashtable_size();
        var thisNodes = new hashBvNode?[thisSize];
        var otherNodes = new hashBvNode?[otherSize];

        for (var i = 0; i < thisSize; i++)
        {
            thisNodes[i] = nodeArr[i];
        }

        for (var i = 0; i < otherSize; i++)
        {
            otherNodes[i] = other.nodeArr[i];
        }

        while (true)
        {
            var lowestThis = unchecked((indexType)int.MaxValue);
            var lowestOther = unchecked((indexType)int.MaxValue);
            var lowestThisIndex = -1;
            var lowestOtherIndex = -1;

            for (var i = 0; i < thisSize; i++)
            {
                if ((thisNodes[i] is hashBvNode node) && (node.baseIndex < lowestThis))
                {
                    lowestThisIndex = i;
                    lowestThis = node.baseIndex;
                }
            }

            for (var i = 0; i < otherSize; i++)
            {
                if ((otherNodes[i] is hashBvNode node) && (node.baseIndex < lowestOther))
                {
                    lowestOtherIndex = i;
                    lowestOther = node.baseIndex;
                }
            }

            var thisNode = (lowestThisIndex < 0) ? null : thisNodes[lowestThisIndex];
            var otherNode = (lowestOtherIndex < 0) ? null : otherNodes[lowestOtherIndex];

            if ((thisNode is null) && (otherNode is null))
            {
                break;
            }

            if ((thisNode is null) || (otherNode is null))
            {
                action(this, other, thisNode, otherNode);

                if (thisNode is not null)
                {
                    thisNodes[lowestThisIndex] = thisNode.next;
                }

                if (otherNode is not null)
                {
                    otherNodes[lowestOtherIndex] = otherNode.next;
                }
            }
            else if (thisNode.baseIndex == otherNode.baseIndex)
            {
                action(this, other, thisNode, otherNode);
                thisNodes[lowestThisIndex] = thisNode.next;
                otherNodes[lowestOtherIndex] = otherNode.next;
            }
            else if (thisNode.baseIndex < otherNode.baseIndex)
            {
                action(this, other, thisNode, null);
                thisNodes[lowestThisIndex] = thisNode.next;
            }
            else
            {
                action(this, other, null, otherNode);
                otherNodes[lowestOtherIndex] = otherNode.next;
            }
        }
    }

#if DEBUG
    public void dump()
    {
        var first = true;
        jitprintf("{");
        _ = ForEachHbvBitSet(this, index =>
        {
            if (!first)
            {
                jitprintf(" ");
            }

            jitprintf($"{index}");
            first = false;
            return HbvWalk.Continue;
        });
        jitprintf("}\n");
    }

    public void dumpFancy()
    {
        var lastOne = unchecked((indexType)(-1));
        var lastZero = unchecked((indexType)(-1));

        jitprintf("{");
        jitprintf($"count:{countBits()}");
        _ = ForEachHbvBitSet(this, index =>
        {
            if (lastOne != unchecked(index - 1))
            {
                if (unchecked(lastZero + 1) != lastOne)
                {
                    jitprintf($" {unchecked(lastZero + 1)}-{lastOne}");
                }
                else
                {
                    jitprintf($" {lastOne}");
                }

                lastZero = unchecked(index - 1);
            }

            lastOne = index;
            return HbvWalk.Continue;
        });

        if (unchecked(lastZero + 1) != lastOne)
        {
            jitprintf($" {unchecked(lastZero + 1)}-{lastOne}");
        }
        else
        {
            jitprintf($" {lastOne}");
        }

        jitprintf("}\n");
    }
#endif
}

public sealed class hashBvIterator
{
    public const indexType NOMOREBITS = unchecked((indexType)(-1));

    public uint hashtable_size;
    public uint hashtable_index;
    public hashBv? bv;
    public hashBvNode? currNode;
    public indexType current_element;
    public indexType current_base;
    public elemType current_data;

    public hashBvIterator()
    {
    }

    public hashBvIterator(hashBv? bitVector)
    {
        bv = bitVector;
        current_element = 0;
        current_base = 0;
        current_data = 0;

        if (bitVector is not null)
        {
            hashtable_size = (uint)bitVector.hashtable_size();
            currNode = bitVector.nodeArr[0];

            if (currNode is null)
            {
                nextNode();
            }
        }
    }

    public void initFrom(hashBv bitVector)
    {
        bv = bitVector;
        hashtable_size = (uint)bitVector.hashtable_size();
        hashtable_index = 0;
        currNode = bitVector.nodeArr[0];
        current_element = 0;
        current_base = 0;
        current_data = 0;

        if (currNode is null)
        {
            nextNode();
        }

        if (currNode is not null)
        {
            current_data = currNode.elements[0];
        }
    }

    private void nextNode()
    {
        if (currNode is not null)
        {
            currNode = currNode.next;
        }

        while (currNode is null)
        {
            hashtable_index = unchecked(hashtable_index + 1);

            if (hashtable_index >= hashtable_size)
            {
                return;
            }

            var bitVector = bv ?? throw new InvalidOperationException("The iterator is not initialized.");
            currNode = bitVector.nodeArr[unchecked((int)hashtable_index)];
        }

        current_element = 0;
        current_base = currNode.baseIndex;
        current_data = currNode.elements[0];
    }

    public indexType nextBit()
    {
        if (currNode is null)
        {
            nextNode();
        }

        while (currNode is not null)
        {
            if (current_data == 0)
            {
                current_element = unchecked(current_element + 1);

                if (current_element == (indexType)currNode.numElements())
                {
                    nextNode();
                    continue;
                }

                assert(current_element < (indexType)currNode.numElements());
                current_data = currNode.elements[unchecked((int)current_element)];
                current_base = unchecked(
                    currNode.baseIndex + current_element * BITS_PER_ELEMENT);
                continue;
            }

            while (current_data != 0)
            {
                if ((current_data & 1) != 0)
                {
                    current_data >>= 1;
                    current_base = unchecked(current_base + 1);
                    return unchecked(current_base - 1);
                }

                current_data >>= 1;
                current_base = unchecked(current_base + 1);
            }
        }

        return NOMOREBITS;
    }
}
