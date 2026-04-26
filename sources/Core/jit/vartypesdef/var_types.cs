// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum var_types : byte
{
    TYP_UNKNOWN,
    TYP_MASK,
    TYP_SIMD,
    TYP_SIMD64,
    TYP_SIMD32,
    TYP_SIMD16,
    TYP_SIMD12,
    TYP_SIMD8,
    TYP_STRUCT,
    TYP_BYREF,
    TYP_REF,
    TYP_DOUBLE,
    TYP_FLOAT,
    TYP_ULONG,
    TYP_LONG,
    TYP_UINT,
    TYP_INT,
    TYP_USHORT,
    TYP_SHORT,
    TYP_UBYTE,
    TYP_BYTE,
    TYP_VOID,
    TYP_UNDEF,
    TYP_COUNT,
}
