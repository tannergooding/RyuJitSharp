// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoContextFlags;
using System;

namespace RyuJitSharp;

/// <summary>Bit-twiddling of contexts assumes word-alignment of method handles and type handles.</summary>
/// <remarks>If this ever changes, some other encoding will be needed.</remarks>
[Flags]
public enum CorInfoContextFlags
{
    /// <summary><see cref="CORINFO_CONTEXT_HANDLE" /> is really a <see cref="CORINFO_METHOD_HANDLE" />.</summary>
    CORINFO_CONTEXTFLAGS_METHOD = 0x00,

    /// <summary><see cref="CORINFO_CONTEXT_HANDLE" /> is really a <see cref="CORINFO_CLASS_HANDLE" />.</summary>
    CORINFO_CONTEXTFLAGS_CLASS = 0x01,

    CORINFO_CONTEXTFLAGS_MASK = 0x01
}
