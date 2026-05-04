// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Simplifies creation and usage of "ShortLong" bitsets.</summary>
public sealed class BitVecTraits : IBitSetTraits<BitVecTraits>
{
    private Compiler _compiler;

    private uint _size;

    /// <summary>pre-computed to avoid computation in GetArrSize</summary>
    private uint _arraySize;

    public unsafe BitVecTraits(Compiler compiler, uint size)
    {
        _compiler = compiler;
        _size = size;

        var elemBits = 8 * (uint)(sizeof(nuint));
        _arraySize = roundUp(size, elemBits) / elemBits;
    }

    public static uint GetArrSize(BitVecTraits env) => env._arraySize;

    public static uint GetEpoch(BitVecTraits env) => env._size;

    public static uint GetSize(BitVecTraits env) => env._size;
}
