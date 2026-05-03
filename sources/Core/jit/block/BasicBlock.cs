// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class BasicBlock : LIR.Range
{
    /// <summary>The dynamic execution weight of this block</summary>
    public weight_t bbWeight;

    public BasicBlock(GenTree? firstNode, GenTree? lastNode)
        : base(firstNode, lastNode)
    {
    }
}
