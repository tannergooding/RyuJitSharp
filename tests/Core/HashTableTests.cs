// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class HashTableTests
{
    [Test]
    public static void SmallHashTableMaintainsCollisionChainsAcrossResizeAndRemoval()
    {
        var table = new SmallHashTable<uint, string>(8, new ConstantHashInfo());

        for (uint key = 0; key < 8; key++)
        {
            Assert.That(table.AddOrUpdate(key, key.ToString(CultureInfo.InvariantCulture)), Is.True);
        }

        Assert.That(table.Count(), Is.EqualTo(8));

        for (uint key = 0; key < 8; key++)
        {
            Assert.That(table.TryGetValue(key, out var value), Is.True);
            Assert.That(value, Is.EqualTo(key.ToString(CultureInfo.InvariantCulture)));
        }

        Assert.That(table.TryRemove(3, out var removedValue), Is.True);
        Assert.That(removedValue, Is.EqualTo("3"));
        Assert.That(table.Contains(3), Is.False);
        Assert.That(table.Count(), Is.EqualTo(7));
        Assert.That(table.AddOrUpdate(4, "updated"), Is.False);
        Assert.That(table.TryGetValue(4, out var updatedValue), Is.True);
        Assert.That(updatedValue, Is.EqualTo("updated"));
    }

    [Test]
    public static void HashTableUsesConfiguredEqualityAndHashing()
    {
        var table = new HashTable<string, int>(
            new ComparerHashTableInfo<string>(StringComparer.OrdinalIgnoreCase));

        Assert.That(table.AddOrUpdate("first", 1), Is.True);
        Assert.That(table.AddOrUpdate("FIRST", 2), Is.False);
        Assert.That(table.Count(), Is.EqualTo(1));
        Assert.That(table.TryGetValue("First", out var value), Is.True);
        Assert.That(value, Is.EqualTo(2));
    }

    [Test]
    public static void TryGetValueRefPreservesValueOnMissAndUpdatesOnHit()
    {
        var table = new HashTable<uint, string>();
        Assert.That(table.AddOrUpdate(1, "stored"), Is.True);

        var value = "unchanged";
        Assert.That(table.TryGetValueRef(2, ref value), Is.False);
        Assert.That(value, Is.EqualTo("unchanged"));
        Assert.That(table.TryGetValueRef(1, ref value), Is.True);
        Assert.That(value, Is.EqualTo("stored"));
    }

    [Test]
    public static void TryRemoveRefPreservesValueOnMissAndUpdatesOnHit()
    {
        var table = new HashTable<uint, string>();
        Assert.That(table.AddOrUpdate(1, "stored"), Is.True);

        var value = "unchanged";
        Assert.That(table.TryRemoveRef(2, ref value), Is.False);
        Assert.That(value, Is.EqualTo("unchanged"));
        Assert.That(table.TryRemoveRef(1, ref value), Is.True);
        Assert.That(value, Is.EqualTo("stored"));
        Assert.That(table.Contains(1), Is.False);
    }

    [Test]
    public static void DefaultHashUsesPrimitiveAndReferenceIdentitySemantics()
    {
        var signedInfo = new DefaultHashTableInfo<int>();
        var unsignedInfo = new DefaultHashTableInfo<uint>();
        var nativeSizeInfo = new DefaultHashTableInfo<nint>();
        var referenceInfo = new DefaultHashTableInfo<IdentityKey>();
        var first = new IdentityKey(1);
        var second = new IdentityKey(1);

        Assert.That(signedInfo.GetHashCode(-1), Is.EqualTo(uint.MaxValue));
        Assert.That(unsignedInfo.GetHashCode(0x87654321), Is.EqualTo(0x87654321));
        Assert.That(nativeSizeInfo.GetHashCode(-1), Is.EqualTo(uint.MaxValue));
        Assert.That(referenceInfo.GetHashCode(null!), Is.Zero);
        Assert.That(referenceInfo.Equals(first, first), Is.True);
        Assert.That(referenceInfo.Equals(first, second), Is.False);
        Assert.That(referenceInfo.GetHashCode(first),
            Is.EqualTo(unchecked((uint)RuntimeHelpers.GetHashCode(first))));

        var table = new HashTable<IdentityKey, int>();
        Assert.That(table.AddOrUpdate(first, 1), Is.True);
        Assert.That(table.AddOrUpdate(second, 2), Is.True);
        Assert.That(table.Count(), Is.EqualTo(2));
        Assert.That(table.TryGetValue(first, out var firstValue), Is.True);
        Assert.That(firstValue, Is.EqualTo(1));
    }

#if DEBUG
    [Test]
    public static void DebugIteratorWalksOccupiedBucketsInIndexOrder()
    {
        var table = new SmallHashTable<uint, uint>(8, new ConstantHashInfo());
        for (uint key = 0; key < 8; key++)
        {
            _ = table.AddOrUpdate(key, key);
        }

        var iterator = table.begin();
        var end = table.end();
        var keys = new uint[8];
        var index = 0;
        var copiedIterator = iterator;
        iterator = ++iterator;
        Assert.That(copiedIterator.Current.Key(), Is.EqualTo(0));
        Assert.That(iterator.Current.Key(), Is.EqualTo(1));
        iterator = copiedIterator;

        while (iterator != end)
        {
            keys[index++] = iterator.Current.Key();
            iterator = ++iterator;
        }

        Assert.That(keys, Is.EqualTo(new uint[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
    }

    [Test]
    public static void DebugKeyValuePairRetainsItsBucketAfterResize()
    {
        var table = new SmallHashTable<uint, uint>(8, new ConstantHashInfo());
        for (uint key = 0; key < 7; key++)
        {
            _ = table.AddOrUpdate(key, key + 10);
        }

        var pair = table.begin().Current;
        _ = table.AddOrUpdate(7, 17);

        Assert.That(pair.Key(), Is.EqualTo(0));
        Assert.That(pair.Value(), Is.EqualTo(10));
    }
#endif

    private sealed class ConstantHashInfo : IHashTableInfo<uint>
    {
        public bool Equals(uint x, uint y) => x == y;

        public uint GetHashCode(uint key) => 0;
    }

    private sealed class IdentityKey
    {
        internal IdentityKey(int value)
        {
            Value = value;
        }

        private int Value { get; }

        public override bool Equals(object? obj) => obj is IdentityKey other && Value == other.Value;

        public override int GetHashCode() => Value;
    }
}
