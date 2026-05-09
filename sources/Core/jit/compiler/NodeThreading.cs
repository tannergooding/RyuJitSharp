// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Current kind of node threading stored in GenTree.gtPrev and GenTree.gtNext.</summary>
public enum NodeThreading
{
    None,

    /// <summary>Locals are threaded (after local morph when optimizing)</summary>
    AllLocals, 

    /// <summary>All nodes are threaded (after gtSetBlockOrder)</summary>
    AllTrees,

    /// <summary>Nodes are in LIR form (after rationalization)</summary>
    LIR,       
}
