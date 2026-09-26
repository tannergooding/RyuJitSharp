// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SwitchRecognitionDetectionTests
{
    [TestCase(4, false)]
    [TestCase(5, true)]
    [TestCase(6, true)]
    public static void DetectionPreservesThresholdAndTrackedChain(int count, bool expected)
    {
        WithCompiler(compiler =>
        {
            var first = CreateChain(compiler, count, 0, GT_EQ);
            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var visited = BitVecOps.MakeEmpty(traits);
            Assert.That(Detect(compiler, first, visited), Is.EqualTo(expected));
            var block = first;
            for (var index = 0; index < count; index++)
            {
                Assert.That(BitVecOps.IsMember(traits, visited, block.bbNum), Is.EqualTo(expected));
                block = block.FalseTarget;
            }

            if (expected)
            {
                Assert.That(Detect(compiler, first, visited), Is.True);
                Assert.That(BitVecOps.IsMember(traits, visited, first.bbNum), Is.False);
                Assert.That(BitVecOps.IsMember(traits, visited, first.FalseTarget.bbNum), Is.True);
            }
        });
    }

    [TestCase(-1, GT_EQ)]
    [TestCase(0, GT_NE)]
    public static void FirstTestMustBeSupported(int initial, genTreeOps oper)
    {
        WithCompiler(compiler =>
        {
            var first = CreateChain(compiler, 5, initial, oper);
            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var visited = BitVecOps.MakeEmpty(traits);
            Assert.That(Detect(compiler, first, visited), Is.False);
            Assert.That(BitVecOps.IsEmpty(traits, visited), Is.True);
        });
    }

    [Test]
    public static void IneligibleSixthTestStillTracksFirstFive()
    {
        WithCompiler(compiler =>
        {
            var first = CreateChain(compiler, 6, 0, GT_EQ);
            var sixth = first;
            for (var index = 0; index < 5; index++)
            {
                sixth = sixth.FalseTarget;
            }

            sixth.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Op1 = compiler.gtNewLclvNode(TYP_INT, 1);
            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var visited = BitVecOps.MakeEmpty(traits);
            BitVecOps.AddElemD(traits, visited, sixth.bbNum);
            Assert.That(Detect(compiler, first, visited), Is.True);
            Assert.That(BitVecOps.IsMember(traits, visited, sixth.bbNum), Is.False);
            Assert.That(BitVecOps.IsMember(traits, visited, first.bbNum), Is.True);
        });
    }

    [Test]
    public static void LastTestMayReverseItsBranches()
    {
        WithCompiler(compiler =>
        {
            var first = CreateChain(compiler, 5, 0, GT_EQ);
            var last = first;
            for (var index = 0; index < 4; index++)
            {
                last = last.FalseTarget;
            }

            last.LastStmt!.RootNode.AsUnOp().Op1.AsOp().SetOper(GT_NE);
            var oldTrue = last.TrueEdge;
            last.TrueEdgeRef = last.FalseEdge;
            last.FalseEdgeRef = oldTrue;
            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var visited = BitVecOps.MakeEmpty(traits);
            Assert.That(Detect(compiler, first, visited), Is.True);
            Assert.That(BitVecOps.IsMember(traits, visited, last.bbNum), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FallthroughTargetsAreSkippedUnlessTheyCycle(bool cycle)
    {
        WithCompiler(compiler =>
        {
            var first = CreateChain(compiler, 5, 0, GT_EQ);
            var matched = first.TrueTarget;
            var proxy = BasicBlock.New(compiler, BBJ_ALWAYS);
            proxy.bbRefs = 0;
            proxy.Next = compiler.fgLastBB;
            matched.Next = proxy;
            proxy.SetKindAndTargetEdge(BBJ_ALWAYS,
                compiler.fgAddRefPred(cycle ? proxy : matched, proxy));
            compiler.fgRedirectEdge(ref first.TrueEdgeRef, proxy);
            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var visited = BitVecOps.MakeEmpty(traits);
            Assert.That(Detect(compiler, first, visited), Is.EqualTo(!cycle));
        });
    }

    [TestCase(0, 0)]
    [TestCase(1, 2)]
    public static void DetectionPreservesNativeNormalizationAllocation(int firstValue, int expectedTrees)
    {
        WithCompiler(compiler =>
        {
            var first = CreateChain(compiler, 5, firstValue, GT_EQ);
#if DEBUG
            var before = compiler.compGenTreeID;
#endif
            var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
            var visited = BitVecOps.MakeEmpty(traits);
            Assert.That(Detect(compiler, first, visited), Is.True);
#if DEBUG
            Assert.That(compiler.compGenTreeID - before, Is.EqualTo(expectedTrees));
#endif
        });
    }

    private static BasicBlock CreateChain(Compiler compiler, int count, int initial, genTreeOps firstOp)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            blocks[index] = BasicBlock.New(compiler, BBJ_COND);
            blocks[index].bbRefs = 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }

        var matched = BasicBlock.New(compiler, BBJ_RETURN);
        var unmatched = BasicBlock.New(compiler, BBJ_RETURN);
        blocks[^1].Next = matched;
        matched.Next = unmatched;
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = unmatched;

        for (var index = 0; index < count; index++)
        {
            var current = blocks[index];
            current.SetCond(compiler.fgAddRefPred(matched, current),
                compiler.fgAddRefPred(index + 1 < count ? blocks[index + 1] : unmatched, current));
            current.TrueEdge.Likelihood = 0.25;
            current.FalseEdge.Likelihood = 0.75;
            var condition = compiler.gtNewBinaryNode(index == 0 ? firstOp : GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewIconNode(TYP_INT, initial + index));
            compiler.fgInsertStmtAtEnd(current, compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition)));
        }

        return blocks[0];
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
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 2;
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optSwitchDetectForCcmp")]
    private static extern bool Detect(Compiler compiler, BasicBlock first, nint[] visited);
}
