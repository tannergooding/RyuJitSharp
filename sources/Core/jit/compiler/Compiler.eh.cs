// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private EHNodeDsc[] ehnNodes = [];

    public EHNodeDsc ehnNode(int id)
    {
        ref var node = ref ehnNodes[id];
        node ??= new EHNodeDsc();
        return node;
    }

    public int ehnNextId;
    public EHNodeDsc? ehnTree;

    public bool ehTableFinalized;

    /// <summary>Give two blocks, return the inner-most enclosing try region that contains both of them.</summary>
    /// <param name="bbOne"></param>
    /// <param name="bbTwo"></param>
    /// <returns>0 if it does not find any try region (which means the inner-most region is the method itself).</returns>
    public ushort bbFindInnermostCommonTryRegion(BasicBlock bbOne, BasicBlock bbTwo)
    {
        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            if (bbInTryRegions(XTnum, bbOne) && bbInTryRegions(XTnum, bbTwo))
            {
                noway_assert(XTnum < MAX_XCPTN_INDEX);
                return (ushort)(XTnum + 1); // Return the tryIndex
            }
        }
        return 0;
    }

    /// <summary>Given a one-biased region index (which may be 0, indicating method region) and a block, return one-biased index for the inner-most enclosing try region that contains the block and the region.</summary>
    /// <param name="tryIndex"></param>
    /// <param name="bbTwo"></param>
    /// <returns>0 if it does not find any try region (which means the inner-most region is the method itself).</returns>
    public ushort bbFindInnermostCommonTryRegion(ushort tryIndex, BasicBlock bbTwo)
    {
        assert(tryIndex <= compHndBBtabCount);

        if (tryIndex is 0)
        {
            return 0;
        }

        for (var XTnum = (ushort)(tryIndex - 1); XTnum < compHndBBtabCount; XTnum++)
        {
            if (bbInTryRegions(XTnum, bbTwo))
            {
                noway_assert(XTnum < MAX_XCPTN_INDEX);
                return (ushort)(XTnum + 1); // Return the tryIndex
            }
        }
        return 0;
    }

    /// <summary>Given a try region, find the innermost handler region that contains it.</summary>
    /// <param name="tryIndex"></param>
    /// <returns></returns>
    /// <remarks>NOTE: tryIndex is 1-based (0 means no handler).</remarks>
    public ushort bbFindInnermostHandlerRegionContainingTryRegion(ushort tryIndex)
    {
        if (tryIndex > 0)
        {
            // tryIndex is 1 based, our interesting clauses start from clause compHndBBtab[tryIndex]
            var blk = ehGetDsc((ushort)(tryIndex - 1)).ebdTryBeg;

            for (var XTnum = tryIndex; XTnum < compHndBBtabCount; XTnum++)
            {
                ref var ehDsc = ref ehGetDsc(XTnum);

                if (bbInHandlerRegions(XTnum, blk))
                {
                    noway_assert(XTnum < MAX_XCPTN_INDEX);
                    return (ushort)(XTnum + 1); // Return the handlerIndex
                }
            }
        }
        return 0;
    }

    /// <summary>Given a handler region, find the innermost try region that contains it.</summary>
    /// <param name="handlerIndex"></param>
    /// <returns></returns>
    /// <remarks>NOTE: handlerIndex is 1-based (0 means no handler).</remarks>
    public ushort bbFindInnermostTryRegionContainingHandlerRegion(ushort handlerIndex)
    {
        if (handlerIndex > 0)
        {
            // handlerIndex is 1 based, therefore our interesting clauses start from clause compHndBBtab[handlerIndex]
            var blk = ehGetDsc((ushort)(handlerIndex - 1)).ebdHndBeg;

            for (var XTnum = handlerIndex; XTnum < compHndBBtabCount; XTnum++)
            {
                ref var ehDsc = ref ehGetDsc(XTnum);

                if (bbInTryRegions(XTnum, blk))
                {
                    noway_assert(XTnum < MAX_XCPTN_INDEX);
                    return (ushort)(XTnum + 1); // Return the tryIndex
                }
            }
        }
        return 0;
    }

    /// <summary>Check if this block is part of a catch handler.</summary>
    /// <param name="blk">The block</param>
    /// <returns>True if the block is part of a catch handler clause. Otherwise false.</returns>
    public bool bbInCatchHandlerBBRange(BasicBlock blk)
    {
        ref var HBtab = ref ehGetBlockHndDsc(blk);

        if (Unsafe.IsNullRef(in HBtab))
        {
            return false;
        }
        return HBtab.HasCatchHandler && HBtab.InHndRegionBBRange(blk);
    }

    public bool bbInCatchHandlerILRange(BasicBlock blk)
    {
        ref var HBtab = ref ehGetBlockHndDsc(blk);

        if (Unsafe.IsNullRef(in HBtab))
        {
            return false;
        }
        return HBtab.HasCatchHandler && HBtab.InHndRegionILRange(blk);
    }

    /// <summary>Given a hndBlk, see if it is in one of tryBlk's catch handler regions.</summary>
    /// <param name="tryBlk"></param>
    /// <param name="hndBlk"></param>
    /// <returns></returns>
    public bool bbInCatchHandlerRegions(BasicBlock tryBlk, BasicBlock hndBlk)
    {
        // Since we create one EHblkDsc for each "catch" of a "try", we might end up
        // with multiple EHblkDsc's that have the same ebdTryBeg and ebdTryLast, but different
        // ebdHndBeg and ebdHndLast. Unfortunately getTryIndex() only returns the index of the first EHblkDsc.
        // 
        // E.g. The following example shows that BB02 has a catch in BB03 and another catch in BB04.
        // 
        //     index  nest, enclosing
        //       0  ::   0,    1 - Try at BB01..BB02 [000..008], Handler at BB03       [009..016]
        //       1  ::   0,      - Try at BB01..BB02 [000..008], Handler at BB04       [017..022]
        // 
        // This function will return true for
        //     bbInCatchHandlerRegions(BB02, BB03) and bbInCatchHandlerRegions(BB02, BB04)

        assert(tryBlk.hasTryIndex);

        if (!hndBlk.hasHndIndex)
        {
            return false;
        }

        var XTnum = tryBlk.TryIndex;
        ref var firstEHblkDsc = ref ehGetDsc(XTnum);
        ref var ehDsc = ref firstEHblkDsc;

        // Rather than searching the whole list, take advantage of our sorting.
        // We will only match against blocks with the same try body (mutually
        // protect regions).  Because of our sort ordering, such regions will
        // always be immediately adjacent, any nested regions will be before the
        // first of the set, and any outer regions will be after the last.
        // Also siblings will be before or after according to their location,
        // but never in between;

        while (XTnum > 0)
        {
            assert(EHblkDsc.ebdIsSameTry(firstEHblkDsc, ehDsc));
            ref var prevEhDsc = ref ehGetDsc((ushort)(XTnum - 1));

            // Stop when the previous region is not mutually protect
            if (!EHblkDsc.ebdIsSameTry(firstEHblkDsc, prevEhDsc))
            {
                break;
            }

            XTnum--;
            ehDsc = ref prevEhDsc;
        }

        // XTnum and ehDsc are now referring to the first region in the set of
        // mutually protect regions.
        assert(EHblkDsc.ebdIsSameTry(firstEHblkDsc, ehDsc));
        assert(Unsafe.AreSame(in ehDsc, in MemoryMarshal.GetArrayDataReference(compHndBBtab)) || !EHblkDsc.ebdIsSameTry(firstEHblkDsc, in ehGetDsc((ushort)(XTnum - 1))));

        do
        {
            if (ehDsc.HasCatchHandler && bbInHandlerRegions(XTnum, hndBlk))
            {
                return true;
            }
            ehDsc = ref ehGetDsc(++XTnum);
        }
        while ((XTnum < compHndBBtabCount) && EHblkDsc.ebdIsSameTry(firstEHblkDsc, ehDsc));

        return false;
    }

    /// <summary>Check to see if an exception raised in the given block could be handled by the given region (possibly after inner regions).</summary>
    /// <param name="regionIndex">Check if this region can handle exceptions from 'blk'</param>
    /// <param name="blk">Consider exceptions raised from this block</param>
    /// <returns>true if The region with index 'regionIndex' can handle exceptions from 'blk'; otherwise, false</returns>
    /// <remarks>For this check, a funclet is considered to be in the region it was extracted from.</remarks>
    public bool bbInExnFlowRegions(ushort regionIndex, BasicBlock blk)
    {
        assert(regionIndex is >= 0 and < EHblkDsc.NO_ENCLOSING_INDEX);

        ref var ExnFlowRegion = ref ehGetBlockExnFlowDsc(blk);
        var tryIndex = Unsafe.IsNullRef(in ExnFlowRegion) ? EHblkDsc.NO_ENCLOSING_INDEX : ehGetIndex(ExnFlowRegion);

        // Loop outward until we find an enclosing try that is the same as the one
        // we are looking for or an outer/later one
        while (tryIndex < regionIndex)
        {
            tryIndex = ehGetEnclosingTryIndex(tryIndex);
        }

        // Now we have the index of 2 try bodies, either they match or not!
        return (tryIndex == regionIndex);
    }

    /// <summary>Check if this block is part of a filter.</summary>
    /// <param name="blk">The block</param>
    /// <returns>True if the block is part of a filter clause. Otherwise false.</returns>
    public bool bbInFilterBBRange(BasicBlock blk)
    {
        ref var HBtab = ref ehGetBlockHndDsc(blk);

        if (Unsafe.IsNullRef(in HBtab))
        {
            return false;
        }
        return HBtab.InFilterRegionBBRange(blk);
    }

    public bool bbInFilterILRange(BasicBlock blk)
    {
        ref var HBtab = ref ehGetBlockHndDsc(blk);

        if (Unsafe.IsNullRef(in HBtab))
        {
            return false;
        }
        return HBtab.InFilterRegionILRange(blk);
    }

    /// <summary>Given a block, check to see if it is in the handler block of the EH descriptor.</summary>
    /// <param name="regionIndex"></param>
    /// <param name="blk"></param>
    /// <returns></returns>
    /// <remarks>For this check, a funclet is considered to be in the region it was extracted from.</remarks>
    public bool bbInHandlerRegions(ushort regionIndex, BasicBlock blk)
    {
        assert(regionIndex is >= 0 and < EHblkDsc.NO_ENCLOSING_INDEX);
        var hndIndex = blk.hasHndIndex ? blk.HndIndex : EHblkDsc.NO_ENCLOSING_INDEX;

        // We can't use the same simple trick here because there is no required ordering
        // of handlers (which also have no required ordering with respect to their try
        // bodies).
        while (hndIndex < EHblkDsc.NO_ENCLOSING_INDEX && hndIndex != regionIndex)
        {
            hndIndex = ehGetEnclosingHndIndex(hndIndex);
        }

        // Now we have the index of 2 try bodies, either they match or not!
        return (hndIndex == regionIndex);
    }

    /// <summary>Given a block and a try region index, check to see if the block is within the try body.</summary>
    /// <param name="regionIndex"></param>
    /// <param name="blk"></param>
    /// <returns></returns>
    /// <remarks>For this check, a funclet is considered to be in the region it was extracted from.</remarks>
    public bool bbInTryRegions(ushort regionIndex, BasicBlock blk)
    {
        assert(regionIndex is >= 0 and < EHblkDsc.NO_ENCLOSING_INDEX);
        var tryIndex = blk.hasTryIndex ? blk.TryIndex : EHblkDsc.NO_ENCLOSING_INDEX;

        // Loop outward until we find an enclosing try that is the same as the one
        // we are looking for or an outer/later one
        while (tryIndex < regionIndex)
        {
            tryIndex = ehGetEnclosingTryIndex(tryIndex);
        }

        // Now we have the index of 2 try bodies, either they match or not!
        return (tryIndex == regionIndex);
    }

    // returns true if this block is the start of any try region.
    // This is computed by examining the current values in the
    // EH table rather than just looking at the block's bbFlags.
    //
    // Note that a block is the beginning of any try region if it is the beginning of the
    // most nested try region it is a member of. Thus, we only need to check the EH
    // table entry related to the try index stored on the block.
    public bool bbIsTryBeg(BasicBlock block)
    {
        ref var ehDsc = ref ehGetBlockTryDsc(block);
        return !Unsafe.IsNullRef(in ehDsc) && (block == ehDsc.ebdTryBeg);
    }

    /// <summary>Returns true if value is between [start..end).</summary>
    /// <param name="value"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static bool jitIsBetween(int value, int start, int end)
        => (start <= value) && (value < end);

    /// <summary>Returns true if value is between [start..end].</summary>
    /// <param name="value"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static bool jitIsBetweenInclusive(int value, int start, int end)
        => (start <= value) && (value <= end);

#if DEBUG
    public void dispIncomingEHClause(ushort num, in CORINFO_EH_CLAUSE clause)
    {
        jitprintf($"EH clause #{num}:\n");
        jitprintf($"  Flags:         0x{clause.Flags:x}");

        // Note: the flags field is kind of weird. It should be compared for equality to determine the type of clause, even though it looks like a bitfield.
        // In particular, CORINFO_EH_CLAUSE_NONE is zero, so you can't use "&" to check it.
        const CORINFO_EH_CLAUSE_FLAGS CORINFO_EH_CLAUSE_TYPE_MASK = (CORINFO_EH_CLAUSE_FLAGS)(0x7);

        switch (clause.Flags & CORINFO_EH_CLAUSE_TYPE_MASK)
        {
            case CORINFO_EH_CLAUSE_NONE:
            {
                jitprintf(" (catch)");
                break;
            }

            case CORINFO_EH_CLAUSE_FILTER:
            {
                jitprintf(" (filter)");
                break;
            }

            case CORINFO_EH_CLAUSE_FINALLY:
            {
                jitprintf(" (finally)");
                break;
            }

            case CORINFO_EH_CLAUSE_FAULT:
            {
                jitprintf(" (fault)");
                break;
            }

            default:
            {
                jitprintf($" (UNKNOWN type {clause.Flags & CORINFO_EH_CLAUSE_TYPE_MASK}!)");
                break;
            }
        }

        if ((clause.Flags & ~CORINFO_EH_CLAUSE_TYPE_MASK) != 0)
        {
            jitprintf($" (extra unknown bits: 0x{clause.Flags & ~CORINFO_EH_CLAUSE_TYPE_MASK:x})");
        }
        jitprintf("\n");

        jitprintf($"  TryOffset:     0x{clause.TryOffset:x}\n");
        jitprintf($"  TryLength:     0x{clause.TryLength:x}\n");
        jitprintf($"  HandlerOffset: 0x{clause.HandlerOffset:x}\n");
        jitprintf($"  HandlerLength: 0x{clause.HandlerLength:x}\n");

        if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
        {
            jitprintf($"  FilterOffset:  0x{clause.FilterOffset:x}\n");
        }
        else
        {
            jitprintf($"  ClassToken:    0x{clause.ClassToken:x}\n");
        }
    }
#endif

    public bool ehBlockHasExnFlowDsc(BasicBlock block)
    {
        if (block.hasTryIndex)
        {
            return true;
        }

        ref var hndDesc = ref ehGetBlockHndDsc(block);
        return (!Unsafe.IsNullRef(in hndDesc) && hndDesc.InFilterRegionBBRange(block) && (hndDesc.ebdEnclosingTryIndex != EHblkDsc.NO_ENCLOSING_INDEX));
    }

    /// <summary>Get the EH descriptor for the most nested region (if any) that may handle exceptions raised in the given block</summary>
    /// <param name="block">Consider exceptions raised from this block</param>
    /// <returns>A reference to the given block's exceptions propagate to caller or a null ref if this region is the innermost handler for exceptions raised in the given block</returns>
    public ref EHblkDsc ehGetBlockExnFlowDsc(BasicBlock block)
    {
        ref var hndDesc = ref ehGetBlockHndDsc(block);

        if ((!Unsafe.IsNullRef(in hndDesc)) && hndDesc.InFilterRegionBBRange(block))
        {
            // If an exception is thrown in a filter (or escapes a callee in a filter),
            // or if exception_continue_search (0/false) is returned at
            // the end of a filter, the (original) exception is propagated to
            // the next outer handler.  The "next outer handler" is the handler
            // of the try region enclosing the try that the filter protects.
            // This may not be the same as the try region enclosing the filter,
            // e.g. in cases like this:
            //    try {
            //      ...
            //    } filter (filter-part) {
            //      handler-part
            //    } catch {  (or finally/fault/filter)
            // which is represented as two EHblkDscs with the same try range,
            // the inner protected by a filter and the outer protected by the
            // other handler; exceptions in the filter-part propagate to the
            // other handler, even though the other handler's try region does not
            // enclose the filter.

            var outerIndex = hndDesc.ebdEnclosingTryIndex;

            if (outerIndex == EHblkDsc.NO_ENCLOSING_INDEX)
            {
                assert(!block.hasTryIndex);
                return ref Unsafe.NullRef<EHblkDsc>();
            }
            return ref ehGetDsc(outerIndex);
        }

        return ref ehGetBlockTryDsc(block);
    }

    /// <summary>Return the EH descriptor for the most nested filter or handler region this BasicBlock is a member of (or null if this block is not in a filter or handler region).</summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public ref EHblkDsc ehGetBlockHndDsc(BasicBlock block)
        => ref (block.hasHndIndex ? ref ehGetDsc(block.HndIndex) : ref Unsafe.NullRef<EHblkDsc>());

    /// <summary>Return the EH descriptor for the most nested 'try' region this BasicBlock is a member of (or null if this block is not in a 'try' region).</summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public ref EHblkDsc ehGetBlockTryDsc(BasicBlock block)
        => ref (block.hasTryIndex ? ref ehGetDsc(block.TryIndex) : ref Unsafe.NullRef<EHblkDsc>());

    /// <summary>Return the EH descriptor for the given region index.</summary>
    /// <param name="regionIndex"></param>
    /// <returns></returns>
    public ref EHblkDsc ehGetDsc(ushort regionIndex)
    {
        assert(regionIndex < compHndBBtabCount);
        return ref compHndBBtab[regionIndex];
    }

    /// <summary>Return the EH descriptor index of the enclosing handler, for the given region index.</summary>
    /// <param name="regionIndex"></param>
    /// <returns></returns>
    public ushort ehGetEnclosingHndIndex(ushort regionIndex)
    {
        return ehGetDsc(regionIndex).ebdEnclosingHndIndex;
    }

    /// <summary>Return the index of the most nested enclosing region for a particular EH region.</summary>
    /// <param name="regionIndex"></param>
    /// <param name="inTryRegion"></param>
    /// <returns>NO_ENCLOSING_INDEX if there is no enclosing region. If the returned index is not NO_ENCLOSING_INDEX, then '*inTryRegion' is set to 'true' if the enclosing region is a 'try', or 'false' if the enclosing region is a handler. (It can never be a filter.)</returns>
    public ushort ehGetEnclosingRegionIndex(ushort regionIndex, out bool inTryRegion)
    {
        assert(regionIndex is not EHblkDsc.NO_ENCLOSING_INDEX);
        ref var ehDsc = ref ehGetDsc(regionIndex);
        return ehDsc.ebdGetEnclosingRegionIndex(out inTryRegion);
    }

    /// <summary>Return the EH descriptor index of the enclosing try, for the given region index.</summary>
    /// <param name="regionIndex"></param>
    /// <returns></returns>
    public ushort ehGetEnclosingTryIndex(ushort regionIndex)
    {
        return ehGetDsc(regionIndex).ebdEnclosingTryIndex;
    }

    /// <summary>Return the EH index given a region descriptor</summary>
    /// <param name="ehDsc"></param>
    /// <returns></returns>
    public ushort ehGetIndex(in EHblkDsc ehDsc)
    {
        assert(Unsafe.IsAddressLessThanOrEqualTo(in compHndBBtab[0], in ehDsc) && Unsafe.IsAddressLessThan(in ehDsc, in compHndBBtab[compHndBBtabCount]));
        var index = (ushort)(Unsafe.ByteOffset(in compHndBBtab[0], in ehDsc) / Unsafe.SizeOf<EHblkDsc>());

        assert(Unsafe.AreSame(in ehDsc, in compHndBBtab[index]));
        return index;
    }

    /// <summary>Return the region index of the most nested EH region this block is in.</summary>
    /// <param name="block">the BasicBlock we want the region index for.</param>
    /// <param name="inTryRegion">an out parameter. As described above.</param>
    /// <returns>in the range [0..compHndBBtabCount]. It is same scale as bbTryIndex/bbHndIndex: 0 means main method, N is used as an index to compHndBBtab[N - 1]. If we don't return 0, then *inTryRegion indicates whether the most nested region for the block is a 'try' clause or filter/handler clause. For 0 return, *inTryRegion is set to true.</returns>
    public ushort ehGetMostNestedRegionIndex(BasicBlock block, out bool inTryRegion)
    {
        assert(block is not null);

        ushort mostNestedRegion;

        if (block.bbHndIndex == 0)
        {
            mostNestedRegion = block.bbTryIndex;
            inTryRegion = true;
        }
        else if (block.bbTryIndex == 0)
        {
            mostNestedRegion = block.bbHndIndex;
            inTryRegion = false;
        }
        else
        {
            if (block.bbTryIndex < block.bbHndIndex)
            {
                mostNestedRegion = block.bbTryIndex;
                inTryRegion = true;
            }
            else
            {
                // A block can't be both in the 'try' and 'handler' region of the same EH region
                assert(block.bbTryIndex != block.bbHndIndex);

                mostNestedRegion = block.bbHndIndex;
                inTryRegion = false;
            }
        }

        assert(mostNestedRegion <= compHndBBtabCount);
        return mostNestedRegion;
    }

    public ref EHblkDsc ehInitHndBlockRange(BasicBlock blk, out BasicBlock? hndBeg, out BasicBlock? hndLast, out bool inFilter)
    {
        ref var hndTab = ref ehGetBlockHndDsc(blk);

        if (!Unsafe.IsNullRef(in hndTab))
        {
            if (hndTab.InFilterRegionBBRange(blk))
            {
                hndBeg = hndTab.ebdFilter;
                hndLast = hndTab.BBFilterLast;
                inFilter = true;
            }
            else
            {
                hndBeg = hndTab.ebdHndBeg;
                hndLast = hndTab.ebdHndLast;
                inFilter = false;
            }
        }
        else
        {
            hndBeg = null;
            hndLast = null;
            inFilter = false;
        }
        return ref hndTab;
    }

    public ref EHblkDsc ehInitHndRange(BasicBlock blk, out IL_OFFSET hndBeg, out IL_OFFSET hndEnd, out bool inFilter)
    {
        ref var hndTab = ref ehGetBlockHndDsc(blk);

        if (!Unsafe.IsNullRef(in hndTab))
        {
            if (hndTab.InFilterRegionILRange(blk))
            {
                hndBeg = hndTab.ebdFilterBegOffs;
                hndEnd = hndTab.ebdFilterEndOffs;
                inFilter = true;
            }
            else
            {
                hndBeg = hndTab.ebdHndBegOffs;
                hndEnd = hndTab.ebdHndEndOffs;
                inFilter = false;
            }
        }
        else
        {
            hndBeg = 0;
            hndEnd = info.compILCodeSize;
            inFilter = false;
        }
        return ref hndTab;
    }

    public ref EHblkDsc ehInitTryBlockRange(BasicBlock blk, out BasicBlock? tryBeg, out BasicBlock? tryLast)
    {
        ref var tryTab = ref ehGetBlockTryDsc(blk);

        if (!Unsafe.IsNullRef(in tryTab))
        {
            tryBeg = tryTab.ebdTryBeg;
            tryLast = tryTab.ebdTryLast;
        }
        else
        {
            tryBeg = null;
            tryLast = null;
        }
        return ref tryTab;
    }

    public ref EHblkDsc ehInitTryRange(BasicBlock blk, out IL_OFFSET tryBeg, out IL_OFFSET tryEnd)
    {
        ref var tryTab = ref ehGetBlockTryDsc(blk);

        if (!Unsafe.IsNullRef(in tryTab))
        {
            tryBeg = tryTab.ebdTryBegOffs;
            tryEnd = tryTab.ebdTryEndOffs;
        }
        else
        {
            tryBeg = 0;
            tryEnd = info.compILCodeSize;
        }
        return ref tryTab;
    }

    /// <summary>The argument 'block' has been deleted. Update the EH table so 'block' is no longer listed as a 'last' block.</summary>
    /// <param name="block"></param>
    /// <remarks>You can't delete a 'begin' block this way.</remarks>
    public void ehUpdateForDeletedBlock(BasicBlock block)
    {
        assert(block.HasFlag(BBF_REMOVED));

        if (!block.hasTryIndex && !block.hasHndIndex)
        {
            // The block is not part of any EH region, so there is nothing to do.
            return;
        }

        var bPrev = block.Prev;
        assert(bPrev is not null);

        ehUpdateLastBlocks(block, bPrev);
    }

    /// <summary>The 'last' block of one or more EH regions might have changed. Update the EH table.</summary>
    /// <param name="oldLast">Search for this block as the 'last' block of one or more EH regions.</param>
    /// <param name="newLast">If 'oldLast' is found to be the 'last' block of an EH region, replace it by 'newLast'.</param>
    /// <remarks>
    ///   <para>This can happen if the EH region shrinks, where one or more blocks have been removed from the region.It can happen if the EH region grows, where one or more blocks have been added at the end of the region.</para>
    ///   <para>We might like to verify the handler table integrity after doing this update, but we can't because this might just be one step by the caller in a transformation back to a legal state.</para>
    /// </remarks>
    public void ehUpdateLastBlocks(BasicBlock oldLast, BasicBlock newLast)
    {
        foreach (ref var HBtab in new EHClauses(this))
        {
            if (HBtab.ebdTryLast == oldLast)
            {
                fgSetTryEnd(ref HBtab, newLast);
            }

            if (HBtab.ebdHndLast == oldLast)
            {
                fgSetHndEnd(ref HBtab, newLast);
            }
        }
    }

    // ToEHHandlerType: Convert a CORINFO_EH_CLAUSE_FLAGS value obtained from the VM in the EH clause structure
    // to the internal EHHandlerType type.
    public EHHandlerType ToEHHandlerType(CORINFO_EH_CLAUSE_FLAGS flags)
    {
        if ((flags & CORINFO_EH_CLAUSE_FAULT) != 0)
        {
            return EH_HANDLER_FAULT;
        }
        else if ((flags & CORINFO_EH_CLAUSE_FINALLY) != 0)
        {
            return EH_HANDLER_FINALLY;
        }
        else if ((flags & CORINFO_EH_CLAUSE_FILTER) != 0)
        {
            return EH_HANDLER_FILTER;
        }
        else
        {
            // If it's none of the others, assume it is a try/catch.
            // The VM (and apparently VC) stick in extra bits in the flags field. We ignore any flags we don't know about.
            return EH_HANDLER_CATCH;
        }
    }

    // Checks the following two conditions:
    // 1) If block A contains block B, A should also contain B's try/filter/handler.
    // 2) A block cannot contain its related try/filter/handler.
    // Both these conditions are checked by making sure that all the blocks for an
    // exception clause are at the same level.
    // The algorithm is: for each exception clause, determine the first block and
    // search through the next links for its corresponding try/handler/filter as the
    // case may be. If not found, then fail.
    public unsafe void verCheckNestingLevel(int initRootId)
    {
        var ehnNodeId = initRootId;

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            var p1 = ehnNode(ehnNodeId++);
            var p2 = ehnNode(ehnNodeId++);

            // we are relying on the fact that ehn nodes are allocated sequentially.
            noway_assert(p1.ehnHandlerNode == p2);
            noway_assert(p2.ehnTryNode == p1);

            // arrange p1 and p2 in sequential order
            if (p1.ehnStartOffset == p2.ehnStartOffset)
            {
                BADCODE("shared exception handler");
            }

            if (p1.ehnStartOffset > p2.ehnStartOffset)
            {
                (p1, p2) = (p2, p1);
            }

            var temp = p1.ehnNext;
            var numSiblings = 0;
            var search = p2;

            if (search.ehnEquivalent is not null)
            {
                search = search.ehnEquivalent;
            }

            do
            {
                if (temp == search)
                {
                    numSiblings++;
                    break;
                }
                if (temp is not null)
                {
                    temp = temp.ehnNext;
                }
            } while (temp is not null);

            CORINFO_EH_CLAUSE clause;
            info.compCompHnd->getEHinfo(info.compMethodHnd, XTnum, &clause);

            if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
            {
                var p3 = ehnNode(ehnNodeId++);

                noway_assert(p3.ehnTryNode == p1 || p3.ehnTryNode == p2);
                noway_assert(p1.ehnFilterNode == p3 || p2.ehnFilterNode == p3);

                if (p3.ehnStartOffset < p1.ehnStartOffset)
                {
                    temp   = p3;
                    search = p1;
                }
                else if (p3.ehnStartOffset < p2.ehnStartOffset)
                {
                    temp   = p1;
                    search = p3;
                }
                else
                {
                    temp   = p2;
                    search = p3;
                }

                if (search.ehnEquivalent is not null)
                {
                    search = search.ehnEquivalent;
                }

                do
                {
                    if (temp == search)
                    {
                        numSiblings++;
                        break;
                    }
                    temp = temp.ehnNext;
                } while (temp is not null);
            }
            else
            {
                numSiblings++;
            }

            if (numSiblings != 2)
            {
                BADCODE("Outer block does not contain all code in inner handler");
            }
        }
    }

    // The following code checks the following rules for the EH table:
    //  1. Overlapping of try blocks not allowed.
    //  2. Handler blocks cannot be shared between different try blocks.
    //  3. Try blocks with Finally or Fault blocks cannot have other handlers.
    //  4. If block A contains block B, A should also contain B's try/filter/handler.
    //  5. A block cannot contain it's related try/filter/handler.
    //  6. Nested block must appear before containing block
    public void verInitEHTree(int numEHClauses)
    {
        ehnNodes = new EHNodeDsc[numEHClauses * 3];
        ehnNextId = 0;
        ehnTree = null;
    }

    /// <summary>Inserts the try, handler and filter (optional) clause information in a tree structure in order to catch incorrect eh formatting (e.g. illegal overlaps, incorrect order)</summary>
    /// <param name="clause"></param>
    /// <param name="handlerTab"></param>
    public void verInsertEhNode(in CORINFO_EH_CLAUSE clause, ref EHblkDsc handlerTab)
    {
        var tryNode = ehnNode(ehnNextId++);
        var handlerNode = ehnNode(ehnNextId++);
        var filterNode = null as EHNodeDsc;

        tryNode.ehnSetTryNodeType();
        tryNode.ehnStartOffset = clause.TryOffset;
        tryNode.ehnEndOffset = clause.TryOffset + clause.TryLength - 1;
        tryNode.ehnHandlerNode = handlerNode;

        if ((clause.Flags & CORINFO_EH_CLAUSE_FINALLY) != 0)
        {
            handlerNode.ehnSetFinallyNodeType();
        }
        else if ((clause.Flags & CORINFO_EH_CLAUSE_FAULT) != 0)
        {
            handlerNode.ehnSetFaultNodeType();
        }
        else
        {
            handlerNode.ehnSetHandlerNodeType();
        }

        handlerNode.ehnStartOffset = clause.HandlerOffset;
        handlerNode.ehnEndOffset = clause.HandlerOffset + clause.HandlerLength - 1;
        handlerNode.ehnTryNode = tryNode;

        if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
        {
            filterNode = ehnNode(ehnNextId++);
            filterNode.ehnStartOffset = clause.FilterOffset;

            var blk = handlerTab.BBFilterLast;
            assert(blk is not null);
            filterNode.ehnEndOffset = blk.bbCodeOffsEnd - 1;

            noway_assert(filterNode.ehnEndOffset != 0);
            filterNode.ehnSetFilterNodeType();
            filterNode.ehnTryNode = tryNode;
            tryNode.ehnFilterNode = filterNode;
        }

        verInsertEhNodeInTree(ref ehnTree, tryNode);
        verInsertEhNodeInTree(ref ehnTree, handlerNode);

        if (filterNode is not null)
        {
            verInsertEhNodeInTree(ref ehnTree, filterNode);
        }
    }

    public void verInsertEhNodeInTree(ref EHNodeDsc? root, EHNodeDsc node)
    {
        // The root node could be changed by this method.
        //
        // node is inserted to
        //   (a) right       of root (root.right       <-- node)
        //   (b) left        of root (node.right       <-- root; node becomes root)
        //   (c) child       of root (root.child       <-- node)
        //   (d) parent      of root (node.child       <-- root; node becomes root)
        //   (e) equivalent  of root (root.equivalent  <-- node)
        //
        // such that siblings are ordered from left to right
        // child parent relationship and equivalence relationship are not violated
        //
        //
        //  Here is a list of all possible cases
        //
        //  Case 1 2 3 4 5 6 7 8 9 10 11 12 13
        //
        //       | | | | |
        //       | | | | |
        //  .......|.|.|.|..................... [ root start ] .....
        //  |        | | | |             |  |
        //  |        | | | |             |  |
        // r|        | | | |          |  |  |
        // o|          | | |          |     |
        // o|          | | |          |     |
        // t|          | | |          |     |
        //  |          | | | |     |  |     |
        //  |          | | | |     |        |
        //  |..........|.|.|.|.....|........|.. [ root end ] ........
        //               | | | |
        //               | | | | |
        //               | | | | |
        //
        //      |<-- - - - n o d e - - - -->|
        //
        //
        // Case Operation
        // --------------
        //  1    (b)
        //  2    Error
        //  3    Error
        //  4    (d)
        //  5    (d)
        //  6    (d)
        //  7    Error
        //  8    Error
        //  9    (a)
        //  10   (c)
        //  11   (c)
        //  12   (c)
        //  13   (e)

        var nStart = node.ehnStartOffset;
        var nEnd = node.ehnEndOffset;

        if (nStart > nEnd)
        {
            BADCODE("start offset greater or equal to end offset");
        }

        node.ehnNext = null;
        node.ehnChild = null;
        node.ehnEquivalent = null;

        while (true)
        {
            if (root is null)
            {
                root = node;
                break;
            }

            var rStart = root.ehnStartOffset;
            var rEnd = root.ehnEndOffset;

            if (nStart < rStart)
            {
                // Case 1
                if (nEnd < rStart)
                {
                    // Left sibling
                    node.ehnNext = root;
                    root = node;
                    return;
                }

                // Case 2, 3
                if (nEnd < rEnd)
                {
                    // [Error]
                    BADCODE("Overlapping try regions");
                }

                // Case 4, 5: [Parent]
                verInsertEhNodeParent(ref root, node);
                return;
            }

            // Cases 6-13 (nStart >= rStart)

            if (nEnd > rEnd)
            {
                // Case 9
                if (nStart > rEnd)
                {
                    // [RightSibling]
                    // Recurse with Root.Sibling as the new root

                    root = ref root.ehnNext;
                    continue;
                }

                // Case 6
                if (nStart == rStart)
                {
                    // [Parent]
                    if (node.ehnIsTryBlock || root.ehnIsTryBlock)
                    {
                        verInsertEhNodeParent(ref root, node);
                        return;
                    }

                    // non try blocks are not allowed to start at the same offset
                    BADCODE("Handlers start at the same offset");
                }

                // Case 7, 8
                BADCODE("Overlapping try regions");
            }

            // Case 10-13 (nStart >= rStart && nEnd <= rEnd)
            if ((nStart != rStart) || (nEnd != rEnd))
            {
                // Cases 10-12: [Child]
                if (root.ehnIsTryBlock)
                {
                    BADCODE("Inner try appears after outer try in exception handling table");
                }
                else
                {
                    // We have an EH clause nested within a handler, but the parent
                    // handler clause came first in the table. The rest of the compiler
                    // doesn't expect this, so sort the EH table.

                    fgNeedToSortEHTable = true;

                    // Case 12 (nStart == rStart)
                    // non try blocks are not allowed to start at the same offset
                    if ((nStart == rStart) && !node.ehnIsTryBlock)
                    {
                        BADCODE("Handlers start at the same offset");
                    }

                    // check this!
                    root = ref root.ehnChild;
                    continue;
                }
            }

            // Case 13: [Equivalent]
            if (!node.ehnIsTryBlock && !root.ehnIsTryBlock)
            {
                BADCODE("Handlers cannot be shared");
            }

            if (!node.ehnIsTryBlock || !root.ehnIsTryBlock)
            {
                // Equivalent is only allowed for try bodies
                // If one is a handler, this means the nesting is wrong
                BADCODE("Handler and try with the same offset");
            }

            node.ehnNext = root;
            node.ehnEquivalent = root;

            // check that the corresponding handler is either a catch handler or a filter

            var nodeHandlerNode = node.ehnHandlerNode;
            assert(nodeHandlerNode is not null);

            var rootHandlerNode = root.ehnHandlerNode;
            assert(rootHandlerNode is not null);

            if (nodeHandlerNode.ehnIsFaultBlock || nodeHandlerNode.ehnIsFinallyBlock ||
                rootHandlerNode.ehnIsFaultBlock || rootHandlerNode.ehnIsFinallyBlock)
            {
                BADCODE("Try block with multiple non-filter/non-handler blocks");
            }

            break;
        }
    }

    /// <summary>Make node the parent of root</summary>
    /// <param name="root"></param>
    /// <param name="node"></param>
    /// <remarks>All siblings of root that are fully or partially nested in node remain siblings of root</remarks>
    public void verInsertEhNodeParent(ref EHNodeDsc root, EHNodeDsc node)
    {
        noway_assert(node.ehnNext is null);
        noway_assert(node.ehnChild is null);

        // Root is nested in Node
        noway_assert(node.ehnStartOffset <= root.ehnStartOffset);
        noway_assert(node.ehnEndOffset >= root.ehnEndOffset);

        // Root is not the same as Node
        noway_assert(node.ehnStartOffset != root.ehnStartOffset || node.ehnEndOffset != root.ehnEndOffset);

        if (node.ehnIsFilterBlock)
        {
            BADCODE("Protected block appearing within filter block");
        }

        var lastChild = null as EHNodeDsc;
        var sibling = root.ehnNext;

        while (sibling is not null)
        {
            // siblings are ordered left to right, largest right.
            // nodes have a width of at least one.
            // Hence sibling start will always be after Node start.

            noway_assert(sibling.ehnStartOffset > node.ehnStartOffset);

            // (1): disjoint
            if (sibling.ehnStartOffset > node.ehnEndOffset)
            {
                break;
            }

            // (2): partial containment.
            if (sibling.ehnEndOffset > node.ehnEndOffset)
            {
                BADCODE("Overlapping try regions");
            }

            // else full containment (follows from (1) and (2))
            lastChild = sibling;
            sibling = sibling.ehnNext;
        }

        // All siblings of Root up to and including lastChild will continue to be
        // siblings of Root (and children of Node). The node to the right of
        // lastChild will become the first sibling of Node.

        if (lastChild is not null)
        {
            // Node has more than one child including Root
            node.ehnNext = lastChild.ehnNext;
            lastChild.ehnNext = null;
        }
        else
        {
            // Root is the only child of Node
            node.ehnNext = root.ehnNext;
            root.ehnNext = null;
        }

        node.ehnChild = root;
        root = node;
    }
}
