// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal interface IJitHashTableBehavior
{
    uint GrowthFactorNumerator { get; }

    uint GrowthFactorDenominator { get; }

    uint DensityFactorNumerator { get; }

    uint DensityFactorDenominator { get; }

    uint MinimumAllocation { get; }

    void NoMemory();
}

internal sealed class JitHashTableBehavior : IJitHashTableBehavior
{
    public uint GrowthFactorNumerator => 3;

    public uint GrowthFactorDenominator => 2;

    public uint DensityFactorNumerator => 3;

    public uint DensityFactorDenominator => 4;

    public uint MinimumAllocation => 7;

    public void NoMemory() => Globals.NOMEM();
}

internal abstract class JitKeyFuncsDefEquals<TKey> : IHashTableInfo<TKey>
{
    public bool Equals(TKey x, TKey y)
    {
        if (!typeof(TKey).IsValueType)
        {
            return ReferenceEquals(x, y);
        }

        if (typeof(TKey) == typeof(float))
        {
            return Unsafe.As<TKey, float>(ref x) == Unsafe.As<TKey, float>(ref y);
        }

        if (typeof(TKey) == typeof(double))
        {
            return Unsafe.As<TKey, double>(ref x) == Unsafe.As<TKey, double>(ref y);
        }

        return EqualityComparer<TKey>.Default.Equals(x, y);
    }

    public abstract uint GetHashCode(TKey key);
}

internal sealed class JitSmallPrimitiveKeyFuncs<TKey> : JitKeyFuncsDefEquals<TKey>
    where TKey : unmanaged
{
    public override uint GetHashCode(TKey value)
    {
        return PrimitiveHash(ref value);
    }

    internal static uint PrimitiveHash(ref TKey value)
    {
        var type = typeof(TKey);
        if (type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
        }

        if (type == typeof(byte))
        {
            return Unsafe.As<TKey, byte>(ref value);
        }
        if (type == typeof(sbyte))
        {
            return unchecked((uint)Unsafe.As<TKey, sbyte>(ref value));
        }
        if (type == typeof(ushort))
        {
            return Unsafe.As<TKey, ushort>(ref value);
        }
        if (type == typeof(short))
        {
            return unchecked((uint)Unsafe.As<TKey, short>(ref value));
        }
        if (type == typeof(uint))
        {
            return Unsafe.As<TKey, uint>(ref value);
        }
        if (type == typeof(int))
        {
            return unchecked((uint)Unsafe.As<TKey, int>(ref value));
        }
        if (type == typeof(ulong))
        {
            return unchecked((uint)Unsafe.As<TKey, ulong>(ref value));
        }
        if (type == typeof(long))
        {
            return unchecked((uint)Unsafe.As<TKey, long>(ref value));
        }
        if (type == typeof(nuint))
        {
            return unchecked((uint)Unsafe.As<TKey, nuint>(ref value));
        }
        if (type == typeof(nint))
        {
            return unchecked((uint)Unsafe.As<TKey, nint>(ref value));
        }

        throw new NotSupportedException($"Small primitive keys of type {typeof(TKey)} are not supported.");
    }
}

internal sealed class JitLargePrimitiveKeyFuncs<TKey> : JitKeyFuncsDefEquals<TKey>
    where TKey : unmanaged
{
    public override uint GetHashCode(TKey value)
    {
        var size = Unsafe.SizeOf<TKey>();
        if (size == 8)
        {
            var bits = Unsafe.As<TKey, ulong>(ref value);
            return unchecked((uint)(bits >> 32) ^ (uint)bits);
        }

        if (size == 4)
        {
            return Unsafe.As<TKey, uint>(ref value);
        }

        if ((size == 2) || (size == 1))
        {
            return JitSmallPrimitiveKeyFuncs<TKey>.PrimitiveHash(ref value);
        }

        assert(false, "Unsupported large primitive key size.");
        throw new NotSupportedException($"Large primitive keys of size {size} are not supported.");
    }
}

internal sealed class JitPtrKeyFuncs<TKey> : JitKeyFuncsDefEquals<TKey>
{
    public override uint GetHashCode(TKey key)
    {
        if (typeof(TKey) == typeof(BasicBlock))
        {
            // BasicBlock key functions hash its stable ID instead of its address.
            return unchecked((uint)((BasicBlock)(object)key!).GetHashCode());
        }

        if (key is null)
        {
            return 0;
        }

        if (key is nint address)
        {
            return unchecked((uint)address);
        }

        if (key is nuint unsignedAddress)
        {
            return unchecked((uint)unsignedAddress);
        }

        if (!typeof(TKey).IsValueType)
        {
            return unchecked((uint)RuntimeHelpers.GetHashCode(key!));
        }

        throw new NotSupportedException($"Pointer keys of type {typeof(TKey)} are not supported.");
    }
}

internal sealed class JitHashTable<TKey, TValue>
{
    internal sealed class Node
    {
        private TKey _key;
        private TValue _value;
        private bool _isAlive;

        internal Node? Next;

        internal Node(Node? next, TKey key, TValue value)
        {
            Next = next;
            _key = key;
            _value = value;
            _isAlive = true;
        }

        public TKey GetKey()
        {
            ObjectDisposedException.ThrowIf(!_isAlive, this);
            return _key;
        }

        public TValue GetValue()
        {
            ObjectDisposedException.ThrowIf(!_isAlive, this);
            return _value;
        }

        public ref TValue GetValueRef()
        {
            ObjectDisposedException.ThrowIf(!_isAlive, this);
            return ref _value;
        }

        internal void Invalidate()
        {
            _isAlive = false;
            Next = null;
            _key = default!;
            _value = default!;
        }
    }

    internal readonly struct ValueReference
    {
        private readonly Node? _node;

        internal ValueReference(Node node)
        {
            _node = node;
        }

        internal ref TValue Value
        {
            get
            {
                if (_node is null)
                {
                    throw new InvalidOperationException("The value reference is uninitialized.");
                }

                return ref _node.GetValueRef();
            }
        }
    }

    internal enum SetKind
    {
        None,
        Overwrite,
    }

    private readonly IHashTableInfo<TKey> _keyInfo;
    private readonly IJitHashTableBehavior _behavior;
    private Node?[]? _table;
    private JitPrimeInfo _tableSizeInfo;
    private uint _tableCount;
    private uint _tableMax;

    internal JitHashTable(IHashTableInfo<TKey> keyInfo, IJitHashTableBehavior? behavior = null)
    {
        ArgumentNullException.ThrowIfNull(keyInfo);
        _keyInfo = keyInfo;
        _behavior = behavior ?? new JitHashTableBehavior();
        _table = null;
        _tableSizeInfo = new JitPrimeInfo();
        _tableCount = 0;
        _tableMax = 0;
    }

    internal bool Lookup(TKey key, ValueReference? value = null)
    {
        var node = FindNode(key);
        if (node is null)
        {
            return false;
        }

        if (value is ValueReference valueReference)
        {
            valueReference.Value = node.GetValue();
        }

        return true;
    }

    internal bool Lookup(TKey key, out TValue value)
    {
        var node = FindNode(key);
        if (node is null)
        {
            value = default!;
            return false;
        }

        value = node.GetValue();
        return true;
    }

    internal ValueReference? LookupPointer(TKey key)
    {
        var node = FindNode(key);
        return node is null ? null : new ValueReference(node);
    }

    internal ValueReference LookupPointerOrAdd(TKey key, TValue defaultValue)
    {
        CheckGrowth();
        assert(_tableSizeInfo.prime != 0);

        var index = GetIndexForKey(key);
        for (var node = _table![index]; node is not null; node = node.Next)
        {
            if (_keyInfo.Equals(key, node.GetKey()))
            {
                return new ValueReference(node);
            }
        }

        var newNode = AllocateNode(_table[index], key, defaultValue);
        _table[index] = newNode;
        _tableCount++;
        return new ValueReference(newNode);
    }

    internal bool Set(TKey key, TValue value, SetKind kind = SetKind.None)
    {
        CheckGrowth();
        assert(_tableSizeInfo.prime != 0);

        var index = GetIndexForKey(key);
        var node = _table![index];
        while ((node is not null) && !_keyInfo.Equals(key, node.GetKey()))
        {
            node = node.Next;
        }

        if (node is not null)
        {
            assert(kind == SetKind.Overwrite);
            node.GetValueRef() = value;
            return true;
        }

        _table[index] = AllocateNode(_table[index], key, value);
        _tableCount++;
        return false;
    }

    internal ValueReference Emplace(TKey key, Func<TValue> valueFactory)
    {
        CheckGrowth();
        assert(_tableSizeInfo.prime != 0);

        var index = GetIndexForKey(key);
        var node = _table![index];
        while ((node is not null) && !_keyInfo.Equals(key, node.GetKey()))
        {
            node = node.Next;
        }

        if (node is null)
        {
            node = AllocateNode(_table[index], key, valueFactory());
            _table[index] = node;
            _tableCount++;
        }

        return new ValueReference(node);
    }

    internal bool Remove(TKey key)
    {
        if (_tableSizeInfo.prime == 0)
        {
            throw new InvalidOperationException("The hash table must be allocated before removing keys.");
        }

        var index = GetIndexForKey(key);
        var node = _table![index];
        Node? previous = null;

        while ((node is not null) && !_keyInfo.Equals(key, node.GetKey()))
        {
            previous = node;
            node = node.Next;
        }

        if (node is null)
        {
            return false;
        }

        if (previous is null)
        {
            _table[index] = node.Next;
        }
        else
        {
            previous.Next = node.Next;
        }

        _tableCount--;
        node.Invalidate();
        return true;
    }

    internal void RemoveAll()
    {
        for (var i = 0; i < _tableSizeInfo.prime; i++)
        {
            for (var node = _table![i]; node is not null;)
            {
                var next = node.Next;
                node.Invalidate();
                node = next;
            }
        }

        _table = null;
        _tableSizeInfo = new JitPrimeInfo();
        _tableCount = 0;
        _tableMax = 0;
    }

    internal uint GetCount() => _tableCount;

    private static Node AllocateNode(Node? next, TKey key, TValue value)
    {
        try
        {
            return new Node(next, key, value);
        }
        catch (OutOfMemoryException)
        {
            Globals.NOMEM();
            throw;
        }
    }

    private int GetIndexForKey(TKey key)
    {
        return (int)_tableSizeInfo.magicNumberRem(_keyInfo.GetHashCode(key));
    }

    private Node? FindNode(TKey key)
    {
        if (_tableSizeInfo.prime == 0)
        {
            return null;
        }

        var index = GetIndexForKey(key);
        var node = _table![(int)index];
        while ((node is not null) && !_keyInfo.Equals(key, node.GetKey()))
        {
            node = node.Next;
        }

        assert((node is null) || _keyInfo.Equals(key, node.GetKey()));
        return node;
    }

    private void Grow()
    {
        var newSize = unchecked(_tableCount * _behavior.GrowthFactorNumerator) /
            _behavior.GrowthFactorDenominator;
        newSize = unchecked(newSize * _behavior.DensityFactorDenominator) /
            _behavior.DensityFactorNumerator;

        if (newSize < _behavior.MinimumAllocation)
        {
            newSize = _behavior.MinimumAllocation;
        }

        if (newSize < _tableCount)
        {
            _behavior.NoMemory();
            Globals.NOMEM();
        }

        Reallocate(newSize);
    }

    private void CheckGrowth()
    {
        if (_tableCount == _tableMax)
        {
            Grow();
        }
    }

    internal void Reallocate(uint newTableSize)
    {
        assert(newTableSize >=
            unchecked(_tableCount * _behavior.DensityFactorDenominator) / _behavior.DensityFactorNumerator);

        var newPrime = NextPrime(newTableSize);
        Node?[] newTable;
        try
        {
            newTable = new Node?[(int)newPrime.prime];
        }
        catch (OutOfMemoryException)
        {
            Globals.NOMEM();
            throw;
        }

        for (var i = 0; i < _tableSizeInfo.prime; i++)
        {
            var node = _table![(int)i];
            while (node is not null)
            {
                var next = node.Next;
                var newIndex = newPrime.magicNumberRem(_keyInfo.GetHashCode(node.GetKey()));
                node.Next = newTable[(int)newIndex];
                newTable[(int)newIndex] = node;
                node = next;
            }
        }

        _table = newTable;
        _tableSizeInfo = newPrime;
        _tableMax = unchecked(newPrime.prime * _behavior.DensityFactorNumerator) /
            _behavior.DensityFactorDenominator;
    }

    private JitPrimeInfo NextPrime(uint number)
    {
        foreach (var primeInfo in Globals.jitPrimeInfo)
        {
            if (primeInfo.prime >= number)
            {
                return primeInfo;
            }
        }

        _behavior.NoMemory();
        Globals.NOMEM();
        throw new InvalidOperationException("The hash-table behavior returned from its no-memory handler.");
    }

    internal ref TValue this[TKey key]
    {
        get
        {
            var value = LookupPointer(key);
            assert(value is not null);
            if (value is not ValueReference valueReference)
            {
                throw new KeyNotFoundException();
            }

            return ref valueReference.Value;
        }
    }

    internal IEnumerable<TKey> Keys()
    {
        using var iterator = new NodeIterator(this);
        while (iterator.MoveNext())
        {
            yield return iterator.Current.GetKey();
        }
    }

    internal IEnumerable<TValue> Values()
    {
        using var iterator = new NodeIterator(this);
        while (iterator.MoveNext())
        {
            yield return iterator.Current.GetValue();
        }
    }

    internal IEnumerable<Node> Entries()
    {
        using var iterator = new NodeIterator(this);
        while (iterator.MoveNext())
        {
            yield return iterator.Current;
        }
    }

    private sealed class NodeIterator : IEnumerator<Node>
    {
        private readonly Node?[]? _table;
        private readonly uint _tableSize;
        private uint _index;
        private Node? _node;
        private bool _started;

        internal NodeIterator(JitHashTable<TKey, TValue> owner)
        {
            _table = owner._table;
            _tableSize = owner._tableSizeInfo.prime;
            _index = 0;
            _node = null;
            _started = false;
        }

        public Node Current => _node ?? throw new InvalidOperationException();

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                while ((_index < _tableSize) && (_table![(int)_index] is null))
                {
                    _index++;
                }

                if (_index < _tableSize)
                {
                    _node = _table![(int)_index];
                    return true;
                }

                return false;
            }

            if (_node is not null)
            {
                _node = _node.Next;
                if (_node is not null)
                {
                    return true;
                }

                _index++;
            }

            while ((_index < _tableSize) && (_table![(int)_index] is null))
            {
                _index++;
            }

            _node = _index < _tableSize ? _table![(int)_index] : null;
            return _node is not null;
        }

        public void Reset()
        {
            _index = 0;
            _node = null;
            _started = false;
        }

        public void Dispose()
        {
        }
    }
}
