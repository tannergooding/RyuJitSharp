// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

/// <summary>Encapsulates the SSA info carried by local nodes. Most local nodes have simple 1-to-1 relationships with their SSA refs. However, defs of promoted structs can represent many SSA defs at the same time, and we need to efficiently encode that.</summary>
public readonly struct SsaNumInfo
{
    // This can be in one of four states:
    //  1. Single SSA name: > RESERVED_SSA_NUM (0).
    //  2. RESERVED_SSA_NUM (0)
    //  3. "Inline composite name": packed SSA numbers of field locals (each could be RESERVED):
    //     [byte 3]: [top bit][ssa num 3] (7 bits)
    //     [byte 2]: [ssa num 2] (8 bits)
    //     [byte 1]: [compact encoding bit][ssa num 1] (7 bits)
    //     [byte 0]: [ssa num 0] (8 bits)
    //     We expect this encoding to cover the 99%+ case of composite names: locals with more
    //     than 127 defs, maximum for this encoding, are rare, and the current limit on the count
    //     of promoted fields is 4.
    //  4. "Outlined composite name": index into the "composite SSA nums" table. The table itself
    //     will have the very simple format of N (the total number of fields / simple names) slots
    //     with full SSA numbers, starting at the encoded index. Notably, the table entries will
    //     include "empty" slots (for untracked fields), as we don't expect to use the table in
    //     the common case, and in the pathological cases, the space overhead should be mitigated
    //     by the cap on the number of tracked locals.
    //
    private const int BITS_PER_SIMPLE_NUM = 8;
    private const int MAX_SIMPLE_NUM = (1 << (BITS_PER_SIMPLE_NUM - 1)) - 1;
    private const int SIMPLE_NUM_MASK = MAX_SIMPLE_NUM;
    private const int SIMPLE_NUM_COUNT = (sizeof(int) * BITS_PER_BYTE) / BITS_PER_SIMPLE_NUM;
    private const int COMPOSITE_ENCODING_BIT = 1 << 31;
    private const int OUTLINED_ENCODING_BIT = 1 << 15;
    private const int OUTLINED_INDEX_LOW_MASK = OUTLINED_ENCODING_BIT - 1;
    private const int OUTLINED_INDEX_HIGH_MASK = ~(COMPOSITE_ENCODING_BIT | OUTLINED_ENCODING_BIT | OUTLINED_INDEX_LOW_MASK);

    private readonly int _value;

    public SsaNumInfo()
    {
        _value = SsaConfig.RESERVED_SSA_NUM;
    }

    private SsaNumInfo(int value)
    {
        _value = value;
    }

    private bool HasCompactFormat
    {
        get
        {
            assert(Debugger.IsAttached || IsComposite);
            return (_value & OUTLINED_ENCODING_BIT) is 0;
        }
    }

    public bool IsComposite => !IsSimple;

    public bool IsInvalid => _value is SsaConfig.RESERVED_SSA_NUM;

    public bool IsSimple => IsInvalid || IsSsaNum(_value);

    public int Num
    {
        get
        {
            assert(Debugger.IsAttached || IsSimple);
            return _value;
        }
    }

    /// <summary>Form a composite SSA number, one capable of representing refs to more than one SSA local.</summary>
    /// <param name="baseNum">The SSA number to base the new one on (composite/invalid)</param>
    /// <param name="compiler">The Compiler instance</param>
    /// <param name="parentLclNum">The promoted local representing a "whole" ref</param>
    /// <param name="index">The field index</param>
    /// <param name="ssaNum">The SSA number</param>
    /// <returns> A new, always composite, SSA number that represents all of the refs in "baseNum", with the field at "index" set to "ssaNum".</returns>
    /// <remarks>It is assumed that the new number represents the same "whole" ref as the old one (the same parent local). If the SSA number needs to be reset fully, a new, RESERVED one should be created, and composed from with the appropriate parent reference.</remarks>
    public static SsaNumInfo Composite(SsaNumInfo baseNum, Compiler compiler, int parentLclNum, int index, int ssaNum)
    {
        assert(baseNum.IsInvalid || baseNum.IsComposite);
        assert(compiler.lvaGetDesc(parentLclNum).lvPromoted);

        if (NumCanBeEncodedCompactly(index, ssaNum) && (baseNum.IsInvalid || baseNum.HasCompactFormat))
        {
            var ssaNumEncoded = ssaNum << (index * BITS_PER_SIMPLE_NUM);

            if (baseNum.IsInvalid)
            {
                return new SsaNumInfo(COMPOSITE_ENCODING_BIT | ssaNumEncoded);
            }
            return new SsaNumInfo(ssaNumEncoded | (baseNum._value & ~(SIMPLE_NUM_MASK << (index * BITS_PER_SIMPLE_NUM))));
        }

        if (!baseNum.IsInvalid && !baseNum.HasCompactFormat)
        {
            baseNum.GetOutlinedNumSlot(compiler, index) = ssaNum;
            return baseNum;
        }

        // This is the only path where we can encounter a null table.
        var outlinedCompositeSsaNums = compiler._outlinedCompositeSsaNums;

        if (outlinedCompositeSsaNums is null)
        {
            outlinedCompositeSsaNums = [];
            compiler._outlinedCompositeSsaNums = outlinedCompositeSsaNums;
        }

        // Allocate a new chunk for the field numbers. Once allocated, it cannot be expanded.
        var count = compiler.lvaGetDesc(parentLclNum).lvFieldCnt;
        var table = CollectionsMarshal.AsSpan(outlinedCompositeSsaNums);

        var firstSlotIdx = table.Length;
        var lastSlotIdx = firstSlotIdx + count - 1;

        // This will grow the table.
        CollectionsMarshal.SetCount(outlinedCompositeSsaNums, lastSlotIdx + 1);

        // Copy over all of the already encoded numbers.
        if (!baseNum.IsInvalid)
        {
            for (var i = firstSlotIdx; i < table.Length; i++)
            {
                table[i] = baseNum.GetNum(compiler, i);
            }
        }

        // Copy the one being set last to overwrite any previous values.
        table[firstSlotIdx + index] = ssaNum;

        // Split the index if it does not fit into a small encoding.
        if ((firstSlotIdx & ~OUTLINED_INDEX_LOW_MASK) is not 0)
        {
            var outIdxLow = firstSlotIdx & OUTLINED_INDEX_LOW_MASK;
            var outIdxHigh = (firstSlotIdx << 1) & OUTLINED_INDEX_HIGH_MASK;
            firstSlotIdx = outIdxLow | outIdxHigh;
        }
        return new SsaNumInfo(COMPOSITE_ENCODING_BIT | OUTLINED_ENCODING_BIT | firstSlotIdx);
    }

    public static SsaNumInfo Simple(int ssaNum)
    {
        assert(IsSsaNum(ssaNum) || (ssaNum == SsaConfig.RESERVED_SSA_NUM));
        return new SsaNumInfo(ssaNum);
    }

    private static bool IsSsaNum(int value) => value > SsaConfig.RESERVED_SSA_NUM;

    /// <summary>Can the given field ref be encoded compactly?</summary>
    /// <param name="index">The SSA number</param>
    /// <param name="ssaNum">The field index</param>
    /// <returns>Whether the ref of the field at "index" can be encoded through the "compact" encoding scheme.</returns>
    /// <remarks>Under stress, we randomly reduce the number of refs that can be encoded compactly, to stress the outlined encoding logic.</remarks>
    private static bool NumCanBeEncodedCompactly(int index, int ssaNum)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.compStressCompile(Compiler.STRESS_SSA_INFO, 20))
        {
            return (ssaNum - 2) < index;
        }
#endif

        assert(index < MAX_NumOfFieldsInPromotableStruct);

        return (ssaNum <= MAX_SIMPLE_NUM) &&
               ((index < SIMPLE_NUM_COUNT) || (SIMPLE_NUM_COUNT <= MAX_NumOfFieldsInPromotableStruct));
    }

    /// <summary>Get the SSA number for a given field.</summary>
    /// <param name="compiler">The Compiler instance</param>
    /// <param name="index">The field index</param>
    /// <returns>The SSA number corresponding to the field at "index".</returns>
    public int GetNum(Compiler compiler, int index)
    {
        assert(IsComposite);

        if (HasCompactFormat)
        {
            return (_value >> (index * BITS_PER_SIMPLE_NUM)) & SIMPLE_NUM_MASK;
        }

        // We expect this case to be very rare (outside stress).
        return GetOutlinedNumSlot(compiler, index);
    }

    /// <summary>Get a reference to the "outlined" SSA number for a field.</summary>
    /// <param name="compiler">The Compiler instance</param>
    /// <param name="index">The field index</param>
    /// <returns>Reference to the SSA number corresponding to the field at "index".</returns>
    private ref int GetOutlinedNumSlot(Compiler compiler, int index)
    {
        assert(IsComposite && !HasCompactFormat);

        // The "outlined" format for a composite number encodes a 30-bit-sized index.
        // First, extract it: this will need "bit stitching" from the two parts.

        var outIndexLow = _value & OUTLINED_INDEX_LOW_MASK;
        var outIndexHigh = (_value & OUTLINED_INDEX_HIGH_MASK) >> 1;

        var outIndex = outIndexLow | outIndexHigh;
        return ref CollectionsMarshal.AsSpan(compiler._outlinedCompositeSsaNums)[outIndex + index];
    }
}
