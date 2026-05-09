// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Gives a rough classification of how often a call site will be executed at runtime.</summary>
public enum InlineCallsiteFrequency
{
    /// <summary>n/a</summary>
    UNUSED,

    /// <summary>once in a blue moon</summary>
    RARE,

    /// <summary>normal call site</summary>
    BORING,

    /// <summary>seen during profiling</summary>
    WARM,

    /// <summary>in a loop</summary>
    LOOP,

    /// <summary>very frequent</summary>
    HOT,
}
