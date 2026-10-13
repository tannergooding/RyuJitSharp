// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regNumber REG_A0 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A0 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A0));

    public static regNumber REG_A1 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A1 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A1));

    public static regNumber REG_A2 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A2 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A2));

    public static regNumber REG_A3 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A3 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A3));

    public static regNumber REG_A4 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A4 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A4));

    public static regNumber REG_A5 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A5 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A5));

    public static regNumber REG_A6 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A6 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A6));

    public static regNumber REG_A7 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_A7 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_A7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_A7));

    public static regNumber REG_EAX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_EAX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_EAX :
        UnsupportedTargetValue<regNumber>(nameof(REG_EAX));

    public static regNumber REG_EBP =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_EBP :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_EBP :
        UnsupportedTargetValue<regNumber>(nameof(REG_EBP));

    public static regNumber REG_EBX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_EBX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_EBX :
        UnsupportedTargetValue<regNumber>(nameof(REG_EBX));

    public static regNumber REG_ECX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_ECX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_ECX :
        UnsupportedTargetValue<regNumber>(nameof(REG_ECX));

    public static regNumber REG_EDI =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_EDI :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_EDI :
        UnsupportedTargetValue<regNumber>(nameof(REG_EDI));

    public static regNumber REG_EDX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_EDX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_EDX :
        UnsupportedTargetValue<regNumber>(nameof(REG_EDX));

    public static regNumber REG_ESI =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_ESI :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_ESI :
        UnsupportedTargetValue<regNumber>(nameof(REG_ESI));

    public static regNumber REG_ESP =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_ESP :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_ESP :
        UnsupportedTargetValue<regNumber>(nameof(REG_ESP));

    public static regNumber REG_F0 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F0 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F0));

    public static regNumber REG_F1 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F1 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F1));

    public static regNumber REG_F10 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F10 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F10));

    public static regNumber REG_F11 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F11 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F11));

    public static regNumber REG_F12 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F12 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F12 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F12));

    public static regNumber REG_F13 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F13 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F13 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F13));

    public static regNumber REG_F14 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F14 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F14 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F14));

    public static regNumber REG_F15 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F15 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F15 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F15));

    public static regNumber REG_F16 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F16 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F16 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F16));

    public static regNumber REG_F17 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F17 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F17 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F17));

    public static regNumber REG_F18 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F18 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F18 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F18));

    public static regNumber REG_F19 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F19 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F19 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F19));

    public static regNumber REG_F2 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F2 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F2));

    public static regNumber REG_F20 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F20 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F20 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F20));

    public static regNumber REG_F21 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F21 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F21 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F21));

    public static regNumber REG_F22 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F22 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F22 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F22));

    public static regNumber REG_F23 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F23 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F23 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F23));

    public static regNumber REG_F24 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F24 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F24 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F24));

    public static regNumber REG_F25 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F25 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F25 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F25));

    public static regNumber REG_F26 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F26 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F26 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F26));

    public static regNumber REG_F27 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F27 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F27 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F27));

    public static regNumber REG_F28 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F28 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F28 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F28));

    public static regNumber REG_F29 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F29 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F29 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F29));

    public static regNumber REG_F3 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F3 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F3));

    public static regNumber REG_F30 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F30 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F30 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F30));

    public static regNumber REG_F31 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F31 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F31 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F31));

    public static regNumber REG_F4 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F4 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F4));

    public static regNumber REG_F5 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F5 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F5));

    public static regNumber REG_F6 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F6 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F6));

    public static regNumber REG_F7 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F7 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F7));

    public static regNumber REG_F8 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F8 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F8));

    public static regNumber REG_F9 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_F9 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_F9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_F9));

    public static regNumber REG_FA0 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA0));

    public static regNumber REG_FA1 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA1));

    public static regNumber REG_FA2 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA2));

    public static regNumber REG_FA3 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA3));

    public static regNumber REG_FA4 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA4));

    public static regNumber REG_FA5 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA5));

    public static regNumber REG_FA6 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA6));

    public static regNumber REG_FA7 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FA7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FA7));

    public static regNumber REG_FFR =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_FFR :
        UnsupportedTargetValue<regNumber>(nameof(REG_FFR));

    public static regNumber REG_FP =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_FP :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_FP :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_FP :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FP :
        UnsupportedTargetValue<regNumber>(nameof(REG_FP));

    public static regNumber REG_FS0 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS0));

    public static regNumber REG_FS1 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS1));

    public static regNumber REG_FS10 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS10));

    public static regNumber REG_FS11 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS11));

    public static regNumber REG_FS2 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS2));

    public static regNumber REG_FS3 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS3));

    public static regNumber REG_FS4 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS4));

    public static regNumber REG_FS5 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS5));

    public static regNumber REG_FS6 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS6));

    public static regNumber REG_FS7 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS7));

    public static regNumber REG_FS8 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS8));

    public static regNumber REG_FS9 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FS9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FS9));

    public static regNumber REG_FT0 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT0));

    public static regNumber REG_FT1 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT1));

    public static regNumber REG_FT10 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT10));

    public static regNumber REG_FT11 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT11));

    public static regNumber REG_FT2 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT2));

    public static regNumber REG_FT3 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT3));

    public static regNumber REG_FT4 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT4));

    public static regNumber REG_FT5 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT5));

    public static regNumber REG_FT6 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT6));

    public static regNumber REG_FT7 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT7));

    public static regNumber REG_FT8 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT8));

    public static regNumber REG_FT9 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_FT9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_FT9));

    public static regNumber REG_GP =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_GP :
        UnsupportedTargetValue<regNumber>(nameof(REG_GP));

    public static regNumber REG_IP0 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_IP0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_IP0));

    public static regNumber REG_IP1 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_IP1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_IP1));

    public static regNumber REG_K0 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K0 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K0));

    public static regNumber REG_K1 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K1 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K1));

    public static regNumber REG_K2 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K2 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K2));

    public static regNumber REG_K3 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K3 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K3));

    public static regNumber REG_K4 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K4 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K4));

    public static regNumber REG_K5 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K5 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K5));

    public static regNumber REG_K6 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K6 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K6));

    public static regNumber REG_K7 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_K7 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_K7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_K7));

    public static regNumber REG_LR =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_LR :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_LR :
        UnsupportedTargetValue<regNumber>(nameof(REG_LR));

    public static regNumber REG_P0 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P0));

    public static regNumber REG_P1 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P1));

    public static regNumber REG_P10 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P10));

    public static regNumber REG_P11 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P11));

    public static regNumber REG_P12 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P12 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P12));

    public static regNumber REG_P13 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P13 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P13));

    public static regNumber REG_P14 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P14 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P14));

    public static regNumber REG_P15 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P15 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P15));

    public static regNumber REG_P2 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P2));

    public static regNumber REG_P3 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P3));

    public static regNumber REG_P4 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P4));

    public static regNumber REG_P5 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P5));

    public static regNumber REG_P6 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P6));

    public static regNumber REG_P7 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P7));

    public static regNumber REG_P8 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P8));

    public static regNumber REG_P9 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_P9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_P9));

    public static regNumber REG_PC =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_PC :
        UnsupportedTargetValue<regNumber>(nameof(REG_PC));

    public static regNumber REG_PR =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_PR :
        UnsupportedTargetValue<regNumber>(nameof(REG_PR));

    public static regNumber REG_R0 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R0 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R0 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_R0 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_R0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R0));

    public static regNumber REG_R1 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R1 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R1));

    public static regNumber REG_R10 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R10 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R10 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R10));

    public static regNumber REG_R11 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R11 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R11 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R11));

    public static regNumber REG_R12 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R12 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R12 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R12 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R12));

    public static regNumber REG_R13 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R13 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R13 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R13 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R13));

    public static regNumber REG_R14 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R14 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R14 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R14 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R14));

    public static regNumber REG_R15 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R15 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R15 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R15 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R15));

    public static regNumber REG_R16 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R16 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R16 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R16));

    public static regNumber REG_R17 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R17 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R17 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R17));

    public static regNumber REG_R18 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R18 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R18 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R18));

    public static regNumber REG_R19 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R19 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R19 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R19));

    public static regNumber REG_R2 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R2 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R2));

    public static regNumber REG_R20 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R20 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R20 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R20));

    public static regNumber REG_R21 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R21 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R21 :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_R21 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R21));

    public static regNumber REG_R22 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R22 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R22 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R22));

    public static regNumber REG_R23 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R23 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R23 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R23));

    public static regNumber REG_R24 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R24 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R24 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R24));

    public static regNumber REG_R25 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R25 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R25 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R25));

    public static regNumber REG_R26 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R26 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R26 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R26));

    public static regNumber REG_R27 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R27 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R27 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R27));

    public static regNumber REG_R28 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R28 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R28 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R28));

    public static regNumber REG_R29 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R29 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R29 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R29));

    public static regNumber REG_R3 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R3 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R3));

    public static regNumber REG_R30 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R30 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R30 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R30));

    public static regNumber REG_R31 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R31 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R31));

    public static regNumber REG_R4 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R4 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R4));

    public static regNumber REG_R5 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R5 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R5));

    public static regNumber REG_R6 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R6 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R6));

    public static regNumber REG_R7 =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R7 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R7));

    public static regNumber REG_R8 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R8 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R8 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R8 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_R8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R8));

    public static regNumber REG_R9 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_R9 :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_R9 :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_R9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_R9));

    public static regNumber REG_RA =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_RA :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_RA :
        UnsupportedTargetValue<regNumber>(nameof(REG_RA));

    public static regNumber REG_RAX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RAX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RAX :
        UnsupportedTargetValue<regNumber>(nameof(REG_RAX));

    public static regNumber REG_RBP =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RBP :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RBP :
        UnsupportedTargetValue<regNumber>(nameof(REG_RBP));

    public static regNumber REG_RBX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RBX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RBX :
        UnsupportedTargetValue<regNumber>(nameof(REG_RBX));

    public static regNumber REG_RCX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RCX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RCX :
        UnsupportedTargetValue<regNumber>(nameof(REG_RCX));

    public static regNumber REG_RDI =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RDI :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RDI :
        UnsupportedTargetValue<regNumber>(nameof(REG_RDI));

    public static regNumber REG_RDX =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RDX :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RDX :
        UnsupportedTargetValue<regNumber>(nameof(REG_RDX));

    public static regNumber REG_RSI =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RSI :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RSI :
        UnsupportedTargetValue<regNumber>(nameof(REG_RSI));

    public static regNumber REG_RSP =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_RSP :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_RSP :
        UnsupportedTargetValue<regNumber>(nameof(REG_RSP));

    public static regNumber REG_S0 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S0));

    public static regNumber REG_S1 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S1 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S1));

    public static regNumber REG_S10 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S10));

    public static regNumber REG_S11 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S11));

    public static regNumber REG_S2 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S2 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S2));

    public static regNumber REG_S3 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S3 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S3));

    public static regNumber REG_S4 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S4 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S4));

    public static regNumber REG_S5 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S5 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S5));

    public static regNumber REG_S6 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S6 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S6));

    public static regNumber REG_S7 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S7 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S7));

    public static regNumber REG_S8 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_S8 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S8));

    public static regNumber REG_S9 =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_S9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_S9));

    public static regNumber REG_SP =>
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_SP :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_SP :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_SP :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_SP :
        UnsupportedTargetValue<regNumber>(nameof(REG_SP));

    public static regNumber REG_STK =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_STK :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_STK :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_STK :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_STK :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_STK :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_STK :
        Configuration.TARGET_WASM ? regNumber.REG_WASM_STK :
        UnsupportedTargetValue<regNumber>(nameof(REG_STK));

    public static regNumber REG_T0 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T0 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T0));

    public static regNumber REG_T1 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T1 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T1));

    public static regNumber REG_T2 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T2 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T2));

    public static regNumber REG_T3 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T3 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T3));

    public static regNumber REG_T4 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T4 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T4));

    public static regNumber REG_T5 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T5 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T5));

    public static regNumber REG_T6 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T6 :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_T6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T6));

    public static regNumber REG_T7 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T7));

    public static regNumber REG_T8 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_T8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_T8));

    public static regNumber REG_TP =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_TP :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_TP :
        UnsupportedTargetValue<regNumber>(nameof(REG_TP));

    public static regNumber REG_V0 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V0));

    public static regNumber REG_V1 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V1));

    public static regNumber REG_V10 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V10));

    public static regNumber REG_V11 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V11));

    public static regNumber REG_V12 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V12 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V12));

    public static regNumber REG_V13 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V13 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V13));

    public static regNumber REG_V14 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V14 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V14));

    public static regNumber REG_V15 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V15 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V15));

    public static regNumber REG_V16 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V16 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V16));

    public static regNumber REG_V17 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V17 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V17));

    public static regNumber REG_V18 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V18 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V18));

    public static regNumber REG_V19 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V19 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V19));

    public static regNumber REG_V2 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V2));

    public static regNumber REG_V20 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V20 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V20));

    public static regNumber REG_V21 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V21 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V21));

    public static regNumber REG_V22 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V22 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V22));

    public static regNumber REG_V23 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V23 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V23));

    public static regNumber REG_V24 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V24 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V24));

    public static regNumber REG_V25 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V25 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V25));

    public static regNumber REG_V26 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V26 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V26));

    public static regNumber REG_V27 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V27 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V27));

    public static regNumber REG_V28 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V28 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V28));

    public static regNumber REG_V29 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V29 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V29));

    public static regNumber REG_V3 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V3));

    public static regNumber REG_V30 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V30 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V30));

    public static regNumber REG_V31 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V31 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V31));

    public static regNumber REG_V4 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V4));

    public static regNumber REG_V5 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V5));

    public static regNumber REG_V6 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V6));

    public static regNumber REG_V7 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V7));

    public static regNumber REG_V8 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V8));

    public static regNumber REG_V9 =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_V9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_V9));

    public static regNumber REG_X0 =>
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_X0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_X0));

    public static regNumber REG_XMM0 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM0 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM0 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM0));

    public static regNumber REG_XMM1 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM1 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM1 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM1));

    public static regNumber REG_XMM10 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM10 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM10));

    public static regNumber REG_XMM11 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM11 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM11));

    public static regNumber REG_XMM12 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM12 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM12));

    public static regNumber REG_XMM13 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM13 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM13));

    public static regNumber REG_XMM14 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM14 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM14));

    public static regNumber REG_XMM15 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM15 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM15));

    public static regNumber REG_XMM16 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM16 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM16));

    public static regNumber REG_XMM17 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM17 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM17));

    public static regNumber REG_XMM18 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM18 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM18));

    public static regNumber REG_XMM19 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM19 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM19));

    public static regNumber REG_XMM2 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM2 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM2 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM2));

    public static regNumber REG_XMM20 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM20 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM20));

    public static regNumber REG_XMM21 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM21 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM21));

    public static regNumber REG_XMM22 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM22 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM22));

    public static regNumber REG_XMM23 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM23 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM23));

    public static regNumber REG_XMM24 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM24 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM24));

    public static regNumber REG_XMM25 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM25 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM25));

    public static regNumber REG_XMM26 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM26 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM26));

    public static regNumber REG_XMM27 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM27 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM27));

    public static regNumber REG_XMM28 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM28 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM28));

    public static regNumber REG_XMM29 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM29 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM29));

    public static regNumber REG_XMM3 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM3 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM3 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM3));

    public static regNumber REG_XMM30 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM30 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM30));

    public static regNumber REG_XMM31 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM31 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM31));

    public static regNumber REG_XMM4 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM4 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM4 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM4));

    public static regNumber REG_XMM5 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM5 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM5 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM5));

    public static regNumber REG_XMM6 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM6 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM6 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM6));

    public static regNumber REG_XMM7 =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_XMM7 :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM7 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM7));

    public static regNumber REG_XMM8 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM8 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM8));

    public static regNumber REG_XMM9 =>
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_XMM9 :
        UnsupportedTargetValue<regNumber>(nameof(REG_XMM9));

    public static regNumber REG_ZERO =>
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_ZERO :
        UnsupportedTargetValue<regNumber>(nameof(REG_ZERO));

    public static regNumber REG_ZR =>
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_ZR :
        UnsupportedTargetValue<regNumber>(nameof(REG_ZR));

    public static regNumber REG_COUNT =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_COUNT :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_COUNT :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_COUNT :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_COUNT :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_COUNT :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_COUNT :
        Configuration.TARGET_WASM ? regNumber.REG_WASM_COUNT :
        UnsupportedTargetValue<regNumber>(nameof(REG_COUNT));

    public static regNumber REG_NA =>
        Configuration.TARGET_X86 ? regNumber.REG_X86_NA :
        Configuration.TARGET_AMD64 ? regNumber.REG_X64_NA :
        Configuration.TARGET_ARM ? regNumber.REG_ARM32_NA :
        Configuration.TARGET_ARM64 ? regNumber.REG_ARM64_NA :
        Configuration.TARGET_LOONGARCH64 ? regNumber.REG_LOONGARCH64_NA :
        Configuration.TARGET_RISCV64 ? regNumber.REG_RISCV64_NA :
        Configuration.TARGET_WASM ? regNumber.REG_WASM_NA :
        UnsupportedTargetValue<regNumber>(nameof(REG_NA));

    public static regNumber ACTUAL_REG_COUNT =>
        Configuration.TARGET_X86 ? regNumber.ACTUAL_REG_X86_COUNT :
        Configuration.TARGET_AMD64 ? regNumber.ACTUAL_REG_X64_COUNT :
        Configuration.TARGET_ARM ? regNumber.ACTUAL_REG_ARM32_COUNT :
        Configuration.TARGET_ARM64 ? regNumber.ACTUAL_REG_ARM64_COUNT :
        Configuration.TARGET_LOONGARCH64 ? regNumber.ACTUAL_REG_LOONGARCH64_COUNT :
        Configuration.TARGET_RISCV64 ? regNumber.ACTUAL_REG_RISCV64_COUNT :
        Configuration.TARGET_WASM ? regNumber.ACTUAL_REG_WASM_COUNT :
        UnsupportedTargetValue<regNumber>(nameof(ACTUAL_REG_COUNT));

}