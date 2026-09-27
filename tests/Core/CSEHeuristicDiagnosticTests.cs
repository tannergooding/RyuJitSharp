// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicDiagnosticTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "codeOptKind")]
    private static extern ref Compiler.codeOptimize OptimizeKind(CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "moderateRefCnt")]
    private static extern ref double ModerateCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "largeFrame")]
    private static extern ref bool LargeFrame(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "hugeFrame")]
    private static extern ref bool HugeFrame(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "sortTab")]
    private static extern ref CSEdsc?[]? SortedCandidates(CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Candidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [TestCase(false)]
    [TestCase(true)]
    public static void InitializationCutoffsUseInvariantSixDecimalFormatting(bool verbose)
    {
        WithCompiler(compiler => {
            compiler.verbose = verbose;
            var heuristic = new CSE_Heuristic(compiler);
            AggressiveCutoff(heuristic) = 123.5;
            ModerateCutoff(heuristic) = 456.125;
            var previousCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var output = CodeGenLifeTransitionTests.Capture(heuristic.Initialize);

                if (verbose)
                {
                    Assert.That(output, Does.StartWith(
                        $"{Environment.NewLine}Aggressive CSE Promotion cutoff is 123.500000{Environment.NewLine}" +
                        $"Moderate CSE Promotion cutoff is 456.125000{Environment.NewLine}"));
                }
                else
                {
                    Assert.That(output, Is.Empty);
                }

                Assert.That(AggressiveCutoff(heuristic), Is.EqualTo(123.5));
                Assert.That(ModerateCutoff(heuristic), Is.EqualTo(456.125));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        });
    }

    [TestCase(false, false, false, false, 300, 100,
        "Aggressive CSE Promotion (400.000000 >= 300.000000)")]
    [TestCase(false, false, false, false, 500, 100,
        "Moderate CSE Promotion (CSE never live at call) (400.000000 >= 100.000000)")]
    [TestCase(false, true, false, false, 500, 100,
        "Moderate CSE Promotion (CSE is live across a call) (400.000000 >= 100.000000)")]
    [TestCase(false, false, false, false, 500, 500,
        "Conservative CSE Promotion (not enregisterable) (400.000000 < 500.000000)")]
    [TestCase(false, true, false, false, 500, 500,
        "Conservative CSE Promotion (400.000000 < 500.000000)")]
    [TestCase(true, false, false, false, 2, 1,
        "Aggressive CSE Promotion (3.000000 >= 2.000000)")]
    [TestCase(true, false, false, false, 4, 2,
        "Codesize CSE Promotion (small frame)")]
    [TestCase(true, false, true, false, 4, 2,
        "Codesize CSE Promotion (large frame)")]
    [TestCase(true, false, true, true, 4, 2,
        "Codesize CSE Promotion (huge frame)")]
    public static void PromotionCategoryMatchesNativeDiagnostic(
        bool smallCode, bool liveAcrossCall, bool largeFrame, bool hugeFrame,
        double aggressiveCutoff, double moderateCutoff, string expected)
    {
        WithCompiler(compiler =>
        {
            compiler.opts.compCodeOpt = smallCode ? Compiler.SMALL_CODE : Compiler.BLENDED_CODE;
            var descriptor = MakeDescriptor(compiler);
            descriptor.csdLiveAcrossCall = liveAcrossCall;
            var heuristic = new CSE_Heuristic(compiler);
            if (smallCode)
            {
                OptimizeKind(heuristic) = Compiler.SMALL_CODE;
            }
            AggressiveCutoff(heuristic) = aggressiveCutoff;
            ModerateCutoff(heuristic) = moderateCutoff;
            LargeFrame(heuristic) = largeFrame;
            HugeFrame(heuristic) = hugeFrame;
            var candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();

            var output = CodeGenLifeTransitionTests.Capture(() => heuristic.PromotionCheck(candidate));

            Assert.That(output, Does.Contain(expected));
        });
    }

    [Test]
    public static void ProfitabilityScoresAndCallSuffixUseNativePrecision()
    {
        WithCompiler(compiler =>
        {
            var descriptor = MakeDescriptor(compiler);
            var heuristic = new CSE_Heuristic(compiler);
            AggressiveCutoff(heuristic) = 300;
            ModerateCutoff(heuristic) = 100;
            var candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();

            var output = CodeGenLifeTransitionTests.Capture(() => heuristic.PromotionCheck(candidate));

            Assert.That(output, Does.Contain(
                "cseRefCnt=400.000000, aggressiveRefCnt=300.000000, moderateRefCnt=100.000000"));
            Assert.That(output, Does.Contain("defCnt=100.000000, useCnt=200.000000, cost=9, size=3"));
            Assert.That(output, Does.Contain(
                "def_cost=1, use_cost=1, extra_no_cost=4, extra_yes_cost=0"));
            Assert.That(output, Does.Contain(
                "CSE cost savings check (1804.000000 >= 300.000000) passes"));

            descriptor.csdLiveAcrossCall = true;
            candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();
            output = CodeGenLifeTransitionTests.Capture(() => heuristic.PromotionCheck(candidate));
            Assert.That(output, Does.Contain("cost=9, size=3, LiveAcrossCall"));
        });
    }

    [Test]
    public static void ModerateStructPromotionReportsNonEnregisterableReason()
    {
        WithCompiler(compiler =>
        {
            var descriptor = MakeDescriptor(compiler, structExpression: true);
            var heuristic = new CSE_Heuristic(compiler);
            AggressiveCutoff(heuristic) = 500;
            ModerateCutoff(heuristic) = 100;
            var candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();

            var output = CodeGenLifeTransitionTests.Capture(() => heuristic.PromotionCheck(candidate));

            Assert.That(output, Does.Contain(
                "Moderate CSE Promotion (not enregisterable) (400.000000 >= 100.000000)"));
        });
    }

    [Test]
    public static void SelectionHeaderUsesNativeWidthAndInvariantSixDecimalWeights()
    {
        WithCompiler(compiler =>
        {
            var descriptor = MakeDescriptor(compiler);
            descriptor.csdHashKey = 0x2a;
            descriptor.defExcSetPromise = 0x11;
            descriptor.csdDefWtCnt = 100.5;
            descriptor.csdUseWtCnt = 200.25;
            Candidates(compiler) = [descriptor];
            CandidateCount(compiler) = 1;
            var heuristic = new CSE_Heuristic(compiler);
            SortedCandidates(heuristic) = [descriptor];

            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var output = CodeGenLifeTransitionTests.Capture(heuristic.ConsiderCandidates);
                Assert.That(output, Does.Contain(
                    "Considering CSE #01 {$2a , $11 } [def=100.500000, use=200.250000, cost=  9      ]"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        });
    }

    private static CSEdsc MakeDescriptor(Compiler compiler, bool structExpression = false)
    {
        GenTree tree;
        if (structExpression)
        {
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16) }];
            tree = compiler.gtNewLclvNode(TYP_STRUCT, 0);
        }
        else
        {
            tree = compiler.gtNewIconNode(TYP_INT, 7);
        }
        tree.SetCosts(9, 3);
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        return new CSEdsc(tree, compiler.gtNewStmt(tree), block)
        {
            csdIndex = 1,
            csdDefCount = 1,
            csdUseCount = 1,
            csdDefWtCnt = 100,
            csdUseWtCnt = 200,
            defExcSetPromise = 0x11,
        };
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compCodeOpt = Compiler.BLENDED_CODE;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        compiler.verbose = true;
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
}
#endif
