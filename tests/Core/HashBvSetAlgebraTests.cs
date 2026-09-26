// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

#if TARGET_AMD64
using indexType = ulong;
#else
using indexType = uint;
#endif

namespace RyuJitSharp.UnitTests;

internal static class HashBvSetAlgebraTests
{
    private static readonly indexType[] s_left = [0, 31, 127, 128, 256, 512];
    private static readonly indexType[] s_right = [31, 64, 128, 384];

    [TestCase("And", 1, 1)]
    [TestCase("And", 16, 1)]
    [TestCase("And", 1, 16)]
    [TestCase("And", 4, 4)]
    [TestCase("Or", 1, 1)]
    [TestCase("Or", 16, 1)]
    [TestCase("Or", 1, 16)]
    [TestCase("Or", 4, 4)]
    [TestCase("Subtract", 1, 1)]
    [TestCase("Subtract", 16, 1)]
    [TestCase("Subtract", 1, 16)]
    [TestCase("Subtract", 4, 4)]
    public static void SetOperationsMergeCollisionsAcrossNativeTraversalSizes(string operation, int leftSize, int rightSize)
    {
        var left = Create(leftSize, s_left);
        var right = Create(rightSize, s_right);
        var expected = operation switch
        {
            "And" => s_left.Intersect(s_right),
            "Or" => s_left.Union(s_right),
            "Subtract" => s_left.Except(s_right),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        var changed = operation switch
        {
            "And" => left.AndWithChange(right),
            "Or" => left.OrWithChange(right),
            "Subtract" => left.SubtractWithChange(right),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        Assert.That(changed, Is.True);
        Assert.That(left.IsValid(), Is.True);
        Assert.That(left.numNodes, Is.EqualTo(NodeCount(left)));
        Assert.That(ReadBits(left), Is.EquivalentTo(expected));
        Assert.That(ReadBits(right), Is.EquivalentTo(s_right));
    }

    [TestCase("And")]
    [TestCase("Or")]
    [TestCase("Subtract")]
    public static void NonChangeWrappersApplyTheSameSetOperation(string operation)
    {
        var left = Create(1, 0, 128);
        var right = Create(4, 128, 256);
        var expected = operation switch
        {
            "And" => new indexType[] { 128 },
            "Or" => [0, 128, 256],
            "Subtract" => [0],
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        switch (operation)
        {
            case "And":
            {
                left.AndWith(right);
                break;
            }

            case "Or":
            {
                left.OrWith(right);
                break;
            }

            case "Subtract":
            {
                left.Subtract(right);
                break;
            }
        }

        Assert.That(left.IsValid(), Is.True);
        Assert.That(ReadBits(left), Is.EquivalentTo(expected));
        Assert.That(ReadBits(right), Is.EquivalentTo(new indexType[] { 128, 256 }));
    }

    [TestCase(1, 1)]
    [TestCase(1, 16)]
    [TestCase(16, 1)]
    [TestCase(4, 4)]
    public static void CompareChecksBitsAndNodeShapeAcrossBucketSizes(int leftSize, int rightSize)
    {
        var left = Create(leftSize, 0, 31, 128, 384);
        var equal = Create(rightSize, 384, 128, 31, 0);
        var disjoint = Create(rightSize, 1, 32, 129, 385);

        Assert.That(left.CompareWith(equal), Is.True);
        Assert.That(equal.CompareWith(left), Is.True);
        Assert.That(left.CompareWith(disjoint), Is.False);
        Assert.That(ReadBits(left), Is.EquivalentTo(new indexType[] { 0, 31, 128, 384 }));

        equal.setBit(512);
        Assert.That(left.CompareWith(equal), Is.False);
        Assert.That(equal.CompareWith(left), Is.False);
    }

    [TestCase("And", false)]
    [TestCase("Or", false)]
    [TestCase("Subtract", true)]
    public static void SelfOperationsPreserveNativeChangeAndSparseNodeRules(string operation, bool expectedChange)
    {
        var vector = Create(4, 0, 32, 128);
        var count = vector.numNodes;

        Assert.That(vector.CompareWith(vector), Is.True);
        var changed = operation switch
        {
            "And" => vector.AndWithChange(vector),
            "Or" => vector.OrWithChange(vector),
            "Subtract" => vector.SubtractWithChange(vector),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        Assert.That(changed, Is.EqualTo(expectedChange));
        Assert.That(vector.IsValid(), Is.True);
        Assert.That(vector.numNodes, Is.EqualTo(operation is "Subtract" ? 0 : count));
        indexType[] expected = operation is "Subtract" ? [] : [0, 32, 128];
        Assert.That(ReadBits(vector), Is.EquivalentTo(expected));
    }

    [Test]
    public static void EmptyPhysicalNodesRemainObservableUntilTheirNativeRemovalCondition()
    {
        var empty = new hashBv();
        _ = empty.getOrAddNodeForIndex(128);
        var absent = new hashBv();
        var copy = Create(1, 128);

        Assert.That(empty.CompareWith(absent), Is.False);
        Assert.That(empty.AndWithChange(copy), Is.False);
        Assert.That(empty.numNodes, Is.EqualTo(1));
        Assert.That(empty.CompareWith(absent), Is.False);
        Assert.That(absent.OrWithChange(empty), Is.True);
        Assert.That(absent.numNodes, Is.EqualTo(1));
        Assert.That(absent.CompareWith(empty), Is.True);

        Assert.That(copy.AndWithChange(empty), Is.True);
        Assert.That(copy.numNodes, Is.Zero);
        Assert.That(empty.numNodes, Is.EqualTo(1));
        empty.ZeroAll();
        Assert.That(empty.CompareWith(copy), Is.True);
        Assert.That(empty.numNodes, Is.Zero);
    }

    [Test]
    public static void CopyAndCompoundOperationsDoNotShareNodesOrChangeTheSource()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var source = Create(16, 0, 256, 512);
        var copy = hashBv.CreateFrom(source, compiler);
        var reused = Create(1, 31);
        reused.copyFrom(source, compiler);

        Assert.That(copy.CompareWith(source), Is.True);
        Assert.That(reused.CompareWith(source), Is.True);
        Assert.That(copy.hashtable_size(), Is.EqualTo(source.hashtable_size()));
        Assert.That(reused.hashtable_size(), Is.EqualTo(source.hashtable_size()));
        Assert.That(copy.getNodeForIndex(256), Is.Not.SameAs(source.getNodeForIndex(256)));
        copy.clearBit(256);
        reused.setBit(384);
        Assert.That(ReadBits(source), Is.EquivalentTo(new indexType[] { 0, 256, 512 }));

        var second = Create(1, 256, 128);
        var third = Create(1, 384);
        reused.Subtract3(source, second);
        Assert.That(ReadBits(reused), Is.EquivalentTo(new indexType[] { 0, 512 }));
        reused.UnionMinus(source, second, third);
        Assert.That(ReadBits(reused), Is.EquivalentTo(new indexType[] { 0, 384, 512 }));

        var buckets = reused.hashtable_size();
        reused.ZeroAll();
        Assert.That(reused.hashtable_size(), Is.EqualTo(buckets));
        Assert.That(reused.numNodes, Is.Zero);
        Assert.That(reused.CompareWith(new hashBv()), Is.True);
        Assert.That(ReadBits(source), Is.EquivalentTo(new indexType[] { 0, 256, 512 }));
    }

    [Test]
    public static void NodeOperationsCoverAllElementsAndReportChangedBits()
    {
        var source = new hashBvNode(0);
        source.setBit(0);
        source.setBit(31);
        source.setBit(32);
        source.setBit(96);

        var other = new hashBvNode(0);
        other.setBit(31);
        other.setBit(64);
        other.setBit(96);

        var identical = new hashBvNode(128);
        identical.copyFrom(source);
        Assert.That(identical.sameAs(source), Is.True);
        Assert.That(identical.baseIndex, Is.Zero);
        Assert.That(source.sameAs(new hashBvNode(128)), Is.False);

        var intersection = new hashBvNode(0);
        intersection.copyFrom(source);
        Assert.That(intersection.Intersects(other), Is.True);
        Assert.That(intersection.AndWithChange(other), Is.Not.Zero);
        Assert.That(ReadBits(intersection), Is.EquivalentTo(new indexType[] { 31, 96 }));
        Assert.That(intersection.AndWithChange(other), Is.Zero);
        Assert.That(intersection.sameAs(other), Is.False);

        var union = new hashBvNode(0);
        union.copyFrom(source);
        Assert.That(union.OrWithChange(other), Is.Not.Zero);
        Assert.That(ReadBits(union), Is.EquivalentTo(new indexType[] { 0, 31, 32, 64, 96 }));
        Assert.That(union.OrWithChange(other), Is.Zero);

        var difference = new hashBvNode(0);
        difference.copyFrom(source);
        Assert.That(difference.SubtractWithChange(other), Is.Not.Zero);
        Assert.That(ReadBits(difference), Is.EquivalentTo(new indexType[] { 0, 32 }));

        var xor = new hashBvNode(0);
        xor.copyFrom(source);
        Assert.That(xor.XorWithChange(other), Is.Not.Zero);
        Assert.That(ReadBits(xor), Is.EquivalentTo(new indexType[] { 0, 32, 64 }));
        Assert.That(xor.sameAs(source), Is.False);
    }

    [TestCase("And", new int[] { 31 })]
    [TestCase("Or", new int[] { 0, 31, 32, 64 })]
    [TestCase("Xor", new int[] { 0, 32, 64 })]
    [TestCase("Subtract", new int[] { 0, 32 })]
    public static void NodeNonChangeOperationsRetainElementValues(string operation, int[] expected)
    {
        var node = new hashBvNode(0);
        node.setBit(0);
        node.setBit(31);
        node.setBit(32);
        var other = new hashBvNode(0);
        other.setBit(31);
        other.setBit(64);

        switch (operation)
        {
            case "And":
            {
                node.AndWith(other);
                break;
            }

            case "Or":
            {
                node.OrWith(other);
                break;
            }

            case "Xor":
            {
                node.XorWith(other);
                break;
            }

            case "Subtract":
            {
                node.Subtract(other);
                break;
            }
        }

        Assert.That(ReadBits(node), Is.EquivalentTo(expected.Select(value => (indexType)value)));
        Assert.That(other.Intersects(node), Is.EqualTo(operation is not "Subtract"));
    }

    private static hashBv Create(int buckets, params indexType[] indices)
    {
        var vector = new hashBv();

        foreach (var index in indices)
        {
            vector.setBit(index);
        }

        vector.Resize(buckets);
        return vector;
    }

    private static int NodeCount(hashBv vector)
    {
        var count = 0;

        foreach (var head in vector.nodeArr)
        {
            for (var node = head; node is not null; node = node.next)
            {
                count++;
            }
        }

        return count;
    }

    private static List<indexType> ReadBits(hashBv vector)
    {
        var values = new List<indexType>();
        Assert.That(Globals.ForEachHbvBitSet(vector, index =>
        {
            values.Add(index);
            return HbvWalk.Continue;
        }), Is.EqualTo(HbvWalk.Continue));
        return values;
    }

    private static List<indexType> ReadBits(hashBvNode node)
    {
        var vector = new hashBv();
        var destination = vector.getOrAddNodeForIndex(node.baseIndex);
        destination.copyFrom(node);
        return ReadBits(vector);
    }
}
