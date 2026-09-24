// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

using indexType = ulong;

namespace RyuJitSharp.UnitTests;

internal static class HashBitVectorTests
{
    [TestCase(4, 1)]
    [TestCase(5, 4)]
    [TestCase(16, 4)]
    [TestCase(17, 16)]
    [TestCase(64, 16)]
    [TestCase(65, 64)]
    public static void GrowthPreservesNativeBucketAndNodeOrder(int nodes, int buckets)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        hashBv.Init(compiler);
        var vector = hashBv.Create(compiler);
        int[] offsets = [0, 31, 32, 127];
        var values = Enumerable.Range(0, nodes).SelectMany(n => offsets.Select(bit => (indexType)((n * 128) + bit))).ToArray();

        foreach (var index in values.Reverse())
        {
            vector.setBit(index);
            vector.setBit(index);
        }

        Assert.That(vector.numNodes, Is.EqualTo(nodes));
        Assert.That(vector.hashtable_size(), Is.EqualTo(buckets));
        Assert.That(vector.IsValid(), Is.True);
        Assert.That(Read(vector), Is.EqualTo(values.OrderBy(index => (index >> 7) & (indexType)(buckets - 1)).ThenBy(index => index)));
        Assert.That(values.All(vector.testBit), Is.True);
        Assert.That(vector.testBit(33), Is.False);
        Assert.That(vector.testBit((indexType)(nodes * 128)), Is.False);
        Assert.That(vector.getNodeForIndex(31), Is.SameAs(vector.getNodeForIndex(0)));
        Assert.That(vector.getNodeForIndex((indexType)(nodes * 128)), Is.Null);
        Assert.That(Read(hashBv.Create(compiler)), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    public static void ClearingRemovesOnlyEmptyNodes(int removed)
    {
        var vector = new hashBv();

        for (var node = 0; node < 12; node++)
        {
            vector.setBit((indexType)(node * 128));
        }

        var index = (indexType)(removed * 128);
        vector.setBit(index + 1);
        vector.clearBit(index);
        Assert.That(vector.testBit(index), Is.False);
        Assert.That(vector.testBit(index + 1), Is.True);
        Assert.That(vector.numNodes, Is.EqualTo(12));

        vector.clearBit(index + 1);
        vector.clearBit(index + 1);
        vector.clearBit(100000);
        Assert.That(vector.numNodes, Is.EqualTo(11));
        Assert.That(vector.hashtable_size(), Is.EqualTo(4));
        Assert.That(vector.IsValid(), Is.True);
        Assert.That(Read(vector).OrderBy(value => value), Is.EqualTo(
            Enumerable.Range(0, 12).Where(node => node != removed).Select(node => (indexType)(node * 128))));
    }

    [Test]
    public static void ShrinkingMergesSortedChains()
    {
        var vector = new hashBv();

        for (var node = 20; node >= 0; node--)
        {
            vector.setBit((indexType)(node * 128));
        }

        vector.Resize(3);
        Assert.That(vector.hashtable_size(), Is.EqualTo(2));
        Assert.That(vector.IsValid(), Is.True);
        Assert.That(Read(vector), Is.EqualTo(Enumerable.Range(0, 21).OrderBy(node => node & 1).ThenBy(node => node)
            .Select(node => (indexType)(node * 128))));

        vector.Resize(1);
        Assert.That(Read(vector), Is.EqualTo(Enumerable.Range(0, 21).Select(node => (indexType)(node * 128))));
        vector.Resize(1);
        Assert.That(vector.numNodes, Is.EqualTo(21));
    }

    [Test]
    public static void AbortingTraversalCanConsumeTheCurrentTemporary()
    {
        var vector = new hashBv();
        vector.setBit(0);
        vector.setBit(128);
        vector.setBit(256);
        var visited = new List<indexType>();

        var result = Globals.ForEachHbvBitSet(vector, index => {
            visited.Add(index);

            if (index == 128)
            {
                vector.clearBit(index);
                return HbvWalk.Abort;
            }

            return HbvWalk.Continue;
        });

        Assert.That(result, Is.EqualTo(HbvWalk.Abort));
        Assert.That(visited, Is.EqualTo(new indexType[] { 0, 128 }));
        Assert.That(Read(vector), Is.EqualTo(new indexType[] { 0, 256 }));
    }

    private static List<indexType> Read(hashBv vector)
    {
        var values = new List<indexType>();
        Assert.That(Globals.ForEachHbvBitSet(vector, index => {
            values.Add(index);
            return HbvWalk.Continue;
        }), Is.EqualTo(HbvWalk.Continue));
        return values;
    }
}
