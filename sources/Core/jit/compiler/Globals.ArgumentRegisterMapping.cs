// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    // Integer and floating-point argument indices overlap, unlike their register numbers.
    // A fixed return buffer uses its special argument index rather than the ordinary table.
    public static regNumber genMapIntRegArgNumToRegNum(uint argNum, CorInfoCallConvExtension callConv)
    {
        EnsureFixedRegisterSet();

        if (hasFixedRetBuffReg(callConv) && (argNum == unchecked((uint)theFixedRetBuffArgNum(callConv))))
        {
            return theFixedRetBuffReg(callConv);
        }

        var registers = IntArgRegs;
        assert(argNum < (uint)registers.Length, "argNum < ArrLen(intArgRegs)");

        return registers[unchecked((int)argNum)];
    }

    public static regNumber genMapFloatRegArgNumToRegNum(uint argNum)
    {
        EnsureFixedRegisterSet();

        if (!Configuration.TARGET_X86)
        {
            var registers = FltArgRegs;
            assert(argNum < (uint)registers.Length, "argNum < ArrLen(fltArgRegs)");

            return registers[unchecked((int)argNum)];
        }

        assert(false, "!\"no x86 float arg regs\\n\"");

        return regNumberExtensions.NotAvailable;
    }

    public static regNumber genMapRegArgNumToRegNum(uint argNum, var_types type, CorInfoCallConvExtension callConv)
    {
        if (varTypeUsesFloatArgReg(type))
        {
            return genMapFloatRegArgNumToRegNum(argNum);
        }
        else
        {
            return genMapIntRegArgNumToRegNum(argNum, callConv);
        }
    }

    public static regMaskTP genMapIntRegArgNumToRegMask(uint argNum)
    {
        EnsureFixedRegisterSet();

        var registers = IntArgRegs;
        assert(argNum < (uint)registers.Length, "argNum < ArrLen(intArgMasks)");
        var reg = registers[unchecked((int)argNum)];

        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    // The native masks contain one bit per register, including only the low ARM double register.
    public static regMaskTP genMapFloatRegArgNumToRegMask(uint argNum)
    {
        EnsureFixedRegisterSet();

        if (!Configuration.TARGET_X86)
        {
            var registers = FltArgRegs;
            assert(argNum < (uint)registers.Length, "argNum < ArrLen(fltArgMasks)");
            var reg = registers[unchecked((int)argNum)];

            return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }

        assert(false, "!\"no x86 float arg regs\\n\"");

        return RBM_NONE;
    }

    public static regMaskTP genMapArgNumToRegMask(uint argNum, var_types type)
    {
        regMaskTP result;
        if (varTypeUsesFloatArgReg(type))
        {
            result = genMapFloatRegArgNumToRegMask(argNum);
            if (Configuration.TARGET_ARM && (type == TYP_DOUBLE))
            {
                const regMask allDoubleRegs =
                    SRBM_F0_ARM | SRBM_F2_ARM | SRBM_F4_ARM | SRBM_F6_ARM |
                    SRBM_F8_ARM | SRBM_F10_ARM | SRBM_F12_ARM | SRBM_F14_ARM |
                    SRBM_F16_ARM | SRBM_F18_ARM | SRBM_F20_ARM | SRBM_F22_ARM |
                    SRBM_F24_ARM | SRBM_F26_ARM | SRBM_F28_ARM | SRBM_F30_ARM;
                assert((result & new regMaskTP(allDoubleRegs)) != RBM_NONE, "(result & RBM_ALLDOUBLE) != 0");
                result |= new regMaskTP(unchecked((regMask)((long)result.Lower << 1)));
            }
        }
        else
        {
            result = genMapIntRegArgNumToRegMask(argNum);
        }

        return result;
    }

    public static uint genMapFloatRegNumToRegArgNum(regNumber regNum)
    {
        EnsureFixedRegisterSet();

        assert((regMaskTP.CreateFromRegNum(regNum, regNum.SingleTypeMask) & new regMaskTP(SRBM_FLTARG_REGS)) != RBM_NONE,
            "genRegMask(regNum) & RBM_FLTARG_REGS");

        if (Configuration.TARGET_ARM)
        {
            return unchecked((uint)(regNum - REG_F0_ARM));
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return unchecked((uint)(regNum - REG_F0_LOONGARCH64));
        }

        if (Configuration.TARGET_RISCV64)
        {
            return unchecked((uint)(regNum - REG_FA0_RISCV64));
        }

        if (Configuration.TARGET_ARM64)
        {
            return unchecked((uint)(regNum - REG_V0_ARM64));
        }

        if (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX)
        {
            return unchecked((uint)(regNum - REG_XMM0_AMD64));
        }

        if (Configuration.TARGET_AMD64)
        {
            switch (regNum)
            {
                case REG_XMM0_AMD64:
                {
                    return 0;
                }

                case REG_XMM1_AMD64:
                {
                    return 1;
                }

                case REG_XMM2_AMD64:
                {
                    return 2;
                }

                case REG_XMM3_AMD64:
                {
                    return 3;
                }

                default:
                {
                    assert(false, "!\"invalid register arg register\"");

                    return unchecked((uint)BAD_VAR_NUM);
                }
            }
        }

        if (Configuration.TARGET_X86)
        {
            assert(false, "!\"flt reg args not allowed\"");

            return unchecked((uint)BAD_VAR_NUM);
        }

        throw new FatalJitException("Floating-point argument register mapping is not supported for this target.");
    }

    public static uint genMapRegNumToRegArgNum(regNumber regNum, var_types type, CorInfoCallConvExtension callConv)
    {
        if (varTypeUsesFloatArgReg(type))
        {
            return genMapFloatRegNumToRegArgNum(regNum);
        }
        else
        {
            return unchecked((uint)genMapIntRegNumToRegArgNum(regNum, callConv));
        }
    }
}
