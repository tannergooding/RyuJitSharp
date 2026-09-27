// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public enum SveMaskPattern
{
    SveMaskPatternLargestPowerOf2 = 0,
    SveMaskPatternVectorCount1 = 1,
    SveMaskPatternVectorCount2 = 2,
    SveMaskPatternVectorCount3 = 3,
    SveMaskPatternVectorCount4 = 4,
    SveMaskPatternVectorCount5 = 5,
    SveMaskPatternVectorCount6 = 6,
    SveMaskPatternVectorCount7 = 7,
    SveMaskPatternVectorCount8 = 8,
    SveMaskPatternVectorCount16 = 9,
    SveMaskPatternVectorCount32 = 10,
    SveMaskPatternVectorCount64 = 11,
    SveMaskPatternVectorCount128 = 12,
    SveMaskPatternVectorCount256 = 13,
    SveMaskPatternLargestMultipleOf4 = 29,
    SveMaskPatternLargestMultipleOf3 = 30,
    SveMaskPatternAll = 31,
    SveMaskPatternNone = 14,
}
#endif
