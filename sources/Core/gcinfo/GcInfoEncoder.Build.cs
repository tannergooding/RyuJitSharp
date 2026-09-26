// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Numerics;

namespace RyuJitSharp;

public sealed unsafe partial class GcInfoEncoder : IDisposable
{
    public void Build()
    {
        EnsureMutable();
        assert(m_IsSlotTableFrozen || m_SlotTable.Count == 0);
        assert(m_CodeLength > 0);
#if DEBUG
        var methodName = stackalloc byte[256];
        var className = stackalloc byte[256];
        _ = m_pCorJitInfo->printMethodName(m_pMethodInfo->ftn, methodName, 256);
        _ = m_pCorJitInfo->printClassName(m_pCorJitInfo->getMethodClass(m_pMethodInfo->ftn), className, 256);
#endif
        m_Built = true;

        var slimHeader = BuildHeader();

        var ranges = m_InterruptibleRanges.ToArray();
        var liveState = new BitArray((uint)m_SlotTable.Count);
        var couldBeLive = new BitArray((uint)m_SlotTable.Count);

        assert(m_CallSites.Length == 0 || ranges.Length == 0);
        var callSites = new uint[m_CallSites.Length];

        for (var i = 0; i < callSites.Length; i++)
        {
            callSites[i] = unchecked(m_CallSites[i] + m_CallSiteSizes[i]);
        }

        WriteUnsigned(m_Info1, (nuint)callSites.Length, NUM_SAFE_POINTS_ENCBASE, GcInfoField.NumCallSitesSize);

        if (!slimHeader)
        {
            WriteUnsigned(m_Info1, (nuint)ranges.Length, NUM_INTERRUPTIBLE_RANGES_ENCBASE, GcInfoField.NumRangesSize);
        }

        var numBitsPerOffset = CeilOfLog2(m_CodeLength);

        foreach (var callSite in callSites)
        {
            Write(m_Info1, callSite, numBitsPerOffset, GcInfoField.CallSitePosSize);
        }

        var lastStop = 0u;

        foreach (var range in ranges)
        {
            WriteUnsigned(m_Info1, unchecked(range.NormStartOffset - lastStop),
                          INTERRUPTIBLE_RANGE_DELTA1_ENCBASE, GcInfoField.RangeSize);
            WriteUnsigned(m_Info1, unchecked(range.NormStopOffset - range.NormStartOffset - 1),
                          INTERRUPTIBLE_RANGE_DELTA2_ENCBASE, GcInfoField.RangeSize);
            lastStop = range.NormStopOffset;
        }

        var transitions = new List<LifetimeTransition>(m_LifetimeTransitions);
        transitions.Sort(static (left, right) =>
        {
            var offset = left.CodeOffset.CompareTo(right.CodeOffset);
            return offset != 0 ? offset : left.SlotId.CompareTo(right.SlotId);
        });

        while (transitions.Count > 0 && transitions[^1].CodeOffset >= m_CodeLength)
        {
            assert(transitions[^1].CodeOffset == m_CodeLength && !transitions[^1].BecomesLive);
            transitions.RemoveAt(transitions.Count - 1);
        }

        EliminateRedundantLiveDeadPairs(transitions);
        SortSlotsAndRemapTransitions(transitions);

        MarkUsedSlots(transitions, ranges, callSites, liveState, couldBeLive);
        var (registers, stackSlots, untracked) = CountSlots();

        Write(m_Info1, registers > 0 ? 1u : 0u, 1, GcInfoField.FlagsSize);

        if (registers > 0)
        {
            WriteUnsigned(m_Info1, registers, NUM_REGISTERS_ENCBASE, GcInfoField.NumRegsSize);
        }

        Write(m_Info1, stackSlots + untracked > 0 ? 1u : 0u, 1, GcInfoField.FlagsSize);

        if (stackSlots + untracked > 0)
        {
            WriteUnsigned(m_Info1, stackSlots, NUM_STACK_SLOTS_ENCBASE, GcInfoField.NumStackSize);
            WriteUnsigned(m_Info1, untracked, NUM_UNTRACKED_SLOTS_ENCBASE, GcInfoField.NumUntrackedSize);
        }

        if (registers + stackSlots + untracked == 0)
        {
            FinishMeasurements(slimHeader, (uint)callSites.Length, (uint)ranges.Length, registers, stackSlots, untracked);
            return;
        }

        WriteSlotTable(registers, stackSlots, untracked);

        if (callSites.Length != 0)
        {
            WriteCallSiteStates(transitions, callSites, liveState, registers + stackSlots);
        }

        if (ranges.Length != 0)
        {
            WriteInterruptibleRanges(transitions, ranges, liveState, couldBeLive);
        }

        FinishMeasurements(slimHeader, (uint)callSites.Length, (uint)ranges.Length, registers, stackSlots, untracked);
    }

    private bool BuildHeader()
    {
        var hasCookie = m_GSCookieStackSlot != NO_STACK_SLOT;
        var hasContext = m_GenericsInstContextStackSlot != NO_STACK_SLOT;
        var hasReversePInvokeFrame = m_ReversePInvokeFrameSlot != NO_STACK_SLOT;
        var slim = !m_IsVarArg && !hasCookie && !hasContext && m_InterruptibleRanges.Count == 0 &&
                   !hasReversePInvokeFrame && (m_StackBaseRegister == NO_REGISTER_OR_AREA || (m_StackBaseRegister ^ 5u) == 0) &&
                   !m_WantsReportOnlyLeaf && m_SizeOfEditAndContinuePreservedArea == NO_REGISTER_OR_AREA;

        Write(m_Info1, slim ? 0u : 1u, 1, GcInfoField.FlagsSize);

        if (slim)
        {
            Write(m_Info1, m_StackBaseRegister == NO_REGISTER_OR_AREA ? 0u : 1u, 1, GcInfoField.FlagsSize);
        }
        else
        {
            Write(m_Info1, m_IsVarArg ? 1u : 0u, 1, GcInfoField.FlagsSize);
            Write(m_Info1, 0, 1, GcInfoField.FlagsSize);
            Write(m_Info1, hasCookie ? 1u : 0u, 1, GcInfoField.FlagsSize);
            Write(m_Info1, 0, 1, GcInfoField.FlagsSize);
            Write(m_Info1, (nuint)m_contextParamType, 2, GcInfoField.FlagsSize);
            Write(m_Info1, m_StackBaseRegister == NO_REGISTER_OR_AREA ? 0u : 1u, 1, GcInfoField.FlagsSize);
            Write(m_Info1, m_WantsReportOnlyLeaf ? 1u : 0u, 1, GcInfoField.FlagsSize);
            Write(m_Info1, m_SizeOfEditAndContinuePreservedArea == NO_REGISTER_OR_AREA ? 0u : 1u, 1, GcInfoField.FlagsSize);
            Write(m_Info1, hasReversePInvokeFrame ? 1u : 0u, 1, GcInfoField.FlagsSize);
            assert(m_Info1.GetBitCount() == 11);
        }

        WriteUnsigned(m_Info1, m_CodeLength, CODE_LENGTH_ENCBASE, GcInfoField.CodeLengthSize);

        if (hasCookie)
        {
            assert(m_GSCookieValidRangeStart > 0 && m_GSCookieValidRangeStart < m_CodeLength);
            assert(m_GSCookieValidRangeEnd > 0 && m_GSCookieValidRangeEnd <= m_CodeLength);
            assert(m_GSCookieValidRangeStart <= m_GSCookieValidRangeEnd);
            WriteUnsigned(m_Info1, m_GSCookieValidRangeStart - 1, NORM_PROLOG_SIZE_ENCBASE, GcInfoField.ProEpilogSize);
            WriteUnsigned(m_Info1, m_CodeLength - m_GSCookieValidRangeEnd, NORM_EPILOG_SIZE_ENCBASE, GcInfoField.ProEpilogSize);
        }
        else if (hasContext)
        {
            assert(m_GSCookieValidRangeStart > 0 && m_GSCookieValidRangeStart < m_CodeLength);
            WriteUnsigned(m_Info1, m_GSCookieValidRangeStart - 1, NORM_PROLOG_SIZE_ENCBASE, GcInfoField.ProEpilogSize);
        }

        if (hasCookie)
        {
            WriteSigned(m_Info1, m_GSCookieStackSlot >> 3, GS_COOKIE_STACK_SLOT_ENCBASE, GcInfoField.GsCookieSize);
        }

        if (hasContext)
        {
            WriteSigned(m_Info1, m_GenericsInstContextStackSlot >> 3,
                        GENERICS_INST_CONTEXT_STACK_SLOT_ENCBASE, GcInfoField.GenericsCtxSize);
        }

        if (!slim && m_StackBaseRegister != NO_REGISTER_OR_AREA)
        {
            WriteUnsigned(m_Info1, m_StackBaseRegister ^ 5u, STACK_BASE_REGISTER_ENCBASE, GcInfoField.StackBaseSize);
        }

        if (m_SizeOfEditAndContinuePreservedArea != NO_REGISTER_OR_AREA)
        {
            WriteUnsigned(m_Info1, m_SizeOfEditAndContinuePreservedArea,
                          SIZE_OF_EDIT_AND_CONTINUE_PRESERVED_AREA_ENCBASE, GcInfoField.EncInfoSize);
        }

        if (hasReversePInvokeFrame)
        {
            WriteSigned(m_Info1, m_ReversePInvokeFrameSlot >> 3, REVERSE_PINVOKE_FRAME_ENCBASE,
                        GcInfoField.ReversePInvokeFrameSize);
        }

        if (!slim)
        {
            assert(m_SizeOfStackOutgoingAndScratchArea != NO_REGISTER_OR_AREA);
            WriteUnsigned(m_Info1, m_SizeOfStackOutgoingAndScratchArea >> 3, SIZE_OF_STACK_AREA_ENCBASE, GcInfoField.FixedAreaSize);
        }

        return slim;
    }

    private void SortSlotsAndRemapTransitions(List<LifetimeTransition> transitions)
    {
        var order = new int[m_SlotTable.Count];
        var sorted = new (GcSlotDesc Slot, int Id)[m_SlotTable.Count];

        for (var i = 0; i < sorted.Length; i++)
        {
            sorted[i] = (m_SlotTable[i], i);
        }

        Array.Sort(sorted, static (a, b) =>
        {
            var leftFlags = (int)a.Slot.Flags ^ (int)GcSlotFlags.GC_SLOT_UNTRACKED;
            var rightFlags = (int)b.Slot.Flags ^ (int)GcSlotFlags.GC_SLOT_UNTRACKED;
            var flags = rightFlags.CompareTo(leftFlags);

            if (flags != 0)
            {
                return flags;
            }

            if (a.Slot.IsRegister())
            {
                return a.Slot.RegisterNumber.CompareTo(b.Slot.RegisterNumber);
            }

            var offset = a.Slot.SpOffset.CompareTo(b.Slot.SpOffset);
            return offset != 0 ? offset : a.Slot.Base.CompareTo(b.Slot.Base);
        });

        for (var i = 0; i < sorted.Length; i++)
        {
            order[sorted[i].Id] = i;
            m_SlotTable[i] = sorted[i].Slot;
        }

        for (var i = 0; i < transitions.Count; i++)
        {
            var transition = transitions[i];
            transition.SlotId = (uint)order[transition.SlotId];
            transitions[i] = transition;
        }
    }

    private void MarkUsedSlots(List<LifetimeTransition> transitions, InterruptibleRange[] ranges,
                               uint[] callSites, BitArray liveState, BitArray couldBeLive)
    {
        if (callSites.Length > 0)
        {
            var current = 0;

            foreach (var callSite in callSites)
            {
                while (current < transitions.Count && transitions[current].CodeOffset < callSite)
                {
                    ApplyTransition(liveState, transitions[current++]);
                }

                couldBeLive.UnionWith(liveState);
            }
        }

        if (ranges.Length > 0)
        {
            liveState.ClearAll();
            var current = 0;

            foreach (var range in ranges)
            {
                while (current < transitions.Count && transitions[current].CodeOffset <= range.NormStartOffset)
                {
                    ApplyTransition(liveState, transitions[current++]);
                }

                couldBeLive.UnionWith(liveState);

                while (current < transitions.Count && transitions[current].CodeOffset < range.NormStopOffset)
                {
                    ApplyTransition(liveState, transitions[current]);
                    couldBeLive.SetBit(transitions[current].SlotId);
                    current++;
                }
            }
        }

        var used = 0;

        for (var i = 0; i < m_SlotTable.Count; i++)
        {
            var slot = m_SlotTable[i];

            if (!slot.IsUntracked() && !couldBeLive.ReadBit((uint)i))
            {
                slot.Flags |= GcSlotFlags.GC_SLOT_IS_DELETED;
                m_SlotTable[i] = slot;
            }
            else
            {
                used++;
            }
        }

        if (used != m_SlotTable.Count)
        {
            _ = transitions.RemoveAll(transition => m_SlotTable[(int)transition.SlotId].IsDeleted());
        }
    }

    private (uint Registers, uint Stack, uint Untracked) CountSlots()
    {
        var registers = 0u;
        var stack = 0u;
        var untracked = 0u;

        foreach (var slot in m_SlotTable)
        {
            if (slot.IsDeleted())
            {
                continue;
            }

            if (slot.IsRegister())
            {
                registers++;
            }
            else if (slot.IsUntracked())
            {
                untracked++;
            }
            else
            {
                stack++;
            }
        }

        return (registers, stack, untracked);
    }

    private void WriteSlotTable(uint registers, uint stackSlots, uint untracked)
    {
        var current = 0;

        for (var group = 0; group < 3; group++)
        {
            var count = group switch
            {
                0 => registers,
                1 => stackSlots,
                _ => untracked,
            };

            var previous = default(GcSlotDesc);

            for (uint index = 0; index < count; index++)
            {
                while (m_SlotTable[current].IsDeleted())
                {
                    current++;
                }

                var slot = m_SlotTable[current++];

                if (group == 0)
                {
                    if (index > 0 && previous.Flags == GcSlotFlags.GC_SLOT_IS_REGISTER)
                    {
                        WriteUnsigned(m_Info1, unchecked(slot.RegisterNumber - previous.RegisterNumber - 1),
                                      REGISTER_DELTA_ENCBASE, GcInfoField.RegSlotSize);
                    }
                    else
                    {
                        WriteUnsigned(m_Info1, slot.RegisterNumber, REGISTER_ENCBASE, GcInfoField.RegSlotSize);
                        Write(m_Info1, (nuint)slot.Flags, 2, GcInfoField.RegSlotSize);
                    }
                }
                else
                {
                    var field = group == 1 ? GcInfoField.StackSlotSize : GcInfoField.UntrackedSlotSize;
                    Write(m_Info1, (nuint)slot.Base, 2, field);
                    var offset = slot.SpOffset >> 3;

                    if (index > 0 && previous.Flags == (group == 1 ? GcSlotFlags.GC_SLOT_BASE : GcSlotFlags.GC_SLOT_UNTRACKED))
                    {
                        WriteUnsigned(m_Info1, unchecked((uint)(offset - (previous.SpOffset >> 3))),
                                      STACK_SLOT_DELTA_ENCBASE, field);
                    }
                    else
                    {
                        WriteSigned(m_Info1, offset, STACK_SLOT_ENCBASE, field);
                        Write(m_Info1, (nuint)slot.Flags, 2, field);
                    }
                }

                previous = slot;
            }
        }
    }

    private static uint CeilOfLog2(ulong value)
    {
        assert(value > 0);
        return (uint)BitOperations.Log2(unchecked(value * 2 - 1));
    }

    private static void ApplyTransition(BitArray state, LifetimeTransition transition)
    {
        assert(state.ReadBit(transition.SlotId) != transition.BecomesLive);
        state.WriteBit(transition.SlotId, transition.BecomesLive);
    }

    public byte* Emit()
    {
        if (!m_Built || m_Emitted)
        {
            throw new InvalidOperationException("Emit requires a completed Build and may only be called once.");
        }

        ObjectDisposedException.ThrowIf(m_Disposed, this);

        m_Emitted = true;
        try
        {
            m_BlockSize = m_Info1.GetByteCount() + m_Info2.GetByteCount();
            var destination = (byte*)m_pCorJitInfo->allocGCInfo((nint)m_BlockSize);

            if (destination is null)
            {
                NOMEM();
            }

            m_Info1.CopyTo(new Span<byte>(destination, checked((int)m_Info1.GetByteCount())));
            m_Info2.CopyTo(new Span<byte>(destination + (nint)m_Info1.GetByteCount(), checked((int)m_Info2.GetByteCount())));
            return destination;
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (m_Disposed)
        {
            return;
        }

        m_Info1.Dispose();
        m_Info2.Dispose();
        m_Disposed = true;
    }

    public nuint GetEncodedGCInfoSize() => m_BlockSize;
}
