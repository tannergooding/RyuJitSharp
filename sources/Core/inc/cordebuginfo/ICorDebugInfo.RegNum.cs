// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    // Note: The debugger needs to target register numbers on platforms other than which the debugger itself
    // is running. To this end it maintains its own values for REGNUM_SP and REGNUM_AMBIENT_SP across multiple
    // platforms. So any change here that may effect these values should be reflected in the definitions
    // contained in debug/inc/DbgIPCEvents.h.

    public enum RegNum
    {
        //
        // TARGET_ARM
        //

        REGNUM_ARM_R0 = 0,
        REGNUM_ARM_R1,
        REGNUM_ARM_R2,
        REGNUM_ARM_R3,
        REGNUM_ARM_R4,
        REGNUM_ARM_R5,
        REGNUM_ARM_R6,
        REGNUM_ARM_R7,
        REGNUM_ARM_R8,
        REGNUM_ARM_R9,
        REGNUM_ARM_R10,
        REGNUM_ARM_R11,
        REGNUM_ARM_R12,
        REGNUM_ARM_SP,
        REGNUM_ARM_LR,
        REGNUM_ARM_PC,
        REGNUM_ARM_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_ARM_AMBIENT_SP,

        REGNUM_ARM_FP = REGNUM_ARM_R11,

        //
        // TARGET_ARM64
        //

        REGNUM_ARM64_X0 = 0,
        REGNUM_ARM64_X1,
        REGNUM_ARM64_X2,
        REGNUM_ARM64_X3,
        REGNUM_ARM64_X4,
        REGNUM_ARM64_X5,
        REGNUM_ARM64_X6,
        REGNUM_ARM64_X7,
        REGNUM_ARM64_X8,
        REGNUM_ARM64_X9,
        REGNUM_ARM64_X10,
        REGNUM_ARM64_X11,
        REGNUM_ARM64_X12,
        REGNUM_ARM64_X13,
        REGNUM_ARM64_X14,
        REGNUM_ARM64_X15,
        REGNUM_ARM64_X16,
        REGNUM_ARM64_X17,
        REGNUM_ARM64_X18,
        REGNUM_ARM64_X19,
        REGNUM_ARM64_X20,
        REGNUM_ARM64_X21,
        REGNUM_ARM64_X22,
        REGNUM_ARM64_X23,
        REGNUM_ARM64_X24,
        REGNUM_ARM64_X25,
        REGNUM_ARM64_X26,
        REGNUM_ARM64_X27,
        REGNUM_ARM64_X28,
        REGNUM_ARM64_FP,
        REGNUM_ARM64_LR,
        REGNUM_ARM64_SP,
        REGNUM_ARM64_PC,
        REGNUM_ARM64_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_ARM64_AMBIENT_SP,

        //
        // TARGET_LOONGARCH64
        //

        REGNUM_LOONGARCH64_R0 = 0,
        REGNUM_LOONGARCH64_RA,
        REGNUM_LOONGARCH64_TP,
        REGNUM_LOONGARCH64_SP,
        REGNUM_LOONGARCH64_A0,
        REGNUM_LOONGARCH64_A1,
        REGNUM_LOONGARCH64_A2,
        REGNUM_LOONGARCH64_A3,
        REGNUM_LOONGARCH64_A4,
        REGNUM_LOONGARCH64_A5,
        REGNUM_LOONGARCH64_A6,
        REGNUM_LOONGARCH64_A7,
        REGNUM_LOONGARCH64_T0,
        REGNUM_LOONGARCH64_T1,
        REGNUM_LOONGARCH64_T2,
        REGNUM_LOONGARCH64_T3,
        REGNUM_LOONGARCH64_T4,
        REGNUM_LOONGARCH64_T5,
        REGNUM_LOONGARCH64_T6,
        REGNUM_LOONGARCH64_T7,
        REGNUM_LOONGARCH64_T8,
        REGNUM_LOONGARCH64_X0,
        REGNUM_LOONGARCH64_FP,
        REGNUM_LOONGARCH64_S0,
        REGNUM_LOONGARCH64_S1,
        REGNUM_LOONGARCH64_S2,
        REGNUM_LOONGARCH64_S3,
        REGNUM_LOONGARCH64_S4,
        REGNUM_LOONGARCH64_S5,
        REGNUM_LOONGARCH64_S6,
        REGNUM_LOONGARCH64_S7,
        REGNUM_LOONGARCH64_S8,
        REGNUM_LOONGARCH64_PC,
        REGNUM_LOONGARCH64_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_LOONGARCH64_AMBIENT_SP,

        //
        // TARGET_RISCV64
        //

        REGNUM_RISCV64_R0 = 0,
        REGNUM_RISCV64_RA,
        REGNUM_RISCV64_SP,
        REGNUM_RISCV64_GP,
        REGNUM_RISCV64_TP,
        REGNUM_RISCV64_T0,
        REGNUM_RISCV64_T1,
        REGNUM_RISCV64_T2,
        REGNUM_RISCV64_FP,
        REGNUM_RISCV64_S1,
        REGNUM_RISCV64_A0,
        REGNUM_RISCV64_A1,
        REGNUM_RISCV64_A2,
        REGNUM_RISCV64_A3,
        REGNUM_RISCV64_A4,
        REGNUM_RISCV64_A5,
        REGNUM_RISCV64_A6,
        REGNUM_RISCV64_A7,
        REGNUM_RISCV64_S2,
        REGNUM_RISCV64_S3,
        REGNUM_RISCV64_S4,
        REGNUM_RISCV64_S5,
        REGNUM_RISCV64_S6,
        REGNUM_RISCV64_S7,
        REGNUM_RISCV64_S8,
        REGNUM_RISCV64_S9,
        REGNUM_RISCV64_S10,
        REGNUM_RISCV64_S11,
        REGNUM_RISCV64_T3,
        REGNUM_RISCV64_T4,
        REGNUM_RISCV64_T5,
        REGNUM_RISCV64_T6,
        REGNUM_RISCV64_PC,
        REGNUM_RISCV64_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_RISCV64_AMBIENT_SP,

        //
        // TARGET_X64
        //

        REGNUM_X64_RAX = 0,
        REGNUM_X64_RCX,
        REGNUM_X64_RDX,
        REGNUM_X64_RBX,
        REGNUM_X64_RSP,
        REGNUM_X64_RBP,
        REGNUM_X64_RSI,
        REGNUM_X64_RDI,
        REGNUM_X64_R8,
        REGNUM_X64_R9,
        REGNUM_X64_R10,
        REGNUM_X64_R11,
        REGNUM_X64_R12,
        REGNUM_X64_R13,
        REGNUM_X64_R14,
        REGNUM_X64_R15,
        REGNUM_X64_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_X64_AMBIENT_SP,

        REGNUM_X64_SP = REGNUM_X64_RSP,

        //
        // TARGET_X86
        //

        REGNUM_X86_EAX = 0,
        REGNUM_X86_ECX,
        REGNUM_X86_EDX,
        REGNUM_X86_EBX,
        REGNUM_X86_ESP,
        REGNUM_X86_EBP,
        REGNUM_X86_ESI,
        REGNUM_X86_EDI,
        REGNUM_X86_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_X86_AMBIENT_SP,

        REGNUM_X86_FP = REGNUM_X86_EBP,
        REGNUM_X86_SP = REGNUM_X86_ESP,
    }
}
