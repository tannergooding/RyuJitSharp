// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum insCFlags : uint
{
#if TARGET_ARM64
    INS_FLAGS_NONE,
    INS_FLAGS_V,
    INS_FLAGS_C,
    INS_FLAGS_CV,
    INS_FLAGS_Z,
    INS_FLAGS_ZV,
    INS_FLAGS_ZC,
    INS_FLAGS_ZCV,
    INS_FLAGS_N,
    INS_FLAGS_NV,
    INS_FLAGS_NC,
    INS_FLAGS_NCV,
    INS_FLAGS_NZ,
    INS_FLAGS_NZV,
    INS_FLAGS_NZC,
    INS_FLAGS_NZCV,
#elif TARGET_XARCH
    INS_FLAGS_NONE = 0x0,
    INS_FLAGS_CF = 0x1,
    INS_FLAGS_ZF = 0x2,
    INS_FLAGS_SF = 0x4,
    INS_FLAGS_OF = 0x8,
#endif
}
