// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET = CORINFO_ARCH_WASM32;

    public const int TARGET_POINTER_SIZE = 4;

    public const int MAX_PASS_SINGLEREG_BYTES = 16;

    public const int MAX_PASS_MULTIREG_BYTES = 0;

    public const int MAX_RET_REG_COUNT = 1;

    public const int MAX_MULTIREG_COUNT = 2;

    public const regNumber REG_FP_FIRST = REG_NA;

    public const regNumber REG_FP_LAST = REG_NA;

    public const regNumber FIRST_FP_ARGREG = REG_NA;

    public const regNumber LAST_FP_ARGREG = REG_NA;

    public const int REGNUM_BITS = 1;

    public const int REGSIZE_BYTES = TARGET_POINTER_SIZE;

    public const int MIN_ARG_AREA_FOR_CALL = 0;

    public const int CODE_ALIGN = 1;

    public const int STACK_ALIGN = 16;

    public const regNumber FIRST_INT_CALLEE_SAVED = REG_NA;

    public const regNumber LAST_INT_CALLEE_SAVED = REG_NA;

    public static regMaskTP RBM_INT_CALLEE_SAVED => RBM_NONE;

    public static regMaskTP RBM_INT_CALLEE_TRASH => RBM_NONE;

    public const regNumber FIRST_FLT_CALLEE_SAVED = REG_NA;

    public const regNumber LAST_FLT_CALLEE_SAVED = REG_NA;

    public static regMaskTP RBM_FLT_CALLEE_SAVED => RBM_NONE;

    public static regMaskTP RBM_FLT_CALLEE_TRASH => RBM_NONE;

    public static regMaskTP RBM_CALLEE_SAVED => RBM_NONE;

    public static regMaskTP RBM_CALLEE_TRASH => RBM_NONE;

    public static regMaskTP RBM_ALLINT => RBM_NONE;

    public static regMaskTP RBM_ALLFLOAT => RBM_NONE;

    public static regMaskTP RBM_ALLDOUBLE => RBM_NONE;

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER => [];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT => [];

    public static ReadOnlySpan<regNumber> IntArgRegs => [];

    public static ReadOnlySpan<regNumber> FltArgRegs => [];

    public const regMask SRBM_INT_CALLEE_SAVED = SRBM_NONE;

    public const regMask SRBM_FLT_CALLEE_SAVED = SRBM_NONE;

    public const regMask SRBM_INT_CALLEE_TRASH = SRBM_NONE;

    public const regMask SRBM_FLT_CALLEE_TRASH = SRBM_NONE;

    public const regMask SRBM_ALLINT = SRBM_NONE;

    public const regMask SRBM_ALLFLOAT = SRBM_NONE;

    public const regMask SRBM_ALLDOUBLE = SRBM_NONE;

    public const regMask SRBM_ARG_REGS = SRBM_NONE;

    // Wasm has no fixed register set; the CSE counters are pinned heuristics, not physical registers.
    public const int CNT_CALLEE_SAVED = 0;

    public const int CNT_CALLEE_SAVED_FOR_CSE = 8;

    public const int CNT_CALLEE_TRASH = 0;

    public const int CNT_CALLEE_TRASH_FOR_CSE = 8;

    public const int CNT_CALLEE_ENREG = CNT_CALLEE_SAVED;

    public const int CNT_CALLEE_ENREG_FOR_CSE = CNT_CALLEE_SAVED_FOR_CSE;

    public const int CNT_CALLEE_SAVED_FLOAT = 0;

    public const int CNT_CALLEE_SAVED_FLOAT_FOR_CSE = 10;

    public const int CNT_CALLEE_TRASH_FLOAT = 0;

    public const int CNT_CALLEE_TRASH_FLOAT_FOR_CSE = 10;

    public const int CNT_CALLEE_ENREG_FLOAT = CNT_CALLEE_SAVED_FLOAT;

    public const int CNT_CALLEE_ENREG_FLOAT_FOR_CSE = CNT_CALLEE_SAVED_FLOAT_FOR_CSE;

    public const int CNT_CALLEE_SAVED_MASK = 0;

    public const int CNT_CALLEE_SAVED_MASK_FOR_CSE = 0;

    public const int CNT_CALLEE_TRASH_MASK = 0;

    public const int CNT_CALLEE_TRASH_MASK_FOR_CSE = 0;

    public const int CNT_CALLEE_ENREG_MASK = CNT_CALLEE_SAVED_MASK;

    public const int CNT_CALLEE_ENREG_MASK_FOR_CSE = CNT_CALLEE_SAVED_MASK_FOR_CSE;

    public const regNumber REG_SECRET_STUB_PARAM = REG_NA;

    public const regNumber REG_FIRST = REG_NA;

    public const regNumber REG_INT_FIRST = REG_NA;

    public const regNumber REG_INT_LAST = REG_NA;

    public static regNumber REG_NEXT(regNumber reg) => (regNumber)((uint)reg + 1u);

    public const regNumber REG_FPBASE = REG_NA;

    public const regNumber REG_SPBASE = REG_NA;

    public const regNumber REG_SCRATCH = REG_NA;

    public const regNumber REG_INTRET = REG_NA;

    public const regNumber REG_FLOATRET = REG_NA;

    public const string STR_FPBASE = "";

    public const string STR_SPBASE = "";

    public const regNumber REG_ARG_0 = REG_NA;

    public static regMaskTP RBM_ARG_REGS => RBM_NONE;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 0;

    public const int FP_REGSIZE_BYTES = 16;

    public const int FPSAVE_REGSIZE_BYTES = 16;
}
#endif
