// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlowGraphBranchThreadingTests
{
    [TestCase(BBJ_ALWAYS, false)]
    [TestCase(BBJ_ALWAYS, true)]
    [TestCase(BBJ_CALLFINALLYRET, false)]
    public static void ThreadsUnconditionalBranchesAndPreservesNativeSideEffects(BBKinds sourceKind, bool lir)
    {
        WithCompiler(compiler => {
            var (source, empty, target) = CreateLinearGraph(compiler, lir, sourceKind);
            source.bbWeight = 10;
            empty.setBBProfileWeight(30);
            empty.SetFlags(BBF_ASYNC_RESUMPTION);

            Assert.That(OptimizeBranchToEmptyUnconditional(compiler, source, empty), Is.True);

            Assert.Multiple(() => {
                Assert.That(source.Target, Is.SameAs(target));
                Assert.That(empty.bbRefs, Is.Zero);
                Assert.That(target.bbRefs, Is.EqualTo(2));
                Assert.That(source.HasFlag(BBF_ASYNC_RESUMPTION), Is.True);
                Assert.That(empty.bbWeight, Is.EqualTo(20));
                Assert.That(empty.hasProfileWeight, Is.True);
            });

            if (sourceKind is BBJ_CALLFINALLYRET)
            {
                Assert.That(source.Kind, Is.EqualTo(BBJ_CALLFINALLYRET));
            }
        });
    }

    [Test]
    public static void ThreadsConditionalEdgeAndReducesDestinationByItsLikelyWeight()
    {
        WithCompiler(compiler => {
            var (source, empty, target, otherTarget) = CreateConditionalGraph(compiler, lir: false, merge: false);
            source.bbWeight = 20;
            source.TrueEdge.Likelihood = 0.25;
            source.FalseEdge.Likelihood = 0.75;
            empty.setBBProfileWeight(40);

            Assert.That(OptimizeBranchToEmptyUnconditional(compiler, source, empty), Is.True);

            Assert.Multiple(() => {
                Assert.That(source.TrueTarget, Is.SameAs(target));
                Assert.That(source.FalseTarget, Is.SameAs(otherTarget));
                Assert.That(empty.bbWeight, Is.EqualTo(35));
                Assert.That(target.bbRefs, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public static void ClampsAnInconsistentDestinationWeightAndInvalidatesProfileConsistency()
    {
        WithCompiler(compiler => {
            var (source, empty, _) = CreateLinearGraph(compiler, lir: false);
            source.bbWeight = 20;
            empty.setBBProfileWeight(5);

            Assert.That(OptimizeBranchToEmptyUnconditional(compiler, source, empty), Is.True);

            Assert.Multiple(() => {
                Assert.That(empty.bbWeight, Is.Zero);
                Assert.That(compiler.fgPgoConsistent, Is.False);
            });
        });
    }

    [Test]
    public static void MergingConditionalEdgesRestoresUnitLikelihood()
    {
        WithCompiler(compiler => {
            var (source, empty, target, _) = CreateConditionalGraph(compiler, lir: true, merge: true);
            source.bbWeight = 20;
            source.TrueEdge.Likelihood = 0.25;
            source.FalseEdge.Likelihood = 0.75;

            Assert.That(OptimizeBranchToEmptyUnconditional(compiler, source, empty), Is.True);

            Assert.Multiple(() => {
                Assert.That(source.TrueEdge, Is.SameAs(source.FalseEdge));
                Assert.That(source.TrueEdge.Likelihood, Is.EqualTo(1.0));
                Assert.That(empty.bbRefs, Is.Zero);
                Assert.That(target.bbRefs, Is.EqualTo(3));
            });
        });
    }

    [TestCase("empty-cycle")]
    [TestCase("different-try")]
    [TestCase("removed-target")]
    [TestCase("cloned-finally")]
    public static void PreservesBranchesThatMustNotBeThreaded(string reason)
    {
        WithCompiler(compiler => {
            var (source, empty, target) = CreateLinearGraph(compiler, lir: false);

            switch (reason)
            {
                case "empty-cycle":
                {
                    var cycle = NewBlock(compiler);
                    target.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(empty, target));
                    empty.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(cycle, empty));
                    cycle.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(empty, cycle));
                    target.Next = cycle;
                    compiler.fgLastBB = cycle;
                    break;
                }

                case "different-try":
                    empty.TryIndex = 0;
                    break;

                case "removed-target":
                    target.SetFlags(BBF_REMOVED);
                    break;

                case "cloned-finally":
                    empty.SetFlags(BBF_CLONED_FINALLY_BEGIN);
                    break;
            }

            var originalTarget = source.Target;
            Assert.That(OptimizeBranchToEmptyUnconditional(compiler, source, empty), Is.False);
            Assert.That(source.Target, Is.SameAs(originalTarget));
        });
    }

    [Test]
    public static void RemovedIntermediateBlockOverridesOtherRefusalChecks()
    {
        WithCompiler(compiler => {
            var (source, empty, target) = CreateLinearGraph(compiler, lir: false);
            empty.SetFlags(BBF_REMOVED | BBF_CLONED_FINALLY_BEGIN);
            empty.TryIndex = 0;
            target.SetFlags(BBF_REMOVED);

            Assert.That(OptimizeBranchToEmptyUnconditional(compiler, source, empty), Is.True);
            Assert.That(source.Target, Is.SameAs(target));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgOptimizeBranchToEmptyUnconditional")]
    private static extern bool OptimizeBranchToEmptyUnconditional(Compiler compiler, BasicBlock block, BasicBlock destination);

    private static (BasicBlock Source, BasicBlock Empty, BasicBlock Target) CreateLinearGraph(
        Compiler compiler, bool lir, BBKinds sourceKind = BBJ_ALWAYS)
    {
        var source = NewBlock(compiler);
        var empty = NewBlock(compiler);
        var target = NewBlock(compiler);
        LinkBlocks(compiler, source, empty, target);

        source.SetKindAndTargetEdge(sourceKind, compiler.fgAddRefPred(empty, source));
        empty.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, empty));

        if (lir)
        {
            source.MakeLir(null, null);
            empty.MakeLir(null, null);
            target.MakeLir(null, null);
        }

        return (source, empty, target);
    }

    private static (BasicBlock Source, BasicBlock Empty, BasicBlock Target, BasicBlock OtherTarget) CreateConditionalGraph(
        Compiler compiler, bool lir, bool merge)
    {
        var source = NewBlock(compiler);
        var empty = NewBlock(compiler);
        var target = NewBlock(compiler);
        var otherTarget = merge ? target : NewBlock(compiler);
        if (merge)
        {
            LinkBlocks(compiler, source, empty, target);
        }
        else
        {
            LinkBlocks(compiler, source, empty, target, otherTarget);
        }

        var trueEdge = compiler.fgAddRefPred(empty, source);
        var falseEdge = compiler.fgAddRefPred(otherTarget, source);
        trueEdge.Likelihood = 0.25;
        falseEdge.Likelihood = 0.75;
        source.SetCond(trueEdge, falseEdge);
        empty.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, empty));

        if (lir)
        {
            source.MakeLir(null, null);
            empty.MakeLir(null, null);
            target.MakeLir(null, null);
            if (!merge)
            {
                otherTarget.MakeLir(null, null);
            }
        }

        return (source, empty, target, otherTarget);
    }

    private static BasicBlock NewBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.bbRefs = 0;
        return block;
    }

    private static void LinkBlocks(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        for (var index = 0; index < blocks.Length - 1; index++)
        {
            blocks[index].Next = blocks[index + 1];
        }

        blocks[0].bbRefs = 1;
        compiler.fgPredsComputed = true;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPgoConsistent = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
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
}
