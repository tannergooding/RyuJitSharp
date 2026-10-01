// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_FIRST = REG_R0;
    public const regNumber REG_INT_FIRST = REG_R0;
    public const regNumber REG_INT_LAST = REG_T6;
    public const regNumber REG_FP_FIRST = REG_FT0;
    public const regNumber REG_FP_LAST = REG_FT11;

    public const regMask SRBM_FPBASE = SRBM_FP;
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

    public const regMask SRBM_ALLINT = SRBM_INT_CALLEE_SAVED | SRBM_INT_CALLEE_TRASH;
    public const regMask SRBM_ALLFLOAT = SRBM_FLT_CALLEE_SAVED | SRBM_FLT_CALLEE_TRASH;
    public const regMask SRBM_ALLDOUBLE = SRBM_ALLFLOAT;

    public const int CNT_CALLEE_TRASH = 15;
    public const int CNT_CALLEE_TRASH_FLOAT = 20;
}
#endif
