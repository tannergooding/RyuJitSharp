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

    public void AddInstructionSet(ManagedInstructionSet instructionSet)
    {
        var nativeOrdinal = Globals.GetNativeInstructionSetOrdinal(instructionSet);
        var fieldIndex = nativeOrdinal / BitsPerFlagsField;
        _flags[fieldIndex] |= 1L << (nativeOrdinal & (BitsPerFlagsField - 1));
    }

    public readonly bool Equals(CORINFO_InstructionSetFlags other)
    {
        ReadOnlySpan<long> flags = _flags;
        return flags.SequenceEqual(other._flags);
    }

    [UnscopedRef]
    public Span<long> GetFlagsRaw() => _flags;

    public readonly int GetInstructionFlagsFieldCount() => FlagsFieldCount;

    internal readonly bool HasNativeInstructionSetOrdinal(int nativeOrdinal)
    {
        if (nativeOrdinal < 0 || nativeOrdinal >= (FlagsFieldCount * BitsPerFlagsField))
        {
            return false;
        }

        var fieldIndex = nativeOrdinal / BitsPerFlagsField;
        var bitMask = 1L << (nativeOrdinal & (BitsPerFlagsField - 1));
        return (_flags[fieldIndex] & bitMask) != 0;
    }

    public readonly bool HasInstructionSet(ManagedInstructionSet instructionSet)
    {
        var nativeOrdinal = Globals.GetNativeInstructionSetOrdinal(instructionSet);
        var fieldIndex = nativeOrdinal / BitsPerFlagsField;
        var bitMask = 1L << (nativeOrdinal & (BitsPerFlagsField - 1));
        return (_flags[fieldIndex] & bitMask) != 0;
    }

    public readonly bool IsEmpty() => !((ReadOnlySpan<long>)(_flags)).ContainsAnyExcept(0);

    public void RemoveInstructionSet(ManagedInstructionSet instructionSet)
    {
        var nativeOrdinal = Globals.GetNativeInstructionSetOrdinal(instructionSet);
        var fieldIndex = nativeOrdinal / BitsPerFlagsField;
        var bitMask = 1L << (nativeOrdinal & (BitsPerFlagsField - 1));
        _flags[fieldIndex] &= ~bitMask;
    }

    public void Reset()
    {
        Span<long> flags = _flags;
        flags.Clear();
    }
}
