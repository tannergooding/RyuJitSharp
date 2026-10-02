// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if HAS_FIXED_REGISTER_SET && (TARGET_X86 || TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64)
using System;

namespace RyuJitSharp;

public static partial class Globals
{
#if TARGET_X86
    public static ReadOnlySpan<regNumber> IntArgRegs => [
        REG_ECX, REG_EDX,
    ];

    public const regMask SRBM_FLTARG_REGS = SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3;
#elif TARGET_ARM
    public static ReadOnlySpan<regNumber> IntArgRegs => [
        REG_R0, REG_R1, REG_R2, REG_R3,
    ];

    public static ReadOnlySpan<regNumber> FltArgRegs => [
        REG_F0, REG_F1, REG_F2, REG_F3, REG_F4, REG_F5, REG_F6, REG_F7,
        REG_F8, REG_F9, REG_F10, REG_F11, REG_F12, REG_F13, REG_F14, REG_F15,
    ];

    public const regMask SRBM_FLTARG_REGS =
        SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7 |
        SRBM_F8 | SRBM_F9 | SRBM_F10 | SRBM_F11 | SRBM_F12 | SRBM_F13 | SRBM_F14 | SRBM_F15;
#elif TARGET_LOONGARCH64
    public static ReadOnlySpan<regNumber> IntArgRegs => [
        REG_A0, REG_A1, REG_A2, REG_A3, REG_A4, REG_A5, REG_A6, REG_A7,
    ];

    public static ReadOnlySpan<regNumber> FltArgRegs => [
        REG_F0, REG_F1, REG_F2, REG_F3, REG_F4, REG_F5, REG_F6, REG_F7,
    ];

    public const regMask SRBM_FLTARG_REGS =
        SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7;
#elif TARGET_RISCV64
    public static ReadOnlySpan<regNumber> IntArgRegs => [
        REG_A0, REG_A1, REG_A2, REG_A3, REG_A4, REG_A5, REG_A6, REG_A7,
    ];

    public static ReadOnlySpan<regNumber> FltArgRegs => [
        REG_FA0, REG_FA1, REG_FA2, REG_FA3, REG_FA4, REG_FA5, REG_FA6, REG_FA7,
    ];

    public const regMask SRBM_FLTARG_REGS =
        SRBM_FA0 | SRBM_FA1 | SRBM_FA2 | SRBM_FA3 | SRBM_FA4 | SRBM_FA5 | SRBM_FA6 | SRBM_FA7;
#endif
}
#endif
