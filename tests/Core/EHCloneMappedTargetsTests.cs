// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EHCloneMappedTargetsTests
{
    [TestCase(BBJ_ALWAYS, false)]
    [TestCase(BBJ_ALWAYS, true)]
    [TestCase(BBJ_CALLFINALLY, true)]
    [TestCase(BBJ_CALLFINALLYRET, true)]
    [TestCase(BBJ_LEAVE, true)]
    [TestCase(BBJ_EHCATCHRET, true)]
    [TestCase(BBJ_EHFILTERRET, true)]
    public static void SingleTargetKindsKeepOrRedirectTheOriginalSuccessor(BBKinds kind, bool redirect)
    {
        WithGraph((compiler, source, clone, first, replacement, _) =>
        {
            source.SetKindAndTargetEdge(kind, compiler.fgAddRefPred(first, source));
            var originalEdge = source.TargetEdge;
            var map = new Dictionary<BasicBlock, BasicBlock>();
            if (redirect)
            {
                map.Add(first, replacement);
            }

            compiler.optSetMappedBlockTargets(source, clone, map);

            var target = redirect ? replacement : first;
            Assert.That(clone.Kind, Is.EqualTo(kind));
            Assert.That(clone.Target, Is.SameAs(target));
            Assert.That(clone.TargetEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(source.TargetEdge, Is.SameAs(originalEdge));
            Assert.That(source.Target, Is.SameAs(first));
        });
    }

    [Test]
    public static void ConditionalEdgesPreserveLikelihoodAndRedirectIndependently()
    {
        WithGraph((compiler, source, clone, first, replacement, second) =>
        {
            var originalTrue = compiler.fgAddRefPred(first, source);
            var originalFalse = compiler.fgAddRefPred(second, source);
            originalTrue.Likelihood = 0.8;
            originalFalse.Likelihood = 0.2;
            source.SetCond(originalTrue, originalFalse);

            compiler.optSetMappedBlockTargets(source, clone, new Dictionary<BasicBlock, BasicBlock>
            {
                [first] = replacement,
            });

            Assert.That(clone.TrueTarget, Is.SameAs(replacement));
            Assert.That(clone.FalseTarget, Is.SameAs(second));
            Assert.That(clone.TrueEdge.Likelihood, Is.EqualTo(0.8));
            Assert.That(clone.FalseEdge.Likelihood, Is.EqualTo(0.2));
            Assert.That(source.TrueEdge, Is.SameAs(originalTrue));
            Assert.That(source.FalseEdge, Is.SameAs(originalFalse));
        });
    }

    [Test]
    public static void SharedConditionalSuccessorRemainsOneDuplicatedEdge()
    {
        WithGraph((compiler, source, clone, first, replacement, _) =>
        {
            var shared = compiler.fgAddRefPred(first, source);
            Assert.That(compiler.fgAddRefPred(first, source), Is.SameAs(shared));
            shared.Likelihood = 1.0;
            source.SetCond(shared, shared);

            compiler.optSetMappedBlockTargets(source, clone, new Dictionary<BasicBlock, BasicBlock>
            {
                [first] = replacement,
            });

            Assert.That(clone.TrueEdge, Is.SameAs(clone.FalseEdge));
            Assert.That(clone.TrueEdge.DupCount, Is.EqualTo(2));
            Assert.That(clone.TrueTarget, Is.SameAs(replacement));
            Assert.That(source.TrueEdge, Is.SameAs(shared));
        });
    }

    [Test]
    public static void FinallyReturnDuplicatesItsSuccessorTableWithRedirectedEdges()
    {
        WithGraph((compiler, source, clone, first, replacement, second) =>
        {
            var originalFirst = compiler.fgAddRefPred(first, source);
            var originalSecond = compiler.fgAddRefPred(second, source);
            originalFirst.Likelihood = 0.35;
            originalSecond.Likelihood = 0.65;
            var originalTable = new BBJumpTable([originalFirst, originalSecond]);
            source.SetEhf(originalTable);

            compiler.optSetMappedBlockTargets(source, clone, new Dictionary<BasicBlock, BasicBlock>
            {
                [first] = replacement,
            });

            Assert.That(clone.Kind, Is.EqualTo(BBJ_EHFINALLYRET));
            Assert.That(clone.EhfTargets, Is.Not.SameAs(originalTable));
            Assert.That(clone.EhfTargets!.Succs[0].DestinationBlock, Is.SameAs(replacement));
            Assert.That(clone.EhfTargets.Succs[1].DestinationBlock, Is.SameAs(second));
            Assert.That(clone.EhfTargets.Succs[0].Likelihood, Is.EqualTo(0.35));
            Assert.That(clone.EhfTargets.Succs[1].Likelihood, Is.EqualTo(0.65));
            Assert.That(source.EhfTargets, Is.SameAs(originalTable));
        });
    }

    [Test]
    public static void SwitchPreservesCaseOrderDuplicatesAndOriginalLikelihoods()
    {
        WithGraph((compiler, source, clone, first, replacement, second) =>
        {
            var repeated = compiler.fgAddRefPred(first, source);
            Assert.That(compiler.fgAddRefPred(first, source), Is.SameAs(repeated));
            var distinct = compiler.fgAddRefPred(second, source);
            repeated.Likelihood = 0.7;
            distinct.Likelihood = 0.3;
            var original = new BBswtDesc([repeated, distinct], [0, 1, 2], hasDefault: true);
            original.Cases[0] = repeated;
            original.Cases[1] = repeated;
            original.Cases[2] = distinct;
            source.SwitchTargets = original;

            compiler.optSetMappedBlockTargets(source, clone, new Dictionary<BasicBlock, BasicBlock>
            {
                [first] = replacement,
            });

            var targets = clone.SwitchTargets;
            Assert.That(targets, Is.Not.SameAs(original));
            Assert.That(targets.Cases[0], Is.SameAs(targets.Cases[1]));
            Assert.That(targets.Cases[0].DestinationBlock, Is.SameAs(replacement));
            Assert.That(targets.Cases[0].DupCount, Is.EqualTo(2));
            Assert.That(targets.Cases[2].DestinationBlock, Is.SameAs(second));
            Assert.That(targets.Succs.Length, Is.EqualTo(2));
            Assert.That(targets.Succs[0], Is.SameAs(targets.Cases[0]));
            Assert.That(targets.Succs[1], Is.SameAs(targets.Cases[2]));
            Assert.That(targets.Succs[0].Likelihood, Is.EqualTo(0.7));
            Assert.That(targets.Succs[1].Likelihood, Is.EqualTo(0.3));
            Assert.That(source.SwitchTargets, Is.SameAs(original));
        });
    }

    [TestCase(BBJ_RETURN)]
    [TestCase(BBJ_THROW)]
    [TestCase(BBJ_EHFAULTRET)]
    public static void TerminalKindsDoNotAddSuccessors(BBKinds kind)
    {
        WithGraph((compiler, source, clone, _, _, _) =>
        {
            source.SetKindAndTargetEdge(kind, null);

            compiler.optSetMappedBlockTargets(source, clone, new Dictionary<BasicBlock, BasicBlock>());

            Assert.That(clone.Kind, Is.EqualTo(kind));
            Assert.That(clone.NumSucc, Is.Zero);
        });
    }

    private static void WithGraph(
        Action<Compiler, BasicBlock, BasicBlock, BasicBlock, BasicBlock, BasicBlock> action)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler =>
        {
            var source = BasicBlock.New(compiler, BBJ_ALWAYS);
            var clone = BasicBlock.New(compiler, BBJ_ALWAYS);
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var replacement = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            source.Next = clone;
            clone.Next = first;
            first.Next = replacement;
            replacement.Next = second;
            compiler.fgFirstBB = source;
            compiler.fgLastBB = second;

            action(compiler, source, clone, first, replacement, second);
        });
    }
}
