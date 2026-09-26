// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SsaBuilderTests
{
    [Test]
    public static void PhiInsertionAndArgumentsPreserveHeadOrderAndUseCounts()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var pred1 = AddBlock(compiler);
            var pred2 = AddBlock(compiler);
            var join = AddBlock(compiler);
            var builder = new SsaBuilder(compiler);
            var first = InsertPhi(builder, compiler, join, 0);
            var second = InsertPhi(builder, compiler, join, 0);
            Assert.That(join.FirstStmt, Is.SameAs(second));
            Assert.That(GetPhiNode(builder, join, 0), Is.SameAs(second));
            Assert.That(second.TreeListBegin!.Oper, Is.EqualTo(genTreeOps.GT_PHI));
            Assert.That(second.RootNode.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_VAR));
            Assert.That(second.RootNode.CostEx, Is.Zero);
            Assert.That(second.TreeListBegin.CostSz, Is.Zero);

            var defs = compiler.lvaTable[0].lvPerSsaData;
            var firstNum = defs.AllocSsaNum();
            defs.GetSsaDef(firstNum) = new LclSsaVarDsc(pred1);
            var secondNum = defs.AllocSsaNum();
            defs.GetSsaDef(secondNum) = new LclSsaVarDsc(pred2);
            compiler.lvaTable[0].lvPerSsaData = defs;
            var phi = second.RootNode.AsLclVar().Data.AsPhi();

            AddPhiArg(builder, join, second, phi, 0, firstNum, pred1);
            AddPhiArg(builder, join, second, phi, 0, firstNum, pred1);
            AddPhiArg(builder, join, second, phi, 0, secondNum, pred2);

            Assert.That(phi.FirstUse!.Node.AsPhiArg().SsaNum, Is.EqualTo(secondNum));
            Assert.That(phi.FirstUse.Next!.Node.AsPhiArg().SsaNum, Is.EqualTo(firstNum));
            Assert.That(phi.FirstUse.Next.Next, Is.Null);
            Assert.That(second.TreeListBegin, Is.SameAs(phi.FirstUse.Node));
            Assert.That(second.TreeListBegin!.Next, Is.SameAs(phi.FirstUse.Next.Node));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(firstNum).HasPhiUse, Is.True);
            Assert.That(compiler.lvaTable[0].GetPerSsaData(secondNum).NumUses, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DiamondInsertsPhiAndRenamesLocalAndMemoryAcrossBothPaths(bool sharedMemory)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var blocks = CreateGraph(compiler, [[1, 2], [3], [3], []]);
            compiler.byrefStatesMatchGcHeapStates = sharedMemory;
            ConfigureTrackedLocal(compiler);

            var firstStore = AddStatement(compiler, blocks[1],
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1)));
            var secondStore = AddStatement(compiler, blocks[2],
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 2)));
            var use = new GenTreeLclVar(TYP_INT, 0);
            _ = AddStatement(compiler, blocks[3], use);

            for (var i = 0; i < blocks.Length; i++)
            {
                blocks[i].bbVarDef = SetOps.MakeEmpty(compiler);
                blocks[i].bbLiveIn = SetOps.MakeEmpty(compiler);
            }
            SetOps.AddElemD(compiler, blocks[1].bbVarDef, 0);
            SetOps.AddElemD(compiler, blocks[2].bbVarDef, 0);
            SetOps.AddElemD(compiler, blocks[3].bbLiveIn, 0);
            blocks[1].bbMemoryDef = 3;
            blocks[2].bbMemoryDef = 3;
            blocks[3].bbMemoryLiveIn = 3;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            var builder = new SsaBuilder(compiler);

            InsertPhiFunctions(builder);
            var phiStmt = GetPhiNode(builder, blocks[3], 0);
            Assert.That(phiStmt, Is.Not.Null);
            Assert.That(blocks[3].bbMemorySsaPhiFunc[0], Is.SameAs(BasicBlock.EmptyMemoryPhiDef));
            Assert.That(blocks[3].bbMemorySsaPhiFunc[1],
                Is.SameAs(sharedMemory ? blocks[3].bbMemorySsaPhiFunc[0] : BasicBlock.EmptyMemoryPhiDef));

            RenameVariables(builder);

            var phi = phiStmt!.RootNode.AsLclVar().Data.AsPhi();
            var firstDef = firstStore.RootNode.AsLclVar().SsaNum;
            var secondDef = secondStore.RootNode.AsLclVar().SsaNum;
            var phiArgs = new Dictionary<BasicBlock, int>();
            foreach (var arg in phi.Uses)
            {
                var node = arg.Node.AsPhiArg();
                phiArgs.Add(node.PredBB, node.SsaNum);
            }
            Assert.That(phiArgs, Has.Count.EqualTo(2));
            Assert.That(phiArgs[blocks[1]], Is.EqualTo(firstDef));
            Assert.That(phiArgs[blocks[2]], Is.EqualTo(secondDef));
            Assert.That(use.SsaNum, Is.EqualTo(phiStmt.RootNode.AsLclVar().SsaNum));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(firstDef).HasPhiUse, Is.True);
            Assert.That(blocks[3].bbMemorySsaPhiFunc[0], Is.Not.SameAs(BasicBlock.EmptyMemoryPhiDef));
            Assert.That(blocks[3].bbMemorySsaPhiFunc[0]!._nextArg, Is.Not.Null);
            Assert.That(blocks[3].bbMemorySsaNumIn[0], Is.GreaterThan(1));
            Assert.That(blocks[3].bbMemorySsaNumIn[1], Is.GreaterThan(1));
            if (sharedMemory)
            {
                Assert.That(blocks[3].bbMemorySsaNumIn[1], Is.EqualTo(blocks[3].bbMemorySsaNumIn[0]));
                Assert.That(blocks[3].bbMemorySsaPhiFunc[1], Is.SameAs(blocks[3].bbMemorySsaPhiFunc[0]));
            }
            else
            {
                Assert.That(blocks[3].bbMemorySsaPhiFunc[1], Is.Not.SameAs(blocks[3].bbMemorySsaPhiFunc[0]));
            }
            Assert.That(compiler.Metrics.VarsInSsa, Is.EqualTo(1));
        });
    }

    [TestCase(false, 3)]
    [TestCase(true, 2)]
    public static void FinalMemoryDefinitionsRespectSharedAndIndependentStates(bool sharedMemory, int lastSsa)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            compiler.byrefStatesMatchGcHeapStates = sharedMemory;
            block.bbMemoryDef = 3;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);

            RenameVariables(new SsaBuilder(compiler));

            Assert.That(block.bbMemorySsaNumIn[0], Is.EqualTo(1));
            Assert.That(block.bbMemorySsaNumIn[1], Is.EqualTo(1));
            Assert.That(block.bbMemorySsaNumOut[0], Is.EqualTo(2));
            Assert.That(block.bbMemorySsaNumOut[1], Is.EqualTo(lastSsa));
            _ = compiler.GetMemoryPerSsaData(lastSsa);
        });
    }

    [Test]
    public static void IntermediateStoreMemoryDefinitionsReachHandlerPhi()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var first = AddBlock(compiler);
            var handler = AddBlock(compiler);
            first.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = first,
                    ebdTryLast = first,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            handler.bbMemoryLiveIn = 3;
            handler.bbMemorySsaPhiFunc[0] = BasicBlock.EmptyMemoryPhiDef;
            handler.bbMemorySsaPhiFunc[1] = BasicBlock.EmptyMemoryPhiDef;
            var builder = new SsaBuilder(compiler);
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));

            RenamePushMemoryDef(builder, store, first);

            Assert.That(handler.bbMemorySsaPhiFunc[0]!.SsaNum, Is.EqualTo(1));
            Assert.That(handler.bbMemorySsaPhiFunc[1], Is.SameAs(BasicBlock.EmptyMemoryPhiDef));
            Assert.That(compiler.GetMemorySsaMap(MemoryKind.ByrefExposed)[store], Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RepeatedNonLocalStoreReplacesMemoryMapEntryAndRetainsHandlerPhis(bool sharedMemory)
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            var handler = AddBlock(compiler);
            block.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = block,
                    ebdTryLast = block,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            compiler.byrefStatesMatchGcHeapStates = sharedMemory;
            handler.bbMemoryLiveIn = 3;
            handler.bbMemorySsaPhiFunc[0] = BasicBlock.EmptyMemoryPhiDef;
            handler.bbMemorySsaPhiFunc[1] = BasicBlock.EmptyMemoryPhiDef;
            var store = new GenTreeStoreInd(TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 8192),
                                            compiler.gtNewIconNode(TYP_INT, 9));
            var builder = new SsaBuilder(compiler);

            RenamePushMemoryDef(builder, store, block);
            Assert.That(compiler.GetMemorySsaMap(MemoryKind.ByrefExposed)[store], Is.EqualTo(1));
            Assert.That(compiler.GetMemorySsaMap(MemoryKind.GcHeap)[store], Is.EqualTo(sharedMemory ? 1 : 2));
            RenamePushMemoryDef(builder, store, block);

            Assert.That(compiler.GetMemorySsaMap(MemoryKind.ByrefExposed)[store], Is.EqualTo(sharedMemory ? 2 : 3));
            Assert.That(compiler.GetMemorySsaMap(MemoryKind.GcHeap)[store], Is.EqualTo(sharedMemory ? 2 : 4));
            Assert.That(handler.bbMemorySsaPhiFunc[0]!.SsaNum, Is.EqualTo(sharedMemory ? 2 : 3));
            Assert.That(handler.bbMemorySsaPhiFunc[1]!.SsaNum, Is.EqualTo(sharedMemory ? 2 : 4));
            Assert.That(handler.bbMemorySsaPhiFunc[0]!._nextArg!.SsaNum, Is.EqualTo(1));
            Assert.That(handler.bbMemorySsaPhiFunc[1]!._nextArg!.SsaNum, Is.EqualTo(sharedMemory ? 1 : 2));
            Assert.That(handler.bbMemorySsaPhiFunc[0],
                sharedMemory ? Is.SameAs(handler.bbMemorySsaPhiFunc[1]) : Is.Not.SameAs(handler.bbMemorySsaPhiFunc[1]));
        });
    }

    [Test]
    public static void PartialDefinitionRecordsPreviousNameAndUse()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            compiler.lvaTable[0].lvInSsa = true;
            var oldSsa = compiler.lvaTable[0].lvPerSsaData.AllocSsaNum();
            compiler.lvaTable[0].GetPerSsaData(oldSsa) = new LclSsaVarDsc(block);
            var builder = new SsaBuilder(compiler);
            RenameStack(builder).Push(block, 0, oldSsa);
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 9));

            var newSsa = RenamePushDef(builder, store, block, 0, false);

            Assert.That(newSsa, Is.EqualTo(oldSsa + 1));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(newSsa).UseDefSsaNum, Is.EqualTo(oldSsa));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(newSsa).DefNode, Is.SameAs(store));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(oldSsa).NumUses, Is.EqualTo(1));
            Assert.That(RenameStack(builder).Top(0), Is.EqualTo(newSsa));
        });
    }

    [Test]
    public static void PromotedStoreKeepsIndependentFieldNamesOnCompositeNode()
    {
        SsaLivenessTests.WithCompiler(3, compiler => {
            var block = AddBlock(compiler);
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvFieldCnt = 2;
            parent.lvFieldLclStart = 1;
            for (var field = 1; field <= 2; field++)
            {
                ref var descriptor = ref compiler.lvaTable[field];
                descriptor.lvIsStructField = true;
                descriptor.lvParentLcl = 0;
                descriptor.lvInSsa = true;
            }
            var builder = new SsaBuilder(compiler);
            var first = new GenTreeLclVar(TYP_STRUCT, 0, compiler.gtNewNothingNode())
            {
                Flags = GTF_VAR_DEF | GTF_ASG,
            };
            var second = new GenTreeLclVar(TYP_STRUCT, 0, compiler.gtNewNothingNode())
            {
                Flags = GTF_VAR_DEF | GTF_ASG,
            };

            RenameDef(builder, first, block);
            RenameDef(builder, second, block);

            Assert.That(first.GetSsaNum(compiler, 0), Is.EqualTo(1));
            Assert.That(first.GetSsaNum(compiler, 1), Is.EqualTo(1));
            Assert.That(second.GetSsaNum(compiler, 0), Is.EqualTo(2));
            Assert.That(second.GetSsaNum(compiler, 1), Is.EqualTo(2));
            Assert.That(compiler.lvaTable[1].GetPerSsaData(2).DefNode, Is.SameAs(second));
            Assert.That(compiler.lvaTable[2].GetPerSsaData(2).DefNode, Is.SameAs(second));
            Assert.That(RenameStack(builder).Top(1), Is.EqualTo(2));
            Assert.That(RenameStack(builder).Top(2), Is.EqualTo(2));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NewlyEnteredTryPropagatesLiveLocalAndMemoryToHandler(bool sharedMemory)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var pred = AddBlock(compiler);
            var entry = AddBlock(compiler);
            var handler = AddBlock(compiler);
            compiler.byrefStatesMatchGcHeapStates = sharedMemory;
            ConfigureTrackedLocal(compiler);
            pred.bbLiveOut = SetOps.MakeSingleton(compiler, 0);
            pred.bbMemorySsaNumOut[0] = 3;
            pred.bbMemorySsaNumOut[1] = sharedMemory ? 3 : 4;
            handler.bbMemorySsaPhiFunc[0] = BasicBlock.EmptyMemoryPhiDef;
            handler.bbMemorySsaPhiFunc[1] = BasicBlock.EmptyMemoryPhiDef;
            var builder = new SsaBuilder(compiler);
            var oldSsa = compiler.lvaTable[0].lvPerSsaData.AllocSsaNum();
            compiler.lvaTable[0].GetPerSsaData(oldSsa) = new LclSsaVarDsc(pred);
            RenameStack(builder).Push(pred, 0, oldSsa);
            var phiStatement = InsertPhi(builder, compiler, handler, 0);

            AddPhiArgsToNewlyEnteredHandler(builder, pred, entry, handler);

            var phiArg = phiStatement.RootNode.AsLclVar().Data.AsPhi().FirstUse!.Node.AsPhiArg();
            Assert.That(phiArg.PredBB, Is.SameAs(entry));
            Assert.That(phiArg.SsaNum, Is.EqualTo(oldSsa));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(oldSsa).HasPhiUse, Is.True);
            Assert.That(handler.bbMemorySsaPhiFunc[0]!.SsaNum, Is.EqualTo(3));
            Assert.That(handler.bbMemorySsaPhiFunc[1]!.SsaNum, Is.EqualTo(sharedMemory ? 3 : 4));
            Assert.That(handler.bbMemorySsaPhiFunc[1],
                sharedMemory ? Is.SameAs(handler.bbMemorySsaPhiFunc[0]) : Is.Not.SameAs(handler.bbMemorySsaPhiFunc[0]));
        });
    }

    [Test]
    public static void UnreachableBlocksReceiveInitialMemoryNumbersWithoutVisitingTheirTrees()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var entry = AddBlock(compiler);
            var unreachable = AddBlock(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);

            RenameVariables(new SsaBuilder(compiler));

            Assert.That(compiler._dfsTree.Contains(unreachable), Is.False);
            Assert.That(entry.bbMemorySsaNumIn[0], Is.EqualTo(1));
            Assert.That(unreachable.bbMemorySsaNumIn[0], Is.EqualTo(1));
            Assert.That(unreachable.bbMemorySsaNumOut[0], Is.EqualTo(1));
            Assert.That(unreachable.bbMemorySsaNumIn[1], Is.EqualTo(1));
            Assert.That(unreachable.bbMemorySsaNumOut[1], Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EnteringTryPropagatesNamesToHandlerAndFilter(bool hasFilter)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var pred = AddBlock(compiler);
            var tryBegin = AddBlock(compiler);
            var filter = AddBlock(compiler);
            var handler = AddBlock(compiler);
            var edge = new FlowEdge(pred, tryBegin, null);
            tryBegin.bbPreds = edge;
            pred.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
            tryBegin.TryIndex = 0;
            handler.HndIndex = 0;
            if (hasFilter)
            {
                filter.HndIndex = 0;
            }
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = hasFilter ? EHHandlerType.EH_HANDLER_FILTER : EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = tryBegin,
                    ebdTryLast = tryBegin,
                    ebdFilter = filter,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            ConfigureTrackedLocal(compiler);
            pred.bbLiveOut = SetOps.MakeSingleton(compiler, 0);
            pred.bbMemorySsaNumOut[0] = 5;
            pred.bbMemorySsaNumOut[1] = 6;
            var builder = new SsaBuilder(compiler);
            var number = compiler.lvaTable[0].lvPerSsaData.AllocSsaNum();
            compiler.lvaTable[0].GetPerSsaData(number) = new LclSsaVarDsc(pred);
            RenameStack(builder).Push(pred, 0, number);
            var handlerPhi = InsertPhi(builder, compiler, handler, 0);
            handler.bbMemorySsaPhiFunc[0] = BasicBlock.EmptyMemoryPhiDef;
            handler.bbMemorySsaPhiFunc[1] = BasicBlock.EmptyMemoryPhiDef;
            Statement? filterPhi = null;
            if (hasFilter)
            {
                filterPhi = InsertPhi(builder, compiler, filter, 0);
                filter.bbMemorySsaPhiFunc[0] = BasicBlock.EmptyMemoryPhiDef;
                filter.bbMemorySsaPhiFunc[1] = BasicBlock.EmptyMemoryPhiDef;
            }

            AddPhiArgsToSuccessors(builder, pred);

            Assert.That(handlerPhi.RootNode.AsLclVar().Data.AsPhi().FirstUse!.Node.AsPhiArg().PredBB, Is.SameAs(tryBegin));
            Assert.That(handler.bbMemorySsaPhiFunc[0]!.SsaNum, Is.EqualTo(5));
            Assert.That(handler.bbMemorySsaPhiFunc[1]!.SsaNum, Is.EqualTo(6));
            if (hasFilter)
            {
                Assert.That(filterPhi!.RootNode.AsLclVar().Data.AsPhi().FirstUse!.Node.AsPhiArg().PredBB,
                    Is.SameAs(tryBegin));
                Assert.That(filter.bbMemorySsaPhiFunc[0]!.SsaNum, Is.EqualTo(5));
                Assert.That(compiler.lvaTable[0].GetPerSsaData(number).NumUses, Is.EqualTo(2));
            }
            else
            {
                Assert.That(compiler.lvaTable[0].GetPerSsaData(number).NumUses, Is.EqualTo(1));
            }
        });
    }

    [Test]
    public static void HandlerPhiRetainsDistinctDefinitionsFromSameThrowingPredecessor()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = AddBlock(compiler);
            var handler = AddBlock(compiler);
            block.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = block,
                    ebdTryLast = block,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            ConfigureTrackedLocal(compiler);
            handler.bbLiveIn = SetOps.MakeSingleton(compiler, 0);
            var builder = new SsaBuilder(compiler);
            var handlerPhi = InsertPhi(builder, compiler, handler, 0);
            var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            var second = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 2));

            var firstNum = RenamePushDef(builder, first, block, 0, true);
            var secondNum = RenamePushDef(builder, second, block, 0, true);

            var uses = handlerPhi.RootNode.AsLclVar().Data.AsPhi().FirstUse;
            Assert.That(uses!.Node.AsPhiArg().SsaNum, Is.EqualTo(secondNum));
            Assert.That(uses.Next!.Node.AsPhiArg().SsaNum, Is.EqualTo(firstNum));
            Assert.That(uses.Next.Next, Is.Null);
            Assert.That(uses.Node.AsPhiArg().PredBB, Is.SameAs(block));
            Assert.That(uses.Next.Node.AsPhiArg().PredBB, Is.SameAs(block));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(firstNum).HasPhiUse, Is.True);
            Assert.That(compiler.lvaTable[0].GetPerSsaData(secondNum).HasPhiUse, Is.True);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "InsertPhi")]
    private static extern Statement InsertPhi(SsaBuilder builder, Compiler compiler, BasicBlock block, int lclNum);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetPhiNode")]
    private static extern Statement? GetPhiNode(SsaBuilder builder, BasicBlock block, int lclNum);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddPhiArg")]
    private static extern void AddPhiArg(SsaBuilder builder, BasicBlock block, Statement statement,
                                         GenTreePhi phi, int lclNum, int ssaNum, BasicBlock pred);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InsertPhiFunctions")]
    private static extern void InsertPhiFunctions(SsaBuilder builder);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenameVariables")]
    private static extern void RenameVariables(SsaBuilder builder);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenamePushMemoryDef")]
    private static extern void RenamePushMemoryDef(SsaBuilder builder, GenTree node, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenamePushDef")]
    private static extern int RenamePushDef(SsaBuilder builder, GenTree node, BasicBlock block, int lclNum, bool isFullDef);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenameDef")]
    private static extern void RenameDef(SsaBuilder builder, GenTree node, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddPhiArgsToNewlyEnteredHandler")]
    private static extern void AddPhiArgsToNewlyEnteredHandler(SsaBuilder builder, BasicBlock pred, BasicBlock entry,
                                                                BasicBlock handler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddPhiArgsToSuccessors")]
    private static extern void AddPhiArgsToSuccessors(SsaBuilder builder, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_renameStack")]
    private static extern ref SsaRenameState RenameStack(SsaBuilder builder);

    private static void ConfigureTrackedLocal(Compiler compiler)
    {
        ref var local = ref compiler.lvaTable[0];
        local.lvInSsa = true;
        local.lvIsParam = true;
        local.lvTracked = true;
        local._varIndex = 0;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = new Statement(tree, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        return statement;
    }

    private static BasicBlock AddBlock(Compiler compiler)
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
        return block;
    }

    private static BasicBlock[] CreateGraph(Compiler compiler, int[][] successors)
    {
        var blocks = new BasicBlock[successors.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = AddBlock(compiler);
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
