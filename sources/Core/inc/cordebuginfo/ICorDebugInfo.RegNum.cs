// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // These numeric identities are shared with the debugger definitions in debug/inc/DbgIPCEvents.h.
    // Keep each target's native numbering explicit; runtime-selected aliases are used at the EE boundary.
    public enum RegNum
    {
        REGNUM_X86_EAX = 0,
        REGNUM_X86_ECX = 1,
        REGNUM_X86_EDX = 2,
        REGNUM_X86_EBX = 3,
        REGNUM_X86_ESP = 4,
        REGNUM_X86_EBP = 5,
        REGNUM_X86_ESI = 6,
        REGNUM_X86_EDI = 7,
        REGNUM_X86_FP = REGNUM_X86_EBP,
        REGNUM_X86_SP = REGNUM_X86_ESP,
        REGNUM_X86_COUNT = 8,
        REGNUM_X86_AMBIENT_SP = 9,

        REGNUM_ARM_R0 = 0,
        REGNUM_ARM_R1 = 1,
        REGNUM_ARM_R2 = 2,
        REGNUM_ARM_R3 = 3,
        REGNUM_ARM_R4 = 4,
        REGNUM_ARM_R5 = 5,
        REGNUM_ARM_R6 = 6,
        REGNUM_ARM_R7 = 7,
        REGNUM_ARM_R8 = 8,
        REGNUM_ARM_R9 = 9,
        REGNUM_ARM_R10 = 10,
        REGNUM_ARM_R11 = 11,
        REGNUM_ARM_R12 = 12,
        REGNUM_ARM_SP = 13,
        REGNUM_ARM_LR = 14,
        REGNUM_ARM_PC = 15,
        REGNUM_ARM_FP = REGNUM_ARM_R11,
        REGNUM_ARM_COUNT = 16,
        REGNUM_ARM_AMBIENT_SP = 17,

        REGNUM_ARM64_X0 = 0,
        REGNUM_ARM64_X1 = 1,
        REGNUM_ARM64_X2 = 2,
        REGNUM_ARM64_X3 = 3,
        REGNUM_ARM64_X4 = 4,
        REGNUM_ARM64_X5 = 5,
        REGNUM_ARM64_X6 = 6,
        REGNUM_ARM64_X7 = 7,
        REGNUM_ARM64_X8 = 8,
        REGNUM_ARM64_X9 = 9,
        REGNUM_ARM64_X10 = 10,
        REGNUM_ARM64_X11 = 11,
        REGNUM_ARM64_X12 = 12,
        REGNUM_ARM64_X13 = 13,
        REGNUM_ARM64_X14 = 14,
        REGNUM_ARM64_X15 = 15,
        REGNUM_ARM64_X16 = 16,
        REGNUM_ARM64_X17 = 17,
        REGNUM_ARM64_X18 = 18,
        REGNUM_ARM64_X19 = 19,
        REGNUM_ARM64_X20 = 20,
        REGNUM_ARM64_X21 = 21,
        REGNUM_ARM64_X22 = 22,
        REGNUM_ARM64_X23 = 23,
        REGNUM_ARM64_X24 = 24,
        REGNUM_ARM64_X25 = 25,
        REGNUM_ARM64_X26 = 26,
        REGNUM_ARM64_X27 = 27,
        REGNUM_ARM64_X28 = 28,
        REGNUM_ARM64_FP = 29,
        REGNUM_ARM64_LR = 30,
        REGNUM_ARM64_SP = 31,
        REGNUM_ARM64_PC = 32,
        REGNUM_ARM64_FP_FIRST = 33,
        REGNUM_ARM64_V0 = 33,
        REGNUM_ARM64_V1 = 34,
        REGNUM_ARM64_V2 = 35,
        REGNUM_ARM64_V3 = 36,
        REGNUM_ARM64_V4 = 37,
        REGNUM_ARM64_V5 = 38,
        REGNUM_ARM64_V6 = 39,
        REGNUM_ARM64_V7 = 40,
        REGNUM_ARM64_V8 = 41,
        REGNUM_ARM64_V9 = 42,
        REGNUM_ARM64_V10 = 43,
        REGNUM_ARM64_V11 = 44,
        REGNUM_ARM64_V12 = 45,
        REGNUM_ARM64_V13 = 46,
        REGNUM_ARM64_V14 = 47,
        REGNUM_ARM64_V15 = 48,
        REGNUM_ARM64_V16 = 49,
        REGNUM_ARM64_V17 = 50,
        REGNUM_ARM64_V18 = 51,
        REGNUM_ARM64_V19 = 52,
        REGNUM_ARM64_V20 = 53,
        REGNUM_ARM64_V21 = 54,
        REGNUM_ARM64_V22 = 55,
        REGNUM_ARM64_V23 = 56,
        REGNUM_ARM64_V24 = 57,
        REGNUM_ARM64_V25 = 58,
        REGNUM_ARM64_V26 = 59,
        REGNUM_ARM64_V27 = 60,
        REGNUM_ARM64_V28 = 61,
        REGNUM_ARM64_V29 = 62,
        REGNUM_ARM64_V30 = 63,
        REGNUM_ARM64_V31 = 64,
        REGNUM_ARM64_COUNT = 65,
        REGNUM_ARM64_AMBIENT_SP = 66,

        REGNUM_AMD64_RAX = 0,
        REGNUM_AMD64_RCX = 1,
        REGNUM_AMD64_RDX = 2,
        REGNUM_AMD64_RBX = 3,
        REGNUM_AMD64_RSP = 4,
        REGNUM_AMD64_RBP = 5,
        REGNUM_AMD64_RSI = 6,
        REGNUM_AMD64_RDI = 7,
        REGNUM_AMD64_R8 = 8,
        REGNUM_AMD64_R9 = 9,
        REGNUM_AMD64_R10 = 10,
        REGNUM_AMD64_R11 = 11,
        REGNUM_AMD64_R12 = 12,
        REGNUM_AMD64_R13 = 13,
        REGNUM_AMD64_R14 = 14,
        REGNUM_AMD64_R15 = 15,
        REGNUM_AMD64_FP_FIRST = 16,
        REGNUM_AMD64_XMM0 = 16,
        REGNUM_AMD64_XMM1 = 17,
        REGNUM_AMD64_XMM2 = 18,
        REGNUM_AMD64_XMM3 = 19,
        REGNUM_AMD64_XMM4 = 20,
        REGNUM_AMD64_XMM5 = 21,
        REGNUM_AMD64_XMM6 = 22,
        REGNUM_AMD64_XMM7 = 23,
        REGNUM_AMD64_XMM8 = 24,
        REGNUM_AMD64_XMM9 = 25,
        REGNUM_AMD64_XMM10 = 26,
        REGNUM_AMD64_XMM11 = 27,
        REGNUM_AMD64_XMM12 = 28,
        REGNUM_AMD64_XMM13 = 29,
        REGNUM_AMD64_XMM14 = 30,
        REGNUM_AMD64_XMM15 = 31,
        REGNUM_AMD64_FP = REGNUM_AMD64_RBP,
        REGNUM_AMD64_SP = REGNUM_AMD64_RSP,
        REGNUM_AMD64_COUNT = 32,
        REGNUM_AMD64_AMBIENT_SP = 33,

        REGNUM_LOONGARCH64_R0 = 0,
        REGNUM_LOONGARCH64_RA = 1,
        REGNUM_LOONGARCH64_TP = 2,
        REGNUM_LOONGARCH64_SP = 3,
        REGNUM_LOONGARCH64_A0 = 4,
        REGNUM_LOONGARCH64_A1 = 5,
        REGNUM_LOONGARCH64_A2 = 6,
        REGNUM_LOONGARCH64_A3 = 7,
        REGNUM_LOONGARCH64_A4 = 8,
        REGNUM_LOONGARCH64_A5 = 9,
        REGNUM_LOONGARCH64_A6 = 10,
        REGNUM_LOONGARCH64_A7 = 11,
        REGNUM_LOONGARCH64_T0 = 12,
        REGNUM_LOONGARCH64_T1 = 13,
        REGNUM_LOONGARCH64_T2 = 14,
        REGNUM_LOONGARCH64_T3 = 15,
        REGNUM_LOONGARCH64_T4 = 16,
        REGNUM_LOONGARCH64_T5 = 17,
        REGNUM_LOONGARCH64_T6 = 18,
        REGNUM_LOONGARCH64_T7 = 19,
        REGNUM_LOONGARCH64_T8 = 20,
        REGNUM_LOONGARCH64_X0 = 21,
        REGNUM_LOONGARCH64_FP = 22,
        REGNUM_LOONGARCH64_S0 = 23,
        REGNUM_LOONGARCH64_S1 = 24,
        REGNUM_LOONGARCH64_S2 = 25,
        REGNUM_LOONGARCH64_S3 = 26,
        REGNUM_LOONGARCH64_S4 = 27,
        REGNUM_LOONGARCH64_S5 = 28,
        REGNUM_LOONGARCH64_S6 = 29,
        REGNUM_LOONGARCH64_S7 = 30,
        REGNUM_LOONGARCH64_S8 = 31,
        REGNUM_LOONGARCH64_PC = 32,
        REGNUM_LOONGARCH64_COUNT = 33,
        REGNUM_LOONGARCH64_AMBIENT_SP = 34,

        REGNUM_RISCV64_R0 = 0,
        REGNUM_RISCV64_RA = 1,
        REGNUM_RISCV64_SP = 2,
        REGNUM_RISCV64_GP = 3,
        REGNUM_RISCV64_TP = 4,
        REGNUM_RISCV64_T0 = 5,
        REGNUM_RISCV64_T1 = 6,
        REGNUM_RISCV64_T2 = 7,
        REGNUM_RISCV64_FP = 8,
        REGNUM_RISCV64_S1 = 9,
        REGNUM_RISCV64_A0 = 10,
        REGNUM_RISCV64_A1 = 11,
        REGNUM_RISCV64_A2 = 12,
        REGNUM_RISCV64_A3 = 13,
        REGNUM_RISCV64_A4 = 14,
        REGNUM_RISCV64_A5 = 15,
        REGNUM_RISCV64_A6 = 16,
        REGNUM_RISCV64_A7 = 17,
        REGNUM_RISCV64_S2 = 18,
        REGNUM_RISCV64_S3 = 19,
        REGNUM_RISCV64_S4 = 20,
        REGNUM_RISCV64_S5 = 21,
        REGNUM_RISCV64_S6 = 22,
        REGNUM_RISCV64_S7 = 23,
        REGNUM_RISCV64_S8 = 24,
        REGNUM_RISCV64_S9 = 25,
        REGNUM_RISCV64_S10 = 26,
        REGNUM_RISCV64_S11 = 27,
        REGNUM_RISCV64_T3 = 28,
        REGNUM_RISCV64_T4 = 29,
        REGNUM_RISCV64_T5 = 30,
        REGNUM_RISCV64_T6 = 31,
        REGNUM_RISCV64_PC = 32,
        REGNUM_RISCV64_COUNT = 33,
        REGNUM_RISCV64_AMBIENT_SP = 34,

        REGNUM_WASM_PC = 0,
        REGNUM_WASM_FP = 0,
        REGNUM_WASM_SP = 1,
        REGNUM_WASM_COUNT = 1,
        REGNUM_WASM_AMBIENT_SP = 2,
    }

    internal static RegNum REGNUM_COUNT
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RegNum.REGNUM_X86_COUNT;
            }

            if (Configuration.TARGET_AMD64)
            {
                return RegNum.REGNUM_AMD64_COUNT;
            }

            if (Configuration.TARGET_ARM)
            {
                return RegNum.REGNUM_ARM_COUNT;
            }

            if (Configuration.TARGET_ARM64)
            {
                return RegNum.REGNUM_ARM64_COUNT;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return RegNum.REGNUM_LOONGARCH64_COUNT;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return RegNum.REGNUM_RISCV64_COUNT;
            }

            if (Configuration.TARGET_WASM)
            {
                return RegNum.REGNUM_WASM_COUNT;
            }

            throw new FatalJitException("Register numbers are not defined for this target.");
        }
    }

    internal static RegNum REGNUM_AMBIENT_SP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RegNum.REGNUM_X86_AMBIENT_SP;
            }

            if (Configuration.TARGET_AMD64)
            {
                return RegNum.REGNUM_AMD64_AMBIENT_SP;
            }

            if (Configuration.TARGET_ARM)
            {
                return RegNum.REGNUM_ARM_AMBIENT_SP;
            }

            if (Configuration.TARGET_ARM64)
            {
                return RegNum.REGNUM_ARM64_AMBIENT_SP;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return RegNum.REGNUM_LOONGARCH64_AMBIENT_SP;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return RegNum.REGNUM_RISCV64_AMBIENT_SP;
            }

            if (Configuration.TARGET_WASM)
            {
                return RegNum.REGNUM_WASM_AMBIENT_SP;
            }

            throw new FatalJitException("Ambient stack-pointer register number is not defined for this target.");
        }
    }

    internal static RegNum REGNUM_FP_FIRST
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RegNum.REGNUM_AMD64_FP_FIRST;
            }

            if (Configuration.TARGET_ARM64)
            {
                return RegNum.REGNUM_ARM64_FP_FIRST;
            }

            if (Configuration.TARGET_X86 || Configuration.TARGET_ARM || Configuration.TARGET_LOONGARCH64 ||
                Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM)
            {
                return (RegNum)0;
            }

            throw new FatalJitException("Floating-point register numbering is not defined for this target.");
        }
    }
}
