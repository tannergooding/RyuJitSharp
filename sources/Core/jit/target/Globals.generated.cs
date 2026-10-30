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

}