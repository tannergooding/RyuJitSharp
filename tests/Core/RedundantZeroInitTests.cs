// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RedundantZeroInitTests
{
    [TestCase(CORINFO_HELP_INIT_PINVOKE_FRAME, false)]
    [TestCase(CORINFO_HELP_POLL_GC, true)]
    public static void PotentialGCSafePointDistinguishesNoGCHelpers(CorInfoHelpFunc helper, bool expected)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var call = compiler.gtNewHelperCallNode(TYP_VOID, helper);

            Assert.That(call.IsHelperCall(), Is.True);
            Assert.That((call.Flags & GTF_CALL) != 0, Is.True);
            Assert.That(compiler.IsPotentialGCSafePoint(call), Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_INT, false)]
    [TestCase(TYP_STRUCT, true)]
    public static void PotentialGCSafePointDistinguishesScalarAndStructLocalStores(var_types type, bool expected)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var store = new GenTreeLclVar(type, 0, compiler.gtNewIconNode(TYP_INT, 0));

            Assert.That(compiler.IsPotentialGCSafePoint(store), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void PotentialGCSafePointIncludesAggregateCallFlagsAndBlockStores()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_INDIRECT, null);
            var aggregate = new GenTreeOp(GT_ADD, TYP_INT, call, compiler.gtNewIconNode(TYP_INT, 1));
            var blockStore = new GenTreeBlk(TYP_STRUCT,
                compiler.gtNewIconNode(TYP_I_IMPL, 1),
                compiler.gtNewIconNode(TYP_INT, 0), new ClassLayout(8));
            var scalar = compiler.gtNewIconNode(TYP_INT, 1);

            Assert.That(compiler.IsPotentialGCSafePoint(call), Is.True);
            Assert.That((aggregate.Flags & GTF_CALL) != 0, Is.True);
            Assert.That(compiler.IsPotentialGCSafePoint(aggregate), Is.True);
            Assert.That(compiler.IsPotentialGCSafePoint(blockStore), Is.True);
            Assert.That(compiler.IsPotentialGCSafePoint(scalar), Is.False);
        });
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    public static void PrologZeroAllowsRemovalOnlyForAnUntrackedFirstStore(int value, bool removed)
    {
        WithGraph(1, compiler => {
            compiler.info.compInitMem = true;
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = AddBlock(compiler);
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, value));
            AddStatement(compiler, block, store);
            SetDfs(compiler);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(block.FirstStmt is null, Is.EqualTo(removed));
            Assert.That(descriptor.lvSuppressedZeroInit, Is.EqualTo(removed));
            Assert.That(descriptor.lvHasExplicitInit, Is.EqualTo(!removed));
            Assert.That((store.Flags & GTF_VAR_EXPLICIT_INIT) != 0, Is.EqualTo(!removed));
        });
    }

    [Test]
    public static void RepeatedZeroAcrossUniqueSuccessorRemovesLaterTrackedDef()
    {
        WithGraph(1, compiler => {
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.lvTracked = true;
            descriptor._varIndex = 0;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            var entry = AddBlock(compiler);
            var successor = AddBlock(compiler);
            var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            var second = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            AddStatement(compiler, entry, first);
            AddStatement(compiler, successor, second);
            successor.bbVarDef = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, successor.bbVarDef, 0);
            SetDfs(compiler);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(entry.FirstStmt, Is.Not.Null);
            Assert.That(successor.FirstStmt, Is.Null);
            Assert.That(descriptor.lvHasExplicitInit, Is.True);
            Assert.That(descriptor.lvSuppressedZeroInit, Is.True);
            Assert.That(SetOps.IsMember(compiler, successor.bbVarDef, 0), Is.False);
        });
    }

    [Test]
    public static void PriorReadPreventsEliminatingOrMarkingTheStore()
    {
        WithGraph(1, compiler => {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var block = AddBlock(compiler);
            AddStatement(compiler, block, new GenTreeLclVar(TYP_INT, 0));
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            AddStatement(compiler, block, store);
            SetDfs(compiler);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(block.LastStmt?.RootNode, Is.SameAs(store));
            Assert.That(compiler.lvaTable[0].lvSuppressedZeroInit, Is.False);
            Assert.That(compiler.lvaTable[0].lvHasExplicitInit, Is.False);
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public static void GcLocalExplicitInitRequiresNoEarlierSafePoint(
        bool precedingCall, bool interruptible, bool explicitInit)
    {
        WithGraph(1, compiler => {
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.codeGen = new CodeGen(compiler) { Interruptible = interruptible };
            var block = AddBlock(compiler);
            if (precedingCall)
            {
                var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_INDIRECT, null);
                call._controlExpr = compiler.gtNewIconNode(TYP_I_IMPL, 1);
                AddStatement(compiler, block, call);
            }
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_I_IMPL, 1));
            AddStatement(compiler, block, store);
            SetDfs(compiler);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(compiler.lvaTable[0].lvHasExplicitInit, Is.EqualTo(explicitInit));
            Assert.That((store.Flags & GTF_VAR_EXPLICIT_INIT) != 0, Is.EqualTo(explicitInit));
        });
    }

    [Test]
    public static void CycleEntryStopsUniqueSuccessorScanBeforeItsStore()
    {
        WithGraph(1, compiler => {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var entry = AddBlock(compiler);
            var cycle = AddBlock(compiler);
            cycle.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(cycle, cycle, null));
            cycle.bbPreds = new FlowEdge(cycle, cycle, entry.bbPreds);
            var initial = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            var cyclic = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            AddStatement(compiler, entry, initial);
            AddStatement(compiler, cycle, cyclic);
            SetDfs(compiler);
            Assert.That(compiler._dfsTree?.HasCycle, Is.True);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(entry.FirstStmt, Is.Null);
            Assert.That(cycle.FirstStmt?.RootNode, Is.SameAs(cyclic));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ThrowingTryBlockPreventsUnsafeExplicitInitUnlessTrackedOutsideHandler(bool tracked)
    {
        WithGraph(1, compiler => {
            var block = AddBlock(compiler);
            var handler = AddBlock(compiler);
            block.SetKindAndTargetEdge(BBJ_RETURN, null);
            block.TryIndex = 0;
            handler.HndIndex = 0;
            handler.CatchType = (bbCatchType)1;
            compiler.compHndBBtab = [new EHblkDsc {
                ebdHandlerType = EH_HANDLER_CATCH,
                ebdTryBeg = block,
                ebdTryLast = block,
                ebdHndBeg = handler,
                ebdHndLast = handler,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            var throwing = compiler.gtNewIconNode(TYP_INT, 4);
            throwing.Flags |= GTF_EXCEPT;
            AddStatement(compiler, block, throwing);
            var descriptor = compiler.lvaTable[0];
            descriptor.lvTracked = tracked;
            compiler.lvaTable[0] = descriptor;
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 5));
            AddStatement(compiler, block, store);
            SetDfs(compiler);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(compiler.lvaTable[0].lvHasExplicitInit, Is.EqualTo(tracked));
            Assert.That((store.Flags & GTF_VAR_EXPLICIT_INIT) != 0, Is.EqualTo(tracked));
        });
    }

    [Test]
    public static void EarlierPromotedParentReferencePreventsFirstFieldInit()
    {
        WithGraph(2, compiler => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvPromoted = true;
            parent.lvFieldCnt = 1;
            parent.lvFieldLclStart = 1;
            ref var field = ref compiler.lvaTable[1];
            field.lvIsStructField = true;
            field.lvParentLcl = 0;

            var block = AddBlock(compiler);
            AddStatement(compiler, block, new GenTreeLclVar(TYP_STRUCT, 0));
            var store = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 0));
            AddStatement(compiler, block, store);
            SetDfs(compiler);

            compiler.optRemoveRedundantZeroInits();

            Assert.That(block.LastStmt?.RootNode, Is.SameAs(store));
            Assert.That(field.lvHasExplicitInit, Is.False);
            Assert.That(field.lvSuppressedZeroInit, Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void VerboseScanReportsBlockAndExplicitInitialization()
    {
        WithGraph(1, compiler => {
            var block = AddBlock(compiler);
            AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7)));
            SetDfs(compiler);
            compiler.verbose = true;

            var output = CodeGenLifeTransitionTests.Capture(compiler.optRemoveRedundantZeroInits);

            Assert.That(output, Does.Contain("*************** In optRemoveRedundantZeroInits()"));
            Assert.That(output, Does.Contain($"Analyzing {FMT_BB(block.bbNum)}"));
            Assert.That(output, Does.Contain("Marking V00 as having an explicit init"));
        });
    }
#endif

    private static void WithGraph(int count, Action<Compiler> action)
    {
        SsaLivenessTests.WithCompiler(count, compiler => {
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
#if TARGET_ARM64
            compiler.lvaFfrRegister = BAD_VAR_NUM;
#endif
            action(compiler);
        });
    }

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
            block.Prev = previous;
            previous.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(previous, block, null));
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;

        return block;
    }

    private static void AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var stmt = new Statement(root, 0);
        compiler.fgInsertStmtAtEnd(block, stmt);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
    }

    private static void SetDfs(Compiler compiler)
    {
        compiler._dfsTree = compiler.fgComputeDfs();
    }
}
