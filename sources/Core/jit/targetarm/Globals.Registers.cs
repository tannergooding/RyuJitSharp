// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_R0;
    public const regNumber REG_INT_FIRST = REG_R0;
    public const regNumber REG_INT_LAST = REG_LR;
    public const regNumber REG_FP_FIRST = REG_F0;
    public const regNumber REG_FP_LAST = REG_F31;

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

    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ALLFLOAT = SRBM_FLT_CALLEE_SAVED | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLDOUBLE =
        SRBM_F0 | SRBM_F2 | SRBM_F4 | SRBM_F6 | SRBM_F8 | SRBM_F10 | SRBM_F12 | SRBM_F14 |
        SRBM_F16 | SRBM_F18 | SRBM_F20 | SRBM_F22 | SRBM_F24 | SRBM_F26 | SRBM_F28 | SRBM_F30;

    public const int CNT_CALLEE_TRASH = 6;
    public const int CNT_CALLEE_TRASH_FLOAT = 16;

    public const int REGSIZE_BYTES = 4;

    public const int CNT_CALLEE_SAVED = 8;
    public const int CNT_CALLEE_SAVED_FLOAT = 16;
    public const int CALLEE_SAVED_REG_MAXSZ = CNT_CALLEE_SAVED * REGSIZE_BYTES;
    public const int CALLEE_SAVED_FLOAT_MAXSZ = CNT_CALLEE_SAVED_FLOAT * sizeof(float);
}
#endif
