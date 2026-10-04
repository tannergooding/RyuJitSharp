// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using NUnit.Framework;

using TestOps = RyuJitSharp.BitSetUint64Ops<
    RyuJitSharp.UnitTests.BitSetUint64TestEnvironment,
    RyuJitSharp.UnitTests.BitSetUint64TestTraits>;
using TestRawOps = RyuJitSharp.BitSetUInt64Ops<
    RyuJitSharp.UnitTests.BitSetUint64TestEnvironment,
    RyuJitSharp.UnitTests.BitSetUint64TestTraits>;
using TestBitSet = RyuJitSharp.BitSetUint64<
    RyuJitSharp.UnitTests.BitSetUint64TestEnvironment,
    RyuJitSharp.UnitTests.BitSetUint64TestTraits>;

namespace RyuJitSharp.UnitTests;

internal static class BitSetUint64Tests
{
    [TestCase(0, 0UL)]
    [TestCase(1, 1UL)]
    [TestCase(63, 0x7FFFFFFFFFFFFFFFUL)]
    [TestCase(64, ulong.MaxValue)]
    public static void MakeFullUsesTheConfiguredWidth(int size, ulong expected)
    {
        var env = new BitSetUint64TestEnvironment(size);
        var full = (TestBitSet)TestOps.MakeFull(env);
        var rawFull = TestRawOps.MakeFull(env);

        Assert.That(full.Bits, Is.EqualTo(expected));
        Assert.That(rawFull, Is.EqualTo(expected));
        Assert.That(TestOps.Count(env, full), Is.EqualTo((uint)size));
        Assert.That(TestRawOps.Count(env, rawFull), Is.EqualTo((uint)size));
        Assert.That(TestOps.IsEmpty(env, full), Is.EqualTo(size == 0));
    }

    [Test]
    public static void OperationsPreserveValueAndMutableContracts()
    {
        var env = new BitSetUint64TestEnvironment(64);
        var empty = (TestBitSet)TestOps.MakeEmpty(env);
#if DEBUG
        Assert.That(TestOps.MayBeUninit(empty), Is.False);
#else
        Assert.That(TestOps.MayBeUninit(empty), Is.True);
#endif
        Assert.That(TestOps.MayBeUninit(TestOps.UninitVal()), Is.True);

        var first = (TestBitSet)TestOps.MakeEmpty(env);
        TestOps.AddElemD(env, ref first, 0);
        TestOps.AddElemD(env, ref first, 10);
        TestOps.AddElemD(env, ref first, 44);
        TestOps.AddElemD(env, ref first, 45);
        Assert.That(TestOps.Count(env, first), Is.EqualTo(4));
        Assert.That(TestOps.IsMember(env, first, 63), Is.False);
        Assert.That(TestOps.IsMember(env, first, 45), Is.True);

        var second = (TestBitSet)TestOps.MakeSingleton(env, 10);
        second = TestOps.AddElem(env, second, 50);
        TestOps.AddElemD(env, ref second, 51);
        Assert.That(TestOps.Count(env, second), Is.EqualTo(3));

        var union = (TestBitSet)TestOps.Union(env, first, second);
        Assert.That(TestOps.Count(env, union), Is.EqualTo(6));
        Assert.That(TestOps.Equal(env, union, TestOps.MakeCopy(env, union)), Is.True);
        Assert.That(TestOps.NotEqual(env, union, first), Is.True);
        Assert.That(TestOps.IsSubset(env, first, union), Is.True);
        Assert.That(TestOps.IsEmptyUnion(env, empty, empty), Is.True);
        Assert.That(TestOps.IsEmptyUnion(env, empty, first), Is.False);

        var unionD = (TestBitSet)TestOps.MakeCopy(env, first);
        TestOps.UnionD(env, ref unionD, second);
        Assert.That(TestOps.Equal(env, unionD, union), Is.True);

        var intersection = (TestBitSet)TestOps.Intersection(env, first, second);
        Assert.That(intersection.Bits, Is.EqualTo(1UL << 10));
        Assert.That(TestOps.IsEmptyIntersection(env, first, second), Is.False);
        Assert.That(TestOps.IsEmptyIntersection(env, empty, first), Is.True);

        var intersectionD = (TestBitSet)TestOps.MakeCopy(env, first);
        TestOps.IntersectionD(env, ref intersectionD, second);
        Assert.That(TestOps.Equal(env, intersectionD, intersection), Is.True);

        var difference = (TestBitSet)TestOps.Diff(env, first, second);
        Assert.That(difference.Bits, Is.EqualTo((1UL << 0) | (1UL << 44) | (1UL << 45)));
        TestOps.DiffD(env, ref first, second);
        Assert.That(TestOps.Equal(env, first, difference), Is.True);

        var removed = (TestBitSet)TestOps.RemoveElem(env, union, 10);
        Assert.That(TestOps.IsMember(env, removed, 10), Is.False);
        TestOps.RemoveElemD(env, ref union, 50);
        Assert.That(TestOps.IsMember(env, union, 50), Is.False);

        var assigned = (TestBitSet)TestOps.MakeEmpty(env);
        TestOps.Assign(env, ref assigned, second);
        Assert.That(TestOps.Equal(env, assigned, second), Is.True);
        TestOps.AssignNouninit(env, ref assigned, difference);
        Assert.That(TestOps.Equal(env, assigned, difference), Is.True);
        TestOps.AssignAllowUninitRhs(env, ref assigned, TestOps.UninitVal());
        Assert.That(TestOps.MayBeUninit(assigned), Is.True);
        TestOps.AssignNoCopy(env, ref assigned, second);
        Assert.That(TestOps.Equal(env, assigned, second), Is.True);

        var liveness = (TestBitSet)TestOps.MakeEmpty(env);
        TestOps.LivenessD(env, ref liveness, first, second, union);
        Assert.That(liveness.Bits, Is.EqualTo(second.Bits | (union.Bits & ~first.Bits)));

        TestOps.ClearD(env, ref liveness);
        Assert.That(TestOps.IsEmpty(env, liveness), Is.True);
    }

    [Test]
    public static void RawUInt64OperationsPreserveNativeValueContracts()
    {
        var env = new BitSetUint64TestEnvironment(64);
        var empty = TestRawOps.MakeEmpty(env);
        Assert.That(TestRawOps.IsEmpty(env, empty), Is.True);
        Assert.That(TestRawOps.MayBeUninit(TestRawOps.UninitVal()), Is.True);
        Assert.That(TestRawOps.MayBeUninit(empty), Is.True);

        var first = TestRawOps.MakeSingleton(env, 0);
        var highest = TestRawOps.MakeSingleton(env, 63);
        Assert.That(TestRawOps.IsMember(env, highest, 63), Is.True);
        TestRawOps.AddElemD(env, ref first, 10);
        TestRawOps.AddElemD(env, ref first, 44);
        TestRawOps.AddElemD(env, ref first, 45);
        Assert.That(TestRawOps.Count(env, first), Is.EqualTo(4));
        Assert.That(TestRawOps.IsMember(env, first, 45), Is.True);
        Assert.That(TestRawOps.IsMember(env, first, 63), Is.False);
        Assert.That(TestRawOps.MakeCopy(env, first), Is.EqualTo(first));

        var second = TestRawOps.MakeSingleton(env, 10);
        second = TestRawOps.AddElem(env, second, 50);
        TestRawOps.AddElemD(env, ref second, 51);
        Assert.That(TestRawOps.Count(env, second), Is.EqualTo(3));

        Assert.That(TestRawOps.IsEmptyUnion(env, empty, empty), Is.True);
        Assert.That(TestRawOps.IsEmptyUnion(env, empty, first), Is.False);

        var unionInput = first;
        var union = TestRawOps.Union(env, ref unionInput, second);
        Assert.That(unionInput, Is.EqualTo(first));
        Assert.That(union, Is.EqualTo(first | second));

        var unionD = first;
        TestRawOps.UnionD(env, ref unionD, second);
        Assert.That(TestRawOps.Equal(env, unionD, union), Is.True);
        Assert.That(TestRawOps.IsSubset(env, first, union), Is.True);
        Assert.That(TestRawOps.Equal(env, union, TestRawOps.MakeCopy(env, union)), Is.True);

        var intersection = TestRawOps.Intersection(env, first, second);
        Assert.That(intersection, Is.EqualTo(1UL << 10));
        Assert.That(TestRawOps.IsEmptyIntersection(env, first, second), Is.False);
        Assert.That(TestRawOps.IsEmptyIntersection(env, empty, first), Is.True);

        var intersectionD = first;
        TestRawOps.IntersectionD(env, ref intersectionD, second);
        Assert.That(intersectionD, Is.EqualTo(intersection));

        var difference = TestRawOps.Diff(env, first, second);
        Assert.That(difference, Is.EqualTo((1UL << 0) | (1UL << 44) | (1UL << 45)));
        var differenceD = first;
        TestRawOps.DiffD(env, ref differenceD, second);
        Assert.That(differenceD, Is.EqualTo(difference));

        Assert.That(TestRawOps.RemoveElem(env, union, 10), Is.EqualTo(union & ~(1UL << 10)));
        var removed = union;
        TestRawOps.RemoveElemD(env, ref removed, 50);
        Assert.That(TestRawOps.IsMember(env, removed, 50), Is.False);

        var assigned = TestRawOps.MakeEmpty(env);
        TestRawOps.Assign(env, ref assigned, second);
        Assert.That(assigned, Is.EqualTo(second));
        TestRawOps.AssignNouninit(env, ref assigned, first);
        Assert.That(assigned, Is.EqualTo(first));
        TestRawOps.AssignAllowUninitRhs(env, ref assigned, TestRawOps.UninitVal());
        Assert.That(TestRawOps.MayBeUninit(assigned), Is.True);
        TestRawOps.AssignNoCopy(env, ref assigned, second);
        Assert.That(assigned, Is.EqualTo(second));

        var liveIn = TestRawOps.MakeEmpty(env);
        TestRawOps.LivenessD(env, ref liveIn, first, second, union);
        Assert.That(liveIn, Is.EqualTo(second | (union & ~first)));
        TestRawOps.ClearD(env, ref liveIn);
        Assert.That(TestRawOps.IsEmpty(env, liveIn), Is.True);

        var iterator = new TestRawOps.Iter(env, union);
        var visited = new List<uint>();
        var bitNum = uint.MaxValue;
        while (iterator.NextElem(ref bitNum))
        {
            visited.Add(bitNum);
        }

        Assert.That(visited, Is.EqualTo(new uint[] { 0, 10, 44, 45, 50, 51 }));
        Assert.That(iterator.NextElem(ref bitNum), Is.False);
        Assert.That(bitNum, Is.EqualTo(51));
    }

    [Test]
    public static void IteratorVisitsAscendingSetBitsAndLeavesOutputAtEnd()
    {
        var env = new BitSetUint64TestEnvironment(64);
        var bitSet = (TestBitSet)TestOps.MakeEmpty(env);
        TestOps.AddElemD(env, ref bitSet, 0);
        TestOps.AddElemD(env, ref bitSet, 10);
        TestOps.AddElemD(env, ref bitSet, 63);

        var iterator = new TestOps.Iter(env, bitSet);
        var visited = new List<uint>();
        var element = uint.MaxValue;

        while (iterator.NextElem(ref element))
        {
            visited.Add(element);
        }

        Assert.That(visited, Is.EqualTo(new uint[] { 0, 10, 63 }));
        Assert.That(iterator.NextElem(ref element), Is.False);
        Assert.That(element, Is.EqualTo(63));

        var emptyEnv = new BitSetUint64TestEnvironment(0);
        var empty = (TestBitSet)TestOps.MakeEmpty(emptyEnv);
        var emptyIterator = new TestOps.Iter(emptyEnv, empty);
        element = 73;
        Assert.That(emptyIterator.NextElem(ref element), Is.False);
        Assert.That(element, Is.EqualTo(73));
    }

#if DEBUG
    [TestCase(0UL, "0000000000000000")]
    [TestCase(0x123456789ABCDEF0UL, "9ABCDEF012345678")]
    [TestCase(ulong.MaxValue, "FFFFFFFFFFFFFFFF")]
    public static void DebugStringFormatsLowWordBeforeHighWord(ulong bits, string expected)
    {
        var env = new BitSetUint64TestEnvironment(64);
        var bitSet = new TestBitSet(env, full: false)
        {
            Bits = bits,
        };

        Assert.That(TestOps.ToString(env, bitSet), Is.EqualTo(expected));
        Assert.That(TestRawOps.ToString(env, bits), Is.EqualTo(expected));
    }
#endif
}

internal sealed class BitSetUint64TestEnvironment
{
    public BitSetUint64TestEnvironment(int size)
    {
        Size = size;
    }

    public int Size { get; }
}

internal readonly struct BitSetUint64TestTraits : IBitSetTraits<BitSetUint64TestEnvironment>
{
    public static int GetArrSize(BitSetUint64TestEnvironment env) => 1;

    public static int GetEpoch(BitSetUint64TestEnvironment env) => 0;

    public static int GetSize(BitSetUint64TestEnvironment env) => env.Size;
}
