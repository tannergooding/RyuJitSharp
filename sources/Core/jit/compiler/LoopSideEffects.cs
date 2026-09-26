// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed class LoopSideEffects
{
    private NativeLoopKeyOrder<Pointer<CORINFO_FIELD_STRUCT_>>? _modifiedFieldOrder;

    private NativeLoopKeyOrder<Pointer<CORINFO_CLASS_STRUCT_>>? _modifiedElemTypeOrder;

    public bool[] HasMemoryHavoc { get; } = new bool[(int)MemoryKindCount];

    public VARSET_TP VarInOut = VarSetOps.UninitVal();

    public VARSET_TP VarUseDef = VarSetOps.UninitVal();

    public FieldHandleSet? FieldsModified;

    public ClassHandleSet? ArrayElemTypesModified;

    public bool ContainsCall;

    public void AddVariableLiveness(Compiler compiler, BasicBlock block)
    {
        VarSetOps.UnionD(compiler, VarInOut, block.bbLiveIn);
        VarSetOps.UnionD(compiler, VarInOut, block.bbLiveOut);

        VarSetOps.UnionD(compiler, VarUseDef, block.bbVarUse);
        VarSetOps.UnionD(compiler, VarUseDef, block.bbVarDef);
    }

    public unsafe void AddModifiedField(Compiler compiler, CORINFO_FIELD_HANDLE fieldHandle, FieldKindForVN fieldKind)
    {
        FieldsModified ??= [];
        _modifiedFieldOrder ??= new NativeLoopKeyOrder<Pointer<CORINFO_FIELD_STRUCT_>>();
        _modifiedFieldOrder.Add(new Pointer<CORINFO_FIELD_STRUCT_>(fieldHandle),
            unchecked((uint)(nuint)fieldHandle));
        FieldsModified[fieldHandle] = fieldKind;
    }

    public unsafe void AddModifiedElemType(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
    {
        ArrayElemTypesModified ??= [];
        _modifiedElemTypeOrder ??= new NativeLoopKeyOrder<Pointer<CORINFO_CLASS_STRUCT_>>();
        _modifiedElemTypeOrder.Add(new Pointer<CORINFO_CLASS_STRUCT_>(classHandle),
            unchecked((uint)(nuint)classHandle));
        ArrayElemTypesModified[classHandle] = true;
    }

    public IEnumerable<KeyValuePair<Pointer<CORINFO_FIELD_STRUCT_>, FieldKindForVN>>
        EnumerateModifiedFieldsInNativeOrder()
    {
        if (_modifiedFieldOrder is null)
        {
            yield break;
        }

        assert(FieldsModified is not null);
        foreach (var field in _modifiedFieldOrder.Keys())
        {
            yield return new KeyValuePair<Pointer<CORINFO_FIELD_STRUCT_>, FieldKindForVN>(
                field, FieldsModified[field]);
        }
    }

    public IEnumerable<Pointer<CORINFO_CLASS_STRUCT_>> EnumerateModifiedElemTypesInNativeOrder()
    {
        if (_modifiedElemTypeOrder is null)
        {
            yield break;
        }

        foreach (var elemType in _modifiedElemTypeOrder.Keys())
        {
            yield return elemType;
        }
    }

    private sealed class NativeLoopKeyOrder<TKey> where TKey : notnull
    {
        // The native table grows to these primes; its iterator visits buckets
        // in index order and follows their head-inserted chains.
        private static ReadOnlySpan<int> PrimeSizes => [
            9, 23, 59, 131, 239, 433, 761, 1399, 2473, 4327, 7499,
            12973, 22433, 46559, 96581, 200341, 415517, 861719,
            1787021, 3705617, 7684087, 15933877, 33040633, 68513161,
            142069021, 294594427, 733045421,
        ];

        private List<(TKey Key, uint Hash)>?[] _buckets = [];
        private uint _count;
        private uint _max;

        public void Add(TKey key, uint hash)
        {
            // Native Set checks growth before looking for an existing key.
            if (_count == _max)
            {
                Grow();
            }

            var bucket = _buckets[hash % (uint)_buckets.Length] ??= [];
            foreach (var (existingKey, _) in bucket)
            {
                if (EqualityComparer<TKey>.Default.Equals(existingKey, key))
                {
                    return;
                }
            }

            bucket.Insert(0, (key, hash));
            _count++;
        }

        public IEnumerable<TKey> Keys()
        {
            foreach (var bucket in _buckets)
            {
                if (bucket is not null)
                {
                    foreach (var (key, _) in bucket)
                    {
                        yield return key;
                    }
                }
            }
        }

        private void Grow()
        {
            var newSize = unchecked(((_count * 3) / 2 * 4) / 3);
            newSize = Math.Max(newSize, 7u);
            if (newSize < _count)
            {
                Globals.NOMEM();
            }

            var prime = 0;
            foreach (var candidate in PrimeSizes)
            {
                if ((uint)candidate >= newSize)
                {
                    prime = candidate;
                    break;
                }
            }

            if (prime == 0)
            {
                Globals.NOMEM();
            }

            var newBuckets = new List<(TKey Key, uint Hash)>?[prime];
            foreach (var bucket in _buckets)
            {
                if (bucket is not null)
                {
                    foreach (var (key, hash) in bucket)
                    {
                        var newBucket = newBuckets[hash % (uint)prime] ??= [];
                        newBucket.Insert(0, (key, hash));
                    }
                }
            }

            _buckets = newBuckets;
            _max = (uint)prime * 3 / 4;
        }
    }
}
