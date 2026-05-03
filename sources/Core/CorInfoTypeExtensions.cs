// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public static class CorInfoTypeExtensions
{
    private static ReadOnlySpan<var_types> s_preciseVarTypes => [
        // see the definition of enum CorInfoType in file inc/corinfo.h
        TYP_UNDEF,  // CORINFO_TYPE_UNDEF           = 0x0,
        TYP_VOID,   // CORINFO_TYPE_VOID            = 0x1,
        TYP_UBYTE,  // CORINFO_TYPE_BOOL            = 0x2,
        TYP_USHORT, // CORINFO_TYPE_CHAR            = 0x3,
        TYP_BYTE,   // CORINFO_TYPE_BYTE            = 0x4,
        TYP_UBYTE,  // CORINFO_TYPE_UBYTE           = 0x5,
        TYP_SHORT,  // CORINFO_TYPE_SHORT           = 0x6,
        TYP_USHORT, // CORINFO_TYPE_USHORT          = 0x7,
        TYP_INT,    // CORINFO_TYPE_INT             = 0x8,
        TYP_UINT,   // CORINFO_TYPE_UINT            = 0x9,
        TYP_LONG,   // CORINFO_TYPE_LONG            = 0xa,
        TYP_ULONG,  // CORINFO_TYPE_ULONG           = 0xb,
        TYP_I_IMPL, // CORINFO_TYPE_NATIVEINT       = 0xc,
        TYP_U_IMPL, // CORINFO_TYPE_NATIVEUINT      = 0xd,
        TYP_FLOAT,  // CORINFO_TYPE_FLOAT           = 0xe,
        TYP_DOUBLE, // CORINFO_TYPE_DOUBLE          = 0xf,
        TYP_REF,    // CORINFO_TYPE_STRING          = 0x10,         // Not used, should remove
        TYP_U_IMPL, // CORINFO_TYPE_PTR             = 0x11,
        TYP_BYREF,  // CORINFO_TYPE_BYREF           = 0x12,
        TYP_STRUCT, // CORINFO_TYPE_VALUECLASS      = 0x13,
        TYP_REF,    // CORINFO_TYPE_CLASS           = 0x14,
        TYP_STRUCT, // CORINFO_TYPE_REFANY          = 0x15,

        // Generic type variables only appear when we're doing
        // verification of generic code, in which case we're running
        // in "import only" mode.  Annoyingly the "import only"
        // mode of the JIT actually does a fair bit of compilation,
        // so we have to trick the compiler into thinking it's compiling
        // a real instantiation.  We do that by just pretending we're
        // compiling the "object" instantiation of the code, i.e. by
        // turing all generic type variables refs, except for a few
        // choice places to do with verification, where we use
        // verification types and CLASS_HANDLEs to track the difference.

        TYP_REF, // CORINFO_TYPE_VAR             = 0x16,
    ];

    private static ReadOnlySpan<var_types> s_varTypes => [
        // see the definition of enum CorInfoType in file inc/corinfo.h
        TYP_UNDEF,  // CORINFO_TYPE_UNDEF           = 0x0,
        TYP_VOID,   // CORINFO_TYPE_VOID            = 0x1,
        TYP_UBYTE,  // CORINFO_TYPE_BOOL            = 0x2,
        TYP_USHORT, // CORINFO_TYPE_CHAR            = 0x3,
        TYP_BYTE,   // CORINFO_TYPE_BYTE            = 0x4,
        TYP_UBYTE,  // CORINFO_TYPE_UBYTE           = 0x5,
        TYP_SHORT,  // CORINFO_TYPE_SHORT           = 0x6,
        TYP_USHORT, // CORINFO_TYPE_USHORT          = 0x7,
        TYP_INT,    // CORINFO_TYPE_INT             = 0x8,
        TYP_INT,    // CORINFO_TYPE_UINT            = 0x9,
        TYP_LONG,   // CORINFO_TYPE_LONG            = 0xa,
        TYP_LONG,   // CORINFO_TYPE_ULONG           = 0xb,
        TYP_I_IMPL, // CORINFO_TYPE_NATIVEINT       = 0xc,
        TYP_I_IMPL, // CORINFO_TYPE_NATIVEUINT      = 0xd,
        TYP_FLOAT,  // CORINFO_TYPE_FLOAT           = 0xe,
        TYP_DOUBLE, // CORINFO_TYPE_DOUBLE          = 0xf,
        TYP_REF,    // CORINFO_TYPE_STRING          = 0x10,         // Not used, should remove
        TYP_I_IMPL, // CORINFO_TYPE_PTR             = 0x11,
        TYP_BYREF,  // CORINFO_TYPE_BYREF           = 0x12,
        TYP_STRUCT, // CORINFO_TYPE_VALUECLASS      = 0x13,
        TYP_REF,    // CORINFO_TYPE_CLASS           = 0x14,
        TYP_STRUCT, // CORINFO_TYPE_REFANY          = 0x15,

        // Generic type variables only appear when we're doing
        // verification of generic code, in which case we're running
        // in "import only" mode.  Annoyingly the "import only"
        // mode of the JIT actually does a fair bit of compilation,
        // so we have to trick the compiler into thinking it's compiling
        // a real instantiation.  We do that by just pretending we're
        // compiling the "object" instantiation of the code, i.e. by
        // turing all generic type variables refs, except for a few
        // choice places to do with verification, where we use
        // verification types and CLASS_HANDLEs to track the difference.

        TYP_REF, // CORINFO_TYPE_VAR             = 0x16,
    ];

    extension(CorInfoType type)
    {
        public var_types PreciseVarType
        {
            get
            {
                assert(s_preciseVarTypes.Length == (int)(CORINFO_TYPE_COUNT));

                // spot check to make certain enumerations have not changed
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_CLASS)] == TYP_REF);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_BYREF)] == TYP_BYREF);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_PTR)] == TYP_U_IMPL);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_INT)] == TYP_INT);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_UINT)] == TYP_UINT);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_DOUBLE)] == TYP_DOUBLE);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_VOID)] == TYP_VOID);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_VALUECLASS)] == TYP_STRUCT);
                assert(s_preciseVarTypes[(int)(CORINFO_TYPE_REFANY)] == TYP_STRUCT);
                assert(s_preciseVarTypes[(int)(type)] != TYP_UNDEF);

                assert(type < CORINFO_TYPE_COUNT);
                return Unsafe.Add(ref MemoryMarshal.GetReference(s_preciseVarTypes), (int)(type));
            }
        }

        public var_types VarType
        {
            get
            {
                assert(s_varTypes.Length == (int)(CORINFO_TYPE_COUNT));
                
                // spot check to make certain enumerations have not changed
                assert(s_varTypes[(int)(CORINFO_TYPE_CLASS)] == TYP_REF);
                assert(s_varTypes[(int)(CORINFO_TYPE_BYREF)] == TYP_BYREF);
                assert(s_varTypes[(int)(CORINFO_TYPE_PTR)] == TYP_I_IMPL);
                assert(s_varTypes[(int)(CORINFO_TYPE_INT)] == TYP_INT);
                assert(s_varTypes[(int)(CORINFO_TYPE_UINT)] == TYP_INT);
                assert(s_varTypes[(int)(CORINFO_TYPE_DOUBLE)] == TYP_DOUBLE);
                assert(s_varTypes[(int)(CORINFO_TYPE_VOID)] == TYP_VOID);
                assert(s_varTypes[(int)(CORINFO_TYPE_VALUECLASS)] == TYP_STRUCT);
                assert(s_varTypes[(int)(CORINFO_TYPE_REFANY)] == TYP_STRUCT);
                assert(s_varTypes[(int)(type)] != TYP_UNDEF);

                assert(type < CORINFO_TYPE_COUNT);
                return Unsafe.Add(ref MemoryMarshal.GetReference(s_varTypes), (int)(type));
            }
        }
    }
}
