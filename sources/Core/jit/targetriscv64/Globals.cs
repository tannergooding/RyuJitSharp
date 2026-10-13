// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET_RISCV64 = CORINFO_ARCH_RISCV64;

    public const int MAX_PASS_SINGLEREG_BYTES_RISCV64 = 8;

    public const int MAX_PASS_MULTIREG_BYTES_RISCV64 = 16;

    public const int MAX_RET_MULTIREG_BYTES_RISCV64 = 16;

    public const int MAX_ARG_REG_COUNT_RISCV64 = 2;

    public const int MAX_REG_ARG_RISCV64 = 8;

    public const int MAX_FLOAT_REG_ARG_RISCV64 = 8;

    public const int TARGET_POINTER_SIZE_RISCV64 = 8;

    public const int MAX_RET_REG_COUNT_RISCV64 = 2;

    public const int MAX_MULTIREG_COUNT_RISCV64 = 2;

    public const int REGNUM_BITS_RISCV64 = 6;

    public const int MIN_ARG_AREA_FOR_CALL_RISCV64 = 0;

    public const int CODE_ALIGN_RISCV64 = 2;

    public const int STACK_ALIGN_RISCV64 = 16;

    public const int J_DIST_SMALL_MAX_NEG = -(1 << 20);

    public const int J_DIST_SMALL_MAX_POS = (1 << 20) - 1;

    public const int B_DIST_SMALL_MAX_NEG_RISCV64 = -4096;

    public const int B_DIST_SMALL_MAX_POS_RISCV64 = 4095;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_RISCV64 = 0;

    public const regNumber FIRST_FP_ARGREG_RISCV64 = REG_RISCV64_FA0;

    public const regNumber LAST_FP_ARGREG_RISCV64 = REG_RISCV64_FA7;

    public const int TARGET_MASKS_SHIFTS_RISCV64 = 1;

    public const int TARGET_HAS_MULHI_RISCV64 = 1;

    public const regNumber REG_ASYNC_CONTINUATION_RET_RISCV64 = REG_RISCV64_A2;

    public const regNumber REG_ARG_0_RISCV64 = REG_RISCV64_A0;

    public const regNumber REG_ARG_1_RISCV64 = REG_RISCV64_A1;

    public const regNumber REG_ARG_2_RISCV64 = REG_RISCV64_A2;

    public const regNumber REG_INTRET_RISCV64 = REG_RISCV64_A0;

    public const regNumber REG_LNGRET_RISCV64 = REG_RISCV64_A0;

    public const regNumber REG_INTRET_1_RISCV64 = REG_RISCV64_A1;

    public const regNumber REG_FLOATRET_1_RISCV64 = REG_RISCV64_FA1;

    public const regNumber REG_R2R_INDIRECT_PARAM_RISCV64 = REG_RISCV64_T5;
}
