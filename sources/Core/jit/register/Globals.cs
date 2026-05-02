// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_X86
    public const int XMMBASE = (int)(REG_EDI + 1);

    public const int KBASE = (int)(REG_XMM7 + 1);

    public const int STKBASE = (int)(REG_K7 + 1);
#elif TARGET_AMD64
    public const int XMMBASE = (int)(REG_R31 + 1);

    public const int KBASE = (int)(REG_XMM31 + 1);

    public const int STKBASE = (int)(REG_K7 + 1);
#elif TARGET_ARM
    public const int FPBASE = (int)(REG_R16 + 1);

    public const int STKBASE = (int)(REG_F31 + 1);
#elif TARGET_ARM64
    public const int VBASE = (int)(REG_R31 + 1);

    public const int PBASE = (int)(REG_V31 + 1);

    public const int NBASE = (int)(REG_P15 + 1);

    public const int STKBASE = (int)(REG_FFR + 1);
#elif TARGET_LOONGARCH64
    public const int FBASE = (int)(REG_S8 + 1);

    public const int NBASE = (int)(REG_F31 + 1);

    public const int STKBASE = (int)(REG_F31 + 1);
#elif TARGET_RISCV64
    public const int FBASE = (int)(REG_T6 + 1);

    public const int NBASE = (int)(REG_FT11 + 1);

    public const int STKBASE = (int)(REG_FT11 + 1);
#elif TARGET_WASM
    public const int STKBASE = 1;
#else
#error Unsupported or unset target architecture
#endif
}
