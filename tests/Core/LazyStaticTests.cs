// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class LazyStaticTests
{
    private sealed class Store
    {
        private ConditionalWeakTable<object, Value>? _table;

        internal bool IsInitialized => _table is not null;

        internal Value Get(object key)
        {
            var table = LazyStatic.GetOrCreate(ref _table);
            return table.GetValue(key, static _ => new Value());
        }
    }

    private sealed class Value
    {
    }

    [Test]
    public static void ConcurrentGetUsesOneAttachedValue()
    {
        var store = new Store();
        var key = new object();
        var values = new Value[256];

        Assert.That(store.IsInitialized, Is.False);
        _ = Parallel.For(0, values.Length, index => values[index] = store.Get(key));

        Assert.That(store.IsInitialized, Is.True);
        for (var index = 1; index < values.Length; index++)
        {
            Assert.That(ReferenceEquals(values[0], values[index]), Is.True);
        }
    }
}
