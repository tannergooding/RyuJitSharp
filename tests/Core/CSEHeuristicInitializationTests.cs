// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicInitializationTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "moderateRefCnt")]
    private static extern ref double ModerateCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "largeFrame")]
    private static extern ref bool LargeFrame(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "cntCalleeTrashInt")]
    private static extern ref int TrashRegisters(Compiler compiler);

    [Test]
    public static void InitializationUsesNativeMinimumCutoffsWithoutLocals()
    {
        WithCompiler(compiler =>
        {
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
            var heuristic = new CSE_Heuristic(compiler);

            heuristic.Initialize();

            Assert.Multiple(() =>
            {
                Assert.That(AggressiveCutoff(heuristic), Is.EqualTo(BB_UNITY_WEIGHT / 2));
                Assert.That(ModerateCutoff(heuristic), Is.EqualTo(BB_UNITY_WEIGHT));
                Assert.That(LargeFrame(heuristic), Is.False);
            });
        });
    }

    [Test]
    public static void InitializationUsesTrackedLocalThresholds()
    {
        WithCompiler(compiler =>
        {
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
            TrashRegisters(compiler) = CNT_CALLEE_TRASH_INT_INIT;
            var count = (CNT_CALLEE_ENREG * 3) + (CNT_CALLEE_TRASH_INT_INIT * 2) + 1;
            compiler.lvaCount = count;
            compiler.lvaTrackedCount = count;
            compiler.lvaTable = new LclVarDsc[count];
            compiler.lvaTrackedToVarNum = new int[count];
            for (var index = 0; index < count; index++)
            {
                ref var local = ref compiler.lvaTable[index];
                local.Type = TYP_INT;
                local.lvIsParam = true;
                local.lvIsRegArg = false;
                local.setLvRefCnt(2);
                local.setLvRefCntWtd(200);
                compiler.lvaTrackedToVarNum[index] = index;
            }

            var heuristic = new CSE_Heuristic(compiler);
            heuristic.Initialize();

            Assert.Multiple(() =>
            {
                Assert.That(AggressiveCutoff(heuristic), Is.EqualTo(300));
                Assert.That(ModerateCutoff(heuristic), Is.EqualTo(250));
            });
        });
    }

    [TestCase(16, false)]
    [TestCase(17, true)]
    public static void InitializationDetectsFrameDisplacementBoundary(int localCount, bool expectedLarge)
    {
        WithCompiler(compiler =>
        {
            compiler.lvaOutgoingArgSpaceVar = localCount;
            compiler.lvaCount = localCount + 1;
            compiler.lvaTable = new LclVarDsc[localCount + 1];
            for (var index = 0; index < localCount; index++)
            {
                ref var local = ref compiler.lvaTable[index];
                local.Type = TYP_LONG;
                local.lvDoNotEnregister = true;
                local.setLvRefCnt(1);
            }

            var heuristic = new CSE_Heuristic(compiler);
            heuristic.Initialize();

            Assert.That(LargeFrame(heuristic), Is.EqualTo(expectedLarge));
        });
    }

    [Test]
    public static void CostOrdersCandidatesByCostThenUseThenDefThenIndex()
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var firstTree = compiler.gtNewIconNode(TYP_INT, 1);
            var secondTree = compiler.gtNewIconNode(TYP_INT, 2);
            firstTree.SetCosts(5, 3);
            secondTree.SetCosts(6, 2);
            var first = new CSEdsc(firstTree, compiler.gtNewStmt(firstTree), block) { csdIndex = 1 };
            var second = new CSEdsc(secondTree, compiler.gtNewStmt(secondTree), block) { csdIndex = 2 };

            Assert.Multiple(() =>
            {
                Assert.That(Compiler.CompareCSECandidatesByExecutionCost(first, second), Is.GreaterThan(0));
                Assert.That(Compiler.CompareCSECandidatesBySize(first, second), Is.LessThan(0));
            });

            secondTree.SetCosts(5, 3);
            first.csdUseWtCnt = 10;
            second.csdUseWtCnt = 20;
            first.csdUseCount = 10;
            second.csdUseCount = 20;
            Assert.That(Compiler.CompareCSECandidatesByExecutionCost(first, second), Is.GreaterThan(0));
            Assert.That(Compiler.CompareCSECandidatesBySize(first, second), Is.GreaterThan(0));

            first.csdUseWtCnt = second.csdUseWtCnt;
            first.csdUseCount = second.csdUseCount;
            first.csdDefWtCnt = 1;
            second.csdDefWtCnt = 2;
            first.csdDefCount = 1;
            second.csdDefCount = 2;
            Assert.That(Compiler.CompareCSECandidatesByExecutionCost(first, second), Is.LessThan(0));
            Assert.That(Compiler.CompareCSECandidatesBySize(first, second), Is.LessThan(0));

            first.csdDefWtCnt = second.csdDefWtCnt;
            first.csdDefCount = second.csdDefCount;
            Assert.That(Compiler.CompareCSECandidatesByExecutionCost(first, second), Is.LessThan(0));
            Assert.That(Compiler.CompareCSECandidatesBySize(first, second), Is.LessThan(0));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
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
