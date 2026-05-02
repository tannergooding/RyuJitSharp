// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.var_types_classification;
using System;

namespace RyuJitSharp;

[Flags]
public enum var_types_classification : byte
{
    VTF_ANY = 0x0000,

    VTF_INT = 0x0001,

    // type is unsigned
    VTF_UNS = 0x0002,

    VTF_FLT = 0x0004,

    // type is GC ref
    VTF_GCR = 0x0008,

    // type is Byref
    VTF_BYR = 0x0010,

    // is machine sized
    VTF_I   = 0x0020,

    // is a struct type
    VTF_S   = 0x0040,

    // is a vector type
    VTF_VEC = 0x0080,
}
