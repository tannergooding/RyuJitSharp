// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private BasicBlock fgCloneTryRegionMutating(BasicBlock tryEntry, CloneTryInfo info,
        ref BasicBlock? insertAfter, List<BasicBlock> blocks, ushort tryIndex,
        ushort outermostTryIndex, ushort enclosingTryIndex, ushort enclosingHndIndex,
        int regionCount, int clonedOutermostIndex)
    {
        var map = info.Map
            ?? throw new FatalJitException("Cloning a try region requires a block map.");
        assert(ehGetDsc(outermostTryIndex).ebdTryBeg == tryEntry);
        info.EHIndexShift = checked((uint)regionCount);

        // Middle insertion moves enclosing clauses but not the original clauses being cloned.
        var indexShift = clonedOutermostIndex - outermostTryIndex;
        assert(indexShift > 0);
        var clonedLowestIndex = clonedOutermostIndex - regionCount + 1;
        JITDUMP($"New EH regions are EH#{clonedLowestIndex:D2} ... EH#{clonedOutermostIndex:D2}\n");

        for (var index = clonedLowestIndex; index <= clonedOutermostIndex; index++)
        {
            var originalIndex = checked((ushort)(index - indexShift));
            ref var cloned = ref compHndBBtab[index];
            cloned = compHndBBtab[originalIndex];
            cloned.ebdID = impInlineRoot.compEHID++;

            if (cloned.ebdEnclosingTryIndex != EHblkDsc.NO_ENCLOSING_INDEX)
            {
                if (cloned.ebdEnclosingTryIndex < clonedOutermostIndex)
                {
                    cloned.ebdEnclosingTryIndex = checked((ushort)(cloned.ebdEnclosingTryIndex + indexShift));
                }
                JITDUMP($"EH#{index:D2} now enclosed in try EH#{cloned.ebdEnclosingTryIndex:D2}\n");
            }
            else
            {
                JITDUMP($"EH#{index:D2} not enclosed in any try\n");
            }

            if (cloned.ebdEnclosingHndIndex != EHblkDsc.NO_ENCLOSING_INDEX)
            {
                if (cloned.ebdEnclosingHndIndex < clonedOutermostIndex)
                {
                    cloned.ebdEnclosingHndIndex = checked((ushort)(cloned.ebdEnclosingHndIndex + indexShift));
                }
                JITDUMP($"EH#{index:D2} now enclosed in handler EH#{cloned.ebdEnclosingHndIndex:D2}\n");
            }
            else
            {
                JITDUMP($"EH#{index:D2} not enclosed in any handler\n");
            }
        }

        JITDUMP("Cloning blocks for try...\n");
        foreach (var block in blocks)
        {
            var insertionPoint = insertAfter
                ?? throw new FatalJitException("Cloning a try region requires an insertion point.");
            var clone = fgNewBBafter(BBJ_ALWAYS, insertionPoint, extendRegion: false);
            JITDUMP($"Adding {FMT_BB(clone.bbNum)} (copy of {FMT_BB(block.bbNum)}) after {FMT_BB(insertionPoint.bbNum)}\n");
            map[block] = clone;
            BasicBlock.CloneBlockState(this, clone, block);
            clone.scaleBBWeight(info.ProfileScale);
            if (info.ScaleOriginalBlockProfile)
            {
                block.scaleBBWeight(Math.Max(0.0, 1.0 - info.ProfileScale));
            }
            insertAfter = clone;
        }
        JITDUMP("Done cloning blocks for try...\n");

        // Clones follow the originals in layout; update both cloned and enclosing EH extents.
        JITDUMP("Fixing region indices...\n");
        foreach (var block in blocks)
        {
            if (!map.TryGetValue(block, out var clone))
            {
                throw new FatalJitException("A cloned block requires its block-map entry.");
            }

            void UpdateBlockReferences(ushort region)
            {
                while (true)
                {
                    ref var descriptor = ref ehGetDsc(region);
                    if (descriptor.ebdTryBeg == block)
                    {
                        descriptor.ebdTryBeg = clone;
                        JITDUMP($"Try begin for EH#{region:D2} is {FMT_BB(clone.bbNum)}\n");
                    }
                    if (descriptor.ebdTryLast == block)
                    {
                        fgSetTryEnd(ref descriptor, clone);
                    }
                    if (descriptor.ebdHndBeg == block)
                    {
                        descriptor.ebdHndBeg = clone;
                        JITDUMP($"Handler begin for EH#{region:D2} is {FMT_BB(clone.bbNum)}\n");
                    }
                    if (descriptor.ebdHndLast == block)
                    {
                        fgSetHndEnd(ref descriptor, clone);
                    }
                    if (descriptor.HasFilter && descriptor.ebdFilter == block)
                    {
                        descriptor.ebdFilter = clone;
                        JITDUMP($"Filter begin for EH#{region:D2} is {FMT_BB(clone.bbNum)}\n");
                    }

                    region = ehGetEnclosingRegionIndex(region, out _);
                    if (region == EHblkDsc.NO_ENCLOSING_INDEX)
                    {
                        break;
                    }
                }
            }

            if (block.hasTryIndex)
            {
                var original = block.TryIndex;
                var clonedIndex = original < enclosingTryIndex
                    ? checked((ushort)(original + indexShift)) : original;
                clone.TryIndex = clonedIndex;
                UpdateBlockReferences(clonedIndex);
            }

            if (block.hasHndIndex)
            {
                var original = block.HndIndex;
                var clonedIndex = original < enclosingHndIndex
                    ? checked((ushort)(original + indexShift)) : original;
                clone.HndIndex = clonedIndex;
                UpdateBlockReferences(clonedIndex);
                if (bbIsHandlerBeg(clone))
                {
                    clone.bbRefs++;
                }
            }
        }
        JITDUMP("Done fixing region indices\n");

        if (info.AddEdges)
        {
            JITDUMP("Adding edges in the newly cloned try\n");
            foreach (var pair in map)
            {
                assert(pair.Value.Kind is BBJ_ALWAYS && !pair.Value.HasInitializedTarget);
                optSetMappedBlockTargets(pair.Key, pair.Value, map);
            }
        }
        else
        {
            JITDUMP("Not adding edges in the newly cloned try\n");
        }

        if (fgHasAddCodeDscMap)
        {
            var descriptors = fgGetAddCodeDscMap();
            var clonedDescriptors = new Stack<AddCodeDsc>();
            assert(clonedLowestIndex >= indexShift && clonedOutermostIndex >= indexShift);
            var originalLowestIndex = clonedLowestIndex - indexShift;
            var originalOutermostIndex = clonedOutermostIndex - indexShift;

            foreach (var add in descriptors.Values)
            {
                var inTry = add.acdTryIndex > 0;
                var inHandler = add.acdHndIndex > 0;
                // ACD indices are one-based; zero denotes a location outside an EH region.
                var cloneTry = inTry &&
                    (add.acdTryIndex - 1 >= originalLowestIndex) &&
                    (add.acdTryIndex - 1 <= originalOutermostIndex);
                var cloneHandler = inHandler &&
                    (add.acdHndIndex - 1 >= originalLowestIndex) &&
                    (add.acdHndIndex - 1 <= originalOutermostIndex);
                if (!cloneTry && !cloneHandler)
                {
                    continue;
                }

                JITDUMP("Will need to clone: ");
#if DEBUG
                if (verbose)
                {
                    add.Dump();
                }
#endif
                var cloned = new AddCodeDsc
                {
                    acdDstBlk = null,
                    acdTryIndex = cloneTry
                        ? checked((ushort)(add.acdTryIndex + indexShift)) : add.acdTryIndex,
                    acdHndIndex = cloneHandler
                        ? checked((ushort)(add.acdHndIndex + indexShift)) : add.acdHndIndex,
                    acdKeyDsg = add.acdKeyDsg,
                    acdKind = add.acdKind,
                    acdUsed = false,
#if !FEATURE_FIXED_OUT_ARGS
                    acdStkLvl = 0,
                    acdStkLvlInit = false,
#endif
#if DEBUG
                    acdNum = acdCount++,
#endif
                };
                clonedDescriptors.Push(cloned);
            }

            while (clonedDescriptors.Count != 0)
            {
                var cloned = clonedDescriptors.Pop();
                descriptors[new AddCodeDscKey(cloned)] = cloned;
                JITDUMP("Added clone: ");
#if DEBUG
                if (verbose)
                {
                    cloned.Dump();
                }
#endif
            }
        }

        var clonedTryEntry = map[tryEntry];
        JITDUMP($"Done cloning, cloned try entry is {FMT_BB(clonedTryEntry.bbNum)}\n");
        return clonedTryEntry;
    }
}
