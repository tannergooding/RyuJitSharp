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
    private struct DumpBitVisitor : IHbvBitVisitor<DumpBitVisitor>
    {
        private bool _first;

        public DumpBitVisitor()
        {
            _first = true;
        }

        public HbvWalk Visit(ulong index)
        {
            if (!_first)
            {
                jitprintf(" ");
            }

            jitprintf($"{index}");
            _first = false;
            return HbvWalk.Continue;
        }
    }

    private struct DumpFancyBitVisitor : IHbvBitVisitor<DumpFancyBitVisitor>
    {
        public ulong LastOne;
        public ulong LastZero;

        public DumpFancyBitVisitor()
        {
            LastOne = Globals.NormalizeIndex(ulong.MaxValue);
            LastZero = Globals.NormalizeIndex(ulong.MaxValue);
        }

        public HbvWalk Visit(ulong index)
        {
            if (LastOne != unchecked(index - 1))
            {
                if (unchecked(LastZero + 1) != LastOne)
                {
                    jitprintf($" {unchecked(LastZero + 1)}-{LastOne}");
                }
                else
                {
                    jitprintf($" {LastOne}");
                }

                LastZero = unchecked(index - 1);
            }

            LastOne = index;
            return HbvWalk.Continue;
        }
    }

    [Conditional("DEBUG")]
    public void dump()
    {
        jitprintf("{");
        var visitor = new DumpBitVisitor();
        _ = ForEachHbvBitSet(this, ref visitor);
        jitprintf("}\n");
    }

    [Conditional("DEBUG")]
    public void dumpFancy()
    {
        jitprintf("{");
        jitprintf($"count:{countBits()}");
        var visitor = new DumpFancyBitVisitor();
        _ = ForEachHbvBitSet(this, ref visitor);

        if (unchecked(visitor.LastZero + 1) != visitor.LastOne)
        {
            jitprintf($" {unchecked(visitor.LastZero + 1)}-{visitor.LastOne}");
        }
        else
        {
            jitprintf($" {visitor.LastOne}");
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

            var bitVector = bv;
            if (bitVector is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.IteratorIsNotInitialized);
            }
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
