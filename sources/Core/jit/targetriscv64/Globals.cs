// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET = CORINFO_ARCH_RISCV64;

    public const int MAX_PASS_SINGLEREG_BYTES = 8;

    public const int MAX_PASS_MULTIREG_BYTES = 16;

    public const int MAX_RET_MULTIREG_BYTES = 16;

    public const int MAX_ARG_REG_COUNT = 2;

    public const int TARGET_POINTER_SIZE = 8;

    public const int MAX_RET_REG_COUNT = 2;

    public const int MAX_MULTIREG_COUNT = 2;

    public const int REGNUM_BITS = 6;

    public const int MIN_ARG_AREA_FOR_CALL = 0;

    public const int CODE_ALIGN = 2;

    public const int STACK_ALIGN = 16;

    public const regNumber FIRST_FP_ARGREG = REG_FA0;

    public const regNumber LAST_FP_ARGREG = REG_FA7;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 1;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_A2;

    public const regNumber REG_ARG_0 = REG_A0;

    public const regNumber REG_ARG_1 = REG_A1;

    public const regNumber REG_ARG_2 = REG_A2;

    public const regNumber REG_INTRET = REG_A0;

    public const regNumber REG_INTRET_1 = REG_A1;

    public const regNumber REG_FLOATRET_1 = REG_FA1;

    public const regNumber REG_R2R_INDIRECT_PARAM = REG_T5;
}
#endif
