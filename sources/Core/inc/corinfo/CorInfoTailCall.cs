// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoTailCall;

namespace RyuJitSharp;

// If you add more values here, keep it in sync with TailCallTypeMap in ..\vm\ClrEtwAll.man and the string enum in CEEInfo::reportTailCallDecision in ..\vm\JITInterface.cpp
public enum CorInfoTailCall
{
    /// <summary>Optimized tail call (epilog + jmp).</summary>
    TAILCALL_OPTIMIZED = 0,

    /// <summary>Optimized into a loop (only when a method tail calls itself).</summary>
    TAILCALL_RECURSIVE = 1,

    /// <summary>Helper assisted tail call (call to JIT_TailCall).</summary>
    TAILCALL_HELPER = 2,

    //
    // failures are negative
    //

    /// <summary>Couldn't do a tail call.</summary>
    TAILCALL_FAIL = -1,
}
