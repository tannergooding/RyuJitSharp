// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regMaskTP RBM_NONE => default;
    public static regMaskTP RBM_EAX_X86 => regMaskTP.CreateFromRegNum(REG_EAX_X86, SRBM_EAX_X86);
    public static regMaskTP RBM_ECX_X86 => regMaskTP.CreateFromRegNum(REG_ECX_X86, SRBM_ECX_X86);
    public static regMaskTP RBM_EDX_X86 => regMaskTP.CreateFromRegNum(REG_EDX_X86, SRBM_EDX_X86);
    public static regMaskTP RBM_EBX_X86 => regMaskTP.CreateFromRegNum(REG_EBX_X86, SRBM_EBX_X86);
    public static regMaskTP RBM_ESP_X86 => regMaskTP.CreateFromRegNum(REG_ESP_X86, SRBM_ESP_X86);
    public static regMaskTP RBM_EBP_X86 => regMaskTP.CreateFromRegNum(REG_EBP_X86, SRBM_EBP_X86);
    public static regMaskTP RBM_ESI_X86 => regMaskTP.CreateFromRegNum(REG_ESI_X86, SRBM_ESI_X86);
    public static regMaskTP RBM_EDI_X86 => regMaskTP.CreateFromRegNum(REG_EDI_X86, SRBM_EDI_X86);
    public static regMaskTP RBM_RAX_X86 => RBM_EAX_X86;
    public static regMaskTP RBM_RCX_X86 => RBM_ECX_X86;
    public static regMaskTP RBM_RDX_X86 => RBM_EDX_X86;
    public static regMaskTP RBM_RBX_X86 => RBM_EBX_X86;
    public static regMaskTP RBM_RSP_X86 => RBM_ESP_X86;
    public static regMaskTP RBM_RBP_X86 => RBM_EBP_X86;
    public static regMaskTP RBM_RSI_X86 => RBM_ESI_X86;
    public static regMaskTP RBM_RDI_X86 => RBM_EDI_X86;
    public static regMaskTP RBM_XMM0_X86 => regMaskTP.CreateFromRegNum(REG_XMM0_X86, SRBM_XMM0_X86);
    public static regMaskTP RBM_XMM1_X86 => regMaskTP.CreateFromRegNum(REG_XMM1_X86, SRBM_XMM1_X86);
    public static regMaskTP RBM_XMM2_X86 => regMaskTP.CreateFromRegNum(REG_XMM2_X86, SRBM_XMM2_X86);
    public static regMaskTP RBM_XMM3_X86 => regMaskTP.CreateFromRegNum(REG_XMM3_X86, SRBM_XMM3_X86);
    public static regMaskTP RBM_XMM4_X86 => regMaskTP.CreateFromRegNum(REG_XMM4_X86, SRBM_XMM4_X86);
    public static regMaskTP RBM_XMM5_X86 => regMaskTP.CreateFromRegNum(REG_XMM5_X86, SRBM_XMM5_X86);
    public static regMaskTP RBM_XMM6_X86 => regMaskTP.CreateFromRegNum(REG_XMM6_X86, SRBM_XMM6_X86);
    public static regMaskTP RBM_XMM7_X86 => regMaskTP.CreateFromRegNum(REG_XMM7_X86, SRBM_XMM7_X86);
    public static regMaskTP RBM_K0_X86 => regMaskTP.CreateFromRegNum(REG_K0_X86, SRBM_K0_X86);
    public static regMaskTP RBM_K1_X86 => regMaskTP.CreateFromRegNum(REG_K1_X86, SRBM_K1_X86);
    public static regMaskTP RBM_K2_X86 => regMaskTP.CreateFromRegNum(REG_K2_X86, SRBM_K2_X86);
    public static regMaskTP RBM_K3_X86 => regMaskTP.CreateFromRegNum(REG_K3_X86, SRBM_K3_X86);
    public static regMaskTP RBM_K4_X86 => regMaskTP.CreateFromRegNum(REG_K4_X86, SRBM_K4_X86);
    public static regMaskTP RBM_K5_X86 => regMaskTP.CreateFromRegNum(REG_K5_X86, SRBM_K5_X86);
    public static regMaskTP RBM_K6_X86 => regMaskTP.CreateFromRegNum(REG_K6_X86, SRBM_K6_X86);
    public static regMaskTP RBM_K7_X86 => regMaskTP.CreateFromRegNum(REG_K7_X86, SRBM_K7_X86);
    public static regMaskTP RBM_STK_X86 => regMaskTP.CreateFromRegNum(REG_STK_X86, SRBM_STK_X86);
    public static regMaskTP RBM_RAX_AMD64 => regMaskTP.CreateFromRegNum(REG_RAX_AMD64, SRBM_RAX_AMD64);
    public static regMaskTP RBM_RCX_AMD64 => regMaskTP.CreateFromRegNum(REG_RCX_AMD64, SRBM_RCX_AMD64);
    public static regMaskTP RBM_RDX_AMD64 => regMaskTP.CreateFromRegNum(REG_RDX_AMD64, SRBM_RDX_AMD64);
    public static regMaskTP RBM_RBX_AMD64 => regMaskTP.CreateFromRegNum(REG_RBX_AMD64, SRBM_RBX_AMD64);
    public static regMaskTP RBM_RSP_AMD64 => regMaskTP.CreateFromRegNum(REG_RSP_AMD64, SRBM_RSP_AMD64);
    public static regMaskTP RBM_RBP_AMD64 => regMaskTP.CreateFromRegNum(REG_RBP_AMD64, SRBM_RBP_AMD64);
    public static regMaskTP RBM_RSI_AMD64 => regMaskTP.CreateFromRegNum(REG_RSI_AMD64, SRBM_RSI_AMD64);
    public static regMaskTP RBM_RDI_AMD64 => regMaskTP.CreateFromRegNum(REG_RDI_AMD64, SRBM_RDI_AMD64);
    public static regMaskTP RBM_R8_AMD64 => regMaskTP.CreateFromRegNum(REG_R8_AMD64, SRBM_R8_AMD64);
    public static regMaskTP RBM_R9_AMD64 => regMaskTP.CreateFromRegNum(REG_R9_AMD64, SRBM_R9_AMD64);
    public static regMaskTP RBM_R10_AMD64 => regMaskTP.CreateFromRegNum(REG_R10_AMD64, SRBM_R10_AMD64);
    public static regMaskTP RBM_R11_AMD64 => regMaskTP.CreateFromRegNum(REG_R11_AMD64, SRBM_R11_AMD64);
    public static regMaskTP RBM_R12_AMD64 => regMaskTP.CreateFromRegNum(REG_R12_AMD64, SRBM_R12_AMD64);
    public static regMaskTP RBM_R13_AMD64 => regMaskTP.CreateFromRegNum(REG_R13_AMD64, SRBM_R13_AMD64);
    public static regMaskTP RBM_R14_AMD64 => regMaskTP.CreateFromRegNum(REG_R14_AMD64, SRBM_R14_AMD64);
    public static regMaskTP RBM_R15_AMD64 => regMaskTP.CreateFromRegNum(REG_R15_AMD64, SRBM_R15_AMD64);
    public static regMaskTP RBM_R16_AMD64 => regMaskTP.CreateFromRegNum(REG_R16_AMD64, SRBM_R16_AMD64);
    public static regMaskTP RBM_R17_AMD64 => regMaskTP.CreateFromRegNum(REG_R17_AMD64, SRBM_R17_AMD64);
    public static regMaskTP RBM_R18_AMD64 => regMaskTP.CreateFromRegNum(REG_R18_AMD64, SRBM_R18_AMD64);
    public static regMaskTP RBM_R19_AMD64 => regMaskTP.CreateFromRegNum(REG_R19_AMD64, SRBM_R19_AMD64);
    public static regMaskTP RBM_R20_AMD64 => regMaskTP.CreateFromRegNum(REG_R20_AMD64, SRBM_R20_AMD64);
    public static regMaskTP RBM_R21_AMD64 => regMaskTP.CreateFromRegNum(REG_R21_AMD64, SRBM_R21_AMD64);
    public static regMaskTP RBM_R22_AMD64 => regMaskTP.CreateFromRegNum(REG_R22_AMD64, SRBM_R22_AMD64);
    public static regMaskTP RBM_R23_AMD64 => regMaskTP.CreateFromRegNum(REG_R23_AMD64, SRBM_R23_AMD64);
    public static regMaskTP RBM_R24_AMD64 => regMaskTP.CreateFromRegNum(REG_R24_AMD64, SRBM_R24_AMD64);
    public static regMaskTP RBM_R25_AMD64 => regMaskTP.CreateFromRegNum(REG_R25_AMD64, SRBM_R25_AMD64);
    public static regMaskTP RBM_R26_AMD64 => regMaskTP.CreateFromRegNum(REG_R26_AMD64, SRBM_R26_AMD64);
    public static regMaskTP RBM_R27_AMD64 => regMaskTP.CreateFromRegNum(REG_R27_AMD64, SRBM_R27_AMD64);
    public static regMaskTP RBM_R28_AMD64 => regMaskTP.CreateFromRegNum(REG_R28_AMD64, SRBM_R28_AMD64);
    public static regMaskTP RBM_R29_AMD64 => regMaskTP.CreateFromRegNum(REG_R29_AMD64, SRBM_R29_AMD64);
    public static regMaskTP RBM_R30_AMD64 => regMaskTP.CreateFromRegNum(REG_R30_AMD64, SRBM_R30_AMD64);
    public static regMaskTP RBM_R31_AMD64 => regMaskTP.CreateFromRegNum(REG_R31_AMD64, SRBM_R31_AMD64);
    public static regMaskTP RBM_EAX_AMD64 => RBM_RAX_AMD64;
    public static regMaskTP RBM_ECX_AMD64 => RBM_RCX_AMD64;
    public static regMaskTP RBM_EDX_AMD64 => RBM_RDX_AMD64;
    public static regMaskTP RBM_EBX_AMD64 => RBM_RBX_AMD64;
    public static regMaskTP RBM_ESP_AMD64 => RBM_RSP_AMD64;
    public static regMaskTP RBM_EBP_AMD64 => RBM_RBP_AMD64;
    public static regMaskTP RBM_ESI_AMD64 => RBM_RSI_AMD64;
    public static regMaskTP RBM_EDI_AMD64 => RBM_RDI_AMD64;
    public static regMaskTP RBM_XMM0_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM0_AMD64, SRBM_XMM0_AMD64);
    public static regMaskTP RBM_XMM1_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM1_AMD64, SRBM_XMM1_AMD64);
    public static regMaskTP RBM_XMM2_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM2_AMD64, SRBM_XMM2_AMD64);
    public static regMaskTP RBM_XMM3_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM3_AMD64, SRBM_XMM3_AMD64);
    public static regMaskTP RBM_XMM4_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM4_AMD64, SRBM_XMM4_AMD64);
    public static regMaskTP RBM_XMM5_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM5_AMD64, SRBM_XMM5_AMD64);
    public static regMaskTP RBM_XMM6_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM6_AMD64, SRBM_XMM6_AMD64);
    public static regMaskTP RBM_XMM7_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM7_AMD64, SRBM_XMM7_AMD64);
    public static regMaskTP RBM_XMM8_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM8_AMD64, SRBM_XMM8_AMD64);
    public static regMaskTP RBM_XMM9_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM9_AMD64, SRBM_XMM9_AMD64);
    public static regMaskTP RBM_XMM10_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM10_AMD64, SRBM_XMM10_AMD64);
    public static regMaskTP RBM_XMM11_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM11_AMD64, SRBM_XMM11_AMD64);
    public static regMaskTP RBM_XMM12_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM12_AMD64, SRBM_XMM12_AMD64);
    public static regMaskTP RBM_XMM13_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM13_AMD64, SRBM_XMM13_AMD64);
    public static regMaskTP RBM_XMM14_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM14_AMD64, SRBM_XMM14_AMD64);
    public static regMaskTP RBM_XMM15_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM15_AMD64, SRBM_XMM15_AMD64);
    public static regMaskTP RBM_XMM16_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM16_AMD64, SRBM_XMM16_AMD64);
    public static regMaskTP RBM_XMM17_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM17_AMD64, SRBM_XMM17_AMD64);
    public static regMaskTP RBM_XMM18_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM18_AMD64, SRBM_XMM18_AMD64);
    public static regMaskTP RBM_XMM19_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM19_AMD64, SRBM_XMM19_AMD64);
    public static regMaskTP RBM_XMM20_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM20_AMD64, SRBM_XMM20_AMD64);
    public static regMaskTP RBM_XMM21_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM21_AMD64, SRBM_XMM21_AMD64);
    public static regMaskTP RBM_XMM22_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM22_AMD64, SRBM_XMM22_AMD64);
    public static regMaskTP RBM_XMM23_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM23_AMD64, SRBM_XMM23_AMD64);
    public static regMaskTP RBM_XMM24_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM24_AMD64, SRBM_XMM24_AMD64);
    public static regMaskTP RBM_XMM25_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM25_AMD64, SRBM_XMM25_AMD64);
    public static regMaskTP RBM_XMM26_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM26_AMD64, SRBM_XMM26_AMD64);
    public static regMaskTP RBM_XMM27_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM27_AMD64, SRBM_XMM27_AMD64);
    public static regMaskTP RBM_XMM28_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM28_AMD64, SRBM_XMM28_AMD64);
    public static regMaskTP RBM_XMM29_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM29_AMD64, SRBM_XMM29_AMD64);
    public static regMaskTP RBM_XMM30_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM30_AMD64, SRBM_XMM30_AMD64);
    public static regMaskTP RBM_XMM31_AMD64 => regMaskTP.CreateFromRegNum(REG_XMM31_AMD64, SRBM_XMM31_AMD64);
    public static regMaskTP RBM_K0_AMD64 => regMaskTP.CreateFromRegNum(REG_K0_AMD64, SRBM_K0_AMD64);
    public static regMaskTP RBM_K1_AMD64 => regMaskTP.CreateFromRegNum(REG_K1_AMD64, SRBM_K1_AMD64);
    public static regMaskTP RBM_K2_AMD64 => regMaskTP.CreateFromRegNum(REG_K2_AMD64, SRBM_K2_AMD64);
    public static regMaskTP RBM_K3_AMD64 => regMaskTP.CreateFromRegNum(REG_K3_AMD64, SRBM_K3_AMD64);
    public static regMaskTP RBM_K4_AMD64 => regMaskTP.CreateFromRegNum(REG_K4_AMD64, SRBM_K4_AMD64);
    public static regMaskTP RBM_K5_AMD64 => regMaskTP.CreateFromRegNum(REG_K5_AMD64, SRBM_K5_AMD64);
    public static regMaskTP RBM_K6_AMD64 => regMaskTP.CreateFromRegNum(REG_K6_AMD64, SRBM_K6_AMD64);
    public static regMaskTP RBM_K7_AMD64 => regMaskTP.CreateFromRegNum(REG_K7_AMD64, SRBM_K7_AMD64);
    public static regMaskTP RBM_STK_AMD64 => regMaskTP.CreateFromRegNum(REG_STK_AMD64, SRBM_STK_AMD64);
    public static regMaskTP RBM_R0_ARM => regMaskTP.CreateFromRegNum(REG_R0_ARM, SRBM_R0_ARM);
    public static regMaskTP RBM_R1_ARM => regMaskTP.CreateFromRegNum(REG_R1_ARM, SRBM_R1_ARM);
    public static regMaskTP RBM_R2_ARM => regMaskTP.CreateFromRegNum(REG_R2_ARM, SRBM_R2_ARM);
    public static regMaskTP RBM_R3_ARM => regMaskTP.CreateFromRegNum(REG_R3_ARM, SRBM_R3_ARM);
    public static regMaskTP RBM_R4_ARM => regMaskTP.CreateFromRegNum(REG_R4_ARM, SRBM_R4_ARM);
    public static regMaskTP RBM_R5_ARM => regMaskTP.CreateFromRegNum(REG_R5_ARM, SRBM_R5_ARM);
    public static regMaskTP RBM_R6_ARM => regMaskTP.CreateFromRegNum(REG_R6_ARM, SRBM_R6_ARM);
    public static regMaskTP RBM_R7_ARM => regMaskTP.CreateFromRegNum(REG_R7_ARM, SRBM_R7_ARM);
    public static regMaskTP RBM_R8_ARM => regMaskTP.CreateFromRegNum(REG_R8_ARM, SRBM_R8_ARM);
    public static regMaskTP RBM_R9_ARM => regMaskTP.CreateFromRegNum(REG_R9_ARM, SRBM_R9_ARM);
    public static regMaskTP RBM_R10_ARM => regMaskTP.CreateFromRegNum(REG_R10_ARM, SRBM_R10_ARM);
    public static regMaskTP RBM_R11_ARM => regMaskTP.CreateFromRegNum(REG_R11_ARM, SRBM_R11_ARM);
    public static regMaskTP RBM_R12_ARM => regMaskTP.CreateFromRegNum(REG_R12_ARM, SRBM_R12_ARM);
    public static regMaskTP RBM_SP_ARM => regMaskTP.CreateFromRegNum(REG_SP_ARM, SRBM_SP_ARM);
    public static regMaskTP RBM_LR_ARM => regMaskTP.CreateFromRegNum(REG_LR_ARM, SRBM_LR_ARM);
    public static regMaskTP RBM_PC_ARM => regMaskTP.CreateFromRegNum(REG_PC_ARM, SRBM_PC_ARM);
    public static regMaskTP RBM_FP_ARM => RBM_R11_ARM;
    public static regMaskTP RBM_R13_ARM => RBM_SP_ARM;
    public static regMaskTP RBM_R14_ARM => RBM_LR_ARM;
    public static regMaskTP RBM_R15_ARM => RBM_PC_ARM;
    public static regMaskTP RBM_F0_ARM => regMaskTP.CreateFromRegNum(REG_F0_ARM, SRBM_F0_ARM);
    public static regMaskTP RBM_F1_ARM => regMaskTP.CreateFromRegNum(REG_F1_ARM, SRBM_F1_ARM);
    public static regMaskTP RBM_F2_ARM => regMaskTP.CreateFromRegNum(REG_F2_ARM, SRBM_F2_ARM);
    public static regMaskTP RBM_F3_ARM => regMaskTP.CreateFromRegNum(REG_F3_ARM, SRBM_F3_ARM);
    public static regMaskTP RBM_F4_ARM => regMaskTP.CreateFromRegNum(REG_F4_ARM, SRBM_F4_ARM);
    public static regMaskTP RBM_F5_ARM => regMaskTP.CreateFromRegNum(REG_F5_ARM, SRBM_F5_ARM);
    public static regMaskTP RBM_F6_ARM => regMaskTP.CreateFromRegNum(REG_F6_ARM, SRBM_F6_ARM);
    public static regMaskTP RBM_F7_ARM => regMaskTP.CreateFromRegNum(REG_F7_ARM, SRBM_F7_ARM);
    public static regMaskTP RBM_F8_ARM => regMaskTP.CreateFromRegNum(REG_F8_ARM, SRBM_F8_ARM);
    public static regMaskTP RBM_F9_ARM => regMaskTP.CreateFromRegNum(REG_F9_ARM, SRBM_F9_ARM);
    public static regMaskTP RBM_F10_ARM => regMaskTP.CreateFromRegNum(REG_F10_ARM, SRBM_F10_ARM);
    public static regMaskTP RBM_F11_ARM => regMaskTP.CreateFromRegNum(REG_F11_ARM, SRBM_F11_ARM);
    public static regMaskTP RBM_F12_ARM => regMaskTP.CreateFromRegNum(REG_F12_ARM, SRBM_F12_ARM);
    public static regMaskTP RBM_F13_ARM => regMaskTP.CreateFromRegNum(REG_F13_ARM, SRBM_F13_ARM);
    public static regMaskTP RBM_F14_ARM => regMaskTP.CreateFromRegNum(REG_F14_ARM, SRBM_F14_ARM);
    public static regMaskTP RBM_F15_ARM => regMaskTP.CreateFromRegNum(REG_F15_ARM, SRBM_F15_ARM);
    public static regMaskTP RBM_F16_ARM => regMaskTP.CreateFromRegNum(REG_F16_ARM, SRBM_F16_ARM);
    public static regMaskTP RBM_F17_ARM => regMaskTP.CreateFromRegNum(REG_F17_ARM, SRBM_F17_ARM);
    public static regMaskTP RBM_F18_ARM => regMaskTP.CreateFromRegNum(REG_F18_ARM, SRBM_F18_ARM);
    public static regMaskTP RBM_F19_ARM => regMaskTP.CreateFromRegNum(REG_F19_ARM, SRBM_F19_ARM);
    public static regMaskTP RBM_F20_ARM => regMaskTP.CreateFromRegNum(REG_F20_ARM, SRBM_F20_ARM);
    public static regMaskTP RBM_F21_ARM => regMaskTP.CreateFromRegNum(REG_F21_ARM, SRBM_F21_ARM);
    public static regMaskTP RBM_F22_ARM => regMaskTP.CreateFromRegNum(REG_F22_ARM, SRBM_F22_ARM);
    public static regMaskTP RBM_F23_ARM => regMaskTP.CreateFromRegNum(REG_F23_ARM, SRBM_F23_ARM);
    public static regMaskTP RBM_F24_ARM => regMaskTP.CreateFromRegNum(REG_F24_ARM, SRBM_F24_ARM);
    public static regMaskTP RBM_F25_ARM => regMaskTP.CreateFromRegNum(REG_F25_ARM, SRBM_F25_ARM);
    public static regMaskTP RBM_F26_ARM => regMaskTP.CreateFromRegNum(REG_F26_ARM, SRBM_F26_ARM);
    public static regMaskTP RBM_F27_ARM => regMaskTP.CreateFromRegNum(REG_F27_ARM, SRBM_F27_ARM);
    public static regMaskTP RBM_F28_ARM => regMaskTP.CreateFromRegNum(REG_F28_ARM, SRBM_F28_ARM);
    public static regMaskTP RBM_F29_ARM => regMaskTP.CreateFromRegNum(REG_F29_ARM, SRBM_F29_ARM);
    public static regMaskTP RBM_F30_ARM => regMaskTP.CreateFromRegNum(REG_F30_ARM, SRBM_F30_ARM);
    public static regMaskTP RBM_F31_ARM => regMaskTP.CreateFromRegNum(REG_F31_ARM, SRBM_F31_ARM);
    public static regMaskTP RBM_STK_ARM => regMaskTP.CreateFromRegNum(REG_STK_ARM, SRBM_STK_ARM);
    public static regMaskTP RBM_R0_ARM64 => regMaskTP.CreateFromRegNum(REG_R0_ARM64, SRBM_R0_ARM64);
    public static regMaskTP RBM_R1_ARM64 => regMaskTP.CreateFromRegNum(REG_R1_ARM64, SRBM_R1_ARM64);
    public static regMaskTP RBM_R2_ARM64 => regMaskTP.CreateFromRegNum(REG_R2_ARM64, SRBM_R2_ARM64);
    public static regMaskTP RBM_R3_ARM64 => regMaskTP.CreateFromRegNum(REG_R3_ARM64, SRBM_R3_ARM64);
    public static regMaskTP RBM_R4_ARM64 => regMaskTP.CreateFromRegNum(REG_R4_ARM64, SRBM_R4_ARM64);
    public static regMaskTP RBM_R5_ARM64 => regMaskTP.CreateFromRegNum(REG_R5_ARM64, SRBM_R5_ARM64);
    public static regMaskTP RBM_R6_ARM64 => regMaskTP.CreateFromRegNum(REG_R6_ARM64, SRBM_R6_ARM64);
    public static regMaskTP RBM_R7_ARM64 => regMaskTP.CreateFromRegNum(REG_R7_ARM64, SRBM_R7_ARM64);
    public static regMaskTP RBM_R8_ARM64 => regMaskTP.CreateFromRegNum(REG_R8_ARM64, SRBM_R8_ARM64);
    public static regMaskTP RBM_R9_ARM64 => regMaskTP.CreateFromRegNum(REG_R9_ARM64, SRBM_R9_ARM64);
    public static regMaskTP RBM_R10_ARM64 => regMaskTP.CreateFromRegNum(REG_R10_ARM64, SRBM_R10_ARM64);
    public static regMaskTP RBM_R11_ARM64 => regMaskTP.CreateFromRegNum(REG_R11_ARM64, SRBM_R11_ARM64);
    public static regMaskTP RBM_R12_ARM64 => regMaskTP.CreateFromRegNum(REG_R12_ARM64, SRBM_R12_ARM64);
    public static regMaskTP RBM_R13_ARM64 => regMaskTP.CreateFromRegNum(REG_R13_ARM64, SRBM_R13_ARM64);
    public static regMaskTP RBM_R14_ARM64 => regMaskTP.CreateFromRegNum(REG_R14_ARM64, SRBM_R14_ARM64);
    public static regMaskTP RBM_R15_ARM64 => regMaskTP.CreateFromRegNum(REG_R15_ARM64, SRBM_R15_ARM64);
    public static regMaskTP RBM_IP0_ARM64 => regMaskTP.CreateFromRegNum(REG_IP0_ARM64, SRBM_IP0_ARM64);
    public static regMaskTP RBM_IP1_ARM64 => regMaskTP.CreateFromRegNum(REG_IP1_ARM64, SRBM_IP1_ARM64);
    public static regMaskTP RBM_PR_ARM64 => regMaskTP.CreateFromRegNum(REG_PR_ARM64, SRBM_PR_ARM64);
    public static regMaskTP RBM_R19_ARM64 => regMaskTP.CreateFromRegNum(REG_R19_ARM64, SRBM_R19_ARM64);
    public static regMaskTP RBM_R20_ARM64 => regMaskTP.CreateFromRegNum(REG_R20_ARM64, SRBM_R20_ARM64);
    public static regMaskTP RBM_R21_ARM64 => regMaskTP.CreateFromRegNum(REG_R21_ARM64, SRBM_R21_ARM64);
    public static regMaskTP RBM_R22_ARM64 => regMaskTP.CreateFromRegNum(REG_R22_ARM64, SRBM_R22_ARM64);
    public static regMaskTP RBM_R23_ARM64 => regMaskTP.CreateFromRegNum(REG_R23_ARM64, SRBM_R23_ARM64);
    public static regMaskTP RBM_R24_ARM64 => regMaskTP.CreateFromRegNum(REG_R24_ARM64, SRBM_R24_ARM64);
    public static regMaskTP RBM_R25_ARM64 => regMaskTP.CreateFromRegNum(REG_R25_ARM64, SRBM_R25_ARM64);
    public static regMaskTP RBM_R26_ARM64 => regMaskTP.CreateFromRegNum(REG_R26_ARM64, SRBM_R26_ARM64);
    public static regMaskTP RBM_R27_ARM64 => regMaskTP.CreateFromRegNum(REG_R27_ARM64, SRBM_R27_ARM64);
    public static regMaskTP RBM_R28_ARM64 => regMaskTP.CreateFromRegNum(REG_R28_ARM64, SRBM_R28_ARM64);
    public static regMaskTP RBM_FP_ARM64 => regMaskTP.CreateFromRegNum(REG_FP_ARM64, SRBM_FP_ARM64);
    public static regMaskTP RBM_LR_ARM64 => regMaskTP.CreateFromRegNum(REG_LR_ARM64, SRBM_LR_ARM64);
    public static regMaskTP RBM_ZR_ARM64 => regMaskTP.CreateFromRegNum(REG_ZR_ARM64, SRBM_ZR_ARM64);
    public static regMaskTP RBM_R16_ARM64 => RBM_IP0_ARM64;
    public static regMaskTP RBM_R17_ARM64 => RBM_IP1_ARM64;
    public static regMaskTP RBM_R18_ARM64 => RBM_PR_ARM64;
    public static regMaskTP RBM_R29_ARM64 => RBM_FP_ARM64;
    public static regMaskTP RBM_R30_ARM64 => RBM_LR_ARM64;
    public static regMaskTP RBM_V0_ARM64 => regMaskTP.CreateFromRegNum(REG_V0_ARM64, SRBM_V0_ARM64);
    public static regMaskTP RBM_V1_ARM64 => regMaskTP.CreateFromRegNum(REG_V1_ARM64, SRBM_V1_ARM64);
    public static regMaskTP RBM_V2_ARM64 => regMaskTP.CreateFromRegNum(REG_V2_ARM64, SRBM_V2_ARM64);
    public static regMaskTP RBM_V3_ARM64 => regMaskTP.CreateFromRegNum(REG_V3_ARM64, SRBM_V3_ARM64);
    public static regMaskTP RBM_V4_ARM64 => regMaskTP.CreateFromRegNum(REG_V4_ARM64, SRBM_V4_ARM64);
    public static regMaskTP RBM_V5_ARM64 => regMaskTP.CreateFromRegNum(REG_V5_ARM64, SRBM_V5_ARM64);
    public static regMaskTP RBM_V6_ARM64 => regMaskTP.CreateFromRegNum(REG_V6_ARM64, SRBM_V6_ARM64);
    public static regMaskTP RBM_V7_ARM64 => regMaskTP.CreateFromRegNum(REG_V7_ARM64, SRBM_V7_ARM64);
    public static regMaskTP RBM_V8_ARM64 => regMaskTP.CreateFromRegNum(REG_V8_ARM64, SRBM_V8_ARM64);
    public static regMaskTP RBM_V9_ARM64 => regMaskTP.CreateFromRegNum(REG_V9_ARM64, SRBM_V9_ARM64);
    public static regMaskTP RBM_V10_ARM64 => regMaskTP.CreateFromRegNum(REG_V10_ARM64, SRBM_V10_ARM64);
    public static regMaskTP RBM_V11_ARM64 => regMaskTP.CreateFromRegNum(REG_V11_ARM64, SRBM_V11_ARM64);
    public static regMaskTP RBM_V12_ARM64 => regMaskTP.CreateFromRegNum(REG_V12_ARM64, SRBM_V12_ARM64);
    public static regMaskTP RBM_V13_ARM64 => regMaskTP.CreateFromRegNum(REG_V13_ARM64, SRBM_V13_ARM64);
    public static regMaskTP RBM_V14_ARM64 => regMaskTP.CreateFromRegNum(REG_V14_ARM64, SRBM_V14_ARM64);
    public static regMaskTP RBM_V15_ARM64 => regMaskTP.CreateFromRegNum(REG_V15_ARM64, SRBM_V15_ARM64);
    public static regMaskTP RBM_V16_ARM64 => regMaskTP.CreateFromRegNum(REG_V16_ARM64, SRBM_V16_ARM64);
    public static regMaskTP RBM_V17_ARM64 => regMaskTP.CreateFromRegNum(REG_V17_ARM64, SRBM_V17_ARM64);
    public static regMaskTP RBM_V18_ARM64 => regMaskTP.CreateFromRegNum(REG_V18_ARM64, SRBM_V18_ARM64);
    public static regMaskTP RBM_V19_ARM64 => regMaskTP.CreateFromRegNum(REG_V19_ARM64, SRBM_V19_ARM64);
    public static regMaskTP RBM_V20_ARM64 => regMaskTP.CreateFromRegNum(REG_V20_ARM64, SRBM_V20_ARM64);
    public static regMaskTP RBM_V21_ARM64 => regMaskTP.CreateFromRegNum(REG_V21_ARM64, SRBM_V21_ARM64);
    public static regMaskTP RBM_V22_ARM64 => regMaskTP.CreateFromRegNum(REG_V22_ARM64, SRBM_V22_ARM64);
    public static regMaskTP RBM_V23_ARM64 => regMaskTP.CreateFromRegNum(REG_V23_ARM64, SRBM_V23_ARM64);
    public static regMaskTP RBM_V24_ARM64 => regMaskTP.CreateFromRegNum(REG_V24_ARM64, SRBM_V24_ARM64);
    public static regMaskTP RBM_V25_ARM64 => regMaskTP.CreateFromRegNum(REG_V25_ARM64, SRBM_V25_ARM64);
    public static regMaskTP RBM_V26_ARM64 => regMaskTP.CreateFromRegNum(REG_V26_ARM64, SRBM_V26_ARM64);
    public static regMaskTP RBM_V27_ARM64 => regMaskTP.CreateFromRegNum(REG_V27_ARM64, SRBM_V27_ARM64);
    public static regMaskTP RBM_V28_ARM64 => regMaskTP.CreateFromRegNum(REG_V28_ARM64, SRBM_V28_ARM64);
    public static regMaskTP RBM_V29_ARM64 => regMaskTP.CreateFromRegNum(REG_V29_ARM64, SRBM_V29_ARM64);
    public static regMaskTP RBM_V30_ARM64 => regMaskTP.CreateFromRegNum(REG_V30_ARM64, SRBM_V30_ARM64);
    public static regMaskTP RBM_V31_ARM64 => regMaskTP.CreateFromRegNum(REG_V31_ARM64, SRBM_V31_ARM64);
    public static regMaskTP RBM_P0_ARM64 => regMaskTP.CreateFromRegNum(REG_P0_ARM64, SRBM_P0_ARM64);
    public static regMaskTP RBM_P1_ARM64 => regMaskTP.CreateFromRegNum(REG_P1_ARM64, SRBM_P1_ARM64);
    public static regMaskTP RBM_P2_ARM64 => regMaskTP.CreateFromRegNum(REG_P2_ARM64, SRBM_P2_ARM64);
    public static regMaskTP RBM_P3_ARM64 => regMaskTP.CreateFromRegNum(REG_P3_ARM64, SRBM_P3_ARM64);
    public static regMaskTP RBM_P4_ARM64 => regMaskTP.CreateFromRegNum(REG_P4_ARM64, SRBM_P4_ARM64);
    public static regMaskTP RBM_P5_ARM64 => regMaskTP.CreateFromRegNum(REG_P5_ARM64, SRBM_P5_ARM64);
    public static regMaskTP RBM_P6_ARM64 => regMaskTP.CreateFromRegNum(REG_P6_ARM64, SRBM_P6_ARM64);
    public static regMaskTP RBM_P7_ARM64 => regMaskTP.CreateFromRegNum(REG_P7_ARM64, SRBM_P7_ARM64);
    public static regMaskTP RBM_P8_ARM64 => regMaskTP.CreateFromRegNum(REG_P8_ARM64, SRBM_P8_ARM64);
    public static regMaskTP RBM_P9_ARM64 => regMaskTP.CreateFromRegNum(REG_P9_ARM64, SRBM_P9_ARM64);
    public static regMaskTP RBM_P10_ARM64 => regMaskTP.CreateFromRegNum(REG_P10_ARM64, SRBM_P10_ARM64);
    public static regMaskTP RBM_P11_ARM64 => regMaskTP.CreateFromRegNum(REG_P11_ARM64, SRBM_P11_ARM64);
    public static regMaskTP RBM_P12_ARM64 => regMaskTP.CreateFromRegNum(REG_P12_ARM64, SRBM_P12_ARM64);
    public static regMaskTP RBM_P13_ARM64 => regMaskTP.CreateFromRegNum(REG_P13_ARM64, SRBM_P13_ARM64);
    public static regMaskTP RBM_P14_ARM64 => regMaskTP.CreateFromRegNum(REG_P14_ARM64, SRBM_P14_ARM64);
    public static regMaskTP RBM_P15_ARM64 => regMaskTP.CreateFromRegNum(REG_P15_ARM64, SRBM_P15_ARM64);
    public static regMaskTP RBM_SP_ARM64 => regMaskTP.CreateFromRegNum(REG_SP_ARM64, SRBM_SP_ARM64);
    public static regMaskTP RBM_FFR_ARM64 => regMaskTP.CreateFromRegNum(REG_FFR_ARM64, SRBM_FFR_ARM64);
    public static regMaskTP RBM_STK_ARM64 => regMaskTP.CreateFromRegNum(REG_STK_ARM64, SRBM_STK_ARM64);
    public static regMaskTP RBM_R0_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_R0_LOONGARCH64, SRBM_R0_LOONGARCH64);
    public static regMaskTP RBM_RA_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_RA_LOONGARCH64, SRBM_RA_LOONGARCH64);
    public static regMaskTP RBM_TP_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_TP_LOONGARCH64, SRBM_TP_LOONGARCH64);
    public static regMaskTP RBM_SP_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_SP_LOONGARCH64, SRBM_SP_LOONGARCH64);
    public static regMaskTP RBM_A0_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A0_LOONGARCH64, SRBM_A0_LOONGARCH64);
    public static regMaskTP RBM_A1_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A1_LOONGARCH64, SRBM_A1_LOONGARCH64);
    public static regMaskTP RBM_A2_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A2_LOONGARCH64, SRBM_A2_LOONGARCH64);
    public static regMaskTP RBM_A3_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A3_LOONGARCH64, SRBM_A3_LOONGARCH64);
    public static regMaskTP RBM_A4_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A4_LOONGARCH64, SRBM_A4_LOONGARCH64);
    public static regMaskTP RBM_A5_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A5_LOONGARCH64, SRBM_A5_LOONGARCH64);
    public static regMaskTP RBM_A6_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A6_LOONGARCH64, SRBM_A6_LOONGARCH64);
    public static regMaskTP RBM_A7_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_A7_LOONGARCH64, SRBM_A7_LOONGARCH64);
    public static regMaskTP RBM_T0_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T0_LOONGARCH64, SRBM_T0_LOONGARCH64);
    public static regMaskTP RBM_T1_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T1_LOONGARCH64, SRBM_T1_LOONGARCH64);
    public static regMaskTP RBM_T2_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T2_LOONGARCH64, SRBM_T2_LOONGARCH64);
    public static regMaskTP RBM_T3_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T3_LOONGARCH64, SRBM_T3_LOONGARCH64);
    public static regMaskTP RBM_T4_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T4_LOONGARCH64, SRBM_T4_LOONGARCH64);
    public static regMaskTP RBM_T5_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T5_LOONGARCH64, SRBM_T5_LOONGARCH64);
    public static regMaskTP RBM_T6_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T6_LOONGARCH64, SRBM_T6_LOONGARCH64);
    public static regMaskTP RBM_T7_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T7_LOONGARCH64, SRBM_T7_LOONGARCH64);
    public static regMaskTP RBM_T8_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_T8_LOONGARCH64, SRBM_T8_LOONGARCH64);
    public static regMaskTP RBM_X0_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_X0_LOONGARCH64, SRBM_X0_LOONGARCH64);
    public static regMaskTP RBM_FP_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_FP_LOONGARCH64, SRBM_FP_LOONGARCH64);
    public static regMaskTP RBM_S0_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S0_LOONGARCH64, SRBM_S0_LOONGARCH64);
    public static regMaskTP RBM_S1_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S1_LOONGARCH64, SRBM_S1_LOONGARCH64);
    public static regMaskTP RBM_S2_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S2_LOONGARCH64, SRBM_S2_LOONGARCH64);
    public static regMaskTP RBM_S3_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S3_LOONGARCH64, SRBM_S3_LOONGARCH64);
    public static regMaskTP RBM_S4_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S4_LOONGARCH64, SRBM_S4_LOONGARCH64);
    public static regMaskTP RBM_S5_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S5_LOONGARCH64, SRBM_S5_LOONGARCH64);
    public static regMaskTP RBM_S6_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S6_LOONGARCH64, SRBM_S6_LOONGARCH64);
    public static regMaskTP RBM_S7_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S7_LOONGARCH64, SRBM_S7_LOONGARCH64);
    public static regMaskTP RBM_S8_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_S8_LOONGARCH64, SRBM_S8_LOONGARCH64);
    public static regMaskTP RBM_R21_LOONGARCH64 => RBM_X0_LOONGARCH64;
    public static regMaskTP RBM_F0_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F0_LOONGARCH64, SRBM_F0_LOONGARCH64);
    public static regMaskTP RBM_F1_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F1_LOONGARCH64, SRBM_F1_LOONGARCH64);
    public static regMaskTP RBM_F2_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F2_LOONGARCH64, SRBM_F2_LOONGARCH64);
    public static regMaskTP RBM_F3_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F3_LOONGARCH64, SRBM_F3_LOONGARCH64);
    public static regMaskTP RBM_F4_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F4_LOONGARCH64, SRBM_F4_LOONGARCH64);
    public static regMaskTP RBM_F5_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F5_LOONGARCH64, SRBM_F5_LOONGARCH64);
    public static regMaskTP RBM_F6_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F6_LOONGARCH64, SRBM_F6_LOONGARCH64);
    public static regMaskTP RBM_F7_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F7_LOONGARCH64, SRBM_F7_LOONGARCH64);
    public static regMaskTP RBM_F8_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F8_LOONGARCH64, SRBM_F8_LOONGARCH64);
    public static regMaskTP RBM_F9_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F9_LOONGARCH64, SRBM_F9_LOONGARCH64);
    public static regMaskTP RBM_F10_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F10_LOONGARCH64, SRBM_F10_LOONGARCH64);
    public static regMaskTP RBM_F11_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F11_LOONGARCH64, SRBM_F11_LOONGARCH64);
    public static regMaskTP RBM_F12_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F12_LOONGARCH64, SRBM_F12_LOONGARCH64);
    public static regMaskTP RBM_F13_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F13_LOONGARCH64, SRBM_F13_LOONGARCH64);
    public static regMaskTP RBM_F14_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F14_LOONGARCH64, SRBM_F14_LOONGARCH64);
    public static regMaskTP RBM_F15_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F15_LOONGARCH64, SRBM_F15_LOONGARCH64);
    public static regMaskTP RBM_F16_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F16_LOONGARCH64, SRBM_F16_LOONGARCH64);
    public static regMaskTP RBM_F17_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F17_LOONGARCH64, SRBM_F17_LOONGARCH64);
    public static regMaskTP RBM_F18_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F18_LOONGARCH64, SRBM_F18_LOONGARCH64);
    public static regMaskTP RBM_F19_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F19_LOONGARCH64, SRBM_F19_LOONGARCH64);
    public static regMaskTP RBM_F20_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F20_LOONGARCH64, SRBM_F20_LOONGARCH64);
    public static regMaskTP RBM_F21_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F21_LOONGARCH64, SRBM_F21_LOONGARCH64);
    public static regMaskTP RBM_F22_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F22_LOONGARCH64, SRBM_F22_LOONGARCH64);
    public static regMaskTP RBM_F23_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F23_LOONGARCH64, SRBM_F23_LOONGARCH64);
    public static regMaskTP RBM_F24_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F24_LOONGARCH64, SRBM_F24_LOONGARCH64);
    public static regMaskTP RBM_F25_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F25_LOONGARCH64, SRBM_F25_LOONGARCH64);
    public static regMaskTP RBM_F26_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F26_LOONGARCH64, SRBM_F26_LOONGARCH64);
    public static regMaskTP RBM_F27_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F27_LOONGARCH64, SRBM_F27_LOONGARCH64);
    public static regMaskTP RBM_F28_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F28_LOONGARCH64, SRBM_F28_LOONGARCH64);
    public static regMaskTP RBM_F29_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F29_LOONGARCH64, SRBM_F29_LOONGARCH64);
    public static regMaskTP RBM_F30_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F30_LOONGARCH64, SRBM_F30_LOONGARCH64);
    public static regMaskTP RBM_F31_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_F31_LOONGARCH64, SRBM_F31_LOONGARCH64);
    public static regMaskTP RBM_STK_LOONGARCH64 => regMaskTP.CreateFromRegNum(REG_STK_LOONGARCH64, SRBM_STK_LOONGARCH64);
    public static regMaskTP RBM_R0_RISCV64 => regMaskTP.CreateFromRegNum(REG_R0_RISCV64, SRBM_R0_RISCV64);
    public static regMaskTP RBM_RA_RISCV64 => regMaskTP.CreateFromRegNum(REG_RA_RISCV64, SRBM_RA_RISCV64);
    public static regMaskTP RBM_SP_RISCV64 => regMaskTP.CreateFromRegNum(REG_SP_RISCV64, SRBM_SP_RISCV64);
    public static regMaskTP RBM_GP_RISCV64 => regMaskTP.CreateFromRegNum(REG_GP_RISCV64, SRBM_GP_RISCV64);
    public static regMaskTP RBM_TP_RISCV64 => regMaskTP.CreateFromRegNum(REG_TP_RISCV64, SRBM_TP_RISCV64);
    public static regMaskTP RBM_T0_RISCV64 => regMaskTP.CreateFromRegNum(REG_T0_RISCV64, SRBM_T0_RISCV64);
    public static regMaskTP RBM_T1_RISCV64 => regMaskTP.CreateFromRegNum(REG_T1_RISCV64, SRBM_T1_RISCV64);
    public static regMaskTP RBM_T2_RISCV64 => regMaskTP.CreateFromRegNum(REG_T2_RISCV64, SRBM_T2_RISCV64);
    public static regMaskTP RBM_FP_RISCV64 => regMaskTP.CreateFromRegNum(REG_FP_RISCV64, SRBM_FP_RISCV64);
    public static regMaskTP RBM_S1_RISCV64 => regMaskTP.CreateFromRegNum(REG_S1_RISCV64, SRBM_S1_RISCV64);
    public static regMaskTP RBM_A0_RISCV64 => regMaskTP.CreateFromRegNum(REG_A0_RISCV64, SRBM_A0_RISCV64);
    public static regMaskTP RBM_A1_RISCV64 => regMaskTP.CreateFromRegNum(REG_A1_RISCV64, SRBM_A1_RISCV64);
    public static regMaskTP RBM_A2_RISCV64 => regMaskTP.CreateFromRegNum(REG_A2_RISCV64, SRBM_A2_RISCV64);
    public static regMaskTP RBM_A3_RISCV64 => regMaskTP.CreateFromRegNum(REG_A3_RISCV64, SRBM_A3_RISCV64);
    public static regMaskTP RBM_A4_RISCV64 => regMaskTP.CreateFromRegNum(REG_A4_RISCV64, SRBM_A4_RISCV64);
    public static regMaskTP RBM_A5_RISCV64 => regMaskTP.CreateFromRegNum(REG_A5_RISCV64, SRBM_A5_RISCV64);
    public static regMaskTP RBM_A6_RISCV64 => regMaskTP.CreateFromRegNum(REG_A6_RISCV64, SRBM_A6_RISCV64);
    public static regMaskTP RBM_A7_RISCV64 => regMaskTP.CreateFromRegNum(REG_A7_RISCV64, SRBM_A7_RISCV64);
    public static regMaskTP RBM_S2_RISCV64 => regMaskTP.CreateFromRegNum(REG_S2_RISCV64, SRBM_S2_RISCV64);
    public static regMaskTP RBM_S3_RISCV64 => regMaskTP.CreateFromRegNum(REG_S3_RISCV64, SRBM_S3_RISCV64);
    public static regMaskTP RBM_S4_RISCV64 => regMaskTP.CreateFromRegNum(REG_S4_RISCV64, SRBM_S4_RISCV64);
    public static regMaskTP RBM_S5_RISCV64 => regMaskTP.CreateFromRegNum(REG_S5_RISCV64, SRBM_S5_RISCV64);
    public static regMaskTP RBM_S6_RISCV64 => regMaskTP.CreateFromRegNum(REG_S6_RISCV64, SRBM_S6_RISCV64);
    public static regMaskTP RBM_S7_RISCV64 => regMaskTP.CreateFromRegNum(REG_S7_RISCV64, SRBM_S7_RISCV64);
    public static regMaskTP RBM_S8_RISCV64 => regMaskTP.CreateFromRegNum(REG_S8_RISCV64, SRBM_S8_RISCV64);
    public static regMaskTP RBM_S9_RISCV64 => regMaskTP.CreateFromRegNum(REG_S9_RISCV64, SRBM_S9_RISCV64);
    public static regMaskTP RBM_S10_RISCV64 => regMaskTP.CreateFromRegNum(REG_S10_RISCV64, SRBM_S10_RISCV64);
    public static regMaskTP RBM_S11_RISCV64 => regMaskTP.CreateFromRegNum(REG_S11_RISCV64, SRBM_S11_RISCV64);
    public static regMaskTP RBM_T3_RISCV64 => regMaskTP.CreateFromRegNum(REG_T3_RISCV64, SRBM_T3_RISCV64);
    public static regMaskTP RBM_T4_RISCV64 => regMaskTP.CreateFromRegNum(REG_T4_RISCV64, SRBM_T4_RISCV64);
    public static regMaskTP RBM_T5_RISCV64 => regMaskTP.CreateFromRegNum(REG_T5_RISCV64, SRBM_T5_RISCV64);
    public static regMaskTP RBM_T6_RISCV64 => regMaskTP.CreateFromRegNum(REG_T6_RISCV64, SRBM_T6_RISCV64);
    public static regMaskTP RBM_R8_RISCV64 => RBM_FP_RISCV64;
    public static regMaskTP RBM_ZERO_RISCV64 => RBM_R0_RISCV64;
    public static regMaskTP RBM_FT0_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT0_RISCV64, SRBM_FT0_RISCV64);
    public static regMaskTP RBM_FT1_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT1_RISCV64, SRBM_FT1_RISCV64);
    public static regMaskTP RBM_FT2_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT2_RISCV64, SRBM_FT2_RISCV64);
    public static regMaskTP RBM_FT3_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT3_RISCV64, SRBM_FT3_RISCV64);
    public static regMaskTP RBM_FT4_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT4_RISCV64, SRBM_FT4_RISCV64);
    public static regMaskTP RBM_FT5_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT5_RISCV64, SRBM_FT5_RISCV64);
    public static regMaskTP RBM_FT6_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT6_RISCV64, SRBM_FT6_RISCV64);
    public static regMaskTP RBM_FT7_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT7_RISCV64, SRBM_FT7_RISCV64);
    public static regMaskTP RBM_FS0_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS0_RISCV64, SRBM_FS0_RISCV64);
    public static regMaskTP RBM_FS1_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS1_RISCV64, SRBM_FS1_RISCV64);
    public static regMaskTP RBM_FA0_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA0_RISCV64, SRBM_FA0_RISCV64);
    public static regMaskTP RBM_FA1_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA1_RISCV64, SRBM_FA1_RISCV64);
    public static regMaskTP RBM_FA2_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA2_RISCV64, SRBM_FA2_RISCV64);
    public static regMaskTP RBM_FA3_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA3_RISCV64, SRBM_FA3_RISCV64);
    public static regMaskTP RBM_FA4_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA4_RISCV64, SRBM_FA4_RISCV64);
    public static regMaskTP RBM_FA5_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA5_RISCV64, SRBM_FA5_RISCV64);
    public static regMaskTP RBM_FA6_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA6_RISCV64, SRBM_FA6_RISCV64);
    public static regMaskTP RBM_FA7_RISCV64 => regMaskTP.CreateFromRegNum(REG_FA7_RISCV64, SRBM_FA7_RISCV64);
    public static regMaskTP RBM_FS2_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS2_RISCV64, SRBM_FS2_RISCV64);
    public static regMaskTP RBM_FS3_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS3_RISCV64, SRBM_FS3_RISCV64);
    public static regMaskTP RBM_FS4_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS4_RISCV64, SRBM_FS4_RISCV64);
    public static regMaskTP RBM_FS5_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS5_RISCV64, SRBM_FS5_RISCV64);
    public static regMaskTP RBM_FS6_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS6_RISCV64, SRBM_FS6_RISCV64);
    public static regMaskTP RBM_FS7_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS7_RISCV64, SRBM_FS7_RISCV64);
    public static regMaskTP RBM_FS8_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS8_RISCV64, SRBM_FS8_RISCV64);
    public static regMaskTP RBM_FS9_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS9_RISCV64, SRBM_FS9_RISCV64);
    public static regMaskTP RBM_FS10_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS10_RISCV64, SRBM_FS10_RISCV64);
    public static regMaskTP RBM_FS11_RISCV64 => regMaskTP.CreateFromRegNum(REG_FS11_RISCV64, SRBM_FS11_RISCV64);
    public static regMaskTP RBM_FT8_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT8_RISCV64, SRBM_FT8_RISCV64);
    public static regMaskTP RBM_FT9_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT9_RISCV64, SRBM_FT9_RISCV64);
    public static regMaskTP RBM_FT10_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT10_RISCV64, SRBM_FT10_RISCV64);
    public static regMaskTP RBM_FT11_RISCV64 => regMaskTP.CreateFromRegNum(REG_FT11_RISCV64, SRBM_FT11_RISCV64);
    public static regMaskTP RBM_STK_RISCV64 => regMaskTP.CreateFromRegNum(REG_STK_RISCV64, SRBM_STK_RISCV64);
    public static regMaskTP RBM_STK_WASM => regMaskTP.CreateFromRegNum(REG_STK_WASM, SRBM_STK_WASM);
    public static regMaskTP RBM_EAX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_EAX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_EAX_AMD64;
            }
            throw new FatalJitException("Register mask 'EAX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_ECX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_ECX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_ECX_AMD64;
            }
            throw new FatalJitException("Register mask 'ECX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_EDX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_EDX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_EDX_AMD64;
            }
            throw new FatalJitException("Register mask 'EDX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_EBX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_EBX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_EBX_AMD64;
            }
            throw new FatalJitException("Register mask 'EBX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_ESP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_ESP_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_ESP_AMD64;
            }
            throw new FatalJitException("Register mask 'ESP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_EBP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_EBP_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_EBP_AMD64;
            }
            throw new FatalJitException("Register mask 'EBP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_ESI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_ESI_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_ESI_AMD64;
            }
            throw new FatalJitException("Register mask 'ESI' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_EDI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_EDI_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_EDI_AMD64;
            }
            throw new FatalJitException("Register mask 'EDI' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RAX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RAX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RAX_AMD64;
            }
            throw new FatalJitException("Register mask 'RAX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RCX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RCX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RCX_AMD64;
            }
            throw new FatalJitException("Register mask 'RCX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RDX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RDX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RDX_AMD64;
            }
            throw new FatalJitException("Register mask 'RDX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RBX
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RBX_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RBX_AMD64;
            }
            throw new FatalJitException("Register mask 'RBX' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RSP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RSP_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RSP_AMD64;
            }
            throw new FatalJitException("Register mask 'RSP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RBP
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RBP_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RBP_AMD64;
            }
            throw new FatalJitException("Register mask 'RBP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RSI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RSI_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RSI_AMD64;
            }
            throw new FatalJitException("Register mask 'RSI' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RDI
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_RDI_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_RDI_AMD64;
            }
            throw new FatalJitException("Register mask 'RDI' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM0
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM0_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM0_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM1
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM1_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM1_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM2
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM2_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM2_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM3
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM3_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM3_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM4
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM4_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM4_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM5
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM5_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM5_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM6
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM6_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM6_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM7
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_XMM7_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM7_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K0
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K0_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K0_AMD64;
            }
            throw new FatalJitException("Register mask 'K0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K1
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K1_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K1_AMD64;
            }
            throw new FatalJitException("Register mask 'K1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K2
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K2_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K2_AMD64;
            }
            throw new FatalJitException("Register mask 'K2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K3
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K3_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K3_AMD64;
            }
            throw new FatalJitException("Register mask 'K3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K4
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K4_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K4_AMD64;
            }
            throw new FatalJitException("Register mask 'K4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K5
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K5_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K5_AMD64;
            }
            throw new FatalJitException("Register mask 'K5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K6
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K6_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K6_AMD64;
            }
            throw new FatalJitException("Register mask 'K6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_K7
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_K7_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_K7_AMD64;
            }
            throw new FatalJitException("Register mask 'K7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_STK
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return RBM_STK_X86;
            }
            if (Configuration.TARGET_AMD64)
            {
                return RBM_STK_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_STK_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_STK_ARM64;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_STK_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_STK_RISCV64;
            }
            if (Configuration.TARGET_WASM)
            {
                return RBM_STK_WASM;
            }
            throw new FatalJitException("Register mask 'STK' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R8
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R8_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R8_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R8_ARM64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_R8_RISCV64;
            }
            throw new FatalJitException("Register mask 'R8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R9
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R9_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R9_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R9_ARM64;
            }
            throw new FatalJitException("Register mask 'R9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R10
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R10_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R10_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R10_ARM64;
            }
            throw new FatalJitException("Register mask 'R10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R11
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R11_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R11_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R11_ARM64;
            }
            throw new FatalJitException("Register mask 'R11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R12
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R12_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R12_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R12_ARM64;
            }
            throw new FatalJitException("Register mask 'R12' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R13
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R13_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R13_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R13_ARM64;
            }
            throw new FatalJitException("Register mask 'R13' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R14
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R14_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R14_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R14_ARM64;
            }
            throw new FatalJitException("Register mask 'R14' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R15
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R15_AMD64;
            }
            if (Configuration.TARGET_ARM)
            {
                return RBM_R15_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R15_ARM64;
            }
            throw new FatalJitException("Register mask 'R15' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R16
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R16_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R16_ARM64;
            }
            throw new FatalJitException("Register mask 'R16' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R17
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R17_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R17_ARM64;
            }
            throw new FatalJitException("Register mask 'R17' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R18
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R18_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R18_ARM64;
            }
            throw new FatalJitException("Register mask 'R18' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R19
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R19_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R19_ARM64;
            }
            throw new FatalJitException("Register mask 'R19' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R20
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R20_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R20_ARM64;
            }
            throw new FatalJitException("Register mask 'R20' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R21
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R21_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R21_ARM64;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_R21_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'R21' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R22
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R22_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R22_ARM64;
            }
            throw new FatalJitException("Register mask 'R22' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R23
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R23_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R23_ARM64;
            }
            throw new FatalJitException("Register mask 'R23' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R24
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R24_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R24_ARM64;
            }
            throw new FatalJitException("Register mask 'R24' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R25
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R25_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R25_ARM64;
            }
            throw new FatalJitException("Register mask 'R25' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R26
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R26_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R26_ARM64;
            }
            throw new FatalJitException("Register mask 'R26' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R27
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R27_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R27_ARM64;
            }
            throw new FatalJitException("Register mask 'R27' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R28
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R28_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R28_ARM64;
            }
            throw new FatalJitException("Register mask 'R28' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R29
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R29_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R29_ARM64;
            }
            throw new FatalJitException("Register mask 'R29' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R30
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R30_AMD64;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R30_ARM64;
            }
            throw new FatalJitException("Register mask 'R30' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R31
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_R31_AMD64;
            }
            throw new FatalJitException("Register mask 'R31' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM8
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM8_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM9
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM9_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM10
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM10_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM11
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM11_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM12
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM12_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM12' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM13
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM13_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM13' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM14
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM14_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM14' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM15
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM15_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM15' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM16
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM16_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM16' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM17
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM17_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM17' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM18
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM18_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM18' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM19
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM19_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM19' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM20
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM20_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM20' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM21
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM21_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM21' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM22
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM22_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM22' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM23
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM23_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM23' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM24
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM24_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM24' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM25
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM25_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM25' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM26
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM26_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM26' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM27
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM27_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM27' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM28
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM28_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM28' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM29
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM29_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM29' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM30
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM30_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM30' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_XMM31
    {
        get
        {
            if (Configuration.TARGET_AMD64)
            {
                return RBM_XMM31_AMD64;
            }
            throw new FatalJitException("Register mask 'XMM31' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R0
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R0_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R0_ARM64;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_R0_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_R0_RISCV64;
            }
            throw new FatalJitException("Register mask 'R0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R1
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R1_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R1_ARM64;
            }
            throw new FatalJitException("Register mask 'R1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R2
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R2_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R2_ARM64;
            }
            throw new FatalJitException("Register mask 'R2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R3
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R3_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R3_ARM64;
            }
            throw new FatalJitException("Register mask 'R3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R4
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R4_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R4_ARM64;
            }
            throw new FatalJitException("Register mask 'R4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R5
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R5_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R5_ARM64;
            }
            throw new FatalJitException("Register mask 'R5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R6
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R6_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R6_ARM64;
            }
            throw new FatalJitException("Register mask 'R6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_R7
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_R7_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_R7_ARM64;
            }
            throw new FatalJitException("Register mask 'R7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_SP
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_SP_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_SP_ARM64;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_SP_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_SP_RISCV64;
            }
            throw new FatalJitException("Register mask 'SP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_LR
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_LR_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_LR_ARM64;
            }
            throw new FatalJitException("Register mask 'LR' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_PC
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_PC_ARM;
            }
            throw new FatalJitException("Register mask 'PC' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FP
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_FP_ARM;
            }
            if (Configuration.TARGET_ARM64)
            {
                return RBM_FP_ARM64;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_FP_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FP_RISCV64;
            }
            throw new FatalJitException("Register mask 'FP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F0
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F0_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F0_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F1
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F1_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F1_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F2
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F2_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F2_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F3
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F3_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F3_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F4
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F4_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F4_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F5
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F5_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F5_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F6
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F6_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F6_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F7
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F7_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F7_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F8
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F8_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F8_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F9
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F9_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F9_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F10
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F10_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F10_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F11
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F11_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F11_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F12
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F12_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F12_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F12' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F13
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F13_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F13_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F13' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F14
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F14_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F14_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F14' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F15
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F15_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F15_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F15' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F16
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F16_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F16_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F16' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F17
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F17_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F17_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F17' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F18
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F18_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F18_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F18' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F19
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F19_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F19_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F19' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F20
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F20_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F20_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F20' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F21
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F21_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F21_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F21' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F22
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F22_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F22_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F22' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F23
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F23_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F23_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F23' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F24
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F24_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F24_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F24' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F25
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F25_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F25_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F25' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F26
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F26_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F26_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F26' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F27
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F27_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F27_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F27' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F28
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F28_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F28_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F28' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F29
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F29_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F29_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F29' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F30
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F30_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F30_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F30' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_F31
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return RBM_F31_ARM;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_F31_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'F31' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_IP0
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_IP0_ARM64;
            }
            throw new FatalJitException("Register mask 'IP0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_IP1
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_IP1_ARM64;
            }
            throw new FatalJitException("Register mask 'IP1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_PR
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_PR_ARM64;
            }
            throw new FatalJitException("Register mask 'PR' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_ZR
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_ZR_ARM64;
            }
            throw new FatalJitException("Register mask 'ZR' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V0
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V0_ARM64;
            }
            throw new FatalJitException("Register mask 'V0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V1
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V1_ARM64;
            }
            throw new FatalJitException("Register mask 'V1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V2
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V2_ARM64;
            }
            throw new FatalJitException("Register mask 'V2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V3
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V3_ARM64;
            }
            throw new FatalJitException("Register mask 'V3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V4
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V4_ARM64;
            }
            throw new FatalJitException("Register mask 'V4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V5
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V5_ARM64;
            }
            throw new FatalJitException("Register mask 'V5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V6
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V6_ARM64;
            }
            throw new FatalJitException("Register mask 'V6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V7
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V7_ARM64;
            }
            throw new FatalJitException("Register mask 'V7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V8
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V8_ARM64;
            }
            throw new FatalJitException("Register mask 'V8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V9
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V9_ARM64;
            }
            throw new FatalJitException("Register mask 'V9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V10
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V10_ARM64;
            }
            throw new FatalJitException("Register mask 'V10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V11
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V11_ARM64;
            }
            throw new FatalJitException("Register mask 'V11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V12
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V12_ARM64;
            }
            throw new FatalJitException("Register mask 'V12' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V13
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V13_ARM64;
            }
            throw new FatalJitException("Register mask 'V13' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V14
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V14_ARM64;
            }
            throw new FatalJitException("Register mask 'V14' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V15
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V15_ARM64;
            }
            throw new FatalJitException("Register mask 'V15' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V16
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V16_ARM64;
            }
            throw new FatalJitException("Register mask 'V16' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V17
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V17_ARM64;
            }
            throw new FatalJitException("Register mask 'V17' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V18
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V18_ARM64;
            }
            throw new FatalJitException("Register mask 'V18' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V19
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V19_ARM64;
            }
            throw new FatalJitException("Register mask 'V19' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V20
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V20_ARM64;
            }
            throw new FatalJitException("Register mask 'V20' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V21
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V21_ARM64;
            }
            throw new FatalJitException("Register mask 'V21' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V22
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V22_ARM64;
            }
            throw new FatalJitException("Register mask 'V22' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V23
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V23_ARM64;
            }
            throw new FatalJitException("Register mask 'V23' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V24
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V24_ARM64;
            }
            throw new FatalJitException("Register mask 'V24' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V25
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V25_ARM64;
            }
            throw new FatalJitException("Register mask 'V25' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V26
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V26_ARM64;
            }
            throw new FatalJitException("Register mask 'V26' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V27
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V27_ARM64;
            }
            throw new FatalJitException("Register mask 'V27' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V28
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V28_ARM64;
            }
            throw new FatalJitException("Register mask 'V28' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V29
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V29_ARM64;
            }
            throw new FatalJitException("Register mask 'V29' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V30
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V30_ARM64;
            }
            throw new FatalJitException("Register mask 'V30' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_V31
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_V31_ARM64;
            }
            throw new FatalJitException("Register mask 'V31' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P0
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P0_ARM64;
            }
            throw new FatalJitException("Register mask 'P0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P1
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P1_ARM64;
            }
            throw new FatalJitException("Register mask 'P1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P2
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P2_ARM64;
            }
            throw new FatalJitException("Register mask 'P2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P3
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P3_ARM64;
            }
            throw new FatalJitException("Register mask 'P3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P4
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P4_ARM64;
            }
            throw new FatalJitException("Register mask 'P4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P5
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P5_ARM64;
            }
            throw new FatalJitException("Register mask 'P5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P6
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P6_ARM64;
            }
            throw new FatalJitException("Register mask 'P6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P7
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P7_ARM64;
            }
            throw new FatalJitException("Register mask 'P7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P8
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P8_ARM64;
            }
            throw new FatalJitException("Register mask 'P8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P9
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P9_ARM64;
            }
            throw new FatalJitException("Register mask 'P9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P10
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P10_ARM64;
            }
            throw new FatalJitException("Register mask 'P10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P11
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P11_ARM64;
            }
            throw new FatalJitException("Register mask 'P11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P12
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P12_ARM64;
            }
            throw new FatalJitException("Register mask 'P12' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P13
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P13_ARM64;
            }
            throw new FatalJitException("Register mask 'P13' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P14
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P14_ARM64;
            }
            throw new FatalJitException("Register mask 'P14' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_P15
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_P15_ARM64;
            }
            throw new FatalJitException("Register mask 'P15' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FFR
    {
        get
        {
            if (Configuration.TARGET_ARM64)
            {
                return RBM_FFR_ARM64;
            }
            throw new FatalJitException("Register mask 'FFR' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_RA
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_RA_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_RA_RISCV64;
            }
            throw new FatalJitException("Register mask 'RA' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_TP
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_TP_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_TP_RISCV64;
            }
            throw new FatalJitException("Register mask 'TP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A0_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A0_RISCV64;
            }
            throw new FatalJitException("Register mask 'A0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A1
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A1_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A1_RISCV64;
            }
            throw new FatalJitException("Register mask 'A1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A2
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A2_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A2_RISCV64;
            }
            throw new FatalJitException("Register mask 'A2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A3
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A3_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A3_RISCV64;
            }
            throw new FatalJitException("Register mask 'A3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A4
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A4_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A4_RISCV64;
            }
            throw new FatalJitException("Register mask 'A4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A5
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A5_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A5_RISCV64;
            }
            throw new FatalJitException("Register mask 'A5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A6
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A6_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A6_RISCV64;
            }
            throw new FatalJitException("Register mask 'A6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_A7
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_A7_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_A7_RISCV64;
            }
            throw new FatalJitException("Register mask 'A7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T0_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T0_RISCV64;
            }
            throw new FatalJitException("Register mask 'T0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T1
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T1_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T1_RISCV64;
            }
            throw new FatalJitException("Register mask 'T1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T2
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T2_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T2_RISCV64;
            }
            throw new FatalJitException("Register mask 'T2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T3
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T3_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T3_RISCV64;
            }
            throw new FatalJitException("Register mask 'T3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T4
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T4_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T4_RISCV64;
            }
            throw new FatalJitException("Register mask 'T4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T5
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T5_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T5_RISCV64;
            }
            throw new FatalJitException("Register mask 'T5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T6
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T6_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_T6_RISCV64;
            }
            throw new FatalJitException("Register mask 'T6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T7
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T7_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'T7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_T8
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_T8_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'T8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_X0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_X0_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'X0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S0
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S0_LOONGARCH64;
            }
            throw new FatalJitException("Register mask 'S0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S1
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S1_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S1_RISCV64;
            }
            throw new FatalJitException("Register mask 'S1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S2
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S2_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S2_RISCV64;
            }
            throw new FatalJitException("Register mask 'S2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S3
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S3_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S3_RISCV64;
            }
            throw new FatalJitException("Register mask 'S3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S4
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S4_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S4_RISCV64;
            }
            throw new FatalJitException("Register mask 'S4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S5
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S5_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S5_RISCV64;
            }
            throw new FatalJitException("Register mask 'S5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S6
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S6_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S6_RISCV64;
            }
            throw new FatalJitException("Register mask 'S6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S7
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S7_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S7_RISCV64;
            }
            throw new FatalJitException("Register mask 'S7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S8
    {
        get
        {
            if (Configuration.TARGET_LOONGARCH64)
            {
                return RBM_S8_LOONGARCH64;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S8_RISCV64;
            }
            throw new FatalJitException("Register mask 'S8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_GP
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_GP_RISCV64;
            }
            throw new FatalJitException("Register mask 'GP' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S9
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S9_RISCV64;
            }
            throw new FatalJitException("Register mask 'S9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S10
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S10_RISCV64;
            }
            throw new FatalJitException("Register mask 'S10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_S11
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_S11_RISCV64;
            }
            throw new FatalJitException("Register mask 'S11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_ZERO
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_ZERO_RISCV64;
            }
            throw new FatalJitException("Register mask 'ZERO' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT0
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT0_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT1
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT1_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT2
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT2_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT3
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT3_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT4
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT4_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT5
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT5_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT6
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT6_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT7
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT7_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS0
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS0_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS1
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS1_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA0
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA0_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA0' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA1
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA1_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA1' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA2
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA2_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA3
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA3_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA4
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA4_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA5
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA5_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA6
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA6_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FA7
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FA7_RISCV64;
            }
            throw new FatalJitException("Register mask 'FA7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS2
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS2_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS2' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS3
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS3_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS3' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS4
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS4_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS4' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS5
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS5_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS5' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS6
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS6_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS6' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS7
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS7_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS7' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS8
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS8_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS9
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS9_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS10
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS10_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FS11
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FS11_RISCV64;
            }
            throw new FatalJitException("Register mask 'FS11' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT8
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT8_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT8' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT9
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT9_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT9' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT10
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT10_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT10' is not available for the configured target.");
        }
    }
    public static regMaskTP RBM_FT11
    {
        get
        {
            if (Configuration.TARGET_RISCV64)
            {
                return RBM_FT11_RISCV64;
            }
            throw new FatalJitException("Register mask 'FT11' is not available for the configured target.");
        }
    }

}