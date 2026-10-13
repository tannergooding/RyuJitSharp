// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public static regNumber REG_EXCEPTION_OBJECT
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_X86_EAX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return Configuration.UNIX_AMD64_ABI ? REG_X64_EDI : REG_X64_ECX;
            }
            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_R0;
            }
            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_R0;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_A0;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_A0;
            }
            if (Configuration.TARGET_WASM)
            {
                return REG_WASM_NA;
            }
            throw new InvalidOperationException("Unsupported target for the exception-object register.");
        }
    }

    public static regMask SRBM_EXCEPTION_OBJECT
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return SRBM_X86_EAX;
            }
            if (Configuration.TARGET_AMD64)
            {
                return Configuration.UNIX_AMD64_ABI ? SRBM_X64_EDI : SRBM_X64_ECX;
            }
            if (Configuration.TARGET_ARM)
            {
                return SRBM_ARM32_R0;
            }
            if (Configuration.TARGET_ARM64)
            {
                return SRBM_ARM64_R0;
            }
            if (Configuration.TARGET_LOONGARCH64)
            {
                return SRBM_LOONGARCH64_A0;
            }
            if (Configuration.TARGET_RISCV64)
            {
                return SRBM_RISCV64_A0;
            }
            if (Configuration.TARGET_WASM)
            {
                return SRBM_NONE;
            }
            throw new InvalidOperationException("Unsupported target for the exception-object register mask.");
        }
    }
}
