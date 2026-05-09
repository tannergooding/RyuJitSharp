// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Carries information about the array type from morph to VN.</summary>
/// <remarks>This node is just a wrapper (similar to GenTreeBox), the real address expression is contained in its first operand.</remarks>
public sealed class GenTreeArrAddr : GenTreeUnOp
{
    private readonly unsafe CORINFO_CLASS_HANDLE _elemClassHandle;
    private readonly var_types _elemType;
    private readonly byte _firstElemOffset;

    public unsafe GenTreeArrAddr(GenTree addr, var_types elemType, CORINFO_CLASS_HANDLE elemClassHandle, byte firstElemOffset)
        : base(GT_ARR_ADDR, addr.Type, addr)
    {
        _elemClassHandle = elemClassHandle;
        _elemType = elemType;
        _firstElemOffset = firstElemOffset;

        assert(addr.Type is TYP_BYREF or TYP_I_IMPL);
        assert(((elemType is TYP_STRUCT) && (elemClassHandle != NO_CLASS_HANDLE)) || (elemClassHandle == NO_CLASS_HANDLE));
    }

    public GenTree Addr => Op1;

    /// <summary>The array element class. Currently only used for arrays of TYP_STRUCT.</summary>
    public unsafe CORINFO_CLASS_HANDLE ElemClassHandle => _elemClassHandle;

    /// <summary>The normalized (TYP_SIMD != TYP_STRUCT) array element type.</summary>
    public var_types ElemType => _elemType;

    /// <summary>Offset to the first element of the array.</summary>
    public byte FirstElemOffset => _firstElemOffset;
}
