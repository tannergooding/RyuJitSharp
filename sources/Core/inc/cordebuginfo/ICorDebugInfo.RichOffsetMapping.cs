// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    public unsafe struct RichOffsetMapping
    {
        /// <summary>Offset in emitted code.</summary>
        public uint NativeOffset;

        /// <summary>Index of inline tree node containing the IL offset (0 for root).</summary>
        public uint Inlinee;

        /// <summary>IL offset of IL instruction in inlinee that this mapping was created from.</summary>
        public uint ILOffset;

        /// <summary>Source information about the IL instruction in the inlinee.</summary>
        public SourceTypes Source;
    }
}
