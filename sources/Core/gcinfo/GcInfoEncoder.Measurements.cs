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

    private static class MeasurementTotals
    {
        internal static readonly GcInfoSize FullInfo = new();
        internal static readonly GcInfoSize PartialInfo = new();
        internal static nuint SlimHeaders;
        internal static nuint FatHeaders;
    }

    internal readonly GcInfoSize? m_CurrentMethodSize;

    private void Write(BitStreamWriter writer, nuint value, uint count, GcInfoField field)
    {
        writer.Write(value, count);
        m_CurrentMethodSize?.Record(field, count);
    }

    private void WriteUnsigned(BitStreamWriter writer, nuint value, uint @base, GcInfoField field)
    {
        var bits = writer.EncodeVarLengthUnsigned(value, @base);
        m_CurrentMethodSize?.Record(field, (nuint)bits);
    }

    private void WriteSigned(BitStreamWriter writer, nint value, uint @base, GcInfoField field)
    {
        var bits = writer.EncodeVarLengthSigned(value, @base);
        m_CurrentMethodSize?.Record(field, (nuint)bits);
    }

    private void WriteVector(BitStreamWriter writer, BitArray vector, GcInfoField field)
    {
        var measurements = m_CurrentMethodSize;
        var before = measurements is null ? (nuint)0 : writer.GetBitCount();
        WriteSlotStateVector(writer, vector);
        measurements?.Record(field, writer.GetBitCount() - before);
    }

    private void WriteVarVector(BitStreamWriter writer, BitArray vector, uint baseSkip, uint baseRun, GcInfoField field)
    {
        var bits = WriteSlotStateVarLengthVector(writer, vector, baseSkip, baseRun);
        m_CurrentMethodSize?.Record(field, bits);
    }

    private void FinishMeasurements(bool slimHeader, uint callSites, uint ranges, uint registers, uint stack, uint untracked)
    {
        var currentMethodSize = m_CurrentMethodSize;

        if (currentMethodSize is null)
        {
            return;
        }

        assert(currentMethodSize.TotalSize == m_Info1.GetBitCount() + m_Info2.GetBitCount());

        if (slimHeader)
        {
            MeasurementTotals.SlimHeaders++;
        }
        else
        {
            MeasurementTotals.FatHeaders++;
        }

        currentMethodSize.NumMethods = 1;
        currentMethodSize.NumCallSites = callSites;
        currentMethodSize.NumRanges = ranges;
        currentMethodSize.NumRegs = registers;
        currentMethodSize.NumStack = stack;
        currentMethodSize.NumUntracked = untracked;
        currentMethodSize.SizeOfCode = m_CodeLength;

        if (ranges != 0)
        {
            MeasurementTotals.FullInfo.Add(currentMethodSize);
        }
        else
        {
            MeasurementTotals.PartialInfo.Add(currentMethodSize);
        }
    }
}
