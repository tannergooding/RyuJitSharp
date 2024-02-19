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
    /// <summary>The code will use the normal alignment.</summary>
    CORJIT_ALLOCMEM_DEFAULT_CODE_ALIGN = 0x00000000,

    /// <summary>The code will be 16-byte aligned.</summary>
    CORJIT_ALLOCMEM_FLG_16BYTE_ALIGN = 0x00000001,

    /// <summary>The read-only data will be 16-byte aligned.</summary>
    CORJIT_ALLOCMEM_FLG_RODATA_16BYTE_ALIGN = 0x00000002,

    /// <summary>The code will be 32-byte aligned.</summary>
    CORJIT_ALLOCMEM_FLG_32BYTE_ALIGN = 0x00000004,

    /// <summary>The read-only data will be 32-byte aligned.</summary>
    CORJIT_ALLOCMEM_FLG_RODATA_32BYTE_ALIGN = 0x00000008,

    /// <summary>The read-only data will be 64-byte aligned.</summary>
    CORJIT_ALLOCMEM_FLG_RODATA_64BYTE_ALIGN = 0x00000010,
}
