// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>multi-dimension (MD) array length or lower bound for a dimension. </summary>
/// <remarks>Used for `array.GetLength(n)`, `array.GetLowerBound(n)`.</remarks>
public sealed class GenTreeMDArr : GenTreeArrCommon
{
    private readonly int _dim;  
    private readonly int _rank;

    public GenTreeMDArr(genTreeOps oper, GenTree arrRef, int dim, int rank)
        : base(oper, TYP_INT, arrRef)
    {
        assert(oper.IsMdArr);
        _dim = dim;
        _rank = rank;
    }

    /// <summary>Array dimension of this array length</summary>
    public int Dim => _dim;

    /// <summary>Array rank of the array</summary>
    public int Rank => _rank;
}
