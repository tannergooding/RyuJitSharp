// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.MemoryKind;

namespace RyuJitSharp;

/// <summary>Enumeration of the kinds of memory whose state changes the compiler tracks</summary>
public enum MemoryKind
{
    /// <summary>Includes anything byrefs can read/write (everything in GcHeap, address-taken locals, unmanaged heap, callers' locals, etc.)</summary>
    ByrefExposed = 0,

    /// <summary>Includes actual GC heap, and also static fields</summary>
    GcHeap,

    /// <summary>Number of MemoryKinds</summary>
    MemoryKindCount,
}
