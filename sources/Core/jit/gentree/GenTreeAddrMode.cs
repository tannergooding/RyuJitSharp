// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

/// <summary>Target-specific canonicalized addressing expression</summary>
public sealed class GenTreeAddrMode : GenTreeOp
{
    // Address is Base + Index*Scale + Offset.
    // These are the legal patterns:
    //
    //      Base                                // Base is not null && Index is null && Scale == 0 && Offset == 0
    //      Base + Index*Scale                  // Base is not null && Index is not null && Scale != 0 && Offset == 0
    //      Base + Offset                       // Base is not null && Index is null && Scale == 0 && Offset != 0
    //      Base + Index*Scale + Offset         // Base is not null && Index is not null && Scale != 0 && Offset != 0
    //             Index*Scale                  // Base is null && Index is not null && Scale >  1 && Offset == 0
    //             Index*Scale + Offset         // Base is null && Index is not null && Scale >  1 && Offset != 0
    //                           Offset         // Base is null && Index is null && Scale == 0 && Offset != 0
    //
    // So, for example:
    //      1. Base + Index is legal with Scale==1
    //      2. If Index is null, Scale should be zero (or uninitialized / unused)
    //      3. If Scale==1, then we should have "Base" instead of "Index*Scale", and "Base + Offset" instead of
    //         "Index*Scale + Offset".

    private byte _scale;

    private int _offset;

    public GenTreeAddrMode(var_types type, GenTree? @base, GenTree? index, byte scale, int offset)
        : base(GT_LEA, type, @base, index)
    {
        assert((@base is not null) || (index is not null));
        _scale = scale;
        _offset = offset;
    }

    public GenTree? Base
    {
        get
        {
            return Op1;
        }

        set
        {
            Op1 = value;
        }
    }

    [MemberNotNullWhen(true, nameof(Base))]
    public bool HasBase => Op1 is not null;

    [MemberNotNullWhen(true, nameof(Index))]
    public bool HasIndex => Op2 is not null;

    public GenTree? Index
    {
        get
        {
            return Op2;
        }

        set
        {
            Op2 = value;
        }
    }

    /// <summary>The scale factor</summary>
    public byte Scale
    {
        get
        {
            return _scale;
        }

        set
        {
            _scale = value;
        }
    }

    /// <summary>The offset to add</summary>
    public int Offset
    {
        get
        {
            return _offset;
        }

        set
        {
            _offset = value;
        }
    }
}
