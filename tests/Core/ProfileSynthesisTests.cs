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

#if DEBUG
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
