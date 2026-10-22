// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    internal static void EnsureFixedRegisterSet()
    {
        if (!Configuration.HAS_FIXED_REGISTER_SET)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedRegisterMasksAreNotSupportedOnThisTarget);
        }
    }

    public static bool floatRegCanHoldType(regNumber reg, var_types type)
    {
        EnsureFixedRegisterSet();

        if (Configuration.TARGET_ARM)
        {
            assert(reg.IsFltReg, conditionExpression: "genIsValidFloatReg(reg)");
            if (type == TYP_DOUBLE)
            {
                return (unchecked((int)reg - (int)REG_ARM32_F0) % 2) == 0;
            }

            // Can be TYP_STRUCT for HFA. It's not clear that's correct; what about
            // HFA of double? We wouldn't be asserting the right alignment, and
            // callers like genRegMaskFloat() wouldn't be generating the right mask.
            assert((type == TYP_FLOAT) || (type == TYP_STRUCT), "(type == TYP_FLOAT) || (type == TYP_STRUCT)");
            return true;
        }

        return true;
    }

    public static regNumber getRegForType(regNumber reg, var_types regType)
    {
        EnsureFixedRegisterSet();

        if (Configuration.TARGET_ARM && (regType == TYP_DOUBLE) &&
            !regNumberExtensions.IsValidArmDoubleRegister(reg))
        {
            reg = unchecked((regNumber)(byte)((int)reg - 1));
        }

        return reg;
    }

    public static SingleTypeRegSet getSingleTypeRegMask(regNumber reg, var_types regType)
    {
        EnsureFixedRegisterSet();

        reg = getRegForType(reg, regType);
        var regMask = reg.SingleTypeMask;
        if (Configuration.TARGET_ARM && (regType == TYP_DOUBLE))
        {
            assert(
                regNumberExtensions.IsValidArmDoubleRegister(reg),
                conditionExpression: "genIsValidDoubleReg(reg)");
            regMask |= unchecked((SingleTypeRegSet)((long)regMask << 1));
        }

        return regMask;
    }

    public static regNumber regNextOfType(regNumber reg, var_types type)
    {
        EnsureFixedRegisterSet();

        regNumber regReturn;
        if (Configuration.TARGET_ARM && (type == TYP_DOUBLE))
        {
            // Skip odd FP registers for double-precision types.
            assert(floatRegCanHoldType(reg, type));
            regReturn = unchecked((regNumber)((int)reg + 2));
        }
        else
        {
            regReturn = unchecked((regNumber)((int)reg + 1));
        }

        if (varTypeUsesIntReg(type))
        {
            if (regReturn > regNumberExtensions.LastIntegerRegister)
            {
                regReturn = regNumberExtensions.NotAvailable;
            }
        }
        else if (Configuration.TARGET_XARCH && varTypeUsesMaskReg(type))
        {
            if (regReturn > regNumberExtensions.LastMaskRegister)
            {
                regReturn = regNumberExtensions.NotAvailable;
            }
        }
        else
        {
            assert(varTypeUsesFloatReg(type));
            if (regReturn > regNumberExtensions.LastFloatingPointRegister)
            {
                regReturn = regNumberExtensions.NotAvailable;
            }
        }

        return regReturn;
    }
}
