// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeColon : GenTreeOp
{
    public unsafe GenTreeColon(var_types typ, GenTree thenNode, GenTree elseNode)
        : base(GT_COLON, typ, elseNode, thenNode)
    {
    }

    // There was quite a bit of confusion in the code base about which of gtOp1 and gtOp2 was the 'then' and 'else' clause of a colon node.
    // Adding these accessors, while not enforcing anything, at least *allows* the programmer to be obviously correct.
    // However, these conventions seem backward.
    // TODO-Cleanup: If we could get these accessors used everywhere, then we could switch them.

    public GenTree ElseNode => Op1;

    public GenTree ThenNode => Op2;
}
