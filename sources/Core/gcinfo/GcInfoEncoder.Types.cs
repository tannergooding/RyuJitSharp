// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Numerics;

namespace RyuJitSharp;

[Flags]
public enum GcSlotFlags
{
    GC_SLOT_BASE = 0,
    GC_SLOT_INTERIOR = 1,
    GC_SLOT_PINNED = 2,
    GC_SLOT_UNTRACKED = 4,
    GC_SLOT_IS_REGISTER = 8,
    GC_SLOT_IS_DELETED = 16,
}

public enum GcStackSlotBase
{
    GC_CALLER_SP_REL = 0,
    GC_SP_REL = 1,
    GC_FRAMEREG_REL = 2,
    GC_SPBASE_FIRST = GC_CALLER_SP_REL,
    GC_SPBASE_LAST = GC_FRAMEREG_REL,
}

public enum GcSlotState
{
    GC_SLOT_DEAD = 0,
    GC_SLOT_LIVE = 1,
}

public enum GENERIC_CONTEXTPARAM_TYPE
{
    GENERIC_CONTEXTPARAM_NONE = 0,
    GENERIC_CONTEXTPARAM_MT = 1,
    GENERIC_CONTEXTPARAM_MD = 2,
    GENERIC_CONTEXTPARAM_THIS = 3,
}

public sealed unsafe partial class GcInfoEncoder
{
    private const uint NUM_NORM_CODE_OFFSETS_PER_CHUNK = 64;
    private const uint NUM_NORM_CODE_OFFSETS_PER_CHUNK_LOG2 = 6;
    private const uint CODE_LENGTH_ENCBASE = 8;
    private const uint NORM_PROLOG_SIZE_ENCBASE = 5;
    private const uint NORM_EPILOG_SIZE_ENCBASE = 3;
    private const uint GS_COOKIE_STACK_SLOT_ENCBASE = 6;
    private const uint GENERICS_INST_CONTEXT_STACK_SLOT_ENCBASE = 6;
    private const uint STACK_BASE_REGISTER_ENCBASE = 3;
    private const uint SIZE_OF_EDIT_AND_CONTINUE_PRESERVED_AREA_ENCBASE = 4;
    private const uint SIZE_OF_STACK_AREA_ENCBASE = 3;
    private const uint REVERSE_PINVOKE_FRAME_ENCBASE = 6;
    private const uint NUM_REGISTERS_ENCBASE = 2;
    private const uint NUM_STACK_SLOTS_ENCBASE = 2;
    private const uint NUM_UNTRACKED_SLOTS_ENCBASE = 1;
    private const uint NUM_SAFE_POINTS_ENCBASE = 2;
    private const uint NUM_INTERRUPTIBLE_RANGES_ENCBASE = 1;
    private const uint INTERRUPTIBLE_RANGE_DELTA1_ENCBASE = 6;
    private const uint INTERRUPTIBLE_RANGE_DELTA2_ENCBASE = 6;
    private const uint REGISTER_ENCBASE = 3;
    private const uint REGISTER_DELTA_ENCBASE = 2;
    private const uint STACK_SLOT_ENCBASE = 6;
    private const uint STACK_SLOT_DELTA_ENCBASE = 4;
    private const uint POINTER_SIZE_ENCBASE = 3;
    private const uint LIVESTATE_RLE_RUN_ENCBASE = 2;
    private const uint LIVESTATE_RLE_SKIP_ENCBASE = 4;
    private const int NO_STACK_SLOT = -1;
    private const uint NO_REGISTER_OR_AREA = uint.MaxValue;

    private readonly ICorJitInfo* m_pCorJitInfo;
    private readonly CORINFO_METHOD_INFO* m_pMethodInfo;
    private readonly BitStreamWriter m_Info1 = new();
    private readonly BitStreamWriter m_Info2 = new();
    private readonly List<InterruptibleRange> m_InterruptibleRanges = [];
    private readonly List<LifetimeTransition> m_LifetimeTransitions = [];
    private readonly List<GcSlotDesc> m_SlotTable = [];

    private bool m_IsVarArg;
    private bool m_WantsReportOnlyLeaf;
    private int m_GSCookieStackSlot = NO_STACK_SLOT;
    private uint m_GSCookieValidRangeStart;
    private uint m_GSCookieValidRangeEnd = uint.MaxValue;
    private int m_GenericsInstContextStackSlot = NO_STACK_SLOT;
    private GENERIC_CONTEXTPARAM_TYPE m_contextParamType;
    private uint m_CodeLength;
    private uint m_StackBaseRegister = NO_REGISTER_OR_AREA;
    private uint m_SizeOfEditAndContinuePreservedArea = NO_REGISTER_OR_AREA;
    private int m_ReversePInvokeFrameSlot = NO_STACK_SLOT;
    private uint m_SizeOfStackOutgoingAndScratchArea = NO_REGISTER_OR_AREA;
    private uint[] m_CallSites = [];
    private byte[] m_CallSiteSizes = [];
    private nuint m_BlockSize;
    private bool m_IsSlotTableFrozen;
    private bool m_Built;
    private bool m_Emitted;
    private bool m_Disposed;

    private struct InterruptibleRange(uint start, uint stop)
    {
        public uint NormStartOffset = start;
        public uint NormStopOffset = stop;
    }

    private struct GcSlotDesc
    {
        public uint RegisterNumber;
        public int SpOffset;
        public GcStackSlotBase Base;
        public GcSlotFlags Flags;

        public readonly bool IsRegister() => (Flags & GcSlotFlags.GC_SLOT_IS_REGISTER) != 0;
        public readonly bool IsUntracked() => (Flags & GcSlotFlags.GC_SLOT_UNTRACKED) != 0;
        public readonly bool IsDeleted() => (Flags & GcSlotFlags.GC_SLOT_IS_DELETED) != 0;
    }

    private struct LifetimeTransition(uint offset, uint slotId, bool live)
    {
        public uint CodeOffset = offset;
        public uint SlotId = slotId;
        public bool BecomesLive = live;
        public bool IsDeleted;
    }

    // Native BitArray stores 32-bit chunks, including padding in the final chunk.
    private sealed class BitArray(uint numBits)
    {
        private readonly uint[] m_Data = new uint[checked((int)((numBits + 31UL) / 32))];

        public bool ReadBit(uint bit) => (m_Data[(int)(bit / 32)] & (1u << (int)(bit % 32))) != 0;

        public void WriteBit(uint bit, bool value)
        {
            if (value)
            {
                m_Data[(int)(bit / 32)] |= 1u << (int)(bit % 32);
            }
            else
            {
                m_Data[(int)(bit / 32)] &= ~(1u << (int)(bit % 32));
            }
        }

        public void SetBit(uint bit) => WriteBit(bit, true);
        public void ClearAll() => Array.Clear(m_Data);

        public void CopyFrom(BitArray other) => other.m_Data.CopyTo(m_Data, 0);

        public void UnionWith(BitArray other)
        {
            for (var i = 0; i < m_Data.Length; i++)
            {
                m_Data[i] |= other.m_Data[i];
            }
        }

        public BitArray Clone()
        {
            var copy = new BitArray((uint)m_Data.Length * 32);
            copy.CopyFrom(this);
            return copy;
        }

        public IEnumerable<uint> SetBits()
        {
            for (var i = 0; i < m_Data.Length; i++)
            {
                var bits = m_Data[i];

                while (bits != 0)
                {
                    var bit = BitOperations.TrailingZeroCount(bits);
                    yield return (uint)(i * 32 + bit);
                    bits &= bits - 1;
                }
            }
        }

        public uint NativeHash()
        {
            var hash = m_Data[0];

            for (var i = 1; i < m_Data.Length; i++)
            {
                hash = BitOperations.RotateRight(hash, 5) ^ m_Data[i];
            }

            return hash;
        }

        public bool EqualsBits(BitArray other) => m_Data.AsSpan().SequenceEqual(other.m_Data);
    }
}
