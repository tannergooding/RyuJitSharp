// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
global using static RyuJitSharp.GenTreeCallDebugFlags;
using System;

namespace RyuJitSharp;

[Flags]
public enum GenTreeCallDebugFlags
{
    GTF_CALL_MD_EMPTY = 0,

    /// <summary>the call is NOT "tail" prefixed but GTF_CALL_M_EXPLICIT_TAILCALL was added because of tail call stress mode</summary>
    GTF_CALL_MD_STRESS_TAILCALL = 1 << 0,

    /// <summary>this call was devirtualized</summary>
    GTF_CALL_MD_DEVIRTUALIZED = 1 << 1,

    /// <summary>this call was optimized to use the unboxed entry point</summary>
    GTF_CALL_MD_UNBOXED = 1 << 2,

    /// <summary>this call was transformed by guarded devirtualization</summary>
    GTF_CALL_MD_GUARDED = 1 << 3,

    /// <summary>this call is a (failed) inline candidate</summary>
    GTF_CALL_MD_WAS_CANDIDATE = 1 << 4,

    /// <summary>this runtime lookup helper is expanded</summary>
    GTF_CALL_MD_RUNTIME_LOOKUP_EXPANDED = 1 << 5,
}
#endif
