// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct TargetArchitecture
{
    public static readonly bool Is64Bit = TARGET_64BIT;

    public static readonly bool IsArm32 = TARGET_ARM32;

    public static readonly bool IsArm64 = TARGET_ARM64;

    public static readonly bool IsArmArch = TARGET_ARMARCH;

    public static readonly bool IsLoongArch64 = TARGET_LOONGARCH64;

    public static readonly bool IsRiscV64 = TARGET_RISCV64;

    public static readonly bool IsX64 = TARGET_X64;

    public static readonly bool IsX86 = TARGET_X86;

    public static readonly bool IsXArch = TARGET_XARCH;
}
