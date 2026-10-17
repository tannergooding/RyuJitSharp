// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

using elemType = ulong;
using indexType = ulong;

namespace RyuJitSharp;

public sealed partial class hashBv
{
    [Conditional("DEBUG")]
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

    [Conditional("DEBUG")]
    public void dumpFancy()
    {
        var lastOne = Globals.NormalizeIndex(ulong.MaxValue);
        var lastZero = Globals.NormalizeIndex(ulong.MaxValue);

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
}

public sealed class hashBvIterator
{
    public static indexType NOMOREBITS => Globals.NormalizeIndex(ulong.MaxValue);

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
        current_data = Globals.NormalizeElement(currNode.elements[0]);
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
                current_data = Globals.NormalizeElement(currNode.elements[unchecked((int)current_element)]);
                current_base = Globals.AddIndex(
                    currNode.baseIndex,
                    current_element * BITS_PER_ELEMENT);
                continue;
            }

            while (current_data != 0)
            {
                if ((current_data & 1) != 0)
                {
                    current_data >>= 1;
                    current_base = Globals.AddIndex(current_base, 1);
                    return Globals.NormalizeIndex(current_base - 1);
                }

                current_data >>= 1;
                current_base = Globals.AddIndex(current_base, 1);
            }
        }

        return NOMOREBITS;
    }
}
