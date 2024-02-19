// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    /// <summary>Represents an individual entry in the inline tree.</summary>
    /// <remarks>This is ordinarily stored as a flat array in which [0] is the root, and the indices below indicate the tree structure.</remarks>
    public unsafe struct InlineTreeNode
    {
        /// <summary>Method handle of inlinee (or root).</summary>
        public CORINFO_METHOD_HANDLE Method;

        /// <summary>IL offset of IL instruction resulting in the inline.</summary>
        public uint ILOffset;

        /// <summary>Index of child in tree, 0 if no children.</summary>
        public uint Child;

        /// <summary>Index of sibling in tree, 0 if no sibling.</summary>
        public uint Sibling;
    }
}
