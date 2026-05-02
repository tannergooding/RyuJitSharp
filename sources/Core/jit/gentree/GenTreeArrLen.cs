// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Single-dimension (SZ) array length. Used for `array.Length`.</summary>
public sealed class GenTreeArrLen : GenTreeArrCommon
{
    private readonly int _arrLenOffset;

    public GenTreeArrLen(var_types type, GenTree arrRef, int lenOffset)
        : base(GT_ARR_LENGTH, type, arrRef)
    {
        _arrLenOffset = lenOffset;
    }

    /// <summary>Constant to add to "ArrRef()" to get the address of the array length.</summary>
    public int ArrLenOffset => _arrLenOffset;
}
