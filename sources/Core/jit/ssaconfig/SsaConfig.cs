// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class SsaConfig
{
    // FIRST ssa num is given to the first definition of a variable which can either be:
    // 1. A regular definition in the program.
    // 2. Or initialization by compInitMem.
    public const int FIRST_SSA_NUM = 1;

    // Sentinel value to indicate variable not touched by SSA.
    public const int RESERVED_SSA_NUM = 0;
}
