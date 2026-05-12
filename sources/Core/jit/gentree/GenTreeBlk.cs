// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>This is the base type for all of the nodes that represent block or struct values.</summary>
/// <remarks>Since it can be a store, it includes gtBlkOpKind to specify the type of code generation that will be used for the block operation.</remarks>
public sealed class GenTreeBlk : GenTreeIndir
{
    private ClassLayout _layout;

    public GenTreeBlk(var_types type, GenTree addr, ClassLayout layout)
        : base(GT_BLK, type, addr, data: null)
    {
        assert(layout.Size != 0);
        _layout = layout;
    }

    public GenTreeBlk(var_types type, GenTree addr, GenTree data, ClassLayout layout)
        : base(GT_STORE_BLK, type, addr, data)
    {
        assert(layout.Size != 0);

        if (data.IsIntegralConst(0))
        {
            data.Flags |= GTF_DONT_CSE;
        }
        _layout = layout;
    }

    public bool ContainsReferences => _layout.HasGCPtr;

    public bool IsOnHeapAndContainsReferences => (Addr.Oper is not GT_LCL_ADDR) && ContainsReferences;

    public bool IsZeroingGCPointersOnHeap => (Oper is GT_STORE_BLK) && Data.IsIntegralConst(0) && IsOnHeapAndContainsReferences;

    public ClassLayout Layout
    {
        get
        {
            return _layout;
        }

        set
        {
            assert(value.Size == _layout.Size);
            _layout = value;
        }
    }

    public new int Size => _layout.Size;
}
