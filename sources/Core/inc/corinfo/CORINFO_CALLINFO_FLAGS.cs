// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_CALLINFO_FLAGS;
using System;

namespace RyuJitSharp;

[Flags]
public enum CORINFO_CALLINFO_FLAGS
{
    CORINFO_CALLINFO_NONE = 0x0000,

    /// <summary>Can the compiler generate code to pass an instantiation parameters? Simple compilers should not use this flag.</summary>
    CORINFO_CALLINFO_ALLOWINSTPARAM = 0x0001,

    /// <summary>Is it a virtual call?</summary>
    CORINFO_CALLINFO_CALLVIRT = 0x0002,

    // UNUSED = 0x0004,
    // UNUSED = 0x0008,

    /// <summary>Perform security checks.</summary>
    CORINFO_CALLINFO_SECURITYCHECKS = 0x0010,

    /// <summary>Resolving target of LDFTN.</summary>
    CORINFO_CALLINFO_LDFTN = 0x0020,

    // UNUSED = 0x0040,
}
