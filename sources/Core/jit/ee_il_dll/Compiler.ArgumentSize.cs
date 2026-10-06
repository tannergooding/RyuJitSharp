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

#if TARGET_AMD64
#if UNIX_AMD64_ABI
        if (varTypeIsStruct(argType))
        {
            var structSize = unchecked((uint)info.compCompHnd->getClassSize(typeHnd));
            return roundUp(structSize, TARGET_POINTER_SIZE);
        }
#endif
        return TARGET_POINTER_SIZE;
#else
        uint argSize;
        var hfaType = TYP_UNDEF;
        var isHfa = false;

        if (varTypeIsStruct(argType))
        {
            hfaType = GetHfaType(typeHnd);
            isHfa = hfaType != TYP_UNDEF;
            var structSize = unchecked((uint)info.compCompHnd->getClassSize(typeHnd));

#if FEATURE_MULTIREG_ARGS
#if TARGET_ARM64
            if (structSize > MAX_PASS_MULTIREG_BYTES)
            {
                return TARGET_POINTER_SIZE;
            }
            else if (structSize > (2 * TARGET_POINTER_SIZE))
            {
                if (TargetOS.IsWindows && info.compIsVarArgs)
                {
                    // Windows ARM64 varargs pass structs in general-purpose registers.
                    isHfa = false;
                }
                if (!isHfa)
                {
                    return TARGET_POINTER_SIZE;
                }
            }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
            if (structSize > MAX_PASS_MULTIREG_BYTES)
            {
                return TARGET_POINTER_SIZE;
            }
#elif !TARGET_ARM
            Globals.NYI("unknown target");
            throw new FatalJitException(CORJIT_SKIPPED, "Unknown target for multi-register arguments.");
#endif
#endif
            argSize = structSize;
        }
        else
        {
            argSize = genTypeSize(argType);
        }

        var argSizeAlignment = eeGetArgSizeAlignment(argType, hfaType == TYP_FLOAT);
        return roundUp(argSize, argSizeAlignment);
#endif
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
                return TARGET_POINTER_SIZE;
            }

            var argSize = genTypeSize(type);
            assert((0 < argSize) && (argSize <= TARGET_POINTER_SIZE));
            return argSize;
        }
        else
        {
            return TARGET_POINTER_SIZE;
        }
    }
}
