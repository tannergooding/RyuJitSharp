// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static SingleTypeRegSet genSingleTypeRegMask(regNumber regNum, var_types type)
    {
        EnsureFixedRegisterSet();

        if (Configuration.TARGET_ARM)
        {
            if (varTypeUsesIntReg(type))
            {
                return LsraGlobals.genSingleTypeRegMask(regNum);
            }

            assert(varTypeUsesFloatReg(type));
            return regNum.GetSingleTypeFloatMask(type);
        }

        return LsraGlobals.genSingleTypeRegMask(regNum);
    }

    public static regMaskTP genRegMask(regNumber reg)
    {
        var result = RBM_NONE;
        regMaskTP.AddRegNumInMask(ref result, reg);

        return result;
    }

    public static regMaskTP genRegMask(regNumber regNum, var_types type)
    {
        var result = RBM_NONE;
        regMaskTP.AddRegNumInMask(ref result, regNum, type);

        return result;
    }
}
