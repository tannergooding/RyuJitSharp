// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Given an array object and an index, checks that the index is within the bounds of the array if necessary and produces the address of the value at that index of the array.</summary>
public sealed class GenTreeIndexAddr : GenTreeOp
{
    private readonly unsafe CORINFO_CLASS_HANDLE _structElemClass;
    private readonly var_types _elemType;
    private readonly int _elemSize;
    private readonly int _lenOffset;
    private readonly int _elemOffset;

    public unsafe GenTreeIndexAddr(GenTree arr, GenTree ind, var_types elemType, CORINFO_CLASS_HANDLE structElemClass, int elemSize, int lenOffset, int elemOffset, bool boundsCheck)
        : base(GT_INDEX_ADDR, TYP_BYREF, arr, ind)
    {
        _structElemClass = structElemClass;
        _elemType = elemType;
        _elemSize = elemSize;
        _lenOffset = lenOffset;
        _elemOffset = elemOffset;

        assert(!varTypeIsStruct(elemType) || (structElemClass != NO_CLASS_HANDLE));

        if (boundsCheck)
        {
            // Do bounds check
            Flags |= GTF_INX_RNGCHK;
        }
        Flags |= (GTF_EXCEPT | GTF_GLOB_REF);
    }

    public GenTree Arr => Op1;

    /// <summary>The offset from the array's base address to its first element.</summary>
    public int ElemOffset => _elemOffset;

    /// <summary>The size of elements in the array</summary>
    public int ElemSize => _elemSize;

    /// <summary>The element type of the array.</summary>
    public var_types ElemType => _elemType;

    public GenTree Index => Op2;

    public bool IsBoundsChecked => (Flags & GTF_INX_RNGCHK) != 0;

    public bool IsNotNull => (Flags & (GTF_INX_ADDR_NONNULL | GTF_INX_RNGCHK)) != 0;

    /// <summary>The offset from the array's base address to its length.</summary>
    public int LenOffset => _lenOffset;

    /// <summary>If the element type is a struct, this is the struct type.</summary>
    public unsafe CORINFO_CLASS_HANDLE StructElemClass => _structElemClass;
}
