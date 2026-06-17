// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class var_typesExtensions
{
    private static ReadOnlySpan<var_types> s_actualTypes => [
        TYP_UNDEF, // TYP_UNDEF
        TYP_VOID, // TYP_VOID
        TYP_INT, // TYP_BYTE
        TYP_INT, // TYP_UBYTE
        TYP_INT, // TYP_SHORT
        TYP_INT, // TYP_USHORT
        TYP_INT, // TYP_INT
        TYP_INT, // TYP_UINT
        TYP_LONG, // TYP_LONG
        TYP_LONG, // TYP_ULONG
        TYP_FLOAT, // TYP_FLOAT
        TYP_DOUBLE, // TYP_DOUBLE
        TYP_REF, // TYP_REF
        TYP_BYREF, // TYP_BYREF
        TYP_STRUCT, // TYP_STRUCT
#if FEATURE_SIMD
        TYP_SIMD8, // TYP_SIMD8
        TYP_SIMD12, // TYP_SIMD12
        TYP_SIMD16, // TYP_SIMD16
#if TARGET_XARCH
        TYP_SIMD32, // TYP_SIMD32
        TYP_SIMD64, // TYP_SIMD64
#elif TARGET_ARM64
        TYP_SIMD, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        TYP_MASK, // TYP_MASK
#endif
#endif
        TYP_UNKNOWN, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_alignments => [
        0, // TYP_UNDEF
        0, // TYP_VOID
        1, // TYP_BYTE
        1, // TYP_UBYTE
        2, // TYP_SHORT
        2, // TYP_USHORT
        4, // TYP_INT
        4, // TYP_UINT
        8, // TYP_LONG
        8, // TYP_ULONG
        4, // TYP_FLOAT
        8, // TYP_DOUBLE
        PS, // TYP_REF
        PS, // TYP_BYREF
        4, // TYP_STRUCT
#if FEATURE_SIMD
        8, // TYP_SIMD8
        16, // TYP_SIMD12
        16, // TYP_SIMD16
#if TARGET_XARCH
        16, // TYP_SIMD32
        16, // TYP_SIMD64
#elif TARGET_ARM64
        16, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        8, // TYP_MASK
#endif
#endif
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<var_types_classification> s_classifications => [
        VTF_ANY, // TYP_UNDEF
        VTF_ANY, // TYP_VOID
        VTF_INT, // TYP_BYTE
        VTF_INT|VTF_UNS, // TYP_UBYTE
        VTF_INT, // TYP_SHORT
        VTF_INT|VTF_UNS, // TYP_USHORT
        VTF_INT|VTF_I32, // TYP_INT
        VTF_INT|VTF_UNS|VTF_I32, // TYP_UINT
        VTF_INT|VTF_I64, // TYP_LONG
        VTF_INT|VTF_UNS|VTF_I64, // TYP_ULONG
        VTF_FLT, // TYP_FLOAT
        VTF_FLT, // TYP_DOUBLE
        VTF_ANY|VTF_GCR|VTF_I, // TYP_REF
        VTF_ANY|VTF_BYR|VTF_I, // TYP_BYREF
        VTF_S, // TYP_STRUCT
#if FEATURE_SIMD
        VTF_S|VTF_VEC, // TYP_SIMD8
        VTF_S|VTF_VEC, // TYP_SIMD12
        VTF_S|VTF_VEC, // TYP_SIMD16
#if TARGET_XARCH
        VTF_S|VTF_VEC, // TYP_SIMD32
        VTF_S|VTF_VEC, // TYP_SIMD64
#elif TARGET_ARM64
        VTF_S|VTF_VEC, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        VTF_S, // TYP_MASK
#endif
#endif
        VTF_ANY, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<emitAttr> s_emitActualSizes => [
        (emitAttr)(0), // TYP_UNDEF
        (emitAttr)(0), // TYP_VOID
        (emitAttr)(4), // TYP_BYTE
        (emitAttr)(4), // TYP_UBYTE
        (emitAttr)(4), // TYP_SHORT
        (emitAttr)(4), // TYP_USHORT
        (emitAttr)(4), // TYP_INT
        (emitAttr)(4), // TYP_UINT
        EPS, // TYP_LONG
        EPS, // TYP_ULONG
        (emitAttr)(4), // TYP_FLOAT
        (emitAttr)(8), // TYP_DOUBLE
        GCS, // TYP_REF
        BRS, // TYP_BYREF
        (emitAttr)(0), // TYP_STRUCT
#if FEATURE_SIMD
        (emitAttr)(8), // TYP_SIMD8
        (emitAttr)(16), // TYP_SIMD12
        (emitAttr)(16), // TYP_SIMD16
#if TARGET_XARCH
        (emitAttr)(32), // TYP_SIMD32
        (emitAttr)(64), // TYP_SIMD64
#elif TARGET_ARM64
        EAU, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        (emitAttr)(8), // TYP_MASK
#endif
#endif
        (emitAttr)(0), // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<emitAttr> s_emitSizes => [
        (emitAttr)(0), // TYP_UNDEF
        (emitAttr)(0), // TYP_VOID
        (emitAttr)(1), // TYP_BYTE
        (emitAttr)(1), // TYP_UBYTE
        (emitAttr)(2), // TYP_SHORT
        (emitAttr)(2), // TYP_USHORT
        (emitAttr)(4), // TYP_INT
        (emitAttr)(4), // TYP_UINT
        EPS, // TYP_LONG
        EPS, // TYP_ULONG
        (emitAttr)(4), // TYP_FLOAT
        (emitAttr)(8), // TYP_DOUBLE
        GCS, // TYP_REF
        BRS, // TYP_BYREF
        (emitAttr)(0), // TYP_STRUCT
#if FEATURE_SIMD
        (emitAttr)(8), // TYP_SIMD8
        (emitAttr)(16), // TYP_SIMD12
        (emitAttr)(16), // TYP_SIMD16
#if TARGET_XARCH
        (emitAttr)(32), // TYP_SIMD32
        (emitAttr)(64), // TYP_SIMD64
#elif TARGET_ARM64
        EAU, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        (emitAttr)(8), // TYP_MASK
#endif
#endif
        (emitAttr)(0), // TYP_UNKNOWN
    ];

#if DEBUG
    private static readonly string[] s_names = [
        "<UNDEF>", // TYP_UNDEF
        "void", // TYP_VOID
        "byte", // TYP_BYTE
        "ubyte", // TYP_UBYTE
        "short", // TYP_SHORT
        "ushort", // TYP_USHORT
        "int", // TYP_INT
        "uint", // TYP_UINT
        "long", // TYP_LONG
        "ulong", // TYP_ULONG
        "float", // TYP_FLOAT
        "double", // TYP_DOUBLE
        "ref", // TYP_REF
        "byref", // TYP_BYREF
        "struct", // TYP_STRUCT
#if FEATURE_SIMD
        "simd8", // TYP_SIMD8
        "simd12", // TYP_SIMD12
        "simd16", // TYP_SIMD16
#if TARGET_XARCH
        "simd32", // TYP_SIMD32
        "simd64", // TYP_SIMD64
#elif TARGET_ARM64
        "simd", // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        "mask", // TYP_MASK
#endif
#endif
        "unknown", // TYP_UNKNOWN
    ];
#endif

    private static ReadOnlySpan<var_types_register> s_registers => [
        VTR_INT, // TYP_UNDEF
        VTR_INT, // TYP_VOID
        VTR_INT, // TYP_BYTE
        VTR_INT, // TYP_UBYTE
        VTR_INT, // TYP_SHORT
        VTR_INT, // TYP_USHORT
        VTR_INT, // TYP_INT
        VTR_INT, // TYP_UINT
        VTR_INT, // TYP_LONG
        VTR_INT, // TYP_ULONG
        VTR_FLOAT, // TYP_FLOAT
        VTR_FLOAT, // TYP_DOUBLE
        VTR_INT, // TYP_REF
        VTR_INT, // TYP_BYREF
        VTR_INT, // TYP_STRUCT
#if FEATURE_SIMD
        VTR_FLOAT, // TYP_SIMD8
        VTR_FLOAT, // TYP_SIMD12
        VTR_FLOAT, // TYP_SIMD16
#if TARGET_XARCH
        VTR_FLOAT, // TYP_SIMD32
        VTR_FLOAT, // TYP_SIMD64
#elif TARGET_ARM64
        VTR_FLOAT, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        VTR_MASK, // TYP_MASK
#endif
#endif
        VTR_INT, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_sizes => [
        0, // TYP_UNDEF
        0, // TYP_VOID
        1, // TYP_BYTE
        1, // TYP_UBYTE
        2, // TYP_SHORT
        2, // TYP_USHORT
        4, // TYP_INT
        4, // TYP_UINT
        8, // TYP_LONG
        8, // TYP_ULONG
        4, // TYP_FLOAT
        8, // TYP_DOUBLE
        PS, // TYP_REF
        PS, // TYP_BYREF
        0, // TYP_STRUCT
#if FEATURE_SIMD
        8, // TYP_SIMD8
        12, // TYP_SIMD12
        16, // TYP_SIMD16
#if TARGET_XARCH
        32, // TYP_SIMD32
        64, // TYP_SIMD64
#elif TARGET_ARM64
        SZU, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        8, // TYP_MASK
#endif
#endif
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_stSzs => [
        0, // TYP_UNDEF
        0, // TYP_VOID
        1, // TYP_BYTE
        1, // TYP_UBYTE
        1, // TYP_SHORT
        1, // TYP_USHORT
        1, // TYP_INT
        1, // TYP_UINT
        2, // TYP_LONG
        2, // TYP_ULONG
        1, // TYP_FLOAT
        2, // TYP_DOUBLE
        PST, // TYP_REF
        PST, // TYP_BYREF
        1, // TYP_STRUCT
#if FEATURE_SIMD
        2, // TYP_SIMD8
        4, // TYP_SIMD12
        4, // TYP_SIMD16
#if TARGET_XARCH
        8, // TYP_SIMD32
        16, // TYP_SIMD64
#elif TARGET_ARM64
        0, // TYP_SIMD
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        2, // TYP_MASK
#endif
#endif
        0, // TYP_UNKNOWN
    ];
}