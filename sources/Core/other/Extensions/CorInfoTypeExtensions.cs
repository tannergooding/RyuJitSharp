// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static class CorInfoTypeExtensions
{
    private static ReadOnlySpan<var_types> s_preciseVarTypes => [
        TYP_UNDEF,      // CORINFO_TYPE_UNDEF     
        TYP_VOID,       // CORINFO_TYPE_VOID      
        TYP_UBYTE,      // CORINFO_TYPE_BOOL      
        TYP_USHORT,     // CORINFO_TYPE_CHAR      
        TYP_BYTE,       // CORINFO_TYPE_BYTE      
        TYP_UBYTE,      // CORINFO_TYPE_UBYTE     
        TYP_SHORT,      // CORINFO_TYPE_SHORT     
        TYP_USHORT,     // CORINFO_TYPE_USHORT    
        TYP_INT,        // CORINFO_TYPE_INT       
        TYP_UINT,       // CORINFO_TYPE_UINT      
        TYP_LONG,       // CORINFO_TYPE_LONG      
        TYP_ULONG,      // CORINFO_TYPE_ULONG     
        TYP_I_IMPL,     // CORINFO_TYPE_NATIVEINT 
        TYP_U_IMPL,     // CORINFO_TYPE_NATIVEUINT
        TYP_FLOAT,      // CORINFO_TYPE_FLOAT     
        TYP_DOUBLE,     // CORINFO_TYPE_DOUBLE    
        TYP_U_IMPL,     // CORINFO_TYPE_PTR       
        TYP_BYREF,      // CORINFO_TYPE_BYREF     
        TYP_STRUCT,     // CORINFO_TYPE_VALUECLASS
        TYP_REF,        // CORINFO_TYPE_CLASS     
    ];

    private static ReadOnlySpan<var_types> s_varTypes => [
        // see the definition of enum CorInfoType in file inc/corinfo.h
        TYP_UNDEF,      // CORINFO_TYPE_UNDEF     
        TYP_VOID,       // CORINFO_TYPE_VOID      
        TYP_UBYTE,      // CORINFO_TYPE_BOOL      
        TYP_USHORT,     // CORINFO_TYPE_CHAR      
        TYP_BYTE,       // CORINFO_TYPE_BYTE      
        TYP_UBYTE,      // CORINFO_TYPE_UBYTE     
        TYP_SHORT,      // CORINFO_TYPE_SHORT     
        TYP_USHORT,     // CORINFO_TYPE_USHORT    
        TYP_INT,        // CORINFO_TYPE_INT       
        TYP_INT,        // CORINFO_TYPE_UINT      
        TYP_LONG,       // CORINFO_TYPE_LONG      
        TYP_LONG,       // CORINFO_TYPE_ULONG     
        TYP_I_IMPL,     // CORINFO_TYPE_NATIVEINT 
        TYP_I_IMPL,     // CORINFO_TYPE_NATIVEUINT
        TYP_FLOAT,      // CORINFO_TYPE_FLOAT     
        TYP_DOUBLE,     // CORINFO_TYPE_DOUBLE    
        TYP_I_IMPL,     // CORINFO_TYPE_PTR       
        TYP_BYREF,      // CORINFO_TYPE_BYREF     
        TYP_STRUCT,     // CORINFO_TYPE_VALUECLASS
        TYP_REF,        // CORINFO_TYPE_CLASS
    ];

    extension(CorInfoType type)
    {
        public var_types PreciseVarType
        {
            get
            {
                assert(s_preciseVarTypes.Length == (int)(CORINFO_TYPE_COUNT));
                return s_preciseVarTypes[(int)(type)];
            }
        }

        public var_types VarType
        {
            get
            {
                assert(s_varTypes.Length == (int)(CORINFO_TYPE_COUNT));
                return s_varTypes[(int)(type)];
            }
        }
    }
}
