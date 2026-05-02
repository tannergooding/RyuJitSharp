// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoArch;

namespace RyuJitSharp;

public enum CorInfoArch
{
    CORINFO_ARCH_X86,

    CORINFO_ARCH_X64,

    CORINFO_ARCH_ARM,

    CORINFO_ARCH_ARM64,

    CORINFO_ARCH_LOONGARCH64,

    CORINFO_ARCH_RISCV64,

    CORINFO_ARCH_WASM32,
}
