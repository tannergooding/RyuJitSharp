// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_RUNTIME_ABI;

namespace RyuJitSharp;

public enum CORINFO_RUNTIME_ABI
{
    CORINFO_CORECLR_ABI = 0x200,

    CORINFO_NATIVEAOT_ABI = 0x300,
}
