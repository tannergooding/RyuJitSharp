// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SwitchRecognitionTests
{
    [TestCase(new int[] { 0, 1 }, false)]
    [TestCase(new int[] { 0, 1, 2 }, true)]
    [TestCase(new int[] { 1, 2, 3, 4, 5 }, true)]
    [TestCase(new int[] { 1, 3, 5 }, true)]
    [TestCase(new int[] { 0, 0, 2 }, true)]
    [TestCase(new int[] { -1, 0, 1 }, false)]
    [TestCase(new int[] { 0, -1, 2, 3 }, false)]
    public static void ConvertsWholeEligiblePrefixAndPreservesCaseMapping(int[] values, bool expected)
    {
        WithCompiler(compiler =>
        {
            var (first, matched, unmatched) = CreateChain(compiler, values);
#if DEBUG
            var originalId = first.LastStmt!.RootNode.TreeId;
#endif
            Assert.That(Detect(compiler, first), Is.EqualTo(expected));
            if (!expected)
            {
                Assert.That(first.Kind, Is.EqualTo(BBJ_COND));
                Assert.That(first.LastStmt?.RootNode.Oper, Is.EqualTo(GT_JTRUE));
                return;
            }

            var low = values.Min();
            var high = values.Max();
            var isNormalized = low > 0 && ((values.Length == high - low + 1) ||
                (high > 62));
            var lowerBound = isNormalized ? low : 0;
            var cases = first.SwitchTargets.Cases;
            Assert.That(first.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(cases.Length, Is.EqualTo(high - lowerBound + 2));
            for (var index = 0; index < cases.Length - 1; index++)
            {
                var present = values.Contains(index + lowerBound);
                Assert.That(cases[index].DestinationBlock, Is.SameAs(present ? matched : unmatched));
            }

            Assert.That(cases[^1].DestinationBlock, Is.SameAs(unmatched));
            Assert.That(first.SwitchTargets.Succs.Length, Is.EqualTo(2));
            Assert.That(first.LastStmt?.RootNode.Oper, Is.EqualTo(GT_SWITCH));
            Assert.That(first.LastStmt?.RootNode.AsUnOp().Op1.Oper,
                Is.EqualTo(isNormalized ? GT_ADD : GT_LCL_VAR));
#if DEBUG
            Assert.That(first.LastStmt?.RootNode.TreeId, Is.EqualTo(originalId));
#endif
            Assert.That(compiler.fgHasSwitch, Is.True);
            Assert.That(compiler.opts.compProcedureSplitting, Is.False);
            var falseLikelihood = Math.Pow(0.75, values.Length);
            Assert.That(cases[^1].Likelihood, Is.EqualTo(falseLikelihood).Within(0.00001));
        });
    }

    [Test]
    public static void TruncatedChainKeepsUnconvertedTailAndUsesSurvivingBounds()
    {
        WithCompiler(compiler =>
        {
            var (first, matched, _) = CreateChain(compiler, [0, 2, 4, 100, 101]);
            var tail = first.FalseTarget.FalseTarget.FalseTarget;
            Assert.That(Detect(compiler, first), Is.True);
            Assert.That(first.SwitchTargets.Cases.Length, Is.EqualTo(6));
            Assert.That(first.SwitchTargets.Cases[4].DestinationBlock, Is.SameAs(matched));
            Assert.That(tail.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(tail.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.False);
            Assert.That(first.SwitchTargets.DefaultCase.DestinationBlock, Is.SameAs(tail));
        });
    }

    [Test]
    public static void DifferentVariableStopsAtFirstUnmatchedTest()
    {
        WithCompiler(compiler =>
        {
            var (first, _, _) = CreateChain(compiler, [0, 1, 2, 3]);
            var fourth = first.FalseTarget.FalseTarget.FalseTarget;
            fourth.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Op1 = compiler.gtNewLclvNode(TYP_INT, 1);
            Assert.That(Detect(compiler, first), Is.True);
            Assert.That(first.SwitchTargets.Cases.Length, Is.EqualTo(4));
            Assert.That(first.SwitchTargets.DefaultCase.DestinationBlock, Is.SameAs(fourth));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConstantMayPrecedeTheVariable(bool swapEveryTest)
    {
        WithCompiler(compiler =>
        {
            var (first, _, _) = CreateChain(compiler, [0, 1, 2]);
            for (var block = first; block.Kind is BBJ_COND; block = block.FalseTarget)
            {
                if ((block != first) && !swapEveryTest)
                {
                    continue;
                }

                var comparison = block.LastStmt!.RootNode.AsUnOp().Op1.AsOp();
                var originalLeft = comparison.Op1;
                comparison.Op1 = comparison.Op2;
                comparison.Op2 = originalLeft;
            }

            Assert.That(Detect(compiler, first), Is.True);
            Assert.That(first.LastStmt?.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReversedTestsAreOnlyEligibleAtTheEnd(bool reverseFirst)
    {
        WithCompiler(compiler =>
        {
            var (first, matched, _) = CreateChain(compiler, [0, 1, 2]);
            var reversed = reverseFirst ? first : first.FalseTarget.FalseTarget;
            reversed.LastStmt!.RootNode.AsUnOp().Op1.AsOp().SetOper(GT_NE);
            var oldTrue = reversed.TrueEdge;
            reversed.TrueEdgeRef = reversed.FalseEdge;
            reversed.FalseEdgeRef = oldTrue;
            Assert.That(Detect(compiler, first), Is.EqualTo(!reverseFirst));
            if (!reverseFirst)
            {
                Assert.That(first.SwitchTargets.Cases[2].DestinationBlock, Is.SameAs(matched));
            }
        });
    }

    [Test]
    public static void ConditionalSideEffectsStayInTheConvertedSwitchValue()
    {
        WithCompiler(compiler =>
        {
            var (first, _, _) = CreateChain(compiler, [0, 1, 2]);
            var stmt = first.LastStmt ?? throw new AssertionException("Missing first test.");
            var comparison = stmt.RootNode.AsUnOp().Op1.AsOp();
            var store = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 7));
            comparison.Op1 = compiler.gtNewCommaNode(TYP_INT, store, comparison.Op1);
            compiler.gtSetStmtInfo(stmt);
            compiler.fgSetStmtSeq(stmt);

            Assert.That(Detect(compiler, first), Is.True);
            var root = first.LastStmt?.RootNode;
            Assert.That(root?.AsUnOp().Op1.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(root?.AsUnOp().Op1.AsOp().Op1, Is.SameAs(store));
            Assert.That((root?.Flags & GTF_ASG) != 0, Is.True);
        });
    }

    [Test]
    public static void OtherEhRegionStopsBeforeRemovingItsFirstBlock()
    {
        WithCompiler(compiler =>
        {
            var (first, _, _) = CreateChain(compiler, [0, 1, 2, 3]);
            var fourth = first.FalseTarget.FalseTarget.FalseTarget;
            fourth.TryIndex = 0;
            Assert.That(Detect(compiler, first), Is.True);
            Assert.That(first.SwitchTargets.DefaultCase.DestinationBlock, Is.SameAs(fourth));
            Assert.That(fourth.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.False);
            Assert.That(fourth.TryIndex, Is.EqualTo(0));
        });
    }

    [Test]
    public static void AllCasesWithOneDestinationKeepOneUniqueSuccessor()
    {
        WithCompiler(compiler =>
        {
            var (first, matched, _) = CreateChain(compiler, [0, 1, 2]);
            var last = first.FalseTarget.FalseTarget;
            compiler.fgRedirectEdge(ref last.FalseEdgeRef, matched);
            Assert.That(Detect(compiler, first), Is.True);
            Assert.That(first.SwitchTargets.Succs.Length, Is.EqualTo(1));
            Assert.That(first.SwitchTargets.Cases.ToArray().All(edge => edge.DestinationBlock == matched), Is.True);
            Assert.That(first.SwitchTargets.Succs[0].DupCount, Is.EqualTo(4));
        });
    }

    [Test]
    public static void ConversionSkipsRarelyRunHead()
    {
        WithCompiler(compiler =>
        {
            var (first, _, _) = CreateChain(compiler, [0, 1, 2]);
            first.bbSetRunRarely();
            Assert.That(compiler.optRecognizeAndOptimizeSwitchJumps(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(first.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [Test]
    public static void PhaseConvertsMatchingChain()
    {
        WithCompiler(compiler =>
        {
            var (first, _, _) = CreateChain(compiler, [0, 1, 2]);
            Assert.That(compiler.optRecognizeAndOptimizeSwitchJumps(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(first.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(first.SwitchTargets.HasDominantCase, Is.False);
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_I_IMPL)]
    public static void MaximumConstantNormalizesAtItsSignedWidth(var_types type)
    {
        WithCompiler(compiler =>
        {
            compiler.lvaTable[0].Type = type;
            var max = type is TYP_INT ? (nint)int.MaxValue : nint.MaxValue;
            var (first, _, _) = CreateChain(compiler, [max - 2, max - 1, max], type);
            Assert.That(Detect(compiler, first), Is.True);
            Assert.That(first.SwitchTargets.Cases.Length, Is.EqualTo(4));
            var offset = first.LastStmt?.RootNode.AsUnOp().Op1.AsOp();
            Assert.That(offset?.Oper, Is.EqualTo(GT_ADD));
            Assert.That(offset?.Type, Is.EqualTo(type));
            Assert.That(offset?.Op2.AsIntCon().IconValue, Is.EqualTo(-(max - 2)));
        });
    }

    [TestCase(0.8, false)]
    [TestCase(1.0, false)]
    [TestCase(0.8, true)]
    public static void DominantCasePeelingPreservesSwitchAndRenormalizesProfile(double fraction, bool complexValue)
    {
        WithCompiler(compiler =>
        {
            var block = NewBlock(compiler, BBJ_SWITCH);
            var first = NewBlock(compiler, BBJ_RETURN);
            var dominant = NewBlock(compiler, BBJ_RETURN);
            var fallback = NewBlock(compiler, BBJ_RETURN);
            block.Next = first;
            first.Next = dominant;
            dominant.Next = fallback;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = fallback;
            block.setBBProfileWeight(100);

            var edges = new[] {
                compiler.fgAddRefPred(first, block),
                compiler.fgAddRefPred(dominant, block),
                compiler.fgAddRefPred(fallback, block),
            };
            edges[0].Likelihood = (1.0 - fraction) / 2;
            edges[1].Likelihood = fraction;
            edges[2].Likelihood = (1.0 - fraction) / 2;
            var targets = new BBswtDesc(edges, new int[3], hasDefault: true, dominantCase: 1);
            edges.CopyTo(targets.Cases);
            block.SwitchTargets = targets;
            GenTree value = compiler.gtNewLclvNode(TYP_INT, 0);
            if (complexValue)
            {
                value = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, value,
                    compiler.gtNewIconNode(TYP_INT, 5));
            }

            var switchStmt = compiler.gtNewStmt(new GenTreeUnOp(GT_SWITCH, TYP_VOID, value));
            compiler.fgInsertStmtAtEnd(block, switchStmt);
            compiler.gtSetStmtInfo(switchStmt);
            compiler.fgSetStmtSeq(switchStmt);

            Assert.That(compiler.optRecognizeAndOptimizeSwitchJumps(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var newBlock = block.Next ?? throw new AssertionException("Missing split switch.");
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(block.TrueTarget, Is.SameAs(dominant));
            Assert.That(block.FalseTarget, Is.SameAs(newBlock));
            Assert.That(block.LastStmt?.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_EQ));
            Assert.That(newBlock.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(newBlock.SwitchTargets.HasDominantCase, Is.False);
            Assert.That(newBlock.LastStmt, Is.SameAs(switchStmt));
            Assert.That(newBlock.bbWeight, Is.EqualTo(100 * (1.0 - fraction)).Within(0.00001));
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(fraction).Within(0.00001));
            Assert.That(block.FalseEdge.Likelihood, Is.EqualTo(1.0 - fraction).Within(0.00001));
            var expected = fraction == 1.0 ? 1.0 / 3 : 0.5;
            Assert.That(newBlock.SwitchTargets.Cases[0].Likelihood, Is.EqualTo(expected).Within(0.00001));
            Assert.That(newBlock.SwitchTargets.Cases[1].Likelihood,
                Is.EqualTo(fraction == 1.0 ? expected : 0).Within(0.00001));
            Assert.That(newBlock.SwitchTargets.Cases[2].Likelihood, Is.EqualTo(expected).Within(0.00001));
            if (complexValue)
            {
                var compare = block.LastStmt!.RootNode.AsUnOp().Op1.AsOp();
                var comma = compare.Op1.Oper is GT_COMMA ? compare.Op1 : compare.Op2;
                Assert.That(comma.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(comma.AsOp().Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(comma.AsOp().Op1.AsLclVar().Data, Is.SameAs(value));
                Assert.That(newBlock.LastStmt?.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            }
        });
    }

    private static (BasicBlock First, BasicBlock Matched, BasicBlock Unmatched)
        CreateChain(Compiler compiler, int[] values, var_types type = TYP_INT) =>
        CreateChain(compiler, values.Select(static value => (nint)value).ToArray(), type);

    private static (BasicBlock First, BasicBlock Matched, BasicBlock Unmatched)
        CreateChain(Compiler compiler, nint[] values, var_types type)
    {
        var blocks = new BasicBlock[values.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = NewBlock(compiler, BBJ_COND);
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }

        var matched = NewBlock(compiler, BBJ_RETURN);
        var unmatched = NewBlock(compiler, BBJ_RETURN);
        blocks[^1].Next = matched;
        matched.Next = unmatched;
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = unmatched;
        for (var index = 0; index < blocks.Length; index++)
        {
            var block = blocks[index];
            block.SetCond(compiler.fgAddRefPred(matched, block),
                compiler.fgAddRefPred(index + 1 == blocks.Length ? unmatched : blocks[index + 1], block));
            block.TrueEdge.Likelihood = 0.25;
            block.FalseEdge.Likelihood = 0.75;
            var compare = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(type, 0), compiler.gtNewIconNode(type, values[index]));
            var statement = compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, compare));
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.gtSetStmtInfo(statement);
            compiler.fgSetStmtSeq(statement);
        }

        return (blocks[0], matched, unmatched);
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        return block;
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
        compiler.opts.compProcedureSplitting = true;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 2;
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optSwitchDetectForConversion")]
    private static extern bool Detect(Compiler compiler, BasicBlock block);

}
