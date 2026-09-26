// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicModesTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRandomCSE")]
    private static extern ref int RandomSalt(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitReplayCSE")]
    private static extern ref byte* ReplayConfig(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitNoCSE2")]
    private static extern ref int DisableCse(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Candidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEattempt")]
    private static extern ref int Attempt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "sortTab")]
    private static extern ref CSEdsc?[]? SortedCandidates(CSE_HeuristicCommon heuristic);

    [TestCase(0)]
    [TestCase(73)]
    public static void RandomUsesMethodHashXorSaltAndInsideOutPermutation(int salt)
    {
        WithCompiler(compiler => {
            RandomSalt(ref JitConfig) = salt;
            var descriptors = CreateCandidates(compiler, 5);
            var heuristic = new RecordingRandom(compiler);
            var expected = new CSEdsc?[descriptors.Length];
            var random = new CLRRandom(compiler.info.compMethodHash() ^ salt);
            for (var i = 0; i < expected.Length; i++)
            {
                var j = random.Next(i + 1);
                if (i != j)
                {
                    expected[i] = expected[j];
                }
                expected[j] = descriptors[i];
            }
            var selected = random.Next(descriptors.Length) + 1;

            heuristic.ConsiderCandidates();

            Assert.That(SortedCandidates(heuristic), Is.EqualTo(expected));
            Assert.That(heuristic.Performed, Is.EqualTo(Array.ConvertAll(expected[..selected], d => d!.csdIndex)));
            Assert.That(Attempt(compiler), Is.EqualTo(selected));
            Assert.That(heuristic.MadeChanges(), Is.True);
        });
    }

    [Test]
    public static void RandomCountsStressDisabledAttemptBeforeOtherGates()
    {
        WithCompiler(compiler => {
            var descriptors = CreateCandidates(compiler, 1);
            DisableCse(ref JitConfig) = 1;
            var heuristic = new RecordingRandom(compiler);

            heuristic.ConsiderCandidates();

            Assert.That(Attempt(compiler), Is.EqualTo(1));
            Assert.That(heuristic.Performed, Is.Empty);
            Assert.That(heuristic.MadeChanges(), Is.False);
            Assert.That(SortedCandidates(heuristic)![0], Is.SameAs(descriptors[0]));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void RandomPreservesExceptionUseAndRawCountGates(int gate)
    {
        WithCompiler(compiler => {
            var descriptor = CreateCandidates(compiler, 1)[0];
            switch (gate)
            {
                case 0:
                {
                    descriptor.defExcSetPromise = ValueNumStore.NoVN;
                    break;
                }
                case 1:
                {
                    descriptor.csdUseWtCnt = 0;
                    break;
                }
                case 2:
                {
                    descriptor.csdDefCount = 0;
                    break;
                }
                default:
                {
                    descriptor.csdUseCount = 0;
                    break;
                }
            }
            var heuristic = new RecordingRandom(compiler);

            heuristic.ConsiderCandidates();

            Assert.That(Attempt(compiler), Is.EqualTo(1));
            Assert.That(heuristic.Performed, Is.Empty);
            Assert.That(heuristic.MadeChanges(), Is.False);
        });
    }

    [Test]
    public static void BothModesUseTheCommonCandidatePredicate()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            tree.SetCosts(9, 9);
            tree._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var random = new RecordingRandom(compiler);
            var replay = new RecordingReplay(compiler);

            Assert.That(random.ConsiderTree(tree, isReturn: false), Is.True);
            Assert.That(replay.ConsiderTree(tree, isReturn: false), Is.True);

            tree.Flags |= GTF_DONT_CSE;
            Assert.That(random.ConsiderTree(tree, isReturn: false), Is.False);
            Assert.That(replay.ConsiderTree(tree, isReturn: false), Is.False);
        });
    }

    [Test]
    public static void ReplayCountsOnlyValidEntriesAndRetriesNonviableCandidates()
    {
        WithCompiler(compiler => {
            var descriptors = CreateCandidates(compiler, 3);
            descriptors[2].defExcSetPromise = ValueNumStore.NoVN;
            var bytes = Encoding.ASCII.GetBytes("0,3,3,2,99\0");
            fixed (byte* configuration = bytes)
            {
                ReplayConfig(ref JitConfig) = configuration;
                var heuristic = new RecordingReplay(compiler);
                compiler.verbose = true;
                var output = CodeGenLifeTransitionTests.Capture(heuristic.ConsiderCandidates);

                Assert.That(Attempt(compiler), Is.EqualTo(3));
                Assert.That(heuristic.Performed, Is.EqualTo([2]));
                Assert.That(heuristic.MadeChanges(), Is.True);
                Assert.That(output, Does.Contain("Invalid candidate number 0"));
                Assert.That(output, Does.Contain("Invalid candidate number 99"));
                Assert.That(output.Split("Abandoned CSE #03 -- not viable").Length - 1, Is.EqualTo(2));
            }
        });
    }

    [Test]
    public static void ReplayTreatsAdjacentMinusAsNextCandidateSign()
    {
        WithCompiler(compiler => {
            _ = CreateCandidates(compiler, 3);
            var bytes = Encoding.ASCII.GetBytes("1-2,3\0");
            fixed (byte* configuration = bytes)
            {
                ReplayConfig(ref JitConfig) = configuration;
                var heuristic = new RecordingReplay(compiler);
                compiler.verbose = true;
                var output = CodeGenLifeTransitionTests.Capture(heuristic.ConsiderCandidates);

                Assert.That(heuristic.Performed, Is.EqualTo([1]));
                Assert.That(Attempt(compiler), Is.EqualTo(1));
                Assert.That(output, Does.Contain("Invalid candidate number -2"));
                Assert.That(output, Does.Contain("Invalid candidate number -3"));
            }
        });
    }

    [Test]
    public static void AnnouncementsReportNativeSaltAndReplayScript()
    {
        WithCompiler(compiler => {
            RandomSalt(ref JitConfig) = 73;
            var bytes = Encoding.ASCII.GetBytes("3,1\0");
            fixed (byte* configuration = bytes)
            {
                ReplayConfig(ref JitConfig) = configuration;
                compiler.verbose = true;
                var random = new RecordingRandom(compiler);
                var replay = new RecordingReplay(compiler);

                var output = CodeGenLifeTransitionTests.Capture(() => {
                    random.Announce();
                    replay.Announce();
                });

                Assert.That(output, Does.Contain("JitRandomCSE is enabled with salt 73"));
                Assert.That(output, Does.Contain("JitReplayCSE is enabled with config 3,1"));
            }
        });
    }

    [Test]
    public static void EmptyCandidateTablesDoNotReadReplayConfigurationOrDrawRandomNumbers()
    {
        WithCompiler(compiler => {
            var random = new RecordingRandom(compiler);
            var replay = new RecordingReplay(compiler);

            random.ConsiderCandidates();
            replay.ConsiderCandidates();

            Assert.That(SortedCandidates(random), Is.Null);
            Assert.That(Attempt(compiler), Is.Zero);
            Assert.That(random.MadeChanges(), Is.False);
            Assert.That(replay.MadeChanges(), Is.False);
        });
    }

    [Test]
    public static void MissingReplayConfigurationFailsRatherThanSelectingNoCandidates()
    {
        WithCompiler(compiler => {
            _ = CreateCandidates(compiler, 1);
            var replay = new RecordingReplay(compiler);
            _ = Assert.Throws<FatalJitException>(replay.ConsiderCandidates);
            Assert.That(Attempt(compiler), Is.Zero);
        });
    }

    private static CSEdsc[] CreateCandidates(Compiler compiler, int count)
    {
        var store = new ValueNumStore(compiler);
        compiler.vnStore = store;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        var descriptors = new CSEdsc[count];
        for (var i = 0; i < count; i++)
        {
            var tree = compiler.gtNewIconNode(TYP_INT, i + 1);
            tree.SetCosts(1, 1);
            descriptors[i] = new CSEdsc(tree, compiler.gtNewStmt(tree), block)
            {
                csdIndex = i + 1,
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
        compiler.info.compFullName = nameof(CSEHeuristicModesTests);
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

    private sealed class RecordingRandom(Compiler compiler) : CSE_HeuristicRandom(compiler)
    {
        public List<int> Performed { get; } = [];

        public override void PerformCSE(CSE_Candidate candidate) => Performed.Add(candidate.CseIndex());
    }

    private sealed class RecordingReplay(Compiler compiler) : CSE_HeuristicReplay(compiler)
    {
        public List<int> Performed { get; } = [];

        public override void PerformCSE(CSE_Candidate candidate) => Performed.Add(candidate.CseIndex());
    }
}
#endif
