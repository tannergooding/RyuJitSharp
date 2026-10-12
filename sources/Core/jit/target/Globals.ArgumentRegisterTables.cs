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
                return [REG_X86_ECX, REG_X86_EDX];
            }

            if (Configuration.TARGET_AMD64)
            {
                return Configuration.TARGET_UNIX
                    ? [REG_X64_EDI, REG_X64_ESI, REG_X64_EDX, REG_X64_ECX, REG_X64_R8, REG_X64_R9]
                    : [REG_X64_ECX, REG_X64_EDX, REG_X64_R8, REG_X64_R9];
            }

            if (Configuration.TARGET_ARM)
            {
                return [REG_ARM32_R0, REG_ARM32_R1, REG_ARM32_R2, REG_ARM32_R3];
            }

            if (Configuration.TARGET_ARM64)
            {
                return [REG_ARM64_R0, REG_ARM64_R1, REG_ARM64_R2, REG_ARM64_R3,
                    REG_ARM64_R4, REG_ARM64_R5, REG_ARM64_R6, REG_ARM64_R7];
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return [REG_LOONGARCH64_A0, REG_LOONGARCH64_A1, REG_LOONGARCH64_A2, REG_LOONGARCH64_A3,
                    REG_LOONGARCH64_A4, REG_LOONGARCH64_A5, REG_LOONGARCH64_A6, REG_LOONGARCH64_A7];
            }

            if (Configuration.TARGET_RISCV64)
            {
                return [REG_RISCV64_A0, REG_RISCV64_A1, REG_RISCV64_A2, REG_RISCV64_A3,
                    REG_RISCV64_A4, REG_RISCV64_A5, REG_RISCV64_A6, REG_RISCV64_A7];
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
                    ? [REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3,
                        REG_X64_XMM4, REG_X64_XMM5, REG_X64_XMM6, REG_X64_XMM7]
                    : [REG_X64_XMM0, REG_X64_XMM1, REG_X64_XMM2, REG_X64_XMM3];
            }

            if (Configuration.TARGET_ARM)
            {
                return [REG_ARM32_F0, REG_ARM32_F1, REG_ARM32_F2, REG_ARM32_F3,
                    REG_ARM32_F4, REG_ARM32_F5, REG_ARM32_F6, REG_ARM32_F7,
                    REG_ARM32_F8, REG_ARM32_F9, REG_ARM32_F10, REG_ARM32_F11,
                    REG_ARM32_F12, REG_ARM32_F13, REG_ARM32_F14, REG_ARM32_F15];
            }

            if (Configuration.TARGET_ARM64)
            {
                return [REG_ARM64_V0, REG_ARM64_V1, REG_ARM64_V2, REG_ARM64_V3,
                    REG_ARM64_V4, REG_ARM64_V5, REG_ARM64_V6, REG_ARM64_V7];
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return [REG_LOONGARCH64_F0, REG_LOONGARCH64_F1, REG_LOONGARCH64_F2, REG_LOONGARCH64_F3,
                    REG_LOONGARCH64_F4, REG_LOONGARCH64_F5, REG_LOONGARCH64_F6, REG_LOONGARCH64_F7];
            }

            if (Configuration.TARGET_RISCV64)
            {
                return [REG_RISCV64_FA0, REG_RISCV64_FA1, REG_RISCV64_FA2, REG_RISCV64_FA3,
                    REG_RISCV64_FA4, REG_RISCV64_FA5, REG_RISCV64_FA6, REG_RISCV64_FA7];
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
                return SRBM_X86_XMM0 | SRBM_X86_XMM1 | SRBM_X86_XMM2 | SRBM_X86_XMM3;
            }

            if (Configuration.TARGET_AMD64)
            {
                return Configuration.TARGET_UNIX
                    ? SRBM_X64_XMM0 | SRBM_X64_XMM1 | SRBM_X64_XMM2 | SRBM_X64_XMM3 |
                        SRBM_X64_XMM4 | SRBM_X64_XMM5 | SRBM_X64_XMM6 | SRBM_X64_XMM7
                    : SRBM_X64_XMM0 | SRBM_X64_XMM1 | SRBM_X64_XMM2 | SRBM_X64_XMM3;
            }

            if (Configuration.TARGET_ARM)
            {
                return SRBM_ARM32_F0 | SRBM_ARM32_F1 | SRBM_ARM32_F2 | SRBM_ARM32_F3 |
                    SRBM_ARM32_F4 | SRBM_ARM32_F5 | SRBM_ARM32_F6 | SRBM_ARM32_F7 |
                    SRBM_ARM32_F8 | SRBM_ARM32_F9 | SRBM_ARM32_F10 | SRBM_ARM32_F11 |
                    SRBM_ARM32_F12 | SRBM_ARM32_F13 | SRBM_ARM32_F14 | SRBM_ARM32_F15;
            }

            if (Configuration.TARGET_ARM64)
            {
                return SRBM_ARM64_V0 | SRBM_ARM64_V1 | SRBM_ARM64_V2 | SRBM_ARM64_V3 |
                    SRBM_ARM64_V4 | SRBM_ARM64_V5 | SRBM_ARM64_V6 | SRBM_ARM64_V7;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return SRBM_LOONGARCH64_F0 | SRBM_LOONGARCH64_F1 | SRBM_LOONGARCH64_F2 |
                    SRBM_LOONGARCH64_F3 | SRBM_LOONGARCH64_F4 | SRBM_LOONGARCH64_F5 |
                    SRBM_LOONGARCH64_F6 | SRBM_LOONGARCH64_F7;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return SRBM_RISCV64_FA0 | SRBM_RISCV64_FA1 | SRBM_RISCV64_FA2 | SRBM_RISCV64_FA3 |
                    SRBM_RISCV64_FA4 | SRBM_RISCV64_FA5 | SRBM_RISCV64_FA6 | SRBM_RISCV64_FA7;
            }

            if (Configuration.TARGET_WASM)
            {
                return SRBM_NONE;
            }

            return UnsupportedTargetValue<regMask>("Invalid target architecture.");
        }
    }
}
