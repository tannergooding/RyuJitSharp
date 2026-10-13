// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST_X86 = REG_X86_EAX;
    public const regNumber REG_INT_FIRST_X86 = REG_X86_EAX;
    public const regNumber REG_INT_LAST_X86 = REG_X86_EDI;
    public const int REG_INT_COUNT_X86 = (int)REG_INT_LAST_X86 - (int)REG_INT_FIRST_X86 + 1;
    public const regNumber REG_FP_FIRST_X86 = REG_X86_XMM0;
    public const regNumber REG_FP_LAST_X86 = REG_X86_XMM7;
    public const regNumber REG_MASK_FIRST_X86 = REG_X86_K0;
    public const regNumber REG_MASK_LAST_X86 = REG_X86_K7;

    public static regNumber REG_NEXT_X86(regNumber reg) => (regNumber)((uint)reg + 1u);
    public static regNumber REG_PREV_X86(regNumber reg) => (regNumber)((uint)reg - 1u);

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_X86 =>
        [REG_X86_EAX, REG_X86_EDX, REG_X86_ECX, REG_X86_ESI, REG_X86_EDI, REG_X86_EBX];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_FLT_X86 =>
        [REG_X86_XMM0, REG_X86_XMM1, REG_X86_XMM2, REG_X86_XMM3, REG_X86_XMM4, REG_X86_XMM5, REG_X86_XMM6, REG_X86_XMM7];

    public static ReadOnlySpan<regNumber> REG_VAR_ORDER_MSK_X86 =>
        [REG_X86_K1, REG_X86_K2, REG_X86_K3, REG_X86_K4, REG_X86_K5, REG_X86_K6, REG_X86_K7];

    public const regNumber REG_LNGARG_LO_X86 = REG_X86_EAX;
    public const regNumber REG_LNGARG_HI_X86 = REG_X86_EDX;
    public const regNumber REG_SCRATCH_X86 = REG_X86_EAX;
    public const regNumber REG_R2R_INDIRECT_PARAM_X86 = REG_X86_EAX;
    public const regNumber REG_WRITE_BARRIER_DST_X86 = REG_ARG_0_X86;
    public const regNumber REG_WRITE_BARRIER_SRC_X86 = REG_ARG_1_X86;
    public const regNumber REG_PINVOKE_FRAME_X86 = REG_X86_EDI;
    public const regNumber REG_PINVOKE_TCB_X86 = REG_X86_ESI;
    public const regNumber REG_PINVOKE_SCRATCH_X86 = REG_X86_EAX;
    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR_X86 = REG_X86_ECX;
    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR_X86 = REG_X86_NA;
    public const regNumber REG_SWIFT_ERROR_X86 = REG_X86_NA;
    public const regNumber REG_SWIFT_SELF_X86 = REG_X86_NA;
    public const regNumber REG_FLT_CALLEE_SAVED_FIRST_X86 = REG_X86_XMM6;
    public const regNumber REG_FLT_CALLEE_SAVED_LAST_X86 = REG_X86_XMM7;

    public const regMask SRBM_FPBASE_X86 = SRBM_X86_EBP;
    public const regMask SRBM_SPBASE_X86 = SRBM_X86_ESP;
    public const regMask SRBM_INTRET_X86 = SRBM_X86_EAX;
    public const regMask SRBM_ARG_REGS_X86 = SRBM_X86_ECX | SRBM_X86_EDX;
    public const regMask SRBM_LNGARG_LO = SRBM_X86_EAX;
    public const regMask SRBM_LNGARG_HI = SRBM_X86_EDX;
    public const regMask SRBM_WRITE_BARRIER_DST_X86 = SRBM_X86_ECX;
    public const regMask SRBM_WRITE_BARRIER_SRC_X86 = SRBM_X86_EDX;
    public const regMask SRBM_R2R_INDIRECT_PARAM_X86 = SRBM_X86_EAX;
    public const regMask SRBM_PINVOKE_FRAME_X86 = SRBM_X86_EDI;
    public const regMask SRBM_PINVOKE_TCB_X86 = SRBM_X86_ESI;
    public const regMask SRBM_PINVOKE_SCRATCH_X86 = SRBM_X86_EAX;
    public const regMask SRBM_VALIDATE_INDIRECT_CALL_TRASH_X86 = SRBM_X86_EAX | SRBM_X86_EDX;
    public const regMask SRBM_INT_CALLEE_SAVED_X86 = SRBM_X86_EBX | SRBM_X86_ESI | SRBM_X86_EDI;
    public const regMask SRBM_INT_CALLEE_TRASH_X86 = SRBM_X86_EAX | SRBM_X86_ECX | SRBM_X86_EDX;
    public const regMask SRBM_CALLEE_SAVED_X86 =
        SRBM_INT_CALLEE_SAVED_X86 | SRBM_FLT_CALLEE_SAVED_X86 | SRBM_MSK_CALLEE_SAVED_X86;
    public const regMask SRBM_ALLINT_X86 = SRBM_INT_CALLEE_SAVED_X86 | SRBM_INT_CALLEE_TRASH_X86;
    public static regMaskTP RBM_NON_BYTE_REGS => RBM_ESI | RBM_EDI;

    public const regMask SRBM_ALLFLOAT_X86 =
        SRBM_X86_XMM0 | SRBM_X86_XMM1 | SRBM_X86_XMM2 | SRBM_X86_XMM3 |
        SRBM_X86_XMM4 | SRBM_X86_XMM5 | SRBM_X86_XMM6 | SRBM_X86_XMM7;
    public const regMask SRBM_ALLDOUBLE_X86 = SRBM_ALLFLOAT_X86;
    public const regMask SRBM_FLT_CALLEE_SAVED_X86 = SRBM_NONE;
    public const regMask SRBM_FLT_CALLEE_TRASH_X86 = SRBM_ALLFLOAT_X86;

    public const regMask SRBM_ALLMASK_INIT_X86 = SRBM_NONE;
    public const regMask SRBM_ALLMASK_EVEX_X86 =
        SRBM_X86_K1 | SRBM_X86_K2 | SRBM_X86_K3 | SRBM_X86_K4 | SRBM_X86_K5 | SRBM_X86_K6 | SRBM_X86_K7;
    public const regMask SRBM_MSK_CALLEE_SAVED_X86 = SRBM_NONE;
    public const regMask SRBM_MSK_CALLEE_TRASH_INIT_X86 = SRBM_NONE;
    public const regMask SRBM_MSK_CALLEE_TRASH_EVEX_X86 = SRBM_ALLMASK_EVEX_X86;

    public const int CNT_HIGHFLOAT_X86 = 0;
    public const int CNT_MASK_REGS_X86 = 8;
    public const int CNT_CALLEE_TRASH_X86 = 3;
    public const int CNT_CALLEE_TRASH_FLOAT_X86 = 6;
    public const int CNT_CALLEE_TRASH_MASK_INIT_X86 = 0;
    public const int CNT_CALLEE_TRASH_MASK_EVEX_X86 = 7;

    public const int CNT_CALLEE_SAVED_X86 = 4;
    public const int CNT_CALLEE_ENREG_X86 = CNT_CALLEE_SAVED_X86 - 1;
    public const int CNT_CALLEE_SAVED_FLOAT_X86 = 0;
    public const int CNT_CALLEE_ENREG_FLOAT_X86 = CNT_CALLEE_SAVED_FLOAT_X86;
    public const int CNT_CALLEE_SAVED_MASK_X86 = 0;
    public const int CNT_CALLEE_ENREG_MASK_X86 = CNT_CALLEE_SAVED_MASK_X86;
    public const int CALLEE_SAVED_REG_MAXSZ_X86 = CNT_CALLEE_SAVED_X86 * REGSIZE_BYTES_X86;

    public const string STR_FPBASE_X86 = "ebp";
    public const string STR_SPBASE_X86 = "esp";
}
