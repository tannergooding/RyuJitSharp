// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEPhaseTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLCSEGreedy")]
    private static extern ref int Greedy(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEheuristic")]
    private static extern ref CSE_HeuristicCommon? CachedHeuristic(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optValnumCSE_phase")]
    private static extern ref bool PhaseActive(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEstart")]
    private static extern ref int CseStart(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLHook")]
    private static extern ref int Hook(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRLCSE")]
    private static extern ref byte* Rl(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRandomCSE")]
    private static extern ref int Random(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitReplayCSE")]
    private static extern ref byte* Replay(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStress")]
    private static extern ref int Stress(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitNoCSE")]
    private static extern ref int NoCse(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitCSEMask")]
    private static extern ref int CseMask(ref JitConfigValues config);

    [TestCase(1, 1, 1, 1, 1, typeof(CSE_HeuristicRLHook))]
    [TestCase(0, 1, 1, 1, 1, typeof(CSE_HeuristicRL))]
    [TestCase(0, 0, 1, 1, 1, typeof(CSE_HeuristicRandom))]
    [TestCase(0, 0, 0, 1, 1, typeof(CSE_HeuristicReplay))]
    [TestCase(0, 0, 0, 0, 1, typeof(CSE_HeuristicParameterized))]
    [TestCase(0, 0, 0, 0, 0, typeof(CSE_Heuristic))]
    public static void DebugFactoryHonorsNativePriorityAndCachesItsChoice(
        int hook, int rl, int random, int replay, int greedy, Type expected)
    {
        WithCompiler(compiler => {
            var config = Encoding.ASCII.GetBytes("0\0");
            fixed (byte* value = config)
            {
                Hook(ref JitConfig) = hook;
                Rl(ref JitConfig) = rl != 0 ? value : null;
                Random(ref JitConfig) = random;
                Replay(ref JitConfig) = replay != 0 ? value : null;
                Greedy(ref JitConfig) = greedy;

                var selected = compiler.optGetCSEheuristic();
                Hook(ref JitConfig) = 0;
                Rl(ref JitConfig) = null;
                Random(ref JitConfig) = 0;
                Replay(ref JitConfig) = null;
                Greedy(ref JitConfig) = 0;

                Assert.That(selected, Is.TypeOf(expected));
                Assert.That(compiler.optGetCSEheuristic(), Is.SameAs(selected));
                Assert.That(CachedHeuristic(compiler), Is.SameAs(selected));
            }
        });
    }

    [Test]
    public static void StressSelectsRandomAheadOfReplayAndParameterized()
    {
        WithCompiler(compiler => {
            compiler.info.compMethodName = nameof(StressSelectsRandomAheadOfReplayAndParameterized);
            Stress(ref JitConfig) = 1;
            Greedy(ref JitConfig) = 1;
            var config = Encoding.ASCII.GetBytes("1\0");
            fixed (byte* value = config)
            {
                Replay(ref JitConfig) = value;
                Assert.That(compiler.optGetCSEheuristic(), Is.TypeOf<CSE_HeuristicRandom>());
            }
        });
    }

    [TestCase(1)]
    [TestCase(0x0F000000)]
    public static void NoCseSkipsPhaseBeforeCreatingHeuristic(int disable)
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.lvaTable = [];
            NoCse(ref JitConfig) = disable;

            Assert.That(compiler.optOptimizeValnumCSEs(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            compiler.optOptimizeCSEs();
            Assert.That(CachedHeuristic(compiler), Is.Null);
            Assert.That(PhaseActive(compiler), Is.False);
            Assert.That(CseStart(compiler), Is.EqualTo(compiler.lvaCount));
        });
    }

    [Test]
    public static void TreeDumpHonorsInclusiveRangeAndAlwaysPrintsFooter()
    {
        WithCompiler(compiler => {
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            var third = BasicBlock.New(compiler, BBJ_RETURN);
            first.Next = second;
            second.Next = third;
            compiler.fgFirstBB = first;
            compiler.fgLastBB = third;

            var firstDump = CodeGenLifeTransitionTests.Capture(() => compiler.fgDumpBlock(first));
            var secondDump = CodeGenLifeTransitionTests.Capture(() => compiler.fgDumpBlock(second));
            var thirdDump = CodeGenLifeTransitionTests.Capture(() => compiler.fgDumpBlock(third));
            var footer = Environment.NewLine + new string('-', 115) + Environment.NewLine;

            Assert.Multiple(() => {
                Assert.That(CodeGenLifeTransitionTests.Capture(() => compiler.fgDumpTrees(first, second)),
                    Is.EqualTo(firstDump + secondDump + footer));
                Assert.That(CodeGenLifeTransitionTests.Capture(() => compiler.fgDumpTrees(first, null)),
                    Is.EqualTo(firstDump + secondDump + thirdDump + footer));
                Assert.That(CodeGenLifeTransitionTests.Capture(() => compiler.fgDumpTrees(null, null)),
                    Is.EqualTo(footer));
            });
        });
    }
#endif

    [Test]
    public static void EmptyMethodRunsProductionWrapperWithoutPromotingCandidates()
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.lvaTable = [];

            compiler.optOptimizeCSEs();

            Assert.That(CachedHeuristic(compiler), Is.Not.Null);
            Assert.That(PhaseActive(compiler), Is.False);
            Assert.That(CseStart(compiler), Is.EqualTo(compiler.lvaCount));
            Assert.That(CandidateCount(compiler), Is.Zero);
        });
    }

    [Test]
    public static void ProductionWrapperClearsOldTreeMarkersBeforeRepeatingPhase()
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.lvaTable = [];
            var child = compiler.gtNewIconNode(TYP_INT, 7);
            var root = new GenTreeUnOp(GT_RETURN, TYP_INT, child);
            child.SetCosts(1, 1);
            root.SetCosts(1, 1);
            var stmt = compiler.gtNewStmt(root);
            compiler.fgInsertStmtAtEnd(block, stmt);
            compiler.fgSetStmtSeq(stmt);
            Assert.That(root.Prev, Is.SameAs(child));

            compiler.optOptimizeCSEs();
            root._cseNum = 1;
            child._cseNum = -1;
            compiler.optOptimizeCSEs();

            Assert.That(root._cseNum, Is.EqualTo(NO_CSE));
            Assert.That(child._cseNum, Is.EqualTo(NO_CSE));
            Assert.That(CseStart(compiler), Is.EqualTo(compiler.lvaCount));
            Assert.That(CandidateCount(compiler), Is.Zero);
            Assert.That(PhaseActive(compiler), Is.False);
        });
    }

    [Test]
    public static void ProductionWrapperPromotesRepeatedExpression()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [];
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = store.VNForExpr(null, TYP_INT);
#if DEBUG
            CseMask(ref JitConfig) = 1;
#endif

            GenTree NewExpression()
            {
                var operand = compiler.gtNewIconNode(TYP_INT, 7);
                operand.SetCosts(3, 3);
                var expression = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, operand);
                expression._vnPair.SetBoth(vn);
                expression.SetCosts(9, 9);
                return expression;
            }

            var first = NewExpression();
            var second = NewExpression();
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            compiler.fgInsertStmtAtEnd(block, firstStmt);
            compiler.fgInsertStmtAtEnd(block, secondStmt);
            compiler.fgSetStmtSeq(firstStmt);
            compiler.fgSetStmtSeq(secondStmt);

            compiler.optOptimizeCSEs();

            Assert.That(firstStmt.RootNode.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(secondStmt.RootNode.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(firstStmt.RootNode.AsOp().Op1.AsLclVar().LclNum,
                Is.EqualTo(secondStmt.RootNode.AsLclVar().LclNum));
            Assert.That(CandidateCount(compiler), Is.EqualTo(1));
        });
    }

    [TestCase(0, typeof(CSE_Heuristic))]
    [TestCase(1, typeof(CSE_HeuristicParameterized))]
    public static void SharedFactoryChoosesParameterizedOrStandard(int greedy, Type expected)
    {
        WithCompiler(compiler => {
            Greedy(ref JitConfig) = greedy;

            var selected = compiler.optGetCSEheuristic();
            Greedy(ref JitConfig) = 1 - greedy;

            Assert.That(selected, Is.TypeOf(expected));
            Assert.That(compiler.optGetCSEheuristic(), Is.SameAs(selected));
            Assert.That(CachedHeuristic(compiler), Is.SameAs(selected));
        });
    }

    [Test]
    public static void CandidateQueryUsesSelectedHeuristic()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 1));
            tree.SetCosts(9, 9);
            tree._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));

            Assert.That(compiler.optIsCSEcandidate(tree), Is.True);
            Assert.That(CachedHeuristic(compiler), Is.TypeOf<CSE_Heuristic>());

            tree.CanCse = false;
            Assert.That(compiler.optIsCSEcandidate(tree), Is.False);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.compAllowStress = true;
        compiler.info.compFullName = nameof(CSEPhaseTests);
#endif
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        CseStart(compiler) = BAD_VAR_NUM;
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
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }
}
