// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.MemoryKind;

namespace RyuJitSharp.UnitTests;

internal static class MemoryKindEnumerationTests
{
    [Test]
    public static void PatternEnumerationIncludesBothKindsInNativeOrder()
    {
        var actual = new List<MemoryKind>();
        foreach (var kind in new AllMemoryKinds())
        {
            actual.Add(kind);
        }

        Assert.That(actual, Is.EqualTo((MemoryKind[])[ByrefExposed, GcHeap]));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InterfaceEnumerationIncludesBothKindsInNativeOrder(bool generic)
    {
        var kinds = new AllMemoryKinds();
        var enumerator = generic
            ? ((IEnumerable<MemoryKind>)kinds).GetEnumerator()
            : ((IEnumerable)kinds).GetEnumerator();
        var actual = new List<MemoryKind>();
        while (enumerator.MoveNext())
        {
            actual.Add((MemoryKind)enumerator.Current);
        }

        Assert.That(actual, Is.EqualTo((MemoryKind[])[ByrefExposed, GcHeap]));
    }

    [Test]
    public static void ResetRestartsTheCompleteSequence()
    {
        var enumerator = new AllMemoryKinds().GetEnumerator();
        for (var iteration = 0; iteration < 2; iteration++)
        {
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(ByrefExposed));
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(GcHeap));
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(enumerator.MoveNext(), Is.False);
            enumerator.Reset();
        }
    }
}
