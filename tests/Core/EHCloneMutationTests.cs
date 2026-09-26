// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.SpecialCodeKind;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EHCloneMutationTests
{
    [Test]
    public static void CloneBlockStateCopiesMetadataAndDeepClonesStatementsWithoutEdges()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var source = BasicBlock.New(compiler, BBJ_RETURN);
            var clone = BasicBlock.New(compiler, BBJ_ALWAYS);
            clone.bbRefs = 0;
            source.TryIndex = 2;
            source.HndIndex = 1;
            source.SetFlags(BBF_INTERNAL);
            source.setBBProfileWeight(90);
            source.bbStkTempsIn = 2;
            source.bbStkTempsOut = 3;
            source.bbCodeOffs = 5;
            source.bbCodeOffsEnd = 11;
#if DEBUG
            source.bbTgtStkDepth = 4;
#endif
            compiler.fgInsertStmtAtEnd(source, compiler.fgNewStmtFromTree(compiler.gtNewIconNode(TYP_INT, 7)));
            var original = source.FirstStmt!;

            BasicBlock.CloneBlockState(compiler, clone, source);

            Assert.That(clone.FlagsRaw, Is.EqualTo(source.FlagsRaw));
            Assert.That(clone.bbWeight, Is.EqualTo(source.bbWeight));
            Assert.That(clone.bbTryIndex, Is.EqualTo(source.bbTryIndex));
            Assert.That(clone.bbHndIndex, Is.EqualTo(source.bbHndIndex));
            Assert.That(clone.bbStkTempsIn, Is.EqualTo(2));
            Assert.That(clone.bbStkTempsOut, Is.EqualTo(3));
            Assert.That(clone.bbCodeOffs, Is.EqualTo(5));
            Assert.That(clone.bbCodeOffsEnd, Is.EqualTo(11));
#if DEBUG
            Assert.That(clone.bbTgtStkDepth, Is.EqualTo(4));
#endif
            Assert.That(clone.FirstStmt, Is.Not.SameAs(original));
            Assert.That(clone.FirstStmt!.RootNode, Is.Not.SameAs(original.RootNode));
            Assert.That(clone.FirstStmt.RootNode.AsIntCon().IconValue, Is.EqualTo((nint)7));
            Assert.That(clone.bbRefs, Is.Zero);
            Assert.That(clone.bbPreds, Is.Null);
            Assert.That(clone.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(clone.HasInitializedTarget, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CloneRegionDuplicatesDescriptorsStatementsEdgesAndProfiles(bool scaleOriginal)
    {
        WithTryGraph((compiler, tryEntry, tryTail, handler, after) =>
        {
            tryEntry.setBBProfileWeight(100);
            tryTail.setBBProfileWeight(50);
            handler.setBBProfileWeight(20);
            var root = compiler.gtNewIconNode(TYP_INT, 42);
            compiler.fgInsertStmtAtEnd(tryEntry, compiler.fgNewStmtFromTree(root));
            tryEntry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(tryTail, tryEntry));
            var map = new Dictionary<BasicBlock, BasicBlock>();
            var collected = new List<BasicBlock>();
            var info = new CloneTryInfo(compiler)
            {
                Map = map,
                BlocksToClone = collected,
                ProfileScale = 0.4,
                ScaleOriginalBlockProfile = scaleOriginal,
                AddEdges = true,
            };
            var insertAfter = after;
            var originalTable = compiler.compHndBBtab[0];

            var clonedEntry = compiler.fgCloneTryRegion(tryEntry, info, ref insertAfter);

            Assert.That(clonedEntry, Is.Not.Null);
            Assert.That(collected, Is.EqualTo([tryEntry, tryTail, handler]));
            Assert.That(map[tryEntry], Is.SameAs(clonedEntry));
            Assert.That(insertAfter, Is.SameAs(map[handler]));
            Assert.That(after.Next, Is.SameAs(clonedEntry));
            Assert.That(map[tryEntry].Next, Is.SameAs(map[tryTail]));
            Assert.That(map[tryTail].Next, Is.SameAs(map[handler]));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[0], Is.EqualTo(originalTable));
            Assert.That(compiler.compHndBBtab[1].ebdTryBeg, Is.SameAs(clonedEntry));
            Assert.That(compiler.compHndBBtab[1].ebdTryLast, Is.SameAs(map[tryTail]));
            Assert.That(compiler.compHndBBtab[1].ebdHndBeg, Is.SameAs(map[handler]));
            Assert.That(compiler.compHndBBtab[1].ebdHndLast, Is.SameAs(map[handler]));
            Assert.That(info.EHIndexShift, Is.EqualTo(1));
            Assert.That(clonedEntry!.TryIndex, Is.EqualTo(1));
            Assert.That(map[handler].HndIndex, Is.EqualTo(1));
            Assert.That(map[handler].bbRefs, Is.EqualTo(1));
            Assert.That(clonedEntry.Target, Is.SameAs(map[tryTail]));
            Assert.That(tryEntry.Target, Is.SameAs(tryTail));
            Assert.That(clonedEntry.FirstStmt!.RootNode, Is.Not.SameAs(root));
            Assert.That(clonedEntry.FirstStmt.RootNode.AsIntCon().IconValue, Is.EqualTo((nint)42));
            Assert.That(clonedEntry.bbWeight, Is.EqualTo(40));
            Assert.That(tryEntry.bbWeight, Is.EqualTo(scaleOriginal ? 60 : 100));
        });
    }

    [Test]
    public static void EhCapacityFailureLeavesGraphTableMapAndWeightsUnchanged()
    {
        WithTryGraph((compiler, tryEntry, _, handler, after) =>
        {
            var table = new EHblkDsc[MAX_XCPTN_INDEX];
            table[0] = compiler.compHndBBtab[0];
            compiler.compHndBBtab = table;
            compiler.compHndBBtabCount = MAX_XCPTN_INDEX;
            var map = new Dictionary<BasicBlock, BasicBlock>();
            var blocks = new List<BasicBlock>();
            var info = new CloneTryInfo(compiler)
            {
                Map = map,
                BlocksToClone = blocks,
                ProfileScale = 0.5,
                AddEdges = true,
            };
            var insertion = after;
            var before = compiler.fgBBcount;
            var originalWeight = tryEntry.bbWeight;

            Assert.That(compiler.fgCloneTryRegion(tryEntry, info, ref insertion), Is.Null);
            Assert.That(insertion, Is.SameAs(after));
            Assert.That(after.Next, Is.Null);
            Assert.That(compiler.fgBBcount, Is.EqualTo(before));
            Assert.That(compiler.compHndBBtab, Is.SameAs(table));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(MAX_XCPTN_INDEX));
            Assert.That(map, Is.Empty);
            Assert.That(blocks, Is.EqualTo([tryEntry, tryEntry.Next, handler]));
            Assert.That(tryEntry.bbWeight, Is.EqualTo(originalWeight));
        });
    }

    [Test]
    public static void MiddleInsertionShiftsEnclosingRegionAndKeepsOriginalDescriptor()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var blocks = new BasicBlock[6];
            for (var index = 0; index < blocks.Length; index++)
            {
                blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
                if (index > 0)
                {
                    blocks[index - 1].Next = blocks[index];
                }
            }

            var (outerEntry, entry, innerHandler, outerTail, outerHandler, after) =
                (blocks[0], blocks[1], blocks[2], blocks[3], blocks[4], blocks[5]);
            compiler.fgFirstBB = outerEntry;
            compiler.fgLastBB = after;
            outerEntry.TryIndex = 1;
            entry.TryIndex = 0;
            innerHandler.TryIndex = 1;
            innerHandler.HndIndex = 0;
            outerTail.TryIndex = 1;
            outerHandler.HndIndex = 1;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry,
                    ebdHndBeg = innerHandler, ebdHndLast = innerHandler,
                    ebdEnclosingTryIndex = 1,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdTryBeg = outerEntry, ebdTryLast = outerTail,
                    ebdHndBeg = outerHandler, ebdHndLast = outerHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 2;
            var info = new CloneTryInfo(compiler) { Map = [], AddEdges = false, ProfileScale = 1 };
            var insertion = innerHandler;

            var clone = compiler.fgCloneTryRegion(entry, info, ref insertion);

            Assert.That(clone, Is.Not.Null);
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(3));
            Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(entry));
            Assert.That(compiler.compHndBBtab[0].ebdEnclosingTryIndex, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[1].ebdTryBeg, Is.SameAs(clone));
            Assert.That(compiler.compHndBBtab[1].ebdHndBeg, Is.SameAs(info.Map![innerHandler]));
            Assert.That(compiler.compHndBBtab[1].ebdEnclosingTryIndex, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[2].ebdTryBeg, Is.SameAs(outerEntry));
            Assert.That(compiler.compHndBBtab[2].ebdTryLast, Is.SameAs(outerTail));
            Assert.That(outerEntry.TryIndex, Is.EqualTo(2));
            Assert.That(clone!.TryIndex, Is.EqualTo(1));
            Assert.That(info.Map[innerHandler].TryIndex, Is.EqualTo(2));
            Assert.That(info.Map[innerHandler].HndIndex, Is.EqualTo(1));
            Assert.That(info.Map[innerHandler].Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(info.Map[innerHandler].HasInitializedTarget, Is.False);
            Assert.That(insertion.Next, Is.SameAs(outerTail));
            Assert.That(info.EHIndexShift, Is.EqualTo(1));
        });
    }

    [Test]
    public static void FilterRegionIsClonedAndRemappedWithoutAddingEdges()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var filter = BasicBlock.New(compiler, BBJ_EHFILTERRET);
            var handler = BasicBlock.New(compiler, BBJ_RETURN);
            var after = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = filter;
            filter.Next = handler;
            handler.Next = after;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = after;
            entry.TryIndex = 0;
            filter.HndIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry, ebdFilter = filter,
                    ebdHndBeg = handler, ebdHndLast = handler,
                    ebdHandlerType = EH_HANDLER_FILTER,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var info = new CloneTryInfo(compiler) { Map = [], ProfileScale = 1 };
            var insertion = after;

            Assert.That(compiler.fgCloneTryRegion(entry, info, ref insertion), Is.Not.Null);

            Assert.That(compiler.compHndBBtab[1].ebdFilter, Is.SameAs(info.Map![filter]));
            Assert.That(compiler.compHndBBtab[1].ebdHndBeg, Is.SameAs(info.Map[handler]));
            Assert.That(info.Map[filter].HndIndex, Is.EqualTo(1));
            Assert.That(info.Map[filter].CatchType, Is.EqualTo(filter.CatchType));
            Assert.That(info.Map[filter].HasInitializedTarget, Is.False);
            Assert.That(info.Map[handler].bbRefs, Is.EqualTo(1));
        });
    }

    [Test]
    public static void ClonedAddCodeDescriptorsFollowTryAndHandlerIndices()
    {
        WithTryGraph((compiler, entry, _, handler, after) =>
        {
            var tryAdd = new Compiler.AddCodeDsc
            {
                acdKind = SCK_RNGCHK_FAIL,
                acdKeyDsg = Compiler.AcdKeyDesignator.KD_TRY,
                acdTryIndex = 1,
                acdUsed = true,
                acdDstBlk = handler,
            };
            var handlerAdd = new Compiler.AddCodeDsc
            {
                acdKind = SCK_OVERFLOW,
                acdKeyDsg = Compiler.AcdKeyDesignator.KD_HND,
                acdHndIndex = 1,
            };
            var map = compiler.fgGetAddCodeDscMap();
            map[new Compiler.AddCodeDscKey(tryAdd)] = tryAdd;
            map[new Compiler.AddCodeDscKey(handlerAdd)] = handlerAdd;
            var info = new CloneTryInfo(compiler) { Map = [], ProfileScale = 1 };
            var insertion = after;

            Assert.That(compiler.fgCloneTryRegion(entry, info, ref insertion), Is.Not.Null);

            Assert.That(map, Has.Count.EqualTo(4));
            Assert.That(map[new Compiler.AddCodeDscKey(tryAdd)], Is.SameAs(tryAdd));
            Assert.That(map[new Compiler.AddCodeDscKey(handlerAdd)], Is.SameAs(handlerAdd));
            var clonedTry = map[new Compiler.AddCodeDscKey(new Compiler.AddCodeDsc
            {
                acdKind = tryAdd.acdKind, acdKeyDsg = tryAdd.acdKeyDsg, acdTryIndex = 2,
            })];
            var clonedHandler = map[new Compiler.AddCodeDscKey(new Compiler.AddCodeDsc
            {
                acdKind = handlerAdd.acdKind, acdKeyDsg = handlerAdd.acdKeyDsg, acdHndIndex = 2,
            })];
            Assert.That(clonedTry.acdTryIndex, Is.EqualTo(2));
            Assert.That(clonedTry.acdUsed, Is.False);
            Assert.That(clonedTry.acdDstBlk, Is.Null);
            Assert.That(clonedHandler.acdHndIndex, Is.EqualTo(2));
            Assert.That(clonedHandler.acdUsed, Is.False);
        });
    }

    [Test]
    public static void FinallyClonesMatchingCallAndReturnAndRemapsBothTargets()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var call = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var continuation = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
            var unrelatedCall = BasicBlock.New(compiler, BBJ_CALLFINALLY);
            var handler = BasicBlock.New(compiler, BBJ_RETURN);
            var after = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = call;
            call.Next = continuation;
            continuation.Next = unrelatedCall;
            unrelatedCall.Next = handler;
            handler.Next = after;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = after;
            entry.TryIndex = 0;
            handler.HndIndex = 0;
            call.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(handler, call));
            continuation.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, compiler.fgAddRefPred(after, continuation));
            unrelatedCall.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(after, unrelatedCall));
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry, ebdTryLast = entry,
                    ebdHndBeg = handler, ebdHndLast = handler,
                    ebdHandlerType = EH_HANDLER_FINALLY,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var info = new CloneTryInfo(compiler) { Map = [], AddEdges = true, ProfileScale = 1 };
            var insertion = after;

            Assert.That(compiler.fgCloneTryRegion(entry, info, ref insertion), Is.Not.Null);

            Assert.That(info.Map, Has.Count.EqualTo(4));
            Assert.That(info.Map!.ContainsKey(unrelatedCall), Is.False);
            Assert.That(info.Map[call].Kind, Is.EqualTo(BBJ_CALLFINALLY));
            Assert.That(info.Map[call].Target, Is.SameAs(info.Map[handler]));
            Assert.That(info.Map[continuation].Kind, Is.EqualTo(BBJ_CALLFINALLYRET));
            Assert.That(info.Map[continuation].Target, Is.SameAs(after));
            Assert.That(info.Map[handler].HndIndex, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtab[1].ebdHndBeg, Is.SameAs(info.Map[handler]));
        });
    }

    [Test]
    public static void NestedTryClonesBothDescriptorsAndTheirEnclosingLinks()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var outerEntry = BasicBlock.New(compiler, BBJ_RETURN);
            var innerEntry = BasicBlock.New(compiler, BBJ_RETURN);
            var innerHandler = BasicBlock.New(compiler, BBJ_RETURN);
            var outerHandler = BasicBlock.New(compiler, BBJ_RETURN);
            var after = BasicBlock.New(compiler, BBJ_RETURN);
            outerEntry.Next = innerEntry;
            innerEntry.Next = innerHandler;
            innerHandler.Next = outerHandler;
            outerHandler.Next = after;
            compiler.fgFirstBB = outerEntry;
            compiler.fgLastBB = after;
            outerEntry.TryIndex = 1;
            innerEntry.TryIndex = 0;
            innerHandler.TryIndex = 1;
            innerHandler.HndIndex = 0;
            outerHandler.HndIndex = 1;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = innerEntry, ebdTryLast = innerEntry,
                    ebdHndBeg = innerHandler, ebdHndLast = innerHandler,
                    ebdEnclosingTryIndex = 1,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdTryBeg = outerEntry, ebdTryLast = innerHandler,
                    ebdHndBeg = outerHandler, ebdHndLast = outerHandler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 2;
            var info = new CloneTryInfo(compiler)
            {
                Map = [], BlocksToClone = [], ProfileScale = 1,
            };
            var insertion = after;

            var clone = compiler.fgCloneTryRegion(outerEntry, info, ref insertion);

            Assert.That(clone, Is.Not.Null);
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(4));
            Assert.That(info.BlocksToClone, Is.EqualTo([outerEntry, innerEntry, innerHandler, outerHandler]));
            Assert.That(info.EHIndexShift, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[2].ebdEnclosingTryIndex, Is.EqualTo(3));
            Assert.That(compiler.compHndBBtab[2].ebdTryBeg, Is.SameAs(info.Map![innerEntry]));
            Assert.That(compiler.compHndBBtab[2].ebdHndBeg, Is.SameAs(info.Map[innerHandler]));
            Assert.That(compiler.compHndBBtab[3].ebdTryBeg, Is.SameAs(clone));
            Assert.That(compiler.compHndBBtab[3].ebdTryLast, Is.SameAs(info.Map[innerHandler]));
            Assert.That(compiler.compHndBBtab[3].ebdHndBeg, Is.SameAs(info.Map[outerHandler]));
            Assert.That(clone!.TryIndex, Is.EqualTo(3));
            Assert.That(info.Map[innerEntry].TryIndex, Is.EqualTo(2));
            Assert.That(info.Map[innerHandler].TryIndex, Is.EqualTo(3));
            Assert.That(info.Map[innerHandler].HndIndex, Is.EqualTo(2));
            Assert.That(info.Map[outerHandler].HndIndex, Is.EqualTo(3));
        });
    }

    private static void WithTryGraph(Action<Compiler, BasicBlock, BasicBlock, BasicBlock, BasicBlock> action)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var tail = BasicBlock.New(compiler, BBJ_RETURN);
            var handler = BasicBlock.New(compiler, BBJ_RETURN);
            var after = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = tail;
            tail.Next = handler;
            handler.Next = after;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = after;
            entry.TryIndex = 0;
            tail.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = entry,
                    ebdTryLast = tail,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            action(compiler, entry, tail, handler, after);
        });
    }
}
