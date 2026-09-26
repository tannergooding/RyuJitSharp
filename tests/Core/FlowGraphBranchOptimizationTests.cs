// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlowGraphBranchOptimizationTests
{
    [TestCase("kind")]
    [TestCase("next")]
    [TestCase("keep")]
    [TestCase("destination")]
    [TestCase("true-target")]
    [TestCase("try-region")]
    public static void RejectsIneligibleBranchesWithoutChangingGraph(string reason)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var (jump, trueTarget, other, destination, falseTarget) = CreateGraph(compiler);
            var jumpEdge = jump.TargetEdge;
            var firstStmt = jump.FirstStmt;
            switch (reason)
            {
                case "kind":
                {
                    jump.SetKindAndTargetEdge(BBJ_RETURN, null);
                    break;
                }

                case "next":
                {
                    jump.Next = destination;
                    break;
                }

                case "keep":
                {
                    jump.SetFlags(BBF_KEEP_BBJ_ALWAYS);
                    break;
                }

                case "destination":
                {
                    destination.SetKindAndTargetEdge(BBJ_RETURN, null);
                    break;
                }

                case "true-target":
                {
                    jump.Next = other;
                    break;
                }

                case "try-region":
                {
                    jump.TryIndex = 0;
                    break;
                }
            }

            Assert.That(compiler.fgOptimizeBranch(jump), Is.False);
            Assert.That(jump.FirstStmt, Is.SameAs(firstStmt));
            Assert.That(jump.Kind, Is.EqualTo(reason == "kind" ? BBJ_RETURN : BBJ_ALWAYS));
            if (reason != "kind")
            {
                Assert.That(jump.TargetEdge, Is.SameAs(jumpEdge));
            }
            Assert.That(destination.FirstStmt?.RootNode.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(falseTarget.bbRefs, Is.EqualTo(1));
            Assert.That(trueTarget.bbRefs, Is.EqualTo(1));
        });
    }

    [TestCase(NodeThreading.None, false)]
    [TestCase(NodeThreading.AllTrees, false)]
    [TestCase(NodeThreading.AllTrees, true)]
    public static void DuplicatesStatementsAndRedirectsEdges(NodeThreading threading, bool profile)
    {
        FlowGraphCleanupTests.WithCompiler(threading, compiler => {
            var (jump, trueTarget, other, destination, falseTarget) = CreateGraph(compiler);
            other.SetFlags(BBF_KEEP_BBJ_ALWAYS);
            destination.SetFlags(BBF_HAS_NEWOBJ);
            destination.TrueEdge.Likelihood = 0.25;
            destination.FalseEdge.Likelihood = 0.75;
            destination.TrueEdge.isHeuristicBased = true;
            destination.FalseEdge.isHeuristicBased = true;
            var condition = destination.LastStmt ?? throw new AssertionException("Missing destination condition.");
            var store = compiler.fgNewStmtFromTree(
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 7)));
            compiler.fgInsertStmtBefore(destination, condition, store);
            var original = jump.FirstStmt ?? throw new AssertionException("Missing source statement.");
            jump.bbSetRunRarely();
            if (profile)
            {
                compiler.fgPgoHaveWeights = true;
                other.setBBProfileWeight(100);
                destination.setBBProfileWeight(100);
                trueTarget.setBBProfileWeight(25);
                falseTarget.setBBProfileWeight(75);
            }

            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            Assert.That(jump.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(jump.FirstStmt, Is.SameAs(original));
            Assert.That(original.NextStmt, Is.Not.SameAs(store));
            Assert.That(original.NextStmt?.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(original.NextStmt?.NextStmt?.RootNode.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(original.NextStmt?.NextStmt, Is.Not.SameAs(condition));
            Assert.That(jump.LastStmt, Is.SameAs(original.NextStmt?.NextStmt));
            Assert.That(jump.FirstStmt?.PrevStmt, Is.SameAs(jump.LastStmt));
            Assert.That(jump.HasFlag(BBF_HAS_NEWOBJ), Is.True);
            Assert.That(jump.TrueTarget, Is.SameAs(trueTarget));
            Assert.That(jump.FalseTarget, Is.SameAs(falseTarget));
            Assert.That(jump.TrueEdge.Likelihood, Is.EqualTo(0.25));
            Assert.That(jump.FalseEdge.Likelihood, Is.EqualTo(0.75));
            Assert.That(jump.TrueEdge.isHeuristicBased, Is.True);
            Assert.That(jump.FalseEdge.isHeuristicBased, Is.True);
            Assert.That(destination.bbRefs, Is.EqualTo(1));
            Assert.That(trueTarget.bbRefs, Is.EqualTo(2));
            Assert.That(falseTarget.bbRefs, Is.EqualTo(2));
            Assert.That(destination.FirstStmt, Is.SameAs(store));
            Assert.That(destination.LastStmt, Is.SameAs(condition));

            if (threading is NodeThreading.AllTrees)
            {
                var clonedJump = jump.LastStmt?.RootNode ?? throw new AssertionException("Missing cloned jump.");
                Assert.That(clonedJump.Prev, Is.SameAs(clonedJump.AsUnOp().Op1));
            }

            if (profile)
            {
                Assert.That(destination.bbWeight, Is.EqualTo(100));
                Assert.That(trueTarget.bbWeight, Is.EqualTo(25));
                Assert.That(falseTarget.bbWeight, Is.EqualTo(75));
                Assert.That(compiler.fgPgoConsistent, Is.True);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void RepairsProfileOnlyWithUsableWeightsAndInvalidatesOutgoingFlow(bool usable, bool outgoing)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var (jump, trueTarget, other, destination, falseTarget) = CreateGraph(compiler);
            other.SetFlags(BBF_KEEP_BBJ_ALWAYS);
            compiler.fgPgoHaveWeights = true;
            destination.setBBProfileWeight(100);
            other.setBBProfileWeight(100);
            if (usable)
            {
                jump.setBBProfileWeight(0);
                trueTarget.setBBProfileWeight(25);
                falseTarget.setBBProfileWeight(75);
            }

            if (outgoing)
            {
                falseTarget.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(trueTarget, falseTarget));
            }

            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            Assert.That(jump.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(destination.bbWeight, Is.EqualTo(100));
            Assert.That(trueTarget.hasProfileWeight, Is.EqualTo(usable));
            Assert.That(falseTarget.hasProfileWeight, Is.EqualTo(usable));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!outgoing));
            if (usable)
            {
                Assert.That(trueTarget.bbWeight, Is.EqualTo(outgoing ? 100 : 25));
                Assert.That(falseTarget.bbWeight, Is.EqualTo(75));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RarityExtendsTheSixByteCostBudget(bool rare)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var (jump, _, other, destination, _) = CreateGraph(compiler);
            other.SetFlags(BBF_KEEP_BBJ_ALWAYS);
            var condition = destination.LastStmt ?? throw new AssertionException("Missing destination condition.");
            var store = compiler.fgNewStmtFromTree(
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 7)));
            compiler.fgInsertStmtBefore(destination, condition, store);
            if (rare)
            {
                jump.bbSetRunRarely();
            }

            var statementId = compiler.compStatementID;
            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            Assert.That(store.CostSz + condition.CostSz, Is.GreaterThan(6).And.LessThanOrEqualTo(12));
            Assert.That(jump.Kind, Is.EqualTo(rare ? BBJ_COND : BBJ_ALWAYS));
            Assert.That(compiler.compStatementID, Is.EqualTo(statementId + (rare ? 2 : 0)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RareAotSourceDoublesTheDuplicationBudget(bool aot)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var (jump, _, other, destination, _) = CreateGraph(compiler);
            other.SetFlags(BBF_KEEP_BBJ_ALWAYS);
            jump.bbSetRunRarely();
            if (aot)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            }

            var condition = destination.LastStmt ?? throw new AssertionException("Missing destination condition.");
            for (var index = 0; index < 3; index++)
            {
                var store = compiler.fgNewStmtFromTree(
                    compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, index)));
                compiler.fgInsertStmtBefore(destination, condition, store);
            }

            var statementId = compiler.compStatementID;
            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            var cost = 0;
            foreach (var statement in destination.Statements)
            {
                cost += statement.CostSz;
            }
            Assert.That(cost, Is.GreaterThan(12).And.LessThanOrEqualTo(24));
            Assert.That(jump.Kind, Is.EqualTo(aot ? BBJ_COND : BBJ_ALWAYS));
            Assert.That(compiler.compStatementID, Is.EqualTo(statementId + (aot ? 4 : 0)));
        });
    }

    [TestCase(1.0, false)]
    [TestCase(0.99, true)]
    public static void ProfileRarityUsesStrictHundredfoldComparison(double sourceWeight, bool duplicate)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var (jump, trueTarget, other, destination, falseTarget) = CreateGraph(compiler);
            other.SetFlags(BBF_KEEP_BBJ_ALWAYS);
            var condition = destination.LastStmt ?? throw new AssertionException("Missing destination condition.");
            var store = compiler.fgNewStmtFromTree(
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 7)));
            compiler.fgInsertStmtBefore(destination, condition, store);
            compiler.fgPgoHaveWeights = true;
            jump.setBBProfileWeight(sourceWeight);
            other.setBBProfileWeight(100 - sourceWeight);
            destination.setBBProfileWeight(100);
            trueTarget.setBBProfileWeight(25);
            falseTarget.setBBProfileWeight(75);

            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            Assert.That(store.CostSz + condition.CostSz, Is.GreaterThan(6).And.LessThanOrEqualTo(12));
            Assert.That(jump.Kind, Is.EqualTo(duplicate ? BBJ_COND : BBJ_ALWAYS));
            Assert.That(destination.bbWeight, Is.EqualTo(100 - (duplicate ? sourceWeight : 0)));
        });
    }

    [Test]
    public static void RejectsNoncomparisonAfterCloningWithoutLinkingStatements()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllTrees, compiler => {
            var (jump, trueTarget, _, destination, falseTarget) = CreateGraph(compiler);
            var condition = destination.LastStmt ?? throw new AssertionException("Missing destination condition.");
            condition.RootNode = new GenTreeUnOp(GT_JTRUE, TYP_VOID, compiler.gtNewLclvNode(TYP_INT, 0));
            var sourceStmt = jump.FirstStmt;
            var statementId = compiler.compStatementID;

            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            Assert.That(compiler.compStatementID, Is.GreaterThan(statementId));
            Assert.That(jump.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(jump.FirstStmt, Is.SameAs(sourceStmt));
            Assert.That(jump.LastStmt, Is.SameAs(sourceStmt));
            Assert.That(jump.Target, Is.SameAs(destination));
            Assert.That(trueTarget.bbRefs, Is.EqualTo(1));
            Assert.That(falseTarget.bbRefs, Is.EqualTo(1));
        });
    }

    [Test]
    public static void HighDuplicationCostReturnsModifiedWithoutChangingEdges()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllTrees, compiler => {
            var (jump, _, _, destination, _) = CreateGraph(compiler);
            var condition = destination.LastStmt ?? throw new AssertionException("Missing destination condition.");
            for (var index = 0; index < 8; index++)
            {
                var expensive = compiler.fgNewStmtFromTree(compiler.gtNewStoreLclVarNode(1,
                    compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                        compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, index))));
                compiler.fgInsertStmtBefore(destination, condition, expensive);
            }

            var statementId = compiler.compStatementID;

            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            var size = 0u;
            foreach (var statement in destination.Statements)
            {
                size += statement.CostSz;
            }
            Assert.That(size, Is.GreaterThan(6));
            Assert.That(jump.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(jump.Target, Is.SameAs(destination));
            Assert.That(compiler.compStatementID, Is.EqualTo(statementId));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CompactsDestinationOnlyWhenRemainingPredecessorAllowsIt(bool compact)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var (jump, _, other, destination, _) = CreateGraph(compiler);
            if (!compact)
            {
                other.SetFlags(BBF_KEEP_BBJ_ALWAYS);
            }

            Assert.That(compiler.fgOptimizeBranch(jump), Is.True);
            Assert.That(compiler.fgLastBB, Is.Not.Null);
            Assert.That(other.Kind, Is.EqualTo(compact ? BBJ_COND : BBJ_ALWAYS));
            Assert.That(other.Next == destination, Is.EqualTo(!compact));
            Assert.That(jump.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    private static (BasicBlock Jump, BasicBlock TrueTarget, BasicBlock Other, BasicBlock Destination,
        BasicBlock FalseTarget) CreateGraph(Compiler compiler)
    {
        var jump = NewBlock(compiler, BBJ_ALWAYS);
        var trueTarget = NewBlock(compiler, BBJ_RETURN);
        var other = NewBlock(compiler, BBJ_ALWAYS);
        var destination = NewBlock(compiler, BBJ_COND);
        var falseTarget = NewBlock(compiler, BBJ_RETURN);
        jump.Next = trueTarget;
        trueTarget.Next = other;
        other.Next = destination;
        destination.Next = falseTarget;
        compiler.fgFirstBB = jump;
        compiler.fgLastBB = falseTarget;
        jump.TargetEdge = compiler.fgAddRefPred(destination, jump);
        other.TargetEdge = compiler.fgAddRefPred(destination, other);
        destination.SetCond(compiler.fgAddRefPred(trueTarget, destination),
            compiler.fgAddRefPred(falseTarget, destination));
        destination.TrueEdge.Likelihood = 0.25;
        destination.FalseEdge.Likelihood = 0.75;
        _ = Append(compiler, jump, compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1)));
        _ = Append(compiler, destination, new GenTreeUnOp(GT_JTRUE, TYP_VOID, Comparison(compiler)));

        return (jump, trueTarget, other, destination, falseTarget);
    }

    private static GenTreeOp Comparison(Compiler compiler)
    {
        var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 4));
        comparison.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
        return comparison;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.fgNewStmtFromTree(tree);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        return block;
    }
}
