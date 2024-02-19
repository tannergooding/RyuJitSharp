// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp;

/// <summary>Returned in <c>getSig</c>, <c>getType</c>, <c>getArgType</c> methods.</summary>
public enum CorInfoType
{
    CORINFO_TYPE_UNDEF = 0x0,

    CORINFO_TYPE_VOID = 0x1,

    CORINFO_TYPE_BOOL = 0x2,

    CORINFO_TYPE_CHAR = 0x3,

    CORINFO_TYPE_BYTE = 0x4,

    CORINFO_TYPE_UBYTE = 0x5,

    CORINFO_TYPE_SHORT = 0x6,

    CORINFO_TYPE_USHORT = 0x7,

    CORINFO_TYPE_INT = 0x8,

    CORINFO_TYPE_UINT = 0x9,

    CORINFO_TYPE_LONG = 0xA,

    CORINFO_TYPE_ULONG = 0xB,

    CORINFO_TYPE_NATIVEINT = 0xC,

    CORINFO_TYPE_NATIVEUINT = 0xD,

    CORINFO_TYPE_FLOAT = 0xE,

    CORINFO_TYPE_DOUBLE = 0xF,

    /// <summary>Not used, should remove.</summary>
    CORINFO_TYPE_STRING = 0x10,

    CORINFO_TYPE_PTR = 0x11,

    CORINFO_TYPE_BYREF = 0x12,

    CORINFO_TYPE_VALUECLASS = 0x13,

    CORINFO_TYPE_CLASS = 0x14,

    CORINFO_TYPE_REFANY = 0x15,

    /// <summary>For a generic type variable.</summary>
    /// <remarks>Generic type variables only appear when the JIT is doing verification (not NOT compilation) of generic code for the EE, in which case we're running the JIT in "import only" mode.</remarks>
    CORINFO_TYPE_VAR = 0x16,

    /// <summary>Number of jit types.</summary>
    CORINFO_TYPE_COUNT,
}
