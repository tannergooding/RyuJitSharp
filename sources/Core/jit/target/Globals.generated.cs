// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regMaskTP RBM_NONE => default;
    public static regMaskTP RBM_X86_EAX => regMaskTP.CreateFromRegNum(REG_X86_EAX, SRBM_X86_EAX);
    public static regMaskTP RBM_X86_ECX => regMaskTP.CreateFromRegNum(REG_X86_ECX, SRBM_X86_ECX);
    public static regMaskTP RBM_X86_EDX => regMaskTP.CreateFromRegNum(REG_X86_EDX, SRBM_X86_EDX);
    public static regMaskTP RBM_X86_EBX => regMaskTP.CreateFromRegNum(REG_X86_EBX, SRBM_X86_EBX);
    public static regMaskTP RBM_X86_ESP => regMaskTP.CreateFromRegNum(REG_X86_ESP, SRBM_X86_ESP);
    public static regMaskTP RBM_X86_EBP => regMaskTP.CreateFromRegNum(REG_X86_EBP, SRBM_X86_EBP);
    public static regMaskTP RBM_X86_ESI => regMaskTP.CreateFromRegNum(REG_X86_ESI, SRBM_X86_ESI);
    public static regMaskTP RBM_X86_EDI => regMaskTP.CreateFromRegNum(REG_X86_EDI, SRBM_X86_EDI);
    public static regMaskTP RBM_X86_RAX => RBM_X86_EAX;
    public static regMaskTP RBM_X86_RCX => RBM_X86_ECX;
    public static regMaskTP RBM_X86_RDX => RBM_X86_EDX;
    public static regMaskTP RBM_X86_RBX => RBM_X86_EBX;
    public static regMaskTP RBM_X86_RSP => RBM_X86_ESP;
    public static regMaskTP RBM_X86_RBP => RBM_X86_EBP;
    public static regMaskTP RBM_X86_RSI => RBM_X86_ESI;
    public static regMaskTP RBM_X86_RDI => RBM_X86_EDI;
    public static regMaskTP RBM_X86_XMM0 => regMaskTP.CreateFromRegNum(REG_X86_XMM0, SRBM_X86_XMM0);
    public static regMaskTP RBM_X86_XMM1 => regMaskTP.CreateFromRegNum(REG_X86_XMM1, SRBM_X86_XMM1);
    public static regMaskTP RBM_X86_XMM2 => regMaskTP.CreateFromRegNum(REG_X86_XMM2, SRBM_X86_XMM2);
    public static regMaskTP RBM_X86_XMM3 => regMaskTP.CreateFromRegNum(REG_X86_XMM3, SRBM_X86_XMM3);
    public static regMaskTP RBM_X86_XMM4 => regMaskTP.CreateFromRegNum(REG_X86_XMM4, SRBM_X86_XMM4);
    public static regMaskTP RBM_X86_XMM5 => regMaskTP.CreateFromRegNum(REG_X86_XMM5, SRBM_X86_XMM5);
    public static regMaskTP RBM_X86_XMM6 => regMaskTP.CreateFromRegNum(REG_X86_XMM6, SRBM_X86_XMM6);
    public static regMaskTP RBM_X86_XMM7 => regMaskTP.CreateFromRegNum(REG_X86_XMM7, SRBM_X86_XMM7);
    public static regMaskTP RBM_X86_K0 => regMaskTP.CreateFromRegNum(REG_X86_K0, SRBM_X86_K0);
    public static regMaskTP RBM_X86_K1 => regMaskTP.CreateFromRegNum(REG_X86_K1, SRBM_X86_K1);
    public static regMaskTP RBM_X86_K2 => regMaskTP.CreateFromRegNum(REG_X86_K2, SRBM_X86_K2);
    public static regMaskTP RBM_X86_K3 => regMaskTP.CreateFromRegNum(REG_X86_K3, SRBM_X86_K3);
    public static regMaskTP RBM_X86_K4 => regMaskTP.CreateFromRegNum(REG_X86_K4, SRBM_X86_K4);
    public static regMaskTP RBM_X86_K5 => regMaskTP.CreateFromRegNum(REG_X86_K5, SRBM_X86_K5);
    public static regMaskTP RBM_X86_K6 => regMaskTP.CreateFromRegNum(REG_X86_K6, SRBM_X86_K6);
    public static regMaskTP RBM_X86_K7 => regMaskTP.CreateFromRegNum(REG_X86_K7, SRBM_X86_K7);
    public static regMaskTP RBM_X86_STK => regMaskTP.CreateFromRegNum(REG_X86_STK, SRBM_STK);
    public static regMaskTP RBM_X64_RAX => regMaskTP.CreateFromRegNum(REG_X64_RAX, SRBM_X64_RAX);
    public static regMaskTP RBM_X64_RCX => regMaskTP.CreateFromRegNum(REG_X64_RCX, SRBM_X64_RCX);
    public static regMaskTP RBM_X64_RDX => regMaskTP.CreateFromRegNum(REG_X64_RDX, SRBM_X64_RDX);
    public static regMaskTP RBM_X64_RBX => regMaskTP.CreateFromRegNum(REG_X64_RBX, SRBM_X64_RBX);
    public static regMaskTP RBM_X64_RSP => regMaskTP.CreateFromRegNum(REG_X64_RSP, SRBM_X64_RSP);
    public static regMaskTP RBM_X64_RBP => regMaskTP.CreateFromRegNum(REG_X64_RBP, SRBM_X64_RBP);
    public static regMaskTP RBM_X64_RSI => regMaskTP.CreateFromRegNum(REG_X64_RSI, SRBM_X64_RSI);
    public static regMaskTP RBM_X64_RDI => regMaskTP.CreateFromRegNum(REG_X64_RDI, SRBM_X64_RDI);
    public static regMaskTP RBM_X64_R8 => regMaskTP.CreateFromRegNum(REG_X64_R8, SRBM_X64_R8);
    public static regMaskTP RBM_X64_R9 => regMaskTP.CreateFromRegNum(REG_X64_R9, SRBM_X64_R9);
    public static regMaskTP RBM_X64_R10 => regMaskTP.CreateFromRegNum(REG_X64_R10, SRBM_X64_R10);
    public static regMaskTP RBM_X64_R11 => regMaskTP.CreateFromRegNum(REG_X64_R11, SRBM_X64_R11);
    public static regMaskTP RBM_X64_R12 => regMaskTP.CreateFromRegNum(REG_X64_R12, SRBM_X64_R12);
    public static regMaskTP RBM_X64_R13 => regMaskTP.CreateFromRegNum(REG_X64_R13, SRBM_X64_R13);
    public static regMaskTP RBM_X64_R14 => regMaskTP.CreateFromRegNum(REG_X64_R14, SRBM_X64_R14);
    public static regMaskTP RBM_X64_R15 => regMaskTP.CreateFromRegNum(REG_X64_R15, SRBM_X64_R15);
    public static regMaskTP RBM_X64_R16 => regMaskTP.CreateFromRegNum(REG_X64_R16, SRBM_X64_R16);
    public static regMaskTP RBM_X64_R17 => regMaskTP.CreateFromRegNum(REG_X64_R17, SRBM_X64_R17);
    public static regMaskTP RBM_X64_R18 => regMaskTP.CreateFromRegNum(REG_X64_R18, SRBM_X64_R18);
    public static regMaskTP RBM_X64_R19 => regMaskTP.CreateFromRegNum(REG_X64_R19, SRBM_X64_R19);
    public static regMaskTP RBM_X64_R20 => regMaskTP.CreateFromRegNum(REG_X64_R20, SRBM_X64_R20);
    public static regMaskTP RBM_X64_R21 => regMaskTP.CreateFromRegNum(REG_X64_R21, SRBM_X64_R21);
    public static regMaskTP RBM_X64_R22 => regMaskTP.CreateFromRegNum(REG_X64_R22, SRBM_X64_R22);
    public static regMaskTP RBM_X64_R23 => regMaskTP.CreateFromRegNum(REG_X64_R23, SRBM_X64_R23);
    public static regMaskTP RBM_X64_R24 => regMaskTP.CreateFromRegNum(REG_X64_R24, SRBM_X64_R24);
    public static regMaskTP RBM_X64_R25 => regMaskTP.CreateFromRegNum(REG_X64_R25, SRBM_X64_R25);
    public static regMaskTP RBM_X64_R26 => regMaskTP.CreateFromRegNum(REG_X64_R26, SRBM_X64_R26);
    public static regMaskTP RBM_X64_R27 => regMaskTP.CreateFromRegNum(REG_X64_R27, SRBM_X64_R27);
    public static regMaskTP RBM_X64_R28 => regMaskTP.CreateFromRegNum(REG_X64_R28, SRBM_X64_R28);
    public static regMaskTP RBM_X64_R29 => regMaskTP.CreateFromRegNum(REG_X64_R29, SRBM_X64_R29);
    public static regMaskTP RBM_X64_R30 => regMaskTP.CreateFromRegNum(REG_X64_R30, SRBM_X64_R30);
    public static regMaskTP RBM_X64_R31 => regMaskTP.CreateFromRegNum(REG_X64_R31, SRBM_X64_R31);
    public static regMaskTP RBM_X64_EAX => RBM_X64_RAX;
    public static regMaskTP RBM_X64_ECX => RBM_X64_RCX;
    public static regMaskTP RBM_X64_EDX => RBM_X64_RDX;
    public static regMaskTP RBM_X64_EBX => RBM_X64_RBX;
    public static regMaskTP RBM_X64_ESP => RBM_X64_RSP;
    public static regMaskTP RBM_X64_EBP => RBM_X64_RBP;
    public static regMaskTP RBM_X64_ESI => RBM_X64_RSI;
    public static regMaskTP RBM_X64_EDI => RBM_X64_RDI;
    public static regMaskTP RBM_X64_XMM0 => regMaskTP.CreateFromRegNum(REG_X64_XMM0, SRBM_X64_XMM0);
    public static regMaskTP RBM_X64_XMM1 => regMaskTP.CreateFromRegNum(REG_X64_XMM1, SRBM_X64_XMM1);
    public static regMaskTP RBM_X64_XMM2 => regMaskTP.CreateFromRegNum(REG_X64_XMM2, SRBM_X64_XMM2);
    public static regMaskTP RBM_X64_XMM3 => regMaskTP.CreateFromRegNum(REG_X64_XMM3, SRBM_X64_XMM3);
    public static regMaskTP RBM_X64_XMM4 => regMaskTP.CreateFromRegNum(REG_X64_XMM4, SRBM_X64_XMM4);
    public static regMaskTP RBM_X64_XMM5 => regMaskTP.CreateFromRegNum(REG_X64_XMM5, SRBM_X64_XMM5);
    public static regMaskTP RBM_X64_XMM6 => regMaskTP.CreateFromRegNum(REG_X64_XMM6, SRBM_X64_XMM6);
    public static regMaskTP RBM_X64_XMM7 => regMaskTP.CreateFromRegNum(REG_X64_XMM7, SRBM_X64_XMM7);
    public static regMaskTP RBM_X64_XMM8 => regMaskTP.CreateFromRegNum(REG_X64_XMM8, SRBM_X64_XMM8);
    public static regMaskTP RBM_X64_XMM9 => regMaskTP.CreateFromRegNum(REG_X64_XMM9, SRBM_X64_XMM9);
    public static regMaskTP RBM_X64_XMM10 => regMaskTP.CreateFromRegNum(REG_X64_XMM10, SRBM_X64_XMM10);
    public static regMaskTP RBM_X64_XMM11 => regMaskTP.CreateFromRegNum(REG_X64_XMM11, SRBM_X64_XMM11);
    public static regMaskTP RBM_X64_XMM12 => regMaskTP.CreateFromRegNum(REG_X64_XMM12, SRBM_X64_XMM12);
    public static regMaskTP RBM_X64_XMM13 => regMaskTP.CreateFromRegNum(REG_X64_XMM13, SRBM_X64_XMM13);
    public static regMaskTP RBM_X64_XMM14 => regMaskTP.CreateFromRegNum(REG_X64_XMM14, SRBM_X64_XMM14);
    public static regMaskTP RBM_X64_XMM15 => regMaskTP.CreateFromRegNum(REG_X64_XMM15, SRBM_X64_XMM15);
    public static regMaskTP RBM_X64_XMM16 => regMaskTP.CreateFromRegNum(REG_X64_XMM16, SRBM_X64_XMM16);
    public static regMaskTP RBM_X64_XMM17 => regMaskTP.CreateFromRegNum(REG_X64_XMM17, SRBM_X64_XMM17);
    public static regMaskTP RBM_X64_XMM18 => regMaskTP.CreateFromRegNum(REG_X64_XMM18, SRBM_X64_XMM18);
    public static regMaskTP RBM_X64_XMM19 => regMaskTP.CreateFromRegNum(REG_X64_XMM19, SRBM_X64_XMM19);
    public static regMaskTP RBM_X64_XMM20 => regMaskTP.CreateFromRegNum(REG_X64_XMM20, SRBM_X64_XMM20);
    public static regMaskTP RBM_X64_XMM21 => regMaskTP.CreateFromRegNum(REG_X64_XMM21, SRBM_X64_XMM21);
    public static regMaskTP RBM_X64_XMM22 => regMaskTP.CreateFromRegNum(REG_X64_XMM22, SRBM_X64_XMM22);
    public static regMaskTP RBM_X64_XMM23 => regMaskTP.CreateFromRegNum(REG_X64_XMM23, SRBM_X64_XMM23);
    public static regMaskTP RBM_X64_XMM24 => regMaskTP.CreateFromRegNum(REG_X64_XMM24, SRBM_X64_XMM24);
    public static regMaskTP RBM_X64_XMM25 => regMaskTP.CreateFromRegNum(REG_X64_XMM25, SRBM_X64_XMM25);
    public static regMaskTP RBM_X64_XMM26 => regMaskTP.CreateFromRegNum(REG_X64_XMM26, SRBM_X64_XMM26);
    public static regMaskTP RBM_X64_XMM27 => regMaskTP.CreateFromRegNum(REG_X64_XMM27, SRBM_X64_XMM27);
    public static regMaskTP RBM_X64_XMM28 => regMaskTP.CreateFromRegNum(REG_X64_XMM28, SRBM_X64_XMM28);
    public static regMaskTP RBM_X64_XMM29 => regMaskTP.CreateFromRegNum(REG_X64_XMM29, SRBM_X64_XMM29);
    public static regMaskTP RBM_X64_XMM30 => regMaskTP.CreateFromRegNum(REG_X64_XMM30, SRBM_X64_XMM30);
    public static regMaskTP RBM_X64_XMM31 => regMaskTP.CreateFromRegNum(REG_X64_XMM31, SRBM_X64_XMM31);
    public static regMaskTP RBM_X64_K0 => regMaskTP.CreateFromRegNum(REG_X64_K0, SRBM_X64_K0);
    public static regMaskTP RBM_X64_K1 => regMaskTP.CreateFromRegNum(REG_X64_K1, SRBM_X64_K1);
    public static regMaskTP RBM_X64_K2 => regMaskTP.CreateFromRegNum(REG_X64_K2, SRBM_X64_K2);
    public static regMaskTP RBM_X64_K3 => regMaskTP.CreateFromRegNum(REG_X64_K3, SRBM_X64_K3);
    public static regMaskTP RBM_X64_K4 => regMaskTP.CreateFromRegNum(REG_X64_K4, SRBM_X64_K4);
    public static regMaskTP RBM_X64_K5 => regMaskTP.CreateFromRegNum(REG_X64_K5, SRBM_X64_K5);
    public static regMaskTP RBM_X64_K6 => regMaskTP.CreateFromRegNum(REG_X64_K6, SRBM_X64_K6);
    public static regMaskTP RBM_X64_K7 => regMaskTP.CreateFromRegNum(REG_X64_K7, SRBM_X64_K7);
    public static regMaskTP RBM_X64_STK => regMaskTP.CreateFromRegNum(REG_X64_STK, SRBM_STK);
    public static regMaskTP RBM_ARM32_R0 => regMaskTP.CreateFromRegNum(REG_ARM32_R0, SRBM_ARM32_R0);
    public static regMaskTP RBM_ARM32_R1 => regMaskTP.CreateFromRegNum(REG_ARM32_R1, SRBM_ARM32_R1);
    public static regMaskTP RBM_ARM32_R2 => regMaskTP.CreateFromRegNum(REG_ARM32_R2, SRBM_ARM32_R2);
    public static regMaskTP RBM_ARM32_R3 => regMaskTP.CreateFromRegNum(REG_ARM32_R3, SRBM_ARM32_R3);
    public static regMaskTP RBM_ARM32_R4 => regMaskTP.CreateFromRegNum(REG_ARM32_R4, SRBM_ARM32_R4);
    public static regMaskTP RBM_ARM32_R5 => regMaskTP.CreateFromRegNum(REG_ARM32_R5, SRBM_ARM32_R5);
    public static regMaskTP RBM_ARM32_R6 => regMaskTP.CreateFromRegNum(REG_ARM32_R6, SRBM_ARM32_R6);
    public static regMaskTP RBM_ARM32_R7 => regMaskTP.CreateFromRegNum(REG_ARM32_R7, SRBM_ARM32_R7);
    public static regMaskTP RBM_ARM32_R8 => regMaskTP.CreateFromRegNum(REG_ARM32_R8, SRBM_ARM32_R8);
    public static regMaskTP RBM_ARM32_R9 => regMaskTP.CreateFromRegNum(REG_ARM32_R9, SRBM_ARM32_R9);
    public static regMaskTP RBM_ARM32_R10 => regMaskTP.CreateFromRegNum(REG_ARM32_R10, SRBM_ARM32_R10);
    public static regMaskTP RBM_ARM32_R11 => regMaskTP.CreateFromRegNum(REG_ARM32_R11, SRBM_ARM32_R11);
    public static regMaskTP RBM_ARM32_R12 => regMaskTP.CreateFromRegNum(REG_ARM32_R12, SRBM_ARM32_R12);
    public static regMaskTP RBM_ARM32_SP => regMaskTP.CreateFromRegNum(REG_ARM32_SP, SRBM_ARM32_SP);
    public static regMaskTP RBM_ARM32_LR => regMaskTP.CreateFromRegNum(REG_ARM32_LR, SRBM_ARM32_LR);
    public static regMaskTP RBM_ARM32_PC => regMaskTP.CreateFromRegNum(REG_ARM32_PC, SRBM_ARM32_PC);
    public static regMaskTP RBM_ARM32_FP => RBM_ARM32_R11;
    public static regMaskTP RBM_ARM32_R13 => RBM_ARM32_SP;
    public static regMaskTP RBM_ARM32_R14 => RBM_ARM32_LR;
    public static regMaskTP RBM_ARM32_R15 => RBM_ARM32_PC;
    public static regMaskTP RBM_ARM32_F0 => regMaskTP.CreateFromRegNum(REG_ARM32_F0, SRBM_ARM32_F0);
    public static regMaskTP RBM_ARM32_F1 => regMaskTP.CreateFromRegNum(REG_ARM32_F1, SRBM_ARM32_F1);
    public static regMaskTP RBM_ARM32_F2 => regMaskTP.CreateFromRegNum(REG_ARM32_F2, SRBM_ARM32_F2);
    public static regMaskTP RBM_ARM32_F3 => regMaskTP.CreateFromRegNum(REG_ARM32_F3, SRBM_ARM32_F3);
    public static regMaskTP RBM_ARM32_F4 => regMaskTP.CreateFromRegNum(REG_ARM32_F4, SRBM_ARM32_F4);
    public static regMaskTP RBM_ARM32_F5 => regMaskTP.CreateFromRegNum(REG_ARM32_F5, SRBM_ARM32_F5);
    public static regMaskTP RBM_ARM32_F6 => regMaskTP.CreateFromRegNum(REG_ARM32_F6, SRBM_ARM32_F6);
    public static regMaskTP RBM_ARM32_F7 => regMaskTP.CreateFromRegNum(REG_ARM32_F7, SRBM_ARM32_F7);
    public static regMaskTP RBM_ARM32_F8 => regMaskTP.CreateFromRegNum(REG_ARM32_F8, SRBM_ARM32_F8);
    public static regMaskTP RBM_ARM32_F9 => regMaskTP.CreateFromRegNum(REG_ARM32_F9, SRBM_ARM32_F9);
    public static regMaskTP RBM_ARM32_F10 => regMaskTP.CreateFromRegNum(REG_ARM32_F10, SRBM_ARM32_F10);
    public static regMaskTP RBM_ARM32_F11 => regMaskTP.CreateFromRegNum(REG_ARM32_F11, SRBM_ARM32_F11);
    public static regMaskTP RBM_ARM32_F12 => regMaskTP.CreateFromRegNum(REG_ARM32_F12, SRBM_ARM32_F12);
    public static regMaskTP RBM_ARM32_F13 => regMaskTP.CreateFromRegNum(REG_ARM32_F13, SRBM_ARM32_F13);
    public static regMaskTP RBM_ARM32_F14 => regMaskTP.CreateFromRegNum(REG_ARM32_F14, SRBM_ARM32_F14);
    public static regMaskTP RBM_ARM32_F15 => regMaskTP.CreateFromRegNum(REG_ARM32_F15, SRBM_ARM32_F15);
    public static regMaskTP RBM_ARM32_F16 => regMaskTP.CreateFromRegNum(REG_ARM32_F16, SRBM_ARM32_F16);
    public static regMaskTP RBM_ARM32_F17 => regMaskTP.CreateFromRegNum(REG_ARM32_F17, SRBM_ARM32_F17);
    public static regMaskTP RBM_ARM32_F18 => regMaskTP.CreateFromRegNum(REG_ARM32_F18, SRBM_ARM32_F18);
    public static regMaskTP RBM_ARM32_F19 => regMaskTP.CreateFromRegNum(REG_ARM32_F19, SRBM_ARM32_F19);
    public static regMaskTP RBM_ARM32_F20 => regMaskTP.CreateFromRegNum(REG_ARM32_F20, SRBM_ARM32_F20);
    public static regMaskTP RBM_ARM32_F21 => regMaskTP.CreateFromRegNum(REG_ARM32_F21, SRBM_ARM32_F21);
    public static regMaskTP RBM_ARM32_F22 => regMaskTP.CreateFromRegNum(REG_ARM32_F22, SRBM_ARM32_F22);
    public static regMaskTP RBM_ARM32_F23 => regMaskTP.CreateFromRegNum(REG_ARM32_F23, SRBM_ARM32_F23);
    public static regMaskTP RBM_ARM32_F24 => regMaskTP.CreateFromRegNum(REG_ARM32_F24, SRBM_ARM32_F24);
    public static regMaskTP RBM_ARM32_F25 => regMaskTP.CreateFromRegNum(REG_ARM32_F25, SRBM_ARM32_F25);
    public static regMaskTP RBM_ARM32_F26 => regMaskTP.CreateFromRegNum(REG_ARM32_F26, SRBM_ARM32_F26);
    public static regMaskTP RBM_ARM32_F27 => regMaskTP.CreateFromRegNum(REG_ARM32_F27, SRBM_ARM32_F27);
    public static regMaskTP RBM_ARM32_F28 => regMaskTP.CreateFromRegNum(REG_ARM32_F28, SRBM_ARM32_F28);
    public static regMaskTP RBM_ARM32_F29 => regMaskTP.CreateFromRegNum(REG_ARM32_F29, SRBM_ARM32_F29);
    public static regMaskTP RBM_ARM32_F30 => regMaskTP.CreateFromRegNum(REG_ARM32_F30, SRBM_ARM32_F30);
    public static regMaskTP RBM_ARM32_F31 => regMaskTP.CreateFromRegNum(REG_ARM32_F31, SRBM_ARM32_F31);
    public static regMaskTP RBM_ARM32_STK => regMaskTP.CreateFromRegNum(REG_ARM32_STK, SRBM_STK);
    public static regMaskTP RBM_ARM64_R0 => regMaskTP.CreateFromRegNum(REG_ARM64_R0, SRBM_ARM64_R0);
    public static regMaskTP RBM_ARM64_R1 => regMaskTP.CreateFromRegNum(REG_ARM64_R1, SRBM_ARM64_R1);
    public static regMaskTP RBM_ARM64_R2 => regMaskTP.CreateFromRegNum(REG_ARM64_R2, SRBM_ARM64_R2);
    public static regMaskTP RBM_ARM64_R3 => regMaskTP.CreateFromRegNum(REG_ARM64_R3, SRBM_ARM64_R3);
    public static regMaskTP RBM_ARM64_R4 => regMaskTP.CreateFromRegNum(REG_ARM64_R4, SRBM_ARM64_R4);
    public static regMaskTP RBM_ARM64_R5 => regMaskTP.CreateFromRegNum(REG_ARM64_R5, SRBM_ARM64_R5);
    public static regMaskTP RBM_ARM64_R6 => regMaskTP.CreateFromRegNum(REG_ARM64_R6, SRBM_ARM64_R6);
    public static regMaskTP RBM_ARM64_R7 => regMaskTP.CreateFromRegNum(REG_ARM64_R7, SRBM_ARM64_R7);
    public static regMaskTP RBM_ARM64_R8 => regMaskTP.CreateFromRegNum(REG_ARM64_R8, SRBM_ARM64_R8);
    public static regMaskTP RBM_ARM64_R9 => regMaskTP.CreateFromRegNum(REG_ARM64_R9, SRBM_ARM64_R9);
    public static regMaskTP RBM_ARM64_R10 => regMaskTP.CreateFromRegNum(REG_ARM64_R10, SRBM_ARM64_R10);
    public static regMaskTP RBM_ARM64_R11 => regMaskTP.CreateFromRegNum(REG_ARM64_R11, SRBM_ARM64_R11);
    public static regMaskTP RBM_ARM64_R12 => regMaskTP.CreateFromRegNum(REG_ARM64_R12, SRBM_ARM64_R12);
    public static regMaskTP RBM_ARM64_R13 => regMaskTP.CreateFromRegNum(REG_ARM64_R13, SRBM_ARM64_R13);
    public static regMaskTP RBM_ARM64_R14 => regMaskTP.CreateFromRegNum(REG_ARM64_R14, SRBM_ARM64_R14);
    public static regMaskTP RBM_ARM64_R15 => regMaskTP.CreateFromRegNum(REG_ARM64_R15, SRBM_ARM64_R15);
    public static regMaskTP RBM_ARM64_IP0 => regMaskTP.CreateFromRegNum(REG_ARM64_IP0, SRBM_ARM64_IP0);
    public static regMaskTP RBM_ARM64_IP1 => regMaskTP.CreateFromRegNum(REG_ARM64_IP1, SRBM_ARM64_IP1);
    public static regMaskTP RBM_ARM64_PR => regMaskTP.CreateFromRegNum(REG_ARM64_PR, SRBM_ARM64_PR);
    public static regMaskTP RBM_ARM64_R19 => regMaskTP.CreateFromRegNum(REG_ARM64_R19, SRBM_ARM64_R19);
    public static regMaskTP RBM_ARM64_R20 => regMaskTP.CreateFromRegNum(REG_ARM64_R20, SRBM_ARM64_R20);
    public static regMaskTP RBM_ARM64_R21 => regMaskTP.CreateFromRegNum(REG_ARM64_R21, SRBM_ARM64_R21);
    public static regMaskTP RBM_ARM64_R22 => regMaskTP.CreateFromRegNum(REG_ARM64_R22, SRBM_ARM64_R22);
    public static regMaskTP RBM_ARM64_R23 => regMaskTP.CreateFromRegNum(REG_ARM64_R23, SRBM_ARM64_R23);
    public static regMaskTP RBM_ARM64_R24 => regMaskTP.CreateFromRegNum(REG_ARM64_R24, SRBM_ARM64_R24);
    public static regMaskTP RBM_ARM64_R25 => regMaskTP.CreateFromRegNum(REG_ARM64_R25, SRBM_ARM64_R25);
    public static regMaskTP RBM_ARM64_R26 => regMaskTP.CreateFromRegNum(REG_ARM64_R26, SRBM_ARM64_R26);
    public static regMaskTP RBM_ARM64_R27 => regMaskTP.CreateFromRegNum(REG_ARM64_R27, SRBM_ARM64_R27);
    public static regMaskTP RBM_ARM64_R28 => regMaskTP.CreateFromRegNum(REG_ARM64_R28, SRBM_ARM64_R28);
    public static regMaskTP RBM_ARM64_FP => regMaskTP.CreateFromRegNum(REG_ARM64_FP, SRBM_ARM64_FP);
    public static regMaskTP RBM_ARM64_LR => regMaskTP.CreateFromRegNum(REG_ARM64_LR, SRBM_ARM64_LR);
    public static regMaskTP RBM_ARM64_ZR => regMaskTP.CreateFromRegNum(REG_ARM64_ZR, SRBM_ARM64_ZR);
    public static regMaskTP RBM_ARM64_R16 => RBM_ARM64_IP0;
    public static regMaskTP RBM_ARM64_R17 => RBM_ARM64_IP1;
    public static regMaskTP RBM_ARM64_R18 => RBM_ARM64_PR;
    public static regMaskTP RBM_ARM64_R29 => RBM_ARM64_FP;
    public static regMaskTP RBM_ARM64_R30 => RBM_ARM64_LR;
    public static regMaskTP RBM_ARM64_V0 => regMaskTP.CreateFromRegNum(REG_ARM64_V0, SRBM_ARM64_V0);
    public static regMaskTP RBM_ARM64_V1 => regMaskTP.CreateFromRegNum(REG_ARM64_V1, SRBM_ARM64_V1);
    public static regMaskTP RBM_ARM64_V2 => regMaskTP.CreateFromRegNum(REG_ARM64_V2, SRBM_ARM64_V2);
    public static regMaskTP RBM_ARM64_V3 => regMaskTP.CreateFromRegNum(REG_ARM64_V3, SRBM_ARM64_V3);
    public static regMaskTP RBM_ARM64_V4 => regMaskTP.CreateFromRegNum(REG_ARM64_V4, SRBM_ARM64_V4);
    public static regMaskTP RBM_ARM64_V5 => regMaskTP.CreateFromRegNum(REG_ARM64_V5, SRBM_ARM64_V5);
    public static regMaskTP RBM_ARM64_V6 => regMaskTP.CreateFromRegNum(REG_ARM64_V6, SRBM_ARM64_V6);
    public static regMaskTP RBM_ARM64_V7 => regMaskTP.CreateFromRegNum(REG_ARM64_V7, SRBM_ARM64_V7);
    public static regMaskTP RBM_ARM64_V8 => regMaskTP.CreateFromRegNum(REG_ARM64_V8, SRBM_ARM64_V8);
    public static regMaskTP RBM_ARM64_V9 => regMaskTP.CreateFromRegNum(REG_ARM64_V9, SRBM_ARM64_V9);
    public static regMaskTP RBM_ARM64_V10 => regMaskTP.CreateFromRegNum(REG_ARM64_V10, SRBM_ARM64_V10);
    public static regMaskTP RBM_ARM64_V11 => regMaskTP.CreateFromRegNum(REG_ARM64_V11, SRBM_ARM64_V11);
    public static regMaskTP RBM_ARM64_V12 => regMaskTP.CreateFromRegNum(REG_ARM64_V12, SRBM_ARM64_V12);
    public static regMaskTP RBM_ARM64_V13 => regMaskTP.CreateFromRegNum(REG_ARM64_V13, SRBM_ARM64_V13);
    public static regMaskTP RBM_ARM64_V14 => regMaskTP.CreateFromRegNum(REG_ARM64_V14, SRBM_ARM64_V14);
    public static regMaskTP RBM_ARM64_V15 => regMaskTP.CreateFromRegNum(REG_ARM64_V15, SRBM_ARM64_V15);
    public static regMaskTP RBM_ARM64_V16 => regMaskTP.CreateFromRegNum(REG_ARM64_V16, SRBM_ARM64_V16);
    public static regMaskTP RBM_ARM64_V17 => regMaskTP.CreateFromRegNum(REG_ARM64_V17, SRBM_ARM64_V17);
    public static regMaskTP RBM_ARM64_V18 => regMaskTP.CreateFromRegNum(REG_ARM64_V18, SRBM_ARM64_V18);
    public static regMaskTP RBM_ARM64_V19 => regMaskTP.CreateFromRegNum(REG_ARM64_V19, SRBM_ARM64_V19);
    public static regMaskTP RBM_ARM64_V20 => regMaskTP.CreateFromRegNum(REG_ARM64_V20, SRBM_ARM64_V20);
    public static regMaskTP RBM_ARM64_V21 => regMaskTP.CreateFromRegNum(REG_ARM64_V21, SRBM_ARM64_V21);
    public static regMaskTP RBM_ARM64_V22 => regMaskTP.CreateFromRegNum(REG_ARM64_V22, SRBM_ARM64_V22);
    public static regMaskTP RBM_ARM64_V23 => regMaskTP.CreateFromRegNum(REG_ARM64_V23, SRBM_ARM64_V23);
    public static regMaskTP RBM_ARM64_V24 => regMaskTP.CreateFromRegNum(REG_ARM64_V24, SRBM_ARM64_V24);
    public static regMaskTP RBM_ARM64_V25 => regMaskTP.CreateFromRegNum(REG_ARM64_V25, SRBM_ARM64_V25);
    public static regMaskTP RBM_ARM64_V26 => regMaskTP.CreateFromRegNum(REG_ARM64_V26, SRBM_ARM64_V26);
    public static regMaskTP RBM_ARM64_V27 => regMaskTP.CreateFromRegNum(REG_ARM64_V27, SRBM_ARM64_V27);
    public static regMaskTP RBM_ARM64_V28 => regMaskTP.CreateFromRegNum(REG_ARM64_V28, SRBM_ARM64_V28);
    public static regMaskTP RBM_ARM64_V29 => regMaskTP.CreateFromRegNum(REG_ARM64_V29, SRBM_ARM64_V29);
    public static regMaskTP RBM_ARM64_V30 => regMaskTP.CreateFromRegNum(REG_ARM64_V30, SRBM_ARM64_V30);
    public static regMaskTP RBM_ARM64_V31 => regMaskTP.CreateFromRegNum(REG_ARM64_V31, SRBM_ARM64_V31);
    public static regMaskTP RBM_ARM64_P0 => regMaskTP.CreateFromRegNum(REG_ARM64_P0, SRBM_ARM64_P0);
    public static regMaskTP RBM_ARM64_P1 => regMaskTP.CreateFromRegNum(REG_ARM64_P1, SRBM_ARM64_P1);
    public static regMaskTP RBM_ARM64_P2 => regMaskTP.CreateFromRegNum(REG_ARM64_P2, SRBM_ARM64_P2);
    public static regMaskTP RBM_ARM64_P3 => regMaskTP.CreateFromRegNum(REG_ARM64_P3, SRBM_ARM64_P3);
    public static regMaskTP RBM_ARM64_P4 => regMaskTP.CreateFromRegNum(REG_ARM64_P4, SRBM_ARM64_P4);
    public static regMaskTP RBM_ARM64_P5 => regMaskTP.CreateFromRegNum(REG_ARM64_P5, SRBM_ARM64_P5);
    public static regMaskTP RBM_ARM64_P6 => regMaskTP.CreateFromRegNum(REG_ARM64_P6, SRBM_ARM64_P6);
    public static regMaskTP RBM_ARM64_P7 => regMaskTP.CreateFromRegNum(REG_ARM64_P7, SRBM_ARM64_P7);
    public static regMaskTP RBM_ARM64_P8 => regMaskTP.CreateFromRegNum(REG_ARM64_P8, SRBM_ARM64_P8);
    public static regMaskTP RBM_ARM64_P9 => regMaskTP.CreateFromRegNum(REG_ARM64_P9, SRBM_ARM64_P9);
    public static regMaskTP RBM_ARM64_P10 => regMaskTP.CreateFromRegNum(REG_ARM64_P10, SRBM_ARM64_P10);
    public static regMaskTP RBM_ARM64_P11 => regMaskTP.CreateFromRegNum(REG_ARM64_P11, SRBM_ARM64_P11);
    public static regMaskTP RBM_ARM64_P12 => regMaskTP.CreateFromRegNum(REG_ARM64_P12, SRBM_ARM64_P12);
    public static regMaskTP RBM_ARM64_P13 => regMaskTP.CreateFromRegNum(REG_ARM64_P13, SRBM_ARM64_P13);
    public static regMaskTP RBM_ARM64_P14 => regMaskTP.CreateFromRegNum(REG_ARM64_P14, SRBM_ARM64_P14);
    public static regMaskTP RBM_ARM64_P15 => regMaskTP.CreateFromRegNum(REG_ARM64_P15, SRBM_ARM64_P15);
    public static regMaskTP RBM_ARM64_SP => regMaskTP.CreateFromRegNum(REG_ARM64_SP, SRBM_ARM64_SP);
    public static regMaskTP RBM_ARM64_FFR => regMaskTP.CreateFromRegNum(REG_ARM64_FFR, SRBM_ARM64_FFR);
    public static regMaskTP RBM_ARM64_STK => regMaskTP.CreateFromRegNum(REG_ARM64_STK, SRBM_STK);
    public static regMaskTP RBM_LOONGARCH64_R0 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_R0, SRBM_LOONGARCH64_R0);
    public static regMaskTP RBM_LOONGARCH64_RA => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_RA, SRBM_LOONGARCH64_RA);
    public static regMaskTP RBM_LOONGARCH64_TP => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_TP, SRBM_LOONGARCH64_TP);
    public static regMaskTP RBM_LOONGARCH64_SP => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_SP, SRBM_LOONGARCH64_SP);
    public static regMaskTP RBM_LOONGARCH64_A0 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A0, SRBM_LOONGARCH64_A0);
    public static regMaskTP RBM_LOONGARCH64_A1 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A1, SRBM_LOONGARCH64_A1);
    public static regMaskTP RBM_LOONGARCH64_A2 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A2, SRBM_LOONGARCH64_A2);
    public static regMaskTP RBM_LOONGARCH64_A3 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A3, SRBM_LOONGARCH64_A3);
    public static regMaskTP RBM_LOONGARCH64_A4 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A4, SRBM_LOONGARCH64_A4);
    public static regMaskTP RBM_LOONGARCH64_A5 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A5, SRBM_LOONGARCH64_A5);
    public static regMaskTP RBM_LOONGARCH64_A6 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A6, SRBM_LOONGARCH64_A6);
    public static regMaskTP RBM_LOONGARCH64_A7 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_A7, SRBM_LOONGARCH64_A7);
    public static regMaskTP RBM_LOONGARCH64_T0 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T0, SRBM_LOONGARCH64_T0);
    public static regMaskTP RBM_LOONGARCH64_T1 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T1, SRBM_LOONGARCH64_T1);
    public static regMaskTP RBM_LOONGARCH64_T2 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T2, SRBM_LOONGARCH64_T2);
    public static regMaskTP RBM_LOONGARCH64_T3 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T3, SRBM_LOONGARCH64_T3);
    public static regMaskTP RBM_LOONGARCH64_T4 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T4, SRBM_LOONGARCH64_T4);
    public static regMaskTP RBM_LOONGARCH64_T5 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T5, SRBM_LOONGARCH64_T5);
    public static regMaskTP RBM_LOONGARCH64_T6 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T6, SRBM_LOONGARCH64_T6);
    public static regMaskTP RBM_LOONGARCH64_T7 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T7, SRBM_LOONGARCH64_T7);
    public static regMaskTP RBM_LOONGARCH64_T8 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_T8, SRBM_LOONGARCH64_T8);
    public static regMaskTP RBM_LOONGARCH64_X0 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_X0, SRBM_LOONGARCH64_X0);
    public static regMaskTP RBM_LOONGARCH64_FP => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_FP, SRBM_LOONGARCH64_FP);
    public static regMaskTP RBM_LOONGARCH64_S0 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S0, SRBM_LOONGARCH64_S0);
    public static regMaskTP RBM_LOONGARCH64_S1 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S1, SRBM_LOONGARCH64_S1);
    public static regMaskTP RBM_LOONGARCH64_S2 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S2, SRBM_LOONGARCH64_S2);
    public static regMaskTP RBM_LOONGARCH64_S3 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S3, SRBM_LOONGARCH64_S3);
    public static regMaskTP RBM_LOONGARCH64_S4 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S4, SRBM_LOONGARCH64_S4);
    public static regMaskTP RBM_LOONGARCH64_S5 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S5, SRBM_LOONGARCH64_S5);
    public static regMaskTP RBM_LOONGARCH64_S6 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S6, SRBM_LOONGARCH64_S6);
    public static regMaskTP RBM_LOONGARCH64_S7 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S7, SRBM_LOONGARCH64_S7);
    public static regMaskTP RBM_LOONGARCH64_S8 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_S8, SRBM_LOONGARCH64_S8);
    public static regMaskTP RBM_LOONGARCH64_R21 => RBM_LOONGARCH64_X0;
    public static regMaskTP RBM_LOONGARCH64_F0 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F0, SRBM_LOONGARCH64_F0);
    public static regMaskTP RBM_LOONGARCH64_F1 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F1, SRBM_LOONGARCH64_F1);
    public static regMaskTP RBM_LOONGARCH64_F2 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F2, SRBM_LOONGARCH64_F2);
    public static regMaskTP RBM_LOONGARCH64_F3 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F3, SRBM_LOONGARCH64_F3);
    public static regMaskTP RBM_LOONGARCH64_F4 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F4, SRBM_LOONGARCH64_F4);
    public static regMaskTP RBM_LOONGARCH64_F5 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F5, SRBM_LOONGARCH64_F5);
    public static regMaskTP RBM_LOONGARCH64_F6 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F6, SRBM_LOONGARCH64_F6);
    public static regMaskTP RBM_LOONGARCH64_F7 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F7, SRBM_LOONGARCH64_F7);
    public static regMaskTP RBM_LOONGARCH64_F8 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F8, SRBM_LOONGARCH64_F8);
    public static regMaskTP RBM_LOONGARCH64_F9 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F9, SRBM_LOONGARCH64_F9);
    public static regMaskTP RBM_LOONGARCH64_F10 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F10, SRBM_LOONGARCH64_F10);
    public static regMaskTP RBM_LOONGARCH64_F11 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F11, SRBM_LOONGARCH64_F11);
    public static regMaskTP RBM_LOONGARCH64_F12 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F12, SRBM_LOONGARCH64_F12);
    public static regMaskTP RBM_LOONGARCH64_F13 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F13, SRBM_LOONGARCH64_F13);
    public static regMaskTP RBM_LOONGARCH64_F14 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F14, SRBM_LOONGARCH64_F14);
    public static regMaskTP RBM_LOONGARCH64_F15 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F15, SRBM_LOONGARCH64_F15);
    public static regMaskTP RBM_LOONGARCH64_F16 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F16, SRBM_LOONGARCH64_F16);
    public static regMaskTP RBM_LOONGARCH64_F17 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F17, SRBM_LOONGARCH64_F17);
    public static regMaskTP RBM_LOONGARCH64_F18 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F18, SRBM_LOONGARCH64_F18);
    public static regMaskTP RBM_LOONGARCH64_F19 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F19, SRBM_LOONGARCH64_F19);
    public static regMaskTP RBM_LOONGARCH64_F20 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F20, SRBM_LOONGARCH64_F20);
    public static regMaskTP RBM_LOONGARCH64_F21 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F21, SRBM_LOONGARCH64_F21);
    public static regMaskTP RBM_LOONGARCH64_F22 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F22, SRBM_LOONGARCH64_F22);
    public static regMaskTP RBM_LOONGARCH64_F23 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F23, SRBM_LOONGARCH64_F23);
    public static regMaskTP RBM_LOONGARCH64_F24 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F24, SRBM_LOONGARCH64_F24);
    public static regMaskTP RBM_LOONGARCH64_F25 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F25, SRBM_LOONGARCH64_F25);
    public static regMaskTP RBM_LOONGARCH64_F26 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F26, SRBM_LOONGARCH64_F26);
    public static regMaskTP RBM_LOONGARCH64_F27 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F27, SRBM_LOONGARCH64_F27);
    public static regMaskTP RBM_LOONGARCH64_F28 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F28, SRBM_LOONGARCH64_F28);
    public static regMaskTP RBM_LOONGARCH64_F29 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F29, SRBM_LOONGARCH64_F29);
    public static regMaskTP RBM_LOONGARCH64_F30 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F30, SRBM_LOONGARCH64_F30);
    public static regMaskTP RBM_LOONGARCH64_F31 => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_F31, SRBM_LOONGARCH64_F31);
    public static regMaskTP RBM_LOONGARCH64_STK => regMaskTP.CreateFromRegNum(REG_LOONGARCH64_STK, SRBM_STK);
    public static regMaskTP RBM_RISCV64_R0 => regMaskTP.CreateFromRegNum(REG_RISCV64_R0, SRBM_RISCV64_R0);
    public static regMaskTP RBM_RISCV64_RA => regMaskTP.CreateFromRegNum(REG_RISCV64_RA, SRBM_RISCV64_RA);
    public static regMaskTP RBM_RISCV64_SP => regMaskTP.CreateFromRegNum(REG_RISCV64_SP, SRBM_RISCV64_SP);
    public static regMaskTP RBM_RISCV64_GP => regMaskTP.CreateFromRegNum(REG_RISCV64_GP, SRBM_RISCV64_GP);
    public static regMaskTP RBM_RISCV64_TP => regMaskTP.CreateFromRegNum(REG_RISCV64_TP, SRBM_RISCV64_TP);
    public static regMaskTP RBM_RISCV64_T0 => regMaskTP.CreateFromRegNum(REG_RISCV64_T0, SRBM_RISCV64_T0);
    public static regMaskTP RBM_RISCV64_T1 => regMaskTP.CreateFromRegNum(REG_RISCV64_T1, SRBM_RISCV64_T1);
    public static regMaskTP RBM_RISCV64_T2 => regMaskTP.CreateFromRegNum(REG_RISCV64_T2, SRBM_RISCV64_T2);
    public static regMaskTP RBM_RISCV64_FP => regMaskTP.CreateFromRegNum(REG_RISCV64_FP, SRBM_RISCV64_FP);
    public static regMaskTP RBM_RISCV64_S1 => regMaskTP.CreateFromRegNum(REG_RISCV64_S1, SRBM_RISCV64_S1);
    public static regMaskTP RBM_RISCV64_A0 => regMaskTP.CreateFromRegNum(REG_RISCV64_A0, SRBM_RISCV64_A0);
    public static regMaskTP RBM_RISCV64_A1 => regMaskTP.CreateFromRegNum(REG_RISCV64_A1, SRBM_RISCV64_A1);
    public static regMaskTP RBM_RISCV64_A2 => regMaskTP.CreateFromRegNum(REG_RISCV64_A2, SRBM_RISCV64_A2);
    public static regMaskTP RBM_RISCV64_A3 => regMaskTP.CreateFromRegNum(REG_RISCV64_A3, SRBM_RISCV64_A3);
    public static regMaskTP RBM_RISCV64_A4 => regMaskTP.CreateFromRegNum(REG_RISCV64_A4, SRBM_RISCV64_A4);
    public static regMaskTP RBM_RISCV64_A5 => regMaskTP.CreateFromRegNum(REG_RISCV64_A5, SRBM_RISCV64_A5);
    public static regMaskTP RBM_RISCV64_A6 => regMaskTP.CreateFromRegNum(REG_RISCV64_A6, SRBM_RISCV64_A6);
    public static regMaskTP RBM_RISCV64_A7 => regMaskTP.CreateFromRegNum(REG_RISCV64_A7, SRBM_RISCV64_A7);
    public static regMaskTP RBM_RISCV64_S2 => regMaskTP.CreateFromRegNum(REG_RISCV64_S2, SRBM_RISCV64_S2);
    public static regMaskTP RBM_RISCV64_S3 => regMaskTP.CreateFromRegNum(REG_RISCV64_S3, SRBM_RISCV64_S3);
    public static regMaskTP RBM_RISCV64_S4 => regMaskTP.CreateFromRegNum(REG_RISCV64_S4, SRBM_RISCV64_S4);
    public static regMaskTP RBM_RISCV64_S5 => regMaskTP.CreateFromRegNum(REG_RISCV64_S5, SRBM_RISCV64_S5);
    public static regMaskTP RBM_RISCV64_S6 => regMaskTP.CreateFromRegNum(REG_RISCV64_S6, SRBM_RISCV64_S6);
    public static regMaskTP RBM_RISCV64_S7 => regMaskTP.CreateFromRegNum(REG_RISCV64_S7, SRBM_RISCV64_S7);
    public static regMaskTP RBM_RISCV64_S8 => regMaskTP.CreateFromRegNum(REG_RISCV64_S8, SRBM_RISCV64_S8);
    public static regMaskTP RBM_RISCV64_S9 => regMaskTP.CreateFromRegNum(REG_RISCV64_S9, SRBM_RISCV64_S9);
    public static regMaskTP RBM_RISCV64_S10 => regMaskTP.CreateFromRegNum(REG_RISCV64_S10, SRBM_RISCV64_S10);
    public static regMaskTP RBM_RISCV64_S11 => regMaskTP.CreateFromRegNum(REG_RISCV64_S11, SRBM_RISCV64_S11);
    public static regMaskTP RBM_RISCV64_T3 => regMaskTP.CreateFromRegNum(REG_RISCV64_T3, SRBM_RISCV64_T3);
    public static regMaskTP RBM_RISCV64_T4 => regMaskTP.CreateFromRegNum(REG_RISCV64_T4, SRBM_RISCV64_T4);
    public static regMaskTP RBM_RISCV64_T5 => regMaskTP.CreateFromRegNum(REG_RISCV64_T5, SRBM_RISCV64_T5);
    public static regMaskTP RBM_RISCV64_T6 => regMaskTP.CreateFromRegNum(REG_RISCV64_T6, SRBM_RISCV64_T6);
    public static regMaskTP RBM_RISCV64_R8 => RBM_RISCV64_FP;
    public static regMaskTP RBM_RISCV64_ZERO => RBM_RISCV64_R0;
    public static regMaskTP RBM_RISCV64_FT0 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT0, SRBM_RISCV64_FT0);
    public static regMaskTP RBM_RISCV64_FT1 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT1, SRBM_RISCV64_FT1);
    public static regMaskTP RBM_RISCV64_FT2 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT2, SRBM_RISCV64_FT2);
    public static regMaskTP RBM_RISCV64_FT3 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT3, SRBM_RISCV64_FT3);
    public static regMaskTP RBM_RISCV64_FT4 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT4, SRBM_RISCV64_FT4);
    public static regMaskTP RBM_RISCV64_FT5 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT5, SRBM_RISCV64_FT5);
    public static regMaskTP RBM_RISCV64_FT6 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT6, SRBM_RISCV64_FT6);
    public static regMaskTP RBM_RISCV64_FT7 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT7, SRBM_RISCV64_FT7);
    public static regMaskTP RBM_RISCV64_FS0 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS0, SRBM_RISCV64_FS0);
    public static regMaskTP RBM_RISCV64_FS1 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS1, SRBM_RISCV64_FS1);
    public static regMaskTP RBM_RISCV64_FA0 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA0, SRBM_RISCV64_FA0);
    public static regMaskTP RBM_RISCV64_FA1 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA1, SRBM_RISCV64_FA1);
    public static regMaskTP RBM_RISCV64_FA2 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA2, SRBM_RISCV64_FA2);
    public static regMaskTP RBM_RISCV64_FA3 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA3, SRBM_RISCV64_FA3);
    public static regMaskTP RBM_RISCV64_FA4 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA4, SRBM_RISCV64_FA4);
    public static regMaskTP RBM_RISCV64_FA5 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA5, SRBM_RISCV64_FA5);
    public static regMaskTP RBM_RISCV64_FA6 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA6, SRBM_RISCV64_FA6);
    public static regMaskTP RBM_RISCV64_FA7 => regMaskTP.CreateFromRegNum(REG_RISCV64_FA7, SRBM_RISCV64_FA7);
    public static regMaskTP RBM_RISCV64_FS2 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS2, SRBM_RISCV64_FS2);
    public static regMaskTP RBM_RISCV64_FS3 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS3, SRBM_RISCV64_FS3);
    public static regMaskTP RBM_RISCV64_FS4 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS4, SRBM_RISCV64_FS4);
    public static regMaskTP RBM_RISCV64_FS5 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS5, SRBM_RISCV64_FS5);
    public static regMaskTP RBM_RISCV64_FS6 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS6, SRBM_RISCV64_FS6);
    public static regMaskTP RBM_RISCV64_FS7 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS7, SRBM_RISCV64_FS7);
    public static regMaskTP RBM_RISCV64_FS8 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS8, SRBM_RISCV64_FS8);
    public static regMaskTP RBM_RISCV64_FS9 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS9, SRBM_RISCV64_FS9);
    public static regMaskTP RBM_RISCV64_FS10 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS10, SRBM_RISCV64_FS10);
    public static regMaskTP RBM_RISCV64_FS11 => regMaskTP.CreateFromRegNum(REG_RISCV64_FS11, SRBM_RISCV64_FS11);
    public static regMaskTP RBM_RISCV64_FT8 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT8, SRBM_RISCV64_FT8);
    public static regMaskTP RBM_RISCV64_FT9 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT9, SRBM_RISCV64_FT9);
    public static regMaskTP RBM_RISCV64_FT10 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT10, SRBM_RISCV64_FT10);
    public static regMaskTP RBM_RISCV64_FT11 => regMaskTP.CreateFromRegNum(REG_RISCV64_FT11, SRBM_RISCV64_FT11);
    public static regMaskTP RBM_RISCV64_STK => regMaskTP.CreateFromRegNum(REG_RISCV64_STK, SRBM_STK);
    public static regMaskTP RBM_WASM_STK => regMaskTP.CreateFromRegNum(REG_WASM_STK, SRBM_STK);
    public static regMaskTP RBM_EAX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_EAX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_EAX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskEAXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_ECX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_ECX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_ECX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskECXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_EDX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_EDX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_EDX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskEDXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_EBX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_EBX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_EBX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskEBXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_ESP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_ESP;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_ESP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskESPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_EBP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_EBP;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_EBP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskEBPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_ESI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_ESI;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_ESI;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskESIIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_EDI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_EDI;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_EDI;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskEDIIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RAX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RAX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RAX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRAXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RCX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RCX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RCX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRCXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RDX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RDX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RDX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRDXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RBX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RBX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RBX;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRBXIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RSP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RSP;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RSP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRSPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RBP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RBP;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RBP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRBPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RSI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RSI;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RSI;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRSIIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RDI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_RDI;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_RDI;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRDIIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM0
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM0;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM1
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM1;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM2
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM2;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM3
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM3;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM4
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM4;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM5
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM5;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM6
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM6;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM7
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_XMM7;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K0
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K0;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K1
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K1;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K2
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K2;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K3
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K3;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K4
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K4;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K5
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K5;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K6
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K6;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_K7
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_K7;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_K7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskK7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_STK
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_X86_STK;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_STK;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_STK;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_STK;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_STK;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_STK;
            }
            if (Configuration.TARGET_WASM)
            {
                return RBM_WASM_STK;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskSTKIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R8
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R8;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R8;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R8;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_R8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R9
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R9;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R9;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R10
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R10;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R10;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R11
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R11;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R11;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R12
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R12;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R12;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R12;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR12IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R13
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R13;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R13;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R13;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR13IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R14
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R14;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R14;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R14;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR14IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R15
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R15;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R15;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R15;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR15IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R16
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R16;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R16;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR16IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R17
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R17;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R17;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR17IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R18
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R18;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R18;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR18IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R19
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R19;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R19;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR19IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R20
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R20;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R20;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR20IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R21
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R21;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R21;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_R21;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR21IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R22
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R22;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R22;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR22IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R23
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R23;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R23;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR23IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R24
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R24;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R24;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR24IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R25
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R25;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R25;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR25IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R26
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R26;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R26;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR26IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R27
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R27;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R27;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR27IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R28
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R28;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R28;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR28IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R29
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R29;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R29;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR29IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R30
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R30;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R30;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR30IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R31
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_R31;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR31IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM8
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM9
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM10
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM11
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM12
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM12;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM12IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM13
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM13;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM13IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM14
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM14;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM14IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM15
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM15;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM15IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM16
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM16;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM16IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM17
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM17;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM17IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM18
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM18;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM18IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM19
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM19;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM19IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM20
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM20;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM20IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM21
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM21;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM21IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM22
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM22;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM22IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM23
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM23;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM23IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM24
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM24;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM24IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM25
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM25;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM25IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM26
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM26;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM26IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM27
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM27;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM27IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM28
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM28;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM28IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM29
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM29;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM29IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM30
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM30;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM30IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_XMM31
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_X64_XMM31;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskXMM31IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R0
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R0;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R0;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_R0;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_R0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R1
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R1;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R2
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R2;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R3
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R3;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R4
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R4;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R5
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R5;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R6
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R6;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_R7
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_R7;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_R7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskR7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_SP
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_SP;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_SP;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_SP;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_SP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskSPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_LR
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_LR;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_LR;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskLRIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_PC
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_PC;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskPCIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FP
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_FP;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_FP;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_FP;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F0
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F0;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F1
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F1;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F2
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F2;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F3
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F3;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F4
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F4;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F5
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F5;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F6
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F6;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F7
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F7;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F8
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F8;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F9
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F9;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F10
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F10;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F11
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F11;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F12
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F12;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F12;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF12IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F13
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F13;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F13;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF13IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F14
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F14;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F14;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF14IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F15
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F15;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F15;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF15IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F16
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F16;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F16;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF16IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F17
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F17;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F17;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF17IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F18
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F18;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F18;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF18IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F19
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F19;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F19;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF19IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F20
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F20;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F20;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF20IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F21
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F21;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F21;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF21IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F22
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F22;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F22;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF22IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F23
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F23;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F23;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF23IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F24
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F24;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F24;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF24IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F25
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F25;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F25;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF25IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F26
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F26;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F26;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF26IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F27
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F27;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F27;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF27IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F28
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F28;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F28;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF28IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F29
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F29;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F29;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF29IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F30
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F30;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F30;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF30IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_F31
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_ARM32_F31;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_F31;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskF31IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_IP0
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_IP0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskIP0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_IP1
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_IP1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskIP1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_PR
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_PR;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskPRIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_ZR
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_ZR;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskZRIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V0
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V1
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V2
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V3
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V4
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V5
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V6
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V7
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V8
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V9
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V10
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V11
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V12
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V12;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV12IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V13
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V13;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV13IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V14
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V14;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV14IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V15
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V15;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV15IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V16
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V16;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV16IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V17
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V17;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV17IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V18
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V18;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV18IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V19
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V19;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV19IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V20
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V20;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV20IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V21
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V21;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV21IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V22
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V22;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV22IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V23
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V23;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV23IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V24
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V24;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV24IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V25
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V25;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV25IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V26
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V26;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV26IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V27
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V27;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV27IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V28
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V28;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV28IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V29
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V29;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV29IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V30
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V30;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV30IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_V31
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_V31;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskV31IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P0
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P1
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P2
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P3
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P4
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P5
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P6
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P7
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P8
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P9
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P10
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P11
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P12
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P12;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP12IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P13
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P13;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP13IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P14
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P14;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP14IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_P15
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_P15;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskP15IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FFR
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ARM64_FFR;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFFRIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_RA
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_RA;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_RA;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskRAIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_TP
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_TP;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_TP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskTPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A0;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A1
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A1;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A2
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A2;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A3
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A3;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A4
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A4;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A5
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A5;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A6
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A6;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_A7
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_A7;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_A7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskA7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T0;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T1
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T1;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T2
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T2;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T3
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T3;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T4
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T4;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T5
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T5;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T6
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T6;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_T6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T7
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_T8
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_T8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskT8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_X0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_X0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskX0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S1
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S1;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S2
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S2;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S3
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S3;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S4
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S4;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S5
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S5;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S6
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S6;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S7
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S7;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S8
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_LOONGARCH64_S8;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_GP
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_GP;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskGPIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S9
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S10
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_S11
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_S11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskS11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_ZERO
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_ZERO;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskZEROIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT0
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT1
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT2
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT3
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT4
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT5
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT6
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT7
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS0
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS1
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA0
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA0;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA0IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA1
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA1;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA1IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA2
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA3
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA4
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA5
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA6
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FA7
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FA7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFA7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS2
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS2;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS2IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS3
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS3;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS3IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS4
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS4;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS4IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS5
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS5;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS5IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS6
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS6;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS6IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS7
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS7;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS7IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS8
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS9
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS10
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FS11
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FS11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFS11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT8
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT8;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT8IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT9
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT9;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT9IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT10
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT10;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT10IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
    public static regMaskTP RBM_FT11
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RISCV64_FT11;
            }
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMaskFT11IsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }

}