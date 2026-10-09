// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
#if DEBUG
using System.IO;
#endif
using System.Linq;
using System.Reflection;
#if DEBUG
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class PhysicalPromotionTests
{
    [TestCase(TYP_VOID)]
    [TestCase(TYP_INT)]
    public static void ConditionalTraversalDoesNotClassifyValuesWithoutReadBacks(var_types type)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;
            var condition = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 0));
            var thenTree = type is TYP_VOID ? compiler.gtNewNothingNode() : compiler.gtNewIconNode(TYP_INT, 2);
            var elseTree = type is TYP_VOID ? compiler.gtNewNothingNode() : compiler.gtNewIconNode(TYP_INT, 3);
            var colon = compiler.gtNewColonNode(type, thenTree, elseTree);
            var qmark = compiler.gtNewQmarkNode(type, condition, colon);
            var statement = compiler.gtNewStmt(qmark);
            compiler.fgInsertStmtAtEnd(block, statement);
            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
            var visitor = CreateReplaceVisitor(compiler, aggregates, liveness);
            _ = visitor.StartBlock(block);
            visitor.StartStatement(statement);

            visitor.WalkTree(ref statement.RootNodeRef);

            Assert.That(statement.RootNode, Is.SameAs(qmark));
            Assert.That(qmark.Op1, Is.SameAs(condition));
            Assert.That(qmark.Op2, Is.SameAs(colon));
            Assert.That(colon.Op1, Is.SameAs(elseTree));
            Assert.That(colon.Op2, Is.SameAs(thenTree));
            Assert.That(qmark.Type, Is.EqualTo(type));
        });
    }

#if DEBUG
    [Test]
    public static void AccessDumpsPreserveNativeCountersAndInducedMetrics()
    {
        var uses = new Compiler.PhysicalPromotionLocalUses();
        var flags = Compiler.PhysicalPromotionAccessKindFlags.IsCallArg |
            Compiler.PhysicalPromotionAccessKindFlags.IsRegCallArg |
            Compiler.PhysicalPromotionAccessKindFlags.IsCallRetBuf |
            Compiler.PhysicalPromotionAccessKindFlags.IsStoreSource |
            Compiler.PhysicalPromotionAccessKindFlags.IsStoreDestination |
            Compiler.PhysicalPromotionAccessKindFlags.IsReturned;
        uses.RecordAccess(0, TYP_STRUCT, new ClassLayout(16), flags, 5);
        uses.RecordInducedAccess(8, TYP_INT, 2);

        var previousOutput = s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
        try
        {
            s_jitstdout = writer;
            uses.DumpAccesses(3);
            uses.DumpInducedAccesses(3);
            writer.Flush();

            var dump = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(dump, Does.Contain("Accesses for V03\n  [000..016) as "));
            Assert.That(dump, Does.Contain("    # store source:                (1, 5)\n"));
            Assert.That(dump, Does.Contain("    # store destination:           (1, 5)\n"));
            Assert.That(dump, Does.Contain("    # as call arg:                 (1, 5)\n"));
            Assert.That(dump, Does.Contain("    # as reg call arg:             (1, 5)\n"));
            Assert.That(dump, Does.Contain("    # as retbuf:                   (1, 5)\n"));
            Assert.That(dump, Does.Contain("    # as returned value:           (1, 5)\n"));
            Assert.That(dump, Does.Contain("Induced accesses for V03\n  int @ 008\n    #: (1, 2)\n"));
        }
        finally
        {
            s_jitstdout = previousOutput;
        }
    }
#endif

    [TestCase(0, 4, true, 0, 1)]
    [TestCase(3, 2, true, 0, 2)]
    [TestCase(4, 4, true, 1, 2)]
    [TestCase(8, 4, true, 1, 2)]
    [TestCase(12, 4, false, 2, 0)]
    public static void OverlappingReplacementsUseHalfOpenIntervals(
        int offset, int size, bool expected, int expectedFirst, int expectedEnd)
    {
        var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
        aggregate.Replacements.Add(new Compiler.PhysicalPromotionReplacement(0, TYP_INT));
        aggregate.Replacements.Add(new Compiler.PhysicalPromotionReplacement(4, TYP_LONG));

        Assert.That(aggregate.OverlappingReplacements(offset, size, out var first, out var end), Is.EqualTo(expected));
        if (expected)
        {
            Assert.That((first, end), Is.EqualTo((expectedFirst, expectedEnd)));
        }
    }

    [Test]
    public static void RecordingKeepsSameOffsetTypesDistinctAndExcludesRegisterCallArgs()
    {
        var uses = new Compiler.PhysicalPromotionLocalUses();
        var layout = new ClassLayout(16);
        uses.RecordAccess(0, TYP_STRUCT, layout,
            Compiler.PhysicalPromotionAccessKindFlags.IsCallArg |
            Compiler.PhysicalPromotionAccessKindFlags.IsRegCallArg, 5);
        uses.RecordAccess(0, TYP_STRUCT, layout,
            Compiler.PhysicalPromotionAccessKindFlags.IsStoredFromCall, 3);
        uses.RecordAccess(0, TYP_INT, null, Compiler.PhysicalPromotionAccessKindFlags.None, 7);
        uses.RecordAccess(0, TYP_LONG, null, Compiler.PhysicalPromotionAccessKindFlags.None, 11);

        Assert.That(uses.Accesses.Count, Is.EqualTo(3));
        Assert.That(uses.Accesses[0].Count, Is.EqualTo(2));
        Assert.That(uses.Accesses[0].CountCallArgsWtd - uses.Accesses[0].CountRegCallArgsWtd, Is.Zero);
        Assert.That(uses.Accesses[0].CountStoredFromCallWtd, Is.EqualTo(3));
        Assert.That(uses.Accesses[1].AccessType, Is.EqualTo(TYP_INT));
        Assert.That(uses.Accesses[2].AccessType, Is.EqualTo(TYP_LONG));
    }

    [Test]
    public static void RemovingPromotedFieldsDoesNotMutateCachedLayoutSegments()
    {
        var layout = new ClassLayout(16);
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var cached = layout.GetNonPadding(compiler);
            var unpromoted = Compiler.PhysicalPromotionCopyNonPadding(cached);
            unpromoted.Subtract(new SegmentList.Segment(0, 8));

            Assert.That(unpromoted.CoveringSegment(out var remaining), Is.True);
            Assert.That((remaining.Start, remaining.End), Is.EqualTo((8u, 16u)));
            Assert.That(cached.CoveringSegment(out var original), Is.True);
            Assert.That((original.Start, original.End), Is.EqualTo((0u, 16u)));
            Assert.That(layout.GetNonPadding(compiler), Is.SameAs(cached));
        });
    }

    [TestCase(false, false, 1)]
    [TestCase(true, false, 0)]
    [TestCase(false, true, 0)]
    public static void SelectionRejectsOverlappingPrimitivesAndGcReinterpretations(
        bool overlap, bool gcMismatch, int expectedPromotions)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.fgFirstBB = new BasicBlock(null, null) { bbWeight = BB_UNITY_WEIGHT };
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);

            var uses = new Compiler.PhysicalPromotionLocalUses();
            uses.RecordAccess(0, gcMismatch ? TYP_REF : TYP_INT, null,
                Compiler.PhysicalPromotionAccessKindFlags.None, BB_UNITY_WEIGHT);
            if (overlap)
            {
                uses.RecordAccess(2, TYP_SHORT, null,
                    Compiler.PhysicalPromotionAccessKindFlags.None, BB_UNITY_WEIGHT);
            }

            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            Assert.That(uses.PickPromotions(compiler, 0, aggregates), Is.EqualTo(expectedPromotions));
            Assert.That(aggregates.Lookup(0)?.Replacements.Count ?? 0, Is.EqualTo(expectedPromotions));
        });
    }

    [TestCase(true, 1)]
    [TestCase(false, 0)]
    public static void RegisterArgumentsAvoidWriteBackCost(bool passedInRegisters, int expectedPromotions)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.fgFirstBB = new BasicBlock(null, null) { bbWeight = BB_UNITY_WEIGHT };
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);

            var uses = new Compiler.PhysicalPromotionLocalUses();
            var flags = Compiler.PhysicalPromotionAccessKindFlags.IsCallArg;
            if (passedInRegisters)
            {
                flags |= Compiler.PhysicalPromotionAccessKindFlags.IsRegCallArg;
            }

            uses.RecordAccess(0, TYP_INT, null, Compiler.PhysicalPromotionAccessKindFlags.None,
                BB_UNITY_WEIGHT);
            uses.RecordAccess(0, TYP_STRUCT, new ClassLayout(16), flags, BB_UNITY_WEIGHT);

            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            Assert.That(uses.PickPromotions(compiler, 0, aggregates), Is.EqualTo(expectedPromotions));
        });
    }

    [Test]
    public static void RegisterCallArgumentCancellationPreservesNativeCostOrder()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.fgFirstBB = new BasicBlock(null, null) { bbWeight = BB_UNITY_WEIGHT };
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);

            var uses = new Compiler.PhysicalPromotionLocalUses();
            uses.RecordAccess(0, TYP_INT, null, Compiler.PhysicalPromotionAccessKindFlags.None, BB_UNITY_WEIGHT);
            uses.RecordAccess(0, TYP_STRUCT, new ClassLayout(16),
                Compiler.PhysicalPromotionAccessKindFlags.IsCallArg, 100);
            uses.RecordAccess(0, TYP_STRUCT, new ClassLayout(16),
                Compiler.PhysicalPromotionAccessKindFlags.IsCallArg |
                Compiler.PhysicalPromotionAccessKindFlags.IsRegCallArg, 1152921504606846976);

            Assert.That(uses.Accesses.Count, Is.EqualTo(3));
            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            Assert.That(uses.PickPromotions(compiler, 0, aggregates), Is.EqualTo(1));
        });
    }

    [TestCase(false, 2)]
    [TestCase(true, 0)]
    public static void InducedCandidatesRejectOverlappingFieldAccesses(bool overlap, int expectedPromotions)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.fgFirstBB = new BasicBlock(null, null) { bbWeight = BB_UNITY_WEIGHT };
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);

            var uses = new Compiler.PhysicalPromotionLocalUses();
            uses.RecordInducedAccess(0, TYP_INT, BB_UNITY_WEIGHT);
            uses.RecordInducedAccess(overlap ? 2 : 8, TYP_INT, BB_UNITY_WEIGHT);

            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            Assert.That(uses.PickInducedPromotions(compiler, 0, aggregates), Is.EqualTo(expectedPromotions));
            if (expectedPromotions != 0)
            {
                Assert.That(aggregates.Lookup(0)!.Replacements[0].Offset, Is.Zero);
                Assert.That(aggregates.Lookup(0)!.Replacements[1].Offset, Is.EqualTo(8));
            }
        });
    }

    [Test]
    public static void CandidateScanSelectsAFieldAndKeepsTheCachedLayoutIntact()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var layout = new ClassLayout(16);
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var statement = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_INT, 0, 0));
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.fgSequenceLocals(statement);

            var select = typeof(Compiler).GetMethod("PhysicalPromotionSelectCandidates",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var aggregates = (Compiler.PhysicalPromotionAggregateInfoMap?)select.Invoke(compiler, null);

            Assert.That(aggregates?.Lookup(0)?.Replacements[0].Offset, Is.Zero);
            Assert.That(aggregates?.Lookup(0)?.UnpromotedMin, Is.EqualTo(4));
            Assert.That(aggregates?.Lookup(0)?.UnpromotedMax, Is.EqualTo(16));
            Assert.That(layout.GetNonPadding(compiler).CoveringSegment(out var cached), Is.True);
            Assert.That((cached.Start, cached.End), Is.EqualTo((0u, 16u)));
        });
    }

    [Test]
    public static void LivenessTracksRemainderAndFieldDeathsSeparately()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            var structRead = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var structStatement = compiler.gtNewStmt(structRead);
            compiler.fgInsertStmtAtEnd(block, structStatement);
            compiler.fgSequenceLocals(structStatement);

            var fieldRead = compiler.gtNewLclFldNode(TYP_INT, 0, 0);
            var fieldStatement = compiler.gtNewStmt(fieldRead);
            compiler.fgInsertStmtAtEnd(block, fieldStatement);
            compiler.fgSequenceLocals(fieldStatement);
            compiler._dfsTree = compiler.fgComputeDfs();

            var select = typeof(Compiler).GetMethod("PhysicalPromotionSelectCandidates",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var aggregates = (Compiler.PhysicalPromotionAggregateInfoMap)select.Invoke(compiler, null)!;
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
            liveness.Run();

            var deaths = liveness.GetDeathsForStructLocal(structRead);
            Assert.That(deaths.IsRemainderDying(), Is.True);
            Assert.That(deaths.IsReplacementDying(0), Is.False);
            Assert.That(fieldRead.Flags & GTF_VAR_DEATH, Is.Not.Zero);
            Assert.That(liveness.IsReplacementLiveIn(block, 0, 0), Is.True);
            Assert.That(liveness.IsReplacementLiveOut(block, 0, 0), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FieldDefinitionKillsOnlyThatFieldsInterblockLiveness(bool defineBeforeUse)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            first.Next = second;
            second.Prev = first;
            first.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(first, second, null));
            compiler.fgFirstBB = first;
            compiler.fgLastBB = second;

            if (defineBeforeUse)
            {
                var store = new GenTreeLclFld(TYP_INT, 0, 0,
                    compiler.gtNewIconNode(TYP_INT, 7), null);
                store.Flags |= GTF_VAR_DEF;
                var statement = compiler.gtNewStmt(store);
                compiler.fgInsertStmtAtEnd(first, statement);
                compiler.fgSequenceLocals(statement);
            }

            var read = compiler.gtNewLclFldNode(TYP_INT, 0, 0);
            var readStatement = compiler.gtNewStmt(read);
            compiler.fgInsertStmtAtEnd(second, readStatement);
            compiler.fgSequenceLocals(readStatement);
            compiler._dfsTree = compiler.fgComputeDfs();

            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
            aggregate.Replacements.Add(new Compiler.PhysicalPromotionReplacement(0, TYP_INT) { LclNum = 2 });
            aggregates.Add(aggregate);
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
            liveness.Run();

            Assert.That(liveness.IsReplacementLiveIn(first, 0, 0), Is.EqualTo(!defineBeforeUse));
            Assert.That(liveness.IsReplacementLiveOut(first, 0, 0), Is.True);
            Assert.That(liveness.IsReplacementLiveIn(second, 0, 0), Is.True);
            Assert.That(liveness.IsReplacementLiveOut(second, 0, 0), Is.False);
            Assert.That(liveness.IsReplacementUsed(first, 0, 0), Is.False);
            Assert.That(liveness.IsReplacementDefined(first, 0, 0), Is.EqualTo(defineBeforeUse));
            Assert.That(liveness.IsReplacementUsed(second, 0, 0), Is.True);
            Assert.That(liveness.IsReplacementDefined(second, 0, 0), Is.False);
            Assert.That(read.Flags & GTF_VAR_DEATH, Is.Not.Zero);
        });
    }

    [Test]
    public static void ReadAndWriteBacksRetainFieldOffsetAndLocalOwnership()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            compiler.lvaTable[1].Type = TYP_INT;
            var replacement = new Compiler.PhysicalPromotionReplacement(8, TYP_INT) { LclNum = 1 };

            var write = compiler.PhysicalPromotionCreateWriteBack(0, replacement).AsLclFld();
            Assert.That(write.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(write.LclNum, Is.Zero);
            Assert.That(write.LclOffs, Is.EqualTo(8));
            Assert.That(write.Data.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);

            var read = compiler.PhysicalPromotionCreateReadBack(0, replacement).AsLclVar();
            Assert.That(read.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(read.LclNum, Is.EqualTo(1));
            Assert.That(read.Data.AsLclFld().LclNum, Is.Zero);
            Assert.That(read.Data.AsLclFld().LclOffs, Is.EqualTo(8));
        });
    }

    [Test]
    public static void DecompositionStatementListPreservesEvaluationOrder()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var statements = new Compiler.PhysicalPromotionDecompositionStatementList();
            statements.AddStatement(compiler.gtNewIconNode(TYP_INT, 1));
            statements.AddStatement(compiler.gtNewIconNode(TYP_INT, 2));
            statements.AddStatement(compiler.gtNewIconNode(TYP_INT, 3));

            var tree = statements.ToCommaTree(compiler);
            Assert.That(tree.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(tree.AsOp().Op1.AsIntCon().IconVal, Is.EqualTo((nint)1));
            Assert.That(tree.AsOp().Op2.AsOp().Op1.AsIntCon().IconVal, Is.EqualTo((nint)2));
            Assert.That(tree.AsOp().Op2.AsOp().Op2.AsIntCon().IconVal, Is.EqualTo((nint)3));
        });
    }

    [Test]
    public static void WriteBackPrefixNestsStoresBeforeTheOriginalUse()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var statements = new Compiler.PhysicalPromotionDecompositionStatementList();
            statements.AddStatement(compiler.gtNewIconNode(TYP_INT, 1));
            statements.AddStatement(compiler.gtNewIconNode(TYP_INT, 2));
            var value = compiler.gtNewIconNode(TYP_INT, 3);

            var tree = statements.PrefixTo(value, compiler);
            Assert.That(tree.AsOp().Op1.AsIntCon().IconVal, Is.EqualTo((nint)1));
            Assert.That(tree.AsOp().Op2.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(tree.AsOp().Op2.AsOp().Op1.AsIntCon().IconVal, Is.EqualTo((nint)2));
            Assert.That(tree.AsOp().Op2.AsOp().Op2, Is.SameAs(value));
        });
    }

    [Test]
    public static void DecomposedAddressIsClonedUntilTheLastUse()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var access = new Compiler.PhysicalPromotionLocationAccess();
            access.InitializeIndir(address, 8, null, GTF_EMPTY, 2);
            var first = access.GrabAddress(4, compiler).AsOp();
            var second = access.GrabAddress(0, compiler).AsOp();

            Assert.That(first.Op1, Is.Not.SameAs(address));
            Assert.That(second.Op1, Is.SameAs(address));
            Assert.That(first.Op2.AsIntCon().IconVal, Is.EqualTo((nint)12));
            Assert.That(second.Op2.AsIntCon().IconVal, Is.EqualTo((nint)8));
            access.CheckFullyUsed();
        });
    }

    [Test]
    public static void DecomposedAddressOffsetWrapsAtPointerWidth()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var access = new Compiler.PhysicalPromotionLocationAccess();
            access.InitializeIndir(compiler.gtNewLclvNode(TYP_BYREF, 0), long.MaxValue, null, GTF_EMPTY, 1);

            var address = access.GrabAddress(8, compiler).AsOp();
            Assert.That(address.Op2.AsIntCon().IconVal, Is.EqualTo(unchecked((nint)(long.MinValue + 7))));
            access.CheckFullyUsed();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DecompositionSpillsOnlyAddressExposedLocalAddresses(bool exposed)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].lvHasLdAddrOp = true;
            if (exposed)
            {
                compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            }

            var check = typeof(Compiler).GetMethod("PhysicalPromotionHasAddressExposedLocals",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            Assert.That(check.Invoke(compiler, [address]), Is.EqualTo(exposed));
        });
    }

    [Test]
    public static void PrivatePhaseRewritesPrimitiveFieldStoreAndRead()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            var write = compiler.gtNewStoreLclFldNode(TYP_INT, 0, 0, compiler.gtNewIconNode(TYP_INT, 42));
            var writeStatement = compiler.gtNewStmt(write);
            compiler.fgInsertStmtAtEnd(block, writeStatement);
            compiler.fgSequenceLocals(writeStatement);
            var readStatement = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_INT, 0, 0));
            compiler.fgInsertStmtAtEnd(block, readStatement);
            compiler.fgSequenceLocals(readStatement);
            compiler._dfsTree = compiler.fgComputeDfs();

            var run = typeof(Compiler).GetMethod("PhysicalPromotionRunImplementation",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = (PhaseStatus)run.Invoke(compiler, null)!;

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(writeStatement.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(writeStatement.RootNode.AsLclVarCommon().LclNum, Is.Not.Zero);
            if (readStatement.RootNode.Oper is GT_LCL_VAR)
            {
                Assert.That(readStatement.RootNode.AsLclVarCommon().LclNum,
                    Is.EqualTo(writeStatement.RootNode.AsLclVarCommon().LclNum));
            }
            else
            {
                Assert.That(readStatement.RootNode.Oper.IsCnsIntOrI, Is.True);
                Assert.That(readStatement.RootNode.AsIntCon().IconVal, Is.EqualTo((nint)42));
            }
        });
    }

    [TestCase(true, true, PhaseStatus.MODIFIED_EVERYTHING)]
    [TestCase(false, true, PhaseStatus.MODIFIED_NOTHING)]
    [TestCase(true, false, PhaseStatus.MODIFIED_NOTHING)]
    public static void PhysicalPromotionWrapperDispatchesOnlyEnabledProfitableCandidates(
        bool enabled, bool hasPrimitiveAccess, PhaseStatus expectedStatus)
    {
        var previousConfig = JitConfig;
        object config = JitConfig;
        typeof(JitConfigValues).GetField("_jitEnablePhysicalPromotion",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(config, 1);

        try
        {
            JitConfig = (JitConfigValues)config;
            FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
                compiler.opts.compFlags = enabled ? CLFLG_STRUCTPROMOTE : 0;
#if DEBUG
                compiler.info.compFullName = nameof(PhysicalPromotionWrapperDispatchesOnlyEnabledProfitableCandidates);
#endif
                compiler.lvaTable[0].Type = TYP_STRUCT;
                compiler.lvaTable[0].Layout = new ClassLayout(16);
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                compiler.fgFirstBB = block;
                compiler.fgLastBB = block;

                GenTree tree = hasPrimitiveAccess
                    ? compiler.gtNewStoreLclFldNode(TYP_INT, 0, 0, compiler.gtNewIconNode(TYP_INT, 42))
                    : compiler.gtNewLclvNode(TYP_STRUCT, 0);
                var statement = compiler.gtNewStmt(tree);
                compiler.fgInsertStmtAtEnd(block, statement);
                compiler.fgSequenceLocals(statement);
                if (hasPrimitiveAccess)
                {
                    var read = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_INT, 0, 0));
                    compiler.fgInsertStmtAtEnd(block, read);
                    compiler.fgSequenceLocals(read);
                }

                compiler._dfsTree = compiler.fgComputeDfs();
                var initialLocalCount = compiler.lvaCount;
                var run = typeof(Compiler).GetMethod("PhysicalPromotion",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.That((PhaseStatus)run.Invoke(compiler, null)!, Is.EqualTo(expectedStatus));

                if (expectedStatus is PhaseStatus.MODIFIED_NOTHING)
                {
                    Assert.That(compiler.lvaCount, Is.EqualTo(initialLocalCount));
                    Assert.That(statement.RootNode, Is.SameAs(tree));
                }
                else
                {
                    Assert.That(compiler.lvaCount, Is.GreaterThan(initialLocalCount));
                    Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    Assert.That(statement.RootNode.AsLclVarCommon().LclNum, Is.Not.Zero);
                    Assert.That(statement.RootNode, Is.Not.SameAs(tree));
                }
            });
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [TestCase(16, 0, 8, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive, 8)]
    [TestCase(16, 0, 16, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.None, 0)]
    [TestCase(24, 4, 8, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.FullBlock, 0)]
    public static void DecompositionSelectsTheUnpromotedRemainder(
        int layoutSize, int offset, int size, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind expectedKind,
        int expectedOffset)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(layoutSize);
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = compiler.lvaTable[0].Layout;
            var aggregateMap = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
            aggregateMap.Add(aggregate);
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregateMap);
            var replacer = CreateReplaceVisitor(compiler, aggregateMap, liveness);
            var source = compiler.gtNewLclvNode(TYP_STRUCT, 1);
            var store = compiler.gtNewStoreLclVarNode(0, source);
            var plan = new Compiler.PhysicalPromotionDecompositionPlan(
                compiler, replacer, aggregateMap, liveness, store, source, false, false);
            for (var position = offset; position < offset + size; position += 4)
            {
                plan.InitReplacement(new Compiler.PhysicalPromotionReplacement(position, TYP_INT), position);
            }

            var strategy = plan.DetermineRemainderStrategy(default);
            Assert.That(strategy.Kind, Is.EqualTo(expectedKind));
            if (expectedKind is Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive)
            {
                Assert.That(strategy.Offset, Is.EqualTo(expectedOffset));
                Assert.That(strategy.Type, Is.EqualTo(TYP_I_IMPL));
            }
        });
    }

#if TARGET_64BIT
    [TestCase(8, TYP_LONG)]
#endif
    [TestCase(1, TYP_UBYTE)]
    [TestCase(2, TYP_USHORT)]
    [TestCase(4, TYP_INT)]
    public static void DecompositionSelectsSupportedScalarRemainderWidths(int layoutSize, var_types expectedType)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            SetStructLayout(compiler, new ClassLayout(layoutSize));
            var plan = CreateRemainderPlan(compiler, compiler.gtNewLclvNode(TYP_STRUCT, 1));

            var strategy = plan.DetermineRemainderStrategy(default);

            Assert.That(strategy.Kind, Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive));
            Assert.That(strategy.Offset, Is.Zero);
            Assert.That(strategy.Type, Is.EqualTo(expectedType));
        });
    }

#if FEATURE_SIMD
    [TestCase(16, TYP_SIMD16)]
#if TARGET_XARCH
    [TestCase(32, TYP_SIMD32)]
    [TestCase(64, TYP_SIMD64)]
#endif
    public static void DecompositionSelectsOnlyAvailableSimdRemainderWidths(int size, var_types expectedType)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            SetStructLayout(compiler, new ClassLayout(size));
            var plan = CreateRemainderPlan(compiler, compiler.gtNewLclvNode(TYP_STRUCT, 1));

            var strategy = plan.DetermineRemainderStrategy(default);
            if (compiler.GetPreferredVectorByteLength() >= size)
            {
                Assert.That(strategy.Kind,
                    Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive));
                Assert.That(strategy.Type, Is.EqualTo(expectedType));
            }
            else
            {
                Assert.That(strategy.Kind,
                    Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.FullBlock));
            }
        });
    }
#endif

    [Test]
    public static void DecompositionAllowsNonzeroScalarRemainderInitialization()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            SetStructLayout(compiler, new ClassLayout(4));
            var source = compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1));
            var plan = CreateRemainderPlan(compiler, source);

            var strategy = plan.DetermineRemainderStrategy(default);

            Assert.That(strategy.Kind, Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive));
            Assert.That(strategy.Type, Is.EqualTo(TYP_INT));
        });
    }

#if FEATURE_SIMD
    [TestCase(0)]
    [TestCase(1)]
    public static void DecompositionChecksInitPatternBeforeChoosingSimdRemainder(int initPattern)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            SetStructLayout(compiler, new ClassLayout(16));
            var source = compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, initPattern));
            var plan = CreateRemainderPlan(compiler, source);

            var strategy = plan.DetermineRemainderStrategy(default);
            var supported = compiler.GetPreferredVectorByteLength() >= 16;
            if ((initPattern == 0) && supported)
            {
                Assert.That(strategy.Kind,
                    Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive));
                Assert.That(strategy.Type, Is.EqualTo(TYP_SIMD16));
            }
            else
            {
                Assert.That(strategy.Kind,
                    Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.FullBlock));
            }
        });
    }
#endif

    [TestCase(TYP_REF, false, 0, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive)]
    [TestCase(TYP_BYREF, false, 0, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive)]
    [TestCase(TYP_REF, true, 0, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive)]
    [TestCase(TYP_REF, true, 1, Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.FullBlock)]
    public static void DecompositionPreservesGcPointerRemainderTypes(
        var_types pointerType, bool isInit, int initPattern,
        Compiler.PhysicalPromotionDecompositionPlan.RemainderKind expectedKind)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var builder = new ClassLayoutBuilder(compiler, TARGET_POINTER_SIZE);
            builder.SetGCPtrType(0, pointerType);
            SetStructLayout(compiler, ClassLayout.Create(compiler, builder));
            var source = isInit
                ? compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, compiler.gtNewIconNode(TYP_INT, initPattern))
                : compiler.gtNewLclvNode(TYP_STRUCT, 1);
            var plan = CreateRemainderPlan(compiler, source);

            var strategy = plan.DetermineRemainderStrategy(default);

            Assert.That(strategy.Kind, Is.EqualTo(expectedKind));
            if (expectedKind is Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive)
            {
                Assert.That(strategy.Offset, Is.Zero);
                Assert.That(strategy.Type, Is.EqualTo(pointerType));
            }
        });
    }

    [Test]
    public static void DecompositionDoesNotReinterpretGcPointerIntersectionsAsPrimitives()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var builder = new ClassLayoutBuilder(compiler, 16);
            builder.SetGCPtrType(1, TYP_REF);
            SetStructLayout(compiler, ClassLayout.Create(compiler, builder));
            var plan = CreateRemainderPlan(compiler, compiler.gtNewLclvNode(TYP_STRUCT, 1));
            plan.InitReplacement(new Compiler.PhysicalPromotionReplacement(0, TYP_INT), 0);
            plan.InitReplacement(new Compiler.PhysicalPromotionReplacement(12, TYP_INT), 12);

            var strategy = plan.DetermineRemainderStrategy(default);

            Assert.That(strategy.Kind, Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.FullBlock));
        });
    }

    [Test]
    public static void DecompositionKeepsDyingRemainderOnlyWhenThereAreNoOtherStructUses()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            SetStructLayout(compiler, new ClassLayout(16));
            var aggregateMap = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0)
            {
                UnpromotedMin = 8,
                UnpromotedMax = 16,
            };
            var replacement = new Compiler.PhysicalPromotionReplacement(0, TYP_LONG) { LclNum = 1 };
            aggregate.Replacements.Add(replacement);
            aggregateMap.Add(aggregate);

            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregateMap);
            var replacer = CreateReplaceVisitor(compiler, aggregateMap, liveness);
            var source = compiler.gtNewLclvNode(TYP_STRUCT, 1);
            var store = compiler.gtNewStoreLclVarNode(0, source);
            var plan = new Compiler.PhysicalPromotionDecompositionPlan(
                compiler, replacer, aggregateMap, liveness, store, source, true, false);
            plan.InitReplacement(replacement, 0);

            var traits = new BitVecTraits(compiler, 2);
            var deaths = BitVecOps.MakeEmpty(traits);
            BitVecOps.AddElemD(traits, deaths, 0);
            var structDeaths = new Compiler.PhysicalPromotionStructDeaths(deaths, aggregate, traits);

            Assert.That(plan.DetermineRemainderStrategy(structDeaths).Kind,
                Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.None));

            var planWithOtherUse = new Compiler.PhysicalPromotionDecompositionPlan(
                compiler, replacer, aggregateMap, liveness, store, source, true, false);
            planWithOtherUse.InitReplacement(replacement, 0);
            planWithOtherUse.MarkNonRemainderUseOfStructLocal();
            var strategy = planWithOtherUse.DetermineRemainderStrategy(structDeaths);

            Assert.That(strategy.Kind,
                Is.EqualTo(Compiler.PhysicalPromotionDecompositionPlan.RemainderKind.Primitive));
            Assert.That(strategy.Offset, Is.EqualTo(8));
            Assert.That(strategy.Type, Is.EqualTo(TYP_LONG));
        });
    }

    private static Compiler.PhysicalPromotionDecompositionPlan CreateRemainderPlan(Compiler compiler, GenTree source)
    {
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[1].Type = TYP_STRUCT;
        compiler.lvaTable[1].Layout = compiler.lvaTable[0].Layout;
        var aggregateMap = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
        var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
        aggregateMap.Add(aggregate);
        var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregateMap);
        var replacer = CreateReplaceVisitor(compiler, aggregateMap, liveness);
        var store = compiler.gtNewStoreLclVarNode(0, source);
        return new Compiler.PhysicalPromotionDecompositionPlan(
            compiler, replacer, aggregateMap, liveness, store, source, false, false);
    }

    private static void SetStructLayout(Compiler compiler, ClassLayout layout)
    {
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[0].Layout = layout;
        compiler.lvaTable[1].Type = TYP_STRUCT;
        compiler.lvaTable[1].Layout = layout;
    }

    [Test]
    public static void PrivatePhaseDecomposesStructCopyWithPromotedFields()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var layout = new ClassLayout(16);
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = layout;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            var srcField = compiler.gtNewLclFldNode(TYP_INT, 0, 0);
            var srcRead = compiler.gtNewStmt(srcField);
            compiler.fgInsertStmtAtEnd(block, srcRead);
            compiler.fgSequenceLocals(srcRead);

            var copy = compiler.gtNewStoreLclVarNode(1, compiler.gtNewLclvNode(TYP_STRUCT, 0));
            var copyStatement = compiler.gtNewStmt(copy);
            compiler.fgInsertStmtAtEnd(block, copyStatement);
            compiler.fgSequenceLocals(copyStatement);

            var dstField = compiler.gtNewLclFldNode(TYP_INT, 1, 0);
            var dstRead = compiler.gtNewStmt(dstField);
            compiler.fgInsertStmtAtEnd(block, dstRead);
            compiler.fgSequenceLocals(dstRead);
            compiler._dfsTree = compiler.fgComputeDfs();

            var run = typeof(Compiler).GetMethod("PhysicalPromotionRunImplementation",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = (PhaseStatus)run.Invoke(compiler, null)!;

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(copyStatement.RootNode.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(srcRead.RootNode.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(srcRead.RootNode.AsLclVarCommon().LclNum, Is.Not.Zero);
            Assert.That(dstRead.RootNode.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(copyStatement.RootNode.AsOp().Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(copyStatement.RootNode.AsOp().Op2.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(copyStatement.RootNode.AsOp().Op2.AsLclVarCommon().Data.AsLclVarCommon().LclNum,
                Is.EqualTo(srcRead.RootNode.AsLclVarCommon().LclNum));
        });
    }

    [Test]
    public static void TwoCopiesBetweenSixFieldAggregatesOmitDyingDestinationRemainders()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var builder = new ClassLayoutBuilder(compiler, 32);
            builder.SetGCPtrType(0, TYP_REF);
            builder.SetGCPtrType(2, TYP_BYREF);
            var layout = ClassLayout.Create(compiler, builder);
            var fields = new (int Offset, var_types Type)[]
            {
                (0, TYP_REF), (8, TYP_INT), (12, TYP_UBYTE),
                (13, TYP_UBYTE), (16, TYP_BYREF), (24, TYP_INT),
            };

            for (var local = 0; local < 2; local++)
            {
                compiler.lvaTable[local].Type = TYP_STRUCT;
                compiler.lvaTable[local].Layout = layout;
            }

            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            void AddFieldReads(int local)
            {
                foreach (var (offset, type) in fields)
                {
                    var statement = compiler.gtNewStmt(compiler.gtNewLclFldNode(type, local, checked((ushort)offset)));
                    compiler.fgInsertStmtAtEnd(block, statement);
                    compiler.fgSequenceLocals(statement);
                }
            }

            AddFieldReads(0);
            var firstCopy = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1,
                compiler.gtNewLclvNode(TYP_STRUCT, 0)));
            compiler.fgInsertStmtAtEnd(block, firstCopy);
            compiler.fgSequenceLocals(firstCopy);
            AddFieldReads(1);
            var secondCopy = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1,
                compiler.gtNewLclvNode(TYP_STRUCT, 0)));
            compiler.fgInsertStmtAtEnd(block, secondCopy);
            compiler.fgSequenceLocals(secondCopy);
            AddFieldReads(1);

            var select = typeof(Compiler).GetMethod("PhysicalPromotionSelectCandidates",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var previousConfig = JitConfig;
            object config = JitConfig;
            typeof(JitConfigValues).GetField("_jitMaxLocalsToTrack",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(config, 1024);
            Compiler.PhysicalPromotionAggregateInfoMap aggregates;
            try
            {
                JitConfig = (JitConfigValues)config;
                aggregates = (Compiler.PhysicalPromotionAggregateInfoMap)select.Invoke(compiler, null)!;
            }
            finally
            {
                JitConfig = previousConfig;
            }

            Assert.That(aggregates, Is.Not.Null);
            for (var local = 0; local < 2; local++)
            {
                var aggregate = aggregates.Lookup(local);
                Assert.That(aggregate, Is.Not.Null, $"V{local} was not selected");
                var replacements = aggregate!.Replacements;
                Assert.That(replacements.Count, Is.EqualTo(fields.Length));
                for (var index = 0; index < fields.Length; index++)
                {
                    Assert.That((replacements[index].Offset, replacements[index].AccessType),
                        Is.EqualTo(fields[index]));
                }
            }

#if DEBUG
            compiler.verbose = true;
#endif
            compiler._dfsTree = compiler.fgComputeDfs();
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
#if DEBUG
            var previousOutput = s_jitstdout;
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            try
            {
                s_jitstdout = writer;
                liveness.Run();
                writer.Flush();

                var dump = Encoding.UTF8.GetString(stream.ToArray());
                Assert.That(dump, Does.Contain("BB01 USE("));
                Assert.That(dump, Does.Contain("BB01 DEF("));
                Assert.That(dump, Does.Contain("BB01 IN ("));
                Assert.That(dump, Does.Contain("BB01 OUT("));
                Assert.That(dump, Does.Contain("V00.[000..008)"));
            }
            finally
            {
                s_jitstdout = previousOutput;
            }
#else
            liveness.Run();
#endif
            var firstDeaths = liveness.GetDeathsForStructLocal(firstCopy.RootNode.AsLclVarCommon());
            var secondDeaths = liveness.GetDeathsForStructLocal(secondCopy.RootNode.AsLclVarCommon());
            Assert.That(firstDeaths.IsRemainderDying(), Is.True);
            Assert.That(secondDeaths.IsRemainderDying(), Is.True);

            var replacer = CreateReplaceVisitor(compiler, aggregates, liveness);
            for (var statement = replacer.StartBlock(block); statement is not null; statement = statement.NextStmt)
            {
                replacer.StartStatement(statement);
                replacer.WalkTree(ref statement.RootNodeRef);
            }

            replacer.EndBlock();

            void AssertFieldCopies(GenTree tree)
            {
                var index = 0;
                var source = aggregates.Lookup(0)!;
                var destination = aggregates.Lookup(1)!;

                void Visit(GenTree node)
                {
                    if (node.Oper is GT_COMMA)
                    {
                        Visit(node.AsOp().Op1);
                        Visit(node.AsOp().Op2);
                        return;
                    }

                    Assert.That(node.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    Assert.That(index, Is.LessThan(fields.Length));
                    Assert.That(node.AsLclVarCommon().LclNum,
                        Is.EqualTo(destination.Replacements[index].LclNum));
                    Assert.That(node.AsLclVarCommon().Data.Oper, Is.EqualTo(GT_LCL_VAR));
                    Assert.That(node.AsLclVarCommon().Data.AsLclVarCommon().LclNum,
                        Is.EqualTo(source.Replacements[index].LclNum));
                    index++;
                }

                Visit(tree);
                Assert.That(index, Is.EqualTo(fields.Length));
            }

            AssertFieldCopies(firstCopy.RootNode);
            AssertFieldCopies(secondCopy.RootNode);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PrivatePhaseDecomposesOnlyConstantStructInit(bool runtimeFill)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            compiler.lvaTable[1].Type = TYP_INT;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            GenTree fill = runtimeFill ? compiler.gtNewLclvNode(TYP_INT, 1) : compiler.gtNewIconNode(TYP_INT, 0);
            var pattern = compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, fill);
            var initStatement = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, pattern));
            compiler.fgInsertStmtAtEnd(block, initStatement);
            compiler.fgSequenceLocals(initStatement);
            var readStatement = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_INT, 0, 0));
            compiler.fgInsertStmtAtEnd(block, readStatement);
            compiler.fgSequenceLocals(readStatement);
            var wholeRead = compiler.gtNewStmt(compiler.gtNewLclvNode(TYP_STRUCT, 0));
            compiler.fgInsertStmtAtEnd(block, wholeRead);
            compiler.fgSequenceLocals(wholeRead);
            compiler._dfsTree = compiler.fgComputeDfs();

            var run = typeof(Compiler).GetMethod("PhysicalPromotionRunImplementation",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = (PhaseStatus)run.Invoke(compiler, null)!;

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            if (runtimeFill)
            {
                Assert.That(initStatement.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(initStatement.RootNode.AsLclVarCommon().LclNum, Is.Zero);
                Assert.That(initStatement.RootNode.Data, Is.SameAs(pattern));
                var foundReadBack = false;
                for (var statement = initStatement.NextStmt; !ReferenceEquals(statement, readStatement);
                     statement = statement?.NextStmt)
                {
                    Assert.That(statement, Is.Not.Null);
                    var root = statement!.RootNode;
                    if ((root.Oper is GT_STORE_LCL_VAR) && (root.AsLclVarCommon().LclNum != 0) &&
                        (root.Data.Oper is GT_LCL_FLD) && (root.Data.AsLclVarCommon().LclNum == 0))
                    {
                        foundReadBack = true;
                    }
                }

                Assert.That(foundReadBack, Is.True, "Runtime fill must read the field from the original struct store");
                return;
            }

            Assert.That(initStatement.RootNode.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(initStatement.RootNode.AsOp().Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(initStatement.RootNode.AsOp().Op1.AsLclVarCommon().LclNum, Is.Not.Zero);
            Assert.That(initStatement.RootNode.AsOp().Op2.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(initStatement.RootNode.AsOp().Op2.AsLclVarCommon().LclNum, Is.Zero);
        });
    }

    [Test]
    public static void PrivatePhaseReturnsFullyPromotedStructAsFieldList()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(8);
            compiler.genReturnLocal = BAD_VAR_NUM;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            var field = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            compiler.fgInsertStmtAtEnd(block, field);
            compiler.fgSequenceLocals(field);
            var ret = compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_RETURN, TYP_STRUCT,
                compiler.gtNewLclvNode(TYP_STRUCT, 0)));
            compiler.fgInsertStmtAtEnd(block, ret);
            compiler.fgSequenceLocals(ret);
            compiler._dfsTree = compiler.fgComputeDfs();

            var run = typeof(Compiler).GetMethod("PhysicalPromotionRunImplementation",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = (PhaseStatus)run.Invoke(compiler, null)!;

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(ret.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_FIELD_LIST));
            Assert.That(field.RootNode.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [Test]
    public static void PrivatePhaseKeepsMergedStructReturnsAsStores()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var layout = new ClassLayout(8);
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = layout;
            compiler.genReturnLocal = 1;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            var field = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            compiler.fgInsertStmtAtEnd(block, field);
            compiler.fgSequenceLocals(field);
            var originalReturn = compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_RETURN, TYP_STRUCT,
                compiler.gtNewLclvNode(TYP_STRUCT, 0)));
            compiler.fgInsertStmtAtEnd(block, originalReturn);
            compiler.fgSequenceLocals(originalReturn);
            compiler._dfsTree = compiler.fgComputeDfs();

            var run = typeof(Compiler).GetMethod("PhysicalPromotionRunImplementation",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = (PhaseStatus)run.Invoke(compiler, null)!;

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(originalReturn.RootNode.IsNothingNode, Is.True);
            Assert.That(block.LastStmt!.RootNode.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(block.LastStmt.RootNode.AsUnOp().Op1.AsLclVarCommon().LclNum, Is.EqualTo(1));
        });
    }

    [Test]
    public static void LogicalOccurrencesPreserveListOrderSizesAndPromotedParents()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = new ClassLayout(8);
            compiler.lvaTable[1].lvPromoted = true;
            compiler.lvaTable[1].lvFieldLclStart = 2;
            compiler.lvaTable[1].lvFieldCnt = 1;
            var field = compiler.gtNewLclFldNode(TYP_SHORT, 0, 2);
            var parent = compiler.gtNewLclvNode(TYP_STRUCT, 1);
            var statement = compiler.gtNewStmt(compiler.gtNewCommaNode(TYP_STRUCT, field, parent));
            compiler.fgSequenceLocals(statement);
            var occurrences = new List<LocalOccurrence>();

            var result = statement.VisitLogicalLocalOccurrencesViaLocalsTreeList(occurrence => {
                occurrences.Add(occurrence);
                return GenTree.VisitResult.Continue;
            });

            Assert.That(result, Is.EqualTo(GenTree.VisitResult.Continue));
            GenTree[] expectedNodes = [field, parent];
            Assert.That(occurrences.Select(occurrence => occurrence.Node), Is.EqualTo(expectedNodes));
            Assert.That((occurrences[0].LclNum, occurrences[1].LclNum), Is.EqualTo((0, 1)));
            Assert.That((occurrences[0].LclOffs, occurrences[1].LclOffs), Is.EqualTo((2, 0)));
            Assert.That((occurrences[0].GetAccessSize(compiler), occurrences[1].GetAccessSize(compiler)),
                Is.EqualTo((2, 8)));
            Assert.That(occurrences[1].GetAccessType(compiler), Is.EqualTo(TYP_STRUCT));

            occurrences.Clear();
            result = statement.VisitLogicalLocalOccurrencesViaLocalsTreeList(occurrence => {
                occurrences.Add(occurrence);
                return GenTree.VisitResult.Abort;
            });
            Assert.That(result, Is.EqualTo(GenTree.VisitResult.Abort));
            Assert.That(occurrences.Count, Is.EqualTo(1));
            Assert.That(statement.LocalsTreeList, Is.EqualTo(expectedNodes));

            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 4);
            address.Flags |= GTF_VAR_DEF;
            var addressOccurrence = new LocalOccurrence(address);
            Assert.That(addressOccurrence.Node, Is.SameAs(address));
            Assert.That(addressOccurrence.LclNum, Is.Zero);
            Assert.That(addressOccurrence.LclOffs, Is.EqualTo(4));
            Assert.That(addressOccurrence.Flags & GTF_VAR_DEF, Is.Not.Zero);
        });
    }

    [TestCase(0, 8, true)]
    [TestCase(0, 4, false)]
    [TestCase(4, 4, false)]
    public static void LivenessDefinitionsRequireFullReplacementCoverage(int offset, int size, bool fullDefinition)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[]]);
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = new ClassLayout(size);
            var store = new GenTreeLclFld(TYP_STRUCT, 0, checked((ushort)offset),
                compiler.gtNewLclvNode(TYP_STRUCT, 1), compiler.lvaTable[1].Layout);
            store.Flags |= GTF_VAR_DEF;
            _ = AddPromotionStatement(compiler, blocks[0], store);
            _ = AddPromotionStatement(compiler, blocks[0], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);

            liveness.Run();

            Assert.That(liveness.IsReplacementDefined(blocks[0], 0, 0), Is.EqualTo(fullDefinition));
            Assert.That(liveness.IsReplacementUsed(blocks[0], 0, 0), Is.EqualTo(!fullDefinition));
            Assert.That(liveness.IsReplacementLiveIn(blocks[0], 0, 0), Is.EqualTo(!fullDefinition));
        });
    }

    [TestCase(0, 8, true, 1)]
    [TestCase(0, 4, false, 2)]
    [TestCase(4, 4, false, 1)]
    public static unsafe void RetbufferOccurrencesUseTheCallDefinitionSize(
        int offset, int size, bool fullDefinition, int expectedReadBacks)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[]]);
            var handle = (CORINFO_CLASS_STRUCT_*)1;
            var layout = new ClassLayout(handle, true, checked((uint)size), TYP_STRUCT, "Retbuffer", "Retbuffer");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            typeof(Compiler).GetField("_classLayoutTable", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(compiler, layouts);
#if DEBUG
            compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, checked((ushort)offset));
            address.Flags |= GTF_VAR_DEF;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.RetClsHnd = handle;
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.RetBuffer));
            _ = AddPromotionStatement(compiler, blocks[0], call);
            var fieldUse = AddPromotionStatement(compiler, blocks[0], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            compiler._dfsTree = compiler.fgComputeDfs();
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);

            liveness.Run();

            Assert.That(liveness.IsReplacementDefined(blocks[0], 0, 0), Is.EqualTo(fullDefinition));
            Assert.That(liveness.IsReplacementUsed(blocks[0], 0, 0), Is.EqualTo(!fullDefinition));
            Assert.That(liveness.GetDeathsForStructLocal(address).IsReplacementDying(0), Is.False);
            var visitor = CreateReplaceVisitor(compiler, aggregates, liveness);
            RewritePromotionBlocks(compiler, visitor);
            Assert.That(CountReadBacks(blocks[0], replacement), Is.EqualTo(expectedReadBacks));
            Assert.That(fieldUse.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
            AssertReadBack(fieldUse.PrevStmt!, 0, replacement);
        });
    }

    [Test]
    public static void ConditionalDefinitionsDoNotKillIncomingReplacementLiveness()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[]]);
            compiler.compQmarkUsed = true;
            var store = compiler.gtNewStoreLclFldNode(TYP_LONG, 0, 0, compiler.gtNewLconNode(17));
            var colon = compiler.gtNewColonNode(TYP_VOID, store, compiler.gtNewNothingNode());
            var condition = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 0));
            _ = AddPromotionStatement(compiler, blocks[0], compiler.gtNewQmarkNode(TYP_VOID, condition, colon));
            _ = AddPromotionStatement(compiler, blocks[0], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);

            liveness.Run();

            Assert.That(liveness.IsReplacementDefined(blocks[0], 0, 0), Is.False);
            Assert.That(liveness.IsReplacementUsed(blocks[0], 0, 0), Is.True);
            Assert.That(liveness.IsReplacementLiveIn(blocks[0], 0, 0), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MixedJoinsReadBackOnlyPendingPredecessors(bool storeOnOtherPath)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1, 2], [3], [3], []]);
            blocks[1].bbWeight = 60;
            blocks[2].bbWeight = 40;
            var other = AddPromotionStatement(compiler, blocks[1], storeOnOtherPath
                ? compiler.gtNewStoreLclFldNode(TYP_LONG, 0, 0, compiler.gtNewLconNode(17))
                : compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var use = AddPromotionStatement(compiler, blocks[3], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            var visitor = PreparePromotion(compiler, aggregates);
            Assert.That(replacement.ReadBackPlacement, Is.Null);

            var dfs = compiler._dfsTree!;
            for (var index = dfs.PostOrderCount; index > 0; index--)
            {
                var block = dfs.GetPostOrder(index - 1);
                var first = visitor.StartBlock(block);
                if (block == blocks[3])
                {
                    Assert.That(replacement.NeedsReadBack, Is.False);
                    Assert.That(replacement.NeedsWriteBack, Is.EqualTo(storeOnOtherPath));
                }

                RewritePromotionStatements(visitor, first);
                visitor.EndBlock();
            }

            Assert.That(CountReadBacks(blocks[0], replacement), Is.Zero);
            Assert.That(CountReadBacks(blocks[1], replacement), Is.EqualTo(storeOnOtherPath ? 0 : 1));
            Assert.That(CountReadBacks(blocks[2], replacement), Is.EqualTo(1));
            AssertReadBack(blocks[2].LastStmt!, 0, replacement);
            Assert.That(CountReadBacks(blocks[3], replacement), Is.Zero);
            Assert.That(use.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
            Assert.That(other.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
            if (storeOnOtherPath)
            {
                Assert.That(other.RootNode.Data.IsIntegralConst(17), Is.True);
            }
        });
    }

    [TestCase(60, false, false, true)]
    [TestCase(60, true, false, true)]
    [TestCase(50, false, false, false)]
    [TestCase(50.000025, false, false, false)]
    [TestCase(60, false, true, false)]
    public static void CommonReadBacksRequireReconciliationAndStrictWeightSavings(
        double siteWeight, bool precomputedDominators, bool bothPathsUse, bool expectedPlacement)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1, 2], [3], [3], []]);
            blocks[1].bbWeight = siteWeight;
            blocks[2].bbWeight = siteWeight;
            _ = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            if (bothPathsUse)
            {
                _ = AddPromotionStatement(compiler, blocks[2], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            }
            _ = AddPromotionStatement(compiler, blocks[3], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            compiler._dfsTree = compiler.fgComputeDfs();
            if (precomputedDominators)
            {
                compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            }
            var previousDominators = compiler._domTree;
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            var visitor = PreparePromotion(compiler, aggregates);

            Assert.That(replacement.ReadBackPlacement, expectedPlacement ? Is.SameAs(blocks[0]) : Is.Null);
            if (precomputedDominators)
            {
                Assert.That(compiler._domTree, Is.SameAs(previousDominators));
            }
            if (bothPathsUse)
            {
                Assert.That(compiler._domTree, Is.Null);
            }

            RewritePromotionBlocks(compiler, visitor);

            Assert.That(CountReadBacks(blocks[0], replacement), Is.EqualTo(expectedPlacement ? 1 : 0));
            Assert.That(blocks.Sum(block => CountReadBacks(block, replacement)), Is.EqualTo(expectedPlacement ? 1 : 2));
            if (expectedPlacement)
            {
                AssertReadBack(blocks[0].FirstStmt!, 0, replacement);
                Assert.That(blocks[0].LastStmt!.RootNode.Oper, Is.EqualTo(GT_JTRUE));
            }
        });
    }

    [Test]
    public static void CommonReadBackDominatorsUseOnlyTheEntrySubtreeOfADfsForest()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1, 2], [3], [3], [], []]);
            blocks[1].bbWeight = blocks[2].bbWeight = 60;
            _ = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            _ = AddPromotionStatement(compiler, blocks[3], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var dfs = compiler.fgComputeDfs();
            Assert.That(dfs.Contains(blocks[4]), Is.False);
            blocks[4].bbPostorderNum = dfs.PostOrderCount;
            var postOrder = dfs.GetPostOrder().Take(dfs.PostOrderCount).Append(blocks[4]).ToArray();
            compiler._dfsTree = new FlowGraphDfsTree(compiler, postOrder, postOrder.Length,
                dfs.HasCycle, dfs.IsProfileAware);
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];

            var visitor = PreparePromotion(compiler, aggregates);

            Assert.That(replacement.ReadBackPlacement, Is.SameAs(blocks[0]));
            Assert.That(compiler._domTree, Is.Null);
            RewritePromotionBlocks(compiler, visitor);
            Assert.That(blocks.Sum(block => CountReadBacks(block, replacement)), Is.EqualTo(1));
        });
    }

    [Test]
    public static void DefinitionsEndTheIncomingValueDuringReadBackPlanning()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1, 2], [3], [3], []]);
            blocks[1].bbWeight = blocks[2].bbWeight = 60;
            _ = AddPromotionStatement(compiler, blocks[1],
                compiler.gtNewStoreLclFldNode(TYP_LONG, 0, 0, compiler.gtNewLconNode(17)));
            _ = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            _ = AddPromotionStatement(compiler, blocks[3], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];

            var visitor = PreparePromotion(compiler, aggregates);

            Assert.That(replacement.ReadBackPlacement, Is.Null);
            Assert.That(compiler._domTree, Is.Null);
            RewritePromotionBlocks(compiler, visitor);
            Assert.That(CountReadBacks(blocks[0], replacement), Is.Zero);
            Assert.That(CountReadBacks(blocks[1], replacement), Is.Zero);
            Assert.That(CountReadBacks(blocks[2], replacement), Is.EqualTo(1));
            Assert.That(CountReadBacks(blocks[3], replacement), Is.Zero);
        });
    }

    [Test]
    public static void DenseReadBackIndicesDistinguishAggregatesAcrossTheBitVectorBoundary()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1], []]);
            compiler.lvaTable = new LclVarDsc[67];
            compiler.lvaCount = compiler.lvaTable.Length;
            for (var index = 0; index < compiler.lvaCount; index++)
            {
                compiler.lvaTable[index].Type = TYP_INT;
            }
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(256);
            compiler.lvaTable[0].lvIsOSRLocal = true;
            compiler.lvaTable[1].Type = TYP_STRUCT;
            compiler.lvaTable[1].Layout = new ClassLayout(4);
            compiler.lvaTable[1].lvIsOSRLocal = true;
            _ = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_INT, 0, 252));
            _ = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_INT, 1, 0));
            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var first = new Compiler.PhysicalPromotionAggregateInfo(0);
            for (var index = 0; index < 64; index++)
            {
                first.Replacements.Add(new Compiler.PhysicalPromotionReplacement(index * 4, TYP_INT)
                {
                    LclNum = index + 2,
                });
            }
            var second = new Compiler.PhysicalPromotionAggregateInfo(1);
            second.Replacements.Add(new Compiler.PhysicalPromotionReplacement(0, TYP_INT) { LclNum = 66 });
            aggregates.Add(first);
            aggregates.Add(second);
            var visitor = PreparePromotion(compiler, aggregates);
            Assert.That(first.Replacements.Select(replacement => replacement.ReadBackIndex),
                Is.EqualTo(Enumerable.Range(0, 64)));
            Assert.That(second.Replacements[0].ReadBackIndex, Is.EqualTo(64));

            RewritePromotionBlocks(compiler, visitor);

            Assert.That(blocks[0].FirstStmt, Is.Null);
            Assert.That(CountReadBacks(blocks[1], first.Replacements[^1]), Is.EqualTo(1));
            Assert.That(CountReadBacks(blocks[1], second.Replacements[0]), Is.EqualTo(1));
            AssertReadBack(blocks[1].FirstStmt!, 0, first.Replacements[^1]);
            AssertReadBack(blocks[1].FirstStmt!.NextStmt!.NextStmt!, 1, second.Replacements[0]);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LoopEntryReadBacksAreSettledBeforeBackedges(bool irreducible)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, irreducible
                ? [[1, 2], [3], [3], [1, 4], []]
                : [[1], [2, 3], [1], []]);
            _ = AddPromotionStatement(compiler, blocks[^1], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            var visitor = PreparePromotion(compiler, aggregates);
            Assert.That(compiler._dfsTree!.HasCycle, Is.True);

            RewritePromotionBlocks(compiler, visitor);

            Assert.That(CountReadBacks(blocks[0], replacement), Is.EqualTo(1));
            Assert.That(blocks.Sum(block => CountReadBacks(block, replacement)), Is.EqualTo(1));
            Assert.That(replacement.ReadBackPlacement, irreducible ? Is.SameAs(blocks[0]) : Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EhBoundariesMaterializeReadBacksBeforeHandlerEntry(bool throwingStatement)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1], [2], [], []]);
            var tryBlock = blocks[1];
            var handler = blocks[3];
            tryBlock.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = tryBlock,
                    ebdTryLast = tryBlock,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            Statement? throwing = null;
            if (throwingStatement)
            {
                compiler.lvaTable[1].Type = TYP_I_IMPL;
                throwing = AddPromotionStatement(compiler, tryBlock,
                    compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_I_IMPL, 1)));
            }
            _ = AddPromotionStatement(compiler, blocks[2], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            _ = AddPromotionStatement(compiler, handler, compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            var visitor = PreparePromotion(compiler, aggregates);

            RewritePromotionBlocks(compiler, visitor);

            Assert.That(CountReadBacks(blocks[0], replacement), Is.Zero);
            Assert.That(CountReadBacks(tryBlock, replacement), Is.EqualTo(1));
            Assert.That(CountReadBacks(handler, replacement), Is.Zero);
            Assert.That(CountReadBacks(blocks[2], replacement), Is.Zero);
            AssertReadBack(tryBlock.FirstStmt!, 0, replacement);
            if (throwing is not null)
            {
                Assert.That(tryBlock.FirstStmt!.NextStmt, Is.SameAs(throwing));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FiltersMaterializeReadBacksAtTransfersAndOuterExceptionBoundaries(
        bool throwingStatement, bool outerHandler)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, outerHandler
                ? [[1], [2], [], [4], [], []] : [[1], [2], [], [4], []]);
            var tryBlock = blocks[1];
            var filter = blocks[3];
            var handler = blocks[4];
            tryBlock.TryIndex = 0;
            filter.HndIndex = 0;
            filter.CatchType = bbCatchType.BBCT_FILTER;
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FILTER_HANDLER;
            filter.SetKindAndTargetEdge(BBJ_EHFILTERRET, filter.TargetEdge);
            var filterReturn = compiler.gtNewStmt(new GenTreeUnOp(GT_RETFILT, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1)));
            compiler.fgInsertStmtAtEnd(filter, filterReturn);
            compiler.fgSequenceLocals(filterReturn);
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_FILTER,
                    ebdTryBeg = tryBlock,
                    ebdTryLast = tryBlock,
                    ebdFilter = filter,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = outerHandler ? (ushort)1 : EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            if (outerHandler)
            {
                blocks[5].HndIndex = 1;
                var outer = new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                    ebdTryBeg = tryBlock,
                    ebdTryLast = tryBlock,
                    ebdHndBeg = blocks[5],
                    ebdHndLast = blocks[5],
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                };
                compiler.compHndBBtab = [compiler.compHndBBtab[0], outer];
                compiler.compHndBBtabCount = 2;
                _ = AddPromotionStatement(compiler, blocks[5], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            }
            var call = AddPromotionStatement(compiler, filter, CreatePromotionRetbufferCall(compiler));
            Statement? throwing = null;
            if (throwingStatement)
            {
                compiler.lvaTable[1].Type = TYP_I_IMPL;
                throwing = AddPromotionStatement(compiler, filter,
                    compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_I_IMPL, 1)));
            }
            _ = AddPromotionStatement(compiler, blocks[2], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var use = AddPromotionStatement(compiler, handler, compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            var visitor = PreparePromotion(compiler, aggregates);

            RewritePromotionBlocks(compiler, visitor);

            Assert.That(CountReadBacks(filter, replacement), Is.EqualTo(1));
            Assert.That(filter.FirstStmt, Is.SameAs(call));
            var readBack = outerHandler ? throwing!.PrevStmt! : filterReturn.PrevStmt!;
            AssertReadBack(readBack, 0, replacement);
            if (outerHandler)
            {
                Assert.That(call.NextStmt, Is.SameAs(readBack));
                Assert.That(readBack.NextStmt, Is.SameAs(throwing));
            }
            else
            {
                Assert.That(call.NextStmt, Is.SameAs(throwing ?? readBack));
                Assert.That(readBack.NextStmt, Is.SameAs(filterReturn));
            }
            Assert.That(filter.LastStmt, Is.SameAs(filterReturn));
            Assert.That(CountReadBacks(handler, replacement), Is.Zero);
            Assert.That(use.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
        });
    }

    [Test]
    public static void FinallyCallsAndReturnsMaterializeReadBacksOnBothTransfers()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[1], [3], [4], [2], []]);
            var callFinally = blocks[1];
            var continuation = blocks[2];
            var handler = blocks[3];
            callFinally.SetKindAndTargetEdge(BBJ_CALLFINALLY, callFinally.TargetEdge);
            continuation.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, continuation.TargetEdge);
            handler.HndIndex = 0;
            handler.CatchType = bbCatchType.BBCT_FINALLY;
            handler.SetEhf(new BBJumpTable([handler.TargetEdge]));
            var finallyReturn = compiler.gtNewStmt(new GenTreeUnOp(GT_RETFILT, TYP_VOID, null));
            compiler.fgInsertStmtAtEnd(handler, finallyReturn);
            compiler.fgSequenceLocals(finallyReturn);
            compiler.compHndBBtab = [
                new EHblkDsc
                {
                    ebdHandlerType = EHHandlerType.EH_HANDLER_FINALLY,
                    ebdTryBeg = callFinally,
                    ebdTryLast = callFinally,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var oldValue = AddPromotionStatement(compiler, handler, compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var call = AddPromotionStatement(compiler, handler, CreatePromotionRetbufferCall(compiler));
            var use = AddPromotionStatement(compiler, continuation, compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var aggregates = CreatePromotionAggregates(compiler);
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            var visitor = PreparePromotion(compiler, aggregates);

            RewritePromotionBlocks(compiler, visitor);

            Assert.That(CountReadBacks(callFinally, replacement), Is.EqualTo(1));
            AssertReadBack(callFinally.LastStmt!, 0, replacement);
            Assert.That(handler.FirstStmt, Is.SameAs(oldValue));
            Assert.That(oldValue.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
            Assert.That(CountReadBacks(handler, replacement), Is.EqualTo(1));
            AssertReadBack(call.NextStmt!, 0, replacement);
            Assert.That(call.NextStmt!.NextStmt, Is.SameAs(finallyReturn));
            Assert.That(handler.LastStmt, Is.SameAs(finallyReturn));
            Assert.That(CountReadBacks(continuation, replacement), Is.Zero);
            Assert.That(use.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
        });
    }

    [Test]
    public static void PrivatePhaseVisitsForwardPredecessorsBeforeNonlexicalSuccessors()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[2], [], [1]]);
            var firstUse = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            var lastUse = AddPromotionStatement(compiler, blocks[1], compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            compiler._dfsTree = compiler.fgComputeDfs();
            var run = typeof(Compiler).GetMethod("PhysicalPromotionRunImplementation",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.That((PhaseStatus)run.Invoke(compiler, null)!, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            Assert.That(blocks[0].FirstStmt, Is.Null);
            Assert.That(blocks[2].FirstStmt, Is.Null);
            var readBack = blocks[1].FirstStmt!.RootNode.AsLclVarCommon();
            Assert.That(readBack.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(readBack.Data.AsLclFld().LclNum, Is.Zero);
            Assert.That(firstUse.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(readBack.LclNum));
            Assert.That(lastUse.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(readBack.LclNum));
        });
    }

#if HAS_FIXED_REGISTER_SET
    [TestCase(TYP_BYTE, 1)]
    [TestCase(TYP_SHORT, 2)]
    [TestCase(TYP_UBYTE, 1)]
    [TestCase(TYP_USHORT, 2)]
    public static void RareSmallRegisterParameterFieldsReceiveExtractionCredit(var_types type, int offset)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[]], layoutSize: TARGET_POINTER_SIZE);
            compiler.lvaTable[0].lvIsOSRLocal = false;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(IntArgRegs[0], 0, TARGET_POINTER_SIZE)),
            ];
            var uses = new Compiler.PhysicalPromotionLocalUses();
            uses.RecordAccess(offset, type, null, Compiler.PhysicalPromotionAccessKindFlags.None, 1);
            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);

            Assert.That(uses.PickPromotions(compiler, 0, aggregates), Is.EqualTo(1));

            var replacement = aggregates.Lookup(0)!.Replacements[0];
            replacement.LclNum = 2;
            compiler.lvaTable[replacement.LclNum].Type = type;
            var fieldUse = AddPromotionStatement(compiler, blocks[0],
                compiler.gtNewLclFldNode(type, 0, checked((ushort)offset)));
            var visitor = PreparePromotion(compiler, aggregates);
            _ = visitor.StartBlock(blocks[0]);
            Assert.That(blocks[0].FirstStmt, Is.SameAs(fieldUse));
            Assert.That(replacement.NeedsReadBack, Is.True);
            RewritePromotionStatements(visitor, fieldUse);
            visitor.EndBlock();
            AssertReadBack(blocks[0].FirstStmt!, 0, replacement);
            Assert.That(blocks[0].FirstStmt!.RootNode.Data.Type, Is.EqualTo(type));
        });
    }
#endif

#if TARGET_AMD64
    [TestCase("split", TYP_SHORT, 9, true)]
    [TestCase("split", TYP_LONG, 4, false)]
    [TestCase("floating", TYP_BYTE, 0, true)]
    [TestCase("floating", TYP_SHORT, 1, false)]
    [TestCase("floating", TYP_FLOAT, 0, false)]
    [TestCase("integer", TYP_FLOAT, 0, true)]
    [TestCase("mixed", TYP_BYTE, 8, true)]
    [TestCase("mixed", TYP_SHORT, 9, false)]
    [TestCase("stack", TYP_SHORT, 1, false)]
    [TestCase("byref", TYP_SHORT, 1, false)]
    public static void ParameterSegmentAndRegisterClassRulesPreserveLazyTypedReadBacks(
        string shape, var_types type, int offset, bool mapsToRegister)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var blocks = CreatePromotionGraph(compiler, [[]], layoutSize: 16);
            compiler.lvaTable[0].lvIsOSRLocal = false;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.info.compArgsCount = 1;
            var first = AbiPassingSegment.InRegister(IntArgRegs[0], 0, 8);
            var floating = AbiPassingSegment.InRegister(FltArgRegs[0], 0, 8);
            var second = AbiPassingSegment.InRegister(IntArgRegs[1], 8, 8);
            var secondFloating = AbiPassingSegment.InRegister(FltArgRegs[0], 8, 8);
            var passing = shape switch {
                "integer" => AbiPassingInformation.FromSegment(compiler, false, first),
                "floating" => AbiPassingInformation.FromSegment(compiler, false, floating),
                "split" => AbiPassingInformation.FromSegments(compiler, first, second),
                "mixed" => AbiPassingInformation.FromSegments(compiler, first, secondFloating),
                "stack" => AbiPassingInformation.FromSegments(compiler, first, AbiPassingSegment.OnStack(0, 8, 8)),
                "byref" => AbiPassingInformation.FromSegment(compiler, true, first),
                _ => throw new ArgumentException("Unknown parameter passing shape.", nameof(shape)),
            };
            compiler.lvaParameterPassingInfo = [passing];
            var maps = typeof(Compiler).GetMethod("PhysicalPromotionMapsToParameterRegister",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.That((bool)maps.Invoke(compiler, [0, offset, type])!, Is.EqualTo(mapsToRegister));
            var uses = new Compiler.PhysicalPromotionLocalUses();
            uses.RecordAccess(offset, type, null, Compiler.PhysicalPromotionAccessKindFlags.None, 1);
            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            Assert.That(uses.PickPromotions(compiler, 0, aggregates), Is.EqualTo(mapsToRegister ? 1 : 0));
            if (!mapsToRegister)
            {
                uses.RecordAccess(offset, type, null, Compiler.PhysicalPromotionAccessKindFlags.None, 200);
                Assert.That(uses.PickPromotions(compiler, 0, aggregates), Is.EqualTo(1));
            }
            var replacement = aggregates.Lookup(0)!.Replacements[0];
            replacement.LclNum = 2;
            compiler.lvaTable[2].Type = type;
            var fieldUse = AddPromotionStatement(compiler, blocks[0],
                compiler.gtNewLclFldNode(type, 0, checked((ushort)offset)));
            var visitor = PreparePromotion(compiler, aggregates);
            _ = visitor.StartBlock(blocks[0]);
            Assert.That(blocks[0].FirstStmt, Is.SameAs(fieldUse));
            Assert.That(replacement.NeedsReadBack, Is.True);

            RewritePromotionStatements(visitor, fieldUse);
            visitor.EndBlock();

            Assert.That(CountReadBacks(blocks[0], replacement), Is.EqualTo(1));
            AssertReadBack(blocks[0].FirstStmt!, 0, replacement);
            Assert.That(blocks[0].FirstStmt!.RootNode.Data.Type, Is.EqualTo(type));
            Assert.That(fieldUse.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
        });
    }
#endif

    private static unsafe GenTreeCall CreatePromotionRetbufferCall(Compiler compiler)
    {
        var handle = (CORINFO_CLASS_STRUCT_*)1;
        var layout = new ClassLayout(handle, true, 8, TYP_STRUCT, "Retbuffer", "Retbuffer");
        var layouts = new ClassLayoutTable();
        _ = layouts.AddObjLayout(compiler, layout);
        typeof(Compiler).GetField("_classLayoutTable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(compiler, layouts);
#if DEBUG
        compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
        var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
        address.Flags |= GTF_VAR_DEF;
        var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
        call.RetClsHnd = handle;
        call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.RetBuffer));

        return call;
    }

    private static BasicBlock[] CreatePromotionGraph(Compiler compiler, int[][] successors, int layoutSize = 8)
    {
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[0].Layout = new ClassLayout(layoutSize);
        compiler.lvaTable[0].lvIsOSRLocal = true;
        compiler.lvaTable[2].Type = TYP_LONG;
        var blocks = new BasicBlock[successors.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            var block = blocks[index] = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbWeight = 100;
            if (index > 0)
            {
                blocks[index - 1].Next = block;
                block.Prev = blocks[index - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        for (var index = 0; index < blocks.Length; index++)
        {
            var block = blocks[index];
            var targets = successors[index];
            if (targets.Length == 1)
            {
                block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[targets[0]], block));
            }
            else if (targets.Length == 2)
            {
                block.SetCond(compiler.fgAddRefPred(blocks[targets[0]], block),
                    compiler.fgAddRefPred(blocks[targets[1]], block));
                var jump = compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID,
                    compiler.gtNewIconNode(TYP_INT, 1)));
                compiler.fgInsertStmtAtEnd(block, jump);
                compiler.fgSequenceLocals(jump);
            }
            else
            {
                Assert.That(targets, Is.Empty);
                var ret = compiler.gtNewStmt(new GenTreeUnOp(GT_RETURN, TYP_VOID, null));
                compiler.fgInsertStmtAtEnd(block, ret);
                compiler.fgSequenceLocals(ret);
            }
        }

        return blocks;
    }

    private static Statement AddPromotionStatement(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtNearEnd(block, statement);
        compiler.fgSequenceLocals(statement);

        return statement;
    }

    private static Compiler.PhysicalPromotionAggregateInfoMap CreatePromotionAggregates(Compiler compiler)
    {
        var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
        var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
        aggregate.Replacements.Add(new Compiler.PhysicalPromotionReplacement(0, TYP_LONG) { LclNum = 2 });
        aggregates.Add(aggregate);

        return aggregates;
    }

    private static Compiler.PhysicalPromotionReplaceVisitor PreparePromotion(
        Compiler compiler, Compiler.PhysicalPromotionAggregateInfoMap aggregates)
    {
        compiler._dfsTree ??= compiler.fgComputeDfs();
        var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
        liveness.Run();

        return CreateReplaceVisitor(compiler, aggregates, liveness);
    }

    private static Compiler.PhysicalPromotionReplaceVisitor CreateReplaceVisitor(
        Compiler compiler, Compiler.PhysicalPromotionAggregateInfoMap aggregates,
        Compiler.PhysicalPromotionLiveness liveness)
    {
        var dfs = compiler._dfsTree ??= compiler.fgFirstBB is null
            ? new FlowGraphDfsTree(compiler, [], 0, false, false) : compiler.fgComputeDfs();
        var visitor = new Compiler.PhysicalPromotionReplaceVisitor(compiler, aggregates, liveness, dfs);
        visitor.PrepareReadBacks();

        return visitor;
    }

    private static void RewritePromotionBlocks(Compiler compiler, Compiler.PhysicalPromotionReplaceVisitor visitor)
    {
        var dfs = compiler._dfsTree!;
        for (var index = dfs.PostOrderCount; index > 0; index--)
        {
            var first = visitor.StartBlock(dfs.GetPostOrder(index - 1));
            RewritePromotionStatements(visitor, first);
            visitor.EndBlock();
        }
    }

    private static void RewritePromotionStatements(Compiler.PhysicalPromotionReplaceVisitor visitor, Statement? first)
    {
        for (var statement = first; statement is not null; statement = statement.NextStmt)
        {
            visitor.StartStatement(statement);
            visitor.WalkTree(ref statement.RootNodeRef);
        }
    }

    private static int CountReadBacks(BasicBlock block, Compiler.PhysicalPromotionReplacement replacement)
    {
        return block.Statements.Count(statement => (statement.RootNode.Oper is GT_STORE_LCL_VAR) &&
            (statement.RootNode.AsLclVarCommon().LclNum == replacement.LclNum) &&
            (statement.RootNode.Data.Oper is GT_LCL_FLD));
    }

    private static void AssertReadBack(Statement statement, int structLcl, Compiler.PhysicalPromotionReplacement replacement)
    {
        var tree = statement.RootNode;
        Assert.That(tree.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
        Assert.That(tree.AsLclVarCommon().LclNum, Is.EqualTo(replacement.LclNum));
        Assert.That(tree.Data.Oper, Is.EqualTo(GT_LCL_FLD));
        Assert.That(tree.Data.AsLclFld().LclNum, Is.EqualTo(structLcl));
        Assert.That(tree.Data.AsLclFld().LclOffs, Is.EqualTo(replacement.Offset));
    }

    [Test]
    public static void ReplacementReadBackRemainsPendingAcrossForwardEdgesUntilUsed()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(8);
            compiler.lvaTable[2].Type = TYP_LONG;
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            first.Next = second;
            second.Prev = first;
            first.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(second, first));
            compiler.fgFirstBB = first;
            compiler.fgLastBB = second;

            var firstStatement = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 0));
            compiler.fgInsertStmtAtEnd(first, firstStatement);
            var use = compiler.gtNewStmt(compiler.gtNewLclFldNode(TYP_LONG, 0, 0));
            compiler.fgInsertStmtAtEnd(second, use);
            compiler.fgSequenceLocals(use);
            compiler._dfsTree = compiler.fgComputeDfs();

            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
            var replacement = new Compiler.PhysicalPromotionReplacement(0, TYP_LONG) { LclNum = 2 };
            aggregate.Replacements.Add(replacement);
            aggregates.Add(aggregate);
            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
            liveness.Run();
            Assert.That(liveness.IsReplacementLiveOut(first, 0, 0), Is.True);

            var visitor = CreateReplaceVisitor(compiler, aggregates, liveness);
            _ = visitor.StartBlock(first);
            visitor.ClearNeedsWriteBack(replacement);
            visitor.SetNeedsReadBack(replacement);
            visitor.EndBlock();

            Assert.That(first.LastStmt, Is.SameAs(firstStatement));
            Assert.That(replacement.NeedsReadBack, Is.False);
            Assert.That(replacement.NeedsWriteBack, Is.True);

            _ = visitor.StartBlock(second);
            Assert.That(replacement.NeedsReadBack, Is.True);
            Assert.That(replacement.NeedsWriteBack, Is.False);
            visitor.StartStatement(use);
            AssertReadBack(second.FirstStmt!, 0, replacement);
            visitor.WalkTree(ref use.RootNodeRef);
            visitor.EndBlock();
            Assert.That(use.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(2));
        });
    }

    [Test]
    public static void LivenessTracksLastReplacementAcrossBitVectorBoundary()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(256);
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var field = compiler.gtNewLclFldNode(TYP_INT, 0, 252);
            var statement = compiler.gtNewStmt(field);
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.fgSequenceLocals(statement);
            compiler._dfsTree = compiler.fgComputeDfs();

            var aggregates = new Compiler.PhysicalPromotionAggregateInfoMap(compiler.lvaCount);
            var aggregate = new Compiler.PhysicalPromotionAggregateInfo(0);
            for (var index = 0; index < Compiler.PHYSICAL_PROMOTION_MAX_PROMOTIONS_PER_STRUCT; index++)
            {
                aggregate.Replacements.Add(new Compiler.PhysicalPromotionReplacement(index * 4, TYP_INT)
                {
                    LclNum = 1
                });
            }
            aggregates.Add(aggregate);

            var liveness = new Compiler.PhysicalPromotionLiveness(compiler, aggregates);
            liveness.Run();

            Assert.That(liveness.IsReplacementLiveIn(block, 0, 63), Is.True);
            Assert.That(liveness.IsReplacementLiveIn(block, 0, 62), Is.False);
            Assert.That((field.Flags & GTF_VAR_DEATH) != 0, Is.True);
        });
    }
}
