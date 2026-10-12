// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class Globals
{
    private static ReadOnlySpan<regNumber> UnsupportedTargetRegisterTable()
    {
        throw new FatalJitException("Invalid target architecture.");
    }

    public static ReadOnlySpan<regNumber> IntArgRegs
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return [REG_ECX_X86, REG_EDX_X86];
            }

            if (Configuration.TARGET_AMD64)
            {
                return Configuration.TARGET_UNIX
                    ? [REG_EDI_AMD64, REG_ESI_AMD64, REG_EDX_AMD64, REG_ECX_AMD64, REG_R8_AMD64, REG_R9_AMD64]
                    : [REG_ECX_AMD64, REG_EDX_AMD64, REG_R8_AMD64, REG_R9_AMD64];
            }

            if (Configuration.TARGET_ARM)
            {
                return [REG_R0_ARM, REG_R1_ARM, REG_R2_ARM, REG_R3_ARM];
            }

            if (Configuration.TARGET_ARM64)
            {
                return [REG_R0_ARM64, REG_R1_ARM64, REG_R2_ARM64, REG_R3_ARM64,
                    REG_R4_ARM64, REG_R5_ARM64, REG_R6_ARM64, REG_R7_ARM64];
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return [REG_A0_LOONGARCH64, REG_A1_LOONGARCH64, REG_A2_LOONGARCH64, REG_A3_LOONGARCH64,
                    REG_A4_LOONGARCH64, REG_A5_LOONGARCH64, REG_A6_LOONGARCH64, REG_A7_LOONGARCH64];
            }

            if (Configuration.TARGET_RISCV64)
            {
                return [REG_A0_RISCV64, REG_A1_RISCV64, REG_A2_RISCV64, REG_A3_RISCV64,
                    REG_A4_RISCV64, REG_A5_RISCV64, REG_A6_RISCV64, REG_A7_RISCV64];
            }

            if (Configuration.TARGET_WASM)
            {
                return [];
            }

            return UnsupportedTargetRegisterTable();
        }
    }

    public static ReadOnlySpan<regNumber> FltArgRegs
    {
        get
        {
            if (Configuration.TARGET_X86 || Configuration.TARGET_WASM)
            {
                return [];
            }

            if (Configuration.TARGET_AMD64)
            {
                return Configuration.TARGET_UNIX
                    ? [REG_XMM0_AMD64, REG_XMM1_AMD64, REG_XMM2_AMD64, REG_XMM3_AMD64,
                        REG_XMM4_AMD64, REG_XMM5_AMD64, REG_XMM6_AMD64, REG_XMM7_AMD64]
                    : [REG_XMM0_AMD64, REG_XMM1_AMD64, REG_XMM2_AMD64, REG_XMM3_AMD64];
            }

            if (Configuration.TARGET_ARM)
            {
                return [REG_F0_ARM, REG_F1_ARM, REG_F2_ARM, REG_F3_ARM,
                    REG_F4_ARM, REG_F5_ARM, REG_F6_ARM, REG_F7_ARM,
                    REG_F8_ARM, REG_F9_ARM, REG_F10_ARM, REG_F11_ARM,
                    REG_F12_ARM, REG_F13_ARM, REG_F14_ARM, REG_F15_ARM];
            }

            if (Configuration.TARGET_ARM64)
            {
                return [REG_V0_ARM64, REG_V1_ARM64, REG_V2_ARM64, REG_V3_ARM64,
                    REG_V4_ARM64, REG_V5_ARM64, REG_V6_ARM64, REG_V7_ARM64];
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return [REG_F0_LOONGARCH64, REG_F1_LOONGARCH64, REG_F2_LOONGARCH64, REG_F3_LOONGARCH64,
                    REG_F4_LOONGARCH64, REG_F5_LOONGARCH64, REG_F6_LOONGARCH64, REG_F7_LOONGARCH64];
            }

            if (Configuration.TARGET_RISCV64)
            {
                return [REG_FA0_RISCV64, REG_FA1_RISCV64, REG_FA2_RISCV64, REG_FA3_RISCV64,
                    REG_FA4_RISCV64, REG_FA5_RISCV64, REG_FA6_RISCV64, REG_FA7_RISCV64];
            }

            return UnsupportedTargetRegisterTable();
        }
    }

    public static regMask SRBM_FLTARG_REGS
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return SRBM_XMM0_X86 | SRBM_XMM1_X86 | SRBM_XMM2_X86 | SRBM_XMM3_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return Configuration.TARGET_UNIX
                    ? SRBM_XMM0_AMD64 | SRBM_XMM1_AMD64 | SRBM_XMM2_AMD64 | SRBM_XMM3_AMD64 |
                        SRBM_XMM4_AMD64 | SRBM_XMM5_AMD64 | SRBM_XMM6_AMD64 | SRBM_XMM7_AMD64
                    : SRBM_XMM0_AMD64 | SRBM_XMM1_AMD64 | SRBM_XMM2_AMD64 | SRBM_XMM3_AMD64;
            }

            if (Configuration.TARGET_ARM)
            {
                return SRBM_F0_ARM | SRBM_F1_ARM | SRBM_F2_ARM | SRBM_F3_ARM |
                    SRBM_F4_ARM | SRBM_F5_ARM | SRBM_F6_ARM | SRBM_F7_ARM |
                    SRBM_F8_ARM | SRBM_F9_ARM | SRBM_F10_ARM | SRBM_F11_ARM |
                    SRBM_F12_ARM | SRBM_F13_ARM | SRBM_F14_ARM | SRBM_F15_ARM;
            }

            if (Configuration.TARGET_ARM64)
            {
                return SRBM_V0_ARM64 | SRBM_V1_ARM64 | SRBM_V2_ARM64 | SRBM_V3_ARM64 |
                    SRBM_V4_ARM64 | SRBM_V5_ARM64 | SRBM_V6_ARM64 | SRBM_V7_ARM64;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return SRBM_F0_LOONGARCH64 | SRBM_F1_LOONGARCH64 | SRBM_F2_LOONGARCH64 |
                    SRBM_F3_LOONGARCH64 | SRBM_F4_LOONGARCH64 | SRBM_F5_LOONGARCH64 |
                    SRBM_F6_LOONGARCH64 | SRBM_F7_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return SRBM_FA0_RISCV64 | SRBM_FA1_RISCV64 | SRBM_FA2_RISCV64 | SRBM_FA3_RISCV64 |
                    SRBM_FA4_RISCV64 | SRBM_FA5_RISCV64 | SRBM_FA6_RISCV64 | SRBM_FA7_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return SRBM_NONE;
            }

            return UnsupportedTargetValue<regMask>("Invalid target architecture.");
        }
    }
}
