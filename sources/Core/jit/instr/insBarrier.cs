// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
namespace RyuJitSharp;

public enum insBarrier : uint
{
    INS_BARRIER_OSHLD = 1,
    INS_BARRIER_OSHST = 2,
    INS_BARRIER_OSH = 3,

    INS_BARRIER_NSHLD = 5,
    INS_BARRIER_NSHST = 6,
    INS_BARRIER_NSH = 7,

    INS_BARRIER_ISHLD = 9,
    INS_BARRIER_ISHST = 10,
    INS_BARRIER_ISH = 11,

    INS_BARRIER_LD = 13,
    INS_BARRIER_ST = 14,
    INS_BARRIER_SY = 15,
}
#endif
