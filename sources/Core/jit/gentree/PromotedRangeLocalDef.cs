// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct PromotedRangeLocalDef : ILocalDef
{
    private readonly byte _index;
    private readonly bool _isEntire;
    private readonly nint _offset;
    private readonly ValueSize _size;
    private readonly nint _valueOffset;
    private readonly ValueSize _storeSize;

    public PromotedRangeLocalDef(GenTreeLclVarCommon def, int lclNum, int index, bool isEntire,
                                nint offset, ValueSize size, nint valueOffset, ValueSize storeSize)
    {
        assert((uint)index < byte.MaxValue);
        DefNode = def;
        LclNum = lclNum;
        _index = (byte)index;
        _isEntire = isEntire;
        _offset = offset;
        _size = size;
        _valueOffset = valueOffset;
        _storeSize = storeSize;
    }

    public GenTreeLclVarCommon DefNode { get; }

    public int LclNum { get; }

    public int MultiDefIndex => _index;

    public bool IsEntire(Compiler compiler) => _isEntire;

    public nint GetOffset(Compiler compiler) => _offset;

    public ValueSize GetSize(Compiler compiler) => _size;

    public nint GetValueOffset(Compiler compiler) => _valueOffset;

    public ValueSize GetStoreSize(Compiler compiler) => _storeSize;
}
