// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET_X86 = CORINFO_ARCH_X86;

    public const int MAX_PASS_SINGLEREG_BYTES_X86 = 8;

    public const int MAX_PASS_MULTIREG_BYTES_X86 = 0;

    public const int MAX_RET_MULTIREG_BYTES_X86 = 8;

    public const int MAX_ARG_REG_COUNT_X86 = 1;

    public const int MAX_RET_REG_COUNT_X86 = 2;

    public const int MAX_MULTIREG_COUNT_X86 = 2;

    public const int TARGET_POINTER_SIZE_X86 = 4;

    public const int MIN_ARG_AREA_FOR_CALL_X86 = 0;

    public const int CODE_ALIGN_X86 = 1;

    public const regNumber FIRST_FP_ARGREG_X86 = REG_X86_XMM0;

    public const regNumber LAST_FP_ARGREG_X86 = REG_X86_XMM3;

    public const int TARGET_MASKS_SHIFTS_X86 = 1;

    public const int TARGET_HAS_MULHI_X86 = 1;

    public const int XMM_REGSIZE_BYTES_X86 = 16;

    public const int YMM_REGSIZE_BYTES_X86 = 32;

    public const int ZMM_REGSIZE_BYTES_X86 = 64;

    public const int REGNUM_BITS_X86 = 6;

    public const int REGSIZE_BYTES_X86 = 4;

    public const int FIRST_ARG_STACK_OFFS_X86 = 2 * REGSIZE_BYTES_X86;

    public static int STACK_ALIGN_X86 => Configuration.UNIX_X86_ABI ? 16 : 4;

    public static int STACK_ALIGN_SHIFT_X86 => Configuration.UNIX_X86_ABI ? 4 : 2;

    public const regNumber REG_FPBASE_X86 = REG_X86_EBP;

    public const regNumber REG_SPBASE_X86 = REG_X86_ESP;

    public const regNumber REG_SECRET_STUB_PARAM_X86 = REG_X86_EAX;

    public const regNumber REG_STACK_PROBE_HELPER_ARG_X86 = REG_X86_EAX;

    public const regNumber REG_ASYNC_CONTINUATION_RET_X86 = REG_X86_ECX;

    public const regNumber REG_ARG_0_X86 = REG_X86_ECX;

    public const regNumber REG_ARG_1_X86 = REG_X86_EDX;

    public const regNumber REG_INTRET_X86 = REG_X86_EAX;

    public const regNumber REG_LNGRET_LO_X86 = REG_X86_EAX;

    public const regNumber REG_LNGRET_HI_X86 = REG_X86_EDX;

    public const int ARG_STACK_PROBE_THRESHOLD_BYTES = 1024;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_X86 = ARG_STACK_PROBE_THRESHOLD_BYTES;
}
