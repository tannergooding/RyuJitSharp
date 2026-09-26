// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionVNStatementTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void FoldingReplacesTheOwningUseWithoutRetaggingTheOriginal(bool nested)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constantVN = store.VNForIntCon(23);
            local._vnPair.SetBoth(constantVN);
            var parent = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, local,
                compiler.gtNewIconNode(TYP_INT, 1));
            var statement = compiler.gtNewStmt(nested ? parent : local);

            Assert.That(compiler.optVNBasedFoldCurStmt(block, statement, nested ? parent : null, local),
                Is.EqualTo(Compiler.WALK_CONTINUE));
            var replacement = nested ? parent.AsOp().Op1 : statement.RootNode;
            Assert.That(replacement.AsIntCon().IconValue, Is.EqualTo((nint)23));
            Assert.That(replacement._vnPair.Conservative, Is.EqualTo(constantVN));
            Assert.That(local.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(replacement, Is.Not.SameAs(local));
        });
    }

    [TestCase(GT_ADD)]
    [TestCase(GT_MUL)]
    [TestCase(GT_LCL_VAR)]
    [TestCase(GT_CALL)]
    public static void FoldingSkipsDontCseNodes(genTreeOps oper)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            GenTree tree = oper switch
            {
                GT_ADD or GT_MUL => compiler.gtNewBinaryNode(oper, TYP_INT,
                    compiler.gtNewIconNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 3)),
                GT_LCL_VAR => compiler.gtNewLclvNode(TYP_INT, 0),
                _ => compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null),
            };
            tree._vnPair.SetBoth(store.VNForIntCon(5));
            tree.Flags |= GTF_DONT_CSE;
            var statement = compiler.gtNewStmt(tree);

            Assert.That(compiler.optVNBasedFoldCurStmt(block, statement, null, tree),
                Is.EqualTo(Compiler.WALK_CONTINUE));
            Assert.That(statement.RootNode, Is.SameAs(tree));
        });
    }

    [Test]
    public static void FoldingSkipsCseLocalsAndWideMultiply()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.lvaTable[0].lvIsCSE = true;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._vnPair.SetBoth(store.VNForIntCon(8));
            var statement = compiler.gtNewStmt(local);
            Assert.That(compiler.optVNBasedFoldCurStmt(block, statement, null, local),
                Is.EqualTo(Compiler.WALK_CONTINUE));
            Assert.That(statement.RootNode, Is.SameAs(local));

            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_LONG,
                compiler.gtNewIconNode(TYP_LONG, 2), compiler.gtNewIconNode(TYP_LONG, 3));
            multiply.Flags |= GTF_MUL_64RSLT;
            multiply._vnPair.SetBoth(store.VNForLongCon(6));
            statement = compiler.gtNewStmt(multiply);
            Assert.That(compiler.optVNBasedFoldCurStmt(block, statement, null, multiply),
                Is.EqualTo(Compiler.WALK_CONTINUE));
            Assert.That(statement.RootNode, Is.SameAs(multiply));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonNullPropagationClearsOnlyProvenIndirectionFaults(bool knownNonNull)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null) { bbNum = 1 };
            compiler.compCurBB = block;
            var address = compiler.gtNewLclvNode(TYP_REF, 0);
            address._vnPair.SetBoth(knownNonNull
                ? store.VNForHandle(0x1234, GTF_ICON_OBJ_HDL) : store.VNForExpr(null, TYP_REF));
            var load = compiler.gtNewIndir(TYP_INT, address);
            var statement = compiler.gtNewStmt(load);

            compiler.optVnNonNullPropCurStmt(block, statement, load);

            Assert.That((load.Flags & GTF_EXCEPT) != 0, Is.EqualTo(!knownNonNull));
            Assert.That((load.Flags & GTF_IND_NONFAULTING) != 0, Is.EqualTo(knownNonNull));
            Assert.That(statement.RootNode, Is.SameAs(load));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonNullPropagationClearsOnlyProvenCallNullChecks(bool knownNonNull)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null) { bbNum = 1 };
            compiler.compCurBB = block;
            var receiver = compiler.gtNewLclvNode(TYP_REF, 0);
            receiver._vnPair.SetBoth(knownNonNull
                ? store.VNForHandle(0x1234, GTF_ICON_OBJ_HDL) : store.VNForExpr(null, TYP_REF));
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_NULLCHECK;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(receiver).WithWellKnownArg(WellKnownArg.ThisPointer));
            var statement = compiler.gtNewStmt(call);

            compiler.optVnNonNullPropCurStmt(block, statement, call);

            Assert.That((call.Flags & (GTF_CALL_NULLCHECK | GTF_EXCEPT)) == 0, Is.EqualTo(knownNonNull));
            Assert.That((call.Flags & GTF_SIDE_EFFECT) != 0, Is.True);
            Assert.That(statement.RootNode, Is.SameAs(call));
        });
    }

    [Test]
    public static void FaultHandlerSkipsTheWholeStatement()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null) { CatchType = BBCT_FAULT };
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._vnPair.SetBoth(store.VNForIntCon(17));
            var statement = compiler.gtNewStmt(local);
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNAssertionPropCurStmt(block, statement), Is.SameAs(statement));
            Assert.That(statement.RootNode, Is.SameAs(local));
            Assert.That(compiler.compCurStmt, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovedStatementReturnsTheNextSurvivingCursor(bool first)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var preceding = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewIconNode(TYP_INT, 3)));
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._vnPair.SetBoth(store.VNForIntCon(7));
            var current = compiler.gtNewStmt(local);
            var following = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewIconNode(TYP_INT, 9)));
            if (!first)
            {
                compiler.fgInsertStmtAtEnd(block, preceding);
            }
            compiler.fgInsertStmtAtEnd(block, current);
            compiler.fgInsertStmtAtEnd(block, following);

            Assert.That(compiler.optVNAssertionPropCurStmt(block, current), Is.SameAs(following));
            Assert.That(block.FirstStmt, Is.SameAs(first ? following : preceding));
            Assert.That(current.RootNode.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(compiler.compCurStmt, Is.SameAs(current));
        });
    }

    [Test]
    public static void ThrowingMorphRemovesFollowingStatementsAndReturnsTheSurvivingCursor()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;
            compiler.fgPredsComputed = true;
            var call = compiler.gtNewHelperCallNode(TYP_VOID, CorInfoHelpFunc.CORINFO_HELP_THROW);
            call.IsNoReturn = true;
            call.Flags |= GTF_EXCEPT;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._vnPair.SetBoth(store.VNForIntCon(19));
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, call, local);
            var current = compiler.gtNewStmt(comma);
            var following = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewIconNode(TYP_INT, 9)));
            compiler.fgInsertStmtAtEnd(block, current);
            compiler.fgInsertStmtAtEnd(block, following);

            Assert.That(compiler.optVNAssertionPropCurStmt(block, current), Is.SameAs(current));
            Assert.That(block.LastStmt, Is.SameAs(current));
            Assert.That(current.RootNode, Is.SameAs(call));
            Assert.That(block.Kind, Is.EqualTo(BBKinds.BBJ_THROW));
            Assert.That(local.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [Test]
    public static void UnchangedStatementDoesNotMorphOrAdvance()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var unknown = compiler.gtNewLclvNode(TYP_INT, 0);
            unknown._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1, unknown));
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNAssertionPropCurStmt(block, statement), Is.SameAs(statement));
            Assert.That(statement.RootNode.AsUnOp().Op1, Is.SameAs(unknown));
            Assert.That(compiler.compCurStmt, Is.Null);
        });
    }

    [Test]
    public static void VisitorFoldsChildrenBeforeMorphingTheOwningStatement()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var second = compiler.gtNewLclvNode(TYP_INT, 1);
            first._vnPair.SetBoth(store.VNForIntCon(5));
            second._vnPair.SetBoth(store.VNForIntCon(7));
            var addition = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first, second);
            addition._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var statement = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, addition));
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNAssertionPropCurStmt(block, statement), Is.SameAs(statement));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(statement.RootNode.AsUnOp().Op1.AsIntCon().IconValue, Is.EqualTo((nint)12));
            Assert.That(first.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(second.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 2;
#if DEBUG
        compiler.info.compFullName = nameof(AssertionVNStatementTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
