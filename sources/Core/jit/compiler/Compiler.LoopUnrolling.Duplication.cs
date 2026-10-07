// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optCanDuplicateLoop(FlowGraphNaturalLoop loop, bool withEH, out string reason)
    {
        reason = "Loop not entirely within one EH region";
        if (!withEH)
        {
            return loop.VisitLoopBlocks(block =>
                BasicBlock.sameEHRegion(block, loop.Header) ? BasicBlockVisit.Continue : BasicBlockVisit.Abort)
                is BasicBlockVisit.Continue;
        }

        var topLevelTries = new List<BasicBlock>();
        _ = loop.VisitLoopBlocks(block => {
            if (BasicBlock.sameEHRegion(block, loop.Header) || !bbIsTryBeg(block))
            {
                return BasicBlockVisit.Continue;
            }

            var headerInTry = loop.Header.hasTryIndex;
            var enclosingTry = ehTrueEnclosingTryIndex(block.TryIndex);
            if ((headerInTry && enclosingTry == loop.Header.TryIndex) ||
                (!headerInTry && enclosingTry == EHblkDsc.NO_ENCLOSING_INDEX))
            {
                var inHandlerOfLoopTry = block.hasHndIndex &&
                    loop.ContainsBlock(ehGetDsc(block.HndIndex).ebdTryBeg);
                if (!inHandlerOfLoopTry)
                {
                    topLevelTries.Add(block);
                }
            }

            return BasicBlockVisit.Continue;
        });

        if (topLevelTries.Count > 0)
        {
            JITDUMP($"L{loop.Index:D2} contains {topLevelTries.Count} top-level try region(s)\n");
        }

        for (var index = topLevelTries.Count - 1; index >= 0; index--)
        {
            if (fgCloneTryRegionFeasibility(topLevelTries[index], new CloneTryInfo(this)) is null)
            {
                reason = "Loop contains uncloneable try region";
                return false;
            }
        }

        reason = "";
        return true;
    }

    private void optDuplicateLoop(FlowGraphNaturalLoop loop, ref BasicBlock insertAfter,
        Dictionary<BasicBlock, BasicBlock> map, double weightScale, bool withEH)
    {
        assert(optCanDuplicateLoop(loop, withEH, out _));
        if (!withEH)
        {
            var currentInsertAfter = insertAfter;
            _ = loop.VisitLoopBlocks(block => {
                var clone = fgNewBBafter(BBJ_ALWAYS, currentInsertAfter, extendRegion: true);
                JITDUMP($"Adding {FMT_BB(clone.bbNum)} (copy of {FMT_BB(block.bbNum)}) after {FMT_BB(currentInsertAfter.bbNum)}\n");
                BasicBlock.CloneBlockState(this, clone, block);
                clone.bbRefs = 0;
                clone.scaleBBWeight(weightScale);
                currentInsertAfter = clone;
                map[block] = clone;
                return BasicBlockVisit.Continue;
            });
            insertAfter = currentInsertAfter;
        }
        else
        {
            optDuplicateLoopWithEH(loop, ref insertAfter, map, weightScale);
        }

        _ = loop.VisitLoopBlocks(block => {
            var clone = map[block];
            assert(!clone.HasInitializedTarget);
            optSetMappedBlockTargets(block, clone, map);
            return BasicBlockVisit.Continue;
        });

        if (withEH)
        {
            foreach (var (block, clone) in map)
            {
                if (!loop.ContainsBlock(block))
                {
                    assert(!clone.HasInitializedTarget);
                    optSetMappedBlockTargets(block, clone, map);
                }
            }
        }
    }

    private void optDuplicateLoopWithEH(FlowGraphNaturalLoop loop, ref BasicBlock insertAfter,
        Dictionary<BasicBlock, BasicBlock> map, double weightScale)
    {
        var insertionPoint = insertAfter;
        var regionEnds = new Stack<(ushort Index, BasicBlock Block, bool IsTry)>();
        if (insertionPoint.hasTryIndex || insertionPoint.hasHndIndex)
        {
            var region = ehGetMostNestedRegionIndex(insertionPoint, out var inTry);
            if (region != 0)
            {
                region--;
                while (true)
                {
                    ref var descriptor = ref ehGetDsc(checked((ushort)region));
                    regionEnds.Push((checked((ushort)region),
                        inTry ? descriptor.ebdTryLast : descriptor.ebdHndLast, inTry));
                    region = ehGetEnclosingRegionIndex(checked((ushort)region), out inTry);
                    if (region == EHblkDsc.NO_ENCLOSING_INDEX)
                    {
                        break;
                    }
                }
            }
        }

        uint ehIndexShift = 0;
        var visited = new HashSet<BasicBlock>();
        var currentInsertAfter = insertAfter;
        _ = loop.VisitLoopBlocks(block => {
            if (!visited.Add(block))
            {
                return BasicBlockVisit.Continue;
            }

            if (bbIsTryBeg(block))
            {
                var info = new CloneTryInfo(this)
                {
                    Map = map,
                    AddEdges = false,
                    ProfileScale = weightScale,
                    BlocksToClone = [],
                };
                if (fgCloneTryRegion(block, info, ref currentInsertAfter) is null)
                {
                    throw new FatalJitException("A loop try region passed feasibility but could not be cloned.");
                }

                ehIndexShift += info.EHIndexShift;
                foreach (var original in info.BlocksToClone)
                {
                    _ = visited.Add(original);
                }

                return BasicBlockVisit.Continue;
            }

            assert(!bbIsHandlerBeg(block));
            var clone = fgNewBBafter(BBJ_ALWAYS, currentInsertAfter, extendRegion: false);
            JITDUMP($"Adding {FMT_BB(clone.bbNum)} (copy of {FMT_BB(block.bbNum)}) after {FMT_BB(currentInsertAfter.bbNum)}\n");
            BasicBlock.CloneBlockState(this, clone, block);
            clone.bbRefs = 0;
            clone.scaleBBWeight(weightScale);
            map[block] = clone;
            currentInsertAfter = clone;
            return BasicBlockVisit.Continue;
        });
        insertAfter = currentInsertAfter;

        while (regionEnds.Count != 0)
        {
            var (index, previousEnd, isTry) = regionEnds.Pop();
            ref var descriptor = ref ehGetDsc(checked((ushort)(index + ehIndexShift)));
            var newEnd = previousEnd == insertionPoint ? insertAfter : previousEnd;
            if (isTry)
            {
                fgSetTryEnd(ref descriptor, newEnd);
            }
            else
            {
                fgSetHndEnd(ref descriptor, newEnd);
            }
        }
    }
}
