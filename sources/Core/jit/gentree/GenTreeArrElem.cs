// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>Bounds checked address (byref) of a general array element, for multidimensional arrays, or 1-d arrays with non-zero lower bounds.</summary>
public sealed class GenTreeArrElem : GenTree
{
    private GenTree _arrObj;
    private ArrIndsInlineArray _arrInds;
    private readonly byte _arrRank;

    // !!! Caution, this is one byte, it is used only on the optimization path of array intrinsics.
    // It stores the size of array elements WHEN it can fit into an "unsigned char".
    // This has caused VSW 571394.
    private readonly byte _arrElemSize;

    public GenTreeArrElem(var_types type, GenTree arr, byte rank, byte elemSize, ReadOnlySpan<GenTree> inds)
        : base(GT_ARR_ELEM, type)
    {
        _arrObj = arr;
        _arrRank = rank;
        _arrElemSize = elemSize;

        assert(rank <= inds.Length);
        Flags |= (_arrObj.Flags & GTF_ALL_EFFECT);

        for (byte i = 0; i < rank; i++)
        {
            _arrInds[i] = inds[i];
            Flags |= (inds[i].Flags & GTF_ALL_EFFECT);
        }

        Flags |= GTF_EXCEPT;
    }

    public byte ArrElemSize => _arrElemSize;

    public ref ArrIndsInlineArray ArrInds => ref _arrInds;

    public GenTree ArrObj
    {
        get
        {
            return _arrObj;
        }

        set
        {
            _arrObj = value;
        }
    }

#nullable disable
    public ref GenTree ArrObjRef => ref _arrObj;
#nullable restore

    public byte ArrRank => _arrRank;

    [InlineArray(GT_ARR_MAX_RANK)]
    public struct ArrIndsInlineArray
    {
#nullable disable
        public GenTree e0;
#nullable restore
    }
}
