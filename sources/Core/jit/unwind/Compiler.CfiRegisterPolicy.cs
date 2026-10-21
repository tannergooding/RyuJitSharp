// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    // Keep the native target register policies here until their shared const APIs can
    // become runtime-selected without changing other backends' constant expressions.
    private static (regMaskTP CalleeSavedMask, regNumber FirstFloatRegister, regNumber LastFloatRegister,
        regNumber LastIntegerRegister, int RegisterSize) GetCfiRegisterPolicy()
    {
        if (Configuration.TARGET_ARM)
        {
            var mask = regMask.SRBM_ARM32_R4 | regMask.SRBM_ARM32_R5 | regMask.SRBM_ARM32_R6 |
                regMask.SRBM_ARM32_R7 | regMask.SRBM_ARM32_R8 | regMask.SRBM_ARM32_R9 | regMask.SRBM_ARM32_R10 |
                regMask.SRBM_ARM32_F16 | regMask.SRBM_ARM32_F17 | regMask.SRBM_ARM32_F18 | regMask.SRBM_ARM32_F19 |
                regMask.SRBM_ARM32_F20 | regMask.SRBM_ARM32_F21 | regMask.SRBM_ARM32_F22 | regMask.SRBM_ARM32_F23 |
                regMask.SRBM_ARM32_F24 | regMask.SRBM_ARM32_F25 | regMask.SRBM_ARM32_F26 | regMask.SRBM_ARM32_F27 |
                regMask.SRBM_ARM32_F28 | regMask.SRBM_ARM32_F29 | regMask.SRBM_ARM32_F30 | regMask.SRBM_ARM32_F31;

            return (new regMaskTP(mask), REG_ARM32_F0, REG_ARM32_F31, REG_ARM32_LR, 4);
        }

        if (Configuration.TARGET_ARM64)
        {
            var mask = regMask.SRBM_ARM64_R19 | regMask.SRBM_ARM64_R20 | regMask.SRBM_ARM64_R21 |
                regMask.SRBM_ARM64_R22 | regMask.SRBM_ARM64_R23 | regMask.SRBM_ARM64_R24 | regMask.SRBM_ARM64_R25 |
                regMask.SRBM_ARM64_R26 | regMask.SRBM_ARM64_R27 | regMask.SRBM_ARM64_R28 |
                regMask.SRBM_ARM64_V8 | regMask.SRBM_ARM64_V9 | regMask.SRBM_ARM64_V10 | regMask.SRBM_ARM64_V11 |
                regMask.SRBM_ARM64_V12 | regMask.SRBM_ARM64_V13 | regMask.SRBM_ARM64_V14 | regMask.SRBM_ARM64_V15;

            return (new regMaskTP(mask), REG_ARM64_V0, REG_ARM64_V31, REG_ARM64_ZR, 8);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            var mask = regMask.SRBM_LOONGARCH64_S0 | regMask.SRBM_LOONGARCH64_S1 | regMask.SRBM_LOONGARCH64_S2 |
                regMask.SRBM_LOONGARCH64_S3 | regMask.SRBM_LOONGARCH64_S4 | regMask.SRBM_LOONGARCH64_S5 |
                regMask.SRBM_LOONGARCH64_S6 | regMask.SRBM_LOONGARCH64_S7 | regMask.SRBM_LOONGARCH64_S8 |
                regMask.SRBM_LOONGARCH64_F24 | regMask.SRBM_LOONGARCH64_F25 | regMask.SRBM_LOONGARCH64_F26 |
                regMask.SRBM_LOONGARCH64_F27 | regMask.SRBM_LOONGARCH64_F28 | regMask.SRBM_LOONGARCH64_F29 |
                regMask.SRBM_LOONGARCH64_F30 | regMask.SRBM_LOONGARCH64_F31;

            return (new regMaskTP(mask), REG_LOONGARCH64_F0, REG_LOONGARCH64_F31, REG_LOONGARCH64_S8, 8);
        }

        if (Configuration.TARGET_RISCV64)
        {
            var mask = regMask.SRBM_RISCV64_S1 | regMask.SRBM_RISCV64_S2 | regMask.SRBM_RISCV64_S3 |
                regMask.SRBM_RISCV64_S4 | regMask.SRBM_RISCV64_S5 | regMask.SRBM_RISCV64_S6 | regMask.SRBM_RISCV64_S7 |
                regMask.SRBM_RISCV64_S8 | regMask.SRBM_RISCV64_S9 | regMask.SRBM_RISCV64_S10 | regMask.SRBM_RISCV64_S11 |
                regMask.SRBM_RISCV64_FS0 | regMask.SRBM_RISCV64_FS1 | regMask.SRBM_RISCV64_FS2 | regMask.SRBM_RISCV64_FS3 |
                regMask.SRBM_RISCV64_FS4 | regMask.SRBM_RISCV64_FS5 | regMask.SRBM_RISCV64_FS6 | regMask.SRBM_RISCV64_FS7 |
                regMask.SRBM_RISCV64_FS8 | regMask.SRBM_RISCV64_FS9 | regMask.SRBM_RISCV64_FS10 | regMask.SRBM_RISCV64_FS11;

            return (new regMaskTP(mask), REG_RISCV64_FT0, REG_RISCV64_FT11, REG_RISCV64_T6, 8);
        }

        if (Configuration.UNIX_AMD64_ABI)
        {
            var mask = regMask.SRBM_X64_RBX | regMask.SRBM_X64_R12 | regMask.SRBM_X64_R13 |
                regMask.SRBM_X64_R14 | regMask.SRBM_X64_R15;

            if (!Configuration.ETW_EBP_FRAMED)
            {
                mask |= regMask.SRBM_X64_RBP;
            }

            return (new regMaskTP(mask), REG_X64_XMM0, REG_X64_XMM31, REG_X64_R31, 8);
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "The target CFI register policy is not ported.");
        return default!;
    }
}
