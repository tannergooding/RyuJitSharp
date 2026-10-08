// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

internal sealed class AddCodeDscMap
{
    private sealed class KeyFuncs : JitKeyFuncsDefEquals<Compiler.AddCodeDscKey>
    {
        public override uint GetHashCode(Compiler.AddCodeDscKey key) => unchecked((uint)key.GetHashCode());
    }

    private readonly JitHashTable<Compiler.AddCodeDscKey, Compiler.AddCodeDsc> _table =
        new(new KeyFuncs());

    internal int Count => checked((int)_table.GetCount());

    internal IEnumerable<Compiler.AddCodeDscKey> Keys => _table.Keys();

    internal IEnumerable<Compiler.AddCodeDsc> Values => _table.Values();

    internal Compiler.AddCodeDsc this[Compiler.AddCodeDscKey key]
    {
        get
        {
            if (!_table.Lookup(key, out var value))
            {
                throw new KeyNotFoundException();
            }

            return value;
        }
        set
        {
            _ = _table.Set(key, value);
        }
    }

    internal void Add(Compiler.AddCodeDscKey key, Compiler.AddCodeDsc value)
    {
        if (_table.Lookup(key))
        {
            throw new ArgumentException("An item with the same key has already been added.", nameof(key));
        }

        _ = _table.Set(key, value);
    }

    internal bool Remove(Compiler.AddCodeDscKey key) => _table.Remove(key);

    internal bool TryGetValue(Compiler.AddCodeDscKey key, out Compiler.AddCodeDsc value) =>
        _table.Lookup(key, out value);
}
