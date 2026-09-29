// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

// Prefetch operation specifier for SVE instructions such as prfb.
public enum insSvePrfop : uint
{
    SVE_PRFOP_PLDL1KEEP = 0b0000,
    SVE_PRFOP_PLDL1STRM = 0b0001,
    SVE_PRFOP_PLDL2KEEP = 0b0010,
    SVE_PRFOP_PLDL2STRM = 0b0011,
    SVE_PRFOP_PLDL3KEEP = 0b0100,
    SVE_PRFOP_PLDL3STRM = 0b0101,
    SVE_PRFOP_PSTL1KEEP = 0b1000,
    SVE_PRFOP_PSTL1STRM = 0b1001,
    SVE_PRFOP_PSTL2KEEP = 0b1010,
    SVE_PRFOP_PSTL2STRM = 0b1011,
    SVE_PRFOP_PSTL3KEEP = 0b1100,
    SVE_PRFOP_PSTL3STRM = 0b1101,

    SVE_PRFOP_CONST6 = 0b0110,
    SVE_PRFOP_CONST7 = 0b0111,
    SVE_PRFOP_CONST14 = 0b1110,
    SVE_PRFOP_CONST15 = 0b1111,
}
#endif
