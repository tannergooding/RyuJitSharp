// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const int ALIGN_SIMD_TYPES = 1;

    public const int FEATURE_PARTIAL_SIMD_CALLEE_SAVE = 1;

    public const CorInfoArch CORINFO_ARCH_TARGET_LOONGARCH64 = CORINFO_ARCH_LOONGARCH64;

    public const int CPU_LOAD_STORE_ARCH = 1;

    public const int CPU_HAS_FP_SUPPORT = 1;

    public const int CPU_HAS_BYTE_REGS = 0;

    public const int MAX_PASS_SINGLEREG_BYTES_LOONGARCH64 = 8;

    public const int MAX_PASS_MULTIREG_BYTES_LOONGARCH64 = 16;

    public const int MAX_RET_MULTIREG_BYTES_LOONGARCH64 = 16;

    public const int MAX_ARG_REG_COUNT_LOONGARCH64 = 2;

    public const int TARGET_POINTER_SIZE_LOONGARCH64 = 8;

    public const int MAX_RET_REG_COUNT_LOONGARCH64 = 2;

    public const int MAX_MULTIREG_COUNT_LOONGARCH64 = 2;

    public const int REGNUM_BITS_LOONGARCH64 = 6;

    public const int FP_REGSIZE_BYTES_LOONGARCH64 = 8;

    public const int MIN_ARG_AREA_FOR_CALL_LOONGARCH64 = 0;

    public const int CODE_ALIGN_LOONGARCH64 = 4;

    public const int STACK_ALIGN_LOONGARCH64 = 16;

    public const int CNT_CALLEE_ENREG_LOONGARCH64 = 9;

    public const int CNT_CALLEE_ENREG_FLOAT_LOONGARCH64 = 8;

    public const int CNT_CALLEE_SAVED_MASK_LOONGARCH64 = 0;

    public const int CNT_CALLEE_TRASH_MASK_LOONGARCH64 = 0;

    public const int CNT_CALLEE_ENREG_MASK_LOONGARCH64 = 0;

    public const regNumber FIRST_FP_ARGREG_LOONGARCH64 = REG_LOONGARCH64_F0;

    public const regNumber LAST_FP_ARGREG_LOONGARCH64 = REG_LOONGARCH64_F7;

    public const regNumber REG_ARG_FP_FIRST_LOONGARCH64 = REG_LOONGARCH64_F0;

    public const regNumber REG_ARG_FP_LAST_LOONGARCH64 = REG_LOONGARCH64_F7;

    public const int MAX_REG_ARG_LOONGARCH64 = 8;

    public const int MAX_FLOAT_REG_ARG_LOONGARCH64 = 8;

    public const int FIRST_ARG_STACK_OFFS_LOONGARCH64 = 2 * REGSIZE_BYTES_LOONGARCH64;

    public const int INIT_ARG_STACK_SLOT_LOONGARCH64 = 0;

    public const int TARGET_MASKS_SHIFTS_LOONGARCH64 = 1;

    public const int TARGET_HAS_MULHI_LOONGARCH64 = 1;

    public const regNumber REG_ASYNC_CONTINUATION_RET_LOONGARCH64 = REG_LOONGARCH64_A2;

    public const regNumber REG_ARG_0_LOONGARCH64 = REG_LOONGARCH64_A0;

    public const regNumber REG_ARG_1_LOONGARCH64 = REG_LOONGARCH64_A1;

    public const regNumber REG_INTRET_LOONGARCH64 = REG_LOONGARCH64_A0;

    public const regNumber REG_R2R_INDIRECT_PARAM_LOONGARCH64 = REG_LOONGARCH64_T8;

    public const int B_DIST_SMALL_MAX_NEG_LOONGARCH64 = -131072;

    public const int B_DIST_SMALL_MAX_POS_LOONGARCH64 = 131071;

    public const int OFFSET_DIST_SMALL_MAX_NEG = -2048;

    public const int OFFSET_DIST_SMALL_MAX_POS = 2047;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_LOONGARCH64 = 0;
}
