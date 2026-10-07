// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET = CORINFO_ARCH_ARM;

    public const int MAX_PASS_SINGLEREG_BYTES = 8;

    public const int MAX_PASS_MULTIREG_BYTES = 32;

    public const int MAX_RET_MULTIREG_BYTES = 32;

    public const int MAX_ARG_REG_COUNT = 4;

    public const int MAX_RET_REG_COUNT = 4;

    public const int MAX_MULTIREG_COUNT = 4;

    public const int TARGET_POINTER_SIZE = 4;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES = 0;

    public const regNumber FIRST_FP_ARGREG = REG_F0;

    public const regNumber LAST_FP_ARGREG = REG_F15;

    public const int TARGET_MASKS_SHIFTS = 0;

    public const int TARGET_HAS_MULHI = 0;

    public const regNumber REG_R2R_INDIRECT_PARAM = REG_R12;

    public const regMask SRBM_R2R_INDIRECT_PARAM = SRBM_R12;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_R2;

    public const regNumber REG_PINVOKE_TCB = REG_R5;

    public const regNumber REG_ARG_0 = REG_R0;

    public const regNumber REG_ARG_1 = REG_R1;

    public const regNumber REG_INTRET = REG_R0;

    public const regMask SRBM_INTRET = SRBM_R0;

    public const regNumber REG_LNGRET_LO = REG_R0;

    public const regNumber REG_LNGRET_HI = REG_R1;

    public const regNumber REG_FLOATRET = REG_F0;
}
#endif
