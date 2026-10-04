// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class BasicBlockHashTests
{
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
}
