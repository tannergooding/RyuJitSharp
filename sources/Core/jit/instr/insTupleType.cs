// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
global using static RyuJitSharp.insTupleType;

using System;

namespace RyuJitSharp;

[Flags]
public enum insTupleType : ushort
{
    INS_TT_NONE = 0x0000,
    INS_TT_FULL = 0x0001,
    INS_TT_HALF = 0x0002,
    INS_TT_IS_BROADCAST = INS_TT_FULL | INS_TT_HALF,
    INS_TT_FULL_MEM = 0x0010,
    INS_TT_TUPLE1_SCALAR = 0x0020,
    INS_TT_TUPLE1_FIXED = 0x0040,
    INS_TT_TUPLE2 = 0x0080,
    INS_TT_TUPLE4 = 0x0100,
    INS_TT_TUPLE8 = 0x0200,
    INS_TT_HALF_MEM = 0x0400,
    INS_TT_QUARTER_MEM = 0x0800,
    INS_TT_EIGHTH_MEM = 0x1000,
    INS_TT_MEM128 = 0x2000,
    INS_TT_MOVDDUP = 0x4000,
    INS_TT_IS_NON_BROADCAST = unchecked((ushort)~INS_TT_IS_BROADCAST),
}
#endif
