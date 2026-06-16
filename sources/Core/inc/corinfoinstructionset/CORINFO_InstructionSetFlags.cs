// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct CORINFO_InstructionSetFlags
{
    private const int FlagsFieldCount = 2;
    private const int BitsPerFlagsField = sizeof(long) * 8;

    private InlineArray2<long> _flags;

    public void Add(CORINFO_InstructionSetFlags other)
    {
        for (var i = 0; i < FlagsFieldCount; i++)
        {
            _flags[i] |= other._flags[i];
        }
    }

    public void AddInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        var index = GetFlagsFieldIndex(instructionSet);
        _flags[index] |= GetRelativeBitMask(instructionSet);
    }

    public readonly bool Equals(CORINFO_InstructionSetFlags other)
    {
        ReadOnlySpan<long> flags = _flags;
        return flags.SequenceEqual(other._flags);
    }

    private static int GetFlagsFieldIndex(CORINFO_InstructionSet instructionSet)
    {
        var bitIndex = (int)(instructionSet);
        return bitIndex / BitsPerFlagsField;
    }

    [UnscopedRef]
    public Span<long> GetFlagsRaw() => _flags;

    public readonly int GetInstructionFlagsFieldCount() => FlagsFieldCount;

    private static long GetRelativeBitMask(CORINFO_InstructionSet instructionSet)
    {
        return 1L << (int)(instructionSet);
    }

    public readonly bool HasInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        var index = GetFlagsFieldIndex(instructionSet);
        var bitIndex = GetRelativeBitMask(instructionSet);
        return ((_flags[index] & bitIndex) != 0);
    }

    public readonly bool IsEmpty() => !((ReadOnlySpan<long>)(_flags)).ContainsAnyExcept(0);

    public void RemoveInstructionSet(CORINFO_InstructionSet instructionSet)
    {
        var index = GetFlagsFieldIndex(instructionSet);
        var bitIndex = GetRelativeBitMask(instructionSet);
        _flags[index] &= ~bitIndex;
    }

    public void Reset()
    {
        Span<long> flags = _flags;
        flags.Clear();
    }
}
