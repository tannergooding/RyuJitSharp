// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_R0;
    public const regNumber REG_INT_FIRST = REG_R0;
    public const regNumber REG_INT_LAST = REG_LR;
    public const int REG_INT_COUNT = (int)REG_INT_LAST - (int)REG_INT_FIRST + 1;
    public const regNumber REG_FP_FIRST = REG_F0;
    public const regNumber REG_FP_LAST = REG_F31;
    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET = REG_R12;
    public const regNumber REG_SHIFT = REG_NA;
    // Variable 64-bit shifts use a helper call with R2 as the shift amount.
    public const regNumber REG_SHIFT_LNG = REG_R2;
    public const regNumber REG_JUMP_THUNK_PARAM = REG_R12;
    // ARM write barriers take the destination in R0 and value in R1; they trash R0, R3, and R12.
    public const regNumber REG_WRITE_BARRIER_DST = REG_ARG_0;
    public const regNumber REG_WRITE_BARRIER_SRC = REG_ARG_1;
    public const regNumber REG_INDIRECT_CALL_TARGET_REG = REG_R12;
    public const regNumber REG_PINVOKE_SCRATCH = REG_R6;

    public static regNumber REG_NEXT(regNumber reg) => reg + 1;

    public static regNumber REG_PREV(regNumber reg) => reg - 1;

    public const int REGNUM_BITS = 6;
    public const int REGSIZE_BYTES = 4;
    public const int MIN_ARG_AREA_FOR_CALL = 0;
    public const int CODE_ALIGN = 2;
    public const int STACK_ALIGN = 8;

    public const regNumber REG_OPT_RSVD = REG_R10;
    public const regMask SRBM_OPT_RSVD = SRBM_R10;

    public const regMask SRBM_GSCOOKIE_TMP = SRBM_R12 | SRBM_LR;

    // This saved SP register must match the InlinedCallFrame unwinding contract.
    public const regNumber REG_SAVED_LOCALLOC_SP = REG_R9;
    public const regMask SRBM_SAVED_LOCALLOC_SP = SRBM_R9;

    public const regNumber REG_STACK_PROBE_HELPER_ARG = REG_R4;
    public const regMask SRBM_STACK_PROBE_HELPER_ARG = SRBM_R4;
    public const regNumber REG_STACK_PROBE_HELPER_CALL_TARGET = REG_R5;
    public const regMask SRBM_STACK_PROBE_HELPER_CALL_TARGET = SRBM_R5;
    public const regMask SRBM_STACK_PROBE_HELPER_TRASH = SRBM_R5 | SRBM_LR;

    public const regMask SRBM_FPBASE = SRBM_R11;
    public const regMask SRBM_INT_CALLEE_SAVED =
        SRBM_R4 | SRBM_R5 | SRBM_R6 | SRBM_R7 | SRBM_R8 | SRBM_R9 | SRBM_R10;
    public const regMask SRBM_INT_CALLEE_TRASH = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 | SRBM_R12 | SRBM_LR;
    public const regMask SRBM_FLT_CALLEE_SAVED =
        SRBM_F16 | SRBM_F17 | SRBM_F18 | SRBM_F19 | SRBM_F20 | SRBM_F21 | SRBM_F22 | SRBM_F23 |
        SRBM_F24 | SRBM_F25 | SRBM_F26 | SRBM_F27 | SRBM_F28 | SRBM_F29 | SRBM_F30 | SRBM_F31;
    public const regMask SRBM_FLT_CALLEE_TRASH =
        SRBM_F0 | SRBM_F1 | SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7 |
        SRBM_F8 | SRBM_F9 | SRBM_F10 | SRBM_F11 | SRBM_F12 | SRBM_F13 | SRBM_F14 | SRBM_F15;

    public const regMask SRBM_CALLEE_SAVED = SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED;
    public const regMask SRBM_CALLEE_TRASH = SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ALLFLOAT = SRBM_FLT_CALLEE_SAVED | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLDOUBLE =
        SRBM_F0 | SRBM_F2 | SRBM_F4 | SRBM_F6 | SRBM_F8 | SRBM_F10 | SRBM_F12 | SRBM_F14 |
        SRBM_F16 | SRBM_F18 | SRBM_F20 | SRBM_F22 | SRBM_F24 | SRBM_F26 | SRBM_F28 | SRBM_F30;
    // Double registers use even/odd pairs; this mask tracks the unused high halves.
    public const regMask SRBM_ALLDOUBLE_HIGH =
        SRBM_F1 | SRBM_F3 | SRBM_F5 | SRBM_F7 | SRBM_F9 | SRBM_F11 | SRBM_F13 | SRBM_F15 |
        SRBM_F17 | SRBM_F19 | SRBM_F21 | SRBM_F23 | SRBM_F25 | SRBM_F27 | SRBM_F29 | SRBM_F31;
    public const regMask SRBM_HIGH_REGS =
        SRBM_R8 | SRBM_R9 | SRBM_R10 | SRBM_R11 | SRBM_R12 | SRBM_SP | SRBM_LR | SRBM_PC;
    public const regMask SRBM_SHIFT = SRBM_ALLINT;
    public const regMask SRBM_SHIFT_LNG = SRBM_R2;
    public const regMask SRBM_JUMP_THUNK_PARAM = SRBM_R12;
    public const regMask SRBM_WRITE_BARRIER_DST = SRBM_R0;
    public const regMask SRBM_WRITE_BARRIER_SRC = SRBM_R1;
    public const regMask SRBM_SECRET_STUB_PARAM = SRBM_R12;
    public const regMask SRBM_PINVOKE_FRAME = SRBM_R4;
    public const regMask SRBM_PINVOKE_TCB = SRBM_R5;
    public const regMask SRBM_PINVOKE_SCRATCH = SRBM_R6;
    public const regMask SRBM_LNGRET = SRBM_R0 | SRBM_R1;
    public const regMask SRBM_LNGRET_LO = SRBM_R0;
    public const regMask SRBM_LNGRET_HI = SRBM_R1;
    public const regMask SRBM_FLOATRET = SRBM_F0;
    public const regMask SRBM_DOUBLERET = SRBM_F0 | SRBM_F1;
    // This matches the CORINFO_HELP_STOP_FOR_GC clobber mask.
    public const regMask SRBM_STOP_FOR_GC_TRASH =
        SRBM_CALLEE_TRASH &
        ~(SRBM_LNGRET | SRBM_R7 | SRBM_R8 | SRBM_R11 | SRBM_DOUBLERET |
          SRBM_F2 | SRBM_F3 | SRBM_F4 | SRBM_F5 | SRBM_F6 | SRBM_F7);
    // P/Invoke frame setup additionally clobbers the TCB and scratch registers.
    public const regMask SRBM_INIT_PINVOKE_FRAME_TRASH =
        SRBM_CALLEE_TRASH | SRBM_PINVOKE_TCB | SRBM_PINVOKE_SCRATCH;
    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH = SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ASYNC_CONTINUATION_RET = SRBM_R2;
    public const regMask SRBM_ARG_0 = SRBM_R0;
    public const regMask SRBM_ARG_1 = SRBM_R1;
    public const regMask SRBM_ARG_2 = SRBM_R2;
    public const regMask SRBM_ARG_3 = SRBM_R3;
    public static regMaskTP RBM_LOW_REGS => new(SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3 | SRBM_R4 | SRBM_R5 | SRBM_R6 | SRBM_R7);
    public static regMaskTP RBM_ALLINT => new(SRBM_ALLINT);
    public static regMaskTP RBM_ALLFLOAT => new(SRBM_ALLFLOAT);
    public static regMaskTP RBM_ALLDOUBLE => new(SRBM_ALLDOUBLE);
    public static regMaskTP RBM_ALLDOUBLE_HIGH => new(SRBM_ALLDOUBLE_HIGH);
    public static regMaskTP RBM_HIGH_REGS => new(SRBM_HIGH_REGS);
    public static regMaskTP RBM_SHIFT => new(SRBM_SHIFT);
    public static regMaskTP RBM_SHIFT_LNG => new(SRBM_SHIFT_LNG);

    public const int CNT_CALLEE_TRASH = 6;
    public const int CNT_CALLEE_TRASH_FLOAT = 16;
    public const int CNT_CALLEE_TRASH_MASK = 0;

    public const int CNT_CALLEE_SAVED = 8;
    public const int CNT_CALLEE_SAVED_FLOAT = 16;
    public const int CNT_CALLEE_SAVED_MASK = 0;
    public const int CNT_CALLEE_ENREG = CNT_CALLEE_SAVED - 1;
    public const int CNT_CALLEE_ENREG_FLOAT = CNT_CALLEE_SAVED_FLOAT;
    public const int CNT_CALLEE_ENREG_MASK = CNT_CALLEE_SAVED_MASK;
    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;
    public const int CALLEE_SAVED_FLOAT_MAXSZ = CNT_CALLEE_SAVED_FLOAT * sizeof(float);

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [
        REG_R3, REG_R2, REG_R1, REG_R0, REG_R4, REG_LR, REG_R12,
        REG_R5, REG_R6, REG_R7, REG_R8, REG_R9, REG_R10,
    ];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT => [
        REG_F8, REG_F9, REG_F10, REG_F11, REG_F12, REG_F13, REG_F14, REG_F15,
        REG_F6, REG_F7, REG_F4, REG_F5, REG_F2, REG_F3, REG_F0, REG_F1,
        REG_F16, REG_F17, REG_F18, REG_F19, REG_F20, REG_F21, REG_F22, REG_F23,
        REG_F24, REG_F25, REG_F26, REG_F27, REG_F28, REG_F29, REG_F30, REG_F31,
    ];

    public const regNumber REG_SCRATCH = REG_LR;
    public const regNumber REG_SECRET_STUB_PARAM = REG_R12;
    public const regMask SRBM_DEFAULT_HELPER_CALL_TARGET = SRBM_R12;
    public const regMask SRBM_CALLEE_TRASH_NOGC =
        SRBM_R2 | SRBM_R3 | SRBM_LR | SRBM_DEFAULT_HELPER_CALL_TARGET;
    public const regMask SRBM_CALLEE_TRASH_WRITEBARRIER =
        SRBM_R0 | SRBM_R3 | SRBM_LR | SRBM_DEFAULT_HELPER_CALL_TARGET;
    public const regMask SRBM_CALLEE_GCTRASH_WRITEBARRIER = SRBM_CALLEE_TRASH_WRITEBARRIER;
    public const regMask SRBM_PROFILER_RET_SCRATCH = SRBM_R2;
    public const regNumber REG_PROFILER_ENTER_ARG = REG_R0;
    public const regMask SRBM_PROFILER_ENTER_ARG = SRBM_R0;
    public const regNumber REG_PROFILER_RET_SCRATCH = REG_R2;
    public const regMask SRBM_PROFILER_ENTER_TRASH = SRBM_NONE;
    public const regNumber REG_PINVOKE_FRAME = REG_R4;
    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_R0;
    public const regNumber REG_FPBASE = REG_R11;
    public const regNumber REG_SPBASE = REG_SP;
    public const regMask SRBM_SPBASE = SRBM_SP;
    public const string STR_FPBASE = "r11";
    public const string STR_SPBASE = "sp";
    public const int FIRST_ARG_STACK_OFFS = 2 * REGSIZE_BYTES;

    public const regNumber REG_ARG_FIRST = REG_R0;
    public const regNumber REG_ARG_LAST = REG_R3;
    public const regNumber REG_ARG_FP_FIRST = REG_F0;
    public const regNumber REG_ARG_FP_LAST = REG_F7;
    public const int MAX_REG_ARG = 4;
    public const int MAX_FLOAT_REG_ARG = 16;
    public const int INIT_ARG_STACK_SLOT = 0;

    public const regNumber REG_ARG_2 = REG_R2;
    public const regNumber REG_ARG_3 = REG_R3;
    public const regMask SRBM_ARG_REGS = SRBM_R0 | SRBM_R1 | SRBM_R2 | SRBM_R3;
}
#endif
