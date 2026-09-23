// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public partial class Globals
{
    public const int TARGET_POINTER_SIZE = 4;

    public const int MAX_PASS_SINGLEREG_BYTES = 16;

    public const int TARGET_MASKS_SHIFTS = 1;

    public const int TARGET_HAS_MULHI = 0;

    public const int FP_REGSIZE_BYTES = 16;

    public const int FPSAVE_REGSIZE_BYTES = 16;
}
#endif
