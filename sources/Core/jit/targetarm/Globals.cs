// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET_ARM = CORINFO_ARCH_ARM;

    public const int MAX_PASS_SINGLEREG_BYTES_ARM = 8;

    public const int MAX_PASS_MULTIREG_BYTES_ARM = 32;

    public const int MAX_RET_MULTIREG_BYTES_ARM = 32;

    public const int MAX_ARG_REG_COUNT_ARM = 4;

    public const int MAX_RET_REG_COUNT_ARM = 4;

    public const int MAX_MULTIREG_COUNT_ARM = 4;

    public const int TARGET_POINTER_SIZE_ARM = 4;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_ARM = 0;

    public const regNumber FIRST_FP_ARGREG_ARM = REG_ARM32_F0;

    public const regNumber LAST_FP_ARGREG_ARM = REG_ARM32_F15;

    public const int TARGET_MASKS_SHIFTS_ARM = 0;

    public const int TARGET_HAS_MULHI_ARM = 0;

    public const regNumber REG_R2R_INDIRECT_PARAM_ARM = REG_ARM32_R12;

    public const regMask SRBM_R2R_INDIRECT_PARAM_ARM = SRBM_ARM32_R12;

    public const regNumber REG_ASYNC_CONTINUATION_RET_ARM = REG_ARM32_R2;

    public const regNumber REG_PINVOKE_TCB_ARM = REG_ARM32_R5;

    public const regNumber REG_ARG_0_ARM = REG_ARM32_R0;

    public const regNumber REG_ARG_1_ARM = REG_ARM32_R1;

    public const regNumber REG_INTRET_ARM = REG_ARM32_R0;

    public const regMask SRBM_INTRET_ARM = SRBM_ARM32_R0;

    public const regNumber REG_LNGRET_LO_ARM = REG_ARM32_R0;

    public const regNumber REG_LNGRET_HI_ARM = REG_ARM32_R1;

    public const int MAX_HFA_RET_SLOTS = 8;

    public const int LBL_DIST_SMALL_MAX_NEG_ARM = 0;
    public const int LBL_DIST_SMALL_MAX_POS_ARM = 1020;
    public const int LBL_DIST_MED_MAX_NEG = -4095;
    public const int LBL_DIST_MED_MAX_POS = 4096;

    public const int CALL_DIST_MAX_NEG = -16777216;
    public const int CALL_DIST_MAX_POS = 16777214;

    public const int LBL_SIZE_SMALL_ARM = 2;
}
