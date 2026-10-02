// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool lvaIsParameter(int varNum)
    {
        ref var varDsc = ref lvaGetDesc(varNum);

        return varDsc.lvIsParam;
    }

    public bool lvaIsRegArgument(int varNum)
    {
        ref var varDsc = ref lvaGetDesc(varNum);

        return varDsc.lvIsRegArg;
    }

    public var_types lvaGetActualType(int lclNum)
    {
        return lvaTable[lclNum].Type.ActualType;
    }

    public var_types mangleVarArgsType(var_types type)
    {
#if TARGET_ARMARCH
#if CONFIGURABLE_ARM_ABI
        var compUseSoftFP = opts.compUseSoftFP;
#else
        var compUseSoftFP = Options.compUseSoftFP;
#endif
        if (compUseSoftFP || (TargetOS.IsWindows && info.compIsVarArgs))
        {
            switch (type)
            {
                case TYP_FLOAT:
                {
                    return TYP_INT;
                }

                case TYP_DOUBLE:
                {
                    return TYP_LONG;
                }

                default:
                {
                    break;
                }
            }

            if (varTypeIsSimd(type))
            {
                // Vectors should be considered like passing a struct.
                return TYP_STRUCT;
            }
        }
#endif
        return type;
    }
}
