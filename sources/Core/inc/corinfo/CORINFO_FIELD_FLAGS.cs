// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_FIELD_FLAGS;
using System;

namespace RyuJitSharp;

/// <summary>Set of flags returned in <see cref="CORINFO_FIELD_INFO.fieldFlags" />.</summary>
[Flags]
public enum CORINFO_FIELD_FLAGS
{
    CORINFO_FLG_FIELD_STATIC = 0x00000001,

    /// <summary>RVA field</summary>
    CORINFO_FLG_FIELD_UNMANAGED = 0x00000002,

    CORINFO_FLG_FIELD_FINAL = 0x00000004,

    /// <summary>This static field is in the GC heap as a boxed object.</summary>
    CORINFO_FLG_FIELD_STATIC_IN_HEAP = 0x00000008,

    /// <summary>InitClass has to be called before accessing the field.</summary>
    CORINFO_FLG_FIELD_INITCLASS = 0x00000020,
}
