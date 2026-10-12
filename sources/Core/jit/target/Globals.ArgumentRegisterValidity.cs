// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static bool isValidIntArgReg(regNumber reg, CorInfoCallConvExtension callConv)
    {
        EnsureFixedRegisterSet();

        return (new regMaskTP(reg.SingleTypeMask) & fullIntArgRegMask(callConv)) != RBM_NONE;
    }

    public static bool isValidFloatArgReg(regNumber reg)
    {
        EnsureFixedRegisterSet();

        if (reg == regNumberExtensions.NotAvailable)
        {
            return false;
        }

        if (Configuration.TARGET_X86)
        {
            return (reg >= REG_XMM0_X86) && (reg <= REG_XMM3_X86);
        }

        var registers = FltArgRegs;
        return (reg >= registers[0]) && (reg <= registers[^1]);
    }
}
