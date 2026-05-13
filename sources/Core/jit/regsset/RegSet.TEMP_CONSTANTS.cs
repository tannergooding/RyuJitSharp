// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
#if !FEATURE_SIMD
    public const int TEMP_MAX_SIZE = sizeof(double);
#elif TARGET_XARCH
    public const int TEMP_MAX_SIZE = ZMM_REGSIZE_BYTES;
#elif TARGET_ARM64
    public const int TEMP_MAX_SIZE = FP_REGSIZE_BYTES;
#endif

#if TARGET_ARM64 && FEATURE_SIMD
    // There are two extra slots for temps with unknown size (TYP_SIMD/TYP_MASK)
    public const int TEMP_SLOT_COUNT = (TEMP_MAX_SIZE / sizeof(int)) + 2;
#else
    public const int TEMP_SLOT_COUNT = (TEMP_MAX_SIZE / sizeof(int));
#endif
}
