// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const AssertionIndex NO_ASSERTION_INDEX = 0;

    /// <summary>This is the amount we have to shift, plus the index, to get the last use bit we want.</summary>
    public const int FIELD_LAST_USE_SHIFT = 26;

    public const int HANDLE_KIND_INDEX_SHIFT = 24;

    public const int NO_CSE = 0;

    public const byte MAX_COST = byte.MaxValue;

    /// <summary>execution cost for an indirection</summary>
    public const int IND_COST_EX = 3;

#if TARGET_XARCH
    // floating-point indirections are slightly more expensive
    public const int FLT_IND_COST_EX = 5;
#else
    // TODO-CQ: Determine the appropriate cost of a floating-point indirection on other targets
    public const int FLT_IND_COST_EX = IND_COST_EX;
#endif

    public const int EMPTY_STRING_SCON = -1;

    // We use the following format when printing the Statement number: Statement->GetID()
    // This define is used with string concatenation to put this in printf format strings
    public static string FMT_STMT(int id) => $"STMT{id:D5}";

    // GTF_SPILL or GTF_SPILLED flag on a multi-reg node indicates that one or
    // more of its result regs are in that state.  The spill flags of each register
    // are stored here. We only need 2 bits per returned register,
    // so this is treated as a 2-bit array. No architecture needs more than 8 bits.

    public const int PACKED_GTF_SPILL = 1;

    public const int PACKED_GTF_SPILLED = 2;

    public const string LONGEST_COMMON_LCL_VAR_DISPLAY = "V99 PInvokeFrame";

    public const int LONGEST_COMMON_LCL_VAR_DISPLAY_LENGTH = 16;

    /// <summary>Return 1-based AssertionIndex from 0-based int index.</summary>
    /// <param name="index">0-based index</param>
    /// <returns>1-based AssertionIndex</returns>
    public static AssertionIndex GetAssertionIndex(ushort index) => (AssertionIndex)(index + 1);

    /// <summary>Get spill flag associated with the return register specified by its index.</summary>
    /// <param name="flags"></param>
    /// <param name="idx">Position or index of the return register</param>
    /// <returns>Returns GTF_* flags associated with the register. Only GTF_SPILL and GTF_SPILLED are considered.</returns>
    public static GenTreeFlags GetMultiRegSpillFlagsByIdx(MultiRegSpillFlags flags, byte idx)
    {
        assert((MAX_MULTIREG_COUNT * 2) <= (sizeof(byte) * BITS_PER_BYTE));
        assert(idx < MAX_MULTIREG_COUNT);

        // It doesn't matter that we possibly leave other high bits here.

        var bits = flags >>> (idx * 2);
        var spillFlags = GTF_EMPTY;

        if ((bits & PACKED_GTF_SPILL) != 0)
        {
            spillFlags |= GTF_SPILL;
        }

        if ((bits & PACKED_GTF_SPILLED) != 0)
        {
            spillFlags |= GTF_SPILLED;
        }

        return spillFlags;
    }

    /// <summary>Set spill flags for the register specified by its index.</summary>
    /// <param name="oldFlags">The current value of the MultiRegSpillFlags for a node.</param>
    /// <param name="flagsToSet">
    ///   <para>GTF_* flags. Only GTF_SPILL and GTF_SPILLED are allowed.</para>
    ///   <para>Note that these are the flags used on non-multireg nodes, and this method adds the appropriate flags to the incoming MultiRegSpillFlags and returns it.</para>
    /// </param>
    /// <param name="idx">Position or index of the register</param>
    /// <returns>The new value for the node's MultiRegSpillFlags.</returns>
    public static MultiRegSpillFlags SetMultiRegSpillFlagsByIdx(MultiRegSpillFlags oldFlags, GenTreeFlags flagsToSet, byte idx)
    {
        assert((MAX_MULTIREG_COUNT * 2) <= (sizeof(byte) * BITS_PER_BYTE));
        assert(idx < MAX_MULTIREG_COUNT);

        var bits = 0;

        if ((flagsToSet & GTF_SPILL) != 0)
        {
            bits |= PACKED_GTF_SPILL;
        }

        if ((flagsToSet & GTF_SPILLED) != 0)
        {
            bits |= PACKED_GTF_SPILLED;
        }

        var packedFlags = PACKED_GTF_SPILL | PACKED_GTF_SPILLED;

        // Clear anything that was already there by masking out the bits before 'or'ing in what we want there.
        return (MultiRegSpillFlags)((oldFlags & ~(packedFlags << (idx * 2))) | (bits << (idx * 2)));
    }
}
