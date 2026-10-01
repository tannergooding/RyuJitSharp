// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class FlowGraphTryRegions
{
    private readonly Compiler _compiler;
    private readonly FlowGraphDfsTree? _dfsTree;
    private readonly FlowGraphTryRegion?[] _tryRegions;
    private readonly BitVecTraits _traits;
    private uint _numRegions;
    private uint _numTryCatchRegions;
    private bool _tryRegionsIncludeHandlerBlocks;
    private bool _hasSideEntry;

    private FlowGraphTryRegions(Compiler comp, FlowGraphDfsTree? dfsTree, uint numRegions)
    {
        _compiler = comp;
        _dfsTree = dfsTree;
        _tryRegions = new FlowGraphTryRegion?[numRegions];
        _traits = new BitVecTraits(comp, dfsTree is null ? comp.fgBBNumMax + 1 : dfsTree.PostOrderCount);
    }

    private void SetHasSideEntry()
    {
        _hasSideEntry = true;
    }

    public BitVecTraits GetBlockBitVecTraits() => _traits;

    public int GetBlockIndex(BasicBlock block) => _dfsTree is not null ? block.bbPostorderNum : block.bbNum;

    public Compiler GetCompiler() => _compiler;

    public uint NumTryRegions() => _numRegions;

    public uint NumTryCatchRegions() => _numTryCatchRegions;

    public FlowGraphDfsTree? GetDfsTree() => _dfsTree;

    public bool TryRegionsIncludeHandlerBlocks() => _tryRegionsIncludeHandlerBlocks;

    public bool HasSideEntry() => _hasSideEntry;

    public FlowGraphTryRegion? GetTryRegionByHeader(BasicBlock block)
    {
        if (!block.hasTryIndex)
        {
            return null;
        }

        ref var dsc = ref _compiler.ehGetBlockTryDsc(block);
        if (block != dsc.ebdTryBeg)
        {
            return null;
        }

        return _tryRegions[dsc.ebdID];
    }

    public static FlowGraphTryRegions Build(Compiler comp, FlowGraphDfsTree? dfsTree, bool includeHandlerBlocks = false)
    {
        // EH IDs are stable across deletion, so the collection can have empty slots.
        uint numTryRegions = comp.compEHID;
        var regions = new FlowGraphTryRegions(comp, dfsTree, numTryRegions);
        assert(numTryRegions >= comp.compHndBBtabCount);
        regions._tryRegionsIncludeHandlerBlocks = includeHandlerBlocks;

        foreach (ref var ehDsc in new EHClauses(comp))
        {
            var region = new FlowGraphTryRegion(comp.ehGetIndex(ehDsc), regions);
            regions._tryRegions[ehDsc.ebdID] = region;
            regions._numRegions++;
            if (ehDsc.HasCatchHandler)
            {
                regions._numTryCatchRegions++;
            }
        }

        if (regions._tryRegions.Length == 0)
        {
            return regions;
        }

        foreach (ref var ehDsc in new EHClauses(comp))
        {
            var region = regions._tryRegions[ehDsc.ebdID];
            assert(region is not null);
            var parentTryIndex = ehDsc.ebdEnclosingTryIndex;
            if (parentTryIndex != EHblkDsc.NO_ENCLOSING_INDEX)
            {
                ref var parentTryDsc = ref comp.compHndBBtab[parentTryIndex];
                region._parent = regions._tryRegions[parentTryDsc.ebdID];
            }
        }

        var traits = regions.GetBlockBitVecTraits();

        foreach (var block in comp.Blocks)
        {
            // Catchret dispatch belongs to the try whose header is returned by
            // the handler descriptor, including mutually protecting clauses.
            if (block.Kind is BBJ_EHCATCHRET)
            {
                assert(block.hasHndIndex);
                var dispatchingTryBlock = comp.ehGetDsc(block.HndIndex).ebdTryBeg;
                var dispatchingTryRegion = regions.GetTryRegionByHeader(dispatchingTryBlock);
                assert(dispatchingTryRegion is not null);
                dispatchingTryRegion.SetRequiresRuntimeResumption();
            }

            if (!block.hasTryIndex)
            {
                continue;
            }

            var tryIndex = block.TryIndex;
            ref var dsc = ref comp.compHndBBtab[tryIndex];
            if (!includeHandlerBlocks && !BasicBlock.sameHndRegion(block, dsc.ebdTryBeg))
            {
                continue;
            }

            var region = regions._tryRegions[dsc.ebdID];
            assert(region is not null);

            // An unreachable block's DFS index is meaningless. Track it only
            // in its innermost try, before ancestor membership or entry work.
            var isUnreachable = dfsTree is not null ? !dfsTree.Contains(block) : block.bbPreds is null;
            if (isUnreachable && (block != dsc.ebdTryBeg) && !block.HasFlag(BBF_THROW_HELPER))
            {
                region._unreachableBlocks.Add(block);
                continue;
            }

            while (region is not null)
            {
                BitVecOps.AddElemD(traits, region._blocks, regions.GetBlockIndex(block));

                foreach (var edge in block.PredEdges)
                {
                    var predBlock = edge.SourceBlock;
                    if (predBlock.Kind is BBJ_EHCATCHRET)
                    {
                        continue;
                    }

                    if (comp.bbInTryRegions(tryIndex, predBlock))
                    {
                        continue;
                    }

                    // The descriptor stays the innermost descriptor as we
                    // walk ancestors; only the membership query index changes.
                    if (block == dsc.ebdTryBeg)
                    {
                        region.AddEntryEdge(edge);
                        continue;
                    }

                    JITDUMP($"Unexpected try region entry edge from {FMT_BB(predBlock.bbNum)} to {FMT_BB(block.bbNum)}\n");
                    regions.SetHasSideEntry();
                }

                region = region._parent;
                if (region is not null)
                {
                    // A nested funclet is in the parent's IL range, but not
                    // in the parent's code body when handlers are excluded.
                    if (!includeHandlerBlocks && !BasicBlock.sameHndRegion(block, region.EhDsc.ebdTryBeg))
                    {
                        break;
                    }

                    tryIndex = comp.ehGetIndex(region.EhDsc);
                }
            }
        }

        return regions;
    }

#if DEBUG
    public static void Dump(FlowGraphTryRegions regions)
    {
        if (regions.NumTryRegions() == 0)
        {
            jitprintf("No try regions in this method\n");
        }
        else
        {
            jitprintf($"{regions.NumTryRegions()} try regions:\n");

            // Iterate stable EH IDs, not EH table order; deleted IDs are gaps.
            foreach (var region in regions._tryRegions)
            {
                if (region is not null)
                {
                    FlowGraphTryRegion.Dump(region);
                    jitprintf("\n");
                }
            }
        }
    }
#endif
}
