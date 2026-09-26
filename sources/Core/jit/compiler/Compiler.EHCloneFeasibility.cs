// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed class CloneTryInfo
{
    public BitVecTraits Traits { get; }

    public nint[] Visited { get; }

    public List<BasicBlock>? BlocksToClone;

    public Dictionary<BasicBlock, BasicBlock>? Map;

    public weight_t ProfileScale;

    public uint EHIndexShift;

    public bool AddEdges;

    public bool ScaleOriginalBlockProfile;

    public CloneTryInfo(Compiler compiler)
    {
        Traits = new BitVecTraits(compiler, compiler.compBasicBlockID);
        Visited = BitVecOps.MakeEmpty(Traits);
    }
}

public partial class Compiler
{
    public BasicBlock? fgCloneTryRegionFeasibility(BasicBlock tryEntry, CloneTryInfo info)
    {
        BasicBlock? insertionPoint = null;
        return fgCloneTryRegionCore(tryEntry, info, ref insertionPoint);
    }

    public BasicBlock? fgCloneTryRegion(BasicBlock tryEntry, CloneTryInfo info, ref BasicBlock insertAfter)
    {
        if (info.Map is null)
        {
            throw new FatalJitException("Cloning a try region requires a block map.");
        }

        var insertionPoint = (BasicBlock?)insertAfter;
        var clone = fgCloneTryRegionCore(tryEntry, info, ref insertionPoint);
        insertAfter = insertionPoint
            ?? throw new FatalJitException("Cloning a try region requires an insertion point.");
        return clone;
    }

    private BasicBlock? fgCloneTryRegionCore(BasicBlock tryEntry, CloneTryInfo info,
        ref BasicBlock? insertAfter)
    {
        assert(bbIsTryBeg(tryEntry));
        var deferCloning = insertAfter is null;
        JITDUMP($"{(deferCloning ? "Checking if it is possible to clone" : "Cloning")} the try region EH#{tryEntry.TryIndex:D2} headed by {FMT_BB(tryEntry.bbNum)}\n");

        var regionsToProcess = new Stack<ushort>();
        var tryIndex = tryEntry.TryIndex;
        var numberOfBlocksToClone = 0;
        var blocksToClone = info.BlocksToClone;
        if (!deferCloning && blocksToClone is null)
        {
            blocksToClone = [];
        }
        var regionCount = 0;

        bool AddBlockToClone(BasicBlock block, string description)
        {
            if (!BitVecOps.TryAddElemD(info.Traits, info.Visited, block.bbID))
            {
                JITDUMP($"[already seen]  {description} block {FMT_BB(block.bbNum)}\n");
                return false;
            }

            JITDUMP($"  {description} block {FMT_BB(block.bbNum)}\n");
            numberOfBlocksToClone++;
            blocksToClone?.Add(block);
            return true;
        }

        JITDUMP($"==> try EH#{tryIndex:D2}\n");
        regionsToProcess.Push(tryIndex);

        // Nested regions are pushed while walking layout, so their processing order is reversed.
        while (regionsToProcess.Count != 0)
        {
            regionCount++;
            var regionIndex = regionsToProcess.Pop();
            ref var descriptor = ref ehGetDsc(regionIndex);
            JITDUMP($"== processing try EH#{regionIndex:D2}\n");

            var firstTryBlock = descriptor.ebdTryBeg;
            var lastTryBlock = descriptor.ebdTryLast;
            if (BitVecOps.IsMember(info.Traits, info.Visited, firstTryBlock.bbID))
            {
                JITDUMP($"already walked try region for EH#{regionIndex:D2}\n");
                assert(BitVecOps.IsMember(info.Traits, info.Visited, lastTryBlock.bbID));
            }
            else
            {
                JITDUMP($"walking try region for EH#{regionIndex:D2}\n");
                foreach (var block in new BasicBlockRangeList(firstTryBlock, lastTryBlock))
                {
                    var added = AddBlockToClone(block, "try region");
                    if (bbIsTryBeg(block) && block != firstTryBlock)
                    {
                        assert(added);
                        JITDUMP($"==> found try EH#{block.TryIndex:D2} nested in try EH#{regionIndex:D2} region at {FMT_BB(block.bbNum)}\n");
                        regionsToProcess.Push(block.TryIndex);
                    }
                }
            }

            if (descriptor.HasFinallyHandler)
            {
                ehGetCallFinallyBlockRange(regionIndex, out var firstCallFinallyBlock, out var lastCallFinallyBlock);
                JITDUMP($"walking callfinally region for EH#{regionIndex:D2} [{FMT_BB(firstCallFinallyBlock.bbNum)} ... {FMT_BB(lastCallFinallyBlock.bbNum)}]\n");
                foreach (var block in new BasicBlockRangeList(firstCallFinallyBlock, lastCallFinallyBlock))
                {
                    if (block.Kind is BBJ_CALLFINALLY && block.Target == descriptor.ebdHndBeg)
                    {
                        _ = AddBlockToClone(block, "callfinally");
                    }
                    else if (block.Kind is BBJ_CALLFINALLYRET)
                    {
                        var previous = block.Prev
                            ?? throw new FatalJitException("A callfinally continuation requires a preceding block.");
                        assert(previous.Kind is BBJ_CALLFINALLY);
                        if (previous.Target == descriptor.ebdHndBeg)
                        {
                            _ = AddBlockToClone(block, "callfinallyret");
                        }
                    }
                }
            }

            if (descriptor.HasFilter)
            {
                var firstFilterBlock = descriptor.ebdFilter;
                var lastFilterBlock = descriptor.BBFilterLast;
                if (BitVecOps.IsMember(info.Traits, info.Visited, firstFilterBlock.bbID))
                {
                    JITDUMP($"already walked filter region for EH#{regionIndex:D2}\n");
                    assert(BitVecOps.IsMember(info.Traits, info.Visited, lastFilterBlock.bbID));
                }
                else
                {
                    JITDUMP($"walking filter region for EH#{regionIndex:D2}\n");
                    foreach (var block in new BasicBlockRangeList(firstFilterBlock, lastFilterBlock))
                    {
                        assert(!bbIsTryBeg(block));
                        _ = AddBlockToClone(block, "filter region");
                    }
                }
            }

            var firstHandlerBlock = descriptor.ebdHndBeg;
            var lastHandlerBlock = descriptor.ebdHndLast;
            if (BitVecOps.IsMember(info.Traits, info.Visited, firstHandlerBlock.bbID))
            {
                JITDUMP($"already walked handler region for EH#{regionIndex:D2}\n");
                assert(BitVecOps.IsMember(info.Traits, info.Visited, lastHandlerBlock.bbID));
            }
            else
            {
                JITDUMP($"walking handler region for EH#{regionIndex:D2}\n");
                foreach (var block in new BasicBlockRangeList(firstHandlerBlock, lastHandlerBlock))
                {
                    var added = AddBlockToClone(block, "handler region");
                    if (bbIsTryBeg(block))
                    {
                        assert(added);
                        JITDUMP($"==> found try entry for EH#{block.TryIndex:D2} nested in handler at {FMT_BB(block.bbNum)}\n");
                        regionsToProcess.Push(block.TryIndex);
                    }
                }
            }

            var enclosingTryIndex = descriptor.ebdEnclosingTryIndex;
            if (enclosingTryIndex != EHblkDsc.NO_ENCLOSING_INDEX &&
                EHblkDsc.ebdIsSameTry(descriptor, ehGetDsc(enclosingTryIndex)))
            {
                JITDUMP($"==> found mutual-protect try EH#{enclosingTryIndex:D2} for EH#{regionIndex:D2}\n");
                regionsToProcess.Push(enclosingTryIndex);
            }

            JITDUMP($"<== finished try EH#{regionIndex:D2}\n");
        }

        ref var tryDescriptor = ref ehGetDsc(tryIndex);
        var outermostTryIndex = tryIndex;
        ushort enclosingIndex;
        while (true)
        {
            enclosingIndex = ehGetDsc(outermostTryIndex).ebdEnclosingTryIndex;
            if (enclosingIndex == EHblkDsc.NO_ENCLOSING_INDEX ||
                !EHblkDsc.ebdIsSameTry(ehGetDsc(enclosingIndex), tryDescriptor))
            {
                break;
            }
            outermostTryIndex = enclosingIndex;
        }

        var enclosingHandlerIndex = tryEntry.hasHndIndex
            ? tryEntry.HndIndex : EHblkDsc.NO_ENCLOSING_INDEX;
        JITDUMP($"Will need to clone {regionCount} EH regions (outermost: EH#{outermostTryIndex:D2}) and {numberOfBlocksToClone} blocks\n");

        ushort insertBeforeIndex;
        if (enclosingIndex == EHblkDsc.NO_ENCLOSING_INDEX &&
            enclosingHandlerIndex == EHblkDsc.NO_ENCLOSING_INDEX)
        {
            JITDUMP("No enclosing EH region; cloned EH clauses will go at the end of the EH table\n");
            insertBeforeIndex = compHndBBtabCount;
        }
        else if (enclosingIndex == EHblkDsc.NO_ENCLOSING_INDEX || enclosingHandlerIndex < enclosingIndex)
        {
            JITDUMP($"Cloned EH clauses will go before enclosing handler region EH#{enclosingHandlerIndex:D2}\n");
            insertBeforeIndex = enclosingHandlerIndex;
        }
        else
        {
            JITDUMP($"Cloned EH clauses will go before enclosing try region EH#{enclosingIndex:D2}\n");
            insertBeforeIndex = enclosingIndex;
            assert(insertBeforeIndex == enclosingIndex);
        }

        if (insertBeforeIndex != compHndBBtabCount)
        {
            JITDUMP($"Existing EH region(s) EH#{insertBeforeIndex:D2}...EH#{compHndBBtabCount - 1:D2} will become EH#{insertBeforeIndex + regionCount:D2}...EH#{compHndBBtabCount + regionCount - 1:D2}\n");
        }

        var clonedOutermostIndex = regionCount > ushort.MaxValue
            ? -1 : fgTryAddEHTableEntries(insertBeforeIndex, (ushort)regionCount, deferAdding: deferCloning);
        if (clonedOutermostIndex < 0)
        {
            JITDUMP("fgCloneTryRegion: unable to expand EH table\n");
            return null;
        }

        if (deferCloning)
        {
            JITDUMP("fgCloneTryRegion: cloning is possible\n");
            return tryEntry;
        }

        return fgCloneTryRegionMutating(tryEntry, info, ref insertAfter,
            blocksToClone ?? throw new FatalJitException("Cloning a try region requires its blocks."),
            tryIndex, outermostTryIndex, enclosingIndex, enclosingHandlerIndex,
            regionCount, clonedOutermostIndex);
    }
}
