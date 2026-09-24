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
        for (var registerNumber = REG_FIRST; (int)registerNumber < _availableRegCount; registerNumber++)
        {
            physRegs[(int)registerNumber].init(registerNumber);
        }

        setRegisterOrder(REG_VAR_ORDER);
        setRegisterOrder(_evexIsSupported ? REG_VAR_ORDER_FLT_EVEX : REG_VAR_ORDER_FLT);
        if (_evexIsSupported)
        {
            setRegisterOrder(REG_VAR_ORDER_MSK);
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
