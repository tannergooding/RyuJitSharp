// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed class ProfileSynthesis
{
    public const weight_t epsilon = 0.001;
    private const weight_t initialBlendFactor = 0.05;
    private const weight_t cappedLikelihood = 0.999;
    private const weight_t returnLikelihood = 0.2;
    private const weight_t ilNextLikelihood = 0.52;
    private const weight_t loopBackLikelihood = 0.9;
    private const weight_t loopExitLikelihood = 0.9;
    private const weight_t throwLikelihood = 0;
    private const int maxSolverIterations = 50;
    private const weight_t maxCount = 1e12;

    private readonly Compiler _comp;
    private readonly FlowGraphDfsTree _dfsTree;
    private readonly FlowGraphNaturalLoops _loops;
    private readonly BasicBlock _entryBlock;
    private readonly weight_t[] _cyclicProbabilities;
    private weight_t _blendFactor = initialBlendFactor;
    private weight_t _loopExitLikelihood = loopExitLikelihood;
    private weight_t _loopBackLikelihood = loopBackLikelihood;
    private weight_t _returnLikelihood = returnLikelihood;
    private readonly int _improperLoopHeaders;
    private int _cappedCyclicProbabilities;
    private bool _hasInfiniteLoop;
    private bool _approximate;
    private bool _overflow;

    private ProfileSynthesis(Compiler compiler)
    {
        _comp = compiler;
        // Synthesis runs both before and after method-entry canonicalization.
        var entryBlock = compiler.opts.IsOSR && (compiler.fgEntryBB is not null) ? compiler.fgEntryBB : compiler.fgFirstBB;
        assert(entryBlock is not null);
        _entryBlock = entryBlock;
        var dfsTree = compiler._dfsTree;
        var loops = compiler._loops;
        if (dfsTree is null)
        {
            dfsTree = compiler.fgComputeDfs();
            loops = FlowGraphNaturalLoops.Find(dfsTree);
        }
        assert(loops is not null);
        _dfsTree = dfsTree;
        _loops = loops;
        _improperLoopHeaders = loops.ImproperLoopHeaders;
        _cyclicProbabilities = new weight_t[loops.NumLoops];
    }

    public static PhaseStatus AdjustThrowEdgeLikelihoods(Compiler compiler)
    {
        var dfsTree = compiler._dfsTree;
        assert(dfsTree is not null);
        var traits = dfsTree.PostOrderTraits();
        var willThrow = BitVecOps.MakeEmpty(traits);

        void TweakLikelihoods(BasicBlock block)
        {
            assert(block.Kind is BBJ_COND);
            FlowEdge throwEdge;
            FlowEdge normalEdge;
            if (BitVecOps.IsMember(traits, willThrow, block.TrueTarget.bbPostorderNum))
            {
                throwEdge = block.TrueEdge;
                normalEdge = block.FalseEdge;
            }
            else
            {
                throwEdge = block.FalseEdge;
                normalEdge = block.TrueEdge;
            }
            throwEdge.Likelihood = throwLikelihood;
            normalEdge.Likelihood = 1.0 - throwLikelihood;
        }

        var modified = false;
        for (var i = 0; i < dfsTree.PostOrderCount; i++)
        {
            var block = dfsTree.GetPostOrder(i);
            if (block.Kind is BBJ_THROW)
            {
                JITDUMP($"{FMT_BB(block.bbNum)} will throw.\n");
                BitVecOps.AddElemD(traits, willThrow, i);
            }
            else if ((block.UniqueSucc is BasicBlock uniqueSucc) &&
                BitVecOps.IsMember(traits, willThrow, uniqueSucc.bbPostorderNum))
            {
                JITDUMP($"{FMT_BB(block.bbNum)} flows into a throw block.\n");
                BitVecOps.AddElemD(traits, willThrow, i);
            }
            else
            {
                var anyPathThrows = false;
                var allPathsThrow = true;
                foreach (var succBlock in block.Succs)
                {
                    if (BitVecOps.IsMember(traits, willThrow, succBlock.bbPostorderNum))
                    {
                        anyPathThrows = true;
                    }
                    else
                    {
                        allPathsThrow = false;
                    }
                }
                if (anyPathThrows)
                {
                    if (allPathsThrow)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} flows into a throw block.\n");
                        BitVecOps.AddElemD(traits, willThrow, i);
                    }
                    else if ((block.Kind is BBJ_COND) && block.TrueEdge.isHeuristicBased)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} can flow into a throw block.\n");
                        assert(block.FalseEdge.isHeuristicBased);
                        TweakLikelihoods(block);
                        modified = true;
                    }
                }
            }
        }

        if (modified && compiler.fgIsUsingProfileWeights)
        {
            JITDUMP($"Modified edge likelihoods. Data {(compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            compiler.fgPgoConsistent = false;
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private void AssignLikelihoods()
    {
        JITDUMP("Assigning edge likelihoods based on heuristics\n");
        foreach (var block in _comp.Blocks)
        {
            switch (block.Kind)
            {
                case BBJ_THROW:
                case BBJ_RETURN:
                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                {
                    break;
                }
                case BBJ_CALLFINALLY:
                case BBJ_ALWAYS:
                case BBJ_CALLFINALLYRET:
                case BBJ_LEAVE:
                case BBJ_EHCATCHRET:
                case BBJ_EHFILTERRET:
                {
                    AssignLikelihoodJump(block);
                    break;
                }
                case BBJ_COND:
                {
                    block.TrueEdge.isHeuristicBased = true;
                    block.FalseEdge.isHeuristicBased = true;
                    AssignLikelihoodCond(block);
                    break;
                }
                case BBJ_SWITCH:
                {
                    AssignLikelihoodSwitch(block);
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }
    }

    private void AssignLikelihoodJump(BasicBlock block)
    {
        block.TargetEdge.Likelihood = 1.0;
    }

    private void AssignLikelihoodCond(BasicBlock block)
    {
        var trueEdge = block.TrueEdge;
        var falseEdge = block.FalseEdge;
        if (trueEdge == falseEdge)
        {
            assert(trueEdge.DupCount == 2);
            trueEdge.Likelihood = 1.0;
            return;
        }

        var trueTarget = trueEdge.DestinationBlock;
        var falseTarget = falseEdge.DestinationBlock;
        var isTrueThrow = trueTarget.Kind is BBJ_THROW;
        var isFalseThrow = falseTarget.Kind is BBJ_THROW;
        if (isTrueThrow != isFalseThrow)
        {
            if (isTrueThrow)
            {
                trueEdge.Likelihood = throwLikelihood;
                falseEdge.Likelihood = 1.0 - throwLikelihood;
            }
            else
            {
                trueEdge.Likelihood = 1.0 - throwLikelihood;
                falseEdge.Likelihood = throwLikelihood;
            }
            return;
        }

        var isTrueEdgeBackEdge = _loops.IsLoopBackEdge(trueEdge);
        var isFalseEdgeBackEdge = _loops.IsLoopBackEdge(falseEdge);
        if (isTrueEdgeBackEdge != isFalseEdgeBackEdge)
        {
            if (isTrueEdgeBackEdge)
            {
                JITDUMP($"{FMT_BB(block.bbNum)}->{FMT_BB(trueTarget.bbNum)} is loop back edge\n");
                trueEdge.Likelihood = _loopBackLikelihood;
                falseEdge.Likelihood = 1.0 - _loopBackLikelihood;
            }
            else
            {
                JITDUMP($"{FMT_BB(block.bbNum)}->{FMT_BB(falseTarget.bbNum)} is loop back edge\n");
                trueEdge.Likelihood = 1.0 - _loopBackLikelihood;
                falseEdge.Likelihood = _loopBackLikelihood;
            }
            return;
        }

        // Prefer staying in the loop; native does not distribute this bias across exits.
        var isTrueEdgeExitEdge = _loops.IsLoopExitEdge(trueEdge);
        var isFalseEdgeExitEdge = _loops.IsLoopExitEdge(falseEdge);
        if (isTrueEdgeExitEdge != isFalseEdgeExitEdge)
        {
            if (isTrueEdgeExitEdge)
            {
                JITDUMP($"{FMT_BB(block.bbNum)}->{FMT_BB(trueTarget.bbNum)} is loop exit edge\n");
                trueEdge.Likelihood = 1.0 - _loopExitLikelihood;
                falseEdge.Likelihood = _loopExitLikelihood;
            }
            else
            {
                JITDUMP($"{FMT_BB(block.bbNum)}->{FMT_BB(falseTarget.bbNum)} is loop exit edge\n");
                trueEdge.Likelihood = _loopExitLikelihood;
                falseEdge.Likelihood = 1.0 - _loopExitLikelihood;
            }
            return;
        }

        var isJumpReturn = trueTarget.Kind is BBJ_RETURN;
        var isNextReturn = falseTarget.Kind is BBJ_RETURN;
        if (isJumpReturn != isNextReturn)
        {
            if (isJumpReturn)
            {
                trueEdge.Likelihood = _returnLikelihood;
                falseEdge.Likelihood = 1.0 - _returnLikelihood;
            }
            else
            {
                trueEdge.Likelihood = 1.0 - _returnLikelihood;
                falseEdge.Likelihood = _returnLikelihood;
            }
            return;
        }

        trueEdge.Likelihood = 1.0 - ilNextLikelihood;
        falseEdge.Likelihood = ilNextLikelihood;
    }

    private void AssignLikelihoodSwitch(BasicBlock block)
    {
        var count = block.SwitchTargets.Cases.Length;
        assert(count != 0);
        var probability = count != 0 ? 1 / (weight_t)count : 0;
        foreach (var edge in block.Succs.Edges)
        {
            edge.Likelihood = probability * edge.DupCount;
        }
    }

    private weight_t SumOutgoingLikelihoods(BasicBlock block, List<weight_t>? likelihoods = null)
    {
        weight_t sum = 0;
        likelihoods?.Clear();
        foreach (var edge in block.Succs.Edges)
        {
            var likelihood = edge.Likelihood;
            likelihoods?.Add(likelihood);
            sum += likelihood;
        }

        return sum;
    }

    private void RepairLikelihoods()
    {
        JITDUMP("Repairing inconsistent or missing edge likelihoods\n");
        foreach (var block in _comp.Blocks)
        {
            switch (block.Kind)
            {
                case BBJ_THROW:
                case BBJ_RETURN:
                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                {
                    break;
                }
                case BBJ_CALLFINALLY:
                case BBJ_ALWAYS:
                case BBJ_CALLFINALLYRET:
                case BBJ_LEAVE:
                case BBJ_EHCATCHRET:
                case BBJ_EHFILTERRET:
                {
                    AssignLikelihoodJump(block);
                    break;
                }
                case BBJ_COND:
                case BBJ_SWITCH:
                {
                    var sum = SumOutgoingLikelihoods(block);
                    var consistent = Compiler.fgProfileWeightsEqual(sum, 1.0, epsilon);
                    var zero = Compiler.fgProfileWeightsEqual(block.bbWeight, 0.0, epsilon);
                    if (consistent && !zero)
                    {
                        break;
                    }
                    JITDUMP($"Repairing likelihoods in {FMT_BB(block.bbNum)}");
                    if (!consistent)
                    {
                        JITDUMP($"; existing likelihood sum: {FMT_WT(sum)}");
                    }
                    if (zero)
                    {
                        JITDUMP("; zero weight block");
                    }
                    JITDUMP("\n");
                    if (block.Kind is BBJ_COND)
                    {
                        AssignLikelihoodCond(block);
                    }
                    else
                    {
                        AssignLikelihoodSwitch(block);
                    }
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }
    }

    private void BlendLikelihoods()
    {
        JITDUMP("Blending existing likelihoods with heuristics\n");
        var likelihoods = new List<weight_t>();
        foreach (var block in _comp.Blocks)
        {
            switch (block.Kind)
            {
                case BBJ_THROW:
                case BBJ_RETURN:
                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                {
                    break;
                }
                case BBJ_CALLFINALLY:
                case BBJ_ALWAYS:
                case BBJ_CALLFINALLYRET:
                case BBJ_LEAVE:
                case BBJ_EHCATCHRET:
                case BBJ_EHFILTERRET:
                {
                    AssignLikelihoodJump(block);
                    break;
                }
                case BBJ_COND:
                case BBJ_SWITCH:
                {
                    var sum = SumOutgoingLikelihoods(block, likelihoods);
                    var unlikely = Compiler.fgProfileWeightsEqual(sum, 0.0, epsilon);
                    var zero = Compiler.fgProfileWeightsEqual(block.bbWeight, 0.0, epsilon);
                    if (block.Kind is BBJ_COND)
                    {
                        AssignLikelihoodCond(block);
                    }
                    else
                    {
                        AssignLikelihoodSwitch(block);
                    }
                    if (unlikely || zero)
                    {
                        JITDUMP($"{(unlikely ? "Existing likelihood" : "Block weight")} in {FMT_BB(block.bbNum)} was zero, using synthesized likelihoods\n");
                        break;
                    }
                    if (!Compiler.fgProfileWeightsEqual(sum, 1.0, epsilon))
                    {
                        var scale = 1.0 / sum;
                        JITDUMP($"Scaling old likelihoods in {FMT_BB(block.bbNum)} by {FMT_WT(scale)}\n");
                        for (var i = 0; i < likelihoods.Count; i++)
                        {
                            likelihoods[i] *= scale;
                        }
                    }

                    JITDUMP($"Blending likelihoods in {FMT_BB(block.bbNum)} with blend factor {FMT_WT(_blendFactor)} \n");
                    var index = 0;
                    foreach (var edge in block.Succs.Edges)
                    {
                        var newLikelihood = edge.Likelihood;
                        var oldLikelihood = likelihoods[index++];
                        edge.Likelihood = ((1.0 - _blendFactor) * oldLikelihood) + (_blendFactor * newLikelihood);
                        JITDUMP($"{FMT_BB(block.bbNum)} -> {FMT_BB(edge.DestinationBlock.bbNum)} was {FMT_WT(oldLikelihood)} now {FMT_WT(edge.Likelihood)}\n");
                    }
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }
    }

    private void ClearLikelihoods()
    {
        foreach (var block in _comp.Blocks)
        {
            foreach (var edge in block.Succs.Edges)
            {
                edge.clearLikelihood();
            }
        }
    }

    private void ReverseLikelihoods()
    {
#if DEBUG
        JITDUMP("Reversing likelihoods\n");
        var likelihoods = new List<weight_t>();
        foreach (var block in _comp.Blocks)
        {
            _ = SumOutgoingLikelihoods(block, likelihoods);
            if (likelihoods.Count < 2)
            {
                continue;
            }
            likelihoods.Reverse();
            var index = 0;
            foreach (var edge in block.Succs.Edges)
            {
                edge.Likelihood = likelihoods[index++];
            }
        }
#endif
    }

    private void RandomizeLikelihoods()
    {
#if DEBUG
        JITDUMP("Randomizing likelihoods\n");
        var likelihoods = new List<weight_t>();
        var random = new CLRRandom(_comp.info.compMethodHash());
        foreach (var block in _comp.Blocks)
        {
            var count = block.NumSucc;
            likelihoods.Clear();
            weight_t sum = 0;
            for (var i = 0; i < count; i++)
            {
                var likelihood = random.NextDouble();
                likelihoods.Add(likelihood);
                sum += likelihood;
            }
            var index = 0;
            foreach (var edge in block.Succs.Edges)
            {
                edge.Likelihood = likelihoods[index++] / sum;
            }
        }
#endif
    }

    private void ComputeCyclicProbabilities()
    {
        foreach (var loop in _loops.InPostOrder())
        {
            ComputeCyclicProbabilities(loop);
        }
    }

    private void ComputeCyclicProbabilities(FlowGraphNaturalLoop loop)
    {
        var hasExit = false;
        var hasLikelyExit = false;
        foreach (var exitEdge in loop.ExitEdges)
        {
            hasExit = true;
            if (exitEdge.Likelihood > 0)
            {
                hasLikelyExit = true;
                break;
            }
        }
        if (!hasLikelyExit)
        {
            JITDUMP($"Loop headed by {FMT_BB(loop.Header.bbNum)} has {(hasExit ? "no likely" : "no")} exit edges (is infinite)\n");
            _hasInfiniteLoop = true;
        }
        _ = loop.VisitLoopBlocks(static block => {
            block.bbWeight = 0.0;
            return BasicBlockVisit.Continue;
        });

        // Inner-loop gains are already known, so one RPO pass suffices for a natural loop.
        _ = loop.VisitLoopBlocksReversePostOrder(block => {
            if (block == loop.Header)
            {
                JITDUMP($"ccp: {FMT_BB(block.bbNum)} :: 1.0 (header)\n");
                block.bbWeight = 1.0;
            }
            else
            {
                var nestedLoop = _loops.GetLoopByHeader(block);
                if (nestedLoop is not null)
                {
                    assert(_cyclicProbabilities[nestedLoop.Index] != 0);
                    var newWeight = 0.0;
                    foreach (var edge in nestedLoop.EntryEdges)
                    {
                        newWeight += edge.LikelyWeight;
                    }
                    newWeight *= _cyclicProbabilities[nestedLoop.Index];
                    block.bbWeight = newWeight;
                    JITDUMP($"ccp: {FMT_BB(block.bbNum)} :: {FMT_WT(newWeight)} (nested header)\n");
                }
                else
                {
                    var newWeight = 0.0;
                    foreach (var edge in block.PredEdges)
                    {
                        // Unreachable predecessors may flow into a reachable loop.
                        if (loop.ContainsBlock(edge.SourceBlock))
                        {
                            newWeight += edge.LikelyWeight;
                        }
                    }
                    block.bbWeight = newWeight;
                    JITDUMP($"ccp: {FMT_BB(block.bbNum)} :: {FMT_WT(newWeight)}\n");
                }
            }
            return BasicBlockVisit.Continue;
        });

        weight_t cyclicWeight = 0;
        var capped = false;
        foreach (var edge in loop.BackEdges)
        {
            JITDUMP($"ccp backedge {FMT_BB(edge.SourceBlock.bbNum)} ({FMT_WT(edge.SourceBlock.bbWeight)}) -> {FMT_BB(loop.Header.bbNum)} likelihood {FMT_WT(edge.Likelihood)}\n");
            cyclicWeight += edge.LikelyWeight;
        }
        if (cyclicWeight > cappedLikelihood)
        {
            JITDUMP($"Cyclic weight {FMT_WT(cyclicWeight)} > {FMT_WT(cappedLikelihood)}(cap) -- will reduce to cap\n");
            capped = true;
            cyclicWeight = cappedLikelihood;
            _cappedCyclicProbabilities++;
        }
        // Despite the native name, this is the expected iteration count, not a probability.
        var cyclicProbability = 1.0 / (1.0 - cyclicWeight);
        JITDUMP($"For loop at {FMT_BB(loop.Header.bbNum)} cyclic weight is {FMT_WT(cyclicWeight)} cyclic probability is {FMT_WT(cyclicProbability)}{(capped ? " [capped]" : "")}{(loop.ContainsImproperHeader ? " [likely underestimated, (loop contains improper loop)]" : "")}\n");
        _cyclicProbabilities[loop.Index] = cyclicProbability;

        if (capped && (loop.ExitEdges.Length > 0))
        {
            weight_t cappedExitWeight = 0;
            foreach (var exitEdge in loop.ExitEdges)
            {
                var exitBlock = exitEdge.SourceBlock;
                var exitBlockWeight = exitBlock.bbWeight * cyclicProbability;
                var exitWeight = exitEdge.Likelihood * exitBlockWeight;
                cappedExitWeight += exitWeight;
                JITDUMP($"Exit from {FMT_BB(exitBlock.bbNum)} has weight {FMT_WT(exitWeight)}\n");
            }
            JITDUMP($"Total exit weight {FMT_WT(cappedExitWeight)}\n");
            if ((cappedExitWeight + epsilon) < 1.0)
            {
                var missingExitWeight = 1.0 - cappedExitWeight;
                JITDUMP($"Loop exit flow deficit from capping is {FMT_WT(missingExitWeight)}\n");
                var adjustedExit = false;
                // Match native's first eligible conditional exit, rather than spreading the deficit.
                foreach (var exitEdge in loop.ExitEdges)
                {
                    var exitBlock = exitEdge.SourceBlock;
                    var exitBlockWeight = exitBlock.bbWeight * cyclicProbability;
                    var currentExitWeight = exitEdge.Likelihood * exitBlockWeight;
                    if ((exitBlock.Kind is BBJ_COND) && (exitBlockWeight > (missingExitWeight + currentExitWeight)))
                    {
                        JITDUMP($"Will adjust likelihood of the exit edge from loop exit block {FMT_BB(exitBlock.bbNum)} to reflect capping; current likelihood is {FMT_WT(exitEdge.Likelihood)}\n");
                        var trueEdge = exitBlock.TrueEdge;
                        var falseEdge = exitBlock.FalseEdge;
                        var exitLikelihood = (missingExitWeight + currentExitWeight) / exitBlockWeight;
                        var continueLikelihood = 1.0 - exitLikelihood;
                        assert(exitLikelihood > exitEdge.Likelihood);
                        if (trueEdge == exitEdge)
                        {
                            trueEdge.Likelihood = exitLikelihood;
                            falseEdge.Likelihood = continueLikelihood;
                        }
                        else
                        {
                            assert(falseEdge == exitEdge);
                            trueEdge.Likelihood = continueLikelihood;
                            falseEdge.Likelihood = exitLikelihood;
                        }
                        adjustedExit = true;
                        JITDUMP($"New likelihood is  {FMT_WT(exitEdge.Likelihood)}\n");
                        break;
                    }
                }
                if (!adjustedExit)
                {
                    JITDUMP("Unable to find suitable exit to carry off capped flow\n");
                }
            }
            else
            {
                JITDUMP("Exit weight comparable or above 1.0, leaving as is\n");
            }
        }
    }

    private void ComputeBlockWeights()
    {
        JITDUMP("Computing block weights\n");
        var useSolver = true;
#if DEBUG
        useSolver = JitConfig.JitSynthesisUseSolver > 0;
#endif
        if (useSolver)
        {
            GaussSeidelSolver();
            return;
        }
        for (var i = _dfsTree.PostOrderCount; i != 0; i--)
        {
            ComputeBlockWeight(_dfsTree.GetPostOrder(i - 1));
        }
        _approximate = (_cappedCyclicProbabilities != 0) || (_improperLoopHeaders > 0);
    }

    private void ComputeBlockWeight(BasicBlock block)
    {
        var loop = _loops.GetLoopByHeader(block);
        var newWeight = block.bbWeight;
        var kind = "";
        if (loop is not null)
        {
            foreach (var edge in loop.EntryEdges)
            {
                if (BasicBlock.sameHndRegion(block, edge.SourceBlock))
                {
                    newWeight += edge.LikelyWeight;
                }
            }
            newWeight *= _cyclicProbabilities[loop.Index];
            kind = " (loop head)";
        }
        else
        {
            foreach (var edge in block.PredEdges)
            {
                if (BasicBlock.sameHndRegion(block, edge.SourceBlock))
                {
                    newWeight += edge.LikelyWeight;
                }
            }
        }
        block.setBBProfileWeight(newWeight);
        JITDUMP($"cbw{kind}: {FMT_BB(block.bbNum)} :: {FMT_WT(block.bbWeight)}\n");

        if (_comp.bbIsTryBeg(block))
        {
            ref var handler = ref _comp.ehGetBlockTryDsc(block);
            if (handler.HasFinallyHandler)
            {
                var finallyEntry = handler.ebdHndBeg;
                finallyEntry.setBBProfileWeight(newWeight);
                kind = " (finally)";
                JITDUMP($"cbw{kind}: {FMT_BB(finallyEntry.bbNum)} :: {FMT_WT(finallyEntry.bbWeight)}\n");
            }
        }
    }

    private void GaussSeidelSolver()
    {
        var countVector = new weight_t[_comp.fgBBNumMax + 1];
        var converged = false;
        weight_t relResidual = 0;
        weight_t oldRelResidual = 0;
        weight_t eigenvalue = 0;
        const weight_t stopRelResidual = 0.001;
        var dfs = _loops.DfsTree;
        var checkEntryExitWeight = true;
        var showDetails = false;
        var callFinalliesCreated = _comp.fgImportDone;
        JITDUMP($"Synthesis solver: flow graph has {_improperLoopHeaders} improper loop headers\n");

        // Natural-loop gains eliminate their cycles. Irreducible flow needs bounded iteration.
        var iterationLimit = _improperLoopHeaders > 0 ? maxSolverIterations : 1;
        var i = 0;
        for (; i < iterationLimit; i++)
        {
            BasicBlock? residualBlock = null;
            BasicBlock? relResidualBlock = null;
            weight_t residual = 0;
            relResidual = 0;
            weight_t entryWeight = 0;
            weight_t exitWeight = 0;

            for (var j = _dfsTree.PostOrderCount; j != 0; j--)
            {
                var block = dfs.GetPostOrder(j - 1);
                weight_t newWeight = 0;
                checkEntryExitWeight &= !block.hasTryIndex;
                if (block == _entryBlock)
                {
                    newWeight = block.bbWeight;
                    entryWeight = newWeight;
                }
                else
                {
                    ref var handler = ref _comp.ehGetBlockHndDsc(block);
                    if (!Unsafe.IsNullRef(in handler))
                    {
                        if (handler.HasFilter && (block == handler.ebdFilter))
                        {
                            newWeight = block.bbWeight;
                        }
                        else if (block == handler.ebdHndBeg)
                        {
                            newWeight = block.bbWeight;
                            if (!callFinalliesCreated && handler.HasFinallyHandler)
                            {
                                newWeight += countVector[handler.ebdTryBeg.bbNum];
                            }
                        }
                    }
                }

                if (block.bbPreds is not null)
                {
                    var loop = _loops.GetLoopByHeader(block);
                    if ((loop is not null) && !loop.ContainsImproperHeader)
                    {
                        foreach (var edge in loop.EntryEdges)
                        {
                            newWeight += edge.Likelihood * countVector[edge.SourceBlock.bbNum];
                        }
                        newWeight *= _cyclicProbabilities[loop.Index];
                    }
                    else
                    {
                        if ((loop is not null) && showDetails)
                        {
                            JITDUMP($" .. not using Cp for {FMT_BB(block.bbNum)}; loop contains improper header\n");
                        }
                        FlowEdge? selfEdge = null;
                        foreach (var edge in block.PredEdges)
                        {
                            var predBlock = edge.SourceBlock;
                            if (predBlock == block)
                            {
                                assert(selfEdge is null);
                                selfEdge = edge;
                                continue;
                            }
                            newWeight += edge.Likelihood * countVector[predBlock.bbNum];
                        }
                        if (selfEdge is not null)
                        {
                            var selfLikelihood = selfEdge.Likelihood;
                            if (selfLikelihood > cappedLikelihood)
                            {
                                _cappedCyclicProbabilities++;
                                selfLikelihood = cappedLikelihood;
                            }
                            newWeight /= 1.0 - selfLikelihood;
                        }
                    }
                }

                // Successive over-relaxation can produce negative counts near an eigenvalue of one.
                // Native therefore uses ordinary Gauss-Seidel with monotonically increasing counts.
                var oldWeight = countVector[block.bbNum];
                var change = newWeight - oldWeight;
                assert(change >= 0);
                var isExit = false;
                if (checkEntryExitWeight)
                {
                    if (block.Kind is BBJ_RETURN)
                    {
                        exitWeight += newWeight;
                        isExit = true;
                    }
                    else if ((block.Kind is BBJ_THROW) && !block.hasTryIndex)
                    {
                        exitWeight += newWeight;
                        isExit = true;
                    }
                }
                if (showDetails)
                {
                    JITDUMP($"iteration {i}: {FMT_BB(block.bbNum)} :: old {FMT_WT(oldWeight)} new {FMT_WT(newWeight)} change {FMT_WT(change)}{(isExit ? " [exit]" : "")}\n");
                }
                countVector[block.bbNum] = newWeight;
                var blockRelResidual = change / (oldWeight < 1e-12 ? 1e-12 : oldWeight);
                if ((relResidualBlock is null) || (blockRelResidual > relResidual))
                {
                    relResidual = blockRelResidual;
                    relResidualBlock = block;
                }
                if ((residualBlock is null) || (change > residual))
                {
                    residual = change;
                    residualBlock = block;
                }
                if (newWeight >= maxCount)
                {
                    JITDUMP($"count overflow in {FMT_BB(block.bbNum)}: {FMT_WT(newWeight)}\n");
                    _overflow = true;
                }
            }

            if (_improperLoopHeaders == 0)
            {
                converged = !_comp.fgImportDone || Compiler.fgProfileWeightsConsistent(entryWeight, exitWeight);
                break;
            }
            if (checkEntryExitWeight)
            {
                var entryExitResidual = weight_t.Abs(entryWeight - exitWeight);
                JITDUMP($"Entry weight {FMT_WT(entryWeight)} exit weight {FMT_WT(exitWeight)} residual {FMT_WT(entryExitResidual)}\n");
                var entryExitRelResidual = entryExitResidual / entryWeight;
                assert(entryExitRelResidual >= 0);
                if (entryExitRelResidual > relResidual)
                {
                    relResidual = entryExitRelResidual;
                    relResidualBlock = _entryBlock;
                }
            }
            assert(residualBlock is not null);
            assert(relResidualBlock is not null);
            JITDUMP($"iteration {i}: max residual is at {FMT_BB(residualBlock.bbNum)} : {FMT_WT(residual)}\n");
            JITDUMP($"iteration {i}: max rel residual is at {FMT_BB(relResidualBlock.bbNum)} : {FMT_WT(relResidual)}\n");
            if (relResidual < stopRelResidual)
            {
                converged = true;
                break;
            }
            if (_overflow)
            {
                break;
            }
            if ((i > 3) && (oldRelResidual > 0))
            {
                eigenvalue = relResidual / oldRelResidual;
                JITDUMP($" eigenvalue {FMT_WT(eigenvalue)}");
            }
            JITDUMP("\n");
            oldRelResidual = relResidual;
        }

        JITDUMP($"{(converged ? "converged" : "failed to converge")} at iteration {i} rel residual {FMT_WT(relResidual)} eigenvalue {FMT_WT(eigenvalue)}\n");
        for (var j = _dfsTree.PostOrderCount; j != 0; j--)
        {
            var block = dfs.GetPostOrder(j - 1);
            var count = countVector[block.bbNum];
            // std::max keeps its first operand when comparison fails, including for NaN.
            block.setBBProfileWeight(0.0 < count ? count : 0.0);
        }
        _approximate = !converged || (_cappedCyclicProbabilities > 0);
    }
}
