// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public unsafe partial struct CORJIT_FLAGS
{
    // Convenience constructor to set exactly one flag.
    public CORJIT_FLAGS(CorJitFlag flag)
    {
        Set(flag);
    }

    public CORJIT_FLAGS(in CORJIT_FLAGS other)
    {
        corJitFlags = other.corJitFlags;
        instructionSetFlags = other.instructionSetFlags;
    }

    public void Reset()
    {
        corJitFlags = 0;
        instructionSetFlags.Reset();
    }

    public void Set(CORINFO_InstructionSet instructionSet)
    {
        instructionSetFlags.AddInstructionSet(instructionSet);
    }

    public readonly bool IsSet(CORINFO_InstructionSet instructionSet) => instructionSetFlags.HasInstructionSet(instructionSet);

    public void Clear(CORINFO_InstructionSet instructionSet)
    {
        instructionSetFlags.RemoveInstructionSet(instructionSet);
    }

    public void Set64BitInstructionSetVariants()
    {
        instructionSetFlags.Set64BitInstructionSetVariants();
    }

    public void Set(CorJitFlag flag)
    {
        corJitFlags |= 1UL << (int)flag;
    }

    public void Clear(CorJitFlag flag)
    {
        corJitFlags &= ~(1UL << (int)flag);
    }

    public readonly bool IsSet(CorJitFlag flag) => (corJitFlags & (1UL << (int)flag)) != 0;

    public void Add(in CORJIT_FLAGS other)
    {
        corJitFlags |= other.corJitFlags;
        instructionSetFlags.Add(other.instructionSetFlags);
    }

    public readonly bool IsEmpty() => (corJitFlags == 0) && instructionSetFlags.IsEmpty();

    public void EnsureValidInstructionSetSupport()
    {
        instructionSetFlags = EnsureInstructionSetFlagsAreValid(instructionSetFlags);
    }

    /// <summary>DO NOT USE THIS FUNCTION! (except in very restricted special cases)</summary>
    public readonly ulong GetFlagsRaw() => corJitFlags;

    /// <summary>DO NOT USE THIS FUNCTION! (except in very restricted special cases)</summary>
    [UnscopedRef]
    public Span<ulong> GetInstructionSetFlagsRaw() => instructionSetFlags.GetFlagsRaw();

    public readonly CORINFO_InstructionSetFlags GetInstructionSetFlags() => instructionSetFlags;

    public readonly int GetInstructionFlagsFieldCount() => instructionSetFlags.GetInstructionFlagsFieldCount();

    private ulong corJitFlags;
    private CORINFO_InstructionSetFlags instructionSetFlags;
}
