// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_ACCESS_FLAGS;
using System;

namespace RyuJitSharp;

[Flags]
public enum CORINFO_ACCESS_FLAGS
{
    /// <summary>Normal access.</summary>
    CORINFO_ACCESS_ANY = 0x0000,

    /// <summary>Accessed via the this reference.</summary>
    CORINFO_ACCESS_THIS = 0x0001,

    // UNUSED = 0x0002,

    /// <summary>Instance is guaranteed non-null.</summary>
    CORINFO_ACCESS_NONNULL = 0x0004,

    /// <summary>Accessed via ldftn.</summary>
    CORINFO_ACCESS_LDFTN = 0x0010,

    //
    // Field access flags
    //

    /// <summary>Field get (ldfld).</summary>
    CORINFO_ACCESS_GET = 0x0100,

    /// <summary>Field set (stfld).</summary>
    CORINFO_ACCESS_SET = 0x0200,

    /// <summary>Field address (ldflda).</summary>
    CORINFO_ACCESS_ADDRESS = 0x0400,

    /// <summary>Field use for InitializeArray.</summary>
    CORINFO_ACCESS_INIT_ARRAY = 0x0800,

    // UNUSED = 0x4000,

    /// <summary>Return fieldFlags and fieldAccessor only. Used by JIT64 during inlining.</summary>
    CORINFO_ACCESS_INLINECHECK = 0x8000,
}
