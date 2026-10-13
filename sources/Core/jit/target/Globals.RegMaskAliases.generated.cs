// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regMask SRBM_A0 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A0 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A0));

    public static regMask SRBM_A1 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A1 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A1));

    public static regMask SRBM_A2 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A2 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A2));

    public static regMask SRBM_A3 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A3 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A3));

    public static regMask SRBM_A4 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A4 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A4));

    public static regMask SRBM_A5 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A5 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A5));

    public static regMask SRBM_A6 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A6 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A6));

    public static regMask SRBM_A7 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_A7 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_A7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_A7));

    public static regMask SRBM_EAX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_EAX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_EAX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_EAX));

    public static regMask SRBM_EBP =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_EBP :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_EBP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_EBP));

    public static regMask SRBM_EBX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_EBX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_EBX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_EBX));

    public static regMask SRBM_ECX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_ECX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_ECX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_ECX));

    public static regMask SRBM_EDI =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_EDI :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_EDI :
        UnsupportedTargetValue<regMask>(nameof(SRBM_EDI));

    public static regMask SRBM_EDX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_EDX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_EDX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_EDX));

    public static regMask SRBM_ESI =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_ESI :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_ESI :
        UnsupportedTargetValue<regMask>(nameof(SRBM_ESI));

    public static regMask SRBM_ESP =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_ESP :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_ESP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_ESP));

    public static regMask SRBM_F0 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F0 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F0));

    public static regMask SRBM_F1 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F1 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F1));

    public static regMask SRBM_F10 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F10 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F10));

    public static regMask SRBM_F11 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F11 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F11));

    public static regMask SRBM_F12 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F12 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F12 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F12));

    public static regMask SRBM_F13 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F13 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F13 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F13));

    public static regMask SRBM_F14 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F14 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F14 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F14));

    public static regMask SRBM_F15 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F15 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F15 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F15));

    public static regMask SRBM_F16 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F16 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F16 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F16));

    public static regMask SRBM_F17 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F17 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F17 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F17));

    public static regMask SRBM_F18 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F18 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F18 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F18));

    public static regMask SRBM_F19 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F19 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F19 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F19));

    public static regMask SRBM_F2 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F2 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F2));

    public static regMask SRBM_F20 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F20 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F20 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F20));

    public static regMask SRBM_F21 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F21 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F21 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F21));

    public static regMask SRBM_F22 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F22 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F22 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F22));

    public static regMask SRBM_F23 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F23 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F23 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F23));

    public static regMask SRBM_F24 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F24 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F24 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F24));

    public static regMask SRBM_F25 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F25 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F25 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F25));

    public static regMask SRBM_F26 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F26 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F26 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F26));

    public static regMask SRBM_F27 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F27 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F27 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F27));

    public static regMask SRBM_F28 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F28 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F28 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F28));

    public static regMask SRBM_F29 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F29 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F29 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F29));

    public static regMask SRBM_F3 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F3 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F3));

    public static regMask SRBM_F30 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F30 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F30 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F30));

    public static regMask SRBM_F31 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F31 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F31 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F31));

    public static regMask SRBM_F4 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F4 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F4));

    public static regMask SRBM_F5 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F5 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F5));

    public static regMask SRBM_F6 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F6 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F6));

    public static regMask SRBM_F7 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F7 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F7));

    public static regMask SRBM_F8 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F8 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F8));

    public static regMask SRBM_F9 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_F9 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_F9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_F9));

    public static regMask SRBM_FA0 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA0));

    public static regMask SRBM_FA1 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA1));

    public static regMask SRBM_FA2 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA2));

    public static regMask SRBM_FA3 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA3));

    public static regMask SRBM_FA4 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA4));

    public static regMask SRBM_FA5 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA5));

    public static regMask SRBM_FA6 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA6));

    public static regMask SRBM_FA7 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FA7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FA7));

    public static regMask SRBM_FFR =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_FFR :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FFR));

    public static regMask SRBM_FP =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_FP :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_FP :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_FP :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FP));

    public static regMask SRBM_FS0 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS0));

    public static regMask SRBM_FS1 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS1));

    public static regMask SRBM_FS10 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS10));

    public static regMask SRBM_FS11 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS11));

    public static regMask SRBM_FS2 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS2));

    public static regMask SRBM_FS3 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS3));

    public static regMask SRBM_FS4 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS4));

    public static regMask SRBM_FS5 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS5));

    public static regMask SRBM_FS6 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS6));

    public static regMask SRBM_FS7 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS7));

    public static regMask SRBM_FS8 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS8));

    public static regMask SRBM_FS9 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FS9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FS9));

    public static regMask SRBM_FT0 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT0));

    public static regMask SRBM_FT1 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT1));

    public static regMask SRBM_FT10 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT10));

    public static regMask SRBM_FT11 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT11));

    public static regMask SRBM_FT2 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT2));

    public static regMask SRBM_FT3 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT3));

    public static regMask SRBM_FT4 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT4));

    public static regMask SRBM_FT5 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT5));

    public static regMask SRBM_FT6 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT6));

    public static regMask SRBM_FT7 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT7));

    public static regMask SRBM_FT8 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT8));

    public static regMask SRBM_FT9 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_FT9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_FT9));

    public static regMask SRBM_GP =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_GP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_GP));

    public static regMask SRBM_IP0 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_IP0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_IP0));

    public static regMask SRBM_IP1 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_IP1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_IP1));

    public static regMask SRBM_K0 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K0 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K0));

    public static regMask SRBM_K1 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K1 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K1));

    public static regMask SRBM_K2 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K2 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K2));

    public static regMask SRBM_K3 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K3 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K3));

    public static regMask SRBM_K4 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K4 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K4));

    public static regMask SRBM_K5 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K5 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K5));

    public static regMask SRBM_K6 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K6 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K6));

    public static regMask SRBM_K7 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_K7 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_K7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_K7));

    public static regMask SRBM_LR =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_LR :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_LR :
        UnsupportedTargetValue<regMask>(nameof(SRBM_LR));

    public static regMask SRBM_P0 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P0));

    public static regMask SRBM_P1 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P1));

    public static regMask SRBM_P10 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P10));

    public static regMask SRBM_P11 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P11));

    public static regMask SRBM_P12 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P12 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P12));

    public static regMask SRBM_P13 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P13 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P13));

    public static regMask SRBM_P14 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P14 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P14));

    public static regMask SRBM_P15 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P15 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P15));

    public static regMask SRBM_P2 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P2));

    public static regMask SRBM_P3 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P3));

    public static regMask SRBM_P4 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P4));

    public static regMask SRBM_P5 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P5));

    public static regMask SRBM_P6 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P6));

    public static regMask SRBM_P7 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P7));

    public static regMask SRBM_P8 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P8));

    public static regMask SRBM_P9 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_P9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_P9));

    public static regMask SRBM_PC =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_PC :
        UnsupportedTargetValue<regMask>(nameof(SRBM_PC));

    public static regMask SRBM_PR =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_PR :
        UnsupportedTargetValue<regMask>(nameof(SRBM_PR));

    public static regMask SRBM_R0 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R0 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R0 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_R0 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_R0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R0));

    public static regMask SRBM_R1 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R1 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R1));

    public static regMask SRBM_R10 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R10 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R10 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R10));

    public static regMask SRBM_R11 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R11 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R11 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R11));

    public static regMask SRBM_R12 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R12 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R12 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R12 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R12));

    public static regMask SRBM_R13 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R13 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R13 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R13 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R13));

    public static regMask SRBM_R14 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R14 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R14 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R14 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R14));

    public static regMask SRBM_R15 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R15 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R15 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R15 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R15));

    public static regMask SRBM_R16 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R16 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R16 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R16));

    public static regMask SRBM_R17 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R17 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R17 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R17));

    public static regMask SRBM_R18 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R18 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R18 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R18));

    public static regMask SRBM_R19 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R19 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R19 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R19));

    public static regMask SRBM_R2 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R2 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R2));

    public static regMask SRBM_R20 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R20 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R20 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R20));

    public static regMask SRBM_R21 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R21 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R21 :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_R21 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R21));

    public static regMask SRBM_R22 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R22 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R22 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R22));

    public static regMask SRBM_R23 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R23 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R23 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R23));

    public static regMask SRBM_R24 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R24 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R24 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R24));

    public static regMask SRBM_R25 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R25 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R25 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R25));

    public static regMask SRBM_R26 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R26 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R26 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R26));

    public static regMask SRBM_R27 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R27 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R27 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R27));

    public static regMask SRBM_R28 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R28 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R28 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R28));

    public static regMask SRBM_R29 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R29 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R29 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R29));

    public static regMask SRBM_R3 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R3 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R3));

    public static regMask SRBM_R30 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R30 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R30 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R30));

    public static regMask SRBM_R31 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R31 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R31));

    public static regMask SRBM_R4 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R4 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R4));

    public static regMask SRBM_R5 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R5 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R5));

    public static regMask SRBM_R6 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R6 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R6));

    public static regMask SRBM_R7 =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R7 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R7));

    public static regMask SRBM_R8 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R8 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R8 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R8 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_R8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R8));

    public static regMask SRBM_R9 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_R9 :
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_R9 :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_R9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_R9));

    public static regMask SRBM_RA =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_RA :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_RA :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RA));

    public static regMask SRBM_RAX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RAX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RAX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RAX));

    public static regMask SRBM_RBP =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RBP :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RBP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RBP));

    public static regMask SRBM_RBX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RBX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RBX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RBX));

    public static regMask SRBM_RCX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RCX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RCX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RCX));

    public static regMask SRBM_RDI =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RDI :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RDI :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RDI));

    public static regMask SRBM_RDX =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RDX :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RDX :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RDX));

    public static regMask SRBM_RSI =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RSI :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RSI :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RSI));

    public static regMask SRBM_RSP =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_RSP :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_RSP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_RSP));

    public static regMask SRBM_S0 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S0));

    public static regMask SRBM_S1 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S1 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S1));

    public static regMask SRBM_S10 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S10));

    public static regMask SRBM_S11 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S11));

    public static regMask SRBM_S2 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S2 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S2));

    public static regMask SRBM_S3 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S3 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S3));

    public static regMask SRBM_S4 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S4 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S4));

    public static regMask SRBM_S5 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S5 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S5));

    public static regMask SRBM_S6 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S6 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S6));

    public static regMask SRBM_S7 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S7 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S7));

    public static regMask SRBM_S8 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_S8 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S8));

    public static regMask SRBM_S9 =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_S9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_S9));

    public static regMask SRBM_SP =>
        Configuration.TARGET_ARM ? regMask.SRBM_ARM32_SP :
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_SP :
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_SP :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_SP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_SP));

    public static regMask SRBM_T0 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T0 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T0));

    public static regMask SRBM_T1 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T1 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T1));

    public static regMask SRBM_T2 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T2 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T2));

    public static regMask SRBM_T3 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T3 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T3));

    public static regMask SRBM_T4 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T4 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T4));

    public static regMask SRBM_T5 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T5 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T5));

    public static regMask SRBM_T6 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T6 :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_T6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T6));

    public static regMask SRBM_T7 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T7));

    public static regMask SRBM_T8 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_T8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_T8));

    public static regMask SRBM_TP =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_TP :
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_TP :
        UnsupportedTargetValue<regMask>(nameof(SRBM_TP));

    public static regMask SRBM_V0 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V0));

    public static regMask SRBM_V1 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V1));

    public static regMask SRBM_V10 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V10));

    public static regMask SRBM_V11 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V11));

    public static regMask SRBM_V12 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V12 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V12));

    public static regMask SRBM_V13 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V13 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V13));

    public static regMask SRBM_V14 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V14 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V14));

    public static regMask SRBM_V15 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V15 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V15));

    public static regMask SRBM_V16 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V16 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V16));

    public static regMask SRBM_V17 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V17 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V17));

    public static regMask SRBM_V18 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V18 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V18));

    public static regMask SRBM_V19 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V19 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V19));

    public static regMask SRBM_V2 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V2));

    public static regMask SRBM_V20 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V20 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V20));

    public static regMask SRBM_V21 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V21 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V21));

    public static regMask SRBM_V22 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V22 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V22));

    public static regMask SRBM_V23 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V23 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V23));

    public static regMask SRBM_V24 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V24 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V24));

    public static regMask SRBM_V25 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V25 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V25));

    public static regMask SRBM_V26 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V26 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V26));

    public static regMask SRBM_V27 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V27 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V27));

    public static regMask SRBM_V28 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V28 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V28));

    public static regMask SRBM_V29 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V29 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V29));

    public static regMask SRBM_V3 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V3));

    public static regMask SRBM_V30 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V30 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V30));

    public static regMask SRBM_V31 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V31 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V31));

    public static regMask SRBM_V4 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V4));

    public static regMask SRBM_V5 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V5));

    public static regMask SRBM_V6 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V6));

    public static regMask SRBM_V7 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V7));

    public static regMask SRBM_V8 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V8));

    public static regMask SRBM_V9 =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_V9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_V9));

    public static regMask SRBM_X0 =>
        Configuration.TARGET_LOONGARCH64 ? regMask.SRBM_LOONGARCH64_X0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_X0));

    public static regMask SRBM_XMM0 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM0 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM0 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM0));

    public static regMask SRBM_XMM1 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM1 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM1 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM1));

    public static regMask SRBM_XMM10 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM10 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM10));

    public static regMask SRBM_XMM11 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM11 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM11));

    public static regMask SRBM_XMM12 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM12 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM12));

    public static regMask SRBM_XMM13 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM13 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM13));

    public static regMask SRBM_XMM14 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM14 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM14));

    public static regMask SRBM_XMM15 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM15 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM15));

    public static regMask SRBM_XMM16 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM16 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM16));

    public static regMask SRBM_XMM17 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM17 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM17));

    public static regMask SRBM_XMM18 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM18 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM18));

    public static regMask SRBM_XMM19 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM19 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM19));

    public static regMask SRBM_XMM2 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM2 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM2 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM2));

    public static regMask SRBM_XMM20 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM20 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM20));

    public static regMask SRBM_XMM21 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM21 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM21));

    public static regMask SRBM_XMM22 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM22 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM22));

    public static regMask SRBM_XMM23 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM23 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM23));

    public static regMask SRBM_XMM24 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM24 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM24));

    public static regMask SRBM_XMM25 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM25 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM25));

    public static regMask SRBM_XMM26 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM26 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM26));

    public static regMask SRBM_XMM27 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM27 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM27));

    public static regMask SRBM_XMM28 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM28 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM28));

    public static regMask SRBM_XMM29 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM29 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM29));

    public static regMask SRBM_XMM3 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM3 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM3 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM3));

    public static regMask SRBM_XMM30 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM30 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM30));

    public static regMask SRBM_XMM31 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM31 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM31));

    public static regMask SRBM_XMM4 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM4 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM4 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM4));

    public static regMask SRBM_XMM5 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM5 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM5 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM5));

    public static regMask SRBM_XMM6 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM6 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM6 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM6));

    public static regMask SRBM_XMM7 =>
        Configuration.TARGET_X86 ? regMask.SRBM_X86_XMM7 :
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM7 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM7));

    public static regMask SRBM_XMM8 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM8 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM8));

    public static regMask SRBM_XMM9 =>
        Configuration.TARGET_AMD64 ? regMask.SRBM_X64_XMM9 :
        UnsupportedTargetValue<regMask>(nameof(SRBM_XMM9));

    public static regMask SRBM_ZERO =>
        Configuration.TARGET_RISCV64 ? regMask.SRBM_RISCV64_ZERO :
        UnsupportedTargetValue<regMask>(nameof(SRBM_ZERO));

    public static regMask SRBM_ZR =>
        Configuration.TARGET_ARM64 ? regMask.SRBM_ARM64_ZR :
        UnsupportedTargetValue<regMask>(nameof(SRBM_ZR));

}