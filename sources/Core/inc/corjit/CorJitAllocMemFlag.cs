// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorJitAllocMemFlag;
using System;

namespace RyuJitSharp;

/// <summary>These are flags passed to <see cref="ICorJitInfo.allocMem" /> to guide the memory allocation for the code, readonly data, and read-write data.</summary>
[Flags]
public enum CorJitAllocMemFlag
{
    CORJIT_ALLOCMEM_HOT_CODE = 1,

    CORJIT_ALLOCMEM_COLD_CODE = 2,

    CORJIT_ALLOCMEM_READONLY_DATA = 4,

    CORJIT_ALLOCMEM_HAS_POINTERS_TO_CODE = 8,
}
