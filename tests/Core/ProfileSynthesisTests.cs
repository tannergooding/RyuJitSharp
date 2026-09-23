// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ProfileSynthesisTests
{
    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public static void ThrowPropagationOnlyAdjustsHeuristicConditionalEdges(bool throwOnTrue, bool heuristic, bool profile)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_COND, BBJ_THROW, BBJ_THROW, BBJ_RETURN);
            var throwing = Edge(blocks[0], blocks[1], 0.4);
            var normal = Edge(blocks[0], blocks[5], 0.6);
            throwing.isHeuristicBased = normal.isHeuristicBased = heuristic;
            blocks[0].SetCond(throwOnTrue ? throwing : normal, throwOnTrue ? normal : throwing);
            blocks[1].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[1], blocks[2], 1));
            blocks[2].SetCond(Edge(blocks[2], blocks[3], 0.3), Edge(blocks[2], blocks[4], 0.7));
            compiler.fgPgoHaveWeights = profile;
            compiler.fgPgoConsistent = true;
            compiler._dfsTree = ComputeDfs(compiler, false);

            Assert.That(ProfileSynthesis.AdjustThrowEdgeLikelihoods(compiler),
                Is.EqualTo(heuristic ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(throwing.Likelihood, Is.EqualTo(heuristic ? 0 : 0.4));
            Assert.That(normal.Likelihood, Is.EqualTo(heuristic ? 1 : 0.6));
            Assert.That(blocks[2].TrueEdge.Likelihood, Is.EqualTo(0.3));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!heuristic || !profile));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SwitchThrowStatePropagatesOnlyWhenEveryPathThrows(bool allThrow)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_SWITCH, BBJ_THROW, allThrow ? BBJ_THROW : BBJ_RETURN, BBJ_RETURN);
            var throwing = Edge(blocks[0], blocks[1], 0.4);
            var normal = Edge(blocks[0], blocks[4], 0.6);
            throwing.isHeuristicBased = normal.isHeuristicBased = true;
            blocks[0].SetCond(throwing, normal);
            var first = Edge(blocks[1], blocks[2], 0.5);
            var second = Edge(blocks[1], blocks[3], 0.5);
            blocks[1].SwitchTargets = new BBswtDesc([first, second], [0, 1], true);
            compiler._dfsTree = ComputeDfs(compiler, false);

            Assert.That(ProfileSynthesis.AdjustThrowEdgeLikelihoods(compiler),
                Is.EqualTo(allThrow ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(throwing.Likelihood, Is.EqualTo(allThrow ? 0 : 0.4));
            Assert.That(first.Likelihood, Is.EqualTo(0.5));
            Assert.That(second.Likelihood, Is.EqualTo(0.5));
        });
    }

    [TestCase(0.005, 0, true)]
    [TestCase(0.02, 0, false)]
    [TestCase(0, 0.005, false)]
    [TestCase(100, 101, true)]
    [TestCase(100, 102, false)]
    [TestCase(double.NaN, 1, false)]
    [TestCase(double.PositiveInfinity, double.PositiveInfinity, false)]
    public static void ProfileConsistencyPreservesNativeRelativeComparison(double first, double second, bool expected)
    {
        Assert.That(Compiler.fgProfileWeightsConsistent(first, second), Is.EqualTo(expected));
    }

    [TestCase(BBJ_THROW, BBJ_ALWAYS, 0.0)]
    [TestCase(BBJ_ALWAYS, BBJ_THROW, 1.0)]
    [TestCase(BBJ_RETURN, BBJ_ALWAYS, 0.2)]
    [TestCase(BBJ_ALWAYS, BBJ_RETURN, 0.8)]
    [TestCase(BBJ_ALWAYS, BBJ_ALWAYS, 0.48)]
    [TestCase(BBJ_RETURN, BBJ_RETURN, 0.48)]
    [TestCase(BBJ_THROW, BBJ_THROW, 0.48)]
    public static void ConditionalHeuristicsPreservePriorityAndMarkers(BBKinds trueKind, BBKinds falseKind, double expected)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, trueKind, falseKind, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.5), Edge(blocks[0], blocks[2], 0.5));
            for (var i = 1; i <= 2; i++)
            {
                if (blocks[i].Kind is BBJ_ALWAYS)
                {
                    blocks[i].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[i], blocks[3], 1));
                }
            }
            AssignLikelihoods(CreateSynthesis(compiler));
            Assert.That(blocks[0].TrueEdge.Likelihood, Is.EqualTo(expected).Within(1e-15));
            Assert.That(blocks[0].FalseEdge.Likelihood, Is.EqualTo(1 - expected).Within(1e-15));
            Assert.That(blocks[0].TrueEdge.isHeuristicBased, Is.True);
            Assert.That(blocks[0].FalseEdge.isHeuristicBased, Is.True);
        });
    }

    [Test]
    public static void DegenerateConditionUsesItsSingleSharedEdge()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN);
            var edge = Edge(blocks[0], blocks[1], 0.4);
            edge.incrementDupCount();
            blocks[0].SetCond(edge, edge);
            AssignLikelihoods(CreateSynthesis(compiler));
            Assert.That(edge.Likelihood, Is.EqualTo(1));
            Assert.That(edge.isHeuristicBased, Is.True);
        });
    }

    [TestCase(false, 10, 0.2, 0.8, 0.2)]
    [TestCase(false, 0, 0.2, 0.8, 0.48)]
    [TestCase(false, 10, 0.2, 0.3, 0.48)]
    [TestCase(true, 10, 0.2, 0.8, 0.214)]
    [TestCase(true, 10, 0.2, 0.3, 0.404)]
    [TestCase(true, 0, 0.2, 0.8, 0.48)]
    [TestCase(true, 10, 0.0, 0.0, 0.48)]
    [TestCase(true, 10, 0.0002, 0.0003, 0.48)]
    public static void RepairAndBlendRespectExistingFlow(bool blend, double weight, double first, double second, double expected)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], first), Edge(blocks[0], blocks[2], second));
            blocks[0].bbWeight = weight;
            var synthesis = CreateSynthesis(compiler);
            if (blend)
            {
                BlendLikelihoods(synthesis);
            }
            else
            {
                RepairLikelihoods(synthesis);
            }
            Assert.That(blocks[0].TrueEdge.Likelihood, Is.EqualTo(expected).Within(1e-15));
            Assert.That(blocks[0].TrueEdge.Likelihood + blocks[0].FalseEdge.Likelihood, Is.EqualTo(1).Within(1e-15));
            Assert.That(blocks[0].TrueEdge.isHeuristicBased, Is.False);
        });
    }

    [Test]
    public static void SwitchPoliciesVisitUniqueEdgesAndClearAllLikelihoodState()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_SWITCH, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            FlowEdge[] edges = [Edge(blocks[0], blocks[1], 0.2), Edge(blocks[0], blocks[2], 0.3), Edge(blocks[0], blocks[3], 0.5)];
            edges[0].incrementDupCount();
            edges[0].isHeuristicBased = true;
            blocks[0].SwitchTargets = new BBswtDesc(edges, [0, 1, 0, 2], true);
            var synthesis = CreateSynthesis(compiler);
            AssignLikelihoods(synthesis);
            Assert.That(edges[0].Likelihood, Is.EqualTo(0.5));
            Assert.That(edges[1].Likelihood, Is.EqualTo(0.25));
            Assert.That(edges[2].Likelihood, Is.EqualTo(0.25));
            ReverseLikelihoods(synthesis);
#if DEBUG
            Assert.That(edges[0].Likelihood, Is.EqualTo(0.25));
            Assert.That(edges[2].Likelihood, Is.EqualTo(0.5));
            var random = new CLRRandom(compiler.info.compMethodHash());
            double[] expected = [random.NextDouble(), random.NextDouble(), random.NextDouble()];
            var sum = expected[0] + expected[1] + expected[2];
#else
            double[] expected = [0.5, 0.25, 0.25];
            const double sum = 1;
#endif
            RandomizeLikelihoods(synthesis);
            for (var i = 0; i < edges.Length; i++)
            {
                Assert.That(edges[i].Likelihood, Is.EqualTo(expected[i] / sum));
            }
            ClearLikelihoods(synthesis);
            foreach (var edge in edges)
            {
                Assert.That(edge.isHeuristicBased, Is.False);
#if DEBUG
                Assert.That(edge.hasLikelihood, Is.False);
#else
                Assert.That(edge.Likelihood, Is.Zero);
#endif
            }
            AssignLikelihoods(synthesis);
            Assert.That(edges[0].Likelihood, Is.EqualTo(0.5));
        });
    }

    [Test]
    public static void NestedLoopGainsAreComputedInsideOut()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            blocks[1].SetCond(Edge(blocks[1], blocks[2], 0.5), Edge(blocks[1], blocks[4], 0.5));
            blocks[2].SetCond(Edge(blocks[2], blocks[2], 0.5), Edge(blocks[2], blocks[3], 0.5));
            blocks[3].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[3], blocks[1], 1));
            var synthesis = CreateSynthesis(compiler);
            AssignLikelihoods(synthesis);
            Assert.That(blocks[1].TrueEdge.Likelihood, Is.EqualTo(0.9));
            Assert.That(blocks[2].TrueEdge.Likelihood, Is.EqualTo(0.9));
            ComputeCyclicProbabilities(synthesis);
            Assert.That(CyclicProbabilities(synthesis).Length, Is.EqualTo(2));
            foreach (var gain in CyclicProbabilities(synthesis))
            {
                Assert.That(gain, Is.EqualTo(10).Within(1e-12));
            }
            Assert.That(blocks[2].bbWeight, Is.EqualTo(9).Within(1e-12));
            Assert.That(CappedCyclicProbabilities(synthesis), Is.Zero);
            Assert.That(HasInfiniteLoop(synthesis), Is.False);
        });
    }

    [TestCase(0.5, false)]
    [TestCase(0.999, false)]
    [TestCase(0.9999, false)]
    [TestCase(0.9999, true)]
    [TestCase(1.0, false)]
    [TestCase(1.0, true)]
    public static void CappedLoopGainRepairsConditionalExits(double backLikelihood, bool exitOnTrue)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN);
            var back = Edge(blocks[0], blocks[0], backLikelihood);
            var exit = Edge(blocks[0], blocks[1], 1 - backLikelihood);
            blocks[0].SetCond(exitOnTrue ? exit : back, exitOnTrue ? back : exit);
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            var capped = backLikelihood > 0.999;
            Assert.That(CyclicProbabilities(synthesis)[0], Is.EqualTo(1 / (1 - Math.Min(backLikelihood, 0.999))));
            Assert.That(CappedCyclicProbabilities(synthesis), Is.EqualTo(capped ? 1 : 0));
            Assert.That(back.Likelihood, Is.EqualTo(Math.Min(backLikelihood, 0.999)).Within(1e-15));
            Assert.That(exit.Likelihood, Is.EqualTo(1 - Math.Min(backLikelihood, 0.999)).Within(1e-15));
            Assert.That(HasInfiniteLoop(synthesis), Is.EqualTo(backLikelihood == 1));
        });
    }

    [Test]
    public static void CappingAdjustsOnlyTheFirstEligibleExitInLoopOrder()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_COND, BBJ_RETURN);
            var firstExit = Edge(blocks[0], blocks[2], 0.0001);
            var secondExit = Edge(blocks[1], blocks[2], 0.0001);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.9999), firstExit);
            blocks[1].SetCond(Edge(blocks[1], blocks[0], 0.9999), secondExit);
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            Assert.That(CappedCyclicProbabilities(synthesis), Is.EqualTo(1));
            Assert.That(firstExit.Likelihood, Is.EqualTo(0.00090001).Within(1e-15));
            Assert.That(secondExit.Likelihood, Is.EqualTo(0.0001));
        });
    }

    [Test]
    public static void InfiniteLoopWithoutAnExitRetainsItsBackedge()
    {
        WithCompiler(compiler => {
            var block = Blocks(compiler, BBJ_ALWAYS)[0];
            block.SetKindAndTargetEdge(BBJ_ALWAYS, Edge(block, block, 1));
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            Assert.That(CyclicProbabilities(synthesis)[0], Is.EqualTo(1 / (1 - 0.999)));
            Assert.That(block.TargetEdge.Likelihood, Is.EqualTo(1));
            Assert.That(HasInfiniteLoop(synthesis), Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BlockWeightPassesPreserveAcyclicAndLoopFlow(bool loop, bool solver)
    {
        WithCompiler(compiler => {
            BasicBlock[] blocks;
            double[] expected;
            if (loop)
            {
                blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN);
                blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
                blocks[1].SetCond(Edge(blocks[1], blocks[2], 0.9), Edge(blocks[1], blocks[3], 0.1));
                blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[2], blocks[1], 1));
                expected = [100, 1000, 900, 100, 0];
            }
            else
            {
                blocks = Blocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN);
                blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.3), Edge(blocks[0], blocks[2], 0.7));
                blocks[1].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[1], blocks[3], 1));
                blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[2], blocks[3], 1));
                expected = [100, 30, 70, 100, 0];
            }
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            SetInputWeights(blocks, 100);
#if DEBUG
            SolverConfig(ref JitConfig) = solver ? 1 : 0;
            var output = Capture(compiler, () => ComputeBlockWeights(synthesis));
            Assert.That(output.Contains("Synthesis solver:", StringComparison.Ordinal), Is.EqualTo(solver));
#else
            ComputeBlockWeights(synthesis);
#endif
            for (var i = 0; i < blocks.Length; i++)
            {
                Assert.That(blocks[i].bbWeight, Is.EqualTo(expected[i]).Within(1e-10));
            }
            Assert.That(Approximate(synthesis), Is.False);
            Assert.That(Overflow(synthesis), Is.False);
        });
    }

    [TestCase(0.8, false)]
    [TestCase(0.999, true)]
    [TestCase(1.0, true)]
    public static void IrreducibleFlowConvergesOrStopsAtFiftyIterations(double backLikelihood, bool approximate)
    {
        WithCompiler(compiler => {
            var blocks = Irreducible(compiler, backLikelihood, selfEdge: false);
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            Assert.That(CyclicProbabilities(synthesis), Is.Empty);
            SetInputWeights(blocks, 100);
            GaussSeidelSolver(synthesis);
            Assert.That(Approximate(synthesis), Is.EqualTo(approximate));
            Assert.That(Overflow(synthesis), Is.False);
            if (approximate)
            {
                var expected = backLikelihood == 1 ? 5000 : 100 * (1 - Math.Pow(backLikelihood, 50)) / (1 - backLikelihood);
                Assert.That(blocks[2].bbWeight, Is.EqualTo(expected).Within(1e-8));
                Assert.That(blocks[1].bbWeight, Is.EqualTo(expected - 50).Within(1e-8));
            }
            else
            {
                Assert.That(blocks[1].bbWeight, Is.EqualTo(450).Within(0.5));
                Assert.That(blocks[2].bbWeight, Is.EqualTo(500).Within(0.5));
                Assert.That(blocks[3].bbWeight, Is.EqualTo(100).Within(0.1));
            }
        });
    }

    [Test]
    public static void IrreducibleSelfEdgesUseTheirLocalGain()
    {
        WithCompiler(compiler => {
            var blocks = Irreducible(compiler, 0.8, selfEdge: true);
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            Assert.That(CyclicProbabilities(synthesis), Is.Empty);
            SetInputWeights(blocks, 100);
            GaussSeidelSolver(synthesis);
            Assert.That(Approximate(synthesis), Is.False);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(900).Within(1));
            Assert.That(blocks[2].bbWeight, Is.EqualTo(500).Within(0.5));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SolverOverflowPreservesNativeSinglePassAndIterativePolicy(bool irreducible)
    {
        WithCompiler(compiler => {
            var blocks = irreducible ? Irreducible(compiler, 0.8, selfEdge: false) : Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            if (!irreducible)
            {
                blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            }
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            SetInputWeights(blocks, 1e12);
            GaussSeidelSolver(synthesis);
            Assert.That(Overflow(synthesis), Is.True);
            Assert.That(Approximate(synthesis), Is.EqualTo(irreducible));
            Assert.That(blocks[irreducible ? 2 : 1].bbWeight, Is.EqualTo(1e12));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SinglePassChecksEntryExitBalanceOnlyAfterImport(bool imported)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.3), Edge(blocks[0], blocks[2], 0.3));
            compiler.fgImportDone = imported;
            var synthesis = CreateSynthesis(compiler);
            SetInputWeights(blocks, 100);
            GaussSeidelSolver(synthesis);
            Assert.That(Approximate(synthesis), Is.EqualTo(imported));
            Assert.That(blocks[1].bbWeight + blocks[2].bbWeight, Is.EqualTo(60));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FinallyWeightsUseImplicitTryFlowBeforeImport(bool imported)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN, BBJ_EHFINALLYRET);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            blocks[0].TryIndex = 0;
            blocks[2].HndIndex = 0;
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_FINALLY,
                ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdHndBeg = blocks[2], ebdHndLast = blocks[2],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            compiler.fgImportDone = imported;
            var synthesis = CreateSynthesis(compiler);
            SetInputWeights(blocks, 100);
            blocks[2].setBBProfileWeight(0.00001);
            GaussSeidelSolver(synthesis);
            Assert.That(blocks[2].bbWeight, Is.EqualTo(imported ? 0.00001 : 100.00001));
        });
    }

    [Test]
    public static void DirectBlockWeightsSeedFinallyWithoutCountingCrossHandlerFlowTwice()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_CALLFINALLY, BBJ_EHFINALLYRET);
            blocks[0].SetKindAndTargetEdge(BBJ_CALLFINALLY, Edge(blocks[0], blocks[1], 1));
            blocks[0].SetFlags(BasicBlockFlags.BBF_RETLESS_CALL);
            blocks[0].TryIndex = 0;
            blocks[1].HndIndex = 0;
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_FINALLY,
                ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdHndBeg = blocks[1], ebdHndLast = blocks[1],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            var synthesis = CreateSynthesis(compiler);
            SetInputWeights(blocks, 100);
            ComputeBlockWeight(synthesis, blocks[0]);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(100));
            ComputeBlockWeight(synthesis, blocks[1]);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(100));
        });
    }

    [TestCase(0, 100)]
    [TestCase(0.0005, 100)]
    [TestCase(0.002, 0.002)]
    [TestCase(200, 200)]
    public static void InputWeightsResetEveryBlockAndApplyNearZeroFallback(double input, double expected)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            foreach (var block in blocks)
            {
                block.setBBProfileWeight(42);
            }
            AssignInputWeights(CreateSynthesis(compiler), input);
            Assert.That(blocks[0].bbWeight, Is.EqualTo(expected));
            Assert.That(blocks[1].bbWeight, Is.Zero);
            Assert.That(blocks[2].bbWeight, Is.Zero);
        });
    }

    [Test]
    public static void InputWeightRemovesEntryLoopGain()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[0], 0.75), Edge(blocks[0], blocks[1], 0.25));
            var synthesis = CreateSynthesis(compiler);
            ComputeCyclicProbabilities(synthesis);
            AssignInputWeights(synthesis, 200);
            Assert.That(blocks[0].bbWeight, Is.EqualTo(50));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ExceptionalInputsRequireReachableTryAndRootCompiler(bool reachable, bool inlinee)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], reachable ? blocks[1] : blocks[4], 1));
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_FILTER,
                ebdTryBeg = blocks[1], ebdTryLast = blocks[1],
                ebdFilter = blocks[2], ebdHndBeg = blocks[3], ebdHndLast = blocks[3],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            if (inlinee)
            {
                compiler.impInlineInfo = new InlineInfo();
            }
            AssignInputWeights(CreateSynthesis(compiler), 100);
            Assert.That(blocks[2].bbWeight, Is.EqualTo(reachable && !inlinee ? 0.00001 : 0));
            Assert.That(blocks[3].bbWeight, Is.EqualTo(reachable && !inlinee ? 0.00001 : 0));
        });
    }

    [Test]
    public static void DriverUpdatesMetadataAndUsesAllPolicies([Values] ProfileSynthesisOption option, [Values] bool hadWeights)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            blocks[0].setBBProfileWeight(80);
            compiler.fgPredsComputed = true;
            compiler.fgPgoHaveWeights = hadWeights;
            compiler.fgPgoSource = ICorJitInfo.PgoSource.Dynamic;
            ProfileSynthesis.Run(compiler, option);
            var expectedSource = option == ProfileSynthesisOption.RepairLikelihoods ? ICorJitInfo.PgoSource.Dynamic
                : hadWeights && (option == ProfileSynthesisOption.BlendLikelihoods) ? ICorJitInfo.PgoSource.Blend
                : ICorJitInfo.PgoSource.Synthesis;
            Assert.That(compiler.fgPgoSource, Is.EqualTo(expectedSource));
            Assert.That(compiler.fgPgoHaveWeights && compiler.fgPgoSynthesized && compiler.fgPgoConsistent, Is.True);
            Assert.That(compiler.fgPgoSingleEdge, Is.True);
            Assert.That(compiler.fgCalledCount, Is.EqualTo(80));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(80));
            Assert.That(compiler.Metrics.ProfileSynthesizedBlendedOrRepaired, Is.EqualTo(1));
#if DEBUG
            Assert.That(compiler.fgPgoDeferredInconsistency, Is.False);
#endif
        });
    }

    [TestCase(false, false, 9, true)]
    [TestCase(false, false, 10, false)]
    [TestCase(true, false, 0, false)]
    [TestCase(false, true, 0, false)]
    public static void SingleEdgeHeuristicsExcludeLargeSizeOptimizedAndStaticConstructors(bool cctor, bool size, int calls, bool expected)
    {
        WithCompiler(compiler => {
            _ = Blocks(compiler, BBJ_RETURN);
            compiler.info.compFlags = cctor ? FLG_CCTOR : 0;
            if (size)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_SIZE_OPT);
            }
            compiler.opts.callInstrCount = calls;
            ProfileSynthesis.Run(compiler, ProfileSynthesisOption.AssignLikelihoods);
            Assert.That(compiler.fgPgoSingleEdge, Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DriverRetainsEntryLoopFrequencyAndDerivesCalledCount(bool inlinee)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[0], 0.75), Edge(blocks[0], blocks[1], 0.25));
            blocks[0].setBBProfileWeight(200);
            compiler.fgCalledCount = 77;
            if (inlinee)
            {
                compiler.impInlineInfo = new InlineInfo();
            }
            ProfileSynthesis.Run(compiler, ProfileSynthesisOption.RetainLikelihoods);
            Assert.That(blocks[0].bbWeight, Is.EqualTo(200));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(50));
            Assert.That(compiler.fgCalledCount, Is.EqualTo(inlinee ? 77 : 50));
            Assert.That(compiler.fgPgoSingleEdge, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InfiniteLoopRetryPolicyIsBounded(bool retain, bool imported)
    {
        WithCompiler(compiler => {
            var block = Blocks(compiler, BBJ_ALWAYS)[0];
            block.SetKindAndTargetEdge(BBJ_ALWAYS, Edge(block, block, 1));
            compiler.fgImportDone = imported;
#if DEBUG
            SolverConfig(ref JitConfig) = 0;
#endif
            var synthesis = CreateSynthesis(compiler);
            RunSynthesis(synthesis, retain ? ProfileSynthesisOption.RetainLikelihoods : ProfileSynthesisOption.AssignLikelihoods);
            Assert.That(compiler.fgPgoConsistent, Is.False);
            Assert.That(compiler.Metrics.ProfileInconsistentInitially, Is.EqualTo(!imported ? 1 : 0));
            Assert.That(BlendFactor(synthesis), Is.EqualTo(!retain ? 1 : 0.05));
            Assert.That(LoopBackLikelihood(synthesis), Is.EqualTo(!retain ? 0.9 * Math.Pow(0.9, 4) : 0.9).Within(1e-15));
        });
    }

    private static BasicBlock[] Irreducible(Compiler compiler, double backLikelihood, bool selfEdge)
    {
        var blocks = Blocks(compiler, BBJ_COND, selfEdge ? BBJ_SWITCH : BBJ_ALWAYS, BBJ_COND, BBJ_RETURN);
        // Conditional successors visit the false edge first; fix RPO for the analytic iteration counts.
        blocks[0].SetCond(Edge(blocks[0], blocks[2], 0.5), Edge(blocks[0], blocks[1], 0.5));
        if (selfEdge)
        {
            blocks[1].SwitchTargets = new BBswtDesc(
                [Edge(blocks[1], blocks[1], 0.5), Edge(blocks[1], blocks[2], 0.5)], [0, 1], true);
        }
        else
        {
            blocks[1].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[1], blocks[2], 1));
        }
        blocks[2].SetCond(Edge(blocks[2], blocks[1], backLikelihood), Edge(blocks[2], blocks[3], 1 - backLikelihood));
        compiler._dfsTree = compiler.fgComputeDfs();
        compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
        Assert.That(compiler._dfsTree.GetPostOrder(compiler._dfsTree.PostOrderCount - 2), Is.SameAs(blocks[1]));

        return blocks;
    }

    private static void SetInputWeights(BasicBlock[] blocks, double entryWeight)
    {
        foreach (var block in blocks)
        {
            block.setBBProfileWeight(0);
        }
        blocks[0].setBBProfileWeight(entryWeight);
    }

#if DEBUG
    [TestCase("0", 0)]
    [TestCase("0x1p-2,0.8", 0.25)]
    [TestCase("1", 1)]
    [TestCase("-1", 0.00001)]
    [TestCase("1.1", 0.00001)]
    [TestCase("nan", 0.00001)]
    [TestCase("inf", 0.00001)]
    public static void ExceptionalWeightOverrideUsesOnlyValidFirstFactor(string setting, double expected)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_RETURN, BBJ_RETURN);
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdHndBeg = blocks[1], ebdHndLast = blocks[1],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            var bytes = Encoding.UTF8.GetBytes(setting + '\0');
            fixed (byte* p = bytes)
            {
                ExceptionWeightConfig(ref JitConfig) = p;
                AssignInputWeights(CreateSynthesis(compiler), 100);
            }
            Assert.That(blocks[1].bbWeight, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase(" , ")]
    public static void EmptyExceptionalWeightOverrideFailsExplicitly(string setting)
    {
        WithCompiler(compiler => {
            _ = Blocks(compiler, BBJ_RETURN);
            var bytes = Encoding.UTF8.GetBytes(setting + '\0');
            fixed (byte* p = bytes)
            {
                ExceptionWeightConfig(ref JitConfig) = p;
                _ = Assert.Throws<FormatException>(() => AssignInputWeights(CreateSynthesis(compiler), 100));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InvalidPreImportFlowDefersConsistencyAssertion(bool imported)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.2), Edge(blocks[0], blocks[2], 0.2));
            compiler.fgPredsComputed = true;
            compiler.fgImportDone = imported;
            SolverConfig(ref JitConfig) = 1;
            var output = Capture(compiler, () => ProfileSynthesis.Run(compiler, ProfileSynthesisOption.RetainLikelihoods));
            Assert.That(compiler.fgPgoDeferredInconsistency, Is.EqualTo(!imported));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!imported));
            Assert.That(output.Contains("Will defer asserting until after importation", StringComparison.Ordinal), Is.EqualTo(!imported));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitSynthesisExceptionWeight")]
    private static extern ref byte* ExceptionWeightConfig(ref JitConfigValues config);

    [Test]
    public static void IncomingChecksSeparateMissingLikelihoodFromWeightBalance()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            var edge = new FlowEdge(blocks[0], blocks[1], null);
            blocks[1].bbPreds = edge;
            blocks[0].bbTargetEdge = edge;
            blocks[0].bbWeight = blocks[1].bbWeight = 0;
            Assert.That(compiler.fgDebugCheckIncomingProfileData(blocks[1], ProfileChecks.CHECK_LIKELY), Is.True);
            Assert.That(compiler.fgDebugCheckIncomingProfileData(blocks[1], ProfileChecks.CHECK_HASLIKELIHOOD), Is.False);
            Assert.That(compiler.fgDebugCheckIncomingProfileData(blocks[1], ProfileChecks.CHECK_NONE), Is.True);
            var output = Capture(compiler, () => _ = compiler.fgDebugCheckIncomingProfileData(blocks[1], ProfileChecks.CHECK_HASLIKELIHOOD));
            Assert.That(output, Does.Match(@"Missing likelihood on [0-9A-F]{16} BB01->BB02\r?\n"));
            edge.Likelihood = 0.9;
            blocks[0].bbWeight = 10;
            blocks[1].bbWeight = 9;
            Assert.That(compiler.fgDebugCheckIncomingProfileData(blocks[1], ProfileChecks.CHECK_LIKELY), Is.True);
            blocks[1].bbWeight = 11;
            Assert.That(compiler.fgDebugCheckIncomingProfileData(blocks[1], ProfileChecks.CHECK_LIKELY), Is.False);
        });
    }

    [Test]
    public static void OutgoingChecksPreserveOsrAndMissingLikelihoodExceptions()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.3), Edge(blocks[0], blocks[2], 0.2));
            const ProfileChecks checks = ProfileChecks.CHECK_HASLIKELIHOOD | ProfileChecks.CHECK_LIKELIHOODSUM;
            Assert.That(compiler.fgDebugCheckOutgoingProfileData(blocks[0], checks), Is.False);
            compiler.fgOSREntryBB = blocks[0];
            Assert.That(compiler.fgDebugCheckOutgoingProfileData(blocks[0], checks), Is.True);
            blocks[0].SetCond(new FlowEdge(blocks[0], blocks[1], null), blocks[0].FalseEdge);
            Assert.That(compiler.fgDebugCheckOutgoingProfileData(blocks[0], checks), Is.False);
            Assert.That(compiler.fgDebugCheckOutgoingProfileData(blocks[0], ProfileChecks.CHECK_LIKELY), Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void ProfileChecksExcludeEhAndOsrIncomingFlow(int entryKind)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetCond(Edge(blocks[0], blocks[1], 0.5), Edge(blocks[0], blocks[2], 0.5));
            blocks[0].TryIndex = 0;
            blocks[0].setBBProfileWeight(10);
            blocks[1].setBBProfileWeight(5);
            blocks[2].setBBProfileWeight(50);
            Assert.That(compiler.fgDebugCheckProfileWeights(ProfileChecks.CHECK_LIKELY), Is.False);
            switch (entryKind)
            {
                case 0:
                    blocks[2].CatchType = bbCatchType.BBCT_FILTER_HANDLER;
                    break;
                case 1:
                    compiler.fgOSREntryBB = blocks[2];
                    break;
                case 2:
                    compiler.fgEntryBB = blocks[2];
                    break;
            }
            Assert.That(compiler.fgDebugCheckProfileWeights(ProfileChecks.CHECK_LIKELY), Is.True);
        });
    }

    [TestCase(BBJ_EHFILTERRET, true)]
    [TestCase(BBJ_EHFINALLYRET, true)]
    [TestCase(BBJ_EHFAULTRET, true)]
    [TestCase(BBJ_EHCATCHRET, true)]
    [TestCase(BBJ_ALWAYS, false)]
    [TestCase(BBJ_CALLFINALLY, false)]
    public static void EhBoundaryClassificationMatchesNative(BBKinds kind, bool outgoing)
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, kind);
            Assert.That(block.hasEHBoundaryIn, Is.False);
            block.CatchType = bbCatchType.BBCT_FILTER;
            Assert.That(block.hasEHBoundaryIn, Is.True);
            Assert.That(block.hasEHBoundaryOut, Is.EqualTo(outgoing));
        });
    }

    [Test]
    public static void ProfileCheckingHonorsUnprofiledAndDeferredBlocks()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            blocks[0].bbWeight = 10;
            blocks[1].bbWeight = 4;
            Assert.That(compiler.fgDebugCheckProfileWeights(ProfileChecks.CHECK_LIKELY), Is.True);
            const ProfileChecks checks = ProfileChecks.CHECK_LIKELY | ProfileChecks.CHECK_ALL_BLOCKS;
            Assert.That(compiler.fgDebugCheckProfileWeights(checks), Is.False);
            blocks[1].bbWeight = 10;
            Assert.That(compiler.fgDebugCheckProfileWeights(checks), Is.True);
            compiler.fgPgoDeferredInconsistency = true;
            Assert.That(compiler.fgDebugCheckProfileWeights(checks), Is.False);
            Assert.That(compiler.fgDebugCheckProfileWeights(ProfileChecks.CHECK_NONE), Is.True);
        });
    }

    [TestCase(-1, true)]
    [TestCase(0, false)]
    public static void PhaseChecksHonorConfigurationAndExactSuccessDump(int config, bool enabled)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Edge(blocks[0], blocks[1], 1));
            blocks[0].setBBProfileWeight(10);
            blocks[1].setBBProfileWeight(10);
            compiler.fgPgoHaveWeights = true;
            compiler.fgPredsComputed = true;
            ProfileCheckConfig(ref JitConfig) = config;
            var output = Capture(compiler, () => compiler.fgDebugCheckProfile(PhaseChecks.CHECK_PROFILE));
            var expected = enabled
                ? "Checking Profile Weights (flags:0x14)\nProfile is self-consistent (2 profiled blocks, 0 unprofiled)\n"
                : "[profile weight checks disabled]\n";
            Assert.That(output, Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitProfileChecks")]
    private static extern ref int ProfileCheckConfig(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitSynthesisUseSolver")]
    private static extern ref int SolverConfig(ref JitConfigValues config);

    private static string Capture(Compiler compiler, Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        var previousVerbose = compiler.verbose;
        try
        {
            s_jitstdout = writer;
            compiler.verbose = true;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
            compiler.verbose = previousVerbose;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDfs")]
    private static extern FlowGraphDfsTree ComputeDfs(Compiler compiler, bool useProfile);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern ProfileSynthesis CreateSynthesis(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AssignLikelihoods")]
    private static extern void AssignLikelihoods(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RepairLikelihoods")]
    private static extern void RepairLikelihoods(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BlendLikelihoods")]
    private static extern void BlendLikelihoods(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ClearLikelihoods")]
    private static extern void ClearLikelihoods(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReverseLikelihoods")]
    private static extern void ReverseLikelihoods(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RandomizeLikelihoods")]
    private static extern void RandomizeLikelihoods(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ComputeCyclicProbabilities")]
    private static extern void ComputeCyclicProbabilities(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cyclicProbabilities")]
    private static extern ref double[] CyclicProbabilities(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cappedCyclicProbabilities")]
    private static extern ref int CappedCyclicProbabilities(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hasInfiniteLoop")]
    private static extern ref bool HasInfiniteLoop(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ComputeBlockWeights")]
    private static extern void ComputeBlockWeights(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AssignInputWeights")]
    private static extern void AssignInputWeights(ProfileSynthesis synthesis, double entryWeight);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Run")]
    private static extern void RunSynthesis(ProfileSynthesis synthesis, ProfileSynthesisOption option);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blendFactor")]
    private static extern ref double BlendFactor(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_loopBackLikelihood")]
    private static extern ref double LoopBackLikelihood(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ComputeBlockWeight")]
    private static extern void ComputeBlockWeight(ProfileSynthesis synthesis, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GaussSeidelSolver")]
    private static extern void GaussSeidelSolver(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_approximate")]
    private static extern ref bool Approximate(ProfileSynthesis synthesis);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_overflow")]
    private static extern ref bool Overflow(ProfileSynthesis synthesis);

    private static FlowEdge Edge(BasicBlock source, BasicBlock target, double likelihood)
    {
        var edge = new FlowEdge(source, target, target.bbPreds) { Likelihood = likelihood };
        edge.incrementDupCount();
        target.bbPreds = edge;
        target.bbRefs++;

        return edge;
    }

    private static BasicBlock[] Blocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, kinds[i]);
            blocks[i].bbRefs = i == 0 ? 1 : 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
                blocks[i].Prev = blocks[i - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        return blocks;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(ProfileSynthesisTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
