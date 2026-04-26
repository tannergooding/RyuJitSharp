// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.Globals;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static partial class Globals
{
    // The following are human readable names for the target architectures
#if TARGET_X86
    public const string TARGET_READABLE_NAME = "X86";
#elif TARGET_AMD64
    public const string TARGET_READABLE_NAME = "AMD64";
#elif TARGET_ARM
    public const string TARGET_READABLE_NAME = "ARM";
#elif TARGET_ARM64
    public const string TARGET_READABLE_NAME = "ARM64";
#elif TARGET_LOONGARCH64
    public const string TARGET_READABLE_NAME = "LOONGARCH64";
#elif TARGET_RISCV64
    public const string TARGET_READABLE_NAME = "RISCV64";
#elif TARGET_WASM32
    public const string TARGET_READABLE_NAME = "WASM32";
#else
#error Unsupported or unset target architecture
#endif

    // The following are intended to capture only those #defines that cannot be replaced with static const members of Target
#if TARGET_AMD64
    public const int REGMASK_BITS              = 64;
    public const int CSE_CONST_SHARED_LOW_BITS = 16;
#elif TARGET_X86
    public const int REGMASK_BITS              = 32;
    public const int CSE_CONST_SHARED_LOW_BITS = 16;
#elif TARGET_ARM
    public const int REGMASK_BITS              = 64;
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_ARM64
    public const int REGMASK_BITS              = 64;
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_LOONGARCH64
    public const int REGMASK_BITS              = 64;
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_RISCV64
    public const int REGMASK_BITS              = 64;
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_WASM32
    public const int REGMASK_BITS              = 32;
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#else
#error Unsupported or unset target architecture
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compFeatureVarArg()
    {
        // Native Varargs are not supported on Unix (all architectures) and Windows ARM
        return TargetOS.IsWindows && !TargetArchitecture.IsArm32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compAppleArm64Abi()
    {
        return TargetArchitecture.IsArm64 && TargetOS.IsApplePlatform;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compFeatureArgSplit()
    {
        return TargetArchitecture.IsLoongArch64
            || TargetArchitecture.IsArm32
            || TargetArchitecture.IsRiscV64
            || (TargetArchitecture.IsArm64 && TargetOS.IsWindows);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool compUnixX86Abi()
    {
        return TargetArchitecture.IsX86 && TargetOS.IsUnix;
    }
}
