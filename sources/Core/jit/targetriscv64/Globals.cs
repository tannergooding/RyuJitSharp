// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Globals
{
    public const int TARGET_POINTER_SIZE = 8;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 1;

    public const regNumber REG_ASYNC_CONTINUATION_RET = REG_A2;

    public const regNumber REG_INTRET = REG_A0;
}
#endif
