// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, gentree.h and gentree.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public readonly struct LocalOccurrence(GenTreeLclVarCommon node)
{
    public GenTree Node => node;

    public int LclNum => node.LclNum;

    public GenTreeFlags Flags => node.Flags;

    public int LclOffs => node.LclOffs;

    public var_types GetAccessType(Compiler compiler)
    {
        assert(node.Oper is not GT_LCL_ADDR);
        return node.Type;
    }

    public int GetAccessSize(Compiler compiler)
    {
        assert(node.Oper is not GT_LCL_ADDR);
        return node.Type is TYP_STRUCT ? checked((int)node.GetLayout(compiler)!.Size) : node.Type.Size;
    }
}
