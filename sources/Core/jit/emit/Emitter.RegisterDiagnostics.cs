// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isDoubleReg(regNumber reg)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException("ARM32 double-register validation is not available for this target.");
            return default!;
        }

        var register = unchecked((uint)reg);
        return register >= (uint)REG_ARM32_F0 &&
            register <= (uint)REG_ARM32_F31 &&
            ((register & 1) == 0);
    }

    public void emitDispRegSet(regMaskTP registers)
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            return;
        }

        jitprintf(" {");
        var separator = "";
        for (var reg = TargetRuntimeMetadata.FirstRegister; reg < ACTUAL_REG_COUNT; reg++)
        {
            if (registers.IsEmpty)
            {
                break;
            }
            var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((registers & mask).IsEmpty)
            {
                continue;
            }
            registers ^= mask;
            jitprintf(separator);
            separator = " ";
            jitprintf(emitRegName(reg));
        }
        jitprintf("}");
    }
}
