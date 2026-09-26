// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed unsafe partial class GcInfoEncoder
{
    private void WriteSlotStateVector(BitStreamWriter writer, BitArray vector)
    {
        for (var i = 0; i < m_SlotTable.Count && !m_SlotTable[i].IsUntracked(); i++)
        {
            if (!m_SlotTable[i].IsDeleted())
            {
                writer.Write(vector.ReadBit((uint)i) ? 1u : 0u, 1);
            }
            else
            {
                assert(!vector.ReadBit((uint)i));
            }
        }
    }

    private (uint Simple, uint Rle, uint RleNeg) GetSlotStateVarLengthSizes(BitArray vector, uint baseSkip, uint baseRun)
    {
        var simple = 1u;

        for (var i = 0; i < m_SlotTable.Count && !m_SlotTable[i].IsUntracked(); i++)
        {
            if (!m_SlotTable[i].IsDeleted())
            {
                simple++;
            }
        }

        if (simple <= 2 + baseSkip + 1 + baseRun + 1)
        {
            return (simple, simple + 1, simple + 1);
        }

        var rle = 2u;
        var rleNeg = 2u;
        var start = 0u;
        var previous = false;
        uint index;

        for (index = 0; index < (uint)m_SlotTable.Count && !m_SlotTable[(int)index].IsUntracked(); index++)
        {
            if (m_SlotTable[(int)index].IsDeleted())
            {
                start++;
                continue;
            }

            var live = vector.ReadBit(index);

            if (live != previous)
            {
                rle += (uint)BitStreamWriter.SizeofVarLengthUnsigned(index - start, previous ? baseRun : baseSkip);
                rleNeg += (uint)BitStreamWriter.SizeofVarLengthUnsigned(index - start, previous ? baseSkip : baseRun);
                start = index + 1;
                previous = live;
            }
        }

        assert(index >= start);
        rle += (uint)BitStreamWriter.SizeofVarLengthUnsigned(index - start, previous ? baseRun : baseSkip);
        rleNeg += (uint)BitStreamWriter.SizeofVarLengthUnsigned(index - start, previous ? baseSkip : baseRun);
        return (simple, rle, rleNeg);
    }

    private uint SizeofSlotStateVarLengthVector(BitArray vector, uint baseSkip, uint baseRun)
    {
        var (simple, rle, rleNeg) = GetSlotStateVarLengthSizes(vector, baseSkip, baseRun);
        return Math.Min(simple, Math.Min(rle, rleNeg));
    }

    private uint WriteSlotStateVarLengthVector(BitStreamWriter writer, BitArray vector, uint baseSkip, uint baseRun)
    {
        var (simple, rle, rleNeg) = GetSlotStateVarLengthSizes(vector, baseSkip, baseRun);
        var startBitCount = writer.GetBitCount();
        uint result;

        if (simple <= rle && simple <= rleNeg)
        {
            writer.Write(0, 1);
            WriteSlotStateVector(writer, vector);
            result = simple;
        }
        else
        {
            writer.Write(1, 1);

            if (rleNeg < rle)
            {
                writer.Write(1, 1);
                (baseSkip, baseRun) = (baseRun, baseSkip);
                result = rleNeg;
            }
            else
            {
                writer.Write(0, 1);
                result = rle;
            }

            var start = 0u;
            var previous = false;
            uint index;

            for (index = 0; index < (uint)m_SlotTable.Count && !m_SlotTable[(int)index].IsUntracked(); index++)
            {
                if (m_SlotTable[(int)index].IsDeleted())
                {
                    start++;
                    continue;
                }

                var live = vector.ReadBit(index);

                if (live != previous)
                {
                    _ = writer.EncodeVarLengthUnsigned(index - start, previous ? baseRun : baseSkip);
                    start = index + 1;
                    previous = live;
                }
            }

            assert(index >= start);
            _ = writer.EncodeVarLengthUnsigned(index - start, previous ? baseRun : baseSkip);
        }

        assert(startBitCount + result == writer.GetBitCount());
        return result;
    }

    private static void EliminateRedundantLiveDeadPairs(List<LifetimeTransition> transitions)
    {
        for (var i = 0; i < transitions.Count; i++)
        {
            if (i + 1 < transitions.Count &&
                transitions[i].CodeOffset == transitions[i + 1].CodeOffset &&
                transitions[i].SlotId == transitions[i + 1].SlotId &&
                transitions[i].IsDeleted == transitions[i + 1].IsDeleted &&
                transitions[i].BecomesLive != transitions[i + 1].BecomesLive)
            {
                transitions.RemoveRange(i, 2);
                i--;
            }
        }
    }

    private sealed class LiveStateTable
    {
        private static readonly int[] s_Primes =
        [
            9, 23, 59, 131, 239, 433, 761, 1399, 2473, 4327, 7499, 12973, 22433,
            46559, 96581, 200341, 415517, 861719, 1787021, 3705617, 7684087,
            15933877, 33040633, 68513161, 142069021, 294594427, 733045421
        ];

        internal sealed class Node(BitArray state, Node? next)
        {
            public readonly BitArray State = state;
            public Node? Next = next;
            public uint Offset;
        }

        private Node?[] m_Buckets = [];
        private int m_Count;
        private int m_Max;

        public Node LookupOrAdd(BitArray state)
        {
            if (m_Buckets.Length != 0)
            {
                var existingBucket = (int)(state.NativeHash() % (uint)m_Buckets.Length);

                for (var node = m_Buckets[existingBucket]; node is not null; node = node.Next)
                {
                    if (node.State.EqualsBits(state))
                    {
                        return node;
                    }
                }
            }

            if (m_Count == m_Max)
            {
                Grow();
            }

            var bucket = (int)(state.NativeHash() % (uint)m_Buckets.Length);
            var added = new Node(state.Clone(), m_Buckets[bucket]);
            m_Buckets[bucket] = added;
            m_Count++;
            return added;
        }

        public IEnumerable<Node> Entries()
        {
            foreach (var bucket in m_Buckets)
            {
                for (var node = bucket; node is not null; node = node.Next)
                {
                    yield return node;
                }
            }
        }

        private void Grow()
        {
            var size = Math.Max(7, m_Count * 3 / 2 * 4 / 3);
            var prime = Array.Find(s_Primes, item => item >= size);

            if (prime == 0)
            {
                NOMEM();
            }

            var buckets = new Node?[prime];

            foreach (var bucket in m_Buckets)
            {
                for (var node = bucket; node is not null;)
                {
                    var next = node.Next;
                    var index = (int)(node.State.NativeHash() % (uint)prime);
                    node.Next = buckets[index];
                    buckets[index] = node;
                    node = next;
                }
            }

            m_Buckets = buckets;
            m_Max = prime * 3 / 4;
        }
    }
}
