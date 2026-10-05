// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET = CORINFO_ARCH_X86;

    public const int MAX_PASS_SINGLEREG_BYTES = 8;

    public const int MAX_PASS_MULTIREG_BYTES = 0;

    public const int MAX_RET_MULTIREG_BYTES = 8;

    public const int MAX_ARG_REG_COUNT = 1;

    public const int MAX_RET_REG_COUNT = 2;

    public const int MAX_MULTIREG_COUNT = 2;

    public const int TARGET_POINTER_SIZE = 4;

    public const int MIN_ARG_AREA_FOR_CALL = 0;

    public const int CODE_ALIGN = 1;

    public const regNumber FIRST_FP_ARGREG = REG_XMM0;

    public const regNumber LAST_FP_ARGREG = REG_XMM3;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 1;

    public const int XMM_REGSIZE_BYTES = 16;

    public const int YMM_REGSIZE_BYTES = 32;

    public const int ZMM_REGSIZE_BYTES = 64;

    public const int REGNUM_BITS = 6;

    public const int REGSIZE_BYTES = 4;

    public const int FIRST_ARG_STACK_OFFS = 2 * REGSIZE_BYTES;

#if UNIX_X86_ABI
    public const int STACK_ALIGN = 16;

    public const int STACK_ALIGN_SHIFT = 4;
#else
    public const int STACK_ALIGN = 4;

    public const int STACK_ALIGN_SHIFT = 2;
#endif

    public const regNumber REG_FPBASE = REG_EBP;

    public const regNumber REG_SPBASE = REG_ESP;

    public const regNumber REG_SECRET_STUB_PARAM = REG_EAX;

    public const regNumber REG_STACK_PROBE_HELPER_ARG = REG_EAX;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_ECX;

    public const regNumber REG_ARG_0 = REG_ECX;

    public const regNumber REG_ARG_1 = REG_EDX;

    public const regNumber REG_INTRET = REG_EAX;

    public const regNumber REG_LNGRET_LO = REG_EAX;

    public const regNumber REG_LNGRET_HI = REG_EDX;

    public const regNumber REG_FLOATRET = REG_NA;

    public const int ARG_STACK_PROBE_THRESHOLD_BYTES = 1024;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES = ARG_STACK_PROBE_THRESHOLD_BYTES;
}
#endif
