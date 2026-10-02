// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Globals
{
    public const int TARGET_POINTER_SIZE = 4;

    public const regNumber FIRST_FP_ARGREG = REG_F0;

    public const regNumber LAST_FP_ARGREG = REG_F15;

    public const int TARGET_MASKS_SHIFTS = 0;

    public const int TARGET_HAS_MULHI = 0;

    public const regNumber REG_R2R_INDIRECT_PARAM = REG_R12;

    public const regMask SRBM_R2R_INDIRECT_PARAM = SRBM_R12;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_R2;

    public const regNumber REG_ARG_0 = REG_R0;

    public const regNumber REG_ARG_1 = REG_R1;

    public const regNumber REG_INTRET = REG_R0;

    public const regNumber REG_LNGRET_LO = REG_R0;

    public const regNumber REG_LNGRET_HI = REG_R1;

    public const regNumber REG_FLOATRET = REG_F0;
}
#endif
