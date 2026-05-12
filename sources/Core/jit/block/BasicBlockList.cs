// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class BasicBlockList
{
    /// <summary>The next BasicBlock in the list, null for end of list.</summary>
    public BasicBlockList? Next;

    /// <summary>The BasicBlock of interest</summary>
    public BasicBlock Block;

    public BasicBlockList(BasicBlock block, BasicBlockList? next = null)
    {
        Block = block;
        Next = next;
    }
}
