// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_XARCH
    public const instruction INS_SHIFT_LEFT_LOGICAL = INS_shl;
    public const instruction INS_SHIFT_RIGHT_LOGICAL = INS_shr;
    public const instruction INS_SHIFT_RIGHT_ARITHM = INS_sar;

    public const instruction INS_AND = INS_and;
    public const instruction INS_OR = INS_or;
    public const instruction INS_XOR = INS_xor;
    public const instruction INS_NEG = INS_neg;
    public const instruction INS_TEST = INS_test;
    public const instruction INS_MUL = INS_imul;
    public const instruction INS_SIGNED_DIVIDE = INS_idiv;
    public const instruction INS_UNSIGNED_DIVIDE = INS_div;
    public const instruction INS_ADDC = INS_adc;
    public const instruction INS_SUBC = INS_sbb;
    public const instruction INS_NOT = INS_not;
#elif TARGET_ARM
    public const instruction INS_SHIFT_LEFT_LOGICAL = INS_lsl;
    public const instruction INS_SHIFT_RIGHT_LOGICAL = INS_lsr;
    public const instruction INS_SHIFT_RIGHT_ARITHM = INS_asr;

    public const instruction INS_AND = INS_and;
    public const instruction INS_OR = INS_orr;
    public const instruction INS_XOR = INS_eor;
    public const instruction INS_NEG = INS_rsb;
    public const instruction INS_TEST = INS_tst;
    public const instruction INS_MUL = INS_mul;
    public const instruction INS_MULADD = INS_mla;
    public const instruction INS_SIGNED_DIVIDE = INS_sdiv;
    public const instruction INS_UNSIGNED_DIVIDE = INS_udiv;
    public const instruction INS_ADDC = INS_adc;
    public const instruction INS_SUBC = INS_sbc;
    public const instruction INS_NOT = INS_mvn;

    public const instruction INS_ABS = INS_vabs;
    public const instruction INS_SQRT = INS_vsqrt;
#elif TARGET_ARM64
    public const instruction INS_MULADD = INS_madd;
    public const instruction INS_ABS = INS_fabs;
    public const instruction INS_SQRT = INS_fsqrt;
#elif TARGET_LOONGARCH64
    // The pinned aliases select double precision; callers select single precision separately.
    public const instruction INS_MULADD = INS_fmadd_d;
    public const instruction INS_ABS = INS_fabs_d;
    public const instruction INS_SQRT = INS_fsqrt_d;
#endif
}
