// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORJIT_FLAGS;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial struct CORJIT_FLAGS
{
    private ulong corJitFlags;
    private CORINFO_InstructionSetFlags instructionSetFlags;

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

    public void Add(in CORJIT_FLAGS other)
    {
        corJitFlags |= other.corJitFlags;
        instructionSetFlags.Add(other.instructionSetFlags);
    }

    public void Clear(CORINFO_InstructionSet instructionSet)
    {
        instructionSetFlags.RemoveInstructionSet(instructionSet);
    }

    public void Clear(CorJitFlag flag)
    {
        corJitFlags &= ~(1UL << (int)flag);
    }
    public void EnsureValidInstructionSetSupport()
    {
        instructionSetFlags = EnsureInstructionSetFlagsAreValid(instructionSetFlags);
    }

    /// <summary>DO NOT USE THIS FUNCTION! (except in very restricted special cases)</summary>
    public readonly ulong GetFlagsRaw() => corJitFlags;

    public readonly int GetInstructionFlagsFieldCount() => instructionSetFlags.GetInstructionFlagsFieldCount();

    public readonly CORINFO_InstructionSetFlags GetInstructionSetFlags() => instructionSetFlags;

    /// <summary>DO NOT USE THIS FUNCTION! (except in very restricted special cases)</summary>
    [UnscopedRef]
    public ref CORINFO_InstructionSetFlags.flagsInlineArray GetInstructionSetFlagsRaw() => ref instructionSetFlags.GetFlagsRaw();

    public readonly bool IsEmpty() => (corJitFlags == 0) && instructionSetFlags.IsEmpty();

    public readonly bool IsSet(CORINFO_InstructionSet instructionSet) => instructionSetFlags.HasInstructionSet(instructionSet);

    public readonly bool IsSet(CorJitFlag flag) => (corJitFlags & (1UL << (int)flag)) != 0;

    public void Reset()
    {
        corJitFlags = 0;
        instructionSetFlags.Reset();
    }

    public void Set(CORINFO_InstructionSet instructionSet)
    {
        instructionSetFlags.AddInstructionSet(instructionSet);
    }

    public void Set(CorJitFlag flag)
    {
        corJitFlags |= 1UL << (int)flag;
    }

    public void Set64BitInstructionSetVariants()
    {
        instructionSetFlags.Set64BitInstructionSetVariants();
    }
}
