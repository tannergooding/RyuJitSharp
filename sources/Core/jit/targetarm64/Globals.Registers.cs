// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;

namespace RyuJitSharp;

public static partial class Globals
{
    public const regNumber REG_FIRST = REG_R0;
    public const regNumber REG_INT_FIRST = REG_R0;
    public const regNumber REG_INT_LAST = REG_ZR;
    public const int REG_INT_COUNT = (int)REG_INT_LAST - (int)REG_INT_FIRST + 1;

    public static regNumber REG_NEXT(regNumber reg) => reg + 1;

    public static regNumber REG_PREV(regNumber reg) => reg - 1;

    public const regNumber REG_FP_FIRST = REG_V0;
    public const regNumber REG_FP_LAST = REG_V31;
    public const regNumber FIRST_FP_ARGREG = REG_V0;
    public const regNumber LAST_FP_ARGREG = REG_V15;

    public const regNumber REG_PREDICATE_FIRST = REG_P0;
    public const regNumber REG_PREDICATE_LAST = REG_P15;
    public const regNumber REG_PREDICATE_LOW_LAST = REG_P7;
    public const regNumber REG_PREDICATE_HIGH_FIRST = REG_P8;
    public const regNumber REG_PREDICATE_HIGH_LAST = REG_P15;
    public const regNumber REG_MASK_FIRST = REG_PREDICATE_FIRST;
    public const regNumber REG_MASK_LAST = REG_PREDICATE_LAST;

    public const int REGNUM_BITS = 7;
    public const int REGSIZE_BYTES = 8;
    public const int FPSAVE_REGSIZE_BYTES = 8;
    public const int MIN_ARG_AREA_FOR_CALL = 0;
    public const int CODE_ALIGN = 4;
    public const int STACK_ALIGN = 16;

    public const regMask SRBM_INT_CALLEE_SAVED =
        SRBM_R19 | SRBM_R20 | SRBM_R21 | SRBM_R22 | SRBM_R23 |
        SRBM_R24 | SRBM_R25 | SRBM_R26 | SRBM_R27 | SRBM_R28;

    public const regMask SRBM_INT_CALLEE_TRASH =
        SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 | SRBM_R4 | SRBM_R5 | SRBM_R6 | SRBM_R7 |
        SRBM_R8 | SRBM_R9 | SRBM_R10 | SRBM_R11 | SRBM_R12 | SRBM_R13 | SRBM_R14 | SRBM_R15 |
        SRBM_IP0 | SRBM_IP1 | SRBM_LR;

    public const regMask SRBM_FLT_CALLEE_SAVED =
        SRBM_V8 | SRBM_V9 | SRBM_V10 | SRBM_V11 |
        SRBM_V12 | SRBM_V13 | SRBM_V14 | SRBM_V15;

    public const regMask SRBM_FLT_CALLEE_TRASH =
        SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3 | SRBM_V4 | SRBM_V5 | SRBM_V6 | SRBM_V7 |
        SRBM_V16 | SRBM_V17 | SRBM_V18 | SRBM_V19 | SRBM_V20 | SRBM_V21 | SRBM_V22 | SRBM_V23 |
        SRBM_V24 | SRBM_V25 | SRBM_V26 | SRBM_V27 | SRBM_V28 | SRBM_V29 | SRBM_V30 | SRBM_V31;

    public const regMask SRBM_LOWMASK =
        SRBM_P0 | SRBM_P1 | SRBM_P2 | SRBM_P3 |
        SRBM_P4 | SRBM_P5 | SRBM_P6 | SRBM_P7;

    public const regMask SRBM_HIGHMASK =
        SRBM_P8 | SRBM_P9 | SRBM_P10 | SRBM_P11 |
        SRBM_P12 | SRBM_P13 | SRBM_P14 | SRBM_P15;

    public const regMask SRBM_ALLMASK = SRBM_LOWMASK | SRBM_HIGHMASK;
    public const regMask SRBM_MSK_CALLEE_SAVED = SRBM_NONE;
    public const regMask SRBM_MSK_CALLEE_TRASH = SRBM_ALLMASK;

    public const regMask SRBM_CALLEE_SAVED = SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED;

    // Predicate single-type mask bits overlap integer bits; combined kill sets need the high bank.
    public static readonly regMaskTP SRBM_CALLEE_TRASH =
        new(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);

    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ALLFLOAT = SRBM_FLT_CALLEE_SAVED | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLDOUBLE = SRBM_ALLFLOAT;

    public const int CNT_CALLEE_SAVED = 11;
    public const int CNT_CALLEE_TRASH = 17;
    public const int CNT_CALLEE_ENREG = CNT_CALLEE_SAVED - 1;
    public const int CNT_CALLEE_SAVED_FLOAT = 8;
    public const int CNT_CALLEE_TRASH_FLOAT = 24;
    public const int CNT_CALLEE_ENREG_FLOAT = CNT_CALLEE_SAVED_FLOAT;
    public const int CNT_CALLEE_SAVED_MASK = 4;
    public const int CNT_CALLEE_TRASH_MASK = 8;
    public const int CNT_CALLEE_ENREG_MASK = CNT_CALLEE_SAVED_MASK;

    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;
    public const int CALLEE_SAVED_FLOAT_MAXSZ = CNT_CALLEE_SAVED_FLOAT * FPSAVE_REGSIZE_BYTES;
    public const regMask SRBM_ENC_CALLEE_SAVED = SRBM_NONE;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [
        REG_R0, REG_R1, REG_R2, REG_R3, REG_R4, REG_R5,
        REG_R6, REG_R7, REG_R8, REG_R9, REG_R10,
        REG_R11, REG_R13, REG_R14,
        REG_R12, REG_R15, REG_IP0, REG_IP1,
        REG_R19, REG_R20, REG_R21, REG_R22, REG_R23, REG_R24,
        REG_R25, REG_R26, REG_R27, REG_R28, REG_LR,
    ];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT => [
        REG_V16, REG_V17, REG_V18, REG_V19,
        REG_V20, REG_V21, REG_V22, REG_V23,
        REG_V24, REG_V25, REG_V26, REG_V27,
        REG_V28, REG_V29, REG_V30, REG_V31,
        REG_V7, REG_V6, REG_V5, REG_V4,
        REG_V8, REG_V9, REG_V10, REG_V11,
        REG_V12, REG_V13, REG_V14, REG_V15,
        REG_V3, REG_V2, REG_V1, REG_V0,
    ];

    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET = REG_R12;
    public const regMask SRBM_DEFAULT_HELPER_CALL_TARGET = SRBM_R12;
    public const regMask SRBM_GSCOOKIE_TMP = SRBM_IP0 | SRBM_IP1;
    public const regNumber REG_SHIFT = REG_NA;
    public const regMask SRBM_SHIFT = SRBM_ALLINT;
    public const regNumber REG_SCRATCH = REG_R9;
    public const regNumber REG_OPT_RSVD = REG_IP1;
    public const regMask SRBM_OPT_RSVD = SRBM_IP1;

    public const regNumber REG_EXCEPTION_OBJECT = REG_R0;
    public const regMask SRBM_EXCEPTION_OBJECT = SRBM_R0;
    public const regNumber REG_JUMP_THUNK_PARAM = REG_R12;
    public const regMask SRBM_JUMP_THUNK_PARAM = SRBM_R12;
    public const regNumber REG_WRITE_BARRIER_DST = REG_R14;
    public const regMask SRBM_WRITE_BARRIER_DST = SRBM_R14;
    public const regNumber REG_WRITE_BARRIER_SRC = REG_R15;
    public const regMask SRBM_WRITE_BARRIER_SRC = SRBM_R15;
    public const regMask SRBM_CALLEE_TRASH_NOGC =
        SRBM_R12 | SRBM_R15 | SRBM_IP0 | SRBM_IP1 | SRBM_DEFAULT_HELPER_CALL_TARGET;
    public const regMask SRBM_CALLEE_TRASH_WRITEBARRIER = SRBM_CALLEE_TRASH_NOGC;
    public const regMask SRBM_CALLEE_GCTRASH_WRITEBARRIER = SRBM_CALLEE_TRASH_NOGC;
    public const regMask SRBM_SECRET_STUB_PARAM = SRBM_R12;
    public const regNumber REG_R2R_INDIRECT_PARAM = REG_R11;
    public const regMask SRBM_R2R_INDIRECT_PARAM = SRBM_R11;
    public const regNumber REG_INDIRECT_CALL_TARGET_REG = REG_IP0;

    public const regNumber REG_INTRET = REG_R0;
    public const regMask SRBM_INTRET = SRBM_R0;
    public const regMask SRBM_LNGRET = SRBM_R0;
    public const regNumber REG_INTRET_1 = REG_R1;
    public const regMask SRBM_INTRET_1 = SRBM_R1;
    public const regNumber REG_FLOATRET = REG_V0;
    public const regMask SRBM_FLOATRET = SRBM_V0;
    public const regMask SRBM_DOUBLERET = SRBM_V0;

    public static readonly regMaskTP SRBM_STOP_FOR_GC_TRASH = SRBM_CALLEE_TRASH;
    public static readonly regMaskTP SRBM_INIT_PINVOKE_FRAME_TRASH = SRBM_CALLEE_TRASH;
    public static readonly regMaskTP SRBM_INTERFACELOOKUP_FOR_SLOT_TRASH =
        new((SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH) & ~(SRBM_ARG_REGS | SRBM_FLTARG_REGS),
            SRBM_MSK_CALLEE_TRASH);
    public const regMask SRBM_INTERFACELOOKUP_FOR_SLOT_RETURN = SRBM_R15;
    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH =
        SRBM_INT_CALLEE_TRASH & ~(SRBM_ARG_REGS | SRBM_R8 | SRBM_R15);
    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_R15;
    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR = REG_R9;

    public const regNumber REG_FPBASE = REG_FP;
    public const regMask SRBM_FPBASE = SRBM_FP;
    public const string STR_FPBASE = "fp";
    public const regNumber REG_SPBASE = REG_SP;
    public const regMask SRBM_SPBASE = SRBM_ZR;
    public const string STR_SPBASE = "sp";
    public const int FIRST_ARG_STACK_OFFS = 2 * REGSIZE_BYTES;

    public const regMask SRBM_ARG_RET_BUFF = SRBM_R8;
    public const int RET_BUFF_ARGNUM = 8;
    public const regNumber REG_ARG_FIRST = REG_R0;
    public const regNumber REG_ARG_LAST = REG_R7;
    public const regNumber REG_ARG_FP_FIRST = REG_V0;
    public const regNumber REG_ARG_FP_LAST = REG_V7;
    public const int INIT_ARG_STACK_SLOT = 0;

    public const regNumber REG_ARG_0 = REG_R0;
    public const regNumber REG_ARG_1 = REG_R1;
    public const regNumber REG_ARG_2 = REG_R2;
    public const regNumber REG_ARG_3 = REG_R3;
    public const regNumber REG_ARG_4 = REG_R4;
    public const regNumber REG_ARG_5 = REG_R5;
    public const regNumber REG_ARG_6 = REG_R6;
    public const regNumber REG_ARG_7 = REG_R7;
    public const regMask SRBM_ARG_0 = SRBM_R0;
    public const regMask SRBM_ARG_1 = SRBM_R1;
    public const regMask SRBM_ARG_2 = SRBM_R2;
    public const regMask SRBM_ARG_3 = SRBM_R3;
    public const regMask SRBM_ARG_4 = SRBM_R4;
    public const regMask SRBM_ARG_5 = SRBM_R5;
    public const regMask SRBM_ARG_6 = SRBM_R6;
    public const regMask SRBM_ARG_7 = SRBM_R7;

    public const regNumber REG_FLTARG_0 = REG_V0;
    public const regNumber REG_FLTARG_1 = REG_V1;
    public const regNumber REG_FLTARG_2 = REG_V2;
    public const regNumber REG_FLTARG_3 = REG_V3;
    public const regNumber REG_FLTARG_4 = REG_V4;
    public const regNumber REG_FLTARG_5 = REG_V5;
    public const regNumber REG_FLTARG_6 = REG_V6;
    public const regNumber REG_FLTARG_7 = REG_V7;
    public const regMask SRBM_FLTARG_0 = SRBM_V0;
    public const regMask SRBM_FLTARG_1 = SRBM_V1;
    public const regMask SRBM_FLTARG_2 = SRBM_V2;
    public const regMask SRBM_FLTARG_3 = SRBM_V3;
    public const regMask SRBM_FLTARG_4 = SRBM_V4;
    public const regMask SRBM_FLTARG_5 = SRBM_V5;
    public const regMask SRBM_FLTARG_6 = SRBM_V6;
    public const regMask SRBM_FLTARG_7 = SRBM_V7;
    public const regMask SRBM_ARG_REGS =
        SRBM_ARG_0 | SRBM_ARG_1 | SRBM_ARG_2 | SRBM_ARG_3 |
        SRBM_ARG_4 | SRBM_ARG_5 | SRBM_ARG_6 | SRBM_ARG_7;
    public const regMask SRBM_FLTARG_REGS =
        SRBM_FLTARG_0 | SRBM_FLTARG_1 | SRBM_FLTARG_2 | SRBM_FLTARG_3 |
        SRBM_FLTARG_4 | SRBM_FLTARG_5 | SRBM_FLTARG_6 | SRBM_FLTARG_7;

    public const regNumber REG_SWIFT_ERROR = REG_R21;
    public const regMask SRBM_SWIFT_ERROR = SRBM_R21;
    public const regNumber REG_SWIFT_SELF = REG_R20;
    public const regMask SRBM_SWIFT_SELF = SRBM_R20;
    public static ReadOnlySpan<regNumber> REG_SWIFT_INTRET_ORDER => [REG_R0, REG_R1, REG_R2, REG_R3];
    public static ReadOnlySpan<regNumber> REG_SWIFT_FLOATRET_ORDER => [REG_V0, REG_V1, REG_V2, REG_V3];
    public const regNumber REG_UNKBASE = REG_R19;
    public const regMask SRBM_UNKBASE = SRBM_R19;
}
#endif
