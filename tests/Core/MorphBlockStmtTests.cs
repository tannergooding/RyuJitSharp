// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class MorphBlockStmtTests
{
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void InertStatementRemovalRespectsDebuggableCode(bool debugCode, bool removed)
    {
        WithCompiler(compiler =>
        {
            compiler.opts.compDbgCode = debugCode;
            var block = new BasicBlock(null, null);
            var statement = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 42));
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.fgMorphBlockStmt(block, statement), Is.EqualTo(removed));
            Assert.That(block.FirstStmt is null, Is.EqualTo(removed));
            if (!removed)
            {
                Assert.That(block.FirstStmt, Is.SameAs(statement));
                Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_CNS_INT));
            }
            Assert.That(compiler.compCurBB, Is.SameAs(block));
            Assert.That(compiler.compCurStmt, Is.SameAs(statement));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MorphedStoreKeepsStatementAndResetsAmbientRemovalState(bool allTrees)
    {
        WithCompiler(compiler =>
        {
            var block = new BasicBlock(null, null);
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            var statement = compiler.gtNewStmt(store);
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.fgRemoveRestOfBlock = true;
            compiler.fgNodeThreading = allTrees ? NodeThreading.AllTrees : NodeThreading.None;

            Assert.That(compiler.fgMorphBlockStmt(block, statement, allowFGChange: false), Is.False);
            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(compiler.fgRemoveRestOfBlock, Is.False);
            if (allTrees)
            {
                Assert.That(statement.TreeListBegin, Is.Not.Null);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ThrowRemovesRemainingStatementsAndOnlyChangesGraphWhenAllowed(
        bool allowFGChange, bool internalFirst)
    {
        WithCompiler(compiler =>
        {
            var block = new BasicBlock(null, null);
            var originalKind = block.Kind;
            compiler.fgFirstBB = block;
            compiler.fgPredsComputed = true;
            if (internalFirst)
            {
                block.SetFlags(BBF_INTERNAL);
            }
            var call = compiler.gtNewHelperCallNode(TYP_VOID, CorInfoHelpFunc.CORINFO_HELP_THROW);
            call.IsNoReturn = true;
            call.Flags |= GTF_EXCEPT;
            var throwing = compiler.gtNewStmt(call);
            var firstRemoved = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
            var secondRemoved = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 2));
            compiler.fgInsertStmtAtEnd(block, throwing);
            compiler.fgInsertStmtAtEnd(block, firstRemoved);
            compiler.fgInsertStmtAtEnd(block, secondRemoved);

            Assert.That(compiler.fgMorphBlockStmt(block, throwing, allowFGChange), Is.False);
            Assert.That(block.FirstStmt, Is.SameAs(throwing));
            Assert.That(block.LastStmt, Is.SameAs(throwing));
            Assert.That(throwing.RootNode, Is.SameAs(call));
            Assert.That(compiler.fgRemoveRestOfBlock, Is.False);
            Assert.That(block.Kind, Is.EqualTo(allowFGChange && !internalFirst ? BBJ_THROW : originalKind));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void LastConditionalFoldsOnlyWhenFlowGraphChangesAllowed(bool allowFGChange, bool removed)
    {
        WithCompiler(compiler =>
        {
            var block = new BasicBlock(null, null);
            var trueBlock = new BasicBlock(null, null);
            var falseBlock = new BasicBlock(null, null);
            compiler.fgFirstBB = block;
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var trueEdge = compiler.fgAddRefPred(trueBlock, block);
            var falseEdge = compiler.fgAddRefPred(falseBlock, block);
            block.SetCond(trueEdge, falseEdge);
            var branch = compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, compiler.gtNewIconNode(TYP_INT, 1)));
            compiler.fgInsertStmtAtEnd(block, branch);

            Assert.That(compiler.fgMorphBlockStmt(block, branch, allowFGChange), Is.EqualTo(removed));
            Assert.That(block.Kind, Is.EqualTo(removed ? BBJ_ALWAYS : BBJ_COND));
            Assert.That(block.FirstStmt is null, Is.EqualTo(removed));
            if (removed)
            {
                Assert.That(block.Target, Is.SameAs(trueBlock));
            }
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(-1, false)]
    [TestCase(0, true)]
    [TestCase(2, true)]
    public static void ConstantSwitchDebitsEachUniqueProfileEdgeOnce(int switchValue, bool targetHasSuccessor)
    {
        WithCompiler(compiler =>
        {
            var block = new BasicBlock(null, null);
            var first = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            var second = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            compiler.fgFirstBB = block;
            compiler.fgPredsComputed = true;
            compiler.fgPgoConsistent = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var firstEdge = compiler.fgAddRefPred(first, block);
            var duplicateEdge = compiler.fgAddRefPred(first, block);
            var secondEdge = compiler.fgAddRefPred(second, block);
            var defaultEdge = compiler.fgAddRefPred(first, block);
            firstEdge.Likelihood = 0.75;
            secondEdge.Likelihood = 0.25;
            var targets = new BBswtDesc([firstEdge, secondEdge], new int[4], hasDefault: true);
            targets.Cases[0] = firstEdge;
            targets.Cases[1] = duplicateEdge;
            targets.Cases[2] = secondEdge;
            targets.Cases[3] = defaultEdge;
            block.SwitchTargets = targets;
            block.setBBProfileWeight(100);
            first.setBBProfileWeight(200);
            second.setBBProfileWeight(100);
            if (targetHasSuccessor)
            {
                var successor = new BasicBlock(null, null) { Kind = BBJ_RETURN };
                second.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(successor, second));
            }

            var statement = compiler.gtNewStmt(new GenTreeUnOp(GT_SWITCH, TYP_VOID,
                compiler.gtNewIconNode(TYP_INT, switchValue)));
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.fgMorphBlockStmt(block, statement), Is.True);

            var selectsSecond = switchValue == 2;
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(block.Target, Is.SameAs(selectsSecond ? second : first));
            Assert.That(first.bbWeight, Is.EqualTo(selectsSecond ? 125 : 225));
            Assert.That(second.bbWeight, Is.EqualTo(selectsSecond ? 175 : 75));
            Assert.That(firstEdge.DupCount, Is.EqualTo(selectsSecond ? 0 : 1));
            Assert.That(secondEdge.DupCount, Is.EqualTo(selectsSecond ? 1 : 0));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!targetHasSuccessor));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
#if DEBUG
        compiler.info.compFullName = nameof(MorphBlockStmtTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
