// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class regNumberExtensions
{
#if HAS_FIXED_REGISTER_SET
    private static readonly string[] s_names = [
#if TARGET_X86
        "eax",      // REG_EAX
        "ecx",      // REG_ECX
        "edx",      // REG_EDX
        "ebx",      // REG_EBX
        "esp",      // REG_ESP
        "ebp",      // REG_EBP
        "esi",      // REG_ESI
        "edi",      // REG_EDI

        "mm0",      // REG_XMM0
        "mm1",      // REG_XMM1
        "mm2",      // REG_XMM2
        "mm3",      // REG_XMM3
        "mm4",      // REG_XMM4
        "mm5",      // REG_XMM5
        "mm6",      // REG_XMM6
        "mm7",      // REG_XMM7

        "k0",       // REG_K0
        "k1",       // REG_K1
        "k2",       // REG_K2
        "k3",       // REG_K3
        "k4",       // REG_K4
        "k5",       // REG_K5
        "k6",       // REG_K6
        "k7",       // REG_K7
#elif TARGET_AMD64
        "rax",      // REG_RAX
        "rcx",      // REG_RCX
        "rdx",      // REG_RDX
        "rbx",      // REG_RBX
        "rsp",      // REG_RSP
        "rbp",      // REG_RBP
        "rsi",      // REG_RSI
        "rdi",      // REG_RDI
        "r8",       // REG_R8
        "r9",       // REG_R9
        "r10",      // REG_R10
        "r11",      // REG_R11
        "r12",      // REG_R12
        "r13",      // REG_R13
        "r14",      // REG_R14
        "r15",      // REG_R15
        "r16",      // REG_R16
        "r17",      // REG_R17
        "r18",      // REG_R18
        "r19",      // REG_R19
        "r20",      // REG_R20
        "r21",      // REG_R21
        "r22",      // REG_R22
        "r23",      // REG_R23
        "r24",      // REG_R24
        "r25",      // REG_R25
        "r26",      // REG_R26
        "r27",      // REG_R27
        "r28",      // REG_R28
        "r29",      // REG_R29
        "r30",      // REG_R30
        "r31",      // REG_R31

        "mm0",      // REG_XMM0
        "mm1",      // REG_XMM1
        "mm2",      // REG_XMM2
        "mm3",      // REG_XMM3
        "mm4",      // REG_XMM4
        "mm5",      // REG_XMM5
        "mm6",      // REG_XMM6
        "mm7",      // REG_XMM7
        "mm8",      // REG_XMM8
        "mm9",      // REG_XMM9
        "mm10",     // REG_XMM10
        "mm11",     // REG_XMM11
        "mm12",     // REG_XMM12
        "mm13",     // REG_XMM13
        "mm14",     // REG_XMM14
        "mm15",     // REG_XMM15
        "mm16",     // REG_XMM16
        "mm17",     // REG_XMM17
        "mm18",     // REG_XMM18
        "mm19",     // REG_XMM19
        "mm20",     // REG_XMM20
        "mm21",     // REG_XMM21
        "mm22",     // REG_XMM22
        "mm23",     // REG_XMM23
        "mm24",     // REG_XMM24
        "mm25",     // REG_XMM25
        "mm26",     // REG_XMM26
        "mm27",     // REG_XMM27
        "mm28",     // REG_XMM28
        "mm29",     // REG_XMM29
        "mm30",     // REG_XMM30
        "mm31",     // REG_XMM31

        "k0",       // REG_K0
        "k1",       // REG_K1
        "k2",       // REG_K2
        "k3",       // REG_K3
        "k4",       // REG_K4
        "k5",       // REG_K5
        "k6",       // REG_K6
        "k7",       // REG_K7
#elif TARGET_ARM
        "r0",       // REG_R0
        "r1",       // REG_R1
        "r2",       // REG_R2
        "r3",       // REG_R3
        "r4",       // REG_R4
        "r5",       // REG_R5
        "r6",       // REG_R6
        "r7",       // REG_R7
        "r8",       // REG_R8
        "r9",       // REG_R9
        "r10",      // REG_R10
        "fp",       // REG_R11, REG_FP
        "r12",      // REG_R12
        "sp",       // REG_R13, REG_SP
        "lr",       // REG_R14, REG_LR
        "pc",       // REG_R15, REG_PC

        "f0",       // REG_F0
        "f1",       // REG_F1
        "f2",       // REG_F2
        "f3",       // REG_F3
        "f4",       // REG_F4
        "f5",       // REG_F5
        "f6",       // REG_F6
        "f7",       // REG_F7
        "f8",       // REG_F8
        "f9",       // REG_F9
        "f10",      // REG_F10
        "f11",      // REG_F11
        "f12",      // REG_F12
        "f13",      // REG_F13
        "f14",      // REG_F14
        "f15",      // REG_F15
        "f16",      // REG_F16
        "f17",      // REG_F17
        "f18",      // REG_F18
        "f19",      // REG_F19
        "f20",      // REG_F20
        "f21",      // REG_F21
        "f22",      // REG_F22
        "f23",      // REG_F23
        "f24",      // REG_F24
        "f25",      // REG_F25
        "f26",      // REG_F26
        "f27",      // REG_F27
        "f28",      // REG_F28
        "f29",      // REG_F29
        "f30",      // REG_F30
        "f31",      // REG_F31
#elif TARGET_ARM64
        "x0",       // REG_R0
        "x1",       // REG_R1
        "x2",       // REG_R2
        "x3",       // REG_R3
        "x4",       // REG_R4
        "x5",       // REG_R5
        "x6",       // REG_R6
        "x7",       // REG_R7
        "x8",       // REG_R8
        "x9",       // REG_R9
        "x10",      // REG_R10
        "x11",      // REG_R11
        "x12",      // REG_R12
        "x13",      // REG_R13
        "x14",      // REG_R14
        "x15",      // REG_R15
        "xip0",     // REG_R16, REG_IP0
        "xip1",     // REG_R17, REG_IP1
        "xpr",      // REG_R18, REG_PR
        "x19",      // REG_R19
        "x20",      // REG_R20
        "x21",      // REG_R21
        "x22",      // REG_R22
        "x23",      // REG_R23
        "x24",      // REG_R24
        "x25",      // REG_R25
        "x26",      // REG_R26
        "x27",      // REG_R27
        "x28",      // REG_R28
        "fp",       // REG_R29, REG_FP
        "lr",       // REG_R30, REG_LR
        "xzr",      // REG_R31, REG_ZR

        "d0",       // REG_V0
        "d1",       // REG_V1
        "d2",       // REG_V2
        "d3",       // REG_V3
        "d4",       // REG_V4
        "d5",       // REG_V5
        "d6",       // REG_V6
        "d7",       // REG_V7
        "d8",       // REG_V8
        "d9",       // REG_V9
        "d10",      // REG_V10
        "d11",      // REG_V11
        "d12",      // REG_V12
        "d13",      // REG_V13
        "d14",      // REG_V14
        "d15",      // REG_V15
        "d16",      // REG_V16
        "d17",      // REG_V17
        "d18",      // REG_V18
        "d19",      // REG_V19
        "d20",      // REG_V20
        "d21",      // REG_V21
        "d22",      // REG_V22
        "d23",      // REG_V23
        "d24",      // REG_V24
        "d25",      // REG_V25
        "d26",      // REG_V26
        "d27",      // REG_V27
        "d28",      // REG_V28
        "d29",      // REG_V29
        "d30",      // REG_V30
        "d31",      // REG_V31

        "p0",       // REG_P0
        "p1",       // REG_P1
        "p2",       // REG_P2
        "p3",       // REG_P3
        "p4",       // REG_P4
        "p5",       // REG_P5
        "p6",       // REG_P6
        "p7",       // REG_P7
        "p8",       // REG_P8
        "p9",       // REG_P9
        "p10",      // REG_P10
        "p11",      // REG_P11
        "p12",      // REG_P12
        "p13",      // REG_P13
        "p14",      // REG_P14
        "p15",      // REG_P15

        "sp",       // REG_SP
        "ffr",      // REG_FFR
#elif TARGET_LOONGARCH64
        "zero",     // REG_R0
        "ra",       // REG_RA
        "tp",       // REG_TP
        "sp",       // REG_SP
        "a0",       // REG_A0
        "a1",       // REG_A1
        "a2",       // REG_A2
        "a3",       // REG_A3
        "a4",       // REG_A4
        "a5",       // REG_A5
        "a6",       // REG_A6
        "a7",       // REG_A7
        "t0",       // REG_T0
        "t1",       // REG_T1
        "t2",       // REG_T2
        "t3",       // REG_T3
        "t4",       // REG_T4
        "t5",       // REG_T5
        "t6",       // REG_T6
        "t7",       // REG_T7
        "t8",       // REG_T8
        "x0",       // REG_X0
        "fp",       // REG_FP
        "s0",       // REG_S0
        "s1",       // REG_S1
        "s2",       // REG_S2
        "s3",       // REG_S3
        "s4",       // REG_S4
        "s5",       // REG_S5
        "s6",       // REG_S6
        "s7",       // REG_S7
        "s8",       // REG_S8

        "f0",       // REG_F0
        "f1",       // REG_F1
        "f2",       // REG_F2
        "f3",       // REG_F3
        "f4",       // REG_F4
        "f5",       // REG_F5
        "f6",       // REG_F6
        "f7",       // REG_F7
        "f8",       // REG_F8
        "f9",       // REG_F9
        "f10",      // REG_F10
        "f11",      // REG_F11
        "f12",      // REG_F12
        "f13",      // REG_F13
        "f14",      // REG_F14
        "f15",      // REG_F15
        "f16",      // REG_F16
        "f17",      // REG_F17
        "f18",      // REG_F18
        "f19",      // REG_F19
        "f20",      // REG_F20
        "f21",      // REG_F21
        "f22",      // REG_F22
        "f23",      // REG_F23
        "f24",      // REG_F24
        "f25",      // REG_F25
        "f26",      // REG_F26
        "f27",      // REG_F27
        "f28",      // REG_F28
        "f29",      // REG_F29
        "f30",      // REG_F30
        "f31",      // REG_F31
#elif TARGET_RISCV64
        "zero",     // REG_R0, REG_ZERO
        "ra",       // REG_RA
        "sp",       // REG_SP
        "gp",       // REG_GP
        "tp",       // REG_TP
        "t0",       // REG_T0
        "t1",       // REG_T1
        "t2",       // REG_T2
        "fp",       // REG_FP
        "s1",       // REG_S1
        "a0",       // REG_A0
        "a1",       // REG_A1
        "a2",       // REG_A2
        "a3",       // REG_A3
        "a4",       // REG_A4
        "a5",       // REG_A5
        "a6",       // REG_A6
        "a7",       // REG_A7
        "s2",       // REG_S2
        "s3",       // REG_S3
        "s4",       // REG_S4
        "s5",       // REG_S5
        "s6",       // REG_S6
        "s7",       // REG_S7
        "s8",       // REG_S8
        "s9",       // REG_S9
        "s10",      // REG_S10
        "s11",      // REG_S11
        "t3",       // REG_T3
        "t4",       // REG_T4
        "t5",       // REG_T5
        "t6",       // REG_T6

        "ft0",      // REG_FT0
        "ft1",      // REG_FT1
        "ft2",      // REG_FT2
        "ft3",      // REG_FT3
        "ft4",      // REG_FT4
        "ft5",      // REG_FT5
        "ft6",      // REG_FT6
        "ft7",      // REG_FT7
        "fs0",      // REG_FS0
        "fs1",      // REG_FS1
        "fa0",      // REG_FA0
        "fa1",      // REG_FA1
        "fa2",      // REG_FA2
        "fa3",      // REG_FA3
        "fa4",      // REG_FA4
        "fa5",      // REG_FA5
        "fa6",      // REG_FA6
        "fa7",      // REG_FA7
        "fs2",      // REG_FS2
        "fs3",      // REG_FS3
        "fs4",      // REG_FS4
        "fs5",      // REG_FS5
        "fs6",      // REG_FS6
        "fs7",      // REG_FS7
        "fs8",      // REG_FS8
        "fs9",      // REG_FS9
        "fs10",     // REG_FS10
        "fs11",     // REG_FS11
        "ft8",      // REG_FT8
        "ft9",      // REG_FT9
        "ft10",     // REG_FT10
        "ft11",     // REG_FT11
#elif !TARGET_WASM
#error Unsupported or unset target architecture
#endif

        "STK", // REG_STK
        "NA",  // REG_NA
    ];
#endif

    extension(regNumber regNum)
    {
#if HAS_FIXED_REGISTER_SET
        public string Name
        {
            get
            {
                assert(s_names.Length == (int)(REG_COUNT + 1));
                return s_names[(int)(regNum)];
            }
        }
#endif
    }
}
