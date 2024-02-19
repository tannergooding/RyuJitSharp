// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_GET_TAILCALL_HELPERS_FLAGS;
using System;

namespace RyuJitSharp;

[Flags]
public enum CORINFO_GET_TAILCALL_HELPERS_FLAGS
{
    /// <summary>The callsite is a callvirt instruction.</summary>
    CORINFO_TAILCALL_IS_CALLVIRT = 0x00000001,

    CORINFO_TAILCALL_THIS_ARG_IS_BYREF = 0x00000002,
}
