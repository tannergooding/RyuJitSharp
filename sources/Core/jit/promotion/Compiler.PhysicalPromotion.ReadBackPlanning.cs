// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed unsafe partial class PhysicalPromotionReplaceVisitor
    {
        public void PrepareReadBacks()
        {
            var index = 0;
            var hasPlannedReadBackCandidates = false;
            foreach (var aggregate in _aggregates.Aggregates)
            {
                ref readonly var descriptor = ref _compiler.lvaGetDesc(aggregate.LclNum);
                for (var replacementIndex = 0; replacementIndex < aggregate.Replacements.Count; replacementIndex++)
                {
                    aggregate.Replacements[replacementIndex].ReadBackIndex = index++;
                    if (!hasPlannedReadBackCandidates && (descriptor.lvIsParam || descriptor.lvIsOSRLocal) &&
                        _liveness.IsReplacementLiveIn(_compiler.fgFirstBB!, aggregate.LclNum, replacementIndex))
                    {
                        hasPlannedReadBackCandidates = true;
                    }
                }
            }

            _readBackTraits = new BitVecTraits(_compiler, index);
            _pendingReadBacksByBlock = new BitVec[_dfsTree.PostOrderCount];
            _currentStructFields = new BitVec[_dfsTree.PostOrderCount];
            _processedBlocks = BitVecOps.MakeEmpty(_postOrderTraits);
            _requiresAlreadyReadBackOnEntry = BitVecOps.MakeEmpty(_postOrderTraits);
            _requiresReadBackOnExit = BitVecOps.MakeEmpty(_postOrderTraits);

            for (var blockIndex = 0; blockIndex < _dfsTree.PostOrderCount; blockIndex++)
            {
                var block = _dfsTree.GetPostOrder(blockIndex);
                if (_compiler.bbIsHandlerBeg(block))
                {
                    BitVecOps.AddElemD(_postOrderTraits, _requiresAlreadyReadBackOnEntry, block.bbPostorderNum);
                }

                _ = block.VisitRegularSuccs(_compiler, successor => {
                    if (successor.bbPostorderNum >= block.bbPostorderNum)
                    {
                        // Includes irreducible backedge targets, whose incoming state must be
                        // settled before their predecessors are visited later in reverse postorder.
                        BitVecOps.AddElemD(_postOrderTraits, _requiresAlreadyReadBackOnEntry, successor.bbPostorderNum);
                    }

                    return BasicBlockVisit.Continue;
                });
            }

            // Replacement does not change the CFG. Planning and materialization share these boundaries.
            for (var blockIndex = 0; blockIndex < _dfsTree.PostOrderCount; blockIndex++)
            {
                if (MustMaterializeReadBacks(_dfsTree.GetPostOrder(blockIndex)))
                {
                    BitVecOps.AddElemD(_postOrderTraits, _requiresReadBackOnExit, blockIndex);
                }
            }

            if (hasPlannedReadBackCandidates)
            {
                PlanReadBacks();
            }
        }

        // This is a profitability model, not a correctness analysis. Uses materialize pending
        // fields and definitions end incoming values. Replacement keeps its exact transitions,
        // including partial writes and EH boundaries, and inserts any remaining readbacks.
        private void PlanReadBacks()
        {
            var entry = _compiler.fgFirstBB!;
            var placementDfs = _dfsTree;
            var domTree = _compiler._domTree;
            var pendingOut = BitVecOps.MakeEmpty(_postOrderTraits);
            var sites = BitVecOps.MakeEmpty(_postOrderTraits);

            foreach (var aggregate in _aggregates.Aggregates)
            {
                ref readonly var descriptor = ref _compiler.lvaGetDesc(aggregate.LclNum);
                if (!descriptor.lvIsParam && !descriptor.lvIsOSRLocal)
                {
                    continue;
                }

                for (var replacementIndex = 0; replacementIndex < aggregate.Replacements.Count; replacementIndex++)
                {
                    var replacement = aggregate.Replacements[replacementIndex];
                    if (!_liveness.IsReplacementLiveIn(entry, aggregate.LclNum, replacementIndex))
                    {
                        continue;
                    }

                    BitVecOps.ClearD(_postOrderTraits, pendingOut);
                    BitVecOps.ClearD(_postOrderTraits, sites);
                    var hasReconciliation = false;
                    for (var blockIndex = _dfsTree.PostOrderCount; blockIndex > 0; blockIndex--)
                    {
                        var block = _dfsTree.GetPostOrder(blockIndex - 1);
                        if (!_liveness.IsReplacementLiveIn(block, aggregate.LclNum, replacementIndex))
                        {
                            continue;
                        }

                        var pending = block == entry;
                        if ((block != entry) &&
                            !BitVecOps.IsMember(_postOrderTraits, _requiresAlreadyReadBackOnEntry, block.bbPostorderNum))
                        {
                            var anyPending = false;
                            var allPending = true;
                            foreach (var edge in block.PredEdges)
                            {
                                var predecessor = edge.SourceBlock;
                                if (!_dfsTree.Contains(predecessor))
                                {
                                    continue;
                                }

                                var predPending = BitVecOps.IsMember(_postOrderTraits, pendingOut,
                                    predecessor.bbPostorderNum);
                                anyPending |= predPending;
                                allPending &= predPending;
                            }

                            pending = anyPending && allPending;
                            if (anyPending && !allPending)
                            {
                                hasReconciliation = true;
                                foreach (var edge in block.PredEdges)
                                {
                                    var predecessor = edge.SourceBlock;
                                    if (_dfsTree.Contains(predecessor) &&
                                        BitVecOps.IsMember(_postOrderTraits, pendingOut, predecessor.bbPostorderNum))
                                    {
                                        BitVecOps.AddElemD(_postOrderTraits, sites, predecessor.bbPostorderNum);
                                        BitVecOps.RemoveElemD(_postOrderTraits, pendingOut, predecessor.bbPostorderNum);
                                    }
                                }
                            }
                        }

                        if (pending && _liveness.IsReplacementUsed(block, aggregate.LclNum, replacementIndex))
                        {
                            BitVecOps.AddElemD(_postOrderTraits, sites, block.bbPostorderNum);
                            pending = false;
                        }

                        if (pending && _liveness.IsReplacementDefined(block, aggregate.LclNum, replacementIndex))
                        {
                            pending = false;
                        }

                        if (pending && _liveness.IsReplacementLiveOut(block, aggregate.LclNum, replacementIndex))
                        {
                            if (BitVecOps.IsMember(_postOrderTraits, _requiresReadBackOnExit, block.bbPostorderNum))
                            {
                                BitVecOps.AddElemD(_postOrderTraits, sites, block.bbPostorderNum);
                            }
                            else
                            {
                                BitVecOps.AddElemD(_postOrderTraits, pendingOut, block.bbPostorderNum);
                            }
                        }
                    }

                    if (!hasReconciliation || (BitVecOps.Count(_postOrderTraits, sites) < 2))
                    {
                        continue;
                    }

                    if (domTree is null)
                    {
                        if (entry.bbPostorderNum + 1 != _dfsTree.PostOrderCount)
                        {
                            // Before global morph, DFS may retain the original OSR entry and a
                            // disconnected merged return. The unique entry's subtree is a prefix.
                            placementDfs = new FlowGraphDfsTree(_compiler, _dfsTree.GetPostOrder(),
                                entry.bbPostorderNum + 1, _dfsTree.HasCycle, _dfsTree.IsProfileAware);
                        }

                        domTree = FlowGraphDominatorTree.Build(placementDfs);
                        if (placementDfs == _dfsTree)
                        {
                            _compiler._domTree = domTree;
                        }
                    }

                    BasicBlock? common = null;
                    weight_t oldWeight = 0;
                    var iterator = new BitVecOps.Iter(_postOrderTraits, sites);
                    uint siteIndex = 0;
                    while (iterator.NextElem(ref siteIndex))
                    {
                        var block = _dfsTree.GetPostOrder(checked((int)siteIndex));
                        if (!placementDfs.Contains(block))
                        {
                            common = null;
                            break;
                        }

                        common = common is null ? block : domTree.Intersect(common, block);
                        oldWeight += block.getBBWeight(_compiler);
                    }

                    if (common is null)
                    {
                        continue;
                    }

                    var commonWeight = common.getBBWeight(_compiler);
                    // Partitioned sites save no dynamic work; commoning can extend live ranges.
                    if ((commonWeight >= oldWeight) ||
                        fgProfileWeightsEqual(commonWeight, oldWeight, oldWeight * 1e-6))
                    {
                        continue;
                    }

                    replacement.ReadBackPlacement = common;
                    JITDUMP($"Planning common readback for {BitVecOps.Count(_postOrderTraits, sites)} " +
                        $"estimated sites V{aggregate.LclNum:D2}.[{replacement.Offset:D3}.." +
                        $"{replacement.Offset + replacement.AccessType.Size:D3}) -> V{replacement.LclNum:D2} " +
                        $"in {FMT_BB(common.bbNum)} (weight {FMT_WT(commonWeight)}, " +
                        $"previous total {FMT_WT(oldWeight)})\n");
                }
            }
        }
    }
}
