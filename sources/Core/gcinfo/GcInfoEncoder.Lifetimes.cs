// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed unsafe partial class GcInfoEncoder
{
    private void WriteCallSiteStates(List<LifetimeTransition> transitions, uint[] callSites,
                                     BitArray liveState, uint trackedSlots)
    {
        var states = new LiveStateTable();
        var callSiteStates = new LiveStateTable.Node[callSites.Length];
        liveState.ClearAll();
        var current = 0;

        for (var i = 0; i < callSites.Length; i++)
        {
            while (current < transitions.Count && transitions[current].CodeOffset < callSites[i])
            {
                ApplyTransition(liveState, transitions[current++]);
            }

            callSiteStates[i] = states.LookupOrAdd(liveState);
        }

        var largestOffset = 0u;
        var sizeOfSets = 0u;

        foreach (var state in states.Entries())
        {
            largestOffset = sizeOfSets;
            sizeOfSets += SizeofSlotStateVarLengthVector(state.State, LIVESTATE_RLE_SKIP_ENCBASE, LIVESTATE_RLE_RUN_ENCBASE);
        }

        var numBitsPerPointer = largestOffset < 2 ? 1u : CeilOfLog2((ulong)largestOffset + 1);
        var sizeOfEncodedPointerWidth = BitStreamWriter.SizeofVarLengthUnsigned(numBitsPerPointer, POINTER_SIZE_ENCBASE);
        var sizeOfDirect = (nuint)callSites.Length * trackedSlots;
        var sizeOfIndirect = (nuint)sizeOfEncodedPointerWidth + ((nuint)callSites.Length * numBitsPerPointer) + 7 + sizeOfSets;

        if (sizeOfIndirect < sizeOfDirect)
        {
            Write(m_Info1, 1, 1, GcInfoField.FlagsSize);
            WriteUnsigned(m_Info1, numBitsPerPointer - 1, POINTER_SIZE_ENCBASE, GcInfoField.CallSiteStateSize);

            foreach (var state in states.Entries())
            {
                state.Offset = checked((uint)m_Info2.GetBitCount());
                WriteVarVector(m_Info2, state.State, LIVESTATE_RLE_SKIP_ENCBASE,
                               LIVESTATE_RLE_RUN_ENCBASE, GcInfoField.CallSiteStateSize);
            }

            assert(m_Info2.GetBitCount() == sizeOfSets);

            foreach (var state in callSiteStates)
            {
                Write(m_Info1, state.Offset, numBitsPerPointer, GcInfoField.CallSiteStateSize);
            }
        }
        else
        {
            Write(m_Info1, 0, 1, GcInfoField.FlagsSize);

            foreach (var state in callSiteStates)
            {
                WriteVector(m_Info1, state.State, GcInfoField.CallSiteStateSize);
            }
        }
    }

    private void WriteInterruptibleRanges(List<LifetimeTransition> transitions, InterruptibleRange[] ranges,
                                          BitArray liveState, BitArray couldBeLive)
    {
        var totalLength = 0u;

        foreach (var range in ranges)
        {
            totalLength = unchecked(totalLength + range.NormStopOffset - range.NormStartOffset);
        }

        assert(totalLength <= m_CodeLength);
        liveState.ClearAll();
        couldBeLive.ClearAll();
        var previousRangeState = new BitArray((uint)m_SlotTable.Count);
        var compressed = new List<LifetimeTransition>(transitions.Count);
        var current = 0;
        var cumulativeLength = 0u;

        foreach (var range in ranges)
        {
            if (current == transitions.Count)
            {
                break;
            }

            while (current < transitions.Count && transitions[current].CodeOffset <= range.NormStartOffset)
            {
                ApplyTransition(liveState, transitions[current++]);
            }

            for (var slot = 0; slot < m_SlotTable.Count; slot++)
            {
                if (liveState.ReadBit((uint)slot) != previousRangeState.ReadBit((uint)slot))
                {
                    compressed.Add(new LifetimeTransition(cumulativeLength, (uint)slot, liveState.ReadBit((uint)slot)));
                }
            }

            while (current < transitions.Count && transitions[current].CodeOffset < range.NormStopOffset)
            {
                var transition = transitions[current++];
                ApplyTransition(liveState, transition);
                transition.CodeOffset = unchecked(transition.CodeOffset - range.NormStartOffset + cumulativeLength);
                compressed.Add(transition);
            }

            cumulativeLength = unchecked(cumulativeLength + range.NormStopOffset - range.NormStartOffset);
            previousRangeState.CopyFrom(liveState);
        }

        // Native marks the skipped transitions deleted, then compacts before sorting each chunk by slot.
        var numChunks = checked((int)(((ulong)totalLength + NUM_NORM_CODE_OFFSETS_PER_CHUNK - 1) / NUM_NORM_CODE_OFFSETS_PER_CHUNK));
        var pointers = new nuint[numChunks];
        liveState.ClearAll();
        couldBeLive.ClearAll();
        current = 0;

        while (current < compressed.Count)
        {
            var start = current;
            var chunk = compressed[start].CodeOffset / NUM_NORM_CODE_OFFSETS_PER_CHUNK;

            do
            {
                var transition = compressed[current++];
                ApplyTransition(liveState, transition);
                couldBeLive.SetBit(transition.SlotId);
            }
            while (current < compressed.Count &&
                   compressed[current].CodeOffset / NUM_NORM_CODE_OFFSETS_PER_CHUNK == chunk);

            pointers[(int)chunk] = m_Info2.GetBitCount() + 1;
            WriteVarVector(m_Info2, couldBeLive, LIVESTATE_RLE_SKIP_ENCBASE,
                           LIVESTATE_RLE_RUN_ENCBASE, GcInfoField.ChunkMaskSize);

            foreach (var slot in couldBeLive.SetBits())
            {
                assert(!m_SlotTable[(int)slot].IsDeleted() && !m_SlotTable[(int)slot].IsUntracked());
                Write(m_Info2, liveState.ReadBit(slot) ? 1u : 0u, 1, GcInfoField.ChunkFinalStateSize);
            }

            compressed.Sort(start, current - start, Comparer<LifetimeTransition>.Create(static (a, b) =>
            {
                var slot = a.SlotId.CompareTo(b.SlotId);
                return slot != 0 ? slot : a.CodeOffset.CompareTo(b.CodeOffset);
            }));

            var transitionIndex = start;

            foreach (var slot in couldBeLive.SetBits())
            {
                while (transitionIndex < current && compressed[transitionIndex].SlotId == slot)
                {
                    assert(transitionIndex == start ||
                           compressed[transitionIndex - 1].SlotId != slot ||
                           compressed[transitionIndex - 1].CodeOffset < compressed[transitionIndex].CodeOffset);

                    var delta = compressed[transitionIndex].CodeOffset - chunk * NUM_NORM_CODE_OFFSETS_PER_CHUNK;

                    if (delta != 0)
                    {
                        assert(delta < NUM_NORM_CODE_OFFSETS_PER_CHUNK);
                        Write(m_Info2, 1, 1, GcInfoField.ChunkTransitionSize);
                        Write(m_Info2, delta, NUM_NORM_CODE_OFFSETS_PER_CHUNK_LOG2,
                              GcInfoField.ChunkTransitionSize);
#if DEBUG
                        m_CurrentMethodSize.NumTransitions++;
#endif
                    }

                    transitionIndex++;
                }

                Write(m_Info2, 0, 1, GcInfoField.ChunkTransitionSize);
            }

            assert(transitionIndex == current);
            couldBeLive.CopyFrom(liveState);
        }

        var largestPointer = (nuint)0;

        for (var i = pointers.Length - 1; i >= 0; i--)
        {
            largestPointer = pointers[i];

            if (largestPointer != 0)
            {
                break;
            }
        }

        var bitsPerPointer = CeilOfLog2((ulong)largestPointer + 1);
        WriteUnsigned(m_Info1, bitsPerPointer, POINTER_SIZE_ENCBASE, GcInfoField.ChunkPtrSize);

        if (bitsPerPointer != 0)
        {
            foreach (var pointer in pointers)
            {
                Write(m_Info1, pointer, bitsPerPointer, GcInfoField.ChunkPtrSize);
            }
        }
    }
}
