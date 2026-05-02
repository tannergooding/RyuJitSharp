// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
global using static RyuJitSharp.GenTreeDebugFlags;
using System;

namespace RyuJitSharp;

/// <summary>a bitmask of debug-only flags for GenTree stored in gtDebugFlags</summary>
[Flags]
public enum GenTreeDebugFlags : ushort
{
    /// <summary>No debug flags.</summary>
    GTF_DEBUG_NONE = 0x0000,

    /// <summary>the node has been morphed (in the global morphing phase)</summary>
    GTF_DEBUG_NODE_MORPHED = 0x0001,

    GTF_DEBUG_NODE_SMALL = 0x0002,

    GTF_DEBUG_NODE_LARGE = 0x0004,

    /// <summary>genProduceReg has been called on this node</summary>
    GTF_DEBUG_NODE_CG_PRODUCED = 0x0008,

    /// <summary>genConsumeReg has been called on this node</summary>
    GTF_DEBUG_NODE_CG_CONSUMED = 0x0010,

    /// <summary>This node was added by LSRA</summary>
    GTF_DEBUG_NODE_LSRA_ADDED = 0x0020,

    /// <summary>These flags are all node (rather than operation) properties.</summary>
    GTF_DEBUG_NODE_MASK = 0x003F,

    /// <summary>GT_LCL_VAR -- This is a CSE LCL_VAR node</summary>
    GTF_DEBUG_VAR_CSE_REF = 0x8000,

    /// <summary>GT_CAST    -- Try to prevent this cast from being folded</summary>
    GTF_DEBUG_CAST_DONT_FOLD = 0x4000,
}
#endif
