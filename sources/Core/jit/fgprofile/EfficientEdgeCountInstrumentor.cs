// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgprofile.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Collections.Generic;
using static RyuJitSharp.Compiler;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using EdgeKind = RyuJitSharp.SpanningTreeVisitor.EdgeKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed unsafe class EfficientEdgeCountInstrumentor : Instrumentor
{
    // The native scratch field bbSparseProbeList is a void*; keep the same per-block
    // head insertion order without placing references to managed probes in native memory.
    private readonly Dictionary<BasicBlock, Probe> _probeHeads = [];
    private readonly bool _minimal;
    private int _blockCount;
    private int _probeCount;
    private int _edgeProbeCount;
    private bool _badcode;

    private sealed class Probe(BasicBlock source, BasicBlock target, Probe? next)
    {
        internal BasicBlock Source = source;
        internal BasicBlock Target = target;
        internal Probe? Next = next;
        internal int SchemaIndex = -1;
        internal EdgeKind Kind;
        internal Probe? Leader;
    }

    private sealed class ForwardingVisitor(EfficientEdgeCountInstrumentor owner) : SpanningTreeVisitor
    {
        public override void Badcode()
        {
            owner._badcode = true;
        }

        public override void VisitBlock(BasicBlock block)
        {
            owner._blockCount++;
            _ = owner._probeHeads.Remove(block);
        }

        public override void VisitTreeEdge(BasicBlock source, BasicBlock target)
        {
        }

        public override void VisitNonTreeEdge(BasicBlock source, BasicBlock target, EdgeKind kind)
        {
            switch (kind)
            {
                case EdgeKind.PostdominatesSource:
                case EdgeKind.Pseudo:
                    owner.NewProbe(source, source, target).Kind = EdgeKind.PostdominatesSource;
                    JITDUMP($"[{owner._probeCount - 1}] New probe for {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)} [source]\n");
                    break;

                case EdgeKind.DominatesTarget:
                    owner.NewProbe(source, source, target).Kind = EdgeKind.DominatesTarget;
                    JITDUMP($"[{owner._probeCount - 1}] New probe for {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)} [target]\n");
                    break;

                case EdgeKind.CriticalEdge:
                    owner.NewProbe(source, source, target).Kind = EdgeKind.CriticalEdge;
                    owner._edgeProbeCount++;
                    JITDUMP($"[{owner._probeCount - 1}] New probe for {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)} [edge]\n");
                    break;

                default:
                    assert(false);
                    break;
            }
        }
    }

    public EfficientEdgeCountInstrumentor(Compiler compiler, bool minimal) : base(compiler)
    {
        _minimal = minimal;
    }

    public override bool ShouldProcess(BasicBlock block) => block.HasFlag(BBF_IMPORTED);

    public override bool ShouldInstrument(BasicBlock block)
        => ShouldProcess(block) && (!_minimal || SchemaCountValue > 1);

    public override void Prepare(bool preImport)
    {
        if (preImport)
        {
            JITDUMP("\nEfficientEdgeCountInstrumentor: preparing for instrumentation\n");
            Compiler.WalkSpanningTree(new ForwardingVisitor(this));
            JITDUMP($"{_blockCount} blocks, {_probeCount} probes ({_edgeProbeCount} on critical edges)\n");
            return;
        }

        assert(!_badcode);
        SplitCriticalEdges();
        RelocateProbes();
    }

    private Probe NewProbe(BasicBlock block, BasicBlock source, BasicBlock target)
    {
        _ = _probeHeads.TryGetValue(block, out var head);
        var probe = new Probe(source, target, head);
        _probeHeads[block] = probe;
        _probeCount++;
        return probe;
    }

    private void NewRelocatedProbe(BasicBlock block, BasicBlock source, BasicBlock target, ref Probe? leader)
    {
        var probe = NewProbe(block, source, target);
        if (leader is null)
        {
            leader = probe;
            probe.Kind = EdgeKind.Leader;
        }
        else
        {
            probe.Kind = EdgeKind.Duplicate;
            probe.Leader = leader;
        }

        JITDUMP($"New {(probe.Kind is EdgeKind.Leader ? "leader" : "duplicate")} probe for " +
            $"{FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)} [reloc to {FMT_BB(block.bbNum)} ]\n");
    }

    private void SplitCriticalEdges()
    {
        if (_edgeProbeCount == 0)
        {
            return;
        }

        JITDUMP($"\nEfficientEdgeCountInstrumentor: splitting up to {_edgeProbeCount} critical edges\n");
        var edgesSplit = 0;
        var edgesIgnored = 0;

        foreach (var block in Compiler.Blocks)
        {
            _probeHeads.TryGetValue(block, out var head);
            if (!ShouldProcess(block))
            {
#if DEBUG
                for (var probe = head; probe is not null; probe = probe.Next)
                {
                    if (probe.Kind is EdgeKind.CriticalEdge)
                    {
                        edgesIgnored++;
                    }
                }
#endif
                continue;
            }

            for (var probe = head; probe is not null; probe = probe.Next)
            {
                var source = probe.Source;
                var target = probe.Target;
                BasicBlock? instrumentedBlock = null;

                switch (probe.Kind)
                {
                    case EdgeKind.PostdominatesSource:
                        instrumentedBlock = source;
                        break;

                    case EdgeKind.DominatesTarget:
                        instrumentedBlock = target;
                        break;

                    case EdgeKind.Relocated:
                        instrumentedBlock = block;
                        break;

                    case EdgeKind.CriticalEdge:
                    {
                        assert(block == source);
                        var found = false;
                        foreach (var succ in block.Succs)
                        {
                            if (target == succ)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                        {
                            instrumentedBlock = Compiler.fgSplitEdge(block, target);
                            instrumentedBlock.SetFlags(BBF_IMPORTED);
                            edgesSplit++;
                            var relocated = NewProbe(instrumentedBlock, source, target);
                            relocated.Kind = EdgeKind.Relocated;
                            JITDUMP($"New relocated probe for {FMT_BB(source.bbNum)} -> {FMT_BB(target.bbNum)} " +
                                $"[reloc to {FMT_BB(instrumentedBlock.bbNum)} ]\n");
                        }
                        else
                        {
                            JITDUMP($"Could not find {FMT_BB(block.bbNum)} -> {FMT_BB(target.bbNum)} edge to instrument\n");
                            JITDUMP(" -- assuming this edge was folded away by the importer\n");
                            instrumentedBlock = source;
                            edgesIgnored++;
                        }

                        probe.Kind = EdgeKind.Deleted;
                        break;
                    }

                    default:
                        assert(false);
                        break;
                }

                assert(instrumentedBlock is not null);
            }
        }

        assert(edgesSplit + edgesIgnored == _edgeProbeCount);
        if (edgesSplit > 0)
        {
            SetModifiedFlow();
        }
    }

    private void RelocateProbes()
    {
        if (!Compiler.opts.IsInstrumentedAndOptimized ||
            (Compiler.optMethodFlags & OMF_HAS_TAILCALL_SUCCESSOR) == 0)
        {
            return;
        }

        JITDUMP("Optimized + instrumented + potential tail calls --- preparing to relocate edge probes\n");
        var criticalPreds = new Stack<BasicBlock>();
        foreach (var block in Compiler.Blocks)
        {
            if (!ShouldProcess(block) || !block.HasFlag(BBF_TAILCALL_SUCCESSOR))
            {
                continue;
            }

            JITDUMP($"Return {FMT_BB(block.bbNum)} is successor of possible tail call\n");
            assert(block.Kind is BBJ_RETURN);
            _ = _probeHeads.TryGetValue(block, out var probe);
            assert(probe is not null && probe.Next is null && probe.Kind is EdgeKind.PostdominatesSource);
            probe.Kind = EdgeKind.Deleted;
            Probe? leader = null;
            criticalPreds.Clear();

            foreach (var pred in block.PredBlocks)
            {
                var succ = pred.UniqueSucc;
                if ((succ is null) || pred.isBBCallFinallyPairTail)
                {
                    JITDUMP($"{FMT_BB(pred.bbNum)} -> {FMT_BB(block.bbNum)} is critical edge\n");
                    criticalPreds.Push(pred);
                }
                else
                {
                    NewRelocatedProbe(pred, probe.Source, probe.Target, ref leader);
                    assert(pred.Kind is BBJ_ALWAYS && pred.Target == block);
                }
            }

            if (criticalPreds.Count > 0)
            {
                var intermediary = Compiler.fgNewBBbefore(BBJ_ALWAYS, block, extendRegion: true);
                intermediary.SetFlags(BBF_IMPORTED);
                var newEdge = Compiler.fgAddRefPred(block, intermediary);
                intermediary.TargetEdge = newEdge;
                NewRelocatedProbe(intermediary, probe.Source, probe.Target, ref leader);
                SetModifiedFlow();

                weight_t weight = 0;
                var allPredsHaveProfile = true;
                while (criticalPreds.Count > 0)
                {
                    var pred = criticalPreds.Pop();
                    Compiler.fgReplaceJumpTarget(pred, block, intermediary);
                    if (pred.hasProfileWeight)
                    {
                        var edge = Compiler.fgGetPredForBlock(intermediary, pred);
                        assert(edge is not null);
                        weight += edge.LikelyWeight;
                    }
                    else
                    {
                        allPredsHaveProfile = false;
                    }
                }

                if (allPredsHaveProfile)
                {
                    intermediary.setBBProfileWeight(weight);
                }
                else
                {
                    intermediary.inheritWeight(block);
                }
            }
        }
    }

    public override void BuildSchemaElements(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema)
    {
        _ = _probeHeads.TryGetValue(block, out var head);
        for (var probe = head; probe is not null; probe = probe.Next)
        {
            if (probe.Kind is EdgeKind.Duplicate or EdgeKind.Deleted)
            {
                continue;
            }

            assert(probe.SchemaIndex == -1);
            probe.SchemaIndex = schema.Count;
            schema.Add(new ICorJitInfo.PgoInstrumentationSchema {
                Count = BlockCountInstrumentor.CounterSlots,
                Other = EfficientEdgeCountBlockToKey(probe.Target),
                InstrumentationKind = Compiler.opts.compCollect64BitCounts
                    ? ICorJitInfo.PgoInstrumentationKind.EdgeLongCount
                    : ICorJitInfo.PgoInstrumentationKind.EdgeIntCount,
                ILOffset = EfficientEdgeCountBlockToKey(probe.Source),
                Offset = 0,
            });
            SchemaCountValue++;
        }
    }

    public override void Instrument(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema, byte* profileMemory)
    {
        var interlocked = JitConfig.JitInterlockedProfiling > 0;
        var scalable = JitConfig.JitScalableProfiling > 0;
        JITDUMP($"Using {(!interlocked && !scalable ? "unsynchronized"
            : interlocked && scalable ? "both interlocked and scalable"
            : interlocked ? "interlocked" : "scalable")} probes\n");

        _probeHeads.TryGetValue(block, out var head);
        for (var probe = head; probe is not null; probe = probe.Next)
        {
            if (probe.Kind is EdgeKind.Deleted)
            {
                continue;
            }

            var index = probe.Kind is EdgeKind.Duplicate ? probe.Leader!.SchemaIndex : probe.SchemaIndex;
            assert(index >= 0 && index < schema.Count);
            var entry = schema[index];
            assert(entry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.EdgeIntCount
                or ICorJitInfo.PgoInstrumentationKind.EdgeLongCount);
            var counter = profileMemory + entry.Offset;

#if DEBUG
            if (JitConfig.JitPropagateSynthesizedCountsToProfileData > 0)
            {
                var edge = Compiler.fgGetPredForBlock(probe.Target, probe.Source);
                if (edge is not null)
                {
                    if (entry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.EdgeIntCount)
                    {
                        *(uint*)counter = ConvertSynthesizedCount32(edge.LikelyWeight);
                    }
                    else
                    {
                        *(ulong*)counter = ConvertSynthesizedCount64(edge.LikelyWeight);
                    }
                }

                return;
            }
#endif
            BasicBlock instrumentedBlock;
            switch (probe.Kind)
            {
                case EdgeKind.PostdominatesSource:
                    instrumentedBlock = probe.Source;
                    break;

                case EdgeKind.DominatesTarget:
                    instrumentedBlock = probe.Target;
                    break;

                case EdgeKind.Relocated:
                case EdgeKind.Leader:
                case EdgeKind.Duplicate:
                    instrumentedBlock = block;
                    break;

                default:
                    assert(false);
                    throw new System.InvalidOperationException("Critical edge probe was not split");
            }

            var countType = entry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.EdgeIntCount
                ? TYP_INT : TYP_LONG;
            var increment = BlockCountInstrumentor.CreateCounterIncrement(Compiler, counter, countType);
            Compiler.fgInsertStmtAtBeg(instrumentedBlock, Compiler.gtNewStmt(increment));
            if (probe.Kind is not EdgeKind.Duplicate)
            {
                InstrCountValue++;
            }
        }
    }
}
