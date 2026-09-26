// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicSelectionTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Candidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "sortTab")]
    private static extern ref CSEdsc?[]? SortedCandidates(CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "sortSiz")]
    private static extern ref nuint SortedSize(CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "moderateRefCnt")]
    private static extern ref double ModerateCutoff(CSE_Heuristic heuristic);

    [Test]
    public static void CandidateCountsUseSameDescriptorAndWeightedExecutionCosts()
    {
        WithCompiler(compiler =>
        {
            var descriptor = MakeDescriptor(compiler, 1, 9, 3);
            descriptor.csdDefCount = 2;
            descriptor.csdUseCount = 3;
            descriptor.csdDefWtCnt = 125;
            descriptor.csdUseWtCnt = 275;
            var heuristic = new CSE_Heuristic(compiler);
            var candidate = new CSE_Candidate(heuristic, descriptor);

            candidate.InitializeCounts();

            Assert.Multiple(() =>
            {
                Assert.That(candidate.CseDsc(), Is.SameAs(descriptor));
                Assert.That(candidate.CseIndex(), Is.EqualTo(1));
                Assert.That(candidate.Expr(), Is.SameAs(descriptor.csdTreeList.tslTree));
                Assert.That(candidate.Size(), Is.EqualTo(3));
                Assert.That(candidate.Cost(), Is.EqualTo(9));
                Assert.That(candidate.DefCount(), Is.EqualTo(125));
                Assert.That(candidate.UseCount(), Is.EqualTo(275));
            });
        });
    }

    [TestCase(TYP_INT, TYP_INT, true)]
    [TestCase(TYP_BYREF, TYP_I_IMPL, true)]
    [TestCase(TYP_I_IMPL, TYP_BYREF, true)]
    [TestCase(TYP_BYREF, TYP_REF, false)]
    [TestCase(TYP_INT, TYP_LONG, false)]
    public static void RewriteCompatibilityPreservesNativeTypePairs(
        var_types localType, var_types expressionType, bool expected)
    {
        WithCompiler(compiler =>
        {
            var heuristic = new CSE_Heuristic(compiler);
            Assert.That(heuristic.IsCompatibleType(localType, expressionType), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void SortCandidatesUsesCostUseDefIndexAndCanonicalDescriptorIdentity()
    {
        WithCompiler(compiler =>
        {
            var first = MakeDescriptor(compiler, 1, 5, 3);
            var second = MakeDescriptor(compiler, 2, 5, 3);
            var third = MakeDescriptor(compiler, 3, 9, 2);
            first.csdUseWtCnt = 20;
            second.csdUseWtCnt = 20;
            third.csdUseWtCnt = 10;
            first.csdDefWtCnt = 4;
            second.csdDefWtCnt = 4;
            Candidates(compiler) = [first, second, third];
            CandidateCount(compiler) = 3;
            var heuristic = new CSE_Heuristic(compiler);

            heuristic.SortCandidates();

            var sorted = SortedCandidates(heuristic);
            Assert.Multiple(() =>
            {
                Assert.That(sorted, Has.Length.EqualTo(3));
                Assert.That(sorted![0], Is.SameAs(third));
                Assert.That(sorted[1], Is.SameAs(first));
                Assert.That(sorted[2], Is.SameAs(second));
                Assert.That(SortedSize(heuristic), Is.EqualTo((nuint)(3 * IntPtr.Size)));
            });
        });
    }

    [TestCase(10, 100, 300, 50, 100, true, true, false, false)]
    [TestCase(10, 40, 80, 200, 100, true, false, true, false)]
    [TestCase(1, 10, 20, 200, 100, false, false, false, true)]
    public static void PromotionUsesNativeCostAndRegisterPressureCategories(
        byte cost, double defWeight, double useWeight, double aggressive, double moderate,
        bool expectedPromotion, bool expectedAggressive, bool expectedModerate, bool expectedConservative)
    {
        WithCompiler(compiler =>
        {
            var descriptor = MakeDescriptor(compiler, 1, cost, 3);
            descriptor.csdDefCount = 1;
            descriptor.csdUseCount = 1;
            descriptor.csdDefWtCnt = defWeight;
            descriptor.csdUseWtCnt = useWeight;
            var heuristic = new CSE_Heuristic(compiler);
            AggressiveCutoff(heuristic) = aggressive;
            ModerateCutoff(heuristic) = moderate;
            var candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();

            var promote = heuristic.PromotionCheck(candidate);

            Assert.Multiple(() =>
            {
                Assert.That(promote, Is.EqualTo(expectedPromotion));
                Assert.That(candidate.IsAggressive(), Is.EqualTo(expectedAggressive));
                Assert.That(candidate.IsModerate(), Is.EqualTo(expectedModerate));
                Assert.That(candidate.IsConservative(), Is.EqualTo(expectedConservative));
            });
        });
    }

    [TestCase(false, 100, 50)]
    [TestCase(true, 200, 100)]
    public static void SuccessfulCallCrossingPromotionRaisesOnlyExceededCutoffs(
        bool liveAcrossCall, double expectedAggressive, double expectedModerate)
    {
        WithCompiler(compiler =>
        {
            var descriptor = MakeDescriptor(compiler, 1, 5, 3);
            descriptor.csdDefWtCnt = 50;
            descriptor.csdUseWtCnt = 50;
            descriptor.csdLiveAcrossCall = liveAcrossCall;
            var heuristic = new CSE_Heuristic(compiler);
            AggressiveCutoff(heuristic) = 100;
            ModerateCutoff(heuristic) = 50;
            var candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();

            heuristic.AdjustHeuristic(candidate);

            Assert.Multiple(() =>
            {
                Assert.That(AggressiveCutoff(heuristic), Is.EqualTo(expectedAggressive));
                Assert.That(ModerateCutoff(heuristic), Is.EqualTo(expectedModerate));
            });
        });
    }

    private static CSEdsc MakeDescriptor(Compiler compiler, int index, byte costEx, byte costSz)
    {
        var tree = compiler.gtNewIconNode(TYP_INT, index);
        tree.SetCosts(costEx, costSz);
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        return new CSEdsc(tree, compiler.gtNewStmt(tree), block) { csdIndex = index };
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
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
