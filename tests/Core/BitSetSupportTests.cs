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
        public TestEnvironment(int size)
        {
            Size = size;
        }

        public int Size { get; }
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
}
