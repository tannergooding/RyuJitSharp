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

    public const SourceTypes SOURCE_TYPE_INVALID = SourceTypes.SOURCE_TYPE_INVALID;
    public const SourceTypes SEQUENCE_POINT = SourceTypes.SEQUENCE_POINT;
    public const SourceTypes STACK_EMPTY = SourceTypes.STACK_EMPTY;
    public const SourceTypes CALL_SITE = SourceTypes.CALL_SITE;
    public const SourceTypes NATIVE_END_OFFSET_UNKNOWN = SourceTypes.NATIVE_END_OFFSET_UNKNOWN;
    public const SourceTypes CALL_INSTRUCTION = SourceTypes.CALL_INSTRUCTION;
    public const SourceTypes ASYNC = SourceTypes.ASYNC;

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
        CALL_INSTRUCTION = 0x10,

        /// <summary>Indicates suspension/resumption for an async call.</summary>
        ASYNC = 0x20,
    }
}
