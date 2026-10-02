// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if HAS_FIXED_REGISTER_SET
namespace RyuJitSharp;

public static partial class Globals
{
    public static bool isValidIntArgReg(regNumber reg, CorInfoCallConvExtension callConv)
    {
        return (new regMaskTP(reg.SingleTypeMask) & fullIntArgRegMask(callConv)) != RBM_NONE;
    }

    public static bool isValidFloatArgReg(regNumber reg)
    {
        if (reg == REG_NA)
        {
            return false;
        }
        else
        {
            return (reg >= FIRST_FP_ARGREG) && (reg <= LAST_FP_ARGREG);
        }
    }
}
#endif
