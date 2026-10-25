// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe uint eeGetArgSize(CorInfoType corInfoType, CORINFO_CLASS_HANDLE typeHnd)
    {
        var argType = corInfoType.VarType;

        if (Configuration.TARGET_AMD64)
        {
            if (Configuration.UNIX_AMD64_ABI && varTypeIsStruct(argType))
            {
                var structSize = unchecked((uint)eeGetClassSize(typeHnd));
                return roundUp(structSize, unchecked((uint)Configuration.TARGET_POINTER_SIZE));
            }

            return unchecked((uint)Configuration.TARGET_POINTER_SIZE);
        }

        uint argSize;
        var hfaType = TYP_UNDEF;

        if (varTypeIsStruct(argType))
        {
            hfaType = GetHfaType(typeHnd);
            var structSize = unchecked((uint)eeGetClassSize(typeHnd));

            if (Configuration.FEATURE_MULTIREG_ARGS)
            {
                if (Configuration.TARGET_ARM64)
                {
                    var isHfa = hfaType != TYP_UNDEF;
                    if (structSize > Configuration.MAX_PASS_MULTIREG_BYTES)
                    {
                        return unchecked((uint)Configuration.TARGET_POINTER_SIZE);
                    }
                    else if (structSize > (2 * Configuration.TARGET_POINTER_SIZE))
                    {
                        if (TargetOS.IsWindows && info.compIsVarArgs)
                        {
                            // Windows ARM64 varargs pass structs in general-purpose registers.
                            isHfa = false;
                        }
                        if (!isHfa)
                        {
                            return unchecked((uint)Configuration.TARGET_POINTER_SIZE);
                        }
                    }
                }
                else if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
                {
                    if (structSize > Configuration.MAX_PASS_MULTIREG_BYTES)
                    {
                        return unchecked((uint)Configuration.TARGET_POINTER_SIZE);
                    }
                }
                else if (!Configuration.TARGET_ARM)
                {
                    Globals.NYI("unknown target");
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnknownTargetForMultiRegisterArguments);
                    return default!;
                }
            }
            argSize = structSize;
        }
        else
        {
            argSize = genTypeSize(argType);
        }

        var argSizeAlignment = eeGetArgSizeAlignment(argType, hfaType == TYP_FLOAT);
        return roundUp(argSize, argSizeAlignment);
    }

    private static uint eeGetArgSizeAlignment(var_types type, bool isFloatHfa)
    {
        if (compAppleArm64Abi())
        {
            if (isFloatHfa)
            {
                assert(varTypeIsStruct(type));
                return sizeof(float);
            }
            if (varTypeIsStruct(type))
            {
                return unchecked((uint)Configuration.TARGET_POINTER_SIZE);
            }

            var argSize = genTypeSize(type);
            assert((0 < argSize) && (argSize <= Configuration.TARGET_POINTER_SIZE));
            return argSize;
        }
        else
        {
            return unchecked((uint)Configuration.TARGET_POINTER_SIZE);
        }
    }
}
