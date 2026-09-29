// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public enum insCond : uint
{
    INS_COND_EQ,
    INS_COND_NE,
    INS_COND_HS,
    INS_COND_LO,

    INS_COND_MI,
    INS_COND_PL,
    INS_COND_VS,
    INS_COND_VC,

    INS_COND_HI,
    INS_COND_LS,
    INS_COND_GE,
    INS_COND_LT,

    INS_COND_GT,
    INS_COND_LE,
}
#endif
