// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_EAX;
    public const regNumber REG_INT_FIRST = REG_EAX;
    public const regNumber REG_INT_LAST = REG_EDI;
    public const int REG_INT_COUNT = (int)REG_INT_LAST - (int)REG_INT_FIRST + 1;
    public const regNumber REG_FP_FIRST = REG_XMM0;
    public const regNumber REG_FP_LAST = REG_XMM7;
    public const regNumber REG_MASK_FIRST = REG_K0;
    public const regNumber REG_MASK_LAST = REG_K7;

    public static regNumber REG_NEXT(regNumber reg) => (regNumber)((uint)reg + 1u);
    public static regNumber REG_PREV(regNumber reg) => (regNumber)((uint)reg - 1u);

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER =>
        [REG_EAX, REG_EDX, REG_ECX, REG_ESI, REG_EDI, REG_EBX];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT =>
        [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_XMM4, REG_XMM5, REG_XMM6, REG_XMM7];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_MSK =>
        [REG_K1, REG_K2, REG_K3, REG_K4, REG_K5, REG_K6, REG_K7];

    public const regNumber REG_LNGARG_LO = REG_EAX;
    public const regNumber REG_LNGARG_HI = REG_EDX;
    public const regNumber REG_SCRATCH = REG_EAX;
    public const regNumber REG_R2R_INDIRECT_PARAM = REG_EAX;
    public const regNumber REG_WRITE_BARRIER_DST = REG_ARG_0;
    public const regNumber REG_WRITE_BARRIER_SRC = REG_ARG_1;
    public const regNumber REG_PINVOKE_FRAME = REG_EDI;
    public const regNumber REG_PINVOKE_TCB = REG_ESI;
    public const regNumber REG_PINVOKE_SCRATCH = REG_EAX;
    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_ECX;
    public const regNumber REG_FLT_CALLEE_SAVED_FIRST = REG_XMM6;
    public const regNumber REG_FLT_CALLEE_SAVED_LAST = REG_XMM7;

    public const regMask SRBM_FPBASE = SRBM_EBP;
    public const regMask SRBM_SPBASE = SRBM_ESP;
    public const regMask SRBM_INTRET = SRBM_EAX;
    public const regMask SRBM_ARG_REGS = SRBM_ECX | SRBM_EDX;
    public const regMask SRBM_LNGARG_LO = SRBM_EAX;
    public const regMask SRBM_LNGARG_HI = SRBM_EDX;
    public const regMask SRBM_WRITE_BARRIER_DST = SRBM_ECX;
    public const regMask SRBM_WRITE_BARRIER_SRC = SRBM_EDX;
    public const regMask SRBM_R2R_INDIRECT_PARAM = SRBM_EAX;
    public const regMask SRBM_PINVOKE_FRAME = SRBM_EDI;
    public const regMask SRBM_PINVOKE_TCB = SRBM_ESI;
    public const regMask SRBM_PINVOKE_SCRATCH = SRBM_EAX;
    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH = SRBM_EAX | SRBM_EDX;
    public const regMask SRBM_INT_CALLEE_SAVED = SRBM_EBX | SRBM_ESI | SRBM_EDI;
    public const regMask SRBM_INT_CALLEE_TRASH = SRBM_EAX | SRBM_ECX | SRBM_EDX;
    public const regMask SRBM_CALLEE_SAVED =
        SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED | SRBM_MSK_CALLEE_SAVED;
    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public static regMaskTP RBM_NON_BYTE_REGS => RBM_ESI | RBM_EDI;

    public const regMask SRBM_ALLFLOAT =
        SRBM_XMM0 | SRBM_XMM1 | SRBM_XMM2 | SRBM_XMM3 |
        SRBM_XMM4 | SRBM_XMM5 | SRBM_XMM6 | SRBM_XMM7;
    public const regMask SRBM_ALLDOUBLE = SRBM_ALLFLOAT;
    public const regMask SRBM_FLT_CALLEE_SAVED = SRBM_NONE;
    public const regMask SRBM_FLT_CALLEE_TRASH = SRBM_ALLFLOAT;

    public const regMask SRBM_ALLMASK_INIT = SRBM_NONE;
    public const regMask SRBM_ALLMASK_EVEX =
        SRBM_K1 | SRBM_K2 | SRBM_K3 | SRBM_K4 | SRBM_K5 | SRBM_K6 | SRBM_K7;
    public const regMask SRBM_MSK_CALLEE_SAVED = SRBM_NONE;
    public const regMask SRBM_MSK_CALLEE_TRASH_INIT = SRBM_NONE;
    public const regMask SRBM_MSK_CALLEE_TRASH_EVEX = SRBM_ALLMASK_EVEX;

    public const int CNT_HIGHFLOAT = 0;
    public const int CNT_MASK_REGS = 8;
    public const int CNT_CALLEE_TRASH = 3;
    public const int CNT_CALLEE_TRASH_FLOAT = 6;
    public const int CNT_CALLEE_TRASH_MASK_INIT = 0;
    public const int CNT_CALLEE_TRASH_MASK_EVEX = 7;

    public const int CNT_CALLEE_SAVED = 4;
    public const int CNT_CALLEE_ENREG = CNT_CALLEE_SAVED - 1;
    public const int CNT_CALLEE_SAVED_FLOAT = 0;
    public const int CNT_CALLEE_ENREG_FLOAT = CNT_CALLEE_SAVED_FLOAT;
    public const int CNT_CALLEE_SAVED_MASK = 0;
    public const int CNT_CALLEE_ENREG_MASK = CNT_CALLEE_SAVED_MASK;
    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;

    public const string STR_FPBASE = "ebp";
    public const string STR_SPBASE = "esp";
}
#endif
