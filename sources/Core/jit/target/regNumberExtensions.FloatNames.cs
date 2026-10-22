// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    extension(regNumber reg)
    {
        public string GetFloatName(var_types type)
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.FloatingRegisterNamesAreOnlyAvailableInDebugBuilds);
                return default!;
            }

            if (Configuration.TARGET_ARM)
            {
                assert(reg.IsFltReg, conditionExpression: "genIsValidFloatReg(reg)");
                if (type == TYP_FLOAT)
                {
                    return reg.Name;
                }

                string regName;
                switch (reg)
                {
                    default:
                    {
                        assert(false, conditionExpression: "!\"Bad double register\"");
                        regName = "d??";
                        break;
                    }

                    case REG_ARM32_F0:
                    {
                        regName = "d0";
                        break;
                    }

                    case REG_ARM32_F2:
                    {
                        regName = "d2";
                        break;
                    }

                    case REG_ARM32_F4:
                    {
                        regName = "d4";
                        break;
                    }

                    case REG_ARM32_F6:
                    {
                        regName = "d6";
                        break;
                    }

                    case REG_ARM32_F8:
                    {
                        regName = "d8";
                        break;
                    }

                    case REG_ARM32_F10:
                    {
                        regName = "d10";
                        break;
                    }

                    case REG_ARM32_F12:
                    {
                        regName = "d12";
                        break;
                    }

                    case REG_ARM32_F14:
                    {
                        regName = "d14";
                        break;
                    }

                    case REG_ARM32_F16:
                    {
                        regName = "d16";
                        break;
                    }

                    case REG_ARM32_F18:
                    {
                        regName = "d18";
                        break;
                    }

                    case REG_ARM32_F20:
                    {
                        regName = "d20";
                        break;
                    }

                    case REG_ARM32_F22:
                    {
                        regName = "d22";
                        break;
                    }

                    case REG_ARM32_F24:
                    {
                        regName = "d24";
                        break;
                    }

                    case REG_ARM32_F26:
                    {
                        regName = "d26";
                        break;
                    }

                    case REG_ARM32_F28:
                    {
                        regName = "d28";
                        break;
                    }

                    case REG_ARM32_F30:
                    {
                        regName = "d30";
                        break;
                    }
                }

                return regName;
            }

            if (Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64)
            {
                assert((uint)reg < (uint)GetRegisterCount(),
                    conditionExpression: "(unsigned)reg < ArrLen(regNamesFloat)");

                return GetName(reg);
            }

            var floatNames = Compiler.DebugData.FloatRegisterNames.FloatNames;
            assert((uint)reg < (uint)floatNames.Length,
                conditionExpression: "(unsigned)reg < ArrLen(regNamesFloat)");

            if (Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH)
            {
                if (type == TYP_SIMD64)
                {
                    return Compiler.DebugData.FloatRegisterNames.ZmmNames[(int)reg];
                }

                if (type == TYP_SIMD32)
                {
                    return Compiler.DebugData.FloatRegisterNames.YmmNames[(int)reg];
                }
            }

            return floatNames[(int)reg];
        }
    }

    internal static string[] CreatePrefixedNames(string prefix)
    {
        var names = new string[GetRegisterCount()];
        for (var index = 0; index < names.Length; index++)
        {
            names[index] = prefix + GetName((regNumber)index);
        }

        return names;
    }

    private static int GetRegisterCount()
    {
        if (Configuration.TARGET_X86)
        {
            return (int)REG_X86_COUNT;
        }

        if (Configuration.TARGET_AMD64)
        {
            return (int)REG_X64_COUNT;
        }

        if (Configuration.TARGET_ARM)
        {
            return (int)REG_ARM32_COUNT;
        }

        if (Configuration.TARGET_ARM64)
        {
            return (int)REG_ARM64_COUNT;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return (int)REG_LOONGARCH64_COUNT;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return (int)REG_RISCV64_COUNT;
        }

        if (Configuration.TARGET_WASM)
        {
            return (int)REG_WASM_COUNT;
        }

        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.InvalidTargetArchitecture);
        return default!;
    }
}
