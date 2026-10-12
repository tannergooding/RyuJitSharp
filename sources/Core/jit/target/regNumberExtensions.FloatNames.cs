// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    extension(regNumber reg)
    {
        public string GetFloatName(var_types type)
        {
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

                    case REG_F0_ARM:
                    {
                        regName = "d0";
                        break;
                    }

                    case REG_F2_ARM:
                    {
                        regName = "d2";
                        break;
                    }

                    case REG_F4_ARM:
                    {
                        regName = "d4";
                        break;
                    }

                    case REG_F6_ARM:
                    {
                        regName = "d6";
                        break;
                    }

                    case REG_F8_ARM:
                    {
                        regName = "d8";
                        break;
                    }

                    case REG_F10_ARM:
                    {
                        regName = "d10";
                        break;
                    }

                    case REG_F12_ARM:
                    {
                        regName = "d12";
                        break;
                    }

                    case REG_F14_ARM:
                    {
                        regName = "d14";
                        break;
                    }

                    case REG_F16_ARM:
                    {
                        regName = "d16";
                        break;
                    }

                    case REG_F18_ARM:
                    {
                        regName = "d18";
                        break;
                    }

                    case REG_F20_ARM:
                    {
                        regName = "d20";
                        break;
                    }

                    case REG_F22_ARM:
                    {
                        regName = "d22";
                        break;
                    }

                    case REG_F24_ARM:
                    {
                        regName = "d24";
                        break;
                    }

                    case REG_F26_ARM:
                    {
                        regName = "d26";
                        break;
                    }

                    case REG_F28_ARM:
                    {
                        regName = "d28";
                        break;
                    }

                    case REG_F30_ARM:
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

            var floatNames = FloatRegisterNames.s_floatNames;
            assert((uint)reg < (uint)floatNames.Length,
                conditionExpression: "(unsigned)reg < ArrLen(regNamesFloat)");

            if (Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH)
            {
                if (type == TYP_SIMD64)
                {
                    return FloatRegisterNames.s_zmmNames[(int)reg];
                }

                if (type == TYP_SIMD32)
                {
                    return FloatRegisterNames.s_ymmNames[(int)reg];
                }
            }

            return floatNames[(int)reg];
        }
    }

    // Keep the Debug-only prefixed names lazy until floating-register diagnostics need them.
    private static class FloatRegisterNames
    {
        internal static readonly string[] s_floatNames = CreatePrefixedNames("x");
        internal static readonly string[] s_ymmNames = CreatePrefixedNames("y");
        internal static readonly string[] s_zmmNames = CreatePrefixedNames("z");

        private static string[] CreatePrefixedNames(string prefix)
        {
            var names = new string[GetRegisterCount()];
            for (var index = 0; index < names.Length; index++)
            {
                names[index] = prefix + GetName((regNumber)index);
            }

            return names;
        }
    }

    private static int GetRegisterCount()
    {
        if (Configuration.TARGET_X86)
        {
            return (int)REG_COUNT_X86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return (int)REG_COUNT_AMD64;
        }

        if (Configuration.TARGET_ARM)
        {
            return (int)REG_COUNT_ARM;
        }

        if (Configuration.TARGET_ARM64)
        {
            return (int)REG_COUNT_ARM64;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return (int)REG_COUNT_LOONGARCH64;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return (int)REG_COUNT_RISCV64;
        }

        if (Configuration.TARGET_WASM)
        {
            return (int)REG_COUNT_WASM;
        }

        throw new FatalJitException("Invalid target architecture.");
    }
}
#endif
