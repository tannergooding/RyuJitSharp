// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;

namespace RyuJitSharp.UnitTests;

internal static class BasicBlockHashTests
{
    [TestCase(BBF_SPLIT_LOST)]
    [TestCase(BBF_SPLIT_GAINED)]
    public static void SplitFlagsExcludeFlagsForbiddenBeforeSplitting(BasicBlockFlags splitFlags)
    {
        Assert.That(BBF_SPLIT_NONEXIST & splitFlags, Is.EqualTo((BasicBlockFlags)0));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(42)]
    public static void HashCodeUsesStableBlockId(int blockId)
    {
        var block = new BasicBlock(null, null) {
            bbID = blockId
        };

        Assert.That(block.GetHashCode(), Is.EqualTo(blockId));
    }

    [Test]
    public static void EqualityUsesReferenceIdentity()
    {
        var block = new BasicBlock(null, null) {
            bbID = 1
        };
        var other = new BasicBlock(null, null) {
            bbID = 1
        };

        Assert.That(block.Equals(block), Is.True);
        Assert.That(block.Equals(other), Is.False);
    }

    [Test]
    public static void RawTargetAccessorsReturnNullAndPopulatedDestinations()
    {
        var target = new BasicBlock(null, null);
        var block = new BasicBlock(null, null);
        block.bbTargetEdge = new FlowEdge(block, target, null);
        block.Kind = BBKinds.BBJ_ALWAYS;

        Assert.That(block.GetTargetRaw(), Is.SameAs(target));
        block.bbTargetEdge = null;
        Assert.That(block.GetTargetRaw(), Is.Null);

        var trueTarget = new BasicBlock(null, null);
        var falseTarget = new BasicBlock(null, null);
        var conditional = new BasicBlock(null, null);
        conditional.SetCond(
            new FlowEdge(conditional, trueTarget, null),
            new FlowEdge(conditional, falseTarget, null));

        Assert.That(conditional.GetTrueTargetRaw(), Is.SameAs(trueTarget));
        Assert.That(conditional.GetFalseTargetRaw(), Is.SameAs(falseTarget));

        conditional.bbTrueEdge = null;
        conditional.bbFalseEdge = null;

        Assert.That(conditional.GetTrueTargetRaw(), Is.Null);
        Assert.That(conditional.GetFalseTargetRaw(), Is.Null);
    }
}
