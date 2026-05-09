// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_MASKED_HW_INTRINSICS
global using static RyuJitSharp.regMaskMsk;
using System;

namespace RyuJitSharp;

[Flags]
public enum regMaskMsk
{
    RBM_NONE_MSK = 0,

#if TARGET_XARCH
    RBM_K0 = 1 << 0,
    RBM_K1 = 1 << 1,
    RBM_K2 = 1 << 2,
    RBM_K3 = 1 << 3,
    RBM_K4 = 1 << 4,
    RBM_K5 = 1 << 5,
    RBM_K6 = 1 << 6,
    RBM_K7 = 1 << 7,
#elif TARGET_ARM64
    RBM_P0 = 1 << 0,
    RBM_P1 = 1 << 1,
    RBM_P2 = 1 << 2,
    RBM_P3 = 1 << 3,
    RBM_P4 = 1 << 4,
    RBM_P5 = 1 << 5,
    RBM_P6 = 1 << 6,
    RBM_P7 = 1 << 7,
    RBM_P8 = 1 << 8,
    RBM_P9 = 1 << 9,
    RBM_P10 = 1 << 10,
    RBM_P11 = 1 << 11,
    RBM_P12 = 1 << 12,
    RBM_P13 = 1 << 13,
    RBM_P14 = 1 << 14,
    RBM_P15 = 1 << 15,
#else
#error Unsupported or unset target architecture
#endif
}
#endif
