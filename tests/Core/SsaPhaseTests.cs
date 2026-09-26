// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SsaPhaseTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void DiamondBuildAndDeepRebuildPreserveNamesAndPhaseState(bool rebuild)
    {
        WithCompiler(3, compiler => {
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], []]);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[2].setLvRefCnt(0);
            compiler.lvaTable[2].setLvRefCntWtd(0);
            compiler.lvaTable[2].lvInSsa = true;
            var deadStore = AddStatement(compiler, blocks[0],
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 0)));
            _ = AddStatement(compiler, blocks[0], new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_NE, TYP_INT, new GenTreeLclVar(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0))));
            var first = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 10));
            var second = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 20));
            _ = AddStatement(compiler, blocks[1], first);
            _ = AddStatement(compiler, blocks[2], second);
            var result = new GenTreeLclVar(TYP_INT, 1);
            _ = AddStatement(compiler, blocks[3], new GenTreeUnOp(GT_RETURN, TYP_INT, result));
            _ = ComputeDominators(compiler);

            Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var phi = GetPhiDefinition(blocks[3]);
            var names = new[] { first.SsaNum, second.SsaNum, phi.SsaNum };
            var counts = compiler.lvaTable.Select(local => local.lvPerSsaData.Count).ToArray();
            var memoryCount = MemoryDefinitions(compiler).Count;
            Assert.That(blocks[0].Statements.Any(statement => ReferenceEquals(statement, deadStore)), Is.False);
            Assert.That(compiler.lvaTable[2].lvInSsa, Is.False);

            if (rebuild)
            {
                _ = compiler.lvaTable[1].lvPerSsaData.AllocSsaNum();
                _ = compiler.AllocMemorySsaNum();
                var staleMap = new Dictionary<GenTree, int> { [result] = 99 };
                foreach (var kind in new AllMemoryKinds())
                {
                    compiler._memorySsaMap[(int)kind] = staleMap;
                }
                compiler._outlinedCompositeSsaNums = [128, 129];

                Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

                var rebuiltPhi = GetPhiDefinition(blocks[3]);
                Assert.That(rebuiltPhi, Is.Not.SameAs(phi));
                phi = rebuiltPhi;
                Assert.That(new[] { first.SsaNum, second.SsaNum, phi.SsaNum }, Is.EqualTo(names));
                Assert.That(compiler.lvaTable.Select(local => local.lvPerSsaData.Count), Is.EqualTo(counts));
                Assert.That(MemoryDefinitions(compiler).Count, Is.EqualTo(memoryCount));
                Assert.That(compiler._outlinedCompositeSsaNums, Is.Empty);
                foreach (var kind in new AllMemoryKinds())
                {
                    Assert.That(compiler._memorySsaMap[(int)kind], Is.Null);
                }
            }

            Assert.That(phi.Data.Oper, Is.EqualTo(GT_PHI));
            Assert.That(names, Is.Unique);
            Assert.That(names.All(number => number > SsaConfig.RESERVED_SSA_NUM), Is.True);
            Assert.That(result.SsaNum, Is.EqualTo(phi.SsaNum));
            Assert.That(PhiArguments(phi), Is.EquivalentTo(new[] {
                (blocks[1].bbNum, first.SsaNum), (blocks[2].bbNum, second.SsaNum),
            }));
            Assert.That(compiler.fgSsaPassesCompleted, Is.EqualTo(rebuild ? 2 : 1));
            Assert.That(compiler.fgSsaValid, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.True);
            Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(Phases.PHASE_BUILD_SSA_RENAME));
#if DEBUG
            compiler.fgDebugCheckSsa();
#endif
        });
    }

    [Test]
    public static void LoopHeaderPhiNamesTheBackedgeUseAndExit()
    {
        WithCompiler(2, compiler => {
            var blocks = CreateGraph(compiler, [[1], [2, 3], [1], []]);
            compiler.lvaTable[0].lvIsParam = true;
            var initial = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 0));
            _ = AddStatement(compiler, blocks[0], initial);
            var conditionUse = new GenTreeLclVar(TYP_INT, 1);
            _ = AddStatement(compiler, blocks[1], new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_LT, TYP_INT, conditionUse, new GenTreeLclVar(TYP_INT, 0))));
            var backedgeUse = new GenTreeLclVar(TYP_INT, 1);
            var update = compiler.gtNewStoreLclVarNode(1,
                new GenTreeOp(GT_ADD, TYP_INT, backedgeUse, compiler.gtNewIconNode(TYP_INT, 1)));
            _ = AddStatement(compiler, blocks[2], update);
            var result = new GenTreeLclVar(TYP_INT, 1);
            _ = AddStatement(compiler, blocks[3], new GenTreeUnOp(GT_RETURN, TYP_INT, result));
            _ = ComputeDominators(compiler);

            Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var phi = GetPhiDefinition(blocks[1]);
            Assert.That(phi.Data.Oper, Is.EqualTo(GT_PHI));
            Assert.That(PhiArguments(phi), Is.EquivalentTo(new[] {
                (blocks[0].bbNum, initial.SsaNum), (blocks[2].bbNum, update.SsaNum),
            }));
            Assert.That(conditionUse.SsaNum, Is.EqualTo(phi.SsaNum));
            Assert.That(backedgeUse.SsaNum, Is.EqualTo(phi.SsaNum));
            Assert.That(result.SsaNum, Is.EqualTo(phi.SsaNum));
            Assert.That(update.SsaNum, Is.Not.EqualTo(phi.SsaNum));
            Assert.That(compiler.fgSsaValid, Is.True);
#if DEBUG
            compiler.fgDebugCheckSsa();
#endif
        });
    }

    [Test]
    public static void BuildRemovesPrologCoveredZeroStoreBeforeRenaming()
    {
        WithCompiler(1, compiler => {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = CreateGraph(compiler, [[]])[0];
            _ = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0)));
            var value = new GenTreeLclVar(TYP_INT, 0);
            var result = AddStatement(compiler, block, new GenTreeUnOp(GT_RETURN, TYP_INT, value));
            _ = ComputeDominators(compiler);

            Assert.That(compiler.fgSsaBuild(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            Assert.That(block.FirstStmt, Is.SameAs(result));
            Assert.That(compiler.lvaTable[0].lvSuppressedZeroInit, Is.True);
            Assert.That(compiler.lvaTable[0].lvInSsa, Is.False);
            Assert.That(value.SsaNum, Is.EqualTo(SsaConfig.RESERVED_SSA_NUM));
            Assert.That(compiler.fgSsaPassesCompleted, Is.EqualTo(1));
#if DEBUG
            compiler.fgDebugCheckSsa();
#endif
        });
    }

    private static GenTreeLclVar GetPhiDefinition(BasicBlock block)
    {
        if (block.FirstStmt is not Statement statement || !statement.IsPhiDefnStmt)
        {
            throw new InvalidOperationException("SSA did not insert the expected PHI definition.");
        }

        return statement.RootNode.AsLclVar();
    }

    private static (int Block, int Ssa)[] PhiArguments(GenTreeLclVar phi)
    {
        var arguments = new List<(int Block, int Ssa)>();
        foreach (var use in phi.Data.AsPhi().Uses)
        {
            var arg = use.Node.AsPhiArg();
            arguments.Add((arg.PredBB.bbNum, arg.SsaNum));
        }

        return [.. arguments];
    }

    private static BasicBlock[] CreateGraph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[i].bbRefs = 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        for (var i = 0; i < blocks.Length; i++)
        {
            if (successors[i].Length == 1)
            {
                blocks[i].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[successors[i][0]], blocks[i]));
            }
            else if (successors[i].Length == 2)
            {
                blocks[i].SetCond(compiler.fgAddRefPred(blocks[successors[i][0]], blocks[i]),
                    compiler.fgAddRefPred(blocks[successors[i][1]], blocks[i]));
            }
        }

        return blocks;
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = new Statement(root, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);

        return statement;
    }

    private static void WithCompiler(int count, Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(count, compiler => {
            compiler.info.compInitMem = true;
            compiler.info.compRetType = TYP_INT;
            compiler.info.compRetNativeType = TYP_INT;
            compiler.fgPredsComputed = true;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            action(compiler);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDominators")]
    private static extern PhaseStatus ComputeDominators(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "lvMemoryPerSsaData")]
    private static extern ref SsaDefArray<SsaMemDef> MemoryDefinitions(Compiler compiler);
}
