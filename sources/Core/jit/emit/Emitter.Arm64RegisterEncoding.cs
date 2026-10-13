// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static uint insEncodeReg_V(regNumber reg, int hi, int lo)
    {
        assert(isVectorRegister(reg));
        var ureg = unchecked((uint)reg - (uint)REG_ARM64_V0);
        var bits = hi - lo + 1;
        var mask = (1u << bits) - 1;

        return (ureg & mask) << lo;
    }

    private static uint insEncodeReg_R(regNumber reg, int hi, int lo)
    {
        assert(isIntegerRegister(reg));
        var ureg = unchecked((uint)reg);
        var bits = hi - lo + 1;
        var mask = (1u << bits) - 1;

        return (ureg & mask) << lo;
    }

    private static bool isGeneralRegisterOrSP(regNumber reg)
    {
        return isGeneralRegister(reg) || (reg == REG_ARM64_SP);
    }

    private static regNumber encodingSPtoZR(regNumber reg)
    {
        return (reg == REG_ARM64_SP) ? REG_ARM64_ZR : reg;
    }

    private static regNumber encodingZRtoSP(regNumber reg)
    {
        return (reg == REG_ARM64_ZR) ? REG_ARM64_SP : reg;
    }
}
