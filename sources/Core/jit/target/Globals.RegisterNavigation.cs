// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regNumber REG_NEXT(regNumber reg)
    {
        if (Configuration.TARGET_X86)
        {
            return REG_NEXT_X86(reg);
        }

        if (Configuration.TARGET_AMD64 ||
            Configuration.TARGET_ARM ||
            Configuration.TARGET_ARM64 ||
            Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64)
        {
            return reg + 1;
        }

        if (Configuration.TARGET_WASM)
        {
            return REG_NEXT_WASM(reg);
        }

        throw new FatalJitException("Register successor is not supported for this target.");
    }

    public static regNumber REG_PREV(regNumber reg)
    {
        if (Configuration.TARGET_X86)
        {
            return REG_PREV_X86(reg);
        }

        if (Configuration.TARGET_AMD64 ||
            Configuration.TARGET_ARM ||
            Configuration.TARGET_ARM64 ||
            Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64)
        {
            return reg - 1;
        }

        throw new FatalJitException("Register predecessor is not supported for this target.");
    }
}
