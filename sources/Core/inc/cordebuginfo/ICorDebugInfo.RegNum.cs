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
#if TARGET_X86
        REGNUM_EAX,
        REGNUM_ECX,
        REGNUM_EDX,
        REGNUM_EBX,
        REGNUM_ESP,
        REGNUM_EBP,
        REGNUM_ESI,
        REGNUM_EDI,
#elif TARGET_ARM
        REGNUM_R0,
        REGNUM_R1,
        REGNUM_R2,
        REGNUM_R3,
        REGNUM_R4,
        REGNUM_R5,
        REGNUM_R6,
        REGNUM_R7,
        REGNUM_R8,
        REGNUM_R9,
        REGNUM_R10,
        REGNUM_R11,
        REGNUM_R12,
        REGNUM_SP,
        REGNUM_LR,
        REGNUM_PC,
#elif TARGET_ARM64
        REGNUM_X0,
        REGNUM_X1,
        REGNUM_X2,
        REGNUM_X3,
        REGNUM_X4,
        REGNUM_X5,
        REGNUM_X6,
        REGNUM_X7,
        REGNUM_X8,
        REGNUM_X9,
        REGNUM_X10,
        REGNUM_X11,
        REGNUM_X12,
        REGNUM_X13,
        REGNUM_X14,
        REGNUM_X15,
        REGNUM_X16,
        REGNUM_X17,
        REGNUM_X18,
        REGNUM_X19,
        REGNUM_X20,
        REGNUM_X21,
        REGNUM_X22,
        REGNUM_X23,
        REGNUM_X24,
        REGNUM_X25,
        REGNUM_X26,
        REGNUM_X27,
        REGNUM_X28,
        REGNUM_FP,
        REGNUM_LR,
        REGNUM_SP,
        REGNUM_PC,
#elif TARGET_AMD64
        REGNUM_RAX,
        REGNUM_RCX,
        REGNUM_RDX,
        REGNUM_RBX,
        REGNUM_RSP,
        REGNUM_RBP,
        REGNUM_RSI,
        REGNUM_RDI,
        REGNUM_R8,
        REGNUM_R9,
        REGNUM_R10,
        REGNUM_R11,
        REGNUM_R12,
        REGNUM_R13,
        REGNUM_R14,
        REGNUM_R15,
#elif TARGET_LOONGARCH64
        REGNUM_R0,
        REGNUM_RA,
        REGNUM_TP,
        REGNUM_SP,
        REGNUM_A0,
        REGNUM_A1,
        REGNUM_A2,
        REGNUM_A3,
        REGNUM_A4,
        REGNUM_A5,
        REGNUM_A6,
        REGNUM_A7,
        REGNUM_T0,
        REGNUM_T1,
        REGNUM_T2,
        REGNUM_T3,
        REGNUM_T4,
        REGNUM_T5,
        REGNUM_T6,
        REGNUM_T7,
        REGNUM_T8,
        REGNUM_X0,
        REGNUM_FP,
        REGNUM_S0,
        REGNUM_S1,
        REGNUM_S2,
        REGNUM_S3,
        REGNUM_S4,
        REGNUM_S5,
        REGNUM_S6,
        REGNUM_S7,
        REGNUM_S8,
        REGNUM_PC,
#elif TARGET_RISCV64
        REGNUM_R0,
        REGNUM_RA,
        REGNUM_SP,
        REGNUM_GP,
        REGNUM_TP,
        REGNUM_T0,
        REGNUM_T1,
        REGNUM_T2,
        REGNUM_FP,
        REGNUM_S1,
        REGNUM_A0,
        REGNUM_A1,
        REGNUM_A2,
        REGNUM_A3,
        REGNUM_A4,
        REGNUM_A5,
        REGNUM_A6,
        REGNUM_A7,
        REGNUM_S2,
        REGNUM_S3,
        REGNUM_S4,
        REGNUM_S5,
        REGNUM_S6,
        REGNUM_S7,
        REGNUM_S8,
        REGNUM_S9,
        REGNUM_S10,
        REGNUM_S11,
        REGNUM_T3,
        REGNUM_T4,
        REGNUM_T5,
        REGNUM_T6,
        REGNUM_PC,
#elif TARGET_WASM
        REGNUM_PC, // wasm doesn't have registers
#else
#warning Register numbers not defined on this platform
#endif

        REGNUM_COUNT,

        // ambient SP support. Ambient SP is the original SP in the non-BP based frame.
        // Ambient SP should not change even if there are push/pop operations in the method.
        REGNUM_AMBIENT_SP,

#if TARGET_X86
        REGNUM_FP = REGNUM_EBP,
        REGNUM_SP = REGNUM_ESP,
#elif TARGET_AMD64
        REGNUM_FP = REGNUM_RBP,
        REGNUM_SP = REGNUM_RSP,
#elif TARGET_ARM
        REGNUM_FP = REGNUM_R11,
#elif TARGET_ARM64
        //Nothing to do here. FP is already alloted.
#elif TARGET_LOONGARCH64
        //Nothing to do here. FP is already alloted.
#elif TARGET_RISCV64
        //Nothing to do here. FP is already alloted.
#else
        // RegNum values should be properly defined for this platform
        REGNUM_FP = 0,
        REGNUM_SP = 1,
#endif
    }
}
