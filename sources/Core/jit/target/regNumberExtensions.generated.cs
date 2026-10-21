// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    private static string GetNameX86(regNumber regNum)
    {
        switch (regNum)
        {
            case REG_X86_EAX:
            {
                return "eax";
            }
            case REG_X86_ECX:
            {
                return "ecx";
            }
            case REG_X86_EDX:
            {
                return "edx";
            }
            case REG_X86_EBX:
            {
                return "ebx";
            }
            case REG_X86_ESP:
            {
                return "esp";
            }
            case REG_X86_EBP:
            {
                return "ebp";
            }
            case REG_X86_ESI:
            {
                return "esi";
            }
            case REG_X86_EDI:
            {
                return "edi";
            }
            case REG_X86_XMM0:
            {
                return "mm0";
            }
            case REG_X86_XMM1:
            {
                return "mm1";
            }
            case REG_X86_XMM2:
            {
                return "mm2";
            }
            case REG_X86_XMM3:
            {
                return "mm3";
            }
            case REG_X86_XMM4:
            {
                return "mm4";
            }
            case REG_X86_XMM5:
            {
                return "mm5";
            }
            case REG_X86_XMM6:
            {
                return "mm6";
            }
            case REG_X86_XMM7:
            {
                return "mm7";
            }
            case REG_X86_K0:
            {
                return "k0";
            }
            case REG_X86_K1:
            {
                return "k1";
            }
            case REG_X86_K2:
            {
                return "k2";
            }
            case REG_X86_K3:
            {
                return "k3";
            }
            case REG_X86_K4:
            {
                return "k4";
            }
            case REG_X86_K5:
            {
                return "k5";
            }
            case REG_X86_K6:
            {
                return "k6";
            }
            case REG_X86_K7:
            {
                return "k7";
            }
            case REG_X86_STK:
            {
                return "STK";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage445
                );
                return default!;
            }
        }
    }

    private static string GetNameX64(regNumber regNum)
    {
        switch (regNum)
        {
            case REG_X64_RAX:
            {
                return "rax";
            }
            case REG_X64_RCX:
            {
                return "rcx";
            }
            case REG_X64_RDX:
            {
                return "rdx";
            }
            case REG_X64_RBX:
            {
                return "rbx";
            }
            case REG_X64_RSP:
            {
                return "rsp";
            }
            case REG_X64_RBP:
            {
                return "rbp";
            }
            case REG_X64_RSI:
            {
                return "rsi";
            }
            case REG_X64_RDI:
            {
                return "rdi";
            }
            case REG_X64_R8:
            {
                return "r8";
            }
            case REG_X64_R9:
            {
                return "r9";
            }
            case REG_X64_R10:
            {
                return "r10";
            }
            case REG_X64_R11:
            {
                return "r11";
            }
            case REG_X64_R12:
            {
                return "r12";
            }
            case REG_X64_R13:
            {
                return "r13";
            }
            case REG_X64_R14:
            {
                return "r14";
            }
            case REG_X64_R15:
            {
                return "r15";
            }
            case REG_X64_R16:
            {
                return "r16";
            }
            case REG_X64_R17:
            {
                return "r17";
            }
            case REG_X64_R18:
            {
                return "r18";
            }
            case REG_X64_R19:
            {
                return "r19";
            }
            case REG_X64_R20:
            {
                return "r20";
            }
            case REG_X64_R21:
            {
                return "r21";
            }
            case REG_X64_R22:
            {
                return "r22";
            }
            case REG_X64_R23:
            {
                return "r23";
            }
            case REG_X64_R24:
            {
                return "r24";
            }
            case REG_X64_R25:
            {
                return "r25";
            }
            case REG_X64_R26:
            {
                return "r26";
            }
            case REG_X64_R27:
            {
                return "r27";
            }
            case REG_X64_R28:
            {
                return "r28";
            }
            case REG_X64_R29:
            {
                return "r29";
            }
            case REG_X64_R30:
            {
                return "r30";
            }
            case REG_X64_R31:
            {
                return "r31";
            }
            case REG_X64_XMM0:
            {
                return "mm0";
            }
            case REG_X64_XMM1:
            {
                return "mm1";
            }
            case REG_X64_XMM2:
            {
                return "mm2";
            }
            case REG_X64_XMM3:
            {
                return "mm3";
            }
            case REG_X64_XMM4:
            {
                return "mm4";
            }
            case REG_X64_XMM5:
            {
                return "mm5";
            }
            case REG_X64_XMM6:
            {
                return "mm6";
            }
            case REG_X64_XMM7:
            {
                return "mm7";
            }
            case REG_X64_XMM8:
            {
                return "mm8";
            }
            case REG_X64_XMM9:
            {
                return "mm9";
            }
            case REG_X64_XMM10:
            {
                return "mm10";
            }
            case REG_X64_XMM11:
            {
                return "mm11";
            }
            case REG_X64_XMM12:
            {
                return "mm12";
            }
            case REG_X64_XMM13:
            {
                return "mm13";
            }
            case REG_X64_XMM14:
            {
                return "mm14";
            }
            case REG_X64_XMM15:
            {
                return "mm15";
            }
            case REG_X64_XMM16:
            {
                return "mm16";
            }
            case REG_X64_XMM17:
            {
                return "mm17";
            }
            case REG_X64_XMM18:
            {
                return "mm18";
            }
            case REG_X64_XMM19:
            {
                return "mm19";
            }
            case REG_X64_XMM20:
            {
                return "mm20";
            }
            case REG_X64_XMM21:
            {
                return "mm21";
            }
            case REG_X64_XMM22:
            {
                return "mm22";
            }
            case REG_X64_XMM23:
            {
                return "mm23";
            }
            case REG_X64_XMM24:
            {
                return "mm24";
            }
            case REG_X64_XMM25:
            {
                return "mm25";
            }
            case REG_X64_XMM26:
            {
                return "mm26";
            }
            case REG_X64_XMM27:
            {
                return "mm27";
            }
            case REG_X64_XMM28:
            {
                return "mm28";
            }
            case REG_X64_XMM29:
            {
                return "mm29";
            }
            case REG_X64_XMM30:
            {
                return "mm30";
            }
            case REG_X64_XMM31:
            {
                return "mm31";
            }
            case REG_X64_K0:
            {
                return "k0";
            }
            case REG_X64_K1:
            {
                return "k1";
            }
            case REG_X64_K2:
            {
                return "k2";
            }
            case REG_X64_K3:
            {
                return "k3";
            }
            case REG_X64_K4:
            {
                return "k4";
            }
            case REG_X64_K5:
            {
                return "k5";
            }
            case REG_X64_K6:
            {
                return "k6";
            }
            case REG_X64_K7:
            {
                return "k7";
            }
            case REG_X64_STK:
            {
                return "STK";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage445
                );
                return default!;
            }
        }
    }

    private static string GetNameARM32(regNumber regNum)
    {
        switch (regNum)
        {
            case REG_ARM32_R0:
            {
                return "r0";
            }
            case REG_ARM32_R1:
            {
                return "r1";
            }
            case REG_ARM32_R2:
            {
                return "r2";
            }
            case REG_ARM32_R3:
            {
                return "r3";
            }
            case REG_ARM32_R4:
            {
                return "r4";
            }
            case REG_ARM32_R5:
            {
                return "r5";
            }
            case REG_ARM32_R6:
            {
                return "r6";
            }
            case REG_ARM32_R7:
            {
                return "r7";
            }
            case REG_ARM32_R8:
            {
                return "r8";
            }
            case REG_ARM32_R9:
            {
                return "r9";
            }
            case REG_ARM32_R10:
            {
                return "r10";
            }
            case REG_ARM32_R11:
            {
                return "r11";
            }
            case REG_ARM32_R12:
            {
                return "r12";
            }
            case REG_ARM32_SP:
            {
                return "sp";
            }
            case REG_ARM32_LR:
            {
                return "lr";
            }
            case REG_ARM32_PC:
            {
                return "pc";
            }
            case REG_ARM32_F0:
            {
                return "f0";
            }
            case REG_ARM32_F1:
            {
                return "f1";
            }
            case REG_ARM32_F2:
            {
                return "f2";
            }
            case REG_ARM32_F3:
            {
                return "f3";
            }
            case REG_ARM32_F4:
            {
                return "f4";
            }
            case REG_ARM32_F5:
            {
                return "f5";
            }
            case REG_ARM32_F6:
            {
                return "f6";
            }
            case REG_ARM32_F7:
            {
                return "f7";
            }
            case REG_ARM32_F8:
            {
                return "f8";
            }
            case REG_ARM32_F9:
            {
                return "f9";
            }
            case REG_ARM32_F10:
            {
                return "f10";
            }
            case REG_ARM32_F11:
            {
                return "f11";
            }
            case REG_ARM32_F12:
            {
                return "f12";
            }
            case REG_ARM32_F13:
            {
                return "f13";
            }
            case REG_ARM32_F14:
            {
                return "f14";
            }
            case REG_ARM32_F15:
            {
                return "f15";
            }
            case REG_ARM32_F16:
            {
                return "f16";
            }
            case REG_ARM32_F17:
            {
                return "f17";
            }
            case REG_ARM32_F18:
            {
                return "f18";
            }
            case REG_ARM32_F19:
            {
                return "f19";
            }
            case REG_ARM32_F20:
            {
                return "f20";
            }
            case REG_ARM32_F21:
            {
                return "f21";
            }
            case REG_ARM32_F22:
            {
                return "f22";
            }
            case REG_ARM32_F23:
            {
                return "f23";
            }
            case REG_ARM32_F24:
            {
                return "f24";
            }
            case REG_ARM32_F25:
            {
                return "f25";
            }
            case REG_ARM32_F26:
            {
                return "f26";
            }
            case REG_ARM32_F27:
            {
                return "f27";
            }
            case REG_ARM32_F28:
            {
                return "f28";
            }
            case REG_ARM32_F29:
            {
                return "f29";
            }
            case REG_ARM32_F30:
            {
                return "f30";
            }
            case REG_ARM32_F31:
            {
                return "f31";
            }
            case REG_ARM32_STK:
            {
                return "STK";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage445
                );
                return default!;
            }
        }
    }

    private static string GetNameARM64(regNumber regNum)
    {
        switch (regNum)
        {
            case REG_ARM64_R0:
            {
                return "x0";
            }
            case REG_ARM64_R1:
            {
                return "x1";
            }
            case REG_ARM64_R2:
            {
                return "x2";
            }
            case REG_ARM64_R3:
            {
                return "x3";
            }
            case REG_ARM64_R4:
            {
                return "x4";
            }
            case REG_ARM64_R5:
            {
                return "x5";
            }
            case REG_ARM64_R6:
            {
                return "x6";
            }
            case REG_ARM64_R7:
            {
                return "x7";
            }
            case REG_ARM64_R8:
            {
                return "x8";
            }
            case REG_ARM64_R9:
            {
                return "x9";
            }
            case REG_ARM64_R10:
            {
                return "x10";
            }
            case REG_ARM64_R11:
            {
                return "x11";
            }
            case REG_ARM64_R12:
            {
                return "x12";
            }
            case REG_ARM64_R13:
            {
                return "x13";
            }
            case REG_ARM64_R14:
            {
                return "x14";
            }
            case REG_ARM64_R15:
            {
                return "x15";
            }
            case REG_ARM64_IP0:
            {
                return "xip0";
            }
            case REG_ARM64_IP1:
            {
                return "xip1";
            }
            case REG_ARM64_PR:
            {
                return "xpr";
            }
            case REG_ARM64_R19:
            {
                return "x19";
            }
            case REG_ARM64_R20:
            {
                return "x20";
            }
            case REG_ARM64_R21:
            {
                return "x21";
            }
            case REG_ARM64_R22:
            {
                return "x22";
            }
            case REG_ARM64_R23:
            {
                return "x23";
            }
            case REG_ARM64_R24:
            {
                return "x24";
            }
            case REG_ARM64_R25:
            {
                return "x25";
            }
            case REG_ARM64_R26:
            {
                return "x26";
            }
            case REG_ARM64_R27:
            {
                return "x27";
            }
            case REG_ARM64_R28:
            {
                return "x28";
            }
            case REG_ARM64_FP:
            {
                return "fp";
            }
            case REG_ARM64_LR:
            {
                return "lr";
            }
            case REG_ARM64_ZR:
            {
                return "xzr";
            }
            case REG_ARM64_V0:
            {
                return "d0";
            }
            case REG_ARM64_V1:
            {
                return "d1";
            }
            case REG_ARM64_V2:
            {
                return "d2";
            }
            case REG_ARM64_V3:
            {
                return "d3";
            }
            case REG_ARM64_V4:
            {
                return "d4";
            }
            case REG_ARM64_V5:
            {
                return "d5";
            }
            case REG_ARM64_V6:
            {
                return "d6";
            }
            case REG_ARM64_V7:
            {
                return "d7";
            }
            case REG_ARM64_V8:
            {
                return "d8";
            }
            case REG_ARM64_V9:
            {
                return "d9";
            }
            case REG_ARM64_V10:
            {
                return "d10";
            }
            case REG_ARM64_V11:
            {
                return "d11";
            }
            case REG_ARM64_V12:
            {
                return "d12";
            }
            case REG_ARM64_V13:
            {
                return "d13";
            }
            case REG_ARM64_V14:
            {
                return "d14";
            }
            case REG_ARM64_V15:
            {
                return "d15";
            }
            case REG_ARM64_V16:
            {
                return "d16";
            }
            case REG_ARM64_V17:
            {
                return "d17";
            }
            case REG_ARM64_V18:
            {
                return "d18";
            }
            case REG_ARM64_V19:
            {
                return "d19";
            }
            case REG_ARM64_V20:
            {
                return "d20";
            }
            case REG_ARM64_V21:
            {
                return "d21";
            }
            case REG_ARM64_V22:
            {
                return "d22";
            }
            case REG_ARM64_V23:
            {
                return "d23";
            }
            case REG_ARM64_V24:
            {
                return "d24";
            }
            case REG_ARM64_V25:
            {
                return "d25";
            }
            case REG_ARM64_V26:
            {
                return "d26";
            }
            case REG_ARM64_V27:
            {
                return "d27";
            }
            case REG_ARM64_V28:
            {
                return "d28";
            }
            case REG_ARM64_V29:
            {
                return "d29";
            }
            case REG_ARM64_V30:
            {
                return "d30";
            }
            case REG_ARM64_V31:
            {
                return "d31";
            }
            case REG_ARM64_P0:
            {
                return "p0";
            }
            case REG_ARM64_P1:
            {
                return "p1";
            }
            case REG_ARM64_P2:
            {
                return "p2";
            }
            case REG_ARM64_P3:
            {
                return "p3";
            }
            case REG_ARM64_P4:
            {
                return "p4";
            }
            case REG_ARM64_P5:
            {
                return "p5";
            }
            case REG_ARM64_P6:
            {
                return "p6";
            }
            case REG_ARM64_P7:
            {
                return "p7";
            }
            case REG_ARM64_P8:
            {
                return "p8";
            }
            case REG_ARM64_P9:
            {
                return "p9";
            }
            case REG_ARM64_P10:
            {
                return "p10";
            }
            case REG_ARM64_P11:
            {
                return "p11";
            }
            case REG_ARM64_P12:
            {
                return "p12";
            }
            case REG_ARM64_P13:
            {
                return "p13";
            }
            case REG_ARM64_P14:
            {
                return "p14";
            }
            case REG_ARM64_P15:
            {
                return "p15";
            }
            case REG_ARM64_SP:
            {
                return "sp";
            }
            case REG_ARM64_FFR:
            {
                return "ffr";
            }
            case REG_ARM64_STK:
            {
                return "STK";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage445
                );
                return default!;
            }
        }
    }

    private static string GetNameLOONGARCH64(regNumber regNum)
    {
        switch (regNum)
        {
            case REG_LOONGARCH64_R0:
            {
                return "zero";
            }
            case REG_LOONGARCH64_RA:
            {
                return "ra";
            }
            case REG_LOONGARCH64_TP:
            {
                return "tp";
            }
            case REG_LOONGARCH64_SP:
            {
                return "sp";
            }
            case REG_LOONGARCH64_A0:
            {
                return "a0";
            }
            case REG_LOONGARCH64_A1:
            {
                return "a1";
            }
            case REG_LOONGARCH64_A2:
            {
                return "a2";
            }
            case REG_LOONGARCH64_A3:
            {
                return "a3";
            }
            case REG_LOONGARCH64_A4:
            {
                return "a4";
            }
            case REG_LOONGARCH64_A5:
            {
                return "a5";
            }
            case REG_LOONGARCH64_A6:
            {
                return "a6";
            }
            case REG_LOONGARCH64_A7:
            {
                return "a7";
            }
            case REG_LOONGARCH64_T0:
            {
                return "t0";
            }
            case REG_LOONGARCH64_T1:
            {
                return "t1";
            }
            case REG_LOONGARCH64_T2:
            {
                return "t2";
            }
            case REG_LOONGARCH64_T3:
            {
                return "t3";
            }
            case REG_LOONGARCH64_T4:
            {
                return "t4";
            }
            case REG_LOONGARCH64_T5:
            {
                return "t5";
            }
            case REG_LOONGARCH64_T6:
            {
                return "t6";
            }
            case REG_LOONGARCH64_T7:
            {
                return "t7";
            }
            case REG_LOONGARCH64_T8:
            {
                return "t8";
            }
            case REG_LOONGARCH64_X0:
            {
                return "x0";
            }
            case REG_LOONGARCH64_FP:
            {
                return "fp";
            }
            case REG_LOONGARCH64_S0:
            {
                return "s0";
            }
            case REG_LOONGARCH64_S1:
            {
                return "s1";
            }
            case REG_LOONGARCH64_S2:
            {
                return "s2";
            }
            case REG_LOONGARCH64_S3:
            {
                return "s3";
            }
            case REG_LOONGARCH64_S4:
            {
                return "s4";
            }
            case REG_LOONGARCH64_S5:
            {
                return "s5";
            }
            case REG_LOONGARCH64_S6:
            {
                return "s6";
            }
            case REG_LOONGARCH64_S7:
            {
                return "s7";
            }
            case REG_LOONGARCH64_S8:
            {
                return "s8";
            }
            case REG_LOONGARCH64_F0:
            {
                return "f0";
            }
            case REG_LOONGARCH64_F1:
            {
                return "f1";
            }
            case REG_LOONGARCH64_F2:
            {
                return "f2";
            }
            case REG_LOONGARCH64_F3:
            {
                return "f3";
            }
            case REG_LOONGARCH64_F4:
            {
                return "f4";
            }
            case REG_LOONGARCH64_F5:
            {
                return "f5";
            }
            case REG_LOONGARCH64_F6:
            {
                return "f6";
            }
            case REG_LOONGARCH64_F7:
            {
                return "f7";
            }
            case REG_LOONGARCH64_F8:
            {
                return "f8";
            }
            case REG_LOONGARCH64_F9:
            {
                return "f9";
            }
            case REG_LOONGARCH64_F10:
            {
                return "f10";
            }
            case REG_LOONGARCH64_F11:
            {
                return "f11";
            }
            case REG_LOONGARCH64_F12:
            {
                return "f12";
            }
            case REG_LOONGARCH64_F13:
            {
                return "f13";
            }
            case REG_LOONGARCH64_F14:
            {
                return "f14";
            }
            case REG_LOONGARCH64_F15:
            {
                return "f15";
            }
            case REG_LOONGARCH64_F16:
            {
                return "f16";
            }
            case REG_LOONGARCH64_F17:
            {
                return "f17";
            }
            case REG_LOONGARCH64_F18:
            {
                return "f18";
            }
            case REG_LOONGARCH64_F19:
            {
                return "f19";
            }
            case REG_LOONGARCH64_F20:
            {
                return "f20";
            }
            case REG_LOONGARCH64_F21:
            {
                return "f21";
            }
            case REG_LOONGARCH64_F22:
            {
                return "f22";
            }
            case REG_LOONGARCH64_F23:
            {
                return "f23";
            }
            case REG_LOONGARCH64_F24:
            {
                return "f24";
            }
            case REG_LOONGARCH64_F25:
            {
                return "f25";
            }
            case REG_LOONGARCH64_F26:
            {
                return "f26";
            }
            case REG_LOONGARCH64_F27:
            {
                return "f27";
            }
            case REG_LOONGARCH64_F28:
            {
                return "f28";
            }
            case REG_LOONGARCH64_F29:
            {
                return "f29";
            }
            case REG_LOONGARCH64_F30:
            {
                return "f30";
            }
            case REG_LOONGARCH64_F31:
            {
                return "f31";
            }
            case REG_LOONGARCH64_STK:
            {
                return "STK";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage445
                );
                return default!;
            }
        }
    }

    private static string GetNameRISCV64(regNumber regNum)
    {
        switch (regNum)
        {
            case REG_RISCV64_R0:
            {
                return "zero";
            }
            case REG_RISCV64_RA:
            {
                return "ra";
            }
            case REG_RISCV64_SP:
            {
                return "sp";
            }
            case REG_RISCV64_GP:
            {
                return "gp";
            }
            case REG_RISCV64_TP:
            {
                return "tp";
            }
            case REG_RISCV64_T0:
            {
                return "t0";
            }
            case REG_RISCV64_T1:
            {
                return "t1";
            }
            case REG_RISCV64_T2:
            {
                return "t2";
            }
            case REG_RISCV64_FP:
            {
                return "fp";
            }
            case REG_RISCV64_S1:
            {
                return "s1";
            }
            case REG_RISCV64_A0:
            {
                return "a0";
            }
            case REG_RISCV64_A1:
            {
                return "a1";
            }
            case REG_RISCV64_A2:
            {
                return "a2";
            }
            case REG_RISCV64_A3:
            {
                return "a3";
            }
            case REG_RISCV64_A4:
            {
                return "a4";
            }
            case REG_RISCV64_A5:
            {
                return "a5";
            }
            case REG_RISCV64_A6:
            {
                return "a6";
            }
            case REG_RISCV64_A7:
            {
                return "a7";
            }
            case REG_RISCV64_S2:
            {
                return "s2";
            }
            case REG_RISCV64_S3:
            {
                return "s3";
            }
            case REG_RISCV64_S4:
            {
                return "s4";
            }
            case REG_RISCV64_S5:
            {
                return "s5";
            }
            case REG_RISCV64_S6:
            {
                return "s6";
            }
            case REG_RISCV64_S7:
            {
                return "s7";
            }
            case REG_RISCV64_S8:
            {
                return "s8";
            }
            case REG_RISCV64_S9:
            {
                return "s9";
            }
            case REG_RISCV64_S10:
            {
                return "s10";
            }
            case REG_RISCV64_S11:
            {
                return "s11";
            }
            case REG_RISCV64_T3:
            {
                return "t3";
            }
            case REG_RISCV64_T4:
            {
                return "t4";
            }
            case REG_RISCV64_T5:
            {
                return "t5";
            }
            case REG_RISCV64_T6:
            {
                return "t6";
            }
            case REG_RISCV64_FT0:
            {
                return "ft0";
            }
            case REG_RISCV64_FT1:
            {
                return "ft1";
            }
            case REG_RISCV64_FT2:
            {
                return "ft2";
            }
            case REG_RISCV64_FT3:
            {
                return "ft3";
            }
            case REG_RISCV64_FT4:
            {
                return "ft4";
            }
            case REG_RISCV64_FT5:
            {
                return "ft5";
            }
            case REG_RISCV64_FT6:
            {
                return "ft6";
            }
            case REG_RISCV64_FT7:
            {
                return "ft7";
            }
            case REG_RISCV64_FS0:
            {
                return "fs0";
            }
            case REG_RISCV64_FS1:
            {
                return "fs1";
            }
            case REG_RISCV64_FA0:
            {
                return "fa0";
            }
            case REG_RISCV64_FA1:
            {
                return "fa1";
            }
            case REG_RISCV64_FA2:
            {
                return "fa2";
            }
            case REG_RISCV64_FA3:
            {
                return "fa3";
            }
            case REG_RISCV64_FA4:
            {
                return "fa4";
            }
            case REG_RISCV64_FA5:
            {
                return "fa5";
            }
            case REG_RISCV64_FA6:
            {
                return "fa6";
            }
            case REG_RISCV64_FA7:
            {
                return "fa7";
            }
            case REG_RISCV64_FS2:
            {
                return "fs2";
            }
            case REG_RISCV64_FS3:
            {
                return "fs3";
            }
            case REG_RISCV64_FS4:
            {
                return "fs4";
            }
            case REG_RISCV64_FS5:
            {
                return "fs5";
            }
            case REG_RISCV64_FS6:
            {
                return "fs6";
            }
            case REG_RISCV64_FS7:
            {
                return "fs7";
            }
            case REG_RISCV64_FS8:
            {
                return "fs8";
            }
            case REG_RISCV64_FS9:
            {
                return "fs9";
            }
            case REG_RISCV64_FS10:
            {
                return "fs10";
            }
            case REG_RISCV64_FS11:
            {
                return "fs11";
            }
            case REG_RISCV64_FT8:
            {
                return "ft8";
            }
            case REG_RISCV64_FT9:
            {
                return "ft9";
            }
            case REG_RISCV64_FT10:
            {
                return "ft10";
            }
            case REG_RISCV64_FT11:
            {
                return "ft11";
            }
            case REG_RISCV64_STK:
            {
                return "STK";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage445
                );
                return default!;
            }
        }
    }



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
            FatalJitException.ThrowFatalJitException(
                "Invalid target architecture.");
            return default;
        }
    }

}