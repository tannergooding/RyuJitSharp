// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum insCFlags : uint
{
    INS_FLAGS_NONE = 0x0,
    INS_FLAGS_V = 0x1,
    INS_FLAGS_C = 0x2,
    INS_FLAGS_CV = 0x3,
    INS_FLAGS_Z = 0x4,
    INS_FLAGS_ZV = 0x5,
    INS_FLAGS_ZC = 0x6,
    INS_FLAGS_ZCV = 0x7,
    INS_FLAGS_N = 0x8,
    INS_FLAGS_NV = 0x9,
    INS_FLAGS_NC = 0xA,
    INS_FLAGS_NCV = 0xB,
    INS_FLAGS_NZ = 0xC,
    INS_FLAGS_NZV = 0xD,
    INS_FLAGS_NZC = 0xE,
    INS_FLAGS_NZCV = 0xF,
    INS_FLAGS_CF = 0x1,
    INS_FLAGS_ZF = 0x2,
    INS_FLAGS_SF = 0x4,
    INS_FLAGS_OF = 0x8,
}
