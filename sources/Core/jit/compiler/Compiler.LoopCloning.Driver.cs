// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.BBKinds;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optLoopComplexityExceeds(FlowGraphNaturalLoop loop, uint limit)
    {
        var complexity = 0u;
        return loop.VisitLoopBlocks(block => {
            assert(limit >= complexity);
            var exceeded = block.ComplexityExceeds(this, limit - complexity, _ => {
                complexity++;
                return 1;
            });
            return exceeded ? BasicBlockVisit.Abort : BasicBlockVisit.Continue;
        }) is BasicBlockVisit.Abort;
    }

    private bool optCloningHeuristic(FlowGraphNaturalLoop loop, LoopCloneContext context)
    {
        var minimum = JitConfig.JitCloneLoopsMinPerCallRatio / 100.0;
        if (minimum <= 0)
        {
            return true;
        }
        var options = context.GetLoopOptInfo(loop.Index)!;
        if (options.Count == 0)
        {
            return false;
        }

        var headerWeight = loop.Header.getBBWeight(this) / BB_UNITY_WEIGHT;
        double benefit = 0;
        foreach (var option in options)
        {
            var (cycles, checkBlock) = option switch
            {
                LcJaggedArrayOptInfo array => (2.0, array.ArrIndex.UseBlock),
                LcSpanOptInfo span => (2.0, span.SpanIndex.UseBlock),
                LcMdArrayOptInfo => (3.0, (BasicBlock?)null),
                LcTypeTestOptInfo type => (3.0, type.Block),
                LcMethodAddrTestOptInfo method => (3.0, method.Block),
                _ => (0.0, (BasicBlock?)null),
            };
            var weight = checkBlock is null ? headerWeight :
                checkBlock.getBBWeight(this) / BB_UNITY_WEIGHT;
            benefit += cycles * weight;
        }

        double cost = 0;
        _ = loop.VisitLoopBlocks(block => {
            uint nodes = 0;
            _ = block.ComplexityExceeds(this, uint.MaxValue, _ => {
                nodes++;
                return 1;
            });
            var blockWeight = block.getBBWeight(this) / BB_UNITY_WEIGHT;
            var normalized = headerWeight > 0 ? Math.Min(1.0, blockWeight / headerWeight) : 1.0;
            cost += normalized * nodes;
            return BasicBlockVisit.Continue;
        });
        if (cost <= 0)
        {
            return false;
        }

        var ratio = benefit / cost;
        if (ratio < minimum)
        {
            JITDUMP(FormattableString.Invariant(
                $"L{loop.Index:D2} rejected by cloning heuristic: PerCallRatio={ratio:F6} < threshold={minimum:F6} (benefit={benefit:F6}, weightedCost={cost:F6})\n"));
            return false;
        }
        return true;
    }

    private BasicBlock optInsertLoopChoiceConditions(LoopCloneContext context, FlowGraphNaturalLoop loop,
        BasicBlock slowPreheader, BasicBlock insertAfter)
    {
        JITDUMP($"Inserting loop L{loop.Index:D2} loop choice conditions\n");
        var loopNum = loop.Index;
        var total = context.GetConditions(loopNum)!.Count;
        if (context.HasBlockConditions(loopNum))
        {
            foreach (var level in context.GetBlockConditions(loopNum))
            {
                total += level.Count;
            }
        }
        assert(total > 0);

        if (context.HasBlockConditions(loopNum))
        {
            var levels = context.GetBlockConditions(loopNum);
            for (var i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                JITDUMP($"Adding loop L{loop.Index:D2} level {i} block conditions\n    ");
#if DEBUG
                if (verbose)
                {
                    context.PrintBlockLevelConditions(i, level);
                }
#endif
                insertAfter = context.CondToStmtInBlock(this, level, slowPreheader, insertAfter, total);
            }
        }
        JITDUMP($"Adding loop L{loop.Index:D2} cloning conditions\n    ");
#if DEBUG
        if (verbose)
        {
            context.PrintConditions(loopNum);
        }
#endif
        JITDUMP("\n");
        return context.CondToStmtInBlock(this, context.GetConditions(loopNum)!,
            slowPreheader, insertAfter, total);
    }

    private void optExtendEnclosingEHRegions(ushort enclosingRegion, BasicBlock beforeSlowPreheader,
        BasicBlock slowPreheader)
    {
        for (var index = checked((ushort)(enclosingRegion - 1)); index < compHndBBtabCount; index++)
        {
            ref var clause = ref ehGetDsc(index);
            if (clause.ebdTryLast == beforeSlowPreheader)
            {
                fgSetTryEnd(ref clause, slowPreheader);
            }
            if (clause.ebdHndLast == beforeSlowPreheader)
            {
                fgSetHndEnd(ref clause, slowPreheader);
            }
        }
    }

    private void optCloneLoop(FlowGraphNaturalLoop loop, LoopCloneContext context)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf("\nCloning ");
            FlowGraphNaturalLoop.Dump(loop);
        }
#endif
        var withEH = true;
#if DEBUG
        withEH = JitConfig.JitCloneLoopsWithEH > 0;
#endif
        assert(loop.EntryEdges.Length == 1);
        var preheader = loop.EntryEdge(0).SourceBlock;
        JITDUMP("Create new preheader block for fast loop\n");
        var fastPreheader = fgNewBBafter(BBJ_ALWAYS, preheader, extendRegion: true);
        JITDUMP($"Adding {FMT_BB(fastPreheader.bbNum)} after {FMT_BB(preheader.bbNum)}\n");
        fastPreheader.inheritWeight(preheader);
        assert(preheader.Kind is BBJ_ALWAYS && preheader.Target == loop.Header);
        var oldEdge = preheader.TargetEdge;
        fgReplacePred(oldEdge, fastPreheader);
        fastPreheader.TargetEdge = oldEdge;
        JITDUMP($"Replace {FMT_BB(preheader.bbNum)} -> {FMT_BB(loop.Header.bbNum)} with " +
            $"{FMT_BB(fastPreheader.bbNum)} -> {FMT_BB(loop.Header.bbNum)}\n");

        var bottom = loop.GetLexicallyBottomMostBlock();
        var beforeSlowPreheader = bottom;
        var enclosingRegion = ehGetMostNestedRegionIndex(preheader, out var inTry);
        if (!BasicBlock.sameEHRegion(beforeSlowPreheader, preheader))
        {
            beforeSlowPreheader = fgFindInsertPoint(enclosingRegion, inTry, fastPreheader,
                endBlk: null, nearBlk: bottom, jumpBlk: null, runRarely: false);
        }

        var extendRegion = BasicBlock.sameEHRegion(beforeSlowPreheader, preheader);
        JITDUMP("Create unique preheader for slow path loop\n");
        var slowPreheader = fgNewBBafter(BBJ_ALWAYS, beforeSlowPreheader, extendRegion);
        JITDUMP($"Adding {FMT_BB(slowPreheader.bbNum)} after {FMT_BB(beforeSlowPreheader.bbNum)}\n");
        slowPreheader.inheritWeight(preheader);
        slowPreheader.scaleBBWeight(LoopCloneContext.SlowPathWeightScaleFactor);
        if (!extendRegion)
        {
            slowPreheader.copyEHRegion(preheader);
            if (enclosingRegion != 0)
            {
                optExtendEnclosingEHRegions(enclosingRegion, beforeSlowPreheader, slowPreheader);
            }
        }

        var map = new BlockToBlockMap();
        var insertion = slowPreheader;
        optDuplicateLoop(loop, ref insertion, map, LoopCloneContext.SlowPathWeightScaleFactor, withEH);
        _ = loop.VisitLoopBlocks(block => {
            block.scaleBBWeight(LoopCloneContext.FastPathWeightScaleFactor);
            return BasicBlockVisit.Continue;
        });
        optPerformStaticOptimizations(loop, context, dynamicPath: true);

        var slowHeader = map.GetValue(loop.Header);
        assert(slowPreheader.Kind is BBJ_ALWAYS && !slowPreheader.HasInitializedTarget);
        slowPreheader.TargetEdge = fgAddRefPred(slowHeader, slowPreheader);
        JITDUMP($"Adding {FMT_BB(slowPreheader.bbNum)} -> {FMT_BB(slowHeader.bbNum)}\n");
        var last = optInsertLoopChoiceConditions(context, loop, slowPreheader, preheader);
        assert(preheader.Kind is BBJ_ALWAYS);
        preheader.TargetEdge = fgAddRefPred(preheader.Next!, preheader);
        assert(last.Next == fastPreheader);
        var falseEdge = fgAddRefPred(fastPreheader, last);
        last.FalseEdge = falseEdge;
        falseEdge.Likelihood = Math.Max(0.0, 1.0 - last.TrueEdge.Likelihood);
    }

    public PhaseStatus optCloneLoops()
    {
        JITDUMP("\n*************** In optCloneLoops()\n");
        if (_loops is null)
        {
            throw new FatalJitException("Loop cloning requires discovered natural loops.");
        }
        if (_loops.NumLoops == 0)
        {
            JITDUMP("  No loops to clone\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
        if (!optLoopCloningEnabled())
        {
            JITDUMP("  Loop cloning disabled\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var context = new LoopCloneContext(_loops.NumLoops);
        if (!optObtainLoopCloningOpts(context))
        {
            JITDUMP("  No clonable loops\n");
            return PhaseStatus.MODIFIED_EVERYTHING;
        }

        var staticallyOptimized = 0;
        foreach (var loop in _loops.InReversePostOrder())
        {
            if (context.GetLoopOptInfo(loop.Index) is null)
            {
                continue;
            }
            if (!optDeriveLoopCloningConditions(loop, context) ||
                !optComputeDerefConditions(loop, context))
            {
                JITDUMP("> Conditions could not be obtained\n");
                context.CancelLoopOptInfo(loop.Index);
                continue;
            }
            context.EvaluateConditions(loop.Index, out var allTrue, out var anyFalse
#if DEBUG
                , verbose
#endif
            );
            if (anyFalse)
            {
                context.CancelLoopOptInfo(loop.Index);
            }
            else if (allTrue)
            {
                optPerformStaticOptimizations(loop, context, dynamicPath: false);
                staticallyOptimized++;
                context.CancelLoopOptInfo(loop.Index);
            }
            else if (JitConfig.JitCloneLoopsSizeLimit is var sizeLimit && sizeLimit >= 0 &&
                optLoopComplexityExceeds(loop, (uint)sizeLimit))
            {
                JITDUMP($"L{loop.Index:D2} exceeds cloning size limit {sizeLimit}\n");
                context.CancelLoopOptInfo(loop.Index);
            }
        }

        assert(Metrics.LoopsCloned == 0);
        foreach (var loop in _loops.InReversePostOrder())
        {
            if (context.GetLoopOptInfo(loop.Index) is null)
            {
                continue;
            }
            if (!optCloningHeuristic(loop, context))
            {
                Metrics.LoopsRejectedForInsufficientBenefit++;
                context.CancelLoopOptInfo(loop.Index);
                continue;
            }

            Metrics.LoopsCloned++;
            context.OptimizeConditions(loop.Index
#if DEBUG
                , verbose
#endif
            );
            context.OptimizeBlockConditions(loop.Index
#if DEBUG
                , verbose
#endif
            );
            optCloneLoop(loop, context);
        }

        if (Metrics.LoopsCloned > 0)
        {
            fgInvalidateDfsTree();
            _dfsTree = fgComputeDfs();
            _loops = FlowGraphNaturalLoops.Find(_dfsTree);
            if (optCanonicalizeLoops())
            {
                fgInvalidateDfsTree();
                _dfsTree = fgComputeDfs();
                _loops = FlowGraphNaturalLoops.Find(_dfsTree);
            }
            if (fgIsUsingProfileWeights)
            {
                JITDUMP($"optCloneLoops: Profile data needs to be propagated through new loops. " +
                    $"Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                fgPgoConsistent = false;
            }
        }
#if DEBUG
        if (verbose)
        {
            jitprintf($"Loops cloned: {Metrics.LoopsCloned}\n");
            jitprintf($"Loops statically optimized: {staticallyOptimized}\n");
            jitprintf("After loop cloning:\n");
            fgDispBasicBlocks(dumpTrees: true);
        }
#endif
        return PhaseStatus.MODIFIED_EVERYTHING;
    }
}
