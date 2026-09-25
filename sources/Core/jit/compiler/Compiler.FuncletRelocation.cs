// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public enum FG_RELOCATE_TYPE
    {
        FG_RELOCATE_TRY,
        FG_RELOCATE_HANDLER,
    }

    public BasicBlock? fgRelocateEHRange(ushort regionIndex, FG_RELOCATE_TYPE relocateType)
    {
        noway_assert(relocateType == FG_RELOCATE_TYPE.FG_RELOCATE_HANDLER);
        ref var handler = ref ehGetDsc(regionIndex);
        BasicBlock? start = null;
        BasicBlock? last = null;

        if (relocateType == FG_RELOCATE_TYPE.FG_RELOCATE_TRY)
        {
            start = handler.ebdTryBeg;
            last = handler.ebdTryLast;
        }
        else if (relocateType == FG_RELOCATE_TYPE.FG_RELOCATE_HANDLER)
        {
            // A filter and its handler remain contiguous and move together.
            start = handler.HasFilter ? handler.ebdFilter : handler.ebdHndBeg;
            last = handler.ebdHndLast;
        }

        noway_assert((start is not null) && (last is not null));
        if (start == fgFirstBB)
        {
            JITDUMP($"*************** Failed fgRelocateEHRange({FMT_BB(start.bbNum)}..{FMT_BB(last.bbNum)}) because can not relocate first block\n");
            return null;
        }

        var inRange = false;
        var validRange = false;
        var block = fgFirstBB;
        while (true)
        {
            if (block == start)
            {
                noway_assert(!inRange);
                inRange = true;
            }
            else if (last.Next == block)
            {
                noway_assert(inRange);
                inRange = false;
                break;
            }

            if (inRange)
            {
                validRange = true;
            }
            if (block is null)
            {
                break;
            }
            block = block.Next;
        }
        noway_assert(validRange && !inRange);
        var previous = start.Prev;
        noway_assert(previous is not null);

        JITDUMP($"Relocating {(relocateType == FG_RELOCATE_TYPE.FG_RELOCATE_TRY ? "try" : "handler")} range {FMT_BB(start.bbNum)}..{FMT_BB(last.bbNum)} (EH#{regionIndex}) to end of BBlist\n");
#if DEBUG
        if (verbose)
        {
            fgDispBasicBlocks();
            fgDispHandlerTab();
        }
#endif
        fgUnlinkRange(start, last);
        var insertAfter = fgLastBB;
        assert(insertAfter is not null);

        // Strictly contained and equal ranges move without metadata changes. A larger enclosing range
        // sharing our last block loses its tail; a smaller range sharing that block moves with us.
        // Handler starts cannot be shared with an enclosing region, so only the ends need adjustment.
        for (ushort index = 0; index < compHndBBtabCount; index++)
        {
            if (index == regionIndex)
            {
                continue;
            }
            ref var other = ref ehGetDsc(index);
            if (other.ebdTryLast == last)
            {
                for (block = other.ebdTryBeg; block is not null; block = block.Next)
                {
                    if (block == previous)
                    {
                        fgSetTryEnd(ref other, previous);
                        break;
                    }
                    else if (other.ebdTryLast.Next == block)
                    {
                        break;
                    }
                }
            }

            if (other.ebdHndLast == last)
            {
                for (block = other.ebdHndBeg; block is not null; block = block.Next)
                {
                    if (block == previous)
                    {
                        fgSetHndEnd(ref other, previous);
                        break;
                    }
                    else if (other.ebdHndLast.Next == block)
                    {
                        break;
                    }
                }
            }
        }

        fgMoveBlocksAfter(start, last, insertAfter);
        if (fgFirstFuncletBB is null)
        {
            fgFirstFuncletBB = start;
        }
        else
        {
            assert(fgFirstFuncletBB != insertAfter.Next);
        }
#if DEBUG
        if (verbose)
        {
            jitprintf("Create funclets: moved region\n");
            fgDispHandlerTab();
        }
#endif
        return last;
    }

    public void fgUnlinkRange(BasicBlock begin, BasicBlock end)
    {
        assert(fgFirstColdBlock is null);
        var previous = begin.Prev;
        assert(previous is not null);
        if (fgLastBB == end)
        {
            fgLastBB = previous;
            previous.Next = null;
        }
        else
        {
            previous.Next = end.Next;
        }

#if DEBUG
        // A moved range cannot include the first funclet or cross the main/funclet boundary.
        for (var block = begin; block != end.Next; block = block.Next)
        {
            assert(block is not null);
            assert(block != fgFirstFuncletBB);
        }
#endif
    }
}
