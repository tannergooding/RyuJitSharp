// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
#if HAS_FIXED_REGISTER_SET
    public static SingleTypeRegSet genSingleTypeRegMask(regNumber regNum, var_types type)
    {
#if TARGET_ARM
        if (varTypeUsesIntReg(type))
        {
            return LsraGlobals.genSingleTypeRegMask(regNum);
        }

        assert(varTypeUsesFloatReg(type));
        return regNum.GetSingleTypeFloatMask(type);
#else
        return LsraGlobals.genSingleTypeRegMask(regNum);
#endif
    }
#endif

    public static regMaskTP genRegMask(regNumber reg)
    {
        var result = RBM_NONE;
        regMaskTP.AddRegNumInMask(ref result, reg);

        return result;
    }

    public static regMaskTP genRegMask(regNumber regNum, var_types type)
    {
        var result = RBM_NONE;
#if TARGET_ARM
        regMaskTP.AddRegNumInMask(ref result, regNum, type);
#else
        regMaskTP.AddRegNumInMask(ref result, regNum);
#endif

        return result;
    }
}
