// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitHashTableTests
{
    [Test]
    public static void LookupSetAndEmplacePreserveExistingValues()
    {
        var table = new JitHashTable<uint, string>(new ConstantHashInfo());
        table.Reallocate(7);

        Assert.That(table.Set(1, "first"), Is.False);
        Assert.That(table.Set(1, "updated", JitHashTable<uint, string>.SetKind.Overwrite), Is.True);
        Assert.That(table.Lookup(1, out var value), Is.True);
        Assert.That(value, Is.EqualTo("updated"));

        var existing = table.Emplace(1, () => throw new InvalidOperationException());
        Assert.That(existing.Value, Is.EqualTo("updated"));

        var added = table.LookupPointerOrAdd(2, "default");
        added.Value = "assigned";
        Assert.That(table.Lookup(2, out value), Is.True);
        Assert.That(value, Is.EqualTo("assigned"));

        var created = table.Emplace(3, () => "constructed");
        Assert.That(created.Value, Is.EqualTo("constructed"));
        Assert.That(table.GetCount(), Is.EqualTo(3));
    }

    [Test]
    public static void ReallocationAndGrowthPreserveNativeChainOrder()
    {
        var table = new JitHashTable<uint, uint>(new ConstantHashInfo());
        table.Reallocate(7);

        for (uint key = 0; key < 6; key++)
        {
            Assert.That(table.Set(key, key), Is.False);
        }

        table.Reallocate(8);
        Assert.That(table.Keys().ToArray(), Is.EqualTo(new uint[] { 0, 1, 2, 3, 4, 5 }));

        Assert.That(table.Set(6, 6), Is.False);
        Assert.That(table.Keys().ToArray(), Is.EqualTo(new uint[] { 6, 5, 4, 3, 2, 1, 0 }));
    }

    [Test]
    public static void RemoveAndRemoveAllInvalidateValueReferences()
    {
        var table = new JitHashTable<uint, string>(new ConstantHashInfo());
        table.Reallocate(7);
        var value = table.LookupPointerOrAdd(1, "value");

        Assert.That(table.Remove(1), Is.True);
        _ = Assert.Throws<ObjectDisposedException>(() => _ = value.Value);
        Assert.That(table.Remove(1), Is.False);
        Assert.That(table.GetCount(), Is.Zero);

        var removedByClear = table.LookupPointerOrAdd(2, "cleared");
        table.RemoveAll();
        _ = Assert.Throws<ObjectDisposedException>(() => _ = removedByClear.Value);
        Assert.That(table.GetCount(), Is.Zero);
        _ = Assert.Throws<InvalidOperationException>(() => table.Remove(2));
    }

    [Test]
    public static void ComparerDefinesKeyEqualityAndHashing()
    {
        var table = new JitHashTable<string, int>(
            new ComparerHashTableInfo<string>(StringComparer.OrdinalIgnoreCase));
        table.Reallocate(7);

        Assert.That(table.Set("first", 1), Is.False);
        Assert.That(table.Set("FIRST", 2, JitHashTable<string, int>.SetKind.Overwrite), Is.True);
        Assert.That(table.Lookup("First", out var value), Is.True);
        Assert.That(value, Is.EqualTo(2));
        Assert.That(table.GetCount(), Is.EqualTo(1));
    }

    [Test]
    public static void PrimitiveKeyFunctionsPreserveNativeHashAndEqualityRules()
    {
        var smallKeyFuncs = new JitSmallPrimitiveKeyFuncs<int>();
        Assert.That(smallKeyFuncs.GetHashCode(-1), Is.EqualTo(uint.MaxValue));

        var largeKeyFuncs = new JitLargePrimitiveKeyFuncs<double>();
        var doubleValue = BitConverter.Int64BitsToDouble(unchecked((long)0x123456789abcdef0));
        Assert.That(largeKeyFuncs.GetHashCode(doubleValue), Is.EqualTo(0x88888888u));

        var floatKeyFuncs = new JitLargePrimitiveKeyFuncs<float>();
        Assert.That(floatKeyFuncs.Equals(float.NaN, float.NaN), Is.False);
        Assert.That(floatKeyFuncs.Equals(0.0f, -0.0f), Is.True);
    }

    [Test]
    public static void PointerKeyFunctionsUseIdentityAndRawAddressSemantics()
    {
        var objectKeyFuncs = new JitPtrKeyFuncs<IdentityKey>();
        var first = new IdentityKey(1);
        var second = new IdentityKey(1);

        Assert.That(objectKeyFuncs.Equals(first, first), Is.True);
        Assert.That(objectKeyFuncs.Equals(first, second), Is.False);
        Assert.That(objectKeyFuncs.GetHashCode(first),
            Is.EqualTo(unchecked((uint)RuntimeHelpers.GetHashCode(first))));

        var objectTable = new JitHashTable<IdentityKey, int>(objectKeyFuncs);
        objectTable.Reallocate(7);
        Assert.That(objectTable.Set(first, 1), Is.False);
        Assert.That(objectTable.Set(second, 2), Is.False);
        Assert.That(objectTable.GetCount(), Is.EqualTo(2));
        Assert.That(objectTable.Lookup(first, out var firstValue), Is.True);
        Assert.That(firstValue, Is.EqualTo(1));

        var signedAddressKeyFuncs = new JitPtrKeyFuncs<nint>();
        var address = unchecked((nint)0x1234567887654321UL);
        Assert.That(signedAddressKeyFuncs.GetHashCode(address), Is.EqualTo(unchecked((uint)address)));
        Assert.That(signedAddressKeyFuncs.Equals(address, address), Is.True);
        Assert.That(signedAddressKeyFuncs.Equals(address, unchecked(address + 1)), Is.False);

        if (IntPtr.Size == 8)
        {
            var collidingAddress = unchecked((nint)0xABCDEF0187654321UL);
            Assert.That(signedAddressKeyFuncs.GetHashCode(collidingAddress),
                Is.EqualTo(signedAddressKeyFuncs.GetHashCode(address)));
            Assert.That(signedAddressKeyFuncs.Equals(address, collidingAddress), Is.False);
        }

        var unsignedAddressKeyFuncs = new JitPtrKeyFuncs<nuint>();
        var unsignedAddress = unchecked((nuint)0x1234567887654321UL);
        Assert.That(unsignedAddressKeyFuncs.GetHashCode(unsignedAddress),
            Is.EqualTo(unchecked((uint)unsignedAddress)));

        var block = new BasicBlock(null, null) { bbID = 42 };
        var blockKeyFuncs = new JitPtrKeyFuncs<BasicBlock>();
        Assert.That(blockKeyFuncs.GetHashCode(block), Is.EqualTo(unchecked((uint)block.GetHashCode())));
    }

    private sealed class ConstantHashInfo : JitKeyFuncsDefEquals<uint>
    {
        public override uint GetHashCode(uint key) => 0;
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
