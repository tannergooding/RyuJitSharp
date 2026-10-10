// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BooleanOptimizationTests
{
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void NonnegativeRangeFoldDoesNotSpeculateOrderedLoads(bool ordered, bool expected)
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewBinaryNode(GT_GE, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0)).AsOp();
            var upper = compiler.gtNewBinaryNode(GT_AND, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, int.MaxValue));
            if (ordered)
            {
                upper.Flags |= GTF_ORDER_SIDEEFF;
            }
            var second = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), upper).AsOp();
            var method = typeof(Compiler).GetMethod("FoldBooleanRangeTests",
                BindingFlags.NonPublic | BindingFlags.Instance) ??
                throw new AssertionException("Missing range fold.");
            Assert.That(method.Invoke(compiler, [first, false, second, false]), Is.EqualTo(expected));
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, true, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    public static void BooleanRangeFoldingPreservesSignedness(
        bool firstUnsigned, bool secondUnsigned, bool expected)
    {
        WithCompiler(compiler =>
        {
            var variable = compiler.gtNewLclvNode(TYP_INT, 0);
            var first = compiler.gtNewBinaryNode(GT_GE, TYP_INT,
                variable, compiler.gtNewIconNode(TYP_INT, 0)).AsOp();
            var secondVariable = compiler.gtNewLclvNode(TYP_INT, 0);
            var second = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                secondVariable, compiler.gtNewIconNode(TYP_INT, 10)).AsOp();
            if (firstUnsigned)
            {
                first.Flags |= GTF_UNSIGNED;
            }

            if (secondUnsigned)
            {
                second.Flags |= GTF_UNSIGNED;
            }

            var oldRight = first.Op2;
            var method = typeof(Compiler).GetMethod("FoldBooleanRangeTests",
                BindingFlags.NonPublic | BindingFlags.Instance) ??
                throw new AssertionException("Missing range fold.");

            Assert.That(method.Invoke(compiler, [first, false, second, false]), Is.EqualTo(expected));
            if (!expected)
            {
                Assert.That(first.Oper, Is.EqualTo(GT_GE));
                Assert.That(first.Op2, Is.SameAs(oldRight));
            }
            else if (!firstUnsigned && secondUnsigned)
            {
                Assert.That(first.Oper, Is.EqualTo(GT_LT));
                Assert.That(first.Op2, Is.SameAs(second.Op2));
                Assert.That(first.IsUnsigned, Is.True);
            }
            else
            {
                Assert.That(first.Oper, Is.EqualTo(GT_LE));
                Assert.That(first.IsUnsigned, Is.True);
            }
        });
    }

    [TestCase(GT_GE, GT_LE, 0L, 100L, true, 0L, 100L)]
    [TestCase(GT_GT, GT_LT, 10L, 20L, true, 11L, 19L)]
    [TestCase(GT_GT, GT_LE, 10L, 11L, false, 11L, 11L)]
    [TestCase(GT_GE, GT_GE, 10L, 20L, false, 0L, 0L)]
    [TestCase(GT_LT, GT_GE, 0L, 0L, false, 0L, -1L)]
    [TestCase(GT_GE, GT_LE, -1L, 20L, false, 0L, 0L)]
    public static void InclusiveRangeIntersectionMatchesNativeNormalization(genTreeOps first,
        genTreeOps second, long firstValue, long secondValue, bool expected, long start, long end)
    {
        var method = typeof(Compiler).GetMethod("GetBooleanRangeIntersection",
            BindingFlags.NonPublic | BindingFlags.Static) ?? throw new AssertionException("Missing range helper.");
        object[] arguments = [TYP_I_IMPL, first, second, (nint)firstValue, (nint)secondValue,
            (nint)0, (nint)0];
        var result = (bool)(method.Invoke(null, arguments) ?? throw new AssertionException("No result."));
        var actualStart = (nint)arguments[5];
        var actualEnd = (nint)arguments[6];
        Assert.That(result, Is.EqualTo(expected));
        Assert.That((long)actualStart, Is.EqualTo(start));
        Assert.That((long)actualEnd, Is.EqualTo(end));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RangeFoldPreservesUnsignedBoundsAndReplacedNodeIdentity(bool swappedFirst)
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
        JitTls.Compiler = compiler;
        try
        {
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 10);
            var first = swappedFirst
                ? compiler.gtNewBinaryNode(GT_GT, TYP_INT, constant, local).AsOp()
                : compiler.gtNewBinaryNode(GT_LT, TYP_INT, local, constant).AsOp();
            var oldRight = first.Op2;
            var second = compiler.gtNewBinaryNode(GT_GT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 20)).AsOp();
            var method = typeof(Compiler).GetMethod("FoldBooleanRangeTests",
                BindingFlags.NonPublic | BindingFlags.Instance) ??
                throw new AssertionException("Missing range fold.");

            Assert.That(method.Invoke(compiler, [first, true, second, true]), Is.EqualTo(true));
            Assert.That(first.Oper, Is.EqualTo(GT_GT));
            Assert.That(first.IsUnsigned, Is.True);
            Assert.That(first.Op1.Oper, Is.EqualTo(GT_SUB));
            Assert.That(first.Op1.AsOp().Op2.IsIntegralConst(10), Is.True);
            Assert.That(first.Op2.IsIntegralConst(10), Is.True);
            Assert.That(first.Op2.Type, Is.EqualTo(TYP_INT));
#if DEBUG
            Assert.That(first.Op2.TreeId, Is.EqualTo(oldRight.TreeId));
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void CompareChainMergesTwoIndependentComparisons()
    {
        WithCompiler(compiler =>
        {
            var first = NewBlock(compiler, BBJ_COND);
            var second = NewBlock(compiler, BBJ_COND);
            var matched = NewBlock(compiler, BBJ_RETURN);
            var unmatched = NewBlock(compiler, BBJ_RETURN);
            first.Next = second;
            second.Next = matched;
            matched.Next = unmatched;
            compiler.fgFirstBB = first;
            compiler.fgLastBB = unmatched;
            first.SetCond(compiler.fgAddRefPred(matched, first), compiler.fgAddRefPred(second, first));
            second.SetCond(compiler.fgAddRefPred(matched, second), compiler.fgAddRefPred(unmatched, second));
            first.TrueEdge.Likelihood = 0.4;
            first.FalseEdge.Likelihood = 0.6;
            second.TrueEdge.Likelihood = 0.5;
            second.FalseEdge.Likelihood = 0.5;
            for (var block = first; block == first || block == second; block = block.Next)
            {
                var firstLocal = block == first ? 0 : 2;
                var condition = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, firstLocal),
                    compiler.gtNewIconNode(TYP_INT, firstLocal + 2));
                var statement = compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition));
                compiler.fgInsertStmtAtEnd(block, statement);
                compiler.gtSetStmtInfo(statement);
                compiler.fgSetStmtSeq(statement);
                condition.SetCosts(7, 7);
            }

            var descriptorType = typeof(Compiler).GetNestedType("OptBoolsDsc",
                BindingFlags.NonPublic) ?? throw new AssertionException("Missing descriptor.");
            var descriptor = Activator.CreateInstance(descriptorType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [first, second, compiler], null) ?? throw new AssertionException("Missing instance.");
            var method = descriptorType.GetMethod("optOptimizeCompareChainCondBlock",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                throw new AssertionException("Missing compare chain.");
            Assert.That(first.LastStmt?.RootNode.AsUnOp().Op1.CostEx, Is.LessThanOrEqualTo(7));
            Assert.That(second.LastStmt?.RootNode.AsUnOp().Op1.CostEx, Is.LessThanOrEqualTo(7));
            Assert.That(method.Invoke(descriptor, null), Is.EqualTo(true));
            Assert.That(first.Kind, Is.EqualTo(BBJ_COND));
            var jump = first.LastStmt?.RootNode.AsUnOp().Op1.AsOp();
            Assert.That(jump?.Oper, Is.EqualTo(GT_NE));
            Assert.That(jump?.Op1.Oper, Is.EqualTo(GT_OR));
            Assert.That(jump?.Op1.AsOp().Op1.Oper, Is.EqualTo(GT_LT));
            Assert.That(jump?.Op1.AsOp().Op2.Oper, Is.EqualTo(GT_LT));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedDestinationCombinesAdjacentNonzeroBranches(bool matchOnFalse)
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
        compiler.fgPredsComputed = true;
        compiler.info.compRetType = TYP_INT;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var first = BasicBlock.New(compiler, BBJ_COND);
            var second = BasicBlock.New(compiler, BBJ_COND);
            var matched = BasicBlock.New(compiler, BBJ_RETURN);
            var unmatched = BasicBlock.New(compiler, BBJ_RETURN);
            first.bbRefs = 0;
            second.bbRefs = 0;
            matched.bbRefs = 0;
            unmatched.bbRefs = 0;
            first.Next = second;
            second.Next = matched;
            matched.Next = unmatched;
            compiler.fgFirstBB = first;
            compiler.fgLastBB = unmatched;

            first.SetCond(compiler.fgAddRefPred(matched, first), compiler.fgAddRefPred(second, first));
            second.SetCond(compiler.fgAddRefPred(matchOnFalse ? unmatched : matched, second),
                compiler.fgAddRefPred(matchOnFalse ? matched : unmatched, second));
            first.TrueEdge.Likelihood = 0.4;
            first.FalseEdge.Likelihood = 0.6;
            second.TrueEdge.Likelihood = 0.5;
            second.FalseEdge.Likelihood = 0.5;
            for (var block = first; block == first || block == second; block = block.Next)
            {
                var local = block == first ? 0 : 1;
                var comparison = (block == second) && matchOnFalse ? GT_EQ : GT_NE;
                var condition = compiler.gtNewBinaryNode(comparison, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, local), compiler.gtNewIconNode(TYP_INT, 0));
                var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
                var statement = compiler.gtNewStmt(jump);
                compiler.fgInsertStmtAtEnd(block, statement);
                compiler.gtSetStmtInfo(statement);
            }

            Assert.That(second.CountOfInEdges, Is.EqualTo(1));
            Assert.That(first.LastStmt?.RootNode.AsUnOp().Op1.AsOp().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(second.LastStmt?.RootNode.AsUnOp().Op1.AsOp().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(second.LastStmt?.RootNode.AsUnOp().Op1.AsOp().Op1.CostEx, Is.LessThanOrEqualTo(12));
            Assert.That(compiler.optOptimizeBools(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(first.LastStmt?.RootNode.AsUnOp().Op1.AsOp().Op1.Oper, Is.EqualTo(GT_OR));
            Assert.That(first.LastStmt?.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(matchOnFalse ? GT_EQ : GT_NE));
            Assert.That(second.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.True);
            Assert.That(first.TrueTarget, Is.SameAs(matchOnFalse ? unmatched : matched));
            Assert.That(first.FalseTarget, Is.SameAs(matchOnFalse ? matched : unmatched));
            Assert.That(first.TrueEdge.Likelihood, Is.EqualTo(matchOnFalse ? 0.3 : 0.7).Within(0.00001));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void PhaseWithoutConditionalBlocksLeavesGraphUnchanged()
    {
        WithCompiler(compiler =>
        {
            var block = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            Assert.That(compiler.optOptimizeBools(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgFirstBB, Is.SameAs(block));
            Assert.That(compiler.fgLastBB, Is.SameAs(block));
            Assert.That(block.Kind, Is.EqualTo(BBJ_RETURN));
        });
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
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 4;
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        compiler.fgNodeThreading = NodeThreading.AllTrees;
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
