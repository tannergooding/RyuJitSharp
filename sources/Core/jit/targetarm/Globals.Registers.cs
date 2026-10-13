// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST_ARM = REG_ARM32_R0;
    public const regNumber REG_INT_FIRST_ARM = REG_ARM32_R0;
    public const regNumber REG_INT_LAST_ARM = REG_ARM32_LR;
    public const int REG_INT_COUNT_ARM = (int)REG_INT_LAST_ARM - (int)REG_INT_FIRST_ARM + 1;
    public const regNumber REG_FP_FIRST_ARM = REG_ARM32_F0;
    public const regNumber REG_FP_LAST_ARM = REG_ARM32_F31;
    public const regNumber REG_DEFAULT_HELPER_CALL_TARGET_ARM = REG_ARM32_R12;
    public const regNumber REG_SHIFT_ARM = REG_ARM32_NA;
    // Variable 64-bit shifts use a helper call with R2 as the shift amount.
    public const regNumber REG_SHIFT_LNG = REG_ARM32_R2;
    public const regNumber REG_JUMP_THUNK_PARAM_ARM = REG_ARM32_R12;
    // ARM write barriers take the destination in R0 and value in R1; they trash R0, R3, and R12.
    public const regNumber REG_WRITE_BARRIER_DST_ARM = REG_ARG_0_ARM;
    public const regNumber REG_WRITE_BARRIER_SRC_ARM = REG_ARG_1_ARM;
    public const regNumber REG_INDIRECT_CALL_TARGET_REG_ARM = REG_ARM32_R12;
    public const regNumber REG_PINVOKE_SCRATCH_ARM = REG_ARM32_R6;
    public const int REGNUM_BITS_ARM = 6;
    public const int REGSIZE_BYTES_ARM = 4;
    public const int MIN_ARG_AREA_FOR_CALL_ARM = 0;
    public const int CODE_ALIGN_ARM = 2;
    public const int STACK_ALIGN_ARM = 8;

    public const regNumber REG_OPT_RSVD_ARM = REG_ARM32_R10;
    public const regMask SRBM_OPT_RSVD_ARM = SRBM_ARM32_R10;

    public const regMask SRBM_GSCOOKIE_TMP_ARM = SRBM_ARM32_R12 | SRBM_ARM32_LR;

    // This saved SP register must match the InlinedCallFrame unwinding contract.
    public const regNumber REG_SAVED_LOCALLOC_SP = REG_ARM32_R9;
    public const regMask SRBM_SAVED_LOCALLOC_SP = SRBM_ARM32_R9;

    public const regNumber REG_STACK_PROBE_HELPER_ARG_ARM = REG_ARM32_R4;
    public const regMask SRBM_STACK_PROBE_HELPER_ARG_ARM = SRBM_ARM32_R4;
    public const regNumber REG_STACK_PROBE_HELPER_CALL_TARGET = REG_ARM32_R5;
    public const regMask SRBM_STACK_PROBE_HELPER_CALL_TARGET = SRBM_ARM32_R5;
    public const regMask SRBM_STACK_PROBE_HELPER_TRASH_ARM = SRBM_ARM32_R5 | SRBM_ARM32_LR;

    public const regMask SRBM_FPBASE_ARM = SRBM_ARM32_R11;
    public const regMask SRBM_INT_CALLEE_SAVED_ARM =
        SRBM_ARM32_R4 | SRBM_ARM32_R5 | SRBM_ARM32_R6 | SRBM_ARM32_R7 | SRBM_ARM32_R8 | SRBM_ARM32_R9 | SRBM_ARM32_R10;
    public const regMask SRBM_INT_CALLEE_TRASH_ARM = SRBM_ARM32_R0 | SRBM_ARM32_R1 | SRBM_ARM32_R2 | SRBM_ARM32_R3 | SRBM_ARM32_R12 | SRBM_ARM32_LR;
    public const regMask SRBM_FLT_CALLEE_SAVED_ARM =
        SRBM_ARM32_F16 | SRBM_ARM32_F17 | SRBM_ARM32_F18 | SRBM_ARM32_F19 | SRBM_ARM32_F20 | SRBM_ARM32_F21 | SRBM_ARM32_F22 | SRBM_ARM32_F23 |
        SRBM_ARM32_F24 | SRBM_ARM32_F25 | SRBM_ARM32_F26 | SRBM_ARM32_F27 | SRBM_ARM32_F28 | SRBM_ARM32_F29 | SRBM_ARM32_F30 | SRBM_ARM32_F31;
    public const regMask SRBM_FLT_CALLEE_TRASH_ARM =
        SRBM_ARM32_F0 | SRBM_ARM32_F1 | SRBM_ARM32_F2 | SRBM_ARM32_F3 | SRBM_ARM32_F4 | SRBM_ARM32_F5 | SRBM_ARM32_F6 | SRBM_ARM32_F7 |
        SRBM_ARM32_F8 | SRBM_ARM32_F9 | SRBM_ARM32_F10 | SRBM_ARM32_F11 | SRBM_ARM32_F12 | SRBM_ARM32_F13 | SRBM_ARM32_F14 | SRBM_ARM32_F15;

    public const regMask SRBM_CALLEE_SAVED_ARM = SRBM_INT_CALLEE_SAVED_ARM | SRBM_FLT_CALLEE_SAVED_ARM;
    public const regMask SRBM_CALLEE_TRASH_ARM = SRBM_INT_CALLEE_TRASH_ARM | SRBM_FLT_CALLEE_TRASH_ARM;
    public const regMask SRBM_ALLINT_ARM = SRBM_INT_CALLEE_SAVED_ARM | SRBM_INT_CALLEE_TRASH_ARM;
    public const regMask SRBM_ALLFLOAT_ARM = SRBM_FLT_CALLEE_SAVED_ARM | SRBM_FLT_CALLEE_TRASH_ARM;
    public const regMask SRBM_ALLDOUBLE_ARM =
        SRBM_ARM32_F0 | SRBM_ARM32_F2 | SRBM_ARM32_F4 | SRBM_ARM32_F6 | SRBM_ARM32_F8 | SRBM_ARM32_F10 | SRBM_ARM32_F12 | SRBM_ARM32_F14 |
        SRBM_ARM32_F16 | SRBM_ARM32_F18 | SRBM_ARM32_F20 | SRBM_ARM32_F22 | SRBM_ARM32_F24 | SRBM_ARM32_F26 | SRBM_ARM32_F28 | SRBM_ARM32_F30;
    // Double registers use even/odd pairs; this mask tracks the unused high halves.
    public const regMask SRBM_ALLDOUBLE_HIGH =
        SRBM_ARM32_F1 | SRBM_ARM32_F3 | SRBM_ARM32_F5 | SRBM_ARM32_F7 | SRBM_ARM32_F9 | SRBM_ARM32_F11 | SRBM_ARM32_F13 | SRBM_ARM32_F15 |
        SRBM_ARM32_F17 | SRBM_ARM32_F19 | SRBM_ARM32_F21 | SRBM_ARM32_F23 | SRBM_ARM32_F25 | SRBM_ARM32_F27 | SRBM_ARM32_F29 | SRBM_ARM32_F31;
    public const regMask SRBM_HIGH_REGS =
        SRBM_ARM32_R8 | SRBM_ARM32_R9 | SRBM_ARM32_R10 | SRBM_ARM32_R11 | SRBM_ARM32_R12 | SRBM_ARM32_SP | SRBM_ARM32_LR | SRBM_ARM32_PC;
    public const regMask SRBM_SHIFT_ARM = SRBM_ALLINT_ARM;
    public const regMask SRBM_SHIFT_LNG = SRBM_ARM32_R2;
    public const regMask SRBM_JUMP_THUNK_PARAM_ARM = SRBM_ARM32_R12;
    public const regMask SRBM_WRITE_BARRIER_DST_ARM = SRBM_ARM32_R0;
    public const regMask SRBM_WRITE_BARRIER_SRC_ARM = SRBM_ARM32_R1;
    public const regMask SRBM_SECRET_STUB_PARAM_ARM = SRBM_ARM32_R12;
    public const regMask SRBM_PINVOKE_FRAME_ARM = SRBM_ARM32_R4;
    public const regMask SRBM_PINVOKE_TCB_ARM = SRBM_ARM32_R5;
    public const regMask SRBM_PINVOKE_SCRATCH_ARM = SRBM_ARM32_R6;
    public const regMask SRBM_LNGRET_ARM = SRBM_ARM32_R0 | SRBM_ARM32_R1;
    public const regMask SRBM_LNGRET_LO = SRBM_ARM32_R0;
    public const regMask SRBM_LNGRET_HI = SRBM_ARM32_R1;
    public const regMask SRBM_FLOATRET_ARM = SRBM_ARM32_F0;
    public const regMask SRBM_DOUBLERET_ARM = SRBM_ARM32_F0 | SRBM_ARM32_F1;
    // This matches the CORINFO_HELP_STOP_FOR_GC clobber mask.
    public const regMask SRBM_STOP_FOR_GC_TRASH_ARM =
        SRBM_CALLEE_TRASH_ARM &
        ~(SRBM_LNGRET_ARM | SRBM_ARM32_R7 | SRBM_ARM32_R8 | SRBM_ARM32_R11 | SRBM_DOUBLERET_ARM |
          SRBM_ARM32_F2 | SRBM_ARM32_F3 | SRBM_ARM32_F4 | SRBM_ARM32_F5 | SRBM_ARM32_F6 | SRBM_ARM32_F7);
    // P/Invoke frame setup additionally clobbers the TCB and scratch registers.
    public const regMask SRBM_INIT_PINVOKE_FRAME_TRASH_ARM =
        SRBM_CALLEE_TRASH_ARM | SRBM_PINVOKE_TCB_ARM | SRBM_PINVOKE_SCRATCH_ARM;
    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM = SRBM_INT_CALLEE_TRASH_ARM;
    public const regMask SRBM_ASYNC_CONTINUATION_RET_ARM = SRBM_ARM32_R2;
    public const regMask SRBM_ARG_0_ARM = SRBM_ARM32_R0;
    public const regMask SRBM_ARG_1_ARM = SRBM_ARM32_R1;
    public const regMask SRBM_ARG_2_ARM = SRBM_ARM32_R2;
    public const regMask SRBM_ARG_3_ARM = SRBM_ARM32_R3;
    public static regMaskTP RBM_LOW_REGS => new(SRBM_ARM32_R0 | SRBM_ARM32_R1 | SRBM_ARM32_R2 | SRBM_ARM32_R3 | SRBM_ARM32_R4 | SRBM_ARM32_R5 | SRBM_ARM32_R6 | SRBM_ARM32_R7);
    public static regMaskTP RBM_ALLINT_ARM => new(SRBM_ALLINT_ARM);
    public static regMaskTP RBM_ALLFLOAT_ARM => new(SRBM_ALLFLOAT_ARM);
    public static regMaskTP RBM_ALLDOUBLE_ARM => new(SRBM_ALLDOUBLE_ARM);
    public static regMaskTP RBM_ALLDOUBLE_HIGH => new(SRBM_ALLDOUBLE_HIGH);
    public static regMaskTP RBM_HIGH_REGS => new(SRBM_HIGH_REGS);
    public static regMaskTP RBM_SHIFT => new(SRBM_SHIFT_ARM);
    public static regMaskTP RBM_SHIFT_LNG => new(SRBM_SHIFT_LNG);

    public const int CNT_CALLEE_TRASH_ARM = 6;
    public const int CNT_CALLEE_TRASH_FLOAT_ARM = 16;
    public const int CNT_CALLEE_TRASH_MASK_ARM = 0;

    public const int CNT_CALLEE_SAVED_ARM = 8;
    public const int CNT_CALLEE_SAVED_FLOAT_ARM = 16;
    public const int CNT_CALLEE_SAVED_MASK_ARM = 0;
    public const int CNT_CALLEE_ENREG_ARM = CNT_CALLEE_SAVED_ARM - 1;
    public const int CNT_CALLEE_ENREG_FLOAT_ARM = CNT_CALLEE_SAVED_FLOAT_ARM;
    public const int CNT_CALLEE_ENREG_MASK_ARM = CNT_CALLEE_SAVED_MASK_ARM;
    public const int CALLEE_SAVED_REG_MAXSZ_ARM = CNT_CALLEE_SAVED_ARM * REGSIZE_BYTES_ARM;
    public const int CALLEE_SAVED_FLOAT_MAXSZ_ARM = CNT_CALLEE_SAVED_FLOAT_ARM * sizeof(float);

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_ARM => [
        REG_ARM32_R3, REG_ARM32_R2, REG_ARM32_R1, REG_ARM32_R0, REG_ARM32_R4, REG_ARM32_LR, REG_ARM32_R12,
        REG_ARM32_R5, REG_ARM32_R6, REG_ARM32_R7, REG_ARM32_R8, REG_ARM32_R9, REG_ARM32_R10,
    ];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_ARM => [
        REG_ARM32_F8, REG_ARM32_F9, REG_ARM32_F10, REG_ARM32_F11, REG_ARM32_F12, REG_ARM32_F13, REG_ARM32_F14, REG_ARM32_F15,
        REG_ARM32_F6, REG_ARM32_F7, REG_ARM32_F4, REG_ARM32_F5, REG_ARM32_F2, REG_ARM32_F3, REG_ARM32_F0, REG_ARM32_F1,
        REG_ARM32_F16, REG_ARM32_F17, REG_ARM32_F18, REG_ARM32_F19, REG_ARM32_F20, REG_ARM32_F21, REG_ARM32_F22, REG_ARM32_F23,
        REG_ARM32_F24, REG_ARM32_F25, REG_ARM32_F26, REG_ARM32_F27, REG_ARM32_F28, REG_ARM32_F29, REG_ARM32_F30, REG_ARM32_F31,
    ];

    public const regNumber REG_SCRATCH_ARM = REG_ARM32_LR;
    public const regNumber REG_SECRET_STUB_PARAM_ARM = REG_ARM32_R12;
    public const regMask SRBM_DEFAULT_HELPER_CALL_TARGET_ARM = SRBM_ARM32_R12;
    public const regMask SRBM_CALLEE_TRASH_NOGC_ARM =
        SRBM_ARM32_R2 | SRBM_ARM32_R3 | SRBM_ARM32_LR | SRBM_DEFAULT_HELPER_CALL_TARGET_ARM;
    public const regMask SRBM_CALLEE_TRASH_WRITEBARRIER_ARM =
        SRBM_ARM32_R0 | SRBM_ARM32_R3 | SRBM_ARM32_LR | SRBM_DEFAULT_HELPER_CALL_TARGET_ARM;
    public const regMask SRBM_CALLEE_GCTRASH_WRITEBARRIER_ARM = SRBM_CALLEE_TRASH_WRITEBARRIER_ARM;
    public const regMask SRBM_PROFILER_RET_SCRATCH = SRBM_ARM32_R2;
    public const regNumber REG_PROFILER_ENTER_ARG = REG_ARM32_R0;
    public const regMask SRBM_PROFILER_ENTER_ARG = SRBM_ARM32_R0;
    public const regNumber REG_PROFILER_RET_SCRATCH = REG_ARM32_R2;
    public const regMask SRBM_PROFILER_ENTER_TRASH_ARM = SRBM_NONE;
    public const regNumber REG_PINVOKE_FRAME_ARM = REG_ARM32_R4;
    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR_ARM = REG_ARM32_R0;
    public const regNumber REG_LNGARG_LO_ARM = REG_ARM32_NA;
    public const regNumber REG_LNGARG_HI_ARM = REG_ARM32_NA;
    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR_ARM = REG_ARM32_NA;
    public const regNumber REG_SWIFT_ERROR_ARM = REG_ARM32_NA;
    public const regNumber REG_SWIFT_SELF_ARM = REG_ARM32_NA;
    public const regNumber REG_FPBASE_ARM = REG_ARM32_R11;
    public const regNumber REG_SPBASE_ARM = REG_ARM32_SP;
    public const regMask SRBM_SPBASE_ARM = SRBM_ARM32_SP;
    public const string STR_FPBASE_ARM = "r11";
    public const string STR_SPBASE_ARM = "sp";
    public const int FIRST_ARG_STACK_OFFS_ARM = 2 * REGSIZE_BYTES_ARM;

    public const regNumber REG_ARG_FIRST_ARM = REG_ARM32_R0;
    public const regNumber REG_ARG_LAST_ARM = REG_ARM32_R3;
    public const regNumber REG_ARG_FP_FIRST_ARM = REG_ARM32_F0;
    public const regNumber REG_ARG_FP_LAST_ARM = REG_ARM32_F7;
    public const int MAX_REG_ARG_ARM = 4;
    public const int MAX_FLOAT_REG_ARG_ARM = 16;
    public const int INIT_ARG_STACK_SLOT_ARM = 0;

    public const regNumber REG_ARG_2_ARM = REG_ARM32_R2;
    public const regNumber REG_ARG_3_ARM = REG_ARM32_R3;
    public const regMask SRBM_ARG_REGS_ARM = SRBM_ARM32_R0 | SRBM_ARM32_R1 | SRBM_ARM32_R2 | SRBM_ARM32_R3;
}
