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
#if TARGET_ARM
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

                case REG_F0:
                {
                    regName = "d0";
                    break;
                }

                case REG_F2:
                {
                    regName = "d2";
                    break;
                }

                case REG_F4:
                {
                    regName = "d4";
                    break;
                }

                case REG_F6:
                {
                    regName = "d6";
                    break;
                }

                case REG_F8:
                {
                    regName = "d8";
                    break;
                }

                case REG_F10:
                {
                    regName = "d10";
                    break;
                }

                case REG_F12:
                {
                    regName = "d12";
                    break;
                }

                case REG_F14:
                {
                    regName = "d14";
                    break;
                }

                case REG_F16:
                {
                    regName = "d16";
                    break;
                }

                case REG_F18:
                {
                    regName = "d18";
                    break;
                }

                case REG_F20:
                {
                    regName = "d20";
                    break;
                }

                case REG_F22:
                {
                    regName = "d22";
                    break;
                }

                case REG_F24:
                {
                    regName = "d24";
                    break;
                }

                case REG_F26:
                {
                    regName = "d26";
                    break;
                }

                case REG_F28:
                {
                    regName = "d28";
                    break;
                }

                case REG_F30:
                {
                    regName = "d30";
                    break;
                }
            }

            return regName;
#elif TARGET_ARM64 || TARGET_LOONGARCH64
            assert((uint)reg < (uint)s_names.Length, conditionExpression: "(unsigned)reg < ArrLen(regNamesFloat)");
            return s_names[(int)reg];
#else
            assert((uint)reg < (uint)FloatRegisterNames.s_floatNames.Length,
                conditionExpression: "(unsigned)reg < ArrLen(regNamesFloat)");

#if FEATURE_SIMD && TARGET_XARCH
            if (type == TYP_SIMD64)
            {
                return FloatRegisterNames.s_zmmNames[(int)reg];
            }
            else if (type == TYP_SIMD32)
            {
                return FloatRegisterNames.s_ymmNames[(int)reg];
            }
#endif

            return FloatRegisterNames.s_floatNames[(int)reg];
#endif
        }
    }

#if !TARGET_ARM && !TARGET_ARM64 && !TARGET_LOONGARCH64
    // A separate holder lets s_names finish initialization regardless of partial-file ordering.
    private static class FloatRegisterNames
    {
        internal static readonly string[] s_floatNames = CreatePrefixedNames("x");
#if FEATURE_SIMD
        internal static readonly string[] s_ymmNames = CreatePrefixedNames("y");
        internal static readonly string[] s_zmmNames = CreatePrefixedNames("z");
#endif

        private static string[] CreatePrefixedNames(string prefix)
        {
            var names = new string[s_names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                names[index] = prefix + s_names[index];
            }

            return names;
        }
    }
#endif
}
#endif
