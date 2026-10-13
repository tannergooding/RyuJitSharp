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
    public static instruction INS_MULADD =>
        Configuration.TARGET_ARM ? INS_mla :
        Configuration.TARGET_ARM64 ? INS_madd :
        Configuration.TARGET_LOONGARCH64 ? INS_fmadd_d :
        throw new FatalJitException("INS_MULADD is not defined for this target.");

    public static instruction INS_ABS =>
        Configuration.TARGET_ARM ? INS_vabs :
        Configuration.TARGET_ARM64 ? INS_fabs :
        Configuration.TARGET_LOONGARCH64 ? INS_fabs_d :
        throw new FatalJitException("INS_ABS is not defined for this target.");

    public static instruction INS_SQRT =>
        Configuration.TARGET_ARM ? INS_vsqrt :
        Configuration.TARGET_ARM64 ? INS_fsqrt :
        Configuration.TARGET_LOONGARCH64 ? INS_fsqrt_d :
        throw new FatalJitException("INS_SQRT is not defined for this target.");

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

        throw new FatalJitException("The instruction alias is not defined for this target.");
    }
}
