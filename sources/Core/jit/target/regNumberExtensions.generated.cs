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
        REG_X86_EAX => "eax",
        REG_X86_ECX => "ecx",
        REG_X86_EDX => "edx",
        REG_X86_EBX => "ebx",
        REG_X86_ESP => "esp",
        REG_X86_EBP => "ebp",
        REG_X86_ESI => "esi",
        REG_X86_EDI => "edi",
        REG_X86_XMM0 => "mm0",
        REG_X86_XMM1 => "mm1",
        REG_X86_XMM2 => "mm2",
        REG_X86_XMM3 => "mm3",
        REG_X86_XMM4 => "mm4",
        REG_X86_XMM5 => "mm5",
        REG_X86_XMM6 => "mm6",
        REG_X86_XMM7 => "mm7",
        REG_X86_K0 => "k0",
        REG_X86_K1 => "k1",
        REG_X86_K2 => "k2",
        REG_X86_K3 => "k3",
        REG_X86_K4 => "k4",
        REG_X86_K5 => "k5",
        REG_X86_K6 => "k6",
        REG_X86_K7 => "k7",
        REG_X86_STK => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameX64(regNumber regNum) => regNum switch
    {
        REG_X64_RAX => "rax",
        REG_X64_RCX => "rcx",
        REG_X64_RDX => "rdx",
        REG_X64_RBX => "rbx",
        REG_X64_RSP => "rsp",
        REG_X64_RBP => "rbp",
        REG_X64_RSI => "rsi",
        REG_X64_RDI => "rdi",
        REG_X64_R8 => "r8",
        REG_X64_R9 => "r9",
        REG_X64_R10 => "r10",
        REG_X64_R11 => "r11",
        REG_X64_R12 => "r12",
        REG_X64_R13 => "r13",
        REG_X64_R14 => "r14",
        REG_X64_R15 => "r15",
        REG_X64_R16 => "r16",
        REG_X64_R17 => "r17",
        REG_X64_R18 => "r18",
        REG_X64_R19 => "r19",
        REG_X64_R20 => "r20",
        REG_X64_R21 => "r21",
        REG_X64_R22 => "r22",
        REG_X64_R23 => "r23",
        REG_X64_R24 => "r24",
        REG_X64_R25 => "r25",
        REG_X64_R26 => "r26",
        REG_X64_R27 => "r27",
        REG_X64_R28 => "r28",
        REG_X64_R29 => "r29",
        REG_X64_R30 => "r30",
        REG_X64_R31 => "r31",
        REG_X64_XMM0 => "mm0",
        REG_X64_XMM1 => "mm1",
        REG_X64_XMM2 => "mm2",
        REG_X64_XMM3 => "mm3",
        REG_X64_XMM4 => "mm4",
        REG_X64_XMM5 => "mm5",
        REG_X64_XMM6 => "mm6",
        REG_X64_XMM7 => "mm7",
        REG_X64_XMM8 => "mm8",
        REG_X64_XMM9 => "mm9",
        REG_X64_XMM10 => "mm10",
        REG_X64_XMM11 => "mm11",
        REG_X64_XMM12 => "mm12",
        REG_X64_XMM13 => "mm13",
        REG_X64_XMM14 => "mm14",
        REG_X64_XMM15 => "mm15",
        REG_X64_XMM16 => "mm16",
        REG_X64_XMM17 => "mm17",
        REG_X64_XMM18 => "mm18",
        REG_X64_XMM19 => "mm19",
        REG_X64_XMM20 => "mm20",
        REG_X64_XMM21 => "mm21",
        REG_X64_XMM22 => "mm22",
        REG_X64_XMM23 => "mm23",
        REG_X64_XMM24 => "mm24",
        REG_X64_XMM25 => "mm25",
        REG_X64_XMM26 => "mm26",
        REG_X64_XMM27 => "mm27",
        REG_X64_XMM28 => "mm28",
        REG_X64_XMM29 => "mm29",
        REG_X64_XMM30 => "mm30",
        REG_X64_XMM31 => "mm31",
        REG_X64_K0 => "k0",
        REG_X64_K1 => "k1",
        REG_X64_K2 => "k2",
        REG_X64_K3 => "k3",
        REG_X64_K4 => "k4",
        REG_X64_K5 => "k5",
        REG_X64_K6 => "k6",
        REG_X64_K7 => "k7",
        REG_X64_STK => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameARM32(regNumber regNum) => regNum switch
    {
        REG_ARM32_R0 => "r0",
        REG_ARM32_R1 => "r1",
        REG_ARM32_R2 => "r2",
        REG_ARM32_R3 => "r3",
        REG_ARM32_R4 => "r4",
        REG_ARM32_R5 => "r5",
        REG_ARM32_R6 => "r6",
        REG_ARM32_R7 => "r7",
        REG_ARM32_R8 => "r8",
        REG_ARM32_R9 => "r9",
        REG_ARM32_R10 => "r10",
        REG_ARM32_R11 => "r11",
        REG_ARM32_R12 => "r12",
        REG_ARM32_SP => "sp",
        REG_ARM32_LR => "lr",
        REG_ARM32_PC => "pc",
        REG_ARM32_F0 => "f0",
        REG_ARM32_F1 => "f1",
        REG_ARM32_F2 => "f2",
        REG_ARM32_F3 => "f3",
        REG_ARM32_F4 => "f4",
        REG_ARM32_F5 => "f5",
        REG_ARM32_F6 => "f6",
        REG_ARM32_F7 => "f7",
        REG_ARM32_F8 => "f8",
        REG_ARM32_F9 => "f9",
        REG_ARM32_F10 => "f10",
        REG_ARM32_F11 => "f11",
        REG_ARM32_F12 => "f12",
        REG_ARM32_F13 => "f13",
        REG_ARM32_F14 => "f14",
        REG_ARM32_F15 => "f15",
        REG_ARM32_F16 => "f16",
        REG_ARM32_F17 => "f17",
        REG_ARM32_F18 => "f18",
        REG_ARM32_F19 => "f19",
        REG_ARM32_F20 => "f20",
        REG_ARM32_F21 => "f21",
        REG_ARM32_F22 => "f22",
        REG_ARM32_F23 => "f23",
        REG_ARM32_F24 => "f24",
        REG_ARM32_F25 => "f25",
        REG_ARM32_F26 => "f26",
        REG_ARM32_F27 => "f27",
        REG_ARM32_F28 => "f28",
        REG_ARM32_F29 => "f29",
        REG_ARM32_F30 => "f30",
        REG_ARM32_F31 => "f31",
        REG_ARM32_STK => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameARM64(regNumber regNum) => regNum switch
    {
        REG_ARM64_R0 => "x0",
        REG_ARM64_R1 => "x1",
        REG_ARM64_R2 => "x2",
        REG_ARM64_R3 => "x3",
        REG_ARM64_R4 => "x4",
        REG_ARM64_R5 => "x5",
        REG_ARM64_R6 => "x6",
        REG_ARM64_R7 => "x7",
        REG_ARM64_R8 => "x8",
        REG_ARM64_R9 => "x9",
        REG_ARM64_R10 => "x10",
        REG_ARM64_R11 => "x11",
        REG_ARM64_R12 => "x12",
        REG_ARM64_R13 => "x13",
        REG_ARM64_R14 => "x14",
        REG_ARM64_R15 => "x15",
        REG_ARM64_IP0 => "xip0",
        REG_ARM64_IP1 => "xip1",
        REG_ARM64_PR => "xpr",
        REG_ARM64_R19 => "x19",
        REG_ARM64_R20 => "x20",
        REG_ARM64_R21 => "x21",
        REG_ARM64_R22 => "x22",
        REG_ARM64_R23 => "x23",
        REG_ARM64_R24 => "x24",
        REG_ARM64_R25 => "x25",
        REG_ARM64_R26 => "x26",
        REG_ARM64_R27 => "x27",
        REG_ARM64_R28 => "x28",
        REG_ARM64_FP => "fp",
        REG_ARM64_LR => "lr",
        REG_ARM64_ZR => "xzr",
        REG_ARM64_V0 => "d0",
        REG_ARM64_V1 => "d1",
        REG_ARM64_V2 => "d2",
        REG_ARM64_V3 => "d3",
        REG_ARM64_V4 => "d4",
        REG_ARM64_V5 => "d5",
        REG_ARM64_V6 => "d6",
        REG_ARM64_V7 => "d7",
        REG_ARM64_V8 => "d8",
        REG_ARM64_V9 => "d9",
        REG_ARM64_V10 => "d10",
        REG_ARM64_V11 => "d11",
        REG_ARM64_V12 => "d12",
        REG_ARM64_V13 => "d13",
        REG_ARM64_V14 => "d14",
        REG_ARM64_V15 => "d15",
        REG_ARM64_V16 => "d16",
        REG_ARM64_V17 => "d17",
        REG_ARM64_V18 => "d18",
        REG_ARM64_V19 => "d19",
        REG_ARM64_V20 => "d20",
        REG_ARM64_V21 => "d21",
        REG_ARM64_V22 => "d22",
        REG_ARM64_V23 => "d23",
        REG_ARM64_V24 => "d24",
        REG_ARM64_V25 => "d25",
        REG_ARM64_V26 => "d26",
        REG_ARM64_V27 => "d27",
        REG_ARM64_V28 => "d28",
        REG_ARM64_V29 => "d29",
        REG_ARM64_V30 => "d30",
        REG_ARM64_V31 => "d31",
        REG_ARM64_P0 => "p0",
        REG_ARM64_P1 => "p1",
        REG_ARM64_P2 => "p2",
        REG_ARM64_P3 => "p3",
        REG_ARM64_P4 => "p4",
        REG_ARM64_P5 => "p5",
        REG_ARM64_P6 => "p6",
        REG_ARM64_P7 => "p7",
        REG_ARM64_P8 => "p8",
        REG_ARM64_P9 => "p9",
        REG_ARM64_P10 => "p10",
        REG_ARM64_P11 => "p11",
        REG_ARM64_P12 => "p12",
        REG_ARM64_P13 => "p13",
        REG_ARM64_P14 => "p14",
        REG_ARM64_P15 => "p15",
        REG_ARM64_SP => "sp",
        REG_ARM64_FFR => "ffr",
        REG_ARM64_STK => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameLOONGARCH64(regNumber regNum) => regNum switch
    {
        REG_LOONGARCH64_R0 => "zero",
        REG_LOONGARCH64_RA => "ra",
        REG_LOONGARCH64_TP => "tp",
        REG_LOONGARCH64_SP => "sp",
        REG_LOONGARCH64_A0 => "a0",
        REG_LOONGARCH64_A1 => "a1",
        REG_LOONGARCH64_A2 => "a2",
        REG_LOONGARCH64_A3 => "a3",
        REG_LOONGARCH64_A4 => "a4",
        REG_LOONGARCH64_A5 => "a5",
        REG_LOONGARCH64_A6 => "a6",
        REG_LOONGARCH64_A7 => "a7",
        REG_LOONGARCH64_T0 => "t0",
        REG_LOONGARCH64_T1 => "t1",
        REG_LOONGARCH64_T2 => "t2",
        REG_LOONGARCH64_T3 => "t3",
        REG_LOONGARCH64_T4 => "t4",
        REG_LOONGARCH64_T5 => "t5",
        REG_LOONGARCH64_T6 => "t6",
        REG_LOONGARCH64_T7 => "t7",
        REG_LOONGARCH64_T8 => "t8",
        REG_LOONGARCH64_X0 => "x0",
        REG_LOONGARCH64_FP => "fp",
        REG_LOONGARCH64_S0 => "s0",
        REG_LOONGARCH64_S1 => "s1",
        REG_LOONGARCH64_S2 => "s2",
        REG_LOONGARCH64_S3 => "s3",
        REG_LOONGARCH64_S4 => "s4",
        REG_LOONGARCH64_S5 => "s5",
        REG_LOONGARCH64_S6 => "s6",
        REG_LOONGARCH64_S7 => "s7",
        REG_LOONGARCH64_S8 => "s8",
        REG_LOONGARCH64_F0 => "f0",
        REG_LOONGARCH64_F1 => "f1",
        REG_LOONGARCH64_F2 => "f2",
        REG_LOONGARCH64_F3 => "f3",
        REG_LOONGARCH64_F4 => "f4",
        REG_LOONGARCH64_F5 => "f5",
        REG_LOONGARCH64_F6 => "f6",
        REG_LOONGARCH64_F7 => "f7",
        REG_LOONGARCH64_F8 => "f8",
        REG_LOONGARCH64_F9 => "f9",
        REG_LOONGARCH64_F10 => "f10",
        REG_LOONGARCH64_F11 => "f11",
        REG_LOONGARCH64_F12 => "f12",
        REG_LOONGARCH64_F13 => "f13",
        REG_LOONGARCH64_F14 => "f14",
        REG_LOONGARCH64_F15 => "f15",
        REG_LOONGARCH64_F16 => "f16",
        REG_LOONGARCH64_F17 => "f17",
        REG_LOONGARCH64_F18 => "f18",
        REG_LOONGARCH64_F19 => "f19",
        REG_LOONGARCH64_F20 => "f20",
        REG_LOONGARCH64_F21 => "f21",
        REG_LOONGARCH64_F22 => "f22",
        REG_LOONGARCH64_F23 => "f23",
        REG_LOONGARCH64_F24 => "f24",
        REG_LOONGARCH64_F25 => "f25",
        REG_LOONGARCH64_F26 => "f26",
        REG_LOONGARCH64_F27 => "f27",
        REG_LOONGARCH64_F28 => "f28",
        REG_LOONGARCH64_F29 => "f29",
        REG_LOONGARCH64_F30 => "f30",
        REG_LOONGARCH64_F31 => "f31",
        REG_LOONGARCH64_STK => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };

    private static string GetNameRISCV64(regNumber regNum) => regNum switch
    {
        REG_RISCV64_R0 => "zero",
        REG_RISCV64_RA => "ra",
        REG_RISCV64_SP => "sp",
        REG_RISCV64_GP => "gp",
        REG_RISCV64_TP => "tp",
        REG_RISCV64_T0 => "t0",
        REG_RISCV64_T1 => "t1",
        REG_RISCV64_T2 => "t2",
        REG_RISCV64_FP => "fp",
        REG_RISCV64_S1 => "s1",
        REG_RISCV64_A0 => "a0",
        REG_RISCV64_A1 => "a1",
        REG_RISCV64_A2 => "a2",
        REG_RISCV64_A3 => "a3",
        REG_RISCV64_A4 => "a4",
        REG_RISCV64_A5 => "a5",
        REG_RISCV64_A6 => "a6",
        REG_RISCV64_A7 => "a7",
        REG_RISCV64_S2 => "s2",
        REG_RISCV64_S3 => "s3",
        REG_RISCV64_S4 => "s4",
        REG_RISCV64_S5 => "s5",
        REG_RISCV64_S6 => "s6",
        REG_RISCV64_S7 => "s7",
        REG_RISCV64_S8 => "s8",
        REG_RISCV64_S9 => "s9",
        REG_RISCV64_S10 => "s10",
        REG_RISCV64_S11 => "s11",
        REG_RISCV64_T3 => "t3",
        REG_RISCV64_T4 => "t4",
        REG_RISCV64_T5 => "t5",
        REG_RISCV64_T6 => "t6",
        REG_RISCV64_FT0 => "ft0",
        REG_RISCV64_FT1 => "ft1",
        REG_RISCV64_FT2 => "ft2",
        REG_RISCV64_FT3 => "ft3",
        REG_RISCV64_FT4 => "ft4",
        REG_RISCV64_FT5 => "ft5",
        REG_RISCV64_FT6 => "ft6",
        REG_RISCV64_FT7 => "ft7",
        REG_RISCV64_FS0 => "fs0",
        REG_RISCV64_FS1 => "fs1",
        REG_RISCV64_FA0 => "fa0",
        REG_RISCV64_FA1 => "fa1",
        REG_RISCV64_FA2 => "fa2",
        REG_RISCV64_FA3 => "fa3",
        REG_RISCV64_FA4 => "fa4",
        REG_RISCV64_FA5 => "fa5",
        REG_RISCV64_FA6 => "fa6",
        REG_RISCV64_FA7 => "fa7",
        REG_RISCV64_FS2 => "fs2",
        REG_RISCV64_FS3 => "fs3",
        REG_RISCV64_FS4 => "fs4",
        REG_RISCV64_FS5 => "fs5",
        REG_RISCV64_FS6 => "fs6",
        REG_RISCV64_FS7 => "fs7",
        REG_RISCV64_FS8 => "fs8",
        REG_RISCV64_FS9 => "fs9",
        REG_RISCV64_FS10 => "fs10",
        REG_RISCV64_FS11 => "fs11",
        REG_RISCV64_FT8 => "ft8",
        REG_RISCV64_FT9 => "ft9",
        REG_RISCV64_FT10 => "ft10",
        REG_RISCV64_FT11 => "ft11",
        REG_RISCV64_STK => "STK",
        _ => throw new FatalJitException("Invalid register number."),
    };



    private static ReadOnlySpan<regMask> s_masks_X86 => [
        SRBM_X86_EAX, // REG_X86_EAX
        SRBM_X86_ECX, // REG_X86_ECX
        SRBM_X86_EDX, // REG_X86_EDX
        SRBM_X86_EBX, // REG_X86_EBX
        SRBM_X86_ESP, // REG_X86_ESP
        SRBM_X86_EBP, // REG_X86_EBP
        SRBM_X86_ESI, // REG_X86_ESI
        SRBM_X86_EDI, // REG_X86_EDI
        SRBM_X86_XMM0, // REG_X86_XMM0
        SRBM_X86_XMM1, // REG_X86_XMM1
        SRBM_X86_XMM2, // REG_X86_XMM2
        SRBM_X86_XMM3, // REG_X86_XMM3
        SRBM_X86_XMM4, // REG_X86_XMM4
        SRBM_X86_XMM5, // REG_X86_XMM5
        SRBM_X86_XMM6, // REG_X86_XMM6
        SRBM_X86_XMM7, // REG_X86_XMM7
        SRBM_X86_K0, // REG_X86_K0
        SRBM_X86_K1, // REG_X86_K1
        SRBM_X86_K2, // REG_X86_K2
        SRBM_X86_K3, // REG_X86_K3
        SRBM_X86_K4, // REG_X86_K4
        SRBM_X86_K5, // REG_X86_K5
        SRBM_X86_K6, // REG_X86_K6
        SRBM_X86_K7, // REG_X86_K7
        SRBM_STK, // REG_X86_STK
    ];

    private static ReadOnlySpan<regMask> s_masks_X64 => [
        SRBM_X64_RAX, // REG_X64_RAX
        SRBM_X64_RCX, // REG_X64_RCX
        SRBM_X64_RDX, // REG_X64_RDX
        SRBM_X64_RBX, // REG_X64_RBX
        SRBM_X64_RSP, // REG_X64_RSP
        SRBM_X64_RBP, // REG_X64_RBP
        SRBM_X64_RSI, // REG_X64_RSI
        SRBM_X64_RDI, // REG_X64_RDI
        SRBM_X64_R8, // REG_X64_R8
        SRBM_X64_R9, // REG_X64_R9
        SRBM_X64_R10, // REG_X64_R10
        SRBM_X64_R11, // REG_X64_R11
        SRBM_X64_R12, // REG_X64_R12
        SRBM_X64_R13, // REG_X64_R13
        SRBM_X64_R14, // REG_X64_R14
        SRBM_X64_R15, // REG_X64_R15
        SRBM_X64_R16, // REG_X64_R16
        SRBM_X64_R17, // REG_X64_R17
        SRBM_X64_R18, // REG_X64_R18
        SRBM_X64_R19, // REG_X64_R19
        SRBM_X64_R20, // REG_X64_R20
        SRBM_X64_R21, // REG_X64_R21
        SRBM_X64_R22, // REG_X64_R22
        SRBM_X64_R23, // REG_X64_R23
        SRBM_X64_R24, // REG_X64_R24
        SRBM_X64_R25, // REG_X64_R25
        SRBM_X64_R26, // REG_X64_R26
        SRBM_X64_R27, // REG_X64_R27
        SRBM_X64_R28, // REG_X64_R28
        SRBM_X64_R29, // REG_X64_R29
        SRBM_X64_R30, // REG_X64_R30
        SRBM_X64_R31, // REG_X64_R31
        SRBM_X64_XMM0, // REG_X64_XMM0
        SRBM_X64_XMM1, // REG_X64_XMM1
        SRBM_X64_XMM2, // REG_X64_XMM2
        SRBM_X64_XMM3, // REG_X64_XMM3
        SRBM_X64_XMM4, // REG_X64_XMM4
        SRBM_X64_XMM5, // REG_X64_XMM5
        SRBM_X64_XMM6, // REG_X64_XMM6
        SRBM_X64_XMM7, // REG_X64_XMM7
        SRBM_X64_XMM8, // REG_X64_XMM8
        SRBM_X64_XMM9, // REG_X64_XMM9
        SRBM_X64_XMM10, // REG_X64_XMM10
        SRBM_X64_XMM11, // REG_X64_XMM11
        SRBM_X64_XMM12, // REG_X64_XMM12
        SRBM_X64_XMM13, // REG_X64_XMM13
        SRBM_X64_XMM14, // REG_X64_XMM14
        SRBM_X64_XMM15, // REG_X64_XMM15
        SRBM_X64_XMM16, // REG_X64_XMM16
        SRBM_X64_XMM17, // REG_X64_XMM17
        SRBM_X64_XMM18, // REG_X64_XMM18
        SRBM_X64_XMM19, // REG_X64_XMM19
        SRBM_X64_XMM20, // REG_X64_XMM20
        SRBM_X64_XMM21, // REG_X64_XMM21
        SRBM_X64_XMM22, // REG_X64_XMM22
        SRBM_X64_XMM23, // REG_X64_XMM23
        SRBM_X64_XMM24, // REG_X64_XMM24
        SRBM_X64_XMM25, // REG_X64_XMM25
        SRBM_X64_XMM26, // REG_X64_XMM26
        SRBM_X64_XMM27, // REG_X64_XMM27
        SRBM_X64_XMM28, // REG_X64_XMM28
        SRBM_X64_XMM29, // REG_X64_XMM29
        SRBM_X64_XMM30, // REG_X64_XMM30
        SRBM_X64_XMM31, // REG_X64_XMM31
        SRBM_X64_K0, // REG_X64_K0
        SRBM_X64_K1, // REG_X64_K1
        SRBM_X64_K2, // REG_X64_K2
        SRBM_X64_K3, // REG_X64_K3
        SRBM_X64_K4, // REG_X64_K4
        SRBM_X64_K5, // REG_X64_K5
        SRBM_X64_K6, // REG_X64_K6
        SRBM_X64_K7, // REG_X64_K7
        SRBM_STK, // REG_X64_STK
    ];

    private static ReadOnlySpan<regMask> s_masks_ARM32 => [
        SRBM_ARM32_R0, // REG_ARM32_R0
        SRBM_ARM32_R1, // REG_ARM32_R1
        SRBM_ARM32_R2, // REG_ARM32_R2
        SRBM_ARM32_R3, // REG_ARM32_R3
        SRBM_ARM32_R4, // REG_ARM32_R4
        SRBM_ARM32_R5, // REG_ARM32_R5
        SRBM_ARM32_R6, // REG_ARM32_R6
        SRBM_ARM32_R7, // REG_ARM32_R7
        SRBM_ARM32_R8, // REG_ARM32_R8
        SRBM_ARM32_R9, // REG_ARM32_R9
        SRBM_ARM32_R10, // REG_ARM32_R10
        SRBM_ARM32_R11, // REG_ARM32_R11
        SRBM_ARM32_R12, // REG_ARM32_R12
        SRBM_ARM32_SP, // REG_ARM32_SP
        SRBM_ARM32_LR, // REG_ARM32_LR
        SRBM_ARM32_PC, // REG_ARM32_PC
        SRBM_ARM32_F0, // REG_ARM32_F0
        SRBM_ARM32_F1, // REG_ARM32_F1
        SRBM_ARM32_F2, // REG_ARM32_F2
        SRBM_ARM32_F3, // REG_ARM32_F3
        SRBM_ARM32_F4, // REG_ARM32_F4
        SRBM_ARM32_F5, // REG_ARM32_F5
        SRBM_ARM32_F6, // REG_ARM32_F6
        SRBM_ARM32_F7, // REG_ARM32_F7
        SRBM_ARM32_F8, // REG_ARM32_F8
        SRBM_ARM32_F9, // REG_ARM32_F9
        SRBM_ARM32_F10, // REG_ARM32_F10
        SRBM_ARM32_F11, // REG_ARM32_F11
        SRBM_ARM32_F12, // REG_ARM32_F12
        SRBM_ARM32_F13, // REG_ARM32_F13
        SRBM_ARM32_F14, // REG_ARM32_F14
        SRBM_ARM32_F15, // REG_ARM32_F15
        SRBM_ARM32_F16, // REG_ARM32_F16
        SRBM_ARM32_F17, // REG_ARM32_F17
        SRBM_ARM32_F18, // REG_ARM32_F18
        SRBM_ARM32_F19, // REG_ARM32_F19
        SRBM_ARM32_F20, // REG_ARM32_F20
        SRBM_ARM32_F21, // REG_ARM32_F21
        SRBM_ARM32_F22, // REG_ARM32_F22
        SRBM_ARM32_F23, // REG_ARM32_F23
        SRBM_ARM32_F24, // REG_ARM32_F24
        SRBM_ARM32_F25, // REG_ARM32_F25
        SRBM_ARM32_F26, // REG_ARM32_F26
        SRBM_ARM32_F27, // REG_ARM32_F27
        SRBM_ARM32_F28, // REG_ARM32_F28
        SRBM_ARM32_F29, // REG_ARM32_F29
        SRBM_ARM32_F30, // REG_ARM32_F30
        SRBM_ARM32_F31, // REG_ARM32_F31
        SRBM_STK, // REG_ARM32_STK
    ];

    private static ReadOnlySpan<regMask> s_masks_ARM64 => [
        SRBM_ARM64_R0, // REG_ARM64_R0
        SRBM_ARM64_R1, // REG_ARM64_R1
        SRBM_ARM64_R2, // REG_ARM64_R2
        SRBM_ARM64_R3, // REG_ARM64_R3
        SRBM_ARM64_R4, // REG_ARM64_R4
        SRBM_ARM64_R5, // REG_ARM64_R5
        SRBM_ARM64_R6, // REG_ARM64_R6
        SRBM_ARM64_R7, // REG_ARM64_R7
        SRBM_ARM64_R8, // REG_ARM64_R8
        SRBM_ARM64_R9, // REG_ARM64_R9
        SRBM_ARM64_R10, // REG_ARM64_R10
        SRBM_ARM64_R11, // REG_ARM64_R11
        SRBM_ARM64_R12, // REG_ARM64_R12
        SRBM_ARM64_R13, // REG_ARM64_R13
        SRBM_ARM64_R14, // REG_ARM64_R14
        SRBM_ARM64_R15, // REG_ARM64_R15
        SRBM_ARM64_IP0, // REG_ARM64_IP0
        SRBM_ARM64_IP1, // REG_ARM64_IP1
        SRBM_ARM64_PR, // REG_ARM64_PR
        SRBM_ARM64_R19, // REG_ARM64_R19
        SRBM_ARM64_R20, // REG_ARM64_R20
        SRBM_ARM64_R21, // REG_ARM64_R21
        SRBM_ARM64_R22, // REG_ARM64_R22
        SRBM_ARM64_R23, // REG_ARM64_R23
        SRBM_ARM64_R24, // REG_ARM64_R24
        SRBM_ARM64_R25, // REG_ARM64_R25
        SRBM_ARM64_R26, // REG_ARM64_R26
        SRBM_ARM64_R27, // REG_ARM64_R27
        SRBM_ARM64_R28, // REG_ARM64_R28
        SRBM_ARM64_FP, // REG_ARM64_FP
        SRBM_ARM64_LR, // REG_ARM64_LR
        SRBM_ARM64_ZR, // REG_ARM64_ZR
        SRBM_ARM64_V0, // REG_ARM64_V0
        SRBM_ARM64_V1, // REG_ARM64_V1
        SRBM_ARM64_V2, // REG_ARM64_V2
        SRBM_ARM64_V3, // REG_ARM64_V3
        SRBM_ARM64_V4, // REG_ARM64_V4
        SRBM_ARM64_V5, // REG_ARM64_V5
        SRBM_ARM64_V6, // REG_ARM64_V6
        SRBM_ARM64_V7, // REG_ARM64_V7
        SRBM_ARM64_V8, // REG_ARM64_V8
        SRBM_ARM64_V9, // REG_ARM64_V9
        SRBM_ARM64_V10, // REG_ARM64_V10
        SRBM_ARM64_V11, // REG_ARM64_V11
        SRBM_ARM64_V12, // REG_ARM64_V12
        SRBM_ARM64_V13, // REG_ARM64_V13
        SRBM_ARM64_V14, // REG_ARM64_V14
        SRBM_ARM64_V15, // REG_ARM64_V15
        SRBM_ARM64_V16, // REG_ARM64_V16
        SRBM_ARM64_V17, // REG_ARM64_V17
        SRBM_ARM64_V18, // REG_ARM64_V18
        SRBM_ARM64_V19, // REG_ARM64_V19
        SRBM_ARM64_V20, // REG_ARM64_V20
        SRBM_ARM64_V21, // REG_ARM64_V21
        SRBM_ARM64_V22, // REG_ARM64_V22
        SRBM_ARM64_V23, // REG_ARM64_V23
        SRBM_ARM64_V24, // REG_ARM64_V24
        SRBM_ARM64_V25, // REG_ARM64_V25
        SRBM_ARM64_V26, // REG_ARM64_V26
        SRBM_ARM64_V27, // REG_ARM64_V27
        SRBM_ARM64_V28, // REG_ARM64_V28
        SRBM_ARM64_V29, // REG_ARM64_V29
        SRBM_ARM64_V30, // REG_ARM64_V30
        SRBM_ARM64_V31, // REG_ARM64_V31
        SRBM_ARM64_P0, // REG_ARM64_P0
        SRBM_ARM64_P1, // REG_ARM64_P1
        SRBM_ARM64_P2, // REG_ARM64_P2
        SRBM_ARM64_P3, // REG_ARM64_P3
        SRBM_ARM64_P4, // REG_ARM64_P4
        SRBM_ARM64_P5, // REG_ARM64_P5
        SRBM_ARM64_P6, // REG_ARM64_P6
        SRBM_ARM64_P7, // REG_ARM64_P7
        SRBM_ARM64_P8, // REG_ARM64_P8
        SRBM_ARM64_P9, // REG_ARM64_P9
        SRBM_ARM64_P10, // REG_ARM64_P10
        SRBM_ARM64_P11, // REG_ARM64_P11
        SRBM_ARM64_P12, // REG_ARM64_P12
        SRBM_ARM64_P13, // REG_ARM64_P13
        SRBM_ARM64_P14, // REG_ARM64_P14
        SRBM_ARM64_P15, // REG_ARM64_P15
        SRBM_ARM64_SP, // REG_ARM64_SP
        SRBM_ARM64_FFR, // REG_ARM64_FFR
        SRBM_STK, // REG_ARM64_STK
    ];

    private static ReadOnlySpan<regMask> s_masks_LOONGARCH64 => [
        SRBM_LOONGARCH64_R0, // REG_LOONGARCH64_R0
        SRBM_LOONGARCH64_RA, // REG_LOONGARCH64_RA
        SRBM_LOONGARCH64_TP, // REG_LOONGARCH64_TP
        SRBM_LOONGARCH64_SP, // REG_LOONGARCH64_SP
        SRBM_LOONGARCH64_A0, // REG_LOONGARCH64_A0
        SRBM_LOONGARCH64_A1, // REG_LOONGARCH64_A1
        SRBM_LOONGARCH64_A2, // REG_LOONGARCH64_A2
        SRBM_LOONGARCH64_A3, // REG_LOONGARCH64_A3
        SRBM_LOONGARCH64_A4, // REG_LOONGARCH64_A4
        SRBM_LOONGARCH64_A5, // REG_LOONGARCH64_A5
        SRBM_LOONGARCH64_A6, // REG_LOONGARCH64_A6
        SRBM_LOONGARCH64_A7, // REG_LOONGARCH64_A7
        SRBM_LOONGARCH64_T0, // REG_LOONGARCH64_T0
        SRBM_LOONGARCH64_T1, // REG_LOONGARCH64_T1
        SRBM_LOONGARCH64_T2, // REG_LOONGARCH64_T2
        SRBM_LOONGARCH64_T3, // REG_LOONGARCH64_T3
        SRBM_LOONGARCH64_T4, // REG_LOONGARCH64_T4
        SRBM_LOONGARCH64_T5, // REG_LOONGARCH64_T5
        SRBM_LOONGARCH64_T6, // REG_LOONGARCH64_T6
        SRBM_LOONGARCH64_T7, // REG_LOONGARCH64_T7
        SRBM_LOONGARCH64_T8, // REG_LOONGARCH64_T8
        SRBM_LOONGARCH64_X0, // REG_LOONGARCH64_X0
        SRBM_LOONGARCH64_FP, // REG_LOONGARCH64_FP
        SRBM_LOONGARCH64_S0, // REG_LOONGARCH64_S0
        SRBM_LOONGARCH64_S1, // REG_LOONGARCH64_S1
        SRBM_LOONGARCH64_S2, // REG_LOONGARCH64_S2
        SRBM_LOONGARCH64_S3, // REG_LOONGARCH64_S3
        SRBM_LOONGARCH64_S4, // REG_LOONGARCH64_S4
        SRBM_LOONGARCH64_S5, // REG_LOONGARCH64_S5
        SRBM_LOONGARCH64_S6, // REG_LOONGARCH64_S6
        SRBM_LOONGARCH64_S7, // REG_LOONGARCH64_S7
        SRBM_LOONGARCH64_S8, // REG_LOONGARCH64_S8
        SRBM_LOONGARCH64_F0, // REG_LOONGARCH64_F0
        SRBM_LOONGARCH64_F1, // REG_LOONGARCH64_F1
        SRBM_LOONGARCH64_F2, // REG_LOONGARCH64_F2
        SRBM_LOONGARCH64_F3, // REG_LOONGARCH64_F3
        SRBM_LOONGARCH64_F4, // REG_LOONGARCH64_F4
        SRBM_LOONGARCH64_F5, // REG_LOONGARCH64_F5
        SRBM_LOONGARCH64_F6, // REG_LOONGARCH64_F6
        SRBM_LOONGARCH64_F7, // REG_LOONGARCH64_F7
        SRBM_LOONGARCH64_F8, // REG_LOONGARCH64_F8
        SRBM_LOONGARCH64_F9, // REG_LOONGARCH64_F9
        SRBM_LOONGARCH64_F10, // REG_LOONGARCH64_F10
        SRBM_LOONGARCH64_F11, // REG_LOONGARCH64_F11
        SRBM_LOONGARCH64_F12, // REG_LOONGARCH64_F12
        SRBM_LOONGARCH64_F13, // REG_LOONGARCH64_F13
        SRBM_LOONGARCH64_F14, // REG_LOONGARCH64_F14
        SRBM_LOONGARCH64_F15, // REG_LOONGARCH64_F15
        SRBM_LOONGARCH64_F16, // REG_LOONGARCH64_F16
        SRBM_LOONGARCH64_F17, // REG_LOONGARCH64_F17
        SRBM_LOONGARCH64_F18, // REG_LOONGARCH64_F18
        SRBM_LOONGARCH64_F19, // REG_LOONGARCH64_F19
        SRBM_LOONGARCH64_F20, // REG_LOONGARCH64_F20
        SRBM_LOONGARCH64_F21, // REG_LOONGARCH64_F21
        SRBM_LOONGARCH64_F22, // REG_LOONGARCH64_F22
        SRBM_LOONGARCH64_F23, // REG_LOONGARCH64_F23
        SRBM_LOONGARCH64_F24, // REG_LOONGARCH64_F24
        SRBM_LOONGARCH64_F25, // REG_LOONGARCH64_F25
        SRBM_LOONGARCH64_F26, // REG_LOONGARCH64_F26
        SRBM_LOONGARCH64_F27, // REG_LOONGARCH64_F27
        SRBM_LOONGARCH64_F28, // REG_LOONGARCH64_F28
        SRBM_LOONGARCH64_F29, // REG_LOONGARCH64_F29
        SRBM_LOONGARCH64_F30, // REG_LOONGARCH64_F30
        SRBM_LOONGARCH64_F31, // REG_LOONGARCH64_F31
        SRBM_STK, // REG_LOONGARCH64_STK
    ];

    private static ReadOnlySpan<regMask> s_masks_RISCV64 => [
        SRBM_RISCV64_R0, // REG_RISCV64_R0
        SRBM_RISCV64_RA, // REG_RISCV64_RA
        SRBM_RISCV64_SP, // REG_RISCV64_SP
        SRBM_RISCV64_GP, // REG_RISCV64_GP
        SRBM_RISCV64_TP, // REG_RISCV64_TP
        SRBM_RISCV64_T0, // REG_RISCV64_T0
        SRBM_RISCV64_T1, // REG_RISCV64_T1
        SRBM_RISCV64_T2, // REG_RISCV64_T2
        SRBM_RISCV64_FP, // REG_RISCV64_FP
        SRBM_RISCV64_S1, // REG_RISCV64_S1
        SRBM_RISCV64_A0, // REG_RISCV64_A0
        SRBM_RISCV64_A1, // REG_RISCV64_A1
        SRBM_RISCV64_A2, // REG_RISCV64_A2
        SRBM_RISCV64_A3, // REG_RISCV64_A3
        SRBM_RISCV64_A4, // REG_RISCV64_A4
        SRBM_RISCV64_A5, // REG_RISCV64_A5
        SRBM_RISCV64_A6, // REG_RISCV64_A6
        SRBM_RISCV64_A7, // REG_RISCV64_A7
        SRBM_RISCV64_S2, // REG_RISCV64_S2
        SRBM_RISCV64_S3, // REG_RISCV64_S3
        SRBM_RISCV64_S4, // REG_RISCV64_S4
        SRBM_RISCV64_S5, // REG_RISCV64_S5
        SRBM_RISCV64_S6, // REG_RISCV64_S6
        SRBM_RISCV64_S7, // REG_RISCV64_S7
        SRBM_RISCV64_S8, // REG_RISCV64_S8
        SRBM_RISCV64_S9, // REG_RISCV64_S9
        SRBM_RISCV64_S10, // REG_RISCV64_S10
        SRBM_RISCV64_S11, // REG_RISCV64_S11
        SRBM_RISCV64_T3, // REG_RISCV64_T3
        SRBM_RISCV64_T4, // REG_RISCV64_T4
        SRBM_RISCV64_T5, // REG_RISCV64_T5
        SRBM_RISCV64_T6, // REG_RISCV64_T6
        SRBM_RISCV64_FT0, // REG_RISCV64_FT0
        SRBM_RISCV64_FT1, // REG_RISCV64_FT1
        SRBM_RISCV64_FT2, // REG_RISCV64_FT2
        SRBM_RISCV64_FT3, // REG_RISCV64_FT3
        SRBM_RISCV64_FT4, // REG_RISCV64_FT4
        SRBM_RISCV64_FT5, // REG_RISCV64_FT5
        SRBM_RISCV64_FT6, // REG_RISCV64_FT6
        SRBM_RISCV64_FT7, // REG_RISCV64_FT7
        SRBM_RISCV64_FS0, // REG_RISCV64_FS0
        SRBM_RISCV64_FS1, // REG_RISCV64_FS1
        SRBM_RISCV64_FA0, // REG_RISCV64_FA0
        SRBM_RISCV64_FA1, // REG_RISCV64_FA1
        SRBM_RISCV64_FA2, // REG_RISCV64_FA2
        SRBM_RISCV64_FA3, // REG_RISCV64_FA3
        SRBM_RISCV64_FA4, // REG_RISCV64_FA4
        SRBM_RISCV64_FA5, // REG_RISCV64_FA5
        SRBM_RISCV64_FA6, // REG_RISCV64_FA6
        SRBM_RISCV64_FA7, // REG_RISCV64_FA7
        SRBM_RISCV64_FS2, // REG_RISCV64_FS2
        SRBM_RISCV64_FS3, // REG_RISCV64_FS3
        SRBM_RISCV64_FS4, // REG_RISCV64_FS4
        SRBM_RISCV64_FS5, // REG_RISCV64_FS5
        SRBM_RISCV64_FS6, // REG_RISCV64_FS6
        SRBM_RISCV64_FS7, // REG_RISCV64_FS7
        SRBM_RISCV64_FS8, // REG_RISCV64_FS8
        SRBM_RISCV64_FS9, // REG_RISCV64_FS9
        SRBM_RISCV64_FS10, // REG_RISCV64_FS10
        SRBM_RISCV64_FS11, // REG_RISCV64_FS11
        SRBM_RISCV64_FT8, // REG_RISCV64_FT8
        SRBM_RISCV64_FT9, // REG_RISCV64_FT9
        SRBM_RISCV64_FT10, // REG_RISCV64_FT10
        SRBM_RISCV64_FT11, // REG_RISCV64_FT11
        SRBM_STK, // REG_RISCV64_STK
    ];

    private static ReadOnlySpan<regMask> s_masks_WASM => [
        SRBM_NONE, // unused register ordinal 0
        SRBM_STK, // REG_WASM_STK
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
                return s_masks_X64;
            }
            if (Configuration.TARGET_ARM)
            {
                return s_masks_ARM32;
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