// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET_WASM = CORINFO_ARCH_WASM32;

    public const int TARGET_POINTER_SIZE_WASM = 4;

    public const int MAX_PASS_SINGLEREG_BYTES_WASM = 16;

    public const int MAX_PASS_MULTIREG_BYTES_WASM = 0;

    public const int MAX_RET_MULTIREG_BYTES_WASM = 0;

    public const int MAX_ARG_REG_COUNT_WASM = 1;

    public const int MAX_RET_REG_COUNT_WASM = 1;

    public const int MAX_MULTIREG_COUNT_WASM = 2;

    public const regNumber REG_FP_FIRST_WASM = REG_WASM_NA;

    public const regNumber REG_FP_LAST_WASM = REG_WASM_NA;

    public const regNumber FIRST_FP_ARGREG_WASM = REG_WASM_NA;

    public const regNumber LAST_FP_ARGREG_WASM = REG_WASM_NA;

    public const int REGNUM_BITS_WASM = 1;

    public const int REGSIZE_BYTES_WASM = TARGET_POINTER_SIZE_WASM;

    public const int MIN_ARG_AREA_FOR_CALL_WASM = 0;

    public const int CODE_ALIGN_WASM = 1;

    public const int STACK_ALIGN_WASM = 16;

    public const regNumber FIRST_INT_CALLEE_SAVED_WASM = REG_WASM_NA;

    public const regNumber LAST_INT_CALLEE_SAVED_WASM = REG_WASM_NA;

    public static regMaskTP RBM_INT_CALLEE_SAVED => RBM_NONE;

    public static regMaskTP RBM_INT_CALLEE_TRASH => RBM_NONE;

    public const regNumber FIRST_FLT_CALLEE_SAVED_WASM = REG_WASM_NA;

    public const regNumber LAST_FLT_CALLEE_SAVED_WASM = REG_WASM_NA;

    public static regMaskTP RBM_FLT_CALLEE_SAVED => RBM_NONE;

    public static regMaskTP RBM_FLT_CALLEE_TRASH => RBM_NONE;

    public static regMaskTP RBM_CALLEE_SAVED => RBM_NONE;

    public static regMaskTP RBM_CALLEE_TRASH => RBM_NONE;

    public static regMaskTP RBM_ALLINT_WASM => RBM_NONE;

    public static regMaskTP RBM_ALLFLOAT_WASM => RBM_NONE;

    public static regMaskTP RBM_ALLDOUBLE_WASM => RBM_NONE;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_WASM => [];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_WASM => [];

    public const regMask SRBM_INT_CALLEE_SAVED_WASM = SRBM_NONE;

    public const regMask SRBM_CALLEE_SAVED_WASM = SRBM_NONE;

    public const regMask SRBM_FLT_CALLEE_SAVED_WASM = SRBM_NONE;

    public const regMask SRBM_INT_CALLEE_TRASH_WASM = SRBM_NONE;

    public const regMask SRBM_FLT_CALLEE_TRASH_WASM = SRBM_NONE;

    public const regMask SRBM_ALLINT_WASM = SRBM_NONE;

    public const regMask SRBM_ALLFLOAT_WASM = SRBM_NONE;

    public const regMask SRBM_ALLDOUBLE_WASM = SRBM_NONE;

    public const regMask SRBM_ARG_REGS_WASM = SRBM_NONE;

    public const regNumber REG_OPT_RSVD_WASM = REG_WASM_NA;

    public const regMask SRBM_OPT_RSVD_WASM = SRBM_NONE;

    // Wasm has no fixed register set; the CSE counters are pinned heuristics, not physical registers.
    public const int CNT_CALLEE_SAVED_WASM = 0;

    public const int CNT_CALLEE_SAVED_FOR_CSE = 8;

    public const int CNT_CALLEE_TRASH_WASM = 0;

    public const int CNT_CALLEE_TRASH_FOR_CSE = 8;

    public const int CNT_CALLEE_ENREG_WASM = CNT_CALLEE_SAVED_WASM;

    public const int CNT_CALLEE_ENREG_FOR_CSE = CNT_CALLEE_SAVED_FOR_CSE;

    public const int CNT_CALLEE_SAVED_FLOAT_WASM = 0;

    public const int CNT_CALLEE_SAVED_FLOAT_FOR_CSE = 10;

    public const int CNT_CALLEE_TRASH_FLOAT_WASM = 0;

    public const int CNT_CALLEE_TRASH_FLOAT_FOR_CSE = 10;

    public const int CNT_CALLEE_ENREG_FLOAT_WASM = CNT_CALLEE_SAVED_FLOAT_WASM;

    public const int CNT_CALLEE_ENREG_FLOAT_FOR_CSE = CNT_CALLEE_SAVED_FLOAT_FOR_CSE;

    public const int CNT_CALLEE_SAVED_MASK_WASM = 0;

    public const int CNT_CALLEE_SAVED_MASK_FOR_CSE = 0;

    public const int CNT_CALLEE_TRASH_MASK_WASM = 0;

    public const int CNT_CALLEE_TRASH_MASK_FOR_CSE = 0;

    public const int CNT_CALLEE_ENREG_MASK_WASM = CNT_CALLEE_SAVED_MASK_WASM;

    public const int CNT_CALLEE_ENREG_MASK_FOR_CSE = CNT_CALLEE_SAVED_MASK_FOR_CSE;

    public const regNumber REG_SECRET_STUB_PARAM_WASM = REG_WASM_NA;

    public const regNumber REG_FIRST_WASM = REG_WASM_NA;

    public const regNumber REG_INT_FIRST_WASM = REG_WASM_NA;

    public const regNumber REG_INT_LAST_WASM = REG_WASM_NA;

    public static regNumber REG_NEXT_WASM(regNumber reg) => (regNumber)((uint)reg + 1u);

    public const regNumber REG_FPBASE_WASM = REG_WASM_NA;

    public const regMask SRBM_FPBASE_WASM = SRBM_NONE;

    public const regNumber REG_SPBASE_WASM = REG_WASM_NA;

    public const regMask SRBM_SPBASE_WASM = SRBM_NONE;

    public const regNumber REG_SCRATCH_WASM = REG_WASM_NA;

    public const regNumber REG_INTRET_WASM = REG_WASM_NA;

    public const string STR_FPBASE_WASM = "";

    public const string STR_SPBASE_WASM = "";

    public const regNumber REG_ARG_0_WASM = REG_WASM_NA;

    public static regMaskTP RBM_ARG_REGS => RBM_NONE;

    public const int TARGET_MASKS_SHIFTS_WASM = 1;

    public const int TARGET_HAS_MULHI_WASM = 0;

    public const int FP_REGSIZE_BYTES_WASM = 16;

    public const int FPSAVE_REGSIZE_BYTES_WASM = 16;
}
