// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly struct UseDefLocation
{
    public UseDefLocation(BasicBlock block, Statement? stmt, GenTreeLclVar? tree)
    {
        Block = block;
        Stmt = stmt;
        Tree = tree;
    }

    public BasicBlock Block { get; }

    public Statement? Stmt { get; }

    public GenTreeLclVar? Tree { get; }
}
