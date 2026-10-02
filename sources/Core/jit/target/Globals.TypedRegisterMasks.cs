// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if HAS_FIXED_REGISTER_SET
namespace RyuJitSharp;

public static partial class Globals
{
    public static bool floatRegCanHoldType(regNumber reg, var_types type)
    {
#if TARGET_ARM
        assert(genIsValidFloatReg(reg));
        if (type == TYP_DOUBLE)
        {
            return (unchecked((int)reg - (int)REG_F0) % 2) == 0;
        }
        else
        {
            // Can be TYP_STRUCT for HFA. It's not clear that's correct; what about
            // HFA of double? We wouldn't be asserting the right alignment, and
            // callers like genRegMaskFloat() wouldn't be generating the right mask.
            assert((type == TYP_FLOAT) || (type == TYP_STRUCT), "(type == TYP_FLOAT) || (type == TYP_STRUCT)");
            return true;
        }
#else
        return true;
#endif
    }

    public static regNumber getRegForType(regNumber reg, var_types regType)
    {
#if TARGET_ARM
        if ((regType == TYP_DOUBLE) && !genIsValidDoubleReg(reg))
        {
            reg = unchecked((regNumber)((int)reg - 1));
        }
#endif
        return reg;
    }

    public static SingleTypeRegSet getSingleTypeRegMask(regNumber reg, var_types regType)
    {
        reg = getRegForType(reg, regType);
        var regMask = reg.SingleTypeMask;
#if TARGET_ARM
        if (regType == TYP_DOUBLE)
        {
            assert(genIsValidDoubleReg(reg));
            regMask |= unchecked((SingleTypeRegSet)((long)regMask << 1));
        }
#endif
        return regMask;
    }

    public static regNumber regNextOfType(regNumber reg, var_types type)
    {
        regNumber regReturn;
#if TARGET_ARM
        if (type == TYP_DOUBLE)
        {
            // Skip odd FP registers for double-precision types.
            assert(floatRegCanHoldType(reg, type));
            regReturn = unchecked((regNumber)((int)reg + 2));
        }
        else
        {
            regReturn = unchecked((regNumber)((int)reg + 1));
        }
#else
        regReturn = unchecked((regNumber)((int)reg + 1));
#endif

        if (varTypeUsesIntReg(type))
        {
            if (regReturn > REG_INT_LAST)
            {
                regReturn = REG_NA;
            }
        }
#if TARGET_XARCH
        else if (varTypeUsesMaskReg(type))
        {
            if (regReturn > REG_MASK_LAST)
            {
                regReturn = REG_NA;
            }
        }
#endif
        else
        {
            assert(varTypeUsesFloatReg(type));
            if (regReturn > REG_FP_LAST)
            {
                regReturn = REG_NA;
            }
        }

        return regReturn;
    }
}
#endif
