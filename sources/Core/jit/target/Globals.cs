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

#if REGMASK_BITS_32 && REGMASK_BITS_64
#error Cannot define both REGMASK_BITS_32 and REGMASK_BITS_64
#endif

#if TARGET_AMD64
    public const int CSE_CONST_SHARED_LOW_BITS = 16;
#elif TARGET_X86
    public const int CSE_CONST_SHARED_LOW_BITS = 16;
#elif TARGET_ARM
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_ARM64
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_LOONGARCH64
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_RISCV64
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#elif TARGET_WASM32
    public const int CSE_CONST_SHARED_LOW_BITS = 12;
#else
#error Unsupported or unset target architecture
#endif

#if REGMASK_BITS_8
    public const string REG_MASK_INT_FMT = "{0:X2}";

    public const string REG_MASK_ALL_FMT = "{0:X2}";
#elif REGMASK_BITS_16
    public const string REG_MASK_INT_FMT = "{0:X4}";

    public const string REG_MASK_ALL_FMT = "{0:X4}";
#elif REGMASK_BITS_32
    public const string REG_MASK_INT_FMT = "{0:X8}";

    public const string REG_MASK_ALL_FMT = "{0:X8}";
#elif REGMASK_BITS_64
    public const string REG_MASK_INT_FMT = "{0:X4}";

    public const string REG_MASK_ALL_FMT = "{0:X16}";
#else
#error Unsupported REGMASK_BITS size
#endif

    public const int REG_LOW_BASE = 0;

#if HAS_MORE_THAN_64_REGISTERS
    public const int REG_HIGH_BASE = 64;
#endif

    public const int RBM_NONE = 0;

#if DEBUG
    public const int DSP_SRC_OPER_LEFT  = 0;

    public const int DSP_SRC_OPER_RIGHT = 1;

    public const int DSP_DST_OPER_LEFT  = 1;

    public const int DSP_DST_OPER_RIGHT = 0;
#endif

#if TARGET_XARCH
    public const int JMP_DIST_SMALL_MAX_NEG = -128;

    public const int JMP_DIST_SMALL_MAX_POS = +127;

    public const int JCC_DIST_SMALL_MAX_NEG = -128;

    public const int JCC_DIST_SMALL_MAX_POS = +127;

    public const int JMP_SIZE_SMALL = 2;

    public const int JMP_SIZE_LARGE = 5;

    public const int JCC_SIZE_SMALL = 2;

    public const int JCC_SIZE_LARGE = 6;

    public const int PUSH_INST_SIZE = 5;

    public const int CALL_INST_SIZE = 5;
#endif

    public const int BITS_PER_BYTE = 8;

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

    /// <summary>Return true if the register number is valid</summary>
    /// <param name="reg"></param>
    /// <returns></returns>
    public static bool genIsValidReg(regNumber reg) => reg < REG_COUNT;
}
