// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

// Flags passed from runtime to JIT.
[Flags]
public enum CORINFO_TAILCALL_HELPERS_FLAGS
{
    // The StoreArgs stub needs to be passed the target function pointer as the first argument.
    CORINFO_TAILCALL_STORE_TARGET = 0x00000001,
}
