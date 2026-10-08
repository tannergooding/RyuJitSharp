// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal interface IHashTableInfo<TKey>
{
    bool Equals(TKey x, TKey y);

    uint GetHashCode(TKey key);
}

internal sealed class DefaultHashTableInfo<TKey> : IHashTableInfo<TKey>
{
    public bool Equals(TKey x, TKey y)
    {
        if (typeof(TKey).IsValueType)
        {
            return EqualityComparer<TKey>.Default.Equals(x, y);
        }

        return ReferenceEquals(x, y);
    }

    public uint GetHashCode(TKey key)
    {
        if (key is null)
        {
            return 0;
        }

        if (typeof(TKey) == typeof(int))
        {
            return unchecked((uint)Unsafe.As<TKey, int>(ref key));
        }

        if (typeof(TKey) == typeof(uint))
        {
            return Unsafe.As<TKey, uint>(ref key);
        }

        if (typeof(TKey) == typeof(nint))
        {
            return unchecked((uint)Unsafe.As<TKey, nint>(ref key));
        }

        if (!typeof(TKey).IsValueType)
        {
            return unchecked((uint)RuntimeHelpers.GetHashCode(key));
        }

        throw new NotSupportedException($"No default native hash is defined for {typeof(TKey)}.");
    }
}

internal sealed class ComparerHashTableInfo<TKey> : IHashTableInfo<TKey>
{
    private readonly IEqualityComparer<TKey> _comparer;

    public ComparerHashTableInfo(IEqualityComparer<TKey> comparer)
    {
        _comparer = comparer;
    }

    public bool Equals(TKey x, TKey y) => _comparer.Equals(x, y);

    public uint GetHashCode(TKey key) => unchecked((uint)_comparer.GetHashCode(key!));
}

internal abstract class HashTableBase<TKey, TValue>
{
    private const int InitialNumBuckets = 8;

    internal struct Bucket
    {
        public bool IsFull;
        public uint FirstOffset;
        public uint NextOffset;
        public uint Hash;
        public TKey Key;
        public TValue Value;
    }

    private readonly IHashTableInfo<TKey> _keyInfo;
    private Bucket[]? _buckets;
    private int _numBuckets;
    private uint _numFullBuckets;

    protected HashTableBase(Bucket[]? buckets, int numBuckets, IHashTableInfo<TKey>? keyInfo)
    {
        _keyInfo = keyInfo ?? new DefaultHashTableInfo<TKey>();
        _buckets = buckets;
        _numBuckets = numBuckets;
        _numFullBuckets = 0;

        if (numBuckets > 0)
        {
            assert((numBuckets & (numBuckets - 1)) == 0);
            assert(buckets is not null);
            Array.Clear(buckets!);
        }
    }

    protected static Bucket[] AllocateBuckets(int count)
    {
        try
        {
            return new Bucket[count];
        }
        catch (OutOfMemoryException)
        {
            Globals.NOMEM();
            throw;
        }
    }

    private static bool Insert(Bucket[] buckets, int numBuckets, uint hash, TKey key, TValue value)
    {
        var mask = (uint)(numBuckets - 1);
        var homeIndex = (int)(hash & mask);

        ref var home = ref buckets[homeIndex];
        if (!home.IsFull)
        {
            assert(home.NextOffset == 0);

            home.IsFull = true;
            home.Hash = hash;
            home.Key = key;
            home.Value = value;
            return true;
        }

        var precedingIndexInChain = (uint)homeIndex;
        var nextIndexInChain = ((uint)homeIndex + home.FirstOffset) & mask;
        for (uint j = 1; j < (uint)numBuckets; j++)
        {
            var bucketIndex = (uint)((homeIndex + j) & mask);
            ref var bucket = ref buckets[(int)bucketIndex];
            if (bucketIndex == nextIndexInChain)
            {
                assert(bucket.IsFull);
                precedingIndexInChain = bucketIndex;
                nextIndexInChain = (bucketIndex + bucket.NextOffset) & mask;
            }
            else if (!bucket.IsFull)
            {
                bucket.IsFull = true;
                if (precedingIndexInChain == nextIndexInChain)
                {
                    bucket.NextOffset = 0;
                }
                else
                {
                    assert(((nextIndexInChain - bucketIndex) & mask) > 0);
                    bucket.NextOffset = (nextIndexInChain - bucketIndex) & mask;
                }

                var offset = (bucketIndex - precedingIndexInChain) & mask;
                assert(offset != 0);

                if (precedingIndexInChain == (uint)homeIndex)
                {
                    buckets[(int)precedingIndexInChain].FirstOffset = offset;
                }
                else
                {
                    buckets[(int)precedingIndexInChain].NextOffset = offset;
                }

                bucket.Hash = hash;
                bucket.Key = key;
                bucket.Value = value;
                return true;
            }
        }

        return false;
    }

    private bool TryGetBucket(uint hash, TKey key, out int precedingIndex, out int bucketIndex)
    {
        if (_numBuckets == 0)
        {
            precedingIndex = -1;
            bucketIndex = -1;
            return false;
        }

        var mask = (uint)(_numBuckets - 1);
        var index = (int)(hash & mask);
        ref var bucket = ref _buckets![index];
        if (bucket.IsFull && (bucket.Hash == hash) && _keyInfo.Equals(bucket.Key, key))
        {
            precedingIndex = index;
            bucketIndex = index;
            return true;
        }

        for (var offset = bucket.FirstOffset; offset != 0; offset = bucket.NextOffset)
        {
            var precedingIndexInChain = index;
            index = (int)(((uint)index + offset) & mask);
            bucket = ref _buckets[index];

            assert(bucket.IsFull);
            if ((bucket.Hash == hash) && _keyInfo.Equals(bucket.Key, key))
            {
                precedingIndex = precedingIndexInChain;
                bucketIndex = index;
                return true;
            }
        }

        precedingIndex = -1;
        bucketIndex = -1;
        return false;
    }

    private void Resize()
    {
        var currentBuckets = _buckets;
        if (_numBuckets > int.MaxValue / 2)
        {
            Globals.NOMEM();
        }

        var newNumBuckets = _numBuckets == 0 ? InitialNumBuckets : _numBuckets * 2;
        var newBuckets = AllocateBuckets(newNumBuckets);

        for (var currentIndex = 0; currentIndex < _numBuckets; currentIndex++)
        {
            ref var currentBucket = ref currentBuckets![currentIndex];
            if (!currentBucket.IsFull)
            {
                continue;
            }

            var inserted = Insert(newBuckets, newNumBuckets, currentBucket.Hash, currentBucket.Key,
                currentBucket.Value);
            assert(inserted);
            if (!inserted)
            {
                throw new InvalidOperationException("The hash table could not reinsert an occupied bucket.");
            }
        }

        _numBuckets = newNumBuckets;
        _buckets = newBuckets;
    }

    public uint Count() => _numFullBuckets;

    public void Clear()
    {
        if (_numBuckets > 0)
        {
            Array.Clear(_buckets!);
            _numFullBuckets = 0;
        }
    }

    public bool AddOrUpdate(TKey key, TValue value)
    {
        var hash = _keyInfo.GetHashCode(key);

        if (TryGetBucket(hash, key, out _, out var index))
        {
            _buckets![index].Value = value;
            return false;
        }

        if ((_numFullBuckets * 5) >= ((uint)_numBuckets * 4))
        {
            Resize();
        }

        var inserted = Insert(_buckets!, _numBuckets, hash, key, value);
        assert(inserted);
        if (!inserted)
        {
            throw new InvalidOperationException("The hash table has no free bucket.");
        }

        _numFullBuckets++;
        return true;
    }

    public bool TryRemoveRef(TKey key, ref TValue value)
    {
        var hash = _keyInfo.GetHashCode(key);
        if (!TryGetBucket(hash, key, out var precedingIndexInChain, out var bucketIndex))
        {
            return false;
        }

        ref var bucket = ref _buckets![bucketIndex];
        if (precedingIndexInChain != bucketIndex)
        {
            var mask = (uint)(_numBuckets - 1);
            var homeIndex = (int)(hash & mask);

            uint nextOffset;
            if (bucket.NextOffset == 0)
            {
                nextOffset = 0;
            }
            else
            {
                var nextIndexInChain = ((uint)bucketIndex + bucket.NextOffset) & mask;
                nextOffset = (nextIndexInChain - (uint)precedingIndexInChain) & mask;
            }

            if (precedingIndexInChain == homeIndex)
            {
                _buckets[(int)precedingIndexInChain].FirstOffset = nextOffset;
            }
            else
            {
                _buckets[(int)precedingIndexInChain].NextOffset = nextOffset;
            }
        }

        bucket.IsFull = false;
        bucket.NextOffset = 0;
        _numFullBuckets--;
        value = bucket.Value;
        return true;
    }

    public bool TryRemove(TKey key, out TValue value)
    {
        value = default!;
        return TryRemoveRef(key, ref value);
    }

    public void Remove(TKey key)
    {
        var removed = TryRemove(key, out _);
        assert(removed);
    }

    public bool TryGetValueRef(TKey key, ref TValue value)
    {
        if (!TryGetBucket(_keyInfo.GetHashCode(key), key, out _, out var index))
        {
            return false;
        }

        value = _buckets![index].Value;
        return true;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        value = default!;
        return TryGetValueRef(key, ref value);
    }

    public bool Contains(TKey key)
    {
        return TryGetBucket(_keyInfo.GetHashCode(key), key, out _, out _);
    }

#if DEBUG
    internal sealed class KeyValuePair
    {
        private readonly Bucket[]? _buckets;
        private readonly int _index;

        internal KeyValuePair()
        {
            _buckets = null;
            _index = 0;
        }

        internal KeyValuePair(Bucket[] buckets, int index)
        {
            _buckets = buckets;
            _index = index;
            assert(buckets[index].IsFull);
        }

        internal ref TKey Key() => ref _buckets![_index].Key;

        internal ref TValue Value() => ref _buckets![_index].Value;
    }

    internal struct Iterator : IEquatable<Iterator>
    {
        private readonly Bucket[]? _buckets;
        private readonly int _numBuckets;
        private int _index;

        public Iterator()
        {
            _buckets = null;
            _numBuckets = 0;
            _index = 0;
        }

        private Iterator(Bucket[]? buckets, int numBuckets, int index)
        {
            _buckets = buckets;
            _numBuckets = numBuckets;
            _index = index;
            assert((buckets is not null) || (numBuckets == 0));
            assert(index <= numBuckets);

            while ((_index != _numBuckets) && !_buckets![_index].IsFull)
            {
                _index++;
            }
        }

        internal readonly KeyValuePair Current
        {
            get
            {
                if (_index >= _numBuckets)
                {
                    return new KeyValuePair();
                }

                assert(_buckets![_index].IsFull);
                return new KeyValuePair(_buckets!, _index);
            }
        }

        internal static Iterator Begin(HashTableBase<TKey, TValue> owner)
        {
            return new Iterator(owner._buckets, owner._numBuckets, 0);
        }

        internal static Iterator End(HashTableBase<TKey, TValue> owner)
        {
            return new Iterator(owner._buckets, owner._numBuckets, owner._numBuckets);
        }

        private Iterator Next()
        {
            do
            {
                _index++;
            } while ((_index != _numBuckets) && !_buckets![_index].IsFull);

            return this;
        }

        public static Iterator operator ++(Iterator iterator) => iterator.Next();

        public static bool operator ==(Iterator left, Iterator right) =>
            ReferenceEquals(left._buckets, right._buckets) && (left._index == right._index);

        public static bool operator !=(Iterator left, Iterator right) => !(left == right);

        public readonly bool Equals(Iterator other) => this == other;

        public override readonly bool Equals(object? obj) => obj is Iterator other && this == other;

        public override readonly int GetHashCode() => HashCode.Combine(_buckets, _index);
    }

    internal Iterator begin() => Iterator.Begin(this);

    internal Iterator end() => Iterator.End(this);
#endif
}

internal sealed class HashTable<TKey, TValue> : HashTableBase<TKey, TValue>
{
    internal HashTable(IHashTableInfo<TKey>? keyInfo = null)
        : base(null, 0, keyInfo)
    {
    }

    internal HashTable(uint initialSize, IHashTableInfo<TKey>? keyInfo = null)
        : base(AllocateBuckets((int)RoundUp(initialSize)), (int)RoundUp(initialSize), keyInfo)
    {
    }

    private static uint RoundUp(uint initialSize)
    {
        assert(initialSize != 0);
        return 1u << (int)Globals.genLog2(initialSize);
    }
}

internal sealed class SmallHashTable<TKey, TValue> : HashTableBase<TKey, TValue>
{
    internal SmallHashTable(uint numInlineBuckets = 8, IHashTableInfo<TKey>? keyInfo = null)
        : this(CreateInlineBuckets(numInlineBuckets), keyInfo)
    {
    }

    private SmallHashTable(Bucket[] buckets, IHashTableInfo<TKey>? keyInfo)
        : base(buckets, buckets.Length, keyInfo)
    {
    }

    private static Bucket[] CreateInlineBuckets(uint numInlineBuckets)
    {
        var log2 = numInlineBuckets == 0 ? 0 : BitOperations.Log2(numInlineBuckets);
        return AllocateBuckets(1 << (int)log2);
    }
}
