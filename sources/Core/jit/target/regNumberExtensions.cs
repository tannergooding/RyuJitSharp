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
                    type is TYP_DOUBLE ? ((regNum - REG_ARM32_F0) % 2) == 0 : type is TYP_FLOAT or TYP_STRUCT,
                    conditionExpression:
                        "type is TYP_DOUBLE ? ((regNum - REG_F0) % 2) == 0 : type is TYP_FLOAT or TYP_STRUCT");
                assert(regNum is >= REG_ARM32_F0 and <= REG_ARM32_F31);
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
                return REG_X86_NA;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_X64_NA;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_NA;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_NA;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_NA;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_NA;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_WASM_NA;
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
                return REG_X86_EDI;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_X64_R31;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_LR;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_ZR;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_S8;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_T6;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_WASM_NA;
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
                return REG_X86_XMM7;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_X64_XMM31;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_F31;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_V31;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_F31;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_FT11;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_WASM_NA;
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
                return REG_X86_K7;
            }

            if (Configuration.TARGET_AMD64)
            {
                return REG_X64_K7;
            }

            if (Configuration.TARGET_ARM64)
            {
                return REG_ARM64_P15;
            }

            if (Configuration.TARGET_ARM)
            {
                return REG_ARM32_NA;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return REG_LOONGARCH64_NA;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return REG_RISCV64_NA;
            }

            if (Configuration.TARGET_WASM)
            {
                return REG_WASM_NA;
            }

            throw new FatalJitException("Invalid target architecture.");
        }
    }

    internal static bool IsValidArmDoubleRegister(regNumber regNum) =>
        Configuration.TARGET_ARM && regNum.IsFltReg && (((int)regNum - (int)REG_ARM32_F0) & 1) is 0;

    // These tags match WasmValueType; the top three register bits carry the value type.
    private const uint WASM_VALUE_TYPE_I32 = 1;
    private const uint WASM_VALUE_TYPE_I64 = 2;
    private const uint WASM_VALUE_TYPE_F32 = 3;
    private const uint WASM_VALUE_TYPE_F64 = 4;
    private const uint WASM_VALUE_TYPE_V128 = 5;
    private const uint WASM_VALUE_TYPE_COUNT = 7;

    private static uint GetWasmValueTypeTag(regNumber regNum) => unchecked((uint)regNum) >> WASM_REG_TYPE_SHIFT;

    public static bool IsValidWasmReg(regNumber regNum)
    {
        var type = GetWasmValueTypeTag(regNum);
        return (type > 0) && (type < WASM_VALUE_TYPE_COUNT);
    }

    public static bool IsValidWasmIntReg(regNumber regNum)
    {
        var type = GetWasmValueTypeTag(regNum);
        return (type is WASM_VALUE_TYPE_I32 or WASM_VALUE_TYPE_I64);
    }

    public static bool IsValidWasmFloatReg(regNumber regNum)
    {
        var type = GetWasmValueTypeTag(regNum);
        return (type is WASM_VALUE_TYPE_F32 or WASM_VALUE_TYPE_F64 or WASM_VALUE_TYPE_V128);
    }

    private static bool IsIntegerRegister(regNumber regNum)
    {
        if (Configuration.TARGET_X86)
        {
            return regNum is >= REG_X86_EAX and <= REG_X86_EDI;
        }

        if (Configuration.TARGET_AMD64)
        {
            return regNum is >= REG_X64_RAX and <= REG_X64_R31;
        }

        if (Configuration.TARGET_ARM)
        {
            return regNum is >= REG_ARM32_R0 and <= REG_ARM32_LR;
        }

        if (Configuration.TARGET_ARM64)
        {
            return regNum is >= REG_ARM64_R0 and <= REG_ARM64_ZR;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return regNum is >= REG_LOONGARCH64_R0 and <= REG_LOONGARCH64_S8;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return regNum is >= REG_RISCV64_R0 and <= REG_RISCV64_T6;
        }

        return false;
    }

    private static bool IsFloatingPointRegister(regNumber regNum)
    {
        if (Configuration.TARGET_X86)
        {
            return regNum is >= REG_X86_XMM0 and <= REG_X86_XMM7;
        }

        if (Configuration.TARGET_AMD64)
        {
            return regNum is >= REG_X64_XMM0 and <= REG_X64_XMM31;
        }

        if (Configuration.TARGET_ARM)
        {
            return regNum is >= REG_ARM32_F0 and <= REG_ARM32_F31;
        }

        if (Configuration.TARGET_ARM64)
        {
            return regNum is >= REG_ARM64_V0 and <= REG_ARM64_V31;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return regNum is >= REG_LOONGARCH64_F0 and <= REG_LOONGARCH64_F31;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return regNum is >= REG_RISCV64_FT0 and <= REG_RISCV64_FT11;
        }

        return false;
    }

    private static bool IsMaskRegister(regNumber regNum)
    {
        if (Configuration.TARGET_X86)
        {
            return regNum is >= REG_X86_K0 and <= REG_X86_K7;
        }

        if (Configuration.TARGET_AMD64)
        {
            return regNum is >= REG_X64_K0 and <= REG_X64_K7;
        }

        if (Configuration.TARGET_ARM64)
        {
            return regNum is >= REG_ARM64_P0 and <= REG_ARM64_P15;
        }

        return false;
    }
}
