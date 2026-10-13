// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void buildPhysRegRecords()
    {
        for (var registerNumber = TargetRuntimeMetadata.FirstRegister; (int)registerNumber < _availableRegCount; registerNumber++)
        {
            physRegs[(int)registerNumber].init(registerNumber);
        }

        if (Configuration.TARGET_XARCH)
        {
            setRegisterOrder(Configuration.TARGET_AMD64 ? REG_VAR_ORDER_AMD64 : REG_VAR_ORDER_X86);

            var floatRegisterOrder = Configuration.TARGET_AMD64
                ? _evexIsSupported ? REG_VAR_ORDER_FLT_EVEX : REG_VAR_ORDER_FLT_AMD64
                : REG_VAR_ORDER_FLT_X86;
            setRegisterOrder(floatRegisterOrder);

            if (_evexIsSupported)
            {
                var maskRegisterOrder = Configuration.TARGET_AMD64 ? REG_VAR_ORDER_MSK_AMD64 : REG_VAR_ORDER_MSK_X86;
                setRegisterOrder(maskRegisterOrder);
            }
        }
    }

    private void setRegisterOrder(ReadOnlySpan<regNumber> registers)
    {
        for (var index = 0; index < registers.Length; index++)
        {
            physRegs[(int)registers[index]].regOrder = (byte)index;
        }
    }
}
