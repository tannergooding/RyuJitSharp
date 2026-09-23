// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

// Reconstruct a separate flow model because instrumentation's pseudo-edges are not CFG edges.
// Non-tree counts come from the schema (missing probes mean zero); tree counts are unknown.
// Conservation determines a block's weight once all incoming or outgoing counts are known,
// then determines its last unknown incoming/outgoing edge. Propagation normalizes real successors.
public sealed unsafe class EfficientEdgeCountReconstructor : SpanningTreeVisitor
{
    private readonly Compiler _compiler;
    private uint _blocks;
    private uint _edges;
    private uint _unknownBlocks;
    private uint _unknownEdges;
    private uint _zeroEdges;
    private readonly Dictionary<int, BasicBlock> _keyToBlockMap = [];
    private readonly Dictionary<EdgeKey, Edge> _edgeKeyToEdgeMap = [];
    private bool _badcode;
    private bool _mismatch;
    private bool _negativeCount;
    private bool _failedToConverge;
    private bool _allWeightsZero = true;
    private bool _entryWeightZero;

    private readonly record struct EdgeKey(int SourceKey, int TargetKey)
    {
        public EdgeKey(BasicBlock source, BasicBlock target)
            : this(EfficientEdgeCountBlockToKey(source), EfficientEdgeCountBlockToKey(target))
        {
        }

        public override int GetHashCode() => SourceKey ^ (TargetKey << 16);
    }

    internal sealed class Edge(BasicBlock source, BasicBlock target)
    {
        internal weight_t Weight;
        internal readonly BasicBlock SourceBlock = source;
        internal readonly BasicBlock TargetBlock = target;
        internal Edge? NextOutgoingEdge;
        internal Edge? NextIncomingEdge;
        internal bool WeightKnown;
        internal bool IsPseudoEdge;
    }

    public sealed class BlockInfo
    {
        internal weight_t Weight;
        internal Edge? IncomingEdges;
        internal Edge? OutgoingEdges;
        internal int IncomingUnknown;
        internal int OutgoingUnknown;
        internal bool WeightKnown;
    }

    public EfficientEdgeCountReconstructor(Compiler compiler)
    {
        _compiler = compiler;
    }

    private static BlockInfo BlockToInfo(BasicBlock block)
    {
        assert(block.bbSparseCountInfo is not null);

        return block.bbSparseCountInfo;
    }

    private static void SetBlockInfo(BasicBlock block, BlockInfo info)
    {
        assert(block.bbSparseCountInfo is null);
        block.bbSparseCountInfo = info;
    }

    public override void Badcode() => _badcode = true;

    public void NegativeCount() => _negativeCount = true;

    public void Mismatch() => _mismatch = true;

    public void FailedToConverge() => _failedToConverge = true;

    public void EntryWeightZero() => _entryWeightZero = true;

    public bool IsGood => !(_entryWeightZero || _negativeCount);

    public override void VisitBlock(BasicBlock block)
    {
    }

    public override void VisitTreeEdge(BasicBlock source, BasicBlock target)
    {
        var key = new EdgeKey(source, target);

        if (_edgeKeyToEdgeMap.ContainsKey(key))
        {
            JITDUMP($"Did not expect tree edge {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)} to be present in the schema (key {key.SourceKey:x8}, {key.TargetKey:x8})\n");
            Mismatch();

            return;
        }

        var edge = new Edge(source, target);
        _edges++;
        _unknownEdges++;
        var sourceInfo = BlockToInfo(source);
        edge.NextOutgoingEdge = sourceInfo.OutgoingEdges;
        sourceInfo.OutgoingEdges = edge;
        sourceInfo.OutgoingUnknown++;
        var targetInfo = BlockToInfo(target);
        edge.NextIncomingEdge = targetInfo.IncomingEdges;
        targetInfo.IncomingEdges = edge;
        targetInfo.IncomingUnknown++;
        JITDUMP($" ... unknown edge {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)}\n");
    }

    public override void VisitNonTreeEdge(BasicBlock source, BasicBlock target, EdgeKind kind)
    {
        var sourceInfo = BlockToInfo(source);

        if (!_edgeKeyToEdgeMap.TryGetValue(new EdgeKey(source, target), out var edge))
        {
            // Absent non-tree edges carry zero flow.
            JITDUMP($"Schema is missing non-tree edge {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)}, will presume zero\n");
            edge = new Edge(source, target) { WeightKnown = true };
            _edges++;
            _zeroEdges++;
        }

        edge.NextOutgoingEdge = sourceInfo.OutgoingEdges;
        sourceInfo.OutgoingEdges = edge;
        var targetInfo = BlockToInfo(target);
        edge.NextIncomingEdge = targetInfo.IncomingEdges;
        targetInfo.IncomingEdges = edge;
        edge.IsPseudoEdge = kind is EdgeKind.Pseudo;
        JITDUMP($" ... {(edge.IsPseudoEdge ? "pseudo " : "known  ")} edge {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)}\n");
    }

    public void Prepare()
    {
#if DEBUG
        uint nReturns = 0;
        uint nZeroReturns = 0;

#endif
        foreach (var block in _compiler.Blocks)
        {
            var key = EfficientEdgeCountBlockToKey(block);
            assert(!_keyToBlockMap.ContainsKey(key));
            _keyToBlockMap[key] = block;
            SetBlockInfo(block, new BlockInfo());
            _blocks++;
            _unknownBlocks++;

#if DEBUG
            if (block.Kind is BBJ_RETURN)
            {
                nReturns++;
            }
#endif
        }

        for (var iSchema = 0; iSchema < _compiler.fgPgoSchemaCount; iSchema++)
        {
            ref var schemaEntry = ref _compiler.fgPgoSchema[iSchema];

            if (schemaEntry.InstrumentationKind is not (ICorJitInfo.PgoInstrumentationKind.EdgeIntCount or ICorJitInfo.PgoInstrumentationKind.EdgeLongCount))
            {
                continue;
            }

            if (!_keyToBlockMap.TryGetValue(schemaEntry.ILOffset, out var sourceBlock))
            {
                JITDUMP($"Could not find source block for schema entry {iSchema} (IL offset/key {schemaEntry.ILOffset:x8})\n");
            }

            if (!_keyToBlockMap.TryGetValue(schemaEntry.Other, out var targetBlock))
            {
                JITDUMP($"Could not find target block for schema entry {iSchema} (IL offset/key {schemaEntry.ILOffset:x8})\n");
            }

            if ((sourceBlock is null) || (targetBlock is null))
            {
                Mismatch();
                continue;
            }

            var profileCount = schemaEntry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.EdgeIntCount
                ? *(uint*)(_compiler.fgPgoData + schemaEntry.Offset)
                : *(ulong*)(_compiler.fgPgoData + schemaEntry.Offset);

#if DEBUG
            if ((JitConfig.JitRandomEdgeCounts != 0) && (nReturns > 0))
            {
                var strategy = _compiler.impInlineRoot._inlineStrategy;
                assert(strategy is not null);
                var random = strategy.GetRandom(JitConfig.JitRandomEdgeCounts);
                var isReturn = sourceBlock.Kind is BBJ_RETURN;
                var rval = random.NextDouble();

                // Match the native count distribution, retaining at least one nonzero return.
                if ((rval <= 0.5) && (!isReturn || (nZeroReturns < (nReturns - 1))))
                {
                    profileCount = 0;

                    if (isReturn)
                    {
                        nZeroReturns++;
                    }
                }
                else if (rval <= 0.85)
                {
                    profileCount = (ulong)random.Next(1, 101);
                }
                else if (rval <= 0.96)
                {
                    profileCount = (ulong)random.Next(101, 10001);
                }
                else if (rval <= 0.995)
                {
                    profileCount = (ulong)random.Next(10001, 100001);
                }
                else
                {
                    profileCount = (ulong)random.Next(100001, 1000001);
                }
            }

#endif
            var weight = (weight_t)profileCount;
            _allWeightsZero &= profileCount == 0;
            var edge = new Edge(sourceBlock, targetBlock) { WeightKnown = true, Weight = weight };
            JITDUMP($"... adding known edge {FMT_BB(sourceBlock.bbNum)} -> {FMT_BB(targetBlock.bbNum)}: weight {FMT_WT(weight)}\n");
            var edgeKey = new EdgeKey(schemaEntry.ILOffset, schemaEntry.Other);
            assert(!_edgeKeyToEdgeMap.ContainsKey(edgeKey));
            _edgeKeyToEdgeMap[edgeKey] = edge;
            _edges++;
        }
    }

    public void Solve()
    {
        if (_badcode || _mismatch || _allWeightsZero)
        {
            JITDUMP($"... not solving because of the {(_badcode ? "badcode" : _allWeightsZero ? "zero counts" : "mismatch")}\n");

            return;
        }

        if (_compiler.opts.IsOSR)
        {
            assert(_compiler.fgOSREntryBB is not null);
            assert(_compiler.fgFirstBB is not null);
            var key = new EdgeKey(_compiler.fgOSREntryBB, _compiler.fgFirstBB);

            if (!_edgeKeyToEdgeMap.TryGetValue(key, out var edge))
            {
                // Account for original-method invocations that transferred at the patchpoint.
                JITDUMP("Method is OSR, adding pseudo edge from osr entry to first block\n");
                edge = new Edge(_compiler.fgOSREntryBB, _compiler.fgFirstBB) { WeightKnown = true, Weight = 1.0 };
                _edges++;
                _edgeKeyToEdgeMap[key] = edge;
                VisitNonTreeEdge(_compiler.fgOSREntryBB, _compiler.fgFirstBB, EdgeKind.Pseudo);
            }
            else
            {
                assert(edge.WeightKnown);
            }
        }

        uint nPasses = 0;
        const uint nLimit = 10;
        JITDUMP($"\nSolver: {_blocks} blocks, {_unknownBlocks} unknown; {_edges} edges, {_unknownEdges} unknown, {_zeroEdges} zero\n");

        while ((_unknownBlocks > 0) && (nPasses < nLimit))
        {
            nPasses++;
            JITDUMP($"\nPass [{nPasses}]: {_unknownBlocks} unknown blocks, {_unknownEdges} unknown edges\n");

            // Reverse layout order approximates reverse postorder over the spanning tree.
            for (var block = _compiler.fgLastBB; block is not null; block = block.Prev)
            {
                var info = BlockToInfo(block);

                if (!info.WeightKnown)
                {
                    JITDUMP($"{FMT_BB(block.bbNum)}: {info.IncomingUnknown} incoming unknown, {info.OutgoingUnknown} outgoing unknown\n");
                    var weight = BB_ZERO_WEIGHT;
                    var weightKnown = false;

                    if (info.IncomingUnknown == 0)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)}: all incoming edge weights known, summing...\n");

                        for (var edge = info.IncomingEdges; edge is not null; edge = edge.NextIncomingEdge)
                        {
                            if (!edge.WeightKnown)
                            {
                                JITDUMP($"... odd, expected {FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} to have known weight\n");
                            }

                            assert(edge.WeightKnown);
                            JITDUMP($"  {FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} has weight {FMT_WT(edge.Weight)}\n");
                            weight += edge.Weight;
                        }

                        JITDUMP($"{FMT_BB(block.bbNum)}: all incoming edge weights known, sum is {FMT_WT(weight)}\n");
                        weightKnown = true;
                    }
                    else if (info.OutgoingUnknown == 0)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)}: all outgoing edge weights known, summing...\n");

                        for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
                        {
                            if (!edge.WeightKnown)
                            {
                                JITDUMP($"... odd, expected {FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} to have known weight\n");
                            }

                            assert(edge.WeightKnown);
                            JITDUMP($"  {FMT_BB(edge.SourceBlock.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} has weight {FMT_WT(edge.Weight)}\n");
                            weight += edge.Weight;
                        }

                        JITDUMP($"{FMT_BB(block.bbNum)}: all outgoing edge weights known, sum is {FMT_WT(weight)}\n");
                        weightKnown = true;
                    }

                    if (weightKnown)
                    {
                        info.Weight = weight;
                        info.WeightKnown = true;
                        assert(_unknownBlocks > 0);
                        _unknownBlocks--;
                    }
                }

                if (!info.WeightKnown)
                {
                    continue;
                }

                if (info.IncomingUnknown == 1)
                {
                    var weight = BB_ZERO_WEIGHT;
                    Edge? resolvedEdge = null;

                    for (var edge = info.IncomingEdges; edge is not null; edge = edge.NextIncomingEdge)
                    {
                        if (edge.WeightKnown)
                        {
                            weight += edge.Weight;
                        }
                        else
                        {
                            assert(resolvedEdge is null);
                            resolvedEdge = edge;
                        }
                    }

                    assert(resolvedEdge is not null);
                    weight = info.Weight - weight;
                    JITDUMP($"{FMT_BB(resolvedEdge.SourceBlock.bbNum)} -> {FMT_BB(resolvedEdge.TargetBlock.bbNum)}: target block weight and all other incoming edge weights known, so weight is {FMT_WT(weight)}\n");

                    if (weight < 0)
                    {
                        // Scalable or racing counters can yield inconsistent counts.
                        NegativeCount();
                        weight = info.Weight * ProfileSynthesis.epsilon;
                        JITDUMP($" .... weight was negative, setting it to {FMT_WT(weight)}\n");
                    }

                    resolvedEdge.Weight = weight;
                    resolvedEdge.WeightKnown = true;
                    assert(BlockToInfo(resolvedEdge.SourceBlock).OutgoingUnknown > 0);
                    BlockToInfo(resolvedEdge.SourceBlock).OutgoingUnknown--;
                    info.IncomingUnknown--;
                    assert(_unknownEdges > 0);
                    _unknownEdges--;
                }

                if (info.OutgoingUnknown == 1)
                {
                    var weight = BB_ZERO_WEIGHT;
                    Edge? resolvedEdge = null;

                    for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
                    {
                        if (edge.WeightKnown)
                        {
                            weight += edge.Weight;
                        }
                        else
                        {
                            assert(resolvedEdge is null);
                            resolvedEdge = edge;
                        }
                    }

                    assert(resolvedEdge is not null);
                    weight = info.Weight - weight;
                    JITDUMP($"{FMT_BB(resolvedEdge.SourceBlock.bbNum)} -> {FMT_BB(resolvedEdge.TargetBlock.bbNum)}: source block weight and all other outgoing edge weights known, so weight is {FMT_WT(weight)}\n");

                    if (weight < 0)
                    {
                        NegativeCount();
                        weight = info.Weight * ProfileSynthesis.epsilon;
                        JITDUMP($" .... weight was negative, setting it to {FMT_WT(weight)}\n");
                    }

                    resolvedEdge.Weight = weight;
                    resolvedEdge.WeightKnown = true;
                    info.OutgoingUnknown--;
                    assert(BlockToInfo(resolvedEdge.TargetBlock).IncomingUnknown > 0);
                    BlockToInfo(resolvedEdge.TargetBlock).IncomingUnknown--;
                    assert(_unknownEdges > 0);
                    _unknownEdges--;
                }
            }
        }

        if (_unknownBlocks != 0)
        {
            JITDUMP($"\nSolver: failed to converge in {nPasses} passes, {_unknownBlocks} blocks and {_unknownEdges} edges remain unsolved\n");
            FailedToConverge();

            return;
        }

        JITDUMP($"\nSolver: converged in {nPasses} passes\n");
        assert(_compiler.fgFirstBB is not null);

        if (BlockToInfo(_compiler.fgFirstBB).Weight == BB_ZERO_WEIGHT)
        {
            assert(!_allWeightsZero);
            JITDUMP("\nSolver: entry block weight is zero\n");
            EntryWeightZero();
        }
    }

    public void Propagate()
    {
        assert(!_failedToConverge);

        if (_badcode || _mismatch || _failedToConverge || _allWeightsZero)
        {
            _compiler.fgPgoHaveWeights = false;
            _compiler.fgPgoFailReason = _badcode ? "PGO data available, but IL was malformed"
                : _mismatch ? "PGO data available, but IL did not match"
                : _failedToConverge ? "PGO data available, but solver did not converge"
                : "PGO data available, profile data was all zero";
            JITDUMP($"... discarding profile count data: {_compiler.fgPgoFailReason}\n");

            return;
        }

        foreach (var block in _compiler.Blocks)
        {
            var info = BlockToInfo(block);
            assert(info.WeightKnown);
            block.setBBProfileWeight(info.Weight);
            var nSucc = block.NumSucc;

            if (nSucc == 0)
            {
                continue;
            }

            if (_compiler.opts.IsOSR && (block == _compiler.fgOSREntryBB))
            {
                PropagateOSREntryEdges(block, info, nSucc);
            }
            else
            {
                PropagateEdges(block, info, nSucc);
            }

            MarkInterestingBlocks(block, info);
        }
    }

    private void PropagateOSREntryEdges(BasicBlock block, BlockInfo info, int nSucc)
    {
        Edge? pseudoEdge = null;
        uint nEdges = 0;
        var successorWeight = BB_ZERO_WEIGHT;

        for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
        {
            if (edge.IsPseudoEdge)
            {
                assert(pseudoEdge is null);
                pseudoEdge = edge;
                continue;
            }

            successorWeight += edge.Weight;
            nEdges++;
        }

        if ((block != _compiler.fgFirstBB) && (pseudoEdge is null))
        {
            assert(_compiler.fgFirstBB is not null);
            JITDUMP($"Missing special OSR pseudo-edge from {FMT_BB(block.bbNum)}-> {FMT_BB(_compiler.fgFirstBB.bbNum)}\n");
            assert(false, "Missing special OSR pseudo-edge");
        }

        if ((nEdges != nSucc) || (info.Weight == BB_ZERO_WEIGHT) || (successorWeight == BB_ZERO_WEIGHT))
        {
            JITDUMP($"\nPropagate: OSR entry block {(nEdges != nSucc ? "has inaccurate flow model" : "has zero weight")}, setting outgoing likelihoods heuristically\n");
            var equalLikelihood = 1.0 / nSucc;

            foreach (var succEdge in block.Succs.Edges)
            {
                JITDUMP($"Setting likelihood of {FMT_BB(block.bbNum)} -> {FMT_BB(succEdge.DestinationBlock.bbNum)} to {FMT_WT(equalLikelihood)} (heur)\n");
                succEdge.Likelihood = equalLikelihood;
            }

            if ((info.Weight == BB_ZERO_WEIGHT) || (successorWeight == BB_ZERO_WEIGHT))
            {
                EntryWeightZero();
            }

            return;
        }

        assert(nEdges == nSucc);
        JITDUMP($"Normalizing OSR successor likelihoods with factor 1/{FMT_WT(successorWeight)}\n");

        for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
        {
            assert(block == edge.SourceBlock);

            if (edge == pseudoEdge)
            {
                continue;
            }

            assert(!edge.IsPseudoEdge);
            var flowEdge = _compiler.fgGetPredForBlock(edge.TargetBlock, block);
            assert(flowEdge is not null);

#if DEBUG
            assert(flowEdge.hasLikelihood);

#endif
            if (nEdges == 1)
            {
                JITDUMP($"Setting likelihood of {FMT_BB(block.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} to {FMT_WT(1.0)} (uniq)\n");
                flowEdge.Likelihood = 1.0;
                break;
            }

            var likelihood = edge.Weight / successorWeight;
            JITDUMP($"Setting likelihood of {FMT_BB(block.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} to {FMT_WT(likelihood)} (pgo)\n");
            flowEdge.Likelihood = likelihood;
        }
    }

    private void PropagateEdges(BasicBlock block, BlockInfo info, int nSucc)
    {
        Edge? pseudoEdge = null;
        uint nEdges = 0;
        var successorWeight = BB_ZERO_WEIGHT;

        for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
        {
            assert(pseudoEdge is null);

            if (edge.IsPseudoEdge)
            {
                pseudoEdge = edge;
                continue;
            }

            successorWeight += edge.Weight;
            nEdges++;
        }

        if (pseudoEdge is not null)
        {
            assert(nSucc == 1);
            assert(block == pseudoEdge.SourceBlock);
            assert(block.HasInitializedTarget);
            assert(block.TargetEdge.Likelihood == 1.0);

            return;
        }

        // Early EH flow can be incomplete; a zero-weight block also has no useful ratios.
        if ((nEdges != nSucc) || (info.Weight == BB_ZERO_WEIGHT) || (successorWeight == BB_ZERO_WEIGHT))
        {
            JITDUMP($"{FMT_BB(block.bbNum)} {(nEdges != nSucc ? "has inaccurate flow model" : "has zero weight")} , setting outgoing likelihoods heuristically\n");
            var equalLikelihood = 1.0 / nSucc;

            foreach (var succEdge in block.Succs.Edges)
            {
                JITDUMP($"Setting likelihood of {FMT_BB(block.bbNum)} -> {FMT_BB(succEdge.DestinationBlock.bbNum)} to {FMT_WT(equalLikelihood)} (heur)\n");
                succEdge.Likelihood = equalLikelihood;
            }

            return;
        }

        assert(nEdges == nSucc);
        JITDUMP($"Normalizing successor likelihoods with factor 1/{FMT_WT(successorWeight)}\n");

        for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
        {
            assert(block == edge.SourceBlock);
            var flowEdge = _compiler.fgGetPredForBlock(edge.TargetBlock, block);
            assert(flowEdge is not null);

            if (nEdges == 1)
            {
                assert(nSucc == 1);
                JITDUMP($"Setting likelihood of {FMT_BB(block.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} to {FMT_WT(1.0)} (uniq)\n");
                flowEdge.Likelihood = 1.0;
                break;
            }

            var likelihood = edge.Weight / successorWeight;
            JITDUMP($"Setting likelihood of {FMT_BB(block.bbNum)} -> {FMT_BB(edge.TargetBlock.bbNum)} to {FMT_WT(likelihood)} (pgo)\n");
            flowEdge.Likelihood = likelihood;
        }
    }

    private static void MarkInterestingBlocks(BasicBlock block, BlockInfo info)
    {
        if (block.Kind is BBJ_SWITCH)
        {
            MarkInterestingSwitches(block, info);
        }
    }

    private static void MarkInterestingSwitches(BasicBlock block, BlockInfo info)
    {
        assert(block.Kind is BBJ_SWITCH);
        // Dynamic PGO normally observes at least 30 calls. Peeling must also pay for
        // an extra branch and code size, hence the 55% minimum dominant fraction.
        const weight_t sufficientSamples = 30.0;
        const weight_t sufficientFraction = 0.55;

        if (info.Weight < sufficientSamples)
        {
            JITDUMP($"Switch in {FMT_BB(block.bbNum)} was hit {FMT_WT(info.Weight)} < {FMT_WT(sufficientSamples)} times, NOT checking for dominant edge\n");

            return;
        }

        JITDUMP($"Switch in {FMT_BB(block.bbNum)} was hit {FMT_WT(info.Weight)} >= {FMT_WT(sufficientSamples)} times, checking for dominant edge\n");
        Edge? dominantEdge = null;

        for (var edge = info.OutgoingEdges; edge is not null; edge = edge.NextOutgoingEdge)
        {
            if (!edge.WeightKnown)
            {
                JITDUMP("Found edge with unknown weight.\n");

                return;
            }

            if ((dominantEdge is null) || (edge.Weight > dominantEdge.Weight))
            {
                dominantEdge = edge;
            }
        }

        assert(dominantEdge is not null);
        var fraction = dominantEdge.Weight / info.Weight;

        if (fraction > 1.0)
        {
            fraction = 1.0;
        }

        if (fraction < sufficientFraction)
        {
            JITDUMP($"Maximum edge likelihood is {FMT_WT(fraction)} < {FMT_WT(sufficientFraction)}; not sufficient to trigger peeling)\n");

            return;
        }

        var jumpTab = block.SwitchTargets.Cases;
        var caseCount = jumpTab.Length;
        var dominantCase = caseCount;

        for (var i = 0; i < caseCount; i++)
        {
            var jumpTarget = jumpTab[i].DestinationBlock;

            if (jumpTarget == dominantEdge.TargetBlock)
            {
                if (dominantCase != caseCount)
                {
                    JITDUMP($"Both case {i} and {dominantCase} lead to {FMT_BB(jumpTarget.bbNum)}-- can't optimize\n");
                    dominantCase = caseCount;
                    break;
                }

                dominantCase = i;
            }
        }

        if (dominantCase == caseCount)
        {
            return;
        }

        if (block.SwitchTargets.HasDefaultCase && (dominantCase == caseCount - 1))
        {
            JITDUMP($"Default case {dominantCase} uniquely leads to target {FMT_BB(dominantEdge.TargetBlock.bbNum)} of dominant edge, so will be peeled already\n");

            return;
        }

        JITDUMP($"Non-default case {dominantCase} uniquely leads to target {FMT_BB(dominantEdge.TargetBlock.bbNum)} of dominant edge with likelihood {FMT_WT(fraction)}; marking for peeling\n");
        block.SwitchTargets.DominantCase = dominantCase;
    }
}
