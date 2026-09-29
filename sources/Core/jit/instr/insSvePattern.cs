// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

// Maps directly to the pattern used in SVE instructions such as cntb.
public enum insSvePattern : uint
{
    SVE_PATTERN_POW2 = 0,   // The largest power of 2.
    SVE_PATTERN_VL1 = 1,
    SVE_PATTERN_VL2 = 2,
    SVE_PATTERN_VL3 = 3,
    SVE_PATTERN_VL4 = 4,
    SVE_PATTERN_VL5 = 5,
    SVE_PATTERN_VL6 = 6,
    SVE_PATTERN_VL7 = 7,
    SVE_PATTERN_VL8 = 8,
    SVE_PATTERN_VL16 = 9,
    SVE_PATTERN_VL32 = 10,
    SVE_PATTERN_VL64 = 11,
    SVE_PATTERN_VL128 = 12,
    SVE_PATTERN_VL256 = 13,
    SVE_PATTERN_MUL4 = 29,  // The largest multiple of 4.
    SVE_PATTERN_MUL3 = 30,  // The largest multiple of 3.
    SVE_PATTERN_ALL = 31,   // All available (implicitly a multiple of two).
}
#endif
