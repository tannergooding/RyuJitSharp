// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_EAX;
    public const regNumber REG_INT_FIRST = REG_EAX;
    public const regNumber REG_INT_LAST = REG_EDI;
    public const regNumber REG_FP_FIRST = REG_XMM0;
    public const regNumber REG_FP_LAST = REG_XMM7;
    public const regNumber REG_MASK_FIRST = REG_K0;
    public const regNumber REG_MASK_LAST = REG_K7;

    public const regMask SRBM_FPBASE = SRBM_EBP;
    public const regMask SRBM_INT_CALLEE_SAVED = SRBM_EBX | SRBM_ESI | SRBM_EDI;
    public const regMask SRBM_INT_CALLEE_TRASH = SRBM_EAX | SRBM_ECX | SRBM_EDX;
    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;

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
    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;
}
#endif
