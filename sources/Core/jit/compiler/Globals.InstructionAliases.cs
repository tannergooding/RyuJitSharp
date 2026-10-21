// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public static instruction INS_SHIFT_LEFT_LOGICAL => GetXarchOrArmInstructionAlias(INS_shl, INS_lsl);
    public static instruction INS_SHIFT_RIGHT_LOGICAL => GetXarchOrArmInstructionAlias(INS_shr, INS_lsr);
    public static instruction INS_SHIFT_RIGHT_ARITHM => GetXarchOrArmInstructionAlias(INS_sar, INS_asr);

    public static instruction INS_AND => GetXarchOrArmInstructionAlias(INS_and, INS_and);
    public static instruction INS_OR => GetXarchOrArmInstructionAlias(INS_or, INS_orr);
    public static instruction INS_XOR => GetXarchOrArmInstructionAlias(INS_xor, INS_eor);
    public static instruction INS_NEG => GetXarchOrArmInstructionAlias(INS_neg, INS_rsb);
    public static instruction INS_TEST => GetXarchOrArmInstructionAlias(INS_test, INS_tst);
    public static instruction INS_MUL => GetXarchOrArmInstructionAlias(INS_imul, INS_mul);
    public static instruction INS_SIGNED_DIVIDE => GetXarchOrArmInstructionAlias(INS_idiv, INS_sdiv);
    public static instruction INS_UNSIGNED_DIVIDE => GetXarchOrArmInstructionAlias(INS_div, INS_udiv);
    public static instruction INS_ADDC => GetXarchOrArmInstructionAlias(INS_adc, INS_adc);
    public static instruction INS_SUBC => GetXarchOrArmInstructionAlias(INS_sbb, INS_sbc);
    public static instruction INS_NOT => GetXarchOrArmInstructionAlias(INS_not, INS_mvn);

    // The pinned aliases select double precision; callers select single precision separately.
    public static instruction INS_MULADD
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return INS_mla;
            }
            else
            {
                if (Configuration.TARGET_ARM64)
                {
                    return INS_madd;
                }
                else
                {
                    if (Configuration.TARGET_LOONGARCH64)
                    {
                        return INS_fmadd_d;
                    }
                    else
                    {
                        FatalJitException.ThrowFatalJitException("INS_MULADD is not defined for this target.");
                        return default!;
                    }
                }
            }
        }
    }

    public static instruction INS_ABS
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return INS_vabs;
            }
            else
            {
                if (Configuration.TARGET_ARM64)
                {
                    return INS_fabs;
                }
                else
                {
                    if (Configuration.TARGET_LOONGARCH64)
                    {
                        return INS_fabs_d;
                    }
                    else
                    {
                        FatalJitException.ThrowFatalJitException("INS_ABS is not defined for this target.");
                        return default!;
                    }
                }
            }
        }
    }

    public static instruction INS_SQRT
    {
        get
        {
            if (Configuration.TARGET_ARM)
            {
                return INS_vsqrt;
            }
            else
            {
                if (Configuration.TARGET_ARM64)
                {
                    return INS_fsqrt;
                }
                else
                {
                    if (Configuration.TARGET_LOONGARCH64)
                    {
                        return INS_fsqrt_d;
                    }
                    else
                    {
                        FatalJitException.ThrowFatalJitException("INS_SQRT is not defined for this target.");
                        return default!;
                    }
                }
            }
        }
    }

    private static instruction GetXarchOrArmInstructionAlias(instruction xarch, instruction arm)
    {
        if (Configuration.TARGET_XARCH)
        {
            return xarch;
        }

        if (Configuration.TARGET_ARM)
        {
            return arm;
        }

        FatalJitException.ThrowFatalJitException("The instruction alias is not defined for this target.");
        return default!;
    }
}
