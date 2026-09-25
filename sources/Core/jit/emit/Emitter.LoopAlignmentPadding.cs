// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_LOOP_ALIGN && TARGET_AMD64
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint getLoopSize(insGroup loopHeader, uint maxLoopSize
#if DEBUG
        , bool isAlignAdjusted, insGroup containingIG, insGroup loopHeadPredIG
#endif
        )
    {
        var compiler = _compiler ?? throw new FatalJitException("Loop-size calculation requires an active compiler.");
        uint loopSize = 0;
        JITDUMP($"*************** In getLoopSize() for {emitLabelString(loopHeader)}\n");

        for (var group = loopHeader; group is not null; group = group.igNext)
        {
            loopSize = unchecked(loopSize + group.igSize);
            JITDUMP($"   {emitLabelString(group)} has {group.igSize} bytes.");

            if (group.endsWithAlignInstr() || group.hadAlignInstr())
            {
#if DEBUG
                if ((group.igLoopBackEdge is not null) && !ReferenceEquals(group.igLoopBackEdge, loopHeader))
                {
                    jitprintf("\n\nMismatch in align instruction.\n");
                    jitprintf($"Containing IG: IG{containingIG.GetDisplayId():D2}\n");
                    jitprintf($"loopHeadPredIG: IG{loopHeadPredIG.GetDisplayId():D2}\n");
                    jitprintf($"loopHeadIG: IG{loopHeader.GetDisplayId():D2}\n");
                    jitprintf($"igInLoop: IG{group.GetDisplayId():D2}\n");
                    jitprintf($"igInLoop->igLoopBackEdge: IG{group.igLoopBackEdge.GetDisplayId():D2}\n");

                    if (group.endsWithAlignInstr())
                    {
                        var lastAlign = group.igLastIns as instrDescAlign
                            ?? throw new FatalJitException("An aligned group requires a final alignment descriptor.");
                        assert(ReferenceEquals(lastAlign.idaIG, group));
                        var targetGroup = lastAlign.idaLoopHeadPredIG?.igNext
                            ?? throw new FatalJitException("An alignment descriptor requires a loop-head group.");
                        jitprintf($"igInLoop has align instruction for : IG{targetGroup.GetDisplayId():D2}\n");
                    }

                    jitprintf("Loop:\n");
                    var current = loopHeader;
                    while ((current is not null) && !ReferenceEquals(current.igLoopBackEdge, loopHeader))
                    {
                        jitprintf($"\tIG{current.GetDisplayId():D2}\n");
                        current = current.igNext;
                    }
                    if (current is null)
                    {
                        jitprintf($"Did not find IG with back edge to IG{loopHeader.GetDisplayId():D2}\n");
                    }

                    assert(false);
                }

                if (isAlignAdjusted)
                {
                    var align = emitAlignList;
                    while ((align is not null) && !ReferenceEquals(align.idaIG, group))
                    {
                        align = align.idaNext;
                    }
                    assert(align is not null);

                    uint adjustedPadding = 0;
                    if (compiler.opts.compJitAlignLoopAdaptive)
                    {
                        adjustedPadding = align.idCodeSize();
                    }
                    else
                    {
                        for (var current = align;
                            (current is not null) && ReferenceEquals(current.idaIG, group);
                            current = current.idaNext)
                        {
                            adjustedPadding = unchecked(adjustedPadding + current.idCodeSize());
                        }
                    }

                    loopSize = unchecked(loopSize - adjustedPadding);
                }
                else
#endif
                {
                    JITDUMP($" but ends with align instruction, taking off " +
                        $"{compiler.opts.compJitAlignPaddingLimit} bytes.");
                    loopSize = unchecked(loopSize - compiler.opts.compJitAlignPaddingLimit);
                }
            }

            if (ReferenceEquals(group.igLoopBackEdge, loopHeader) || (loopSize > maxLoopSize))
            {
#if DEBUG
                if (ReferenceEquals(group.igLoopBackEdge, loopHeader))
                {
                    JITDUMP(" -- Found the back edge.\n");
                }
                else
                {
                    JITDUMP($" -- loopSize exceeded the threshold of {maxLoopSize} bytes.\n");
                }
#endif
                break;
            }

            JITDUMP("\n");
        }

        JITDUMP($"loopSize of {emitLabelString(loopHeader)} = {loopSize} bytes.\n");
        return loopSize;
    }

    private uint emitCalculatePaddingForLoopAlignment(insGroup loopHeadIG, uint offset
#if DEBUG
        , bool isAlignAdjusted, insGroup containingIG, insGroup loopHeadPredIG
#endif
        )
    {
        var compiler = _compiler ?? throw new FatalJitException("Loop-alignment padding requires an active compiler.");
        uint alignmentBoundary = compiler.opts.compJitAlignLoopBoundary;

        if ((offset & (alignmentBoundary - 1)) == 0)
        {
            JITDUMP($";; Skip alignment: 'Loop at {emitLabelString(loopHeadIG)} " +
                $"already aligned at {alignmentBoundary}B boundary.'\n");
            return 0;
        }

        uint maxLoopSize;
        var maxLoopBlocksAllowed = 0;
        if (compiler.opts.compJitAlignLoopAdaptive)
        {
            maxLoopBlocksAllowed = BitOperations.Log2(alignmentBoundary) - 1;
            maxLoopSize = unchecked(alignmentBoundary * (uint)maxLoopBlocksAllowed);
        }
        else
        {
            maxLoopSize = compiler.opts.compJitAlignLoopMaxCodeSize;
        }

        var loopSize = getLoopSize(loopHeadIG, maxLoopSize
#if DEBUG
            , isAlignAdjusted, containingIG, loopHeadPredIG
#endif
            );
        if (loopSize > maxLoopSize)
        {
            JITDUMP($";; Skip alignment: 'Loop at {emitLabelString(loopHeadIG)} is big. " +
                $"LoopSize= {loopSize}, MaxLoopSize= {maxLoopSize}.'\n");
            return 0;
        }

        uint paddingToAdd = 0;
        var minBlocksNeededForLoop = unchecked((loopSize + alignmentBoundary - 1) / alignmentBoundary);
        var skipPadding = false;

        if (compiler.opts.compJitAlignLoopAdaptive)
        {
            var maxPaddingBytes = (1u << (maxLoopBlocksAllowed - (int)minBlocksNeededForLoop + 1)) - 1;
            var paddingBytes = unchecked((uint)-(int)offset) & (alignmentBoundary - 1);

            if (paddingBytes > maxPaddingBytes)
            {
                alignmentBoundary >>= 1;
                maxPaddingBytes = 1u << (maxLoopBlocksAllowed - (int)minBlocksNeededForLoop + 1);
                paddingBytes = unchecked((uint)-(int)offset) & (alignmentBoundary - 1);

                if (paddingBytes == 0)
                {
                    skipPadding = true;
                    JITDUMP($";; Skip alignment: 'Loop at {emitLabelString(loopHeadIG)} " +
                        $"already aligned at {alignmentBoundary}B boundary.'\n");
                }
                else if (paddingBytes > maxPaddingBytes)
                {
                    skipPadding = true;
                    JITDUMP($";; Skip alignment: 'Loop at {emitLabelString(loopHeadIG)} " +
                        $"PaddingNeeded= {paddingBytes}, MaxPadding= {maxPaddingBytes}, " +
                        $"LoopSize= {loopSize}, AlignmentBoundary= {alignmentBoundary}B.'\n");
                }
            }

            if (!skipPadding)
            {
                var extraBytesNotInLoop = unchecked(
                    ((uint)compiler.opts.compJitAlignLoopBoundary * minBlocksNeededForLoop) - loopSize);
                var currentOffset = offset % alignmentBoundary;

                if (currentOffset > extraBytesNotInLoop)
                {
                    paddingToAdd = paddingBytes;
                }
                else
                {
                    JITDUMP($";; Skip alignment: 'Loop at {emitLabelString(loopHeadIG)} " +
                        $"is aligned to fit in {minBlocksNeededForLoop} blocks of {alignmentBoundary} chunks.'\n");
                }
            }
        }
        else
        {
            var extraBytesNotInLoop = unchecked((alignmentBoundary * minBlocksNeededForLoop) - loopSize);
            var currentOffset = offset % alignmentBoundary;

#if DEBUG
            if (compiler.opts.compJitAlignLoopForJcc)
            {
                currentOffset++;
            }
#endif
            if (currentOffset > extraBytesNotInLoop)
            {
                paddingToAdd = unchecked((uint)-(int)offset) & (alignmentBoundary - 1);
            }
            else
            {
                JITDUMP($";; Skip alignment: 'Loop at {emitLabelString(loopHeadIG)} " +
                    $"is aligned to fit in {minBlocksNeededForLoop} blocks of {alignmentBoundary} chunks.'\n");
            }
        }

        JITDUMP($";; Calculated padding to add {paddingToAdd} bytes to align " +
            $"{emitLabelString(loopHeadIG)} at {alignmentBoundary}B boundary.\n");
        assert((paddingToAdd == 0) || (((offset + paddingToAdd) & (alignmentBoundary - 1)) == 0));
        return paddingToAdd;
    }
}
#endif
