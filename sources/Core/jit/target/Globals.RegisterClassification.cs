// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static bool isFloatRegType(var_types type)
    {
        return varTypeUsesFloatReg(type);
    }

#if HAS_FIXED_REGISTER_SET
    public static bool isByteReg(regNumber reg)
    {
#if CPU_HAS_BYTE_REGS
        return reg <= REG_EBX;
#else
        return true;
#endif
    }
#endif
}
