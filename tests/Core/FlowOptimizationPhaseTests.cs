// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowOptimizationPhaseTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void OnlyEarlyFlowPhaseEnablesTailDuplication(bool early)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compIsStatic = true;
            compiler.lvaArg0Var = BAD_VAR_NUM;
            compiler.info.compRetType = TYP_UBYTE;
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var dead = BasicBlock.New(compiler, BBJ_RETURN);
            entry.SetFlags(BBF_IMPORTED);
            dead.SetFlags(BBF_IMPORTED);
            Link(compiler, entry, dead);
            var condition = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0));
            condition.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;
            compiler.fgInsertStmtAtEnd(entry,
                compiler.fgNewStmtFromTree(new GenTreeUnOp(GT_RETURN, TYP_INT, condition)));

            var status = early ? compiler.optOptimizeFlow() : compiler.optOptimizePreLayout();

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Kind, Is.EqualTo(early ? BBJ_COND : BBJ_RETURN));
            Assert.That(compiler.fgBBcount, Is.EqualTo(early ? 3 : 1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RarePropagationBacktracksWithoutOverwritingProfiledPredecessor(bool profile)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var middle = BasicBlock.New(compiler, BBJ_RETURN);
            var last = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, first, middle, last);
            first.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(middle, first));
            middle.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(last, middle));
            last.bbSetRunRarely();
            if (profile)
            {
                first.setBBProfileWeight(7);
            }

            Assert.That(compiler.fgExpandRarelyRunBlocks(), Is.True);
            Assert.That(first.isRunRarely, Is.EqualTo(!profile));
            Assert.That(middle.isRunRarely, Is.True);
            Assert.That(last.isRunRarely, Is.True);
            Assert.That(compiler.fgExpandRarelyRunBlocks(), Is.False);
            if (profile)
            {
                Assert.That(first.bbWeight, Is.EqualTo(7));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConditionalRequiresBothSuccessorsToBeRare(bool both)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var taken = BasicBlock.New(compiler, BBJ_RETURN);
            var other = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, first, taken, other);
            first.SetCond(compiler.fgAddRefPred(taken, first), compiler.fgAddRefPred(other, first));
            taken.bbSetRunRarely();
            if (both)
            {
                other.bbSetRunRarely();
            }

            Assert.That(compiler.fgExpandRarelyRunBlocks(), Is.EqualTo(both));
            Assert.That(first.isRunRarely, Is.EqualTo(both));
        });
    }

    [TestCase(GT_EQ, GT_NONE, false, true)]
    [TestCase(GT_EQ, GT_COPY, false, true)]
    [TestCase(GT_EQ, GT_RELOAD, false, true)]
    [TestCase(GT_ADD, GT_NONE, false, false)]
    [TestCase(GT_JCC, GT_NONE, false, true)]
    [TestCase(GT_EQ, GT_NONE, true, false)]
    public static void PostLayoutReversesOnlyInPlaceAndPreservesLirAndEdges(
        genTreeOps oper, genTreeOps wrapper, bool coldTarget, bool reverse)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.LIR, compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var taken = BasicBlock.New(compiler, BBJ_RETURN);
            var other = BasicBlock.New(compiler, BBJ_RETURN);
            Link(compiler, block, taken, other);
            block.SetCond(compiler.fgAddRefPred(taken, block), compiler.fgAddRefPred(other, block));
            var oldTrue = block.TrueEdge;
            var oldFalse = block.FalseEdge;
            oldTrue.Likelihood = 0.25;
            oldFalse.Likelihood = 0.75;
            compiler.fgFirstColdBlock = coldTarget ? taken : null;
            var nodes = new List<GenTree>();
            GenTree condition;

            if (oper is GT_JCC)
            {
                condition = new GenTreeCC(GT_JCC, TYP_VOID, new GenCondition(GenCondition.EQ));
                nodes.Add(condition);
            }
            else
            {
                var left = compiler.gtNewLclvNode(TYP_INT, 0);
                var right = compiler.gtNewIconNode(TYP_INT, 4);
                condition = compiler.gtNewBinaryNode(oper, TYP_INT, left, right);
                nodes.Add(left);
                nodes.Add(right);
                nodes.Add(condition);
                var operand = condition;
                if (wrapper is GT_COPY or GT_RELOAD)
                {
                    operand = new GenTreeCopyOrReload(wrapper, TYP_INT, condition);
                    nodes.Add(operand);
                }
                nodes.Add(new GenTreeUnOp(GT_JTRUE, TYP_VOID, operand));
            }

            block.MakeLir(null, null);
            foreach (var node in nodes)
            {
                block.InsertAtEnd(node);
            }

            Assert.That(compiler.optOptimizePostLayout(), Is.EqualTo(
                reverse ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.TrueEdge, Is.SameAs(reverse ? oldFalse : oldTrue));
            Assert.That(block.FalseEdge, Is.SameAs(reverse ? oldTrue : oldFalse));
            Assert.That(oldTrue.Likelihood, Is.EqualTo(0.25));
            Assert.That(oldFalse.Likelihood, Is.EqualTo(0.75));
            Assert.That(block.GetLastNode(), Is.SameAs(nodes[^1]));
            for (var i = 0; i < nodes.Count; i++)
            {
                Assert.That(nodes[i].Prev, Is.SameAs(i == 0 ? null : nodes[i - 1]));
                Assert.That(nodes[i].Next, Is.SameAs(i + 1 == nodes.Count ? null : nodes[i + 1]));
            }

            if (oper is GT_JCC)
            {
                Assert.That(condition.AsCC().Condition.Code, Is.EqualTo(GenCondition.NE));
            }
            else
            {
                Assert.That(condition.Oper, Is.EqualTo(reverse ? GT_NE : oper));
            }
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_DOUBLE)]
    public static void InPlaceRelopReversalRetainsFloatingUnorderedSemantics(var_types type)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(type, 0), compiler.gtNewLclvNode(type, 1));

            Assert.That(compiler.gtTryReverseCond(comparison), Is.True);
            Assert.That(comparison.Oper, Is.EqualTo(GT_GE));
            Assert.That((comparison.Flags & GTF_RELOP_NAN_UN) != 0, Is.EqualTo(type is TYP_DOUBLE));
            Assert.That(compiler.gtTryReverseCond(comparison), Is.True);
            Assert.That(comparison.Oper, Is.EqualTo(GT_LT));
            Assert.That(comparison.Flags & GTF_RELOP_NAN_UN, Is.EqualTo(GTF_EMPTY));
        });
    }

    [TestCase(0, 1)]
    [TestCase(7, 0)]
    [TestCase(-9, 0)]
    public static void IntegralConditionReversalUsesBooleanNegation(int value, int reversed)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var constant = compiler.gtNewIconNode(TYP_INT, value);

            Assert.That(compiler.gtTryReverseCond(constant), Is.True);
            Assert.That((int)constant.IconValue, Is.EqualTo(reversed));
        });
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        foreach (var block in blocks)
        {
            block.bbRefs = 0;
        }
        blocks[0].bbRefs++;
        for (var i = 1; i < blocks.Length; i++)
        {
            blocks[i - 1].Next = blocks[i];
        }
    }
}
