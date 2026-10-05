// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
#if FEATURE_SIMD
#error SIMD Unimplemented yet LOONGARCH
#endif

namespace RyuJitSharp;

public partial class Globals
{
#if FEATURE_SIMD
    public const int ALIGN_SIMD_TYPES = 1;

    public const int FEATURE_PARTIAL_SIMD_CALLEE_SAVE = 1;
#endif

    public const CorInfoArch CORINFO_ARCH_TARGET = CORINFO_ARCH_LOONGARCH64;

    public const int CPU_LOAD_STORE_ARCH = 1;

    public const int CPU_HAS_FP_SUPPORT = 1;

    public const int CPU_HAS_BYTE_REGS = 0;

    public const int MAX_PASS_SINGLEREG_BYTES = 8;

    public const int MAX_PASS_MULTIREG_BYTES = 16;

    public const int MAX_RET_MULTIREG_BYTES = 16;

    public const int MAX_ARG_REG_COUNT = 2;

    public const int TARGET_POINTER_SIZE = 8;

    public const int MAX_RET_REG_COUNT = 2;

    public const int MAX_MULTIREG_COUNT = 2;

    public const int REGNUM_BITS = 6;

    public const int FP_REGSIZE_BYTES = 8;

    public const int MIN_ARG_AREA_FOR_CALL = 0;

    public const int CODE_ALIGN = 4;

    public const int STACK_ALIGN = 16;

    public const int CNT_CALLEE_ENREG = 9;

    public const int CNT_CALLEE_ENREG_FLOAT = 8;

    public const int CNT_CALLEE_SAVED_MASK = 0;

    public const int CNT_CALLEE_TRASH_MASK = 0;

    public const int CNT_CALLEE_ENREG_MASK = 0;

    public const regNumber FIRST_FP_ARGREG = REG_F0;

    public const regNumber LAST_FP_ARGREG = REG_F7;

    public const regNumber REG_ARG_FP_FIRST = REG_F0;

    public const regNumber REG_ARG_FP_LAST = REG_F7;

    public const int MAX_REG_ARG = 8;

    public const int MAX_FLOAT_REG_ARG = 8;

    public const int FIRST_ARG_STACK_OFFS = 2 * REGSIZE_BYTES;

    public const int INIT_ARG_STACK_SLOT = 0;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 1;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_A2;

    public const regNumber REG_ARG_0 = REG_A0;

    public const regNumber REG_ARG_1 = REG_A1;

    public const regNumber REG_INTRET = REG_A0;

    public const regNumber REG_R2R_INDIRECT_PARAM = REG_T8;

    public const int B_DIST_SMALL_MAX_NEG = -131072;

    public const int B_DIST_SMALL_MAX_POS = 131071;

    public const int OFFSET_DIST_SMALL_MAX_NEG = -2048;

    public const int OFFSET_DIST_SMALL_MAX_POS = 2047;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES = 0;
}
#endif
