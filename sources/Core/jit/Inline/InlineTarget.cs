// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>describes the possible targets of an inline observation.</summary>
public enum InlineTarget
{
    /// <summary>observation applies to all calls to this callee</summary>
    CALLEE,

    /// <summary>observation applies to all calls made by this caller</summary>
    CALLER,

    /// <summary>observation applies to a specific call site</summary>
    CALLSITE,
}
