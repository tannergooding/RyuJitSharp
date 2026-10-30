// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed class FlowGraphTryRegion
{
    private readonly FlowGraphTryRegions _regions;
    private readonly EHblkDsc[] _ehTable;
    private readonly ushort _ehIndex;
    private readonly List<FlowEdge> _entryEdges = [];
    private bool _requiresRuntimeResumption;
    internal FlowGraphTryRegion? _parent;
    internal readonly BitVec _blocks;
    internal readonly List<BasicBlock> _unreachableBlocks = [];

    internal FlowGraphTryRegion(ushort ehIndex, FlowGraphTryRegions regions)
    {
        _regions = regions;
        // Keep the native descriptor's backing slot, not a struct snapshot or
        // an EH ID that would lose the descriptor's reference identity.
        _ehTable = regions.GetCompiler().compHndBBtab;
        _ehIndex = ehIndex;
        _blocks = BitVecOps.MakeEmpty(regions.GetBlockBitVecTraits());
    }

    internal ref EHblkDsc EhDsc => ref _ehTable[_ehIndex];

    internal void SetRequiresRuntimeResumption()
    {
        _requiresRuntimeResumption = true;
    }

    internal bool IsMutualProtectWith(FlowGraphTryRegion other) => EHblkDsc.ebdIsSameTry(EhDsc, other.EhDsc);

    internal void AddEntryEdge(FlowEdge edge)
    {
        _entryEdges.Add(edge);
    }

    public uint NumBlocks() => (uint)BitVecOps.Count(_regions.GetBlockBitVecTraits(), _blocks);

    public bool HasCatchHandler() => EhDsc.HasCatchHandler;

    public ReadOnlySpan<FlowEdge> EntryEdges() => CollectionsMarshal.AsSpan(_entryEdges);

    public ReadOnlySpan<BasicBlock> UnreachableBlocks() => CollectionsMarshal.AsSpan(_unreachableBlocks);

    public bool RequiresRuntimeResumption() => _requiresRuntimeResumption;

    public bool CanEnumerateInReversePostOrder() => _regions.GetDfsTree() is not null;

    // Unlike loops, try regions enumerate their regular block bit vector directly.
    public BasicBlockVisit VisitTryRegionBlocksReversePostOrder(Func<BasicBlock, BasicBlockVisit> func)
    {
        var visitor = new BasicBlockDelegateVisitor(func);
        return VisitTryRegionBlocksReversePostOrder(ref visitor);
    }

    public BasicBlockVisit VisitTryRegionBlocksReversePostOrder<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IBasicBlockVisitor<TVisitor>
    {
        assert(CanEnumerateInReversePostOrder());
        var dfsTree = _regions.GetDfsTree();
        assert(dfsTree is not null);
        var traits = _regions.GetBlockBitVecTraits();
        var adapter = new TryRegionBlockVisitorAdapter<TVisitor>(ref visitor, dfsTree);
        var result = BitVecOps.VisitBitsReverse(traits, _blocks, ref adapter);

        return result ? BasicBlockVisit.Continue : BasicBlockVisit.Abort;
    }

    private ref struct TryRegionBlockVisitorAdapter<TVisitor> : IBitSetVisitor<TryRegionBlockVisitorAdapter<TVisitor>>
        where TVisitor : struct, IBasicBlockVisitor<TVisitor>
    {
        private ref TVisitor _visitor;
        private readonly FlowGraphDfsTree _dfsTree;

        public TryRegionBlockVisitorAdapter(ref TVisitor visitor, FlowGraphDfsTree dfsTree)
        {
            _visitor = ref visitor;
            _dfsTree = dfsTree;
        }

        public bool Visit(int index)
        {
            assert((uint)index < (uint)_dfsTree.PostOrderCount);
            return _visitor.Visit(_dfsTree.GetPostOrder(index)) is BasicBlockVisit.Continue;
        }
    }

    public static void Dump(FlowGraphTryRegion region)
    {
        var regions = region._regions;
        var comp = regions.GetCompiler();
        var regionNum = comp.ehGetIndex(region.EhDsc);
        jitprintf($"EH#{regionNum:D2}: {region.NumBlocks()} blocks");

        if (!regions.TryRegionsIncludeHandlerBlocks())
        {
            jitprintf(" [excluding handler blocks]");
        }

        if (region._parent is FlowGraphTryRegion parent)
        {
            var parentRegionNum = comp.ehGetIndex(parent.EhDsc);
            jitprintf($" [ in EH#{parentRegionNum:D2}]:");
        }
        else
        {
            jitprintf(" [outermost]:");
        }

        if (region.CanEnumerateInReversePostOrder())
        {
            jitprintf(" [rpo]:");
            _ = region.VisitTryRegionBlocksReversePostOrder(block => {
                jitprintf($" {FMT_BB(block.bbNum)}");
                return BasicBlockVisit.Continue;
            });
        }
        else
        {
            jitprintf(" [bbNum]:");
            _ = BitVecOps.VisitBits(regions.GetBlockBitVecTraits(), region._blocks, index => {
                jitprintf($" {FMT_BB(index)}");
                return true;
            });
        }

        jitprintf(" [entries]: ");
        foreach (var edge in region.EntryEdges())
        {
            var predBlock = edge.SourceBlock;
            var succBlock = edge.DestinationBlock;
            jitprintf($" {FMT_BB(predBlock.bbNum)}->{FMT_BB(succBlock.bbNum)}");
            if (predBlock.HasFlag(BBF_ASYNC_RESUMPTION))
            {
                jitprintf("[async]");
            }
            if (predBlock.HasFlag(BBF_CATCH_RESUMPTION))
            {
                jitprintf("[catch]");
            }
        }
    }
}
