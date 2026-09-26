// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private readonly record struct MapSelectWorkCacheEntry(ValueNum Result, ValueNum[] MemoryDependencies);

    private readonly Dictionary<(ValueNum Map, ValueNum Index), MapSelectWorkCacheEntry> _mapSelectWorkCache = [];
    private readonly List<(ValueNum Map, ValueNum Index)> _fixedPointMapSels = [];
#if DEBUG
    private uint _numMapSels;
#endif

    private sealed class SmallValueNumSet
    {
        private readonly List<ValueNum> _values = [];
        private HashSet<ValueNum>? _set;

        public bool Add(ValueNum vn)
        {
            if (_set is null)
            {
                if (_values.Contains(vn))
                {
                    return false;
                }

                if (_values.Count == 4)
                {
                    _set = [.. _values];
                    _ = _set.Add(vn);
                }
            }
            else if (!_set.Add(vn))
            {
                return false;
            }

            _values.Add(vn);
            return true;
        }

        public void AddRange(IEnumerable<ValueNum> values)
        {
            foreach (var vn in values)
            {
                _ = Add(vn);
            }
        }

        public ValueNum[] ToArray() => [.. _values];
    }

    private bool MapIsPrecise(ValueNum map) => TypeOfVN(map) is TYP_HEAP or TYP_MEM;

    private bool MapIsPhysical(ValueNum map) => !MapIsPrecise(map);

    public ValueNum EncodePhysicalSelector(uint offset, uint size)
    {
        assert(size != 0);
        return VNForLongCon(unchecked((long)((ulong)offset | ((ulong)size << 32))));
    }

    public uint DecodePhysicalSelector(ValueNum selector, out uint size)
    {
        var value = unchecked((ulong)ConstantValue<long>(selector));
        size = (uint)(value >> 32);
        return unchecked((uint)value);
    }

    public ValueNum VNForMapStore(ValueNum map, ValueNum index, ValueNum value)
    {
        assert(MapIsPrecise(map));

        var block = _compiler.compCurBB
            ?? throw new FatalJitException("A precise map store requires the current block.");
        assert(_compiler._blockToLoop is not null);
        var loop = _compiler._blockToLoop.GetLoop(block);
        var loopIndex = loop?.Index ?? NoLoop;
        var result = VNForFunc(TypeOfVN(map), VNF_MapStore, map, index, value, loopIndex);

#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"    VNForMapStore({map}, {index}, {value}):{VNMapTypeName(TypeOfVN(result))} " +
                $"in {FMT_BB(block.bbNum)} returns ");
            _compiler.vnPrint(result, 1);
            JITDUMP("\n");
        }
#endif
        return result;
    }

    public ValueNum VNForMapPhysicalStore(ValueNum map, uint offset, uint size, ValueNum value)
    {
        assert(MapIsPhysical(map));
        var selector = EncodePhysicalSelector(offset, size);
        var result = VNForFunc(TypeOfVN(map), VNF_MapPhysicalStore, map, selector, value);

#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"    VNForMapPhysicalStore:{VNMapTypeName(TypeOfVN(result))} returns ");
            _compiler.vnPrint(result, 1);
            JITDUMP("\n");
        }
#endif
        return result;
    }

    public ValueNum VNForMapSelect(ValueNumKind vnk, var_types type, ValueNum map, ValueNum index)
    {
        assert(MapIsPrecise(map));
        var result = VNForMapSelectInner(vnk, type, map, index);

#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"    VNForMapSelect({map}, {index}):{VNMapTypeName(type)} returns ");
            _compiler.vnPrint(result, 1);
            JITDUMP("\n");
        }
#endif
        return result;
    }

    public ValueNum VNForMapPhysicalSelect(ValueNumKind vnk, var_types type, ValueNum map, uint offset, uint size)
    {
        assert(MapIsPhysical(map));
        var selector = EncodePhysicalSelector(offset, size);
        var result = VNForMapSelectInner(vnk, type, map, selector);

#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"    VNForMapPhysicalSelect({map}, ");
            DumpPhysicalSelector(selector);
            JITDUMP($"):{VNMapTypeName(type)} returns ");
            _compiler.vnPrint(result, 1);
            JITDUMP("\n");
        }
#endif
        return result;
    }

    private ValueNum VNForMapSelectInner(ValueNumKind vnk, var_types type, ValueNum map, ValueNum index)
    {
        var budget = _mapSelectBudget;
        var usedRecursiveVN = false;
        var memoryDependencies = new SmallValueNumSet();
        var result = VNForMapSelectWork(vnk, type, map, index, ref budget, ref usedRecursiveVN, memoryDependencies);

        assert((budget >= 0) && (budget <= _mapSelectBudget));

        if ((_compiler.compCurBB is BasicBlock block) && (_compiler.compCurTree is GenTree tree))
        {
            assert(_compiler._blockToLoop is not null);
            if (_compiler._blockToLoop.GetLoop(block) is not null)
            {
                foreach (var memoryVN in memoryDependencies.ToArray())
                {
                    _compiler.optRecordLoopMemoryDependence(tree, block, memoryVN);
                }
            }
        }

        return result;
    }

    private ValueNum VNForMapSelectWork(ValueNumKind vnk, var_types type, ValueNum map, ValueNum index,
        ref int budget, ref bool usedRecursiveVN, SmallValueNumSet memoryDependencies)
    {
        while (true)
        {
            assert((map != NoVN) && (index != NoVN));
            assert((map == VNNormalValue(map)) && (index == VNNormalValue(index)));
            usedRecursiveVN = false;

#if DEBUG
            _numMapSels++;
            var limit = JitConfig.JitVNMapSelLimit;
            assert((limit == 0) || (_numMapSels < limit));
#endif
            var key = (Map: map, Index: index);
            if (_mapSelectWorkCache.TryGetValue(key, out var entry))
            {
                memoryDependencies.AddRange(entry.MemoryDependencies);
                return entry.Result;
            }

            if (_fixedPointMapSels.Contains(key))
            {
                usedRecursiveVN = true;
                return RecursiveVN;
            }

            if (budget == 0)
            {
                var opaque = VNForExpr(null, type);
                _mapSelectWorkCache.Add(key, new(opaque, []));
                return opaque;
            }
            budget--;

            var recMemoryDependencies = new SmallValueNumSet();
            VNFuncApp funcApp = default;
            if (GetVNFunc(map, ref funcApp))
            {
                switch (funcApp.Func)
                {
                    case VNF_MapStore:
                    {
                        assert(MapIsPrecise(map));
                        if (funcApp.GetArg(1) == index)
                        {
                            _ = memoryDependencies.Add(funcApp.GetArg(0));
                            return funcApp.GetArg(2);
                        }

                        if (IsVNConstant(index) && IsVNConstant(funcApp.GetArg(1)))
                        {
                            map = funcApp.GetArg(0);
                            continue;
                        }
                        break;
                    }

                    case VNF_MapPhysicalStore:
                    {
                        assert(MapIsPhysical(map));
                        var storeSelector = funcApp.GetArg(1);
                        if (index == storeSelector)
                        {
                            return funcApp.GetArg(2);
                        }

                        var selectOffset = DecodePhysicalSelector(index, out var selectSize);
                        var storeOffset = DecodePhysicalSelector(storeSelector, out var storeSize);
                        var selectEndOffset = unchecked(selectOffset + selectSize);
                        var storeEndOffset = unchecked(storeOffset + storeSize);

                        if ((storeOffset <= selectOffset) && (selectEndOffset <= storeEndOffset))
                        {
                            map = funcApp.GetArg(2);
                            index = EncodePhysicalSelector(unchecked(selectOffset - storeOffset), selectSize);
                            continue;
                        }

                        if ((storeEndOffset <= selectOffset) || (selectEndOffset <= storeOffset))
                        {
                            map = funcApp.GetArg(0);
                            continue;
                        }
                        break;
                    }

                    case VNF_BitCast:
                    {
                        assert(MapIsPhysical(map));
                        map = funcApp.GetArg(0);
                        continue;
                    }

                    case VNF_ZeroObj:
                    {
                        assert(MapIsPhysical(map));
                        if (type is not TYP_STRUCT)
                        {
                            return VNZeroForType(type);
                        }
                        break;
                    }
                }
            }
            else
            {
                VNPhiDef phiDef = default;
                VNMemoryPhiDef memoryPhiDef = default;
                var isLocalPhi = GetPhiDef(map, ref phiDef);
                var isMemoryPhi = !isLocalPhi && GetMemoryPhiDef(map, ref memoryPhiDef);
                if (isLocalPhi || isMemoryPhi)
                {
                    var args = isLocalPhi ? phiDef.SsaArgs : memoryPhiDef.SsaArgs;
                    _fixedPointMapSels.Add(key);

                    var sameSelResult = RecursiveVN;
                    var allSame = true;
                    foreach (var ssaArg in args.Span)
                    {
                        if (budget <= 0)
                        {
                            allSame = false;
                            break;
                        }

                        var phiArgVN = isLocalPhi
                            ? _compiler.lvaGetDesc(phiDef.LclNum).GetPerSsaData(ssaArg)._vnPair[vnk]
                            : _compiler.GetMemoryPerSsaData(ssaArg)._vnPair[vnk];
                        if (phiArgVN == NoVN)
                        {
                            allSame = false;
                            break;
                        }

                        var usedRecursiveArg = false;
                        var current = VNForMapSelectWork(vnk, type, phiArgVN, index,
                            ref budget, ref usedRecursiveArg, recMemoryDependencies);
                        usedRecursiveVN |= usedRecursiveArg;
                        if (sameSelResult == RecursiveVN)
                        {
                            sameSelResult = current;
                        }
                        if ((current != RecursiveVN) && (current != sameSelResult))
                        {
                            allSame = false;
                            break;
                        }
                    }

                    assert(_fixedPointMapSels[^1] == key);
                    _fixedPointMapSels.RemoveAt(_fixedPointMapSels.Count - 1);

                    if (allSame && (sameSelResult != RecursiveVN))
                    {
                        if (!usedRecursiveVN)
                        {
                            var cached = new MapSelectWorkCacheEntry(sameSelResult, recMemoryDependencies.ToArray());
                            assert(!_mapSelectWorkCache.TryGetValue(key, out var prior) ||
                                (prior.Result == sameSelResult));
                            _mapSelectWorkCache[key] = cached;
                        }

                        memoryDependencies.AddRange(recMemoryDependencies.ToArray());
                        return sameSelResult;
                    }
                }
            }

            if (!_mapSelectWorkCache.TryGetValue(key, out entry))
            {
                var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func2);
                var offset = chunk.AllocVN();
                var record = chunk.FuncApp(offset, 2).Span;
                record[0] = (int)VNF_MapSelect;
                record[1] = map;
                record[2] = index;
                entry = new MapSelectWorkCacheEntry(
                    unchecked(chunk.BaseVN + offset), recMemoryDependencies.ToArray());
                _mapSelectWorkCache.Add(key, entry);
            }

            memoryDependencies.AddRange(recMemoryDependencies.ToArray());
            return entry.Result;
        }
    }

    private static string VNMapTypeName(var_types type) => type switch
    {
        TYP_HEAP => "heap",
        TYP_MEM => "mem",
        TYP_VOID => "void",
        TYP_BYTE => "byte",
        TYP_UBYTE => "ubyte",
        TYP_SHORT => "short",
        TYP_USHORT => "ushort",
        TYP_INT => "int",
        TYP_UINT => "uint",
        TYP_LONG => "long",
        TYP_ULONG => "ulong",
        TYP_FLOAT => "float",
        TYP_DOUBLE => "double",
        TYP_REF => "ref",
        TYP_BYREF => "byref",
        TYP_STRUCT => "struct",
#if FEATURE_SIMD
        TYP_SIMD8 => "simd8",
        TYP_SIMD12 => "simd12",
        TYP_SIMD16 => "simd16",
#if TARGET_XARCH
        TYP_SIMD32 => "simd32",
        TYP_SIMD64 => "simd64",
#elif TARGET_ARM64
        TYP_SIMD => "simd",
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        TYP_MASK => "mask",
#endif
#endif
        _ => throw new FatalJitException("Unrecognized map value type."),
    };

#if DEBUG
    private void DumpPhysicalSelector(ValueNum selector)
    {
        var offset = DecodePhysicalSelector(selector, out var size);
        jitprintf(size == 1 ? $"[{offset}]" : $"[{offset}:{unchecked(offset + size - 1)}]");
    }
#endif
}
