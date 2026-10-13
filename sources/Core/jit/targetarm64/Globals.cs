// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public const CorInfoArch CORINFO_ARCH_TARGET_ARM64 = CORINFO_ARCH_ARM64;

    public const int MAX_PASS_SINGLEREG_BYTES_ARM64 = 16;

    public const int MAX_PASS_MULTIREG_BYTES_ARM64 = 64;

    public const int MAX_RET_MULTIREG_BYTES_ARM64 = 64;

    public const int MAX_ARG_REG_COUNT_ARM64 = 4;

    public const int MAX_RET_REG_COUNT_ARM64 = 4;

    public const int MAX_MULTIREG_COUNT_ARM64 = 4;

    public const int TARGET_POINTER_SIZE_ARM64 = 8;

    public const int TARGET_MASKS_SHIFTS_ARM64 = 1;

    public const int TARGET_HAS_MULHI_ARM64 = 1;

    public const int FP_REGSIZE_BYTES_ARM64 = 16;

    public const regNumber REG_SCRATCH_V = REG_ARM64_V9;

    public const regNumber REG_SCRATCH_P = REG_ARM64_P4;

    public const int MIN_SVE_REGSIZE_BYTES = 16;

    public const int MAX_SVE_REGSIZE_BYTES = 256;

    public const regNumber REG_SECRET_STUB_PARAM_ARM64 = REG_ARM64_R12;

    public const regNumber REG_ASYNC_CONTINUATION_RET_ARM64 = REG_ARM64_R2;

    public const regNumber REG_ARG_RET_BUFF = REG_ARM64_R8;

    public const int MAX_REG_ARG_ARM64 = 8;

    public const int MAX_FLOAT_REG_ARG_ARM64 = 8;

}
