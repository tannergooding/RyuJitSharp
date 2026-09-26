// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InductionVariablePhaseTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnableInductionVariableOpts")]
    private static extern ref int EnableInductionVariables(ref JitConfigValues config);

    [Test]
    public static void CountedLoopBecomesDownwardsAndUnusedPrimaryIVIsRemoved()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        EnableInductionVariables(ref JitConfig) = 1;

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 0 }];
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
#if DEBUG
        compiler.info.compFullName = nameof(CountedLoopBecomesDownwardsAndUnusedPrimaryIVIsRemoved);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var preheader = BasicBlock.New(compiler, BBJ_ALWAYS);
            var header = BasicBlock.New(compiler, BBJ_ALWAYS);
            var latch = BasicBlock.New(compiler, BBJ_COND);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            preheader.Next = header;
            header.Prev = preheader;
            header.Next = latch;
            latch.Prev = header;
            latch.Next = exit;
            exit.Prev = latch;
            compiler.fgFirstBB = preheader;
            compiler.fgLastBB = exit;
            compiler.fgPredsComputed = true;
            exit.bbLiveIn = VarSetOps.MakeEmpty(compiler);
            preheader.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(header, preheader));
            header.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(latch, header));
            latch.SetCond(compiler.fgAddRefPred(header, latch), compiler.fgAddRefPred(exit, latch));

            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.lvInSsa = true;
            var enter = descriptor.lvPerSsaData.AllocSsaNum();
            var current = descriptor.lvPerSsaData.AllocSsaNum();
            var backedge = descriptor.lvPerSsaData.AllocSsaNum();
            var init = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            init.SsaNum = enter;
            descriptor.GetPerSsaData(enter) = new LclSsaVarDsc(preheader, init);
            compiler.fgInsertStmtAtEnd(preheader, compiler.fgNewStmtFromTree(init));

            var phi = new GenTreePhi(TYP_INT)
            {
                FirstUse = new GenTreePhi.Use(new GenTreePhiArg(TYP_INT, 0, enter, preheader))
                {
                    Next = new GenTreePhi.Use(new GenTreePhiArg(TYP_INT, 0, backedge, latch)),
                },
            };
            var phiStore = compiler.gtNewStoreLclVarNode(0, phi);
            phiStore.SsaNum = current;
            descriptor.GetPerSsaData(current) = new LclSsaVarDsc(header, phiStore);
            var phiStmt = compiler.fgNewStmtFromTree(phiStore);
            compiler.fgInsertStmtAtEnd(header, phiStmt);

            var currentForStep = compiler.gtNewLclvNode(TYP_INT, 0);
            currentForStep.SsaNum = current;
            var next = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                currentForStep, compiler.gtNewIconNode(TYP_INT, 1));
            var update = compiler.gtNewStoreLclVarNode(0, next);
            update.SsaNum = backedge;
            descriptor.GetPerSsaData(backedge) = new LclSsaVarDsc(latch, update);
            var updateStmt = compiler.fgNewStmtFromTree(update);
            compiler.fgInsertStmtAtEnd(latch, updateStmt);

            var currentForTest = compiler.gtNewLclvNode(TYP_INT, 0);
            currentForTest.SsaNum = current;
            var compare = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                currentForTest, compiler.gtNewIconNode(TYP_INT, 5));
            var testStmt = compiler.fgNewStmtFromTree(
                compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare));
            compiler.fgInsertStmtAtEnd(latch, testStmt);

            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler.fgMightHaveNaturalLoops = compiler._dfsTree.HasCycle;
            compiler.vnStore = new ValueNumStore(compiler);
            Assert.That(compiler._loops.NumLoops, Is.EqualTo(1));
            Assert.That(compiler._loops.GetLoopByIndex(0).GetPreheader(), Is.SameAs(preheader));

            Assert.That(compiler.optInductionVariables(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.LoopsMadeDownwardsCounted, Is.EqualTo(1));
            Assert.That(compiler.Metrics.UnusedIVsRemoved, Is.EqualTo(1));
            Assert.That(compare.Oper, Is.EqualTo(GT_NE));
            Assert.That(compare.AsOp().Op2.IsIntegralConst(0), Is.True);
            Assert.That(header.FirstStmt, Is.SameAs(phiStmt));
            Assert.That(preheader.LastStmt?.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(preheader.LastStmt?.RootNode.AsLclVarCommon().Data.AsIntConCommon().IntegralValue,
                Is.EqualTo(6));
            Assert.That(updateStmt, Is.Not.SameAs(latch.FirstStmt));
            Assert.That(latch.FirstStmt?.NextStmt, Is.SameAs(testStmt));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }
}
