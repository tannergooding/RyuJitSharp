// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed unsafe partial class GcInfoEncoder
{
    internal enum GcInfoField
    {
        UntrackedSlotSize,
        NumUntrackedSize,
        FlagsSize,
        RetKindSize,
        CodeLengthSize,
        ProEpilogSize,
        SecObjSize,
        GsCookieSize,
        PspSymSize,
        GenericsCtxSize,
        StackBaseSize,
        ReversePInvokeFrameSize,
        FixedAreaSize,
        EncInfoSize,
        NumCallSitesSize,
        NumRangesSize,
        CallSitePosSize,
        RangeSize,
        NumRegsSize,
        NumStackSize,
        RegSlotSize,
        StackSlotSize,
        CallSiteStateSize,
        EhPosSize,
        EhStateSize,
        ChunkPtrSize,
        ChunkMaskSize,
        ChunkFinalStateSize,
        ChunkTransitionSize,
        Count,
    }

#if DEBUG
    internal sealed class GcInfoSize
    {
        private readonly nuint[] m_Fields = new nuint[(int)GcInfoField.Count];

        public nuint TotalSize { get; private set; }
        public nuint NumMethods;
        public nuint NumCallSites;
        public nuint NumRanges;
        public nuint NumRegs;
        public nuint NumStack;
        public nuint NumUntracked;
        public nuint NumTransitions;
        public nuint SizeOfCode;

        private nuint this[GcInfoField field]
        {
            get
            {
                return m_Fields[(int)field];
            }
            set
            {
                m_Fields[(int)field] = value;
            }
        }

        public nuint GetFieldSize(string field)
        {
            if (!Enum.TryParse(field, out GcInfoField category) || category == GcInfoField.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(field));
            }

            return this[category];
        }

        public void Record(GcInfoField field, nuint bits)
        {
            this[field] = unchecked(this[field] + bits);
            TotalSize = unchecked(TotalSize + bits);
        }

        public void Add(GcInfoSize other)
        {
            TotalSize = unchecked(TotalSize + other.TotalSize);
            NumMethods = unchecked(NumMethods + other.NumMethods);
            NumCallSites = unchecked(NumCallSites + other.NumCallSites);
            NumRanges = unchecked(NumRanges + other.NumRanges);
            NumRegs = unchecked(NumRegs + other.NumRegs);
            NumStack = unchecked(NumStack + other.NumStack);
            NumUntracked = unchecked(NumUntracked + other.NumUntracked);
            NumTransitions = unchecked(NumTransitions + other.NumTransitions);
            SizeOfCode = unchecked(SizeOfCode + other.SizeOfCode);

            for (var i = 0; i < m_Fields.Length; i++)
            {
                m_Fields[i] = unchecked(m_Fields[i] + other.m_Fields[i]);
            }
        }
    }

    internal static readonly GcInfoSize g_FiGcInfoSize = new();
    internal static readonly GcInfoSize g_PiGcInfoSize = new();
    internal static nuint g_NumSlimHeaders;
    internal static nuint g_NumFatHeaders;
    internal readonly GcInfoSize m_CurrentMethodSize = new();
#endif

    private void Write(BitStreamWriter writer, nuint value, uint count, GcInfoField field)
    {
        writer.Write(value, count);
#if DEBUG
        m_CurrentMethodSize.Record(field, count);
#endif
    }

    private void WriteUnsigned(BitStreamWriter writer, nuint value, uint @base, GcInfoField field)
    {
#if DEBUG
        var bits = writer.EncodeVarLengthUnsigned(value, @base);
        m_CurrentMethodSize.Record(field, (nuint)bits);
#else
        _ = writer.EncodeVarLengthUnsigned(value, @base);
#endif
    }

    private void WriteSigned(BitStreamWriter writer, nint value, uint @base, GcInfoField field)
    {
#if DEBUG
        var bits = writer.EncodeVarLengthSigned(value, @base);
        m_CurrentMethodSize.Record(field, (nuint)bits);
#else
        _ = writer.EncodeVarLengthSigned(value, @base);
#endif
    }

    private void WriteVector(BitStreamWriter writer, BitArray vector, GcInfoField field)
    {
#if DEBUG
        var before = writer.GetBitCount();
#endif
        WriteSlotStateVector(writer, vector);
#if DEBUG
        m_CurrentMethodSize.Record(field, writer.GetBitCount() - before);
#endif
    }

    private void WriteVarVector(BitStreamWriter writer, BitArray vector, uint baseSkip, uint baseRun, GcInfoField field)
    {
#if DEBUG
        var bits = WriteSlotStateVarLengthVector(writer, vector, baseSkip, baseRun);
        m_CurrentMethodSize.Record(field, bits);
#else
        _ = WriteSlotStateVarLengthVector(writer, vector, baseSkip, baseRun);
#endif
    }

    private void FinishMeasurements(bool slimHeader, uint callSites, uint ranges, uint registers, uint stack, uint untracked)
    {
#if DEBUG
        assert(m_CurrentMethodSize.TotalSize == m_Info1.GetBitCount() + m_Info2.GetBitCount());

        if (slimHeader)
        {
            g_NumSlimHeaders++;
        }
        else
        {
            g_NumFatHeaders++;
        }

        m_CurrentMethodSize.NumMethods = 1;
        m_CurrentMethodSize.NumCallSites = callSites;
        m_CurrentMethodSize.NumRanges = ranges;
        m_CurrentMethodSize.NumRegs = registers;
        m_CurrentMethodSize.NumStack = stack;
        m_CurrentMethodSize.NumUntracked = untracked;
        m_CurrentMethodSize.SizeOfCode = m_CodeLength;

        if (ranges != 0)
        {
            g_FiGcInfoSize.Add(m_CurrentMethodSize);
        }
        else
        {
            g_PiGcInfoSize.Add(m_CurrentMethodSize);
        }
#endif
    }
}
