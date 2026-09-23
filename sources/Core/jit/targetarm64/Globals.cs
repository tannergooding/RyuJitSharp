// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Globals
{
    public const int TARGET_POINTER_SIZE = 8;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 1;

    public const regNumber REG_SCRATCH_V = REG_V9;

    public const regNumber REG_SCRATCH_P = REG_P4;

    public const int MAX_SVE_REGSIZE_BYTES = 256;
}
#endif
