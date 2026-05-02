// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoMethodRuntimeFlags;
using System;

namespace RyuJitSharp;

/// <summary>Flags computed by a runtime compiler.</summary>
[Flags]
public enum CorInfoMethodRuntimeFlags
{
    /// <summary>The method is not suitable for inlining.</summary>
    CORINFO_FLG_BAD_INLINEE = 0x00000001,

    // unused = 0x00000002,
    // unused = 0x00000004,

    /// <summary>The JIT decided to switch to MinOpt for this method, when it was not requested.</summary>
    CORINFO_FLG_SWITCHED_TO_MIN_OPT = 0x00000008,

    /// <summary>The JIT decided to switch to tier 1 for this method, when a different tier was requested.</summary>
    CORINFO_FLG_SWITCHED_TO_OPTIMIZED = 0x00000010,
}
