// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public const ValueNum RecursiveVN = -2;
    public const int NoLoop = -1;
    public const int UnknownLoop = -2;

    private const int LogChunkSize = 6;
    private const int ChunkSize = 1 << LogChunkSize;
    private const int ChunkOffsetMask = ChunkSize - 1;
    private const int NoChunk = -1;
    private const int SmallIntConstMin = -1;
    private const int SmallIntConstMax = 10;
    private const int SmallIntConstNum = SmallIntConstMax - SmallIntConstMin + 1;

    private readonly Compiler _compiler;
    private readonly List<Chunk> _chunks = [];
    private readonly int[] _curAllocChunk = new int[(int)TYP_COUNT * ((int)ChunkExtraAttribs.CEA_Count + 1)];
    private readonly ValueNum[] _vnsForSmallIntConsts = new ValueNum[SmallIntConstNum];
    private ValueNum _nextChunkBase;
    private int _mapSelectBudget;
    private Dictionary<int, ValueNum>? _intCnsMap;
    private Dictionary<long, ValueNum>? _longCnsMap;
    private Dictionary<int, ValueNum>? _floatCnsMap;
    private Dictionary<long, ValueNum>? _doubleCnsMap;
    private Dictionary<nuint, ValueNum>? _byrefCnsMap;
    private Dictionary<VNHandle, ValueNum>? _handleMap;
    private Dictionary<VNFunc, ValueNum>? _vnFunc0Map;
    private Dictionary<(VNFunc Func, ValueNum Arg0, ValueNum Arg1), ValueNum>? _vnFunc2Map;

    private enum ChunkExtraAttribs : byte
    {
        CEA_Const,
        CEA_Handle,
        CEA_PhiDef,
        CEA_MemoryPhiDef,
        CEA_Func0,
        CEA_Func1,
        CEA_Func2,
        CEA_Func3,
        CEA_Func4,
        CEA_Count,
    }

    private readonly record struct VNHandle(nint Value, GenTreeFlags Flags);

    private sealed class Chunk
    {
        public readonly Array Defs;
        public readonly ValueNum BaseVN;
        public readonly var_types Type;
        public readonly ChunkExtraAttribs Attribs;
        public int NumUsed;

        public Chunk(ref ValueNum nextBaseVN, var_types type, ChunkExtraAttribs attribs)
        {
            BaseVN = nextBaseVN;
            Type = type;
            Attribs = attribs;
            Defs = attribs switch {
                ChunkExtraAttribs.CEA_Const => type switch {
                    TYP_INT => new int[ChunkSize],
                    TYP_LONG => new long[ChunkSize],
                    TYP_FLOAT => new float[ChunkSize],
                    TYP_DOUBLE => new double[ChunkSize],
                    TYP_BYREF => new nuint[ChunkSize],
                    TYP_REF => new nuint[3],
#if FEATURE_SIMD
                    TYP_SIMD8 => new simd8_t[ChunkSize],
                    TYP_SIMD12 => new simd12_t[ChunkSize],
                    TYP_SIMD16 => new simd16_t[ChunkSize],
#if TARGET_XARCH
                    TYP_SIMD32 => new simd32_t[ChunkSize],
                    TYP_SIMD64 => new simd64_t[ChunkSize],
#elif TARGET_ARM64
                    TYP_SIMD => throw new NotImplementedException("Scalable VN constant storage is not yet ported."),
#endif
#if FEATURE_MASKED_HW_INTRINSICS
#if TARGET_ARM64
                    TYP_MASK => throw new NotImplementedException("Scalable/fixed ARM64 VN mask storage is not yet ported."),
#else
                    TYP_MASK => new simdmask_t[ChunkSize],
#endif
#endif
#endif
                    _ => throw new UnreachableException(),
                },
                ChunkExtraAttribs.CEA_Handle => new VNHandle[ChunkSize],
                ChunkExtraAttribs.CEA_PhiDef => new VNPhiDef[ChunkSize],
                ChunkExtraAttribs.CEA_MemoryPhiDef => new VNMemoryPhiDef[ChunkSize],
                >= ChunkExtraAttribs.CEA_Func0 and <= ChunkExtraAttribs.CEA_Func4
                    => new int[ChunkSize * ((int)attribs - (int)ChunkExtraAttribs.CEA_Func0 + 1)],
                _ => throw new UnreachableException(),
            };
            nextBaseVN = unchecked(nextBaseVN + ChunkSize);
        }

        public int AllocVN()
        {
            assert(NumUsed < ChunkSize);
            return NumUsed++;
        }

        public Memory<ValueNum> FuncApp(int offset, int arity)
        {
            assert(Attribs is >= ChunkExtraAttribs.CEA_Func0 and <= ChunkExtraAttribs.CEA_Func4);
            assert(arity == (int)Attribs - (int)ChunkExtraAttribs.CEA_Func0);
            // Each record is the 32-bit function symbol followed by its arguments.
            return ((int[])Defs).AsMemory(offset * (arity + 1), arity + 1);
        }
    }

    public ValueNumStore(Compiler compiler)
    {
        _compiler = compiler;
        _ = _chunks.EnsureCapacity(8);
        _curAllocChunk.AsSpan().Fill(NoChunk);
        _vnsForSmallIntConsts.AsSpan().Fill(NoVN);
        _chunks.Add(new Chunk(ref _nextChunkBase, TYP_REF, ChunkExtraAttribs.CEA_Const) { NumUsed = 3 });
        _mapSelectBudget = JitConfig.JitVNMapSelBudget;
        if (_mapSelectBudget <= 0)
        {
            _mapSelectBudget = DEFAULT_MAP_SELECT_BUDGET;
        }

#if DEBUG
        if (compiler.compStressCompile(Compiler.compStressArea.STRESS_VN_BUDGET, 50))
        {
            assert(compiler._inlineStrategy is not null);
            var random = compiler._inlineStrategy.GetRandom(compiler.info.compMethodHash());
            if (random.NextDouble() <= 0.5)
            {
                _mapSelectBudget = random.Next(0, 5);
            }
            else
            {
                var limit = random.Next(1, DEFAULT_MAP_SELECT_BUDGET + 1);
                _mapSelectBudget = random.Next(0, limit);
            }

            JITDUMP($"VN Stress: setting select budget to {_mapSelectBudget}\n");
        }
#endif
    }

    public static ValueNum VNForVoid() => 1;

    public static ValueNum VNForEmptyExcSet() => 2;

    public static ValueNumPair VNPForVoid() => new(VNForVoid(), VNForVoid());

    public static ValueNumPair VNPForEmptyExcSet() => new(VNForEmptyExcSet(), VNForEmptyExcSet());

    private static int GetChunkNum(ValueNum vn) => vn >>> LogChunkSize;

    private static int ChunkOffset(ValueNum vn) => vn & ChunkOffsetMask;

    private Chunk GetAllocChunk(var_types type, ChunkExtraAttribs attribs)
    {
        var index = ((int)type * ((int)ChunkExtraAttribs.CEA_Count + 1)) + (int)attribs;
        var chunkNum = _curAllocChunk[index];
        if (chunkNum != NoChunk)
        {
            var current = _chunks[chunkNum];
            if (current.NumUsed < ChunkSize)
            {
                return current;
            }
        }

        var chunk = new Chunk(ref _nextChunkBase, type, attribs);
        _curAllocChunk[index] = _chunks.Count;
        _chunks.Add(chunk);
        return chunk;
    }

    private ValueNum VnForConst<T, TKey>(T value, TKey key, Dictionary<TKey, ValueNum> map, var_types type)
        where T : unmanaged
        where TKey : notnull
    {
        ref var vn = ref CollectionsMarshal.GetValueRefOrAddDefault(map, key, out var exists);
        if (!exists)
        {
            vn = NoVN;
        }

        if (vn == NoVN)
        {
            var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Const);
            var offset = chunk.AllocVN();
            vn = unchecked(chunk.BaseVN + offset);
            ((T[])chunk.Defs)[offset] = value;
        }

        return vn;
    }

    public ValueNum VNForIntCon(int value)
    {
        if (value is >= SmallIntConstMin and <= SmallIntConstMax)
        {
            var index = value - SmallIntConstMin;
            var cached = _vnsForSmallIntConsts[index];
            if (cached != NoVN)
            {
                return cached;
            }

            cached = VnForConst(value, value, _intCnsMap ??= [], TYP_INT);
            _vnsForSmallIntConsts[index] = cached;
            return cached;
        }

        return VnForConst(value, value, _intCnsMap ??= [], TYP_INT);
    }

    public ValueNum VNForIntPtrCon(nint value)
        => nint.Size == 8 ? VNForLongCon(value) : VNForIntCon((int)value);

    public ValueNum VNForLongCon(long value) => VnForConst(value, value, _longCnsMap ??= [], TYP_LONG);

    // Bit keys preserve signed zero and every NaN payload, unlike managed floating equality.
    public ValueNum VNForFloatCon(float value)
        => VnForConst(value, BitConverter.SingleToInt32Bits(value), _floatCnsMap ??= [], TYP_FLOAT);

    public ValueNum VNForDoubleCon(double value)
        => VnForConst(value, BitConverter.DoubleToInt64Bits(value), _doubleCnsMap ??= [], TYP_DOUBLE);

    public ValueNum VNForByrefCon(nuint value) => VnForConst(value, value, _byrefCnsMap ??= [], TYP_BYREF);

    public ValueNum VNForHandle(nint value, GenTreeFlags flags)
    {
        assert((flags & ~GTF_ICON_HDL_MASK) == 0);
        var handle = new VNHandle(value, flags);
        _handleMap ??= [];
        ref var vn = ref CollectionsMarshal.GetValueRefOrAddDefault(_handleMap, handle, out var exists);
        if (exists && (vn != NoVN))
        {
            return vn;
        }

        vn = NoVN;
        var chunk = GetAllocChunk(Compiler.gtGetTypeForIconFlags(flags), ChunkExtraAttribs.CEA_Handle);
        var offset = chunk.AllocVN();
        ((VNHandle[])chunk.Defs)[offset] = handle;
        vn = unchecked(chunk.BaseVN + offset);
        return vn;
    }

    public var_types TypeOfVN(ValueNum vn) => vn == NoVN ? TYP_UNDEF : _chunks[GetChunkNum(vn)].Type;

    public bool IsVNConstant(ValueNum vn)
    {
        if (vn == NoVN)
        {
            return false;
        }

        var chunk = _chunks[GetChunkNum(vn)];
        return chunk.Attribs == ChunkExtraAttribs.CEA_Const ? vn != VNForVoid() : chunk.Attribs == ChunkExtraAttribs.CEA_Handle;
    }

    public bool IsVNConstantNonHandle(ValueNum vn) => IsVNConstant(vn) && !IsVNHandle(vn);

    public bool IsVNInt32Constant(ValueNum vn) => IsVNConstant(vn) && (TypeOfVN(vn) == TYP_INT);

    public bool IsVNHandle(ValueNum vn) => (vn != NoVN) && (_chunks[GetChunkNum(vn)].Attribs == ChunkExtraAttribs.CEA_Handle);

    public bool IsVNHandle(ValueNum vn, GenTreeFlags flags) => IsVNHandle(vn) && (GetHandleFlags(vn) == flags);

    public bool IsVNObjHandle(ValueNum vn) => IsVNHandle(vn, GTF_ICON_OBJ_HDL);

    public bool IsVNTypeHandle(ValueNum vn) => IsVNHandle(vn, GTF_ICON_CLASS_HDL);

    public GenTreeFlags GetHandleFlags(ValueNum vn)
    {
        assert(IsVNHandle(vn));
        var chunk = _chunks[GetChunkNum(vn)];
        var flags = ((VNHandle[])chunk.Defs)[ChunkOffset(vn)].Flags;
        assert((flags & ~GTF_ICON_HDL_MASK) == 0);
        return flags;
    }

    public T ConstantValue<T>(ValueNum vn) where T : unmanaged, INumberBase<T>
    {
        var chunk = _chunks[GetChunkNum(vn)];
        assert(chunk.Attribs is ChunkExtraAttribs.CEA_Const or ChunkExtraAttribs.CEA_Handle);
        assert((chunk.Attribs == ChunkExtraAttribs.CEA_Handle) || (Unsafe.SizeOf<T>() == chunk.Type.Size));
        assert((chunk.Attribs == ChunkExtraAttribs.CEA_Handle) ||
            (varTypeIsFloating(chunk.Type) == ((typeof(T) == typeof(float)) || (typeof(T) == typeof(double)))));
        return CoercedConstantValue<T>(vn);
    }

    public T CoercedConstantValue<T>(ValueNum vn) where T : unmanaged, INumberBase<T>
    {
        var chunk = _chunks[GetChunkNum(vn)];
        var offset = ChunkOffset(vn);
        assert(chunk.Attribs is ChunkExtraAttribs.CEA_Const or ChunkExtraAttribs.CEA_Handle);
        if (chunk.Attribs == ChunkExtraAttribs.CEA_Handle)
        {
            return T.CreateTruncating(((VNHandle[])chunk.Defs)[offset].Value);
        }

        return chunk.Type switch {
            TYP_REF or TYP_BYREF => T.CreateTruncating(((nuint[])chunk.Defs)[offset]),
            TYP_INT => T.CreateTruncating(((int[])chunk.Defs)[offset]),
            TYP_LONG => T.CreateTruncating(((long[])chunk.Defs)[offset]),
            TYP_FLOAT => T.CreateTruncating(((float[])chunk.Defs)[offset]),
            TYP_DOUBLE => T.CreateTruncating(((double[])chunk.Defs)[offset]),
            _ => throw new UnreachableException(),
        };
    }

    public bool IsVNIntegralConstant<T>(ValueNum vn, out T value) where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        if (IsVNConstant(vn) && varTypeIsIntegral(TypeOfVN(vn)))
        {
            var constant = CoercedConstantValue<long>(vn);
            if ((constant >= long.CreateSaturating(T.MinValue)) && (constant <= long.CreateSaturating(T.MaxValue)))
            {
                value = T.CreateTruncating(constant);
                return true;
            }
        }

        value = T.Zero;
        return false;
    }

    public bool VNIsValid(ValueNum vn)
    {
        var chunkNum = GetChunkNum(vn);
        return (chunkNum < _chunks.Count) && (ChunkOffset(vn) < _chunks[chunkNum].NumUsed);
    }

    public bool IsVNFunc(ValueNum vn)
        => (vn != NoVN) && (_chunks[GetChunkNum(vn)].Attribs is >= ChunkExtraAttribs.CEA_Func0 and <= ChunkExtraAttribs.CEA_Func4);

    public bool GetVNFunc(ValueNum vn, ref VNFuncApp funcApp)
    {
        if (vn == NoVN)
        {
            return false;
        }

        var chunk = _chunks[GetChunkNum(vn)];
        var offset = ChunkOffset(vn);
        assert(offset < chunk.NumUsed);
        var arity = (int)chunk.Attribs - (int)ChunkExtraAttribs.CEA_Func0;
        if (arity is >= 0 and <= 4)
        {
            var record = chunk.FuncApp(offset, arity);
            funcApp = new VNFuncApp((VNFunc)record.Span[0], record[1..]);
            return true;
        }

        return false;
    }

    public bool IsVNBinFunc(ValueNum vn, VNFunc func, ref ValueNum op1, ref ValueNum op2)
    {
        var app = new VNFuncApp();
        if (GetVNFunc(vn, ref app) && app.FuncIs(func) && (app.Arity == 2))
        {
            op1 = app.GetArg(0);
            op2 = app.GetArg(1);
            return true;
        }

        return false;
    }

    public bool IsVNBinFuncWithConst<T>(ValueNum vn, VNFunc func, ref ValueNum op, ref T constant)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        var left = NoVN;
        var right = NoVN;
        if (IsVNBinFunc(vn, func, ref left, ref right))
        {
            if (IsVNIntegralConstant(right, out T rightConstant))
            {
                op = left;
                constant = rightConstant;
                return true;
            }

            if (VNFuncIsCommutative(func) && IsVNIntegralConstant(left, out T leftConstant))
            {
                op = right;
                constant = leftConstant;
                return true;
            }
        }

        return false;
    }

    public static int VNFuncArity(VNFunc func) => ((int)VNFuncExtensions.GetAttributes(func) & VNFOA_ArityMask) >> VNFOA_ArityShift;

    public static bool VNFuncIsCommutative(VNFunc func) => (VNFuncExtensions.GetAttributes(func) & VNFOA_Commutative) != 0;

    public ValueNum VNNormalValue(ValueNum vn)
    {
        var app = new VNFuncApp();
        return GetVNFunc(vn, ref app) && app.FuncIs(VNF_ValWithExc) && (app.Arity == 2) ? app.GetArg(0) : vn;
    }

    public ValueNum VNForFunc(var_types type, VNFunc func)
    {
        assert(VNFuncArity(func) == 0);
        _vnFunc0Map ??= [];
        ref var vn = ref CollectionsMarshal.GetValueRefOrAddDefault(_vnFunc0Map, func, out var exists);
        if (!exists)
        {
            vn = NoVN;
        }

        if (vn == NoVN)
        {
            var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func0);
            var offset = chunk.AllocVN();
            chunk.FuncApp(offset, 0).Span[0] = (int)func;
            vn = unchecked(chunk.BaseVN + offset);
        }

        return vn;
    }

    public ValueNum VNForFuncNoFolding(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert((arg0VN != NoVN) && (arg1VN != NoVN));
        assert((arg0VN == VNNormalValue(arg0VN)) && (arg1VN == VNNormalValue(arg1VN)));
        assert(VNFuncArity(func) == 2);
        _vnFunc2Map ??= [];
        ref var vn = ref CollectionsMarshal.GetValueRefOrAddDefault(_vnFunc2Map, (func, arg0VN, arg1VN), out var exists);
        if (!exists)
        {
            vn = NoVN;
        }

        if (vn == NoVN)
        {
            var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func2);
            var offset = chunk.AllocVN();
            var record = chunk.FuncApp(offset, 2).Span;
            record[0] = (int)func;
            record[1] = arg0VN;
            record[2] = arg1VN;
            vn = unchecked(chunk.BaseVN + offset);
        }

        return vn;
    }

    public ValueNum VNForExpr(BasicBlock? block, var_types type)
    {
        var loopIndex = UnknownLoop;
        if (block is not null)
        {
            assert(_compiler._blockToLoop is not null);
            var loop = _compiler._blockToLoop.GetLoop(block);
            loopIndex = loop is null ? NoLoop : loop.Index;
        }

        var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func1);
        var offset = chunk.AllocVN();
        var record = chunk.FuncApp(offset, 1).Span;
        record[0] = (int)VNF_MemOpaque;
        record[1] = loopIndex;
        return unchecked(chunk.BaseVN + offset);
    }
}
