// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>A parent class for GenTreeArrLen, GenTreeMDArr (so, accessing array meta-data for either single-dimensional or multi-dimensional arrays).</summary>
public abstract class GenTreeArrCommon : GenTreeUnOp
{
    protected GenTreeArrCommon(genTreeOps oper, var_types type, GenTree arrRef)
        : base(oper, type, arrRef)
    {
    }

    /// <summary>The array address node</summary>
    public GenTree ArrRef => Op1;
}
