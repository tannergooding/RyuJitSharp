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
        TYP_SIMD8, // TYP_SIMD8
        TYP_SIMD12, // TYP_SIMD12
        TYP_SIMD16, // TYP_SIMD16
        TYP_SIMD32, // TYP_SIMD32
        TYP_SIMD64, // TYP_SIMD64
        TYP_SIMD_ARM64, // TYP_SIMD_ARM64
        TYP_MASK, // TYP_MASK
        TYP_UNKNOWN, // TYP_UNKNOWN
    ];

    // Keep each target's table literal constant so the spans can use RVA-backed data.
    private static ReadOnlySpan<byte> s_alignments =>
        Configuration.TARGET_64BIT ? s_alignments64 : s_alignments32;

    private static ReadOnlySpan<byte> s_alignments32 => [
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
        4, // TYP_REF
        4, // TYP_BYREF
        4, // TYP_STRUCT
        8, // TYP_SIMD8
        16, // TYP_SIMD12
        16, // TYP_SIMD16
        16, // TYP_SIMD32
        16, // TYP_SIMD64
        16, // TYP_SIMD_ARM64
        8, // TYP_MASK
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_alignments64 => [
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
        8, // TYP_REF
        8, // TYP_BYREF
        4, // TYP_STRUCT
        8, // TYP_SIMD8
        16, // TYP_SIMD12
        16, // TYP_SIMD16
        16, // TYP_SIMD32
        16, // TYP_SIMD64
        16, // TYP_SIMD_ARM64
        8, // TYP_MASK
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<var_types_classification> s_classifications =>
        Configuration.TARGET_64BIT ? s_classifications64 : s_classifications32;

    private static ReadOnlySpan<var_types_classification> s_classifications32 => [
        VTF_ANY, // TYP_UNDEF
        VTF_ANY, // TYP_VOID
        VTF_INT, // TYP_BYTE
        VTF_INT|VTF_UNS, // TYP_UBYTE
        VTF_INT, // TYP_SHORT
        VTF_INT|VTF_UNS, // TYP_USHORT
        VTF_INT|VTF_I, // TYP_INT
        VTF_INT|VTF_UNS|VTF_I, // TYP_UINT
        VTF_INT|VTF_ANY, // TYP_LONG
        VTF_INT|VTF_UNS|VTF_ANY, // TYP_ULONG
        VTF_FLT, // TYP_FLOAT
        VTF_FLT, // TYP_DOUBLE
        VTF_ANY|VTF_GCR|VTF_I, // TYP_REF
        VTF_ANY|VTF_BYR|VTF_I, // TYP_BYREF
        VTF_S, // TYP_STRUCT
        VTF_S|VTF_VEC, // TYP_SIMD8
        VTF_S|VTF_VEC, // TYP_SIMD12
        VTF_S|VTF_VEC, // TYP_SIMD16
        VTF_S|VTF_VEC, // TYP_SIMD32
        VTF_S|VTF_VEC, // TYP_SIMD64
        VTF_S|VTF_VEC, // TYP_SIMD_ARM64
        VTF_S, // TYP_MASK
        VTF_ANY, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<var_types_classification> s_classifications64 => [
        VTF_ANY, // TYP_UNDEF
        VTF_ANY, // TYP_VOID
        VTF_INT, // TYP_BYTE
        VTF_INT|VTF_UNS, // TYP_UBYTE
        VTF_INT, // TYP_SHORT
        VTF_INT|VTF_UNS, // TYP_USHORT
        VTF_INT|VTF_ANY, // TYP_INT
        VTF_INT|VTF_UNS|VTF_ANY, // TYP_UINT
        VTF_INT|VTF_I, // TYP_LONG
        VTF_INT|VTF_UNS|VTF_I, // TYP_ULONG
        VTF_FLT, // TYP_FLOAT
        VTF_FLT, // TYP_DOUBLE
        VTF_ANY|VTF_GCR|VTF_I, // TYP_REF
        VTF_ANY|VTF_BYR|VTF_I, // TYP_BYREF
        VTF_S, // TYP_STRUCT
        VTF_S|VTF_VEC, // TYP_SIMD8
        VTF_S|VTF_VEC, // TYP_SIMD12
        VTF_S|VTF_VEC, // TYP_SIMD16
        VTF_S|VTF_VEC, // TYP_SIMD32
        VTF_S|VTF_VEC, // TYP_SIMD64
        VTF_S|VTF_VEC, // TYP_SIMD_ARM64
        VTF_S, // TYP_MASK
        VTF_ANY, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<emitAttr> s_emitActualSizes =>
        Configuration.TARGET_64BIT ? s_emitActualSizes64 : s_emitActualSizes32;

    private static ReadOnlySpan<emitAttr> s_emitActualSizes32 => [
        (emitAttr)(0), // TYP_UNDEF
        (emitAttr)(0), // TYP_VOID
        (emitAttr)(4), // TYP_BYTE
        (emitAttr)(4), // TYP_UBYTE
        (emitAttr)(4), // TYP_SHORT
        (emitAttr)(4), // TYP_USHORT
        (emitAttr)(4), // TYP_INT
        (emitAttr)(4), // TYP_UINT
        EA_4BYTE, // TYP_LONG
        EA_4BYTE, // TYP_ULONG
        (emitAttr)(4), // TYP_FLOAT
        (emitAttr)(8), // TYP_DOUBLE
        EA_GCREF_FLG | EA_4BYTE, // TYP_REF
        EA_BYREF_FLG | EA_4BYTE, // TYP_BYREF
        (emitAttr)(0), // TYP_STRUCT
        (emitAttr)(8), // TYP_SIMD8
        (emitAttr)(16), // TYP_SIMD12
        (emitAttr)(16), // TYP_SIMD16
        (emitAttr)(32), // TYP_SIMD32
        (emitAttr)(64), // TYP_SIMD64
        EAU, // TYP_SIMD_ARM64
        (emitAttr)(8), // TYP_MASK
        (emitAttr)(0), // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<emitAttr> s_emitActualSizes64 => [
        (emitAttr)(0), // TYP_UNDEF
        (emitAttr)(0), // TYP_VOID
        (emitAttr)(4), // TYP_BYTE
        (emitAttr)(4), // TYP_UBYTE
        (emitAttr)(4), // TYP_SHORT
        (emitAttr)(4), // TYP_USHORT
        (emitAttr)(4), // TYP_INT
        (emitAttr)(4), // TYP_UINT
        EA_8BYTE, // TYP_LONG
        EA_8BYTE, // TYP_ULONG
        (emitAttr)(4), // TYP_FLOAT
        (emitAttr)(8), // TYP_DOUBLE
        EA_GCREF_FLG | EA_8BYTE, // TYP_REF
        EA_BYREF_FLG | EA_8BYTE, // TYP_BYREF
        (emitAttr)(0), // TYP_STRUCT
        (emitAttr)(8), // TYP_SIMD8
        (emitAttr)(16), // TYP_SIMD12
        (emitAttr)(16), // TYP_SIMD16
        (emitAttr)(32), // TYP_SIMD32
        (emitAttr)(64), // TYP_SIMD64
        EAU, // TYP_SIMD_ARM64
        (emitAttr)(8), // TYP_MASK
        (emitAttr)(0), // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<emitAttr> s_emitSizes =>
        Configuration.TARGET_64BIT ? s_emitSizes64 : s_emitSizes32;

    private static ReadOnlySpan<emitAttr> s_emitSizes32 => [
        (emitAttr)(0), // TYP_UNDEF
        (emitAttr)(0), // TYP_VOID
        (emitAttr)(1), // TYP_BYTE
        (emitAttr)(1), // TYP_UBYTE
        (emitAttr)(2), // TYP_SHORT
        (emitAttr)(2), // TYP_USHORT
        (emitAttr)(4), // TYP_INT
        (emitAttr)(4), // TYP_UINT
        EA_4BYTE, // TYP_LONG
        EA_4BYTE, // TYP_ULONG
        (emitAttr)(4), // TYP_FLOAT
        (emitAttr)(8), // TYP_DOUBLE
        EA_GCREF_FLG | EA_4BYTE, // TYP_REF
        EA_BYREF_FLG | EA_4BYTE, // TYP_BYREF
        (emitAttr)(0), // TYP_STRUCT
        (emitAttr)(8), // TYP_SIMD8
        (emitAttr)(16), // TYP_SIMD12
        (emitAttr)(16), // TYP_SIMD16
        (emitAttr)(32), // TYP_SIMD32
        (emitAttr)(64), // TYP_SIMD64
        EAU, // TYP_SIMD_ARM64
        (emitAttr)(8), // TYP_MASK
        (emitAttr)(0), // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<emitAttr> s_emitSizes64 => [
        (emitAttr)(0), // TYP_UNDEF
        (emitAttr)(0), // TYP_VOID
        (emitAttr)(1), // TYP_BYTE
        (emitAttr)(1), // TYP_UBYTE
        (emitAttr)(2), // TYP_SHORT
        (emitAttr)(2), // TYP_USHORT
        (emitAttr)(4), // TYP_INT
        (emitAttr)(4), // TYP_UINT
        EA_8BYTE, // TYP_LONG
        EA_8BYTE, // TYP_ULONG
        (emitAttr)(4), // TYP_FLOAT
        (emitAttr)(8), // TYP_DOUBLE
        EA_GCREF_FLG | EA_8BYTE, // TYP_REF
        EA_BYREF_FLG | EA_8BYTE, // TYP_BYREF
        (emitAttr)(0), // TYP_STRUCT
        (emitAttr)(8), // TYP_SIMD8
        (emitAttr)(16), // TYP_SIMD12
        (emitAttr)(16), // TYP_SIMD16
        (emitAttr)(32), // TYP_SIMD32
        (emitAttr)(64), // TYP_SIMD64
        EAU, // TYP_SIMD_ARM64
        (emitAttr)(8), // TYP_MASK
        (emitAttr)(0), // TYP_UNKNOWN
    ];

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
        VTR_FLOAT, // TYP_SIMD8
        VTR_FLOAT, // TYP_SIMD12
        VTR_FLOAT, // TYP_SIMD16
        VTR_FLOAT, // TYP_SIMD32
        VTR_FLOAT, // TYP_SIMD64
        VTR_FLOAT, // TYP_SIMD_ARM64
        VTR_MASK, // TYP_MASK
        VTR_INT, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_sizes =>
        Configuration.TARGET_64BIT ? s_sizes64 : s_sizes32;

    private static ReadOnlySpan<byte> s_sizes32 => [
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
        4, // TYP_REF
        4, // TYP_BYREF
        0, // TYP_STRUCT
        8, // TYP_SIMD8
        12, // TYP_SIMD12
        16, // TYP_SIMD16
        32, // TYP_SIMD32
        64, // TYP_SIMD64
        SZU, // TYP_SIMD_ARM64
        8, // TYP_MASK
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_sizes64 => [
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
        8, // TYP_REF
        8, // TYP_BYREF
        0, // TYP_STRUCT
        8, // TYP_SIMD8
        12, // TYP_SIMD12
        16, // TYP_SIMD16
        32, // TYP_SIMD32
        64, // TYP_SIMD64
        SZU, // TYP_SIMD_ARM64
        8, // TYP_MASK
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_stSzs =>
        Configuration.TARGET_64BIT ? s_stSzs64 : s_stSzs32;

    private static ReadOnlySpan<byte> s_stSzs32 => [
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
        1, // TYP_REF
        1, // TYP_BYREF
        1, // TYP_STRUCT
        2, // TYP_SIMD8
        4, // TYP_SIMD12
        4, // TYP_SIMD16
        8, // TYP_SIMD32
        16, // TYP_SIMD64
        0, // TYP_SIMD_ARM64
        2, // TYP_MASK
        0, // TYP_UNKNOWN
    ];

    private static ReadOnlySpan<byte> s_stSzs64 => [
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
        2, // TYP_REF
        2, // TYP_BYREF
        1, // TYP_STRUCT
        2, // TYP_SIMD8
        4, // TYP_SIMD12
        4, // TYP_SIMD16
        8, // TYP_SIMD32
        16, // TYP_SIMD64
        0, // TYP_SIMD_ARM64
        2, // TYP_MASK
        0, // TYP_UNKNOWN
    ];

    extension(var_types varType)
    {
        public string Name
        {
            get
            {
                if (Configuration.DEBUG)
                {
                    switch (varType)
                    {
                        case var_types.TYP_UNDEF:
                        {
                            return "<UNDEF>"; // TYP_UNDEF
                        }
                        case var_types.TYP_VOID:
                        {
                            return "void"; // TYP_VOID
                        }
                        case var_types.TYP_BYTE:
                        {
                            return "byte"; // TYP_BYTE
                        }
                        case var_types.TYP_UBYTE:
                        {
                            return "ubyte"; // TYP_UBYTE
                        }
                        case var_types.TYP_SHORT:
                        {
                            return "short"; // TYP_SHORT
                        }
                        case var_types.TYP_USHORT:
                        {
                            return "ushort"; // TYP_USHORT
                        }
                        case var_types.TYP_INT:
                        {
                            return "int"; // TYP_INT
                        }
                        case var_types.TYP_UINT:
                        {
                            return "uint"; // TYP_UINT
                        }
                        case var_types.TYP_LONG:
                        {
                            return "long"; // TYP_LONG
                        }
                        case var_types.TYP_ULONG:
                        {
                            return "ulong"; // TYP_ULONG
                        }
                        case var_types.TYP_FLOAT:
                        {
                            return "float"; // TYP_FLOAT
                        }
                        case var_types.TYP_DOUBLE:
                        {
                            return "double"; // TYP_DOUBLE
                        }
                        case var_types.TYP_REF:
                        {
                            return "ref"; // TYP_REF
                        }
                        case var_types.TYP_BYREF:
                        {
                            return "byref"; // TYP_BYREF
                        }
                        case var_types.TYP_STRUCT:
                        {
                            return "struct"; // TYP_STRUCT
                        }
                        case var_types.TYP_SIMD8:
                        {
                            return "simd8"; // TYP_SIMD8
                        }
                        case var_types.TYP_SIMD12:
                        {
                            return "simd12"; // TYP_SIMD12
                        }
                        case var_types.TYP_SIMD16:
                        {
                            return "simd16"; // TYP_SIMD16
                        }
                        case var_types.TYP_SIMD32:
                        {
                            return "simd32"; // TYP_SIMD32
                        }
                        case var_types.TYP_SIMD64:
                        {
                            return "simd64"; // TYP_SIMD64
                        }
                        case var_types.TYP_SIMD_ARM64:
                        {
                            return "simd"; // TYP_SIMD_ARM64
                        }
                        case var_types.TYP_MASK:
                        {
                            return "mask"; // TYP_MASK
                        }
                        case var_types.TYP_UNKNOWN:
                        {
                            return "unknown"; // TYP_UNKNOWN
                        }
                        default:
                        {
                            JitExceptionHelpers.ThrowIndexOutOfRangeException();
                            return default!;
                        }
                    }
                }

                return varType.ToString();
            }
        }
    }
}