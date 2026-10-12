// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    private static string GetNameX86(regNumber regNum) => regNum switch
    {
        REG_EAX_X86 => "eax",
        REG_ECX_X86 => "ecx",
        REG_EDX_X86 => "edx",
        REG_EBX_X86 => "ebx",
        REG_ESP_X86 => "esp",
        REG_EBP_X86 => "ebp",
        REG_ESI_X86 => "esi",
        REG_EDI_X86 => "edi",
        REG_XMM0_X86 => "mm0",
        REG_XMM1_X86 => "mm1",
        REG_XMM2_X86 => "mm2",
        REG_XMM3_X86 => "mm3",
        REG_XMM4_X86 => "mm4",
        REG_XMM5_X86 => "mm5",
        REG_XMM6_X86 => "mm6",
        REG_XMM7_X86 => "mm7",
        REG_K0_X86 => "k0",
        REG_K1_X86 => "k1",
        REG_K2_X86 => "k2",
        REG_K3_X86 => "k3",
        REG_K4_X86 => "k4",
        REG_K5_X86 => "k5",
        REG_K6_X86 => "k6",
        REG_K7_X86 => "k7",
        REG_STK_X86 => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameAMD64(regNumber regNum) => regNum switch
    {
        REG_RAX_AMD64 => "rax",
        REG_RCX_AMD64 => "rcx",
        REG_RDX_AMD64 => "rdx",
        REG_RBX_AMD64 => "rbx",
        REG_RSP_AMD64 => "rsp",
        REG_RBP_AMD64 => "rbp",
        REG_RSI_AMD64 => "rsi",
        REG_RDI_AMD64 => "rdi",
        REG_R8_AMD64 => "r8",
        REG_R9_AMD64 => "r9",
        REG_R10_AMD64 => "r10",
        REG_R11_AMD64 => "r11",
        REG_R12_AMD64 => "r12",
        REG_R13_AMD64 => "r13",
        REG_R14_AMD64 => "r14",
        REG_R15_AMD64 => "r15",
        REG_R16_AMD64 => "r16",
        REG_R17_AMD64 => "r17",
        REG_R18_AMD64 => "r18",
        REG_R19_AMD64 => "r19",
        REG_R20_AMD64 => "r20",
        REG_R21_AMD64 => "r21",
        REG_R22_AMD64 => "r22",
        REG_R23_AMD64 => "r23",
        REG_R24_AMD64 => "r24",
        REG_R25_AMD64 => "r25",
        REG_R26_AMD64 => "r26",
        REG_R27_AMD64 => "r27",
        REG_R28_AMD64 => "r28",
        REG_R29_AMD64 => "r29",
        REG_R30_AMD64 => "r30",
        REG_R31_AMD64 => "r31",
        REG_XMM0_AMD64 => "mm0",
        REG_XMM1_AMD64 => "mm1",
        REG_XMM2_AMD64 => "mm2",
        REG_XMM3_AMD64 => "mm3",
        REG_XMM4_AMD64 => "mm4",
        REG_XMM5_AMD64 => "mm5",
        REG_XMM6_AMD64 => "mm6",
        REG_XMM7_AMD64 => "mm7",
        REG_XMM8_AMD64 => "mm8",
        REG_XMM9_AMD64 => "mm9",
        REG_XMM10_AMD64 => "mm10",
        REG_XMM11_AMD64 => "mm11",
        REG_XMM12_AMD64 => "mm12",
        REG_XMM13_AMD64 => "mm13",
        REG_XMM14_AMD64 => "mm14",
        REG_XMM15_AMD64 => "mm15",
        REG_XMM16_AMD64 => "mm16",
        REG_XMM17_AMD64 => "mm17",
        REG_XMM18_AMD64 => "mm18",
        REG_XMM19_AMD64 => "mm19",
        REG_XMM20_AMD64 => "mm20",
        REG_XMM21_AMD64 => "mm21",
        REG_XMM22_AMD64 => "mm22",
        REG_XMM23_AMD64 => "mm23",
        REG_XMM24_AMD64 => "mm24",
        REG_XMM25_AMD64 => "mm25",
        REG_XMM26_AMD64 => "mm26",
        REG_XMM27_AMD64 => "mm27",
        REG_XMM28_AMD64 => "mm28",
        REG_XMM29_AMD64 => "mm29",
        REG_XMM30_AMD64 => "mm30",
        REG_XMM31_AMD64 => "mm31",
        REG_K0_AMD64 => "k0",
        REG_K1_AMD64 => "k1",
        REG_K2_AMD64 => "k2",
        REG_K3_AMD64 => "k3",
        REG_K4_AMD64 => "k4",
        REG_K5_AMD64 => "k5",
        REG_K6_AMD64 => "k6",
        REG_K7_AMD64 => "k7",
        REG_STK_AMD64 => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameARM(regNumber regNum) => regNum switch
    {
        REG_R0_ARM => "r0",
        REG_R1_ARM => "r1",
        REG_R2_ARM => "r2",
        REG_R3_ARM => "r3",
        REG_R4_ARM => "r4",
        REG_R5_ARM => "r5",
        REG_R6_ARM => "r6",
        REG_R7_ARM => "r7",
        REG_R8_ARM => "r8",
        REG_R9_ARM => "r9",
        REG_R10_ARM => "r10",
        REG_R11_ARM => "r11",
        REG_R12_ARM => "r12",
        REG_SP_ARM => "sp",
        REG_LR_ARM => "lr",
        REG_PC_ARM => "pc",
        REG_F0_ARM => "f0",
        REG_F1_ARM => "f1",
        REG_F2_ARM => "f2",
        REG_F3_ARM => "f3",
        REG_F4_ARM => "f4",
        REG_F5_ARM => "f5",
        REG_F6_ARM => "f6",
        REG_F7_ARM => "f7",
        REG_F8_ARM => "f8",
        REG_F9_ARM => "f9",
        REG_F10_ARM => "f10",
        REG_F11_ARM => "f11",
        REG_F12_ARM => "f12",
        REG_F13_ARM => "f13",
        REG_F14_ARM => "f14",
        REG_F15_ARM => "f15",
        REG_F16_ARM => "f16",
        REG_F17_ARM => "f17",
        REG_F18_ARM => "f18",
        REG_F19_ARM => "f19",
        REG_F20_ARM => "f20",
        REG_F21_ARM => "f21",
        REG_F22_ARM => "f22",
        REG_F23_ARM => "f23",
        REG_F24_ARM => "f24",
        REG_F25_ARM => "f25",
        REG_F26_ARM => "f26",
        REG_F27_ARM => "f27",
        REG_F28_ARM => "f28",
        REG_F29_ARM => "f29",
        REG_F30_ARM => "f30",
        REG_F31_ARM => "f31",
        REG_STK_ARM => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameARM64(regNumber regNum) => regNum switch
    {
        REG_R0_ARM64 => "x0",
        REG_R1_ARM64 => "x1",
        REG_R2_ARM64 => "x2",
        REG_R3_ARM64 => "x3",
        REG_R4_ARM64 => "x4",
        REG_R5_ARM64 => "x5",
        REG_R6_ARM64 => "x6",
        REG_R7_ARM64 => "x7",
        REG_R8_ARM64 => "x8",
        REG_R9_ARM64 => "x9",
        REG_R10_ARM64 => "x10",
        REG_R11_ARM64 => "x11",
        REG_R12_ARM64 => "x12",
        REG_R13_ARM64 => "x13",
        REG_R14_ARM64 => "x14",
        REG_R15_ARM64 => "x15",
        REG_IP0_ARM64 => "xip0",
        REG_IP1_ARM64 => "xip1",
        REG_PR_ARM64 => "xpr",
        REG_R19_ARM64 => "x19",
        REG_R20_ARM64 => "x20",
        REG_R21_ARM64 => "x21",
        REG_R22_ARM64 => "x22",
        REG_R23_ARM64 => "x23",
        REG_R24_ARM64 => "x24",
        REG_R25_ARM64 => "x25",
        REG_R26_ARM64 => "x26",
        REG_R27_ARM64 => "x27",
        REG_R28_ARM64 => "x28",
        REG_FP_ARM64 => "fp",
        REG_LR_ARM64 => "lr",
        REG_ZR_ARM64 => "xzr",
        REG_V0_ARM64 => "d0",
        REG_V1_ARM64 => "d1",
        REG_V2_ARM64 => "d2",
        REG_V3_ARM64 => "d3",
        REG_V4_ARM64 => "d4",
        REG_V5_ARM64 => "d5",
        REG_V6_ARM64 => "d6",
        REG_V7_ARM64 => "d7",
        REG_V8_ARM64 => "d8",
        REG_V9_ARM64 => "d9",
        REG_V10_ARM64 => "d10",
        REG_V11_ARM64 => "d11",
        REG_V12_ARM64 => "d12",
        REG_V13_ARM64 => "d13",
        REG_V14_ARM64 => "d14",
        REG_V15_ARM64 => "d15",
        REG_V16_ARM64 => "d16",
        REG_V17_ARM64 => "d17",
        REG_V18_ARM64 => "d18",
        REG_V19_ARM64 => "d19",
        REG_V20_ARM64 => "d20",
        REG_V21_ARM64 => "d21",
        REG_V22_ARM64 => "d22",
        REG_V23_ARM64 => "d23",
        REG_V24_ARM64 => "d24",
        REG_V25_ARM64 => "d25",
        REG_V26_ARM64 => "d26",
        REG_V27_ARM64 => "d27",
        REG_V28_ARM64 => "d28",
        REG_V29_ARM64 => "d29",
        REG_V30_ARM64 => "d30",
        REG_V31_ARM64 => "d31",
        REG_P0_ARM64 => "p0",
        REG_P1_ARM64 => "p1",
        REG_P2_ARM64 => "p2",
        REG_P3_ARM64 => "p3",
        REG_P4_ARM64 => "p4",
        REG_P5_ARM64 => "p5",
        REG_P6_ARM64 => "p6",
        REG_P7_ARM64 => "p7",
        REG_P8_ARM64 => "p8",
        REG_P9_ARM64 => "p9",
        REG_P10_ARM64 => "p10",
        REG_P11_ARM64 => "p11",
        REG_P12_ARM64 => "p12",
        REG_P13_ARM64 => "p13",
        REG_P14_ARM64 => "p14",
        REG_P15_ARM64 => "p15",
        REG_SP_ARM64 => "sp",
        REG_FFR_ARM64 => "ffr",
        REG_STK_ARM64 => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameLOONGARCH64(regNumber regNum) => regNum switch
    {
        REG_R0_LOONGARCH64 => "zero",
        REG_RA_LOONGARCH64 => "ra",
        REG_TP_LOONGARCH64 => "tp",
        REG_SP_LOONGARCH64 => "sp",
        REG_A0_LOONGARCH64 => "a0",
        REG_A1_LOONGARCH64 => "a1",
        REG_A2_LOONGARCH64 => "a2",
        REG_A3_LOONGARCH64 => "a3",
        REG_A4_LOONGARCH64 => "a4",
        REG_A5_LOONGARCH64 => "a5",
        REG_A6_LOONGARCH64 => "a6",
        REG_A7_LOONGARCH64 => "a7",
        REG_T0_LOONGARCH64 => "t0",
        REG_T1_LOONGARCH64 => "t1",
        REG_T2_LOONGARCH64 => "t2",
        REG_T3_LOONGARCH64 => "t3",
        REG_T4_LOONGARCH64 => "t4",
        REG_T5_LOONGARCH64 => "t5",
        REG_T6_LOONGARCH64 => "t6",
        REG_T7_LOONGARCH64 => "t7",
        REG_T8_LOONGARCH64 => "t8",
        REG_X0_LOONGARCH64 => "x0",
        REG_FP_LOONGARCH64 => "fp",
        REG_S0_LOONGARCH64 => "s0",
        REG_S1_LOONGARCH64 => "s1",
        REG_S2_LOONGARCH64 => "s2",
        REG_S3_LOONGARCH64 => "s3",
        REG_S4_LOONGARCH64 => "s4",
        REG_S5_LOONGARCH64 => "s5",
        REG_S6_LOONGARCH64 => "s6",
        REG_S7_LOONGARCH64 => "s7",
        REG_S8_LOONGARCH64 => "s8",
        REG_F0_LOONGARCH64 => "f0",
        REG_F1_LOONGARCH64 => "f1",
        REG_F2_LOONGARCH64 => "f2",
        REG_F3_LOONGARCH64 => "f3",
        REG_F4_LOONGARCH64 => "f4",
        REG_F5_LOONGARCH64 => "f5",
        REG_F6_LOONGARCH64 => "f6",
        REG_F7_LOONGARCH64 => "f7",
        REG_F8_LOONGARCH64 => "f8",
        REG_F9_LOONGARCH64 => "f9",
        REG_F10_LOONGARCH64 => "f10",
        REG_F11_LOONGARCH64 => "f11",
        REG_F12_LOONGARCH64 => "f12",
        REG_F13_LOONGARCH64 => "f13",
        REG_F14_LOONGARCH64 => "f14",
        REG_F15_LOONGARCH64 => "f15",
        REG_F16_LOONGARCH64 => "f16",
        REG_F17_LOONGARCH64 => "f17",
        REG_F18_LOONGARCH64 => "f18",
        REG_F19_LOONGARCH64 => "f19",
        REG_F20_LOONGARCH64 => "f20",
        REG_F21_LOONGARCH64 => "f21",
        REG_F22_LOONGARCH64 => "f22",
        REG_F23_LOONGARCH64 => "f23",
        REG_F24_LOONGARCH64 => "f24",
        REG_F25_LOONGARCH64 => "f25",
        REG_F26_LOONGARCH64 => "f26",
        REG_F27_LOONGARCH64 => "f27",
        REG_F28_LOONGARCH64 => "f28",
        REG_F29_LOONGARCH64 => "f29",
        REG_F30_LOONGARCH64 => "f30",
        REG_F31_LOONGARCH64 => "f31",
        REG_STK_LOONGARCH64 => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameRISCV64(regNumber regNum) => regNum switch
    {
        REG_R0_RISCV64 => "zero",
        REG_RA_RISCV64 => "ra",
        REG_SP_RISCV64 => "sp",
        REG_GP_RISCV64 => "gp",
        REG_TP_RISCV64 => "tp",
        REG_T0_RISCV64 => "t0",
        REG_T1_RISCV64 => "t1",
        REG_T2_RISCV64 => "t2",
        REG_FP_RISCV64 => "fp",
        REG_S1_RISCV64 => "s1",
        REG_A0_RISCV64 => "a0",
        REG_A1_RISCV64 => "a1",
        REG_A2_RISCV64 => "a2",
        REG_A3_RISCV64 => "a3",
        REG_A4_RISCV64 => "a4",
        REG_A5_RISCV64 => "a5",
        REG_A6_RISCV64 => "a6",
        REG_A7_RISCV64 => "a7",
        REG_S2_RISCV64 => "s2",
        REG_S3_RISCV64 => "s3",
        REG_S4_RISCV64 => "s4",
        REG_S5_RISCV64 => "s5",
        REG_S6_RISCV64 => "s6",
        REG_S7_RISCV64 => "s7",
        REG_S8_RISCV64 => "s8",
        REG_S9_RISCV64 => "s9",
        REG_S10_RISCV64 => "s10",
        REG_S11_RISCV64 => "s11",
        REG_T3_RISCV64 => "t3",
        REG_T4_RISCV64 => "t4",
        REG_T5_RISCV64 => "t5",
        REG_T6_RISCV64 => "t6",
        REG_FT0_RISCV64 => "ft0",
        REG_FT1_RISCV64 => "ft1",
        REG_FT2_RISCV64 => "ft2",
        REG_FT3_RISCV64 => "ft3",
        REG_FT4_RISCV64 => "ft4",
        REG_FT5_RISCV64 => "ft5",
        REG_FT6_RISCV64 => "ft6",
        REG_FT7_RISCV64 => "ft7",
        REG_FS0_RISCV64 => "fs0",
        REG_FS1_RISCV64 => "fs1",
        REG_FA0_RISCV64 => "fa0",
        REG_FA1_RISCV64 => "fa1",
        REG_FA2_RISCV64 => "fa2",
        REG_FA3_RISCV64 => "fa3",
        REG_FA4_RISCV64 => "fa4",
        REG_FA5_RISCV64 => "fa5",
        REG_FA6_RISCV64 => "fa6",
        REG_FA7_RISCV64 => "fa7",
        REG_FS2_RISCV64 => "fs2",
        REG_FS3_RISCV64 => "fs3",
        REG_FS4_RISCV64 => "fs4",
        REG_FS5_RISCV64 => "fs5",
        REG_FS6_RISCV64 => "fs6",
        REG_FS7_RISCV64 => "fs7",
        REG_FS8_RISCV64 => "fs8",
        REG_FS9_RISCV64 => "fs9",
        REG_FS10_RISCV64 => "fs10",
        REG_FS11_RISCV64 => "fs11",
        REG_FT8_RISCV64 => "ft8",
        REG_FT9_RISCV64 => "ft9",
        REG_FT10_RISCV64 => "ft10",
        REG_FT11_RISCV64 => "ft11",
        REG_STK_RISCV64 => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };



    private static ReadOnlySpan<regMask> s_masks_X86 => [
        SRBM_EAX_X86, // REG_EAX_X86
        SRBM_ECX_X86, // REG_ECX_X86
        SRBM_EDX_X86, // REG_EDX_X86
        SRBM_EBX_X86, // REG_EBX_X86
        SRBM_ESP_X86, // REG_ESP_X86
        SRBM_EBP_X86, // REG_EBP_X86
        SRBM_ESI_X86, // REG_ESI_X86
        SRBM_EDI_X86, // REG_EDI_X86
        SRBM_XMM0_X86, // REG_XMM0_X86
        SRBM_XMM1_X86, // REG_XMM1_X86
        SRBM_XMM2_X86, // REG_XMM2_X86
        SRBM_XMM3_X86, // REG_XMM3_X86
        SRBM_XMM4_X86, // REG_XMM4_X86
        SRBM_XMM5_X86, // REG_XMM5_X86
        SRBM_XMM6_X86, // REG_XMM6_X86
        SRBM_XMM7_X86, // REG_XMM7_X86
        SRBM_K0_X86, // REG_K0_X86
        SRBM_K1_X86, // REG_K1_X86
        SRBM_K2_X86, // REG_K2_X86
        SRBM_K3_X86, // REG_K3_X86
        SRBM_K4_X86, // REG_K4_X86
        SRBM_K5_X86, // REG_K5_X86
        SRBM_K6_X86, // REG_K6_X86
        SRBM_K7_X86, // REG_K7_X86
        SRBM_STK_X86, // REG_STK_X86
    ];

    private static ReadOnlySpan<regMask> s_masks_AMD64 => [
        SRBM_RAX_AMD64, // REG_RAX_AMD64
        SRBM_RCX_AMD64, // REG_RCX_AMD64
        SRBM_RDX_AMD64, // REG_RDX_AMD64
        SRBM_RBX_AMD64, // REG_RBX_AMD64
        SRBM_RSP_AMD64, // REG_RSP_AMD64
        SRBM_RBP_AMD64, // REG_RBP_AMD64
        SRBM_RSI_AMD64, // REG_RSI_AMD64
        SRBM_RDI_AMD64, // REG_RDI_AMD64
        SRBM_R8_AMD64, // REG_R8_AMD64
        SRBM_R9_AMD64, // REG_R9_AMD64
        SRBM_R10_AMD64, // REG_R10_AMD64
        SRBM_R11_AMD64, // REG_R11_AMD64
        SRBM_R12_AMD64, // REG_R12_AMD64
        SRBM_R13_AMD64, // REG_R13_AMD64
        SRBM_R14_AMD64, // REG_R14_AMD64
        SRBM_R15_AMD64, // REG_R15_AMD64
        SRBM_R16_AMD64, // REG_R16_AMD64
        SRBM_R17_AMD64, // REG_R17_AMD64
        SRBM_R18_AMD64, // REG_R18_AMD64
        SRBM_R19_AMD64, // REG_R19_AMD64
        SRBM_R20_AMD64, // REG_R20_AMD64
        SRBM_R21_AMD64, // REG_R21_AMD64
        SRBM_R22_AMD64, // REG_R22_AMD64
        SRBM_R23_AMD64, // REG_R23_AMD64
        SRBM_R24_AMD64, // REG_R24_AMD64
        SRBM_R25_AMD64, // REG_R25_AMD64
        SRBM_R26_AMD64, // REG_R26_AMD64
        SRBM_R27_AMD64, // REG_R27_AMD64
        SRBM_R28_AMD64, // REG_R28_AMD64
        SRBM_R29_AMD64, // REG_R29_AMD64
        SRBM_R30_AMD64, // REG_R30_AMD64
        SRBM_R31_AMD64, // REG_R31_AMD64
        SRBM_XMM0_AMD64, // REG_XMM0_AMD64
        SRBM_XMM1_AMD64, // REG_XMM1_AMD64
        SRBM_XMM2_AMD64, // REG_XMM2_AMD64
        SRBM_XMM3_AMD64, // REG_XMM3_AMD64
        SRBM_XMM4_AMD64, // REG_XMM4_AMD64
        SRBM_XMM5_AMD64, // REG_XMM5_AMD64
        SRBM_XMM6_AMD64, // REG_XMM6_AMD64
        SRBM_XMM7_AMD64, // REG_XMM7_AMD64
        SRBM_XMM8_AMD64, // REG_XMM8_AMD64
        SRBM_XMM9_AMD64, // REG_XMM9_AMD64
        SRBM_XMM10_AMD64, // REG_XMM10_AMD64
        SRBM_XMM11_AMD64, // REG_XMM11_AMD64
        SRBM_XMM12_AMD64, // REG_XMM12_AMD64
        SRBM_XMM13_AMD64, // REG_XMM13_AMD64
        SRBM_XMM14_AMD64, // REG_XMM14_AMD64
        SRBM_XMM15_AMD64, // REG_XMM15_AMD64
        SRBM_XMM16_AMD64, // REG_XMM16_AMD64
        SRBM_XMM17_AMD64, // REG_XMM17_AMD64
        SRBM_XMM18_AMD64, // REG_XMM18_AMD64
        SRBM_XMM19_AMD64, // REG_XMM19_AMD64
        SRBM_XMM20_AMD64, // REG_XMM20_AMD64
        SRBM_XMM21_AMD64, // REG_XMM21_AMD64
        SRBM_XMM22_AMD64, // REG_XMM22_AMD64
        SRBM_XMM23_AMD64, // REG_XMM23_AMD64
        SRBM_XMM24_AMD64, // REG_XMM24_AMD64
        SRBM_XMM25_AMD64, // REG_XMM25_AMD64
        SRBM_XMM26_AMD64, // REG_XMM26_AMD64
        SRBM_XMM27_AMD64, // REG_XMM27_AMD64
        SRBM_XMM28_AMD64, // REG_XMM28_AMD64
        SRBM_XMM29_AMD64, // REG_XMM29_AMD64
        SRBM_XMM30_AMD64, // REG_XMM30_AMD64
        SRBM_XMM31_AMD64, // REG_XMM31_AMD64
        SRBM_K0_AMD64, // REG_K0_AMD64
        SRBM_K1_AMD64, // REG_K1_AMD64
        SRBM_K2_AMD64, // REG_K2_AMD64
        SRBM_K3_AMD64, // REG_K3_AMD64
        SRBM_K4_AMD64, // REG_K4_AMD64
        SRBM_K5_AMD64, // REG_K5_AMD64
        SRBM_K6_AMD64, // REG_K6_AMD64
        SRBM_K7_AMD64, // REG_K7_AMD64
        SRBM_STK_AMD64, // REG_STK_AMD64
    ];

    private static ReadOnlySpan<regMask> s_masks_ARM => [
        SRBM_R0_ARM, // REG_R0_ARM
        SRBM_R1_ARM, // REG_R1_ARM
        SRBM_R2_ARM, // REG_R2_ARM
        SRBM_R3_ARM, // REG_R3_ARM
        SRBM_R4_ARM, // REG_R4_ARM
        SRBM_R5_ARM, // REG_R5_ARM
        SRBM_R6_ARM, // REG_R6_ARM
        SRBM_R7_ARM, // REG_R7_ARM
        SRBM_R8_ARM, // REG_R8_ARM
        SRBM_R9_ARM, // REG_R9_ARM
        SRBM_R10_ARM, // REG_R10_ARM
        SRBM_R11_ARM, // REG_R11_ARM
        SRBM_R12_ARM, // REG_R12_ARM
        SRBM_SP_ARM, // REG_SP_ARM
        SRBM_LR_ARM, // REG_LR_ARM
        SRBM_PC_ARM, // REG_PC_ARM
        SRBM_F0_ARM, // REG_F0_ARM
        SRBM_F1_ARM, // REG_F1_ARM
        SRBM_F2_ARM, // REG_F2_ARM
        SRBM_F3_ARM, // REG_F3_ARM
        SRBM_F4_ARM, // REG_F4_ARM
        SRBM_F5_ARM, // REG_F5_ARM
        SRBM_F6_ARM, // REG_F6_ARM
        SRBM_F7_ARM, // REG_F7_ARM
        SRBM_F8_ARM, // REG_F8_ARM
        SRBM_F9_ARM, // REG_F9_ARM
        SRBM_F10_ARM, // REG_F10_ARM
        SRBM_F11_ARM, // REG_F11_ARM
        SRBM_F12_ARM, // REG_F12_ARM
        SRBM_F13_ARM, // REG_F13_ARM
        SRBM_F14_ARM, // REG_F14_ARM
        SRBM_F15_ARM, // REG_F15_ARM
        SRBM_F16_ARM, // REG_F16_ARM
        SRBM_F17_ARM, // REG_F17_ARM
        SRBM_F18_ARM, // REG_F18_ARM
        SRBM_F19_ARM, // REG_F19_ARM
        SRBM_F20_ARM, // REG_F20_ARM
        SRBM_F21_ARM, // REG_F21_ARM
        SRBM_F22_ARM, // REG_F22_ARM
        SRBM_F23_ARM, // REG_F23_ARM
        SRBM_F24_ARM, // REG_F24_ARM
        SRBM_F25_ARM, // REG_F25_ARM
        SRBM_F26_ARM, // REG_F26_ARM
        SRBM_F27_ARM, // REG_F27_ARM
        SRBM_F28_ARM, // REG_F28_ARM
        SRBM_F29_ARM, // REG_F29_ARM
        SRBM_F30_ARM, // REG_F30_ARM
        SRBM_F31_ARM, // REG_F31_ARM
        SRBM_STK_ARM, // REG_STK_ARM
    ];

    private static ReadOnlySpan<regMask> s_masks_ARM64 => [
        SRBM_R0_ARM64, // REG_R0_ARM64
        SRBM_R1_ARM64, // REG_R1_ARM64
        SRBM_R2_ARM64, // REG_R2_ARM64
        SRBM_R3_ARM64, // REG_R3_ARM64
        SRBM_R4_ARM64, // REG_R4_ARM64
        SRBM_R5_ARM64, // REG_R5_ARM64
        SRBM_R6_ARM64, // REG_R6_ARM64
        SRBM_R7_ARM64, // REG_R7_ARM64
        SRBM_R8_ARM64, // REG_R8_ARM64
        SRBM_R9_ARM64, // REG_R9_ARM64
        SRBM_R10_ARM64, // REG_R10_ARM64
        SRBM_R11_ARM64, // REG_R11_ARM64
        SRBM_R12_ARM64, // REG_R12_ARM64
        SRBM_R13_ARM64, // REG_R13_ARM64
        SRBM_R14_ARM64, // REG_R14_ARM64
        SRBM_R15_ARM64, // REG_R15_ARM64
        SRBM_IP0_ARM64, // REG_IP0_ARM64
        SRBM_IP1_ARM64, // REG_IP1_ARM64
        SRBM_PR_ARM64, // REG_PR_ARM64
        SRBM_R19_ARM64, // REG_R19_ARM64
        SRBM_R20_ARM64, // REG_R20_ARM64
        SRBM_R21_ARM64, // REG_R21_ARM64
        SRBM_R22_ARM64, // REG_R22_ARM64
        SRBM_R23_ARM64, // REG_R23_ARM64
        SRBM_R24_ARM64, // REG_R24_ARM64
        SRBM_R25_ARM64, // REG_R25_ARM64
        SRBM_R26_ARM64, // REG_R26_ARM64
        SRBM_R27_ARM64, // REG_R27_ARM64
        SRBM_R28_ARM64, // REG_R28_ARM64
        SRBM_FP_ARM64, // REG_FP_ARM64
        SRBM_LR_ARM64, // REG_LR_ARM64
        SRBM_ZR_ARM64, // REG_ZR_ARM64
        SRBM_V0_ARM64, // REG_V0_ARM64
        SRBM_V1_ARM64, // REG_V1_ARM64
        SRBM_V2_ARM64, // REG_V2_ARM64
        SRBM_V3_ARM64, // REG_V3_ARM64
        SRBM_V4_ARM64, // REG_V4_ARM64
        SRBM_V5_ARM64, // REG_V5_ARM64
        SRBM_V6_ARM64, // REG_V6_ARM64
        SRBM_V7_ARM64, // REG_V7_ARM64
        SRBM_V8_ARM64, // REG_V8_ARM64
        SRBM_V9_ARM64, // REG_V9_ARM64
        SRBM_V10_ARM64, // REG_V10_ARM64
        SRBM_V11_ARM64, // REG_V11_ARM64
        SRBM_V12_ARM64, // REG_V12_ARM64
        SRBM_V13_ARM64, // REG_V13_ARM64
        SRBM_V14_ARM64, // REG_V14_ARM64
        SRBM_V15_ARM64, // REG_V15_ARM64
        SRBM_V16_ARM64, // REG_V16_ARM64
        SRBM_V17_ARM64, // REG_V17_ARM64
        SRBM_V18_ARM64, // REG_V18_ARM64
        SRBM_V19_ARM64, // REG_V19_ARM64
        SRBM_V20_ARM64, // REG_V20_ARM64
        SRBM_V21_ARM64, // REG_V21_ARM64
        SRBM_V22_ARM64, // REG_V22_ARM64
        SRBM_V23_ARM64, // REG_V23_ARM64
        SRBM_V24_ARM64, // REG_V24_ARM64
        SRBM_V25_ARM64, // REG_V25_ARM64
        SRBM_V26_ARM64, // REG_V26_ARM64
        SRBM_V27_ARM64, // REG_V27_ARM64
        SRBM_V28_ARM64, // REG_V28_ARM64
        SRBM_V29_ARM64, // REG_V29_ARM64
        SRBM_V30_ARM64, // REG_V30_ARM64
        SRBM_V31_ARM64, // REG_V31_ARM64
        SRBM_P0_ARM64, // REG_P0_ARM64
        SRBM_P1_ARM64, // REG_P1_ARM64
        SRBM_P2_ARM64, // REG_P2_ARM64
        SRBM_P3_ARM64, // REG_P3_ARM64
        SRBM_P4_ARM64, // REG_P4_ARM64
        SRBM_P5_ARM64, // REG_P5_ARM64
        SRBM_P6_ARM64, // REG_P6_ARM64
        SRBM_P7_ARM64, // REG_P7_ARM64
        SRBM_P8_ARM64, // REG_P8_ARM64
        SRBM_P9_ARM64, // REG_P9_ARM64
        SRBM_P10_ARM64, // REG_P10_ARM64
        SRBM_P11_ARM64, // REG_P11_ARM64
        SRBM_P12_ARM64, // REG_P12_ARM64
        SRBM_P13_ARM64, // REG_P13_ARM64
        SRBM_P14_ARM64, // REG_P14_ARM64
        SRBM_P15_ARM64, // REG_P15_ARM64
        SRBM_SP_ARM64, // REG_SP_ARM64
        SRBM_FFR_ARM64, // REG_FFR_ARM64
        SRBM_STK_ARM64, // REG_STK_ARM64
    ];

    private static ReadOnlySpan<regMask> s_masks_LOONGARCH64 => [
        SRBM_R0_LOONGARCH64, // REG_R0_LOONGARCH64
        SRBM_RA_LOONGARCH64, // REG_RA_LOONGARCH64
        SRBM_TP_LOONGARCH64, // REG_TP_LOONGARCH64
        SRBM_SP_LOONGARCH64, // REG_SP_LOONGARCH64
        SRBM_A0_LOONGARCH64, // REG_A0_LOONGARCH64
        SRBM_A1_LOONGARCH64, // REG_A1_LOONGARCH64
        SRBM_A2_LOONGARCH64, // REG_A2_LOONGARCH64
        SRBM_A3_LOONGARCH64, // REG_A3_LOONGARCH64
        SRBM_A4_LOONGARCH64, // REG_A4_LOONGARCH64
        SRBM_A5_LOONGARCH64, // REG_A5_LOONGARCH64
        SRBM_A6_LOONGARCH64, // REG_A6_LOONGARCH64
        SRBM_A7_LOONGARCH64, // REG_A7_LOONGARCH64
        SRBM_T0_LOONGARCH64, // REG_T0_LOONGARCH64
        SRBM_T1_LOONGARCH64, // REG_T1_LOONGARCH64
        SRBM_T2_LOONGARCH64, // REG_T2_LOONGARCH64
        SRBM_T3_LOONGARCH64, // REG_T3_LOONGARCH64
        SRBM_T4_LOONGARCH64, // REG_T4_LOONGARCH64
        SRBM_T5_LOONGARCH64, // REG_T5_LOONGARCH64
        SRBM_T6_LOONGARCH64, // REG_T6_LOONGARCH64
        SRBM_T7_LOONGARCH64, // REG_T7_LOONGARCH64
        SRBM_T8_LOONGARCH64, // REG_T8_LOONGARCH64
        SRBM_X0_LOONGARCH64, // REG_X0_LOONGARCH64
        SRBM_FP_LOONGARCH64, // REG_FP_LOONGARCH64
        SRBM_S0_LOONGARCH64, // REG_S0_LOONGARCH64
        SRBM_S1_LOONGARCH64, // REG_S1_LOONGARCH64
        SRBM_S2_LOONGARCH64, // REG_S2_LOONGARCH64
        SRBM_S3_LOONGARCH64, // REG_S3_LOONGARCH64
        SRBM_S4_LOONGARCH64, // REG_S4_LOONGARCH64
        SRBM_S5_LOONGARCH64, // REG_S5_LOONGARCH64
        SRBM_S6_LOONGARCH64, // REG_S6_LOONGARCH64
        SRBM_S7_LOONGARCH64, // REG_S7_LOONGARCH64
        SRBM_S8_LOONGARCH64, // REG_S8_LOONGARCH64
        SRBM_F0_LOONGARCH64, // REG_F0_LOONGARCH64
        SRBM_F1_LOONGARCH64, // REG_F1_LOONGARCH64
        SRBM_F2_LOONGARCH64, // REG_F2_LOONGARCH64
        SRBM_F3_LOONGARCH64, // REG_F3_LOONGARCH64
        SRBM_F4_LOONGARCH64, // REG_F4_LOONGARCH64
        SRBM_F5_LOONGARCH64, // REG_F5_LOONGARCH64
        SRBM_F6_LOONGARCH64, // REG_F6_LOONGARCH64
        SRBM_F7_LOONGARCH64, // REG_F7_LOONGARCH64
        SRBM_F8_LOONGARCH64, // REG_F8_LOONGARCH64
        SRBM_F9_LOONGARCH64, // REG_F9_LOONGARCH64
        SRBM_F10_LOONGARCH64, // REG_F10_LOONGARCH64
        SRBM_F11_LOONGARCH64, // REG_F11_LOONGARCH64
        SRBM_F12_LOONGARCH64, // REG_F12_LOONGARCH64
        SRBM_F13_LOONGARCH64, // REG_F13_LOONGARCH64
        SRBM_F14_LOONGARCH64, // REG_F14_LOONGARCH64
        SRBM_F15_LOONGARCH64, // REG_F15_LOONGARCH64
        SRBM_F16_LOONGARCH64, // REG_F16_LOONGARCH64
        SRBM_F17_LOONGARCH64, // REG_F17_LOONGARCH64
        SRBM_F18_LOONGARCH64, // REG_F18_LOONGARCH64
        SRBM_F19_LOONGARCH64, // REG_F19_LOONGARCH64
        SRBM_F20_LOONGARCH64, // REG_F20_LOONGARCH64
        SRBM_F21_LOONGARCH64, // REG_F21_LOONGARCH64
        SRBM_F22_LOONGARCH64, // REG_F22_LOONGARCH64
        SRBM_F23_LOONGARCH64, // REG_F23_LOONGARCH64
        SRBM_F24_LOONGARCH64, // REG_F24_LOONGARCH64
        SRBM_F25_LOONGARCH64, // REG_F25_LOONGARCH64
        SRBM_F26_LOONGARCH64, // REG_F26_LOONGARCH64
        SRBM_F27_LOONGARCH64, // REG_F27_LOONGARCH64
        SRBM_F28_LOONGARCH64, // REG_F28_LOONGARCH64
        SRBM_F29_LOONGARCH64, // REG_F29_LOONGARCH64
        SRBM_F30_LOONGARCH64, // REG_F30_LOONGARCH64
        SRBM_F31_LOONGARCH64, // REG_F31_LOONGARCH64
        SRBM_STK_LOONGARCH64, // REG_STK_LOONGARCH64
    ];

    private static ReadOnlySpan<regMask> s_masks_RISCV64 => [
        SRBM_R0_RISCV64, // REG_R0_RISCV64
        SRBM_RA_RISCV64, // REG_RA_RISCV64
        SRBM_SP_RISCV64, // REG_SP_RISCV64
        SRBM_GP_RISCV64, // REG_GP_RISCV64
        SRBM_TP_RISCV64, // REG_TP_RISCV64
        SRBM_T0_RISCV64, // REG_T0_RISCV64
        SRBM_T1_RISCV64, // REG_T1_RISCV64
        SRBM_T2_RISCV64, // REG_T2_RISCV64
        SRBM_FP_RISCV64, // REG_FP_RISCV64
        SRBM_S1_RISCV64, // REG_S1_RISCV64
        SRBM_A0_RISCV64, // REG_A0_RISCV64
        SRBM_A1_RISCV64, // REG_A1_RISCV64
        SRBM_A2_RISCV64, // REG_A2_RISCV64
        SRBM_A3_RISCV64, // REG_A3_RISCV64
        SRBM_A4_RISCV64, // REG_A4_RISCV64
        SRBM_A5_RISCV64, // REG_A5_RISCV64
        SRBM_A6_RISCV64, // REG_A6_RISCV64
        SRBM_A7_RISCV64, // REG_A7_RISCV64
        SRBM_S2_RISCV64, // REG_S2_RISCV64
        SRBM_S3_RISCV64, // REG_S3_RISCV64
        SRBM_S4_RISCV64, // REG_S4_RISCV64
        SRBM_S5_RISCV64, // REG_S5_RISCV64
        SRBM_S6_RISCV64, // REG_S6_RISCV64
        SRBM_S7_RISCV64, // REG_S7_RISCV64
        SRBM_S8_RISCV64, // REG_S8_RISCV64
        SRBM_S9_RISCV64, // REG_S9_RISCV64
        SRBM_S10_RISCV64, // REG_S10_RISCV64
        SRBM_S11_RISCV64, // REG_S11_RISCV64
        SRBM_T3_RISCV64, // REG_T3_RISCV64
        SRBM_T4_RISCV64, // REG_T4_RISCV64
        SRBM_T5_RISCV64, // REG_T5_RISCV64
        SRBM_T6_RISCV64, // REG_T6_RISCV64
        SRBM_FT0_RISCV64, // REG_FT0_RISCV64
        SRBM_FT1_RISCV64, // REG_FT1_RISCV64
        SRBM_FT2_RISCV64, // REG_FT2_RISCV64
        SRBM_FT3_RISCV64, // REG_FT3_RISCV64
        SRBM_FT4_RISCV64, // REG_FT4_RISCV64
        SRBM_FT5_RISCV64, // REG_FT5_RISCV64
        SRBM_FT6_RISCV64, // REG_FT6_RISCV64
        SRBM_FT7_RISCV64, // REG_FT7_RISCV64
        SRBM_FS0_RISCV64, // REG_FS0_RISCV64
        SRBM_FS1_RISCV64, // REG_FS1_RISCV64
        SRBM_FA0_RISCV64, // REG_FA0_RISCV64
        SRBM_FA1_RISCV64, // REG_FA1_RISCV64
        SRBM_FA2_RISCV64, // REG_FA2_RISCV64
        SRBM_FA3_RISCV64, // REG_FA3_RISCV64
        SRBM_FA4_RISCV64, // REG_FA4_RISCV64
        SRBM_FA5_RISCV64, // REG_FA5_RISCV64
        SRBM_FA6_RISCV64, // REG_FA6_RISCV64
        SRBM_FA7_RISCV64, // REG_FA7_RISCV64
        SRBM_FS2_RISCV64, // REG_FS2_RISCV64
        SRBM_FS3_RISCV64, // REG_FS3_RISCV64
        SRBM_FS4_RISCV64, // REG_FS4_RISCV64
        SRBM_FS5_RISCV64, // REG_FS5_RISCV64
        SRBM_FS6_RISCV64, // REG_FS6_RISCV64
        SRBM_FS7_RISCV64, // REG_FS7_RISCV64
        SRBM_FS8_RISCV64, // REG_FS8_RISCV64
        SRBM_FS9_RISCV64, // REG_FS9_RISCV64
        SRBM_FS10_RISCV64, // REG_FS10_RISCV64
        SRBM_FS11_RISCV64, // REG_FS11_RISCV64
        SRBM_FT8_RISCV64, // REG_FT8_RISCV64
        SRBM_FT9_RISCV64, // REG_FT9_RISCV64
        SRBM_FT10_RISCV64, // REG_FT10_RISCV64
        SRBM_FT11_RISCV64, // REG_FT11_RISCV64
        SRBM_STK_RISCV64, // REG_STK_RISCV64
    ];

    private static ReadOnlySpan<regMask> s_masks_WASM => [
        SRBM_NONE_WASM, // unused register ordinal 0
        SRBM_STK_WASM, // REG_STK_WASM
    ];

    private static ReadOnlySpan<regMask> s_masks
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return s_masks_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return s_masks_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return s_masks_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return s_masks_ARM64;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return s_masks_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return s_masks_RISCV64;
            }
            if (Configuration.TARGET_WASM)
            {
                return s_masks_WASM;
            }
            throw new FatalJitException("Invalid target architecture.");
        }
    }

}