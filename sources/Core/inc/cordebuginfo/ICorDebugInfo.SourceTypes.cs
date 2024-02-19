// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // Note that SourceTypes can be OR'd together - it's possible that
    // a sequence point will also be a stack_empty point, and/or a call site.
    // The debugger will check to see if a boundary offset's source field &
    // SEQUENCE_POINT is true to determine if the boundary is a sequence point.

    [Flags]
    public enum SourceTypes
    {
        /// <summary>To indicate that nothing else applies.</summary>
        SOURCE_TYPE_INVALID = 0x00,

        /// <summary>The debugger asked for it.</summary>
        SEQUENCE_POINT = 0x01,

        /// <summary>The stack is empty here.</summary>
        STACK_EMPTY = 0x02,

        /// <summary>This is a call site.</summary>
        CALL_SITE = 0x04,

        /// <summary>Indicates a epilog endpoint.</summary>
        NATIVE_END_OFFSET_UNKNOWN = 0x08,

        /// <summary>The actual instruction of a call.</summary>
        CALL_INSTRUCTION = 0x10
    }
}
