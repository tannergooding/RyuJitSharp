// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class OSREntryFlowTests
{
    [TestCase(false, false, false, 17)]
    [TestCase(true, false, false, 17)]
    [TestCase(true, true, false, 17)]
    [TestCase(true, true, true, 17)]
    [TestCase(false, true, false, 0)]
    [TestCase(true, false, false, 0)]
    public static void EntryRedirectionPreservesEdgesAndProfile(
        bool profiled, bool originalBackedge, bool sameEntry, double calledCount)
    {
        SsaLivenessTests.WithCompiler(0, compiler =>
        {
            compiler.fgPredsComputed = true;
            compiler.fgPgoConsistent = true;
            compiler.fgCalledCount = calledCount;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var original = BasicBlock.New(compiler, BBJ_RETURN);
            var target = BasicBlock.New(compiler, BBJ_RETURN);
            var tail = BasicBlock.New(compiler, BBJ_RETURN);
            original.Next = target;
            target.Prev = original;
            target.Next = tail;
            tail.Prev = target;
            original.bbRefs = 1;
            compiler.fgFirstBB = original;
            compiler.fgLastBB = tail;
            compiler.fgEntryBB = original;
            compiler.fgOSREntryBB = sameEntry ? original : target;
            original.bbWeight = 1000;
            target.bbWeight = 100;
            tail.bbWeight = 100;
            if (profiled)
            {
                original.setBBProfileWeight(1000);
            }

            var entryEdge = compiler.fgAddRefPred(target, original);
            entryEdge.Likelihood = 1;
            original.SetKindAndTargetEdge(BBJ_ALWAYS, entryEdge);
            var bodyEdge = compiler.fgAddRefPred(tail, target);
            bodyEdge.Likelihood = 1;
            target.SetKindAndTargetEdge(BBJ_ALWAYS, bodyEdge);
            if (originalBackedge)
            {
                var backedge = compiler.fgAddRefPred(original, tail);
                backedge.Likelihood = 1;
                tail.SetKindAndTargetEdge(BBJ_ALWAYS, backedge);
            }

            compiler.fgFixEntryFlowForOSR();

            var first = compiler.fgFirstBB ?? throw new AssertionException("Missing OSR entry block.");
            Assert.That(first, Is.Not.SameAs(original));
            Assert.That(first.Next, Is.SameAs(original));
            Assert.That(original.Prev, Is.SameAs(first));
            Assert.That(first.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(first.TargetEdge.DestinationBlock, Is.SameAs(compiler.fgOSREntryBB));
            Assert.That(first.TargetEdge.SourceBlock, Is.SameAs(first));
            Assert.That(first.bbRefs, Is.EqualTo(1));
            Assert.That(first.bbWeight, Is.EqualTo(calledCount));
            Assert.That(first.hasProfileWeight, Is.EqualTo(profiled));
            Assert.That(first.isRunRarely, Is.EqualTo(calledCount == 0));
            Assert.That(original.bbRefs, Is.EqualTo((originalBackedge ? 1 : 0) + (sameEntry ? 1 : 0)));
            Assert.That(original.TargetEdge, Is.SameAs(entryEdge));
            Assert.That(compiler.fgEntryBB, Is.SameAs(original));
            Assert.That(compiler.fgLastBB, Is.SameAs(tail));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!originalBackedge || sameEntry));
        });
    }
}
