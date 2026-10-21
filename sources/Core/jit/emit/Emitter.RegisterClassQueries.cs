// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isGeneralRegister(regNumber reg)
    {
        if (Configuration.TARGET_XARCH)
        {
            return reg.IsIntReg;
        }

        if (Configuration.TARGET_ARM)
        {
            return reg >= REG_ARM32_R0 && reg <= REG_ARM32_PC;
        }

        if (Configuration.TARGET_ARM64)
        {
            return reg >= REG_ARM64_R0 && reg <= REG_ARM64_LR;
        }

        if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            return reg.IsIntReg;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "General-register classification is not supported for this target.");
        return default!;
    }
}
