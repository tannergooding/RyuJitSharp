// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class PostInlineNoReturnTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void RootCallTrimsStatementsAndUpdatesSuccessorProfiles(bool profile)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var successor = BasicBlock.New(compiler, BBJ_RETURN);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            block.Next = successor;
            successor.Next = exit;
            compiler.fgLastBB = exit;
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(successor, block));
            successor.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(exit, successor));
            if (profile)
            {
                block.setBBProfileWeight(40);
                successor.setBBProfileWeight(100);
            }

            var before = Append(compiler, block, compiler.gtNewNothingNode());
            var call = Call(compiler, true);
            var trimming = Append(compiler, block, call);
            _ = Append(compiler, block, compiler.gtNewIconNode(TYP_INT, 1));
            _ = Append(compiler, block, compiler.gtNewIconNode(TYP_INT, 2));
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.Kind, Is.EqualTo(BBJ_THROW));
            Assert.That(block.FirstStmt, Is.SameAs(before));
            Assert.That(before.NextStmt, Is.SameAs(trimming));
            Assert.That(trimming.NextStmt, Is.Null);
            Assert.That(trimming.RootNode, Is.SameAs(call));
            Assert.That(successor.bbPreds, Is.Null);
            Assert.That(successor.bbWeight, Is.EqualTo(profile ? 60 : 100));
            Assert.That(block.bbWeight, Is.EqualTo(profile ? 40 : 0));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!profile));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NestedCallsFollowExecutionOrderAndPreserveEarlierEffects(bool reverse)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var before = Call(compiler, false);
            var left = Call(compiler, true);
            var right = Call(compiler, true);
            var nested = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            nested.IsReverseOp = reverse;
            var root = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, before, nested);
            var trimming = Append(compiler, block, root);
            _ = Append(compiler, block, Call(compiler, false));

            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Statement[] statements = [.. block.Statements];
            Assert.That(statements, Has.Length.EqualTo(2));
            Assert.That(statements[0].RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(statements[0].RootNode.AsLclVarCommon().Data, Is.SameAs(before));
            Assert.That(statements[1], Is.SameAs(trimming));
            Assert.That(trimming.RootNode, Is.SameAs(reverse ? right : left));
            Assert.That(trimming.RootNode.Flags & GenTreeFlags.GTF_CALL, Is.Not.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PreorderSelectsOuterNoReturnCallBeforeItsArguments(bool outerNoReturn)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var inner = Call(compiler, true);
            var outer = Call(compiler, outerNoReturn);
            _ = outer.Args.PushBack(NewCallArg.CreateForPrimitive(inner));
            var trimming = Append(compiler, block, outer);
            _ = Append(compiler, block, compiler.gtNewNothingNode());
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(trimming.RootNode, Is.SameAs(outerNoReturn ? outer : inner));
            Assert.That(block.FirstStmt, Is.SameAs(trimming));
            Assert.That(block.LastStmt, Is.SameAs(trimming));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void CallsUnderQmarksAreIgnoredWhileLaterStatementsRemainEligible(bool laterNoReturn, bool inCondition)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var noReturnCall = Call(compiler, true);
            var qmark = inCondition
                ? compiler.gtNewQmarkNode(TYP_INT, noReturnCall,
                    compiler.gtNewColonNode(TYP_INT, compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 0)))
                : Qmark(compiler, noReturnCall);
            var first = Append(compiler, block, qmark);
            var second = Append(compiler, block, Call(compiler, laterNoReturn));
            var after = Append(compiler, block, compiler.gtNewNothingNode());
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(laterNoReturn
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(first));
            Assert.That(first.RootNode, Is.SameAs(qmark));
            Assert.That(block.LastStmt, Is.SameAs(laterNoReturn ? second : after));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void QmarkAnywhereInNestedTrimStatementVetoesTheWholeBlock(bool qmarkFirst)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var call = Call(compiler, true);
            var qmark = Qmark(compiler, compiler.gtNewIconNode(TYP_INT, 1));
            var root = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                qmarkFirst ? qmark : call, qmarkFirst ? call : qmark);
            var first = Append(compiler, block, root);
            var later = Append(compiler, block, Call(compiler, true));
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(first.RootNode, Is.SameAs(root));
            Assert.That(block.LastStmt, Is.SameAs(later));
        });
    }

    [Test]
    public static void RootNoReturnCallWithQmarkArgumentDoesNotNeedSplitting()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var call = Call(compiler, true);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(Qmark(compiler, compiler.gtNewIconNode(TYP_INT, 1))));
            var first = Append(compiler, block, call);
            _ = Append(compiler, block, compiler.gtNewNothingNode());
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(first));
            Assert.That(block.LastStmt, Is.SameAs(first));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MethodPredicateGatesCleanup(bool registered)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var call = Call(compiler, false);
            call.IsNoReturn = true;
            if (registered)
            {
                compiler.setMethodHasNoReturnCalls();
            }
            _ = Append(compiler, block, call);
            Assert.That(compiler.doesMethodHaveNoReturnCalls(), Is.EqualTo(registered));
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(registered
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase(BBJ_THROW)]
    [TestCase(BBJ_RETURN)]
    public static void ExistingThrowAndStatementsWithoutNoReturnCallsAreUnchanged(BBKinds kind)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            block.SetKindAndTargetEdge(kind, null);
            var first = Append(compiler, block, Call(compiler, kind == BBJ_THROW));
            var last = Append(compiler, block, compiler.gtNewNothingNode());
            compiler.setMethodHasNoReturnCalls();
            Assert.That(compiler.fgPostInlineNoReturnCleanup(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(first));
            Assert.That(block.LastStmt, Is.SameAs(last));
            Assert.That(block.Kind, Is.EqualTo(kind));
        });
    }

    private static GenTreeQmark Qmark(Compiler compiler, GenTree thenTree)
        => compiler.gtNewQmarkNode(TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0),
            compiler.gtNewColonNode(TYP_INT, thenTree, compiler.gtNewIconNode(TYP_INT, 0)));

    private static GenTreeCall Call(Compiler compiler, bool noReturn)
    {
        var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
        if (noReturn)
        {
            compiler.setCallDoesNotReturn(call);
        }

        return call;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = compiler.gtNewStmt(root);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static BasicBlock Block(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        return block;
    }
}
