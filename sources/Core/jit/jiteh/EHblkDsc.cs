// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct EHblkDsc
{
    public const ushort NO_ENCLOSING_INDEX = ushort.MaxValue;

    /// <summary>First block of the try</summary>
    public BasicBlock ebdTryBeg;

    /// <summary>Last block of the try</summary>
    public BasicBlock ebdTryLast;

    /// <summary>First block of the handler</summary>
    public BasicBlock ebdHndBeg;

    /// <summary>Last block of the handler</summary>
    public BasicBlock ebdHndLast;

    /// <summary>First block of filter, if HasFilter</summary>
    public BasicBlock? ebdFilter;

    /// <summary>Exception type (a class token), if !HasFilter</summary>
    public bbCatchType ebdTyp;

    /// <summary>Unique ID for this eh descriptor (stable across add/delete/inlining)</summary>
    public ushort ebdID;

    public EHHandlerType ebdHandlerType;

    // The index of the enclosing outer try region, NO_ENCLOSING_INDEX if none.
    // Be careful of 'mutually protect' catch and filter clauses (multiple
    // handlers with the same try region): the try regions 'nest' so we set
    // ebdEnclosingTryIndex, but the inner catch is *NOT* nested within the outer catch!
    // That is, if the "inner catch" throws an exception, it won't be caught by
    // the "outer catch" for mutually protect handlers.
    public ushort ebdEnclosingTryIndex;

    // The index of the enclosing outer handler region, NO_ENCLOSING_INDEX if none.
    public ushort ebdEnclosingHndIndex;

    // After funclets are created, this is the index of corresponding FuncInfoDsc
    // Special case for Filter/Filter-handler:
    //   Like the IL the filter funclet immediately precedes the filter-handler funclet.
    //   So this index points to the filter-handler funclet. If you want the filter
    //   funclet index, just subtract 1.
    public ushort ebdFuncIndex;

    internal IL_OFFSET _ebdTryBegOffset;

    internal IL_OFFSET _ebdTryEndOffset;

    internal IL_OFFSET _ebdFilterBegOffset;

    internal IL_OFFSET _ebdHndBegOffset;

    internal IL_OFFSET _ebdHndEndOffset;

    /// <summary>Returns the last block of the filter.</summary>
    /// <remarks>Assumes the EH clause is a try/filter/filter-handler type.</remarks>
    public readonly BasicBlock? BBFilterLast
    {
        get
        {
            noway_assert(HasFilter);

            noway_assert(ebdFilter is not null);
            noway_assert(ebdHndBeg is not null);

            // The last block of the filter is the block immediately preceding the first block of the handler.
            return ebdHndBeg.Prev;
        }
    }

    /// <summary>Returns 'true' for either try/catch, or try/filter/filter-handler.</summary>
    public readonly bool HasCatchHandler => ebdHandlerType is EH_HANDLER_CATCH or EH_HANDLER_FILTER;

    [MemberNotNullWhen(true, nameof(ebdFilter), nameof(BBFilterLast))]
    public readonly bool HasFilter => ebdHandlerType is EH_HANDLER_FILTER;

    public readonly bool HasFinallyHandler => ebdHandlerType is EH_HANDLER_FINALLY;

    public readonly bool HasFaultHandler => ebdHandlerType is EH_HANDLER_FAULT or EH_HANDLER_FAULT_WAS_FINALLY;

    public readonly bool HasFinallyOrFaultHandler => HasFinallyHandler || HasFaultHandler;

    /// <summary>Returns the block to which control will flow if an (otherwise-uncaught) exception is raised in the try.</summary>
    /// <remarks>
    ///   <para>This is normally "ebdHndBeg", unless the try region has a filter, in which case that is returned.</para>
    ///   <para>This is, in some sense, the "true handler," at least in the sense of control flow.</para>
    ///   <para>Note that we model the transition from a filter to its handler as normal, non-exceptional control flow.</para>
    /// </remarks>
    public readonly BasicBlock ExFlowBlock => HasFilter ? ebdFilter : ebdHndBeg;

    // We used to assert that the IL offsets in the EH table matched the IL offset stored
    // on the blocks pointed to by the try/filter/handler block pointers. This is true at
    // import time, but can fail to be true later in compilation when we start doing
    // flow optimizations.
    //
    // That being said, the IL offsets in the EH table should only be examined early,
    // during importing. After importing, use block info instead.

    /// <summary>IL offsets of EH try/end regions as they are imported</summary>
    public readonly IL_OFFSET ebdTryBegOffs => _ebdTryBegOffset;

    public readonly IL_OFFSET ebdTryEndOffs => _ebdTryEndOffset;

    /// <summary>only set if <see cref="HasFilter" /></summary>
    public readonly IL_OFFSET ebdFilterBegOffs => _ebdHndBegOffset;

    public readonly IL_OFFSET ebdFilterEndOffs => _ebdHndEndOffset;

    public readonly IL_OFFSET ebdHndBegOffs
    {
        get
        {
            assert(HasFilter);
            return _ebdFilterBegOffset;
        }
    }

    public readonly IL_OFFSET ebdHndEndOffs
    {
        get
        {
            // end of filter is beginning of handler
            assert(HasFilter);
            return _ebdHndBegOffset;
        }
    }

    public static bool ebdIsSameILTry(in EHblkDsc h1, in EHblkDsc h2)
        => ((h1._ebdTryBegOffset == h2._ebdTryBegOffset) && (h1._ebdTryEndOffset == h2._ebdTryEndOffset));

    public static bool ebdIsSameTry(in EHblkDsc h1, in EHblkDsc h2)
        => ((h1.ebdTryBeg == h2.ebdTryBeg) && (h1.ebdTryLast == h2.ebdTryLast));

    /// <summary>Returns true if pBlk is a block in the range [pStart..pEnd).</summary>
    /// <param name="pBlk"></param>
    /// <param name="pStart"></param>
    /// <param name="pEnd"></param>
    /// <returns></returns>
    /// <remarks>The check is inclusive of pStart, exclusive of pEnd.</remarks>
    private static bool InBBRange(BasicBlock pBlk, BasicBlock pStart, BasicBlock? pEnd)
    {
        for (var pWalk = pStart; pWalk != pEnd; pWalk = pWalk!.Next)
        {
            if (pWalk == pBlk)
            {
                return true;
            }
        }
        return false;
    }

    public readonly bool InTryRegionILRange(BasicBlock pBlk)
    {
        // BBF_INTERNAL blocks may not have a valid bbCodeOffs.
        // This function should only be used before any BBF_INTERNAL blocks have been added.

        assert(!pBlk.HasFlag(BBF_INTERNAL));
        return Compiler.jitIsBetween(pBlk.bbCodeOffs, _ebdTryBegOffset, _ebdTryEndOffset);
    }

    public readonly bool InFilterRegionILRange(BasicBlock pBlk)
    {
        // BBF_INTERNAL blocks may not have a valid bbCodeOffs. This function
        // should only be used before any BBF_INTERNAL blocks have been added.

        assert(!pBlk.HasFlag(BBF_INTERNAL));
        return HasFilter && Compiler.jitIsBetween(pBlk.bbCodeOffs, _ebdFilterBegOffset, ebdFilterEndOffs);
    }

    public readonly bool InHndRegionILRange(BasicBlock pBlk)
    {
        // BBF_INTERNAL blocks may not have a valid bbCodeOffs. This function
        // should only be used before any BBF_INTERNAL blocks have been added.

        assert(!pBlk.HasFlag(BBF_INTERNAL));
        return Compiler.jitIsBetween(pBlk.bbCodeOffs, _ebdHndBegOffset, _ebdHndEndOffset);
    }

    public readonly bool InTryRegionBBRange(BasicBlock pBlk) => InBBRange(pBlk, ebdTryBeg, ebdTryLast.Next);

    public readonly bool InFilterRegionBBRange(BasicBlock pBlk) => HasFilter && InBBRange(pBlk, ebdFilter, ebdHndBeg);

    public readonly bool InHndRegionBBRange(BasicBlock pBlk) => InBBRange(pBlk, ebdHndBeg, ebdHndLast.Next);

    // Return the region index of the most nested EH region that encloses this region, or NO_ENCLOSING_INDEX
    // if this region is directly in the main function body. Set '*inTryRegion' to 'true' if this region is
    // most nested within a 'try' region, or 'false' if this region is most nested within a handler. (Note
    // that filters cannot contain nested EH regions.)
    public readonly int ebdGetEnclosingRegionIndex(out bool inTryRegion)
    {
        if (ebdEnclosingTryIndex == NO_ENCLOSING_INDEX)
        {
            if (ebdEnclosingHndIndex == NO_ENCLOSING_INDEX)
            {
                Unsafe.SkipInit(out inTryRegion);
                return NO_ENCLOSING_INDEX;
            }
            else
            {
                inTryRegion = false;
                return ebdEnclosingHndIndex;
            }
        }
        else if (ebdEnclosingHndIndex == NO_ENCLOSING_INDEX)
        {
            inTryRegion = true;
            return ebdEnclosingTryIndex;
        }
        else
        {
            assert(ebdEnclosingTryIndex != ebdEnclosingHndIndex);

            if (ebdEnclosingTryIndex < ebdEnclosingHndIndex)
            {
                inTryRegion = true;
                return ebdEnclosingTryIndex;
            }
            else
            {
                inTryRegion = false;
                return ebdEnclosingHndIndex;
            }
        }
    }

    public readonly bool ebdIsSameTry(Compiler compiler, ushort t2)
    {
        ref var h2 = ref compiler.ehGetDsc(t2);
        return ebdIsSameTry(this, h2);
    }

    public readonly bool ebdIsSameTry(BasicBlock ebdTryBeg, BasicBlock ebdTryLast)
        => ((this.ebdTryBeg == ebdTryBeg) && (this.ebdTryLast == ebdTryLast));

#if DEBUG
    public readonly void DispEntry(ushort XTnum)
    {
        jitprintf($" {ebdID:D2}     {XTnum:D2}  ::");

        if (ebdEnclosingTryIndex == NO_ENCLOSING_INDEX)
        {
            jitprintf("      ");
        }
        else
        {
            jitprintf($"  {ebdEnclosingTryIndex:D2}  ");
        }

        if (ebdEnclosingHndIndex == NO_ENCLOSING_INDEX)
        {
            jitprintf("      ");
        }
        else
        {
            jitprintf($"  {ebdEnclosingHndIndex:D2}  ");
        }

        //////////////
        ////////////// Protected (try) region
        //////////////

        jitprintf($"- Try at {FMT_BB(ebdTryBeg.bbNum)}..{FMT_BB(ebdTryLast.bbNum)}");

        /* ( brace matching editor workaround to compensate for the following line */
        jitprintf($" [{_ebdTryBegOffset:X3}..{_ebdTryEndOffset:X3}), ");

        //////////////
        ////////////// Filter region
        //////////////

        if (HasFilter)
        {
            /* ( brace matching editor workaround to compensate for the following line */
            jitprintf($"Filter at {FMT_BB(ebdFilter.bbNum)}..{FMT_BB(BBFilterLast.bbNum)} [{_ebdFilterBegOffset:X3}..{_ebdHndBegOffset:X3}), ");
        }

        //////////////
        ////////////// Handler region
        //////////////

        if (ebdHndBeg.CatchType is BBCT_FINALLY)
        {
            jitprintf("Finally");
        }
        else if (ebdHndBeg.CatchType is BBCT_FAULT)
        {
            jitprintf("Fault  ");
        }
        else
        {
            jitprintf("Handler");
        }

        jitprintf($" at {FMT_BB(ebdHndBeg.bbNum)}..{FMT_BB(ebdHndLast.bbNum)}");
        jitprintf($" [{_ebdHndBegOffset:X3}..{_ebdHndEndOffset:X3})");
        jitprintf("\n");
    }
#endif
}
