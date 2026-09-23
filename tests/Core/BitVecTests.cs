// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class BitVecTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(63)]
    [TestCase(64)]
    [TestCase(65)]
    [TestCase(127)]
    [TestCase(128)]
    [TestCase(192)]
    [TestCase(255)]
    [TestCase(256)]
    public static void FullSetContainsEveryValidBitAndNoPadding(int size)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var traits = new BitVecTraits(compiler, size);
        var full = BitSetOps<BitVecTraits, BitVecTraits>.MakeFull(traits);
        Assert.That((int)BitSetOps<BitVecTraits, BitVecTraits>.Count(traits, full), Is.EqualTo(size));

        for (var index = 0; index < size; index++)
        {
            Assert.That(BitSetOps<BitVecTraits, BitVecTraits>.IsMember(traits, full, index), Is.True);
        }

        var visited = 0;
        Assert.That(BitSetOps<BitVecTraits, BitVecTraits>.VisitBits(traits, full, index => index == visited++), Is.True);
        Assert.That(visited, Is.EqualTo(size));
    }
}
