// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeBfm : GenTreeOp
{
    private readonly uint _offset;
    private readonly uint _width;

    public GenTreeBfm(genTreeOps oper, var_types type, GenTree baseNode, GenTree? src, uint offset, uint width)
        : base(oper, type, baseNode, src)
    {
        assert(oper is GT_BFX);
        assert(src is null);
        _offset = offset;
        _width = width;
    }

    public uint GetOffset() => _offset;

    public uint GetWidth() => _width;
}
