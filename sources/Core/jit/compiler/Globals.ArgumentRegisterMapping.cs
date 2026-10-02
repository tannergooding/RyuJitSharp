// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if HAS_FIXED_REGISTER_SET
namespace RyuJitSharp;

public static partial class Globals
{
    // Integer and floating-point argument indices overlap, unlike their register numbers.
    // A fixed return buffer uses its special argument index rather than the ordinary table.
    public static regNumber genMapIntRegArgNumToRegNum(uint argNum, CorInfoCallConvExtension callConv)
    {
        if (hasFixedRetBuffReg(callConv) && (argNum == unchecked((uint)theFixedRetBuffArgNum(callConv))))
        {
            return theFixedRetBuffReg(callConv);
        }

        assert(argNum < (uint)IntArgRegs.Length, "argNum < ArrLen(intArgRegs)");

        return IntArgRegs[unchecked((int)argNum)];
    }

    public static regNumber genMapFloatRegArgNumToRegNum(uint argNum)
    {
#if !TARGET_X86
        assert(argNum < (uint)FltArgRegs.Length, "argNum < ArrLen(fltArgRegs)");

        return FltArgRegs[unchecked((int)argNum)];
#else
        assert(false, "!\"no x86 float arg regs\\n\"");

        return REG_NA;
#endif
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
        assert(argNum < (uint)IntArgRegs.Length, "argNum < ArrLen(intArgMasks)");
        var reg = IntArgRegs[unchecked((int)argNum)];

        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    // The native masks contain one bit per register, including only the low ARM double register.
    public static regMaskTP genMapFloatRegArgNumToRegMask(uint argNum)
    {
#if !TARGET_X86
        assert(argNum < (uint)FltArgRegs.Length, "argNum < ArrLen(fltArgMasks)");
        var reg = FltArgRegs[unchecked((int)argNum)];

        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
#else
        assert(false, "!\"no x86 float arg regs\\n\"");

        return RBM_NONE;
#endif
    }

    public static regMaskTP genMapArgNumToRegMask(uint argNum, var_types type)
    {
        regMaskTP result;
        if (varTypeUsesFloatArgReg(type))
        {
            result = genMapFloatRegArgNumToRegMask(argNum);
#if TARGET_ARM
            if (type == TYP_DOUBLE)
            {
                assert((result & new regMaskTP(SRBM_ALLDOUBLE)) != RBM_NONE, "(result & RBM_ALLDOUBLE) != 0");
                result |= result << 1;
            }
#endif
        }
        else
        {
            result = genMapIntRegArgNumToRegMask(argNum);
        }

        return result;
    }

    public static uint genMapFloatRegNumToRegArgNum(regNumber regNum)
    {
        assert((regMaskTP.CreateFromRegNum(regNum, regNum.SingleTypeMask) & new regMaskTP(SRBM_FLTARG_REGS)) != RBM_NONE,
            "genRegMask(regNum) & RBM_FLTARG_REGS");

#if TARGET_ARM
        return unchecked((uint)(regNum - REG_F0));
#elif TARGET_LOONGARCH64
        return unchecked((uint)(regNum - REG_F0));
#elif TARGET_RISCV64
        return unchecked((uint)(regNum - REG_FA0));
#elif TARGET_ARM64
        return unchecked((uint)(regNum - REG_V0));
#elif UNIX_AMD64_ABI
        return unchecked((uint)(regNum - REG_FLTARG_0));
#else
        // In this branch MAX_FLOAT_REG_ARG is four on AMD64 and zero on x86.
#if TARGET_AMD64
        switch (regNum)
        {
            case REG_FLTARG_0:
            {
                return 0;
            }

            case REG_FLTARG_1:
            {
                return 1;
            }

            case REG_FLTARG_2:
            {
                return 2;
            }

            case REG_FLTARG_3:
            {
                return 3;
            }
#if UNIX_AMD64_ABI

            case REG_FLTARG_4:
            {
                return 4;
            }
#endif

            default:
            {
                assert(false, "!\"invalid register arg register\"");

                return unchecked((uint)BAD_VAR_NUM);
            }
        }
#else
        assert(false, "!\"flt reg args not allowed\"");

        return unchecked((uint)BAD_VAR_NUM);
#endif
#endif
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
#endif
