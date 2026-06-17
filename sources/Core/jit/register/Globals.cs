// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_X86
    public const int XMMBASE = 8;

    public const int KBASE = 16;

    public static int XMMMASK(int x) => 1 << (x + XMMBASE);

    public static int KMASK(int x) => 1 << (x + KBASE);
#elif TARGET_AMD64
    public const int XMMBASE = 32;

    public const int KBASE = 64;

    public static long GPRMASK(int x) => 1L << x;

    public static long XMMMASK(int x) => 1L << (x + XMMBASE);

    public static long KMASK(int x) => 1L << x;
#elif TARGET_ARM
    public const int FPBASE = 16;

    public static long VFPMASK(int x) => 1L << (x + FPBASE);
#elif TARGET_ARM64
    public const int VBASE = 32;

    public const int PBASE = 64;

    public const int NBASE = 80;

    public static long RMASK(int x) => 1L << x;

    public static long VMASK(int x) => 1L << (x + VBASE);

    public static long PMASK(int x) => 1L << x;
#elif TARGET_LOONGARCH64
    public const int FBASE = 32;

    public const int NBASE = 64;

    public static long RMASK(int x) => 1L << x;

    public static long FMASK(int x) => 1L << (x + FBASE);
#elif TARGET_RISCV64
    public const int FBASE = 32;

    public const int NBASE = 64;

    public static long RMASK(int x) => 1L << x;

    public static long FMASK(int x) => 1L << (x + FBASE);
#elif TARGET_WASM
    // No register sets on wasm
#else
#error Unsupported or unset target architecture
#endif
}
