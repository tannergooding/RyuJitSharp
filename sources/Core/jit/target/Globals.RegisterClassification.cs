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

    public static bool isByteReg(regNumber reg)
    {
        EnsureFixedRegisterSet();

        return !Configuration.TARGET_XARCH ||
            (reg <= (Configuration.TARGET_X86 ? REG_EBX_X86 : REG_EBX_AMD64));
    }
}
