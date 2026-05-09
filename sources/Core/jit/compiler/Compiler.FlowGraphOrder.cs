// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const FlowGraphOrder FGOrderTree = FlowGraphOrder.FGOrderTree;
    public const FlowGraphOrder FGOrderLinear = FlowGraphOrder.FGOrderLinear;

    public enum FlowGraphOrder
    {
        /// <summary>the dominant ordering is the tree order, and the nodes contained in each tree and sub-tree are contiguous, and can be traversed (in gtNext/gtPrev order) by traversing the tree according to the order of the operands.</summary>
        FGOrderTree,

        /// <summary>the dominant ordering is the linear order.</summary>
        FGOrderLinear,
    }
}
