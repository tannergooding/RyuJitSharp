// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public sealed class VirtualStubParamInfo
    {
        public VirtualStubParamInfo()
        {
            if (Configuration.TARGET_X86)
            {
                Reg = REG_X86_EAX;
                RegMask = RBM_X86_EAX;
            }
            else if (Configuration.TARGET_AMD64)
            {
                Reg = REG_X64_R11;
                RegMask = RBM_X64_R11;
            }
            else if (Configuration.TARGET_ARM)
            {
                Reg = REG_ARM32_R12;
                RegMask = RBM_ARM32_R12;
            }
            else if (Configuration.TARGET_ARM64)
            {
                Reg = REG_ARM64_R11;
                RegMask = RBM_ARM64_R11;
            }
            else if (Configuration.TARGET_LOONGARCH64)
            {
                Reg = REG_LOONGARCH64_T8;
                RegMask = new regMaskTP(SRBM_LOONGARCH64_T8);
            }
            else if (Configuration.TARGET_RISCV64)
            {
                Reg = REG_RISCV64_T5;
                RegMask = new regMaskTP(SRBM_RISCV64_T5);
            }
            else if (Configuration.TARGET_WASM)
            {
                Reg = REG_NA;
                RegMask = RBM_NONE;
            }
            else
            {
                FatalJitException.ThrowFatalJitException("Unsupported or unset target architecture.");
                return;
            }
        }

        public regNumber Reg { get; }

        public regMaskTP RegMask { get; }
    }
}
