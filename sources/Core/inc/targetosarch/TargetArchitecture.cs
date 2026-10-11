// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class TargetArchitecture
{
    public static bool Is64Bit => Configuration.TARGET_64BIT;

    public static bool IsWasm => Configuration.TARGET_WASM;

    public static bool IsX86 => Configuration.TARGET_X86;

    public static bool IsX64 => Configuration.TARGET_AMD64;

    public static bool IsArm64 => Configuration.TARGET_ARM64;

    public static bool IsArm32 => Configuration.TARGET_ARM;

    public static bool IsArmArch => Configuration.TARGET_ARMARCH;

    public static bool IsLoongArch64 => Configuration.TARGET_LOONGARCH64;

    public static bool IsRiscV64 => Configuration.TARGET_RISCV64;
}
