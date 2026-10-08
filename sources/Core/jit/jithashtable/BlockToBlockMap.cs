// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

// Clone-edge construction observes the native BlockToBlockMap bucket traversal order.
public sealed class BlockToBlockMapDictionary : IReadOnlyDictionary<BasicBlock, BasicBlock>
{
    private readonly JitHashTable<BasicBlock, BasicBlock> _table =
        new(new JitPtrKeyFuncs<BasicBlock>());

    [SuppressMessage("Design", "CA1043:Use Integral Or String Argument For Indexers",
        Justification = "BasicBlock is the key type of the native BlockToBlockMap.")]
    public BasicBlock this[BasicBlock key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            if (_table.Lookup(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException();
        }
    }

    public IEnumerable<BasicBlock> Keys => _table.Keys();

    public IEnumerable<BasicBlock> Values => _table.Values();

    public int Count => checked((int)_table.GetCount());

    public void Add(BasicBlock key, BasicBlock value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_table.Lookup(key, out _))
        {
            throw new ArgumentException("An item with the same key has already been added.", nameof(key));
        }

        _ = _table.Set(key, value);
    }

    public void Add(KeyValuePair<BasicBlock, BasicBlock> entry)
    {
        Add(entry.Key, entry.Value);
    }

    public void Clear() => _table.RemoveAll();

    public bool ContainsKey(BasicBlock key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _table.Lookup(key);
    }

    public BasicBlock GetValue(BasicBlock key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_table.Lookup(key, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException();
    }

    public void Set(BasicBlock key, BasicBlock value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _ = _table.Set(key, value, JitHashTable<BasicBlock, BasicBlock>.SetKind.Overwrite);
    }

    public bool TryAdd(BasicBlock key, BasicBlock value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_table.Lookup(key))
        {
            return false;
        }

        _ = _table.Set(key, value);
        return true;
    }

    public bool TryGetValue(BasicBlock key, [MaybeNullWhen(false)] out BasicBlock value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _table.Lookup(key, out value);
    }

    public IEnumerator<KeyValuePair<BasicBlock, BasicBlock>> GetEnumerator()
    {
        foreach (var entry in _table.Entries())
        {
            yield return new KeyValuePair<BasicBlock, BasicBlock>(entry.GetKey(), entry.GetValue());
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
