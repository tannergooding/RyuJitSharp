// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class ListExtensionsTests
{
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public static void ExpansionAfterRemovalInitializesNewMembershipEntries(int retainedCount, bool set)
    {
        var list = new List<byte> { 1, 2, 3, 4 };
        if (retainedCount == 0)
        {
            list.Clear();
        }
        else
        {
            list.RemoveRange(retainedCount, list.Count - retainedCount);
        }

        if (set)
        {
            list.ExpandAndSet(3, (byte)9);
        }
        else
        {
            Assert.That(list.ExpandAndGet(3), Is.Zero);
        }

        for (var i = 0; i < list.Count; i++)
        {
            var expected = i < retainedCount ? i + 1 : (set && i == 3) ? 9 : 0;
            Assert.That(list[i], Is.EqualTo(expected), $"Membership entry {i}");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExpansionPreservesExistingEntriesAndDefaultsGaps(bool set)
    {
        var list = new List<byte> { 1, 2 };
        if (set)
        {
            list.ExpandAndSet(8, (byte)9);
        }
        else
        {
            Assert.That(list.ExpandAndGet(8), Is.Zero);
        }

        Assert.That(list, Is.EqualTo(new byte[] { 1, 2, 0, 0, 0, 0, 0, 0, set ? (byte)9 : (byte)0 }));
        Assert.That(list.ExpandAndGet(1), Is.EqualTo(2));
        list.ExpandAndSet(0, (byte)7);
        Assert.That(list.Count, Is.EqualTo(9));
        Assert.That(list[0], Is.EqualTo(7));
    }
}
