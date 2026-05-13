// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public enum var_types : byte
{
    TYP_UNDEF,

    TYP_VOID,

    TYP_BYTE,

    TYP_UBYTE,

    TYP_SHORT,

    TYP_USHORT,

    TYP_INT,

    TYP_UINT,

    TYP_LONG,

    TYP_ULONG,

    TYP_FLOAT,

    TYP_DOUBLE,

    TYP_REF,

    TYP_BYREF,

    TYP_STRUCT,

#if FEATURE_SIMD
    TYP_SIMD8,

    TYP_SIMD12,

    TYP_SIMD16,

#if TARGET_XARCH
    TYP_SIMD32,

    TYP_SIMD64,
#elif TARGET_ARM64
    TYP_Simd,
#endif

#if FEATURE_MASKED_HW_INTRINSICS
    TYP_MASK,
#endif
#endif

    TYP_UNKNOWN,

    TYP_COUNT,
}
