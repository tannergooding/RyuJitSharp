// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class SwitchOptimizationTests
{
    [TestCase(false, 1)]
    [TestCase(false, 4)]
    [TestCase(true, 1)]
    [TestCase(true, 4)]
    public static void SingleSuccessorRemovesDispatchAndNormalizesDuplicateCounts(bool lir, int cases)
    {
        WithCompiler(lir ? NodeThreading.LIR : NodeThreading.AllTrees, compiler => {
            var (block, _, targets) = CreateSwitch(compiler, new int[cases]);
            var edge = block.SwitchTargets.Cases[0];
            edge.Likelihood = 1;

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(block.TargetEdge, Is.SameAs(edge));
            Assert.That(edge.DupCount, Is.EqualTo(1));
            Assert.That(targets[0].bbRefs, Is.EqualTo(1));
            Assert.That(edge.Likelihood, Is.EqualTo(1));
            Assert.That(block.FirstStmt, Is.Null);
            Assert.That(block.FirstNode, Is.Null);
            Assert.That(block.LastNode, Is.Null);
            Assert.That(compiler.fgPgoConsistent, Is.True);
        });
    }

    [TestCase(NodeThreading.None, false)]
    [TestCase(NodeThreading.None, true)]
    [TestCase(NodeThreading.AllTrees, false)]
    [TestCase(NodeThreading.AllTrees, true)]
    public static void SingleSuccessorExtractsEffectsButRemovesStaleEffectFlags(NodeThreading threading, bool store)
    {
        WithCompiler(threading, compiler => {
            var (block, node, _) = CreateSwitch(compiler, [0, 0, 0]);
            var statement = block.LastStmt ?? throw new AssertionException("Missing switch statement.");
            GenTree? assignment = null;
            if (store)
            {
                assignment = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 7));
                node.AsUnOp().Op1 = compiler.gtNewCommaNode(TYP_INT, assignment, node.AsUnOp().Op1);
            }

            PrepareStatement(compiler, statement);
            node.Flags |= GTF_ASG;

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            if (store)
            {
                Assert.That(block.LastStmt, Is.SameAs(statement));
                Assert.That(statement.RootNode, Is.SameAs(assignment));
                if (threading is NodeThreading.AllTrees)
                {
                    var storeTree = assignment ?? throw new AssertionException("Missing store.");
                    Assert.That(statement.TreeListBegin, Is.SameAs(storeTree.AsLclVar().Data));
                    Assert.That(statement.RootNode.Next, Is.Null);
                    Assert.That(compiler.compCurBB, Is.SameAs(block));
                }
            }
            else
            {
                Assert.That(block.FirstStmt, Is.Null);
            }
        });
    }

    [TestCase(NodeThreading.None, TYP_INT)]
    [TestCase(NodeThreading.AllTrees, TYP_INT)]
    [TestCase(NodeThreading.None, TYP_I_IMPL)]
    [TestCase(NodeThreading.AllTrees, TYP_I_IMPL)]
    public static void TwoCasesBecomeEqualityAndRetainRootIdentity(NodeThreading threading, var_types type)
    {
        WithCompiler(threading, compiler => {
            compiler.lvaTable[0].Type = type;
            var (block, node, targets) = CreateSwitch(compiler, [0, 1], type);
            var value = node.AsUnOp().Op1;
            var trueEdge = block.SwitchTargets.Cases[0];
            var falseEdge = block.SwitchTargets.Cases[1];

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            var statement = block.LastStmt ?? throw new AssertionException("Missing conditional statement.");
            var jump = statement.RootNode;
            var condition = jump.AsUnOp().Op1.AsOp();
            Assert.That(jump.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(jump, Is.Not.SameAs(node));
            Assert.That(condition.Oper, Is.EqualTo(GT_EQ));
            Assert.That(condition.Op1, Is.SameAs(value));
            Assert.That(condition.Op2.Type, Is.EqualTo(type.ActualType));
            Assert.That(condition.Op2.IsIntegralConst(0), Is.True);
            Assert.That(condition.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE),
                Is.EqualTo(GTF_RELOP_JMP_USED | GTF_DONT_CSE));
            Assert.That(block.TrueEdge, Is.SameAs(trueEdge));
            Assert.That(block.FalseEdge, Is.SameAs(falseEdge));
            Assert.That(targets[0].bbRefs, Is.EqualTo(1));
            Assert.That(targets[1].bbRefs, Is.EqualTo(1));
            Assert.That(node.Prev is null && node.Next is null, Is.True);
#if DEBUG
            Assert.That(jump.TreeId, Is.EqualTo(node.TreeId));
#endif
            if (threading is NodeThreading.AllTrees)
            {
                Assert.That(statement.TreeListBegin, Is.SameAs(value));
                Assert.That(condition.Next, Is.SameAs(jump));
                Assert.That(jump.Prev, Is.SameAs(condition));
                Assert.That(jump.Next, Is.Null);
            }
        });
    }

    [Test]
    public static void TwoLirCasesRemoveTheTableAndLowerTheReplacementBranch()
    {
        WithCompiler(NodeThreading.LIR, compiler => {
            var (block, node, _) = CreateSwitch(compiler, [0, 1]);
            var table = node.AsOp().Op2;
            var trueEdge = block.SwitchTargets.Cases[0];
            var falseEdge = block.SwitchTargets.Cases[1];
            CurrentLowering(compiler) = new Lowering(compiler, new LinearScan(compiler));

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            var branch = block.LastNode ?? throw new AssertionException("Missing lowered branch.");
            Assert.That(branch.Oper, Is.EqualTo(GT_JCC));
            Assert.That(branch.AsCC().Condition.Code, Is.EqualTo(GenCondition.EQ));
            Assert.That(block.TrueEdge, Is.SameAs(trueEdge));
            Assert.That(block.FalseEdge, Is.SameAs(falseEdge));
            Assert.That(block.Any(tree => tree.Oper is GT_JMPTABLE or GT_SWITCH_TABLE or GT_JTRUE), Is.False);
            Assert.That(table.Prev is null && table.Next is null, Is.True);
            Assert.That(node.Prev is null && node.Next is null, Is.True);
            Assert.That(branch.Prev?.Next, Is.SameAs(branch));
#if DEBUG
            Assert.That(branch.TreeId, Is.EqualTo(node.TreeId));
#endif
        });
    }

    [TestCase(2)]
    [TestCase(5)]
    public static void RepeatedNonDefaultCasesBecomeAnUnsignedRangeComparison(int nonDefaultCases)
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            int[] cases = [.. Enumerable.Repeat(0, nonDefaultCases), 1];
            var (block, node, targets) = CreateSwitch(compiler, cases);
            var oldValue = node.AsUnOp().Op1;
            var first = block.SwitchTargets.Cases[0];
            var last = block.SwitchTargets.DefaultCase;
            first.Likelihood = 0.75;
            last.Likelihood = 0.25;

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            var statement = block.LastStmt ?? throw new AssertionException("Missing range statement.");
            var comparison = statement.RootNode.AsUnOp().Op1.AsOp();
            Assert.That(comparison.Oper, Is.EqualTo(GT_LT));
            Assert.That(comparison.IsUnsigned, Is.True);
            Assert.That(comparison.Op1, Is.SameAs(oldValue));
            Assert.That(comparison.Op2.IsIntegralConst(nonDefaultCases), Is.True);
            Assert.That(first.DupCount, Is.EqualTo(1));
            Assert.That(targets[0].bbRefs, Is.EqualTo(1));
            Assert.That(block.TrueEdge, Is.SameAs(first));
            Assert.That(block.FalseEdge, Is.SameAs(last));
            Assert.That(first.Likelihood, Is.EqualTo(0.75));
#if DEBUG
            Assert.That(statement.RootNode.TreeId, Is.EqualTo(node.TreeId));
#endif
        });
    }

    [TestCase("default-shared")]
    [TestCase("no-default")]
    [TestCase("unthreaded")]
    [TestCase("lir")]
    public static void LeavesNontrivialSwitchesOutsideTheRangeOptimizationContract(string reason)
    {
        WithCompiler(reason == "lir" ? NodeThreading.LIR :
            reason == "unthreaded" ? NodeThreading.None : NodeThreading.AllTrees, compiler => {
            var (block, node, _) = CreateSwitch(compiler, reason == "default-shared" ? [0, 1, 1] : [0, 0, 1],
                hasDefault: reason != "no-default");

            var descriptor = block.SwitchTargets;
            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.False);
            Assert.That(block.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(block.SwitchTargets, Is.SameAs(descriptor));
            Assert.That(block.IsLIR ? block.LastNode : block.LastStmt?.RootNode, Is.SameAs(node));
            Assert.That(compiler.fgPgoConsistent, Is.True);
        });
    }

    [Test]
    public static void ThreadsRepeatedSuccessorsThroughChainsAndDebitsEachProfileEdgeOnce()
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (block, _, targets) = CreateSwitch(compiler, [0, 0, 1, 2]);
            var first = targets[0];
            var second = NewBlock(compiler, BBJ_ALWAYS);
            targets[^1].Next = second;
            var destination = NewBlock(compiler, BBJ_RETURN);
            second.Next = destination;
            compiler.fgLastBB = destination;
            first.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(second, first));
            second.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(destination, second));
            first.SetFlags(BBF_ASYNC_RESUMPTION);
            first.setBBProfileWeight(100);
            second.setBBProfileWeight(100);
            block.setBBProfileWeight(40);
            block.SwitchTargets.Cases[0].Likelihood = 0.5;

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(block.SwitchTargets.Cases[0].DestinationBlock, Is.SameAs(destination));
            Assert.That(block.SwitchTargets.Cases[0], Is.SameAs(block.SwitchTargets.Cases[1]));
            Assert.That(block.SwitchTargets.Cases[0].DupCount, Is.EqualTo(2));
            Assert.That(first.bbWeight, Is.EqualTo(80));
            Assert.That(second.bbWeight, Is.EqualTo(80));
            Assert.That(first.bbRefs, Is.Zero);
            Assert.That(second.bbRefs, Is.EqualTo(1));
            Assert.That(destination.bbRefs, Is.EqualTo(3));
            Assert.That(block.HasFlag(BBF_ASYNC_RESUMPTION), Is.True);
            Assert.That(compiler.fgPgoConsistent, Is.False);
        });
    }

    [TestCase("self")]
    [TestCase("empty-cycle")]
    [TestCase("different-try")]
    public static void PreservesForbiddenJumpThreading(string reason)
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (block, _, targets) = CreateSwitch(compiler, [0, 1, 2]);
            var destination = targets[0];
            if (reason == "self")
            {
                destination.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(destination, destination));
            }
            else
            {
                destination.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(targets[1], destination));
                if (reason == "empty-cycle")
                {
                    targets[1].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(destination, targets[1]));
                }
                else
                {
                    destination.TryIndex = 0;
                }
            }

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.False);
            Assert.That(block.SwitchTargets.Cases[0].DestinationBlock, Is.SameAs(destination));
            Assert.That(compiler.fgPgoConsistent, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CycleDetectionStopsAtNonemptyStatementBlocksAndIgnoresLirOffsets(bool lir)
    {
        WithCompiler(lir ? NodeThreading.LIR : NodeThreading.None, compiler => {
            var (block, _, targets) = CreateSwitch(compiler, [0, 1, 2]);
            var first = targets[0];
            var second = targets[1];
            first.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(second, first));
            second.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(first, second));
            if (lir)
            {
                first.InsertAtEnd(new GenTreeILOffset(default));
                second.InsertAtEnd(new GenTreeILOffset(default));
                Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.False);
            }
            else
            {
                compiler.fgInsertStmtAtEnd(second, compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1,
                    compiler.gtNewIconNode(TYP_INT, 7))));
                Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
                Assert.That(block.SwitchTargets.Cases[0].DestinationBlock, Is.SameAs(second));
                Assert.That(block.SwitchTargets.Cases[1].DestinationBlock, Is.SameAs(second));
            }
        });
    }

#if !JIT32_GCENCODER
    [TestCase(false)]
    [TestCase(true)]
    public static void RangeSwitchCanFoldAllTheWayToABooleanReturn(bool reverse)
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, node, targets) = CreateSwitch(compiler, [0, 0, 0, 1]);
            compiler.info.compRetType = TYP_UBYTE;
            AppendReturn(compiler, targets[0], reverse ? 0 : 1);
            AppendReturn(compiler, targets[1], reverse ? 1 : 0);

            Assert.That(compiler.fgOptimizeSwitchBranches(block), Is.True);
            var root = block.LastStmt?.RootNode ?? throw new AssertionException("Missing return.");
            Assert.That(block.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(root.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(root.Type, Is.EqualTo(TYP_INT));
            Assert.That(root.AsUnOp().Op1.Oper, Is.EqualTo(reverse ? GT_GE : GT_LT));
            Assert.That(root.AsUnOp().Op1.AsOp().IsUnsigned, Is.True);
            Assert.That(root.AsUnOp().Op1.Flags & GTF_RELOP_JMP_USED, Is.EqualTo((GenTreeFlags)0));
            Assert.That(targets[0].bbRefs, Is.Zero);
            Assert.That(targets[1].bbRefs, Is.Zero);
#if DEBUG
            Assert.That(root.TreeId, Is.EqualTo(node.TreeId));
#endif
        });
    }

    [TestCase(false, 10, 20, 20)]
    [TestCase(true, 10, 20, 20)]
    [TestCase(false, BAD_IL_OFFSET, 20, BAD_IL_OFFSET)]
    [TestCase(false, 20, int.MinValue, int.MinValue)]
    public static void BooleanReturnFoldingPreservesIdentityWeightsOffsetsAndThreading(
        bool reverse, int trueEnd, int falseEnd, int expectedEnd)
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, trueBlock, falseBlock, node, condition) = CreateConditionalReturns(compiler, reverse);
            block.setBBProfileWeight(40);
            block.TrueEdge.Likelihood = 0.25;
            block.FalseEdge.Likelihood = 0.75;
            trueBlock.setBBProfileWeight(100);
            falseBlock.setBBProfileWeight(100);
            trueBlock.bbCodeOffsEnd = trueEnd;
            falseBlock.bbCodeOffsEnd = falseEnd;
            var statement = block.LastStmt ?? throw new AssertionException("Missing conditional.");

            Assert.That(compiler.fgFoldCondToReturnBlock(block), Is.True);
            var root = statement.RootNode;
            Assert.That(block.LastStmt, Is.SameAs(statement));
            Assert.That(root.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(root.AsUnOp().Op1, Is.SameAs(condition));
            Assert.That(condition.Oper, Is.EqualTo(reverse ? GT_GE : GT_LT));
            Assert.That(condition.Flags & GTF_RELOP_JMP_USED, Is.EqualTo((GenTreeFlags)0));
            Assert.That(condition.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
            Assert.That(condition.Next, Is.SameAs(root));
            Assert.That(root.Prev, Is.SameAs(condition));
            Assert.That(root.Next, Is.Null);
            Assert.That(node.Prev is null && node.Next is null, Is.True);
            Assert.That(block.bbCodeOffsEnd, Is.EqualTo(expectedEnd));
            Assert.That(trueBlock.bbWeight, Is.EqualTo(90));
            Assert.That(falseBlock.bbWeight, Is.EqualTo(70));
            Assert.That(trueBlock.bbPreds, Is.Null);
            Assert.That(falseBlock.bbPreds, Is.Null);
            Assert.That(trueBlock.HasFlag(BBF_REMOVED), Is.False);
#if DEBUG
            Assert.That(root.TreeId, Is.EqualTo(node.TreeId));
            Assert.That(root.TreeId, Is.Not.EqualTo(condition.TreeId));
#endif
        });
    }

    [TestCase("wrong-return-type")]
    [TestCase("not-return")]
    [TestCase("eh-mismatch")]
    [TestCase("shared-epilogue")]
    [TestCase("not-comparison")]
    [TestCase("equal-values")]
    [TestCase("not-boolean")]
    [TestCase("call-effects")]
    [TestCase("exception-effects")]
    [TestCase("global-store")]
    public static void RefusesUnsafeBooleanReturnFolds(string reason)
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, trueBlock, falseBlock, node, _) = CreateConditionalReturns(compiler, false);
            var trueReturn = trueBlock.LastStmt?.RootNode ?? throw new AssertionException("Missing true return.");
            var falseReturn = falseBlock.LastStmt?.RootNode ?? throw new AssertionException("Missing false return.");
            switch (reason)
            {
                case "wrong-return-type":
                {
                    compiler.info.compRetType = TYP_INT;
                    break;
                }

                case "not-return":
                {
                    trueBlock.Kind = BBJ_THROW;
                    break;
                }

                case "eh-mismatch":
                {
                    trueBlock.TryIndex = 0;
                    break;
                }

                case "shared-epilogue":
                {
                    compiler.genReturnBB = trueBlock;
                    break;
                }

                case "not-comparison":
                {
                    node.AsUnOp().Op1 = compiler.gtNewLclvNode(TYP_INT, 0);
                    break;
                }

                case "equal-values":
                {
                    falseReturn.AsUnOp().Op1 = compiler.gtNewIconNode(TYP_INT, 1);
                    break;
                }

                case "not-boolean":
                {
                    trueReturn.AsUnOp().Op1 = compiler.gtNewIconNode(TYP_INT, 2);
                    break;
                }

                case "call-effects":
                {
                    trueReturn.Flags |= GTF_CALL;
                    break;
                }

                case "exception-effects":
                {
                    falseReturn.Flags |= GTF_EXCEPT;
                    break;
                }

                case "global-store":
                {
                    trueReturn.Flags |= GTF_ASG | GTF_GLOB_REF;
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown refusal case.");
                }
            }

            Assert.That(compiler.fgFoldCondToReturnBlock(block), Is.False);
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(block.LastStmt?.RootNode, Is.SameAs(node));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AllowsDeadLocalStoresButNotAnAdditionalSharedEpilogue(bool bothShared)
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, trueBlock, falseBlock, _, _) = CreateConditionalReturns(compiler, false);
            var returnStatement = trueBlock.LastStmt ?? throw new AssertionException("Missing return.");
            var store = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 5)));
            compiler.fgInsertStmtBefore(trueBlock, returnStatement, store);
            var other = NewBlock(compiler, BBJ_ALWAYS);
            falseBlock.Next = other;
            compiler.fgLastBB = other;
            other.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(trueBlock, other));
            if (bothShared)
            {
                var first = other.TargetEdge;
                other.SetCond(first, compiler.fgAddRefPred(falseBlock, other));
            }

            Assert.That(compiler.fgFoldCondToReturnBlock(block), Is.EqualTo(!bothShared));
            Assert.That(block.Kind, Is.EqualTo(bothShared ? BBJ_COND : BBJ_RETURN));
            Assert.That(trueBlock.bbRefs, Is.EqualTo(bothShared ? 2 : 1));
            Assert.That(trueBlock.FirstStmt, Is.SameAs(store));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CompactsEitherReturnProxyBeforeFolding(bool falseProxy)
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, trueBlock, falseBlock, _, _) = CreateConditionalReturns(compiler, false);
            var proxy = falseProxy ? falseBlock : trueBlock;
            var destination = NewBlock(compiler, BBJ_RETURN);
            falseBlock.Next = destination;
            compiler.fgLastBB = destination;
            AppendReturn(compiler, destination, falseProxy ? 0 : 1);
            proxy.FirstStmt = null;
            proxy.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(destination, proxy));

            Assert.That(compiler.fgFoldCondToReturnBlock(block), Is.True);
            Assert.That(destination.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(proxy.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(block.Kind, Is.EqualTo(BBJ_RETURN));
        });
    }

    [Test]
    public static void ReportsCompactionEvenWhenItRemovesTheConditionalInsteadOfFoldingAReturn()
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, trueBlock, falseBlock, _, _) = CreateConditionalReturns(compiler, false);
            trueBlock.FirstStmt = null;
            trueBlock.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(falseBlock, trueBlock));

            Assert.That(compiler.fgFoldCondToReturnBlock(block), Is.True);
            Assert.That(falseBlock.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(block.Target, Is.SameAs(trueBlock));
            Assert.That(block.FirstStmt, Is.Null);
        });
    }
#else
    [Test]
    public static void LegacyGcEncoderDoesNotIntroduceAnotherEpilogue()
    {
        WithCompiler(NodeThreading.AllTrees, compiler => {
            var (block, _, _, _, _) = CreateConditionalReturns(compiler, false);
            Assert.That(compiler.fgFoldCondToReturnBlock(block), Is.False);
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
        });
    }
#endif

    [Test]
    public static void UniquePredecessorCountsEdgesRatherThanDuplicateSwitchCasesAndExcludesEntry()
    {
        WithCompiler(NodeThreading.None, compiler => {
            var (block, _, targets) = CreateSwitch(compiler, [0, 0]);
            var target = targets[0];
            Assert.That(target.GetUniquePred(compiler), Is.SameAs(block));
            Assert.That(block.GetUniquePred(compiler), Is.Null);
            compiler.fgFirstBB = target;
            Assert.That(target.GetUniquePred(compiler), Is.Null);
            compiler.fgFirstBB = block;
            var other = NewBlock(compiler, BBJ_ALWAYS);
            target.Next = other;
            compiler.fgLastBB = other;
            other.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, other));
            Assert.That(target.GetUniquePred(compiler), Is.Null);
        });
    }

    private static (BasicBlock Block, BasicBlock TrueBlock, BasicBlock FalseBlock, GenTree Node, GenTreeOp Condition)
        CreateConditionalReturns(Compiler compiler, bool reverse)
    {
        compiler.info.compRetType = TYP_UBYTE;
        var block = NewBlock(compiler, BBJ_COND);
        var trueBlock = NewBlock(compiler, BBJ_RETURN);
        var falseBlock = NewBlock(compiler, BBJ_RETURN);
        block.Next = trueBlock;
        trueBlock.Next = falseBlock;
        compiler.fgFirstBB = block;
        compiler.fgLastBB = falseBlock;
        block.SetCond(compiler.fgAddRefPred(trueBlock, block), compiler.fgAddRefPred(falseBlock, block));
        block.TrueEdge.Likelihood = 0.5;
        block.FalseEdge.Likelihood = 0.5;
        var condition = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 5));
        condition.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
        var node = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
        var statement = compiler.gtNewStmt(node);
        compiler.fgInsertStmtAtEnd(block, statement);
        PrepareStatement(compiler, statement);
        AppendReturn(compiler, trueBlock, reverse ? 0 : 1);
        AppendReturn(compiler, falseBlock, reverse ? 1 : 0);

        return (block, trueBlock, falseBlock, node, condition);
    }

    private static void AppendReturn(Compiler compiler, BasicBlock block, int value)
    {
        var statement = compiler.gtNewStmt(new GenTreeUnOp(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, value)));
        compiler.fgInsertStmtAtEnd(block, statement);
        PrepareStatement(compiler, statement);
    }

    private static (BasicBlock Block, GenTree Node, BasicBlock[] Targets) CreateSwitch(
        Compiler compiler, int[] cases, var_types type = TYP_INT, bool hasDefault = true)
    {
        var block = NewBlock(compiler, BBJ_SWITCH);
        var targets = Enumerable.Range(0, cases.Max() + 1).Select(_ => NewBlock(compiler, BBJ_RETURN)).ToArray();
        var previous = block;
        foreach (var target in targets)
        {
            previous.Next = target;
            previous = target;
        }

        compiler.fgFirstBB = block;
        compiler.fgLastBB = previous;
        var edges = new FlowEdge[cases.Length];
        var successors = new List<FlowEdge>();
        for (var i = 0; i < cases.Length; i++)
        {
            var edge = compiler.fgAddRefPred(targets[cases[i]], block);
            edges[i] = edge;
            if (!successors.Contains(edge))
            {
                successors.Add(edge);
            }
        }

        foreach (var edge in successors)
        {
            edge.Likelihood = (double)edge.DupCount / cases.Length;
        }

        var descriptor = new BBswtDesc([.. successors], new int[cases.Length], hasDefault);
        edges.CopyTo(descriptor.Cases);
        block.SwitchTargets = descriptor;
        var value = compiler.gtNewLclvNode(type, 0);
        GenTree node;
        if (block.IsLIR)
        {
            var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL);
            node = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, value, table);
            block.InsertAtEnd(value);
            block.InsertAtEnd(table);
            block.InsertAtEnd(node);
        }
        else
        {
            node = new GenTreeUnOp(GT_SWITCH, TYP_VOID, value);
            var statement = compiler.gtNewStmt(node);
            compiler.fgInsertStmtAtEnd(block, statement);
            PrepareStatement(compiler, statement);
        }

        return (block, node, targets);
    }

    private static void PrepareStatement(Compiler compiler, Statement statement)
    {
        if (compiler.fgNodeThreading is NodeThreading.AllTrees)
        {
            compiler.gtSetStmtInfo(statement);
            compiler.fgSetStmtSeq(statement);
        }
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;

        return block;
    }

    private static void WithCompiler(NodeThreading threading, Action<Compiler> action)
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
        compiler.compHndBBtab = [];
        compiler.compRationalIRForm = threading is NodeThreading.LIR;
        compiler.fgNodeThreading = threading;
        compiler.fgPredsComputed = true;
        compiler.fgPgoConsistent = true;
        compiler.info.compRetType = TYP_INT;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pLowering")]
    private static extern ref Lowering? CurrentLowering(Compiler compiler);
}
