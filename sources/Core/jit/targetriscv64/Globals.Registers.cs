// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_R0;
    public const regNumber REG_INT_FIRST = REG_R0;
    public const regNumber REG_INT_LAST = REG_T6;
    public const regNumber REG_FP_FIRST = REG_FT0;
    public const regNumber REG_FP_LAST = REG_FT11;
    public const int REG_INT_COUNT = (int)REG_INT_LAST - (int)REG_INT_FIRST + 1;

    public const regNumber FIRST_INT_CALLEE_SAVED = REG_S1;
    public const regNumber LAST_INT_CALLEE_SAVED = REG_S11;
    public const regNumber FIRST_FLT_CALLEE_SAVED = REG_FS0;
    public const regNumber LAST_FLT_CALLEE_SAVED = REG_FS11;

    public const regMask SRBM_INT_CALLEE_SAVED =
        SRBM_S1 | SRBM_S2 | SRBM_S3 | SRBM_S4 | SRBM_S5 | SRBM_S6 |
        SRBM_S7 | SRBM_S8 | SRBM_S9 | SRBM_S10 | SRBM_S11;
    public const regMask SRBM_INT_CALLEE_TRASH =
        SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7 |
        SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T3 | SRBM_T4 | SRBM_T5 | SRBM_T6;
    public const regMask SRBM_FLT_CALLEE_SAVED =
        SRBM_FS0 | SRBM_FS1 | SRBM_FS2 | SRBM_FS3 | SRBM_FS4 | SRBM_FS5 |
        SRBM_FS6 | SRBM_FS7 | SRBM_FS8 | SRBM_FS9 | SRBM_FS10 | SRBM_FS11;
    public const regMask SRBM_FLT_CALLEE_TRASH =
        SRBM_FA0 | SRBM_FA1 | SRBM_FA2 | SRBM_FA3 | SRBM_FA4 | SRBM_FA5 | SRBM_FA6 | SRBM_FA7 |
        SRBM_FT0 | SRBM_FT1 | SRBM_FT2 | SRBM_FT3 | SRBM_FT4 | SRBM_FT5 | SRBM_FT6 | SRBM_FT7 |
        SRBM_FT8 | SRBM_FT9 | SRBM_FT10 | SRBM_FT11;
    public const regMask SRBM_CALLEE_SAVED = SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED;
    public const regMask SRBM_CALLEE_TRASH = SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH;

    public const regMask SRBM_GSCOOKIE_TMP = SRBM_T0 | SRBM_T1;

    public const regMask SRBM_FPBASE = SRBM_FP;
    public const regMask SRBM_SPBASE = SRBM_SP;

    public const regMask SRBM_ARG_REGS =
        SRBM_A0 | SRBM_A1 | SRBM_A2 | SRBM_A3 | SRBM_A4 | SRBM_A5 | SRBM_A6 | SRBM_A7;

    public const regMask SRBM_ARG_0 = SRBM_A0;
    public const regMask SRBM_ARG_1 = SRBM_A1;
    public const regMask SRBM_ARG_2 = SRBM_A2;
    public const regMask SRBM_ARG_3 = SRBM_A3;
    public const regMask SRBM_ARG_4 = SRBM_A4;
    public const regMask SRBM_ARG_5 = SRBM_A5;
    public const regMask SRBM_ARG_6 = SRBM_A6;
    public const regMask SRBM_ARG_7 = SRBM_A7;

    public const regMask SRBM_INTRET = SRBM_A0;
    public const regMask SRBM_LNGRET = SRBM_A0;
    public const regMask SRBM_INTRET_1 = SRBM_A1;

    public const regMask SRBM_FLOATRET = SRBM_FA0;
    public const regMask SRBM_DOUBLERET = SRBM_FA0;
    public const regMask SRBM_FLOATRET_1 = SRBM_FA1;
    public const regMask SRBM_DOUBLERET_1 = SRBM_FA1;

    public const regMask SRBM_DEFAULT_HELPER_CALL_TARGET = SRBM_T2;

    public const regNumber REG_TMP_0 = REG_T0;

    public const regNumber REG_SHIFT = REG_NA;
    public const regMask SRBM_SHIFT = SRBM_ALLINT;

    public const regMask SRBM_OPT_RSVD = SRBM_T6;

    public const regNumber REG_JUMP_THUNK_PARAM = REG_T2;
    public const regMask SRBM_JUMP_THUNK_PARAM = SRBM_T2;

    // The write-barrier helpers preserve t3, their destination register, so it is excluded from the kill masks.
    public const regMask SRBM_WRITE_BARRIER_DST = SRBM_T3;
    public const regMask SRBM_WRITE_BARRIER_SRC = SRBM_T4;

    public const regMask SRBM_CALLEE_TRASH_NOGC =
        SRBM_T0 | SRBM_T1 | SRBM_T2 | SRBM_T4 | SRBM_T6 | SRBM_DEFAULT_HELPER_CALL_TARGET;
    public const regMask SRBM_CALLEE_TRASH_WRITEBARRIER = SRBM_CALLEE_TRASH_NOGC;
    public const regMask SRBM_CALLEE_GCTRASH_WRITEBARRIER = SRBM_CALLEE_TRASH_NOGC;

    public const regMask SRBM_SECRET_STUB_PARAM = SRBM_T2;
    public const regMask SRBM_R2R_INDIRECT_PARAM = SRBM_T5;
    public const regNumber REG_INDIRECT_CALL_TARGET_REG = REG_T5;

    public const regNumber REG_PROFILER_ENTER_ARG_FUNC_ID = REG_T0;
    public const regMask SRBM_PROFILER_ENTER_ARG_FUNC_ID = SRBM_T0;
    public const regNumber REG_PROFILER_ENTER_ARG_CALLER_SP = REG_T1;
    public const regMask SRBM_PROFILER_ENTER_ARG_CALLER_SP = SRBM_T1;
    public const regNumber REG_PROFILER_LEAVE_ARG_FUNC_ID = REG_PROFILER_ENTER_ARG_FUNC_ID;
    public const regMask SRBM_PROFILER_LEAVE_ARG_FUNC_ID = SRBM_PROFILER_ENTER_ARG_FUNC_ID;
    public const regNumber REG_PROFILER_LEAVE_ARG_CALLER_SP = REG_PROFILER_ENTER_ARG_CALLER_SP;
    public const regMask SRBM_PROFILER_LEAVE_ARG_CALLER_SP = SRBM_PROFILER_ENTER_ARG_CALLER_SP;

    public const regMask SRBM_PROFILER_ENTER_TRASH =
        SRBM_CALLEE_TRASH & ~(SRBM_ARG_REGS | SRBM_FLTARG_REGS | SRBM_FP);
    public const regMask SRBM_PROFILER_LEAVE_TRASH = SRBM_PROFILER_ENTER_TRASH;
    public const regMask SRBM_PROFILER_TAILCALL_TRASH = SRBM_PROFILER_LEAVE_TRASH;

    public static readonly regMaskTP SRBM_STOP_FOR_GC_TRASH = new(SRBM_CALLEE_TRASH);
    public static readonly regMaskTP SRBM_INIT_PINVOKE_FRAME_TRASH = new(SRBM_CALLEE_TRASH);
    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH =
        SRBM_INT_CALLEE_TRASH & ~(SRBM_ARG_REGS | SRBM_T3);
    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR = REG_T0;
    public const regMask SRBM_ASYNC_CONTINUATION_RET = SRBM_A2;

    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ALLFLOAT = SRBM_FLT_CALLEE_SAVED | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLDOUBLE = SRBM_ALLFLOAT;

    public const regMask SRBM_MSK_CALLEE_TRASH = SRBM_NONE;

    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET = REG_T2;

    public const regNumber REG_SCRATCH = REG_T0;

    public const regNumber REG_OPT_RSVD = REG_T6;

    public const regNumber REG_WRITE_BARRIER_DST = REG_T3;

    public const regNumber REG_WRITE_BARRIER_SRC = REG_T4;

    public const regNumber REG_SECRET_STUB_PARAM = REG_T2;

    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_T3;

    public const regNumber REG_FPBASE = REG_FP;

    public const string STR_FPBASE = "fp";

    public const regNumber REG_SPBASE = REG_SP;

    public const string STR_SPBASE = "sp";

    public const regNumber REG_ARG_FIRST = REG_A0;

    public const regNumber REG_ARG_LAST = REG_A7;

    public const regNumber REG_ARG_FP_FIRST = REG_FA0;

    public const regNumber REG_ARG_FP_LAST = REG_FA7;

    public const int INIT_ARG_STACK_SLOT = 0;

    public const int FIRST_ARG_STACK_OFFS = 2 * REGSIZE_BYTES;

    public const regNumber REG_ARG_3 = REG_A3;
    public const regNumber REG_ARG_4 = REG_A4;
    public const regNumber REG_ARG_5 = REG_A5;
    public const regNumber REG_ARG_6 = REG_A6;
    public const regNumber REG_ARG_7 = REG_A7;

    public static regNumber REG_NEXT(regNumber reg) => reg + 1;

    public static regNumber REG_PREV(regNumber reg) => reg - 1;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER =>
    [
        REG_A0, REG_A1, REG_A2, REG_A3, REG_A4, REG_A5, REG_A6, REG_A7,
        REG_T0, REG_T1, REG_T2, REG_T3, REG_T4, REG_T5, REG_T6,
        REG_S1, REG_S2, REG_S3, REG_S4, REG_S5, REG_S6, REG_S7, REG_S8, REG_S9, REG_S10, REG_S11,
    ];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT =>
    [
        REG_FT4, REG_FT5, REG_FT6, REG_FT7, REG_FT8, REG_FT9, REG_FT10, REG_FT11,
        REG_FA2, REG_FA3, REG_FA4, REG_FA5, REG_FA6, REG_FA7,
        REG_FT0, REG_FT1, REG_FT2, REG_FT3,
        REG_FS6, REG_FS7, REG_FS8, REG_FS9, REG_FS10, REG_FS11, REG_FS2, REG_FS3, REG_FS4, REG_FS5,
        REG_FS0, REG_FS1, REG_FA1, REG_FA0,
    ];

    public const int CNT_CALLEE_TRASH = 15;
    public const int CNT_CALLEE_TRASH_FLOAT = 20;

    public const int CNT_CALLEE_ENREG = 10;

    public const int CNT_CALLEE_ENREG_FLOAT = 12;

    public const int CNT_CALLEE_SAVED_MASK = 0;

    public const int CNT_CALLEE_TRASH_MASK = 0;

    public const int CNT_CALLEE_ENREG_MASK = CNT_CALLEE_SAVED_MASK;

    public const int REGSIZE_BYTES = 8;

    public const int FP_REGSIZE_BYTES = 8;

    public const int FPSAVE_REGSIZE_BYTES = 8;

    public const int CNT_CALLEE_SAVED = 11;
    public const int CNT_CALLEE_SAVED_FLOAT = 12;
    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;
    public const int CALLEE_SAVED_FLOAT_MAXSZ = CNT_CALLEE_SAVED_FLOAT * FPSAVE_REGSIZE_BYTES;
}
#endif
