// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    extension(regNumber regNum)
    {
        public regMask SingleTypeMask
        {
            get
            {
                unchecked
                {
                    assert((uint)regNum < (uint)s_masks.Length);
                    return s_masks[(int)regNum];
                }
            }
        }

        public regMaskTP GetFloatMask(var_types type = TYP_DOUBLE)
        {
            return new regMaskTP(regNum.GetSingleTypeFloatMask(type));
        }

        public regMask GetSingleTypeFloatMask(var_types type = TYP_DOUBLE)
        {
            Globals.EnsureFixedRegisterSet();
            assert(regNum.IsFltReg);
            if (Configuration.TARGET_ARM)
            {
                assert(
                    type is TYP_DOUBLE ? ((regNum - REG_F0_ARM) % 2) == 0 : type is TYP_FLOAT or TYP_STRUCT,
                    conditionExpression:
                        "type is TYP_DOUBLE ? ((regNum - REG_F0) % 2) == 0 : type is TYP_FLOAT or TYP_STRUCT");
                assert(regNum is >= REG_F0_ARM and <= REG_F31_ARM);
            }

            var mask = regNum.SingleTypeMask;
            if (Configuration.TARGET_ARM && (type is TYP_DOUBLE))
            {
                mask |= (regNum + 1).SingleTypeMask;
            }

            return mask;
        }

        public bool IsFltReg => IsFloatingPointRegister(regNum);

        public bool IsIntReg => IsIntegerRegister(regNum);

        public bool IsMskReg =>
            Configuration.FEATURE_MASKED_HW_INTRINSICS && IsMaskRegister(regNum);

        public string Name => GetName(regNum);
    }

    internal static regNumber NotAvailable
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_NA_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_NA_AMD64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_NA_ARM;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_NA_ARM64;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_NA_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_NA_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_NA_WASM;
            }

            throw new FatalJitException("Invalid target architecture.");
        }
    }

    internal static regNumber LastIntegerRegister
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_EDI_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_R31_AMD64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_LR_ARM;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_ZR_ARM64;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_S8_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_T6_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_NA_WASM;
            }

            throw new FatalJitException("Invalid target architecture.");
        }
    }

    internal static regNumber LastFloatingPointRegister
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_XMM7_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_XMM31_AMD64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_F31_ARM;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_V31_ARM64;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_F31_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_FT11_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_NA_WASM;
            }

            throw new FatalJitException("Invalid target architecture.");
        }
    }

    internal static regNumber LastMaskRegister
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return REG_K7_X86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_K7_AMD64;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_P15_ARM64;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_NA_ARM;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_NA_LOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_NA_RISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_NA_WASM;
            }

            throw new FatalJitException("Invalid target architecture.");
        }
    }

    internal static bool IsValidArmDoubleRegister(regNumber regNum) =>
        Configuration.TARGET_ARM && regNum.IsFltReg && (((int)regNum - (int)REG_F0_ARM) & 1) is 0;

    private static bool IsIntegerRegister(regNumber regNum)
    {
        if (Configuration.TARGET_X86)
        {
            return regNum is >= REG_EAX_X86 and <= REG_EDI_X86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return regNum is >= REG_RAX_AMD64 and <= REG_R31_AMD64;
        }

        if (Configuration.TARGET_ARM)
        {
            return regNum is >= REG_R0_ARM and <= REG_LR_ARM;
        }

        if (Configuration.TARGET_ARM64)
        {
            return regNum is >= REG_R0_ARM64 and <= REG_ZR_ARM64;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return regNum is >= REG_R0_LOONGARCH64 and <= REG_S8_LOONGARCH64;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return regNum is >= REG_R0_RISCV64 and <= REG_T6_RISCV64;
        }

        return false;
    }

    private static bool IsFloatingPointRegister(regNumber regNum)
    {
        if (Configuration.TARGET_X86)
        {
            return regNum is >= REG_XMM0_X86 and <= REG_XMM7_X86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return regNum is >= REG_XMM0_AMD64 and <= REG_XMM31_AMD64;
        }

        if (Configuration.TARGET_ARM)
        {
            return regNum is >= REG_F0_ARM and <= REG_F31_ARM;
        }

        if (Configuration.TARGET_ARM64)
        {
            return regNum is >= REG_V0_ARM64 and <= REG_V31_ARM64;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return regNum is >= REG_F0_LOONGARCH64 and <= REG_F31_LOONGARCH64;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return regNum is >= REG_FT0_RISCV64 and <= REG_FT11_RISCV64;
        }

        return false;
    }

    private static bool IsMaskRegister(regNumber regNum)
    {
        if (Configuration.TARGET_X86)
        {
            return regNum is >= REG_K0_X86 and <= REG_K7_X86;
        }

        if (Configuration.TARGET_AMD64)
        {
            return regNum is >= REG_K0_AMD64 and <= REG_K7_AMD64;
        }

        if (Configuration.TARGET_ARM64)
        {
            return regNum is >= REG_P0_ARM64 and <= REG_P15_ARM64;
        }

        return false;
    }
}
