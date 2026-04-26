// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class TargetArchitecture
{
#if TARGET_64BIT
    public const bool Is64Bit = true;
#else
    public const bool Is64Bit = false;
#endif

#if TARGET_ARM
    public const bool IsX86 = false;
    public const bool IsX64 = false;
    public const bool IsArm64 = false;
    public const bool IsArm32 = true;
    public const bool IsArmArch = true;
    public const bool IsLoongArch64 = false;
    public const bool IsRiscV64 = false;
#elif TARGET_ARM64
    public const bool IsX86 = false;
    public const bool IsX64 = false;
    public const bool IsArm64 = true;
    public const bool IsArm32 = false;
    public const bool IsArmArch = true;
    public const bool IsLoongArch64 = false;
    public const bool IsRiscV64 = false;
#elif TARGET_AMD64
    public const bool IsX86 = false;
    public const bool IsX64 = true;
    public const bool IsArm64 = false;
    public const bool IsArm32 = false;
    public const bool IsArmArch = false;
    public const bool IsLoongArch64 = false;
    public const bool IsRiscV64 = false;
#elif TARGET_X86
    public const bool IsX86 = true;
    public const bool IsX64 = false;
    public const bool IsArm64 = false;
    public const bool IsArm32 = false;
    public const bool IsArmArch = false;
    public const bool IsLoongArch64 = false;
    public const bool IsRiscV64 = false;
#elif TARGET_LOONGARCH64
    public const bool IsX86 = false;
    public const bool IsX64 = false;
    public const bool IsArm64 = false;
    public const bool IsArm32 = false;
    public const bool IsArmArch = false;
    public const bool IsLoongArch64 = true;
    public const bool IsRiscV64 = false;
#elif TARGET_RISCV64
    public const bool IsX86 = false;
    public const bool IsX64 = false;
    public const bool IsArm64 = false;
    public const bool IsArm32 = false;
    public const bool IsArmArch = false;
    public const bool IsLoongArch64 = false;
    public const bool IsRiscV64 = true;
#elif TARGET_WASM
    public const bool IsX86 = false;
    public const bool IsX64 = false;
    public const bool IsArm64 = false;
    public const bool IsArm32 = false;
    public const bool IsArmArch = false;
    public const bool IsLoongArch64 = false;
    public const bool IsRiscV64 = false;
#else
#error Unknown architecture
#endif
}
