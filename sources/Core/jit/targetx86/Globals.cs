// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Globals
{
    public const int TARGET_POINTER_SIZE = 4;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 1;

    public const int XMM_REGSIZE_BYTES = 16;

    public const int REGSIZE_BYTES = 4;

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

    public const int ARG_STACK_PROBE_THRESHOLD_BYTES = 1024;

    public const int STACK_PROBE_BOUNDARY_THRESHOLD_BYTES = ARG_STACK_PROBE_THRESHOLD_BYTES;
}
#endif
