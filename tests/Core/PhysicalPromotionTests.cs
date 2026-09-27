// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System.IO;
#endif
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

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class PhysicalPromotionTests
{
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
            var replacer = new Compiler.PhysicalPromotionReplaceVisitor(compiler, aggregateMap, liveness);
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

            var replacer = new Compiler.PhysicalPromotionReplaceVisitor(compiler, aggregates, liveness);
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
    public static void ReplacementReadBackIsInsertedAtLiveBlockExit()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(8);
            compiler.lvaTable[2].Type = TYP_LONG;
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            first.Next = second;
            second.Prev = first;
            first.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(first, second, null));
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

            var visitor = new Compiler.PhysicalPromotionReplaceVisitor(compiler, aggregates, liveness);
            _ = visitor.StartBlock(first);
            visitor.ClearNeedsWriteBack(replacement);
            visitor.SetNeedsReadBack(replacement);
            visitor.EndBlock();

            Assert.That(first.LastStmt, Is.Not.SameAs(firstStatement));
            Assert.That(first.LastStmt!.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(first.LastStmt.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(2));
            Assert.That(replacement.NeedsReadBack, Is.False);
            Assert.That(replacement.NeedsWriteBack, Is.True);
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
