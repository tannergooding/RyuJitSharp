// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicRLTests
{
    private static readonly double[] s_initialDistribution = [0.0, 0.5, 1.0, 0.5];
    private static readonly double[] s_stopLikelihoods = [0.5];
    private static readonly double[] s_performLikelihoods = [0.5, 1.0];
    private static readonly int[] s_performedCandidate = [1];

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLCSE")]
    private static extern ref byte* RlParameters(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLCSEAlpha")]
    private static extern ref byte* RlAlpha(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLCSEGreedy")]
    private static extern ref int RlGreedy(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLCSEVerbose")]
    private static extern ref int RlVerbose(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitReplayCSEReward")]
    private static extern ref byte* ReplayReward(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitReplayCSE")]
    private static extern ref byte* ReplaySequence(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLHookCSEDecisions")]
    private static extern ref byte* HookDecisions(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLHookEmitFeatureNames")]
    private static extern ref int EmitFeatureNames(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Candidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEattempt")]
    private static extern ref int Attempt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_parameters")]
    private static extern ref double[] Parameters(CSE_HeuristicParameterized heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_baseLikelihoods")]
    private static extern ref List<double>? BaseLikelihoods(CSE_HeuristicParameterized heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_likelihoods")]
    private static extern ref List<double> Likelihoods(CSE_HeuristicParameterized heuristic);

    [Test]
    public static void HookUsesZeroBasedIndicesCountsViableAttemptsAndEmitsOriginalSequence()
    {
        WithCompiler(compiler =>
        {
            var descriptors = CreateCandidates(compiler, 2);
            descriptors[1].defExcSetPromise = ValueNumStore.NoVN;
            var script = Encoding.ASCII.GetBytes("2,0,0,1,-1\0");
            fixed (byte* configuration = script)
            {
                HookDecisions(ref JitConfig) = configuration;
                EmitFeatureNames(ref JitConfig) = 1;
                var heuristic = new RecordingHook(compiler);
                heuristic.ConsiderCandidates();

                Assert.That(heuristic.Performed, Is.EqualTo([1, 1]));
                Assert.That(Attempt(compiler), Is.EqualTo(2));
                Assert.That(heuristic.MadeChanges(), Is.True);

                var output = CodeGenLifeTransitionTests.Capture(heuristic.DumpMetrics);
                Assert.That(output, Does.Contain(" featureNames type,viable,live_across_call"));
                Assert.That(output, Does.Contain(" features #1,"));
                Assert.That(output, Does.Contain(" features #2,"));
                Assert.That(output, Does.Contain(" seq 2,0,0,1,-1"));
            }
        });
    }

    [Test]
    public static void SoftmaxUsesNativeTopDownOrderAndCapturesInitialDistribution()
    {
        WithCompiler(compiler =>
        {
            _ = CreateCandidates(compiler, 1);
            var emptyParameters = Encoding.ASCII.GetBytes("\0");
            fixed (byte* configuration = emptyParameters)
            {
                RlParameters(ref JitConfig) = configuration;
                var heuristic = new RecordingRL(compiler);
                heuristic.ConsiderCandidates();

                Assert.That(BaseLikelihoods(heuristic), Is.EqualTo(s_initialDistribution));
                var firstDraw = new CLRRandom(compiler.info.compMethodHash()).NextDouble();
                Assert.That(heuristic.Performed, firstDraw < 0.5 ? Is.Empty : Is.EqualTo(s_performedCandidate));
                Assert.That(Likelihoods(heuristic), Is.EqualTo(firstDraw < 0.5
                    ? s_stopLikelihoods : s_performLikelihoods));
            }
        });
    }

    [Test]
    public static void UpdateAccumulatesPolicyGradientAgainstStoppingChoice()
    {
        WithCompiler(compiler =>
        {
            var descriptor = CreateCandidates(compiler, 1)[0];
            var emptyParameters = Encoding.ASCII.GetBytes("\0");
            var script = Encoding.ASCII.GetBytes("1,0\0");
            var rewards = Encoding.ASCII.GetBytes("2,0\0");
            var alpha = Encoding.ASCII.GetBytes("0.5\0");
            fixed (byte* parameters = emptyParameters)
            fixed (byte* sequence = script)
            fixed (byte* rewardValues = rewards)
            fixed (byte* learningRate = alpha)
            {
                RlParameters(ref JitConfig) = parameters;
                ReplaySequence(ref JitConfig) = sequence;
                ReplayReward(ref JitConfig) = rewardValues;
                RlAlpha(ref JitConfig) = learningRate;
                var heuristic = new RecordingRL(compiler);

                heuristic.ConsiderCandidates();

                var features = new double[25];
                var stopping = new double[25];
                heuristic.GetFeatures(descriptor, features);
                heuristic.GetFeatures(null, stopping);
                for (var index = 0; index < features.Length; index++)
                {
                    Assert.That(Parameters(heuristic)[index],
                        Is.EqualTo(0.5 * (features[index] - stopping[index])).Within(1e-10),
                        $"parameter {index}");
                }

                Assert.That(heuristic.Performed, Is.EqualTo([1]));
                Assert.That(Attempt(compiler), Is.EqualTo(1));
            }
        });
    }

    [Test]
    public static void GreedyModeUsesParameterizedSelectionWithoutSoftmax()
    {
        WithCompiler(compiler =>
        {
            _ = CreateCandidates(compiler, 1);
            var parameters = Encoding.ASCII.GetBytes("10\0");
            fixed (byte* configuration = parameters)
            {
                RlParameters(ref JitConfig) = configuration;
                RlGreedy(ref JitConfig) = 1;
                var heuristic = new RecordingRL(compiler);

                heuristic.ConsiderCandidates();

                Assert.That(heuristic.Performed, Is.EqualTo(s_performedCandidate));
                Assert.That(BaseLikelihoods(heuristic), Is.Empty);
                Assert.That(heuristic.MadeChanges(), Is.True);
            }
        });
    }

    [Test]
    public static void UpdateAcceptsLastRewardSlotAndRejectsAnOverlongSequence()
    {
        WithCompiler(compiler =>
        {
            _ = CreateCandidates(compiler, 1);
            var parameters = Encoding.ASCII.GetBytes("\0");
            var rewards = Encoding.ASCII.GetBytes("2\0");
            var lastSlot = Encoding.ASCII.GetBytes(string.Concat(System.Linq.Enumerable.Repeat("99,", 64)) + "0\0");
            var pastEnd = Encoding.ASCII.GetBytes(string.Concat(System.Linq.Enumerable.Repeat("99,", 65)) + "0\0");
            fixed (byte* configuration = parameters)
            fixed (byte* rewardValues = rewards)
            fixed (byte* lastSequence = lastSlot)
            fixed (byte* tooLongSequence = pastEnd)
            {
                RlParameters(ref JitConfig) = configuration;
                ReplayReward(ref JitConfig) = rewardValues;
                ReplaySequence(ref JitConfig) = lastSequence;
                var heuristic = new RecordingRL(compiler);
                heuristic.ConsiderCandidates();
                Assert.That(heuristic.Performed, Is.Empty);
                Assert.That(Attempt(compiler), Is.Zero);

                ReplaySequence(ref JitConfig) = tooLongSequence;
                heuristic = new RecordingRL(compiler);
                _ = Assert.Throws<FatalJitException>(heuristic.ConsiderCandidates);
            }
        });
    }

    [Test]
    public static void UpdateDiagnosticsUseNativeWeightPrecisionAndRewardWidth()
    {
        WithCompiler(compiler =>
        {
            _ = CreateCandidates(compiler, 1);
            var parameters = Encoding.ASCII.GetBytes("\0");
            var sequence = Encoding.ASCII.GetBytes("0,1\0");
            var rewards = Encoding.ASCII.GetBytes("2.5,-12.75\0");
            var alpha = Encoding.ASCII.GetBytes("1.25\0");
            fixed (byte* configuration = parameters)
            fixed (byte* sequenceValues = sequence)
            fixed (byte* rewardValues = rewards)
            fixed (byte* learningRate = alpha)
            {
                RlParameters(ref JitConfig) = configuration;
                ReplaySequence(ref JitConfig) = sequenceValues;
                ReplayReward(ref JitConfig) = rewardValues;
                RlAlpha(ref JitConfig) = learningRate;
                RlVerbose(ref JitConfig) = 1;

                var heuristic = new RecordingRL(compiler);
                var output = CodeGenLifeTransitionTests.Capture(heuristic.ConsiderCandidates);

                Assert.That(output, Does.Contain(
                    "Updating parameters with sequence 0, 1 alpha 1.25 and rewards  2.5000,-12.7500"));
            }
        });
    }

    private static CSEdsc[] CreateCandidates(Compiler compiler, int count)
    {
        var store = new ValueNumStore(compiler);
        compiler.vnStore = store;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgBBcount = 1;
        block.bbPostorderNum = 1;
        var descriptors = new CSEdsc[count];
        for (var index = 0; index < count; index++)
        {
            var tree = compiler.gtNewIconNode(TYP_INT, index + 1);
            tree.SetCosts(3, 2);
            descriptors[index] = new CSEdsc(tree, compiler.gtNewStmt(tree), block)
            {
                csdIndex = index + 1,
                csdDefCount = 1,
                csdUseCount = 1,
                csdDefWtCnt = 1,
                csdUseWtCnt = 1,
                defExcSetPromise = store.VNForExpr(null, TYP_INT),
            };
        }

        Candidates(compiler) = descriptors;
        CandidateCount(compiler) = count;
        return descriptors;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.info.compFullName = nameof(CSEHeuristicRLTests);
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }

    private sealed class RecordingHook(Compiler compiler) : CSE_HeuristicRLHook(compiler)
    {
        public List<int> Performed { get; } = [];

        public override void PerformCSE(CSE_Candidate candidate) => Performed.Add(candidate.CseIndex());
    }

    private sealed class RecordingRL(Compiler compiler) : CSE_HeuristicRL(compiler)
    {
        public List<int> Performed { get; } = [];

        public override void PerformCSE(CSE_Candidate candidate) => Performed.Add(candidate.CseIndex());
    }
}
#endif
