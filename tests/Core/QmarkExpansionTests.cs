// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class QmarkExpansionTests
{
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public static void ExpansionPreservesNativeBranchShapeAndLikelihoods(int shape, bool internalBlock)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            block.SetFlags(BBF_GC_SAFE_POINT | BBF_NEEDS_GCPOLL | BBF_HAS_NEWOBJ);
            block.SetFlags(internalBlock ? BBF_INTERNAL : BBF_IMPORTED);
            block.setBBProfileWeight(100);
            var before = compiler.gtNewStmt(compiler.gtNewNothingNode());
            var after = compiler.gtNewStmt(compiler.gtNewNothingNode());
            var thenTree = shape == 2 ? compiler.gtNewNothingNode() : Store(compiler, 10);
            var elseTree = shape == 1 ? compiler.gtNewNothingNode() : Store(compiler, 20);
            var qmark = Qmark(compiler, TYP_VOID, thenTree, elseTree);
            qmark.ThenNodeLikelihood = 80;
            var originalCondition = qmark.Cond;
            var stmt = compiler.gtNewStmt(qmark);
            compiler.fgInsertStmtAtEnd(block, before);
            compiler.fgInsertStmtAtEnd(block, stmt);
            compiler.fgInsertStmtAtEnd(block, after);

            Assert.That(compiler.fgExpandQmarkStmt(block, stmt, false), Is.False);
            var blocks = compiler.Blocks.ToArray();
            Assert.That(blocks.Length, Is.EqualTo(shape == 0 ? 5 : 4));
            var cond = blocks[1];
            var remainder = blocks[^1];
            Assert.That(block.FirstStmt, Is.SameAs(before));
            Assert.That(before.NextStmt, Is.Null);
            Assert.That(remainder.FirstStmt, Is.SameAs(after));
            Assert.That(remainder.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(remainder.bbWeight, Is.EqualTo(100));
            Assert.That(remainder.HasFlag(BBF_GC_SAFE_POINT), Is.True);
            Assert.That(block.HasFlag(BBF_NEEDS_GCPOLL), Is.False);
            Assert.That(block.Target, Is.SameAs(cond));
            Assert.That(cond.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(cond.bbWeight, Is.EqualTo(100));
            var branch = (cond.FirstStmt ?? throw new InvalidOperationException()).RootNode;
            Assert.That(branch.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(branch.AsUnOp().Op1, Is.SameAs(qmark.Cond));
            Assert.That(qmark.Cond.Oper, Is.EqualTo(shape == 2 ? GT_LCL_VAR : GT_EQ));
            if (shape != 2)
            {
                Assert.That(qmark.Cond.AsOp().Op1, Is.SameAs(originalCondition));
            }

            foreach (var generated in blocks.Skip(1))
            {
                Assert.That(generated.HasFlag(BBF_HAS_NEWOBJ), Is.True);
                Assert.That(generated.HasFlag(BBF_INTERNAL), Is.EqualTo(internalBlock));
                Assert.That(generated.HasFlag(BBF_IMPORTED), Is.EqualTo(!internalBlock));
            }

            if (shape == 0)
            {
                Assert.That(cond.TrueTarget, Is.SameAs(blocks[3]));
                Assert.That(cond.FalseTarget, Is.SameAs(blocks[2]));
                Assert.That(cond.TrueEdge.Likelihood, Is.EqualTo(0.2));
                Assert.That(cond.FalseEdge.Likelihood, Is.EqualTo(0.8));
                Assert.That(blocks[2].bbWeight, Is.EqualTo(80));
                Assert.That(blocks[3].bbWeight, Is.EqualTo(20));
                Assert.That((blocks[2].FirstStmt ?? throw new InvalidOperationException()).RootNode, Is.SameAs(thenTree));
                Assert.That((blocks[3].FirstStmt ?? throw new InvalidOperationException()).RootNode, Is.SameAs(elseTree));
            }
            else
            {
                Assert.That(cond.TrueTarget, Is.SameAs(remainder));
                Assert.That(cond.FalseTarget, Is.SameAs(blocks[2]));
                Assert.That(cond.TrueEdge.Likelihood, Is.EqualTo(0.8));
                Assert.That(cond.FalseEdge.Likelihood, Is.EqualTo(0.2));
                Assert.That(blocks[2].bbWeight, Is.EqualTo(shape == 1 ? 80 : 20));
                Assert.That((blocks[2].FirstStmt ?? throw new InvalidOperationException()).RootNode,
                    Is.SameAs(shape == 1 ? thenTree : elseTree));
            }

            Assert.That(remainder.bbRefs, Is.EqualTo(2));
            foreach (var arm in blocks.Skip(2).SkipLast(1))
            {
                Assert.That(arm.Target, Is.SameAs(remainder));
                Assert.That(arm.TargetEdge.Likelihood, Is.EqualTo(1));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StoredArmsSplitCommasBeforeWritingBack(bool field)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var sideEffect = Store(compiler, 3);
            var thenValue = compiler.gtNewIconNode(TYP_INT, 10);
            var elseValue = compiler.gtNewIconNode(TYP_INT, 20);
            var qmark = Qmark(compiler, TYP_INT, compiler.gtNewCommaNode(TYP_INT, sideEffect, thenValue), elseValue);
            GenTree store = field
                ? compiler.gtNewStoreLclFldNode(TYP_INT, 2, 4, qmark)
                : compiler.gtNewStoreLclVarNode(1, qmark);
            var stmt = compiler.gtNewStmt(store);
            compiler.fgInsertStmtAtEnd(block, stmt);

            Assert.That(compiler.fgGetTopLevelQmark(store, out var dst), Is.SameAs(qmark));
            Assert.That(dst, Is.SameAs(store));
            Assert.That(compiler.fgExpandQmarkNodes(false), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compQmarkRationalized, Is.True);
            var blocks = compiler.Blocks.ToArray();
            var thenStmt = blocks[2].FirstStmt ?? throw new InvalidOperationException();
            Assert.That(thenStmt.RootNode, Is.SameAs(sideEffect));
            var thenStore = (thenStmt.NextStmt ?? throw new InvalidOperationException()).RootNode;
            var elseStore = (blocks[3].FirstStmt ?? throw new InvalidOperationException()).RootNode;

            foreach (var armStore in new[] { thenStore, elseStore })
            {
                Assert.That(armStore.Oper, Is.EqualTo(field ? GT_STORE_LCL_FLD : GT_STORE_LCL_VAR));
                Assert.That(armStore.AsLclVarCommon().LclNum, Is.EqualTo(field ? 2 : 1));
                Assert.That(armStore.Type, Is.EqualTo(TYP_INT));
                if (field)
                {
                    Assert.That(armStore.AsLclFld().LclOffs, Is.EqualTo(4));
                }
            }

            Assert.That(thenStore.AsLclVarCommon().Data, Is.SameAs(thenValue));
            Assert.That(elseStore.AsLclVarCommon().Data, Is.SameAs(elseValue));
        });
    }

    [Test]
    public static void EarlyExpansionLeavesUnmarkedNestedQmarksForLateExpansion()
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var nested = Qmark(compiler, TYP_VOID, Store(compiler, 1), Store(compiler, 2));
            var outer = Qmark(compiler, TYP_VOID, nested, Store(compiler, 3));
            outer.IsEarlyExpandableQmark = true;
            var later = Qmark(compiler, TYP_VOID, Store(compiler, 4), Store(compiler, 5));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(outer));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(later));

            Assert.That(compiler.fgExpandQmarkNodes(true), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
            compiler.optMethodFlags |= OMF_HAS_EARLY_QMARKS;
            Assert.That(compiler.fgExpandQmarkNodes(true), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(5));
            Assert.That(compiler.compQmarkRationalized, Is.False);
            Assert.That(compiler.fgExpandQmarkNodes(false), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(13));
            Assert.That(compiler.compQmarkRationalized, Is.True);
            Assert.That(compiler.Blocks.SelectMany(b => b.Statements).Any(s => compiler.gtTreeContainsOper(s.RootNode, GT_QMARK)), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NoReturnArmsBecomeThrowBlocks(bool trueArm)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var call = compiler.gtNewHelperCallNode(TYP_VOID, CorInfoHelpFunc.CORINFO_HELP_THROW);
            call.IsNoReturn = true;
            var other = Store(compiler, 1);
            var qmark = Qmark(compiler, TYP_VOID, trueArm ? call : other, trueArm ? other : call);
            var stmt = compiler.gtNewStmt(qmark);
            compiler.fgInsertStmtAtEnd(block, stmt);

            Assert.That(compiler.fgExpandQmarkStmt(block, stmt, false), Is.True);
            var blocks = compiler.Blocks.ToArray();
            var throwing = blocks[trueArm ? 2 : 3];
            Assert.That(throwing.Kind, Is.EqualTo(BBJ_THROW));
            Assert.That(throwing.NumSucc, Is.Zero);
            Assert.That(throwing.isRunRarely, Is.True);
            Assert.That((throwing.FirstStmt ?? throw new InvalidOperationException()).RootNode, Is.SameAs(call));
            Assert.That(blocks[^1].bbRefs, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ThrowConversionSubtractsSuccessorProfileFlow(bool successorHasOutgoingFlow)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var successor = compiler.fgNewBBafter(BBJ_RETURN, block, true);
            var exit = compiler.fgNewBBafter(BBJ_RETURN, successor, true);
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(successor, block));
            if (successorHasOutgoingFlow)
            {
                successor.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(exit, successor));
            }

            block.setBBProfileWeight(30);
            successor.setBBProfileWeight(100);
            compiler.fgPgoConsistent = true;
            compiler.fgConvertBBToThrowBB(block);

            Assert.That(block.Kind, Is.EqualTo(BBJ_THROW));
            Assert.That(block.bbWeight, Is.EqualTo(30));
            Assert.That(successor.bbWeight, Is.EqualTo(70));
            Assert.That(successor.bbRefs, Is.Zero);
            Assert.That(successor.bbPreds, Is.Null);
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!successorHasOutgoingFlow));
        });
    }

    [Test]
    public static void ExpansionExtendsTheOriginalExceptionRegion()
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var handler = compiler.fgNewBBafter(BBJ_THROW, block, true);
            block.TryIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = block, ebdTryLast = block, ebdHndBeg = handler, ebdHndLast = handler }
            ];
            compiler.compHndBBtabCount = 1;
            var qmark = Qmark(compiler, TYP_VOID, Store(compiler, 1), Store(compiler, 2));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(qmark));

            Assert.That(compiler.fgExpandQmarkNodes(false), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var blocks = compiler.Blocks.ToArray();
            Assert.That(blocks.Length, Is.EqualTo(6));
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(blocks[4]));
            Assert.That(compiler.compHndBBtab[0].ebdHndBeg, Is.SameAs(handler));
            Assert.That(blocks.Take(5).All(b => b.TryIndex == 0), Is.True);
        });
    }

    [Test]
    public static void ThrowConversionUnpairsCallFinallyWithoutRemovingItsTail()
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var tail = compiler.fgNewBBafter(BBJ_CALLFINALLYRET, block, true);
            var continuation = compiler.fgNewBBafter(BBJ_RETURN, tail, true);
            var handler = compiler.fgNewBBafter(BBJ_EHFINALLYRET, continuation, true);
            block.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(handler, block));
            tail.TargetEdge = compiler.fgAddRefPred(continuation, tail);
            tail.SetFlags(BBF_DONT_REMOVE);
            var returnEdge = compiler.fgAddRefPred(tail, handler);
            returnEdge.Likelihood = 1;
            handler.SetEhf(new BBJumpTable([returnEdge]));

            compiler.fgConvertBBToThrowBB(block);
            Assert.That(block.Kind, Is.EqualTo(BBJ_THROW));
            Assert.That(block.HasFlag(BBF_RETLESS_CALL), Is.False);
            Assert.That(block.Next, Is.SameAs(tail));
            Assert.That(tail.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(tail.Target, Is.SameAs(continuation));
            Assert.That(tail.HasFlag(BBF_DONT_REMOVE), Is.False);
            Assert.That(tail.bbRefs, Is.Zero);
            Assert.That(handler.bbRefs, Is.Zero);
            Assert.That(handler.NumSucc, Is.Zero);
            Assert.That(compiler.fgBBcount, Is.EqualTo(4));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NoQmarksLeaveThePhaseUnchanged(bool early)
    {
        WithCompiler(compiler => {
            compiler.optMethodFlags |= OMF_HAS_EARLY_QMARKS;
            Assert.That(compiler.fgExpandQmarkNodes(early), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.compQmarkRationalized, Is.False);
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
        });
    }

    private static GenTreeLclVar Store(Compiler compiler, int value)
        => compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, value));

    private static GenTreeQmark Qmark(Compiler compiler, var_types type, GenTree thenTree, GenTree elseTree)
        => compiler.gtNewQmarkNode(type, compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewColonNode(type, thenTree, elseTree));

    private static void WithCompiler(Action<Compiler> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_LONG }
        ];
        compiler.lvaCount = 3;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 0;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
