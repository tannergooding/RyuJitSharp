// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_R0;
    public const regNumber REG_INT_FIRST = REG_R0;
    public const regNumber REG_INT_LAST = REG_S8;
    public const regNumber REG_FP_FIRST = REG_F0;
    public const regNumber REG_FP_LAST = REG_F31;

    public const regMask SRBM_ARG_REGS =
        SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7;

    public const regMask SRBM_GSCOOKIE_TMP = SRBM_T0 | SRBM_T1;

    public const regMask SRBM_FPBASE = SRBM_FP;
    public const regMask SRBM_INT_CALLEE_SAVED =
        SRBM_S0 | SRBM_S1 | SRBM_S2 | SRBM_S3 | SRBM_S4 | SRBM_S5 | SRBM_S6 | SRBM_S7 | SRBM_S8;
    public const regMask SRBM_INT_CALLEE_TRASH =
        SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7 |
        SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6 | SRBM_T7 | SRBM_T8;
    public const regMask SRBM_FLT_CALLEE_SAVED =
        SRBM_F24 | SRBM_F25 | SRBM_F26 | SRBM_F27 | SRBM_F28 | SRBM_F29 | SRBM_F30 | SRBM_F31;
    public const regMask SRBM_FLT_CALLEE_TRASH =
        SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7;

    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ALLFLOAT = SRBM_FLT_CALLEE_SAVED | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLDOUBLE = SRBM_ALLFLOAT;

    public const regMask SRBM_INTRET = SRBM_A0;

    public const regMask SRBM_INTRET_1 = SRBM_A1;

    public const regMask SRBM_FLOATRET_1 = SRBM_F1;

    public const regMask SRBM_MSK_CALLEE_TRASH = SRBM_NONE;

    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET = REG_T4;

    public const regNumber REG_SCRATCH = REG_T0;

    public const regNumber REG_SCRATCH_FLT = REG_F11;

    public const regNumber REG_SECRET_STUB_PARAM = REG_T2;

    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_T3;

    public const regNumber REG_WRITE_BARRIER_DST = REG_T6;

    public const regNumber REG_WRITE_BARRIER_SRC = REG_T7;

    public const regNumber REG_INTRET_1 = REG_A1;

    public const regNumber REG_FLOATRET_1 = REG_F1;

    public const regNumber REG_FPBASE = REG_FP;

    public const string STR_FPBASE = "fp";

    public const regNumber REG_SPBASE = REG_SP;

    public const string STR_SPBASE = "sp";

    public const regNumber REG_ARG_FIRST = REG_A0;

    public const regNumber REG_ARG_LAST = REG_A7;

    public const regNumber REG_ARG_2 = REG_A2;

    public static regNumber REG_NEXT(regNumber reg) => reg + 1;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER =>
    [
        REG_A0, REG_A1, REG_A2, REG_A3, REG_A4, REG_A5, REG_A6, REG_A7,
        REG_T0, REG_T1, REG_T2, REG_T3, REG_T4, REG_T5, REG_T6, REG_T7, REG_T8,
        REG_S0, REG_S1, REG_S2, REG_S3, REG_S4, REG_S5, REG_S6, REG_S7, REG_S8,
    ];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT =>
    [
        REG_F12, REG_F13, REG_F14, REG_F15, REG_F16, REG_F17, REG_F18, REG_F19,
        REG_F2, REG_F3, REG_F4, REG_F5, REG_F6, REG_F7, REG_F8, REG_F9, REG_F10,
        REG_F20, REG_F21, REG_F22, REG_F23,
        REG_F24, REG_F25, REG_F26, REG_F27, REG_F28, REG_F29, REG_F30, REG_F31,
        REG_F1, REG_F0,
    ];

    public const int CNT_CALLEE_TRASH = 17;
    public const int CNT_CALLEE_TRASH_FLOAT = 24;

    public const int REGSIZE_BYTES = 8;
    public const int FPSAVE_REGSIZE_BYTES = 8;

    public const int CNT_CALLEE_SAVED = 10;
    public const int CNT_CALLEE_SAVED_FLOAT = 8;
    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;
    public const int CALLEE_SAVED_FLOAT_MAXSZ = CNT_CALLEE_SAVED_FLOAT * FPSAVE_REGSIZE_BYTES;
}
#endif
