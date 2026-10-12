// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace RyuJitSharp;

public partial class Globals
{
    public static regNumber REG_FLOATRET
    {
        get
        {
            if (Configuration.TARGET_X86 || Configuration.TARGET_WASM)
            {
                return regNumberExtensions.NotAvailable;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_X64_XMM0;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_F0;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_V0;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_F0;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_FA0;
            }

            return UnsupportedTargetValue<regNumber>("Floating-point return register is not supported for this target.");
        }
    }
}
