// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed unsafe partial class GcInfoEncoder
{
    public GcInfoEncoder(ICorJitInfo* pCorJitInfo, CORINFO_METHOD_INFO* pMethodInfo)
    {
#if !TARGET_AMD64
        throw new PlatformNotSupportedException("Only the AMD64 GC info encoding is implemented.");
#else
        if (pCorJitInfo is null || pMethodInfo is null)
        {
            throw new ArgumentNullException(pCorJitInfo is null ? nameof(pCorJitInfo) : nameof(pMethodInfo));
        }

        m_pCorJitInfo = pCorJitInfo;
        m_pMethodInfo = pMethodInfo;
#endif
    }

    public void DefineCallSites(uint* pCallSites, byte* pCallSiteSizes, uint numCallSites)
    {
        if (numCallSites > 0 && (pCallSites is null || pCallSiteSizes is null))
        {
            throw new ArgumentNullException(pCallSites is null ? nameof(pCallSites) : nameof(pCallSiteSizes));
        }

        DefineCallSites(new ReadOnlySpan<uint>(pCallSites, checked((int)numCallSites)),
                        new ReadOnlySpan<byte>(pCallSiteSizes, checked((int)numCallSites)));
    }

    public void DefineCallSites(ReadOnlySpan<uint> callSites, ReadOnlySpan<byte> callSiteSizes)
    {
        EnsureMutable();

        if (callSites.Length != callSiteSizes.Length)
        {
            throw new ArgumentException("Every call site needs a corresponding instruction size.");
        }

        for (var i = 0; i < callSites.Length; i++)
        {
            assert(callSiteSizes[i] > 0);

            if (i > 0)
            {
                assert(callSites[i] >= unchecked(callSites[i - 1] + callSiteSizes[i - 1]));
            }
        }

        m_CallSites = callSites.ToArray();
        m_CallSiteSizes = callSiteSizes.ToArray();
    }

    public uint GetRegisterSlotId(uint regNum, GcSlotFlags flags)
    {
        EnsureMutable();
        assert(!m_IsSlotTableFrozen);
        assert((flags & (GcSlotFlags.GC_SLOT_IS_REGISTER | GcSlotFlags.GC_SLOT_IS_DELETED | GcSlotFlags.GC_SLOT_UNTRACKED)) == 0);
        var slotId = checked((uint)m_SlotTable.Count);
        m_SlotTable.Add(new GcSlotDesc { RegisterNumber = regNum, Flags = flags | GcSlotFlags.GC_SLOT_IS_REGISTER });
        return slotId;
    }

    public uint GetStackSlotId(int spOffset, GcSlotFlags flags, GcStackSlotBase spBase = GcStackSlotBase.GC_CALLER_SP_REL)
    {
        EnsureMutable();
        assert(!m_IsSlotTableFrozen);
        assert(spBase != GcStackSlotBase.GC_SP_REL || spOffset >= 0);
        assert((flags & (GcSlotFlags.GC_SLOT_IS_REGISTER | GcSlotFlags.GC_SLOT_IS_DELETED)) == 0);
        assert(spOffset % 8 == 0);
        var slotId = checked((uint)m_SlotTable.Count);
        m_SlotTable.Add(new GcSlotDesc { SpOffset = spOffset, Base = spBase, Flags = flags });
        return slotId;
    }

    public void FinalizeSlotIds()
    {
        EnsureMutable();
        assert(!m_IsSlotTableFrozen);
        m_IsSlotTableFrozen = true;
    }

    public void DefineInterruptibleRange(uint startInstructionOffset, uint length)
    {
        EnsureMutable();
        var stop = unchecked(startInstructionOffset + length);
        assert(m_InterruptibleRanges.Count == 0 || startInstructionOffset >= m_InterruptibleRanges[^1].NormStopOffset);

        if (stop > startInstructionOffset)
        {
            if (m_InterruptibleRanges.Count != 0 && startInstructionOffset == m_InterruptibleRanges[^1].NormStopOffset)
            {
                var range = m_InterruptibleRanges[^1];
                range.NormStopOffset = stop;
                m_InterruptibleRanges[^1] = range;
            }
            else
            {
                m_InterruptibleRanges.Add(new InterruptibleRange(startInstructionOffset, stop));
            }
        }
    }

    public void SetSlotState(uint instructionOffset, uint slotId, GcSlotState slotState)
    {
        EnsureMutable();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slotId, (uint)m_SlotTable.Count);

        assert((m_SlotTable[(int)slotId].Flags & GcSlotFlags.GC_SLOT_UNTRACKED) == 0);
        m_LifetimeTransitions.Add(new LifetimeTransition(instructionOffset, slotId, slotState == GcSlotState.GC_SLOT_LIVE));
    }

    public void SetIsVarArg()
    {
        EnsureMutable();
        m_IsVarArg = true;
    }

    public void SetCodeLength(uint length)
    {
        EnsureMutable();
        assert(length > 0);
        assert(m_CodeLength == 0 || m_CodeLength == length);
        m_CodeLength = length;
    }

    public void SetPrologSize(uint prologSize)
    {
        EnsureMutable();
        assert(prologSize != 0);
        assert(m_GSCookieValidRangeStart == 0 || m_GSCookieValidRangeStart == prologSize);
        assert(m_GSCookieValidRangeEnd == uint.MaxValue || m_GSCookieValidRangeEnd == unchecked(prologSize + 1));
        m_GSCookieValidRangeStart = prologSize;
        m_GSCookieValidRangeEnd = unchecked(prologSize + 1);
    }

    public void SetGSCookieStackSlot(int spOffsetGSCookie, uint validRangeStart, uint validRangeEnd)
    {
        EnsureMutable();
        assert(spOffsetGSCookie != NO_STACK_SLOT);
        assert(m_GSCookieStackSlot == NO_STACK_SLOT || m_GSCookieStackSlot == spOffsetGSCookie);
        assert(validRangeStart < validRangeEnd);
        m_GSCookieStackSlot = spOffsetGSCookie;
        m_GSCookieValidRangeStart = validRangeStart;
        m_GSCookieValidRangeEnd = validRangeEnd;
    }

    public void SetGenericsInstContextStackSlot(int spOffsetGenericsContext, GENERIC_CONTEXTPARAM_TYPE type)
    {
        EnsureMutable();
        assert(spOffsetGenericsContext != NO_STACK_SLOT);
        assert(m_GenericsInstContextStackSlot == NO_STACK_SLOT || m_GenericsInstContextStackSlot == spOffsetGenericsContext);
        m_GenericsInstContextStackSlot = spOffsetGenericsContext;
        m_contextParamType = type;
    }

    public void SetStackBaseRegister(uint regNum)
    {
        EnsureMutable();
        assert(regNum != NO_REGISTER_OR_AREA);
        assert(m_StackBaseRegister == NO_REGISTER_OR_AREA || m_StackBaseRegister == regNum);
        m_StackBaseRegister = regNum;
    }

    public void SetSizeOfEditAndContinuePreservedArea(uint slots)
    {
        EnsureMutable();
        assert(slots != NO_REGISTER_OR_AREA);
        assert(m_SizeOfEditAndContinuePreservedArea == NO_REGISTER_OR_AREA);
        m_SizeOfEditAndContinuePreservedArea = slots;
    }

    public void SetWantsReportOnlyLeaf()
    {
        EnsureMutable();
        m_WantsReportOnlyLeaf = true;
    }

    public void SetSizeOfStackOutgoingAndScratchArea(uint size)
    {
        EnsureMutable();
        assert(size != NO_REGISTER_OR_AREA);
        assert(m_SizeOfStackOutgoingAndScratchArea == NO_REGISTER_OR_AREA || m_SizeOfStackOutgoingAndScratchArea == size);
        m_SizeOfStackOutgoingAndScratchArea = size;
    }

    public void SetReversePInvokeFrameSlot(int spOffset)
    {
        EnsureMutable();
        m_ReversePInvokeFrameSlot = spOffset;
    }

    private void EnsureMutable()
    {
        ObjectDisposedException.ThrowIf(m_Disposed, this);

        if (m_Built || m_Emitted)
        {
            throw new InvalidOperationException("GC information cannot change after Build.");
        }
    }
}
