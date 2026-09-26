// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicParameterizedTests
{
    private static readonly double[] s_expectedLocalWeights = [3];
    private static readonly int[] s_bothCandidates = [2, 1];
    private static readonly int[] s_firstCandidate = [2];

    private sealed class RecordingHeuristic(Compiler compiler) : CSE_HeuristicParameterized(compiler)
    {
        public readonly List<int> Performed = [];
        public bool UnmarkFirst;
        public CSEdsc? InvalidatedCandidate;

        public override void PerformCSE(CSE_Candidate candidate)
        {
            Performed.Add(candidate.CseIndex());
            m_addCSEcount++;
            if (UnmarkFirst && (Performed.Count == 1))
            {
                if (InvalidatedCandidate is not CSEdsc invalidated)
                {
                    throw new InvalidOperationException("An invalidation target is required.");
                }
                invalidated.defExcSetPromise = ValueNumStore.NoVN;
                Unmarks(m_compiler)++;
            }
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Candidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEunmarks")]
    private static extern ref int Unmarks(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "cntCalleeTrashInt")]
    private static extern ref int CalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "sortTab")]
    private static extern ref CSEdsc?[]? SortedCandidates(CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_parameters")]
    private static extern ref double[] Parameters(CSE_HeuristicParameterized heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_registerPressure")]
    private static extern ref uint RegisterPressure(CSE_HeuristicParameterized heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_localWeights")]
    private static extern ref List<double>? LocalWeights(CSE_HeuristicParameterized heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_addCSEcount")]
    private static extern ref uint AddedCount(CSE_HeuristicCommon heuristic);

    [Test]
    public static void FeatureVectorPreservesScaleLogDistanceAndPhysicalCallScan()
    {
        WithCompiler(0, compiler =>
        {
            var blocks = new BasicBlock[4];
            for (var index = 0; index < blocks.Length; index++)
            {
                blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
                blocks[index].bbPostorderNum = index;
                if (index != 0)
                {
                    blocks[index - 1].Next = blocks[index];
                }
            }
            blocks[1].SetFlags(BBF_HAS_CALL);
            var descriptor = MakeDescriptor(compiler, blocks[1], 1, 3, 2);
            descriptor.csdTreeList.tslNext = new treeStmtLst(descriptor.csdTreeList.tslTree,
                descriptor.csdTreeList.tslStmt, blocks[3]);
            descriptor.csdDefCount = 2;
            descriptor.csdUseCount = 4;
            descriptor.csdDefWtCnt = 0.25;
            descriptor.csdUseWtCnt = 0.5;
            descriptor.numDistinctLocals = 1;
            descriptor.numLocalOccurrences = 2;
            descriptor.csdTreeList.tslTree.Flags |= GTF_MAKE_CSE;
            var heuristic = new CSE_HeuristicParameterized(compiler);
            var features = new double[25];

            heuristic.GetFeatures(descriptor, features);

            Assert.Multiple(() =>
            {
                Assert.That(features[0], Is.EqualTo(3));
                Assert.That(features[1], Is.EqualTo(Math.Log(500)).Within(1e-12));
                Assert.That(features[2], Is.EqualTo(Math.Log(250)).Within(1e-12));
                Assert.That(features[3], Is.EqualTo(2));
                Assert.That(features[4], Is.EqualTo(4));
                Assert.That(features[5], Is.EqualTo(2));
                Assert.That(features[7], Is.EqualTo(5));
                Assert.That(features[14], Is.EqualTo(5));
                Assert.That(features[15], Is.EqualTo(1));
                Assert.That(features[16], Is.EqualTo(2));
                Assert.That(features[18], Is.EqualTo(Math.Log(2000)).Within(1e-12));
                Assert.That(features[19], Is.EqualTo(Math.Log(1000)).Within(1e-12));
                Assert.That(features[20], Is.EqualTo(2.5));
                Assert.That(features[21], Is.EqualTo(5));
                Assert.That(features[22], Is.EqualTo(5));
                Assert.That(features[23], Is.EqualTo(5));
                Assert.That(features[24], Is.Zero);
            });
        });
    }

    [TestCase(0u, 0.001)]
    [TestCase(1u, 50.0)]
    [TestCase(2u, 100.0)]
    [TestCase(3u, 100.0)]
    public static void StoppingPressureAccountsForPriorPromotions(uint alreadyPromoted, double weight)
    {
        WithCompiler(0, compiler =>
        {
            var heuristic = new CSE_HeuristicParameterized(compiler);
            RegisterPressure(heuristic) = 2;
            AddedCount(heuristic) = alreadyPromoted;
            LocalWeights(heuristic) = [100, 50];
            var features = new double[25];

            heuristic.GetFeatures(null, features);

            var expected = Math.Log(Math.Max(0.001, weight) / 0.001);
            Assert.That(features[24], Is.EqualTo(expected).Within(1e-12));
            Assert.That(features[..24], Is.All.EqualTo(0));
        });
    }

    [Test]
    public static void CaptureLocalWeightsKeepsTrackedOrderAndSkipsNonIntegralLocals()
    {
        WithCompiler(4, compiler =>
        {
            compiler.lvaTrackedCount = 4;
            compiler.lvaTrackedToVarNum = [2, 0, 3, 1];
            for (var index = 0; index < 4; index++)
            {
                compiler.lvaTable[index].setLvRefCnt(1);
                compiler.lvaTable[index].setLvRefCntWtd((index + 1) * BB_UNITY_WEIGHT);
            }
            compiler.lvaTable[0].Type = TYP_FLOAT;
            compiler.lvaTable[1].lvDoNotEnregister = true;
            compiler.lvaTable[3].setLvRefCnt(0);
            var heuristic = new CSE_HeuristicParameterized(compiler);

            heuristic.CaptureLocalWeights();

            Assert.That(LocalWeights(heuristic), Is.EqualTo(s_expectedLocalWeights));
        });
    }

    [TestCase(0.0, 0)]
    [TestCase(1.0, 1)]
    public static void GreedyTiesPreferStoppingAndThenLowestCandidateIndex(double costWeight, int chosen)
    {
        WithCompiler(0, compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbPostorderNum = 0;
            var first = MakeDescriptor(compiler, block, 1, 3, 2);
            var second = MakeDescriptor(compiler, block, 2, 3, 2);
            CandidateCount(compiler) = 2;
            var heuristic = new CSE_HeuristicParameterized(compiler);
            Array.Fill(Parameters(heuristic), 0);
            Parameters(heuristic)[0] = costWeight;
            LocalWeights(heuristic) = [];
            SortedCandidates(heuristic) = [first, second];
            List<CSE_HeuristicParameterized.Choice> choices = [];

            var firstChoice = heuristic.ChooseGreedy(choices, recompute: true);
            Assert.That(firstChoice.Descriptor?.csdIndex ?? 0, Is.EqualTo(chosen));

            if (chosen != 0)
            {
                firstChoice.Performed = true;
                SortedCandidates(heuristic)![0] = null;
                var next = heuristic.ChooseGreedy(choices, recompute: false);
                Assert.That(next.Descriptor, Is.SameAs(second));
                next = heuristic.ChooseGreedy(choices, recompute: true);
                Assert.That(next.Descriptor, Is.SameAs(second));
            }
        });
    }

    [Test]
    public static void GreedyRecomputesStoppingPreferenceWithoutRebuildingCandidateFeatures()
    {
        WithCompiler(0, compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbPostorderNum = 0;
            var first = MakeDescriptor(compiler, block, 1, 8, 2);
            var second = MakeDescriptor(compiler, block, 2, 7, 2);
            CandidateCount(compiler) = 2;
            var heuristic = new CSE_HeuristicParameterized(compiler);
            Array.Fill(Parameters(heuristic), 0);
            Parameters(heuristic)[0] = 1;
            Parameters(heuristic)[24] = 1;
            RegisterPressure(heuristic) = 1;
            LocalWeights(heuristic) = [100, 0.001];
            SortedCandidates(heuristic) = [first, second];
            List<CSE_HeuristicParameterized.Choice> choices = [];

            var firstChoice = heuristic.ChooseGreedy(choices, recompute: true);
            Assert.That(firstChoice.Descriptor, Is.SameAs(first));
            firstChoice.Performed = true;
            AddedCount(heuristic) = 1;
            var next = heuristic.ChooseGreedy(choices, recompute: false);
            Assert.That(next.Descriptor, Is.Null);
            Assert.That(next.Preference, Is.EqualTo(Math.Log(100 / 0.001)).Within(1e-12));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GreedyPolicyProcessesEachCandidateAndRecomputesAfterUnmark(bool unmark)
    {
        WithCompiler(0, compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbPostorderNum = 0;
            var first = MakeDescriptor(compiler, block, 1, 3, 2);
            var second = MakeDescriptor(compiler, block, 2, 4, 2);
            CandidateCount(compiler) = 2;
            Candidates(compiler) = [first, second];
            var heuristic = new RecordingHeuristic(compiler)
            {
                UnmarkFirst = unmark,
                InvalidatedCandidate = first,
            };
            Array.Fill(Parameters(heuristic), 0);
            Parameters(heuristic)[0] = 1;

            heuristic.ConsiderCandidates();

            Assert.That(heuristic.Performed, Is.EqualTo(unmark ? s_firstCandidate : s_bothCandidates));
            Assert.That(heuristic.MadeChanges(), Is.True);
            var sorted = SortedCandidates(heuristic);
            Assert.That(sorted, Has.Length.EqualTo(2));
            Assert.That(sorted![0], Is.EqualTo(unmark ? first : null));
            Assert.That(sorted[1], Is.Null);
            Assert.That(Unmarks(compiler), Is.EqualTo(unmark ? 1 : 0));
        });
    }

    private static CSEdsc MakeDescriptor(Compiler compiler, BasicBlock block,
        int index, byte costEx, byte costSz)
    {
        var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
            compiler.gtNewIconNode(TYP_INT, index), compiler.gtNewIconNode(TYP_INT, 1));
        tree.SetCosts(costEx, costSz);
        return new CSEdsc(tree, compiler.gtNewStmt(tree), block)
        {
            csdIndex = index,
            csdDefCount = 1,
            csdUseCount = 1,
            csdDefWtCnt = 1,
            csdUseWtCnt = 1,
            defExcSetPromise = 1,
        };
    }

    private static void WithCompiler(int localCount, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[localCount];
        compiler.lvaCount = localCount;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        CalleeTrash(compiler) = CNT_CALLEE_TRASH_INT_INIT;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
