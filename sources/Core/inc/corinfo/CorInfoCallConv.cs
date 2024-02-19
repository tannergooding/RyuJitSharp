// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoCallConv;

namespace RyuJitSharp;

/// <summary>Returned in <c>getSig</c>.</summary>
/// <remarks>These correspond to CorCallingConvention.</remarks>
public enum CorInfoCallConv
{
    CORINFO_CALLCONV_DEFAULT = 0x0,

    // Instead of using the below values, use the CorInfoCallConvExtension enum for unmanaged calling conventions.
    // CORINFO_CALLCONV_C          = 0x1,
    // CORINFO_CALLCONV_STDCALL    = 0x2,
    // CORINFO_CALLCONV_THISCALL   = 0x3,
    // CORINFO_CALLCONV_FASTCALL   = 0x4,

    CORINFO_CALLCONV_VARARG = 0x5,

    CORINFO_CALLCONV_FIELD = 0x6,

    CORINFO_CALLCONV_LOCAL_SIG = 0x7,

    CORINFO_CALLCONV_PROPERTY = 0x8,

    CORINFO_CALLCONV_UNMANAGED = 0x9,

    // used ONLY for IL stub PInvoke vararg calls
    CORINFO_CALLCONV_NATIVEVARARG = 0xB,

    // Calling convention is bottom 4 bits
    CORINFO_CALLCONV_MASK = 0x0F,

    CORINFO_CALLCONV_GENERIC = 0x10,

    CORINFO_CALLCONV_HASTHIS = 0x20,

    CORINFO_CALLCONV_EXPLICITTHIS = 0x40,

    // Passed last. Same as CORINFO_GENERICS_CTXT_FROM_PARAMTYPEARG
    CORINFO_CALLCONV_PARAMTYPE = 0x80,
}
