// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public bool genCanOmitNormalizationForBswap16(GenTree tree)
    {
        if (_compiler.opts.OptimizationDisabled)
        {
            return false;
        }

        assert(tree.Oper is GT_BSWAP16);
        var next = tree.Next;
        if ((next is null) || (next.Oper is not GT_CAST))
        {
            return false;
        }

        var cast = next.AsCast();
        if (cast.HasOverflowCheck || !ReferenceEquals(cast.CastOp, tree))
        {
            return false;
        }

        return cast.CastType is TYP_USHORT or TYP_SHORT;
    }
}
