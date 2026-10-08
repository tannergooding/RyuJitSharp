// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RyuJitSharp;

namespace RyuJitSharp.UnitTests;

internal static class BitSetSupportTests
{
    private static readonly uint[] s_expectedBitIndexes = [0, 10, 63, 64, 129];
    private static readonly int[] s_expectedVisitedBits = [0, 1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] s_expectedVisitedBitsReverse = [7, 6, 5, 4, 3, 2, 1, 0];

    [Test]
    public static void UninitializedValueIsDistinguishedFromNonemptyBitSet()
    {
        var environment = new TestEnvironment(size: 130);
        var uninitialized = BitSetOps<TestEnvironment, TestBitSetTraits>.UninitVal();
        var empty = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeEmpty(environment);

        Assert.That(BitSetOps<TestEnvironment, TestBitSetTraits>.MaybeUninit(uninitialized), Is.True);
        Assert.That(BitSetOps<TestEnvironment, TestBitSetTraits>.MaybeUninit(empty), Is.False);

        var emptyEnvironment = new TestEnvironment(size: 0);
        var zeroSized = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeEmpty(emptyEnvironment);

        Assert.That(BitSetOps<TestEnvironment, TestBitSetTraits>.MaybeUninit(zeroSized), Is.True);
    }

    [Test]
    public static void OperationCounterWritesSortedCountsAtTheNativeInterval()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var counter = new BitSetSupport.BitSetOpCounter(path);

            for (var i = 0; i < 1_000_000; i++)
            {
                counter.RecordOp(BitSetSupport.Operation.BSOP_AddElemD);
            }

            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var expectedTopLine = string.Format(
                CultureInfo.InvariantCulture,
                "   Op {0,40}: {1,8}",
                nameof(BitSetSupport.Operation.BSOP_AddElemD),
                1_000_000);

            Assert.That(BitSetSupport.OpNames.Length, Is.EqualTo((int)BitSetSupport.Operation.BSOP_NUMOPS));
            Assert.That(lines, Has.Length.EqualTo(BitSetSupport.OpNames.Length + 1));
            Assert.That(lines[0], Is.EqualTo("@ 1000000 total ops."));
            Assert.That(lines[1], Is.EqualTo(expectedTopLine));
            Assert.That(lines[^1], Does.Contain(nameof(BitSetSupport.Operation.BSOP_ToString)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public static void CounterWrapperRecordsEverySupportedOperation()
    {
        var counter = new BitSetSupport.BitSetOpCounter("unused");
        var environment = new TestEnvironment(size: 8, counter: counter);
        var bitSet = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.MakeEmpty(environment);
        var full = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.MakeFull(environment);
        var singleton = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.MakeSingleton(environment, 2);

        var uninitialized = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.UninitVal();
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.MaybeUninit(uninitialized),
            Is.True);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.MaybeUninit(singleton),
            Is.False);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.IsEmptyUnion(environment, bitSet, bitSet),
            Is.True);

        var tryAdded = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeEmpty(environment);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.TryAddElemD(environment, tryAdded, 3),
            Is.True);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.TryAddElemD(environment, tryAdded, 3),
            Is.False);

        var dataFlow = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeCopy(environment, full);
        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.DataFlowD(
            environment,
            dataFlow,
            singleton,
            bitSet);
        Assert.That(BitSetOps<TestEnvironment, TestBitSetTraits>.Equal(environment, dataFlow, singleton), Is.True);

        var live = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeEmpty(environment);
        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.LivenessD(
            environment,
            live,
            singleton,
            bitSet,
            full);
        Assert.That(BitSetOps<TestEnvironment, TestBitSetTraits>.IsMember(environment, live, 2), Is.False);
        Assert.That(BitSetOps<TestEnvironment, TestBitSetTraits>.IsMember(environment, live, 3), Is.True);

        var visitedBits = new List<int>();
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.VisitBits(
                environment,
                full,
                bit =>
                {
                    visitedBits.Add(bit);
                    return true;
                }),
            Is.True);
        Assert.That(visitedBits, Is.EqualTo(s_expectedVisitedBits));

        visitedBits.Clear();
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.VisitBitsReverse(
                environment,
                full,
                bit =>
                {
                    visitedBits.Add(bit);
                    return true;
                }),
            Is.True);
        Assert.That(visitedBits, Is.EqualTo(s_expectedVisitedBitsReverse));

        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Assign(environment, ref bitSet, singleton);
        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.AssignAllowUninitRhs(
            environment,
            ref bitSet,
            BitSetOps<TestEnvironment, TestBitSetTraits>.UninitVal());

        var noCopySource = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeCopy(environment, singleton);
        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.AssignNoCopy(
            environment,
            ref bitSet,
            noCopySource);
        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.ClearD(environment, bitSet);
        _ = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.MakeCopy(environment, full);

        Assert.That(BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.IsEmpty(environment, bitSet), Is.True);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Count(environment, full),
            Is.EqualTo((nint)8));
        Assert.That(BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.IsMember(environment, singleton, 2), Is.True);

        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.AddElemD(environment, bitSet, 1);
        var added = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.AddElem(environment, bitSet, 2);
        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.RemoveElemD(environment, bitSet, 1);
        _ = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.RemoveElem(environment, added, 2);

        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.UnionD(environment, bitSet, singleton);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.UnionDChanged(environment, bitSet, full),
            Is.True);
        _ = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Union(environment, singleton, full);

        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.IntersectionD(environment, bitSet, singleton);
        var intersection =
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Intersection(environment, full, singleton);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.IsEmptyIntersection(
                environment,
                intersection,
                singleton),
            Is.False);

        BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.DiffD(environment, bitSet, singleton);
        _ = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Diff(environment, full, singleton);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.IsSubset(environment, singleton, full),
            Is.True);
        Assert.That(
            BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Equal(environment, singleton, intersection),
            Is.True);

#if DEBUG
        _ = BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.ToString(environment, bitSet);
#endif

        var iterator = new BitSetOpsWithCounter<TestEnvironment, TestBitSetCounterTraits>.Iter(environment, singleton);
        var bit = uint.MaxValue;
        Assert.That(iterator.NextElem(ref bit), Is.True);
        Assert.That(bit, Is.EqualTo(2));
        Assert.That(iterator.NextElem(ref bit), Is.False);

        BitSetSupport.Operation[] countedOperations =
        [
            BitSetSupport.Operation.BSOP_MakeEmpty,
            BitSetSupport.Operation.BSOP_MakeFull,
            BitSetSupport.Operation.BSOP_MakeSingleton,
            BitSetSupport.Operation.BSOP_Assign,
            BitSetSupport.Operation.BSOP_AssignAllowUninitRhs,
            BitSetSupport.Operation.BSOP_AssignNocopy,
            BitSetSupport.Operation.BSOP_ClearD,
            BitSetSupport.Operation.BSOP_MakeCopy,
            BitSetSupport.Operation.BSOP_IsEmpty,
            BitSetSupport.Operation.BSOP_Count,
            BitSetSupport.Operation.BSOP_IsMember,
            BitSetSupport.Operation.BSOP_AddElemD,
            BitSetSupport.Operation.BSOP_AddElem,
            BitSetSupport.Operation.BSOP_RemoveElemD,
            BitSetSupport.Operation.BSOP_RemoveElem,
            BitSetSupport.Operation.BSOP_UnionD,
            BitSetSupport.Operation.BSOP_UnionDChanged,
            BitSetSupport.Operation.BSOP_Union,
            BitSetSupport.Operation.BSOP_IntersectionD,
            BitSetSupport.Operation.BSOP_Intersection,
            BitSetSupport.Operation.BSOP_IsEmptyIntersection,
            BitSetSupport.Operation.BSOP_DiffD,
            BitSetSupport.Operation.BSOP_Diff,
            BitSetSupport.Operation.BSOP_IsSubset,
            BitSetSupport.Operation.BSOP_Equal,
#if DEBUG
            BitSetSupport.Operation.BSOP_ToString,
#endif
        ];

        foreach (var operation in countedOperations)
        {
            Assert.That(counter.GetCount(operation), Is.EqualTo(1u), operation.ToString());
        }

        Assert.That(counter.GetCount(BitSetSupport.Operation.BSOP_NextBit), Is.EqualTo(2u));
        Assert.That(counter.TotalOps, Is.EqualTo((uint)(countedOperations.Length + 2)));
    }

    [Test]
    public static void IteratorTraversesEveryWordAndPreservesOutputOnEnd()
    {
        var environment = new TestEnvironment(size: 130);
        var bitSet = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeEmpty(environment);

        foreach (var bitIndex in s_expectedBitIndexes)
        {
            BitSetOps<TestEnvironment, TestBitSetTraits>.AddElemD(environment, bitSet, (int)bitIndex);
        }

        var iterator = new BitSetOps<TestEnvironment, TestBitSetTraits>.Iter(environment, bitSet);
        var visited = new List<uint>();
        var bit = uint.MaxValue;

        while (iterator.NextElem(ref bit))
        {
            visited.Add(bit);
        }

        Assert.That(visited, Is.EqualTo(s_expectedBitIndexes));
        Assert.That(iterator.NextElem(ref bit), Is.False);
        Assert.That(bit, Is.EqualTo(s_expectedBitIndexes[^1]));

        var emptyEnvironment = new TestEnvironment(size: 0);
        var emptyBitSet = BitSetOps<TestEnvironment, TestBitSetTraits>.MakeEmpty(emptyEnvironment);
        var emptyIterator = new BitSetOps<TestEnvironment, TestBitSetTraits>.Iter(emptyEnvironment, emptyBitSet);
        bit = 73;
        Assert.That(emptyIterator.NextElem(ref bit), Is.False);
        Assert.That(bit, Is.EqualTo(73));
    }

    private sealed class TestEnvironment
    {
        public TestEnvironment(int size, BitSetSupport.BitSetOpCounter? counter = null)
        {
            Size = size;
            Counter = counter;
        }

        public int Size { get; }

        public BitSetSupport.BitSetOpCounter? Counter { get; }
    }

    private readonly struct TestBitSetTraits : IBitSetTraits<TestEnvironment>
    {
        public static int GetArrSize(TestEnvironment env)
        {
            var bitsPerWord = IntPtr.Size * 8;
            return (env.Size + bitsPerWord - 1) / bitsPerWord;
        }

        public static int GetEpoch(TestEnvironment env) => 0;

        public static int GetSize(TestEnvironment env) => env.Size;
    }

    private readonly struct TestBitSetCounterTraits :
        IBitSetTraits<TestEnvironment>,
        IBitSetOpCounterTraits<TestEnvironment>
    {
        public static int GetArrSize(TestEnvironment env) => TestBitSetTraits.GetArrSize(env);

        public static int GetEpoch(TestEnvironment env) => TestBitSetTraits.GetEpoch(env);

        public static int GetSize(TestEnvironment env) => TestBitSetTraits.GetSize(env);

        public static BitSetSupport.BitSetOpCounter GetOpCounter(TestEnvironment env)
            => env.Counter ?? throw new InvalidOperationException("The test environment has no bitset operation counter.");
    }
}
