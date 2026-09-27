// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class ColdSectionSelectionTests
{
    [TestCase(BBJ_ALWAYS, 2u)]
    [TestCase(BBJ_EHCATCHRET, 2u)]
    [TestCase(BBJ_LEAVE, 2u)]
    [TestCase(BBJ_COND, 2u)]
    [TestCase(BBJ_CALLFINALLY, 5u)]
    [TestCase(BBJ_CALLFINALLYRET, 0u)]
    [TestCase(BBJ_SWITCH, 10u)]
    [TestCase(BBJ_THROW, 1u)]
    [TestCase(BBJ_EHFINALLYRET, 1u)]
    [TestCase(BBJ_EHFAULTRET, 1u)]
    [TestCase(BBJ_EHFILTERRET, 1u)]
    [TestCase(BBJ_RETURN, 3u)]
    public static void CodeEstimateIncludesControlFlowAndEveryStatement(BBKinds kind, uint controlCost)
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, kind);
            AddCost(compiler, block, 2);
            AddCost(compiler, block, 4);
            Assert.That(compiler.fgGetCodeEstimate(block), Is.EqualTo(controlCost + 6));
        });
    }

    [Test]
    public static void CodeEstimateExcludesPhiDefinitions()
    {
        WithCompiler(compiler => {
            var block = Blocks(compiler, 100)[0];
            var phi = compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_INT));
            phi.SetCosts(254, 254);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(phi));
            AddCost(compiler, block, 5);
            Assert.That(compiler.fgGetCodeEstimate(block), Is.EqualTo(8));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ProcedureSplittingGateControlsPhaseExecution(bool enabled)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0, 0);
            compiler.opts.compProcedureSplitting = enabled;
            Assert.That(compiler.fgDetermineFirstColdBlock(), Is.EqualTo(enabled
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(enabled ? blocks[1] : null));
            Assert.That(blocks[0].HasFlag(BBF_COLD), Is.False);
            Assert.That(blocks[1].HasFlag(BBF_COLD), Is.EqualTo(enabled));
            Assert.That(blocks[2].HasFlag(BBF_COLD), Is.EqualTo(enabled));
        });
    }

    [TestCase(4, false)]
    [TestCase(5, true)]
    public static void LoneColdBlockRequiresEightEstimatedBytes(byte statementCost, bool split)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0);
            AddCost(compiler, blocks[1], statementCost);
            Assert.That(compiler.fgDetermineFirstColdBlock(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(split ? blocks[1] : null));
        });
    }

    [TestCase(4, 2)]
    [TestCase(5, 1)]
    public static void ConditionalFallthroughSkipsTinyColdCandidate(byte statementCost, int firstColdIndex)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0, 0);
            blocks[0].SetKindAndTargetEdge(BBJ_COND, null);
            AddCost(compiler, blocks[1], statementCost);
            AddCost(compiler, blocks[2], 5);
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(blocks[firstColdIndex]));
            Assert.That(blocks[1].HasFlag(BBF_COLD), Is.EqualTo(firstColdIndex == 1));
        });
    }

    [Test]
    public static void LaterHotBlockRestartsTheColdSuffixSearch()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0, 100, 0, 0);
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(blocks[3]));
            Assert.That(blocks[1].HasFlag(BBF_COLD), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(100)]
    public static void EntirelyColdOrHotMethodHasNoSplit(double weight)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, weight, weight, weight);
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.Null);
            Assert.That(blocks, Has.All.Matches<BasicBlock>(block => !block.HasFlag(BBF_COLD)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FuncletsMoveOnlyAsAnEntireColdSection(bool hotFunclet)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, hotFunclet ? 100 : 0, 0);
            compiler.fgFirstFuncletBB = blocks[1];
            Assert.That(compiler.fgFuncletsAreCold(), Is.EqualTo(!hotFunclet));
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(hotFunclet ? null : blocks[1]));
            Assert.That(blocks[2].HasFlag(BBF_COLD), Is.EqualTo(!hotFunclet));
        });
    }

    [Test]
    public static void HotFuncletCancelsAnEarlierMainBodyCandidate()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0, 100, 0);
            compiler.fgFirstFuncletBB = blocks[2];
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.Null);
        });
    }

    [Test]
    public static void ColdBoundaryDoesNotSeparateCallFinallyPair()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0, 0);
            blocks[0].SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(blocks[2], blocks[0]));
            blocks[1].SetKindAndTargetEdge(BBJ_CALLFINALLYRET, compiler.fgAddRefPred(blocks[2], blocks[1]));
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(blocks[2]));
            Assert.That(blocks[1].HasFlag(BBF_COLD), Is.False);
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void DebugEHGatePreservesNoChangeStatus(bool allowEH)
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 0, 0);
            compiler.compHndBBtabCount = 1;
            compiler.opts.compProcedureSplittingEH = allowEH;
            Assert.That(compiler.fgDetermineFirstColdBlock(), Is.EqualTo(allowEH
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(allowEH ? blocks[1] : null));
        });
    }

    [Test]
    public static void StressSplittingBypassesWeightAndSingleBlockSize()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, 100, 100);
            StressProcedureSplitting(ref JitConfig) = 1;
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.SameAs(blocks[1]));
            Assert.That(blocks[1].HasFlag(BBF_COLD), Is.True);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(100));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StressSplittingDoesNotCreateAnEmptyColdSection(bool finallyPair)
    {
        WithCompiler(compiler => {
            var blocks = finallyPair ? Blocks(compiler, 100, 100) : Blocks(compiler, 100);
            if (finallyPair)
            {
                blocks[0].SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(blocks[0], blocks[0]));
                blocks[1].SetKindAndTargetEdge(BBJ_CALLFINALLYRET, compiler.fgAddRefPred(blocks[0], blocks[1]));
            }
            StressProcedureSplitting(ref JitConfig) = 1;
            _ = compiler.fgDetermineFirstColdBlock();
            Assert.That(compiler.fgFirstColdBlock, Is.Null);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressProcedureSplitting")]
    private static extern ref int StressProcedureSplitting(ref JitConfigValues config);
#endif

    private static void AddCost(Compiler compiler, BasicBlock block, byte cost)
    {
        var node = compiler.gtNewIconNode(TYP_INT, 1);
        node.SetCosts(cost, cost);
        compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(node));
    }

    private static BasicBlock[] Blocks(Compiler compiler, params double[] weights)
    {
        var blocks = new BasicBlock[weights.Length];
        for (var index = 0; index < weights.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index].setBBProfileWeight(weights[index]);
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        return blocks;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                compiler.opts.compProcedureSplitting = true;
#if DEBUG
                compiler.opts.compProcedureSplittingEH = true;
#endif
                action(compiler);
            });
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }
}
