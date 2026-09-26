// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class IncrementalSsaBuilderTests
{
    [Test]
    public static void LiveInKeysDistinguishBlocksAndLocals()
    {
        SsaLivenessTests.WithCompiler(2, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], [2], []]);
            compiler.lvaTable[0].lvInSsa = true;
            compiler.lvaTable[1].lvInSsa = true;

            Assert.That(compiler.AddInsertedSsaLiveIn(blocks[1], 0), Is.True);
            Assert.That(compiler.AddInsertedSsaLiveIn(blocks[1], 1), Is.True);
            Assert.That(compiler.AddInsertedSsaLiveIn(blocks[2], 0), Is.True);
            Assert.That(compiler.AddInsertedSsaLiveIn(blocks[1], 0), Is.False);
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[2], 1), Is.False);
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[1], 1), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SingleDefinitionKeepsNormalValueAndPropagatesLivenessToItsBlock(bool withException)
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], [2], []]);
            compiler.vnStore = new ValueNumStore(compiler);
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var vn = compiler.vnStore.VNForIntCon(7);
            var sourceVN = withException
                ? compiler.vnStore.VNWithExc(vn, compiler.vnStore.VNExcSetSingleton(
                    compiler.vnStore.VNForExpr(null, TYP_REF)))
                : vn;
            value._vnPair = new ValueNumPair(sourceVN, sourceVN);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            var defStmt = AddStatement(compiler, blocks[0], store);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            var useStmt = AddStatement(compiler, blocks[2], use);
            var builder = new IncrementalSsaBuilder(compiler, 0);

            builder.InsertDef(new UseDefLocation(blocks[0], defStmt, store));
            Assert.That(builder.FinalizeDefs(), Is.True);
            builder.InsertUse(new UseDefLocation(blocks[2], useStmt, use));

            Assert.That(store.SsaNum, Is.EqualTo(SsaConfig.FIRST_SSA_NUM));
            Assert.That(use.SsaNum, Is.EqualTo(store.SsaNum));
            ref var descriptor = ref compiler.lvaTable[0].GetPerSsaData(store.SsaNum);
            Assert.That(descriptor.Block, Is.SameAs(blocks[0]));
            Assert.That(descriptor.DefNode, Is.SameAs(store));
            Assert.That(descriptor._vnPair, Is.EqualTo(new ValueNumPair(vn, vn)));
            Assert.That(descriptor.NumUses, Is.EqualTo(1));
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[0], 0), Is.False);
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[1], 0), Is.True);
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[2], 0), Is.True);
        });
    }

    [Test]
    public static void DiamondCreatesPrunedPhiWithBothDefinitionsAndValueNumbers()
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], []]);
            compiler.vnStore = new ValueNumStore(compiler);
            var firstValue = compiler.gtNewIconNode(TYP_INT, 11);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 13);
            var firstVN = compiler.vnStore.VNForIntCon(11);
            var secondVN = compiler.vnStore.VNForIntCon(13);
            firstValue._vnPair = new ValueNumPair(firstVN, firstVN);
            secondValue._vnPair = new ValueNumPair(secondVN, secondVN);
            var first = compiler.gtNewStoreLclVarNode(0, firstValue);
            var second = compiler.gtNewStoreLclVarNode(0, secondValue);
            var firstStmt = AddStatement(compiler, blocks[1], first);
            var secondStmt = AddStatement(compiler, blocks[2], second);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            var useStmt = AddStatement(compiler, blocks[3], use);
            var repeatedUse = compiler.gtNewLclvNode(TYP_INT, 0);
            var repeatedStmt = AddStatement(compiler, blocks[3], repeatedUse);
            var builder = new IncrementalSsaBuilder(compiler, 0);

            builder.InsertDef(new UseDefLocation(blocks[1], firstStmt, first));
            builder.InsertDef(new UseDefLocation(blocks[2], secondStmt, second));
            Assert.That(builder.FinalizeDefs(), Is.True);
            builder.InsertUse(new UseDefLocation(blocks[3], useStmt, use));
            builder.InsertUse(new UseDefLocation(blocks[3], repeatedStmt, repeatedUse));

            var phiStmt = blocks[3].FirstStmt;
            Assert.That(phiStmt?.IsPhiDefnStmt, Is.True);
            var phiDef = phiStmt!.RootNode.AsLclVar();
            Assert.That(phiDef.SsaNum, Is.EqualTo(second.SsaNum + 1));
            Assert.That(use.SsaNum, Is.EqualTo(phiDef.SsaNum));
            Assert.That(repeatedUse.SsaNum, Is.EqualTo(phiDef.SsaNum));
            Assert.That(phiStmt.TreeListBegin!.Oper, Is.EqualTo(GT_PHI_ARG));
            var args = new Dictionary<BasicBlock, int>();
            foreach (var arg in phiDef.Data.AsPhi().Uses)
            {
                args.Add(arg.Node.AsPhiArg().PredBB, arg.Node.AsPhiArg().SsaNum);
            }
            Assert.That(args, Has.Count.EqualTo(2));
            Assert.That(args[blocks[1]], Is.EqualTo(first.SsaNum));
            Assert.That(args[blocks[2]], Is.EqualTo(second.SsaNum));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(first.SsaNum).HasPhiUse, Is.True);
            Assert.That(compiler.lvaTable[0].GetPerSsaData(second.SsaNum).HasPhiUse, Is.True);
            Assert.That(compiler.lvaTable[0].GetPerSsaData(phiDef.SsaNum)._vnPair.BothDefined(), Is.True);
            Assert.That(compiler.lvaTable[0].GetPerSsaData(phiDef.SsaNum).NumUses, Is.EqualTo(2));
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[3], 0), Is.True);
        });
    }

    [Test]
    public static void LatestStoreInSameStatementReachesSubsequentUse()
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], []]);
            compiler.vnStore = new ValueNumStore(compiler);
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var firstVN = compiler.vnStore.VNForIntCon(1);
            var secondVN = compiler.vnStore.VNForIntCon(2);
            firstValue._vnPair = new ValueNumPair(firstVN, firstVN);
            secondValue._vnPair = new ValueNumPair(secondVN, secondVN);
            var first = compiler.gtNewStoreLclVarNode(0, firstValue);
            var second = compiler.gtNewStoreLclVarNode(0, secondValue);
            var stmt = AddStatement(compiler, blocks[1], compiler.gtNewCommaNode(TYP_INT, first, second));
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            var useStmt = AddStatement(compiler, blocks[1], use);
            var builder = new IncrementalSsaBuilder(compiler, 0);

            builder.InsertDef(new UseDefLocation(blocks[1], stmt, first));
            builder.InsertDef(new UseDefLocation(blocks[1], stmt, second));
            Assert.That(builder.FinalizeDefs(), Is.True);
            builder.InsertUse(new UseDefLocation(blocks[1], useStmt, use));

            Assert.That(use.SsaNum, Is.EqualTo(second.SsaNum));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(second.SsaNum).NumUses, Is.EqualTo(1));
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[1], 0), Is.False);
        });
    }

    [Test]
    public static void UseBetweenStoresInSameStatementReachesFirstDefinition()
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], []]);
            compiler.vnStore = new ValueNumStore(compiler);
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var firstVN = compiler.vnStore.VNForIntCon(1);
            var secondVN = compiler.vnStore.VNForIntCon(2);
            firstValue._vnPair = new ValueNumPair(firstVN, firstVN);
            secondValue._vnPair = new ValueNumPair(secondVN, secondVN);
            var first = compiler.gtNewStoreLclVarNode(0, firstValue);
            var second = compiler.gtNewStoreLclVarNode(0, secondValue);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            var sequence = compiler.gtNewCommaNode(TYP_INT, first,
                compiler.gtNewCommaNode(TYP_INT, use, second));
            var stmt = AddStatement(compiler, blocks[1], sequence);
            var builder = new IncrementalSsaBuilder(compiler, 0);

            builder.InsertDef(new UseDefLocation(blocks[1], stmt, first));
            builder.InsertDef(new UseDefLocation(blocks[1], stmt, second));
            Assert.That(builder.FinalizeDefs(), Is.True);
            builder.InsertUse(new UseDefLocation(blocks[1], stmt, use));

            Assert.That(use.SsaNum, Is.EqualTo(first.SsaNum));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(first.SsaNum).NumUses, Is.EqualTo(1));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(second.SsaNum).NumUses, Is.Zero);
        });
    }

    [Test]
    public static void UnreachableUseTakesFirstRecordedDefinition()
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], [], []]);
            compiler.vnStore = new ValueNumStore(compiler);
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var firstVN = compiler.vnStore.VNForIntCon(1);
            var secondVN = compiler.vnStore.VNForIntCon(2);
            firstValue._vnPair = new ValueNumPair(firstVN, firstVN);
            secondValue._vnPair = new ValueNumPair(secondVN, secondVN);
            var first = compiler.gtNewStoreLclVarNode(0, firstValue);
            var second = compiler.gtNewStoreLclVarNode(0, secondValue);
            var firstStmt = AddStatement(compiler, blocks[0], first);
            var secondStmt = AddStatement(compiler, blocks[1], second);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            var useStmt = AddStatement(compiler, blocks[2], use);
            var builder = new IncrementalSsaBuilder(compiler, 0);

            builder.InsertDef(new UseDefLocation(blocks[0], firstStmt, first));
            builder.InsertDef(new UseDefLocation(blocks[1], secondStmt, second));
            Assert.That(builder.FinalizeDefs(), Is.True);
            builder.InsertUse(new UseDefLocation(blocks[2], useStmt, use));

            Assert.That(use.SsaNum, Is.EqualTo(first.SsaNum));
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[2], 0), Is.True);
            Assert.That(compiler.IsInsertedSsaLiveIn(blocks[0], 0), Is.False);
        });
    }

    [Test]
    public static void DefinitionAfterUseDoesNotReachBackwards()
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var blocks = CreateGraph(compiler, [[1], []]);
            compiler.vnStore = new ValueNumStore(compiler);
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var firstVN = compiler.vnStore.VNForIntCon(1);
            var secondVN = compiler.vnStore.VNForIntCon(2);
            firstValue._vnPair = new ValueNumPair(firstVN, firstVN);
            secondValue._vnPair = new ValueNumPair(secondVN, secondVN);
            var first = compiler.gtNewStoreLclVarNode(0, firstValue);
            var second = compiler.gtNewStoreLclVarNode(0, secondValue);
            var firstStmt = AddStatement(compiler, blocks[1], first);
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            var useStmt = AddStatement(compiler, blocks[1], use);
            var secondStmt = AddStatement(compiler, blocks[1], second);
            var builder = new IncrementalSsaBuilder(compiler, 0);

            builder.InsertDef(new UseDefLocation(blocks[1], firstStmt, first));
            builder.InsertDef(new UseDefLocation(blocks[1], secondStmt, second));
            Assert.That(builder.FinalizeDefs(), Is.True);
            builder.InsertUse(new UseDefLocation(blocks[1], useStmt, use));

            Assert.That(use.SsaNum, Is.EqualTo(first.SsaNum));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(first.SsaNum).NumUses, Is.EqualTo(1));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(second.SsaNum).NumUses, Is.Zero);
        });
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var stmt = compiler.gtNewStmt(root);
        compiler.fgInsertStmtAtEnd(block, stmt);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
        return stmt;
    }

    private static BasicBlock[] CreateGraph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            if (compiler.fgLastBB is BasicBlock previous)
            {
                previous.Next = block;
            }
            else
            {
                compiler.fgFirstBB = block;
            }
            compiler.fgLastBB = block;
            blocks[index] = block;
        }

        for (var index = 0; index < blocks.Length; index++)
        {
            List<FlowEdge> edges = [];
            foreach (var successor in successors[index])
            {
                var target = blocks[successor];
                var edge = new FlowEdge(blocks[index], target, target.bbPreds);
                target.bbPreds = edge;
                edges.Add(edge);
            }

            if (edges.Count == 1)
            {
                blocks[index].SetKindAndTargetEdge(BBJ_ALWAYS, edges[0]);
            }
            else if (edges.Count == 2)
            {
                blocks[index].SetCond(edges[0], edges[1]);
            }
        }

        return blocks;
    }
}
